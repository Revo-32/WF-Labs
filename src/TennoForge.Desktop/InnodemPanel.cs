using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record InnodemEditor(TextBox Launch,TextBox Distance,TextBox Projectile,TextBox Target,TextBox Motion,TextBox Location,ComboBox Co);
    private readonly List<InnodemEditor> innodemEditors=[];
    private readonly StackPanel innodemRows=new();
    private readonly CheckBox innodemOnly=new(){Content="투사체만 계산",IsChecked=true};
    private Expander? innodemPanel;
    private bool InnodemSelected=>weapon.SelectedItem is Weapon w&&w.Id==InnodemFlight.WeaponId;
    private FrameworkElement CreateInnodemPanel()
    {
        var note=T("조준 활공에서 발사하는 파동입니다. 60m/s로 이동하며 21m 안의 대상을 계속 관통합니다. 같은 발사 번호로 대상을 추가하면 하나의 파동으로 계산합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var caveat=T("위키에 불규칙한 위치 배율과 컨오 1·2배 분기의 선택 규칙이 없습니다. 아래 배율을 지정한 조건부 결과이며 자동 확정값이 아닙니다.",14);caveat.TextWrapping=TextWrapping.Wrap;caveat.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
        innodemOnly.Checked+=(_,_)=>Recalculate();innodemOnly.Unchecked+=(_,_)=>Recalculate();
        innodemPanel=new(){Header="인노뎀 · 조준 활공 파동",Visibility=Visibility.Collapsed,Content=Stack(note,caveat,innodemOnly,innodemRows,Button("＋ 파동 명중 대상",()=>{AddInnodemContact(new(0,.1,6));Recalculate();}))};return innodemPanel;
    }
    private void AddInnodemContact(InnodemContact c)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var co=new ComboBox{ItemsSource=new[]{1,2},SelectedItem=c.CoMultiplier};var e=new InnodemEditor(Input(c.Launch),Input(c.Distance),Input(c.Projectile),Input(c.Target),Input(c.MotionMultiplier),Input(c.LocationMultiplier),co);
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Line(Field("발사 번호",e.Projectile),Field("대상 번호",e.Target)));
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("비행 거리 (m)",e.Distance)));
        row.Children.Add(Line(Field("발사 동작 배율",e.Motion),Field("위치 배율 · 가정",e.Location),Field("컨오 분기",e.Co)));
        var arrival=T("",13);arrival.Margin=new(0,0,0,8);row.Children.Add(arrival);
        void Arrival(){arrival.Text=double.TryParse(e.Launch.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var t)&&double.TryParse(e.Distance.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var d)?$"명중 {t+d/60:0.###}초 · 60m/s 직선 비행":"명중 시각은 발사·거리에서 계산";}Arrival();
        var remove=Button("삭제",()=>{innodemEditors.Remove(e);innodemRows.Children.Remove(row);Recalculate();});remove.Height=32;row.Children.Add(remove);innodemEditors.Add(e);innodemRows.Children.Add(row);
        foreach(var box in new[]{e.Launch,e.Distance,e.Projectile,e.Target,e.Motion,e.Location})box.TextChanged+=(_,_)=>{Arrival();Recalculate();};e.Co.SelectionChanged+=(_,_)=>Recalculate();
    }
    private InnodemContact[] ReadInnodemContacts()=>!InnodemSelected?[]:innodemEditors.Select(e=>new InnodemContact(Number(e.Launch,"인노뎀 발사"),Number(e.Launch,"인노뎀 발사")+Number(e.Distance,"비행 거리")/60,Number(e.Distance,"비행 거리"),Integer(e.Projectile,"발사 번호"),Integer(e.Target,"대상 번호"),Number(e.Motion,"발사 동작 배율"),string.IsNullOrWhiteSpace(e.Location.Text)?null:Number(e.Location,"위치 배율 가정"),e.Co.SelectedItem as int?)).ToArray();
    private void ApplyInnodemContacts(EncounterSetup? setup){innodemEditors.Clear();innodemRows.Children.Clear();innodemOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.InnodemContacts??[])AddInnodemContact(c);}
}
