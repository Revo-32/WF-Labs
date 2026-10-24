using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddAzimaControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var current=c.Azima??new();void Edit(Func<AzimaScenario,AzimaScenario> edit)=>storedConditions=storedConditions with{Azima=edit(storedConditions.Azima??new())};
        var panel=Stack(Text("투척 시 남은 탄창을 사용하고 멀티샷마다 터렛을 만듭니다. 터렛 수 제한 없이 4방향으로 회전 사격합니다. 약 10초 지속 외에 정확한 회전·발사 시점·폭발 범위는 위키에 없어 아래 값을 명시해야 합니다."));
        panel.Children.Add(Number("투척 후 착지까지 (초)",current.LandingSeconds,v=>Edit((AzimaScenario s)=>s with{LandingSeconds=v})));
        panel.Children.Add(Number("터렛 위치 · 전방 (m)",current.CenterX,v=>Edit((AzimaScenario s)=>s with{CenterX=v??double.NaN})));
        panel.Children.Add(Number("터렛 위치 · 측면 (m)",current.CenterY,v=>Edit((AzimaScenario s)=>s with{CenterY=v??double.NaN})));
        panel.Children.Add(Number("기본 75발 탄창의 지속 시간 (초) · 위키 약 10초",current.UnmoddedLifetime,v=>Edit((AzimaScenario s)=>s with{UnmoddedLifetime=v})));
        panel.Children.Add(Number("기본 4방향 발사 횟수 / 초",current.VolleyRate,v=>Edit((AzimaScenario s)=>s with{VolleyRate=v})));
        panel.Children.Add(Number("착지 후 첫 사격 (초)",current.FirstVolley,v=>Edit((AzimaScenario s)=>s with{FirstVolley=v})));
        panel.Children.Add(Number("회전 속도 (도 / 초)",current.RotationSpeed,v=>Edit((AzimaScenario s)=>s with{RotationSpeed=v})));
        panel.Children.Add(Number("시작 방향 (도)",current.InitialAngle,v=>Edit((AzimaScenario s)=>s with{InitialAngle=v??double.NaN})));
        panel.Children.Add(Number("대상 명중 반경 (m)",current.HitRadius,v=>Edit((AzimaScenario s)=>s with{HitRadius=v})));
        panel.Children.Add(Number("만료 폭발 반경 (m)",current.ExplosionRadius,v=>Edit((AzimaScenario s)=>s with{ExplosionRadius=v})));
        panel.Children.Add(Number("폭발 끝점 피해 감소 (%)",current.ExplosionFalloff*100,v=>Edit((AzimaScenario s)=>s with{ExplosionFalloff=v/100})));
        var infinite=new ComboBox{ItemsSource=new[]{"탄약 무한 투척 비용 · 문서 상충으로 미지정","탄약 무한에도 탄창 전체 소비","탄약 무한 중 투척 비용 없음"},SelectedIndex=current.InfiniteConsumesMagazine is null?0:current.InfiniteConsumesMagazine.Value?1:2};infinite.SelectionChanged+=(_,_)=>{Edit((AzimaScenario s)=>s with{InfiniteConsumesMagazine=infinite.SelectedIndex==0?null:infinite.SelectedIndex==1});Refresh();};panel.Children.Add(infinite);
        panel.Children.Add(Text("회전과 사격의 정확한 시간은 지정한 시나리오입니다. 일반 탄약 효율은 투척 시 저장하여 지속에 반영하며 최대 30초입니다. 탄약 무한은 지속 시간을 늘리지 않습니다. 사격 피해와 연사 버프는 교전 중 갱신합니다."));
        conditionPanel.Children.Add(new Expander{Header="아지마 · 회전 터렛",Content=panel,IsExpanded=attack.SelectedIndex==2,Margin=new(0,4,0,12)});
    }
}
