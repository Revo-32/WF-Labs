using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckDisplayedCapture(Action<bool,string> yes)
    {
        // An inactive private desktop has no displayed framebuffer. Verify native
        // coordinates here; capture freshness is checked against the live game.
        if(Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")!="1")return;
        var window=new Window{Owner=this,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,Width=480,Height=300,Left=80,Top=100};
        async Task Paint(){window.UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);await Task.Delay(50);}
        try
        {
            window.Show();await Paint();var handle=new WindowInteropHelper(window).Handle;
            var first=RelicCapture.ClientBounds(handle);var point=window.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(window);
            yes(first.X==(int)point.X&&first.Y==(int)point.Y&&first.X!=0&&first.Y!=0,"네이티브 캡처 좌표: 원점 밖 창의 화면 좌표");
            yes(first.Width==(int)Math.Round(window.ActualWidth*dpi.DpiScaleX)&&first.Height==(int)Math.Round(window.ActualHeight*dpi.DpiScaleY),"네이티브 캡처 좌표: DPI 적용 실제 클라이언트 크기");
            window.Left=185;window.Top=165;await Paint();
            var moved=RelicCapture.ClientBounds(handle);point=window.PointToScreen(new Point());
            yes(moved.X==(int)point.X&&moved.Y==(int)point.Y&&moved.X!=first.X&&moved.Y!=first.Y,"네이티브 창 이동 후 캡처 좌표 갱신");
        }
        finally{window.Close();}
    }
}
