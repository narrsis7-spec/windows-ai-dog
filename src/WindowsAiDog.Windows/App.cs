using System.Windows;
using System.IO;
using WindowsAiDog.Windows.Views;
namespace WindowsAiDog.Windows;
public sealed class App:Application {
 private Mutex? single;
 private string? smokeDirectory;
 [STAThread]public static void Main(){new App().Run();}
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);single=new Mutex(true,"Local\\WindowsAiDog.Desktop",out bool created);
  if(!created){Shutdown();return;}
  DispatcherUnhandledException+=(s,a)=>{try{var log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WindowsAiDog");Directory.CreateDirectory(log);File.AppendAllText(Path.Combine(log,"error-types.log"),a.Exception.GetType().Name+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}a.Handled=true;MessageBox.Show("앱 오류가 발생했습니다. 창을 닫고 다시 실행하세요.");};
  if(e.Args.Contains("--smoke"))smokeDirectory=Path.Combine(Path.GetTempPath(),"WindowsAiDog-smoke-"+Guid.NewGuid());
  string? profileDirectory=null;
  int profileIndex=Array.IndexOf(e.Args,"--profile");
  if(profileIndex>=0){
   if(profileIndex+1>=e.Args.Length){Shutdown();return;}
   var folder=new DirectoryInfo(Path.GetFullPath(e.Args[profileIndex+1]));const string prefix="WindowsAiDog-QA-";
   if(!string.Equals(folder.Parent?.FullName,Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!folder.Name.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParse(folder.Name[prefix.Length..],out _)){Shutdown();return;}
   profileDirectory=folder.FullName;
  }
  var pet=new PetWindow(profileDirectory??smokeDirectory,diagnosticTrace:profileDirectory is not null&&e.Args.Contains("--record-runtime"));MainWindow=pet;pet.Show();
  if(e.Args.Contains("--smoke")) {
   pet.ApplySettings(pet.Settings with{Muted=true,FocusMode=true});
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
   timer.Tick+=(s,a)=>{timer.Stop();Shutdown();};timer.Start();
  }
 }
 protected override void OnExit(ExitEventArgs e){single?.Dispose();if(smokeDirectory is not null&&Path.GetFullPath(smokeDirectory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Directory.Exists(smokeDirectory))Directory.Delete(smokeDirectory,true);base.OnExit(e);}
}
