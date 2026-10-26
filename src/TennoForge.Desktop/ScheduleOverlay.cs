using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class ScheduleOverlay : Window
{
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetStyle(IntPtr hwnd,int index,IntPtr value);
    private readonly OverlayPositioner positioner=new();
    private readonly ScheduleOverlayView view=new();
    private readonly Border frame;
    private readonly Border caption;
    private readonly LayeredOverlayFrame nativeFrame;
    private readonly CheckBox steelOnly=new(){Content="강철의 길만",FontSize=12,Margin=new(8,0,8,0),VerticalAlignment=VerticalAlignment.Center};
    private bool syncingFilter;
    private readonly Action<SchedulePreferences> save;
    private SchedulePreferences preferences;
    private Int32Rect? reference;
    internal bool Editing {get;private set;}
    internal bool Dismissed {get;private set;}
    internal string DisplayText=>view.Model?.PlainText??"";
    internal ScheduleOverlay(SchedulePreferences value,Action<SchedulePreferences> onSave)
    {
        preferences=value.Sanitize();save=onSave;Title="오버레이 편집";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowActivated=false;ShowInTaskbar=false;Topmost=true;Focusable=false;UseLayoutRounding=true;
        MinWidth=320;MinHeight=260;MaxWidth=1100;MaxHeight=1100;Width=preferences.Width;Height=preferences.Height;Left=-30000;Top=-30000;
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var title=new TextBlock{Text="오버레이 편집",FontSize=13,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new(12,0,0,0)};
        var titleBar=new DockPanel();var done=new Button{Content="완료",Padding=new(12,3,12,3),Margin=new(0,3,6,3),FontSize=12};done.Click+=(_,_)=>FinishEdit();System.Windows.Automation.AutomationProperties.SetName(done,"오버레이 편집 완료");DockPanel.SetDock(done,Dock.Right);titleBar.Children.Add(done);titleBar.Children.Add(title);
        steelOnly.Foreground=Brushes.White;steelOnly.SetResourceReference(StyleProperty,"ChecklistItem");System.Windows.Automation.AutomationProperties.SetName(steelOnly,"오버레이 강철의 길만");DockPanel.SetDock(steelOnly,Dock.Right);titleBar.Children.Insert(1,steelOnly);
        void Filter(){if(syncingFilter)return;preferences=preferences with{FissureView=steelOnly.IsChecked==true?1:0};save(preferences);}steelOnly.Checked+=(_,_)=>Filter();steelOnly.Unchecked+=(_,_)=>Filter();
        caption=new Border{Child=titleBar,Height=34,Background=new SolidColorBrush(Color.FromRgb(36,39,43)),Visibility=Visibility.Collapsed};layout.Children.Add(caption);Grid.SetRow(view,1);layout.Children.Add(view);
        frame=new Border{Child=layout,CornerRadius=new(3),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(100,145,149,154))};Content=frame;ApplyAppearance();
        nativeFrame=new(this,()=>Editing);SourceInitialized+=(_,_)=>SetPassive(true);Closed+=(_,_)=>Dismissed=true;
        PreviewKeyDown+=(_,e)=>{if(Editing&&e.Key==System.Windows.Input.Key.Escape){FinishEdit();e.Handled=true;}};
    }
    private void ApplyAppearance()
    {var color=Color.FromArgb((byte)Math.Round(preferences.BackgroundOpacity*255),15,17,19);if(frame.Background is not SolidColorBrush b||b.Color!=color)frame.Background=new SolidColorBrush(color);}
    private void SetPassive(bool pass)
    {
        var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;
        long style=RelicOverlay.GetStyle(hwnd,-20).ToInt64()|0x08000000L|0x80L;
        SetStyle(hwnd,-20,new(pass?style|0x20L:style&~0x20L));IsHitTestVisible=!pass;
    }
    internal void Update(string time,ScheduleOverlayData data,SchedulePreferences value)
    {
        // Appearance changes remain live while the user is positioning the window.
        preferences=value.Sanitize();syncingFilter=true;steelOnly.IsChecked=preferences.FissureView==1;syncingFilter=false;ApplyAppearance();view.Update(time,data,preferences);
    }
    internal void PresentAt(Int32Rect bounds)
    {
        if(Dismissed||Editing)return;reference=bounds;if(!IsVisible)Show();nativeFrame.EnsureTransparent();
        var dpi=VisualTreeHelper.GetDpi(this);int width=Math.Min(Math.Max(100,bounds.Width),(int)Math.Round(preferences.Width*dpi.DpiScaleX));int height=Math.Min(Math.Max(100,bounds.Height),(int)Math.Round(preferences.Height*dpi.DpiScaleY));
        int x=bounds.X+(int)Math.Round(preferences.X*Math.Max(0,bounds.Width-width));int y=bounds.Y+(int)Math.Round(preferences.Y*Math.Max(0,bounds.Height-height));
        positioner.Apply(new WindowInteropHelper(this).Handle,new(x,y,width,height));
    }
    internal void BeginEdit(Int32Rect bounds)
    {
        if(Editing)return;PresentAt(bounds);Editing=true;view.SuspendMotion(true);caption.Visibility=Visibility.Visible;ResizeMode=ResizeMode.CanResize;
        nativeFrame.EnsureTransparent();SetPassive(false);
    }
    internal void FinishEdit()
    {
        if(!Editing)return;if(WindowState!=WindowState.Normal)WindowState=WindowState.Normal;UpdateLayout();var dpi=VisualTreeHelper.GetDpi(this);
        if(reference is {} b)
        {
            preferences=(preferences with{Width=ActualWidth,Height=ActualHeight,X=(Left*dpi.DpiScaleX-b.X)/Math.Max(1,b.Width-ActualWidth*dpi.DpiScaleX),Y=(Top*dpi.DpiScaleY-b.Y)/Math.Max(1,b.Height-ActualHeight*dpi.DpiScaleY)}).Sanitize();save(preferences);
        }
        Editing=false;caption.Visibility=Visibility.Collapsed;ResizeMode=ResizeMode.NoResize;nativeFrame.EnsureTransparent();SetPassive(true);Hide();view.SuspendMotion(false);
    }
    internal void Conceal(){if(!Editing&&IsVisible)Hide();}
}

