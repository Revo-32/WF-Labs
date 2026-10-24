using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TennoForge.Desktop;
internal static class Backdrop
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    public static bool Apply(Window window, bool lightweight, bool dark = true)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return false;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        var useDark=dark?1:0;DwmSetWindowAttribute(hwnd,20,ref useDark,sizeof(int));
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = (Color)ColorConverter.ConvertFromString(dark?"#101112":"#F4F4F2");
        var margins = new Margins();
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var corners = 2; DwmSetWindowAttribute(hwnd, 33, ref corners, sizeof(int));
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return false;
        var type = 1; // Opaque dashboard: no full-window glass surface under small text.
        return DwmSetWindowAttribute(hwnd, 38, ref type, sizeof(int)) == 0;
    }
}
