using System.Net.Http;
using System.IO;
using System.Text.Json;
using WindowsAiDog.Windows.Storage;
namespace WindowsAiDog.Windows.Chat;
public sealed class OpenAiCompatibleProvider:IChatProvider {
 private readonly ProviderDescriptor descriptor;
 public string Id=>descriptor.Id;public string DisplayName=>descriptor.DisplayName;public BillingMode Billing=>descriptor.Billing;
 public IChatAuth Auth {get;}public IChatClient Client {get;}
 public OpenAiCompatibleProvider(HttpClient http,ProviderConfiguration config,string folder) {
  descriptor=ProviderRegistry.Get(config.Id);if(!ProviderRegistry.EndpointAllowed(config.Id,config.Endpoint))throw new ArgumentException(descriptor.FixedEndpoint?"이 서비스의 주소는 고정입니다.":"주소가 유효하지 않습니다.");
  Auth=new EndpointAuthentication(http,config,folder);Client=config.Id=="openai-api"?new ResponsesChatClient(http):new OpenAiCompatibleChatClient(http,config.Endpoint,descriptor);
 }
}
public sealed class EndpointAuthentication(HttpClient http,ProviderConfiguration config,string folder):IChatAuth {
 private readonly EndpointKeyStore store=new(folder,config.Endpoint);
 private string Key {get{try{return store.Read();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){return "";}}}
 private readonly ProviderDescriptor descriptor=ProviderRegistry.Get(config.Id);
 public bool Connected=>config.Id=="local"||!string.IsNullOrWhiteSpace(Key);
 public Task ConnectAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();if(!Connected)throw new InvalidOperationException("연결 설정에서 API 키를 저장하세요.");return Task.CompletedTask;}
 public Task<string> AccessTokenAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(Key);}
 public async Task<IReadOnlyList<ModelChoice>> ModelsAsync(CancellationToken ct) {
  using var request=new HttpRequestMessage(HttpMethod.Get,ProviderEndpoint.Route(config.Endpoint,"models"));ProviderRegistry.Authorize(request,descriptor,Key);
  using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();
  var body=await response.Content.ReadAsStringAsync(ct);if(body.Length>1024*1024)throw new InvalidDataException("모델 목록이 너무 큽니다.");
  using var doc=JsonDocument.Parse(body);var models=doc.RootElement.GetProperty("data").EnumerateArray().Take(100).Select(m=>m.GetProperty("id").GetString()!).Where(m=>!string.IsNullOrWhiteSpace(m)&&m.Length<=200).Select(ModelId.Normalize).Distinct().Select(m=>new ModelChoice(m,m)).ToList();
  if(config.Model!=""&&!models.Any(m=>m.Slug==config.Model))models.Insert(0,new(config.Model,config.Model+" · 직접 지정"));
  return models;
 }
 public Task<bool> DisconnectAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();store.Clear();return Task.FromResult(true);}
}
public static class ModelId {
 // Gemini lists models as "models/<name>"; requests use the bare name.
 public static string Normalize(string id)=>id.StartsWith("models/",StringComparison.Ordinal)?id["models/".Length..]:id;
}
