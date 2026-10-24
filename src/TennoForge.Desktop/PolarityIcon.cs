using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class PolarityIcon
{
    private record Part(Geometry Shape, bool Filled, bool Stroked);
    private record IconData(Size Viewport,Part[] Parts);
    private static readonly Dictionary<string, IconData> Shapes = new();
    public static FrameworkElement Create(string polarity)
    {
        string? name = polarity switch { "AP_ATTACK" or "Madurai" => "Madurai", "AP_DEFENSE" or "Vazarin" => "Vazarin", "AP_TACTIC" or "Naramon" => "Naramon",
            "AP_POWER" or "Zenurik"=>"Zenurik","AP_WARD" or "Unairu"=>"Unairu","AP_PRECEPT" or "Penjaga"=>"Penjaga","AP_UMBRA" or "Umbra"=>"Umbra",_ => null };
        var label=name switch{"Madurai"=>"마두라이","Vazarin"=>"바자린","Naramon"=>"나라몬","Zenurik"=>"제누릭","Unairu"=>"유나이루","Penjaga"=>"펜자가","Umbra"=>"움브라",_=>ModBoard.PolarityLabel(polarity)};
        var host=new Grid{Width=16,Height=16,VerticalAlignment=VerticalAlignment.Center,ToolTip=label};
        System.Windows.Automation.AutomationProperties.SetName(host,label);
        if(name is null)
        {
            var symbol=new TextBlock{Text=polarity is "Omni" or "Universal"?"∞":polarity is "None" or ""?"—":"?",FontSize=14,Height=16,LineHeight=16,
                LineStackingStrategy=LineStackingStrategy.BlockLineHeight,TextWrapping=TextWrapping.NoWrap,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(0)};
            symbol.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");host.Children.Add(symbol);return host;
        }
        if (!Shapes.TryGetValue(name, out var data))
        {
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/WFLabs;component/Assets/Polarities/{name}.svg")).Stream;
            var doc = XDocument.Load(stream);
            var viewport=((string)doc.Root!.Attribute("viewBox")!).Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s,CultureInfo.InvariantCulture)).ToArray();
            var parts = doc.Descendants().Where(e => e.Name.LocalName == "path").Select(e =>
            {
                var style = (string?)e.Attribute("style") ?? "";
                var geometry = Geometry.Parse((style.Contains("fill-rule:evenodd") ? "F0 " : "F1 ") + (string)e.Attribute("d")!).Clone();
                var transforms=new TransformGroup();
                foreach(var element in e.AncestorsAndSelf())
                {
                    if(element.Attribute("transform") is not {} transform)continue;
                    var match=Regex.Match(transform.Value,@"^translate\(\s*([-\d.]+)[ ,]+([-\d.]+)\s*\)$");
                    if(!match.Success)throw new InvalidDataException("극성 SVG의 지원하지 않는 변환: "+transform.Value);
                    transforms.Children.Add(new TranslateTransform(double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)));
                }
                transforms.Children.Add(new TranslateTransform(-viewport[0],-viewport[1]));geometry.Transform=transforms;
                geometry.Freeze(); return new Part(geometry, !style.Contains("fill:none"), style.Contains("stroke:")&&!style.Contains("stroke:none"));
            }).ToArray();
            data=new(new(viewport[2],viewport[3]),parts);Shapes[name] = data;
        }
        var canvas = new Canvas { Width = data.Viewport.Width, Height = data.Viewport.Height };
        foreach (var part in data.Parts)
        {
            var path = new System.Windows.Shapes.Path { Data = part.Shape, StrokeThickness = 1 };
            if (part.Filled) path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
            if (part.Stroked) path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
            canvas.Children.Add(path);
        }
        host.Children.Add(new Viewbox { Child = canvas, Stretch=Stretch.Uniform });return host;
    }
}
