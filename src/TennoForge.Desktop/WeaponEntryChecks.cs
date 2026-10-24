using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal object CheckWeaponEntries(Action<FrameworkElement,string,int,int> render)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
        var saved=ReadBuild();ShowModding();render(this,"melee",1760,920);
        weapon.ApplyTemplate();weapon.Filter("");Yes(weapon.Suggestions.Items.Cast<Weapon>().All(w=>!ZawCatalog.IsZaw(w.Id)),"일반 무기 후보에서 모든 조우 조합 제외");
        var zaw=OpenZawBuilder(false);render(zaw,"zaw-builder",440,650);
        var selectors=LayoutDiagnostics.Tree(zaw).OfType<ComboBox>().ToArray();selectors[0].SelectedIndex=0;selectors[1].SelectedIndex=0;selectors[2].SelectedIndex=0;
        var expected=ZawCatalog.Id(((ZawStrike)selectors[0].SelectedItem).Id,((ZawGrip)selectors[1].SelectedItem).Id,((ZawLink)selectors[2].SelectedItem).Id);
        LayoutDiagnostics.Tree(zaw).OfType<Button>().Single(b=>b.Content as string=="이 조우 사용").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Yes(((Weapon)weapon.SelectedItem).Id==expected,"조우 전용 조립으로 선택·계산 연결");
        var reopened=OpenZawBuilder(false);render(reopened,"zaw-restored",440,650);var restoredParts=LayoutDiagnostics.Tree(reopened).OfType<ComboBox>().ToArray();
        Yes(((ZawStrike)restoredParts[0].SelectedItem).Id==((ZawStrike)selectors[0].SelectedItem).Id,"조우 재편집에서 현재 부품 유지");
        var lich=catalog.Weapons.First(w=>MeleeRuleCatalog.Load().Weapon(w.Id).Raw.TryGetProperty("IsLichWeapon",out var v)&&v.ValueKind==JsonValueKind.True);
        weapon.SelectedItem=lich;attackMode.SelectedIndex=1;board.ClearMods();bonusElementChoice.SelectedIndex=5;bonusPercent.Text="25";var first=DamageEngine.Calculate(catalog,ReadBuild()).ExpectedHit;bonusPercent.Text="60";var second=DamageEngine.Calculate(catalog,ReadBuild()).ExpectedHit;
        Yes(lichFields!.Visibility==Visibility.Visible&&second>first,"근접 베일런스 25→60% 입력과 계산 증가");render(this,"melee-valence",1760,920);Yes(LayoutDiagnostics.Inspect(root).Length==0,"근접 베일런스 표시에서 잘림 없음");Yes(board.MeasureLibrary(root).FullyVisibleRows>=2,"근접 베일런스 표시 중 보관함 두 줄");
        ApplyBuild(saved);
        foreach(var kind in new[]{"primary","secondary"}){ShowRanged(kind);render(this,kind,1760,920);checks.AddRange(rangedPages[kind].CheckWeaponEntries((element,name,width,height)=>render(element,kind+"-"+name,width,height),()=>render(this,kind+"-valence",1760,920)));}
        ShowModding();return new{passed=true,checks};
    }
}
internal sealed partial class RangedPage
{
    internal string[] CheckWeaponEntries(Action<FrameworkElement,string,int,int> render,Action capturePage)
    {
        var saved=Capture();var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new Exception(category+": "+name);checks.Add(category+": "+name);}
        try
        {
            Yes(kitgunButton.Visibility==Visibility.Visible&&kitgunButton.ActualWidth>0,"일반 무기에서 키트건 버튼 노출");weapon.ApplyTemplate();weapon.Filter("");Yes(weapon.Suggestions.Items.Cast<Weapon>().All(w=>RangedKitguns.Get(w.Id) is null),"일반 검색에 키트건 부품 조합 없음");
            var before=JsonSerializer.Serialize(Capture(),Catalog.Json);var dialog=CreateKitgunDialog()!;render(dialog,"kitgun",480,850);
            var controls=LayoutDiagnostics.Tree((FrameworkElement)dialog.Content).OfType<ComboBox>().ToArray();var chamber=controls.Single(c=>c.SelectedItem is KitgunDefinition);
            chamber.SelectedIndex=chamber.Items.Count-1;var chosen=(KitgunDefinition)chamber.SelectedItem;
            Yes(before==JsonSerializer.Serialize(Capture(),Catalog.Json),"챔버 미리보기만으로 현재 빌드 변경 안 함");
            LayoutDiagnostics.Tree((FrameworkElement)dialog.Content).OfType<Button>().Single(b=>b.Content as string=="적용").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(Capture().WeaponId==chosen.WeaponId&&Capture().Kitgun is not null,"챔버·그립·로더 적용과 저장 연결");var assembled=Capture();Restore(assembled);Yes(Capture().Kitgun==assembled.Kitgun,"키트건 부품 복원");
            var cancelled=CreateKitgunDialog()!;render(cancelled,"kitgun-cancel",480,850);before=JsonSerializer.Serialize(Capture(),Catalog.Json);LayoutDiagnostics.Tree((FrameworkElement)cancelled.Content).OfType<Button>().Single(b=>b.Content as string=="취소").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Yes(before==JsonSerializer.Serialize(Capture(),Catalog.Json),"키트건 취소 시 빌드 유지");
            var lich=weapon.Items.Cast<Weapon>().First(w=>RangedEngine.Weapons.Single(x=>x.Id==w.Id).Raw.TryGetProperty("IsLichWeapon",out var flag)&&flag.ValueKind==JsonValueKind.True);
            weapon.SelectedItem=lich;bonusPercent.Text="25";var low=RangedEngine.Calculate(Capture()).ExpectedShot;bonusPercent.Text="60";var high=RangedEngine.Calculate(Capture()).ExpectedShot;
            Yes(bonusRow.Visibility==Visibility.Visible&&high>low,"베일런스 25→60% 입력과 계산 증가");var bonusSaved=Capture();Restore(bonusSaved);Yes(Capture().Progenitor==bonusSaved.Progenitor,"베일런스 원소·수치 저장 복원");capturePage();
            Yes(LayoutDiagnostics.Inspect(this).Length==0,"베일런스 표시에서 잘림 없음");var second=(FrameworkElement)library.Children[library.Columns];Yes(second.TransformToAncestor(libraryScroller).TransformBounds(new Rect(second.RenderSize)).Bottom<=libraryScroller.ActualHeight+.5,"베일런스 표시 중 보관함 두 줄");
            bonusPercent.Text="61";bool rejected=false;try{RangedEngine.Calculate(Capture());}catch(ArgumentException){rejected=true;}Yes(rejected,"60% 초과 입력 거부");
        }
        finally{Restore(saved);}return checks.ToArray();
    }
}
