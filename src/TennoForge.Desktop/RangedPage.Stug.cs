using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddStugControls(RangedConditions c,Func<string,double,Action<double>,FrameworkElement> Number,Func<string,double?,Action<double?>,FrameworkElement> OptionalNumber,Func<string,bool,Action<bool>,CheckBox> Toggle)
    {
        var panel=Stack(Toggle("같은 지점에 쏘아 구체 중첩",c.StugMerge,v=>storedConditions=storedConditions with{StugMerge=v}),Toggle("인카논 어댑터 설치됨",c.StugAdapterInstalled,v=>storedConditions=storedConditions with{StugAdapterInstalled=v}),OptionalNumber("중간 중첩 폭발 반경 (m) · 필요할 때만 지정",c.StugExplosionRadius,v=>storedConditions=storedConditions with{StugExplosionRadius=v}),Text("일반 구체는 10중첩, 인카논은 5중첩에서 즉시 폭발합니다. 그 전에는 마지막 부착 후 1.5초에 폭발합니다. 위키에 없는 중간 중첩의 정확한 반경은 중심의 단일 대상 외 계산에서 별도 입력이 필요합니다."));
        panel.Children.Add(Text("인카논 자탄 · 도탄 경로",16));
        panel.Children.Add(Text("모든 자탄에 같은 경로를 적용합니다. 시간은 최초 구체 폭발 이후이며 최대 3회입니다."));
        foreach(var entry in (c.StugBounces??[]).Select((hit,index)=>(hit,index)))
        {
            void Change(Func<RangedCollision,RangedCollision> edit){var hits=storedConditions.StugBounces!.ToArray();hits[entry.index]=edit(hits[entry.index]);storedConditions=storedConditions with{StugBounces=hits};}
            panel.Children.Add(Stack(Text($"도탄 {entry.index+1}",15),Number("구체 폭발 이후 (초)",entry.hit.AfterImpact,v=>Change(h=>h with{AfterImpact=v})),Number("폭발 중심의 대상 번호 · 1부터",entry.hit.Target+1,v=>Change(h=>h with{Target=(int)v-1})),Button("도탄 삭제",()=>{storedConditions=storedConditions with{StugBounces=storedConditions.StugBounces!.Where((_,i)=>i!=entry.index).ToArray()};RefreshConditionControls();Refresh();})));
        }
        panel.Children.Add(Button("＋ 도탄 위치",()=>{if((storedConditions.StugBounces?.Length??0)>=3)return;storedConditions=storedConditions with{StugBounces=[..storedConditions.StugBounces??[],new((storedConditions.StugBounces?.LastOrDefault()?.AfterImpact??0)+.5)]};RefreshConditionControls();Refresh();}));
        panel.Children.Add(Button("자탄 모두 빗나감",()=>{storedConditions=storedConditions with{StugBounces=[]};RefreshConditionControls();Refresh();}));
        if(c.StugBounces is null)panel.Children.Add(Text("인카논 자탄 경로 미지정"));
        conditionPanel.Children.Add(new Expander{Header="스터그 · 부착과 중첩",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
