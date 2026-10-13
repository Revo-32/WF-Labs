using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private string[] CheckWeaponSearchUx()
    {
        var checks=new List<string>();
        void Yes(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        void Key(WeaponSearchBox box,System.Windows.Input.Key key)
        {
            var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(this),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};box.QueryInput.RaiseEvent(args);
        }
        foreach(var box in new[]{weapon,rivenWeapon})
        {
            if(box==weapon)ShowModding();else ShowRivens();
            UpdateLayout();box.ApplyTemplate();
            var original=(Weapon)box.SelectedItem;var version=generation;var rivenVersion=rivenCompareGeneration;var session=rivenSession;var baseline=rivenBaseline;
            var changes=0;SelectionChangedEventHandler observe=(_,_)=>changes++;box.SelectionChanged+=observe;
            box.QueryInput.Focus();box.QueryInput.Text="nikana";
            Yes(box.IsDropDownOpen&&box.Suggestions.Items.Count>=2&&box.Suggestions.Items.Cast<Weapon>().All(w=>w.EnglishName.Contains("Nikana",StringComparison.OrdinalIgnoreCase)),"한/영 검색 후보 목록 · "+(box==weapon?"모딩":"리벤")+$" open={box.IsDropDownOpen}, query={box.QueryInput.Text}, count={box.Suggestions.Items.Count}, matches={string.Join(",",box.Suggestions.Items.Cast<Weapon>().Take(5).Select(w=>w.EnglishName))}");
            Key(box,System.Windows.Input.Key.Down);Key(box,System.Windows.Input.Key.Down);Key(box,System.Windows.Input.Key.Up);
            Yes((Weapon)box.SelectedItem==original&&changes==0&&generation==version&&rivenCompareGeneration==rivenVersion&&session==rivenSession&&baseline==rivenBaseline,"입력·화살표 탐색은 무기·계산·리벤 작업을 변경하지 않음");
            var highlight=box.Suggestions.SelectedIndex;
            var composition=new TextComposition(InputManager.Current,box.QueryInput,"니");
            box.QueryInput.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});
            Key(box,System.Windows.Input.Key.Down);Key(box,System.Windows.Input.Key.Enter);
            Yes(box.Suggestions.SelectedIndex==highlight&&changes==0,"IME 조합 중 방향키·Enter로 무기를 확정하지 않음");
            box.QueryInput.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.TextInputEvent,Handled=true});
            Key(box,System.Windows.Input.Key.Down);Yes(box.Suggestions.SelectedIndex==highlight+1,"IME 조합 완료 뒤 후보 탐색 복구");
            Key(box,System.Windows.Input.Key.Escape);
            Yes(!box.IsDropDownOpen&&box.QueryInput.Text==((Weapon)original).Name&&changes==0,"Esc 취소: 원래 선택명 복원");
            box.QueryInput.Text="없는무기zzzz";Key(box,System.Windows.Input.Key.Down);Key(box,System.Windows.Input.Key.Enter);
            Yes(box.Suggestions.Items.Count==0&&(Weapon)box.SelectedItem==original&&changes==0,"검색 결과 없음: Enter로 임의 선택하지 않음");
            box.QueryInput.Text="니카나";
            Yes(box.Suggestions.Items.Count>=2,"한국어 부분 일치 검색");
            var target=box.Suggestions.Items.Cast<Weapon>().First(w=>w!=original);var index=box.Suggestions.Items.IndexOf(target);
            for(var n=0;n<=index;n++)Key(box,System.Windows.Input.Key.Down);
            Key(box,System.Windows.Input.Key.Enter);
            Yes((Weapon)box.SelectedItem==target&&changes==1&&!box.IsDropDownOpen,"방향키와 Enter로 정확히 한 번 선택 확정");
            box.QueryInput.Text="nikana";box.Suggestions.UpdateLayout();
            var mouseTarget=box.Suggestions.Items.Cast<Weapon>().First(w=>w!=target);
            box.Suggestions.ScrollIntoView(mouseTarget);box.Suggestions.UpdateLayout();
            var row=(ListBoxItem)box.Suggestions.ItemContainerGenerator.ContainerFromItem(mouseTarget);
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
            Yes((Weapon)box.SelectedItem==mouseTarget&&changes==2,"후보 클릭으로 선택 확정");
            box.QueryInput.Text="nikana";Key(box,System.Windows.Input.Key.Tab);
            Yes(!box.IsDropDownOpen&&box.QueryInput.Text==mouseTarget.Name,"Tab 이탈은 미확정 검색 취소");
            box.SelectionChanged-=observe;box.SelectedItem=original;
        }
        return checks.ToArray();
    }
}
