using System.Windows;
using System.Windows.Media;

namespace TennoForge.Desktop;
internal static class Theme
{
    public static bool IsDark { get; private set; } = true;
    public static void Apply(bool dark)
    {
        IsDark = dark;
        var colors = new Dictionary<string,(string Dark,string Light)> {
            ["TextBrush"]=("#F0F5F8","#0C131D"),["MutedBrush"]=("#A8B9CB","#526579"),
            ["BrandPrimaryBrush"]=("#F0F5F8","#0C131D"),["BrandAccentBrush"]=("#64D9F2","#253547"),
            ["AccentBrush"]=("#64D9F2","#006B83"),["PurpleBrush"]=("#C3A2E6","#7551A0"),
            ["ErrorBrush"]=("#FF989F","#AB2E3C"),["SurfaceBrush"]=("#111D2B","#F8FBFD"),
            ["SolidSurfaceBrush"]=("#111D2B","#F8FBFD"),["ControlBrush"]=("#172536","#FFFFFF"),
            ["StrokeBrush"]=("#253547","#CBD7E2"),["HoverBrush"]=("#253D50","#DFEBF2"),
            ["PopupBrush"]=("#172536","#F8FBFD"),["ScrollbarThumbBrush"]=("#536B82","#8096AB"),
            ["AccentSurfaceBrush"]=("#153642","#DAF1F7"),["CardFillBrush"]=("#172536","#FFFFFF"),
            ["CardHeadBrush"]=("#253547","#D8E4EE"),["EmptyBrush"]=("#101B28","#E3ECF3"),
            ["SwitchOffBrush"]=("#2B3E52","#C5D3DF"),["SwitchKnobBrush"]=("#0C131D","#FFFFFF"),
            ["SelectedNavBrush"]=("#153642","#DAF1F7"),["CloseHoverBrush"]=("#74363D","#F3C8CD"),
            ["ResultsBackgroundBrush"]=("#0C131D","#F0F5F8"),["ResultsSurfaceBrush"]=("#111D2B","#F8FBFD"),
            ["ResultsStrokeBrush"]=("#253547","#CBD7E2"),["ResultsTextBrush"]=("#F0F5F8","#0C131D"),["ResultsMutedBrush"]=("#A8B9CB","#526579"),
            ["CapacityGoodBrush"]=("#93D7B0","#286540"),["ChartAccentBrush"]=("#64D9F2","#006B83")
        };
        foreach(var (key,value) in colors) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?value.Dark:value.Light));b.Freeze();Application.Current.Resources[key]=b; }
        ModAppearance.ApplyTheme(dark);
    }
}
