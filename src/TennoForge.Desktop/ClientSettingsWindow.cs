using System.Windows;
using System.Windows.Controls;
using WFLabs.Client;

namespace TennoForge.Desktop;
internal sealed class ClientSettingsWindow : Window
{
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,LineHeight=22,Margin=new(0,12,0,12)};
    private readonly TextBlock version=new(){Margin=new(0,0,0,4)},checkedAt=new(){Margin=new(0,4,0,12)};
    private readonly TextBlock notes=new(){TextWrapping=TextWrapping.Wrap,LineHeight=22};
    private readonly Expander releaseNotes=new(){Header="변경 사항",Visibility=Visibility.Collapsed,Margin=new(0,12,0,16)};
    private readonly ProgressBar progress=new(){Minimum=0,Maximum=100,Height=5,Margin=new(0,0,0,12),Visibility=Visibility.Collapsed};
    private readonly Button check=new(){Content="업데이트 확인"},download=new(){Content="다운로드",Margin=new(8,0,0,0),Visibility=Visibility.Collapsed};
    private readonly ClientUpdates updates;
    private readonly bool ownsUpdates;
    private readonly Action saveRecovery;
    internal ClientSettingsWindow(Action saveRecovery,ClientUpdates? updates=null)
    {
        this.saveRecovery=saveRecovery;this.updates=updates??new(new VelopackUpdateFeed());ownsUpdates=updates is null;
        Title="WF Labs · 실행 및 업데이트";Width=560;Height=660;ResizeMode=ResizeMode.NoResize;
        SetResourceReference(FontFamilyProperty,"AppFont");WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid();root.RowDefinitions.Add(new(){Height=new(64)});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var controls=WindowFrame.Controls(this);controls.Children.RemoveAt(1);controls.Children.RemoveAt(0);controls.HorizontalAlignment=HorizontalAlignment.Right;root.Children.Add(controls);
        var panel=new StackPanel{Margin=new(24,8,24,24)};
        var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        panel.Children.Add(new TextBlock{Text="업데이트",FontSize=22,Margin=new(0,0,0,16)});
        panel.Children.Add(version);panel.Children.Add(checkedAt);panel.Children.Add(status);panel.Children.Add(progress);
        progress.SetResourceReference(ProgressBar.ForegroundProperty,"AccentBrush");progress.SetResourceReference(ProgressBar.BackgroundProperty,"StrokeBrush");
        checkedAt.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        var actions=new Grid();actions.ColumnDefinitions.Add(new());actions.ColumnDefinitions.Add(new());actions.Children.Add(check);Grid.SetColumn(download,1);actions.Children.Add(download);panel.Children.Add(actions);
        releaseNotes.Content=new ScrollViewer{Content=notes,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};panel.Children.Add(releaseNotes);
        var current=ClientPreferences.Load();
        var autoCheck=new CheckBox{Content="새 버전 자동 확인",IsChecked=current.CheckUpdatesAutomatically,Margin=new(0,16,0,12)};
        var autoDownload=new CheckBox{Content="업데이트 자동 다운로드",IsChecked=current.DownloadUpdatesAutomatically,Margin=new(0,0,0,12),IsEnabled=current.CheckUpdatesAutomatically};
        autoCheck.Checked+=(_,_)=>autoDownload.IsEnabled=true;autoCheck.Unchecked+=(_,_)=>autoDownload.IsEnabled=false;
        panel.Children.Add(autoCheck);panel.Children.Add(autoDownload);
        panel.Children.Add(new TextBlock{Text="앱 시작 시와 사용 중 4시간마다 확인합니다. 다운로드 후 설치 시점은 직접 선택할 수 있습니다.",TextWrapping=TextWrapping.Wrap,LineHeight=22,Margin=new(0,0,0,20)});
        panel.Children.Add(new TextBlock{Text="자동 실행",FontSize=18,Margin=new(0,0,0,14)});
        var windows=new CheckBox{Content="Windows 로그인 시 WF Labs 실행",IsChecked=current.StartWithWindows,Margin=new(0,0,0,12)};
        var game=new CheckBox{Content="Warframe 실행 시 WF Labs 실행",IsChecked=current.StartWithWarframe,Margin=new(0,0,0,12)};
        panel.Children.Add(windows);panel.Children.Add(game);
        var settingsStatus=new TextBlock{TextWrapping=TextWrapping.Wrap,LineHeight=22,Visibility=Visibility.Collapsed,Margin=new(0,12,0,0)};
        var apply=new Button{Content="설정 저장",Margin=new(0,8,0,0)};panel.Children.Add(apply);panel.Children.Add(settingsStatus);
        apply.Click+=async(_,_)=>
        {
            try
            {
                var selected=ClientPreferences.Load() with {StartWithWindows=windows.IsChecked==true,StartWithWarframe=game.IsChecked==true,CheckUpdatesAutomatically=autoCheck.IsChecked==true,DownloadUpdatesAutomatically=autoDownload.IsChecked==true};
                var old=ClientPreferences.Load();
                if(old.StartWithWindows!=selected.StartWithWindows||old.StartWithWarframe!=selected.StartWithWarframe)ClientIntegration.Configure(selected);else selected.Save();
                settingsStatus.Text="설정을 저장했습니다.";settingsStatus.Visibility=Visibility.Visible;
                await this.updates.CheckAsync(automatic:true);
            }
            catch(Exception ex){settingsStatus.Text="설정을 저장하지 못했습니다: "+ex.Message;settingsStatus.Visibility=Visibility.Visible;}
        };
        check.Click+=async(_,_)=>await this.updates.CheckAsync();
        download.Click+=async(_,_)=>
        {
            if(this.updates.Phase==ClientUpdatePhase.Downloading){this.updates.Cancel();return;}
            if(this.updates.Phase==ClientUpdatePhase.Ready)
            {
                if(ConfirmationWindow.Ask(this,"업데이트 설치","현재 모딩·리벤 작업을 보관하고 업데이트를 설치한 뒤 다시 시작합니다.","설치 후 다시 시작"))this.updates.Apply(this.saveRecovery,ClientIntegration.StopWatcher);
            }
            else await this.updates.DownloadAsync();
        };
        this.updates.Changed+=Refresh;Refresh();
        Closed+=(_,_)=>{this.updates.Changed-=Refresh;if(ownsUpdates)this.updates.Dispose();};
        WindowFrame.Attach(this,root);ResizeMode=ResizeMode.NoResize;
    }
    private void Refresh()
    {
        var state=updates.Phase;
        version.Text="현재 버전 "+updates.CurrentVersion+(updates.Release is {} release?"  →  "+release.Version:"");
        checkedAt.Text=updates.LastChecked is {} time?$"마지막 확인 {time.LocalDateTime:yyyy-MM-dd HH:mm}":"";checkedAt.Visibility=checkedAt.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        status.Text=updates.Status+(state==ClientUpdatePhase.Downloading?$" {updates.Progress}%":"");
        status.SetResourceReference(TextBlock.ForegroundProperty,state==ClientUpdatePhase.Error?"ErrorBrush":"TextBrush");
        progress.Visibility=state==ClientUpdatePhase.Downloading?Visibility.Visible:Visibility.Collapsed;progress.Value=updates.Progress;
        check.IsEnabled=!updates.Busy&&state is not (ClientUpdatePhase.Unavailable or ClientUpdatePhase.Ready);
        download.Visibility=updates.Release is not null?Visibility.Visible:Visibility.Collapsed;
        download.IsEnabled=state is ClientUpdatePhase.Downloading or ClientUpdatePhase.Available or ClientUpdatePhase.Ready or ClientUpdatePhase.Error;
        download.Content=state switch{ClientUpdatePhase.Downloading=>"다운로드 취소",ClientUpdatePhase.Ready=>"설치 후 다시 시작",_=>"다운로드"};
        notes.Text=updates.Release?.Notes??"";releaseNotes.Visibility=notes.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        if(updates.Release is {Bytes:>0} available&&state==ClientUpdatePhase.Available)status.Text+=$"\n다운로드 크기 {available.Bytes/1048576d:N1} MB";
    }
    internal void PreviewForAudit(bool downloading)
    {
        progress.Visibility=downloading?Visibility.Visible:Visibility.Collapsed;progress.Value=63;
        status.Text=downloading?"업데이트 다운로드 중… 63%":"업데이트를 완료하지 못했습니다. 현재 버전은 계속 사용할 수 있습니다.\n"+string.Join("\n",Enumerable.Repeat("연결 상태를 확인하고 다시 시도해 주세요. 다운로드 주소에 접근할 수 없습니다.",12));
    }
}
