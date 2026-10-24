using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

internal static class TextSmoothingChecks
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var results=new List<object>();
        foreach(var font in UiFonts.Choices)foreach(var scale in new[]{1d,1.25,1.5})
        {
            UiFonts.Apply(font.Id);
            var grid=new Grid{Background=new SolidColorBrush(Color.FromRgb(16,17,18))};
            for(int i=0;i<2;i++)grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
            var modes=new[]{(TextFormattingMode.Display,TextRenderingMode.Auto),(TextFormattingMode.Ideal,TextRenderingMode.ClearType)};
            for(int i=0;i<modes.Length;i++)
            {
                var (format,mode)=modes[i];
                var column=new StackPanel{Margin=new(16)};Grid.SetColumn(column,i);grid.Children.Add(column);
                column.Children.Add(new TextBlock{Text=font.Label+"\n"+(i==0?"기존 · 픽셀에 맞춘 윤곽":"수정 · 부드러운 윤곽"),FontSize=16,Margin=new(0,0,0,20)});
                foreach(var weight in new[]{FontWeights.Normal,FontWeights.SemiBold})
                {
                    var sample=new TextBlock{Text="근접 피해        최소 176.097%\n치명타 확률    최대 215.23%\n상태이상 확률    184.8%\n콤보 지속 시간    8.661초",FontSize=15,LineHeight=34,FontWeight=weight,Margin=new(0,0,0,20)};
                    TextOptions.SetTextFormattingMode(sample,format);TextOptions.SetTextRenderingMode(sample,mode);column.Children.Add(sample);
                }
            }
            App.Render(grid,Path.Combine(folder,$"compare-{font.Id}-{scale}.png"),820,440,scale);
            var counts=new Dictionary<string,int>();
            foreach(var mode in new[]{TextRenderingMode.Auto,TextRenderingMode.Aliased,TextRenderingMode.Grayscale,TextRenderingMode.ClearType})
                foreach(var format in new[]{TextFormattingMode.Display,TextFormattingMode.Ideal})
                {
                    var sample=new TextBlock{Text="근접 피해 176.097%",Foreground=Brushes.White,FontSize=15,Margin=new(8)};
                    TextOptions.SetTextRenderingMode(sample,mode);TextOptions.SetTextFormattingMode(sample,format);
                    var root=new Border{Background=Brushes.Black,Child=sample};
                    VisualTreeHelper.SetRootDpi(root,new(scale,scale));root.Measure(new(300,48));root.Arrange(new(0,0,300,48));root.UpdateLayout();
                    var bitmap=new RenderTargetBitmap((int)(300*scale),(int)(48*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(root);
                    var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
                    var intermediate=0;for(int p=0;p<pixels.Length;p+=4)if(pixels[p]>0&&pixels[p]<255)intermediate++;
                    counts[$"{format}/{mode}"]=intermediate;
                }
            
            if(scale==1 && (counts["Display/Aliased"]!=0||counts["Ideal/ClearType"]<=counts["Display/Auto"]+100))throw new Exception(font.Label+": 자연스러운 글자 윤곽 검증 실패");
            var control=new TextBox();var label=new TextBlock();var rootForDefaults=new StackPanel();rootForDefaults.Children.Add(control);rootForDefaults.Children.Add(label);rootForDefaults.Measure(new(300,100));rootForDefaults.Arrange(new(0,0,300,100));
            foreach(var element in new DependencyObject[]{control,label})if(TextOptions.GetTextFormattingMode(element)!=TextFormattingMode.Ideal||TextOptions.GetTextRenderingMode(element)!=TextRenderingMode.ClearType)throw new Exception("본문·입력창의 기본 렌더링 설정이 일치하지 않습니다.");
            results.Add(new{font=font.Id,scale,intermediatePixels=counts});
        }
        File.WriteAllText(Path.Combine(folder,"smoothing.json"),JsonSerializer.Serialize(new{passed=true,results},TennoForge.Core.Catalog.Json));
    }
}
