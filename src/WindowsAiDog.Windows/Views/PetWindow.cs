using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using WindowsAiDog.Core;
using WindowsAiDog.Windows.Interop;
using WindowsAiDog.Windows.Audio;
using WindowsAiDog.Windows.Storage;
using WindowsAiDog.Windows.Chat;
using WindowsAiDog.Windows.Rendering;
namespace WindowsAiDog.Windows.Views;
public sealed class PetWindow:Window {
 private readonly Stopwatch clock=Stopwatch.StartNew();
 private readonly DispatcherTimer animation=new(){Interval=TimeSpan.FromMilliseconds(33)};
 private readonly DispatcherTimer visibility=new(){Interval=TimeSpan.FromSeconds(1)};
 private readonly MonitorService monitors=new();
 private readonly Func<IntPtr,bool> fullscreenProbe;
 private readonly SettingsStore settingsStore;
 private readonly DogSoundPlayback playback;
 private readonly LocalSoundPlayer sound;
 private readonly BehaviorController behavior;
 private readonly System.Net.Http.HttpClient http;
 private AppSettings settings;
 private readonly Canvas canvas=new(){Width=224,Height=220};
 private readonly SpriteVisual sprite;
 private readonly TextBlock caption=new(){Foreground=new SolidColorBrush(Color.FromRgb(0x4A,0x34,0x26)),FontSize=11,IsHitTestVisible=false};
 // Small name tag that sits just under the dog's paws; hidden while the chat input is open.
 private readonly Border captionPill=new(){Background=new SolidColorBrush(Color.FromArgb(215,0xFF,0xFB,0xF5)),BorderBrush=new SolidColorBrush(Color.FromRgb(0xEE,0xCF,0xAE)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(7,0,7,1),IsHitTestVisible=false};
 private IntPtr hwnd;
 private bool locked,suppressed,dragging,manualHidden;
 private Point dragOffset;
 private double direction=1,nextRandom,lastTick,motionX=double.NaN;
 private ChatWindow? chat;private bool inputRequested;
 private readonly bool diagnosticTrace; private long traceRows;
 private void Trace(string kind,SoundCue? cue=null){if(!diagnosticTrace||traceRows++>6000)return;try{NativeMethods.GetWindowRect(hwnd,out var pos);File.AppendAllText(Path.Combine(DataDirectory,"runtime-trace.jsonl"),System.Text.Json.JsonSerializer.Serialize(new{t=clock.Elapsed.TotalSeconds,kind,action=sprite.CurrentAction.ToString(),requested=behavior.State.Action?.ToString(),frame=sprite.CurrentFrame,treatEaten=sprite.TreatEaten,cue=cue?.ToString(),x=pos.Left,y=pos.Top,inputVisible=chat?.IsVisible==true,movementLocked=chat?.MovementLocked==true,travel=sprite.TravelPixelsPerSecond})+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}
 private SettingsWindow? settingsWindow;
 private BubbleWindow? bubble; private string? lastAiReply; public BubbleWindow? CurrentBubble=>bubble; public ChatWindow Conversation=>chat??CreateConversation();
 private ChatWindow CreateConversation(){chat=new(this);chat.Closed+=(s,e)=>chat=null;return chat;}
 private TrayIcon? tray;
 private readonly Random random=new();
 public ProviderManager Providers {get;}
 public IChatProvider Provider=>Providers.Current;
 private long treatId; private double? treatDone;
 public bool LastTreatBarkPlayed {get;private set;}
 public IChatAuth Auth=>Provider.Auth;
 public IChatClient Chat=>Provider.Client;
 public AppSettings Settings=>settings with{Affinity=behavior.State.Affinity};
 public string DataDirectory {get;}
 public SpriteVisual Sprite=>sprite;
 public bool TrayRegistered=>tray?.Registered==true;
 public PetWindow(string? dataDirectory=null,IChatProvider? provider=null,Func<IntPtr,bool>? fullscreenProbe=null,ILocalPlayback? soundPlayback=null,bool diagnosticTrace=false) {
  this.diagnosticTrace=diagnosticTrace;
  this.fullscreenProbe=fullscreenProbe??MonitorService.Fullscreen;
  Title="강아지";Width=224;Height=220;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
  Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;Content=canvas;
  DataDirectory=dataDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WindowsAiDog");
  settingsStore=new(DataDirectory);settings=settingsStore.Load();behavior=new(new(settings.Name,settings.Affinity));
  behavior.Apply(new(PetEventKind.SetFocus,settings.FocusMode),clock.Elapsed);
  spriteFolder=Path.Combine(AppContext.BaseDirectory,"assets",Directory.Exists(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v9"))?"sprites-v9":Directory.Exists(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v8"))?"sprites-v8":Directory.Exists(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v7"))?"sprites-v7":Directory.Exists(Path.Combine(AppContext.BaseDirectory,"assets","sprites-v6"))?"sprites-v6":"sprites");sprite=new(spriteFolder);canvas.Children.Add(sprite);
  playback=new(Path.Combine(AppContext.BaseDirectory,"assets","sounds"));playback.PlaybackOpened+=cue=>Trace("audio-opened",cue);playback.PlaybackEnded+=()=>Trace("audio-ended");sound=new(soundPlayback??playback);
  http=new(new System.Net.Http.HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60),MaxResponseContentBufferSize=1024*1024};
  Providers=new(http,DataDirectory,provider);
  captionPill.Child=caption;canvas.Children.Add(captionPill);UpdateCaption();canvas.ContextMenu=Menu();
  MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){OpenChat();return;}Stroke();dragging=true;dragOffset=e.GetPosition(this);CaptureMouse();};
  MouseMove+=(s,e)=>{if(dragging&&e.LeftButton==MouseButtonState.Pressed){var p=PointToScreen(e.GetPosition(this));var dpi=VisualTreeHelper.GetDpi(this);MoveTo(p.X-dragOffset.X*dpi.DpiScaleX,p.Y-dragOffset.Y*dpi.DpiScaleY);}};
  MouseLeftButtonUp+=(s,e)=>{dragging=false;ReleaseMouseCapture();Save();};
  SourceInitialized+=(s,e)=>{
   hwnd=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(hwnd).AddHook(Hook);
   long style=NativeMethods.GetWindowLongPtr(hwnd,-20).ToInt64();NativeMethods.SetWindowLongPtr(hwnd,-20,new IntPtr(style|0x08000000|0x80));
   tray=new(hwnd,Path.Combine(AppContext.BaseDirectory,"assets","dog.ico"));
   NativeMethods.WTSRegisterSessionNotification(hwnd,0);MoveTo(settings.X,settings.Y);UpdateSound();PollVisibility();
  };
  animation.Tick+=(s,e)=>Tick();visibility.Tick+=(s,e)=>PollVisibility();
  Loaded+=(s,e)=>{visibility.Start();if(!suppressed)animation.Start();PositionCompanions();Trace("loaded");};
  LocationChanged+=(s,e)=>PositionCompanions();
  Closing+=(s,e)=>{Save();animation.Stop();visibility.Stop();sound.Stop();bubble?.Close();chat?.Close();settingsWindow?.Close();tray?.Dispose();NativeMethods.WTSUnRegisterSessionNotification(hwnd);playback.Dispose();http.Dispose();};
 }
 private ContextMenu Menu() {
  var menu=new ContextMenu();
  MenuItem Item(string label,Action command){var item=new MenuItem{Header=label};item.Click+=(s,e)=>command();menu.Items.Add(item);return item;}
  Item("대화 입력",OpenChat);Item("최근 AI 답변 다시 보기",()=>{if(lastAiReply is not null)SayAi(lastAiReply);});Item("쓰다듬기",Stroke);Item("간식",()=>Request(PetAction.Treat));
  // Quick toggles mirror the settings window; the check mark shows the current state.
  var focus=Item("집중 모드",()=>ApplySettings(Settings with{FocusMode=!settings.FocusMode}));
  var mute=Item("음소거",()=>ApplySettings(Settings with{Muted=!settings.Muted}));
  menu.Opened+=(s,e)=>{focus.IsChecked=settings.FocusMode;mute.IsChecked=settings.Muted;};
  Item("관리 설정 · 이름 / AI / 소리",OpenSettings);
  var actions=new MenuItem{Header="동작 선택"};foreach(var value in Enum.GetValues<PetAction>()){var item=new MenuItem{Header=ActionLabel(value)};item.Click+=(s,e)=>Request(value);actions.Items.Add(item);}menu.Items.Add(actions);
  Item("강아지 숨기기 / 보이기",()=>{if(tray?.Registered!=true){Say("트레이 아이콘을 확인하지 못해 숨기지 않았어요.");return;}manualHidden=!manualHidden;PollVisibility();});
  Item("화면 안으로 다시 가져오기",()=>{manualHidden=false;MoveTo(double.NaN,double.NaN);PollVisibility();});
  Item("앱 종료",()=>Application.Current.Shutdown());return menu;
 }
 private static string ActionLabel(PetAction a)=>a switch{PetAction.WagTail=>"꼬리 흔들기",PetAction.Walk=>"걷기",PetAction.Run=>"달리기",PetAction.Jump=>"점프",PetAction.Treat=>"간식",PetAction.Sit=>"앉기",PetAction.LieDown=>"엎드리기",_=>"고개 기울이기"};
 private IntPtr Hook(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled) {
  if(tray is not null&&(uint)message==tray.TaskbarCreated)tray.Add();
  if(message==TrayIcon.Callback) {
   if(l.ToInt32()==0x203){manualHidden=false;PollVisibility();OpenChat();}
   if(l.ToInt32()==0x205){canvas.ContextMenu!.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;canvas.ContextMenu.IsOpen=true;}
  }
  if(message==0x21){handled=true;return new IntPtr(3);}
  if(message==0x84){long point=l.ToInt64();var local=PointFromScreen(new Point((short)(point&0xffff),(short)((point>>16)&0xffff)));var hit=canvas.InputHitTest(local);if(hit is null||ReferenceEquals(hit,canvas)){handled=true;return new IntPtr(-1);}}
  if(message==0x02b1){if(w.ToInt32()==7)locked=true;if(w.ToInt32()==8)locked=false;PollVisibility();}
  if(message==0x02e0||message==0x007e)Dispatcher.BeginInvoke(new Action(()=>{if(NativeMethods.GetWindowRect(hwnd,out var r))MoveTo(r.Left,r.Top);}));
  return IntPtr.Zero;
 }
 private void PollVisibility() {
  if(hwnd==IntPtr.Zero)return;
  bool hide=manualHidden||locked||fullscreenProbe(hwnd);bool previous=suppressed;suppressed=hide||settings.FocusMode;UpdateSound();
  if(suppressed!=previous) {
   behavior.Apply(new(PetEventKind.SetFocus,suppressed),clock.Elapsed);
   sprite.Restart(PetAction.WagTail,clock.Elapsed.TotalSeconds,true);
   sprite.Tick(PetAction.WagTail,clock.Elapsed.TotalSeconds,false,false);sprite.UpdateLayout();
   lastTick=clock.Elapsed.TotalSeconds;nextRandom=lastTick+6;
  }
  if(suppressed)treatDone=null;
  if(hide){animation.Stop();Hide();bubble?.Hide();chat?.Hide();settingsWindow?.Hide();}
  else{if(!IsVisible)Show();if(inputRequested&&chat is not null&&!chat.IsVisible)chat.Show();if(bubble is not null&&!bubble.IsVisible)bubble.Show();PositionCompanions();if(settings.FocusMode)animation.Stop();else if(!animation.IsEnabled){lastTick=clock.Elapsed.TotalSeconds;animation.Start();}}
  UpdateCaption();
 }
 private string spriteFolder="";
 // Head crop of the first idle frame, used as the chat input avatar. Null when the art is missing.
 public System.Windows.Media.ImageSource? FaceImage(){
  try{var path=Path.Combine(spriteFolder,"idle.png");if(!File.Exists(path))return null;var b=new System.Windows.Media.Imaging.BitmapImage();b.BeginInit();b.UriSource=new Uri(path);b.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;b.EndInit();b.Freeze();
   var face=new System.Windows.Media.Imaging.CroppedBitmap(b,new Int32Rect(232,128,120,120));face.Freeze();return face;}
  catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException){return null;}
 }
 private void UpdateCaption(){
  caption.Text=settings.Name;captionPill.Visibility=chat?.IsVisible==true?Visibility.Collapsed:Visibility.Visible;
  // Fixed on the ground line under the paws: following the per-frame outline made it shake.
  captionPill.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var size=captionPill.DesiredSize;
  Canvas.SetLeft(captionPill,(224-size.Width)/2);Canvas.SetTop(captionPill,Math.Min(GroundY+2,Height-size.Height));
 }
 private void UpdateSound()=>sound.SetPolicy(new(settings.Volume,settings.Muted,TimeSpan.FromSeconds(settings.SoundIntervalSeconds),suppressed));
 private void Tick()=>TickAt(clock.Elapsed.TotalSeconds);
 private void TickAt(double now) {
  if(suppressed)return;
  if(dragging){if(behavior.State.Action is PetAction.Walk or PetAction.Run)sprite.Tick(behavior.State.Action.Value,now,true,direction<0,.1);lastTick=now;return;}
  lastTick=now;
  behavior.Apply(new(PetEventKind.Tick),TimeSpan.FromSeconds(now));
  if(chat?.MovementLocked!=true&&behavior.State.Action is null&&now>=nextRandom) {
   var choices=new[]{PetAction.WagTail,PetAction.Walk,PetAction.Walk,PetAction.TiltHead,PetAction.Run,PetAction.Jump,PetAction.Sit,PetAction.LieDown};Request(choices[random.Next(choices.Length)]);nextRandom=now+random.Next(6,13);
  }
  var action=behavior.State.Action??PetAction.WagTail;
  double travelSeconds=sprite.Tick(action,now,chat?.MovementLocked==true&&action is PetAction.Walk or PetAction.Run,direction<0,.1);if(treatDone is not null&&sprite.TreatEaten&&sprite.Completed(PetAction.Treat,now)){treatDone=null;Trace("treat-complete");LastTreatBarkPlayed=sound.TryPlayInteraction(SoundCue.Bark,treatId,clock.Elapsed);if(LastTreatBarkPlayed)Trace("audio-request",SoundCue.Bark);}
  UpdateCaption();if(bubble?.IsVisible==true||chat?.IsVisible==true)PositionCompanions();
  if(chat?.MovementLocked!=true&&!sprite.IsTransitioning&&(behavior.State.Action is PetAction.Walk or PetAction.Run||sprite.IsSettling)&&NativeMethods.GetWindowRect(hwnd,out var rect)) {
   double speed=sprite.TravelPixelsPerSecond;var dpi=VisualTreeHelper.GetDpi(this);
   if(!double.IsFinite(motionX)||Math.Abs(rect.Left-Math.Round(motionX))>1)motionX=rect.Left;var desired=new Placement(motionX+direction*speed*travelSeconds*dpi.DpiScaleX,rect.Top);var clamped=monitors.Clamp(desired,rect.Right-rect.Left,rect.Bottom-rect.Top);
   if(Math.Abs(clamped.X-desired.X)>0.1)direction=-direction;motionX=clamped.X;MoveTo(clamped.X,clamped.Y);
  }
  Trace("frame");
 }
 private void MoveTo(double x,double y) {
  if(hwnd==IntPtr.Zero)return;
  NativeMethods.GetWindowRect(hwnd,out var r);var p=monitors.Clamp(new(x,y),Math.Max(1,r.Right-r.Left),Math.Max(1,r.Bottom-r.Top));
  NativeMethods.SetWindowPos(hwnd,IntPtr.Zero,(int)Math.Round(p.X),(int)Math.Round(p.Y),0,0,0x15);PositionCompanions();
 }
 public void Request(PetAction value) {
  if(suppressed||!sprite.Supports(value))return;LastTreatBarkPlayed=false;treatDone=value==PetAction.Treat?clock.Elapsed.TotalSeconds+8.0/6.0:null;if(value==PetAction.Treat)treatId++;var kind=value==PetAction.Treat?PetEventKind.Treat:PetEventKind.RequestAction;
  var d=behavior.Apply(new(kind,Action:value),clock.Elapsed);
  if(d.Action is PetAction action)sprite.Restart(action,clock.Elapsed.TotalSeconds);
  if(value!=PetAction.Treat&&d.Sound is SoundCue cue){if(sound.TryPlay(cue,clock.Elapsed))Trace("audio-request",cue);}Trace("action-request");UpdateCaption();
 }
 public void Stroke(){if(suppressed)return;var d=behavior.Apply(new(PetEventKind.Stroke),clock.Elapsed);if(d.Action is PetAction action)sprite.Restart(action,clock.Elapsed.TotalSeconds);if(d.Sound is SoundCue cue)sound.TryPlay(cue,clock.Elapsed);UpdateCaption();Say("쓰다듬어 줘서 좋아요! ♥");}
 public void ApplySettings(AppSettings value) {
  if(!SettingsValidator.IsValid(value))throw new ArgumentException("설정값을 확인하세요.");
  settings=value;behavior.Apply(new(PetEventKind.SetFocus,value.FocusMode),clock.Elapsed);PollVisibility();Save();
 }
 private void Save() {
  if(hwnd!=IntPtr.Zero&&NativeMethods.GetWindowRect(hwnd,out var r))settings=settings with{X=r.Left,Y=r.Top,Affinity=behavior.State.Affinity};
  try{settingsStore.Save(settings);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
 }
 public void OpenChat(){if(locked)return;if(manualHidden){manualHidden=false;PollVisibility();}inputRequested=true;Conversation.Show();PositionCompanions();Conversation.FocusInput();}
 public void InputClosed(){inputRequested=false;}
 public void OpenSettings(){if(locked)return;if(manualHidden){manualHidden=false;PollVisibility();}if(settingsWindow is not null){settingsWindow.Show();settingsWindow.Activate();return;}settingsWindow=new(this);settingsWindow.Closed+=(s,e)=>settingsWindow=null;settingsWindow.Show();settingsWindow.Activate();}
 public void Say(string text)=>ShowBubble(text,BubbleKind.Emotion);
 public void SayNotice(string text)=>ShowBubble(text,BubbleKind.Notice);
 public void SayAi(string text){lastAiReply=text;ShowBubble(text,BubbleKind.Ai);}
 private void ShowBubble(string text,BubbleKind kind){
  if(locked||manualHidden||!IsVisible||kind==BubbleKind.Emotion&&(suppressed||bubble?.Kind==BubbleKind.Ai||chat?.MovementLocked==true))return;
  bubble?.Close();var created=new BubbleWindow(text,OpenChat,kind);bubble=created;created.Closed+=(s,e)=>{if(ReferenceEquals(bubble,created))bubble=null;};created.Show();PositionCompanions();
 }
 // Stable dog geometry in pet-window DIPs: sprites stand on y=190 (anchor 405 × 0.44), the body is
 // centred near x=112 and about 64 wide each side, chest about 62 above the ground.
 private const double GroundY=190,DogHalfWidth=64,ChestHeight=62;
 private Point? bubbleHead;
 private void PositionCompanions(){
  if(hwnd==IntPtr.Zero||!NativeMethods.GetWindowRect(hwnd,out var pet))return;
  var dpi=VisualTreeHelper.GetDpi(this);
  // Attach to the dog that is drawn, not to the window edges: head for the bubble, body for the input.
  var dog=sprite.DogBounds;if(dog.IsEmpty||dog.Width<=0){dog=new Rect(0,0,sprite.ActualWidth,sprite.ActualHeight);}
  Point Screen(Point p){try{return sprite.PointToScreen(p);}catch(InvalidOperationException){return new Point(pet.Left+p.X*dpi.DpiScaleX,pet.Top+p.Y*dpi.DpiScaleY);}}
  // The bubble follows the head but eases toward it and ignores sub-2px wobble from legs and tail.
  var target=sprite.HeadPoint==default?new Point(dog.Left+dog.Width/2,dog.Top):sprite.HeadPoint;
  if(bubble?.IsVisible!=true||bubbleHead is not Point last||(target-last).Length>60)bubbleHead=target;
  else if((target-last).Length>2)bubbleHead=last+(target-last)*.3;
  var head=Screen(bubbleHead.Value);var ground=Screen(new Point(112,GroundY));double scale=dpi.DpiScaleX;
  bool Bounds(Window? window,out IntPtr handle,out NativeMethods.Rect r){handle=IntPtr.Zero;r=default;if(window is null||!window.IsVisible)return false;handle=new WindowInteropHelper(window).Handle;return handle!=IntPtr.Zero&&NativeMethods.GetWindowRect(handle,out r);}
  void Move(IntPtr handle,NativeMethods.Rect r,Placement p){if(r.Left!=(int)p.X||r.Top!=(int)p.Y)NativeMethods.SetWindowPos(handle,IntPtr.Zero,(int)p.X,(int)p.Y,0,0,0x0001|0x0004|0x0010);}
  if(Bounds(bubble,out var bh,out var br)){
   double w=br.Right-br.Left,h=br.Bottom-br.Top;var placed=monitors.Clamp(new(head.X-w/2,head.Y-h+2*dpi.DpiScaleY),w,h);bool below=true;
   // No room above the head (dog near the top of the screen): show the bubble under the dog instead.
   if(placed.Y>head.Y-h/2){placed=monitors.Clamp(new(head.X-w/2,ground.Y),w,h);below=false;}
   Move(bh,br,placed);var bd=VisualTreeHelper.GetDpi(bubble!);bubble!.PointTail((head.X-placed.X)/bd.DpiScaleX,below);
  }
  if(Bounds(chat,out var ch,out var cr)){
   double w=cr.Right-cr.Left,h=cr.Bottom-cr.Top,gap=4*dpi.DpiScaleY;
   // Prefer right under the dog; when the dog walks along the screen bottom, sit beside it at chest
   // height on the side it faces, falling back to the other side.
   // Anchored to the dog's centre and ground line (not the per-frame outline) so it never jitters.
   var want=new Placement(ground.X-w/2,ground.Y+gap);var placed=monitors.Clamp(want,w,h);var side=Dock.Top;
   if(Math.Abs(placed.Y-want.Y)>1){
    double y=ground.Y-ChestHeight*scale-h/2;bool right=!sprite.FacingLeft;
    for(int attempt=0;attempt<2;attempt++,right=!right){want=new(right?ground.X+DogHalfWidth*scale:ground.X-DogHalfWidth*scale-w,y);placed=monitors.Clamp(want,w,h);side=right?Dock.Left:Dock.Right;if(Math.Abs(placed.X-want.X)<=1)break;}
   }
   Move(ch,cr,placed);var cd=VisualTreeHelper.GetDpi(chat!);chat!.PointTail(side,(ground.X-placed.X)/cd.DpiScaleX);
  }
 }

}
public enum BubbleKind {Emotion,Ai,Notice}
public sealed class BubbleWindow:Window {
 public BubbleKind Kind {get;}
 public string FullText {get;}
 private readonly Grid bubbleRoot=new();private readonly Border body;private readonly System.Windows.Shapes.Path tail=new(){Width=22,Height=12,Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false};
 // targetX: the dog's head in this window's DIPs; below: the dog is under the bubble.
 public void PointTail(double targetX,bool below){double width=ActualWidth>0?ActualWidth:Width;double x=Math.Clamp(targetX,24,Math.Max(24,width-24));tail.Margin=new Thickness(x-11,0,0,0);tail.VerticalAlignment=below?VerticalAlignment.Bottom:VerticalAlignment.Top;tail.RenderTransform=below?Transform.Identity:new ScaleTransform(1,-1,11,6);body.Margin=below?new Thickness(0,0,0,11):new Thickness(0,11,0,0);}
 // The dog speaking: a compact cream bubble with just the words. Click it to dismiss.
 public BubbleWindow(string text,Action reply,BubbleKind kind=BubbleKind.Ai) {
  Kind=kind;FullText=text;Title=kind==BubbleKind.Emotion?"강아지 감정":"강아지 AI 말풍선";SizeToContent=SizeToContent.WidthAndHeight;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowActivated=false;ShowInTaskbar=false;Topmost=true;
  var ink=new SolidColorBrush(Color.FromRgb(0x4A,0x34,0x26));var words=new TextBlock{Text=kind==BubbleKind.Notice?"ⓘ "+text:text,TextWrapping=TextWrapping.Wrap,FontSize=14,LineHeight=21,MaxWidth=250,Foreground=kind==BubbleKind.Notice?new SolidColorBrush(Color.FromRgb(0x6B,0x5A,0x4C)):ink};
  var scroll=new ScrollViewer{MaxHeight=190,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Content=words};
  var fill=kind==BubbleKind.Notice?Color.FromRgb(0xF7,0xF4,0xF0):Color.FromRgb(0xFF,0xFB,0xF5);var line=kind==BubbleKind.Notice?Color.FromRgb(0xD9,0xCF,0xC4):Color.FromRgb(0xEE,0xCF,0xAE);
  body=new Border{Background=new SolidColorBrush(fill),BorderBrush=new SolidColorBrush(line),BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(18),Padding=new Thickness(14,9,14,10),Margin=new Thickness(0,0,0,11),Child=scroll,
   Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(0x7A,0x4E,0x2A),BlurRadius=10,ShadowDepth=2,Direction=270,Opacity=.16}};
  tail.Fill=body.Background;tail.Stroke=body.BorderBrush;tail.StrokeThickness=1.5;tail.Data=Geometry.Parse("M 0,0 L 11,12 L 22,0");
  // The tail is drawn after the body so its fill covers the border where they meet.
  bubbleRoot.Margin=new Thickness(6,4,6,2);bubbleRoot.Children.Add(body);bubbleRoot.Children.Add(tail);Content=bubbleRoot;Cursor=System.Windows.Input.Cursors.Hand;ToolTip="클릭하면 닫혀요";PointTail(60,true);
  MouseLeftButtonUp+=(s,e)=>Close();
  SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;NativeMethods.SetWindowLongPtr(h,-20,new IntPtr(NativeMethods.GetWindowLongPtr(h,-20).ToInt64()|0x08000000|0x80));};
  double remaining=kind==BubbleKind.Emotion?3:kind==BubbleKind.Ai?Math.Clamp(8+text.Length/12.0,12,60):8;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(.25)};timer.Tick+=(s,e)=>{if(IsMouseOver||IsKeyboardFocusWithin||!IsVisible)return;remaining-=.25;if(remaining<=0){timer.Stop();Close();}};Closed+=(s,e)=>timer.Stop();Loaded+=(s,e)=>timer.Start();
 }
}

