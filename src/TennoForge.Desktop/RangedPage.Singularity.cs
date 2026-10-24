using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddSingularityControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("명중 시 즉시 폭발하거나 랭크에 따른 확률로 5초 특이점을 생성합니다. 특이점이 생긴 구체는 폭발하지 않으며 합체·도탄도 없습니다. 아래 원문 미기재 항목은 선택한 시나리오로만 계산합니다."));
        var brackets=new[]{new ArcaneChoice("Unspecified","피해 증가 브라켓 · 선택 필요"),new ArcaneChoice("Additive","기본 피해 모드와 가산하는 시나리오"),new ArcaneChoice("Separate","별도 최종 배율 시나리오")};
        var bracket=new ComboBox{ItemsSource=brackets,DisplayMemberPath="Name",SelectedItem=brackets.Single(x=>x.Id==c.SingularityDamageBracket),Margin=new(0,8,0,8)};bracket.SelectionChanged+=(_,_)=>{if(bracket.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{SingularityDamageBracket=v.Id};Refresh();}};panel.Children.Add(bracket);
        var multishot=new ComboBox{ItemsSource=new[]{"멀티샷 처리 · 원문 미기재","독립 구체마다 확률 판정","한 구체의 값으로 합산"},SelectedIndex=c.SingularityIndependentOrbs is null?0:c.SingularityIndependentOrbs.Value?1:2,Margin=new(0,0,0,8)};multishot.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{SingularityIndependentOrbs=multishot.SelectedIndex==0?null:multishot.SelectedIndex==1};Refresh();};panel.Children.Add(multishot);
        panel.Children.Add(Number("명중까지 비행 시간 (초)",c.SimulorArrivalSeconds,v=>storedConditions=storedConditions with{SimulorArrivalSeconds=v}));
        panel.Children.Add(Number("끌어오기 반경 가정 (m)",c.SingularityPullRadius,v=>storedConditions=storedConditions with{SingularityPullRadius=v}));
        panel.Children.Add(Number("활성 특이점 한도 · 원문 미기재",c.SingularityLimit,v=>storedConditions=storedConditions with{SingularityLimit=v is {} n&&n==Math.Truncate(n)?(int)n:null}));
        panel.Children.Add(Number("아군에게 부착 · 대상 배열 축의 시작 위치 (m) · 빈칸은 적 조준",c.SingularityAllyPosition,v=>storedConditions=storedConditions with{SingularityAllyPosition=v}));
        panel.Children.Add(Number("아군 이동 속도 (m/s) · 지정 직선 경로",c.SingularityAllySpeed,v=>storedConditions=storedConditions with{SingularityAllySpeed=v??double.NaN}));
        var allyFailure=new ComboBox{ItemsSource=new[]{"아군 접촉 시 생성 실패 · 원문 미기재","주변에 즉시 폭발하는 시나리오","폭발하지 않는 시나리오"},SelectedIndex=c.SingularityAllyFailedExplosion is null?0:c.SingularityAllyFailedExplosion.Value?1:2,Margin=new(0,4,0,8)};allyFailure.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{SingularityAllyFailedExplosion=allyFailure.SelectedIndex==0?null:allyFailure.SelectedIndex==1};Refresh();};panel.Children.Add(allyFailure);
        var omit=new CheckBox{Content="수치 미기재인 특이점 피해 제외",IsChecked=c.SingularityPulses is {Length:0},Margin=new(0,4,0,8)};omit.Checked+=(_,_)=>{storedConditions=storedConditions with{SingularityPulses=[]};Refresh();};omit.Unchecked+=(_,_)=>{storedConditions=storedConditions with{SingularityPulses=null};Refresh();};panel.Children.Add(omit);
        var events=Stack(Text("피해 사건은 모드·치명을 적용한 뒤의 방어 적용 전 출력입니다. 기본 시뮬러의 틱을 대신 사용하지 않습니다."));
        for(int i=0;i<(c.SingularityPulses?.Length??0);i++)
        {
            var index=i;var pulse=c.SingularityPulses![i];void Edit(Func<RangedSingularityPulse,RangedSingularityPulse> edit){var values=storedConditions.SingularityPulses!.ToArray();values[index]=edit(values[index]);storedConditions=storedConditions with{SingularityPulses=values};Refresh();}
            var types=Enum.GetValues<DamageType>().Select(t=>new ArcaneChoice(t.ToString(),Labels.Damage(t))).ToArray();var type=new ComboBox{ItemsSource=types,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=pulse.Type.ToString(),Margin=new(0,4,0,8)};type.SelectionChanged+=(_,_)=>{if(type.SelectedValue is string value)Edit(p=>p with{Type=Enum.Parse<DamageType>(value)});};
            events.Children.Add(Stack(Text($"특이점 피해 {i+1}"),Number("생성 후 (초) · 5초 미만",pulse.AfterSpawn,v=>Edit(p=>p with{AfterSpawn=v??0})),type,Number("방어 적용 전 피해",pulse.Output,v=>Edit(p=>p with{Output=v??0})),Number("피해 반경 (m)",pulse.Radius,v=>Edit(p=>p with{Radius=v??0})),Button("사건 삭제",()=>{storedConditions=storedConditions with{SingularityPulses=storedConditions.SingularityPulses!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();})));
        }
        events.Children.Add(Button("＋ 특이점 피해 사건",()=>{storedConditions=storedConditions with{SingularityPulses=[..storedConditions.SingularityPulses??[],new(0,DamageType.Magnetic,0,0)]};RefreshConditionControls();Refresh();}));panel.Children.Add(new Expander{Header="특이점 피해 · 직접 시나리오 입력",Content=events,IsExpanded=c.SingularityPulses is {Length:>0},Margin=new(0,4,0,12)});
        conditionPanel.Children.Add(new Expander{Header="스폰테이니어스 싱귤래리티",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});AddPullControls(c,Number);
    }
}
