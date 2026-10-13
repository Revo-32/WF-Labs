using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record GlassEditor(TextBox Time,TextBox Target);
    private sealed record GlassSlamEditor(TextBox Time,TextBox Target,TextBox Slam,CheckBox Heavy);
    private readonly List<GlassEditor> glassEditors=[];
    private readonly List<GlassSlamEditor> glassSlamEditors=[];
    private readonly StackPanel glassRows=new(),glassSlamRows=new();
    private readonly CheckBox glassOnly=new(){Content="유리 폭발만 계산",IsChecked=true};
    private Expander? glassPanel;
    private bool VitricaSelected=>weapon.SelectedItem is Weapon w&&w.Id==VitricaGlassState.WeaponId;
    private FrameworkElement CreateVitricaPanel()
    {
        var note=T("유리화 투사체가 닿은 곳의 5m 안을 7초간 유리화합니다. 피해를 받으면 유리가 벗겨지고, 아래 슬램 접촉으로 깨뜨리면 원래 대상과 6m 주변에 각각 폭발 피해를 줍니다. 같은 슬램 번호는 한 번의 강공격 콤보를 공유합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var scope=T("기본 슬램과 지상 파동의 피해는 이 설정에 포함하지 않습니다. 파동의 치명타·상태 기본 수치는 위키 미기재입니다.",14);scope.TextWrapping=TextWrapping.Wrap;scope.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
        glassOnly.Checked+=(_,_)=>Recalculate();glassOnly.Unchecked+=(_,_)=>Recalculate();
        glassPanel=new(){Header="비트리카 · 유리화와 파괴",Visibility=Visibility.Collapsed,Content=Stack(note,scope,glassOnly,glassRows,Button("＋ 유리화 접촉",()=>{AddGlass(new(0));Recalculate();}),glassSlamRows,Button("＋ 슬램 접촉",()=>{AddGlassSlam(new(.5,Slam:glassSlamEditors.Count+1));Recalculate();}))};return glassPanel;
    }
    private static TextBox GlassNumber(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
    private static FrameworkElement GlassLine(params UIElement[] fields)
    {var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
    private void AddGlass(VitricaGlassContact c)
    {
        var e=new GlassEditor(GlassNumber(c.Time),GlassNumber(c.Target));var row=new StackPanel{Margin=new(0,10,0,8)};row.Children.Add(GlassLine(Field("유리화 접촉 (초)",e.Time),Field("중심 대상 번호",e.Target)));row.Children.Add(Button("삭제",()=>{glassEditors.Remove(e);glassRows.Children.Remove(row);Recalculate();}));glassRows.Children.Add(row);glassEditors.Add(e);e.Time.TextChanged+=(_,_)=>Recalculate();e.Target.TextChanged+=(_,_)=>Recalculate();
    }
    private void AddGlassSlam(VitricaSlamContact c)
    {
        var e=new GlassSlamEditor(GlassNumber(c.Time),GlassNumber(c.Target),GlassNumber(c.Slam),new(){Content="강 슬램",IsChecked=c.Heavy});var row=new StackPanel{Margin=new(0,12,0,8)};row.Children.Add(GlassLine(Field("슬램 접촉 (초)",e.Time),Field("대상 번호",e.Target),Field("슬램 번호",e.Slam)));row.Children.Add(GlassLine(e.Heavy,Button("삭제",()=>{glassSlamEditors.Remove(e);glassSlamRows.Children.Remove(row);Recalculate();})));glassSlamRows.Children.Add(row);glassSlamEditors.Add(e);foreach(var input in new[]{e.Time,e.Target,e.Slam})input.TextChanged+=(_,_)=>Recalculate();e.Heavy.Checked+=(_,_)=>Recalculate();e.Heavy.Unchecked+=(_,_)=>Recalculate();
    }
    private VitricaGlassContact[] ReadGlasses()=>!VitricaSelected?[]:glassEditors.Select(e=>new VitricaGlassContact(Number(e.Time,"유리화 접촉 시각"),Integer(e.Target,"유리화 중심 대상"))).ToArray();
    private VitricaSlamContact[] ReadGlassSlams()=>!VitricaSelected?[]:glassSlamEditors.Select(e=>new VitricaSlamContact(Number(e.Time,"슬램 접촉 시각"),Integer(e.Target,"슬램 대상"),e.Heavy.IsChecked==true,Integer(e.Slam,"슬램 번호"))).ToArray();
    private void ApplyGlass(EncounterSetup? setup){glassEditors.Clear();glassRows.Children.Clear();glassSlamEditors.Clear();glassSlamRows.Children.Clear();glassOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.VitricaGlasses??[])AddGlass(c);foreach(var c in setup?.VitricaSlams??[])AddGlassSlam(c);}
}
