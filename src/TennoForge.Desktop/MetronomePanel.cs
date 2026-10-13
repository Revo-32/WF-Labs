using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private readonly CheckBox metronomeEnabled=new(){Content="메트로놈 · 오페라 / 포르테"},operaEnabled=new(){Content="오페라 · 멀티샷",IsChecked=true},forteEnabled=new(){Content="포르테 · 근접 피해"};
    private readonly ComboBox metronomeRank=new();
    private readonly TextBox metronomeStrength=new(){Text="100"},metronomeDuration=new(){Text="100"},operaRemaining=new(),forteRemaining=new();
    private FrameworkElement CreateMetronomePanel()
    {
        metronomeRank.ItemsSource=Enumerable.Range(0,4);metronomeRank.SelectedItem=3;
        var operaLeft=Field("오페라 남은 시간 (초) · 비우면 기본 지속시간",operaRemaining);
        var forteLeft=Field("포르테 남은 시간 (초) · 비우면 기본 지속시간",forteRemaining);
        var note=T("오페라는 콰서스 단검과 리디머 프라임 사격의 추가 투사체를 계산합니다. 포르테는 프레셔 포인트·컨오와 가산합니다. 도중에 버프를 받거나 갱신하는 시각은 전투 중 행동에서 설정하세요.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var detail=Stack(FlightLine(Field("어빌리티 랭크",metronomeRank),Field("시전자 위력 (%)",metronomeStrength)),Field("시전자 지속 (%)",metronomeDuration),operaEnabled,operaLeft,forteEnabled,forteLeft,note);
        void Show(){detail.Visibility=metronomeEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;operaLeft.Visibility=operaEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;forteLeft.Visibility=forteEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
        foreach(var c in new[]{metronomeEnabled,operaEnabled,forteEnabled}){c.Checked+=(_,_)=>Show();c.Unchecked+=(_,_)=>Show();}Show();
        FlightWatch(metronomeEnabled,operaEnabled,forteEnabled,metronomeRank,metronomeStrength,metronomeDuration,operaRemaining,forteRemaining);
        return Stack(metronomeEnabled,detail);
    }
    private MetronomeSetup? ReadMetronome()=>metronomeEnabled.IsChecked!=true?null:new(metronomeRank.SelectedItem is int rank?rank:3,Number(metronomeStrength,"메트로놈 위력")/100,Number(metronomeDuration,"메트로놈 지속")/100,operaEnabled.IsChecked==true,forteEnabled.IsChecked==true,
        operaEnabled.IsChecked==true&&!string.IsNullOrWhiteSpace(operaRemaining.Text)?Number(operaRemaining,"오페라 잔여 시간"):null,
        forteEnabled.IsChecked==true&&!string.IsNullOrWhiteSpace(forteRemaining.Text)?Number(forteRemaining,"포르테 잔여 시간"):null);
    private void ApplyMetronome(MetronomeSetup? setup)
    {
        metronomeEnabled.IsChecked=setup is not null;var s=setup??new(Opera:true);metronomeRank.SelectedItem=s.Rank;metronomeStrength.Text=(s.Strength*100).ToString(CultureInfo.InvariantCulture);metronomeDuration.Text=(s.Duration*100).ToString(CultureInfo.InvariantCulture);
        operaEnabled.IsChecked=s.Opera;forteEnabled.IsChecked=s.Forte;operaRemaining.Text=s.OperaRemaining?.ToString(CultureInfo.InvariantCulture)??"";forteRemaining.Text=s.ForteRemaining?.ToString(CultureInfo.InvariantCulture)??"";
    }
}
