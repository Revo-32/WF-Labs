using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddReturningBladeControls(RangedConditions c,Func<string,double,Action<double>,FrameworkElement> Number,Func<string,bool,Action<bool>,CheckBox> Toggle)
    {
        var panel=Stack(Text("재장전 때 설치 칼날이 돌아옵니다. 모든 칼날에 아래 경로를 공통 적용합니다. 실제 경로는 적·플레이어 위치에 따라 달라집니다."));
        foreach(var entry in (c.ReturningBladeHits??[]).Select((hit,index)=>(hit,index)))
        {
            void Change(Func<RangedCollision,RangedCollision> edit){var hits=storedConditions.ReturningBladeHits!.ToArray();hits[entry.index]=edit(hits[entry.index]);storedConditions=storedConditions with{ReturningBladeHits=hits};}
            panel.Children.Add(Stack(Text($"복귀 명중 {entry.index+1}",15),Number("재장전 시작 후 (초)",entry.hit.AfterImpact,v=>Change(h=>h with{AfterImpact=v})),Number("대상 번호 · 1부터",entry.hit.Target+1,v=>Change(h=>h with{Target=(int)v-1})),Toggle("복귀 때 약점 명중",entry.hit.Headshot,v=>Change(h=>h with{Headshot=v})),Button("명중 삭제",()=>{storedConditions=storedConditions with{ReturningBladeHits=storedConditions.ReturningBladeHits!.Where((_,i)=>i!=entry.index).ToArray()};RefreshConditionControls();Refresh();})));
        }
        panel.Children.Add(Button("＋ 복귀 경로의 적",()=>{var used=(storedConditions.ReturningBladeHits??[]).Select(h=>h.Target).ToHashSet();var next=Enumerable.Range(0,storedConditions.GroupCount).FirstOrDefault(i=>!used.Contains(i),-1);if(next<0)return;storedConditions=storedConditions with{ReturningBladeHits=[..storedConditions.ReturningBladeHits??[],new(.5,next)]};RefreshConditionControls();Refresh();}));
        panel.Children.Add(Button("복귀 칼날 모두 빗나감",()=>{storedConditions=storedConditions with{ReturningBladeHits=[]};RefreshConditionControls();Refresh();}));
        if(c.ReturningBladeHits is null)panel.Children.Add(Text("복귀 경로 미지정 · 재장전 전까지의 직접 피해만 계산할 수 있습니다."));
        panel.Children.Add(Toggle("제이드 앙상블 · 다른 두 무기 버프 활성",c.JadeEnsemble,v=>{storedConditions=storedConditions with{JadeEnsemble=v,CantareBuffSeconds=0};RefreshConditionControls();}));
        panel.Children.Add(Number("시작 시 합창 버프 잔여 시간 (초)",c.CantareBuffSeconds,v=>storedConditions=storedConditions with{CantareBuffSeconds=v}));
        conditionPanel.Children.Add(new Expander{Header="칸타레 · 복귀 경로와 합창",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
