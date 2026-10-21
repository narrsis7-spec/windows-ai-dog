// C# adaptation of Hermes provider descriptor/catalog ideas. See assets/licenses/hermes-MIT.txt.
using System.Net;
using System.Net.Http;
namespace WindowsAiDog.Windows.Chat;
public enum AuthenticationKind { SubscriptionOAuth,ApiKey,LocalEndpoint }
// FixedEndpoint: preset services whose address cannot be edited (the key stays bound to it).
// KeyHeader: extra header that also carries the key (Anthropic uses x-api-key); Headers: fixed extras.
public sealed record ProviderDescriptor(string Id,string DisplayName,AuthenticationKind Authentication,BillingMode Billing,string DefaultEndpoint,bool FixedEndpoint=false,string? KeyPage=null,string? KeyHeader=null,IReadOnlyDictionary<string,string>? Headers=null) {
 public override string ToString()=>DisplayName;
}
public static class ProviderRegistry {
 public static IReadOnlyList<ProviderDescriptor> All {get;}=new[]{
  new ProviderDescriptor("chatgpt","ChatGPT 구독",AuthenticationKind.SubscriptionOAuth,BillingMode.SubscriptionSharing,"https://api.openai.com/v1"),
  new ProviderDescriptor("openai-api","OpenAI API · 별도 종량 과금",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.openai.com/v1",true,"https://platform.openai.com/api-keys"),
  // API-key services reached through their OpenAI-compatible endpoints.
  new ProviderDescriptor("anthropic","Claude API (Anthropic)",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.anthropic.com/v1",true,"https://console.anthropic.com/settings/keys","x-api-key",new Dictionary<string,string>{["anthropic-version"]="2023-06-01"}),
  new ProviderDescriptor("gemini","Gemini API (Google)",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://generativelanguage.googleapis.com/v1beta/openai",true,"https://aistudio.google.com/apikey"),
  new ProviderDescriptor("openrouter","OpenRouter · 여러 AI를 한 키로",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://openrouter.ai/api/v1",true,"https://openrouter.ai/keys"),
  new ProviderDescriptor("groq","Groq",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.groq.com/openai/v1",true,"https://console.groq.com/keys"),
  new ProviderDescriptor("deepseek","DeepSeek",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.deepseek.com/v1",true,"https://platform.deepseek.com/api_keys"),
  new ProviderDescriptor("mistral","Mistral",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.mistral.ai/v1",true,"https://console.mistral.ai/api-keys"),
  new ProviderDescriptor("xai","xAI Grok",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.x.ai/v1",true,"https://console.x.ai"),
  new ProviderDescriptor("compatible-api","기타 OpenAI 호환 API · 주소 직접 입력",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.openai.com/v1"),
  new ProviderDescriptor("local","로컬 AI · 내 PC에서 실행한 서버",AuthenticationKind.LocalEndpoint,BillingMode.Local,"http://127.0.0.1:1234/v1")};
 public static ProviderDescriptor Get(string id)=>All.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("알 수 없는 제공자");
 // Preset services accept only their own address; custom and local entries use the general rules.
 public static bool EndpointAllowed(string id,string endpoint){var d=Get(id);if(!ProviderEndpoint.IsValid(endpoint,d.Authentication==AuthenticationKind.LocalEndpoint))return false;return !d.FixedEndpoint||ProviderEndpoint.Canonical(endpoint)==ProviderEndpoint.Canonical(d.DefaultEndpoint);}
 // Adds the provider's key header(s) to a request; Bearer is always sent when there is a key.
 public static void Authorize(HttpRequestMessage request,ProviderDescriptor? d,string key){
  if(key!="")request.Headers.Authorization=new("Bearer",key);if(d is null)return;
  if(d.KeyHeader is string header&&key!="")request.Headers.TryAddWithoutValidation(header,key);
  if(d.Headers is not null)foreach(var (name,value) in d.Headers)request.Headers.TryAddWithoutValidation(name,value);
 }
}
public static class ProviderEndpoint {
 public static bool IsValid(string value,bool local) {
  if(string.IsNullOrWhiteSpace(value)||value.Length>2048||!Uri.TryCreate(value,UriKind.Absolute,out var u)||u.UserInfo!=""||u.Query!=""||u.Fragment!="")return false;
  bool loop=u.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(u.Host,out var ip)&&IPAddress.IsLoopback(ip);
  return local?loop&&u.Scheme is "http" or "https":!loop&&u.Scheme=="https";
 }
 public static string Canonical(string endpoint)=>new Uri(endpoint).AbsoluteUri.TrimEnd('/');
 public static Uri Route(string endpoint,string suffix)=>new(endpoint.TrimEnd('/')+"/"+suffix);
}
