using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private RivenCaptureWindow? rivenCaptureWindow;
    private RivenCapturePreferences rivenCapturePreferences=RivenCapturePreferences.Read();
    private RelicHotkey? rivenHotkey;
    private RelicAutoDetector? rivenAuto;
    private RivenOverlay? rivenOverlay;
    private RivenOverlay? rivenOverlayRight;
    private readonly DispatcherTimer rivenVisualTimer=new(){Interval=TimeSpan.FromMilliseconds(750)};
    private readonly RivenWatchState rivenWatch=new(640,600);
    private Rect[] rivenTextRegions=[];
    private bool rivenProbing;
    private RivenOcrResult? lastRivenCapture;
    private CancellationTokenSource? rivenCapturePending;
    private readonly DispatcherTimer rivenDebounce=new(){Interval=TimeSpan.FromMilliseconds(900)};
    private string rivenCaptureStatus="";
    private int rivenCaptureGeneration;
    private void InitializeRivenCapture()
    {
        if(!UiFonts.PersistChanges)return;
        rivenHotkey=new RelicHotkey(this,()=>_=CaptureRivenAsync(false),0x5747,0x56);
        rivenDebounce.Tick+=(_,_)=>{rivenDebounce.Stop();_=CaptureRivenAsync(true);};
        rivenVisualTimer.Tick+=async(_,_)=>await ProbeRivenAsync();
        ConfigureRivenCapture(rivenCapturePreferences);
        Closed+=(_,_)=>{rivenCaptureGeneration++;rivenVisualTimer.Stop();rivenDebounce.Stop();rivenAuto?.Dispose();rivenHotkey?.Dispose();rivenCapturePending?.Cancel();CloseRivenOverlay();RelicCapture.ClearForegroundGameCache();};
    }
    private void ConfigureRivenCapture(RivenCapturePreferences preferences)
    {
        rivenCapturePreferences=preferences;
        rivenCaptureStatus=preferences.Automatic?"리벤 화면을 열거나 순환하면 자동으로 갱신합니다.":"자동인식을 켜면 리벤 카드의 등장과 옵션 변경을 감지합니다.";SyncRivenRecognition();
        if(!UiFonts.PersistChanges)return;
        var generation=++rivenCaptureGeneration;
        preferences.Save();rivenCapturePending?.Cancel();rivenAuto?.Dispose();rivenAuto=null;rivenDebounce.Stop();
        rivenVisualTimer.Stop();rivenVisualTimer.Interval=TimeSpan.FromMilliseconds(750);rivenWatch.Reset();rivenTextRegions=[];CloseRivenOverlay();if(preferences.Automatic)rivenVisualTimer.Start();
        var registered=rivenHotkey?.Enable(preferences.Hotkey)==true;
        rivenCaptureStatus=preferences.Automatic?"자동 인식 대기 · 리벤 화면을 열거나 순환하면 자동으로 갱신합니다.":preferences.Hotkey&&!registered?"리벤 단축키가 다른 앱과 충돌했습니다.":"자동 인식 꺼짐";
        SyncRivenRecognition();
        if(preferences.Automatic)rivenAuto=new RelicAutoDetector(_=>{},_=>{},debugOutput:false,observed:line=>
        {
            // Observed in the local game log. No historical replay and no per-frame screen scan.
            if(!IsRivenCaptureSignal(line))return;
            Dispatcher.BeginInvoke(new Action(()=>{if(generation!=rivenCaptureGeneration||!rivenCapturePreferences.Automatic)return;rivenWatch.Retry();}));
        });
    }
    internal static bool IsRivenCaptureSignal(string line)=>line.Contains("Script [Info]: RivenEvolution.lua:",StringComparison.Ordinal);
    private void CloseRivenOverlay(){if(rivenOverlay is {Dismissed:false})rivenOverlay.Close();if(rivenOverlayRight is {Dismissed:false})rivenOverlayRight.Close();}
    private void InvalidateRivenCapture(string message)
    {
        CloseRivenOverlay();rivenTextRegions=[];lastRivenCapture=null;
        if(rivenCaptureWindow is {} window){window.LastResult=null;window.Results.Children.Clear();}
        SetRivenCaptureStatus(message);
    }
    private async Task ProbeRivenAsync()
    {
        if(rivenProbing||rivenCapturePending is not null||!rivenCapturePreferences.Automatic)return;rivenProbing=true;
        try
        {
            var game=RelicCapture.ForegroundGameWindow();if(game==IntPtr.Zero){rivenVisualTimer.Interval=TimeSpan.FromMilliseconds(750);CloseRivenOverlay();rivenWatch.Reset();rivenTextRegions=[];SetRivenCaptureStatus("게임 전경 대기 · 워프레임이 앞에 있을 때 자동으로 읽습니다.");return;}
            var frame=RelicCapture.Capture(game,640);var probe=RivenVisualProbe.Analyze(frame,rivenTextRegions);
            // Expanded-card changes get two independent observations sooner;
            // idle screens keep the existing low-rate probe. Never queue OCR
            // while another request is active (guarded at this method's entry).
            rivenVisualTimer.Interval=TimeSpan.FromMilliseconds(probe.Regions.Length>0?400:750);
            // Use the existing stability wait to prepare weights once. No new
            // capture, full-screen inference, GPU use or per-tick work queue.
            if(probe.Regions.Length>0)_=RivenOcr.PrepareAsync();
            if(probe.Regions.Length==0)SetRivenCaptureStatus("리벤 카드 대기 · 자동 인식 켜짐");
            var action=rivenWatch.Observe(probe.Regions.Length>0,probe.Signature,Environment.TickCount64,allowInitialRead:true);
            RivenPerformance.Event("probe",new{candidate=probe.Regions.Length>0,action=action.ToString()});
            switch(action)
            {
                case RivenWatchAction.Read:await CaptureRivenAsync(true,probe,frame);break;
                case RivenWatchAction.Clear:InvalidateRivenCapture(probe.Regions.Length>0?"카드 변경 감지 · 새 리벤을 읽습니다.":"리벤 카드 대기 · 자동 인식 켜짐");break;
            }
        }
        catch(Exception ex)when(ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        {CloseRivenOverlay();rivenWatch.Reset();rivenTextRegions=[];SetRivenCaptureStatus(ex.Message);}
        finally{rivenProbing=false;}
    }
    internal void ShowRivenCapture(string kind)
    {
        if(rivenCaptureWindow is not null){rivenCaptureWindow.Show();rivenCaptureWindow.Activate();return;}
        var window=new RivenCaptureWindow(rivenCapturePreferences){Owner=this};rivenCaptureWindow=window;
        window.SettingsChanged=ConfigureRivenCapture;
        window.ImportRequested=report=>
        {
            if(!report.Valid)return;
            OpenRivenClass(report.Kind);
            if(report.Kind=="Melee")ApplyRivenWorkbench(RivenScan.Melee(report));
            else rangedRivenPages[report.Kind=="Pistol"?"secondary":"primary"].Set(RivenScan.Ranged(report));
            window.Close();
        };
        window.Closed+=(_,_)=>rivenCaptureWindow=null;
        if(lastRivenCapture is {} last)window.SetResult(last);
        if(lastRivenCapture is null&&rivenCaptureStatus.Length>0||rivenCaptureStatus.Contains("충돌"))window.Status.Text=rivenCaptureStatus;
        window.Show();
    }
    private void SetRivenCaptureStatus(string message)
    {
        if(rivenCaptureStatus==message)return;rivenCaptureStatus=message;SyncRivenRecognition();
        if(rivenCaptureWindow is {} window)window.Status.Text=message;
        if(!UiFonts.PersistChanges)return;
        try
        {
            var path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","riven-capture-status.json");
            System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,message,automatic=rivenCapturePreferences.Automatic}));
        }
        catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
    }
    private async Task CaptureRivenAsync(bool automatic,RivenProbe? probe=null,BitmapSource? preview=null)
    {
        using var requestScope=RivenPerformance.Measure("request",new{automatic});
        if(!automatic)rivenDebounce.Stop();
        rivenCapturePending?.Cancel();using var pending=new CancellationTokenSource();rivenCapturePending=pending;
        try
        {
            var game=RelicCapture.GameWindow();if(RelicCapture.GetForegroundWindow()!=game||RelicCapture.IsMinimized(game))return;
            // Side panels stay outside the card ROI. Rechecking must not destroy
            // native windows or present old values as newly confirmed results.
            if(!automatic)CloseRivenOverlay();
            else{rivenOverlay?.SetPending(true);rivenOverlayRight?.SetPending(true);}
            SetRivenCaptureStatus("리벤 옵션을 읽고 있습니다…");
            await Task.Delay(80,pending.Token);var bounds=RelicCapture.ClientBounds(game);RelicCapture.ValidateGameSize(bounds.Width,bounds.Height);
            var image=RivenPerformance.Time("capture",()=>RelicCapture.Capture(game));var preferences=rivenCapturePreferences;
            var result=probe is null?await RivenOcr.ReadAsync(image,preferences.ReadKind,preferences.Rank,pending.Token):await RivenOcr.ReadAutomaticAsync(image,probe.Regions,preferences.ReadKind,preferences.Rank,pending.Token);
            var cropBounds=result.SourceCrop??RivenVisualProbe.CropBounds(probe?.Regions??[]);
            RivenPerformance.Event("ocr-result",new{result.Seconds,cards=result.Cards.Select(c=>new{c.Corroborated,c.Report.Valid,options=c.Report.Options.Select(o=>new{o.Key,o.Value,o.Negative})})});
            if(pending.IsCancellationRequested||RelicCapture.GetForegroundWindow()!=game)return;
            if(automatic&&probe is not null)
            {
                var textRegions=result.Cards.Select(c=>new Rect(cropBounds.X+c.Bounds.X*cropBounds.Width,cropBounds.Y+c.Bounds.Y*cropBounds.Height,c.Bounds.Width*cropBounds.Width,c.Bounds.Height*cropBounds.Height)).ToArray();
                var current=RivenVisualProbe.Analyze(RelicCapture.Capture(game,640),textRegions);
                var before=preview is null?probe:RivenVisualProbe.Analyze(preview,textRegions);
                // Compare the recognized text, never the surrounding reveal animation.
                if(current.Regions.Length==0){rivenWatch.Reset();InvalidateRivenCapture("리벤 카드 대기 · 자동 인식 켜짐");return;}
                if(textRegions.Length>0&&RivenWatchState.AlignedDifference(before.Signature,current.Signature,640)>.15)
                {RivenPerformance.Event("discard-changed");rivenWatch.Reset();rivenTextRegions=[];SetRivenCaptureStatus("옵션 변경 감지 · 새 화면을 다시 읽습니다.");return;}
                rivenTextRegions=textRegions;rivenWatch.Prime(current.Signature,Environment.TickCount64);
                if(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated))rivenWatch.Retry();
            }
            lastRivenCapture=result;rivenCaptureWindow?.SetResult(result);
            SetRivenCaptureStatus(result.Cards.Length==0?"리벤 옵션 판독 실패 · 카드가 보이는 상태에서 다시 감지합니다.":$"리벤 {result.Cards.Length}개 · {result.Seconds:0.0}초 · "+(result.Cards.All(c=>c.Corroborated&&c.Report.Valid)?"인식 완료":"인식 내용 확인 필요"));
            if(automatic&&result.Cards.Length==0){rivenOverlay?.SetPending(true);rivenOverlayRight?.SetPending(true);return;}
            if(rivenOverlay is {Dismissed:false}&&rivenOverlayRight is {Dismissed:false})
            {rivenOverlay.UpdateResult(result);rivenOverlayRight.UpdateResult(result);}
            else{CloseRivenOverlay();rivenOverlay=new RivenOverlay(game,result,automatic:automatic);rivenOverlayRight=new RivenOverlay(game,result,right:true,automatic:automatic);rivenOverlay.Present();rivenOverlayRight.Present();}
            RivenPerformance.Event("presented");
        }
        catch(OperationCanceledException){}
        catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.IO.IOException or Tesseract.TesseractException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        {SetRivenCaptureStatus(ex.Message);rivenWatch.Retry();}
        finally{if(ReferenceEquals(rivenCapturePending,pending))rivenCapturePending=null;}
    }
}
