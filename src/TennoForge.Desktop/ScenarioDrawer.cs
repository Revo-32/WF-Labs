using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Markup;

namespace TennoForge.Desktop;

// One persistent settings tree: opening/closing never copies or resets a build.
internal sealed class ScenarioDrawer : Grid
{
    internal static bool ReduceMotion {get;set;}
    private readonly Grid overlay=new(){Visibility=Visibility.Collapsed,ClipToBounds=true};
    private readonly Grid moving=new(){VerticalAlignment=VerticalAlignment.Bottom};
    private readonly Border sheet=new(){Padding=new(26,20,26,22),BorderThickness=new(1),CornerRadius=new(8,8,0,0)};
    private readonly TranslateTransform slide=new();
    private readonly TextBlock summary=new(){TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock warning=new(){FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
    private readonly System.Windows.Shapes.Path arrow=new(){Data=Geometry.Parse("M 1,7 L 7,1 L 13,7"),Width=14,Height=8,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,VerticalAlignment=VerticalAlignment.Center,Margin=new(12,0,0,0),RenderTransformOrigin=new(.5,.5)};
    private readonly Button handle=new(){Height=40,Padding=new(38,6,38,6),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};
    private readonly Button close=new(){Content="완료 · 닫기",Padding=new(14,6,14,6),Margin=new(18,0,0,0)};
    private readonly UIElement workspace;
    private IInputElement? previousFocus;
    internal bool IsOpen {get;private set;}
    internal FrameworkElement Sheet=>sheet;
    internal Button HandleForCheck=>handle;
    internal Button CloseForCheck=>close;
    internal bool SettledForCheck=>!slide.HasAnimatedProperties&&slide.Y==0;
    internal double MotionOffsetForCheck=>slide.Y;
    internal ScenarioDrawer(UIElement workspace,UIElement settings)
    {
        this.workspace=workspace;RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});RowDefinitions.Add(new(){Height=new(40)});Children.Add(workspace);
        var handleContent=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        var label=RivenPresentation.Text("상황 설정",15);label.Margin=new(0);label.VerticalAlignment=VerticalAlignment.Center;handleContent.Children.Add(label);handleContent.Children.Add(arrow);
        arrow.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"TextBrush");handle.Content=handleContent;handle.SetResourceReference(Control.BackgroundProperty,"SolidSurfaceBrush");handle.SetResourceReference(Control.BorderBrushProperty,"StrokeBrush");
        handle.Template=(ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
              <Grid>
                <Path x:Name="Contour" Data="M 0,40 L 29,3 Q 31,0 36,0 L 564,0 Q 569,0 571,3 L 600,40 Z" Stretch="Fill" Fill="{TemplateBinding Background}" Stroke="{TemplateBinding BorderBrush}" StrokeThickness="1"/>
                <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Contour" Property="Fill" Value="{DynamicResource HoverBrush}"/><Setter TargetName="Contour" Property="Stroke" Value="{DynamicResource AccentBrush}"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Contour" Property="Stroke" Value="{DynamicResource AccentBrush}"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="Contour" Property="Fill" Value="{DynamicResource AccentSurfaceBrush}"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        Grid.SetRow(handle,1);Children.Add(handle);Panel.SetZIndex(handle,21);
        handle.Click+=(_,_)=>SetOpen(!IsOpen);AutomationProperties.SetName(handle,"상황 설정 열기");
        var scrim=new Border{Background=new SolidColorBrush(Color.FromArgb(100,0,0,0))};scrim.MouseLeftButtonDown+=(_,e)=>{SetOpen(false);e.Handled=true;};overlay.Children.Add(scrim);
        sheet.SetResourceReference(Border.BackgroundProperty,"SolidSurfaceBrush");sheet.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");
        moving.RowDefinitions.Add(new(){Height=new(40)});moving.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});Grid.SetRow(sheet,1);moving.Children.Add(sheet);moving.RenderTransform=slide;Panel.SetZIndex(moving,1);
        var body=new Grid();body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var heading=new DockPanel{Margin=new(0,0,0,14)};DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);heading.Children.Add(RivenPresentation.Text("상황 설정",22));
        warning.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");var warningStyle=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));var emptyWarning=new Trigger{Property=TextBlock.TextProperty,Value=""};emptyWarning.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));warningStyle.Triggers.Add(emptyWarning);warning.Style=warningStyle;
        body.Children.Add(RivenPresentation.Stack(heading,warning));Grid.SetRow(settings,1);body.Children.Add(settings);sheet.Child=body;
        close.Click+=(_,_)=>SetOpen(false);UiHierarchy.Primary(close);overlay.Children.Add(moving);Panel.SetZIndex(overlay,20);Grid.SetRowSpan(overlay,2);Children.Add(overlay);
        KeyboardNavigation.SetTabNavigation(sheet,KeyboardNavigationMode.Cycle);KeyboardNavigation.SetControlTabNavigation(sheet,KeyboardNavigationMode.Cycle);
        PreviewKeyDown+=(_,e)=>
        {
            if(!IsOpen||e.Key!=Key.Escape)return;
            if(SearchField.HandlesEscape(Keyboard.FocusedElement))return;
            // Let the active dropdown consume the first Escape; the next closes the sheet.
            if(LayoutDiagnostics.Tree(sheet).OfType<ComboBox>().Any(c=>c.IsDropDownOpen))return;
            SetOpen(false);e.Handled=true;
        };
        SizeChanged+=(_,_)=>{handle.Width=Math.Max(1,ActualWidth/3);moving.Height=Math.Max(40,ActualHeight*.86);};
        Unloaded+=(_,_)=>SetOpen(false,false);
    }
    internal void SetSummary(string text){summary.Text=text;handle.ToolTip=text;AutomationProperties.SetName(handle,"상황 설정 "+(IsOpen?"닫기":"열기")+" · "+text);}
    internal void SetStatusSource(TextBlock source)=>warning.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(TextBlock.Text)){Source=source});
    internal void SetOpen(bool open,bool animate=true)
    {
        if(open==IsOpen&&!(overlay.Visibility==Visibility.Visible&&!open))return;
        var wasVisible=overlay.Visibility==Visibility.Visible;var previousOffset=slide.Y;
        IsOpen=open;slide.BeginAnimation(TranslateTransform.YProperty,null);slide.Y=0;
        arrow.RenderTransform=new RotateTransform(open?180:0);AutomationProperties.SetName(handle,"상황 설정 "+(open?"닫기":"열기")+" · "+summary.Text);
        if(open)
        {
            if(!wasVisible){previousFocus=Keyboard.FocusedElement;Children.Remove(handle);Grid.SetRow(handle,0);moving.Children.Add(handle);}
            overlay.Visibility=Visibility.Visible;workspace.IsEnabled=false;close.Focus();
        }
        void FinishClose(){if(IsOpen)return;overlay.Visibility=Visibility.Collapsed;workspace.IsEnabled=true;slide.BeginAnimation(TranslateTransform.YProperty,null);slide.Y=0;moving.Children.Remove(handle);Grid.SetRow(handle,1);if(!Children.Contains(handle))Children.Add(handle);if(IsLoaded){if(previousFocus is UIElement {IsVisible:true,IsEnabled:true} element)element.Focus();else handle.Focus();}}
        if(!animate||ReduceMotion||!SystemParameters.ClientAreaAnimation){if(!open)FinishClose();return;}
        // The handle starts exactly at its docked position and remains attached to the panel.
        var travel=Math.Max(0,(double.IsFinite(moving.Height)?moving.Height:ActualHeight*.86)-40);
        var motion=new DoubleAnimation(open&&!wasVisible?travel:previousOffset,open?0:travel,TimeSpan.FromMilliseconds(open?220:170)){EasingFunction=new CubicEase{EasingMode=open?EasingMode.EaseOut:EasingMode.EaseIn},FillBehavior=FillBehavior.Stop};
        if(!open)motion.Completed+=(_,_)=>FinishClose();slide.BeginAnimation(TranslateTransform.YProperty,motion);
    }
    internal static Grid Columns(params UIElement[] columns)
    {
        var grid=new Grid();for(int i=0;i<columns.Length;i++){grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});var host=new Border{Child=columns[i],Margin=new(i==0?0:18,0,i==columns.Length-1?0:18,0)};Grid.SetColumn(host,i);grid.Children.Add(host);}return grid;
    }
}
