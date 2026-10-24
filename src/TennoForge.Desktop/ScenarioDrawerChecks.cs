using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckScenarioDrawers()
    {
        var checks=new List<string>();var savedMotion=ScenarioDrawer.ReduceMotion;
        void Yes(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        try
        {
            ScenarioDrawer.ReduceMotion=false;
            foreach(var kind in new[]{"melee","primary","secondary"})
            {
                if(kind=="melee")ShowModding();else ShowRanged(kind);UpdateLayout();
                var drawer=kind=="melee"?scenarioDrawer!:rangedPages[kind].DrawerForCheck;
                string Build()=>kind=="melee"?JsonSerializer.Serialize(ReadBuild(),Catalog.Json):JsonSerializer.Serialize(rangedPages[kind].Capture(),Catalog.Json);
                var before=Build();
                Yes(!drawer.IsOpen&&drawer.HandleForCheck.ActualHeight>=40,kind+": 닫힌 초기 상태와 하단 진입 버튼");
                var handle=drawer.HandleForCheck;
                Yes(Math.Abs(handle.ActualWidth-drawer.ActualWidth/3)<1,kind+": 손잡이는 전체 너비의 1/3");
                Yes(Math.Abs(handle.TranslatePoint(new(),drawer).X-(drawer.ActualWidth-handle.ActualWidth)/2)<1,kind+": 손잡이는 창 하단 중앙 정렬");
                Yes(handle.Template.FindName("Contour",handle) is System.Windows.Shapes.Path {Data:not null},kind+": 사다리꼴 벡터 윤곽");
                drawer.HandleForCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(80);
                if(SystemParameters.ClientAreaAnimation)Yes(drawer.MotionOffsetForCheck>0&&drawer.MotionOffsetForCheck<drawer.Sheet.ActualHeight,kind+": 패널이 하단에서 올라오는 중간 프레임");
                await Task.Delay(220);UpdateLayout();
                Yes(drawer.IsOpen&&drawer.Sheet.IsVisible,kind+": 하단 버튼 클릭으로 열기");
                var bounds=drawer.Sheet.TransformToAncestor(drawer).TransformBounds(new Rect(drawer.Sheet.RenderSize));
                Yes(bounds.Width>drawer.ActualWidth-5&&bounds.Height>300&&bounds.Top>=0&&bounds.Bottom<=drawer.ActualHeight+1,kind+": 패널이 창 안에 배치");
                Yes(Math.Abs(bounds.Top-(handle.TranslatePoint(new(),drawer).Y+handle.ActualHeight))<1,kind+": 올라온 손잡이와 패널 상단이 연결됨");
                Yes(KeyboardNavigation.GetTabNavigation(drawer.Sheet)==KeyboardNavigationMode.Cycle,kind+": 설정 내부 Tab 순환");
                drawer.CloseForCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(220);UpdateLayout();
                Yes(!drawer.IsOpen&&!drawer.Sheet.IsVisible,kind+": 완료 버튼으로 닫기");
                Yes(Math.Abs(handle.TranslatePoint(new(),drawer).Y+handle.ActualHeight-drawer.ActualHeight)<1,kind+": 닫으면 손잡이가 창 하단으로 복귀");
                drawer.SetOpen(true,false);
                var dropdown=LayoutDiagnostics.Tree(drawer.Sheet).OfType<ComboBox>().First(c=>c.IsVisible&&c.IsEnabled);dropdown.IsDropDownOpen=true;
                drawer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(drawer)!,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Yes(drawer.IsOpen,kind+": 드롭다운을 펼친 상태의 Esc는 패널을 닫지 않음");dropdown.IsDropDownOpen=false;
                drawer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(drawer)!,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});await Task.Delay(220);
                Yes(!drawer.IsOpen,kind+": Esc 닫기");
                drawer.SetOpen(true);drawer.SetOpen(false);drawer.SetOpen(true);await Task.Delay(270);
                Yes(drawer.IsOpen&&drawer.Sheet.IsVisible,kind+": 빠른 재열기로 이전 닫기 애니메이션 취소");
                drawer.SetOpen(false,false);ScenarioDrawer.ReduceMotion=true;drawer.SetOpen(true);
                Yes(drawer.IsOpen&&drawer.SettledForCheck,kind+": 경량 모드 즉시 열기");
                drawer.SetOpen(false);Yes(before==Build(),kind+": 열기·닫기로 입력 빌드가 변경되지 않음");
                drawer.SetOpen(true,false);ShowCatalog();UpdateLayout();
                Yes(!drawer.IsOpen,kind+": 다른 탭 이동 시 패널 닫힘");ScenarioDrawer.ReduceMotion=false;
            }
        }
        finally{ScenarioDrawer.ReduceMotion=savedMotion;ShowModding();UpdateLayout();}
        return new{passed=true,checks};
    }
}
