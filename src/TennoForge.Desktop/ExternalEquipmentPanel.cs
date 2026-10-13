using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private readonly ComboBox externalPrimary=new(),externalPrimaryRank=new(),jetStreamRank=new();
    private readonly CheckBox jetStreamActive=new(){Content="제트 스트림"};
    private readonly TextBox jetStreamStrength=new(){Text="100"},jetStreamRemaining=new();
    private Expander? externalEquipmentPanel;
    private FrameworkElement CreateExternalEquipmentPanel()
    {
        externalPrimary.ItemsSource=new[]{new KeyValuePair<string,string>("None","없음"),new("Daikyu","다이큐 · 아말감 타겟 어콰이어드"),new("Argonak","아르고나크 · 아말감 메탈 어거")};externalPrimary.DisplayMemberPath="Value";externalPrimary.SelectedValuePath="Key";externalPrimary.SelectedValue="None";
        externalPrimaryRank.ItemsSource=Enumerable.Range(0,6);externalPrimaryRank.SelectedItem=5;jetStreamRank.ItemsSource=Enumerable.Range(0,4);jetStreamRank.SelectedItem=3;
        var rank=Field("주무기 아말감 모드 랭크",externalPrimaryRank);
        var jet=Stack(FlightLine(Field("제트 스트림 랭크",jetStreamRank),Field("시전자 위력 (%)",jetStreamStrength)),Field("범위 밖 잔여 시간 (0~3초) · 비우면 범위 안",jetStreamRemaining));
        var note=T("니카나 흡혈은 상태 틱을 제외합니다. 단검 방어력 감소는 상태 틱·인플루언스에도 적용합니다. 해당 무기군에서만 효과가 발생합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        void Show(){rank.Visibility=externalPrimary.SelectedValue as string=="None"?Visibility.Collapsed:Visibility.Visible;jet.Visibility=jetStreamActive.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
        externalPrimary.SelectionChanged+=(_,_)=>Show();jetStreamActive.Checked+=(_,_)=>Show();jetStreamActive.Unchecked+=(_,_)=>Show();Show();
        FlightWatch(externalPrimary,externalPrimaryRank,jetStreamActive,jetStreamRank,jetStreamStrength,jetStreamRemaining);
        return externalEquipmentPanel=new(){Header="외부 장비 · 근접 지원",Content=Stack(Field("함께 장착한 주무기",externalPrimary),rank,note,jetStreamActive,jet,CreateWarcryPanel(),CreateMetronomePanel(),CreateHallOfMirrorsPanel(),CreateAtlasRubblePanel())};
    }
    private MeleeExternalEquipment? ReadExternalEquipment()
    {
        var primary=externalPrimary.SelectedValue as string??"None";
        if(primary=="None"&&jetStreamActive.IsChecked!=true&&warcryEnabled.IsChecked!=true&&metronomeEnabled.IsChecked!=true&&mirrorsEnabled.IsChecked!=true&&rubbleEnabled.IsChecked!=true)return null;
        return new(primary,externalPrimaryRank.SelectedItem is int rank?rank:5,jetStreamActive.IsChecked==true,jetStreamRank.SelectedItem is int jr?jr:3,Number(jetStreamStrength,"제트 스트림 시전자 위력")/100,ReadWarcry(),ReadMetronome(),jetStreamActive.IsChecked==true&&!string.IsNullOrWhiteSpace(jetStreamRemaining.Text)?Number(jetStreamRemaining,"제트 스트림 잔여 시간"):null,ReadHallOfMirrors(),ReadAtlasRubble());
    }
    private void ApplyExternalEquipment(MeleeExternalEquipment? e)
    {
        e??=new();externalPrimary.SelectedValue=e.Primary;externalPrimaryRank.SelectedItem=e.Rank;jetStreamActive.IsChecked=e.JetStream;jetStreamRank.SelectedItem=e.JetStreamRank;jetStreamStrength.Text=(e.JetStreamStrength*100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        jetStreamRemaining.Text=e.JetStreamRemaining?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
        ApplyWarcry(e.Warcry);ApplyMetronome(e.Metronome);ApplyHallOfMirrors(e.HallOfMirrors);ApplyAtlasRubble(e.AtlasRubble);
    }
}
