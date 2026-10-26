using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckWeaponSelection(Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var saved=ReadBuild();
        void Yes(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        ShowModding();await Idle();checks.AddRange(CheckWeaponSearchUx());
        CheckClickRegions(weapon,Yes);
        try
        {
            weaponClass.SelectedIndex=0;weapon.SelectedItem=catalog.Weapons.Single(w=>w.EnglishName=="Ceramic Dagger");await Idle();
            Yes(incarnonMenu.IsVisible&&incarnonChoice.IsEnabled,"근접 지원 무기 선택 직후 인카논 진입 표시");
            Invoke(incarnonMenu.Entry);await Idle();Yes(incarnonMenu.Popup.IsOpen&&incarnonChoice.IsVisible,"근접 인카논 버튼 실제 팝업 열기");
            ((IToggleProvider)new CheckBoxAutomationPeer(incarnonChoice).GetPattern(PatternInterface.Toggle)).Toggle();await Idle();
            Yes(ReadBuild().Melee?.Incarnon==true,"근접 변신 토글이 계산 입력에 반영");
            var choice=evolutionChoices.OrderBy(x=>x.Key).First().Value;choice.SelectedIndex=1;await Idle();
            Yes(ReadBuild().Melee!.Evolutions!.Contains(((EvolutionChoice)choice.SelectedItem).Perk!.Id),"근접 진화 선택이 계산 입력에 반영");
            _=DamageEngine.Calculate(catalog,ReadBuild() with{Stance=null});
            render((FrameworkElement)incarnonMenu.Popup.Child,"melee-incarnon",400,540,1);
            Yes(LayoutDiagnostics.Inspect((FrameworkElement)incarnonMenu.Popup.Child).Length==0,"근접 인카논 팝업 겹침·잘림 없음");
            var build=ReadBuild();incarnonMenu.Popup.IsOpen=false;ApplyBuild(build);await Idle();Yes(incarnonChoice.IsChecked==true&&evolutionChoices.Values.Any(c=>c.SelectedIndex==1),"근접 변신·진화 복원");
            weapon.SelectedItem=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");await Idle();
            Yes(incarnonMenu.Visibility==Visibility.Collapsed&&!incarnonChoice.IsEnabled,"인카논 미지원 무기에서 잘못된 변신 방지");
            foreach(var kind in new[]{"primary","secondary"})
            {
                ShowRanged(kind);await Idle();checks.AddRange(await rangedPages[kind].CheckIncarnonSelection(render));
            }
        }
        finally{ShowModding();ApplyBuild(saved);}
        return new{passed=true,checks=checks.Count,details=checks};
    }
    internal static void CheckClickRegions(WeaponSearchBox box,Action<bool,string> yes)
    {
        box.ApplyTemplate();var original=box.SelectedItem;
        foreach(var part in new[]{"SearchBorder","Query","Open"})
        {
            box.CancelSearch();var element=(UIElement)box.Template.FindName(part,box);
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
            yes(box.IsDropDownOpen&&box.Suggestions.Items.Count>1&&Equals(box.SelectedItem,original),part+" 클릭은 목록만 열고 무기 유지");
        }
        box.QueryInput.Text="없는무기zzzz";yes(box.IsDropDownOpen&&box.Suggestions.Items.Count==0&&Equals(box.SelectedItem,original),"열린 목록에서 검색해도 임의 확정 없음");box.CancelSearch();
    }
}

internal sealed partial class RangedPage
{
    internal async Task<string[]> CheckIncarnonSelection(Action<FrameworkElement,string,int,int,double> render)
    {
        var saved=Capture();var checks=new List<string>();
        void Yes(bool value,string label){if(!value)throw new Exception(category+": "+label);checks.Add(category+": "+label);}
        try
        {
            MainWindow.CheckClickRegions(weapon,Yes);
            weapon.SelectedItem=weapon.Items.Cast<Weapon>().Single(w=>w.EnglishName==(category=="primary"?"Braton Prime":"Lato Prime"));
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Yes(incarnonMenu.IsVisible,"무기명 옆 인카논 진입 표시");
            ((IInvokeProvider)new ButtonAutomationPeer(incarnonMenu.Entry).GetPattern(PatternInterface.Invoke)).Invoke();
            await Task.Delay(100);Yes(incarnonMenu.Popup.IsOpen,"실제 버튼으로 팝업 열기");
            var ordinary=RangedEngine.Calculate(Capture()).ExpectedShot;
            var form=LayoutDiagnostics.Tree((FrameworkElement)incarnonMenu.Popup.Child).OfType<ComboBox>().First();form.SelectedIndex=1;
            await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Yes(RangedIncarnons.IsForm(Selected!,Capture().AttackIndex),"인카논 형태가 공격 구성요소에 반영");
            Yes(Math.Abs(RangedEngine.Calculate(Capture()).ExpectedShot-ordinary)>.01,"인카논 전환 후 실제 피해 수치 변경");
            var choice=LayoutDiagnostics.Tree((FrameworkElement)incarnonMenu.Popup.Child).OfType<ComboBox>().Skip(1).First();choice.SelectedIndex=1;
            Yes(Capture().Evolutions?.Length==1,"선택한 진화가 저장·계산 입력에 반영");
            render((FrameworkElement)incarnonMenu.Popup.Child,category+"-incarnon",400,540,1);
            Yes(LayoutDiagnostics.Inspect((FrameworkElement)incarnonMenu.Popup.Child).Length==0,"인카논 팝업 겹침·잘림 없음");
            var build=Capture();incarnonMenu.Popup.IsOpen=false;Restore(build);Yes(Capture().AttackIndex==build.AttackIndex&&Capture().Evolutions!.SequenceEqual(build.Evolutions!),"형태·진화 복원");
        }
        finally{Restore(saved);}return checks.ToArray();
    }
}
