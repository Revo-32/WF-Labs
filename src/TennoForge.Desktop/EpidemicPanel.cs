using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record EpidemicEditor(TextBox Time,TextBox Target,ComboBox Phase);
    private readonly List<EpidemicEditor> epidemicEditors=[];
    private readonly StackPanel epidemicRows=new();
    private readonly CheckBox epidemicOnly=new(){Content="파동·폭발만 계산"};
    private Expander? epidemicPanel;
    private bool EpidemicEquipped=>selectedExodia is {Active:true} slot&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code=="Epidemic";
    private FrameworkElement CreateEpidemicPanel()
    {
        epidemicOnly.Checked+=(_,_)=>Recalculate();epidemicOnly.Unchecked+=(_,_)=>Recalculate();
        var note=T("파동과 끝 폭발이 맞는 시각·대상을 지정합니다. 위키에 이동 속도와 정확한 범위 경계가 없어 자동 명중은 추정하지 않습니다. 끝 폭발은 중심 피해·기본 배율 약30% 기준입니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        epidemicPanel=new(){Header="에피데믹 · 파동과 끝 폭발",IsExpanded=true,Visibility=Visibility.Collapsed,
            Content=Stack(note,epidemicOnly,epidemicRows,Button("＋ 명중",()=>{AddEpidemicImpact(new(0));Recalculate();}))};return epidemicPanel;
    }
    private void AddEpidemicImpact(EpidemicImpact value)
    {
        TextBox Input(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
        var editor=new EpidemicEditor(Input(value.Time),Input(value.Target),new(){ItemsSource=new[]{"파동 · 폭발 피해10","끝 폭발 · 바이러스 약30%"},SelectedIndex=(int)value.Phase});
        var row=new StackPanel{Margin=new(0,8,0,10)};var header=new DockPanel();
        var remove=Button("×",()=>{epidemicEditors.Remove(editor);epidemicRows.Children.Remove(row);Recalculate();});remove.Width=30;remove.Padding=new(4);remove.Margin=new(6,0,0,0);remove.ToolTip="명중 삭제";DockPanel.SetDock(remove,Dock.Right);header.Children.Add(remove);header.Children.Add(editor.Phase);row.Children.Add(header);
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());var left=(FrameworkElement)Field("명중 시각 (초)",editor.Time);left.Margin=new(0,0,6,0);grid.Children.Add(left);var right=(FrameworkElement)Field("대상 번호",editor.Target);Grid.SetColumn(right,1);grid.Children.Add(right);row.Children.Add(grid);
        editor.Target.ToolTip="1은 기본 대상, 2부터는 함께 맞는 적입니다. 지정하지 않은 적에게 자동 전파하지 않습니다.";
        epidemicEditors.Add(editor);epidemicRows.Children.Add(row);
        editor.Time.TextChanged+=(_,_)=>Recalculate();editor.Target.TextChanged+=(_,_)=>Recalculate();editor.Phase.SelectionChanged+=(_,_)=>Recalculate();
    }
    private EpidemicImpact[] ReadEpidemic()=>!EpidemicEquipped?[]:epidemicEditors.Select(e=>new EpidemicImpact(Number(e.Time,"에피데믹 명중 시각"),(EpidemicPhase)e.Phase.SelectedIndex,Integer(e.Target,"에피데믹 대상 번호"))).ToArray();
    private void ApplyEpidemic(EncounterSetup? setup)
    {epidemicRows.Children.Clear();epidemicEditors.Clear();foreach(var impact in setup?.Epidemic??[])AddEpidemicImpact(impact);epidemicOnly.IsChecked=setup?.ScriptedAttacksOnly==true;}
}
