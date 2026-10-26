using System.Globalization;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record WorldAlertMission(string Id,string Node,string MissionCode,string Reward,bool SteelPath,DateTimeOffset StartUtc,DateTimeOffset EndUtc)
{
    public string Mission=>FissureParser.MissionLabel(MissionCode);
    public bool Active(DateTimeOffset now)=>StartUtc<=now&&now<EndUtc;
}
public static class GameAlerts
{
    private static readonly Lazy<Dictionary<string,string>> itemNames=new(()=>InventoryCatalog.Load().Items.ToDictionary(i=>i.Id,i=>i.Name,StringComparer.Ordinal));
    public static WorldAlertMission[] FromApi(JsonElement root)
    {
        if(!root.TryGetProperty("alerts",out var list)||list.ValueKind!=JsonValueKind.Array)return [];
        var found=new List<WorldAlertMission>();foreach(var e in list.EnumerateArray())
        {
            var id=Text(e,"id");var start=Date(Text(e,"activation"));var end=Date(Text(e,"expiry"));
            if(id is null||start is null||end is null||end<=start||!e.TryGetProperty("mission",out var m)||m.ValueKind!=JsonValueKind.Object)continue;
            string node=Text(m,"nodeKey")??Text(m,"node")??"위치 확인 필요";string code=FissureParser.CodeFromApi(Text(m,"typeKey")??Text(m,"type")??"Unknown");
            string reward=m.TryGetProperty("reward",out var r)?Reward(r,false):"보상 확인 필요";
            found.Add(new(id,FissureParser.NodeLabel(node),code,reward,Flag(m,"isHard"),start.Value,end.Value));
        }
        return found.DistinctBy(a=>a.Id).ToArray();
    }
    public static WorldAlertMission[] FromOfficial(JsonElement root)
    {
        if(!root.TryGetProperty("Alerts",out var list)||list.ValueKind!=JsonValueKind.Array)return [];
        var found=new List<WorldAlertMission>();foreach(var e in list.EnumerateArray())
        {
            string? id=e.TryGetProperty("_id",out var oid)?Text(oid,"$oid"):null;var start=OfficialDate(e,"Activation");var end=OfficialDate(e,"Expiry");
            if(id is null||start is null||end is null||end<=start||!e.TryGetProperty("MissionInfo",out var m)||m.ValueKind!=JsonValueKind.Object)continue;
            string reward=m.TryGetProperty("missionReward",out var r)?Reward(r,true):"보상 확인 필요";
            found.Add(new(id,FissureParser.NodeLabel(Text(m,"location")??"위치 확인 필요"),Text(m,"missionType")??"Unknown",reward,Flag(m,"isHard")||Flag(m,"Hard")||Flag(e,"Hard"),start.Value,end.Value));
        }
        return found.DistinctBy(a=>a.Id).ToArray();
    }
    private static string Reward(JsonElement r,bool official)
    {
        if(r.ValueKind!=JsonValueKind.Object)return "보상 확인 필요";var pieces=new List<string>();
        if(r.TryGetProperty("countedItems",out var list)&&list.ValueKind==JsonValueKind.Array)
        foreach(var item in list.EnumerateArray().Take(32))
        {string? id=Text(item,official?"ItemType":"uniqueName");long count=Number(item,official?"ItemCount":"count");if(count<=0)continue;pieces.Add(Name(id)+$" ×{count:N0}");}
        if(r.TryGetProperty("items",out list)&&list.ValueKind==JsonValueKind.Array)
        foreach(var item in list.EnumerateArray().Take(32)){if(item.ValueKind==JsonValueKind.String){var label=item.GetString()??"";pieces.Add(official?Name(label):itemNames.Value.Values.Contains(label,StringComparer.Ordinal)?label:"추가 보상 · 이름 확인 필요");}}
        if(r.TryGetProperty("itemsUniqueNames",out list)&&list.ValueKind==JsonValueKind.Array)
        {pieces.RemoveAll(s=>s=="추가 보상 · 이름 확인 필요");foreach(var item in list.EnumerateArray().Take(32))if(item.ValueKind==JsonValueKind.String)pieces.Add(Name(item.GetString()));}
        long credits=Number(r,"credits");if(credits>0)pieces.Add($"{credits:N0} 크레딧");return pieces.Count==0?"보상 확인 필요":string.Join(" · ",pieces);
    }
    private static string Name(string? id)=>id is not null&&itemNames.Value.TryGetValue(id.Replace("/Lotus/StoreItems/","/Lotus/",StringComparison.Ordinal),out var name)?name:"보상 이름 확인 필요";
    private static long Number(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var n)?Math.Clamp(n,0,1_000_000_000):0;
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static bool Flag(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
    private static DateTimeOffset? Date(string? value)=>DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var d)&&d.Year is >=2020 and <=2100?d:null;
    private static DateTimeOffset? OfficialDate(JsonElement e,string key)
    {if(e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Object&&v.TryGetProperty("$date",out v)&&long.TryParse(Text(v,"$numberLong"),out long ms)&&ms is >=1577836800000 and <=4133980800000)return DateTimeOffset.FromUnixTimeMilliseconds(ms);return null;}
    public static bool Fresh(WorldScheduleSnapshot? snapshot,DateTimeOffset now)=>snapshot?.AlertsUtc is {} t&&now-t<TimeSpan.FromMinutes(3)&&t-now<TimeSpan.FromMinutes(2);
    public static ScheduleAlert[] Due(WorldScheduleData data,SchedulePreferences preferences,DateTimeOffset now,ISet<string> delivered)
    {
        if(!preferences.NotifyGameAlerts||data.Cached||!Fresh(data.Snapshot,now))return [];
        string filter=(preferences.AlertRewardFilter??"").Trim();return data.Snapshot!.Alerts.Where(a=>a.Active(now)&&!delivered.Contains("alert:"+a.Id)&&(filter.Length==0||a.Reward.Contains(filter,StringComparison.OrdinalIgnoreCase)))
            .Select(a=>new ScheduleAlert("alert:"+a.Id,$"얼럿 · {a.Mission}",$"{a.Node}\n{a.Reward}\n종료 {WorldScheduleEngine.KoreaTime(a.EndUtc)}",a.EndUtc)).ToArray();
    }
}
