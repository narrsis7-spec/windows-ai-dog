using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace WindowsAiDog.Core;
public static class SettingsValidator {
 public static bool IsValid(AppSettings s) {
  if(s.Name is null)return false;
  var length=new StringInfo(s.Name).LengthInTextElements;
  return s.SchemaVersion==1 && length is >=1 and <=20 && !string.IsNullOrWhiteSpace(s.Name)
    && !s.Name.Any(char.IsControl) && s.Affinity is >=0 and <=100 && s.Volume is >=0 and <=100
    && s.SoundIntervalSeconds is >=1 and <=60
    && (double.IsNaN(s.X)||double.IsFinite(s.X)) && (double.IsNaN(s.Y)||double.IsFinite(s.Y));
 }
}
public static class ActionValidator {
 public static PetReply? Parse(string json) {
  if(Encoding.UTF8.GetByteCount(json)>16384)return null;
  try {
   using var doc=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=4});
   if(doc.RootElement.ValueKind!=JsonValueKind.Object)return null;
   var fields=doc.RootElement.EnumerateObject().ToArray();
   if(fields.Length!=2||fields.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=2
     ||!fields.Any(x=>x.Name=="reply")||!fields.Any(x=>x.Name=="action"))return null;
   var reply=doc.RootElement.GetProperty("reply");
   var action=doc.RootElement.GetProperty("action");
   if(reply.ValueKind!=JsonValueKind.String || reply.GetString() is not string text || text.Length>2000)return null;
   if(action.ValueKind==JsonValueKind.Null)return new(text,null);
   if(action.ValueKind!=JsonValueKind.String)return null;
   var value=action.GetString();
   if(value is null || !Enum.GetNames<PetAction>().Contains(value,StringComparer.Ordinal))return null;
   return new(text,Enum.Parse<PetAction>(value));
  } catch(JsonException) {return null;}
 }
}
public sealed class SoundGate {
 public SoundPolicy Policy {get;private set;}=new();
 private TimeSpan? last;
 public void SetPolicy(SoundPolicy policy) => Policy=policy;
 public bool TryPlayInteraction(TimeSpan now){if(Policy.Muted||Policy.Suppressed||Policy.Volume<=0)return false;last=now;return true;}
 public bool TryPlay(TimeSpan now) {
  if(Policy.Muted||Policy.Suppressed||Policy.Volume<=0)return false;
  if(last is not null&&now-last<(Policy.MinimumInterval??TimeSpan.FromSeconds(10)))return false;
  last=now;return true;
 }
}
public static class MonitorGeometry {
 public static Placement Clamp(Placement p,WorkArea area,double width,double height) =>
  new(Math.Clamp(double.IsFinite(p.X)?p.X:area.X,area.X,Math.Max(area.X,area.X+area.Width-width)),
      Math.Clamp(double.IsFinite(p.Y)?p.Y:area.Y+area.Height-height,area.Y,Math.Max(area.Y,area.Y+area.Height-height)));
}
public static class SpriteAtlas {
 public static bool IsValid(SpriteFrame f,int width,int height) =>
  f.X>=0&&f.Y>=0&&f.Width>0&&f.Height>0&&(long)f.X+f.Width<=width&&(long)f.Y+f.Height<=height
  && double.IsFinite(f.AnchorX)&&double.IsFinite(f.AnchorY)&&f.DurationMs is >0 and <=10000;
}
public sealed class ChatStreamState {
 public bool Completed {get;private set;}
 public string? Error {get;private set;}
 private bool terminal;
 public void Apply(ChatEvent value) {
  if(terminal)return;
  if(value.Kind==ChatEventKind.Completed){Completed=true;terminal=true;}
  else if(value.Kind is ChatEventKind.Incomplete or ChatEventKind.Failed) {
   Completed=false;Error=value.ErrorCode??value.Kind.ToString();terminal=true;
  }
 }
 public void End() {if(!terminal){Error="stream_interrupted";terminal=true;}}
}
public static class OAuthSecurity {
 public static string Challenge(string verifier)=>Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
 public static string Base64Url(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
 public static bool ValidateCallback(string expected,string? actual,string? client,string? error) =>
  actual is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),Encoding.UTF8.GetBytes(actual))
  && error is null && !string.IsNullOrWhiteSpace(client)&&client!="dynamic_agent_client";
}

public static class ReplyCommit {
 public static void Apply(PetReply reply,CancellationToken ct,Action<PetReply> apply){ct.ThrowIfCancellationRequested();apply(reply);}
}
