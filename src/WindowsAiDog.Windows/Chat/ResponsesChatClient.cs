using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Chat;
public sealed class ResponsesChatClient(HttpClient http):IChatClient {
 public async Task<PetReply> SendAsync(string token,string model,string text,string name,CancellationToken ct,IReadOnlyList<ConversationTurn>? history=null) {
  if(text.Length is <1 or >4000)throw new ArgumentException("입력은 1~4000자");
  var messages=new List<object>{
   new {role="system",content=PetPrompt.Instruction}};
  if(history is not null)foreach(var turn in history.TakeLast(12)){if(turn.Role is not ("user" or "assistant")||turn.Text.Length>4000)throw new ArgumentException("invalid history");messages.Add(new {role=turn.Role,content=turn.Text});}
  messages.Add(new{role="user",content=JsonSerializer.Serialize(new{pet_name=name,message=text})});
  using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  request.Content=new StringContent(JsonSerializer.Serialize(new {
   model,store=false,stream=true,max_output_tokens=1000,
   input=messages

  }),Encoding.UTF8,"application/json");
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException("HTTP "+(int)response.StatusCode,null,response.StatusCode);
  using var stream=await response.Content.ReadAsStreamAsync(ct);
  using var reader=new StreamReader(stream);
  var state=new ChatStreamState();var output=new StringBuilder();
  try {while(await ReadLineBoundedAsync(reader,ct) is string line) {
   if(!line.StartsWith("data:",StringComparison.Ordinal))continue;
   var payload=line[5..].Trim();if(payload=="[DONE]")break;
   using var document=JsonDocument.Parse(payload);
   var root=document.RootElement;
   if(!root.TryGetProperty("type",out var type))continue;
   switch(type.GetString()) {
    case "response.output_text.delta":
     var delta=root.GetProperty("delta").GetString()??"";
     if(output.Length+delta.Length>16384)throw new InvalidDataException("reply_too_large");
     output.Append(delta);state.Apply(new(ChatEventKind.TextDelta));break;
    case "response.completed":state.Apply(new(ChatEventKind.Completed));break;
    case "response.incomplete":state.Apply(new(ChatEventKind.Incomplete));break;
    case "response.failed":
     string code="response_failed";
     if(root.TryGetProperty("response",out var r)&&r.TryGetProperty("error",out var e)&&e.ValueKind==JsonValueKind.Object&&e.TryGetProperty("code",out var c))code=c.GetString()??code;
     state.Apply(new(ChatEventKind.Failed,ErrorCode:code));break;
    case "error":state.Apply(new(ChatEventKind.Failed,ErrorCode:"stream_error"));break;
   }
   if(state.Completed||state.Error is not null)break;
  }
  }catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException){throw new InvalidDataException("invalid_stream_event",e);}
  state.End();ct.ThrowIfCancellationRequested();
  if(!state.Completed)throw new InvalidDataException(state.Error??"stream_interrupted");
  return ActionValidator.Parse(output.ToString())??throw new InvalidDataException("invalid_pet_reply");
 }
 private static async Task<string?> ReadLineBoundedAsync(StreamReader reader,CancellationToken ct) {
  var line=new StringBuilder();var buffer=new char[1];
  while(await reader.ReadAsync(buffer.AsMemory(),ct)>0){if(buffer[0]=='\n')return line.ToString().TrimEnd('\r');line.Append(buffer[0]);if(line.Length>65536)throw new InvalidDataException("event_too_large");}
  return line.Length==0?null:line.ToString();
 }
}
public static class IdTokenValidator {
 private static byte[] Decode(string text){text=text.Replace('-','+').Replace('_','/');return Convert.FromBase64String(text.PadRight((text.Length+3)/4*4,'='));}
 public static string Validate(string token,string jwks,string client,string nonce,DateTimeOffset now) {
  if(token.Length>32768)throw new InvalidDataException("invalid_identity");
  try {
   var parts=token.Split('.');if(parts.Length!=3)throw new InvalidDataException("invalid_identity");
   using var header=JsonDocument.Parse(Decode(parts[0]));using var claims=JsonDocument.Parse(Decode(parts[1]));using var keys=JsonDocument.Parse(jwks);
   if(header.RootElement.GetProperty("alg").GetString()!="RS256")throw new InvalidDataException("invalid_signature_algorithm");
   var kid=header.RootElement.GetProperty("kid").GetString();
   var key=keys.RootElement.GetProperty("keys").EnumerateArray().Single(x=>x.GetProperty("kid").GetString()==kid&&x.GetProperty("kty").GetString()=="RSA");
   using var rsa=RSA.Create();
   rsa.ImportParameters(new(){Modulus=Decode(key.GetProperty("n").GetString()!),Exponent=Decode(key.GetProperty("e").GetString()!)});
   if(!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0]+"."+parts[1]),Decode(parts[2]),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new InvalidDataException("invalid_signature");
   var c=claims.RootElement;var aud=c.GetProperty("aud");
   bool audience=aud.ValueKind==JsonValueKind.String?aud.GetString()==client:aud.EnumerateArray().Any(x=>x.GetString()==client);
   if(c.GetProperty("iss").GetString()!="https://auth.openai.com"||!audience||c.GetProperty("exp").GetInt64()<=now.ToUnixTimeSeconds()||c.GetProperty("nonce").GetString()!=nonce)throw new InvalidDataException("invalid_identity_claims");
   return c.GetProperty("sub").GetString()??throw new InvalidDataException("missing_subject");
  } catch(Exception e) when(e is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or CryptographicException) {throw new InvalidDataException("invalid_identity",e);}
 }
}
