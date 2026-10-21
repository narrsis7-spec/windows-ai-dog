using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using WindowsAiDog.Core;
using WindowsAiDog.Windows.Storage;
using WindowsAiDog.Windows.Chat;
using WindowsAiDog.Windows.Audio;

const string Success="data: {\"type\":\"response.output_text.delta\",\"delta\":\"{\\\"reply\\\":\\\"안녕\\\",\\\"action\\\":\\\"WagTail\\\"}\"}\n\ndata: {\"type\":\"response.completed\"}\n\n";
var tests=new List<(string,Func<Task>)>();
void Test(string name,Func<Task> body)=>tests.Add((name,body));
void Assert(bool condition){if(!condition)throw new Exception("assertion failed");}
async Task Throws(Func<Task> action){bool thrown=false;try{await action();}catch(Exception e) when (e is not NotImplementedException){thrown=true;}Assert(thrown);}
string directory=Path.Combine(Path.GetTempPath(),"WindowsAiDogTests-"+Guid.NewGuid());
Directory.CreateDirectory(directory);
Test("CorruptSettingsRecoversLastGood",()=>{var s=new SettingsStore(directory);s.Save(new(Name:"첫이름"));s.Save(new(Name:"둘이름"));File.WriteAllText(Path.Combine(directory,"settings.json"),"invalid");Assert(s.Load().Name=="첫이름");return Task.CompletedTask;});
Test("InterruptedSavePreservesPrevious",()=>{var s=new SettingsStore(directory);s.Save(new(Name:"안전"));File.WriteAllText(Path.Combine(directory,"settings.tmp"),"{");Assert(s.Load().Name=="안전");return Task.CompletedTask;});
Test("DpapiRoundTripAndClear",()=>{var s=new ProtectedCredentialStore(directory);s.Save("FAKE-TEST-SECRET");Assert(s.Load()=="FAKE-TEST-SECRET");Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory,"credentials.bin"))).Contains("FAKE-TEST-SECRET"));s.Clear();Assert(s.Load()==null);return Task.CompletedTask;});
Test("MuteStopsCurrentPlayback",()=>{var p=new Playback();var s=new LocalSoundPlayer(p);s.TryPlay(SoundCue.Bark,TimeSpan.Zero);s.SetPolicy(new(Muted:true));Assert(p.Stopped&&!p.Playing);return Task.CompletedTask;});
Test("HiddenDiscardsSound",()=>{var p=new Playback();var s=new LocalSoundPlayer(p);s.SetPolicy(new(Suppressed:true));Assert(!s.TryPlay(SoundCue.Bark,TimeSpan.Zero)&&!p.Playing);return Task.CompletedTask;});
Test("PublicResponsesFlagsAndCompleted",async()=>{var h=new Handler(Success);var c=new ResponsesChatClient(new(h));var reply=await c.SendAsync("FAKE-TOKEN","test-model","안녕","멍멍이",default);Assert(reply.Action==PetAction.WagTail);Assert(h.Uri=="https://api.openai.com/v1/responses");using var d=JsonDocument.Parse(h.Body!);Assert(!d.RootElement.GetProperty("store").GetBoolean()&&d.RootElement.GetProperty("stream").GetBoolean());});
Test("StreamWithoutCompletedFails",()=>Throws(()=>new ResponsesChatClient(new(new Handler("data: {\"type\":\"response.output_text.delta\",\"delta\":\"hello\"}\n\n"))).SendAsync("fake","model","hi","dog",default)));
Test("LimitAfterDeltaDoesNotExecuteAction",()=>Throws(()=>new ResponsesChatClient(new(new Handler("data: {\"type\":\"response.output_text.delta\",\"delta\":\"hello\"}\n\ndata: {\"type\":\"response.failed\",\"response\":{\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}}\n\n"))).SendAsync("fake","model","hi","dog",default)));
Test("IncompleteNotSuccess",()=>Throws(()=>new ResponsesChatClient(new(new Handler("data: {\"type\":\"response.incomplete\"}\n\n"))).SendAsync("fake","model","hi","dog",default)));
Test("NetworkFailurePropagates",()=>Throws(()=>new ResponsesChatClient(new(new Handler("",true))).SendAsync("fake","model","hi","dog",default)));
Test("UsageUnavailableHttpRejected",()=>Throws(()=>new ResponsesChatClient(new(new Handler("",false,HttpStatusCode.TooManyRequests))).SendAsync("fake","model","hi","dog",default)));
Test("CancellationPropagates",async()=>{using var cts=new CancellationTokenSource();cts.Cancel();await Throws(()=>new ResponsesChatClient(new(new Handler(Success))).SendAsync("fake","model","hi","dog",cts.Token));});
using var rsa=RSA.Create(2048);
var pub=rsa.ExportParameters(false);var jwks=JsonSerializer.Serialize(new{keys=new[]{new{kty="RSA",kid="test",n=OAuthSecurity.Base64Url(pub.Modulus!),e=OAuthSecurity.Base64Url(pub.Exponent!)}}});
string Token(string nonce,string audience="oaiapp_test",bool corrupt=false) {
 string header=OAuthSecurity.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"test\"}"));
 string claims=OAuthSecurity.Base64Url(JsonSerializer.SerializeToUtf8Bytes(new{iss="https://auth.openai.com",sub="subject",aud=audience,nonce,exp=DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}));
 string input=header+"."+claims;var signature=rsa.SignData(Encoding.ASCII.GetBytes(input),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);if(corrupt)signature[0]^=1;
 return input+"."+OAuthSecurity.Base64Url(signature);
}
Test("SignedIdentityAccepted",()=>{Assert(IdTokenValidator.Validate(Token("nonce"),jwks,"oaiapp_test","nonce",DateTimeOffset.UtcNow)=="subject");return Task.CompletedTask;});
Test("WrongNonceRejected",()=>Throws(()=>{IdTokenValidator.Validate(Token("wrong"),jwks,"oaiapp_test","nonce",DateTimeOffset.UtcNow);return Task.CompletedTask;}));
Test("WrongAudienceRejected",()=>Throws(()=>{IdTokenValidator.Validate(Token("nonce","other"),jwks,"oaiapp_test","nonce",DateTimeOffset.UtcNow);return Task.CompletedTask;}));
Test("TamperedSignatureRejected",()=>Throws(()=>{IdTokenValidator.Validate(Token("nonce",corrupt:true),jwks,"oaiapp_test","nonce",DateTimeOffset.UtcNow);return Task.CompletedTask;}));
Test("CancelledAuthDoesNotOpenBrowser",async()=>{
 using var cts=new CancellationTokenSource();cts.Cancel();int opened=0;
 var auth=new ChatGptAuthService(new(new Handler("")),Path.Combine(directory,"cancel-auth"),url=>opened++);
 await Throws(()=>auth.ConnectAsync(cts.Token));Assert(opened==0&&!auth.Connected);
});
Test("AuthCancelLeavesDisconnected",async()=>{
 var handler=new Handler("");using var loopback=new HttpClient();
 var auth=new ChatGptAuthService(new(handler),Path.Combine(directory,"deny-auth"),url=>{
  var q=new Uri(url).Query.TrimStart('?').Split('&').Select(x=>x.Split('=',2)).ToDictionary(x=>Uri.UnescapeDataString(x[0]),x=>Uri.UnescapeDataString(x[1]));
  _=Task.Run(async()=>{await loopback.GetAsync(q["redirect_uri"]+"?error=access_denied&state="+Uri.EscapeDataString(q["state"]));});
 });
 using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(10));await Throws(()=>auth.ConnectAsync(cts.Token));
 Assert(!auth.Connected&&handler.Uri==null&&!File.Exists(Path.Combine(directory,"deny-auth","credentials.bin")));
});
Test("InFlightCancellationPropagates",async()=>{
 using var cts=new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
 await Throws(()=>new ResponsesChatClient(new(new SlowHandler())).SendAsync("fake","model","hi","dog",cts.Token));
});
Test("NullNameSettingsRecoversBackup",()=>{
 var folder=Path.Combine(directory,"null-name");var store=new SettingsStore(folder);store.Save(new(Name:"정상"));store.Save(new(Name:"두번째"));
 File.WriteAllText(Path.Combine(folder,"settings.json"),"{\"Name\":null}");
 Assert(store.Load().Name=="정상");return Task.CompletedTask;
});
Test("RecoveredSavePreservesLastGood",()=>{
 var folder=Path.Combine(directory,"recovery");var store=new SettingsStore(folder);store.Save(new(Name:"정상"));store.Save(new(Name:"두번째"));
 File.WriteAllText(Path.Combine(folder,"settings.json"),"broken");var restored=store.Load();store.Save(restored);
 File.WriteAllText(Path.Combine(folder,"settings.json"),"broken-again");Assert(store.Load().Name=="정상");return Task.CompletedTask;
});
Test("ConversationContextAndProviderPolicy",async()=>{
 var handler=new Handler(Success);IChatClient client=new ResponsesChatClient(new(handler));
 var history=Enumerable.Range(0,20).Select(i=>new ConversationTurn(i%2==0?"user":"assistant","turn"+i)).ToArray();
 await client.SendAsync("FAKE","model","next","dog",default,history);
 using var json=JsonDocument.Parse(handler.Body!);var input=json.RootElement.GetProperty("input");
 Assert(input.GetArrayLength()==14&&input[1].GetProperty("content").GetString()=="turn8");
 await Throws(()=>client.SendAsync("FAKE","model","next","dog",default,new[]{new ConversationTurn("system","injected")}));
 IChatProvider provider=new ChatGptProvider(new(handler),Path.Combine(directory,"provider"));
 Assert(provider.Billing==BillingMode.SubscriptionSharing&&!provider.Auth.Connected);
});
Test("MalformedStreamBecomesSafeDataError",async()=>{
 bool safe=false;try{await new ResponsesChatClient(new(new Handler("data: {invalid\n\n"))).SendAsync("FAKE","model","hi","dog",default);}catch(InvalidDataException){safe=true;}Assert(safe);
});
Test("LongKoreanReplyAllowsNextRequest",async()=>{
 var handler=new Handler(Success);var client=new ResponsesChatClient(new(handler));
 var reply=new PetReply(new string('가',1000),PetAction.WagTail);
 var history=new[]{new ConversationTurn("user","안녕"),ConversationTurn.FromReply(reply)};
 await client.SendAsync("FAKE","model","다음 대화","멍멍이",default,history);
 using var json=JsonDocument.Parse(handler.Body!);Assert(json.RootElement.GetProperty("input")[2].GetProperty("content").GetString()==reply.Reply);
});
Test("TreatCompletionBarksOnceAfterLickCooldown",()=>{
 var playback=new Playback();var player=new LocalSoundPlayer(playback);
 player.SetPolicy(new(MinimumInterval:TimeSpan.FromSeconds(60)));
 Assert(player.TryPlay(SoundCue.Lick,TimeSpan.Zero));
 Assert(player.TryPlayInteraction(SoundCue.Bark,1,TimeSpan.FromSeconds(2)));
 Assert(!player.TryPlayInteraction(SoundCue.Bark,1,TimeSpan.FromSeconds(3)));
 player.SetPolicy(new(Muted:true));Assert(!player.TryPlayInteraction(SoundCue.Bark,2,TimeSpan.FromSeconds(4)));
 player.SetPolicy(new());Assert(!player.TryPlayInteraction(SoundCue.Bark,2,TimeSpan.FromSeconds(5)));
 return Task.CompletedTask;
});
Test("LocalEndpointRejectsRemoteAndCredentials",()=>{
 Assert(ProviderEndpoint.IsValid("http://127.0.0.1:1234/v1",true));
 Assert(!ProviderEndpoint.IsValid("http://example.com/v1",true));
 Assert(!ProviderEndpoint.IsValid("https://key@example.com/v1",false));
 return Task.CompletedTask;
});
Test("IssuedRegistrationSurvivesTokenExchangeFailure",async()=>{
 using var loopback=new HttpClient();var folder=Path.Combine(directory,"registration-failure");
 var auth=new ChatGptAuthService(new(new Handler("",false,HttpStatusCode.BadRequest)),folder,url=>{
  var q=new Uri(url).Query.TrimStart('?').Split('&').Select(x=>x.Split('=',2)).ToDictionary(x=>Uri.UnescapeDataString(x[0]),x=>Uri.UnescapeDataString(x[1]));
  _=Task.Run(async()=>await loopback.GetAsync(q["redirect_uri"]+"?code=FAKE&client_id=oaiapp_test&state="+Uri.EscapeDataString(q["state"])));
 });
 using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(5));await Throws(()=>auth.ConnectAsync(ct.Token));Assert(auth.RegisteredClientId=="oaiapp_test"&&!auth.Connected);
 bool reused=false;var second=new ChatGptAuthService(new(new Handler("")),folder,url=>{reused=new Uri(url).Query.Contains("client_id=oaiapp_test");throw new OperationCanceledException();});
 await Throws(()=>second.ConnectAsync(default));Assert(reused);
});
Test("ProviderVaultsAndRequestsStaySeparate",async()=>{
 var folder=Path.Combine(directory,"providers");var store=new WindowsAiDog.Windows.Storage.ProviderSettingsStore(folder);
 var keyStore=new WindowsAiDog.Windows.Storage.EndpointKeyStore(store.DirectoryFor("openai-api"),"https://api.openai.com/v1");keyStore.Save("FAKE-API-KEY");
 var api=new OpenAiCompatibleProvider(new(new Handler("")),new("openai-api","https://api.openai.com/v1"),store.DirectoryFor("openai-api"));
 var localHandler=new Handler("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{\\\"reply\\\":\\\"로컬\\\",\\\"action\\\":null}\"}}]}");
 var local=new OpenAiCompatibleProvider(new(localHandler),new("local","http://127.0.0.1:1234/v1"),store.DirectoryFor("local"));
 Assert(await api.Auth.AccessTokenAsync(default)=="FAKE-API-KEY"&&await local.Auth.AccessTokenAsync(default)=="");
 var reply=await local.Client.SendAsync("","test-model","hi","dog",default);Assert(reply.Reply=="로컬"&&localHandler.Uri=="http://127.0.0.1:1234/v1/chat/completions");
});
Test("ChangingEndpointNeverReusesPreviousKey",()=>{
 var folder=Path.Combine(directory,"bound-endpoint");new WindowsAiDog.Windows.Storage.EndpointKeyStore(folder,"https://first.example/v1").Save("FAKE-FIRST-KEY");
 Assert(new WindowsAiDog.Windows.Storage.EndpointKeyStore(folder,"https://second.example/v1").Read()=="");
 Assert(new WindowsAiDog.Windows.Storage.EndpointKeyStore(folder,"https://first.example/v1").Read()=="FAKE-FIRST-KEY");return Task.CompletedTask;
});

