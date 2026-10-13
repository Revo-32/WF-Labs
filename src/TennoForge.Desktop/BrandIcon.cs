using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace TennoForge.Desktop;

internal static class BrandIcon
{
    private static readonly Uri IconUri = new("pack://application:,,,/WFLabs;component/Assets/Brand/wf-labs.ico");
    private static readonly Lazy<ImageSource> WindowImage = new(() =>
    {
        var icon = BitmapFrame.Create(IconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        icon.Freeze();
        return icon;
    });

    internal static ImageSource WindowIcon => WindowImage.Value;

    internal static FrameworkElement CreateSymbol()
    {
        // Read the exported master instead of maintaining a second copy of the logo geometry.
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/WFLabs;component/Assets/Brand/wf-labs-symbol.svg")).Stream;
        var document = XDocument.Load(stream);
        var canvas = new Canvas { Width = 256, Height = 256, IsHitTestVisible = false };
        foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "path"))
        {
            var geometry = Geometry.Parse("F1 " + (string)element.Attribute("d")!);
            geometry.Freeze();
            var path = new System.Windows.Shapes.Path { Data = geometry };
            path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
                (string?)element.Attribute("fill") == "#64D9F2" ? "BrandAccentBrush" : "BrandPrimaryBrush");
            canvas.Children.Add(path);
        }
        var symbol = new Viewbox { Child = canvas, Width = 32, Height = 32, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(symbol, "WF Labs 로고");
        return symbol;
    }
}
