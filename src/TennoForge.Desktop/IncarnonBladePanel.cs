using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record BladeEditor(TextBox Launch,TextBox Impact,TextBox Projectile,TextBox Target,TextBox Travel,TextBox Hit,ComboBox Motion,CheckBox Surface,CheckBox Physical,TextBox BladeTime,TextBox BladeTarget);
    private readonly List<BladeEditor> bladeEditors=[];
    private readonly StackPanel bladeRows=new();
    private readonly CheckBox bladeOnly=new(){Content="지정한 공격만 계산 · 자동 반복 끄기",IsChecked=true};
    private Expander? bladePanel;
    private bool BladeSelected=>weapon.SelectedItem is Weapon w&&IncarnonBladeFlight.Applies(w.Id);
    private FrameworkElement CreateBladePanel()
    {
        var note=T("발사 동작과 명중 시각을 지정합니다. 헤이트는 발사 타격의 스탠스 배율을 물려받고 명중 0.4초 뒤 폭발합니다. 액크 & 브런트는 공중 2배 · 슬라이드 1배로 즉시 폭발합니다. 표면 충돌은 선택한 대상 위치에서 직격 없이 폭발합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        bladeOnly.Checked+=(_,_)=>Recalculate();bladeOnly.Unchecked+=(_,_)=>Recalculate();
        bladePanel=new(){Header="인카논 · 유령 칼날",Visibility=Visibility.Collapsed,Content=Stack(note,bladeOnly,bladeRows,Button("＋ 투사체 발사",()=>{
            var number=bladeEditors.Count==0?1:bladeEditors.Max(e=>Integer(e.Projectile,"투사체 번호"))+1;
            AddBladeContact(new(0,.1,number,Motion:weapon.SelectedItem is Weapon w&&w.Id==IncarnonBladeFlight.Hate?"Neutral":"Aerial"));Recalculate();
        }))};return bladePanel;
    }
    private void AddBladeContact(IncarnonBladeContact c)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var hate=weapon.SelectedItem is Weapon w&&w.Id==IncarnonBladeFlight.Hate;
        var motion=new ComboBox{ItemsSource=hate?new[]{new KeyValuePair<string,string>("Neutral","정지"),new("Forward","전진")}:new[]{new KeyValuePair<string,string>("Aerial","공중"),new("Slide","슬라이드")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=c.Motion};
        var e=new BladeEditor(Input(c.Launch),Input(c.Impact),Input(c.Projectile),Input(c.Target),Input(c.Travel),Input(c.LaunchHit),motion,new(){Content="표면 충돌",IsChecked=c.Surface},new(){Content="같은 공격의 칼날도 명중",IsChecked=c.BladeImpact is not null},Input(c.BladeImpact??c.Launch),Input(c.BladeTarget));
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Line(Field("발사 번호",e.Projectile),Field("대상 번호",e.Target),Field("발사 동작",e.Motion)));
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("명중 (초)",e.Impact),Field("거리 (m) · 선택",e.Travel)));
        if(hate)row.Children.Add(Field("발사 타격 · 콤보 내 순서",e.Hit));
        else
        {
            var detail=Stack(Line(Field("칼날 명중 (초)",e.BladeTime),Field("칼날 대상 번호",e.BladeTarget)),Field("타격 순서 · 공중 1~3 / 슬라이드 1",e.Hit));
            detail.Visibility=e.Physical.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
            var explanation=T("선택한 칼날 1타와 이 투사체가 원소 충전을 한 번만 공유합니다. 두 접촉 시각은 직접 지정하며 실제 칼날 명중만 콤보를 얻습니다.",14,"#596B85");explanation.TextWrapping=TextWrapping.Wrap;detail.Children.Add(explanation);
            row.Children.Add(e.Physical);row.Children.Add(detail);
            e.Physical.Checked+=(_,_)=>{detail.Visibility=Visibility.Visible;Recalculate();};e.Physical.Unchecked+=(_,_)=>{detail.Visibility=Visibility.Collapsed;Recalculate();};
        }
        var remove=Button("삭제",()=>{bladeEditors.Remove(e);bladeRows.Children.Remove(row);Recalculate();});remove.Height=32;row.Children.Add(Line(e.Surface,remove));bladeEditors.Add(e);bladeRows.Children.Add(row);
        foreach(var box in new[]{e.Launch,e.Impact,e.Projectile,e.Target,e.Travel,e.Hit,e.BladeTime,e.BladeTarget})box.TextChanged+=(_,_)=>Recalculate();e.Motion.SelectionChanged+=(_,_)=>Recalculate();e.Surface.Checked+=(_,_)=>Recalculate();e.Surface.Unchecked+=(_,_)=>Recalculate();
    }
    private IncarnonBladeContact[] ReadBladeContacts()=>!BladeSelected?[]:bladeEditors.Select(e=>new IncarnonBladeContact(Number(e.Launch,"유령 칼날 발사 시각"),Number(e.Impact,"유령 칼날 명중 시각"),Integer(e.Projectile,"투사체 번호"),Integer(e.Target,"대상 번호"),(string?)e.Motion.SelectedValue??"",Integer(e.Hit,"발사 타격"),e.Surface.IsChecked==true,string.IsNullOrWhiteSpace(e.Travel.Text)?null:Number(e.Travel,"비행 거리"),e.Physical.IsChecked==true?Number(e.BladeTime,"칼날 명중 시각"):null,Integer(e.BladeTarget,"칼날 대상 번호"))).ToArray();
    private void ApplyBladeContacts(EncounterSetup? setup){bladeEditors.Clear();bladeRows.Children.Clear();bladeOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.IncarnonBlades??[])AddBladeContact(c);}
}
