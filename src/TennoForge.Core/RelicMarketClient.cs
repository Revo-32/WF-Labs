using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TennoForge.Core;

// Read-only v2 integration. A single gate also covers catalog refresh, so all callers share the rate budget.
public sealed class RelicMarketClient : IDisposable
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static DateTimeOffset nextRequest;
    private readonly HttpClient http;
    private readonly string folder;
    private readonly ConcurrentDictionary<string,MarketQuote> memory=new();
    private readonly ConcurrentDictionary<string,SemaphoreSlim> itemGates=new();
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    public bool SnapshotMode{get;}
    private int requestCount;
    public int RequestCount=>Volatile.Read(ref requestCount);
    // Another page may have atomically replaced the shared disk snapshot.
    // Forget this reader's copies without issuing an API request.
    public void ReloadSavedQuotes()=>memory.Clear();
    public static readonly TimeSpan SnapshotInterval=TimeSpan.FromHours(12);
    public RelicMarketClient(string cacheFolder,HttpMessageHandler? handler=null,bool snapshotMode=false)
    {
        SnapshotMode=snapshotMode;
        folder=cacheFolder;http=handler is null?new HttpClient():new HttpClient(handler);
        http.Timeout=TimeSpan.FromSeconds(5);http.BaseAddress=new("https://api.warframe.market/v2/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd(ClientIdentity.UserAgent);
        http.DefaultRequestHeaders.Add("Language","ko");http.DefaultRequestHeaders.Add("Platform","pc");
    }
    public async Task<(RelicCatalog Catalog,string? Notice)> CatalogAsync(bool refresh,CancellationToken token)
    {
        var fallback=RelicCatalog.Bundled();var path=Path.Combine(folder,"items.json");
        try {if(File.Exists(path)){using var cache=JsonDocument.Parse(await File.ReadAllTextAsync(path,token));fallback=RelicCatalog.Parse(cache.RootElement,cache.RootElement.GetProperty("retrievedUtc").GetDateTimeOffset());}}
        catch(Exception e) when(e is IOException or JsonException or InvalidDataException or KeyNotFoundException or FormatException){ }
        if(!refresh&&DateTimeOffset.UtcNow-fallback.RetrievedUtc<TimeSpan.FromHours(24))return(fallback,null);
        try
        {
            using var doc=await GetAsync("items",false,token);var now=DateTimeOffset.UtcNow;var catalog=RelicCatalog.Parse(doc.RootElement,now);
            await SaveAsync(path,JsonSerializer.Serialize(new{retrievedUtc=now,data=doc.RootElement.GetProperty("data")}),token);
            return(catalog,null);
        }
        catch(Exception e) when(IsRecoverable(e,token)) {return(fallback,$"품목 갱신 실패 · {fallback.RetrievedUtc.LocalDateTime:yyyy-MM-dd} 저장 목록 사용 · {Message(e)}");}
    }
    public async Task<MarketQuote> QuoteAsync(RelicItem item,MarketPresence presence,bool crossplay,bool refresh,CancellationToken token)
    {
        if(!item.Tradable)throw new ArgumentException("거래 불가 품목은 마켓을 조회하지 않습니다.");
        var key=Key(item,presence,crossplay);
        var requested=DateTimeOffset.UtcNow;var itemGate=itemGates.GetOrAdd(key,_=>new(1,1));await itemGate.WaitAsync(token);
        try
        {
            if(memory.TryGetValue(key,out var recent)&&recent.FetchedUtc>=requested)refresh=false;
            return await QuoteCoreAsync(item,presence,crossplay,refresh,key,token);
        }
        finally{itemGate.Release();}
    }
    private async Task<MarketQuote> QuoteCoreAsync(RelicItem item,MarketPresence presence,bool crossplay,bool refresh,string key,CancellationToken token)
    {
        var path=Path.Combine(folder,key+".json");
        memory.TryGetValue(key,out var saved);
        if(saved is null)try{if(File.Exists(path))saved=JsonSerializer.Deserialize<MarketQuote>(await File.ReadAllTextAsync(path,token),Json);}catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){ }
        if(saved is not null&&(saved.ItemId!=item.Id||saved.Presence!=presence||saved.Crossplay!=crossplay||saved.FetchedUtc>DateTimeOffset.UtcNow.AddMinutes(1)))saved=null;
        if(SnapshotMode&&!refresh)
            return saved is not null?saved with{Cached=true,Snapshot=true,Stale=DateTimeOffset.UtcNow-saved.FetchedUtc>=SnapshotInterval}
                :new(item.Id,DateTimeOffset.UtcNow,presence,crossplay,null,null,null,0,0,Error:"저장 시세 준비 중 · 백그라운드 갱신 후 표시됩니다.",Snapshot:true);
        if(!refresh&&saved is not null&&DateTimeOffset.UtcNow-saved.FetchedUtc<TimeSpan.FromSeconds(90))return saved with{Cached=true};
        try
        {
            using var doc=await GetAsync("orders/item/"+Uri.EscapeDataString(item.Slug),crossplay,token);
            var quote=RelicMarketMath.Summarize(doc.RootElement,item.Id,DateTimeOffset.UtcNow,presence,crossplay,item.Charges,item.MarketRank);
            memory[key]=quote;await SaveAsync(path,JsonSerializer.Serialize(quote),token);return quote;
        }
        catch(Exception e) when(IsRecoverable(e,token))
        {
            return saved is not null&&DateTimeOffset.UtcNow-saved.FetchedUtc<TimeSpan.FromDays(1)
                ?saved with{Cached=true,Stale=true,Error=Message(e)}
                :new(item.Id,DateTimeOffset.UtcNow,presence,crossplay,null,null,null,0,0,Error:Message(e));
        }
    }
    private static string Key(RelicItem item,MarketPresence presence,bool crossplay)=>Uri.EscapeDataString(item.Id)+"-"+presence+"-"+crossplay+"-"+item.Charges+"-"+item.MarketRank;
    public async Task<bool> SnapshotDueAsync(RelicItem item,bool crossplay,CancellationToken token)
    {
        foreach(var presence in new[]{MarketPresence.InGame,MarketPresence.Online})
        {
            var key=Key(item,presence,crossplay);memory.TryGetValue(key,out var saved);
            try{if(saved is null&&File.Exists(Path.Combine(folder,key+".json")))saved=JsonSerializer.Deserialize<MarketQuote>(await File.ReadAllTextAsync(Path.Combine(folder,key+".json"),token),Json);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}
            if(saved is null||saved.ItemId!=item.Id||saved.Presence!=presence||saved.Crossplay!=crossplay||saved.Error is not null||saved.FetchedUtc>DateTimeOffset.UtcNow.AddMinutes(1)||DateTimeOffset.UtcNow-saved.FetchedUtc>=SnapshotInterval)return true;
        }
        return false;
    }
    public async Task RefreshSnapshotItemAsync(RelicItem item,bool crossplay,CancellationToken token)
    {
        if(!item.Tradable)return;
        using var doc=await GetAsync("orders/item/"+Uri.EscapeDataString(item.Slug),crossplay,token);
        var now=DateTimeOffset.UtcNow;
        // One order response supplies both presence filters, not two API calls.
        foreach(var presence in new[]{MarketPresence.InGame,MarketPresence.Online})
        {
            var quote=RelicMarketMath.Summarize(doc.RootElement,item.Id,now,presence,crossplay,item.Charges,item.MarketRank);
            var key=Key(item,presence,crossplay);memory[key]=quote;
            await SaveAsync(Path.Combine(folder,key+".json"),JsonSerializer.Serialize(quote),token);
        }
    }
    public async Task<RelicItem[]> PriceItemsAsync(CancellationToken token)
    {
        var path=Path.Combine(folder,"items.json");
        // The bundled OCR catalog deliberately excludes sets. Its recent timestamp
        // must not prevent the first full manifest download for price preparation.
        var (catalog,_)=await CatalogAsync(!File.Exists(path),token);var items=catalog.Items.Where(i=>i.Tradable).ToList();
        // The full /items manifest also contains Prime sets, which are useful to
        // the future mastery cost planner but must never enter reward OCR matching.
        if(!File.Exists(path))throw new InvalidOperationException("전체 프라임 품목 목록을 받지 못했습니다. 저장된 시세를 유지하며 다시 시도합니다.");
        try
        {
            if(File.Exists(path))
            {
                using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(path,token));
                foreach(var item in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    if(!item.TryGetProperty("tags",out var tags)||!tags.EnumerateArray().Any(t=>t.GetString()=="prime"))continue;
                    var names=item.GetProperty("i18n");var en=names.GetProperty("en").GetProperty("name").GetString()!;
                    var ko=names.TryGetProperty("ko",out var korean)?korean.GetProperty("name").GetString():en;
                    items.Add(new(item.GetProperty("id").GetString()!,item.GetProperty("slug").GetString()!,ko??en,en,item.TryGetProperty("ducats",out var d)?d.GetInt32():0));
                }
            }
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException){}
        // Goal sets (including non-Prime crafted equipment) share the same
        // 12-hour store. Append them after reward items; OCR never awaits this.
        items.AddRange(FarmingMarket.Document.Bundles.Select(b=>b.Item));
        items.AddRange(FarmingMarket.Document.Bundles.SelectMany(b=>b.Parts).Select(p=>p.Item));
        return items.DistinctBy(i=>i.Id).ToArray();
    }
    private async Task<JsonDocument> GetAsync(string path,bool crossplay,CancellationToken token)
    {
        // Rate-limit request starts, not response completion. Slow responses must not
        // serialize all four rewards. A single deadline also covers the response body.
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));var requestToken=deadline.Token;
        using var req=new HttpRequestMessage(HttpMethod.Get,path);req.Headers.Add("Crossplay",crossplay?"true":"false");
        Task<HttpResponseMessage> pending;
        await Gate.WaitAsync(requestToken);
        try
        {
            var wait=nextRequest-DateTimeOffset.UtcNow;
            if(wait>TimeSpan.FromSeconds(3))throw new HttpRequestException($"조회 제한 중 · {Math.Ceiling(wait.TotalSeconds)}초 후 다시 시도하세요.");
            if(wait>TimeSpan.Zero)await Task.Delay(wait,requestToken);
            nextRequest=DateTimeOffset.UtcNow.AddMilliseconds(400);
            Interlocked.Increment(ref requestCount);
            pending=http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,requestToken);
        }
        finally{Gate.Release();}
        using var response=await pending;
        if(response.StatusCode==HttpStatusCode.TooManyRequests||(int)response.StatusCode==509)
        {
            var retry=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow)??TimeSpan.FromSeconds(30);
            await Gate.WaitAsync(requestToken);
            try{var until=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(Math.Clamp(retry.TotalSeconds,1,300));if(until>nextRequest)nextRequest=until;}
            finally{Gate.Release();}
            throw new HttpRequestException("마켓 조회 제한 · 잠시 후 새로고침하세요.");
        }
        response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync(requestToken);if(bytes.Length>16_000_000)throw new InvalidDataException("마켓 응답이 너무 큽니다.");
        var doc=JsonDocument.Parse(bytes);
        if(!doc.RootElement.TryGetProperty("data",out var data)||data.ValueKind!=JsonValueKind.Array){doc.Dispose();throw new InvalidDataException("마켓 응답 형식을 확인할 수 없습니다.");}
        return doc;
    }
    private static bool IsRecoverable(Exception e,CancellationToken token)=>!token.IsCancellationRequested&&e is HttpRequestException or IOException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException or TaskCanceledException;
    private static string Message(Exception e)=>e is TaskCanceledException?"연결 시간 초과":e is HttpRequestException h&&h.StatusCode is not null?$"마켓 연결 실패 · HTTP {(int)h.StatusCode}":e is HttpRequestException?e.Message:"응답을 읽지 못했습니다.";
    private static async Task SaveAsync(string path,string json,CancellationToken token)
    {
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(temp,json,token);File.Move(temp,path,true);}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){ }
        finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}}
    }
    public void Dispose()=>http.Dispose();
}
