using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Audio;
public interface ILocalPlayback {void Play(SoundCue cue,int volume);void Stop();}
public sealed class LocalSoundPlayer(ILocalPlayback playback) {
 private readonly SoundGate gate=new();
 private long lastInteraction;
 public bool TryPlayInteraction(SoundCue cue,long interactionId,TimeSpan now){if(interactionId<=lastInteraction)return false;lastInteraction=interactionId;if(!gate.TryPlayInteraction(now))return false;playback.Stop();playback.Play(cue,gate.Policy.Volume);return true;}
 public void SetPolicy(SoundPolicy policy){gate.SetPolicy(policy);if(policy.Muted||policy.Suppressed||policy.Volume==0)Stop();}
 public bool TryPlay(SoundCue cue,TimeSpan now){if(!gate.TryPlay(now))return false;playback.Stop();playback.Play(cue,gate.Policy.Volume);return true;}
 public void Stop()=>playback.Stop();
}
