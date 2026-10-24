using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddTendrilControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        if(Selected is not {} w||!RangedTendrils.Applies(w))return;
        var panel=Stack(Text("표적을 획득하는 중에도 탄약을 소비합니다. 위키에 지연 수치가 없어 직접 설정하며, 0초는 이미 표적을 획득한 상태입니다."),Number("공통 표적 획득 지연 (초)",c.TendrilAcquisitionSeconds,v=>storedConditions=storedConditions with{TendrilAcquisitionSeconds=v}));
        if(w.Category=="primary")panel.Children.Add(Text("관통으로 직접 맞은 적은 분기 대상에서 제외합니다. 추가 대상을 찾을 수 있는 가장 먼 직격 지점 한 곳에서 최대 3개 촉수가 6m 내의 적에게 각각 70% 피해를 줍니다."));
        else if(c.TendrilRoutes is null)panel.Children.Add(Button("5개 촉수 대상 설정",()=>{storedConditions=storedConditions with{TendrilRoutes=Enumerable.Range(0,5).Select(_=>new RangedTendrilRoute(0)).ToArray()};RefreshConditionControls();Refresh();}));
        else for(int i=0;i<c.TendrilRoutes.Length;i++)
        {
            var index=i;var path=c.TendrilRoutes[i];void Edit(Func<RangedTendrilRoute,RangedTendrilRoute> edit){var values=storedConditions.TendrilRoutes!.ToArray();values[index]=edit(values[index]);storedConditions=storedConditions with{TendrilRoutes=values};Refresh();}
            var targets=new ComboBox{ItemsSource=Enumerable.Range(0,c.GroupCount).Select(n=>new ArcaneChoice(n.ToString(),$"대상 {n+1}")).Prepend(new("-1","붙지 않음 / 빗나감")).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=path.Miss?"-1":path.Target.ToString(),Margin=new(0,4,0,8)};targets.SelectionChanged+=(_,_)=>{if(targets.SelectedValue is string value){var n=int.Parse(value);Edit(p=>p with{Target=Math.Max(0,n),Miss=n<0});}};
            var auto=new CheckBox{Content="자동 표적 · 몸통에 부착",IsChecked=path.AutoTarget,Margin=new(0,4,0,8)};auto.Checked+=(_,_)=>Edit(p=>p with{AutoTarget=true});auto.Unchecked+=(_,_)=>Edit(p=>p with{AutoTarget=false});
            panel.Children.Add(Stack(Text($"촉수 {i+1}"),targets,auto,Number("개별 획득 지연 (초) · 빈칸은 공통값",path.AcquisitionSeconds,v=>Edit(p=>p with{AcquisitionSeconds=v}))));
        }
        conditionPanel.Children.Add(new Expander{Header="버미스플라이서 · 촉수 조준",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
