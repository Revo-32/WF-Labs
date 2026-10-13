using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;
internal static class NativeWindowCapture
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd,IntPtr dc,uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    internal static bool Save(IntPtr hwnd,int width,int height,string path)
    {
        var source=GetDC(hwnd);var dc=CreateCompatibleDC(source);var bitmap=CreateCompatibleBitmap(source,width,height);var previous=SelectObject(dc,bitmap);
        try
        {
            if(!PrintWindow(hwnd,dc,2))return false;
            var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            // Off-screen WPF windows can return a successful but all-black image.
            var converted=new FormatConvertedBitmap(image,System.Windows.Media.PixelFormats.Bgra32,null,0);
            var pixels=new byte[width*height*4];converted.CopyPixels(pixels,width*4,0);
            if(!Enumerable.Range(0,width*height).Any(i=>pixels[i*4]>32||pixels[i*4+1]>32||pixels[i*4+2]>32))return false;
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);return true;
        }
        finally{SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(hwnd,source);}
    }
}
