using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

internal static class RivenFooterPreparationChecks
{
    internal static async Task<object> RunAsync()
    {
        var reports=new List<object>();
        var prepare=typeof(RivenFooterAnchor).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!;
        var run=typeof(RivenFooterAnchor).GetMethod("FindPreparedAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var (w,h,dpi,format) in new[]{(1920,1080,96d,PixelFormats.Bgra32),(3440,1440,120d,PixelFormats.Bgra32),
            (3840,2160,300d,PixelFormats.Bgra32),(511,137,144d,PixelFormats.Pbgra32),(513,138,96d,PixelFormats.Bgr24)})
        {
            int channels=format==PixelFormats.Bgr24?3:4;var sourcePixels=new byte[w*h*channels];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int i=(y*w+x)*channels;byte alpha=(byte)(64+(x*11+y*17)%192);
                sourcePixels[i]=(byte)((x*17+y*11)%256);sourcePixels[i+1]=(byte)((x*7+y*23)%256);sourcePixels[i+2]=(byte)((x*13+y*3)%256);
                if(channels==4)sourcePixels[i+3]=format==PixelFormats.Pbgra32?alpha:(byte)255;
                if(format==PixelFormats.Pbgra32)for(int c=0;c<3;c++)sourcePixels[i+c]=(byte)(sourcePixels[i+c]*alpha/255);
            }
            var source=BitmapSource.Create(w,h,dpi,dpi,format,null,sourcePixels,w*channels);source.Freeze();Array.Clear(sourcePixels);
            await Verify(source,$"{w}x{h}-{dpi}-{format}",checkPixelTransfer:true);
        }
        // These fixtures contain real MR footers. Only locate rectangles;
        // this check never performs OCR, screen capture or native input.
        foreach(string name in new[]{"innodem-real.png","reroll-pair.png"})
            await Verify(RelicOcr.Load(Path.Combine(Environment.CurrentDirectory,"tests","fixtures","riven",name)),name,checkPixelTransfer:true);
        return new{passed=true,checks=reports.Count,fixtures=reports,screenCapture=false,nativeInput=false};

        async Task Verify(BitmapSource source,string name,bool checkPixelTransfer)
        {
            // Frozen reference of the old async ownership boundary: full BGRA
            // copy, 96 DPI detached image, then 2560px scale and another copy.
            var bgra=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
            var full=new byte[bgra.PixelWidth*bgra.PixelHeight*4];bgra.CopyPixels(full,bgra.PixelWidth*4,0);
            var detached=BitmapSource.Create(bgra.PixelWidth,bgra.PixelHeight,96,96,PixelFormats.Bgra32,null,full,bgra.PixelWidth*4);detached.Freeze();Array.Clear(full);
            double scale=Math.Min(1,2560d/detached.PixelWidth);
            var legacy=new FormatConvertedBitmap(scale<1?new TransformedBitmap(detached,new ScaleTransform(scale,scale)):detached,PixelFormats.Bgra32,null,0);
            var expectedPixels=new byte[legacy.PixelWidth*legacy.PixelHeight*4];legacy.CopyPixels(expectedPixels,legacy.PixelWidth*4,0);
            var actual=prepare.Invoke(null,[new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0)])!;
            var type=actual.GetType();int actualWidth=(int)type.GetProperty("Width")!.GetValue(actual)!,actualHeight=(int)type.GetProperty("Height")!.GetValue(actual)!;
            var actualPixels=(byte[])type.GetProperty("Pixels")!.GetValue(actual)!;
            try
            {
                if(checkPixelTransfer&&(actualWidth!=legacy.PixelWidth||actualHeight!=legacy.PixelHeight||!actualPixels.SequenceEqual(expectedPixels)))
                    throw new InvalidOperationException(name+" MR 준비 픽셀·배율 불일치");
                var expected=RivenFooterAnchor.Find(legacy);var found=await (Task<Rect[]>)run.Invoke(null,[actual,CancellationToken.None])!;
                if(!expected.SequenceEqual(found))throw new InvalidOperationException(name+" MR 좌표·정렬 불일치");
                if(actualPixels.Any(v=>v!=0))throw new InvalidOperationException(name+" MR 준비 픽셀 종료 후 미삭제");
                var cancelled=prepare.Invoke(null,[new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0)])!;
                var cancelledPixels=(byte[])type.GetProperty("Pixels")!.GetValue(cancelled)!;
                try
                {
                    try{await (Task<Rect[]>)run.Invoke(null,[cancelled,new CancellationToken(true)])!;throw new InvalidOperationException(name+" MR 취소 누락");}
                    catch(OperationCanceledException){}
                    if(cancelledPixels.Any(v=>v!=0))throw new InvalidOperationException(name+" MR 취소 후 픽셀 미삭제");
                }
                finally{Array.Clear(cancelledPixels);}
                reports.Add(new{name,width=actualWidth,height=actualHeight,sourceDpi=source.DpiX,sourceFormat=source.Format.ToString(),pixelsEqual=true,rectangles=found,clearedAfterUse=true,clearedAfterCancellation=true});
            }
            finally{Array.Clear(expectedPixels);Array.Clear(actualPixels);}
        }
    }
}
