// C# adaptation of Hermes provider descriptor/catalog ideas. See assets/licenses/hermes-MIT.txt.
using System.Net;
namespace WindowsAiDog.Windows.Chat;
public enum AuthenticationKind { SubscriptionOAuth,ApiKey,LocalEndpoint }
public sealed record ProviderDescriptor(string Id,string DisplayName,AuthenticationKind Authentication,BillingMode Billing,string DefaultEndpoint) {
 public override string ToString()=>DisplayName;
}
public static class ProviderRegistry {
 public static IReadOnlyList<ProviderDescriptor> All {get;}=new[]{
  new ProviderDescriptor("chatgpt","ChatGPT 구독",AuthenticationKind.SubscriptionOAuth,BillingMode.SubscriptionSharing,"https://api.openai.com/v1"),
  new ProviderDescriptor("openai-api","OpenAI API · 별도 종량 과금",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.openai.com/v1"),
  new ProviderDescriptor("compatible-api","호환 API · 공급자 과금",AuthenticationKind.ApiKey,BillingMode.ApiUsage,"https://api.openai.com/v1"),
  new ProviderDescriptor("local","로컬 OpenAI 호환 · 직접 실행한 서버",AuthenticationKind.LocalEndpoint,BillingMode.Local,"http://127.0.0.1:1234/v1")};
 public static ProviderDescriptor Get(string id)=>All.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("알 수 없는 제공자");
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
