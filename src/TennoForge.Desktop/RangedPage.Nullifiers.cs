using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddNullifierControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var current=c.Nullifier??new(Kind:RangedNullifiers.EnemyKind((enemy.SelectedItem as EnemyDefinition)?.Id??""));
        void Edit(Func<NullifierScenario,NullifierScenario> edit)=>storedConditions=storedConditions with{Nullifier=edit(storedConditions.Nullifier??current)};
        var options=new[]{new ArcaneChoice("None","막 없음"),new ArcaneChoice("Crewman","널리파이어 크루맨 생성기"),new ArcaneChoice("Corrupted","커럽티드 널리파이어 생성기"),new ArcaneChoice("Capture","생포 대상의 널리파이어 생성기"),new ArcaneChoice("TerraOsprey","테라 실드 오스프리 · 중화 저스티스 무효"),new ArcaneChoice("Bursa","아이솔레이터 벌사 · 중화 저스티스 무효")};
        var kind=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedItem=options.Single(v=>v.Id==current.Kind),Margin=new(0,4,0,8)};kind.SelectionChanged+=(_,_)=>{if(kind.SelectedItem is ArcaneChoice v){Edit(s=>s with{Kind=v.Id});RefreshConditionControls();Refresh();}};
        var panel=Stack(kind);
        if(current.Kind!="None")
        {
            panel.Children.Add(Text("막에 막힌 사격은 적의 체력 피해에 포함하지 않습니다. 피해를 프레임마다 합산하고 치명타 없이 현재 반경을 축소합니다. 정확한 붕괴 경계와 재생 속도는 위키에 없어 시나리오로 지정합니다."));
            panel.Children.Add(Number("시작 반경 · 최대 반경 대비 (%)",current.InitialRadius*100,v=>Edit(s=>s with{InitialRadius=(v??100)/100})));
            panel.Children.Add(Number("붕괴 반경 가정 (%)",current.CollapseRadius*100,v=>Edit(s=>s with{CollapseRadius=v/100})));
            panel.Children.Add(Number("게임 프레임 시간 (ms)",current.FrameSeconds*1000,v=>Edit(s=>s with{FrameSeconds=v/1000})));
            var omit=new CheckBox{Content="이 교전에서 막 재생 제외",IsChecked=current.NoRegrowth,Margin=new(0,4,0,8)};omit.Checked+=(_,_)=>{Edit(s=>s with{NoRegrowth=true});Refresh();};omit.Unchecked+=(_,_)=>{Edit(s=>s with{NoRegrowth=false});Refresh();};panel.Children.Add(omit);
            panel.Children.Add(Number("피격 후 재생 대기 (초)",current.RecoveryDelay,v=>Edit(s=>s with{RecoveryDelay=v})));
            panel.Children.Add(Number("초당 재생 반경 (%)",current.GrowthPerSecond*100,v=>Edit(s=>s with{GrowthPerSecond=v/100})));
            var radial=new ComboBox{ItemsSource=new[]{"방사 피해 위치 · 선택 필요","막 외부 폭발 · 막으로 차단","막 내부 폭발 · 적에게 적용"},SelectedIndex=current.RadialBlocked is null?0:current.RadialBlocked.Value?1:2,Margin=new(0,4,0,8)};radial.SelectionChanged+=(_,_)=>{Edit(s=>s with{RadialBlocked=radial.SelectedIndex==0?null:radial.SelectedIndex==1});Refresh();};panel.Children.Add(radial);
        }
        conditionPanel.Children.Add(new Expander{Header="널리파이어 보호막",Content=panel,IsExpanded=current.Kind!="None",Margin=new(0,4,0,12)});
    }
    private void AddCannonadeControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var scenario=c.Cannonade??new();void Edit(Func<CannonadeScenario,CannonadeScenario> edit)=>storedConditions=storedConditions with{Cannonade=edit(storedConditions.Cannonade??new())};
        var panel=Stack(Text("구형 캐노네이드: 기존 보유분만 사용할 수 있습니다. 250 Blast·10m·최대 30%는 확인되지만 모드 브라켓과 거리 감쇠 곡선은 미기재입니다."));
        var fixedPacket=new CheckBox{Content="비치명 고정 폭발 · 추가 상태 없음 시나리오",IsChecked=scenario.FixedNoncriticalPacket,Margin=new(0,4,0,8)};fixedPacket.Checked+=(_,_)=>{Edit(s=>s with{FixedNoncriticalPacket=true});Refresh();};fixedPacket.Unchecked+=(_,_)=>{Edit(s=>s with{FixedNoncriticalPacket=false});Refresh();};panel.Children.Add(fixedPacket);
        panel.Children.Add(Number("250 피해에 적용할 최종 배율",scenario.OutputMultiplier,v=>Edit(s=>s with{OutputMultiplier=v})));
        var radial=new ComboBox{ItemsSource=new[]{"방사 명중의 발동 여부 · 원문 미기재","방사 피해에서도 발동","직접 명중에서만 발동"},SelectedIndex=scenario.RadialTriggers is null?0:scenario.RadialTriggers.Value?1:2,Margin=new(0,4,0,8)};radial.SelectionChanged+=(_,_)=>{Edit(s=>s with{RadialTriggers=radial.SelectedIndex==0?null:radial.SelectedIndex==1});Refresh();};panel.Children.Add(radial);
        panel.Children.Add(Text("거리, 배율을 한 줄씩 입력합니다. 첫 거리는 0, 마지막은 10m이며 사이를 선형 보간합니다. 예시는 확정된 게임 수치가 아닙니다."));
        var curve=new TextBox{AcceptsReturn=true,MinHeight=80,Text=string.Join("\n",(scenario.Falloff??[]).Select(p=>$"{p.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {p.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}")),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        curve.TextChanged+=(_,_)=>{var points=curve.Text.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(line=>{var pieces=line.Split(',',StringSplitOptions.TrimEntries);return pieces.Length==2&&double.TryParse(pieces[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var d)&&double.TryParse(pieces[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var m)?new CannonadeFalloff(d,m):new(-1,-1);}).ToArray();Edit(s=>s with{Falloff=points.Length==0?null:points});Refresh();};panel.Children.Add(curve);
        conditionPanel.Children.Add(new Expander{Header="캐노네이드 · 폭발 시나리오",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
