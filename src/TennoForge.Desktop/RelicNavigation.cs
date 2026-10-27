using System.IO;
using System.Text.Json;
using System.Windows;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private RelicPage? relicPage;
    private RelicHotkey? relicHotkey;
    private bool capturingRelic;
    private RelicOverlay? relicOverlay;
    private RelicAutoDetector? relicAuto;
    private CancellationTokenSource? automaticCapture;
    private bool automaticOverlay;
    private int autoGeneration;
    private string autoStatus="";
    private void EnsureRelicPage()
    {
        if(relicPage is null)
        {
            relicPage=new RelicPage();relicPage.CaptureRequested=CaptureRelicAsync;relicPage.GameReturnRequested=ReturnToGame;relicPage.HotkeyChanged=SetRelicHotkey;
            relicPage.FarmingHint=Farming.Reward;
            relicPage.AutomaticChanged=SetRelicAutomatic;relicPage.AutoStatus.Text=string.IsNullOrWhiteSpace(autoStatus)?relicPage.Automatic.IsChecked==true?"워프레임 보상 화면을 기다립니다.":"자동인식을 켜면 보상 화면에서 시세가 자동으로 표시됩니다.":autoStatus;
            relicPage.Shortcut.IsChecked=relicHotkey?.Registered==true;
            if(UiFonts.PersistChanges&&RelicPreferences.Read().Hotkey&&relicHotkey?.Registered!=true)relicPage.Status.Text="단축키가 다른 프로그램과 충돌했습니다. 게임 화면 읽기를 사용하거나 단축키를 다시 켜세요.";
            if(UiFonts.PersistChanges)_=InitializePrices();
            if(UiFonts.PersistChanges&&Farming.Document.Goals.Any(g=>g.Enabled&&g.Notify)){EnsureSchedulePage();schedulePage!.StartMonitoring();}
            async Task InitializePrices(){await relicPage.RefreshCatalogAsync(false);relicPage.StartPriceRefresh();}
            Closed+=(_,_)=>{relicHotkey?.Dispose();if(relicOverlay is {Dismissed:false})relicOverlay.Close();relicPage.Dispose();};
        }
    }
    internal void ShowRelics()
    {
        EnsureRelicHub();page.Content=relicHub;SelectPage(4);
    }
    private void SetRelicAutomatic(bool enabled)
    {
        autoStatus=enabled?"자동 인식 대기 · 워프레임 보상 화면을 기다립니다.":"자동인식을 켜면 보상 화면에서 시세가 자동으로 표시됩니다.";
        if(relicPage is not null)relicPage.AutoStatus.Text=autoStatus;
        if(!UiFonts.PersistChanges)return;
        var generation=++autoGeneration;relicAuto?.Dispose();relicAuto=null;StopAutomaticCapture();
        (RelicPreferences.Read() with{Automatic=enabled}).Save();
        autoStatus=enabled?"자동 인식 대기 · 워프레임 보상 화면을 기다립니다.":"자동 인식 꺼짐";
        if(relicPage is not null)relicPage.AutoStatus.Text=autoStatus;
        if(!enabled)return;
        relicAuto=new RelicAutoDetector(signal=>Dispatcher.BeginInvoke(new Action(()=>
        {
            if(generation!=autoGeneration)return;
            if(signal==TennoForge.Core.RelicRewardSignal.Close)StopAutomaticCapture();
            else _=CaptureAutomaticRelicAsync();
        })),message=>Dispatcher.BeginInvoke(new Action(()=>
        {
            if(generation!=autoGeneration)return;autoStatus=message;if(relicPage is not null)relicPage.AutoStatus.Text=message;
        })));
    }
    private void StopAutomaticCapture()
    {
        if(automaticCapture is not null){automaticCapture.Cancel();relicPage?.CancelRecognition();}
        if(automaticOverlay&&relicOverlay is {Dismissed:false})relicOverlay.Close();
        automaticOverlay=false;
    }
    private async Task CaptureAutomaticRelicAsync()
    {
        if(capturingRelic||relicPage?.Busy==true||automaticCapture is not null)return;
        using var pending=new CancellationTokenSource(TimeSpan.FromSeconds(18));automaticCapture=pending;
        var operation=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var game=RelicCapture.GameWindow();
            if(RelicCapture.GetForegroundWindow()!=game||RelicCapture.IsMinimized(game))return;
            if(relicOverlay is {Dismissed:false})relicOverlay.Close();
            // Let reward reveal animation settle. Recheck focus immediately before capture.
            await Task.Delay(800,pending.Token);
            if(RelicCapture.GetForegroundWindow()!=game||RelicCapture.IsMinimized(game))return;
            var bounds=RelicCapture.ClientBounds(game);RelicCapture.ValidateGameSize(bounds.Width,bounds.Height);
            EnsureRelicPage();var image=RelicCapture.Capture(game);
            relicOverlay=new RelicOverlay(game,relicPage!);automaticOverlay=true;
            relicOverlay.Closed+=(_,_)=>{if(ReferenceEquals(automaticCapture,pending)){pending.Cancel();relicPage?.CancelRecognition();}};
            relicOverlay.Present();
            using var cancel=pending.Token.Register(()=>Dispatcher.BeginInvoke(new Action(()=>
            {if(ReferenceEquals(automaticCapture,pending))StopAutomaticCapture();})));
            await relicPage!.ReadAsync(image,operation:operation,live:true);
            if(!pending.IsCancellationRequested)relicPage.AutoStatus.Text=$"자동 인식 완료 · 감지 후 {operation.Elapsed.TotalSeconds:0.0}초";
        }
        catch(OperationCanceledException){}
        catch(Exception e){autoStatus="자동 인식 대기 · "+e.Message;if(relicPage is not null)relicPage.AutoStatus.Text=autoStatus;}
        finally{if(ReferenceEquals(automaticCapture,pending))automaticCapture=null;}
    }
    private void SetRelicHotkey(bool enabled)
    {
        if(!UiFonts.PersistChanges)return;
        relicHotkey??=new RelicHotkey(this,()=>_=CaptureRelicAsync());var active=relicHotkey.Enable(enabled);
        if(enabled&&!active){relicPage!.Status.Text="Ctrl + Shift + R을 다른 프로그램이 사용 중입니다. 게임 화면 읽기 버튼을 사용하세요.";relicPage.Shortcut.IsChecked=false;}
        (RelicPreferences.Read() with{Hotkey=active}).Save();
    }
    private void InitializeRelicHotkey()
    {
        if(UiFonts.PersistChanges)
        {
            EnsureRelicPage();
            SetRelicAutomatic(RelicPreferences.Read().Automatic);
            Closed+=(_,_)=>{autoGeneration++;relicAuto?.Dispose();StopAutomaticCapture();};
        }
        if(!UiFonts.PersistChanges||!RelicPreferences.Read().Hotkey)return;
        relicHotkey=new RelicHotkey(this,()=>_=CaptureRelicAsync());relicHotkey.Enable(true);Closed+=(_,_)=>relicHotkey.Dispose();
    }
    private async Task CaptureRelicAsync()
    {
        if(relicOverlay is {Dismissed:false}){relicOverlay.Close();relicOverlay=null;return;}
        if(capturingRelic||relicPage?.Busy==true)return;
        capturingRelic=true;
        automaticOverlay=false;
        var operation=System.Diagnostics.Stopwatch.StartNew();
        relicPage?.PreviewWindowForCheck?.Close();
        var hidden=false;var oldState=WindowState;
        try
        {
            var game=RelicCapture.GameWindow();
            // Construct the page before hiding the main window, without activating it.
            ShowRelicRewards();
            // The pixels are captured before showing WF Labs; never read the foreground browser/desktop instead.
            if(IsVisible){Hide();hidden=true;await Task.Delay(180);}
            if(RelicCapture.IsMinimized(game))RelicCapture.ShowWindow(game,9);
            RelicCapture.SetForegroundWindow(game);await Task.Delay(300);
            var bounds=RelicCapture.ClientBounds(game);RelicCapture.ValidateGameSize(bounds.Width,bounds.Height);
            var image=RelicCapture.Capture(game);
            if(relicPage!.Overlay.IsChecked==true)
            {
                // Restore only a minimized taskbar entry; game input focus stays untouched.
                if(hidden){WindowState=WindowState.Minimized;var activated=ShowActivated;ShowActivated=false;Show();ShowActivated=activated;hidden=false;}
                relicOverlay=new RelicOverlay(game,relicPage);relicOverlay.Present();
                await relicPage.ReadAsync(image,operation:operation,live:true);return;
            }
            if(hidden){Show();WindowState=oldState;}if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
            ShowRelicRewards();Activate();await relicPage!.ReadAsync(image,operation:operation);
        }
        catch(Exception e){if(hidden){Show();WindowState=oldState;}ShowRelicRewards();relicPage!.Status.Text=e.Message;}
        finally{capturingRelic=false;}
    }
    private void ReturnToGame()
    {
        try
        {
            var game=RelicCapture.GameWindow();if(RelicCapture.IsMinimized(game))RelicCapture.ShowWindow(game,9);
            if(!RelicCapture.SetForegroundWindow(game))throw new InvalidOperationException("게임 창으로 전환하지 못했습니다. Alt + Tab으로 워프레임을 선택하세요.");
            WindowState=WindowState.Minimized;
        }
        catch(Exception e){relicPage!.Status.Text=e.Message;}
    }
}
