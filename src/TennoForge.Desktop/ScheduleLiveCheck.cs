using System.IO;
using System.Text.Json;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal static class ScheduleLiveCheck
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);var catalog=ScheduleCatalog.Load();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(55));using var client=new WorldScheduleClient(Path.Combine(directory,"cache"));using var vendors=new VendorReportClient();
        var data=await client.RefreshAsync(true,stop.Token);var now=DateTimeOffset.UtcNow;var next=data.Arbitrations.NextS(catalog,now);var reports=await Task.WhenAll(new[]{"tenet","coda"}.Select(s=>vendors.FetchAsync(catalog,s,now,stop.Token)));
        var result=new{alertsFresh=GameAlerts.Fresh(data.Snapshot,now),alertsSource=data.Snapshot?.AlertsSource,alertsUtc=data.Snapshot?.AlertsUtc,alerts=data.Snapshot?.Alerts.Where(a=>a.Active(now)).ToArray(),fissuresFresh=FissureAlerts.Fresh(data.Snapshot,now),fissuresSource=data.Snapshot?.FissuresSource,fissuresUtc=data.Snapshot?.FissuresUtc,fissures=data.Snapshot?.Fissures.Where(f=>f.Active(now)).ToArray(),checkedUtc=now,data.Cached,data.Notice,requests=client.RequestCount,sourceUtc=data.Snapshot?.Timestamp,fresh=data.Fresh(now),cycleFreshness=data.Snapshot?.Cycles.Select(c=>new{c.Id,c.Source,c.SourceUtc,fresh=data.CycleFresh(c,now)}),conflict=data.ArbitrationConflict(now),nextS=next is null?null:new{node=catalog.Nodes.Single(n=>n.Id==next.NodeId),start=next.StartUtc,kst=WorldScheduleEngine.KoreaTime(next.StartUtc),remaining=WorldScheduleEngine.Countdown(next.StartUtc,now)},cycles=data.Snapshot?.Cycles.Select(c=>WorldScheduleEngine.CycleAt(c,now)),shops=new[]{WorldScheduleEngine.ShopAt(catalog,"tenet",now),WorldScheduleEngine.ShopAt(catalog,"coda",now)},reports};
        File.WriteAllText(Path.Combine(directory,"live.json"),JsonSerializer.Serialize(result,Catalog.Json));
        File.WriteAllText(Path.Combine(directory,"incarnon-live.json"),JsonSerializer.Serialize(new{checkedUtc=now,data.Cached,anchor=data.Snapshot?.Incarnon,current=IncarnonRotations.Current(now,data.Snapshot?.Incarnon),upcoming=IncarnonRotations.Upcoming(now,8,data.Snapshot?.Incarnon)},Catalog.Json));
        if(data.Snapshot is null||data.Cached||data.Snapshot.Cycles.Length!=5||next is null)throw new InvalidDataException("온라인 주기·예측 일정 검수 실패. live.json을 확인하세요.");
    }
}
