using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsAiDog.Core;
using WindowsAiDog.Windows.Storage;
namespace WindowsAiDog.Windows.Chat;
public record Credentials(string ClientId,string Subject,string Label,string AccessToken,string RefreshToken,string IdToken,string Scope,DateTimeOffset ExpiresAt);
public record ModelChoice(string Slug,string DisplayName) { public override string ToString()=>DisplayName; }
public sealed class ChatGptAuthService:IChatAuth {
 private readonly HttpClient http;
 private readonly ProtectedCredentialStore store;
 private readonly ProtectedCredentialStore registrationStore;
 private record Registration(string ClientId,string? Subject=null,string? IdToken=null);
 private Registration? registration;
 public string? RegisteredClientId=>registration?.ClientId;
 private readonly string directory;
 private readonly Action<string> openBrowser;
 private readonly SemaphoreSlim refreshLock=new(1,1);
 private Credentials? current;
 public bool Connected=>current is not null;
 public string AccountLabel=>current?.Label??"연결되지 않음";
 public ChatGptAuthService(HttpClient http,string directory,Action<string>? openBrowser=null) {
  this.http=http;this.directory=directory;store=new(directory);registrationStore=new(Path.Combine(directory,"registration"));this.openBrowser=openBrowser??(url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true}));
  try{var persisted=registrationStore.Load();if(persisted is not null){var value=JsonSerializer.Deserialize<Registration>(persisted);if(value?.ClientId?.StartsWith("oaiapp_",StringComparison.Ordinal)==true)registration=value;}var saved=store.Load();if(saved is not null)current=JsonSerializer.Deserialize<Credentials>(saved);}catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception){current=null;}
 }
 public async Task ConnectAsync(CancellationToken ct) {
  ct.ThrowIfCancellationRequested();
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));
  string hostPath=Path.Combine(directory,"host-id.txt");Directory.CreateDirectory(directory);
  if(!File.Exists(hostPath))File.WriteAllText(hostPath,"urn:uuid:"+Guid.NewGuid());
  string host=File.ReadAllText(hostPath).Trim();
  string state=Random(),nonce=Random(),verifier=Random();
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start(1);
  try {
   int port=((IPEndPoint)listener.LocalEndpoint).Port;
   string redirect=$"http://127.0.0.1:{port}/auth/callback";
   string client=current?.ClientId??registration?.ClientId??"dynamic_agent_client";
   var query=new Dictionary<string,string> {
    ["client_id"]=client,["ext_agent_host_id"]=host,["response_type"]="code",["redirect_uri"]=redirect,
    ["scope"]="openid profile email offline_access resource.invoke chatgpt.tokens.use.direct",["resource"]="https://api.openai.com/v1",
    ["state"]=state,["nonce"]=nonce,["code_challenge_method"]="S256",["code_challenge"]=OAuthSecurity.Challenge(verifier)
   };
   if(client=="dynamic_agent_client")query["agent_name_hint"]="Windows AI Dog";
   else if((current?.IdToken??registration?.IdToken) is string hint)query["id_token_hint"]=hint;
   string url="https://auth.openai.com/api/accounts/authorize?"+string.Join("&",query.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));
   openBrowser(url);
   using var socket=await listener.AcceptTcpClientAsync(timeout.Token);
   using var network=socket.GetStream();using var reader=new StreamReader(network,Encoding.ASCII,false,1024,true);
   string line=await ReadFirstLine(reader,timeout.Token);
   var parts=line.Split(' ');
   if(parts.Length!=3||parts[0]!="GET")throw new InvalidDataException("invalid_callback");
   var callback=new Uri("http://127.0.0.1:"+port+parts[1]);
   if(callback.AbsolutePath!="/auth/callback"||callback.Host!="127.0.0.1")throw new InvalidDataException("invalid_callback");
   var values=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var item in callback.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)) {
    var pair=item.Split('=',2);var key=Uri.UnescapeDataString(pair[0]);
    if(!values.TryAdd(key,pair.Length==2?Uri.UnescapeDataString(pair[1].Replace('+',' ')):""))throw new InvalidDataException("duplicate_callback_parameter");
   }
   string? callbackClient=values.GetValueOrDefault("client_id")??(client=="dynamic_agent_client"?null:client);
   bool valid=OAuthSecurity.ValidateCallback(state,values.GetValueOrDefault("state"),callbackClient,values.GetValueOrDefault("error"));
   var browserText=Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nConnection: close\r\n\r\n앱으로 돌아가 연결 결과를 확인하세요.");
   await network.WriteAsync(browserText,timeout.Token);
   if(!valid||(client!="dynamic_agent_client"&&callbackClient!=client)||!values.TryGetValue("code",out var code))throw new InvalidDataException("authorization_cancelled_or_invalid");
   timeout.Token.ThrowIfCancellationRequested();
   registration??=new(callbackClient!);registrationStore.Save(JsonSerializer.Serialize(registration));
   using var response=await http.PostAsync("https://auth.openai.com/api/accounts/oauth/token",new FormUrlEncodedContent(new Dictionary<string,string>{
    ["grant_type"]="authorization_code",["client_id"]=callbackClient!,["code"]=code,["code_verifier"]=verifier,["redirect_uri"]=redirect,["resource"]="https://api.openai.com/v1"
   }),timeout.Token);
   response.EnsureSuccessStatusCode();using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
   var r=payload.RootElement;string id=r.GetProperty("id_token").GetString()!;
   string jwks=await http.GetStringAsync("https://auth.openai.com/.well-known/jwks.json",timeout.Token);
   string subject=IdTokenValidator.Validate(id,jwks,callbackClient!,nonce,DateTimeOffset.UtcNow);
   if((current?.Subject??registration?.Subject) is string selectedSubject&&subject!=selectedSubject)throw new InvalidDataException("account_mismatch");
   string scope=r.GetProperty("scope").GetString()??"";
   if(!scope.Split(' ').Contains("chatgpt.tokens.use.direct"))throw new InvalidDataException("plan_usage_not_granted");
   if(r.GetProperty("token_type").GetString()!="Bearer")throw new InvalidDataException("invalid_token_type");
   var validated=new Credentials(callbackClient!,subject,"연결된 ChatGPT 계정",r.GetProperty("access_token").GetString()!,r.GetProperty("refresh_token").GetString()!,id,scope,DateTimeOffset.UtcNow.AddSeconds(r.GetProperty("expires_in").GetInt32()));
   timeout.Token.ThrowIfCancellationRequested();store.Save(JsonSerializer.Serialize(validated));registration=new(callbackClient!,subject,id);registrationStore.Save(JsonSerializer.Serialize(registration));current=validated;
  } finally{listener.Stop();}
 }
 public async Task<string> AccessTokenAsync(CancellationToken ct) {
  await refreshLock.WaitAsync(ct);
  try {
   var c=current??throw new InvalidOperationException("먼저 ChatGPT를 연결하세요.");
   if(c.ExpiresAt>DateTimeOffset.UtcNow.AddMinutes(1))return c.AccessToken;
   using var response=await http.PostAsync("https://auth.openai.com/api/accounts/oauth/token",new FormUrlEncodedContent(new Dictionary<string,string>{
    ["grant_type"]="refresh_token",["client_id"]=c.ClientId,["refresh_token"]=c.RefreshToken,["resource"]="https://api.openai.com/v1"
   }),ct);
   response.EnsureSuccessStatusCode();using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var r=payload.RootElement;
   var updated=c with {AccessToken=r.GetProperty("access_token").GetString()!,RefreshToken=r.TryGetProperty("refresh_token",out var refresh)?refresh.GetString()!:c.RefreshToken,Scope=r.TryGetProperty("scope",out var granted)?granted.GetString()??"":c.Scope,ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(r.GetProperty("expires_in").GetInt32())};
   if(!updated.Scope.Split(' ').Contains("chatgpt.tokens.use.direct")){current=null;store.Clear();throw new InvalidDataException("plan_usage_not_granted");}
   ct.ThrowIfCancellationRequested();store.Save(JsonSerializer.Serialize(updated));current=updated;return updated.AccessToken;
  } finally{refreshLock.Release();}
 }
 public async Task<IReadOnlyList<ModelChoice>> ModelsAsync(CancellationToken ct) {
  using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.openai.com/v1/models");
  request.Headers.Authorization=new("Bearer",await AccessTokenAsync(ct));
  using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();
  using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
  return payload.RootElement.GetProperty("models").EnumerateArray().Where(x=>x.TryGetProperty("visibility",out var v)&&v.GetString()=="list")
   .Select(x=>new ModelChoice(x.GetProperty("slug").GetString()!,x.GetProperty("display_name").GetString()!)).Take(100).ToArray();
 }
 public async Task<bool> DisconnectAsync(CancellationToken ct) {
  bool revoked=true;
  try {
   if(current is not null) {
    using var response=await http.PostAsync("https://auth.openai.com/api/accounts/oauth/revoke",new FormUrlEncodedContent(new Dictionary<string,string>{
     ["token"]=current.RefreshToken,["token_type_hint"]="refresh_token",["client_id"]=current.ClientId
    }),ct);revoked=response.IsSuccessStatusCode;
   }
  } catch(Exception e) when(e is HttpRequestException or OperationCanceledException){revoked=false;}
  finally{current=null;store.Clear();}
  return revoked;
 }
 private static string Random()=>OAuthSecurity.Base64Url(RandomNumberGenerator.GetBytes(32));
 private static async Task<string> ReadFirstLine(StreamReader reader,CancellationToken ct) {
  var line=new StringBuilder();var chars=new char[1];
  while(await reader.ReadAsync(chars.AsMemory(),ct)>0){if(chars[0]=='\n')return line.ToString().TrimEnd('\r');line.Append(chars[0]);if(line.Length>8192)throw new InvalidDataException("callback_too_large");}
  throw new InvalidDataException("invalid_callback");
 }
}
