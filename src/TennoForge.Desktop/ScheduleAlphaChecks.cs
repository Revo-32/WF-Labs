using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class ScheduleAlphaChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);var phases=new List<object>();var checks=new List<string>();var preferences=new SchedulePreferences{Width=420,Height=440,BackgroundOpacity=.5};
        void Yes(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        var window=new ScheduleOverlay(preferences,p=>preferences=p);var data=new ScheduleOverlayData([new("test","다음 S티어 중재","감청 · 목성 / Callisto","00:42:10","예상",true)],[],false,"","");
        async Task Phase(string name)
        {
            window.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.Render);await Task.Delay(80);var h=new WindowInteropHelper(window).Handle;var source=HwndSource.FromHwnd(h);
            phases.Add(new{name,clear=source.CompositionTarget.BackgroundColor.ToString(),alpha=source.CompositionTarget.BackgroundColor.A,style=RelicOverlay.GetStyle(h,-20).ToInt64(),background=window.Background.ToString(),window.AllowsTransparency,window.Editing});
            File.WriteAllText(Path.Combine(directory,"surface.json"),JsonSerializer.Serialize(phases,Catalog.Json));
            Yes(source.CompositionTarget.BackgroundColor.A==0,name+" · 실제 HWND 합성 바탕 alpha 0");Yes((RelicOverlay.GetStyle(h,-20).ToInt64()&0x80000)!=0,name+" · WS_EX_LAYERED 유지");
        }
        try
        {
            window.Update("12:34:56",data,preferences);window.PresentAt(new(20,20,1600,1000));await Phase("initial");
            for(int i=0;i<6;i++){Theme.Apply(i%2==0);window.BeginEdit(new(20,20,1600,1000));await Phase("edit-"+i);window.Width=420+i*2;window.Height=440+i*2;await Phase("resize-"+i);window.FinishEdit();window.Update("12:34:56",data,preferences);window.PresentAt(new(20,20,1600,1000));await Phase("normal-"+i);window.Conceal();window.PresentAt(new(20,20,1600,1000));await Phase("reshow-"+i);}
            foreach(double opacity in new[]{0d,.25,.5,.82,1d})
            {
                preferences=preferences with{BackgroundOpacity=opacity};window.Update("12:34:56",data,preferences);await Phase("opacity-"+opacity);var content=(FrameworkElement)window.Content;content.UpdateLayout();
                int width=(int)Math.Ceiling(content.ActualWidth),height=(int)Math.Ceiling(content.ActualHeight);var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
                var bg=Pixel(bitmap,width-24,height-24);Yes(Math.Abs(bg[3]-opacity*255)<=1,$"투명도 {(1-opacity)*100:0}% · 배경 픽셀 alpha");
                byte[] glyph=new byte[160*38*4];bitmap.CopyPixels(new Int32Rect(12,8,160,38),glyph,160*4,0);Yes(Enumerable.Range(0,glyph.Length/4).Any(i=>glyph[i*4+3]>=250),$"투명도 {(1-opacity)*100:0}% · 글자 불투명도 유지");
                if(opacity is not (0 or .5 or 1))continue;
                foreach(var (name,color) in new[]{("bright",Color.FromRgb(238,242,247)),("dark",Color.FromRgb(22,31,44))})
                {
                    var composite=new DrawingVisual();using(var dc=composite.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(color),null,new Rect(0,0,width,height));dc.DrawImage(bitmap,new Rect(0,0,width,height));}
                    var target=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);target.Render(composite);var actual=Pixel(target,width-24,height-24);
                    var expected=new[]{19*opacity+color.B*(1-opacity),17*opacity+color.G*(1-opacity),15*opacity+color.R*(1-opacity)};
                    Yes(Enumerable.Range(0,3).All(i=>Math.Abs(actual[i]-expected[i])<=2),$"{name} 바탕 · 투명도 {(1-opacity)*100:0}% 합성 색");Save(target,Path.Combine(directory,$"overlay-{name}-transparency-{(1-opacity)*100:0}.png"));
                }
            }
            File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=Array.Empty<string>(),checks,nativeSurfaceVerified=true,displayedDesktopCapture=false},Catalog.Json));
        }
        finally{window.Close();}
    }
    private static byte[] Pixel(BitmapSource source,int x,int y){byte[] value=new byte[4];source.CopyPixels(new Int32Rect(x,y,1,1),value,4,0);return value;}
    private static void Save(BitmapSource image,string path){var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(image));using var s=File.Create(path);e.Save(s);}
}
