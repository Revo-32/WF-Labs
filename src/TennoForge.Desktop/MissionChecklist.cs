using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TennoForge.Desktop;

internal sealed class MissionChecklist : Button
{
    internal readonly Dictionary<string,CheckBox> Choices=new();
    internal readonly Popup Menu;
    internal event Action? Changed;
    private bool updating;
    internal string[] Excluded=>Choices.Where(p=>p.Value.IsChecked!=true).Select(p=>p.Key).ToArray();
    internal MissionChecklist(IEnumerable<string> names,IEnumerable<string> excluded)
    {
        MinWidth=175;HorizontalContentAlignment=HorizontalAlignment.Left;Padding=new(12,4,12,4);
        SetResourceReference(StyleProperty,typeof(Button));
        AutomationProperties.SetName(this,"중재 미션 유형 선택");
        var body=new StackPanel();var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,0,12)};
        foreach(var (title,enabled) in new[]{("모두 선택",true),("모두 해제",false)})
        {var b=new Button{Content=title,Margin=new(0,0,8,0),Padding=new(12,6,12,6)};b.Click+=(_,_)=>SetExcluded(enabled?[]:Choices.Keys);actions.Children.Add(b);}
        body.Children.Add(actions);var grid=new UniformGrid{Columns=2};
        foreach(var name in names.Order())
        {
            var c=new CheckBox{Content=name,IsChecked=!excluded.Contains(name),Margin=new(6,8,18,8),MinHeight=28};c.SetResourceReference(StyleProperty,"ChecklistItem");AutomationProperties.SetName(c,"중재 "+name+" 표시");Choices[name]=c;grid.Children.Add(c);
            c.Checked+=(_,_)=>Update();c.Unchecked+=(_,_)=>Update();
        }
        body.Children.Add(grid);var frame=new Border{Child=body,Padding=new(16),BorderThickness=new(1)};frame.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");frame.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");
        Menu=new Popup{PlacementTarget=this,Placement=PlacementMode.Bottom,StaysOpen=false,Child=frame,AllowsTransparency=false};
        Click+=(_,_)=>Menu.IsOpen=!Menu.IsOpen;Unloaded+=(_,_)=>Menu.IsOpen=false;
        ToolTip="체크를 해제한 미션은 S티어라도 목록과 다음 S티어 타이머에서 제외합니다.";Update();
    }
    internal void SetExcluded(IEnumerable<string> names)
    {var set=names.ToHashSet();updating=true;foreach(var (name,c) in Choices)c.IsChecked=!set.Contains(name);updating=false;Update();}
    private void Update(){Content=Excluded.Length==0?"모든 유형  ▾":Excluded.Length==Choices.Count?"선택한 유형 없음  ▾":$"{Choices.Count-Excluded.Length} / {Choices.Count} 유형  ▾";if(!updating)Changed?.Invoke();}
}
