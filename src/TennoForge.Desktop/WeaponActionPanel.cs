using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record WeaponContactEditor(TextBox Time,TextBox Target,TextBox Projectile,TextBox Travel,ComboBox Kind,TextBox BladeHit,TextBox Penetrated,TextBox Launched,TextBox SlamHit);
    private sealed record WeaponActionEditor(int Id,TextBox Start,TextBox Launch,ComboBox Sequence,ComboBox Model,CheckBox Independent,List<WeaponContactEditor> Contacts,StackPanel Rows,TextBox Expires,CheckBox Sliding,string Mode,ComboBox Actor);
    private readonly List<WeaponActionEditor> weaponActionEditors=[];
    private readonly StackPanel weaponActionRows=new();
    private Expander? weaponActionPanel;
    private string? weaponActionEditorId;
    private FrameworkElement? warfanSlamButtons;
    private bool WeaponActionSelected=>weapon.SelectedItem is Weapon w&&WeaponActionFlight.Applies(w.Id);
    private FrameworkElement CreateWeaponActionPanel()
    {
        var note=T("칼날과 투사체가 실제로 닿는 순서를 지정합니다. 한 공격에서 나온 성분은 같은 콤보를 사용합니다. 이 목록이 있으면 자동 반복 공격은 사용하지 않습니다. 오페라는 추가 단검까지 가능한 경로를 지정하며, 그 단검이 발사되지 않은 표본에서는 해당 접촉이 빠집니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        warfanSlamButtons=FlightLine(Button("＋ 일반 슬램",()=>AddSlam(false)),Button("＋ 강슬램",()=>AddSlam(true)));
        weaponActionPanel=new(){Header="고유 공격 · 접촉 경로",Visibility=Visibility.Collapsed,Content=Stack(note,warfanSlamButtons,weaponActionRows,Button("＋ 공격 입력",()=>{
            if(weapon.SelectedItem is not Weapon w)return;if(!WeaponStanceMechanic.Applies(w.Id)&&w.Id!=VinquibusState.WeaponId){AddSlam(false);return;}var start=weaponActionEditors.Count==0?0:weaponActionEditors.Max(e=>Number(e.Start,"입력 시각"))+1;
            AddWeaponAction(new(start,start,Input:weaponActionEditors.Count==0?1:weaponActionEditors.Max(e=>e.Id)+1,Contacts:[new(start+.2)],ExplosionModel:w.Id==WeaponStanceMechanic.Edun?"Unresolved":"Module"));Recalculate();}))};return weaponActionPanel;
    }
    private void AddSlam(bool heavy)
    {
        if(weapon.SelectedItem is not Weapon w||!WarfanSlam.Applies(w.Id))return;
        var start=weaponActionEditors.Count==0?0:weaponActionEditors.Max(e=>Number(e.Start,"입력 시각"))+1;
        AddWeaponAction(new(start,start,Input:weaponActionEditors.Count==0?1:weaponActionEditors.Max(e=>e.Id)+1,Contacts:[],Mode:heavy?"HeavySlam":"Slam"));Recalculate();
    }
    private void AddWeaponAction(WeaponActionInput v)
    {
        if(weapon.SelectedItem is not Weapon w||!WeaponActionFlight.Applies(w.Id))return;
        weaponActionEditorId=w.Id;
        var sequence=new ComboBox{ItemsSource=Enumerable.Range(1,WeaponActionFlight.Sequences(w.Id)).Select(i=>new KeyValuePair<int,string>(i,w.Id==VinquibusState.WeaponId?(i==1?"사격 성분 · 1×":"막기 정지 사격 · 3×"):$"{i}번째 동작")).ToArray(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=v.Sequence};
        var model=new ComboBox{ItemsSource=new[]{new KeyValuePair<string,string>("Unresolved","출처 분기 선택"),new("Module","데이터 표 · 0.8초 뒤 · 4m"),new("Article","무기 본문 · 1초 뒤 · 5m")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=v.ExplosionModel};
        if(w.Id==WeaponStanceMechanic.Agendus){model.ItemsSource=new[]{new KeyValuePair<string,string>("Unresolved","출처 분기 선택"),new("Module","데이터 표 · 최저6.95%"),new("Article","무기 본문 · 최저5%")};model.SelectedValue=v.ExplosionModel;}
        var independent=new CheckBox{Content="단검별 독립 타격 가정 · 병합 제외",IsChecked=v.IndependentDaggers};
        var actor=new ComboBox{ItemsSource=Enumerable.Range(0,5).Select(n=>new KeyValuePair<int,string>(n,n==0?"플레이어":$"미라지 분신 {n}")).ToArray(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=v.Hologram};
        var e=new WeaponActionEditor(v.Input,FlightNumber(v.Start),FlightNumber(v.Launch),sequence,model,independent,[],new(),FlightNumber(v.ProjectileExpires),new(){Content="슬라이딩 중 발사",IsChecked=v.Sliding},v.Mode,actor);var row=new StackPanel{Margin=new(0,10,0,12)};
        row.Children.Add(FlightLine(T((WarfanSlam.Active(v)?v.Mode=="HeavySlam"?"강슬램 ":"슬램 ":"공격 ")+v.Input,15),Button("삭제",()=>{weaponActionEditors.Remove(e);weaponActionRows.Children.Remove(row);Recalculate();})));
        row.Children.Add(Field("공격 주체",actor));
        row.Children.Add(WarfanSlam.Active(v)?Field("공격 입력 (초)",e.Start):FlightLine(Field("공격 입력 (초)",e.Start),Field("발사 (초)",e.Launch)));
        if(!WarfanSlam.Active(v)&&WeaponActionFlight.Sequences(w.Id)>1)row.Children.Add(Field("고유 동작 순서",sequence));
        if(w.Id==VinquibusState.WeaponId)row.Children.Add(T("생존한 적은 관통하지 않습니다. 처치한 첫 적 뒤의 접촉을 추가할 수 있습니다. 폭발은 발사 +0.8초이며 표면 위치는 선택한 대상 좌표를 사용합니다.",13));
        if(w.Id==WeaponStanceMechanic.Edun)row.Children.Add(Field("폭발 · 위키 출처 상충",model));
        if(w.Id==WeaponStanceMechanic.Agendus)row.Children.Add(Field("거리 감쇠 · 위키 출처 상충",model));
        if(!WarfanSlam.Active(v)&&WeaponStanceMechanic.IsQuassus(w.Id))row.Children.Add(independent);
        if(w.Id==WeaponStanceMechanic.Grigori){row.Children.Add(e.Sliding);row.Children.Add(Field("원반 만료 (초) · 수명 수식 미기재",e.Expires));}
        row.Children.Add(e.Rows);row.Children.Add(Button("＋ 접촉",()=>{AddWeaponContact(e,new(e.Contacts.Count==0?Number(e.Start,"입력 시각")+.2:e.Contacts.Max(c=>Number(c.Time,"명중"))+.2,SlamHit:e.Contacts.Count+1));Recalculate();}));
        weaponActionEditors.Add(e);weaponActionRows.Children.Add(row);foreach(var c in v.Contacts??[])AddWeaponContact(e,c);
        FlightWatch(e.Start,e.Launch,sequence,model,independent,e.Expires,e.Sliding,actor);
    }
    private void AddWeaponContact(WeaponActionEditor parent,WeaponActionContact c)
    {
        if(weapon.SelectedItem is not Weapon w)return;
        var slam=parent.Mode is "Slam" or "HeavySlam";
        var choices=new List<KeyValuePair<string,string>>();
        if(!slam&&w.Id!=WeaponStanceMechanic.Korumm)choices.Add(new("Projectile","투사체 직격"));
        if(!slam&&Enumerable.Range(1,WeaponActionFlight.Sequences(w.Id)).Any(i=>WeaponActionFlight.BladeHits(w.Id,i).Length>0))choices.Add(new("Blade","칼날 접촉"));
        if(WeaponActionFlight.AutomaticExplosion(w.Id)||w.Id is WeaponStanceMechanic.Grigori or VinquibusState.WeaponId)choices.Add(new("Surface",w.Id==WeaponStanceMechanic.Grigori?"표면 반사 · 피해 없음":w.Id==VinquibusState.WeaponId?"표면 정지 · 발사 후 0.8초 폭발":"표면 충돌 · 폭발"));
        if(w.Id is WeaponStanceMechanic.Verdilac or WeaponStanceMechanic.Korumm)choices.Add(new("Explosion",w.Id==WeaponStanceMechanic.Korumm?"주변 충격파":"소멸 폭발"));
        if(slam){choices.Clear();choices.Add(new("Slam","슬램 광역 타격"));}
        var kind=new ComboBox{ItemsSource=choices,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=slam?"Slam":c.Blade?"Blade":c.Surface?"Surface":c.Explosion||w.Id==WeaponStanceMechanic.Korumm?"Explosion":"Projectile"};
        var e=new WeaponContactEditor(FlightNumber(c.Time),FlightNumber(c.Target),FlightNumber(c.Projectile),FlightNumber(c.Travel),kind,FlightNumber(c.BladeHit),FlightNumber(c.PenetratedMeters),FlightNumber(c.Launched),FlightNumber(c.SlamHit));
        var row=new StackPanel{Margin=new(0,10,0,6)};row.Children.Add(FlightLine(kind,Button("접촉 삭제",()=>{parent.Contacts.Remove(e);parent.Rows.Children.Remove(row);Recalculate();})));
        row.Children.Add(FlightLine(Field("명중 (초)",e.Time),Field("대상 · 폭발 중심 번호",e.Target)));
        if(slam)row.Children.Add(Field("슬램 타격 번호 · 1 또는 2",e.SlamHit));
        var projectile=FlightLine(Field("투사체 번호",e.Projectile),Field("비행 거리 (m)",e.Travel));var blade=Field("이 동작의 칼날 타격 번호",e.BladeHit);row.Children.Add(projectile);row.Children.Add(blade);
        var penetrated=Field($"누적 관통 두께 (m) · 최대{WeaponActionFlight.PunchThrough(w.Id):0.#}m",e.Penetrated);if(WeaponStanceMechanic.IsWarfan(w.Id))row.Children.Add(penetrated);
        var launch=Field("이 화염구 발사 (초) · 개별 시각",e.Launched);if(w.Id==WeaponStanceMechanic.Nepheri)row.Children.Add(launch);
        void Show(){var value=kind.SelectedValue as string;projectile.Visibility=value is "Projectile" or "Surface"?Visibility.Visible:Visibility.Collapsed;blade.Visibility=value=="Blade"?Visibility.Visible:Visibility.Collapsed;penetrated.Visibility=value=="Projectile"?Visibility.Visible:Visibility.Collapsed;launch.Visibility=value=="Projectile"?Visibility.Visible:Visibility.Collapsed;}
        kind.SelectionChanged+=(_,_)=>Show();Show();parent.Contacts.Add(e);parent.Rows.Children.Add(row);FlightWatch(e.Time,e.Target,e.Projectile,e.Travel,kind,e.BladeHit,e.Penetrated,e.Launched,e.SlamHit);
    }
    private WeaponActionInput[] ReadWeaponActions()
    {
        if(!WeaponActionSelected||weapon.SelectedItem is not Weapon w||weaponActionEditorId!=w.Id)return [];
        return weaponActionEditors.Select(e=>new WeaponActionInput(
            Number(e.Start,"공격 입력"),e.Mode is "Slam" or "HeavySlam"?Number(e.Start,"공격 입력"):Number(e.Launch,"발사"),(int)e.Sequence.SelectedValue,e.Id,
            e.Contacts.Select(c=>new WeaponActionContact(
                Number(c.Time,"명중"),Integer(c.Target,"대상 번호"),Integer(c.Projectile,"투사체 번호"),Number(c.Travel,"비행 거리"),
                c.Kind.SelectedValue as string=="Blade",Integer(c.BladeHit,"칼날 타격 번호"),c.Kind.SelectedValue as string=="Surface",c.Kind.SelectedValue as string=="Explosion",
                Number(c.Penetrated,"관통 두께"),FlightOptional(c.Launched,"개별 발사"),Integer(c.SlamHit,"슬램 타격 번호"))).ToArray(),
            e.Model.SelectedValue as string??"Unresolved",e.Independent.IsChecked==true,FlightOptional(e.Expires,"원반 만료"),e.Sliding.IsChecked==true,e.Mode,e.Actor.SelectedValue is int actor?actor:0)).ToArray();
    }
    private void ApplyWeaponActions(EncounterSetup? setup){weaponActionEditors.Clear();weaponActionRows.Children.Clear();foreach(var v in setup?.WeaponActions??[])AddWeaponAction(v);}
    private void RefreshWeaponActionVisibility(string id)
    {
        if(warfanSlamButtons is not null)warfanSlamButtons.Visibility=WarfanSlam.Applies(id)?Visibility.Visible:Visibility.Collapsed;
        if(weaponActionPanel is null)return;weaponActionPanel.Visibility=WeaponActionFlight.Applies(id)?Visibility.Visible:Visibility.Collapsed;
        if(weaponActionEditorId is {} previous&&previous!=id){weaponActionEditors.Clear();weaponActionRows.Children.Clear();weaponActionEditorId=id;}
    }
}
