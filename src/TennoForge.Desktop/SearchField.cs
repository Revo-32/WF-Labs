using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TennoForge.Desktop;

// A hint is decoration, never editable text. Remove it synchronously on focus:
// fading it out would still leave a caret over the hint during the transition.
internal sealed class SearchField : Grid
{
    internal TextBox Input {get;}
    internal TextBlock Hint {get;}
    internal Button ClearButton {get;}
    internal SearchField(TextBox input,string hint)
    {
        Input=input;Margin=input.Margin;input.Margin=new(0);ClipToBounds=true;
        var padding=input.Padding;input.Padding=new(32,padding.Top,34,padding.Bottom);
        input.VerticalContentAlignment=VerticalAlignment.Center;
        if(string.IsNullOrEmpty(AutomationProperties.GetName(input)))AutomationProperties.SetName(input,hint);
        Children.Add(input);
        var icon=new Path{Data=Geometry.Parse("M 9,5 A 4,4 0 1 1 1,5 A 4,4 0 1 1 9,5 M 8,8 L 12,12"),Width=13,Height=13,Stretch=Stretch.Uniform,StrokeThickness=1.4,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new(12,0,0,0),IsHitTestVisible=false};
        icon.SetResourceReference(Shape.StrokeProperty,"MutedBrush");Children.Add(icon);
        Hint=new TextBlock{Text=hint,Margin=new(33,0,34,0),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,IsHitTestVisible=false};
        Hint.SetBinding(TextBlock.FontSizeProperty,new System.Windows.Data.Binding(nameof(input.FontSize)){Source=input});
        Hint.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");Children.Add(Hint);
        var cross=new Path{Data=Geometry.Parse("M 1,1 L 9,9 M 9,1 L 1,9"),Width=10,Height=10,StrokeThickness=1.4,Stretch=Stretch.Uniform,IsHitTestVisible=false};cross.SetResourceReference(Shape.StrokeProperty,"MutedBrush");
        ClearButton=new Button{Content=cross,Width=26,Height=26,Padding=new(0),Margin=new(0,0,4,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,BorderThickness=new(0),Background=Brushes.Transparent,ToolTip="검색어 지우기",Visibility=Visibility.Collapsed};
        AutomationProperties.SetName(ClearButton,hint+" · 검색어 지우기");SetZIndex(ClearButton,1);Children.Add(ClearButton);
        ClearButton.Click+=(_,e)=>{input.Clear();input.Focus();e.Handled=true;};
        input.TextChanged+=(_,_)=>Refresh();
        input.IsKeyboardFocusWithinChanged+=(_,_)=>Refresh();
        input.IsEnabledChanged+=(_,_)=>{ClearButton.IsEnabled=input.IsEnabled;};
        input.PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape&&input.Text.Length>0&&!composing)
            {input.Clear();e.Handled=true;}
        };
        TextCompositionManager.AddPreviewTextInputStartHandler(input,(_,_)=>composing=true);
        input.AddHandler(TextCompositionManager.TextInputEvent,new TextCompositionEventHandler((_,_)=>composing=false),true);
        input.LostKeyboardFocus+=(_,_)=>composing=false;
        Loaded+=(_,_)=>Refresh();Refresh();
    }
    private bool composing;
    internal static bool HandlesEscape(IInputElement? focused)=>focused is TextBox input&&input.Parent is SearchField field&&(input.Text.Length>0||field.composing);
    private void Refresh()
    {
        var hasText=Input.Text.Length>0;
        Hint.Visibility=!hasText&&!Input.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Collapsed;
        var reveal=hasText&&ClearButton.Visibility!=Visibility.Visible;
        ClearButton.Visibility=hasText?Visibility.Visible:Visibility.Collapsed;
        if(reveal&&Input.IsKeyboardFocusWithin)UiMotion.RevealAction(ClearButton);
        if(!hasText){ClearButton.BeginAnimation(OpacityProperty,null);ClearButton.Opacity=1;}
    }
}
