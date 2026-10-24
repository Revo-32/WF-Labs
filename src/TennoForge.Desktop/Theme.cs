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
            ["TextBrush"]=("#F4F4F2","#101112"),["MutedBrush"]=("#B8BABC","#56595D"),
            ["BrandPrimaryBrush"]=("#F4F4F2","#101112"),["BrandAccentBrush"]=("#F2CB57","#3A3D40"),
            ["AccentBrush"]=("#F2CB57","#795900"),["PurpleBrush"]=("#C3A2E6","#7551A0"),
            ["ErrorBrush"]=("#FF989F","#AB2E3C"),["SurfaceBrush"]=("#181A1C","#FAFAF8"),
            ["SolidSurfaceBrush"]=("#181A1C","#FAFAF8"),["ControlBrush"]=("#222528","#FFFFFF"),
            ["StrokeBrush"]=("#3A3D40","#C8CBCD"),["HoverBrush"]=("#33373B","#EBE9E2"),
            ["PopupBrush"]=("#222528","#FAFAF8"),["ScrollbarThumbBrush"]=("#73787D","#7C8287"),
            ["AccentSurfaceBrush"]=("#35301F","#FFF0BE"),["CardFillBrush"]=("#222528","#FFFFFF"),
            ["CardHeadBrush"]=("#3A3D40","#E4E5E2"),["EmptyBrush"]=("#181A1C","#E8E9E6"),
            ["SwitchOffBrush"]=("#464A4E","#C9CDCF"),["SwitchKnobBrush"]=("#101112","#FFFFFF"),
            ["SelectedNavBrush"]=("#35301F","#FFF0BE"),["CloseHoverBrush"]=("#74363D","#F3C8CD"),
            ["ResultsBackgroundBrush"]=("#101112","#F4F4F2"),["ResultsSurfaceBrush"]=("#181A1C","#FAFAF8"),
            ["ResultsStrokeBrush"]=("#3A3D40","#C8CBCD"),["ResultsTextBrush"]=("#F4F4F2","#101112"),["ResultsMutedBrush"]=("#B8BABC","#56595D"),
            ["CapacityGoodBrush"]=("#93D7B0","#286540"),["ChartAccentBrush"]=("#F2CB57","#795900")
        };
        foreach(var (key,value) in colors) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?value.Dark:value.Light));b.Freeze();Application.Current.Resources[key]=b; }
        ModAppearance.ApplyTheme(dark);
        foreach(Window window in Application.Current.Windows)RefreshPlots(window);
        static void RefreshPlots(DependencyObject node)
        {
            if(node is Plot plot)plot.InvalidateVisual();
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)RefreshPlots(VisualTreeHelper.GetChild(node,i));
        }
    }
}
