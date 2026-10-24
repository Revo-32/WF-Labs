using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal object CheckWorkflowUx()
    {
        var checks=new List<string>();
        void Yes(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
        void Click(string label)=>workflowBar.Children.OfType<RadioButton>().Single(b=>Equals(b.Content,label)).SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,true);
        ShowModding();
        Yes(root.Children.IndexOf(workflowBar)<root.Children.IndexOf(workspaceViewport),"Tab 탐색도 분류 선택 뒤 작업 입력으로 진행");
        Yes(workflowBar.Children.OfType<RadioButton>().Select(b=>b.Content).SequenceEqual(new[]{"주무기","보조무기","근접"}),"모딩 세 종류를 드롭다운 없이 노출");
        Click("주무기");Yes(page.Content==rangedPages["primary"],"주무기 버튼 한 번으로 주무기 모딩");
        var startingBuild=rangedPages["primary"].Capture();rangedPages["primary"].PrepareRivenAudit();rangedPages["primary"].CloseRivenAudit();
        var build=rangedPages["primary"].Capture();OpenCurrentRiven();
        Yes(!Descendants((DependencyObject)page.Content).OfType<WeaponSearchBox>().Any(),"총기 리벤 분석에 무기 검색 입력 없음");
        var primary=rangedRivenPages["primary"];primary.PrepareRecoveryCheck(true);var initial=primary.CaptureWorkspace();
        Click("산탄총");Yes(primary.RivenClass=="Shotgun","산탄총 독립 수치 범위 선택");Click("소총");
        Yes(primary.CaptureWorkspace().Selected==initial.Selected,"종류 전환 후 소총 분석 대상 보존");
        Yes(JsonSerializer.Serialize(initial.Drafts,Catalog.Json)==JsonSerializer.Serialize(primary.CaptureWorkspace().Drafts.Where(d=>initial.Drafts.Any(x=>x.WeaponId==d.WeaponId)),Catalog.Json),"종류 전환으로 작성 중 입력·세션을 초기화하지 않음");
        Click("권총");ReturnToModding();Yes(page.Content==rangedPages["primary"]&&JsonSerializer.Serialize(build,Catalog.Json)==JsonSerializer.Serialize(rangedPages["primary"].Capture(),Catalog.Json),"권총 분석 뒤 원래 주무기 빌드로 복귀");
        OpenCurrentRiven();Yes(primary.CaptureWorkspace().Drafts.Any(d=>d.Session?.Pending is not null),"모딩에서 다시 분석을 열어도 미채택 순환을 덮어쓰지 않음");
        OpenRivenClass("Melee");Yes(!Descendants((DependencyObject)page.Content).OfType<WeaponSearchBox>().Any(),"근접 리벤 분석에 무기 검색 입력 없음");
        var source=RangedEngine.Weapons.First(w=>w.Category=="primary"&&RangedEngine.ModCategory(w)=="Rifle"&&RangedEngine.N(w.Raw,"Disposition")>0);
        var target=RangedEngine.Weapons.First(w=>w.Category=="primary"&&RangedEngine.ModCategory(w)=="Rifle"&&w.Id!=source.Id&&RangedEngine.N(w.Raw,"Disposition")>0);
        var roll=new RangedRivenRoll(source.Id,8,[new("Damage",0),new("Multishot",0)],RivenBalance.Version,10,"Madurai");
        var bound=RivenBuildBinding.Bind(roll,build with{WeaponId=target.Id});
        Yes(bound.WeaponId==target.Id&&roll.WeaponId==source.Id&&RangedRivens.Range(roll,roll.Traits[0])==RangedRivens.Range(bound,bound.Traits[0]),"공통 옵션은 원본을 보존하고 현재 빌드에 같은 범위로 연결");
        var shotgun=RangedEngine.Weapons.First(w=>w.Category=="primary"&&RangedEngine.ModCategory(w)=="Shotgun");bool rejected=false;
        try{RivenBuildBinding.Bind(roll,build with{WeaponId=shotgun.Id});}catch(ArgumentException){rejected=true;}
        Yes(rejected,"다른 종류의 리벤을 자동 변환하거나 무기를 몰래 바꾸지 않음");
        ShowCatalog();Yes(workflowBar.Visibility==Visibility.Collapsed,"자료 화면에서 관계없는 무기 선택 숨김");
        ReturnToModding();Yes(lastModdingKind==1,"자료 열람 뒤에도 모딩 맥락 보존");
        rangedPages["primary"].Restore(startingBuild);
        return new{passed=true,checks};
    }
    internal void WorkflowPreview(string kind){if(kind=="primary"||kind=="secondary")ShowRanged(kind);else if(kind=="mod-melee")ShowModding();else OpenRivenClass(kind);}
}
