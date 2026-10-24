using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddOrdinarySimulorControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var scenario=c.OrdinarySimulor??new();
        void Edit(Func<OrdinarySimulorScenario,OrdinarySimulorScenario> edit)=>storedConditions=storedConditions with{OrdinarySimulor=edit(storedConditions.OrdinarySimulor??new())};
        var panel=Stack(Text("일반 시뮬러는 멀티샷 구체가 즉시 합체하며, 합체마다 수명이 20초로 갱신됩니다. 활성 구체는 4개, 특이점은 3개까지입니다. 아래 수치 미기재 항목에는 사이노이드의 값을 사용하지 않습니다."),Number("목표 지점 도착 (초)",c.SimulorArrivalSeconds,v=>storedConditions=storedConditions with{SimulorArrivalSeconds=v}),Number("미합체 구체 수명 가정 (초)",scenario.UnmergedLifetime,v=>Edit(s=>s with{UnmergedLifetime=v})),Number("특이점 3개일 때 단축 수명 (초)",scenario.LimitedLifetime,v=>Edit(s=>s with{LimitedLifetime=v})));
        var merge=new CheckBox{Content="같은 지점의 구체를 합체",IsChecked=c.SimulorMerge,Margin=new(0,4,0,8)};merge.Checked+=(_,_)=>{storedConditions=storedConditions with{SimulorMerge=true};Refresh();};merge.Unchecked+=(_,_)=>{storedConditions=storedConditions with{SimulorMerge=false};Refresh();};panel.Children.Add(merge);
        var mode=new ComboBox{ItemsSource=new[]{"완성 특이점에 추가 구체 · 원문 미명시","완성 특이점과 별도로 배치","계속 합체 · 중첩별 출력 필요"},SelectedIndex=scenario.VortexMerge=="Separate"?1:scenario.VortexMerge=="Merge"?2:0,Margin=new(0,4,0,8)};mode.SelectionChanged+=(_,_)=>{Edit(s=>s with{VortexMerge=mode.SelectedIndex==1?"Separate":mode.SelectedIndex==2?"Merge":"Unspecified"});Refresh();};panel.Children.Add(mode);
        foreach(var merging in new[]{true,false})
        {
            var rows=merging?scenario.Merge:scenario.Explosion;var values=Stack(Text("원문의 기본 모듈 값만으로 중첩별 증가식을 결정할 수 없습니다. 입력값은 모드 적용 전 Magnetic 피해·반경입니다."));
            foreach(var stack in new[]{2,3,4}.Concat(rows?.Select(r=>r.Stacks)??[]).Distinct().Order())
            {
                var row=rows?.SingleOrDefault(r=>r.Stacks==stack);
                void Set(bool damage,double? value){Edit(s=>{var entries=(merging?s.Merge:s.Explosion)??[];var old=entries.SingleOrDefault(r=>r.Stacks==stack)??new(stack,0,0);var next=entries.Where(r=>r.Stacks!=stack).Append(damage?old with{Damage=value??0}:old with{Radius=value??0}).ToArray();return merging?s with{Merge=next}:s with{Explosion=next};});}
                values.Children.Add(Stack(Text($"{stack}중첩"),Number("기본 피해",row?.Damage,v=>Set(true,v)),Number("반경 (m)",row?.Radius,v=>Set(false,v))));
            }
            values.Children.Add(Button("＋ 다음 중첩",()=>{Edit(s=>{var entries=(merging?s.Merge:s.Explosion)??[];var n=Math.Max(4,entries.Select(r=>r.Stacks).DefaultIfEmpty(4).Max())+1;return merging?s with{Merge=[..entries,new(n,0,0)]}:s with{Explosion=[..entries,new(n,0,0)]};});RefreshConditionControls();Refresh();}));
            panel.Children.Add(new Expander{Header=merging?"합체 피해 · 중첩별 시나리오":"기폭 피해 · 중첩별 시나리오",Content=values,Margin=new(0,4,0,8)});
        }
        panel.Children.Add(new Expander{Header="특이점 틱 · 원문 미기재 수치",Content=Stack(Number("방어 전 틱 출력 · 0은 피해 제외 가정",scenario.TickOutput,v=>Edit(s=>s with{TickOutput=v})),Number("피해 반경 (m)",scenario.TickRadius,v=>Edit(s=>s with{TickRadius=v})),Number("피해 주기 (초)",scenario.TickInterval,v=>Edit(s=>s with{TickInterval=v})),Number("생성 후 첫 틱 (초)",scenario.FirstTick,v=>Edit(s=>s with{FirstTick=v}))),Margin=new(0,4,0,8)});
        conditionPanel.Children.Add(new Expander{Header="일반 시뮬러 · 구체와 특이점",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
