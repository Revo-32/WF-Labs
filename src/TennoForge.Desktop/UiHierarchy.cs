using System.Windows;
using System.Windows.Controls;

namespace TennoForge.Desktop;

// Shared task hierarchy. Secondary detail remains available without competing with the next action.
internal static class UiHierarchy
{
    internal static void Primary(Button button)
    {
        button.SetResourceReference(Control.BackgroundProperty,"AccentSurfaceBrush");
        button.SetResourceReference(Control.BorderBrushProperty,"AccentBrush");
        button.SetResourceReference(Control.ForegroundProperty,"AccentBrush");
        button.FontWeight=FontWeights.SemiBold;
    }

    internal static void SetPrimary(Button button,bool value)
    {
        if(value){Primary(button);return;}
        button.ClearValue(Control.BackgroundProperty);button.ClearValue(Control.BorderBrushProperty);
        button.ClearValue(Control.ForegroundProperty);button.ClearValue(Control.FontWeightProperty);
    }

    internal static FrameworkElement Heading(string title)
    {
        var text=RivenPresentation.Text(title,17);text.Margin=new(0);
        var rule=new Border{Child=text,BorderThickness=new(0,1,0,0),Padding=new(0,14,0,0),Margin=new(0,10,0,12)};
        rule.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");return rule;
    }

    internal static FrameworkElement RivenStart(Action focusInput,Action openReference)
    {
        var enter=new Button{Content="옵션 선택",Padding=new(14,8,14,8),Margin=new(0,0,8,8)};
        Primary(enter);enter.Click+=(_,_)=>focusInput();
        var browse=new Button{Content="옵션 도감",Padding=new(14,8,14,8),Margin=new(0,0,0,8)};
        browse.Click+=(_,_)=>openReference();
        var actions=new WrapPanel{Margin=new(0,10,0,0)};actions.Children.Add(enter);actions.Children.Add(browse);
        return RivenPresentation.Stack(RivenPresentation.Text("분석할 리벤이 없습니다",20),
            RivenPresentation.Text("옵션과 수치를 입력하면 최소·최대 범위와 현재 위치가 표시됩니다.",15,"MutedBrush"),actions);
    }
}