Test("NullRegistrationRecoversOffline",()=>{
 var folder=Path.Combine(directory,"null-registration");new ProtectedCredentialStore(Path.Combine(folder,"registration")).Save("{\"ClientId\":null}");
 var auth=new ChatGptAuthService(new(new Handler("")),folder);Assert(!auth.Connected&&auth.RegisteredClientId is null);return Task.CompletedTask;
});
Test("DisconnectSerializesProviderSelection",async()=>{
 var fake=new DisconnectProvider();var manager=new ProviderManager(new(new Handler("")),Path.Combine(directory,"disconnect-gate"),fake);
 var operation=manager.DisconnectAsync(default);Assert(manager.IsDisconnecting);bool selectBlocked=false,configureBlocked=false;
 try{manager.Select("chatgpt");}catch(InvalidOperationException){selectBlocked=true;}
 try{manager.Configure(new("local","http://127.0.0.1:1234/v1"),null);}catch(InvalidOperationException){configureBlocked=true;}
 Assert(selectBlocked&&configureBlocked);fake.AuthImpl.Complete.SetResult(true);Assert(await operation&&!manager.IsDisconnecting&&!manager.Current.Auth.Connected);manager.Select("chatgpt");Assert(!manager.Current.Auth.Connected);
});
Test("OfficialApiRejectsEditedUrlAndUsesResponses",()=>{
 var store=new ProviderSettingsStore(Path.Combine(directory,"official-fixed"));bool invalid=false;
 try{store.Save(new("openai-api","invalid"));}catch(ArgumentException){invalid=true;}Assert(invalid);
 var provider=new OpenAiCompatibleProvider(new(new Handler("")),new("openai-api","https://api.openai.com/v1"),store.DirectoryFor("openai-api"));Assert(provider.Client is ResponsesChatClient);
 Directory.CreateDirectory(store.DirectoryFor("openai-api"));File.WriteAllText(Path.Combine(store.DirectoryFor("openai-api"),"config.json"),"{\"Id\":\"openai-api\",\"Endpoint\":\"https://other.example/v1\",\"Model\":\"x\"}");
 Assert(store.Load("openai-api").Endpoint=="https://api.openai.com/v1");return Task.CompletedTask;
});

