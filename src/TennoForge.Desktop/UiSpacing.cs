using System.Windows;
using System.Windows.Controls;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    // Form actions use an explicit rhythm; compact mod slots/toolbars keep their own geometry.
    private static StackPanel SpacedStack(double gap,params UIElement[] children)
    {
        var panel=new StackPanel();
        for(var i=0;i<children.Length;i++)
        {
            var child=(FrameworkElement)children[i];var margin=child.Margin;
            child.Margin=new(margin.Left,margin.Top,margin.Right,i==children.Length-1?margin.Bottom:Math.Max(gap,margin.Bottom));
            panel.Children.Add(child);
        }
        return panel;
    }
}
