using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Data;

namespace TennoForge.Desktop;

// Group spacing belongs to the always-visible container, never to optional actions.
internal sealed class RivenOptionInput : StackPanel
{
    internal int Index {get;}
    internal ComboBox Attribute {get;}
    internal TextBox Value {get;}
    internal ComboBox Sign {get;}
    internal TextBlock ValueHint {get;}
    internal RivenOptionInput(int index,ComboBox attribute,TextBox value,ComboBox sign,params FrameworkElement[] details)
    {
        Index=index;Attribute=attribute;Value=value;Sign=sign;Margin=new(0,0,0,18);
        var label=RivenPresentation.Text($"옵션 {index+1}",14,"MutedBrush");label.Margin=new(0,0,0,6);Children.Add(label);
        attribute.Margin=new(0,0,0,6);Children.Add(attribute);
        value.Margin=new(0,0,8,0);sign.Margin=new(0);value.MinHeight=sign.MinHeight=38;
        var input=new Grid();input.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});input.ColumnDefinitions.Add(new(){Width=new(112)});
        var valueHost=new Grid();valueHost.Children.Add(value);
        var hint=ValueHint=RivenPresentation.Text("수치 입력",14,"MutedBrush");hint.Margin=new(11,0,16,0);hint.VerticalAlignment=VerticalAlignment.Center;hint.IsHitTestVisible=false;
        hint.SetBinding(TextBlock.FontSizeProperty,new Binding(nameof(value.FontSize)){Source=value});
        valueHost.Children.Add(hint);void RefreshHint()=>hint.Visibility=string.IsNullOrEmpty(value.Text)&&!value.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Collapsed;
        value.TextChanged+=(_,_)=>RefreshHint();value.IsKeyboardFocusWithinChanged+=(_,_)=>RefreshHint();RefreshHint();
        input.Children.Add(valueHost);Grid.SetColumn(sign,1);input.Children.Add(sign);Children.Add(input);
        foreach(var detail in details){detail.Margin=new(0,6,0,0);Children.Add(detail);}
        AutomationProperties.SetName(attribute,$"옵션 {index+1} 종류");AutomationProperties.SetName(value,$"옵션 {index+1} 수치");AutomationProperties.SetName(sign,$"옵션 {index+1} 양옵·음옵");
    }
    internal static ComboBox SignFor(CheckBox negative)
    {
        var sign=new ComboBox{ItemsSource=new[]{"양옵 (+)","음옵 (−)"},SelectedIndex=negative.IsChecked==true?1:0};
        sign.SelectionChanged+=(_,_)=>negative.IsChecked=sign.SelectedIndex==1;
        negative.Checked+=(_,_)=>sign.SelectedIndex=1;negative.Unchecked+=(_,_)=>sign.SelectedIndex=0;
        sign.SetBinding(IsEnabledProperty,new Binding(nameof(IsEnabled)){Source=negative});return sign;
    }
}
