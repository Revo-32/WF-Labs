using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record OkinaEditor(TextBox Launch,TextBox Impact,TextBox Dagger,TextBox Target,TextBox Travel);
    private readonly List<OkinaEditor> okinaEditors=[];
    private readonly StackPanel okinaRows=new();
    private Expander? okinaPanel;
    private FrameworkElement CreateOkinaPanel()
    {
        var note=T("처치로 생긴 단검은 최대 6개입니다. 궤도 대기시간은 위키에 없어 발사·명중 시각을 지정합니다. 단검 번호는 생성 순서입니다. 비행 거리를 알면 18m/s 조건도 확인합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        okinaPanel=new(){Header="오키나 · 유령 단검 비행",Visibility=Visibility.Collapsed,Content=Stack(note,okinaRows,Button("＋ 단검 비행",()=>{AddOkinaFlight(new(.1,1,Target:ReadNearby().Length>0?2:1));Recalculate();}))};return okinaPanel;
    }
    private void AddOkinaFlight(OkinaDaggerFlight f)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var e=new OkinaEditor(Input(f.Launch),Input(f.Impact),Input(f.Dagger),Input(f.Target),Input(f.Travel));var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("명중 (초)",e.Impact),Field("비행 거리 (m) · 선택",e.Travel)));
        var remove=Button("삭제",()=>{okinaEditors.Remove(e);okinaRows.Children.Remove(row);Recalculate();});remove.Height=36;remove.VerticalAlignment=VerticalAlignment.Bottom;
        row.Children.Add(Line(Field("단검 번호",e.Dagger),Field("대상 번호",e.Target),remove));okinaEditors.Add(e);okinaRows.Children.Add(row);
        foreach(var box in new[]{e.Launch,e.Impact,e.Dagger,e.Target,e.Travel})box.TextChanged+=(_,_)=>Recalculate();
    }
    private OkinaDaggerFlight[] ReadOkinaFlights()=>weapon.SelectedItem is not Weapon w||!OkinaDaggerState.Applies(w.Id)?[]:okinaEditors.Select(e=>new OkinaDaggerFlight(Number(e.Launch,"단검 발사 시각"),Number(e.Impact,"단검 명중 시각"),Integer(e.Dagger,"단검 번호"),Integer(e.Target,"단검 대상 번호"),string.IsNullOrWhiteSpace(e.Travel.Text)?null:Number(e.Travel,"단검 비행 거리"))).ToArray();
    private void ApplyOkinaFlights(EncounterSetup? setup){okinaEditors.Clear();okinaRows.Children.Clear();foreach(var f in setup?.OkinaDaggers??[])AddOkinaFlight(f);}
}
