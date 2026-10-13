using System.Windows;
using System.Windows.Media;

namespace TennoForge.Desktop;

internal static class TextRendering
{
    // UI text uses pixel-aligned metrics at every monitor scale. Formula text can
    // explicitly request Ideal metrics. ClearType requires an opaque paint surface.
    internal static TextFormattingMode Formatting(DpiScale dpi)=>TextFormattingMode.Display;
    internal static void Apply(DependencyObject target,DpiScale dpi)
    {
        TextOptions.SetTextFormattingMode(target,Formatting(dpi));
        TextOptions.SetTextRenderingMode(target,TextRenderingMode.Auto);
        TextOptions.SetTextHintingMode(target,TextHintingMode.Fixed);
    }
}
