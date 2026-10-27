using System.Net;
using System.Text;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record WorldScheduleData(WorldScheduleSnapshot? Snapshot,ArbitrationSchedule Arbitrations,bool Cached,string? Notice,DateTimeOffset CheckedUtc)
{
    public bool Fresh(DateTimeOffset now)=>!Cached&&WorldScheduleEngine.Fresh(Snapshot,now);
    public bool CycleFresh(CycleAnchor anchor,DateTimeOffset now)=>!Cached&&(anchor.SourceUtc??Snapshot?.Timestamp) is {} at&&now-at<TimeSpan.FromMinutes(10)&&at-now<TimeSpan.FromMinutes(2);
    public bool ArbitrationConflict(DateTimeOffset now)=>Fresh(now)&&Snapshot?.LiveArbitrationNode is {} id&&Snapshot.LiveArbitrationStart<=now&&now<Snapshot.LiveArbitrationEnd&&Arbitrations.Current(now)?.NodeId!=id;
}
public sealed class WorldScheduleClient : IDisposable
{
    private readonly HttpClient http;private readonly string folder;private readonly ScheduleCatalog catalog;
    private readonly SemaphoreSlim gate=new(1,1);private DateTimeOffset lastAttempt=DateTimeOffset.MinValue,arbitrationFetched;
    private WorldScheduleData current;
    public int RequestCount {get;private set;}
    public WorldScheduleData Current=>current;
    public WorldScheduleClient(string folder,HttpMessageHandler? handler=null)
    {
        this.folder=folder;catalog=ScheduleCatalog.Load();http=handler is null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(8);http.DefaultRequestHeaders.UserAgent.ParseAdd(ClientIdentity.UserAgent);
        var table=ArbitrationSchedule.Bundled(catalog);arbitrationFetched=table.RetrievedUtc;
        try{var p=Path.Combine(folder,"arbitrations.json");if(File.Exists(p)){using var doc=JsonDocument.Parse(File.ReadAllText(p));arbitrationFetched=doc.RootElement.GetProperty("retrievedUtc").GetDateTimeOffset();table=ArbitrationSchedule.Parse(doc.RootElement.GetProperty("csv").GetString()!,arbitrationFetched,"browse.wf · 저장 일정표");}}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or FormatException or KeyNotFoundException){}
        WorldScheduleSnapshot? snapshot=null;
        try{var p=Path.Combine(folder,"worldstate.json");if(File.Exists(p)){using var doc=JsonDocument.Parse(File.ReadAllText(p));snapshot=WorldScheduleEngine.Parse(doc.RootElement);}}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or InvalidOperationException){}
        try{var p=Path.Combine(folder,"official-cycles.json");if(File.Exists(p)){using var doc=JsonDocument.Parse(File.ReadAllText(p));snapshot=WorldScheduleEngine.WithOfficialCycles(snapshot,doc.RootElement);}}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException){}
        current=new(snapshot,table,true,snapshot is null?"주기 정보를 불러오세요. 중재·상점 예상 일정은 먼저 확인할 수 있습니다.":"저장된 월드 상태 · 온라인 갱신 전",DateTimeOffset.MinValue);
    }
    public async Task<WorldScheduleData> RefreshAsync(bool force,CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var now=DateTimeOffset.UtcNow;if(!force&&now-lastAttempt<TimeSpan.FromMinutes(1))return current;lastAttempt=now;
            var snapshot=current.Snapshot;bool cached=true;var notices=new List<string>();
            try
            {
                var raw=await GetAsync("https://api.warframestat.us/pc?language=ko",token);using var doc=JsonDocument.Parse(raw);var next=WorldScheduleEngine.Parse(doc.RootElement);
                if(next.Timestamp>now.AddMinutes(2))throw new InvalidDataException("서버 시각과 PC 시각 차이가 큽니다. PC 시계를 확인하세요.");
                snapshot=next;cached=false;Save("worldstate.json",raw);
                if(!WorldScheduleEngine.Fresh(next,now))notices.Add("주기 API의 기준 시각이 오래되었습니다. 저장 기준 예상으로 표시합니다.");
            }
            catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException or InvalidOperationException or FormatException or KeyNotFoundException)
            {notices.Add("주기 갱신 실패 · 마지막 자료 유지 · "+Reason(e));}
            if(cached||!WorldScheduleEngine.Fresh(snapshot,now)||!FissureAlerts.Fresh(snapshot,now)||!GameAlerts.Fresh(snapshot,now)||snapshot?.Incarnon is {} incarnon&&incarnon.EndUtc<=now)
            {
                try
                {
                    var raw=await GetAsync("https://api.warframe.com/cdn/worldState.php",token);using var doc=JsonDocument.Parse(raw);var next=WorldScheduleEngine.WithOfficialCycles(snapshot,doc.RootElement);
                    if(next.Cycles.Any(a=>a.SourceUtc>now.AddMinutes(2)))throw new InvalidDataException("공식 자료와 PC 시각 차이가 큽니다.");
                    snapshot=next;cached=false;Save("official-cycles.json",raw);notices.Add("평원·캠비온은 공식 자료로 보완 · 다른 지역은 개별 기준 시각 확인");
                }
                catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException or InvalidOperationException or FormatException or KeyNotFoundException)
                {notices.Add("공식 주기 보완 불가 · 마지막 정상 기준 유지");}
            }
            var table=current.Arbitrations;
            if(force||now-arbitrationFetched>=TimeSpan.FromHours(6)||now<arbitrationFetched)
            {
                try
                {
                    var csv=await GetAsync("https://raw.githubusercontent.com/calamity-inc/browse.wf/senpai/arbys.txt",token);
                    var candidate=ArbitrationSchedule.Parse(csv,now,"browse.wf · 갱신 일정표");
                    if(candidate.Current(now) is null)throw new InvalidDataException("중재 일정표에 현재 시간이 포함되지 않았습니다.");
                    table=candidate;arbitrationFetched=now;Save("arbitrations.json",JsonSerializer.Serialize(new{retrievedUtc=now,csv}));
                }
                catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException)
                {notices.Add("중재 일정표 갱신 실패 · 이전 일정 유지");}
            }
            current=new(snapshot,table,cached,notices.Count==0?null:string.Join(" · ",notices),now);return current;
        }
        finally{gate.Release();}
    }
    private async Task<string> GetAsync(string url,CancellationToken token)
    {
        RequestCount++;using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var buffer=new MemoryStream();byte[] bytes=new byte[16384];int read;
        while((read=await stream.ReadAsync(bytes,deadline.Token))>0){if(buffer.Length+read>4*1024*1024)throw new InvalidDataException("일정 응답이 너무 큽니다.");buffer.Write(bytes,0,read);}return Encoding.UTF8.GetString(buffer.ToArray());
    }
    private void Save(string name,string raw)
    {try{Directory.CreateDirectory(folder);var path=Path.Combine(folder,name);var tmp=path+".tmp";File.WriteAllText(tmp,raw);File.Move(tmp,path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}}
    private static string Reason(Exception e)=>e switch{TaskCanceledException=>"응답 시간 초과",HttpRequestException=>"연결 또는 서버 응답 확인 필요",InvalidDataException=>e.Message,JsonException or InvalidOperationException or FormatException or KeyNotFoundException=>"자료 형식이 올바르지 않습니다",_=>"응답 자료를 읽지 못했습니다"};
    public void Dispose()=>http.Dispose();
}
