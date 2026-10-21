namespace WindowsAiDog.Core;
public enum PetAction { WagTail, Walk, Run, Jump, Treat, TiltHead, Sit, LieDown }
public enum Mood { Calm, Happy, Curious, Resting }
public enum SoundCue { Bark, Lick }
public enum PetEventKind { Tick, Stroke, Treat, SetFocus, RequestAction }
public record PetEvent(PetEventKind Kind, bool Focus=false, PetAction? Action=null);
public record PetSnapshot(string Name="멍멍이",int Affinity=0,Mood Mood=Mood.Calm,bool FocusMode=false,PetAction? Action=null);
public record BehaviorDecision(PetSnapshot State,PetAction? Action,SoundCue? Sound);
public record SoundPolicy(int Volume=40,bool Muted=false,TimeSpan? MinimumInterval=null,bool Suppressed=false);
public record VisibilityState(bool Locked,bool Fullscreen,bool FocusMode) { public bool IsSuppressed => Locked||Fullscreen||FocusMode; }
public record AppSettings(int SchemaVersion=1,string Name="멍멍이",int Affinity=0,bool FocusMode=false,int Volume=40,bool Muted=false,int SoundIntervalSeconds=10,double X=double.NaN,double Y=double.NaN);
public record WorkArea(double X,double Y,double Width,double Height);
public record Placement(double X,double Y);
public record SpriteFrame(int X,int Y,int Width,int Height,double AnchorX,double AnchorY,int DurationMs);
public record PetReply(string Reply,PetAction? Action);
public enum ChatEventKind { TextDelta,Completed,Incomplete,Failed }
public record ChatEvent(ChatEventKind Kind,string? Text=null,string? ErrorCode=null);