internal sealed class ScheduleToast : Window
{
    private readonly System.Windows.Threading.DispatcherTimer lifetime=new(){Interval=TimeSpan.FromSeconds(10)};
    internal ScheduleToast(string title,string detail,Int32Rect? bounds=null,SchedulePreferences? settings=null)
    {
        Title="WF Labs · 일정 알림";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowActivated=false;ShowInTaskbar=false;Topmost=true;Focusable=false;IsHitTestVisible=false;UseLayoutRounding=true;
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);Width=420;SizeToContent=SizeToContent.Height;
        var body=new StackPanel();body.Children.Add(new TextBlock{Text=title,FontSize=17,Foreground=new SolidColorBrush(Color.FromRgb(242,203,87)),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)});
        body.Children.Add(new TextBlock{Text=detail,FontSize=14,LineHeight=21,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White});
        var appearance=(settings??new()).Sanitize();foreach(var text in body.Children.OfType<TextBlock>()){text.FontSize*=appearance.TextScale;text.LineHeight=text.FontSize*1.5;ScheduleOverlayView.Shadow(text,appearance.TextShadow,appearance.ShadowStrength);}
        Content=new Border{Child=body,Padding=new(18),Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(appearance.BackgroundOpacity*255),15,17,19)),BorderBrush=new SolidColorBrush(Color.FromRgb(108,97,51)),BorderThickness=new(1)};
        var area=SystemParameters.WorkArea;Left=area.Right-Width-24;Top=area.Bottom-220;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;SetPassive(h);};
        Loaded+=(_,_)=>{if(bounds is {} b){var d=VisualTreeHelper.GetDpi(this);new OverlayPositioner().Apply(new WindowInteropHelper(this).Handle,new(b.X+b.Width-(int)(Width*d.DpiScaleX)-24,b.Y+24,(int)(Width*d.DpiScaleX),(int)Math.Ceiling(ActualHeight*d.DpiScaleY)));}lifetime.Start();};
        lifetime.Tick+=(_,_)=>Close();Closed+=(_,_)=>lifetime.Stop();
    }
    private static void SetPassive(IntPtr h)
    {SetStyle(h,-20,new(RelicOverlay.GetStyle(h,-20).ToInt64()|RelicOverlay.PassThroughStyle));}
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetStyle(IntPtr hwnd,int index,IntPtr value);
}
