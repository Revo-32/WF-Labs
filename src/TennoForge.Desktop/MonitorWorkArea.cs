using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TennoForge.Desktop;
internal static class MonitorWorkArea
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct Info {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor,ref Info info);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    internal static Info Read(nint monitor){var info=new Info{Size=Marshal.SizeOf<Info>(),Device=""};if(!GetMonitorInfo(monitor,ref info))throw new InvalidOperationException("모니터 작업 영역을 읽을 수 없습니다.");return info;}
    internal static void Attach(Window window)
    {
        nint lastMonitor=0;uint lastDpi=0;bool queued=false;
        void Refresh()
        {
            queued=false;var hwnd=new WindowInteropHelper(window).Handle;if(hwnd==0)return;
            var monitor=MonitorFromWindow(hwnd,2);var dpi=GetDpiForWindow(hwnd);
            if(monitor==lastMonitor&&dpi==lastDpi)return;lastMonitor=monitor;lastDpi=dpi;
            var area=Read(monitor).Work;var scale=dpi/96d;
            window.MinWidth=Math.Min(1760,(area.Right-area.Left)/scale);
            window.MinHeight=Math.Min(920,(area.Bottom-area.Top)/scale);
            GetWindowRect(hwnd,out var bounds);
            // Off-screen automated previews must stay off-screen. Native windows on
            // a smaller display use the existing scrollable workspace.
            if(window.WindowState!=WindowState.Normal||bounds.Right<=area.Left||bounds.Left>=area.Right||bounds.Bottom<=area.Top||bounds.Top>=area.Bottom)return;
            var width=Math.Min(bounds.Right-bounds.Left,area.Right-area.Left);var height=Math.Min(bounds.Bottom-bounds.Top,area.Bottom-area.Top);
            SetWindowPos(hwnd,0,Math.Clamp(bounds.Left,area.Left,area.Right-width),Math.Clamp(bounds.Top,area.Top,area.Bottom-height),width,height,0x14);
        }
        void Queue(){if(queued)return;queued=true;window.Dispatcher.BeginInvoke(Refresh,DispatcherPriority.Loaded);}
        window.SourceInitialized+=(_,_)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source?.AddHook((nint h,int m,nint w,nint l,ref bool handled)=>{if(m is 0x47 or 0x2E0)Queue();return 0;});Queue();};
    }
}