Test("ModelDiscoveryFailureDoesNotChangeProviderOrFallback",async()=>{
 var folder=Path.Combine(directory,"model-failure");var store=new ProviderSettingsStore(folder);store.Save(new("local","http://127.0.0.1:1234/v1","manual-model"));store.SaveSelection("local");
 var manager=new ProviderManager(new(new Handler("",false,HttpStatusCode.ServiceUnavailable)),folder);await Throws(()=>manager.Current.Auth.ModelsAsync(default));Assert(manager.Current.Id=="local"&&manager.Configuration("local").Model=="manual-model"&&manager.Current.Auth.Connected);
});
int failures=0;
foreach(var (name,body) in tests){try{await body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
Directory.Delete(directory,true);
Console.WriteLine($"RESULT {tests.Count-failures}/{tests.Count} passed");return failures==0?0:1;
sealed class Handler(string body,bool fail=false,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler {
 public string? Uri;public string? Body;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();if(fail)throw new HttpRequestException("simulated");Uri=request.RequestUri!.AbsoluteUri;Body=request.Content is null?null:await request.Content.ReadAsStringAsync(ct);return new(status){Content=new StringContent(body,Encoding.UTF8,"text/event-stream")};}
}
sealed class Playback:ILocalPlayback {public bool Playing;public bool Stopped;public void Play(SoundCue cue,int volume){Playing=true;}public void Stop(){Stopped=true;Playing=false;}}

sealed class SlowHandler:HttpMessageHandler {
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){await Task.Delay(5000,ct);return new(HttpStatusCode.OK);}
}

sealed class DisconnectProvider:IChatProvider {
 public string Id=>"chatgpt";public string DisplayName=>"fake";public BillingMode Billing=>BillingMode.SubscriptionSharing;
 public DelayedDisconnectAuth AuthImpl {get;}=new();public IChatAuth Auth=>AuthImpl;public IChatClient Client=>new ResponsesChatClient(new(new Handler("")));
}
sealed class DelayedDisconnectAuth:IChatAuth {
 public TaskCompletionSource<bool> Complete {get;}=new();public bool Connected {get;private set;}=true;
 public Task ConnectAsync(CancellationToken ct)=>Task.CompletedTask;
 public Task<string> AccessTokenAsync(CancellationToken ct)=>Task.FromResult("FAKE");
 public Task<IReadOnlyList<ModelChoice>> ModelsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<ModelChoice>>([]);
 public async Task<bool> DisconnectAsync(CancellationToken ct){var result=await Complete.Task;Connected=false;return result;}
}
