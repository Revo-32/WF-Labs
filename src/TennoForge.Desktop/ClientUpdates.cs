using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;
using Velopack.Sources;
using WFLabs.Client;

namespace TennoForge.Desktop;

internal enum ClientUpdatePhase { Unavailable, Idle, Checking, Current, Available, Downloading, Ready, Applying, Error }
internal record ClientRelease(string Version,long Bytes,string Notes,UpdateInfo? Plan=null,VelopackAsset? Asset=null);
internal interface IClientUpdateFeed
{
    string CurrentVersion { get; }
    string? UnavailableReason { get; }
    ClientRelease? Pending { get; }
    Task<ClientRelease?> CheckAsync();
    Task DownloadAsync(ClientRelease release,Action<int> progress,CancellationToken cancellation);
    void Apply(ClientRelease release);
}

internal sealed class VelopackUpdateFeed : IClientUpdateFeed
{
    private readonly UpdateManager? manager;
    private readonly string[]? restartArguments;
    public static string AppVersion=>typeof(VelopackUpdateFeed).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"알 수 없음";
    internal record ReleaseConfiguration(string RepositoryUrl="",bool Prerelease=false);
    public string CurrentVersion=>manager?.CurrentVersion?.ToString()??AppVersion;
    public string? UnavailableReason { get; }
    public ClientRelease? Pending=>manager is {IsInstalled:true,IsPortable:false,UpdatePendingRestart:{} asset}?Release(asset):null;
    internal static bool IsRepository(string value)=>Regex.IsMatch(value,@"\Ahttps://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?\z",RegexOptions.CultureInvariant);
    internal VelopackUpdateFeed()
    {
        try
        {
            var config=JsonSerializer.Deserialize<ReleaseConfiguration>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"release-config.json")))??new();
            if(!IsRepository(config.RepositoryUrl)){UnavailableReason="배포 저장소가 연결되지 않았습니다. 연결된 설치형 버전에서 업데이트할 수 있습니다.";return;}
            manager=new UpdateManager(new GithubSource(config.RepositoryUrl,null,config.Prerelease));
            if(!manager.IsInstalled||manager.IsPortable)UnavailableReason="개발·압축 버전입니다. 자동 업데이트는 설치형 버전에서 사용할 수 있습니다.";
        }
        catch(Exception ex){UnavailableReason="업데이트 설정을 읽지 못했습니다. "+ex.Message;}
    }
    // Installation QA uses the same controller and package verification as the client.
    internal VelopackUpdateFeed(UpdateManager manager,string[]? restartArguments=null){this.manager=manager;this.restartArguments=restartArguments;if(!manager.IsInstalled||manager.IsPortable)UnavailableReason="설치형 버전이 아닙니다.";}
    private static ClientRelease Release(VelopackAsset asset,UpdateInfo? plan=null)=>new(asset.Version.ToString(),asset.Size,asset.NotesMarkdown??"",plan,asset);
    public async Task<ClientRelease?> CheckAsync(){var update=await manager!.CheckForUpdatesAsync();return update is null?null:Release(update.TargetFullRelease,update);}
    public Task DownloadAsync(ClientRelease release,Action<int> progress,CancellationToken cancellation)=>manager!.DownloadUpdatesAsync(release.Plan??throw new InvalidOperationException("다운로드할 버전을 다시 확인하세요."),progress,cancellation);
    public void Apply(ClientRelease release)=>manager!.ApplyUpdatesAndRestart(release.Asset??throw new InvalidOperationException("설치할 파일이 없습니다."),restartArguments);
}

