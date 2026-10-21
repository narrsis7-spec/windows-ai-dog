using WindowsAiDog.Core;
var tests=new List<(string,Action)>();
void Test(string name,Action body)=>tests.Add((name,body));
void Assert(bool condition) {if(!condition)throw new Exception("assertion failed");}
Test("FocusSuppressesMotionAndSound",()=>{var c=new BehaviorController();c.Apply(new(PetEventKind.SetFocus,true),TimeSpan.Zero);var d=c.Apply(new(PetEventKind.RequestAction,Action:PetAction.Run),TimeSpan.FromSeconds(1));Assert(d.Action==null&&d.Sound==null&&d.State.Mood==Mood.Resting);});
Test("NeglectDoesNotReduceAffinity",()=>{var c=new BehaviorController(new(Affinity:50));Assert(c.Apply(new(PetEventKind.Tick),TimeSpan.FromDays(5)).State.Affinity==50);});
Test("StrokeCooldownUsesMonotonicClock",()=>{var c=new BehaviorController();for(int i=0;i<100;i++)c.Apply(new(PetEventKind.Stroke),TimeSpan.Zero);Assert(c.State.Affinity==1);c.Apply(new(PetEventKind.Stroke),TimeSpan.FromSeconds(2));Assert(c.State.Affinity==2);});
Test("AffinityCapsAt100",()=>{var c=new BehaviorController(new(Affinity:99));c.Apply(new(PetEventKind.Treat),TimeSpan.Zero);c.Apply(new(PetEventKind.Treat),TimeSpan.FromSeconds(3));Assert(c.State.Affinity==100);});
Test("UnknownEnumRejected",()=>Assert(ActionValidator.Parse("{\"reply\":\"안녕\",\"action\":\"DeleteFile\"}")==null));
Test("NumericEnumRejected",()=>Assert(ActionValidator.Parse("{\"reply\":\"안녕\",\"action\":1}")==null));
Test("ExtraFieldRejected",()=>Assert(ActionValidator.Parse("{\"reply\":\"안녕\",\"action\":null,\"shell\":\"whoami\"}")==null));
Test("DuplicateFieldRejected",()=>Assert(ActionValidator.Parse("{\"reply\":\"a\",\"reply\":\"b\",\"action\":null}")==null));
Test("ValidActionAllowed",()=>Assert(ActionValidator.Parse("{\"reply\":\"안녕\",\"action\":\"Jump\"}")?.Action==PetAction.Jump));
Test("OversizedReplyRejected",()=>Assert(ActionValidator.Parse(System.Text.Json.JsonSerializer.Serialize(new{reply=new string('x',2001),action=(string?)null}))==null));
Test("ControlCharacterNameRejected",()=>Assert(!SettingsValidator.IsValid(new(Name:"hello\nworld"))));
Test("InvalidVolumeRejected",()=>Assert(!SettingsValidator.IsValid(new(Volume:101))));
Test("ValidUnicodeNameAccepted",()=>Assert(SettingsValidator.IsValid(new(Name:"멍멍🐶"))));
Test("MutedGateRejectsSound",()=>{var g=new SoundGate();g.SetPolicy(new(Muted:true));Assert(!g.TryPlay(TimeSpan.Zero));});
Test("HiddenGateRejectsSound",()=>{var g=new SoundGate();g.SetPolicy(new(Suppressed:true));Assert(!g.TryPlay(TimeSpan.Zero));});
Test("MinimumIntervalPreventsReplay",()=>{var g=new SoundGate();Assert(g.TryPlay(TimeSpan.Zero));Assert(!g.TryPlay(TimeSpan.FromSeconds(1)));Assert(g.TryPlay(TimeSpan.FromSeconds(10)));});
Test("NegativeMonitorPositionPreserved",()=>Assert(MonitorGeometry.Clamp(new(-1000,100),new(-1920,0,1920,1080),160,140)==new Placement(-1000,100)));
Test("DetachedMonitorRecovered",()=>Assert(MonitorGeometry.Clamp(new(-4000,4000),new(0,0,1920,1080),160,140)==new Placement(0,940)));
Test("RectOutsideImageRejected",()=>Assert(!SpriteAtlas.IsValid(new(1700,0,100,100,0,0,100),1774,887)));
Test("AirborneAnchorPreserved",()=>Assert(SpriteAtlas.IsValid(new(0,0,200,200,100,146,100),1774,887)));
Test("StreamWithoutCompletedFails",()=>{var s=new ChatStreamState();s.Apply(new(ChatEventKind.TextDelta,"hi"));s.End();Assert(!s.Completed&&s.Error!=null);});
Test("LimitAfterDeltaDoesNotComplete",()=>{var s=new ChatStreamState();s.Apply(new(ChatEventKind.TextDelta,"hi"));s.Apply(new(ChatEventKind.Failed,ErrorCode:"subscription_sharing_usage_limit_exceeded"));s.End();Assert(!s.Completed&&s.Error=="subscription_sharing_usage_limit_exceeded");});
Test("CompletedStreamSucceeds",()=>{var s=new ChatStreamState();s.Apply(new(ChatEventKind.Completed));s.End();Assert(s.Completed&&s.Error==null);});
Test("IncompleteNotSuccess",()=>{var s=new ChatStreamState();s.Apply(new(ChatEventKind.Incomplete));s.End();Assert(!s.Completed&&s.Error!=null);});
Test("PkceRfc7636Vector",()=>Assert(OAuthSecurity.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
Test("WrongStateRejected",()=>Assert(!OAuthSecurity.ValidateCallback("expected","wrong","oaiapp_test",null)));
Test("AuthCancelRejected",()=>Assert(!OAuthSecurity.ValidateCallback("expected","expected","oaiapp_test","access_denied")));
Test("DynamicClientNotAccepted",()=>Assert(!OAuthSecurity.ValidateCallback("expected","expected","dynamic_agent_client",null)));
Test("NullNameRejected",()=>Assert(!SettingsValidator.IsValid(new(Name:null!))));
Test("CancelledCompletedReplyDoesNotExecuteAction",()=>{
 using var cts=new CancellationTokenSource();cts.Cancel();int count=0;bool cancelled=false;
 try {ReplyCommit.Apply(new("hello",PetAction.Jump),cts.Token,result=>count++);}catch(OperationCanceledException){cancelled=true;}
 Assert(cancelled&&count==0);
});
Test("CompletedReplyCommittedOnce",()=>{int count=0;ReplyCommit.Apply(new("hello",PetAction.Jump),default,result=>count++);Assert(count==1);});
int failures=0;
foreach(var (name,body) in tests){try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
Console.WriteLine($"RESULT {tests.Count-failures}/{tests.Count} passed");
return failures==0?0:1;
