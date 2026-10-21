using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsAiDog.Windows.Views;
using WindowsAiDog.Core;
internal static class Program {
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLong(IntPtr hwnd,int index);
 static void Capture(FrameworkElement visual,string path) {
  int width=(int)Math.Ceiling(visual.ActualWidth),height=(int)Math.Ceiling(visual.ActualHeight);
  var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(visual),null,new Rect(0,0,width,height));bitmap.Render(drawing);
  var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
  using var file=File.Create(path);png.Save(file);
 }
 [STAThread]static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var root=Path.Combine(Path.GetTempPath(),"WindowsAiDogUi-"+Guid.NewGuid());Directory.CreateDirectory(root);
  var tests=new List<(string,Action)>();int failures=0;
  var before=GetForegroundWindow();
  IntPtr testForeground=IntPtr.Zero;
  var pet=new PetWindow(root,fullscreenProbe:own=>testForeground==IntPtr.Zero?WindowsAiDog.Windows.Interop.MonitorService.Fullscreen(own):WindowsAiDog.Windows.Interop.MonitorService.IsFullscreenWindow(own,testForeground));pet.Show();pet.UpdateLayout();
  var hwnd=new WindowInteropHelper(pet).Handle;
  void Check(string name,Action test)=>tests.Add((name,test));
  void Assert(bool condition){if(!condition)throw new Exception("assertion failed");}
  Check("PetShowDoesNotStealForeground",()=>Assert(before==GetForegroundWindow()));
  Check("PetWindowHasNoActivateStyle",()=>Assert((GetWindowLong(hwnd,-20).ToInt64()&0x08000000)!=0));
  Check("MouseActivateReturnsNoActivate",()=>Assert(SendMessage(hwnd,0x21,IntPtr.Zero,IntPtr.Zero).ToInt32()==3));
  Check("TransparentMarginClickThrough",()=>{var p=pet.PointToScreen(new Point(2,2));long packed=((long)(ushort)(short)p.Y<<16)|(ushort)(short)p.X;Assert(SendMessage(hwnd,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt32()==-1);});
  Check("LockHidesPetAndUnlockKeepsFocus",()=>{pet.ApplySettings(pet.Settings with{FocusMode=true});SendMessage(hwnd,0x2b1,new IntPtr(7),IntPtr.Zero);Assert(!pet.IsVisible);SendMessage(hwnd,0x2b1,new IntPtr(8),IntPtr.Zero);Assert(pet.Settings.FocusMode);});
  Check("StrokeIncreasesAffinity",()=>{pet.ApplySettings(pet.Settings with{FocusMode=false});int start=pet.Settings.Affinity;pet.Stroke();Assert(pet.Settings.Affinity==start+1);});
  Check("RenderingProducesPng",()=>{
   var visual=(System.Windows.Controls.Canvas)pet.Content;visual.Measure(new Size(224,220));visual.Arrange(new Rect(0,0,224,220));
   var bitmap=new RenderTargetBitmap(224,220,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory("artifacts");
   using var file=File.Create("artifacts/test-pet-preview.png");png.Save(file);
   Assert(file.Length>100);
  });
  Check("SettingsRestoreAfterRestart",()=>{
   pet.ApplySettings(pet.Settings with{Name="테스트멍",FocusMode=true,Muted=true,Volume=30});
   var restored=new PetWindow(root);
   Assert(restored.Settings.Name=="테스트멍"&&restored.Settings.FocusMode&&restored.Settings.Muted&&restored.Settings.Volume==30);
   restored.Close();
  });
  Check("SettingsAndChatScreenshots",()=>{
   var settings=new SettingsWindow(pet);settings.Show();settings.UpdateLayout();
   Capture((FrameworkElement)settings.Content,"artifacts/settings-preview.png");settings.Close();
   var chat=new ChatWindow(pet);chat.Show();chat.UpdateLayout();
   // Skip the entrance fade so the preview shows the settled input bar.
   var bar=(FrameworkElement)chat.Content;bar.BeginAnimation(UIElement.OpacityProperty,null);bar.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,null);bar.UpdateLayout();
   Capture(bar,"artifacts/chat-preview.png");chat.Close();
   var dpi=VisualTreeHelper.GetDpi(pet);
   Console.WriteLine($"ACTUAL_DPI {dpi.PixelsPerInchX}x{dpi.PixelsPerInchY}");
  });
  Check("LockHidesChatAndSettings",()=>{
   pet.OpenChat();pet.OpenSettings();
   SendMessage(hwnd,0x2b1,new IntPtr(7),IntPtr.Zero);
   Assert(app.Windows.OfType<Window>().Where(w=>w is ChatWindow or SettingsWindow).All(w=>!w.IsVisible));
   SendMessage(hwnd,0x2b1,new IntPtr(8),IntPtr.Zero);
  });
  Check("ApprovedV5CatalogLoads48Frames",()=>{
   var catalog=WindowsAiDog.Windows.Rendering.SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites"));
   Assert(catalog.Clips.Count==6&&catalog.Clips.Values.Sum(c=>c.Frames.Count)==48);
   Assert(catalog.Clips[PetAction.Walk].Frames[1].Width==444);
  });
  Check("RunAndJumpKeepFixedAirborneAnchors",()=>{
   var catalog=WindowsAiDog.Windows.Rendering.SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites"));
   Assert(catalog.Clips[PetAction.Jump].Frames.All(f=>f.AnchorX==221.75&&f.AnchorY==423));
   Assert(catalog.Clips[PetAction.Run].Frames.Take(4).All(f=>f.AnchorY==384));
  });
  Check("JumpDoesNotLoopAndFocusFreezes",()=>{
   var timeline=new WindowsAiDog.Windows.Rendering.SpriteTimeline();
   Assert(timeline.Frame(PetAction.Jump,0,false)==0);
   Assert(timeline.Frame(PetAction.Jump,0.4,false)==3);
   Assert(timeline.Frame(PetAction.Jump,3,false)==7);
   Assert(timeline.Frame(PetAction.Jump,4,true)==7);
  });
  Check("TrayIconRegisters",()=>Assert(pet.TrayRegistered));
  Check("All48RenderedFramesAndDirection",()=>{
   var sheet=new RenderTargetBitmap(224*8,220*6,96,96,PixelFormats.Pbgra32);
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
    int row=0;foreach(var action in Enum.GetValues<PetAction>().Where(a=>a is not (PetAction.Sit or PetAction.LieDown))){
     pet.Sprite.Restart(action,0,true);
     for(int i=0;i<8;i++){
      double seconds=(i+0.05)*(action==PetAction.Jump?1.0/12:pet.Sprite.CycleSeconds/8);
      pet.Sprite.Tick(action,seconds,false,false);pet.Sprite.UpdateLayout();Assert(pet.Sprite.CurrentFrame==i);
      var frame=new RenderTargetBitmap(224,220,96,96,PixelFormats.Pbgra32);frame.Render(pet.Sprite);
      dc.DrawRectangle(Brushes.AliceBlue,null,new Rect(i*224,row*220,224,220));dc.DrawImage(frame,new Rect(i*224,row*220,224,220));
     }row++;
    }
   }
   sheet.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sheet));using(var file=File.Create("artifacts/v5-runtime-48.png"))png.Save(file);
   pet.Sprite.Restart(PetAction.Walk,0,true);pet.Sprite.Tick(PetAction.Walk,2.05*pet.Sprite.CycleSeconds/8,false,true);Assert(pet.Sprite.CurrentFrame==2);
  });
  Check("BubbleRemainsInsideWorkArea",()=>{
   pet.ApplySettings(pet.Settings with{FocusMode=false});pet.Say("테두리 말풍선 확인");
   var bubble=app.Windows.OfType<BubbleWindow>().First(w=>w.IsVisible);
   bubble.UpdateLayout();Capture((FrameworkElement)bubble.Content,"artifacts/bubble-preview.png");
   Assert(bubble.Left>=SystemParameters.VirtualScreenLeft&&bubble.Top>=SystemParameters.VirtualScreenTop);bubble.Close();
  });
  Check("PackagedDogSoundsAreShortPcm",()=>{
   foreach(var cue in Enum.GetValues<SoundCue>()){
    var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"assets","sounds",cue.ToString().ToLowerInvariant()+".wav"));
    Assert(System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WAVE"&&bytes.Length>1000&&bytes.Length<200000);
   }
  });
  Check("NativeFullscreenRectangleHidesPetWithInjectedForeground",()=>{
   var fullscreen=new Window{WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Left=0,Top=0,Width=SystemParameters.PrimaryScreenWidth,Height=SystemParameters.PrimaryScreenHeight,Topmost=true,Background=Brushes.White,Title="강아지 전체 화면 검증"};
   try{fullscreen.Show();fullscreen.Activate();fullscreen.UpdateLayout();var fullHwnd=new WindowInteropHelper(fullscreen).Handle;
    Console.WriteLine($"LIVE_FOREGROUND_ACQUIRED {GetForegroundWindow()==fullHwnd}");testForeground=fullHwnd;Assert(WindowsAiDog.Windows.Interop.MonitorService.IsFullscreenWindow(hwnd,fullHwnd));
    SendMessage(hwnd,0x2b1,new IntPtr(8),IntPtr.Zero);Assert(!pet.IsVisible);
   }finally{testForeground=IntPtr.Zero;fullscreen.Close();SendMessage(hwnd,0x2b1,new IntPtr(8),IntPtr.Zero);}
  });
  Check("OfflineChatPreservesInputWithoutInference",()=>{
   var offline=new ChatWindow(pet);offline.Show();offline.UpdateLayout();
   var input=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(offline)!;
   input.Text="오프라인 테스트";var send=(Task)typeof(ChatWindow).GetMethod("Send",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(offline,null)!;
   Assert(send.IsCompleted&&input.Text=="오프라인 테스트"&&!pet.Auth.Connected);offline.Close();
  });
  Check("FocusReturnsToGroundedIdle",()=>{pet.ApplySettings(pet.Settings with{FocusMode=false});pet.Sprite.Restart(PetAction.Jump,0,true);pet.Sprite.Tick(PetAction.Jump,0.4,false,false);Assert(pet.Sprite.CurrentFrame==4);pet.ApplySettings(pet.Settings with{FocusMode=true});Assert(pet.Sprite.CurrentAction==PetAction.WagTail&&pet.Sprite.CurrentFrame==0);});
  Check("NativeMediaDecoderAcceptsDogClips",()=>{
   foreach(var cue in Enum.GetValues<SoundCue>()){
    var media=new MediaPlayer();bool opened=false;var frame=new System.Windows.Threading.DispatcherFrame();
    var timeout=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
    media.MediaOpened+=(s,e)=>{opened=media.HasAudio&&media.NaturalDuration.HasTimeSpan;frame.Continue=false;};
    media.MediaFailed+=(s,e)=>frame.Continue=false;timeout.Tick+=(s,e)=>frame.Continue=false;
    try{media.Volume=0;media.Open(new Uri(Path.Combine(AppContext.BaseDirectory,"assets","sounds",cue.ToString().ToLowerInvariant()+".wav")));timeout.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);Assert(opened);}finally{timeout.Stop();media.Close();}
   }
  });
  Check("RendererPreservesGroundAtSimulated200PercentDpi",()=>{
   pet.Sprite.Restart(PetAction.Jump,0);pet.Sprite.Tick(PetAction.Jump,0,false,false);pet.Sprite.UpdateLayout();
   int Bottom(int scale){var bitmap=new RenderTargetBitmap(224*scale,220*scale,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(pet.Sprite);var bytes=new byte[224*220*scale*scale*4];bitmap.CopyPixels(bytes,224*scale*4,0);for(int y=220*scale-1;y>=0;y--)for(int x=0;x<224*scale;x++)if(bytes[(y*224*scale+x)*4+3]>8)return y;return -1;}
   int a=Bottom(1),b=Bottom(2);Assert(a>100&&Math.Abs(b-2*a)<=2);
  });
  Check("MenuTogglesShowCurrentMuteAndFocusState",()=>{
   var menu=((System.Windows.Controls.Canvas)pet.Content).ContextMenu!;
   System.Windows.Controls.MenuItem Find(string header)=>menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i=>(string)i.Header==header);
   var mute=Find("음소거");var focus=Find("집중 모드");var before=pet.Settings;
   try{
    pet.ApplySettings(pet.Settings with{Muted=true,FocusMode=false});menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent));Assert(mute.IsChecked&&!focus.IsChecked);
    mute.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent));Assert(!pet.Settings.Muted&&!mute.IsChecked);
    focus.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent));Assert(pet.Settings.FocusMode&&focus.IsChecked);
   }finally{pet.ApplySettings(before);}
  });
  Check("TraySettingsRestoresManuallyHiddenPet",()=>{
   pet.ApplySettings(pet.Settings with{FocusMode=false});
   var menu=((System.Windows.Controls.Canvas)pet.Content).ContextMenu!;
   var hide=menu.Items.OfType<System.Windows.Controls.MenuItem>().First(i=>i.Header.ToString()!.StartsWith("강아지 숨기기"));
   hide.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));Assert(!pet.IsVisible);
   pet.OpenSettings();Assert(pet.IsVisible&&app.Windows.OfType<SettingsWindow>().Any(w=>w.IsVisible));
   foreach(var window in app.Windows.OfType<SettingsWindow>().ToArray())window.Close();
  });
  Check("ProviderSwitchCancelsPendingAndClearsHistory",()=>{
   var window=new ChatWindow(pet);window.Show();window.UpdateLayout();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var history=(List<WindowsAiDog.Windows.Chat.ConversationTurn>)typeof(ChatWindow).GetField("history",flags)!.GetValue(window)!;history.Add(new("user","다른 업체로 보내면 안 됨"));
   using var pending=new CancellationTokenSource();typeof(ChatWindow).GetField("pending",flags)!.SetValue(window,pending);
   pet.Providers.Select("local");Assert(pending.IsCancellationRequested&&history.Count==0);typeof(ChatWindow).GetField("pending",flags)!.SetValue(window,null);
   var input=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",flags)!.GetValue(window)!;input.Text="로컬 테스트 준비";
   Capture((FrameworkElement)window.Content,"artifacts/providers-chat-preview.png");window.Close();
   var dialog=new ProviderSettingsWindow(pet.Providers);dialog.Show();dialog.UpdateLayout();Capture((FrameworkElement)dialog.Content,"artifacts/providers-settings-preview.png");dialog.Close();pet.Providers.Select("chatgpt");
  });
  Check("ActualTreatTimelineCompletionBark",()=>{
   pet.ApplySettings(pet.Settings with{Muted=true,FocusMode=false});pet.Request(PetAction.Treat);
   var clock=(System.Diagnostics.Stopwatch)typeof(PetWindow).GetField("clock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(pet)!;
   pet.Sprite.Restart(PetAction.Treat,clock.Elapsed.TotalSeconds-2,true);
   typeof(PetWindow).GetMethod("Tick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(pet,null);Assert(!pet.LastTreatBarkPlayed);
   Assert(typeof(PetWindow).GetField("treatDone",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(pet)==null);
  });
  Check("V6EightClipsShareWorldAnchorAndScale",()=>{
   var legacySprite=new WindowsAiDog.Windows.Rendering.SpriteVisual(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v6"));legacySprite.Measure(new Size(224,210));legacySprite.Arrange(new Rect(0,0,224,210));var catalog=WindowsAiDog.Windows.Rendering.SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v6"));Assert(catalog.Clips.Count==8&&catalog.Clips.Values.Sum(c=>c.Frames.Count)==64);
   Assert(catalog.Clips.Values.SelectMany(c=>c.Frames).All(f=>f.Width==444&&f.Height==444&&f.AnchorX==222&&f.AnchorY==405));
   var sheet=new RenderTargetBitmap(224*8,220*8,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
    int row=0;foreach(var action in Enum.GetValues<PetAction>()){
     legacySprite.Restart(action,0,true);for(int i=0;i<8;i++){double seconds=(i+0.05)/(action==PetAction.Run?12:action is PetAction.Treat or PetAction.TiltHead or PetAction.Sit or PetAction.LieDown?6:8);legacySprite.Tick(action,seconds,false,false);legacySprite.UpdateLayout();Assert(legacySprite.CurrentFrame==i);var bitmap=new RenderTargetBitmap(224,220,96,96,PixelFormats.Pbgra32);bitmap.Render(legacySprite);dc.DrawRectangle(Brushes.AliceBlue,null,new Rect(i*224,row*220,224,220));dc.DrawImage(bitmap,new Rect(i*224,row*220,224,220));}row++;
    }
   }sheet.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sheet));using(var f=File.Create("artifacts/v6-runtime-64.png"))png.Save(f);
  });
  Check("TreatCompletionRequestsOneBarkWithInjectedPlayback",()=>{
   var recorder=new PlaybackRecorder();var instance=new PetWindow(Path.Combine(root,"treat-live"),fullscreenProbe:_=>false,soundPlayback:recorder);instance.Show();instance.ApplySettings(instance.Settings with{Muted=false,FocusMode=false,SoundIntervalSeconds=60});instance.Request(PetAction.Treat);
   var frame=new System.Windows.Threading.DispatcherFrame();// Eight treat frames plus the action blend, with a margin for dispatcher ticks.
   int waitMs=(int)Math.Ceiling((8.0/5+WindowsAiDog.Windows.Rendering.SpriteVisual.BlendSeconds+.25)*1000);
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(waitMs)};timer.Tick+=(s,e)=>frame.Continue=false;
   try{timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);Assert(recorder.Cues.SequenceEqual(new[]{SoundCue.Bark})&&instance.LastTreatBarkPlayed&&instance.Sprite.CurrentFrame==7);
    File.WriteAllText("artifacts/treat-completion-evidence.json",System.Text.Json.JsonSerializer.Serialize(new{cues=recorder.Cues.Select(c=>c.ToString()),completedFrame=instance.Sprite.CurrentFrame,elapsedMilliseconds=waitMs,automaticCooldownSeconds=60,audible=false,note="Actual WPF dispatcher timeline with injected recording playback; no subjective audition"}));
    Capture((FrameworkElement)instance.Content,"artifacts/treat-complete-preview.png");
   }finally{timer.Stop();instance.Close();}
  });
  Check("IdleBodyStaysSteadyWhileTailWags",()=>{
   var folder=Path.Combine(AppContext.BaseDirectory,"assets",Directory.Exists(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v9"))?"sprites-v9":"sprites-v8");
   var v=new WindowsAiDog.Windows.Rendering.SpriteVisual(folder);v.Measure(new Size(224,210));v.Arrange(new Rect(0,0,224,210));v.Restart(PetAction.WagTail,0,true);
   byte[]? baseline=null;double bodyChange=0,tailChange=0;double cycle=v.CycleSeconds;
   for(int i=0;i<8;i++){v.Tick(PetAction.WagTail,(i+.01)*cycle/8,false,false);v.UpdateLayout();var path=Path.Combine(root,"stationary-"+i+".png");Capture(v,path);var b=new BitmapImage();b.BeginInit();b.UriSource=new Uri(path);b.CacheOption=BitmapCacheOption.OnLoad;b.EndInit();var px=new byte[224*210*4];b.CopyPixels(px,224*4,0);
    if(baseline is null){baseline=px;continue;}
    // Tail sits upper-left of the rump; the rest of the dog must not drift between idle frames.
    double body=0,tail=0;int nb=0,nt=0;for(int y=0;y<210;y++)for(int x=0;x<224;x++){int z=(y*224+x)*4+3;int d=Math.Abs(px[z]-baseline[z]);if(y<100&&x<90){tail+=d;nt++;}else{body+=d;nb++;}}
    bodyChange=Math.Max(bodyChange,body/nb);tailChange=Math.Max(tailChange,tail/nt);
   }Console.WriteLine($"IDLE bodyAlphaDelta={bodyChange:F2} tailAlphaDelta={tailChange:F2}");// The idle clip keeps small natural head movements while it watches the user.
   Assert(bodyChange<4&&tailChange>bodyChange*3);
  });
  Check("FullSubjectIdleHasNoAbruptAlphaLoss",()=>{
   byte[] Pixels(int frame){pet.Sprite.Restart(PetAction.WagTail,0,true);pet.Sprite.Tick(PetAction.WagTail,(frame+0.05)/8,false,false);pet.Sprite.UpdateLayout();var bitmap=new RenderTargetBitmap(224,220,96,96,PixelFormats.Pbgra32);bitmap.Render(pet.Sprite);var bytes=new byte[224*220*4];bitmap.CopyPixels(bytes,224*4,0);return bytes;}
   var areas=new List<int>();for(int frame=0;frame<8;frame++){var bytes=Pixels(frame);int count=0;for(int y=0;y<210;y++)for(int x=0;x<224;x++)if(bytes[(y*224+x)*4+3]>16)count++;Assert(count>5000);areas.Add(count);}Assert(areas.Max()/(double)areas.Min()<1.12);
  });
  Check("SitAndLieExitReverseBeforeNextAction",()=>{
   foreach(var pose in new[]{PetAction.Sit,PetAction.LieDown}){pet.Sprite.Restart(pose,0,true);pet.Sprite.Tick(pose,2,false,false);Assert(pet.Sprite.CurrentFrame==7);pet.Sprite.Restart(PetAction.Walk,2);pet.Sprite.Tick(PetAction.Walk,2.2,false,false);Assert(pet.Sprite.IsTransitioning&&pet.Sprite.CurrentAction==pose&&pet.Sprite.CurrentFrame<7);pet.Sprite.Tick(PetAction.Walk,3.5,false,false);pet.Sprite.Tick(PetAction.Walk,3.5+WindowsAiDog.Windows.Rendering.SpriteVisual.BlendSeconds+.01,false,false);Assert(!pet.Sprite.IsTransitioning&&pet.Sprite.CurrentAction==PetAction.Walk);}
  });

  Check("ContinuousIdlePoseTransitionsAndLoopSeam",()=>{
   Directory.CreateDirectory("artifacts/transition-frames");int index=0;double now=0;
   void Frame(PetAction action,double seconds){pet.Sprite.Tick(action,seconds,false,false);pet.Sprite.UpdateLayout();Capture(pet.Sprite,$"artifacts/transition-frames/{index++:D3}.png");}
   pet.Sprite.Restart(PetAction.WagTail,now,true);
   for(int i=0;i<16;i++){Frame(PetAction.WagTail,now);now+=0.125;}
   foreach(var pose in new[]{PetAction.TiltHead,PetAction.Sit,PetAction.LieDown}){
    pet.Sprite.Restart(pose,now);for(int i=0;i<12;i++){Frame(pose,now);now+=1.0/6;}
    pet.Sprite.Restart(PetAction.WagTail,now);for(int i=0;i<16;i++){Frame(PetAction.WagTail,now);now+=0.125;}
    Assert(!pet.Sprite.IsTransitioning&&pet.Sprite.CurrentAction==PetAction.WagTail);
   }
   File.WriteAllText("artifacts/transition-timeline.txt","WPF renderer timeline: 16 idle loop frames; tilt, sit, lie each 12 forward/held frames + 16 return/idle frames; fixed per-clip scaling; no per-frame fitting. Rendered simulation, not desktop screen recording.");
  });

  Check("LateReplyAfterProviderChangeCannotCommit",()=>{
   var fake=new LateProvider();var instance=new PetWindow(Path.Combine(root,"late-provider"),fake,fullscreenProbe:_=>false,soundPlayback:new PlaybackRecorder());instance.Show();instance.ApplySettings(instance.Settings with{Muted=true,FocusMode=true});
   var window=new ChatWindow(instance);window.Show();window.UpdateLayout();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var models=(System.Windows.Controls.ComboBox)typeof(ChatWindow).GetField("models",flags)!.GetValue(window)!;models.ItemsSource=new[]{new WindowsAiDog.Windows.Chat.ModelChoice("fake","fake")};models.SelectedIndex=0;
   ((System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",flags)!.GetValue(window)!).Text="late";
   var task=(Task)typeof(ChatWindow).GetMethod("Send",flags)!.Invoke(window,null)!;Assert(!task.IsCompleted);instance.Providers.Select("local");fake.Reply.SetResult(new PetReply("SHOULD_NOT_APPEAR",PetAction.Jump));
   var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};var limit=System.Diagnostics.Stopwatch.StartNew();timer.Tick+=(s,e)=>{if(task.IsCompleted||limit.Elapsed.TotalSeconds>3)frame.Continue=false;};
   try{timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);Assert(task.IsCompletedSuccessfully);var history=(List<WindowsAiDog.Windows.Chat.ConversationTurn>)typeof(ChatWindow).GetField("history",flags)!.GetValue(window)!;var transcript=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("transcript",flags)!.GetValue(window)!;Assert(history.Count==0&&!transcript.Text.Contains("SHOULD_NOT_APPEAR")&&instance.Sprite.CurrentAction!=PetAction.Jump);}
   finally{timer.Stop();window.Close();instance.Close();}
  });

  Check("TailLoopSeamAndPoseBoundaryDoNotJump",()=>{
   byte[] Pixels(){pet.Sprite.UpdateLayout();var bitmap=new RenderTargetBitmap(224,210,96,96,PixelFormats.Pbgra32);bitmap.Render(pet.Sprite);var bytes=new byte[224*210*4];bitmap.CopyPixels(bytes,224*4,0);return bytes;}
   pet.Sprite.Restart(PetAction.WagTail,0,true);double cycle=pet.Sprite.CycleSeconds;pet.Sprite.Tick(PetAction.WagTail,cycle-.001,false,false);var a=Pixels();pet.Sprite.Tick(PetAction.WagTail,cycle+.001,false,false);var b=Pixels();double difference=a.Zip(b,(x,y)=>Math.Abs(x-y)).Average();double maxAdjacent=0;pet.Sprite.Restart(PetAction.WagTail,0,true);pet.Sprite.Tick(PetAction.WagTail,0,false,false);var prior=Pixels();for(int i=1;i<8;i++){pet.Sprite.Tick(PetAction.WagTail,(i+.01)*cycle/8,false,false);var next=Pixels();maxAdjacent=Math.Max(maxAdjacent,prior.Zip(next,(x,y)=>Math.Abs(x-y)).Average());prior=next;}Console.WriteLine("SEAM difference="+difference+" maxAdjacent="+maxAdjacent);Assert(difference<=maxAdjacent+0.01);
   foreach(var pose in new[]{PetAction.TiltHead,PetAction.Sit,PetAction.LieDown}){pet.Sprite.Restart(PetAction.WagTail,0,true);pet.Sprite.Tick(PetAction.WagTail,0,false,false);var before=Pixels();pet.Sprite.Restart(pose,0);pet.Sprite.Tick(pose,0,false,false);Console.WriteLine("BOUNDARY "+pose+" equal="+before.SequenceEqual(Pixels())+" transition="+pet.Sprite.IsTransitioning);Assert(before.SequenceEqual(Pixels())&&pet.Sprite.IsTransitioning);pet.Sprite.Tick(pose,WindowsAiDog.Windows.Rendering.SpriteVisual.BlendSeconds+.02,false,false);Assert(!pet.Sprite.IsTransitioning&&pet.Sprite.CurrentFrame==0);}
   File.WriteAllText("artifacts/tail-seam-evidence.json",System.Text.Json.JsonSerializer.Serialize(new{seamMeanAbsoluteByteDifference=difference,tailMotion="whole generated frames, no body/tail compositing; byte difference is a discontinuity bound, not a naturalness verdict",transitionSeconds=WindowsAiDog.Windows.Rendering.SpriteVisual.BlendSeconds,firstTransitionPixelsMatchPrevious=true}));
  });
  Check("ActualNativeTreatPlaybackLifecycle",()=>{
   using var native=new WindowsAiDog.Windows.Audio.DogSoundPlayback(Path.Combine(AppContext.BaseDirectory,"assets","sounds"));var opened=new List<SoundCue>();int ended=0;native.PlaybackOpened+=cue=>opened.Add(cue);native.PlaybackEnded+=()=>ended++;
   var instance=new PetWindow(Path.Combine(root,"native-treat"),fullscreenProbe:_=>false,soundPlayback:native);instance.Show();instance.ApplySettings(instance.Settings with{Muted=false,FocusMode=false,Volume=20,SoundIntervalSeconds=60});instance.Request(PetAction.Treat);
   var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};timer.Tick+=(s,e)=>frame.Continue=false;
   try{timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);File.WriteAllText("artifacts/native-treat-playback-evidence.json",System.Text.Json.JsonSerializer.Serialize(new{opened=opened.Select(c=>c.ToString()),ended,volume=20,injectedRecorder=false,nativeMediaPlayer=true,subjectiveAudition=false}));Assert(opened.SequenceEqual(new[]{SoundCue.Bark})&&ended>=1&&instance.LastTreatBarkPlayed);}
   finally{timer.Stop();instance.Close();}
  });
  Check("TreatCompletionMuteFocusAndHiddenTakePriority",()=>{
   foreach(string policy in new[]{"mute","focus","hidden"}){
    var recorder=new PlaybackRecorder();var instance=new PetWindow(Path.Combine(root,"suppress-"+policy),fullscreenProbe:_=>false,soundPlayback:recorder);instance.Show();instance.ApplySettings(instance.Settings with{Muted=false,FocusMode=false});instance.Request(PetAction.Treat);
    if(policy=="mute")instance.ApplySettings(instance.Settings with{Muted=true});
    else if(policy=="focus")instance.ApplySettings(instance.Settings with{FocusMode=true});
    else SendMessage(new WindowInteropHelper(instance).Handle,0x2b1,new IntPtr(7),IntPtr.Zero);
    var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1700)};timer.Tick+=(s,e)=>frame.Continue=false;
    try{timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);Assert(!recorder.Cues.Contains(SoundCue.Bark)&&!instance.LastTreatBarkPlayed);}
    finally{timer.Stop();instance.Close();}
   }
  });

  Check("HitMaskMatchesVisibleRotationAndTransitionPixels",()=>{
   var method=typeof(WindowsAiDog.Windows.Rendering.SpriteVisual).GetMethod("HitTestCore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)!;int compared=0;
   foreach(var state in new[]{(PetAction.WagTail,.25),(PetAction.WagTail,.75),(PetAction.TiltHead,.09),(PetAction.Sit,.09),(PetAction.LieDown,.09),(PetAction.LieDown,1.6)}){
    pet.Sprite.Restart(PetAction.WagTail,0,true);pet.Sprite.Tick(PetAction.WagTail,0,false,false);if(state.Item1!=PetAction.WagTail)pet.Sprite.Restart(state.Item1,0);pet.Sprite.Tick(state.Item1,state.Item2,false,false);pet.Sprite.UpdateLayout();
    var bitmap=new RenderTargetBitmap(224,210,96,96,PixelFormats.Pbgra32);bitmap.Render(pet.Sprite);var pixels=new byte[224*210*4];bitmap.CopyPixels(pixels,224*4,0);
    for(int y=0;y<210;y+=3)for(int x=0;x<224;x+=3){var hit=method.Invoke(pet.Sprite,new object[]{new PointHitTestParameters(new Point(x+.1,y+.1))});Assert((hit is not null)==(pixels[(y*224+x)*4+3]>8));compared++;}
   }File.WriteAllText("artifacts/hit-mask-evidence.json",System.Text.Json.JsonSerializer.Serialize(new{compared,transparentPixelClickThrough=true,rotationAndBlend=true}));
  });

  Check("GroundedPoseFeetKeepExactSourceBaseline",()=>{
   var catalog=WindowsAiDog.Windows.Rendering.SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v6"));
   foreach(var action in new[]{PetAction.WagTail,PetAction.TiltHead,PetAction.Sit,PetAction.LieDown}){
    var clip=catalog.Clips[action];var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(clip.Image);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();
    foreach(var f in clip.Frames){var crop=new CroppedBitmap(image,new Int32Rect(f.X,f.Y,444,444));var bitmap=new FormatConvertedBitmap(crop,PixelFormats.Bgra32,null,0);var pixels=new byte[444*444*4];bitmap.CopyPixels(pixels,444*4,0);int bottom=-1;for(int y=0;y<444;y++)for(int x=0;x<444;x++)if(pixels[(y*444+x)*4+3]>8)bottom=y;Assert(bottom==404);}
   }
  });

  Check("V4PetShowsOnlyItsName",()=>{
   var canvas=(System.Windows.Controls.Canvas)pet.Content;
   // The name sits in a small tag (Border) under the dog; it is still the only text on the pet.
   var texts=canvas.Children.OfType<System.Windows.Controls.TextBlock>().Concat(canvas.Children.OfType<System.Windows.Controls.Border>().Select(b=>b.Child).OfType<System.Windows.Controls.TextBlock>());
   Assert(texts.Single().Text==pet.Settings.Name);
  });
  Check("V4ConversationIsOneCompactInput",()=>{
   var window=new ChatWindow(pet);window.Show();window.UpdateLayout();
   try{Assert(window.Width<=340&&window.ActualHeight<=85);}finally{window.Close();}
  });
  Check("V4LongKoreanBubbleHasScrollableWholeText",()=>{
   var text=string.Concat(Enumerable.Repeat("긴 한국어 답변은 잘리지 않고 끝까지 읽을 수 있어야 합니다. ",40));
   var window=new BubbleWindow(text,()=>{});window.Show();window.UpdateLayout();
   IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
   try{Assert(Children(window).OfType<System.Windows.Controls.ScrollViewer>().Any(v=>v.ScrollableHeight>0));}finally{window.Close();}
  });
  Check("V4JumpEndsOnItsOriginalGround",()=>{
   int Bottom(PetAction action,double time){pet.Sprite.Restart(action,0,true);pet.Sprite.Tick(action,time,false,false);pet.Sprite.UpdateLayout();var b=new RenderTargetBitmap(224,210,96,96,PixelFormats.Pbgra32);b.Render(pet.Sprite);var bytes=new byte[224*210*4];b.CopyPixels(bytes,224*4,0);for(int y=209;y>=0;y--)for(int x=0;x<224;x++)if(bytes[(y*224+x)*4+3]>16)return y;return -1;}
   Assert(Math.Abs(Bottom(PetAction.WagTail,0)-Bottom(PetAction.Jump,2.8))<=2);
  });


  Check("V4CanceledJumpLandsBeforeNextAction",()=>{
   pet.Sprite.Restart(PetAction.Jump,0,true);pet.Sprite.Tick(PetAction.Jump,.7,false,false);Assert(pet.Sprite.JumpHeight>30);
   pet.Sprite.Restart(PetAction.Walk,.7);pet.Sprite.Tick(PetAction.Walk,.8,false,false);Assert(pet.Sprite.CurrentAction==PetAction.Jump&&pet.Sprite.IsTransitioning&&pet.Sprite.JumpHeight>0);
   pet.Sprite.Tick(PetAction.Walk,1.06,false,false);Assert(pet.Sprite.CurrentAction==PetAction.Walk&&pet.Sprite.JumpHeight==0);
  });
  Check("V4LocalEmotionDoesNotOverwriteAiReply",()=>{
   // Earlier visibility tests leave the shared input open. This case requires no typing lock.
   pet.Conversation.CloseInput();pet.ApplySettings(pet.Settings with{FocusMode=false});Assert(pet.IsVisible&&!pet.Conversation.MovementLocked);
   pet.SayAi("AI 답변은 유지됩니다.");var first=pet.CurrentBubble;Assert(first?.IsVisible==true);pet.Say("로컬 감정");
   if(!ReferenceEquals(first,pet.CurrentBubble)||pet.CurrentBubble?.Kind!=BubbleKind.Ai)throw new Exception("AI priority assertion failed");
   first?.Close();pet.Say("로컬 감정");
   if(pet.CurrentBubble?.Kind!=BubbleKind.Emotion)throw new Exception("emotion resumption assertion failed");pet.CurrentBubble?.Close();
  });
  Check("V6TypingSuppressesEmotionUntilInputCloses",()=>{
   var instance=new PetWindow(Path.Combine(root,"bubble-priority"),fullscreenProbe:_=>false,soundPlayback:new PlaybackRecorder());instance.Show();instance.ApplySettings(instance.Settings with{FocusMode=false,Muted=true});
   try{
    instance.OpenChat();var input=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(instance.Conversation)!;input.Text="입력 중";Assert(instance.Conversation.MovementLocked);
    instance.Say("로컬 감정");Assert(instance.CurrentBubble is null);
    instance.SayAi("입력 중에도 AI 답변은 읽을 수 있습니다.");var first=instance.CurrentBubble;Assert(first?.Kind==BubbleKind.Ai&&first.IsVisible);instance.Say("로컬 감정");Assert(ReferenceEquals(first,instance.CurrentBubble));
    first!.UpdateLayout();Capture((FrameworkElement)first.Content,"artifacts/bubble-ai-during-input.png");first.Close();instance.Say("로컬 감정");Assert(instance.CurrentBubble is null&&instance.Conversation.MovementLocked);
    instance.Conversation.CloseInput();Assert(!instance.Conversation.MovementLocked);instance.Say("입력 종료 후 로컬 감정");Assert(instance.CurrentBubble?.Kind==BubbleKind.Emotion&&instance.CurrentBubble.IsVisible);
    instance.CurrentBubble!.UpdateLayout();Capture((FrameworkElement)instance.CurrentBubble.Content,"artifacts/bubble-emotion-after-input.png");
   }finally{instance.Close();}
  });
  Check("V4TypingStopsMovementWithoutCallingAi",()=>{
   pet.ApplySettings(pet.Settings with{FocusMode=false});pet.OpenChat();var input=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(pet.Conversation)!;input.Text="입력 중";
   pet.Request(PetAction.Walk);pet.Sprite.Restart(PetAction.Walk,0,true);var before=pet.Left;
   var tick=typeof(PetWindow).GetMethod("TickAt",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
   tick.Invoke(pet,new object[]{.16});tick.Invoke(pet,new object[]{1.16});Assert(pet.Left==before&&pet.Sprite.CurrentFrame==0);
   input.Clear();pet.Conversation.CloseInput();tick.Invoke(pet,new object[]{1.8});Assert(pet.Left==before&&pet.Sprite.CurrentFrame==0);
   tick.Invoke(pet,new object[]{1.85});tick.Invoke(pet,new object[]{1.95});tick.Invoke(pet,new object[]{2.05});Assert(pet.Sprite.CurrentFrame==(int)Math.Round(.25*8/pet.Sprite.CycleSeconds,9));
  });
  Check("V4CatalogContainsSixteenJumpFrames",()=>{
   var catalog=WindowsAiDog.Windows.Rendering.SpriteCatalog.Load(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v7"));Assert(catalog.Clips.Values.Sum(c=>c.Frames.Count)==72&&catalog.Clips[PetAction.Jump].Frames.Count==16);
  });


  Check("V4AiBubbleReturnsAfterTemporaryHide",()=>{
   pet.ApplySettings(pet.Settings with{FocusMode=false});pet.SayAi("복귀 때도 읽을 수 있어야 합니다.");
   SendMessage(hwnd,0x2b1,new IntPtr(7),IntPtr.Zero);Assert(pet.CurrentBubble?.IsVisible==false);
   SendMessage(hwnd,0x2b1,new IntPtr(8),IntPtr.Zero);Assert(pet.CurrentBubble?.IsVisible==true);pet.CurrentBubble?.Close();
  });


  Check("V5StartupInputStaysHidden",()=>{
   var instance=new PetWindow(Path.Combine(root,"v5-start"),fullscreenProbe:_=>false,soundPlayback:new PlaybackRecorder());instance.Show();instance.UpdateLayout();
   var frame=new System.Windows.Threading.DispatcherFrame();instance.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false),System.Windows.Threading.DispatcherPriority.ContextIdle);System.Windows.Threading.Dispatcher.PushFrame(frame);
   try{Assert(!instance.Conversation.IsVisible);instance.Say("감정");Assert(!instance.Conversation.IsVisible);SendMessage(new WindowInteropHelper(instance).Handle,0x2b1,new IntPtr(7),IntPtr.Zero);SendMessage(new WindowInteropHelper(instance).Handle,0x2b1,new IntPtr(8),IntPtr.Zero);Assert(!instance.Conversation.IsVisible);}finally{instance.Close();}
  });
  Check("V5EscapeClosesExplicitInput",()=>{
   pet.OpenChat();var window=pet.Conversation;var input=(System.Windows.Controls.TextBox)typeof(ChatWindow).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
   input.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Escape){RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent});Assert(!window.IsVisible&&!window.MovementLocked);
  });
  Check("V5TreatRequestMakesNoEarlySound",()=>{
   var recorder=new PlaybackRecorder();var instance=new PetWindow(Path.Combine(root,"v5-treat"),fullscreenProbe:_=>false,soundPlayback:recorder);instance.Show();instance.ApplySettings(instance.Settings with{Muted=false,FocusMode=false,SoundIntervalSeconds=15});instance.Request(PetAction.Treat);try{Assert(recorder.Cues.Count==0);}finally{instance.Close();}
  });
  Check("CompanionsAttachToTheDrawnDog",()=>{
   var instance=new PetWindow(Path.Combine(root,"companions"),fullscreenProbe:_=>false,soundPlayback:new PlaybackRecorder());instance.Show();instance.ApplySettings(instance.Settings with{FocusMode=false,Muted=true});
   void Pump(int ms){var f=new System.Windows.Threading.DispatcherFrame();var t=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();System.Windows.Threading.Dispatcher.PushFrame(f);}
   var place=typeof(PetWindow).GetMethod("PositionCompanions",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var area=SystemParameters.WorkArea;
   try{
    foreach(var (name,top) in new[]{("middle",area.Top+area.Height/2-110),("bottom",area.Bottom-220)}){
     instance.Left=area.Left+area.Width/2-112;instance.Top=top;Pump(150);
     instance.SayAi("응~ 마고 여기 있어! 불렀어?");instance.OpenChat();Pump(450);place.Invoke(instance,null);Pump(150);
     var bubble=instance.CurrentBubble!;var chat=instance.Conversation;var dog=instance.Sprite.DogBounds;
     double dogTop=instance.Top+dog.Top,dogBottom=instance.Top+dog.Bottom,dogLeft=instance.Left+dog.Left,dogRight=instance.Left+dog.Right;
     // Bubble tail ends just above the head; the input sits under the dog or right beside its body.
     Assert(Math.Abs(bubble.Top+bubble.ActualHeight-dogTop)<=24);
     bool under=chat.Top>=dogBottom-4&&chat.Top<=dogBottom+24;bool beside=Math.Abs(chat.Left-dogRight)<=24||Math.Abs(chat.Left+chat.ActualWidth-dogLeft)<=24;
     Assert(under||beside);
     // Picture of the arrangement (window contents only, no desktop) for review.
     double minX=Math.Min(instance.Left,Math.Min(bubble.Left,chat.Left)),minY=Math.Min(instance.Top,Math.Min(bubble.Top,chat.Top));
     double maxX=Math.Max(instance.Left+instance.ActualWidth,Math.Max(bubble.Left+bubble.ActualWidth,chat.Left+chat.ActualWidth)),maxY=Math.Max(instance.Top+instance.ActualHeight,Math.Max(bubble.Top+bubble.ActualHeight,chat.Top+chat.ActualHeight));
     var bar=(FrameworkElement)chat.Content;bar.BeginAnimation(UIElement.OpacityProperty,null);bar.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,null);
     var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x7C,0x84,0xC9)),null,new Rect(0,0,maxX-minX,maxY-minY));
      foreach(var w in new Window[]{instance,bubble,chat}){var c=(FrameworkElement)w.Content;c.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(c.ActualWidth),(int)Math.Ceiling(c.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(c);dc.DrawImage(b,new Rect(w.Left-minX,w.Top-minY,c.ActualWidth,c.ActualHeight));}}
     var sheet=new RenderTargetBitmap((int)Math.Ceiling(maxX-minX),(int)Math.Ceiling(maxY-minY),96,96,PixelFormats.Pbgra32);sheet.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sheet));using(var file=File.Create($"artifacts/companions-{name}.png"))png.Save(file);
     bubble.Close();chat.CloseInput();Pump(100);
    }
   }finally{instance.Close();}
  });
  Check("V5BubbleHasDogPointingTail",()=>{
   var window=new BubbleWindow("둥근 말풍선",()=>{});window.Show();window.UpdateLayout();
   IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
   try{Assert(Children(window).OfType<System.Windows.Shapes.Path>().Any(p=>p.Data is not null&&p.ActualHeight>=10));}finally{window.Close();}
  });

  var filter=Environment.GetEnvironmentVariable("WINDOWSAIDOG_UI_TEST_FILTER");
  if(!string.IsNullOrWhiteSpace(filter)){var names=filter.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);tests=tests.Where(t=>names.Contains(t.Item1,StringComparer.Ordinal)).ToList();if(tests.Count==0)throw new InvalidOperationException("No UI tests matched filter");}
  foreach(var (name,test) in tests){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
  pet.Close();app.Shutdown();
  // The GUID folder was created by this runner and contains only its own test data.
  if(root.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);
  Console.WriteLine($"RESULT {tests.Count-failures}/{tests.Count} passed");
  return failures==0?0:1;
 }
}

