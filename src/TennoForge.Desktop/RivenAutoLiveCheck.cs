using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    // Uses the actual production handler. Main window is never shown/activated;
    // only the same no-activate, click-through side overlays can appear.
    internal async Task CheckRivenAutoLive(string dir)
    {
        rivenCapturePreferences=rivenCapturePreferences with{Automatic=true};
        try
        {
            int foregroundFrames=0;
            for(int i=0;i<160&&foregroundFrames<24;i++)
            {
                if(RelicCapture.GetForegroundWindow()==RelicCapture.GameWindow())foregroundFrames++;
                await ProbeRivenAsync();
                File.AppendAllText(Path.Combine(dir,"events.jsonl"),JsonSerializer.Serialize(new{i,status=rivenCaptureStatus,left=rivenOverlay is {IsVisible:true,Dismissed:false},right=rivenOverlayRight is {IsVisible:true,Dismissed:false}})+Environment.NewLine);
                if(lastRivenCapture is {} result)
                {
                    File.WriteAllText(Path.Combine(dir,"recognition.json"),JsonSerializer.Serialize(result,Catalog.Json));
                    if(rivenOverlay is {IsVisible:true,Dismissed:false}&&rivenOverlayRight is {IsVisible:true,Dismissed:false}&&!File.Exists(Path.Combine(dir,"overlay.png")))
                    {
                        var image=RelicCapture.Capture(RelicCapture.GameWindow());var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(dir,"overlay.png"));encoder.Save(stream);
                    }
                }
                await Task.Delay(750);
            }
        }
        finally{CloseRivenOverlay();}
    }
}
