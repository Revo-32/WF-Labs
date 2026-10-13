using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record FlightContactChoice(GlaiveContactKind Value,string Label);
    private sealed record GlaiveContactEditor(TextBox Time,TextBox Target,ComboBox Kind,CheckBox Surface,TextBox X,TextBox Y,TextBox Z,TextBox Thickness);
    private sealed record GlaiveEditor(TextBox Id,TextBox Launch,TextBox Returned,CheckBox Charged,CheckBox Airborne,TextBox Buffered,TextBox Recall,TextBox Travel,
        CheckBox Explode,TextBox ExplosionTime,TextBox ExplosionTarget,CheckBox Position,TextBox X,TextBox Y,TextBox Z,List<GlaiveContactEditor> Contacts,ComboBox Actor);
    private readonly List<GlaiveEditor> glaiveEditors=[];
    private readonly StackPanel glaiveRows=new();
    private readonly CheckBox glaiveOnly=new(){Content="이 투척 경로만 계산",IsChecked=true};
    private Expander? glaivePanel;
    private bool GlaiveSelected=>weapon.SelectedItem is Weapon w&&GlaiveFlightState.Applies(w.Id);
    private FrameworkElement CreateGlaivePanel()
    {
        var note=T("발사 → 접촉 → 기폭 → 회수. 각 시각과 경로는 교전 조건입니다. 위키에 없는 비행·유도 시간을 자동으로 만들지 않습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        glaiveOnly.Checked+=(_,_)=>Recalculate();glaiveOnly.Unchecked+=(_,_)=>Recalculate();
        glaivePanel=new(){Header="투척 경로",Visibility=Visibility.Collapsed,Content=Stack(note,glaiveOnly,glaiveRows,Button("＋ 투척",()=>{
            var start=glaiveEditors.Count==0?0:Number(glaiveEditors[^1].Returned,"이전 회수 시각")+.2;
            AddGlaive(new(start,start+2,Throw:glaiveEditors.Count==0?1:glaiveEditors.Max(e=>Integer(e.Id,"투척 번호"))+1,Contacts:[new(start+.5)],Detonation:new(start+1)));Recalculate();
        }))};return glaivePanel;
    }
    private static TextBox FlightNumber(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
    private static FrameworkElement FlightLine(params UIElement[] fields)
    {var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
    private void FlightWatch(params UIElement[] fields)
    {foreach(var f in fields){if(f is TextBox box)box.TextChanged+=(_,_)=>Recalculate();if(f is CheckBox c){c.Checked+=(_,_)=>Recalculate();c.Unchecked+=(_,_)=>Recalculate();}if(f is ComboBox combo)combo.SelectionChanged+=(_,_)=>Recalculate();}}
    private static double? FlightOptional(TextBox box,string label)=>string.IsNullOrWhiteSpace(box.Text)?null:Number(box,label);
    private void AddGlaive(GlaiveFlight f)
    {
        var d=f.Detonation;var actor=HologramActorChoice(f.Hologram);
        var e=new GlaiveEditor(FlightNumber(f.Throw),FlightNumber(f.Launch),FlightNumber(f.Returned),new(){Content="충전 투척",IsChecked=f.Charged},new(){Content="공중에서 발사",IsChecked=f.Airborne},FlightNumber(f.BufferedAt),FlightNumber(f.RecallAt),FlightNumber(f.FirstLegDistance),
            new(){Content="수동 기폭",IsChecked=d is not null},FlightNumber(d?.Time),FlightNumber(d?.CenterTarget??1),new(){Content="기폭 위치 직접 지정",IsChecked=d?.Position is not null},FlightNumber(d?.Position?.X??0),FlightNumber(d?.Position?.Y??0),FlightNumber(d?.Position?.Z??0),[],actor);
        var row=new StackPanel{Margin=new(0,12,0,14)};var heading=T("투척 "+f.Throw,15);heading.FontWeight=FontWeights.SemiBold;
        if(weapon.SelectedItem is Weapon {Id:WolfSledgeFlight.WeaponId})
        {
            e.Charged.Visibility=Visibility.Collapsed;e.Airborne.IsChecked=false;e.Airborne.IsEnabled=false;
            var hint=T("지상 투척 · 충전 정도에 따른 피해 변화 없음\n전진 접촉마다 폭발 · 지형 또는 두 번째 접촉 후 귀환",14,"#596B85");hint.TextWrapping=TextWrapping.Wrap;row.Children.Add(hint);
        }
        row.Children.Add(FlightLine(heading,Button("삭제",()=>{glaiveEditors.Remove(e);glaiveRows.Children.Remove(row);Recalculate();})));
        row.Children.Add(Field("공격 주체",actor));
        row.Children.Add(FlightLine(e.Charged,e.Airborne));row.Children.Add(FlightLine(Field("발사 (초)",e.Launch),Field("회수 완료 (초)",e.Returned)));
        var contacts=new StackPanel();foreach(var c in f.Contacts??[])AddGlaiveContact(e,contacts,c);
        row.Children.Add(contacts);row.Children.Add(Button("＋ 접촉",()=>{AddGlaiveContact(e,contacts,new(Number(e.Launch,"발사")+.5));Recalculate();}));
        row.Children.Add(e.Explode);var explosion=Stack(FlightLine(Field("기폭 시각 (초)",e.ExplosionTime),Field("중심 대상 번호",e.ExplosionTarget)),e.Position);
        var position=FlightLine(Field("X (m)",e.X),Field("Y (m)",e.Y),Field("Z (m)",e.Z));explosion.Children.Add(position);row.Children.Add(explosion);
        void ShowExplosion(){explosion.Visibility=e.Explode.IsChecked==true?Visibility.Visible:Visibility.Collapsed;position.Visibility=e.Position.IsChecked==true?Visibility.Visible:Visibility.Collapsed;e.ExplosionTarget.IsEnabled=e.Position.IsChecked!=true;}
        void ActorChanged(){if(actor.SelectedValue is int n&&n>0&&weapon.SelectedItem is not Weapon {Id:WolfSledgeFlight.WeaponId}){e.Explode.IsChecked=false;e.Explode.IsEnabled=false;e.Explode.ToolTip="분신은 수동 기폭하지 않습니다.";}else e.Explode.IsEnabled=true;}actor.SelectionChanged+=(_,_)=>ActorChanged();ActorChanged();
        e.Explode.Checked+=(_,_)=>ShowExplosion();e.Explode.Unchecked+=(_,_)=>ShowExplosion();e.Position.Checked+=(_,_)=>ShowExplosion();e.Position.Unchecked+=(_,_)=>ShowExplosion();ShowExplosion();
        var detail=new Expander{Header="연속 투척 · 귀환 · 거리",Content=Stack(Field("다음 투척 입력 (초) · 이전 회수 전",e.Buffered),Field("폭발 없이 귀환 시작 (초) · 선택",e.Recall),Field("첫 접촉까지 이동 거리 (m) · 선택",e.Travel))};row.Children.Add(detail);
        glaiveEditors.Add(e);glaiveRows.Children.Add(row);FlightWatch(e.Launch,e.Returned,e.Charged,e.Airborne,e.Buffered,e.Recall,e.Travel,e.Explode,e.ExplosionTime,e.ExplosionTarget,e.Position,e.X,e.Y,e.Z,actor);
    }
    private void AddGlaiveContact(GlaiveEditor owner,StackPanel host,GlaiveContact c)
    {
        var kind=new ComboBox{ItemsSource=new[]{new FlightContactChoice(GlaiveContactKind.Bounce,"반사"),new FlightContactChoice(GlaiveContactKind.PunchThrough,"전진 관통"),new FlightContactChoice(GlaiveContactKind.Return,"귀환 관통")},DisplayMemberPath="Label",SelectedValuePath="Value",SelectedValue=c.Kind};
        var e=new GlaiveContactEditor(FlightNumber(c.Time),FlightNumber(c.Target),kind,new(){Content="지형 접촉",IsChecked=c.Surface is not null},FlightNumber(c.Surface?.X??0),FlightNumber(c.Surface?.Y??0),FlightNumber(c.Surface?.Z??0),FlightNumber(c.Thickness));
        var row=new StackPanel{Margin=new(0,8,0,4)};row.Children.Add(FlightLine(Field("접촉 (초)",e.Time),Field("접촉 방식",e.Kind)));row.Children.Add(FlightLine(Field("대상 번호",e.Target),e.Surface));
        var coords=FlightLine(Field("X (m)",e.X),Field("Y (m)",e.Y),Field("Z (m)",e.Z));row.Children.Add(coords);var width=Field("몸체 두께 (m) · 선택",e.Thickness);row.Children.Add(width);
        void Update(){coords.Visibility=e.Surface.IsChecked==true?Visibility.Visible:Visibility.Collapsed;e.Target.IsEnabled=e.Surface.IsChecked!=true;width.Visibility=(GlaiveContactKind)e.Kind.SelectedValue==GlaiveContactKind.PunchThrough?Visibility.Visible:Visibility.Collapsed;if(e.Surface.IsChecked==true)e.Kind.SelectedValue=GlaiveContactKind.Bounce;}
        e.Surface.Checked+=(_,_)=>Update();e.Surface.Unchecked+=(_,_)=>Update();e.Kind.SelectionChanged+=(_,_)=>Update();Update();
        row.Children.Add(Button("접촉 삭제",()=>{owner.Contacts.Remove(e);host.Children.Remove(row);Recalculate();}));owner.Contacts.Add(e);host.Children.Add(row);FlightWatch(e.Time,e.Target,e.Kind,e.Surface,e.X,e.Y,e.Z,e.Thickness);
    }
    private GlaiveFlight[] ReadGlaives()=>!GlaiveSelected?[]:glaiveEditors.Select(e=>new GlaiveFlight(Number(e.Launch,"투척 발사"),Number(e.Returned,"회수 완료"),Integer(e.Id,"투척 번호"),e.Charged.IsChecked==true,e.Airborne.IsChecked==true,FlightOptional(e.Buffered,"연속 투척 입력"),FlightOptional(e.Recall,"귀환 시작"),
        e.Contacts.Select(c=>new GlaiveContact(Number(c.Time,"접촉 시각"),Integer(c.Target,"대상 번호"),(GlaiveContactKind)c.Kind.SelectedValue,c.Surface.IsChecked==true?new(Number(c.X,"지형X"),Number(c.Y,"지형Y"),Number(c.Z,"지형Z")):null,FlightOptional(c.Thickness,"몸체 두께"))).ToArray(),
        e.Explode.IsChecked==true?new(Number(e.ExplosionTime,"수동 기폭"),e.Position.IsChecked==true?new(Number(e.X,"기폭X"),Number(e.Y,"기폭Y"),Number(e.Z,"기폭Z")):null,Integer(e.ExplosionTarget,"기폭 대상")):null,FlightOptional(e.Travel,"첫 비행 거리"),e.Actor.SelectedValue is int n?n:0)).ToArray();
    private void ApplyGlaives(EncounterSetup? setup){glaiveEditors.Clear();glaiveRows.Children.Clear();glaiveOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var f in setup?.GlaiveFlights??[])AddGlaive(f);}
}
