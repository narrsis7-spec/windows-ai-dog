using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WindowsAiDog.Windows.Rendering;
// Whole image inverse displacement. The field is exactly zero below y=202
// and right of x=190, above the reference back/rump; no body mask or layers.
public static class StationaryTailFrames {
 public static BitmapSource[] Create(BitmapSource source,int count) {
  var canonical=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);canonical.Freeze();
  int w=canonical.PixelWidth,h=canonical.PixelHeight,stride=w*4;
  var pixels=new byte[stride*h];canonical.CopyPixels(pixels,stride,0);var result=new BitmapSource[count];
  for(int frame=0;frame<count;frame++){
   var output=(byte[])pixels.Clone();double amplitude=12*Math.Sin(frame*2*Math.PI/count);
   for(int y=0;y<Math.Min(202,h);y++)for(int x=0;x<Math.Min(190,w);x++){
    double t=Math.Clamp((202-y)/65.0,0,1),u=Math.Clamp((190-x)/35.0,0,1);
    double shift=amplitude*t*t*(3-2*t)*u*u*(3-2*u),sx=x-shift;
    int lo=(int)Math.Floor(sx);double fraction=sx-lo;
    for(int c=0;c<4;c++){
     int a=lo>=0&&lo<w?pixels[y*stride+lo*4+c]:0;
     int b=lo+1>=0&&lo+1<w?pixels[y*stride+(lo+1)*4+c]:0;
     output[y*stride+x*4+c]=(byte)Math.Round(a*(1-fraction)+b*fraction);
    }
   }
   var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,output,stride);bitmap.Freeze();result[frame]=bitmap;
  }
  return result;
 }
}
