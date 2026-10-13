using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

// Vector Core: three connected strokes. The final W diagonal is also the F stem.
// No runtime rasterizer or external packages; export with the repository's .NET 8 SDK.
internal static class Program
{
    private const string Navy = "#0C131D", White = "#F0F5F8", Cyan = "#64D9F2", Steel = "#253547";
    private static readonly int[] Sizes = [16, 32, 48, 64, 128, 256];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly (string Path, string Color)[] Strokes =
    [
        ("M36,64 L66,192 108,124 144,192 162,128", White),
        ("M159.75,136 L180,64 224,64", White),
        ("M162,128 L212,128", Cyan)
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("프로젝트 루트 경로를 지정하세요.");
        var root = Path.GetFullPath(args[0]);
        var target = Path.Combine(root, "src/TennoForge.Desktop/Assets/Brand");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(target, "png"));
        Directory.CreateDirectory(Path.Combine(target, "preview"));
        var parts = Strokes.Select(s => (Shape: Outline(s.Path, 24), s.Color)).ToArray();
        var lightParts = parts.Select(p => (p.Shape, Color: p.Color == White ? Navy : Steel)).ToArray();
        WriteSvg(target, "wf-labs-symbol.svg", 256, 256, parts, false);
        WriteSvg(target, "wf-labs-symbol-light.svg", 256, 256, lightParts, false);
        WriteSvg(target, "wf-labs-icon.svg", 256, 256, parts, true);

        var family = new FontFamily(new Uri(Path.Combine(root, "src/TennoForge.Desktop/Fonts") + "/"), "./#Wanted Sans");
        var text = new FormattedText("WF Labs", Invariant, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 88, Brush(White), 1);
        if (!text.BuildGeometry(new Point()).Bounds.IsEmpty)
        {
            var word = text.BuildGeometry(new Point());
            var bounds = word.Bounds;
            word.Transform = new TranslateTransform(284 - bounds.X, 128 - bounds.Height / 2 - bounds.Y);
            var horizontal = parts.Append((word.GetOutlinedPathGeometry(), White)).ToArray();
            var horizontalLight = lightParts.Append((word.GetOutlinedPathGeometry(), Navy)).ToArray();
            WriteSvg(target, "wf-labs-horizontal.svg", 652, 256, horizontal, false);
            WriteSvg(target, "wf-labs-horizontal-light.svg", 652, 256, horizontalLight, false);
            SavePng(Render(652, 256, 1, dc => DrawParts(dc, horizontal)), Path.Combine(target, "png/wf-labs-horizontal.png"));
        }

