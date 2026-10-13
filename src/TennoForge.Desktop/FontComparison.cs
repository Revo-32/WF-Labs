using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TennoForge.Desktop;

// Diagnostic only: identical text, colors, DPI and rendering, with no scaled screenshots.
internal sealed class FontComparison : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var dpi=VisualTreeHelper.GetDpi(this);
        var wanted=(FontFamily)Application.Current.Resources["Wanted"];
        var malgun=new FontFamily("Malgun Gothic");
        var ink=(Brush)FindResource("ResultsTextBrush");
        var muted=(Brush)FindResource("ResultsMutedBrush");
        dc.DrawRectangle((Brush)FindResource("ResultsBackgroundBrush"),null,new Rect(RenderSize));
        void Text(string text,FontFamily family,double size,double x,double y,Brush color)
        {
            var typeface=new Typeface(family,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
            var formatted=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,typeface,size,color,null,TextRendering.Formatting(dpi),dpi.PixelsPerDip);
            dc.DrawText(formatted,new Point(Math.Round(x*dpi.DpiScaleX)/dpi.DpiScaleX,Math.Round(y*dpi.DpiScaleY)/dpi.DpiScaleY));
        }
        Text($"동일한 배경 · 색 · 렌더링 / 배율 {dpi.PixelsPerDip:P0}",wanted,14,20,16,ink);
        Text("크기",wanted,14,20,54,ink);
        Text("Wanted Sans",wanted,16,112,52,ink);
        Text("맑은 고딕",malgun,16,426,52,ink);
        var row=0;
        foreach(var size in new[]{11d,12,13,14,16})
        {
            var y=92+row++*46;
            Text($"{size:0} DIP",wanted,13,20,y,ink);
            Text("비치명 합계 · 치명타 확률 28.0%",wanted,size,112,y,muted);
            Text("비치명 합계 · 치명타 확률 28.0%",malgun,size,426,y,muted);
        }
    }
}
