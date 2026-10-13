using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record HoverEditor(TextBox Launch,TextBox Attach,TextBox Target,TextBox Id,TextBox Detonate,TextBox Returned,TextBox Ticks,TextBox Travel);
    private readonly List<HoverEditor> hoverEditors=[];
    private readonly StackPanel hoverRows=new();
    private readonly CheckBox hoverOnly=new(){Content="매달기 투척만 계산",IsChecked=true};
    private readonly CheckBox hoverTable=new(){Content="위키 데이터 표 기준으로 계산",IsChecked=false};
    private Expander? hoverPanel;
    private bool OrviusSelected=>weapon.SelectedItem is Weapon w&&w.Id==OrviusHoverState.WeaponId;
    private FrameworkElement CreateOrviusPanel()
    {
        var note=T("본문: 10 베기·상확 미기재 / 데이터 표: 75 냉기·상확 60%. 서로 다른 출처이므로 표 기준 계산을 선택하면 조건부 결과로 표시합니다. 3초 자동 폭발 피해는 미확인이고, 수동 기폭은 계산합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        foreach(var toggle in new[]{hoverOnly,hoverTable}){toggle.Checked+=(_,_)=>Recalculate();toggle.Unchecked+=(_,_)=>Recalculate();}
        hoverPanel=new(){Header="오르비우스 · 매달기 투척",Visibility=Visibility.Collapsed,Content=Stack(note,hoverTable,hoverOnly,hoverRows,Button("＋ 투척",()=>{AddHover(new(0,.1,Throw:hoverEditors.Count+1,Detonate:2,Returned:2.2,TickTimes:[.2,1.2,2.2]));Recalculate();}))};return hoverPanel;
    }
    private void AddHover(OrviusHover h)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var e=new HoverEditor(Input(h.Launch),Input(h.Attach),Input(h.Target),Input(h.Throw),Input(h.Detonate),Input(h.Returned),new(){Text=string.Join(", ",(h.TickTimes??[]).Select(t=>t.ToString(CultureInfo.InvariantCulture)))},Input(h.Travel));
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Line(Field("투척 번호",e.Id),Field("대상 번호",e.Target)));
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("매달기 시작 (초)",e.Attach),Field("거리 (m) · 선택",e.Travel)));
        row.Children.Add(Field("3회 피해 시각 (초) · 기폭 이후 틱은 취소",e.Ticks));
        row.Children.Add(Line(Field("수동 기폭 (초)",e.Detonate),Field("회수 완료 (초)",e.Returned)));
        var remove=Button("삭제",()=>{hoverEditors.Remove(e);hoverRows.Children.Remove(row);Recalculate();});row.Children.Add(remove);hoverEditors.Add(e);hoverRows.Children.Add(row);
        foreach(var box in new[]{e.Launch,e.Attach,e.Target,e.Id,e.Detonate,e.Returned,e.Ticks,e.Travel})box.TextChanged+=(_,_)=>Recalculate();
    }
    private OrviusHover[] ReadHovers()=>!OrviusSelected?[]:hoverEditors.Select(e=>new OrviusHover(Number(e.Launch,"오르비우스 발사"),Number(e.Attach,"매달기 시작"),Integer(e.Target,"대상 번호"),Integer(e.Id,"투척 번호"),string.IsNullOrWhiteSpace(e.Detonate.Text)?null:Number(e.Detonate,"수동 기폭"),string.IsNullOrWhiteSpace(e.Returned.Text)?null:Number(e.Returned,"회수 완료"),e.Ticks.Text.Split([',',' ',';'],StringSplitOptions.RemoveEmptyEntries).Select(t=>double.TryParse(t,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:throw new ArgumentException("매달기 피해 시각은 숫자와 쉼표로 입력하세요.")).ToArray(),hoverTable.IsChecked==true,string.IsNullOrWhiteSpace(e.Travel.Text)?null:Number(e.Travel,"투사체 비행 거리"))).ToArray();
    private void ApplyHovers(EncounterSetup? setup){hoverEditors.Clear();hoverRows.Children.Clear();hoverOnly.IsChecked=setup?.ScriptedAttacksOnly==true;hoverTable.IsChecked=setup?.OrviusHovers?.Any(h=>h.UseInfobox)==true;foreach(var h in setup?.OrviusHovers??[])AddHover(h);}
}
