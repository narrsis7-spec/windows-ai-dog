using System.IO;
using System.Text.Json;
using WindowsAiDog.Windows.Chat;
namespace WindowsAiDog.Windows.Storage;
public sealed record ProviderConfiguration(string Id,string Endpoint,string Model="");
public sealed class ProviderSettingsStore(string root) {
 public string DirectoryFor(string id){ProviderRegistry.Get(id);return Path.Combine(root,"providers",id);}
 public ProviderConfiguration Load(string id) {
  var d=ProviderRegistry.Get(id);
  try{var p=Path.Combine(DirectoryFor(id),"config.json");if(new FileInfo(p).Length>8192)return new(id,d.DefaultEndpoint);var c=JsonSerializer.Deserialize<ProviderConfiguration>(File.ReadAllText(p));if(c is not null&&c.Id==id&&c.Model.Length<=200&&(id=="chatgpt"||ProviderRegistry.EndpointAllowed(id,c.Endpoint)))return c;}
  catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException or NullReferenceException or UriFormatException){}
  return new(id,d.DefaultEndpoint);
 }
 public string Selected {
  get{try{return ProviderRegistry.Get(File.ReadAllText(Path.Combine(root,"selected-provider.txt")).Trim()).Id;}catch(Exception e) when(e is IOException or ArgumentException or UnauthorizedAccessException){return "chatgpt";}}
 }
 public void SaveSelection(string id){ProviderRegistry.Get(id);Directory.CreateDirectory(root);Atomic(Path.Combine(root,"selected-provider.txt"),id);}
 public void Save(ProviderConfiguration c){if(c.Id=="chatgpt"||c.Model.Length>200)throw new ArgumentException("주소와 모델을 확인하세요.");if(!ProviderRegistry.EndpointAllowed(c.Id,c.Endpoint))throw new ArgumentException(ProviderRegistry.Get(c.Id).FixedEndpoint?"이 서비스의 주소는 고정입니다. 다른 서버는 기타 OpenAI 호환 API를 선택하세요.":"주소를 확인하세요.");var folder=DirectoryFor(c.Id);Directory.CreateDirectory(folder);Atomic(Path.Combine(folder,"config.json"),JsonSerializer.Serialize(c));}
 private static void Atomic(string p,string text){File.WriteAllText(p+".tmp",text);File.Move(p+".tmp",p,true);}
}
