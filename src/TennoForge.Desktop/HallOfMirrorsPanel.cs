using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private static ComboBox HologramActorChoice(int selected)=>new(){ItemsSource=Enumerable.Range(0,5).Select(n=>new KeyValuePair<int,string>(n,n==0?"플레이어":$"미라지 분신 {n}")).ToArray(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=selected};
    private sealed record HologramEditor(TextBox Time,ComboBox Number,TextBox Target,ComboBox Attack,TextBox StanceHit,TextBox Distance,TextBox TargetIndex);
    private readonly List<HologramEditor> hologramEditors=[];
    private readonly StackPanel hologramRows=new();
    private readonly CheckBox mirrorsEnabled=new(){Content="홀 오브 미러즈 · 미라지 분신"},mirrorsActive=new(){Content="교전 시작 전에 소환",IsChecked=true},malevolenceEnabled=new(){Content="홀 오브 멀레볼런스"},hologramOnly=new(){Content="지정한 분신 명중만 계산"};
    private readonly ComboBox mirrorsRank=new(),malevolenceRank=new(),mirrorArcaneRule=new();
    private readonly TextBox mirrorsStrength=new(){Text="100"},mirrorsDuration=new(){Text="100"},mirrorsRemaining=new(),malevolenceKills=new(){Text="0"},mirrorShields=new(),mirrorOvershields=new(){Text="0"};
    private FrameworkElement CreateHallOfMirrorsPanel()
    {
        mirrorsRank.ItemsSource=Enumerable.Range(0,4);mirrorsRank.SelectedItem=3;malevolenceRank.ItemsSource=Enumerable.Range(0,4);malevolenceRank.SelectedItem=3;
        mirrorArcaneRule.ItemsSource=new[]{new KeyValuePair<string,string>("Unresolved","아케인 문구 충돌 시 계산 중지"),new("Article","본문 기준 · 분신의 아케인 발동 제외")};mirrorArcaneRule.DisplayMemberPath="Value";mirrorArcaneRule.SelectedValuePath="Key";mirrorArcaneRule.SelectedValue="Unresolved";
        var augment=Stack(Field("멀레볼런스 랭크",malevolenceRank),Field("시작 처치 중첩 (0~10)",malevolenceKills));
        var note=T("분신마다 실제 명중을 추가합니다. 플레이어 콤보와 근접 처치 버프를 공유하지 않습니다. 고유 투사체는 접촉 경로의 공격 주체에서 분신을 선택하세요. 리탈리에이션은 분신 자신의 실드를 입력해야 합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var detail=Stack(FlightLine(Field("어빌리티 랭크",mirrorsRank),Field("위력 (%)",mirrorsStrength)),Field("지속 (%)",mirrorsDuration),mirrorsActive,Field("시작 잔여 시간 (초) · 비우면 기본 지속",mirrorsRemaining),malevolenceEnabled,augment,
            new Expander{Header="분신 아케인 · 출처와 실드",Content=Stack(Field("아케인 발동 출처 기준",mirrorArcaneRule),FlightLine(Field("분신 실드 · 모르면 비움",mirrorShields),Field("분신 오버실드",mirrorOvershields)))},note,hologramOnly,hologramRows,Button("＋ 분신 명중",()=>{AddHologramContact(new(hologramEditors.Count==0?0:hologramEditors.Max(e=>Number(e.Time,"명중 시각"))+.2));Recalculate();}));
        void Show(){detail.Visibility=mirrorsEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;augment.Visibility=malevolenceEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
        foreach(var c in new[]{mirrorsEnabled,malevolenceEnabled}){c.Checked+=(_,_)=>Show();c.Unchecked+=(_,_)=>Show();}Show();
        FlightWatch(mirrorsEnabled,mirrorsActive,mirrorsRank,mirrorsStrength,mirrorsDuration,mirrorsRemaining,malevolenceEnabled,malevolenceRank,malevolenceKills,mirrorShields,mirrorOvershields,mirrorArcaneRule,hologramOnly);
        weapon.SelectionChanged+=(_,_)=>{hologramEditors.Clear();hologramRows.Children.Clear();};
        return Stack(mirrorsEnabled,detail);
    }
    private void AddHologramContact(HologramContact c)
    {
        if(weapon.SelectedItem is not Weapon w)return;
        var actor=new ComboBox{ItemsSource=Enumerable.Range(1,4),SelectedItem=c.Number};
        var attack=new ComboBox{ItemsSource=MeleeEngine.Attacks(w.Id).Select(a=>new AttackChoice(a,AttackName(a))).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.Attack};
        var e=new HologramEditor(FlightNumber(c.Time),actor,FlightNumber(c.Target),attack,FlightNumber(c.StanceHit),FlightNumber(c.Distance),FlightNumber(c.TargetIndex));var row=new StackPanel{Margin=new(0,10,0,12)};
        row.Children.Add(FlightLine(Field("명중 (초)",e.Time),Field("분신 번호",actor),Field("대상 번호",e.Target)));
        row.Children.Add(Field("공격 동작",attack));row.Children.Add(new Expander{Header="거리·관통·스탠스 타격",Content=Stack(FlightLine(Field("분신에서 적까지 (m)",e.Distance),Field("관통 대상 순서",e.TargetIndex)),Field("선택 스탠스의 타격 번호 · 0이면 위 동작",e.StanceHit))});
        row.Children.Add(Button("삭제",()=>{hologramEditors.Remove(e);hologramRows.Children.Remove(row);Recalculate();}));hologramEditors.Add(e);hologramRows.Children.Add(row);FlightWatch(e.Time,actor,e.Target,attack,e.StanceHit,e.Distance,e.TargetIndex);
    }
    private HallOfMirrorsSetup? ReadHallOfMirrors()=>mirrorsEnabled.IsChecked!=true?null:new(
        mirrorsRank.SelectedItem is int rank?rank:3,Number(mirrorsStrength,"미라지 위력")/100,Number(mirrorsDuration,"미라지 지속")/100,mirrorsActive.IsChecked==true,FlightOptional(mirrorsRemaining,"분신 잔여 시간"),
        malevolenceEnabled.IsChecked==true?malevolenceRank.SelectedItem is int mr?mr:3:null,Integer(malevolenceKills,"멀레볼런스 중첩"),FlightOptional(mirrorShields,"분신 실드"),Number(mirrorOvershields,"분신 오버실드"),mirrorArcaneRule.SelectedValue as string??"Unresolved");
    private HologramContact[] ReadHologramContacts()=>mirrorsEnabled.IsChecked!=true?[]:hologramEditors.Select(e=>new HologramContact(Number(e.Time,"분신 명중 시각"),e.Number.SelectedItem is int n?n:1,Integer(e.Target,"분신 대상"),e.Attack.SelectedValue as string??"Normal Attack",Integer(e.StanceHit,"분신 스탠스 타격"),Number(e.Distance,"분신-적 거리"),Integer(e.TargetIndex,"분신 관통 대상 순서"))).ToArray();
    private void ApplyHallOfMirrors(HallOfMirrorsSetup? setup)
    {
        mirrorsEnabled.IsChecked=setup is not null;var s=setup??new();mirrorsRank.SelectedItem=s.Rank;mirrorsStrength.Text=(s.Strength*100).ToString(CultureInfo.InvariantCulture);mirrorsDuration.Text=(s.Duration*100).ToString(CultureInfo.InvariantCulture);mirrorsActive.IsChecked=s.Active;mirrorsRemaining.Text=s.Remaining?.ToString(CultureInfo.InvariantCulture)??"";
        malevolenceEnabled.IsChecked=s.MalevolenceRank is not null;malevolenceRank.SelectedItem=s.MalevolenceRank??3;malevolenceKills.Text=s.Kills.ToString();mirrorShields.Text=s.CloneShields?.ToString(CultureInfo.InvariantCulture)??"";mirrorOvershields.Text=s.CloneOvershields.ToString(CultureInfo.InvariantCulture);mirrorArcaneRule.SelectedValue=s.ArcaneTriggers;
    }
    private void ApplyHologramContacts(EncounterSetup? setup)
    {hologramEditors.Clear();hologramRows.Children.Clear();hologramOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.HologramContacts??[])AddHologramContact(c);}
}
