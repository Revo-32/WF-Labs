using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record AcquisitionCost(string Id,string Currency,long Amount);
public sealed record AcquisitionRoute(string Kind,string Location,string Detail,AcquisitionCost[] Costs,string Condition,string Source,double? Chance);
public sealed record AcquisitionEntry(string Id,string WikiUrl,long Revision,string Summary,string Original,AcquisitionRoute[] Routes,int? BuiltPlatinum,int? BlueprintCredits);
public sealed record AcquisitionSource(string Url,long Revision,string Timestamp,string Sha256);
public sealed record AcquisitionDocument(int Version,DateTimeOffset RetrievedUtc,AcquisitionEntry[] Entries,AcquisitionSource[] Sources);

public static class InventoryAcquisition
{
    private static readonly Lazy<AcquisitionDocument> document=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.inventory-acquisition.json")!;
        return JsonSerializer.Deserialize<AcquisitionDocument>(stream,Catalog.Json)!;
    });
    private static readonly Lazy<IReadOnlyDictionary<string,AcquisitionEntry>> lookup=new(()=>Document.Entries.ToDictionary(e=>e.Id));
    public static AcquisitionDocument Document=>document.Value;
    public static AcquisitionEntry? Find(string id)=>lookup.Value.GetValueOrDefault(id);
    public static AcquisitionRoute? DirectCredits(string id)=>Find(id)?.Routes.FirstOrDefault(r=>r.Kind=="market"&&r.Detail=="완성품 크레딧 구매"&&r.Costs is [{Currency:"크레딧",Amount:>0}]);
    // Prefer the non-premium acquisition path. A platinum shortcut on a farmable
    // item never hides its mission/quest route or makes it a premium-only item.
    public static int AcquisitionOrder(string id)
    {
        var entry=Find(id);if(entry is null)return 2;
        if(entry.Routes.Any(r=>r.Kind is "market" or "dojo"&&r.Costs is [{Currency:"크레딧",Amount:>0}]))return 0;
        if(entry.Routes.Any(r=>r.Kind is not ("market" or "unavailable" or "internal")||r.Costs.Any(c=>c.Currency!="플래티넘")))return 1;
        if(entry.Routes.Any(r=>r.Kind=="market"&&r.Costs.Any(c=>c.Currency=="플래티넘")))return 2;
        return 3;
    }
    public static string OrderLabel(int order)=>order switch{0=>"① 크레딧 구매",1=>"② 미션·퀘스트·교환",2=>"③ 플래티넘 구매",_=>"현재 획득 제한"};
}
