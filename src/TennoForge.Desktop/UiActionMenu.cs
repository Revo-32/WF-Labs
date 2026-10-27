using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TennoForge.Desktop;

// Native ContextMenu supplies directional navigation, invocation, dismissal and focus handling.
internal sealed class UiActionMenu : Button
{
    internal ContextMenu Menu {get;}=new();
    internal UiActionMenu(string label)
    {
        Style=(Style)Application.Current.FindResource(typeof(Button));
        var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
        var chevron=new Path{Data=Geometry.Parse("M 0 0 L 4 4 L 8 0"),Width=8,Height=4,Stretch=Stretch.Fill,StrokeThickness=1.5,Margin=new(10,0,0,0),VerticalAlignment=VerticalAlignment.Center};
        chevron.SetResourceReference(Shape.StrokeProperty,"MutedBrush");content.Children.Add(chevron);Content=content;
        Padding=new(12,7,12,7);Margin=new(0,0,8,0);MinHeight=36;ContextMenu=Menu;
        AutomationProperties.SetName(this,label);Click+=(_,_)=>Open();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Down||(e.Key==Key.System&&e.SystemKey==Key.Down)){Open();e.Handled=true;}};
    }
    internal void Open(){Menu.PlacementTarget=this;Menu.Placement=PlacementMode.Bottom;Menu.IsOpen=true;}
    internal MenuItem Action(string label,Action action,string shortcut="")
    {var item=new MenuItem{Header=label,InputGestureText=shortcut};AutomationProperties.SetName(item,label);item.Click+=(_,_)=>action();Menu.Items.Add(item);return item;}
}
