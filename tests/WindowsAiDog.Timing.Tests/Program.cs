using WindowsAiDog.Core;
using WindowsAiDog.Windows.Rendering;
using System.IO;
using System.Reflection;
using WindowsAiDog.Windows.Views;

internal static class Program {
 static double Step(SpriteVisual sprite,PetAction action,double now,bool paused=false)=>sprite.Tick(action,now,paused,false,.1);
 static SpriteVisual Fresh(PetAction action,bool immediate=true) {
  var sprite=new SpriteVisual(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v9"));
  sprite.Restart(action,0,immediate);return sprite;
 }
 static void Equal(double actual,double expected){if(Math.Abs(actual-expected)>1e-8)throw new Exception($"expected {expected:R}, actual {actual:R}");}
 static void Frame(SpriteVisual sprite,int expected){if(sprite.CurrentFrame!=expected)throw new Exception($"expected frame {expected}, actual {sprite.CurrentFrame}");}
 static readonly SpriteCatalog Catalog=SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v9"));
 // Frame shown after the given seconds of motion, from the clip's own atlas fps.
 static int Pose(PetAction action,double motionSeconds){var c=Catalog.Clips[action];return (int)(Math.Round(motionSeconds,9)*c.Fps)%c.Frames.Count;}
 static double B=>SpriteVisual.BlendSeconds;
 // Steps in bounded 0.05s ticks to `to`, asserting no travel while the transition is still running.
 static void Advance(SpriteVisual s,PetAction a,double from,double to){for(double t=from+.05;t<to-1e-9;t+=.05)Equal(Step(s,a,t),0);Equal(Step(s,a,to),0);}
 [STAThread]static int Main() {
  var tests=new List<(string,Action)>();void Check(string name,Action run)=>tests.Add((name,run));
  Check("DelayedWalkSharesPointOneSeconds",()=>{var s=Fresh(PetAction.Walk);Equal(Step(s,PetAction.Walk,.5),.1);Frame(s,0);});
  Check("DelayedRunSharesPointOneSeconds",()=>{var s=Fresh(PetAction.Run);Equal(Step(s,PetAction.Run,.5),.1);Frame(s,Pose(PetAction.Run,.1));});
  Check("RunBeforeFrameBoundaryDoesNotAdvanceEarly",()=>{var s=Fresh(PetAction.Run);Step(s,PetAction.Run,1.0/Catalog.Clips[PetAction.Run].Fps-.000001);Frame(s,0);});
  Check("TransitionCrossingIntegratesOnlyAfterBlend",()=>{var s=Fresh(PetAction.Walk,false);Advance(s,PetAction.Walk,0,B-.02);Equal(Step(s,PetAction.Walk,B+.02),.02);Frame(s,0);});
  Check("ExactTransitionBoundaryDoesNotTravel",()=>{var s=Fresh(PetAction.Walk,false);Advance(s,PetAction.Walk,0,B);});
  Check("ImmediatelyAfterBoundaryTravelsOnlyRemainder",()=>{var s=Fresh(PetAction.Run,false);Advance(s,PetAction.Run,0,B);Equal(Step(s,PetAction.Run,B+.001),.001);});
  Check("DelayedTransitionTickUsesBoundedRemainder",()=>{var s=Fresh(PetAction.Walk,false);Advance(s,PetAction.Walk,0,B-.02);Equal(Step(s,PetAction.Walk,B+.32),.08);Frame(s,0);});
  Check("TypingPauseFreezesFrameAndTravel",()=>{var s=Fresh(PetAction.Walk);Step(s,PetAction.Walk,.08);Step(s,PetAction.Walk,.16);Frame(s,Pose(PetAction.Walk,.16));Equal(Step(s,PetAction.Walk,.9,true),0);Frame(s,Pose(PetAction.Walk,.16));});
  Check("TypingResumeDoesNotCatchUp",()=>{var s=Fresh(PetAction.Walk);Step(s,PetAction.Walk,.08);Step(s,PetAction.Walk,.16);Step(s,PetAction.Walk,.9,true);Step(s,PetAction.Walk,1.9,true);Equal(Step(s,PetAction.Walk,2.3),0);Frame(s,Pose(PetAction.Walk,.16));Equal(Step(s,PetAction.Walk,2.35),.05);Frame(s,Pose(PetAction.Walk,.21));});
  Check("DragPauseDoesNotAdvanceRunPhase",()=>{var s=Fresh(PetAction.Run);Step(s,PetAction.Run,.08);Step(s,PetAction.Run,.16);Step(s,PetAction.Run,.9,true);Step(s,PetAction.Run,1.9,true);Equal(Step(s,PetAction.Run,2.3),0);Frame(s,1);});
  Check("PauseDuringBlendPreservesBlend",()=>{var s=Fresh(PetAction.Walk,false);Step(s,PetAction.Walk,.08);Step(s,PetAction.Walk,1,true);if(!s.IsTransitioning)throw new Exception("paused blend completed");Equal(Step(s,PetAction.Walk,2),0);if(!s.IsTransitioning)throw new Exception("resume skipped blend");Advance(s,PetAction.Walk,2,2+B-.08);Equal(Step(s,PetAction.Walk,2+B-.06),.02);});
  Check("RepeatedPausesDoNotAccumulateTravel",()=>{var s=Fresh(PetAction.Run);double t=0,travel=0;for(int i=0;i<3;i++){t+=.05;travel+=Step(s,PetAction.Run,t);t+=1;travel+=Step(s,PetAction.Run,t,true);t+=1;travel+=Step(s,PetAction.Run,t);}Equal(travel,.15);Frame(s,1);});
  Check("NormalWalkCycleRetainsEightFrames",()=>{var s=Fresh(PetAction.Walk);double travel=0;for(int i=1;i<=20;i++)travel+=Step(s,PetAction.Walk,i*.05);Equal(travel,1);Frame(s,Pose(PetAction.Walk,1));});
  Check("BackwardBoundedSampleDoesNotReplayTravel",()=>{var s=Fresh(PetAction.Run);Equal(Step(s,PetAction.Run,.1),.1);Frame(s,Pose(PetAction.Run,.1));Equal(Step(s,PetAction.Run,.05),0);Frame(s,Pose(PetAction.Run,.1));Equal(Step(s,PetAction.Run,.15),.05);});
  Check("ChangedActionWhilePausedStartsWithoutCatchup",()=>{var s=Fresh(PetAction.Walk);Step(s,PetAction.Walk,.08);Step(s,PetAction.Walk,.16);Step(s,PetAction.Walk,.9,true);s.Restart(PetAction.Run,1);Equal(Step(s,PetAction.Run,1.1,true),0);Frame(s,0);Equal(Step(s,PetAction.Run,2),0);Advance(s,PetAction.Run,2,2+B);Equal(Step(s,PetAction.Run,2+B+.02),.02);});
  foreach(var pose in new[]{PetAction.Sit,PetAction.LieDown})Check(pose+"ReverseThenBlendDoesNotTravelEarly",()=>{var s=Fresh(pose);s.Tick(pose,.9,false,false);Frame(s,5);s.Restart(PetAction.Walk,1);for(int i=1;i<=10;i++)Equal(Step(s,PetAction.Walk,1+i*.1),0);Advance(s,PetAction.Walk,2,2+B);Equal(Step(s,PetAction.Walk,2+B+.02),.02);});
  Check("JumpLandingThenBlendDoesNotTravelEarly",()=>{var s=Fresh(PetAction.Jump);s.Tick(PetAction.Jump,.7,false,false);s.Restart(PetAction.Walk,.7);for(int i=1;i<=4;i++)Equal(Step(s,PetAction.Walk,.7+i*.1),0);Equal(s.JumpHeight,0);Advance(s,PetAction.Walk,1.1,1.1+B);Equal(Step(s,PetAction.Walk,1.1+B+.02),.02);});
  Check("HeadTiltReversesBackToStandingBeforeIdle",()=>{var s=Fresh(PetAction.TiltHead);s.Tick(PetAction.TiltHead,2,false,false);Frame(s,7);s.Restart(PetAction.WagTail,2);s.Tick(PetAction.WagTail,2.2,false,false);if(s.CurrentAction!=PetAction.TiltHead||s.CurrentFrame>=7)throw new Exception("tilt did not reverse");s.Tick(PetAction.WagTail,3.4,false,false);s.Tick(PetAction.WagTail,3.4+B+.01,false,false);if(s.CurrentAction!=PetAction.WagTail||s.IsTransitioning)throw new Exception("tilt never returned");});
  foreach(var gait in new[]{PetAction.Walk,PetAction.Run})Check(gait+"StopsOnNearStandingFrameAndKeepsTravelling",()=>{
   var s=Fresh(gait);var settle=Catalog.Clips[gait].SettleFrames!;double t=0;
   // Walk until the gait sits on a frame that is not a settle frame, then ask it to stop.
   do{t+=.05;Step(s,gait,t);}while(settle.Contains(s.CurrentFrame));
   s.Restart(PetAction.WagTail,t);if(!s.IsSettling||s.CurrentAction!=gait)throw new Exception("stopped mid-stride");
   double travel=0;int guard=0;while(s.IsSettling&&guard++<80){t+=.05;travel+=Step(s,PetAction.WagTail,t);}
   if(s.IsSettling||travel<=0||s.CurrentAction!=PetAction.WagTail||!s.IsTransitioning)throw new Exception($"settle failed travel={travel}");
  });
  Check("StoppingAfterWalkingLeftKeepsFacingLeft",()=>{var s=Fresh(PetAction.Walk);double t=0;for(int i=0;i<4;i++){t+=.05;s.Tick(PetAction.Walk,t,false,true,.1);}
   s.Restart(PetAction.WagTail,t);for(int i=0;i<40;i++){t+=.05;s.Tick(PetAction.WagTail,t,false,true,.1);}
   if(s.CurrentAction!=PetAction.WagTail||s.IsTransitioning||!s.FacingLeft)throw new Exception("idle flipped back to the right");});
  Check("RunEndsThroughSlowDownClipWithEasingTravel",()=>{
   var s=Fresh(PetAction.Run);var stop=Catalog.Clips[PetAction.Run].Stop!;double t=0;for(int i=0;i<6;i++){t+=.05;Step(s,PetAction.Run,t);}
   s.Restart(PetAction.WagTail,t);var travels=new List<double>();int guard=0;
   while(s.IsSettling&&guard++<200){t+=.05;travels.Add(Step(s,PetAction.WagTail,t));}
   double duration=stop.Frames.Count/(double)stop.Fps;
   if(s.IsSettling||s.CurrentAction!=PetAction.WagTail)throw new Exception("stop clip never finished");
   if(travels.Count*.05<duration-.06)throw new Exception($"stop ended early after {travels.Count*.05:F2}s");
   if(!(travels[0]>travels[travels.Count/2]&&travels[travels.Count/2]>travels[^2]))throw new Exception("travel did not ease out");
  });
  Check("TreatStillUsesWallTime",()=>{var s=Fresh(PetAction.Treat);s.Tick(PetAction.Treat,.8,false,false);Frame(s,4);});
  Check("JumpStillUsesWallTime",()=>{var s=Fresh(PetAction.Jump);s.Tick(PetAction.Jump,.7,false,false);if(s.JumpHeight<=30)throw new Exception("jump timing changed");});
  Check("PetWindowDragWiringFreezesAndResumesWithoutCatchup",()=>{
   string root=Path.Combine(Path.GetTempPath(),"WindowsAiDogTiming-"+Guid.NewGuid());
   var pet=new PetWindow(root,fullscreenProbe:_=>false);var tick=typeof(PetWindow).GetMethod("TickAt",BindingFlags.NonPublic|BindingFlags.Instance)!;
   var dragging=typeof(PetWindow).GetField("dragging",BindingFlags.NonPublic|BindingFlags.Instance)!;
   try{pet.Request(PetAction.Walk);pet.Sprite.Restart(PetAction.Walk,0,true);tick.Invoke(pet,[.08]);tick.Invoke(pet,[.16]);int held=Pose(PetAction.Walk,.16);Frame(pet.Sprite,held);dragging.SetValue(pet,true);tick.Invoke(pet,[.9]);Frame(pet.Sprite,held);dragging.SetValue(pet,false);tick.Invoke(pet,[1.9]);Frame(pet.Sprite,held);tick.Invoke(pet,[1.95]);tick.Invoke(pet,[2.05]);Frame(pet.Sprite,Pose(PetAction.Walk,.31));}finally{pet.Close();}
  });
  int failures=0;foreach(var (name,run) in tests){try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
  Console.WriteLine($"RESULT {tests.Count-failures}/{tests.Count} passed");return failures==0?0:1;
 }
}
