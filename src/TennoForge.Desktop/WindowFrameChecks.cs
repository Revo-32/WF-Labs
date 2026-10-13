using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TennoForge.Desktop;

internal static class WindowFrameChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    internal static object Run(Window window,string? capturePath=null)
    {
        // A connected visual tree is required for native chrome hit testing over WPF buttons.
        // Place the probe outside the virtual desktop; never activate it or add a taskbar item.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Left = SystemParameters.VirtualScreenLeft - window.Width - 100;
        window.Top = SystemParameters.VirtualScreenTop - window.Height - 100;
        window.Show();
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        window.ApplyTemplate(); window.UpdateLayout();
        if (!GetWindowRect(hwnd, out var outer) || !GetClientRect(hwnd, out var client)) throw new Exception("실제 창 크기 확인 실패");
        var width = outer.Right - outer.Left; var height = outer.Bottom - outer.Top;
        var clientWidth = client.Right - client.Left; var clientHeight = client.Bottom - client.Top;
        if (Math.Abs(height - clientHeight) > 2 || Math.Abs(width - clientWidth) > 2) throw new Exception($"기본 제목줄/프레임이 남아 있습니다: 창 {width}×{height}, 본문 {clientWidth}×{clientHeight}");
        if ((GetWindowLong(hwnd, -20) & 0x80000) != 0 || window.AllowsTransparency) throw new Exception("레이어드 창이 활성화되어 있습니다.");
        int Hit(int x, int y) => SendMessage(hwnd, 0x84, IntPtr.Zero, new IntPtr(((outer.Top + y) << 16) | ((outer.Left + x) & 0xffff))).ToInt32();
        var dpi=GetDpiForWindow(hwnd);
        int HitPoint(Point point)=>Hit((int)Math.Round(point.X*dpi/96),(int)Math.Round(point.Y*dpi/96));
        var headerPoints=((MainWindow)window).HeaderHitPointsForCheck();
        var captionHit = HitPoint(headerPoints.Caption);
        var rightHit = Hit(width - 2, height / 2);
        if (captionHit != 2 || rightHit != 11) throw new Exception($"제목 영역 이동/오른쪽 크기 조절 실패: {captionHit}, {rightHit}");
        var closeButtonHit = HitPoint(headerPoints.Close);
        if (closeButtonHit != 1) throw new Exception($"닫기 버튼이 이동 영역에 가려졌습니다: {closeButtonHit}");
        var tabHit=HitPoint(headerPoints.Tab);var settingsHit=HitPoint(headerPoints.Settings);
        if(tabHit!=1||settingsHit!=1)throw new Exception($"상단 메뉴/설정이 이동 영역에 가려졌습니다: {tabHit}, {settingsHit}");
        var cornerPreference = -1;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) &&
            (DwmGetWindowAttribute(hwnd, 33, out cornerPreference, sizeof(int)) != 0 || cornerPreference != 2)) throw new Exception("Windows 둥근 모서리 설정 실패");
        var moddingLayout=window is MainWindow main?main.CheckModdingLayout("실제 HWND의 모딩 작업 영역"):null;
        var nativeCapture=capturePath is not null&&NativeWindowCapture.Save(hwnd,width,height,capturePath);
        var pointerInput=((MainWindow)window).CheckNativeDragInput();
        window.Hide();
        var perMonitorV2=AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(hwnd),new IntPtr(-4));
        return new { passed = true, width, height, clientWidth, clientHeight, captionHit, rightHit, closeButtonHit, tabHit,settingsHit,cornerPreference, layered = false,dpi,perMonitorV2,moddingLayout,nativeCapture,pointerInput, note = "화면 밖 실제 HWND의 프레임·버튼·DWM·DPI 검사 및 PrintWindow 캡처. 실제 다중 모니터 이동·사용자 모니터 픽셀 배열 검사는 아님." };
    }
}
