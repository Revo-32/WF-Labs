using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddPullControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("원문은 끌어오기 범위와 효과를 명시하지만 속도·개별 경로는 명시하지 않습니다. 적이 이동하면 촉수·폭발·상태 범위 피해와 대상 사이 거리가 함께 달라집니다."));
        var choices=new[]{new ArcaneChoice("Unspecified","이동 경로 · 원문 수치 없음"),new ArcaneChoice("None","끌어오기 제외 · 피해만 계산"),new ArcaneChoice("Blocked","벽 등에 가로막혀 위치 유지"),new ArcaneChoice("Linear","지정 시간 동안 중심으로 직선 이동")};var choice=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=choices.Single(x=>x.Id==c.PullMotion),Margin=new(0,4,0,8)};choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is ArcaneChoice value){storedConditions=storedConditions with{PullMotion=value.Id};Refresh();}};panel.Children.Add(choice);panel.Children.Add(Number("중심까지 이동 시간 가정 (초)",c.PullSeconds,v=>storedConditions=storedConditions with{PullSeconds=v}));
        if(Selected?.Id==RangedPullWeapons.Proboscis)
        {
            panel.Children.Add(Number("명중 후 첫 촉수 타격 (초)",c.ProboscisTickOne,v=>storedConditions=storedConditions with{ProboscisTickOne=v}));panel.Children.Add(Number("명중 후 두 번째 촉수 타격 (초)",c.ProboscisTickTwo,v=>storedConditions=storedConditions with{ProboscisTickTwo=v}));panel.Children.Add(Text("폭발은 명중 후 1.7초입니다. 두 촉수 타격은 그 전에 발생해야 합니다. 촉수 반경 9m에는 반경 모드가 적용되지 않습니다."));
        }
        else if(Selected?.Id==RangedPullWeapons.Scyotid)
        {
            panel.Children.Add(Number("올가미 비행 시간 가정 (초) · 거리 0은 즉시",c.SnareFlightSeconds,v=>storedConditions=storedConditions with{SnareFlightSeconds=v}));
            var rounds=new[]{new ArcaneChoice("Unspecified","소수 대상 제한 · 원문 미명시"),new ArcaneChoice("Down","버림"),new ArcaneChoice("Up","올림")};var rounding=new ComboBox{ItemsSource=rounds,DisplayMemberPath="Name",SelectedItem=rounds.Single(x=>x.Id==c.SnareTargetRounding),Margin=new(0,4,0,8)};rounding.SelectionChanged+=(_,_)=>{if(rounding.SelectedItem is ArcaneChoice value){storedConditions=storedConditions with{SnareTargetRounding=value.Id};Refresh();}};panel.Children.Add(rounding);
            panel.Children.Add(Text("올가미는 최대 8명 × 멀티샷에 영향을 줍니다. 기본 사격 도중 보조 공격 사건을 추가하면 4발을 소비한 뒤 기본 사격으로 복귀합니다."));
        }
        conditionPanel.Children.Add(new Expander{Header="끌어오기 · 타격과 이동",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
