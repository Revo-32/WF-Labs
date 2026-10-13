using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private sealed record DebuffRow(ComboBox Kind,ComboBox Rank,TextBox Strength,TextBox Remaining,ComboBox Group,CheckBox Allied);
    private sealed record DebuffEditorState(Expander View,StackPanel Rows,List<DebuffRow> Editors);
    private DebuffEditorState? primaryDebuffs;
    private DebuffEditorState CreateDebuffEditor(IEnumerable<TargetDebuff>? initial=null)
    {
        var rows=new StackPanel();var editors=new List<DebuffRow>();
        var state=new DebuffEditorState(new Expander{Header="대상 어빌리티 효과"},rows,editors);
        state.View.Content=Stack(rows,Button("＋ 대상 효과",()=>{AddDebuffRow(state,new("Petrify"));Recalculate();}));
        foreach(var d in initial??[])AddDebuffRow(state,d);
        return state;
    }
    private void AddDebuffRow(DebuffEditorState editor,TargetDebuff seed)
    {
        var kind=new ComboBox{ItemsSource=TargetDebuffs.Definitions,DisplayMemberPath="Name",SelectedValuePath="Code",SelectedValue=seed.Code};
        var rank=new ComboBox{ItemsSource=new[]{0,1,2,3},SelectedItem=seed.Rank,MinWidth=55};
        var strength=new TextBox{Text=(seed.Strength*100).ToString(CultureInfo.InvariantCulture)};
        var remaining=new TextBox{Text=seed.Remaining.ToString(CultureInfo.InvariantCulture),ToolTip="교전 시작에 실제로 남아 있는 시간입니다. 전파 지연·시전 시각을 위키 값으로 추정하지 않습니다."};
        var group=new ComboBox{ItemsSource=Enumerable.Range(1,32).ToArray(),SelectedItem=seed.Group};
        var allied=new CheckBox{Content="다른 워프레임이 건 석화",IsChecked=seed.Allied};var entry=new DebuffRow(kind,rank,strength,remaining,group,allied);var row=new StackPanel{Margin=new(0,6,0,12)};
        var top=new DockPanel();var remove=Button("×",()=>{editor.Editors.Remove(entry);editor.Rows.Children.Remove(row);Recalculate();});remove.ToolTip="대상 효과 해제";remove.Width=30;remove.Padding=new(0);remove.Margin=new(6,0,0,0);DockPanel.SetDock(remove,Dock.Right);top.Children.Add(remove);top.Children.Add(kind);row.Children.Add(top);
        var strengthField=(FrameworkElement)Field("시전자 위력 (%)",strength);var rankField=(FrameworkElement)Field("어빌리티 랭크",rank);
        row.Children.Add(Columns(rankField,strengthField));row.Children.Add(Field("시작 잔여시간 (초)",remaining));
        var groupField=(FrameworkElement)Field("돔 번호 · 같은 돔끼리 전파",group);row.Children.Add(groupField);row.Children.Add(allied);
        editor.Editors.Add(entry);editor.Rows.Children.Add(row);
        void Refresh()
        {
            var d=kind.SelectedItem as TargetDebuffDefinition;strengthField.Visibility=rankField.Visibility=d?.StrengthScaled==true?Visibility.Visible:Visibility.Collapsed;
            groupField.Visibility=d?.Code=="Strangledome"?Visibility.Visible:Visibility.Collapsed;allied.Visibility=d?.Code=="Petrify"?Visibility.Visible:Visibility.Collapsed;
            kind.ToolTip=d is null?null:d.Name+" · "+d.Source+(d.Code=="Ensnare"?"\n윕클로 피해×2. 다른 근접무기 피해에는 이 배수가 적용되지 않습니다.":d.Code=="Strangledome"?"\n윕클로는 대상 취약도×2에서 제외됩니다. 돔 자체 피해는 근접 피해에 합산하지 않습니다.":"\n대상이 받는 피해에 적용. 프레셔/컨오와 별도 계산합니다.");
        }
        void Changed(){if(!changingMelee&&!updatingScene&&!initializing)Recalculate();}
        allied.Checked+=(_,_)=>Changed();allied.Unchecked+=(_,_)=>Changed();Refresh();kind.SelectionChanged+=(_,_)=>{Refresh();Changed();};rank.SelectionChanged+=(_,_)=>Changed();strength.TextChanged+=(_,_)=>Changed();remaining.TextChanged+=(_,_)=>Changed();group.SelectionChanged+=(_,_)=>Changed();
    }
    private TargetDebuff[] ReadDebuffs(DebuffEditorState? state)=>state?.Editors.Select(e=>new TargetDebuff((string)e.Kind.SelectedValue,(int)e.Rank.SelectedItem,Number(e.Strength,"효과 시전자 위력")/100,Number(e.Remaining,"효과 시작 잔여시간"),(int)e.Group.SelectedItem,e.Allied.IsChecked==true)).ToArray()??[];
    private void ApplyPrimaryDebuffs(TargetDebuff[]? values)
    {if(primaryDebuffs is null)return;primaryDebuffs.Editors.Clear();primaryDebuffs.Rows.Children.Clear();foreach(var d in values??[])AddDebuffRow(primaryDebuffs,d);}
}
