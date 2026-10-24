using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddResidualControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var current=c.Residual??new();var id=kitgun!.Arcane;
        void Edit(Func<ResidualScenario,ResidualScenario> update)=>storedConditions=storedConditions with{Residual=update(storedConditions.Residual??new())};
        var panel=Stack(Text("처치 시 20% · 랭크별 3/6/9/12초 · 두 키트건 합계 최대 2개. 아래 값은 위키에 없는 발동 시점·피해 브라켓을 위한 시나리오입니다."));
        var packet=new CheckBox{Content="비치명 고정 출력 시나리오 사용",IsChecked=current.FixedPacketScenario,Margin=new(0,4,0,8)};
        packet.Checked+=(_,_)=>{Edit((ResidualScenario s)=>s with{FixedPacketScenario=true});Refresh();};packet.Unchecked+=(_,_)=>{Edit((ResidualScenario s)=>s with{FixedPacketScenario=false});Refresh();};panel.Children.Add(packet);
        panel.Children.Add(Number("기본 피해에 적용할 최종 배율",current.OutputMultiplier,v=>Edit((ResidualScenario s)=>s with{OutputMultiplier=v})));
        void Choice(string title,bool? value,Action<bool?> change,string yes,string no)
        {
            var box=new ComboBox{ItemsSource=new[]{"선택 필요",yes,no},SelectedIndex=value is null?0:value.Value?1:2};box.SelectionChanged+=(_,_)=>{change(box.SelectedIndex==0?null:box.SelectedIndex==1);Refresh();};panel.Children.Add(Field(title,box));
        }
        Choice("레지듀얼 처치 귀속",current.WeaponCredit,v=>Edit((ResidualScenario s)=>s with{WeaponCredit=v}),"무기 처치 효과 발동","독립 효과로 처리");
        void Times(string title,double[]? values,Action<double[]?> change)
        {
            var box=new TextBox{Text=values is null?"":values.Length==0?"없음":string.Join(", ",values),ToolTip="초 단위로 쉼표로 구분합니다. 빈칸은 미지정, '없음'은 사건이 없는 시나리오입니다."};
            box.TextChanged+=(_,_)=>{change(string.IsNullOrWhiteSpace(box.Text)?null:box.Text.Trim()=="없음"?[]:box.Text.Split(',').Select(t=>double.TryParse(t.Trim(),out var n)?n:double.NaN).ToArray());Refresh();};panel.Children.Add(Field(title,box));
        }
        if(id is "ResidualBoils" or "ResidualShock")Times("생성 후 발동 시점 (초)",current.Activations,v=>Edit((ResidualScenario s)=>s with{Activations=v}));
        else
        {
            panel.Children.Add(Number("피해 틱 간격 (초)",current.TickInterval,v=>Edit((ResidualScenario s)=>s with{TickInterval=v})));
            panel.Children.Add(Number("생성 후 첫 틱 (초)",current.FirstTick,v=>Edit((ResidualScenario s)=>s with{FirstTick=v})));
        }
        if(id=="ResidualBoils")
        {
            panel.Children.Add(Number("폭발을 유발하는 적 접근 반경 (m)",current.BoilsApproachRadius,v=>Edit((ResidualScenario s)=>s with{BoilsApproachRadius=v})));
            panel.Children.Add(Number("10m 끝점 피해 감소 (%) · 위키 약 90%",current.BoilsFalloff*100,v=>Edit((ResidualScenario s)=>s with{BoilsFalloff=v/100})));
        }
        if(id is "ResidualBoils" or "ResidualShock")panel.Children.Add(Number("상태 피해 1틱 출력 · 방어 적용 전",current.StatusTick,v=>Edit((ResidualScenario s)=>s with{StatusTick=v})));
        if(id=="ResidualViremia")Choice("무기 원소 모드 처리",current.ViremiaCombines,v=>Edit((ResidualScenario s)=>s with{ViremiaCombines=v}),"무기 원소 추가·결합","고정 독성 피해");
        Times("다른 키트건 장판의 남은 시간 (초) · 최대 2개",current.OtherKitgunPoolEnds,v=>Edit((ResidualScenario s)=>s with{OtherKitgunPoolEnds=v}));
        panel.Children.Add(Text(id=="ResidualViremia"?"바이레미아는 위키에 기록된 버그 기준으로 상태 확률 0%를 적용합니다.":"원문에 기재된 고유 상태 확률 20%를 적용합니다."));
        conditionPanel.Children.Add(new Expander{Header="레지듀얼 · 생성과 범위 피해",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
