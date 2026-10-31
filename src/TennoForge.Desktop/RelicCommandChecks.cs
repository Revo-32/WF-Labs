using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private static async Task<(string[] Checks,string[] Limitations)> CheckRelicCommandMenus(RelicPage view,Action<FrameworkElement,string> capture)
    {
        var checks=new List<string>();var limitations=new List<string>();void Yes(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
        static KeyEventArgs KeyDown(UIElement target,Key key){var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),0,key){RoutedEvent=Keyboard.KeyDownEvent};target.RaiseEvent(e);return e;}
        view.ImportMenu.Focus();Invoke(view.ImportMenu);await Idle();
        Yes(view.ImportMenu.Menu.IsOpen&&view.LoadButton.IsVisible&&view.PasteButton.IsVisible,"수동 이미지 명령: 실제 버튼에서 테마 ContextMenu 열기");capture(view.ImportMenu.Menu,"input-menu-popup");
        bool focused=view.ImportMenu.Menu.Focus();var down=KeyDown(view.ImportMenu.Menu,Key.Down);await Idle();
        if(focused)Yes(Keyboard.FocusedElement is MenuItem first&&ReferenceEquals(first,view.LoadButton),"이미지 메뉴 방향키로 첫 항목 탐색");
        else
        {
            Yes(down.Handled&&ReferenceEquals(FocusManager.GetFocusedElement(view.ImportMenu.Menu),view.ImportMenu.Menu),"비활성 검수 HWND: 표준 ContextMenu Down handler 처리·논리 초점 유지");
            limitations.Add("비활성 격리 HWND가 popup의 키보드 초점/native 메뉴 모드를 허용하지 않아 방향키의 실제 선택 이동·Escape/Enter의 native 메뉴 종료·물리 초점 복귀는 미검증. 표준 Down handler의 routed event/논리 초점과 별도 UIA 명령·설정 적용을 검증함. 팝업은 close API로 정리함.");
        }
        if(focused)
        {
            KeyDown(view.LoadButton,Key.Escape);await Idle();
            Yes(!view.ImportMenu.Menu.IsOpen&&ReferenceEquals(Keyboard.FocusedElement,view.ImportMenu),"이미지 메뉴 Escape 닫기·원래 버튼 초점 복귀");
        }
        else{view.ImportMenu.Menu.IsOpen=false;await Idle();}
        var clipboard=view.ClipboardImage;view.ClipboardImage=()=>null;
        try
        {
            Invoke(view.ImportMenu);await Idle();
            if(focused){view.PasteButton.Focus();KeyDown(view.PasteButton,Key.Enter);await Idle();Yes(!view.ImportMenu.Menu.IsOpen,"이미지 메뉴 Enter 실행 뒤 메뉴 닫힘");}
            else{Invoke(view.PasteButton);await Idle();view.ImportMenu.Menu.IsOpen=false;await Idle();}
            Yes(view.Status.Text.Contains("클립보드에 이미지가 없습니다"),focused?"이미지 메뉴 Enter로 실제 붙여넣기 실행":"비활성 검수 HWND: 열린 메뉴 항목의 UIA Invoke로 실제 붙여넣기 실행");
        }
        finally{view.ClipboardImage=clipboard;}
        Invoke(view.ImportMenu);await Idle();
        if(ReferenceEquals(Mouse.Captured,view.ImportMenu.Menu))
        {
            view.ImportMenu.Menu.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownOutsideCapturedElementEvent});await Idle();
            Yes(!view.ImportMenu.Menu.IsOpen,"이미지 메뉴 바깥클릭 닫힘 · WPF capture 이벤트");
        }
        else{limitations.Add("격리 HWND에서 native mouse capture가 허용되지 않아 물리 바깥클릭 닫힘은 미검증. 메뉴 close API로 검수 팝업만 정리함.");view.ImportMenu.Menu.IsOpen=false;await Idle();}
        Invoke(view.RecognitionBar.SettingsButton);await Idle();
        Yes(view.RecognitionMenu.IsOpen,"자동인식 바로 옆 설정 버튼에서 메뉴 열기");
        capture(view.RecognitionMenu,"recognition-menu-popup");
        bool overlay=view.Overlay.IsChecked==true;var overlayItem=(MenuItem)view.RecognitionMenu.Items[1];
        ((IInvokeProvider)new MenuItemAutomationPeer(overlayItem).GetPattern(PatternInterface.Invoke)).Invoke();await Idle();
        Yes(view.Overlay.IsChecked!=overlay,"인식 설정 체크 메뉴에서 기존 오버레이 설정 적용");view.RecognitionMenu.IsOpen=false;view.Overlay.IsChecked=overlay;
        Invoke(view.PriceMenu);await Idle();var presence=(MenuItem)view.PriceMenu.Menu.Items[1];
        ((IExpandCollapseProvider)new MenuItemAutomationPeer(presence).GetPattern(PatternInterface.ExpandCollapse)).Expand();await Idle();
        capture(view.PriceMenu.Menu,"price-menu-popup");
        if(presence.Template.FindName("PART_Popup",presence) is System.Windows.Controls.Primitives.Popup{Child:FrameworkElement child})capture(child,"price-presence-popup");
        int previous=view.Presence.SelectedIndex,target=previous==0?1:0;Invoke((MenuItem)presence.Items[target]);await Idle();
        Yes(view.Presence.SelectedIndex==target,"시세 설정 하위 메뉴 실제 펼치기·UIA 항목 실행·거래자 필터 적용");
        if(focused)Yes(!view.PriceMenu.Menu.IsOpen,"시세 설정 항목 실행 후 표준 메뉴 닫힘");
        else view.PriceMenu.Menu.IsOpen=false;
        view.Presence.SelectedIndex=previous;
        return(checks.ToArray(),limitations.ToArray());
    }
}
