using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Rendering;
public sealed class SpriteVisual:FrameworkElement {
 private readonly SpriteCatalog catalog;
 private readonly Dictionary<PetAction,BitmapSource[]> images=new();
 private readonly RenderTargetBitmap hitBitmap=new(224,210,96,96,PixelFormats.Pbgra32);
 private readonly byte[] hitPixels=new byte[224*210*4];private int revision,hitRevision=-1;
 private readonly SpriteTimeline timeline=new();
 private PetAction clip=PetAction.WagTail;
 private int frame=-1;
 private double started,lastSample; private bool motionPaused; private double jumpHeight,landingStarted,landingFromHeight; private PetAction? landingNext; public double JumpHeight=>jumpHeight;
 private BitmapSource? blendFrom;private BitmapSource[]? blendFrames;private double blendDuration,blendAmount=1;

 public const double BlendSeconds=.35;
 private PetAction? pendingPose;private double reverseStarted;private int reverseFrom;
 // Set while walk/run keeps stepping until a frame close to the standing pose before switching.
 private PetAction? pendingStop;
 // Run ending through its own slow-down clip: the next action waits until the clip has played.
 private PetAction? stopNext;private double stopStarted;private BitmapSource[]? runStopImages;
 public bool IsSettling=>pendingStop is not null||stopNext is not null;
 public bool IsTransitioning=>pendingPose is not null||landingNext is not null||blendAmount<1;
 public bool Completed(PetAction action,double now)=>clip==action&&pendingPose is null&&frame==images[clip].Length-1&&now-started>=blendDuration+images[clip].Length/(double)catalog.Clips[clip].Fps;
 private bool mirrored;
 public bool FacingLeft=>mirrored;
 public bool Supports(PetAction action)=>catalog.Clips.ContainsKey(action);

