using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TennoForge.Desktop;

// One aligned control for the switch, actual detector state, and its explanation.
internal sealed class RecognitionStatusBar : Border
{
    private static readonly DependencyProperty MessageProperty=DependencyProperty.Register("Message",typeof(string),typeof(RecognitionStatusBar),new PropertyMetadata("",(d,e)=>((RecognitionStatusBar)d).Refresh()));
    private readonly CheckBox toggle;
    private readonly TextBlock heading=new(){FontSize=16,FontWeight=FontWeights.SemiBold};
    private readonly Ellipse dot=new(){Width=8,Height=8,Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};
    internal readonly Button SettingsButton=new(){Content="인식 설정",Margin=new(16,0,0,0),Padding=new(10,5,10,5),MinHeight=32,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed};
    private Action? settingsAction;
    private bool busy;
    internal string State=>heading.Text;
    internal RecognitionStatusBar(string name,CheckBox automatic,TextBlock detail)
    {
        toggle=automatic;Padding=new(14,10,14,10);Margin=new(0,8,0,12);BorderThickness=new(3,0,0,0);
        SetResourceReference(BackgroundProperty,"SurfaceBrush");
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Child=grid;
        grid.Children.Add(dot);var text=new StackPanel{Margin=new(0,0,24,0)};Grid.SetColumn(text,1);grid.Children.Add(text);
        text.Children.Add(heading);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new(0,5,0,0);text.Children.Add(detail);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(actions,2);grid.Children.Add(actions);
        toggle.Content="자동인식";toggle.Margin=new(0);toggle.VerticalAlignment=VerticalAlignment.Center;actions.Children.Add(toggle);actions.Children.Add(SettingsButton);
        AutomationProperties.SetName(SettingsButton,name+" 인식 설정");SettingsButton.Click+=(_,_)=>settingsAction?.Invoke();
        AutomationProperties.SetName(toggle,name+" 자동인식 켜기 / 끄기");AutomationProperties.SetLiveSetting(heading,AutomationLiveSetting.Polite);
        SetBinding(MessageProperty,new Binding(nameof(TextBlock.Text)){Source=detail});
        toggle.Checked+=(_,_)=>Refresh();toggle.Unchecked+=(_,_)=>Refresh();Refresh();
    }
    internal void SetBusy(bool value){busy=value;Refresh();}
    internal void SetSettingsAction(Action action){settingsAction=action;SettingsButton.Visibility=Visibility.Visible;}
    private void Refresh()
    {
        string message=(string)GetValue(MessageProperty);bool enabled=toggle.IsChecked==true;
        bool warning=enabled&&(message.Contains("실패")||message.Contains("확인 필요")||message.Contains("충돌")||message.Contains("오류"));
        bool reading=busy||(enabled&&(message.Contains("읽고")||message.Contains("다시 읽")||message.Contains("읽는 중")));
        string state=reading?"화면 읽는 중":!enabled?"자동인식 꺼짐":warning?"자동인식 · 확인 필요":message.Contains("완료")?"자동인식 켜짐 · 인식 완료":"자동인식 켜짐 · 화면 대기";
        string brush=warning?"ErrorBrush":enabled||reading?"AccentBrush":"MutedBrush";
        heading.SetResourceReference(TextBlock.ForegroundProperty,brush);dot.SetResourceReference(Shape.FillProperty,brush);SetResourceReference(BorderBrushProperty,brush);
        if(heading.Text!=state){heading.Text=state;if(reading)UiMotion.RevealAction(dot);}
    }
}
