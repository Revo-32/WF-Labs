using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    internal void OpenFeatures()
    {
        if(featureWindow is not null){featureWindow.Activate();return;}
        FeatureTabs.Clear();var root=BuildFeatureRoot();
        featureWindow=new Window{Title="WF Labs · 오버레이·알림",Width=1100,Height=780,MinWidth=960,MinHeight=650,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
        featureWindow.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){featureWindow?.Close();e.Handled=true;}};
        WindowFrame.Attach(featureWindow,root);System.Windows.Shell.WindowChrome.GetWindowChrome(featureWindow).CaptionHeight=0;
        var refresh=refreshFeatureContents!;DataChanged+=refresh;
        featureWindow.Closed+=(_,_)=>{DataChanged-=refresh;refreshFeatureContents=null;refreshFeatureFilters=null;refreshFeaturePreview=null;overlay?.FinishEdit();featureWindow=null;dialogOverlaySwitch=null;FeatureTabs.Clear();TickFeatures();};featureWindow.Show();
    }
    private void BeginOverlayEdit()
    {
        overlay??=new(Preferences,p=>{Preferences=p;SavePreferences();});overlay.Update(TimeZoneInfo.ConvertTime(Clock(),WorldScheduleEngine.Korea).ToString("HH:mm:ss"),OverlayData(Clock()),Preferences);
        Int32Rect bounds;try{bounds=RelicCapture.ClientBounds(RelicCapture.GameWindow());}catch(InvalidOperationException){var a=SystemParameters.WorkArea;var d=VisualTreeHelper.GetDpi(featureWindow??Window.GetWindow(this));bounds=new((int)(a.X*d.DpiScaleX),(int)(a.Y*d.DpiScaleY),(int)(a.Width*d.DpiScaleX),(int)(a.Height*d.DpiScaleY));}
        overlay.BeginEdit(bounds);
    }
    private FrameworkElement OverlaySlider(string label,double min,double max,double initial,Action<double> apply,Func<double,string> format)
    {
        var body=new StackPanel{Margin=new(0,6,0,10)};var row=new DockPanel();var value=Text(format(initial),12,"MutedBrush");DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);row.Children.Add(Text(label,13));body.Children.Add(row);
        var slider=new Slider{Minimum=min,Maximum=max,Value=initial,TickFrequency=5,IsSnapToTickEnabled=true,MinHeight=24};slider.SetResourceReference(StyleProperty,"ScheduleSlider");AutomationProperties.SetName(slider,label);slider.ValueChanged+=(_,_)=>{apply(slider.Value);value.Text=format(slider.Value);SavePreferences();TickFeatures();};body.Children.Add(slider);return body;
    }
}
