using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Rendering;
public sealed record AtlasPose(int X,int Y,int Width,int Height,double AnchorX,double AnchorY);
public sealed record SpriteClip(string Image,int Fps,IReadOnlyList<AtlasPose> Frames,double TravelPixelsPerSecond=0,int? EatCompletedFrame=null,IReadOnlyList<int>? Sequence=null,bool StationaryTailWag=false,IReadOnlyList<double>? CoatColorGain=null,IReadOnlyList<int>? SettleFrames=null,SpriteClip? Stop=null);
public sealed class SpriteCatalog {
 public IReadOnlyDictionary<PetAction,SpriteClip> Clips {get;}
 private SpriteCatalog(Dictionary<PetAction,SpriteClip> clips)=>Clips=clips;
 private static readonly Dictionary<PetAction,string> Names=new(){[PetAction.WagTail]="idle",[PetAction.Walk]="walk",[PetAction.Run]="run",[PetAction.Jump]="jump",[PetAction.TiltHead]="headtilt",[PetAction.Treat]="treat",[PetAction.Sit]="sit",[PetAction.LieDown]="lie"};
 public static SpriteCatalog Load(string directory) {
  var atlas=Path.Combine(directory,"atlas.json");
  if(new FileInfo(atlas).Length>128*1024)throw new InvalidDataException("atlas too large");
  using var doc=JsonDocument.Parse(File.ReadAllText(atlas),new(){MaxDepth=12});
  var root=doc.RootElement;
  bool v9=root.GetProperty("version").GetString()=="v9-eight"; bool v8=v9||root.GetProperty("version").GetString()=="v8-eight"; bool v7=v8||root.GetProperty("version").GetString()=="v7-eight"; bool v6=v7||root.GetProperty("version").GetString()=="v6-eight";
  if(!v6&&root.GetProperty("version").GetString()!="v5-six")throw new InvalidDataException("unsupported atlas");
  int count=v6?8:6;if(root.GetProperty("totalFrames").GetInt32()!=(v7?72:count*8))throw new InvalidDataException("frame total");
  var actions=root.GetProperty("actions");if(actions.EnumerateObject().Count()!=count)throw new InvalidDataException("action count");
  var clips=new Dictionary<PetAction,SpriteClip>();
  foreach(var (action,name) in Names.Where(p=>v6||p.Key is not(PetAction.Sit or PetAction.LieDown))) {
   int frameCount=v7&&action==PetAction.Jump?16:8; var c=actions.GetProperty(name);var image=c.GetProperty("image").GetString();
   if(image!=name+".png")throw new InvalidDataException("unexpected image");
   var path=Path.Combine(directory,image);
   if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("image too large");
   using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(c.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("sprite hash mismatch");
   var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(path);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();
   int w=c.GetProperty("width").GetInt32(),h=c.GetProperty("height").GetInt32(),fps=c.GetProperty("defaultFps").GetInt32();
   if(w!=(v6?1776:1774)||h!=(v7&&action==PetAction.Jump?1776:v6?888:887)||bitmap.PixelWidth!=w||bitmap.PixelHeight!=h||fps is <1 or >30)throw new InvalidDataException("sprite dimensions/fps");
   var sequence=c.GetProperty("playbackSequence").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
   if(sequence.Length<frameCount||sequence.Length>64||sequence.Any(v=>v<0||v>=frameCount)||(!v8||action!=PetAction.WagTail)&&!sequence.SequenceEqual(Enumerable.Range(0,frameCount)))throw new InvalidDataException("sequence");
   var frames=new List<AtlasPose>();foreach(var f in c.GetProperty("frames").EnumerateArray()) {
    var r=f.GetProperty("sourceRect");var a=f.GetProperty("anchor");
    var pose=new AtlasPose(r.GetProperty("x").GetInt32(),r.GetProperty("y").GetInt32(),r.GetProperty("width").GetInt32(),r.GetProperty("height").GetInt32(),a.GetProperty("x").GetDouble(),a.GetProperty("y").GetDouble());
    if(f.GetProperty("index").GetInt32()!=frames.Count||!SpriteAtlas.IsValid(new(pose.X,pose.Y,pose.Width,pose.Height,pose.AnchorX,pose.AnchorY,1000/fps),w,h)||pose.AnchorX<0||pose.AnchorX>pose.Width||pose.AnchorY<0||pose.AnchorY>pose.Height)throw new InvalidDataException("invalid frame");
    if(v6&&(pose.AnchorX!=222||pose.AnchorY!=405||pose.Width!=444||pose.Height!=444))throw new InvalidDataException("v6 world anchor");
    if(!v6&&action==PetAction.Jump&&(pose.AnchorX!=221.75||pose.AnchorY!=423))throw new InvalidDataException("jump anchor");
    if(!v6&&action==PetAction.Run&&(pose.AnchorX!=221.75||pose.AnchorY!=(frames.Count<4?384:345)))throw new InvalidDataException("run anchor");
    frames.Add(pose);
   }
   if(frames.Count!=frameCount)throw new InvalidDataException("frame count");
   double travel=c.TryGetProperty("travelPixelsPerSecond",out var speed)?speed.GetDouble():action==PetAction.Walk?42:action==PetAction.Run?115:0;
   int? eat=c.TryGetProperty("eatCompletedFrame",out var eaten)?eaten.GetInt32():null;
   if(!double.IsFinite(travel)||travel<0||travel>250||eat is int index&&(action!=PetAction.Treat||index<0||index>=frameCount))throw new InvalidDataException("invalid action metadata");
   bool wag=c.TryGetProperty("stationaryTailWag",out var stationary)&&stationary.GetBoolean();
   if(wag&&(!v9||action!=PetAction.WagTail))throw new InvalidDataException("invalid stationary wag");
   var gain=c.TryGetProperty("coatColorGain",out var coat)?coat.EnumerateArray().Select(x=>x.GetDouble()).ToArray():null;
   if(gain is not null&&(!v9||gain.Length!=3||gain.Any(x=>!double.IsFinite(x)||x<.75||x>1.4)))throw new InvalidDataException("invalid coat gain");
   // Locomotion frames closest to the standing idle pose; a stop waits for one of them.
   var settle=c.TryGetProperty("settleFrames",out var settleJson)?settleJson.EnumerateArray().Select(v=>v.GetInt32()).ToArray():null;
   if(settle is not null&&(action is not(PetAction.Walk or PetAction.Run)||settle.Length==0||settle.Any(v=>v<0||v>=frameCount)))throw new InvalidDataException("invalid settle frames");
   // Optional one-shot clip that takes a run down to the standing pose (runstop.png).
   SpriteClip? stop=null;
   if(c.TryGetProperty("stop",out var stopJson)){if(!v9||action!=PetAction.Run)throw new InvalidDataException("invalid stop clip");stop=LoadStop(directory,stopJson);}
   clips.Add(action,new(path,fps,frames,travel,eat,sequence,wag,gain,settle,stop));
  }
  return new(clips);
 }
 private static SpriteClip LoadStop(string directory,JsonElement c){
  if(c.GetProperty("image").GetString()!="runstop.png")throw new InvalidDataException("unexpected stop image");
  var path=Path.Combine(directory,"runstop.png");
  if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("image too large");
  using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(c.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("sprite hash mismatch");
  var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(path);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();
  int w=c.GetProperty("width").GetInt32(),h=c.GetProperty("height").GetInt32(),fps=c.GetProperty("defaultFps").GetInt32();
  if(w!=1776||h is not(888 or 1776)||bitmap.PixelWidth!=w||bitmap.PixelHeight!=h||fps is <1 or >30)throw new InvalidDataException("stop dimensions/fps");
  var frames=new List<AtlasPose>();foreach(var f in c.GetProperty("frames").EnumerateArray()){
   var r=f.GetProperty("sourceRect");var a=f.GetProperty("anchor");
   var pose=new AtlasPose(r.GetProperty("x").GetInt32(),r.GetProperty("y").GetInt32(),r.GetProperty("width").GetInt32(),r.GetProperty("height").GetInt32(),a.GetProperty("x").GetDouble(),a.GetProperty("y").GetDouble());
   if(f.GetProperty("index").GetInt32()!=frames.Count||!SpriteAtlas.IsValid(new(pose.X,pose.Y,pose.Width,pose.Height,pose.AnchorX,pose.AnchorY,1000/fps),w,h)||pose.AnchorX!=222||pose.AnchorY!=405||pose.Width!=444||pose.Height!=444)throw new InvalidDataException("invalid stop frame");
   frames.Add(pose);
  }
  if(frames.Count!=h/444*4)throw new InvalidDataException("stop frame count");
  return new(path,fps,frames);
 }
}
public sealed class SpriteTimeline {
 private int current;
 public int Frame(PetAction action,double seconds,bool paused,int frameCount=8,int? framesPerSecond=null) {
  if(paused)return current;
  int fps=action==PetAction.Run?12:action is PetAction.Treat or PetAction.TiltHead or PetAction.Sit or PetAction.LieDown?6:8;
  fps=framesPerSecond??fps; int step=(int)Math.Min(int.MaxValue,Math.Max(0,seconds)*fps);
  current=action is PetAction.Jump or PetAction.Treat or PetAction.TiltHead or PetAction.Sit or PetAction.LieDown?Math.Min(frameCount-1,step):step%frameCount;return current;
 }
}
