using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TennoForge.Desktop;

// ToolTipService owns the hover delay. Position updates only on pointer/content changes.
internal sealed class CursorTooltip
{
    private readonly FrameworkElement owner;
    private readonly Func<Point, object> contentAt;
    internal readonly ToolTip Tip;
    private object? previous;
    private Point pointer;
    private bool keyboard;
    internal Point LastPosition { get; private set; }

    private CursorTooltip(FrameworkElement owner, Func<Point, object> contentAt, int delay)
    {
        this.owner=owner;this.contentAt=contentAt;
        Tip=new ToolTip { PlacementTarget=owner, Placement=PlacementMode.RelativePoint, MaxWidth=360, IsHitTestVisible=false, HasDropShadow=false, ShowsToolTipOnKeyboardFocus=true };
        owner.ToolTip=Tip;
        ToolTipService.SetInitialShowDelay(owner,delay);ToolTipService.SetBetweenShowDelay(owner,60);ToolTipService.SetShowDuration(owner,60000);
        ToolTipService.SetShowsToolTipOnKeyboardFocus(owner,true);
        owner.ToolTipOpening+=(_,e)=>
        {
            if(Mouse.LeftButton==MouseButtonState.Pressed){e.Handled=true;return;}
            keyboard=!owner.IsMouseOver&&owner.IsKeyboardFocusWithin;
            pointer=keyboard?new Point(owner.ActualWidth,0):Mouse.GetPosition(owner);Refresh(true);
        };
        owner.MouseMove+=(_,e)=>{keyboard=false;pointer=e.GetPosition(owner);if(e.LeftButton==MouseButtonState.Pressed){Close();return;}Refresh();};
        owner.GotKeyboardFocus+=(_,_)=>{keyboard=true;pointer=new(owner.ActualWidth,0);Refresh();};
        owner.MouseLeave+=(_,_)=>Close();owner.Unloaded+=(_,_)=>Close();
        owner.PreviewMouseDown+=(_,_)=>Close();owner.PreviewMouseWheel+=(_,_)=>Close();
        owner.LostKeyboardFocus+=(_,_)=>{if(keyboard)Close();};
        Tip.Opened+=(_,_)=>Refresh();
        Tip.SizeChanged+=(_,_)=>{if(Tip.IsOpen)Refresh();};
    }
    internal static CursorTooltip Attach(FrameworkElement owner,Func<Point,object> contentAt,int delay=220)=>new(owner,contentAt,delay);
    internal static CursorTooltip Attach(FrameworkElement owner,object content,int delay=220)=>new(owner,_=>content,delay);
    internal void Close()=>Tip.IsOpen=false;
    internal void Refresh(bool positionBeforeOpening=false)
    {
        var content=contentAt(pointer);
        TextRendering.Apply(Tip,VisualTreeHelper.GetDpi(owner));
        if(!Equals(previous,content)){previous=content;Tip.Content=content is string text?TextContent(text):content;}
        if(!Tip.IsOpen&&!positionBeforeOpening)return;
        if(PresentationSource.FromVisual(owner) is null)return;
        Tip.Measure(new Size(360,double.PositiveInfinity));
        var size=Tip.DesiredSize;
        var bounds=WorkArea(owner,pointer);
        LastPosition=Place(pointer,size,bounds);
        Tip.HorizontalOffset=LastPosition.X;Tip.VerticalOffset=LastPosition.Y;
    }
    internal static StackPanel TextContent(string text)
    {
        var lines=text.Replace("\r","").Split('\n');var panel=new StackPanel{MaxWidth=326};
        var title=new TextBlock{Text=lines[0],FontSize=14,FontWeight=FontWeights.SemiBold,LineHeight=22,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,Margin=new Thickness(0,0,0,lines.Length>1?6:0)};
        panel.Children.Add(title);
        if(lines.Length>1)panel.Children.Add(new TextBlock{Text=string.Join("\n",lines.Skip(1)),FontSize=14,LineHeight=22,LineStackingStrategy=LineStackingStrategy.BlockLineHeight});
        return panel;
    }
    // Work area is in owner DIPs. Prefer lower-right, flip horizontally/vertically at edges.
    internal static Point Place(Point pointer,Size size,Rect area)
    {
        const double gap=16,edge=6;
        var x=pointer.X+gap;var y=pointer.Y+gap;
        if(x+size.Width>area.Right-edge)x=pointer.X-gap-size.Width;
        if(y+size.Height>area.Bottom-edge)y=pointer.Y-gap-size.Height;
        return new Point(Math.Clamp(x,area.Left+edge,Math.Max(area.Left+edge,area.Right-size.Width-edge)),Math.Clamp(y,area.Top+edge,Math.Max(area.Top+edge,area.Bottom-size.Height-edge)));
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size;public NativeRect Monitor,Work;public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    internal static Rect WorkArea(FrameworkElement owner,Point point)
    {
        var screen=owner.PointToScreen(point);var monitor=MonitorFromPoint(new NativePoint{X=(int)Math.Round(screen.X),Y=(int)Math.Round(screen.Y)},2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(monitor,ref info))return new Rect(owner.PointFromScreen(new Point()),SystemParameters.WorkArea.Size);
        return new Rect(owner.PointFromScreen(new Point(info.Work.Left,info.Work.Top)),owner.PointFromScreen(new Point(info.Work.Right,info.Work.Bottom)));
    }
    internal void MoveForCheck(Point point){pointer=point;keyboard=false;Refresh();}
}
