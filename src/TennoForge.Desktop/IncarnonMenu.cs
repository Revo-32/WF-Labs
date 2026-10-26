using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TennoForge.Desktop;

// Same entry point beside the weapon name for melee, primary and secondary.
internal sealed class IncarnonMenu : Grid
{
    internal readonly Button Entry=new(){Content="인카논 ▾",MinHeight=32,Padding=new(10,3,10,3),Margin=new(4,0,0,0)};
    internal readonly Popup Popup=new(){Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true};
    private readonly ContentControl editor=new();
    internal IncarnonMenu()
    {
        Visibility=Visibility.Collapsed;Children.Add(Entry);Children.Add(Popup);Popup.PlacementTarget=Entry;
        var close=new Button{Content="완료",HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,10,0,0)};
        var panel=new Grid();panel.RowDefinitions.Add(new(){Height=GridLength.Auto});panel.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});panel.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Grid.SetRow(close,2);panel.Children.Add(close);
        var title=new TextBlock{Text="인카논 설정",FontSize=18,Margin=new(0,0,0,12)};panel.Children.Add(title);
        var scroll=new ScrollViewer{Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,8,0)};Grid.SetRow(scroll,1);panel.Children.Add(scroll);
        var border=new Border{Width=400,Height=540,Padding=new(16),BorderThickness=new(1),CornerRadius=new(5),Margin=new(0,4,0,0),Child=panel};
        border.SetResourceReference(Border.BackgroundProperty,"PopupBrush");border.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Popup.Child=border;
        Entry.Click+=(_,_)=>Popup.IsOpen=!Popup.IsOpen;close.Click+=(_,_)=>{Popup.IsOpen=false;Entry.Focus();};
        border.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!LayoutDiagnostics.Tree(border).OfType<ComboBox>().Any(c=>c.IsDropDownOpen)){Popup.IsOpen=false;Entry.Focus();e.Handled=true;}};
        Unloaded+=(_,_)=>Popup.IsOpen=false;
        Popup.Opened+=(_,_)=>{editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));scroll.ScrollToTop();};
        Entry.ToolTip="일반 / 인카논 형태와 진화 선택";
    }
    internal void SetEditor(UIElement content)=>editor.Content=content;
    internal void SetState(bool supported,bool transformed)
    {
        Visibility=supported?Visibility.Visible:Visibility.Collapsed;
        Entry.Content=transformed?"인카논 · 변신 ▾":"인카논 ▾";
        if(!supported)Popup.IsOpen=false;
    }
}
