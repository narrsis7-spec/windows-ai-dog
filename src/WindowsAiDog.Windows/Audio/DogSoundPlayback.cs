using System.IO;
using System.Windows.Media;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Audio;
public sealed class DogSoundPlayback(string directory):ILocalPlayback,IDisposable {
 private readonly MediaPlayer player=new();
 public event Action<SoundCue>? PlaybackOpened;public event Action? PlaybackEnded;private SoundCue activeCue;private bool observed;
 public void Play(SoundCue cue,int volume) {
  string file=Path.Combine(directory,cue.ToString().ToLowerInvariant()+".wav");
  if(!File.Exists(file))return;
  if(!observed){observed=true;player.MediaOpened+=(s,e)=>{if(player.HasAudio)PlaybackOpened?.Invoke(activeCue);};player.MediaEnded+=(s,e)=>PlaybackEnded?.Invoke();}activeCue=cue;player.Open(new Uri(file));player.Volume=Math.Clamp(volume/100.0,0,1);player.Play();
 }
 public void Stop()=>player.Stop();
 public void Dispose()=>player.Close();
}
