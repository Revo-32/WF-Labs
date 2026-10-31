using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WFLabs.Client;

namespace TennoForge.Desktop;
internal static class ClientUpdateChecks
{
    internal sealed class Feed : IClientUpdateFeed
    {
        public string CurrentVersion=>"0.8.0-preview.1";
        public string? UnavailableReason { get; init; }
        public ClientRelease? Pending { get; init; }
        public ClientRelease? Next { get; set; }=new("0.8.0-preview.2",82*1048576,"모딩 능력치 표시 개선\n클라이언트 자동 업데이트 추가");
        internal int Checks,Downloads,Applies;
        internal TaskCompletionSource<ClientRelease?>? CheckGate;
        internal TaskCompletionSource? DownloadGate;
        internal bool FailCheck,FailDownload;
        internal List<string> Actions=[];
        public async Task<ClientRelease?> CheckAsync(){Checks++;if(FailCheck)throw new System.IO.IOException("오프라인");return CheckGate is {} gate?await gate.Task:Next;}
        public async Task DownloadAsync(ClientRelease release,Action<int> progress,CancellationToken cancellation)
        {Downloads++;progress(63);if(FailDownload)throw new System.IO.IOException("다운로드 연결 끊김");if(DownloadGate is {} gate)await gate.Task.WaitAsync(cancellation);}
        public void Apply(ClientRelease release){Actions.Add("apply");Applies++;}
    }
    internal static async Task<object> Run(Action<FrameworkElement,string> render)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
        var old=JsonSerializer.Deserialize<ClientPreferences>("{\"StartWithWarframe\":true}")!;
        Yes(old.StartWithWarframe&&old.CheckUpdatesAutomatically&&old.DownloadUpdatesAutomatically,"기존 자동 실행 설정 호환 · 새 업데이트 기본값");
        Yes(VelopackUpdateFeed.IsRepository("https://github.com/Revo-32/WF-Labs")&&!VelopackUpdateFeed.IsRepository("https://github.com.evil.test/a/b")&&!VelopackUpdateFeed.IsRepository("https://github.com/a/b?token=x"),"공개 GitHub 저장소 주소 형식 검사");
        Yes(!VelopackUpdateFeed.IncludePrereleases("1.0.0",true)&&!VelopackUpdateFeed.IncludePrereleases("1.0.0+rc-note",true),"정식판은 잘못된 설정이어도 미리보기 업데이트 제외");
        Yes(VelopackUpdateFeed.IncludePrereleases("1.0.0-rc.1",true)&&!VelopackUpdateFeed.IncludePrereleases("1.0.0-rc.1",false),"미리보기 채널 허용 및 정식 전용 설정 존중");
        var diagnostic=ClientDiagnostics.Capture();
        var diagnosticJson=JsonSerializer.Serialize(diagnostic);
        Yes(!diagnosticJson.Contains(Environment.UserName,StringComparison.OrdinalIgnoreCase)&&!diagnosticJson.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),StringComparison.OrdinalIgnoreCase),"진단 파일에는 사용자 이름·프로필 경로 없음");
        Yes(diagnostic.Components.Length>0&&diagnostic.SchemaVersion==1,"진단 정보 · 앱·런타임·OCR 구성요소 상태");
        var planningCheckFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"WFLabs-planning-update-"+Guid.NewGuid().ToString("N"));
        try{InstalledPlanningCheck.Prepare(planningCheckFolder);Yes(InstalledPlanningCheck.Verify(planningCheckFolder),"개인 목표·예약·오버레이 설정 파일의 새 프로세스 복원 fixture");}
        finally{if(System.IO.Directory.Exists(planningCheckFolder)&&System.IO.Path.GetFullPath(planningCheckFolder).StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))System.IO.Directory.Delete(planningCheckFolder,true);}
        var unavailable=new Feed{UnavailableReason="개발 버전"};using(var u=new ClientUpdates(unavailable)){await u.CheckAsync();Yes(unavailable.Checks==0&&u.Phase==ClientUpdatePhase.Unavailable,"미설치 버전은 업데이트 네트워크 요청 없음");}
        var off=new Feed();using(var u=new ClientUpdates(off,()=>new(CheckUpdatesAutomatically:false))){await u.CheckAsync(true);Yes(off.Checks==0,"자동 확인 해제 시 자동 요청 없음");await u.CheckAsync();Yes(off.Checks==1&&off.Downloads==0,"자동 확인을 꺼도 수동 확인 가능");}
        var latest=new Feed{Next=null};using(var u=new ClientUpdates(latest)){await u.CheckAsync();Yes(u.Phase==ClientUpdatePhase.Current&&u.LastChecked is not null,"최신 버전·마지막 확인 시각 표시");}
        var automatic=new Feed();using(var u=new ClientUpdates(automatic)){await u.CheckAsync(true);Yes(u.Phase==ClientUpdatePhase.Ready&&automatic.Downloads==1&&automatic.Applies==0,"자동 확인·다운로드 후 사용자 재시작 대기");}
        var noDownload=new Feed();using(var u=new ClientUpdates(noDownload,()=>new(DownloadUpdatesAutomatically:false))){await u.CheckAsync(true);Yes(u.Phase==ClientUpdatePhase.Available&&noDownload.Downloads==0,"자동 다운로드 해제 시 알림만 표시");}
        var pending=new Feed{Pending=new("0.8.0-preview.2",82*1048576,"")};using(var u=new ClientUpdates(pending)){await u.CheckAsync();Yes(u.Phase==ClientUpdatePhase.Ready&&pending.Checks==0,"오프라인 재실행에서도 다운로드 완료 상태 복원");}
        var concurrent=new Feed{CheckGate=new()};using(var u=new ClientUpdates(concurrent)){var first=u.CheckAsync();await u.CheckAsync();Yes(concurrent.Checks==1&&u.Busy,"중복 확인 요청 병합");concurrent.CheckGate.SetResult(null);await first;Yes(!u.Busy,"확인 완료 후 버튼 활성 복구");}
        var retry=new Feed{FailCheck=true};using(var u=new ClientUpdates(retry)){await u.CheckAsync();Yes(u.Phase==ClientUpdatePhase.Error&&!u.Busy,"오프라인 오류 후 재시도 가능");retry.FailCheck=false;await u.CheckAsync();retry.FailDownload=true;await u.DownloadAsync();Yes(u.Phase==ClientUpdatePhase.Error&&retry.Applies==0,"다운로드 실패 시 현재 설치본 유지");retry.FailDownload=false;await u.DownloadAsync();Yes(u.Phase==ClientUpdatePhase.Ready,"실패한 다운로드 재시도");}
        var cancel=new Feed{DownloadGate=new()};using(var u=new ClientUpdates(cancel)){await u.CheckAsync();var task=u.DownloadAsync();u.Cancel();await task;Yes(u.Phase==ClientUpdatePhase.Available&&!u.Busy,"다운로드 취소 후 다시 받기 가능");cancel.DownloadGate=null;await u.DownloadAsync();Yes(u.Phase==ClientUpdatePhase.Ready,"취소 뒤 다운로드 완료");}
        var apply=new Feed();using(var u=new ClientUpdates(apply))
        {
            await u.CheckAsync(true);
            u.Apply(()=>throw new System.IO.IOException("디스크 쓰기 실패"),()=>apply.Actions.Add("stop"));
            Yes(apply.Actions.Count==0&&u.Phase==ClientUpdatePhase.Ready,"작업 보관 실패 시 설치·도우미 종료 중단");
            u.Apply(()=>apply.Actions.Add("save"),()=>apply.Actions.Add("stop"));
            Yes(apply.Actions.SequenceEqual(new[]{"save","stop","apply"}),"현재 작업 보관 → 도우미 종료 → 설치 순서");
        }
        var shared=new Feed{DownloadGate=new()};using(var u=new ClientUpdates(shared))
        {
            await u.CheckAsync();var dialog=new ClientSettingsWindow(()=>{},u);render((FrameworkElement)dialog.Content,"update-available");
            var task=u.DownloadAsync();await System.Windows.Threading.Dispatcher.Yield();render((FrameworkElement)dialog.Content,"update-downloading");dialog.Close();
            Yes(u.Phase==ClientUpdatePhase.Downloading&&!task.IsCompleted,"설정창을 닫아도 공유 다운로드 유지");
            shared.DownloadGate.SetResult();await task;
            var reopened=new ClientSettingsWindow(()=>{},u);render((FrameworkElement)reopened.Content,"update-ready");
            var diagnosticPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"WFLabs-diagnostic-check-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                reopened.ChooseDiagnosticFile=()=>diagnosticPath;
                ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(reopened.ExportDiagnostics).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
                await System.Windows.Threading.Dispatcher.Yield();
                var exported=JsonSerializer.Deserialize<ClientDiagnosticReport>(System.IO.File.ReadAllText(diagnosticPath))!;
                Yes(exported.SchemaVersion==1&&exported.Components.Length>0,"실제 진단 저장 버튼 · JSON 파일 생성");
                reopened.ChooseDiagnosticFile=()=>null;var original=System.IO.File.ReadAllText(diagnosticPath);
                ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(reopened.ExportDiagnostics).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();await System.Windows.Threading.Dispatcher.Yield();
                Yes(System.IO.File.ReadAllText(diagnosticPath)==original,"진단 저장 취소 시 기존 파일 보존");
            }
            finally{if(System.IO.File.Exists(diagnosticPath))System.IO.File.Delete(diagnosticPath);}
            Yes(u.Phase==ClientUpdatePhase.Ready&&shared.Downloads==1,"설정창 재진입 시 완료 상태·버전 유지");reopened.Close();
        }
        return new{passed=true,checks};
    }
}

