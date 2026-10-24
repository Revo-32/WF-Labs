using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal async Task<object> CheckPopupLayout()
    {
        var checks=new List<object>();
        async Task Settle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
        async Task Verify(Popup popup,string name)
        {
            popup.Opacity=0;popup.IsOpen=true;await Settle();var child=(FrameworkElement)popup.Child;
            var origin=PointToScreen(new(0,0));var end=PointToScreen(new(ActualWidth,ActualHeight));
            var tl=child.PointToScreen(new(0,0));var br=child.PointToScreen(new(child.ActualWidth,child.ActualHeight));
            if(tl.X<origin.X-2||tl.Y<origin.Y-2||br.X>end.X+2||br.Y>end.Y+2)throw new Exception($"{name}: 팝업이 앱 밖으로 나감: {tl}..{br}, 앱 {origin}..{end}");
            checks.Add(new{name,width=child.ActualWidth,height=child.ActualHeight,contained=true});
        }
        foreach(var size in new[]{new Size(1920,1000),new Size(1760,920),new Size(1280,680)})
        {
            MinWidth=0;MinHeight=0;Width=size.Width;Height=size.Height;ShowModding();await Settle();
            if(size.Width>=1760&&(workspaceViewport.ComputedHorizontalScrollBarVisibility==Visibility.Visible||workspaceViewport.ComputedVerticalScrollBarVisibility==Visibility.Visible))throw new Exception($"최소 지원 크기에서 불필요한 외부 스크롤바: {size}, actual={workspaceViewport.ActualWidth}x{workspaceViewport.ActualHeight}, viewport={workspaceViewport.ViewportWidth}x{workspaceViewport.ViewportHeight}, page={page.ActualWidth}x{page.ActualHeight}");
            await Verify(settingsPopup,"설정 "+size);await Verify(capacityPopup!,"수용량 "+size);
            if(settingsPopup.IsOpen)throw new Exception("수용량을 연 뒤 설정 팝업이 남아 있습니다.");
            capacityPopup!.Child.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(capacityPopup.Child),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            await Settle();if(capacityPopup.IsOpen)throw new Exception("Esc로 수용량 팝업이 닫히지 않습니다.");
            checks.Add(new{name="Esc로 닫기 "+size,contained=true});await Verify(capacityPopup,"수용량 다시 열기 "+size);
            foreach(var kind in new[]{"exilus","melee","zaw"})
            {
                OpenEquipmentPicker(kind,false);await Verify(equipmentPicker!,kind+" "+size);
                if(capacityPopup!.IsOpen)throw new Exception("장비 선택 뒤 수용량 팝업이 남아 있습니다.");
                ShowRivens();await Settle();if(equipmentPicker?.IsOpen==true)throw new Exception("페이지 이동 뒤 팝업이 남아 있습니다.");ShowModding();await Settle();
            }
            if(size.Width<1760)
            {
                if(workspaceViewport.ScrollableWidth<=0||workspaceViewport.ScrollableHeight<=0)throw new Exception("작은 작업 영역의 초과 내용에 접근할 수 없습니다.");
                workspaceViewport.ScrollToEnd();workspaceViewport.ScrollToRightEnd();await Settle();
                if(Math.Abs(workspaceViewport.VerticalOffset-workspaceViewport.ScrollableHeight)>1||Math.Abs(workspaceViewport.HorizontalOffset-workspaceViewport.ScrollableWidth)>1)throw new Exception("작업 영역 끝에 도달하지 못했습니다.");
                checks.Add(new{name="작은 화면의 가로·세로 끝까지 접근",contained=true});workspaceViewport.ScrollToTop();workspaceViewport.ScrollToLeftEnd();
            }
        }
        return new{passed=true,checks};
    }
}
