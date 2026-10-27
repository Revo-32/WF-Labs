using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record FarmingBundlePart(string GameId,long Count,RelicItem Item);
public sealed record FarmingBundle(string ResultId,RelicItem Item,FarmingBundlePart[] Parts);
public sealed record FarmingMarketDocument(int SchemaVersion,DateTimeOffset RetrievedUtc,string Source,FarmingBundle[] Bundles);
public static class FarmingMarket
{
    private static readonly Lazy<FarmingMarketDocument> bundled=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.inventory-market-bundles.json")!;
        var doc=JsonSerializer.Deserialize<FarmingMarketDocument>(stream,Catalog.Json)??throw new InvalidDataException("세트 구성 자료를 읽지 못했습니다.");
        if(doc.SchemaVersion!=1||doc.Bundles.Any(b=>b.Parts.Length==0||b.Parts.Any(p=>p.Count<=0)||b.Parts.Select(p=>p.GameId).Distinct().Count()!=b.Parts.Length))throw new InvalidDataException("세트 구성 수량을 확인하세요.");
        return doc;
    });
    public static FarmingMarketDocument Document=>bundled.Value;
    private static readonly Lazy<Dictionary<string,RelicItem>> parts=new(()=>Document.Bundles.SelectMany(b=>b.Parts).GroupBy(p=>p.GameId).ToDictionary(g=>g.Key,g=>g.First().Item));
    public static RelicItem? Part(string gameId)=>InventoryMarket.Find(gameId)??parts.Value.GetValueOrDefault(gameId);
    public static string[] GameIds(string marketId)=>InventoryMarket.Document.Entries.Where(e=>e.Item.Id==marketId).Select(e=>e.GameId)
        .Concat(Document.Bundles.SelectMany(b=>b.Parts).Where(p=>p.Item.Id==marketId).Select(p=>p.GameId)).Distinct().ToArray();
}
public sealed record FarmingPurchaseLine(RelicItem Item,int Quantity,decimal Cost,bool Bundle,RecipeIngredient[] Contents);
public sealed record FarmingPurchaseComparison(decimal? PartsCost,decimal? BestCost,FarmingPurchaseLine[] Lines,RecipeIngredient[] ExtraItems,string[] MissingPriceIds,bool Stale,int States)
{
    public decimal? Savings=>PartsCost.HasValue&&BestCost.HasValue?PartsCost.Value-BestCost.Value:null;
}
public static class FarmingPurchases
{
    private sealed record Offer(RelicItem Item,decimal Price,long[] Contents,bool Bundle,RecipeIngredient[] AllParts,bool Stale);
    private sealed record State(long[] Remaining,decimal Cost,int[] Counts);
    // Exact minimum cash outlay among the provided, frozen quotes. Owned parts
    // are already reserved; surplus resale is never subtracted from the price.
    public static FarmingPurchaseComparison Compare(IReadOnlyDictionary<string,long> missing,IReadOnlyDictionary<string,PlannerPrice> prices,
        FarmingBundle[] bundles,Func<string,RelicItem?> part,CancellationToken cancellation=default)
    {
        if(missing.Any(p=>p.Value<0)||prices.Values.Any(p=>!double.IsFinite(p.Platinum)||p.Platinum<=0))throw new ArgumentException("구매 수량·호가를 확인하세요.");
        var ids=missing.Where(p=>p.Value>0&&part(p.Key) is not null).Select(p=>p.Key).Order(StringComparer.Ordinal).ToArray();
        if(ids.Length==0)return new(0,0,[],[],[],false,0);
        var required=ids.Select(i=>missing[i]).ToArray();var index=ids.Select((id,i)=>(id,i)).ToDictionary(x=>x.id,x=>x.i);
        var offers=new List<Offer>();var missingQuotes=new HashSet<string>();var singles=new Dictionary<int,int>();
        for(int i=0;i<ids.Length;i++)
        {
            var item=part(ids[i])!;
            if(!prices.TryGetValue(ids[i],out var quote)){missingQuotes.Add(item.Id);continue;}
            var counts=new long[ids.Length];counts[i]=1;singles[i]=offers.Count;
            offers.Add(new(item,(decimal)quote.Platinum,counts,false,[new(ids[i],1)],quote.Stale));
        }
        foreach(var bundle in bundles.Where(b=>b.Parts.Any(p=>index.ContainsKey(p.GameId))).DistinctBy(b=>b.Item.Id))
        {
            if(bundle.Parts.Any(p=>p.Count<=0)||bundle.Parts.Select(p=>p.GameId).Distinct().Count()!=bundle.Parts.Length)throw new ArgumentException("세트 구성 수량을 확인하세요.");
            if(!prices.TryGetValue(bundle.Item.Id,out var quote)){missingQuotes.Add(bundle.Item.Id);continue;}
            var counts=new long[ids.Length];foreach(var p in bundle.Parts)if(index.TryGetValue(p.GameId,out int i))counts[i]=p.Count;
            offers.Add(new(bundle.Item,(decimal)quote.Platinum,counts,true,bundle.Parts.Select(p=>new RecipeIngredient(p.GameId,p.Count)).ToArray(),quote.Stale));
        }
        decimal? partsCost=singles.Count==ids.Length?Enumerable.Range(0,ids.Length).Sum(i=>required[i]*offers[singles[i]].Price):null;
        if(Enumerable.Range(0,ids.Length).Any(i=>!offers.Any(o=>o.Contents[i]>0)))return new(partsCost,null,[],[],missingQuotes.ToArray(),false,0);
        // Sets only link a few parts. Solve disconnected components separately so
        // unrelated goals do not form a combinatorial Cartesian product.
        var parent=Enumerable.Range(0,ids.Length).ToArray();int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        foreach(var offer in offers){var used=Enumerable.Range(0,ids.Length).Where(i=>offer.Contents[i]>0).ToArray();foreach(var i in used.Skip(1))parent[Find(i)]=Find(used[0]);}
        var totalCounts=new int[offers.Count];int states=0;
        foreach(var component in Enumerable.Range(0,ids.Length).GroupBy(Find))
        {
            cancellation.ThrowIfCancellationRequested();var positions=component.ToArray();var usable=Enumerable.Range(0,offers.Count).Where(o=>positions.Any(i=>offers[o].Contents[i]>0)).ToArray();
            var start=positions.Select(i=>required[i]).ToArray();
            // Every offer can pay its cost across the units it supplies. These
            // rounded-down unit weights form a feasible lower bound for A*.
            var weights=positions.Select(i=>Math.Max(0,decimal.Floor(usable.Where(o=>offers[o].Contents[i]>0).Min(o=>offers[o].Price/positions.Sum(j=>offers[o].Contents[j]))*100000000m)/100000000m-.00000001m)).ToArray();
            decimal Lower(long[] remaining)=>remaining.Select((n,i)=>n*weights[i]).Sum();
            string Key(long[] remaining)=>string.Join(',',remaining);
            var queue=new PriorityQueue<State,decimal>();var best=new Dictionary<string,decimal>{{Key(start),0}};
            queue.Enqueue(new(start,0,new int[usable.Length]),Lower(start));State? found=null;
            while(queue.TryDequeue(out var state,out _))
            {
                if((++states&63)==0)cancellation.ThrowIfCancellationRequested();
                if(best[Key(state.Remaining)]<state.Cost)continue;
                if(state.Remaining.All(n=>n==0)){found=state;break;}
                for(int o=0;o<usable.Length;o++)
                {
                    var offer=offers[usable[o]];var next=state.Remaining.Select((n,i)=>Math.Max(0,n-offer.Contents[positions[i]])).ToArray();
                    if(next.SequenceEqual(state.Remaining))continue;decimal cost=state.Cost+offer.Price;string key=Key(next);
                    if(best.TryGetValue(key,out var previous)&&previous<=cost)continue;
                    best[key]=cost;var counts=(int[])state.Counts.Clone();counts[o]=checked(counts[o]+1);queue.Enqueue(new(next,cost,counts),cost+Lower(next));
                }
            }
            if(found is null)throw new InvalidOperationException("구매 경로를 확인하지 못했습니다.");
            for(int o=0;o<usable.Length;o++)totalCounts[usable[o]]=checked(totalCounts[usable[o]]+found.Counts[o]);
        }
        var lines=offers.Select((o,i)=>(Offer:o,Count:totalCounts[i])).Where(x=>x.Count>0).Select(x=>new FarmingPurchaseLine(x.Offer.Item,x.Count,x.Count*x.Offer.Price,x.Offer.Bundle,x.Offer.AllParts)).ToArray();
        var purchased=new Dictionary<string,long>();foreach(var line in lines)foreach(var p in line.Contents)purchased[p.Id]=checked(purchased.GetValueOrDefault(p.Id)+p.Count*line.Quantity);
        var extra=purchased.Where(p=>p.Value>missing.GetValueOrDefault(p.Key)).Select(p=>new RecipeIngredient(p.Key,p.Value-missing.GetValueOrDefault(p.Key))).ToArray();
        return new(partsCost,lines.Sum(l=>l.Cost),lines,extra,missingQuotes.ToArray(),offers.Where((o,i)=>totalCounts[i]>0).Any(o=>o.Stale),states);
    }
}
