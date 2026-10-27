using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tesseract;

namespace TennoForge.Desktop;

internal static class OcrPixels
{
    // Two OCR readers are the concurrency limit. Bound retained byte buffers
    // instead of growing the shared per-core pool after many image sizes.
    private static readonly ArrayPool<byte> Buffers=ArrayPool<byte>.Create(2*1024*1024,2);
    internal static Pix Create(BitmapSource source)
    {
        // A reference switch is used only by paired regression measurements.
        // Other pixel formats retain the existing decoder and color semantics.
        // Fractional DPI goes through WIC's existing PNG pHYs rounding. Runtime
        // OCR masks use integral 300 DPI, so they retain the direct fast path.
        if(source.Format!=PixelFormats.Gray8||source.DpiX!=Math.Truncate(source.DpiX)||source.DpiY!=Math.Truncate(source.DpiY)||
            Environment.GetEnvironmentVariable("WF_LABS_OCR_PIX")=="png")return FromPng(source);
        int width=source.PixelWidth,height=source.PixelHeight,stride=checked((width+3)&~3),length=checked(stride*height);
        var buffer=Buffers.Rent(length);Pix? pix=null;
        try
        {
            source.CopyPixels(buffer,stride,0);
            if(stride!=width)for(int y=0;y<height;y++)Array.Fill(buffer,(byte)255,y*stride+width,stride-width);
            pix=Pix.Create(width,height,8);var data=pix.GetData();
            if(data.WordsPerLine*4!=stride)throw new InvalidOperationException("OCR 픽셀 행 크기가 일치하지 않습니다.");
            Marshal.Copy(buffer,0,data.Data,length);
            // Leptonica stores the leftmost byte in each word's most significant
            // position. Convert raster bytes exactly once on little-endian x64.
            data.EndianByteSwap();
            pix.XRes=(int)Math.Floor(source.DpiX+.5);pix.YRes=(int)Math.Floor(source.DpiY+.5);
            return pix;
        }
        catch{pix?.Dispose();throw;}
        finally{Array.Clear(buffer);Buffers.Return(buffer);}
    }
    internal static Pix FromPng(BitmapSource source)=>Pix.LoadFromMemory(Png(source));
    internal static byte[] Png(BitmapSource source)
    {
        using var buffer=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));encoder.Save(buffer);return buffer.ToArray();
    }
}