 public int CurrentFrame=>frame;
 public double CycleSeconds=>(catalog.Clips[clip].Sequence?.Count??images[clip].Length)/(double)catalog.Clips[clip].Fps;
 public double TravelPixelsPerSecond=>catalog.Clips[clip].TravelPixelsPerSecond;
 public bool TreatEaten=>clip==PetAction.Treat&&frame>=(catalog.Clips[clip].EatCompletedFrame??images[clip].Length-2);
 public PetAction CurrentAction=>clip;
 public SpriteVisual(string directory) {
  Width=224;Height=210;catalog=SpriteCatalog.Load(directory);
  foreach(var (action,c) in catalog.Clips) {
   var b=new BitmapImage();b.BeginInit();b.UriSource=new Uri(c.Image);b.CacheOption=BitmapCacheOption.OnLoad;b.EndInit();b.Freeze();
   images[action]=c.Frames.Select(f=>{var crop=new CroppedBitmap(b,new Int32Rect(f.X,f.Y,f.Width,f.Height));crop.Freeze();return NormalizeCoat(crop,c.CoatColorGain);}).ToArray();
   if(c.StationaryTailWag)images[action]=StationaryTailFrames.Create(images[action][0],8);
   if(c.Stop is SpriteClip stop){var s=new BitmapImage();s.BeginInit();s.UriSource=new Uri(stop.Image);s.CacheOption=BitmapCacheOption.OnLoad;s.EndInit();s.Freeze();runStopImages=stop.Frames.Select(f=>{var crop=new CroppedBitmap(s,new Int32Rect(f.X,f.Y,f.Width,f.Height));crop.Freeze();return (BitmapSource)crop;}).ToArray();}

  }
  Tick(PetAction.WagTail,0,false,false);
 }
 public void Restart(PetAction action,double now,bool immediate=false){
  lastSample=now;motionPaused=false;
  if(!immediate&&clip==PetAction.Jump&&jumpHeight>.01){if(landingNext is null){landingStarted=now;landingFromHeight=jumpHeight;}landingNext=action;return;}
  landingNext=null;jumpHeight=0;
  if(!immediate&&clip is PetAction.Sit or PetAction.LieDown or PetAction.TiltHead&&action!=clip&&frame>=0){pendingPose=action;reverseStarted=now;reverseFrom=frame;return;}
  if(!immediate&&clip==PetAction.Run&&action is not(PetAction.Walk or PetAction.Run)&&frame>=0&&runStopImages is not null){if(stopNext is null)stopStarted=now;stopNext=action;return;}
  if(!immediate&&clip is PetAction.Walk or PetAction.Run&&action is not(PetAction.Walk or PetAction.Run)&&frame>=0&&catalog.Clips[clip].SettleFrames is {} settle&&!settle.Contains(frame)){pendingStop=action;return;}
  pendingPose=null;pendingStop=null;stopNext=null;Begin(action,now,immediate);
 }
 private void Begin(PetAction action,double now,bool immediate){
  if(!immediate&&frame>=0&&clip!=action){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Paint(dc);var snapshot=new RenderTargetBitmap(224,210,96,96,PixelFormats.Pbgra32);snapshot.Render(visual);snapshot.Freeze();blendFrom=snapshot;blendDuration=BlendSeconds;blendAmount=0;}
  else{blendFrom=null;blendDuration=0;blendAmount=1;}
  blendFrames=null;clip=action;started=now;frame=-1;
 }
 public double Tick(PetAction action,double now,bool paused,bool faceLeft,double? maximumDelta=null) {
  bool locomotion=action is PetAction.Walk or PetAction.Run;
  if(locomotion&&maximumDelta is not null)now=Math.Max(now,lastSample);
  double priorPose=clip==action&&pendingPose is null&&landingNext is null?Math.Max(0,lastSample-started-blendDuration):0;
  double rawDelta=Math.Max(0,now-lastSample);
  double delta=locomotion&&(paused||motionPaused)?0:maximumDelta is double cap?Math.Min(rawDelta,cap):rawDelta;
  if(locomotion){
   // Exclude stalls and pauses from both animation phase and window travel.
   // Consume the first resumed sample as a new baseline, never as catch-up.
   double excluded=rawDelta-delta;started+=excluded;
   if(pendingPose is not null)reverseStarted+=excluded;
   if(landingNext is not null)landingStarted+=excluded;
  }
  lastSample=now;motionPaused=locomotion&&paused;
  if(landingNext is not null){landingNext=action;double t=Math.Clamp((now-landingStarted)/.35,0,1);jumpHeight=landingFromHeight*(1-t)*(1-t);frame=Math.Min(images[clip].Length-1,10+(int)(t*6));InvalidatePose();if(t<1)return 0;var next=landingNext.Value;landingNext=null;jumpHeight=0;Begin(next,now,false);}
  if(pendingPose is not null){pendingPose=action;double reverseTime=locomotion?Math.Round(now-reverseStarted,9):now-reverseStarted;int reverseStep=(int)Math.Max(0,reverseTime*6);if(reverseStep<=reverseFrom){int reverseIndex=reverseFrom-reverseStep;if(frame!=reverseIndex){frame=reverseIndex;InvalidatePose();}return 0;}var next=pendingPose.Value;pendingPose=null;Begin(next,now,false);}
  if(stopNext is not null){
   if(action==clip){stopNext=null;frame=-1;started=now;}
   else{
    stopNext=action;var stop=catalog.Clips[PetAction.Run].Stop!;double t=Math.Max(0,now-stopStarted),duration=stop.Frames.Count/(double)stop.Fps;
    int stopFrame=Math.Min(stop.Frames.Count-1,(int)(Math.Round(t,9)*stop.Fps));if(stopFrame!=frame){frame=stopFrame;InvalidatePose();}
    // Window travel eases out with the clip so the dog does not slide once it has stopped.
    if(t<duration)return (maximumDelta is double stopCap?Math.Min(rawDelta,stopCap):rawDelta)*Math.Max(0,1-t/duration);
    var next=stopNext.Value;Begin(next,now,false);stopNext=null;
   }
  }
  if(pendingStop is not null){
   if(action==clip)pendingStop=null;
   else{
    // Keep stepping (and travelling) in the current gait until a near-standing frame comes up.
    pendingStop=action;var gait=catalog.Clips[clip];int count=gait.Sequence?.Count??gait.Frames.Count;
    int gaitStep=(int)(Math.Round(Math.Max(0,now-started-blendDuration),9)*gait.Fps)%count;int gaitIndex=gait.Sequence?[gaitStep]??gaitStep;
    if(!gait.SettleFrames!.Contains(gaitIndex)){if(gaitIndex!=frame){frame=gaitIndex;InvalidatePose();}return maximumDelta is double limit?Math.Min(rawDelta,limit):rawDelta;}
    var next=pendingStop.Value;pendingStop=null;Begin(next,now,false);
   }
  }
  if(clip!=action){Restart(action,now);motionPaused=locomotion&&paused;if(pendingPose is not null||pendingStop is not null||stopNext is not null)return Tick(action,now,paused,faceLeft,maximumDelta);}
  double elapsed=Math.Max(0,now-started);double priorBlend=blendAmount;blendAmount=blendDuration>0?Math.Clamp(elapsed/blendDuration,0,1):1;if(blendAmount>=1){blendFrom=null;blendFrames=null;}
  double poseTime=Math.Max(0,elapsed-blendDuration);var active=catalog.Clips[clip];
  // Excluded wall-time subtraction can leave exact frame boundaries one ULP short.
  double frameTime=locomotion?Math.Round(poseTime,9):poseTime;
  int step=timeline.Frame(action,frameTime,locomotion?false:paused,active.Sequence?.Count??active.Frames.Count,active.Fps);int index=active.Sequence?[step]??step;
  double travelSeconds=locomotion?Math.Clamp(poseTime-priorPose,0,delta):0;
  double priorHeight=jumpHeight; double phase=(poseTime*active.Fps-3)/10; jumpHeight=clip==PetAction.Jump&&active.Frames.Count==16&&phase>0&&phase<1?42*Math.Pow(Math.Sin(Math.PI*phase),2):0;

  // Every pose keeps facing the way the dog last moved, so stopping never flips it around.
  bool flip=faceLeft;
  if(index==frame&&mirrored==flip&&Math.Abs(priorBlend-blendAmount)<0.0001&&Math.Abs(priorHeight-jumpHeight)<0.0001)return travelSeconds;frame=index;mirrored=flip;InvalidatePose();return travelSeconds;
 }
 private static BitmapSource NormalizeCoat(BitmapSource source,IReadOnlyList<double>? gain) {
  if(gain is null||gain.All(x=>Math.Abs(x-1)<.00001))return source;
  var image=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);image.Freeze();int stride=image.PixelWidth*4;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);
  for(int i=0;i<pixels.Length;i+=4)for(int c=0;c<3;c++)pixels[i+c]=(byte)Math.Min(pixels[i+3],Math.Round(pixels[i+c]*gain[2-c]));
  var output=BitmapSource.Create(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Pbgra32,null,pixels,stride);output.Freeze();return output;
 }
 private Rect Destination {
  get{var poses=stopNext is not null&&catalog.Clips[clip].Stop is SpriteClip stop?stop.Frames:catalog.Clips[clip].Frames;var f=poses[Math.Min(poses.Count-1,Math.Max(0,frame))];const double scale=0.44;return new(112-f.AnchorX*scale,190-f.AnchorY*scale-jumpHeight,f.Width*scale,f.Height*scale);}
 }
 protected override void OnRender(DrawingContext dc) {
  base.OnRender(dc);Paint(dc);
 }
 private void Paint(DrawingContext dc) {
  if(frame<0)return;
  if(blendFrom is not null&&blendAmount<1){
   if(blendFrames is null){var visual=new DrawingVisual();using(var context=visual.RenderOpen())PaintPose(context);var target=new RenderTargetBitmap(224,210,96,96,PixelFormats.Pbgra32);target.Render(visual);var a=new byte[224*210*4];var b=new byte[a.Length];blendFrom.CopyPixels(a,224*4,0);target.CopyPixels(b,224*4,0);blendFrames=new BitmapSource[7];
    for(int i=0;i<7;i++){double t=i/6.0;var pixels=new byte[a.Length];for(int j=0;j<pixels.Length;j++)pixels[j]=(byte)Math.Round(a[j]*(1-t)+b[j]*t);var bitmap=BitmapSource.Create(224,210,96,96,PixelFormats.Pbgra32,null,pixels,224*4);bitmap.Freeze();blendFrames[i]=bitmap;}
   }dc.DrawImage(blendFrames[Math.Min(6,(int)Math.Round(blendAmount*6))],new Rect(0,0,224,210));return;
  }
  PaintPose(dc);
 }
 private void PaintPose(DrawingContext dc) {
  if(mirrored)dc.PushTransform(new ScaleTransform(-1,1,112,0));
  dc.DrawImage(stopNext is not null&&runStopImages is not null?runStopImages[Math.Min(frame,runStopImages.Length-1)]:images[clip][frame],Destination);
  if(mirrored)dc.Pop();
 }
 private void InvalidatePose(){revision++;InvalidateVisual();UpdateAnchors();}
 // Visible dog box and head point of the current pose, in this element's coordinates, so speech
 // bubbles and the chat input can attach to the dog instead of the (mostly empty) window edges.
 public Rect DogBounds{get;private set;}
 public Point HeadPoint{get;private set;}
 private readonly Dictionary<BitmapSource,(Int32Rect Box,double HeadX)> shapes=new();
 private BitmapSource CurrentImage=>stopNext is not null&&runStopImages is not null?runStopImages[Math.Min(frame,runStopImages.Length-1)]:images[clip][frame];
 private void UpdateAnchors(){
  if(frame<0)return;var image=CurrentImage;
  if(!shapes.TryGetValue(image,out var shape))shapes[image]=shape=Measure(image);
  if(shape.Box.Width==0)return;var d=Destination;double k=d.Width/image.PixelWidth;
  double X(double px){double x=d.X+px*k;return mirrored?224-x:x;}
  double left=X(shape.Box.X),right=X(shape.Box.X+shape.Box.Width);
  DogBounds=new Rect(new Point(Math.Min(left,right),d.Y+shape.Box.Y*k),new Point(Math.Max(left,right),d.Y+(shape.Box.Y+shape.Box.Height)*k));
  HeadPoint=new Point(X(shape.HeadX),DogBounds.Top);
 }
 // Opaque box plus the head's centre: within the top third of the dog, the widest run of opaque
 // columns is the head (the raised tail is a narrower run).
 private static (Int32Rect,double) Measure(BitmapSource image){
  var source=new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);int w=source.PixelWidth,h=source.PixelHeight;var px=new byte[w*h*4];source.CopyPixels(px,w*4,0);
  int minX=w,minY=h,maxX=-1,maxY=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(px[(y*w+x)*4+3]>64){if(x<minX)minX=x;if(x>maxX)maxX=x;if(y<minY)minY=y;if(y>maxY)maxY=y;}
  if(maxX<0)return (new Int32Rect(),w/2.0);
  int band=minY+Math.Max(8,(maxY-minY)/3);var mass=new double[w];for(int y=minY;y<band;y++)for(int x=minX;x<=maxX;x++)mass[x]+=px[(y*w+x)*4+3];
  double best=0,head=(minX+maxX)/2.0;int start=-1;double run=0,weighted=0;
  for(int x=minX;x<=maxX+1;x++){bool on=x<=maxX&&mass[x]>255*2;if(on){if(start<0){start=x;run=0;weighted=0;}run+=mass[x];weighted+=mass[x]*x;}else if(start>=0){if(run>best){best=run;head=weighted/run;}start=-1;}}
  return (new Int32Rect(minX,minY,maxX-minX+1,maxY-minY+1),head);
 }
 protected override HitTestResult? HitTestCore(PointHitTestParameters parameters) {
  var p=parameters.HitPoint;int x=(int)Math.Floor(p.X),y=(int)Math.Floor(p.Y);
  if(frame<0||x<0||y<0||x>=224||y>=210)return null;
  if(hitRevision!=revision){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Paint(dc);hitBitmap.Clear();hitBitmap.Render(visual);hitBitmap.CopyPixels(hitPixels,224*4,0);hitRevision=revision;}
  return hitPixels[(y*224+x)*4+3]>8?new PointHitTestResult(this,p):null;
 }
}
