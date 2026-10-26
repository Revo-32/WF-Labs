using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TennoForge.Desktop;

internal static class OverlayPositionerChecks
{
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    internal static void Run(Window overlay)
    {
        var hwnd=new WindowInteropHelper(overlay).Handle;var positioner=new OverlayPositioner();
        var rect=new Int32Rect(-29000,-29000,340,510);positioner.Apply(hwnd,rect);
        var peer=new Window{Width=50,Height=50,Left=-28000,Top=-28000,ShowActivated=false,ShowInTaskbar=false,Topmost=true,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
            peer.Show();var foreground=RelicCapture.GetForegroundWindow();var previous=GetWindow(hwnd,3);
            for(int i=0;i<120;i++)positioner.Apply(hwnd,rect);
            if(positioner.Writes!=1)throw new InvalidOperationException("정지한 오버레이가 네이티브 위치를 반복 갱신함");
            positioner.Apply(hwnd,new Int32Rect(-28950,-28950,360,510));
            if(positioner.Writes!=2||RelicCapture.GetForegroundWindow()!=foreground||GetWindow(hwnd,3)!=previous)
                throw new InvalidOperationException("오버레이 이동이 다른 창의 표시 순서 또는 포커스를 변경함");
        }
        finally{peer.Close();}
    }
}