        var frames = new List<byte[]>();
        var checks = new List<object>();
        foreach (var size in Sizes)
        {
            // The 16 px master aligns the cyan bar to full pixel rows and opens the W counter.
            // Larger sizes retain the canonical vector geometry without a downsampling cascade.
            var optical = size == 16 ? new[]
            {
                (Outline("M36,64 L66,192 108,116 144,192 162,128", 28), White),
                (Outline("M159.75,136 L180,64 224,64", 28), White),
                (Geometry.Parse("M161,112 H224 V144 H152 Z").GetOutlinedPathGeometry(), Cyan)
            } : parts;
            var bitmap = Render(size, size, size / 256d, dc => DrawIcon(dc, optical));
            var file = Path.Combine(target, $"png/wf-labs-icon-{size}.png");
            var png = SavePng(bitmap, file);
            frames.Add(size >= 128 ? png : Dib(bitmap));
            var decoded = BitmapFrame.Create(new Uri(file), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoded.PixelWidth != size || decoded.PixelHeight != size) throw new Exception("PNG 크기 불일치");
            checks.Add(new { size, width = decoded.PixelWidth, height = decoded.PixelHeight, opticalMaster = size == 16 });
        }
        var ico = Path.Combine(target, "wf-labs.ico");
        using (var writer = new BinaryWriter(File.Create(ico)))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)Sizes.Length);
            var offset = 6 + Sizes.Length * 16;
            for (var i = 0; i < Sizes.Length; i++)
            {
                writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i])); writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (var frame in frames) writer.Write(frame);
        }
        using (var stream = File.OpenRead(ico))
        {
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (!decoder.Frames.Select(f => f.PixelWidth).Order().SequenceEqual(Sizes) || decoder.Frames.Any(f => f.PixelHeight != f.PixelWidth)) throw new Exception("ICO 프레임 크기 불일치");
            foreach (var frame in decoder.Frames)
            {
                var png = BitmapFrame.Create(new Uri(Path.Combine(target, $"png/wf-labs-icon-{frame.PixelWidth}.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (!Pixels(frame).SequenceEqual(Pixels(png))) throw new Exception($"ICO {frame.PixelWidth}px 픽셀 불일치");
            }
        }
        ContactSheet(target, parts, family);
        File.WriteAllText(Path.Combine(target, "preview/asset-check.json"), JsonSerializer.Serialize(new
        { passed = true, png = checks, icoFrames = Sizes, icoSmallFrames = "32-bit BGRA DIB + AND mask", icoLargeFrames = "PNG", vectorStrokes = 3, font = "Wanted Sans SemiBold (outlined)", palette = new[] { Navy, White, Cyan, Steel } }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine($"브랜드 SVG 5개, PNG 아이콘 6개, ICO 6개 해상도 생성 및 디코딩 확인: {target}");
        return 0;
    }

    private static PathGeometry Outline(string data, double width) => Geometry.Parse(data).GetWidenedPathGeometry(
        new Pen(Brushes.White, width) { LineJoin = PenLineJoin.Bevel, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, .01, ToleranceType.Absolute);
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static string PathData(Geometry geometry)
    {
        var data = geometry.GetOutlinedPathGeometry().ToString(Invariant);
        data = data.StartsWith("F1", StringComparison.Ordinal) || data.StartsWith("F0", StringComparison.Ordinal) ? data[2..].TrimStart() : data;
        return Regex.Replace(data, @"-?\d+(?:\.\d+)?(?:[Ee][+-]?\d+)?", m => double.Parse(m.Value, Invariant).ToString("0.###", Invariant));
    }
    private static void WriteSvg(string folder, string name, int width, int height, IEnumerable<(PathGeometry Shape, string Color)> parts, bool tile)
    {
        var svg = new XElement(Svg + "svg", new XAttribute("viewBox", $"0 0 {width} {height}"), new XAttribute("width", width), new XAttribute("height", height),
            new XAttribute("role", "img"), new XAttribute("aria-labelledby", "title desc"),
            new XElement(Svg + "title", new XAttribute("id", "title"), "WF Labs · Vector Core"),
            new XElement(Svg + "desc", new XAttribute("id", "desc"), "W와 F를 하나의 기하학적 흐름으로 연결한 WF Labs 로고"));
        if (tile) svg.Add(new XElement(Svg + "rect", new XAttribute("width", 256), new XAttribute("height", 256), new XAttribute("rx", 36), new XAttribute("fill", Navy)));
        foreach (var part in parts) svg.Add(new XElement(Svg + "path", new XAttribute("fill", part.Color), new XAttribute("fill-rule", "nonzero"), new XAttribute("d", PathData(part.Shape))));
        new XDocument(new XDeclaration("1.0", "utf-8", null), svg).Save(Path.Combine(folder, name));
    }
    private static void DrawParts(DrawingContext dc, IEnumerable<(PathGeometry Shape, string Color)> parts)
    { foreach (var part in parts) dc.DrawGeometry(Brush(part.Color), null, part.Shape); }
    private static void DrawIcon(DrawingContext dc, IEnumerable<(PathGeometry Shape, string Color)> parts)
    { dc.DrawRoundedRectangle(Brush(Navy), null, new Rect(0, 0, 256, 256), 36, 36); DrawParts(dc, parts); }
    private static RenderTargetBitmap Render(int width, int height, double scale, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(scale, scale)); draw(dc); dc.Pop(); }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); return bitmap;
    }
    private static byte[] SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); var data = stream.ToArray(); File.WriteAllBytes(path, data); return data;
    }
    private static byte[] Dib(BitmapSource bitmap)
    {
        var size = bitmap.PixelWidth; var stride = size * 4; var pixels = new byte[stride * size];
        new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, stride, 0);
        var maskStride = (size + 31) / 32 * 4;
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write(40); w.Write(size); w.Write(size * 2); w.Write((ushort)1); w.Write((ushort)32);
        w.Write(0); w.Write(stride * size + maskStride * size); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (var y = size - 1; y >= 0; y--) w.Write(pixels, y * stride, stride);
        for (var y = size - 1; y >= 0; y--)
        {
            var mask = new byte[maskStride];
            for (var x = 0; x < size; x++) if (pixels[y * stride + x * 4 + 3] == 0) mask[x / 8] |= (byte)(0x80 >> x % 8);
            w.Write(mask);
        }
        return stream.ToArray();
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4];
        new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0).CopyPixels(bytes, image.PixelWidth * 4, 0);
        return bytes;
    }
    private static void ContactSheet(string target, (PathGeometry Shape, string Color)[] parts, FontFamily family)
    {
        var sheet = Render(1200, 900, 1, dc =>
        {
            dc.DrawRectangle(Brush(Navy), null, new Rect(0, 0, 1200, 900));
            void Label(string value, double x, double y, double size = 16, string color = White) => dc.DrawText(new FormattedText(value, Invariant, FlowDirection.LeftToRight,
                new Typeface(family, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), size, Brush(color), 1), new Point(x, y));
            Label("WF LABS   /   VECTOR CORE", 48, 32, 18, Cyan);
            dc.PushTransform(new TranslateTransform(64, 104)); DrawParts(dc, parts); dc.Pop();
            var horizontal = XDocument.Load(Path.Combine(target, "wf-labs-horizontal.svg")).Descendants(Svg + "path");
            dc.PushTransform(new TranslateTransform(440, 104));
            foreach (var path in horizontal) dc.DrawGeometry(Brush((string)path.Attribute("fill")!), null, Geometry.Parse((string)path.Attribute("d")!));
            dc.Pop();
            Label("심볼 · 세 개의 연결된 획", 64, 390, 16, "#A0B0C0");
            Label("Wanted Sans SemiBold · 아웃라인 워드마크", 460, 390, 16, "#A0B0C0");
            dc.DrawLine(new Pen(Brush(Steel), 1), new Point(48, 448), new Point(1152, 448));
            Label("애플리케이션 아이콘 · 실제 픽셀 크기", 48, 478);
            var x = 48d;
            foreach (var size in Sizes)
            {
                var frame = BitmapFrame.Create(new Uri(Path.Combine(target, $"png/wf-labs-icon-{size}.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                dc.DrawRectangle(Brush("#182332"), null, new Rect(x, 536, Math.Max(88, size + 24), 280));
                dc.DrawImage(frame, new Rect(x + 12, 548, size, size));
                Label($"{size} px", x + 12, 826, 16, "#A0B0C0");
                x += Math.Max(88, size + 24) + 24;
            }
        });
        SavePng(sheet, Path.Combine(target, "preview/wf-labs-brand-sheet.png"));
    }
}
