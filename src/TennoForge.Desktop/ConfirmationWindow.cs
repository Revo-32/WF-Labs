using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TennoForge.Desktop;
internal sealed class ConfirmationWindow : Window
{
    internal ConfirmationWindow(string title,string message,string accept)
    {
        Title="WF Labs · "+title;Width=520;Height=320;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(FontFamilyProperty,"AppFont");
        var root=new Grid{Margin=new(24)};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        root.Children.Add(new TextBlock{Text=title,FontSize=22,Margin=new(0,4,0,20)});
        var scroll=new ScrollViewer{Content=new TextBlock{Text=message,LineHeight=24,TextWrapping=TextWrapping.Wrap},VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new(0,0,0,20)};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=new Button{Content="취소",IsCancel=true,MinWidth=96,Margin=new(0,0,8,0)};
        var confirm=new Button{Content=accept,IsDefault=true,MinWidth=120};confirm.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");
        cancel.Click+=(_,_)=>DialogResult=false;confirm.Click+=(_,_)=>DialogResult=true;actions.Children.Add(cancel);actions.Children.Add(confirm);Grid.SetRow(actions,2);root.Children.Add(actions);
        WindowFrame.Attach(this,root);ResizeMode=ResizeMode.NoResize;
    }
    internal static bool Ask(Window owner,string title,string message,string accept)=>new ConfirmationWindow(title,message,accept){Owner=owner}.ShowDialog()==true;
}
