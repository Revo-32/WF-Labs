using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record InventoryItem(string Id,string Name,string EnglishName,string Category,bool Masterable,int PointsPerRank,int MaxRank,int MasteryRequired,bool GildRequired,bool Tradable,int? BlueprintCredits,string? MarketId);
public sealed record RecipeIngredient(string Id,long Count);
public sealed record InventoryRecipe(string BlueprintId,string ResultId,long Credits,int Seconds,int Quantity,bool ConsumeBlueprint,RecipeIngredient[] Ingredients);
public sealed record InventoryRelicReward(string Id,string Rarity,int Count);
public sealed record InventoryRelic(string Id,string Family,string Name,int Refinement,InventoryRelicReward[] Rewards);
public sealed record InventoryCatalog(int SchemaVersion,DateTimeOffset RetrievedUtc,InventoryItem[] Items,InventoryRecipe[] Recipes,InventoryRelic[] Relics)
{
    public static InventoryCatalog Load()
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.inventory-catalog.json")!;
        var catalog=JsonSerializer.Deserialize<InventoryCatalog>(s,Catalog.Json)!;
        return catalog with{Items=catalog.Items.Select(i=>
        {
            var result=i.BlueprintCredits is null&&InventoryAcquisition.Find(i.Id)?.BlueprintCredits is >0 and var cost?i with{BlueprintCredits=cost}:i;
            return InventoryMarket.Find(i.Id) is {} market?result with{MarketId=market.Id,Tradable=true}:result;
        }).ToArray()};
    }
    public string Name(string id)=>Items.FirstOrDefault(i=>i.Id==id)?.Name??"이름 확인 필요";
}
public sealed record OwnedEquipment(string Id,int Rank,int Forma,bool? Gilded,string[] ModularParts);
public sealed record InventoryCraft(string BlueprintId,DateTimeOffset? CompletesUtc);
// This is the entire persisted whitelist. Account IDs, authentication fields,
// fingerprints, chat, player names and the original account JSON never survive parsing.
public sealed record InventorySnapshot(int SchemaVersion,DateTimeOffset CapturedUtc,string Source,int? MasteryRank,long Credits,
    Dictionary<string,long> Quantities,Dictionary<string,int> MasteryRanks,OwnedEquipment[] Equipment,InventoryCraft[] Crafting,
    bool InventoryComplete,bool MasteryComplete,string[] Sections)
{
    public static InventorySnapshot Empty=>new(1,DateTimeOffset.MinValue,"연동 전",null,0,new(),new(),[],[],false,false,[]);
    public long Count(string id)=>Quantities.GetValueOrDefault(id);
}

