using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddGrenadeEventControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("이동 중 기폭은 기폭 시각의 좌표를 지정합니다. 착지 위치는 유지하고, 대상 부착은 그 대상의 이동을 따릅니다. 같은 발사의 멀티샷 유탄은 같은 경로를 사용하는 시나리오입니다."));
        var water=new ComboBox{ItemsSource=new[]{"일반 펜타 물 접촉 · 문서 상충으로 미지정","물 표면에서 즉시 기폭","물 표면에서 멈춤"},SelectedIndex=c.PentaWaterDetonates is null?0:c.PentaWaterDetonates.Value?1:2};water.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{PentaWaterDetonates=water.SelectedIndex==0?null:water.SelectedIndex==1};Refresh();};panel.Children.Add(water);
        panel.Children.Add(Text("테더 그레네이즈는 물·타르 접촉 시 즉시 기폭합니다."));
        for(int i=0;i<(c.GrenadeEvents?.Length??0);i++)
        {
            var index=i;var row=c.GrenadeEvents![i];void Change(Func<RangedGrenadeEvent,RangedGrenadeEvent> edit){var rows=storedConditions.GrenadeEvents!.ToArray();rows[index]=edit(rows[index]);storedConditions=storedConditions with{GrenadeEvents=rows};Refresh();}
            var kinds=new[]{new ArcaneChoice("Position","이동 중 현재 좌표"),new ArcaneChoice("Land","지형에 착지"),new ArcaneChoice("Attach","대상·사체에 부착"),new ArcaneChoice("Water","물 표면 접촉"),new ArcaneChoice("Tar","타르 접촉")};var kind=new ComboBox{ItemsSource=kinds,DisplayMemberPath="Name",SelectedItem=kinds.Single(k=>k.Id==row.Kind)};kind.SelectionChanged+=(_,_)=>{if(kind.SelectedItem is ArcaneChoice k){Change((RangedGrenadeEvent r)=>r with{Kind=k.Id});RefreshConditionControls();}};
            var body=Stack(kind,Number("교전 시작 후 시각 (초)",row.Time,v=>Change((RangedGrenadeEvent r)=>r with{Time=v??double.NaN})),Number("몇 번째 발사의 유탄",row.Shot,v=>Change((RangedGrenadeEvent r)=>r with{Shot=v is {} n&&n==Math.Truncate(n)?(int)n:-1})));
            if(row.Kind=="Attach")body.Children.Add(Number("부착 대상 번호 · 1부터",row.Target+1,v=>Change((RangedGrenadeEvent r)=>r with{Target=v is {} n&&n==Math.Truncate(n)?(int)n-1:null})));
            else
            {
                body.Children.Add(Number("좌표 · 전방 (m)",row.X,v=>Change((RangedGrenadeEvent r)=>r with{X=v??double.NaN})));
                body.Children.Add(Number("좌표 · 측면 (m)",row.Y,v=>Change((RangedGrenadeEvent r)=>r with{Y=v??double.NaN})));
            }
            body.Children.Add(Button("사건 삭제",()=>{storedConditions=storedConditions with{GrenadeEvents=storedConditions.GrenadeEvents!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();}));
            panel.Children.Add(new Expander{Header=$"{row.Time:0.##}초 · 유탄 발사 {row.Shot}",Content=body,IsExpanded=true,Margin=new(0,4,0,8)});
        }
        panel.Children.Add(Button("＋ 유탄 경로 사건",()=>{storedConditions=storedConditions with{ManualGrenades=true,GrenadeEvents=[..storedConditions.GrenadeEvents??[],new(1,1,"Position")]};RefreshConditionControls();Refresh();}));
        conditionPanel.Children.Add(new Expander{Header="펜타 · 이동 중 기폭·부착·환경 접촉",Content=panel,IsExpanded=(c.GrenadeEvents?.Length??0)>0,Margin=new(0,4,0,12)});
    }
}
