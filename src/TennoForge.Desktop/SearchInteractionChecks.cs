using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckSearchInteractions(Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var initialFont=UiFonts.Current.Id;
        void Yes(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
        async Task Settle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        async Task Value(TextBox input,string text){((IValueProvider)UIElementAutomationPeer.CreatePeerForElement(input)!.GetPattern(PatternInterface.Value)).SetValue(text);await Settle();}
        async Task Invoke(UIElement button){((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)).Invoke();await Settle();}
        void Key(TextBox box,Key key)=>box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(box),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
        async Task Field(SearchField field,string name,FrameworkElement surface)
        {
            var input=field.Input;var saved=input.Text;
            var width=surface==Content?1760:(int)Math.Ceiling(surface.ActualWidth);
            var height=surface==Content?920:(int)Math.Ceiling(surface.ActualHeight);
            void Shot(string state)=>render(surface,name+"-"+state,width,height,1);
            try
            {
                await Value(input,"");settingsButton.Focus();await Settle();
                Yes(field.Hint.Visibility==Visibility.Visible,name+": 빈 검색창 안내 표시");Shot("empty");
                UIElementAutomationPeer.CreatePeerForElement(input)!.SetFocus();
                Yes(input.IsKeyboardFocusWithin&&field.Hint.Visibility==Visibility.Collapsed,name+": 포커스 즉시 안내 제거·커서 겹침 없음");
                await Settle();Yes(input.CaretIndex==0&&input.Text.Length==0,name+": 안내 문구는 실제 입력값이 아님");Shot("focused");
                var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input);
                var contentHost=(FrameworkElement)host.Template.FindName("PART_ScrollContentPresenter",host);var availableWidth=contentHost.ActualWidth;
                var caret=input.GetRectFromCharacterIndex(0);
                var hintX=field.Hint.TranslatePoint(new(),input).X;
                Yes(!caret.IsEmpty&&Math.Abs(caret.Left-hintX)<=4,name+$": 커서와 안내 문구 시작점 일치 ({caret.Left:0.##}/{hintX:0.##})");
                await Value(input,"검증용없는이름zzzz");
                Yes(field.Hint.Visibility==Visibility.Collapsed&&field.ClearButton.IsVisible,name+": 입력 후 지우기 버튼 표시");
                Yes(Math.Abs(contentHost.ActualWidth-availableWidth)<1,name+": 지우기 버튼 출현에도 입력 너비 유지");Shot("no-results");
                input.Focus();input.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Yes(field.ClearButton.IsKeyboardFocused,name+": Tab으로 검색어 지우기에 이동");
                await Invoke(field.ClearButton);
                Yes(input.Text==""&&input.IsKeyboardFocusWithin&&field.Hint.Visibility==Visibility.Collapsed,name+": 지우기 실행 후 검색창에 포커스 유지");
                await Value(input,"치명타");input.Focus();
                var composition=new TextComposition(InputManager.Current,input,"ㄱ");
                input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});
                Key(input,System.Windows.Input.Key.Escape);
                Yes(input.Text=="치명타",name+": IME 조합 중 Escape가 검색어를 지우지 않음");
                input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.TextInputEvent,Handled=true});
                Key(input,System.Windows.Input.Key.Escape);await Settle();
                Yes(input.Text==""&&input.IsKeyboardFocusWithin,name+": 조합 종료 뒤 Escape로 검색어만 지움");
                Yes(field.Hint.Visibility==Visibility.Collapsed,name+": 지운 직후에도 커서와 안내가 겹치지 않음");
                await Value(input,string.Concat(Enumerable.Repeat("긴 한국어 검색어와 English ",12)));input.CaretIndex=input.Text.Length;await Settle();
                var clear=field.ClearButton.TransformToAncestor(field).TransformBounds(new Rect(field.ClearButton.RenderSize));
                var text=contentHost.TransformToAncestor(field).TransformBounds(new Rect(contentHost.RenderSize));
                Yes(text.Right<=clear.Left+1&&clear.Right<=field.ActualWidth,name+": 긴 입력도 지우기 버튼과 겹치지 않음");
                await Invoke(field.ClearButton);settingsButton.Focus();await Settle();
                Yes(field.Hint.Visibility==Visibility.Visible,name+": 빈 상태로 이탈하면 안내 복원");
            }
            finally{input.Text=saved;}
        }
        try
        {
            ShowModding();await Settle();
            var probe=LayoutDiagnostics.Tree(board.LibraryView).OfType<ComboBox>().First();
            probe.IsDropDownOpen=true;await Settle();
            var probePopup=(Popup)probe.Template.FindName("PART_Popup",probe);
            Yes(!double.IsNaN(UiMotion.Angle(probePopup)),"드롭다운 모션 연결: "+UiMotion.Diagnostic(probe));probe.IsDropDownOpen=false;
            foreach(var dark in new[]{true,false})
            {
                SetDarkTheme(dark);SetLightMode(false);
                foreach(var kind in new[]{"melee","primary","secondary"})
                {
                    if(kind=="melee")ShowModding();else ShowRanged(kind);await Settle();
                    await Field(LayoutDiagnostics.Tree((DependencyObject)page.Content).OfType<SearchField>().Single(f=>f.IsVisible),$"{kind}-{dark}",(FrameworkElement)Content);
                }
                ShowCatalog();await Settle();await Field(LayoutDiagnostics.Tree((DependencyObject)page.Content).OfType<SearchField>().Single(),$"catalog-{dark}",(FrameworkElement)Content);
                ShowModding();await Settle();
                foreach(var kind in new[]{"exilus","melee","zaw"})
                {
                    OpenEquipmentPicker(kind);await Settle();var popup=equipmentPicker!;
                    await Field(LayoutDiagnostics.Tree(popup.Child).OfType<SearchField>().Single(),$"picker-{kind}-{dark}",(FrameworkElement)popup.Child);
                    var field=LayoutDiagnostics.Tree(popup.Child).OfType<SearchField>().Single();field.Input.Focus();Key(field.Input,System.Windows.Input.Key.Escape);await Settle();
                    Yes(!popup.IsOpen,$"picker-{kind}-{dark}: 빈 검색창 Escape로 선택창 닫기");CloseEquipmentPicker();
                }
                foreach(var kind in new[]{"melee","primary","secondary"})
                {
                    if(kind=="melee")ShowModding();else ShowRanged(kind);ShowRivens();
                    if(kind=="melee")SelectRivenSectionForCheck(2);else rangedRivenPages[kind].SelectSectionForCheck(2);
                    await Settle();await Field(LayoutDiagnostics.Tree((DependencyObject)page.Content).OfType<SearchField>().Single(f=>f.IsVisible),$"riven-{kind}-{dark}",(FrameworkElement)Content);
                    foreach(var option in LayoutDiagnostics.Tree((DependencyObject)page.Content).OfType<RivenOptionInput>().Where(o=>o.IsVisible).ToArray())
                    {
                        var previous=option.Value.Text;await Value(option.Value,"");settingsButton.Focus();await Settle();
                        Yes(option.ValueHint.Visibility==Visibility.Visible,$"riven-{kind}-{dark}/{option.Index}: 수치 안내 표시");
                        option.Value.Focus();Yes(option.ValueHint.Visibility==Visibility.Collapsed,$"riven-{kind}-{dark}/{option.Index}: 수치 입력 포커스에 안내 즉시 제거");
                        await Value(option.Value,"123.4");Yes(option.ValueHint.Visibility==Visibility.Collapsed,$"riven-{kind}-{dark}/{option.Index}: 실제 수치와 안내 겹침 없음");
                        await Value(option.Value,"");Yes(option.ValueHint.Visibility==Visibility.Collapsed,$"riven-{kind}-{dark}/{option.Index}: 수치 삭제 후 포커스 상태 유지");
                        option.Value.Text=previous;
                    }
                }
                ShowModding();await Settle();
                var combo=LayoutDiagnostics.Tree(board.LibraryView).OfType<ComboBox>().First();
                var peer=UIElementAutomationPeer.CreatePeerForElement(combo)!;
                var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);
                var dropdown=(Popup)combo.Template.FindName("PART_Popup",combo);
                expand.Expand();await Settle();
                Yes(dropdown.IsOpen&&!double.IsNaN(UiMotion.Angle(dropdown)),"일반 드롭다운 열림 표시 연결: "+dropdown.IsOpen+" / "+UiMotion.Angle(dropdown)+" / "+UiMotion.Diagnostic(combo));
                if(UiMotion.Enabled)Yes(UiMotion.Animating(dropdown),"일반 모드에서 화살표 전환 애니메이션 실행");
                await Task.Delay(180);Yes(Math.Abs(UiMotion.Angle(dropdown)-180)<.1,"열린 드롭다운 화살표가 위를 향함");
                expand.Collapse();expand.Expand();await Task.Delay(180);Yes(Math.Abs(UiMotion.Angle(dropdown)-180)<.1,"빠른 열기·닫기에서도 마지막 상태 유지");
                expand.Collapse();await Task.Delay(180);Yes(Math.Abs(UiMotion.Angle(dropdown))<.1,"닫힌 드롭다운 화살표 복원");
                expand.Expand();SetLightMode(true);Yes(!UiMotion.Animating(dropdown)&&UiMotion.Angle(dropdown)==180,"경량 모드 전환 즉시 진행 중 모션 종료");
                expand.Collapse();await Settle();Yes(!UiMotion.Animating(dropdown)&&UiMotion.Angle(dropdown)==0,"경량 모드에서 애니메이션 없이 상태 전환");SetLightMode(false);
                weapon.IsDropDownOpen=true;await Task.Delay(180);Yes(UiMotion.Angle(weapon.SearchPopup)==180,"무기 검색에도 같은 열림 표시");weapon.CancelSearch();await Task.Delay(180);
            }
            checks.AddRange(await CheckWeaponSearchUx());
            foreach(var font in UiFonts.Choices)foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})
            {
                UiFonts.Apply(font.Id);SetDarkTheme(dark);ShowModding();await Settle();
                var field=LayoutDiagnostics.Tree(board.LibraryView).OfType<SearchField>().Single();
                await Value(field.Input,"");field.Input.Focus();await Settle();
                render((FrameworkElement)Content,$"focus-{font.Id}-{dark}-{scale}",1760,920,scale);
                var caret=field.Input.GetRectFromCharacterIndex(0);
                Yes(!caret.IsEmpty&&Math.Abs(caret.Left-(field.Input.Padding.Left+1))<4&&field.Hint.Visibility==Visibility.Collapsed,$"{font.Id}/{dark}/{scale}: 입력 시작점과 빈 포커스 상태");
            }
            return new{passed=true,method="UIA Focus·Value·Invoke·ExpandCollapse, 실제 WPF Tab 탐색·IME 조합 이벤트·Escape·모션 최종 상태",checks};
        }
        finally{CloseEquipmentPicker();UiFonts.Apply(initialFont);SetLightMode(false);SetDarkTheme(true);ShowModding();}
    }
}
