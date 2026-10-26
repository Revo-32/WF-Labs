using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

// Explicit diagnostic invocation only. No window, input, activation or upload.
internal static class RivenProbeLiveCheck
{
    internal static async Task Once(string dir)
    {
        var game=RelicCapture.GameWindow();var image=RelicCapture.Capture(game);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(dir,"game.png")))encoder.Save(stream);
        var preview=RelicCapture.Capture(game,640);var probe=RivenVisualProbe.Analyze(preview);
        var result=await RivenOcr.ReadAsync(RivenVisualProbe.Crop(image,probe.Regions),"Melee",8,default);
        File.WriteAllText(Path.Combine(dir,"recognition.json"),JsonSerializer.Serialize(result,Catalog.Json));
    }
    internal static async Task<int> Replay(string input,string dir)
    {
        var source=RelicOcr.Load(Path.Combine(input,"game.png"));
        // Production starts on the second native preview, not a WPF-rescaled still.
        var probe=RivenVisualProbe.Analyze(RelicOcr.Load(Path.Combine(input,"preview-1.png")));
        var result=await RivenOcr.ReadAutomaticAsync(source,probe.Regions,"Melee",8,default);
        File.WriteAllText(Path.Combine(dir,"recognition.json"),JsonSerializer.Serialize(result,Catalog.Json));
        var crop=result.SourceCrop??RivenVisualProbe.CropBounds(probe.Regions);
        var known=result.Cards.Select(c=>new System.Windows.Rect(crop.X+c.Bounds.X*crop.Width,crop.Y+c.Bounds.Y*crop.Height,c.Bounds.Width*crop.Width,c.Bounds.Height*crop.Height)).ToArray();
        var state=new RivenWatchState(640);byte[]? last=null;var frames=new List<object>();bool primed=false;int reads=0;
        for(int i=0;i<16;i++)
        {
            var image=RelicOcr.Load(Path.Combine(input,$"preview-{i}.png"));var p=RivenVisualProbe.Analyze(image,primed?known:null);
            var action=state.Observe(p.Regions.Length>0,p.Signature,i*750,true);
            frames.Add(new{i,p.Regions,action=action.ToString(),difference=last is null?0:RivenWatchState.Difference(last,p.Signature)});last=p.Signature;
            if(action==RivenWatchAction.Read){reads++;primed=true;state.Prime(RivenVisualProbe.Analyze(image,known).Signature,i*750);}
        }
        File.WriteAllText(Path.Combine(dir,"frames.json"),JsonSerializer.Serialize(frames,Catalog.Json));
        if(result.Cards.Length!=1||!result.Cards[0].Corroborated||!result.Cards[0].Report.Options.Select(o=>o.Value).SequenceEqual(new[]{118.8,108.1,106.4,100d}))throw new InvalidOperationException("실전 연속 화면의 네 옵션 인식 불일치");
        if(reads!=1)throw new InvalidOperationException($"실전 연속 화면 최초 인식 1회 필요: {reads}회");
        return reads;
    }
    internal static async Task Run(string dir)
    {
        var watch=new RivenWatchState(640);byte[]? previous=null;BitmapSource? first=null;
        void SaveImage(BitmapSource image,string file){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(dir,file));encoder.Save(stream);}
        for(int i=0;i<16;i++)
        {
            var game=RelicCapture.GameWindow();bool foreground=RelicCapture.GetForegroundWindow()==game;
            if(foreground&&!RelicCapture.IsMinimized(game))
            {
                var preview=RelicCapture.Capture(game,640);var probe=RivenVisualProbe.Analyze(preview);
                var action=watch.Observe(probe.Regions.Length>0,probe.Signature,Environment.TickCount64);
                File.AppendAllText(Path.Combine(dir,"frames.jsonl"),JsonSerializer.Serialize(new{i,foreground,bounds=RelicCapture.ClientBounds(game),probe.Regions,action=action.ToString(),difference=previous is null?0:RivenWatchState.Difference(previous,probe.Signature)})+Environment.NewLine);
                previous=probe.Signature;SaveImage(preview,$"preview-{i}.png");
                if(first is null){first=RelicCapture.Capture(game);SaveImage(first,"game.png");}
            }
            else File.AppendAllText(Path.Combine(dir,"frames.jsonl"),JsonSerializer.Serialize(new{i,foreground})+Environment.NewLine);
            await Task.Delay(750);
        }
        if(first is not null)
        {
            var probe=RivenVisualProbe.Analyze(first);File.WriteAllText(Path.Combine(dir,"full-probe.json"),JsonSerializer.Serialize(new{probe.Regions},Catalog.Json));
            var result=await RivenOcr.ReadAsync(first,"Melee",8,default);File.WriteAllText(Path.Combine(dir,"recognition.json"),JsonSerializer.Serialize(result,Catalog.Json));
        }
    }
}
