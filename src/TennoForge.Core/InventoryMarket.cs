using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record InventoryMarketEntry(string GameId,RelicItem Item);
public sealed record InventoryMarketDocument(DateTimeOffset RetrievedUtc,string Source,string SourceSha256,InventoryMarketEntry[] Entries);
public static class InventoryMarket
{
    private static readonly Lazy<InventoryMarketDocument> document=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.inventory-market-items.json")!;
        return JsonSerializer.Deserialize<InventoryMarketDocument>(stream,Catalog.Json)!;
    });
    private static readonly Lazy<IReadOnlyDictionary<string,RelicItem>> lookup=new(()=>Document.Entries.ToDictionary(e=>e.GameId,e=>e.Item));
    public static InventoryMarketDocument Document=>document.Value;
    public static RelicItem[] Items=>Document.Entries.Select(e=>e.Item).DistinctBy(i=>i.Id).ToArray();
    public static RelicItem? Find(string gameId)=>lookup.Value.GetValueOrDefault(gameId);
}
