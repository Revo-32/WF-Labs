using System.Runtime.InteropServices;
using System.Windows;

namespace TennoForge.Desktop;

// Showing a Topmost WPF window establishes its initial band. Tracking the game
// must not repeatedly promote it above other apps' overlays in that band.
internal sealed class OverlayPositioner
{
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    internal const uint PassiveFlags=0x0010|0x0004|0x0200; // NOACTIVATE | NOZORDER | NOOWNERZORDER
    private Int32Rect? last;
    internal int Writes {get;private set;}
    internal void Apply(IntPtr hwnd,Int32Rect rect)
    {
        if(last==rect)return;
        if(!SetWindowPos(hwnd,IntPtr.Zero,rect.X,rect.Y,rect.Width,rect.Height,PassiveFlags))
            throw new InvalidOperationException("오버레이 위치를 갱신하지 못했습니다.",new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        last=rect;Writes++;
    }
}
