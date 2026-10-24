using System.Windows;
using System.Windows.Media;

namespace TennoForge.Desktop;

internal static class TextRendering
{
    // Natural glyph metrics preserve curved/diagonal outlines instead of GDI-style
    // pixel fitting. ClearType uses the matching natural filter on opaque surfaces;
    // WPF falls back to grayscale coverage where subpixel rendering is unavailable.
    internal static TextFormattingMode Formatting(DpiScale dpi)=>TextFormattingMode.Ideal;
    internal static void Apply(DependencyObject target,DpiScale dpi)
    {
        TextOptions.SetTextFormattingMode(target,Formatting(dpi));
        TextOptions.SetTextRenderingMode(target,TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(target,TextHintingMode.Fixed);
    }
}
