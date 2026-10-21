using System.Text.Json;
using WindowsAiDog.Windows.Chat;
namespace WindowsAiDog.Windows.Storage;
public sealed class EndpointKeyStore(string folder,string endpoint) {
 private record BoundKey(string Endpoint,string Key);
 private readonly ProtectedCredentialStore vault=new(folder);
 public string Read(){try{var value=vault.Load();if(value is null||value.Length>16384)return "";var bound=JsonSerializer.Deserialize<BoundKey>(value);return bound is not null&&bound.Key.Length<=8192&&bound.Endpoint==ProviderEndpoint.Canonical(endpoint)?bound.Key:"";}catch(Exception e) when(e is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException or NullReferenceException){return "";}}
 public void Save(string key){if(string.IsNullOrWhiteSpace(key)||key.Length>8192||key.Any(char.IsControl))throw new ArgumentException("키 형식을 확인하세요.");vault.Save(JsonSerializer.Serialize(new BoundKey(ProviderEndpoint.Canonical(endpoint),key)));}
 public void Clear()=>vault.Clear();
}
