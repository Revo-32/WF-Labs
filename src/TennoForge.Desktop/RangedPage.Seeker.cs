using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddSeekerControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        if(kitgun is not {Arcane:"PaxSeeker"} pax)return;
        var panel=Stack(Text($"약점 처치 시 유도탄 {pax.ArcaneRank+1}개를 생성합니다. 위키에 없는 유도 속도·무작위 대상 배분은 아래 경로 시나리오를 사용합니다."));
        if(c.PaxSeekerPaths?.Length!=pax.ArcaneRank+1)panel.Children.Add(Button("유도탄 경로 설정",()=>{storedConditions=storedConditions with{PaxSeekerPaths=Enumerable.Range(0,pax.ArcaneRank+1).Select(_=>new RangedSeekerPath(0,null)).ToArray()};RefreshConditionControls();Refresh();}));
        else for(int i=0;i<c.PaxSeekerPaths.Length;i++)
        {
            var index=i;var path=c.PaxSeekerPaths[i];void Edit(Func<RangedSeekerPath,RangedSeekerPath> change){var values=storedConditions.PaxSeekerPaths!.ToArray();values[index]=change(values[index]);storedConditions=storedConditions with{PaxSeekerPaths=values};Refresh();}
            var target=new ComboBox{ItemsSource=Enumerable.Range(0,c.GroupCount).Select(n=>new ArcaneChoice(n.ToString(),$"대상 {n+1}")).Prepend(new("-1","지형에 막힘 / 빗나감")).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=path.Miss?"-1":path.Target.ToString(),Margin=new(0,4,0,8)};
            target.SelectionChanged+=(_,_)=>{if(target.SelectedValue is string value){var n=int.Parse(value);Edit(p=>p with{Target=Math.Max(0,n),Miss=n<0});}};
            panel.Children.Add(Stack(Text($"유도탄 {i+1}"),target,Number("생성 후 도착까지 (초)",path.Flight,v=>Edit(p=>p with{Flight=v}))));
        }
        var repeat=new ComboBox{ItemsSource=new[]{"유도탄 자체 처치의 재발동 · 원문 미확정","유도탄 자체 처치는 재발동 안 함","유도탄 자체 처치도 재발동"},SelectedIndex=c.PaxSeekerRetrigger is null?0:c.PaxSeekerRetrigger.Value?2:1,Margin=new(0,8,0,8)};repeat.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{PaxSeekerRetrigger=repeat.SelectedIndex==0?null:repeat.SelectedIndex==2};Refresh();};panel.Children.Add(repeat);
        panel.Children.Add(Text("개별 유도탄은 대상 머리를 향하며 캐치문만 약점 배율을 받지 않습니다. 처치한 적을 경로 대상으로 지정하면 사망한 대상을 다시 공격하지 않습니다."));
        conditionPanel.Children.Add(new Expander{Header="팍스 시커 · 유도탄 경로",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
