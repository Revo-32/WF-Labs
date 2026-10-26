using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace TennoForge.Desktop;

// WindowChrome restores SystemColors.WindowColor when detached. That is correct
// for an opaque window, but destroys the clear surface of a per-pixel overlay.
// Keep the layered HWND for its lifetime and provide only native frame hit tests.
internal sealed class LayeredOverlayFrame
{
    private readonly Window window;
    private readonly Func<bool> editing;
    private HwndSource? source;
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    internal LayeredOverlayFrame(Window target,Func<bool> isEditing)
    {
        window=target;editing=isEditing;
        target.SourceInitialized+=(_,_)=>{source=HwndSource.FromHwnd(new WindowInteropHelper(target).Handle);source.AddHook(WndProc);EnsureTransparent();};
        target.DpiChanged+=(_,_)=>EnsureTransparent();target.Closed+=(_,_)=>{if(source is {IsDisposed:false})source.RemoveHook(WndProc);source=null;};
    }
    internal void EnsureTransparent()
    {
        if(source is {IsDisposed:false,CompositionTarget: {} target}&&target.BackgroundColor.A!=0)target.BackgroundColor=Colors.Transparent;
    }
    private IntPtr WndProc(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam,ref bool handled)
    {
        if(message==0x83) // WM_NCCALCSIZE: custom frame is entirely inside the client surface.
        {handled=true;return IntPtr.Zero;}
        if(message==0x86) // WM_NCACTIVATE: do not paint an opaque native caption.
        {handled=true;return new(1);}
        if(message!=0x84||!editing()||!GetWindowRect(hwnd,out var rect))return IntPtr.Zero;
        long packed=lparam.ToInt64();int x=unchecked((short)(packed&0xffff)),y=unchecked((short)((packed>>16)&0xffff));
        var dpi=VisualTreeHelper.GetDpi(window);int bx=(int)Math.Ceiling(7*dpi.DpiScaleX),by=(int)Math.Ceiling(7*dpi.DpiScaleY);
        bool left=x<rect.Left+bx,right=x>=rect.Right-bx,top=y<rect.Top+by,bottom=y>=rect.Bottom-by;
        int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
        if(hit==1&&y<rect.Top+34*dpi.DpiScaleY)
        {
            var point=window.PointFromScreen(new Point(x,y));var element=window.InputHitTest(point) as DependencyObject;
            bool control=false;while(element is not null&&element!=window)
            {if(element is ButtonBase){control=true;break;}element=element is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element);}
            if(!control)hit=2;
        }
        handled=true;return new(hit);
    }
}
