using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

// One-shot live validation: explicit hotkey or a reward event in automatic mode.
// Only the already foreground Warframe window is captured. Never activates a window.
internal static class RelicLiveCheck
{
    internal static void Arm(string directory,bool automatic=false)
    {
        var dir=Path.GetFullPath(directory);Directory.CreateDirectory(dir);
        var host=new Window{Title="WF Labs 실전 검사 대기",Width=1,Height=1,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false,Focusable=false};
        var cache=automatic?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","relic-market-cache"):Path.Combine(dir,"market-cache");
        var page=new RelicPage(new RelicMarketClient(cache,snapshotMode:automatic));page.Shortcut.IsChecked=!automatic;
        if(automatic)page.StartPriceRefresh();
        RelicHotkey? hotkey=null;RelicAutoDetector? detector=null;RelicOverlay? overlay=null;bool running=false,finished=false,rewardClosed=false;
        var expiry=new DispatcherTimer{Interval=TimeSpan.FromMinutes(automatic?60:15)};
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,Catalog.Json));
        void Event(string phase)=>File.AppendAllText(Path.Combine(dir,"events.jsonl"),JsonSerializer.Serialize(new{phase,utc=DateTimeOffset.UtcNow})+Environment.NewLine);
        void Finish()
        {
            if(finished)return;finished=true;expiry.Stop();detector?.Dispose();hotkey?.Dispose();if(overlay is {Dismissed:false})overlay.Close();page.Dispose();host.Close();Application.Current.Shutdown(0);
        }
        expiry.Tick+=(_,_)=>{if(!running){Save("expired.json",new{reason=automatic?"60분 동안 보상 진입 이벤트 없음":"15분 동안 사용자 단축키 입력 없음",utc=DateTimeOffset.UtcNow});Finish();}};
        async Task Capture()
        {
            if(running){if(overlay is {Dismissed:false})overlay.Close();return;}
            try
            {
                var game=RelicCapture.GameWindow();
                if(RelicCapture.GetForegroundWindow()!=game){Event("skipped-game-not-foreground");Save("waiting.json",new{reason="워프레임이 이미 전면일 때만 캡처합니다.",utc=DateTimeOffset.UtcNow});return;}
                running=true;expiry.Stop();var total=Stopwatch.StartNew();
                if(automatic){await Task.Delay(800);if(finished||rewardClosed||RelicCapture.GetForegroundWindow()!=game){running=false;expiry.Start();return;}}
                var bounds=RelicCapture.ClientBounds(game);RelicCapture.ValidateGameSize(bounds.Width,bounds.Height);
                var frame=RelicCapture.Capture(game);double captureSeconds=total.Elapsed.TotalSeconds;
                // User-triggered diagnostic evidence, kept locally; not a production autosave.
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame));
                using(var file=File.Create(Path.Combine(dir,"reward-capture.png")))encoder.Save(file);
                Save("captured.json",new{width=frame.PixelWidth,height=frame.PixelHeight,captureSeconds,utc=DateTimeOffset.UtcNow,gameActivated=false});
                overlay=new RelicOverlay(game,page);if(automatic)overlay.Closed+=(_,_)=>page.CancelRecognition();overlay.Present();
                Event(overlay.IsVisible?"overlay-shown":"overlay-not-visible");
                await page.ReadAsync(frame,operation:total,live:true);
                Save("result.json",new{elapsedSeconds=page.LastElapsedSeconds,firstPricedSeconds=page.FirstPricedSeconds,captureSeconds,status=page.Status.Text,recognition=page.LastRecognitionForCheck,
                    cards=page.Cards.Select(c=>new{item=c.Item,quote=c.Quote,state=c.State.Text}),gameStillForeground=RelicCapture.GetForegroundWindow()==game,overlayVisible=!overlay.Dismissed,gameInputsSent=false,imagesUploaded=false});
                // Observe the existing overlay lifetime without activating the main app.
                while(!overlay.Dismissed&&total.Elapsed<TimeSpan.FromSeconds(16))await Task.Delay(100);
                Finish();
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Finish();}
        }
        host.Show();
        if(automatic)
        {
            detector=new RelicAutoDetector(signal=>host.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(finished)return;
                Event("reward-"+signal.ToString().ToLowerInvariant());
                if(signal==RelicRewardSignal.Open&&!running){rewardClosed=false;_=Capture();}
                if(signal==RelicRewardSignal.Close){rewardClosed=true;if(overlay is {Dismissed:false})overlay.Close();page.CancelRecognition();}
            })),message=>host.Dispatcher.BeginInvoke(new Action(()=>{if(!finished)Save("detector.json",new{message,utc=DateTimeOffset.UtcNow});})));
            Save("ready.json",new{ready=true,automatic=true,minimum="1920×1080",foregroundChanges=false,armedUtc=DateTimeOffset.UtcNow});expiry.Start();return;
        }
        hotkey=new RelicHotkey(host,()=>_=Capture());
        if(!hotkey.Enable(true)){Save("ready.json",new{ready=false,reason="Ctrl + Shift + R이 다른 앱에서 사용 중입니다."});Finish();return;}
        Save("ready.json",new{ready=true,hotkey="Ctrl + Shift + R",minimum="1920×1080",captureOnlyOnUserHotkey=true,foregroundChanges=false,armedUtc=DateTimeOffset.UtcNow});expiry.Start();
    }
}
