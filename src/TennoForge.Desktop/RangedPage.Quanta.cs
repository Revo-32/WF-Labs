using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddQuantaControls(RangedConditions c,Func<string,double,Action<double>,FrameworkElement> Number,Func<string,double?,Action<double?>,FrameworkElement> OptionalNumber)
    {
        var panel=Stack(Text("직격은 작은 폭발, 다른 플레이어의 공격 또는 주변 큐브의 폭발로 기폭하면 큰 폭발이 발생합니다. 기본은 큐브 한 개를 조준 대상에게 직선으로 발사하는 배치입니다."));
        var shoot=new CheckBox{Content="내 기본 사격으로 경로의 큐브 기폭",IsChecked=c.ShootOwnCubes,Margin=new(0,4,0,8)};shoot.Checked+=(_,_)=>{storedConditions=storedConditions with{ShootOwnCubes=true};RefreshConditionControls();Refresh();};shoot.Unchecked+=(_,_)=>{storedConditions=storedConditions with{ShootOwnCubes=false};RefreshConditionControls();Refresh();};panel.Children.Add(shoot);
        if(c.ShootOwnCubes)
        {
            panel.Children.Add(OptionalNumber("큐브 충돌 반경 (m) · 원문 미기재 / 0은 중심 조준",c.CubeHitRadius,v=>storedConditions=storedConditions with{CubeHitRadius=v}));var pass=new ComboBox{ItemsSource=new[]{"기폭한 빔 틱의 뒤쪽 명중 · 원문 미확정","큐브 기폭 후 같은 틱은 멈춤","같은 빔 틱도 뒤의 적에 명중"},SelectedIndex=c.CubeBeamContinues is null?0:c.CubeBeamContinues.Value?2:1,Margin=new(0,4,0,8)};pass.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{CubeBeamContinues=pass.SelectedIndex==0?null:pass.SelectedIndex==2};Refresh();};panel.Children.Add(pass);panel.Children.Add(Text("예약한 보조 공격은 큐브를 한 번 발사한 뒤 기본 빔으로 돌아갑니다. 빔과 이동 중 큐브가 실제 교차할 때 큰 폭발과 인접 큐브의 연쇄 기폭을 처리합니다."));
        }
        var lifetimes=new[]{new ArcaneChoice("Unspecified","자연 만료 기준 미선택"),new ArcaneChoice("AfterStop","정지 후 8초 · 위키 상세 설명"),new ArcaneChoice("AfterLaunch","발사 후 9초 · 위키 특성 요약")};
        var lifetime=new ComboBox{ItemsSource=lifetimes,DisplayMemberPath="Name",SelectedItem=lifetimes.Single(x=>x.Id==c.CubeLifetimeRule),Margin=new(0,4,0,8)};
        lifetime.SelectionChanged+=(_,_)=>{if(lifetime.SelectedItem is ArcaneChoice value){storedConditions=storedConditions with{CubeLifetimeRule=value.Id};Refresh();}};panel.Children.Add(lifetime);
        panel.Children.Add(Text("관통·도탄·멀티샷은 원문에 고정 궤적이 없습니다. 경로를 직접 지정할 때는 전방/옆 좌표와 발사 후 시각으로 이동을 정의합니다. 입력한 충돌만 적용하며 외부 기폭은 다른 플레이어의 공격으로 가정합니다."));
        if(c.CubeRoutes is not null)
        {
            panel.Children.Add(OptionalNumber("관통할 몸통 유효 두께 (m)",c.TargetBodyThickness,v=>storedConditions=storedConditions with{TargetBodyThickness=v}));
            panel.Children.Add(Button("기본 직선 경로로 복원",()=>{storedConditions=storedConditions with{CubeRoutes=null};RefreshConditionControls();Refresh();}));
        }
        for(int i=0;i<(c.CubeRoutes?.Length??0);i++)
        {
            var index=i;var route=c.CubeRoutes![i];
            void Change(Func<RangedCubeRoute,RangedCubeRoute> action,bool redraw=false){var values=storedConditions.CubeRoutes!.ToArray();values[index]=action(values[index]);storedConditions=storedConditions with{CubeRoutes=values};if(redraw)RefreshConditionControls();Refresh();}
            var controls=Stack(Number("정지 위치 · 전방 (m)",route.StopX,v=>Change(x=>x with{StopX=v})),Number("정지 위치 · 옆 방향 (m)",route.StopY,v=>Change(x=>x with{StopY=v})),Number("발사부터 정지까지 (초)",route.StopSeconds,v=>Change(x=>x with{StopSeconds=v})),OptionalNumber("외부 공격으로 기폭 · 발사 후 (초)",route.DamagedAfterLaunch,v=>Change(x=>x with{DamagedAfterLaunch=v})));
            for(int j=0;j<(route.Contacts?.Length??0);j++)
            {
                var contactIndex=j;var hit=route.Contacts![j];
                void Edit(Func<RangedCollision,RangedCollision> action){Change(r=>{var hits=r.Contacts!.ToArray();hits[contactIndex]=action(hits[contactIndex]);return r with{Contacts=hits};});}
                controls.Children.Add(Text($"적 충돌 {j+1}",15));
                controls.Children.Add(Number("발사 후 명중 시각 (초)",hit.AfterImpact,v=>Edit(h=>h with{AfterImpact=v})));
                controls.Children.Add(Number("대상 번호 · 1부터",hit.Target+1,v=>Edit(h=>h with{Target=(int)v-1})));
                controls.Children.Add(Button("충돌 삭제",()=>Change(r=>r with{Contacts=r.Contacts!.Where((_,n)=>n!=contactIndex).ToArray()},true)));
            }
            controls.Children.Add(Button("＋ 적 충돌",()=>Change(r=>r with{Contacts=[..r.Contacts??[],new(r.StopSeconds/2)]},true)));
            for(int j=0;j<(route.Waypoints?.Length??0);j++)
            {
                var pointIndex=j;var point=route.Waypoints![j];
                void Edit(Func<RangedCubeWaypoint,RangedCubeWaypoint> action){Change(r=>{var points=r.Waypoints!.ToArray();points[pointIndex]=action(points[pointIndex]);return r with{Waypoints=points};});}
                controls.Children.Add(Text($"경유점 {j+1} · 반사·감속 경로",15));
                controls.Children.Add(Number("발사 후 시각 (초)",point.AfterLaunch,v=>Edit(p=>p with{AfterLaunch=v})));
                controls.Children.Add(Number("전방 (m)",point.Forward,v=>Edit(p=>p with{Forward=v})));
                controls.Children.Add(Number("옆 방향 (m)",point.Side,v=>Edit(p=>p with{Side=v})));
                controls.Children.Add(Button("경유점 삭제",()=>Change(r=>r with{Waypoints=r.Waypoints!.Where((_,n)=>n!=pointIndex).ToArray()},true)));
            }
            controls.Children.Add(Button("＋ 경유점",()=>Change(r=>r with{Waypoints=[..r.Waypoints??[],new(((r.Waypoints??[]).LastOrDefault()?.AfterLaunch??0)+.1,0,0)]},true)));
            controls.Children.Add(Button("큐브 경로 삭제",()=>{storedConditions=storedConditions with{CubeRoutes=storedConditions.CubeRoutes!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();}));
            panel.Children.Add(new Expander{Header=$"큐브 {i+1} · 지정 경로",Content=controls,IsExpanded=true,Margin=new(0,8,0,8)});
        }
        panel.Children.Add(Button("＋ 큐브 경로",()=>{storedConditions=storedConditions with{CubeRoutes=[..storedConditions.CubeRoutes??[],new(15,0)]};RefreshConditionControls();Refresh();}));
        conditionPanel.Children.Add(new Expander{Header="콴타 · 큐브와 연쇄 기폭",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
