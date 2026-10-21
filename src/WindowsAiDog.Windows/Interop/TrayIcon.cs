using System.Runtime.InteropServices;
namespace WindowsAiDog.Windows.Interop;
public sealed class TrayIcon:IDisposable {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 private struct Data {
  public int Size;public IntPtr Window;public uint Id,Flags,Message;public IntPtr Icon;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;
  public uint State,StateMask;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;
  public uint Timeout;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string InfoTitle;
  public uint InfoFlags;public Guid Guid;public IntPtr BalloonIcon;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern bool Shell_NotifyIcon(uint message,ref Data data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr LoadImage(IntPtr instance,string name,uint type,int width,int height,uint flags);
 [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint RegisterWindowMessage(string message);
 public const int Callback=0x8001;
 public uint TaskbarCreated {get;}=RegisterWindowMessage("TaskbarCreated");
 private Data data;
 public TrayIcon(IntPtr window,string icon) {
  data=new(){Size=Marshal.SizeOf<Data>(),Window=window,Id=1,Flags=7,Message=Callback,Icon=LoadImage(IntPtr.Zero,icon,1,32,32,0x10),Tip="강아지 · 더블 클릭: 대화 / 오른쪽 클릭: 메뉴",Info="",InfoTitle=""};Add();
 }
 public bool Registered {get;private set;}
 public void Add()=>Registered=Shell_NotifyIcon(0,ref data);
 public void Dispose(){Shell_NotifyIcon(2,ref data);if(data.Icon!=IntPtr.Zero)DestroyIcon(data.Icon);}
}