// Owned by the application, not by a settings dialog: closing the dialog must not
// lose a download, launch a second check, or restart without an explicit action.
internal sealed class ClientUpdates : IDisposable
{
    private readonly IClientUpdateFeed feed;
    private readonly Func<ClientPreferences> preferences;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? operation;
    private bool disposed;
    internal event Action? Changed;
    internal ClientUpdatePhase Phase { get; private set; }
    internal ClientRelease? Release { get; private set; }
    internal string Status { get; private set; }="업데이트를 확인할 수 있습니다.";
    internal int Progress { get; private set; }
    internal DateTimeOffset? LastChecked { get; private set; }
    internal string CurrentVersion=>feed.CurrentVersion;
    internal bool Busy=>operation is not null||Phase==ClientUpdatePhase.Applying;
    internal ClientUpdates(IClientUpdateFeed feed,Func<ClientPreferences>? preferences=null)
    {
        this.feed=feed;this.preferences=preferences??ClientPreferences.Load;
        try
        {
            if(feed.UnavailableReason is {} reason){Phase=ClientUpdatePhase.Unavailable;Status=reason;}
            else if(feed.Pending is {} pending){Release=pending;Set(ClientUpdatePhase.Ready,"다운로드한 업데이트가 있습니다. 다시 시작하면 설치합니다.");}
            else Phase=ClientUpdatePhase.Idle;
        }
        catch(Exception ex){Phase=ClientUpdatePhase.Error;Status="다운로드한 업데이트를 확인하지 못했습니다. 다시 확인해 주세요.\n"+ex.Message;}
    }
    private void Set(ClientUpdatePhase phase,string status){Phase=phase;Status=status;if(!disposed)Changed?.Invoke();}
    internal async Task CheckAsync(bool automatic=false)
    {
        if(disposed||Busy||Phase is ClientUpdatePhase.Unavailable or ClientUpdatePhase.Ready)return;
        var policy=preferences();if(automatic&&!policy.CheckUpdatesAutomatically)return;
        operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            Set(ClientUpdatePhase.Checking,"새 버전을 확인하고 있습니다…");
            // Velopack owns the request timeout. Keep this operation busy until the
            // request actually ends; cancellation must not create overlapping checks.
            var found=await feed.CheckAsync();operation.Token.ThrowIfCancellationRequested();
            LastChecked=DateTimeOffset.Now;Release=found;
            Set(found is null?ClientUpdatePhase.Current:ClientUpdatePhase.Available,found is null?"현재 최신 버전입니다.":$"{found.Version} 버전을 사용할 수 있습니다.");
        }
        catch(OperationCanceledException){Set(ClientUpdatePhase.Idle,"업데이트 확인을 취소했습니다.");}
        catch(Exception ex){Set(ClientUpdatePhase.Error,"업데이트를 확인하지 못했습니다. 현재 버전은 계속 사용할 수 있습니다.\n"+ex.Message);}
        finally{operation.Dispose();operation=null;if(!disposed)Changed?.Invoke();}
        if(automatic&&preferences().DownloadUpdatesAutomatically&&Phase==ClientUpdatePhase.Available)await DownloadAsync();
    }
    internal async Task DownloadAsync()
    {
        if(disposed||Busy||Release is null||Phase is not (ClientUpdatePhase.Available or ClientUpdatePhase.Error))return;
        var selected=Release;operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token=operation.Token;Progress=0;
        try
        {
            Set(ClientUpdatePhase.Downloading,"업데이트를 다운로드하고 있습니다…");
            IProgress<int> progress=new Progress<int>(value=>{if(!disposed&&!token.IsCancellationRequested&&Phase==ClientUpdatePhase.Downloading){Progress=Math.Clamp(value,0,100);Changed?.Invoke();}});
            await feed.DownloadAsync(selected,progress.Report,token);token.ThrowIfCancellationRequested();
            Progress=100;Set(ClientUpdatePhase.Ready,"다운로드 완료. 현재 작업을 보관하고 다시 시작하면 업데이트됩니다.");
        }
        catch(OperationCanceledException){Set(ClientUpdatePhase.Available,"다운로드를 취소했습니다. 다시 다운로드할 수 있습니다.");}
        catch(Exception ex){Set(ClientUpdatePhase.Error,"다운로드하지 못했습니다. 현재 버전은 계속 사용할 수 있습니다.\n"+ex.Message);}
        finally{operation.Dispose();operation=null;if(!disposed)Changed?.Invoke();}
    }
    internal void Cancel()=>operation?.Cancel();
    internal void Apply(Action saveRecovery,Action stopWatcher)
    {
        if(disposed||Busy||Phase!=ClientUpdatePhase.Ready||Release is null)return;
        try{saveRecovery();stopWatcher();Set(ClientUpdatePhase.Applying,"작업을 보관했습니다. 업데이트를 설치하고 다시 시작합니다…");feed.Apply(Release);}
        catch(Exception ex){Set(ClientUpdatePhase.Ready,"업데이트 적용을 시작하지 못했습니다. 현재 작업을 확인한 뒤 다시 시도하세요.\n"+ex.Message);}
    }
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();operation?.Cancel();lifetime.Dispose();Changed=null;}
}
