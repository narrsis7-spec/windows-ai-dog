using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.ComponentModel;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Storage;
public sealed class SettingsStore(string directory) {
 private static readonly JsonSerializerOptions Options=new(){WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
 public AppSettings Load() {
  foreach(var name in new[]{"settings.json","settings.last-good.json"}) {
   try{var s=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(directory,name)),Options);if(s is not null&&SettingsValidator.IsValid(s))return s;}
   catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){}
  }
  return new();
 }
 public void Save(AppSettings settings) {
  if(!SettingsValidator.IsValid(settings))throw new ArgumentException("잘못된 설정");
  Directory.CreateDirectory(directory);
  var path=Path.Combine(directory,"settings.json");var tmp=Path.Combine(directory,"settings.tmp");
  File.WriteAllText(tmp,JsonSerializer.Serialize(settings,Options));
  bool validPrimary=false;
  try{var previous=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path),Options);validPrimary=previous is not null&&SettingsValidator.IsValid(previous);}
  catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){}
  if(validPrimary)File.Replace(tmp,path,Path.Combine(directory,"settings.last-good.json"));
  else File.Move(tmp,path,true);
 }
}
public sealed class ProtectedCredentialStore(string directory) {
 private string FilePath=>Path.Combine(directory,"credentials.bin");
 public void Save(string value) {
  Directory.CreateDirectory(directory);
  byte[] plain=System.Text.Encoding.UTF8.GetBytes(value);
  try{var encrypted=Protect(plain,false);var tmp=FilePath+".tmp";File.WriteAllBytes(tmp,encrypted);File.Move(tmp,FilePath,true);}
  finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);}
 }
 public string? Load() {
  if(!File.Exists(FilePath))return null;
  var plain=Protect(File.ReadAllBytes(FilePath),true);
  try{return System.Text.Encoding.UTF8.GetString(plain);}finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);}
 }
 public void Clear() {if(File.Exists(FilePath))File.Delete(FilePath);}
 [StructLayout(LayoutKind.Sequential)] private struct Blob {public int Length;public IntPtr Data;}
 [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
 [DllImport("crypt32.dll",SetLastError=true)]private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
 [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr pointer);
 private static byte[] Protect(byte[] bytes,bool decrypt) {
  var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};
  try {
   Marshal.Copy(bytes,0,input.Data,bytes.Length);
   bool ok=decrypt?CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out var output)
    :CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
   if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());
   try{var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;}
   finally{Marshal.Copy(new byte[output.Length],0,output.Data,output.Length);LocalFree(output.Data);}
  } finally{Marshal.Copy(new byte[input.Length],0,input.Data,input.Length);Marshal.FreeHGlobal(input.Data);}
 }
}
