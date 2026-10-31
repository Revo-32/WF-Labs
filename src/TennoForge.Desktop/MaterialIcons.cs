using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;

namespace TennoForge.Desktop;

internal static class MaterialIcons
{
    private static readonly Lazy<Dictionary<string,string>> files=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WFLabs.material-icons.json")!;
        using var manifest=JsonDocument.Parse(stream);
        return manifest.RootElement.GetProperty("images").EnumerateArray()
            .SelectMany(image=>image.GetProperty("ids").EnumerateArray().Select(id=>(Id:id.GetString()!,File:image.GetProperty("file").GetString()!)))
            .ToDictionary(pair=>pair.Id,pair=>pair.File,StringComparer.Ordinal);
    });
    private static readonly Dictionary<string,ImageSource> cache=new(StringComparer.Ordinal);
    internal static bool HasIcon(string id)=>files.Value.ContainsKey(id);
    internal static ImageSource? Get(string id)
    {
        if(!files.Value.TryGetValue(id,out var file))return null;
        if(cache.TryGetValue(file,out var existing))return existing;
        var resource=Application.GetResourceStream(new Uri("pack://application:,,,/WFLabs;component/Assets/Materials/"+file));
        if(resource is null)return null;
        using var stream=resource.Stream;
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth=128;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();cache[file]=bitmap;return bitmap;
    }
}

public sealed class MaterialIconConverter:MarkupExtension,IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)=>this;
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is string id?MaterialIcons.Get(id):null;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
