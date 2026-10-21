using System.Runtime.InteropServices;
namespace WindowsAiDog.Windows.Interop;
internal static class NativeMethods {
 [StructLayout(LayoutKind.Sequential)]internal struct Rect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]internal struct MonitorInfo{public int Size;public Rect Monitor,Work;public int Flags;}
 internal delegate bool MonitorCallback(IntPtr monitor,IntPtr hdc,IntPtr rect,IntPtr data);
 [DllImport("user32.dll")]internal static extern bool EnumDisplayMonitors(IntPtr hdc,IntPtr clip,MonitorCallback callback,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")]internal static extern IntPtr MonitorFromWindow(IntPtr window,int flags);
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int cx,int cy,uint flags);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetClassName(IntPtr window,System.Text.StringBuilder buffer,int max);
 [DllImport("wtsapi32.dll")]internal static extern bool WTSRegisterSessionNotification(IntPtr window,int flags);
 [DllImport("wtsapi32.dll")]internal static extern bool WTSUnRegisterSessionNotification(IntPtr window);
}
