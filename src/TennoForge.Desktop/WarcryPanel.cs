using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private readonly CheckBox warcryEnabled=new(){Content="워크라이"},warcryHelminth=new(){Content="헬민스로 주입한 워크라이"},
        warcrySelf=new(){Content="내가 시전",IsChecked=true},warcryAtStart=new(){Content="교전 시작 전에 활성화",IsChecked=true},
        eternalWar=new(){Content="이터널 워 · 근접 처치로 지속시간 연장"},warcryHysteria=new(){Content="내 히스테리아 활성 · 방어력 보너스 3배"};
    private readonly ComboBox warcryRank=new(),eternalRank=new();
    private readonly TextBox warcryStrength=new(){Text="100"},warcryDuration=new(){Text="100"},warcryRemaining=new(),warcryBaseArmor=new(){Text="855"};
    private FrameworkElement CreateWarcryPanel()
    {
        warcryRank.ItemsSource=Enumerable.Range(0,4);warcryRank.SelectedItem=3;eternalRank.ItemsSource=Enumerable.Range(0,4);eternalRank.SelectedItem=3;
        var remaining=Field("시작 시 남은 시간 (초) · 비우면 기본 지속시간",warcryRemaining);
        var rank=Field("이터널 워 랭크",eternalRank);
        var note=T("공속은 퓨리와 합산합니다. 방어력은 받는 워프레임의 기본값으로 계산합니다. 855는 발키르 기본값이며 다른 워프레임은 수정하세요. 시전·무효화·아군 버프 재진입은 전투 중 행동에서 설정합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var detail=Stack(warcrySelf,warcryHelminth,FlightLine(Field("어빌리티 랭크",warcryRank),Field("시전자 위력 (%)",warcryStrength)),
            Field("시전자 지속 (%)",warcryDuration),warcryAtStart,remaining,eternalWar,rank,Field("받는 워프레임의 기본 방어력",warcryBaseArmor),warcryHysteria,note);
        var automaticHysteria=false;
        void Show(){var talons=weapon.SelectedItem is Weapon w&&ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Talons;if(talons){warcryHysteria.IsChecked=true;automaticHysteria=true;}else if(automaticHysteria){warcryHysteria.IsChecked=false;automaticHysteria=false;}detail.Visibility=warcryEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;remaining.Visibility=warcryAtStart.IsChecked==true?Visibility.Visible:Visibility.Collapsed;rank.Visibility=eternalWar.IsChecked==true?Visibility.Visible:Visibility.Collapsed;warcryHysteria.IsEnabled=!talons&&(warcryHelminth.IsChecked!=true||warcrySelf.IsChecked!=true);if(!warcryHysteria.IsEnabled&&!talons)warcryHysteria.IsChecked=false;}
        foreach(var c in new[]{warcryEnabled,warcryAtStart,eternalWar,warcryHelminth,warcrySelf}){c.Checked+=(_,_)=>Show();c.Unchecked+=(_,_)=>Show();}Show();
        weapon.SelectionChanged+=(_,_)=>Show();
        FlightWatch(warcryEnabled,warcryHelminth,warcrySelf,warcryAtStart,eternalWar,warcryHysteria,warcryRank,eternalRank,warcryStrength,warcryDuration,warcryRemaining,warcryBaseArmor);
        return Stack(warcryEnabled,detail);
    }
    private WarcrySetup? ReadWarcry()=>warcryEnabled.IsChecked!=true?null:new(
        Rank:warcryRank.SelectedItem is int r?r:3,Strength:Number(warcryStrength,"워크라이 시전자 위력")/100,Duration:Number(warcryDuration,"워크라이 시전자 지속")/100,
        Helminth:warcryHelminth.IsChecked==true,EternalWarRank:eternalWar.IsChecked==true?eternalRank.SelectedItem is int er?er:3:null,
        SelfCast:warcrySelf.IsChecked==true,Active:warcryAtStart.IsChecked==true,Remaining:warcryAtStart.IsChecked==true&&!string.IsNullOrWhiteSpace(warcryRemaining.Text)?Number(warcryRemaining,"워크라이 시작 잔여 시간"):null,
        BaseArmor:Number(warcryBaseArmor,"받는 워프레임 기본 방어력"),HysteriaActive:warcryHysteria.IsChecked==true||weapon.SelectedItem is Weapon w&&ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Talons);
    private void ApplyWarcry(WarcrySetup? setup)
    {
        warcryEnabled.IsChecked=setup is not null;var s=setup??new();warcryRank.SelectedItem=s.Rank;warcryStrength.Text=(s.Strength*100).ToString(CultureInfo.InvariantCulture);
        warcryDuration.Text=(s.Duration*100).ToString(CultureInfo.InvariantCulture);warcryHelminth.IsChecked=s.Helminth;warcrySelf.IsChecked=s.SelfCast;
        warcryAtStart.IsChecked=s.Active;warcryRemaining.Text=s.Remaining?.ToString(CultureInfo.InvariantCulture)??"";eternalWar.IsChecked=s.EternalWarRank is not null;
        eternalRank.SelectedItem=s.EternalWarRank??3;warcryBaseArmor.Text=s.BaseArmor.ToString(CultureInfo.InvariantCulture);warcryHysteria.IsChecked=s.HysteriaActive;
    }
}