public static class InventoryImport
{
    public static readonly string[] EquipmentSections=["Suits","LongGuns","Pistols","Melee","SpaceSuits","SpaceGuns","SpaceMelee","Sentinels","SentinelWeapons","KubrowPets","OperatorAmps","MechSuits","Hoverboards"];
    private static string? Str(JsonElement x,string key)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static long Num(JsonElement x,string key,long fallback=0)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty(key,out var v)&&v.TryGetInt64(out var n)?n:fallback;
    private static IEnumerable<JsonElement> Rows(JsonElement x,string key)=>x.TryGetProperty(key,out var a)&&a.ValueKind==JsonValueKind.Array?a.EnumerateArray():[];
    public static string PathId(string value)=>value.Replace("/Lotus/StoreItems/","/Lotus/",StringComparison.Ordinal).TrimStart('/') is var p?"/"+p:value;
    public static InventorySnapshot Parse(string json,InventoryCatalog catalog,string source="JSON 가져오기")
    {
        if(json.Length>32*1024*1024)throw new InvalidDataException("인벤토리 파일이 너무 큽니다.");
        using var doc=JsonDocument.Parse(json,new(){MaxDepth=80});return Parse(doc.RootElement,catalog,source);
    }
    public static InventorySnapshot Parse(JsonElement root,InventoryCatalog catalog,string source)
    {
        // Documented GEP envelopes. Never recursively search unrelated objects.
        if(root.TryGetProperty("info",out var info)&&info.TryGetProperty("match_info",out var match)&&match.TryGetProperty("inventory",out var inv))
            return inv.ValueKind==JsonValueKind.String?Parse(inv.GetString()!,catalog,source):Parse(inv,catalog,source);
        if(root.TryGetProperty("key",out var key)&&key.GetString()=="inventory")
        {var v=root.TryGetProperty("value",out var value)?value:root.GetProperty("data");return v.ValueKind==JsonValueKind.String?Parse(v.GetString()!,catalog,source):Parse(v,catalog,source);}
        if(root.TryGetProperty("schemaVersion",out _))
        {
            var saved=root.Deserialize<InventorySnapshot>(Catalog.Json)??throw new InvalidDataException("인벤토리가 비어 있습니다.");Validate(saved);return saved;
        }
        var allowed=EquipmentSections.Concat(new[]{"XPInfo","MiscItems","Recipes","RawUpgrades","Upgrades","FusionTreasures","PendingRecipes","PlayerLevel","RegularCredits"}).ToHashSet();
        var sections=root.EnumerateObject().Select(p=>p.Name).Where(allowed.Contains).ToArray();
        if(!sections.Contains("XPInfo")&&!sections.Contains("MiscItems")&&!sections.Contains("Suits"))throw new InvalidDataException("지원하는 인벤토리 데이터가 아닙니다.");
        var lookup=catalog.Items.ToDictionary(x=>x.Id);var counts=new Dictionary<string,long>();var history=new Dictionary<string,int>();var equipment=new List<OwnedEquipment>();
        void Add(string id,long count){id=PathId(id);if(!id.StartsWith("/Lotus/",StringComparison.Ordinal)||id.Length>512||count<0||count>1_000_000_000_000)throw new InvalidDataException("잘못된 품목 또는 수량입니다.");counts[id]=checked(counts.GetValueOrDefault(id)+count);}
        int Rank(string id,long xp){lookup.TryGetValue(id,out var item);int basis=item?.PointsPerRank==200?1000:500;return Math.Clamp((int)Math.Sqrt(Math.Max(0,xp)/(double)basis),0,item?.MaxRank>0?item.MaxRank:40);}
        foreach(var section in EquipmentSections)foreach(var e in Rows(root,section))
        {
            if(Str(e,"ItemType") is not {} id)continue;id=PathId(id);var parts=Rows(e,"ModularParts").Where(p=>p.ValueKind==JsonValueKind.String).Select(p=>PathId(p.GetString()!)).ToArray();
            // Mastery belongs to the strike/chamber/prism, not the generic assembled weapon.
            var masterPart=parts.FirstOrDefault(p=>lookup.GetValueOrDefault(p)?.Masterable==true);if(masterPart is not null)id=masterPart;
            bool? gilded=e.TryGetProperty("Gilded",out var g)&&g.ValueKind is JsonValueKind.True or JsonValueKind.False?g.GetBoolean():null;
            if(Str(e,"ItemName") is {Length:>0}&&lookup.GetValueOrDefault(id)?.GildRequired==true)gilded=true;
            var rank=Rank(id,Num(e,"XP"));equipment.Add(new(id,rank,(int)Math.Clamp(Num(e,"Polarized"),0,100),gilded,parts));Add(id,1);
        }
        foreach(var section in new[]{"MiscItems","Recipes","RawUpgrades","FusionTreasures"})foreach(var e in Rows(root,section))if(Str(e,"ItemType") is {} id)Add(id,Num(e,"ItemCount"));
        foreach(var e in Rows(root,"Upgrades"))if(Str(e,"ItemType") is {} id)Add(id,1);
        foreach(var e in Rows(root,"XPInfo"))
        {
            if(Str(e,"ItemType") is not {} id)continue;id=PathId(id);history[id]=Math.Max(history.GetValueOrDefault(id),Rank(id,Num(e,"XP")));
        }
        // The historical XP ledger remains authoritative across Forma, gilding,
        // sales and duplicate copies. Current equipment rank cannot overwrite it.
        var crafting=Rows(root,"PendingRecipes").Where(e=>Str(e,"ItemType") is not null).Select(e=>new InventoryCraft(PathId(Str(e,"ItemType")!),Completion(e))).ToArray();
        bool full=new[]{"Suits","LongGuns","Pistols","Melee","MiscItems","Recipes"}.All(sections.Contains);
        var snapshot=new InventorySnapshot(1,DateTimeOffset.UtcNow,source,root.TryGetProperty("PlayerLevel",out var mr)&&mr.TryGetInt32(out var m)?m:null,Num(root,"RegularCredits"),counts,history,equipment.ToArray(),crafting,full,root.TryGetProperty("XPInfo",out var xp)&&xp.ValueKind==JsonValueKind.Array,sections);
        Validate(snapshot);return snapshot;
    }
    private static DateTimeOffset? Completion(JsonElement e)
    {
        if(!e.TryGetProperty("CompletionDate",out var d))return null;
        if(d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("$date",out var date))d=date;
        if(d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("$numberLong",out var number))d=number;
        if(d.ValueKind==JsonValueKind.String){if(DateTimeOffset.TryParse(d.GetString(),out var parsed))return parsed;if(long.TryParse(d.GetString(),out var n))return DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(n,0,253402300799999));}
        return d.ValueKind==JsonValueKind.Number&&d.TryGetInt64(out var millis)?DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(millis,0,253402300799999)):null;
    }
    public static void Validate(InventorySnapshot s)
    {
        if(s.SchemaVersion!=1||s.MasteryRank is <0 or >100||s.Credits<0||s.Quantities is null||s.MasteryRanks is null||s.Equipment is null||s.Crafting is null||s.Sections is null||s.Quantities.Count>100000||s.Equipment.Length>20000)throw new InvalidDataException("인벤토리 형식이나 버전이 올바르지 않습니다.");
        if(s.Quantities.Any(p=>!p.Key.StartsWith("/Lotus/",StringComparison.Ordinal)||p.Key.Length>512||p.Value is <0 or >1_000_000_000_000)||s.MasteryRanks.Any(p=>!p.Key.StartsWith("/Lotus/",StringComparison.Ordinal)||p.Value is <0 or >40))throw new InvalidDataException("인벤토리 수량 또는 마스터리 이력이 올바르지 않습니다.");
    }
    public static void Save(string path,InventorySnapshot snapshot)
    {
        Validate(snapshot);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(snapshot,Catalog.Json));File.Move(temp,path,true);
    }
    public static string[] MissingUpdateSections(InventorySnapshot previous,InventorySnapshot next)
    {
        if(previous.CapturedUtc==DateTimeOffset.MinValue)return [];
        var known=EquipmentSections.Concat(new[]{"MiscItems","Recipes","RawUpgrades","Upgrades","FusionTreasures","PendingRecipes","XPInfo","PlayerLevel","RegularCredits"}).ToHashSet(StringComparer.Ordinal);
        return previous.Sections.Where(s=>known.Contains(s)&&!next.Sections.Contains(s,StringComparer.Ordinal)).ToArray();
    }
}
