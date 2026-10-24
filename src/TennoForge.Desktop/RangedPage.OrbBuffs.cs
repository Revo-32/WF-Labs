using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddInfestedControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Number("구체 정지까지 (초) · 원문 미기재",c.InfestedStopSeconds,v=>storedConditions=storedConditions with{InfestedStopSeconds=v}),Number("첫 주기 타격 (초) · 0~1 시나리오",c.InfestedFirstPulse,v=>storedConditions=storedConditions with{InfestedFirstPulse=v}));
        var through=new CheckBox{Content="내 탄환이 구체를 통과하도록 조준",IsChecked=c.InfestedShootThrough,Margin=new(0,8,0,8)};through.Checked+=(_,_)=>{storedConditions=storedConditions with{InfestedShootThrough=true};Refresh();};through.Unchecked+=(_,_)=>{storedConditions=storedConditions with{InfestedShootThrough=false};Refresh();};panel.Children.Add(through);
        var merge=new CheckBox{Content="같은 정지 위치의 구체 합체",IsChecked=c.InfestedMerge,Margin=new(0,0,0,8)};merge.Checked+=(_,_)=>{storedConditions=storedConditions with{InfestedMerge=true};Refresh();};merge.Unchecked+=(_,_)=>{storedConditions=storedConditions with{InfestedMerge=false};Refresh();};panel.Children.Add(merge);
        var overflow=new ComboBox{ItemsSource=new[]{"2+2 합체의 초과분 · 원문 미확정","초과 1중첩 소실 가정","초과 1중첩 별도 유지 가정"},SelectedIndex=c.InfestedMergeOverflow=="Discard"?1:c.InfestedMergeOverflow=="Remainder"?2:0,Margin=new(0,0,0,8)};overflow.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{InfestedMergeOverflow=overflow.SelectedIndex==1?"Discard":overflow.SelectedIndex==2?"Remainder":"Unspecified"};Refresh();};panel.Children.Add(overflow);
        panel.Children.Add(Text("구체 발사는 탄약 5를 소비합니다. 보조 공격 사용 시점을 추가하면 구체를 발사한 뒤 기본 사격으로 돌아갑니다. 버스팅 매스 장착 시 재발사는 기존 구체를 기폭합니다. 구체 이동은 지정한 정지 시점까지의 직선 경로이며 도탄·외부 근접 밀어내기는 포함하지 않습니다."));
        conditionPanel.Children.Add(new Expander{Header="감염 구체 · 이동·합체·저장",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
    private void AddOrbBuffControls(RangedConditions c)
    {
        var panel=Stack(Text("탄환이 실제로 통과하는 감염 구체를 순서대로 추가합니다. 구체 자체의 합체 수와 탄환이 지나는 구체 수는 서로 다릅니다."));
        for(int i=0;i<(c.InfestedOrbs?.Length??0);i++)
        {
            var index=i;var row=new DockPanel{Margin=new(0,4,0,4)};var remove=Button("×",()=>{storedConditions=storedConditions with{InfestedOrbs=storedConditions.InfestedOrbs!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();});remove.ToolTip="통과 구체 삭제";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var choice=new ComboBox{ItemsSource=new[]{"1중첩 구체 · 전기 +100%","2중첩 구체 · 전기 +366%","3중첩 구체 · 전기 +500%"},SelectedIndex=c.InfestedOrbs![i]-1,Margin=new(0,0,8,0)};choice.SelectionChanged+=(_,_)=>{var values=storedConditions.InfestedOrbs!.ToArray();values[index]=choice.SelectedIndex+1;storedConditions=storedConditions with{InfestedOrbs=values};Refresh();};row.Children.Add(choice);panel.Children.Add(row);
        }
        panel.Children.Add(Button("＋ 통과 구체",()=>{storedConditions=storedConditions with{InfestedOrbs=[..storedConditions.InfestedOrbs??[],1]};RefreshConditionControls();Refresh();}));
        if(c.InfestedOrbs is {Length:>0})
        {
            var elements=new[]{new ArcaneChoice("None","추가 전기 결합 · 명확하면 자동")}.Concat(new[]{DamageType.Heat,DamageType.Cold,DamageType.Toxin,DamageType.Radiation,DamageType.Magnetic,DamageType.Corrosive}.Select(t=>new ArcaneChoice(t.ToString(),Labels.Damage(t)))).ToArray();var element=new ComboBox{ItemsSource=elements,DisplayMemberPath="Name",SelectedItem=elements.Single(x=>x.Id==(c.OrbElectricDestination?.ToString()??"None")),Margin=new(0,8,0,8)};element.SelectionChanged+=(_,_)=>{if(element.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{OrbElectricDestination=v.Id=="None"?null:Enum.Parse<DamageType>(v.Id)};Refresh();}};panel.Children.Add(element);
            var inherit=new ComboBox{ItemsSource=new[]{"파생 광역 버프 상속 · 원문 미확정","광역 공격도 버프 적용 가정","광역 공격은 버프 제외 가정"},SelectedIndex=c.OrbBuffAoe is null?0:c.OrbBuffAoe.Value?1:2,Margin=new(0,0,0,8)};inherit.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{OrbBuffAoe=inherit.SelectedIndex==0?null:inherit.SelectedIndex==1};Refresh();};panel.Children.Add(inherit);
            panel.Children.Add(Text($"최종 피해 ×{RangedOrbBuffs.FinalMultiplier(c):0.####} · 고정 치확 +{.25*c.InfestedOrbs.Length:P0} · 치피 모드 +25% 한 번"));
        }
        var simulor=Stack(Text("시뮬러와 사이노이드 시뮬러의 통과 전기는 다른 원소와 결합하지 않습니다."));
        for(int i=0;i<(c.SimulorOrbs?.Length??0);i++)
        {
            var index=i;var row=new DockPanel{Margin=new(0,4,0,4)};var remove=Button("×",()=>{storedConditions=storedConditions with{SimulorOrbs=storedConditions.SimulorOrbs!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();});remove.ToolTip="시뮬러 통과 구체 삭제";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var choice=new ComboBox{ItemsSource=new[]{"1중첩 · 전기 +100%","2중첩 · 전기 +300%","3중첩 · 전기 +400%","4중첩 · 전기 +500%"},SelectedIndex=c.SimulorOrbs![i]-1,Margin=new(0,0,8,0)};choice.SelectionChanged+=(_,_)=>{var values=storedConditions.SimulorOrbs!.ToArray();values[index]=choice.SelectedIndex+1;storedConditions=storedConditions with{SimulorOrbs=values};Refresh();};row.Children.Add(choice);simulor.Children.Add(row);
        }
        simulor.Children.Add(Button("＋ 시뮬러 통과 구체",()=>{storedConditions=storedConditions with{SimulorOrbs=[..storedConditions.SimulorOrbs??[],1]};RefreshConditionControls();Refresh();}));
        if(c.SimulorOrbs is {Length:>0}&&(c.InfestedOrbs?.Length??0)==0)
        {
            var inherit=new ComboBox{ItemsSource=new[]{"파생 광역 버프 상속 · 원문 미확정","광역 공격도 버프 적용 가정","광역 공격은 버프 제외 가정"},SelectedIndex=c.OrbBuffAoe is null?0:c.OrbBuffAoe.Value?1:2,Margin=new(0,4,0,8)};inherit.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{OrbBuffAoe=inherit.SelectedIndex==0?null:inherit.SelectedIndex==1};Refresh();};simulor.Children.Add(inherit);
        }
        if(c.SimulorOrbs is {Length:>0}&&c.InfestedOrbs is {Length:>0})
        {
            var mixed=new ComboBox{ItemsSource=new[]{"두 구체 종류의 치피 중첩 · 원문 미확정","공유 보너스 +25% 시나리오","각 종류 +25%, 합계 +50% 시나리오"},SelectedIndex=c.MixedOrbCriticalStacks is null?0:c.MixedOrbCriticalStacks.Value?2:1,Margin=new(0,4,0,8)};mixed.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{MixedOrbCriticalStacks=mixed.SelectedIndex==0?null:mixed.SelectedIndex==2};Refresh();};simulor.Children.Add(mixed);
        }
        conditionPanel.Children.Add(new Expander{Header="외부 시뮬러 구체 · 탄환 통과",Content=simulor,IsExpanded=c.SimulorOrbs is {Length:>0},Margin=new(0,4,0,12)});
        conditionPanel.Children.Add(new Expander{Header="외부 감염 구체 · 탄환 통과",Content=panel,IsExpanded=c.InfestedOrbs is {Length:>0},Margin=new(0,4,0,12)});
    }
}
