using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddOrbInteractionControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("외부 탄환 흡수와 도탄·근접 밀치기를 시간순으로 지정합니다. 구체 번호는 생성 순서이며 1부터 시작합니다. 물리 엔진의 임의 경로를 만들지 않고 지정한 위치까지 직선 이동합니다."));
        for(int i=0;i<(c.OrbInteractions?.Length??0);i++)
        {
            var index=i;var row=c.OrbInteractions![i];void Change(Func<RangedOrbInteraction,RangedOrbInteraction> edit){var rows=storedConditions.OrbInteractions!.ToArray();rows[index]=edit(rows[index]);storedConditions=storedConditions with{OrbInteractions=rows};Refresh();}
            var kinds=new[]{new ArcaneChoice("Absorb","외부 탄환 흡수"),new ArcaneChoice("Bounce","지형 도탄"),new ArcaneChoice("MeleePush","근접 타격으로 이동")};var kind=new ComboBox{ItemsSource=kinds,DisplayMemberPath="Name",SelectedItem=kinds.Single(k=>k.Id==row.Kind)};kind.SelectionChanged+=(_,_)=>{if(kind.SelectedItem is ArcaneChoice k){Change((RangedOrbInteraction r)=>r with{Kind=k.Id});RefreshConditionControls();}};
            var body=Stack(kind,Number("교전 시작 후 시각 (초)",row.Time,v=>Change((RangedOrbInteraction r)=>r with{Time=v??double.NaN})),Number("구체 번호",row.Orb+1,v=>Change((RangedOrbInteraction r)=>r with{Orb=v is {} n&&n==Math.Truncate(n)?(int)n-1:-1})));
            if(row.Kind=="Absorb")
            {
                body.Children.Add(Text("탄환의 치명타·팩션 적용 전 피해를 입력하세요. 구체의 전기 추가와 0.66 배율, 장착한 버스팅 매스의 저장 계수는 자동 적용합니다."));
                var elements=Stack();foreach(var type in Enum.GetValues<DamageType>().Where(t=>t<=DamageType.Corrosive))elements.Children.Add(Number(Labels.Damage(type),row.Packet?.GetValueOrDefault(type)??0,v=>Change((RangedOrbInteraction r)=>r with{Packet=new Dictionary<DamageType,double>(r.Packet??[]){[type]=v??double.NaN}})));body.Children.Add(new Expander{Header="흡수 탄환의 속성별 피해",Content=elements,IsExpanded=true,Margin=new(0,4,0,8)});
                body.Children.Add(Number("전기 추가 계산의 기본 피해",row.ElementalBasis,v=>Change((RangedOrbInteraction r)=>r with{ElementalBasis=v??double.NaN})));
            }
            else
            {
                body.Children.Add(Number("도착 위치 · 전방 (m)",row.X,v=>Change((RangedOrbInteraction r)=>r with{X=v??double.NaN})));
                body.Children.Add(Number("도착 위치 · 측면 (m)",row.Y,v=>Change((RangedOrbInteraction r)=>r with{Y=v??double.NaN})));
                body.Children.Add(Number("도착까지 이동 시간 (초)",row.TravelSeconds,v=>Change((RangedOrbInteraction r)=>r with{TravelSeconds=v??double.NaN})));
            }
            body.Children.Add(Button("사건 삭제",()=>{storedConditions=storedConditions with{OrbInteractions=storedConditions.OrbInteractions!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();}));
            panel.Children.Add(new Expander{Header=$"{row.Time:0.##}초 · 구체 {row.Orb+1}",Content=body,IsExpanded=true,Margin=new(0,4,0,8)});
        }
        panel.Children.Add(Button("＋ 구체 사건",()=>{storedConditions=storedConditions with{OrbInteractions=[..storedConditions.OrbInteractions??[],new(1,0,"MeleePush")]};RefreshConditionControls();Refresh();}));
        conditionPanel.Children.Add(new Expander{Header="감염 구체 · 외부 사격과 이동",Content=panel,IsExpanded=(c.OrbInteractions?.Length??0)>0,Margin=new(0,4,0,12)});
    }
}
