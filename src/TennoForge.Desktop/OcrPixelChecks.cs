using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tesseract;

namespace TennoForge.Desktop;

internal static class OcrPixelChecks
{
    internal static object Run()
    {
        var checks=new List<string>();
        void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        using(var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(OcrModels.DirectoryPath,"runtime-models.json"))))
        {
            foreach(var model in metadata.RootElement.GetProperty("models").EnumerateObject())
            {
                using var bytes=File.OpenRead(Path.Combine(OcrModels.DirectoryPath,model.Name));
                Yes(Convert.ToHexString(SHA256.HashData(bytes)).Equals(model.Value.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"배포 OCR 모델 해시 · "+model.Name);
            }
            Yes(File.Exists(Path.Combine(OcrModels.DirectoryPath,"MODEL-MODIFICATIONS.txt")),"OCR 런타임 모델 변경·원본·라이선스 고지 존재");
        }
        foreach(int width in new[]{1,2,3,4,5,31,319,320,321,1921})foreach(int height in new[]{1,3,49})foreach(double dpi in new[]{96d,96.5,144,144.5,150.5,300,300.5})
        {
            var bytes=Enumerable.Range(0,width*height).Select(i=>(byte)(i*73+i/width*41)).ToArray();
            var image=BitmapSource.Create(width,height,dpi,dpi,PixelFormats.Gray8,null,bytes,width);image.Freeze();
            using var reference=OcrPixels.FromPng(image);using var direct=OcrPixels.Create(image);
            Yes(reference.Width==direct.Width&&reference.Height==direct.Height&&reference.Depth==direct.Depth&&reference.XRes==direct.XRes&&reference.YRes==direct.YRes,$"픽셀 크기·DPI {width}×{height} / {dpi}");
            byte[] Raster(Pix pix){var data=pix.GetData();int stride=data.WordsPerLine*4;var raw=new byte[stride*height];Marshal.Copy(data.Data,raw,0,raw.Length);return Enumerable.Range(0,width*height).Select(i=>raw[(i/width)*stride+((i%width)&~3)+(3-((i%width)&3))]).ToArray();}
            Yes(Raster(direct).SequenceEqual(bytes)&&Raster(reference).SequenceEqual(bytes),$"모든 유효 픽셀·행 경계 동일 {width}×{height} / {dpi}");
        }
        var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())
        {dc.DrawRectangle(Brushes.White,null,new(0,0,820,110));dc.DrawText(new FormattedText("스티아낙스 프라임 섀시 설계도\n+118.8% 상태 이상 지속 시간",CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),28,Brushes.Black,1),new(14,10));}
        var rendered=new RenderTargetBitmap(820,110,96,96,PixelFormats.Pbgra32);rendered.Render(drawing);
        var gray=new FormatConvertedBitmap(rendered,PixelFormats.Gray8,null,0);
        using var engine1=new TesseractEngine(OcrModels.DirectoryPath,OcrModels.Korean,EngineMode.LstmOnly);
        using var engine2=new TesseractEngine(OcrModels.DirectoryPath,OcrModels.Korean,EngineMode.LstmOnly);
        using var pngPix=OcrPixels.FromPng(gray);using var rawPix=OcrPixels.Create(gray);
        using var a=engine1.Process(pngPix,PageSegMode.SingleBlock);using var b=engine2.Process(rawPix,PageSegMode.SingleBlock);
        Yes(!string.IsNullOrWhiteSpace(a.GetText())&&a.GetText()==b.GetText()&&a.GetMeanConfidence()==b.GetMeanConfidence(),"동일 한국어·수치 원문과 OCR 신뢰도: PNG 대비 직접 전송");
        using var colorReference=OcrPixels.FromPng(rendered);using var colorFallback=OcrPixels.Create(rendered);
        Yes(colorReference.Depth==colorFallback.Depth&&colorReference.XRes==colorFallback.XRes,"컬러 형식은 기존 PNG 경로 유지");
        return new{passed=true,checks=checks.Count,details=checks,text=b.GetText(),confidence=b.GetMeanConfidence()};
    }
}
