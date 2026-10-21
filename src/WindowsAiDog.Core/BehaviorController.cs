namespace WindowsAiDog.Core;
public sealed class BehaviorController {
 public PetSnapshot State {get;private set;}
 private TimeSpan? lastReward;
 private TimeSpan activeUntil;
 public BehaviorController(PetSnapshot? state=null) { State=state??new(); }
 public BehaviorDecision Apply(PetEvent input,TimeSpan now) {
  if(input.Kind==PetEventKind.SetFocus) State=State with {FocusMode=input.Focus,Mood=input.Focus?Mood.Resting:Mood.Calm,Action=null};
  if(State.FocusMode) return new(State,null,null);
  PetAction? action=null; SoundCue? sound=null;
  if(input.Kind==PetEventKind.Stroke || input.Kind==PetEventKind.Treat) {
   if(lastReward is null || now-lastReward>=TimeSpan.FromSeconds(2)) {
    State=State with {Affinity=Math.Min(100,State.Affinity+1)};lastReward=now;
   }
   action=input.Kind==PetEventKind.Stroke?PetAction.WagTail:PetAction.Treat;
   sound=input.Kind==PetEventKind.Stroke?null:SoundCue.Lick;
   State=State with {Mood=Mood.Happy};activeUntil=now+TimeSpan.FromSeconds(3);
  } else if(input.Kind==PetEventKind.RequestAction && input.Action is PetAction requested && Enum.IsDefined(requested)) {
   action=requested;sound=requested is PetAction.Run or PetAction.Jump?SoundCue.Bark:null;State=State with {Mood=Mood.Curious};activeUntil=now+TimeSpan.FromSeconds(requested==PetAction.Sit?20:requested==PetAction.LieDown?30:3);
  } else if(input.Kind==PetEventKind.Tick && now>=activeUntil) {
   State=State with {Mood=Mood.Calm,Action=null};
  }
  if(action is not null)State=State with {Action=action};
  return new(State,action,sound);
 }
}
