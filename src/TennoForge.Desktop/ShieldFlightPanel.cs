using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record ShieldEditor(TextBox Id,TextBox Launch,TextBox Returned,TextBox Impact,TextBox Target,TextBox Travel,CheckBox Explode,TextBox ExplosionTime,TextBox ExplosionTarget,CheckBox Position,TextBox X,TextBox Y,TextBox Z,TextBox FinisherSeconds,ComboBox Actor);
    private readonly List<ShieldEditor> shieldEditors=[];
    private readonly StackPanel shieldRows=new();
    private Expander? shieldPanel;
    private bool ShieldSelected=>weapon.SelectedItem is Weapon {Id:ShieldThrowMechanic.Sigma};
    private FrameworkElement CreateShieldFlightPanel()
    {
        var note=T("공중에서 방패를 던지고 강공격 입력으로 회수 폭발을 일으킵니다. 아래에 지정한 투척·폭발만 계산합니다. 자동 유도 경로와 귀환 시간은 임의로 만들지 않습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        shieldPanel=new(){Header="방패 투척 · 회수 폭발",Visibility=Visibility.Collapsed,Content=Stack(note,shieldRows,Button("＋ 방패 투척",()=>{var start=shieldEditors.Count==0?0:Number(shieldEditors[^1].Returned,"이전 회수")+.1;AddShield(new(start,start+1,Impact:start+.2,Throw:shieldEditors.Count==0?1:shieldEditors.Max(e=>Integer(e.Id,"투척 번호"))+1));Recalculate();}))};return shieldPanel;
    }
    private void AddShield(ShieldFlight f)
    {
        var d=f.Detonation;var actor=HologramActorChoice(f.Hologram);var e=new ShieldEditor(FlightNumber(f.Throw),FlightNumber(f.Launch),FlightNumber(f.Returned),FlightNumber(f.Impact),FlightNumber(f.Target),FlightNumber(f.Travel),new(){Content="강공격 입력 · 회수 폭발",IsChecked=d is not null},FlightNumber(d?.Time),FlightNumber(d?.CenterTarget??1),new(){Content="기폭 위치 직접 지정",IsChecked=d?.Position is not null},FlightNumber(d?.Position?.X??0),FlightNumber(d?.Position?.Y??0),FlightNumber(d?.Position?.Z??0),FlightNumber(f.FinisherWindowSeconds),actor);
        var row=new StackPanel{Margin=new(0,10,0,12)};
        row.Children.Add(FlightLine(T("투척 "+f.Throw,15),Button("삭제",()=>{shieldEditors.Remove(e);shieldRows.Children.Remove(row);Recalculate();})));
        row.Children.Add(Field("공격 주체",actor));
        row.Children.Add(FlightLine(Field("발사 (초)",e.Launch),Field("회수 완료 (초)",e.Returned)));
        row.Children.Add(FlightLine(Field("직격 (초) · 빗나가면 비움",e.Impact),Field("직격 대상 번호",e.Target)));
        row.Children.Add(Field("첫 비행 거리 (m) · 선택",e.Travel));e.Travel.ToolTip="입력하면60m/s 최단 비행과 사거리를 대조합니다. 본문25m/무기 데이터24m의 차이가 영향을 주는 경계에서는 계산을 확정하지 않습니다.";
        row.Children.Add(Field("마무리 노출 (초) · 원문 미기재",e.FinisherSeconds));e.FinisherSeconds.ToolTip="후속 타격에 은신 피해 보너스를 적용할 시간입니다. 위키에 시간이 없어 지정하면 사용자 조건으로 표시합니다. 충격/전기 상태 발생으로 노출이 먼저 해제될 수 있습니다.";
        row.Children.Add(e.Explode);
        var explosion=Stack(FlightLine(Field("기폭 (초)",e.ExplosionTime),Field("기폭 중심 대상 번호",e.ExplosionTarget)),e.Position);
        var coords=FlightLine(Field("X (m)",e.X),Field("Y (m)",e.Y),Field("Z (m)",e.Z));explosion.Children.Add(coords);row.Children.Add(explosion);
        void Show(){explosion.Visibility=e.Explode.IsChecked==true?Visibility.Visible:Visibility.Collapsed;coords.Visibility=e.Position.IsChecked==true?Visibility.Visible:Visibility.Collapsed;e.ExplosionTarget.IsEnabled=e.Position.IsChecked!=true;}
        e.Explode.Checked+=(_,_)=>Show();e.Explode.Unchecked+=(_,_)=>Show();e.Position.Checked+=(_,_)=>Show();e.Position.Unchecked+=(_,_)=>Show();Show();
        shieldEditors.Add(e);shieldRows.Children.Add(row);FlightWatch(e.Launch,e.Returned,e.Impact,e.Target,e.Travel,e.Explode,e.ExplosionTime,e.ExplosionTarget,e.Position,e.X,e.Y,e.Z,e.FinisherSeconds,actor);
    }
    private ShieldFlight[] ReadShieldFlights()=>!ShieldSelected?[]:shieldEditors.Select(e=>new ShieldFlight(Number(e.Launch,"방패 발사"),Number(e.Returned,"방패 회수"),FlightOptional(e.Impact,"방패 직격"),Integer(e.Target,"방패 직격 대상"),Integer(e.Id,"방패 투척 번호"),FlightOptional(e.Travel,"방패 비행 거리"),e.Explode.IsChecked==true?new(Number(e.ExplosionTime,"방패 기폭"),e.Position.IsChecked==true?new(Number(e.X,"기폭 X"),Number(e.Y,"기폭 Y"),Number(e.Z,"기폭 Z")):null,Integer(e.ExplosionTarget,"방패 기폭 대상")):null,FlightOptional(e.FinisherSeconds,"방패 마무리 노출 시간"),e.Actor.SelectedValue is int n?n:0)).ToArray();
    private void ApplyShieldFlights(EncounterSetup? setup){shieldEditors.Clear();shieldRows.Children.Clear();foreach(var f in setup?.ShieldFlights??[])AddShield(f);}
}
