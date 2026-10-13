using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private ClientUpdates? clientUpdates;
    private Button updateButton=null!;
    private DispatcherTimer? updateTimer;
    private CancellationTokenSource? updateLifetime;
    private ClientUpdates Updates
    {
        get
        {
            if(clientUpdates is null){clientUpdates=new(new VelopackUpdateFeed());clientUpdates.Changed+=RefreshUpdateBadge;}
            return clientUpdates;
        }
    }
    private void OpenClientSettings(){settingsPopup.IsOpen=false;new ClientSettingsWindow(SaveUpdateRecovery,Updates){Owner=this}.ShowDialog();}
    private void RefreshUpdateBadge()
    {
        if(clientUpdates is null)return;
        updateButton.Content=clientUpdates.Phase switch{ClientUpdatePhase.Available=>"새 업데이트",ClientUpdatePhase.Downloading=>$"업데이트 {clientUpdates.Progress}%",ClientUpdatePhase.Ready=>"업데이트 준비됨",_=>"업데이트"};
        updateButton.ToolTip=clientUpdates.Status;
        updateButton.SetResourceReference(Control.ForegroundProperty,clientUpdates.Phase is ClientUpdatePhase.Available or ClientUpdatePhase.Downloading or ClientUpdatePhase.Ready?"AccentBrush":"TextBrush");
    }
    internal void StartUpdateChecks()
    {
        if(updateLifetime is not null)return;
        updateLifetime=new();var token=updateLifetime.Token;
        updateTimer=new(){Interval=TimeSpan.FromHours(4)};
        updateTimer.Tick+=async(_,_)=>await Updates.CheckAsync(automatic:true);updateTimer.Start();
        Closed+=(_,_)=>{updateLifetime.Cancel();updateTimer.Stop();clientUpdates?.Dispose();updateLifetime.Dispose();};
        _=InitialCheck();
        async Task InitialCheck(){try{await Task.Delay(TimeSpan.FromSeconds(3),token);await Updates.CheckAsync(automatic:true);}catch(OperationCanceledException){}}
    }
}
