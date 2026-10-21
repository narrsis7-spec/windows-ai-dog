using System.IO;
using System.Windows.Media;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Audio;
// Original synthetic QA tones. These are not recordings of a dog.
public sealed class TestTonePlayback:ILocalPlayback,IDisposable {
 private readonly MediaPlayer player=new();
 private readonly string directory;
 public TestTonePlayback(string directory) {this.directory=directory;Directory.CreateDirectory(directory);}
 public void Play(SoundCue cue,int volume) {
  var path=Path.Combine(directory,cue+".wav");
  if(!File.Exists(path)) {
   int rate=16000,count=4000;using var writer=new BinaryWriter(File.Create(path));
   writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
   writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
   writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
   double frequency=cue==SoundCue.Bark?350:850;
   for(int i=0;i<count;i++)writer.Write((short)(Math.Sin(2*Math.PI*frequency*i/rate)*Math.Sin(Math.PI*i/count)*6000));
  }
  player.Open(new Uri(path));player.Volume=Math.Clamp(volume/100.0,0,1);player.Play();
 }
 public void Stop(){player.Stop();player.Close();}
 public void Dispose()=>Stop();
}
