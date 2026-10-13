using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record PoolEditor(TextBox Created,TextBox Enter,TextBox Exit,TextBox Target,TextBox Blind);
    private readonly List<PoolEditor> poolEditors=[];
    private readonly StackPanel poolRows=new();
    private readonly CheckBox poolsOnly=new(){Content="웅덩이만 계산",IsChecked=false};
    private Expander? caustPoolPanel;
    private bool CaustacystSelected=>weapon.SelectedItem is Weapon w&&CaustacystMechanic.Applies(w.Id);
    private FrameworkElement CreateCaustacystPanel()
    {
        var note=T("웅덩이에 들어간 즉시 피해를 받고, 머무는 동안 1초마다 반복합니다. 생성 후 4초까지는 위키 본문과 표가 일치합니다. 실명 시간은 원문에 없어 직접 지정하는 시나리오입니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        poolsOnly.Checked+=(_,_)=>Recalculate();poolsOnly.Unchecked+=(_,_)=>Recalculate();
        caustPoolPanel=new(){Header="카우스타시스트 · 웅덩이 체류",IsExpanded=false,Visibility=Visibility.Collapsed,Content=Stack(note,poolsOnly,poolRows,Button("＋ 체류 구간",()=>{AddPoolWindow(new(0,4));Recalculate();}))};return caustPoolPanel;
    }
    private void AddPoolWindow(CaustacystPoolWindow value)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var e=new PoolEditor(Input(value.Created),Input(value.Enter),Input(value.Exit),Input(value.Target),Input(value.BlindDuration));
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields)
        {
            var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;
        }
        row.Children.Add(Line(Field("생성 (초)",e.Created),Field("진입 (초)",e.Enter),Field("이탈 (초)",e.Exit)));
        var remove=Button("삭제",()=>{poolEditors.Remove(e);poolRows.Children.Remove(row);Recalculate();});remove.VerticalAlignment=VerticalAlignment.Bottom;remove.Height=36;
        row.Children.Add(Line(Field("대상 번호",e.Target),Field("실명 시간 가정 (초)",e.Blind),remove));
        poolEditors.Add(e);poolRows.Children.Add(row);foreach(var box in new[]{e.Created,e.Enter,e.Exit,e.Target,e.Blind})box.TextChanged+=(_,_)=>Recalculate();
    }
    private CaustacystPoolWindow[] ReadPoolWindows()=>!CaustacystSelected?[]:poolEditors.Select(e=>new CaustacystPoolWindow(Number(e.Enter,"웅덩이 진입 시각"),Number(e.Exit,"웅덩이 이탈 시각"),Number(e.Created,"웅덩이 생성 시각"),Integer(e.Target,"웅덩이 대상 번호"),string.IsNullOrWhiteSpace(e.Blind.Text)?null:Number(e.Blind,"실명 지속시간 가정"))).ToArray();
    private void ApplyPoolWindows(EncounterSetup? setup)
    {poolEditors.Clear();poolRows.Children.Clear();poolsOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var w in setup?.CaustacystPools??[])AddPoolWindow(w);}
}
