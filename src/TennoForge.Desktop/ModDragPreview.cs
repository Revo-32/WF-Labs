using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

// One snapshot per drag, event-driven position updates. Never intercepts a drop.
internal sealed class ModDragPreview : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X,Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    private readonly FrameworkElement source;
    private readonly Panel host;
    private readonly Canvas overlay=new(){IsHitTestVisible=false,Opacity=.76};
    private readonly Image ghost=new(){IsHitTestVisible=false,Stretch=Stretch.Fill};
    private readonly Size cardSize;
    private readonly double originalOpacity;
    private Point position;
    private bool disposed;
    internal Point Position=>position;
    internal bool Detached=>disposed;
    internal bool IsHitTestVisible=>overlay.IsHitTestVisible;
    internal double Opacity=>overlay.Opacity;
    internal Visibility Visibility=>overlay.Visibility;
    internal int VisibleSnapshotPixels {get;}
    internal object Inspect(string snapshotPath)
    {
        if(ghost.ActualWidth<1||ghost.ActualHeight<1||!host.Children.Contains(overlay))throw new InvalidOperationException("드래그 카드가 실제 화면에 배치되지 않았습니다.");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create((BitmapSource)ghost.Source));using var stream=System.IO.File.Create(snapshotPath);encoder.Save(stream);
        return new{position,visible=overlay.Visibility.ToString(),overlayWidth=overlay.ActualWidth,overlayHeight=overlay.ActualHeight,ghostWidth=ghost.ActualWidth,ghostHeight=ghost.ActualHeight,cardSize,attached=host.Children.Contains(overlay)};
    }
    private ModDragPreview(FrameworkElement source,Panel host,FrameworkElement? appearance)
    {
        this.source=source;this.host=host;originalOpacity=source.Opacity;
        source.InvalidateVisual();source.UpdateLayout();
        cardSize=source.RenderSize;
        var dpi=VisualTreeHelper.GetDpi(source);
        var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(cardSize.Width*dpi.DpiScaleX)),Math.Max(1,(int)Math.Ceiling(cardSize.Height*dpi.DpiScaleY)),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
        // Rendering an arranged child directly retains its parent offset and can
        // produce an empty bitmap. Paint its local visual bounds at (0,0).
        if(appearance is not null)
        {
            // A fresh, origin-aligned card avoids snapshots of recycled visuals
            // whose drawing cache is empty immediately after an equip/remove.
            appearance.Width=cardSize.Width;appearance.Height=cardSize.Height;appearance.Margin=new(0);
            VisualTreeHelper.SetRootDpi(appearance,dpi);TextRendering.Apply(appearance,dpi);
            appearance.Measure(cardSize);appearance.Arrange(new Rect(cardSize));appearance.UpdateLayout();bitmap.Render(appearance);
        }
        else
        {
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())
                dc.DrawRectangle(new VisualBrush(source){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(cardSize),Stretch=Stretch.Fill},null,new Rect(cardSize));
            bitmap.Render(drawing);
        }
        var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
        VisibleSnapshotPixels=Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight).Count(i=>pixels[i*4+3]>16);
        bitmap.Freeze();ghost.Source=bitmap;ghost.Width=cardSize.Width;ghost.Height=cardSize.Height;
        overlay.Children.Add(ghost);Panel.SetZIndex(overlay,int.MaxValue);
        if(host is Grid grid){Grid.SetRowSpan(overlay,Math.Max(1,grid.RowDefinitions.Count));Grid.SetColumnSpan(overlay,Math.Max(1,grid.ColumnDefinitions.Count));}
        host.Children.Add(overlay);source.Opacity=.38;host.PreviewDragOver+=OnDragOver;source.GiveFeedback+=OnFeedback;
        FollowCursor();
    }
    internal static ModDragPreview? Start(FrameworkElement source,FrameworkElement? appearance=null)
    {
        if(source.ActualWidth<1||source.ActualHeight<1)return null;
        if(Window.GetWindow(source)?.Content is not FrameworkElement shell)return null;
        // The decorator's child is the complete application, so the ghost can cross columns.
        var host=shell is Border {Child:System.Windows.Documents.AdornerDecorator decorator}?decorator.Child as Panel:null;
        return host is not null?new(source,host,appearance):null;
    }
    private void OnDragOver(object sender,DragEventArgs e)=>Move(e.GetPosition(host));
    private void OnFeedback(object sender,GiveFeedbackEventArgs e)=>FollowCursor();
    private void FollowCursor()
    {if(GetCursorPos(out var point)&&PresentationSource.FromVisual(host) is not null)Move(host.PointFromScreen(new(point.X,point.Y)));}
    internal void Move(Point cursor)
    {
        overlay.Visibility=new Rect(host.RenderSize).Contains(cursor)?Visibility.Visible:Visibility.Hidden;
        position=new(cursor.X+16,cursor.Y+18);
        if(position.X+cardSize.Width>host.ActualWidth)position.X=cursor.X-cardSize.Width-16;
        if(position.Y+cardSize.Height>host.ActualHeight)position.Y=cursor.Y-cardSize.Height-18;
        Canvas.SetLeft(ghost,position.X);Canvas.SetTop(ghost,position.Y);host.UpdateLayout();
    }
    internal void MoveFrom(Visual relativeTo,Point point)=>Move(relativeTo.TransformToVisual(host).Transform(point));
    public void Dispose()
    {
        if(disposed)return;disposed=true;host.PreviewDragOver-=OnDragOver;source.GiveFeedback-=OnFeedback;
        source.Opacity=originalOpacity;host.Children.Remove(overlay);
    }
}
