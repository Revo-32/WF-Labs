using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record WaveEditor(TextBox Launch,TextBox Impact,TextBox Distance,TextBox Projectile,TextBox Target,TextBox Wave,TextBox Cast,ComboBox Combo,ComboBox Falloff,ComboBox ImpactCombo,TextBox ImpactGroup);
    private readonly List<WaveEditor> waveEditors=[];
    private readonly StackPanel waveRows=new();
    private readonly CheckBox wavesOnly=new(){Content="파동만 계산",IsChecked=true};
    private Expander? wavePanel;
    private bool WaveSelected=>weapon.SelectedItem is Weapon w&&ExaltedWaveFlight.Applies(w.Id);
    private FrameworkElement CreateExaltedWavePanel()
    {
        var note=T("발사 동작에 포함된 파동을 선택합니다. 같은 파동 번호로 관통 대상을 추가하고, 강공격 교차 파동 2개는 같은 공격 번호로 묶습니다. 검·주먹 접촉은 별도입니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var scope=T("블레이드의 75m 시간식과 70m 거리식은 원문이 충돌해 조건부 선택입니다. 데저트 윈드는 20m·시야 조건을 확인하며 속도 미기재로 접촉 시각을 지정합니다.",14);scope.TextWrapping=TextWrapping.Wrap;scope.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
        wavesOnly.Checked+=(_,_)=>Recalculate();wavesOnly.Unchecked+=(_,_)=>Recalculate();
        wavePanel=new(){Header="익절티드 · 파동 비행",Visibility=Visibility.Collapsed,Content=Stack(note,scope,wavesOnly,waveRows,Button("＋ 파동 명중 대상",()=>{AddWave(new(0,.1,3));Recalculate();}))};return wavePanel;
    }
    private void AddWave(ExaltedWaveContact c)
    {
        TextBox N(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
        var w=(Weapon)weapon.SelectedItem;var d=ExaltedRules.Find(w.Id)!;var blade=d.Family==ExaltedFamily.Blade;
        var combos=stanceCatalog.ForWeapon(w).Single(s=>s.Id==d.Stance).Combos.Where(x=>x.Hits.Any(h=>h.AttackType=="Ranged")).Select(x=>new KeyValuePair<string,string>(x.Key,x.InputLabel)).ToArray();
        var combo=new ComboBox{ItemsSource=combos,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=c.Combo};
        var falloff=new ComboBox{ItemsSource=new[]{new KeyValuePair<int,string>(0,"원문 선택 필요"),new(1,"시간식 · 30m/s / 75m"),new(2,"거리식 · 0~70m 선형")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=c.FalloffModel};
        var impactCombo=new ComboBox{ItemsSource=new[]{new KeyValuePair<string,string>("","동작 없음 · 100%")}.Concat(stanceCatalog.ForWeapon(w).Single(s=>s.Id==d.Stance).Combos.Where(x=>x.FinisherKind is null&&x.Hits.Any(h=>h.AttackType is "" or "360")).Select(x=>new KeyValuePair<string,string>(x.Key,x.InputLabel))).ToArray(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=c.ImpactCombo??""};
        var e=new WaveEditor(N(c.Launch),N(c.Impact),N(c.Distance),N(c.Projectile),N(c.Target),N(c.Wave),N(c.Cast),combo,falloff,impactCombo,N(c.ImpactGroup));e.Impact.IsReadOnly=blade;
        var row=new StackPanel{Margin=new(0,12,0,8)};
        row.Children.Add(GlassLine(Field("공격 번호",e.Cast),Field("파동 번호",e.Projectile),Field("대상 번호",e.Target)));
        row.Children.Add(GlassLine(Field("발사 동작",e.Combo),Field("동작 내 파동 순서",e.Wave)));
        row.Children.Add(GlassLine(Field("발사 (초)",e.Launch),Field("누적 비행 (m)",e.Distance),Field(blade?"명중 (초) · 자동":"명중 (초)",e.Impact)));
        if(blade)row.Children.Add(Field("감쇠 원문 · 조건부 계산",e.Falloff));
        var impactFields=GlassLine(Field("명중 순간 진행 중인 동작",e.ImpactCombo),Field("동작 내 공격 순서",e.ImpactGroup));
        if(blade)row.Children.Add(impactFields);
        e.ImpactCombo.ToolTip="일반 파동은 명중 순간 동작의 배율을 읽습니다. 발사 동작과 같다고 가정하지 않습니다. 위키 익절티드 블레이드 스탠스의 명시된 규칙입니다.";
        void ImpactVisibility(){impactFields.Visibility=blade&&e.Combo.SelectedValue is not "Heavy"?Visibility.Visible:Visibility.Collapsed;e.ImpactGroup.IsEnabled=e.ImpactCombo.SelectedValue is string key&&key.Length>0;}
        ImpactVisibility();
        var remove=Button("삭제",()=>{waveEditors.Remove(e);waveRows.Children.Remove(row);Recalculate();});remove.Height=32;row.Children.Add(remove);waveRows.Children.Add(row);waveEditors.Add(e);
        void Arrival(){if(blade&&double.TryParse(e.Launch.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var launch)&&double.TryParse(e.Distance.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var distance))e.Impact.Text=(launch+distance/30).ToString("G17",CultureInfo.InvariantCulture);}
        foreach(var input in new[]{e.Launch,e.Distance})input.TextChanged+=(_,_)=>{Arrival();Recalculate();};foreach(var input in new[]{e.Impact,e.Projectile,e.Target,e.Wave,e.Cast,e.ImpactGroup})input.TextChanged+=(_,_)=>Recalculate();e.Combo.SelectionChanged+=(_,_)=>{ImpactVisibility();Recalculate();};e.Falloff.SelectionChanged+=(_,_)=>Recalculate();e.ImpactCombo.SelectionChanged+=(_,_)=>{ImpactVisibility();Recalculate();};
    }
    private ExaltedWaveContact[] ReadWaves()=>!WaveSelected?[]:waveEditors.Select(e=>new ExaltedWaveContact(Number(e.Launch,"파동 발사"),Number(e.Impact,"파동 명중"),Number(e.Distance,"파동 누적 비행 거리"),Integer(e.Projectile,"파동 번호"),Integer(e.Target,"파동 대상"),(string?)e.Combo.SelectedValue??"",Integer(e.Wave,"스탠스 파동 순서"),Integer(e.Cast,"공격 번호"),(int?)e.Falloff.SelectedValue??0,e.Combo.SelectedValue is not "Heavy"&&e.ImpactCombo.SelectedValue is string key&&key.Length>0?key:null,Integer(e.ImpactGroup,"명중 순간 공격 순서"))).ToArray();
    private void ApplyWaves(EncounterSetup? setup){waveEditors.Clear();waveRows.Children.Clear();wavesOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.ExaltedWaves??[])AddWave(c);}
}
