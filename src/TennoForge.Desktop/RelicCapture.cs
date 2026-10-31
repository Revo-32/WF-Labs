using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

internal static class RelicCapture
{
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetClientRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")]private static extern bool ClientToScreen(IntPtr hwnd,ref NativePoint point);
    [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr hwnd,int command);
    internal static bool IsMinimized(IntPtr hwnd)=>IsIconic(hwnd);
    internal static Int32Rect ClientBounds(IntPtr hwnd)
    {
        if(!GetClientRect(hwnd,out var rect))throw new InvalidOperationException("게임 창의 크기를 확인하지 못했습니다.");
        var point=new NativePoint();if(!ClientToScreen(hwnd,ref point))throw new InvalidOperationException("게임 창의 위치를 확인하지 못했습니다.");
        return new(point.X,point.Y,rect.Right-rect.Left,rect.Bottom-rect.Top);
    }
    internal static void ValidateGameSize(int width,int height)
    {
        if(width<1920||height<1080)throw new InvalidOperationException("성유물 실시간 인식은 FHD(1920×1080) 이상을 지원합니다. 게임 해상도를 높인 뒤 다시 읽어주세요.");
    }
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
    [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("gdi32.dll")]private static extern bool BitBlt(IntPtr dc,int x,int y,int width,int height,IntPtr src,int sx,int sy,int rop);
    [DllImport("gdi32.dll")]private static extern bool StretchBlt(IntPtr dc,int x,int y,int width,int height,IntPtr src,int sx,int sy,int sourceWidth,int sourceHeight,int rop);
    [DllImport("gdi32.dll")]private static extern int SetStretchBltMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")]private static extern bool SetBrushOrgEx(IntPtr dc,int x,int y,IntPtr previous);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
    private static readonly object foregroundIdentitySync=new();
    private static Process? foregroundProcess;
    private static uint foregroundProcessId;
    private static bool foregroundIsGame;
    internal static int ForegroundIdentityLookups {get;private set;}
    internal static (bool Visible,bool Minimized,uint ProcessId,uint ThreadId,int Lookups) WindowIdentityStateForCheck(IntPtr window)
    {
        uint thread=GetWindowThreadProcessId(window,out var pid);
        return(IsWindowVisible(window),IsIconic(window),pid,thread,ForegroundIdentityLookups);
    }
    internal static IntPtr ForegroundGameWindow()=>GameWindowIdentity(GetForegroundWindow());
    // This helper verifies identity only. Periodic detection supplies the actual
    // foreground HWND; the capture path independently checks focus again.
    internal static IntPtr GameWindowIdentity(IntPtr window)
    {
        if(window==IntPtr.Zero||!IsWindowVisible(window)||IsIconic(window)||GetWindowThreadProcessId(window,out var pid)==0||pid==0)return IntPtr.Zero;
        lock(foregroundIdentitySync)
        {
            try
            {
                // Query the current HWND owner on every observation, but keep one
                // bound Process handle instead of enumerating every system process.
                // HasExited refers to that handle, so reused HWNDs/PIDs do not make
                // a later unrelated process inherit a positive Warframe identity.
                if(foregroundProcess is not null&&foregroundProcessId==pid&&!foregroundProcess.HasExited)
                    return foregroundIsGame&&GetWindowThreadProcessId(window,out var owner)!=0&&owner==pid?window:IntPtr.Zero;
                ClearForegroundGameCache();
                foregroundProcess=Process.GetProcessById(checked((int)pid));foregroundProcessId=pid;ForegroundIdentityLookups++;
                if(foregroundProcess.HasExited){ClearForegroundGameCache();return IntPtr.Zero;}
                var name=foregroundProcess.ProcessName;
                foregroundIsGame=(name.Equals("Warframe.x64",StringComparison.OrdinalIgnoreCase)||name.Equals("Warframe",StringComparison.OrdinalIgnoreCase))&&!foregroundProcess.HasExited;
                return foregroundIsGame&&GetWindowThreadProcessId(window,out var currentOwner)!=0&&currentOwner==pid?window:IntPtr.Zero;
            }
            catch(Exception e)when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {ClearForegroundGameCache();return IntPtr.Zero;}
        }
    }
    internal static void ClearForegroundGameCache()
    {
        lock(foregroundIdentitySync)
        {
            foregroundProcess?.Dispose();foregroundProcess=null;foregroundProcessId=0;foregroundIsGame=false;
        }
    }
    internal static IntPtr GameWindow()
    {
        var handles=new List<IntPtr>();
        foreach(var name in new[]{"Warframe.x64","Warframe"})foreach(var process in Process.GetProcessesByName(name))using(process)
            if(process.MainWindowHandle!=IntPtr.Zero&&IsWindowVisible(process.MainWindowHandle))handles.Add(process.MainWindowHandle);
        var foreground=GetForegroundWindow();if(handles.Contains(foreground))return foreground;
        if(handles.Count!=1)throw new InvalidOperationException("캡처할 워프레임 창을 찾지 못했습니다. 게임을 켜거나 보상 스크린샷을 불러오세요.");
        return handles[0];
    }
    internal static BitmapSource Capture(IntPtr hwnd,int? previewWidth=null)
    {
        if(GetForegroundWindow()!=hwnd)throw new InvalidOperationException("워프레임을 화면 앞으로 가져온 뒤 단축키를 누르세요. 다른 창이 앞에 있으면 캡처하지 않습니다.");
        if(IsIconic(hwnd)||!GetClientRect(hwnd,out var bounds))throw new InvalidOperationException("최소화된 게임은 캡처할 수 없습니다.");
        int width=bounds.Right-bounds.Left,height=bounds.Bottom-bounds.Top;
        if(width<80||height<30||(long)width*height>40_000_000)throw new InvalidOperationException("게임 화면 크기를 확인하세요.");
        int targetWidth=previewWidth is {} limit?Math.Min(width,limit):width,targetHeight=(int)Math.Round(height*(double)targetWidth/width);
        // A GPU swap-chain window's client DC can return an old backing surface.
        // Read the displayed desktop pixels at the client's screen coordinates.
        // Keep SRCCOPY only: never request CAPTUREBLT/layered-window repainting.
        var origin=new NativePoint();if(!ClientToScreen(hwnd,ref origin))throw new InvalidOperationException("게임 화면의 위치를 확인하지 못했습니다.");
        var source=GetDC(IntPtr.Zero);if(source==IntPtr.Zero)throw new InvalidOperationException("게임 캡처 컨텍스트를 얻지 못했습니다.");
        var dc=CreateCompatibleDC(source);var bitmap=CreateCompatibleBitmap(source,targetWidth,targetHeight);var previous=SelectObject(dc,bitmap);
        try
        {
            if(previewWidth is not null){SetStretchBltMode(dc,4);SetBrushOrgEx(dc,0,0,IntPtr.Zero);}
            bool copied=bitmap!=IntPtr.Zero&&(previewWidth is null?BitBlt(dc,0,0,width,height,source,origin.X,origin.Y,0x00CC0020):StretchBlt(dc,0,0,targetWidth,targetHeight,source,origin.X,origin.Y,width,height,0x00CC0020));
            if(!copied)throw new InvalidOperationException("캡처하지 못했습니다. 테두리 없는 창 모드 또는 스크린샷 불러오기를 사용하세요.");
            if(GetForegroundWindow()!=hwnd||IsIconic(hwnd))throw new InvalidOperationException("캡처 중 게임 전경이 바뀌어 화면을 폐기했습니다.");
            var latest=ClientBounds(hwnd);
            if(latest.X!=origin.X||latest.Y!=origin.Y||latest.Width!=width||latest.Height!=height)throw new InvalidOperationException("캡처 중 게임 창의 위치·크기가 바뀌어 화면을 폐기했습니다.");
            var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;
        }
        finally{SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(IntPtr.Zero,source);}
    }
}

internal sealed class RelicHotkey : IDisposable
{
    private readonly int Id;
    private readonly uint key;
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    private readonly HwndSource source;private readonly Action action;private bool disposed;
    internal bool Registered{get;private set;}
    internal RelicHotkey(Window owner,Action action,int id=0x5746,uint key=0x52)
    {
        Id=id;this.key=key;this.action=action;source=HwndSource.FromHwnd(new WindowInteropHelper(owner).EnsureHandle());source.AddHook(Hook);
    }
    internal bool Enable(bool enabled)
    {
        if(disposed||source.IsDisposed)return false;
        if(Registered){UnregisterHotKey(source.Handle,Id);Registered=false;}
        if(enabled)Registered=RegisterHotKey(source.Handle,Id,0x0002|0x0004|0x4000,key); // Ctrl+Shift, no repeat
        return Registered;
    }
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {if(message==0x0312&&wParam.ToInt32()==Id){handled=true;action();}return IntPtr.Zero;}
    public void Dispose(){if(disposed)return;Enable(false);if(!source.IsDisposed)source.RemoveHook(Hook);disposed=true;}
}