public sealed partial class MainWindow
{
    internal object CheckUpdateNavigation()
    {
        var checks=new List<string>();
        foreach(var select in navigation)
        {
            select.IsChecked=true;UpdateLayout();CheckHeaderLayout();
            if(!IsPresented(updateButton))throw new Exception("업데이트 버튼이 탭에서 사라짐");checks.Add(select.Content+" 탭에서 업데이트 접근");
        }
        var saved=CaptureUpdateWorkspace();comparisonBuild=ReadBuild();ShowRivens();SetDarkTheme(false);SetLightMode(true);
        var workspace=CaptureUpdateWorkspace();var json=JsonSerializer.Serialize(workspace,TennoForge.Core.Catalog.Json);
        comparisonBuild=null;ShowModding();SetDarkTheme(true);SetLightMode(false);
        ApplyUpdateWorkspace(JsonSerializer.Deserialize<UpdateWorkspace>(json,TennoForge.Core.Catalog.Json)!);
        if(comparisonBuild is null||navigation[1].IsChecked!=true||Theme.IsDark||!lightMode)throw new Exception("업데이트 전 작업 상태 복원 실패");
        checks.Add("업데이트 후 모딩 비교 기준·활성 탭·테마·경량 모드 복구");ApplyUpdateWorkspace(saved);
        return new{passed=true,checks};
    }
}
