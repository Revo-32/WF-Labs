using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class PolarityIcon
{
    private record Part(Geometry Shape, bool Filled, bool Stroked);
    private static readonly Dictionary<string, Part[]> Shapes = new();
    public static FrameworkElement Create(string polarity)
    {
        if(polarity is "Omni" or "Universal")
        {var symbol=new TextBlock{Text="∞",FontSize=16,ToolTip=ModBoard.PolarityLabel(polarity)};symbol.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");return symbol;}
        string? name = polarity switch { "AP_ATTACK" => "Madurai", "AP_DEFENSE" => "Vazarin", "AP_TACTIC" => "Naramon", _ => null };
        if (name is null) return new TextBlock { Text = "—", ToolTip = ModLibrary.PolarityName(polarity) };
        if (!Shapes.TryGetValue(name, out var parts))
        {
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/WFLabs;component/Assets/Polarities/{name}.svg")).Stream;
            var doc = XDocument.Load(stream);
            parts = doc.Descendants().Where(e => e.Name.LocalName == "path").Select(e =>
            {
                var style = (string?)e.Attribute("style") ?? "";
                var geometry = Geometry.Parse((style.Contains("fill-rule:nonzero") ? "F1 " : "F0 ") + (string)e.Attribute("d")!);
                geometry.Freeze(); return new Part(geometry, !style.Contains("fill:none"), !style.Contains("stroke:none"));
            }).ToArray();
            Shapes[name] = parts;
        }
        var canvas = new Canvas { Width = 50, Height = 50 };
        foreach (var part in parts)
        {
            var path = new System.Windows.Shapes.Path { Data = part.Shape, StrokeThickness = 1 };
            if (part.Filled) path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
            if (part.Stroked) path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
            canvas.Children.Add(path);
        }
        return new Viewbox { Child = canvas, Width = 15, Height = 15, ToolTip = ModLibrary.PolarityName(polarity) };
    }
}
