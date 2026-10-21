using System.Runtime.InteropServices;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Interop;
public sealed class MonitorService {
 public IReadOnlyList<WorkArea> Areas() {
  var result=new List<WorkArea>();
  NativeMethods.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(m,h,r,d)=>{var info=new NativeMethods.MonitorInfo{Size=Marshal.SizeOf<NativeMethods.MonitorInfo>()};if(NativeMethods.GetMonitorInfo(m,ref info))result.Add(new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top));return true;},IntPtr.Zero);
  return result;
 }
 public Placement Clamp(Placement position,double width,double height) {
  var areas=Areas();var area=areas.FirstOrDefault(a=>position.X+width/2>=a.X&&position.X+width/2<a.X+a.Width&&position.Y+height/2>=a.Y&&position.Y+height/2<a.Y+a.Height)??areas.FirstOrDefault()??new WorkArea(0,0,1920,1080);
  return MonitorGeometry.Clamp(position,area,width,height);
 }
 public static bool Fullscreen(IntPtr own) {
  return IsFullscreenWindow(own,NativeMethods.GetForegroundWindow());
 }
 public static bool IsFullscreenWindow(IntPtr own,IntPtr foreground) {
  if(foreground==IntPtr.Zero||foreground==own)return false;
  var cls=new System.Text.StringBuilder(100);NativeMethods.GetClassName(foreground,cls,100);
  if(cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd")return false;
  var monitor=NativeMethods.MonitorFromWindow(foreground,2);var info=new NativeMethods.MonitorInfo{Size=Marshal.SizeOf<NativeMethods.MonitorInfo>()};
  if(!NativeMethods.GetMonitorInfo(monitor,ref info)||!NativeMethods.GetWindowRect(foreground,out var rect))return false;
  return rect.Left<=info.Monitor.Left&&rect.Top<=info.Monitor.Top&&rect.Right>=info.Monitor.Right&&rect.Bottom>=info.Monitor.Bottom;
 }
}