sealed class PlaybackRecorder:WindowsAiDog.Windows.Audio.ILocalPlayback {public List<SoundCue> Cues=new();public void Play(SoundCue cue,int volume)=>Cues.Add(cue);public void Stop(){}}

sealed class LateProvider:WindowsAiDog.Windows.Chat.IChatProvider,WindowsAiDog.Windows.Chat.IChatAuth,WindowsAiDog.Windows.Chat.IChatClient {
 public TaskCompletionSource<PetReply> Reply {get;}=new();public string Id=>"chatgpt";public string DisplayName=>"fake";public WindowsAiDog.Windows.Chat.BillingMode Billing=>WindowsAiDog.Windows.Chat.BillingMode.SubscriptionSharing;
 public WindowsAiDog.Windows.Chat.IChatAuth Auth=>this;public WindowsAiDog.Windows.Chat.IChatClient Client=>this;public bool Connected=>true;
 public Task ConnectAsync(CancellationToken ct)=>Task.CompletedTask;public Task<string> AccessTokenAsync(CancellationToken ct)=>Task.FromResult("FAKE");
 public Task<IReadOnlyList<WindowsAiDog.Windows.Chat.ModelChoice>> ModelsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<WindowsAiDog.Windows.Chat.ModelChoice>>([]);
 public Task<bool> DisconnectAsync(CancellationToken ct)=>Task.FromResult(true);
 public Task<PetReply> SendAsync(string token,string model,string userText,string petName,CancellationToken ct,IReadOnlyList<WindowsAiDog.Windows.Chat.ConversationTurn>? history=null)=>Reply.Task;
}
