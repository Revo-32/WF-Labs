using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record FissureMission(string Id,string Node,string MissionCode,int Tier,bool SteelPath,bool Storm,DateTimeOffset StartUtc,DateTimeOffset EndUtc)
{
    public string Mission=>FissureParser.MissionLabel(MissionCode);
    public string TierLabel=>Tier switch{1=>"리스",2=>"메소",3=>"네오",4=>"액시",5=>"레퀴엠",6=>"옴니아",_=>"등급 확인 필요"};
    public string Mode=>Storm?"보이드 폭풍":SteelPath?"강철의 길":"일반";
    public bool Active(DateTimeOffset now)=>StartUtc<=now&&now<EndUtc;
}
public static class FissureParser
{
    private sealed record Region(string Id,string Name,string EnglishName,string Planet,string Mission);
    private sealed record Regions(Region[] Nodes,Dictionary<string,string> Missions);
    private static readonly Regions catalog=Load();
    private static Regions Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.schedule-regions.json")!;return JsonSerializer.Deserialize<Regions>(s,Catalog.Json)!;}
    public static IReadOnlyDictionary<string,string> MissionTypes=>catalog.Missions;
    public static string MissionLabel(string code)=>catalog.Missions.GetValueOrDefault(code,"미션 확인 필요 · "+code);
    internal static string NodeLabel(string node)
    {var r=catalog.Nodes.FirstOrDefault(n=>n.Id==node||n.Name==node.Split(" (")[0]||n.EnglishName==node.Split(" (")[0]);return r is null?node:$"{r.Planet} · {r.Name}";}
    internal static string CodeFromApi(string key)=>apiCodes.GetValueOrDefault(key,key);
    // API keys are stable English identifiers; visible labels always come from Korean public export.
    private static readonly Dictionary<string,string> apiCodes=new(StringComparer.OrdinalIgnoreCase){
        ["Extermination"]="MT_EXTERMINATION",["Exterminate"]="MT_EXTERMINATION",["Capture"]="MT_CAPTURE",["Rescue"]="MT_RESCUE",["Sabotage"]="MT_SABOTAGE",["Mobile Defense"]="MT_MOBILE_DEFENSE",["Defense"]="MT_DEFENSE",["Survival"]="MT_SURVIVAL",["Interception"]="MT_TERRITORY",["Excavation"]="MT_EXCAVATE",["Spy"]="MT_INTEL",["Disruption"]="MT_ARTIFACT",["Void Cascade"]="MT_VOID_CASCADE",["Void Flood"]="MT_CORRUPTION",["Alchemy"]="MT_ALCHEMY",["Mirror Defense"]="MT_MIRROR_DEFENSE",["Assassination"]="MT_ASSASSINATION",["Skirmish"]="MT_RAILJACK"};
    public static FissureMission[] FromApi(JsonElement root)
    {
        if(!root.TryGetProperty("fissures",out var array)||array.ValueKind!=JsonValueKind.Array)return [];
        var found=new List<FissureMission>();
        foreach(var e in array.EnumerateArray())
        {
            string? id=Text(e,"id"),node=Text(e,"nodeKey")??Text(e,"node"),key=Text(e,"missionTypeKey");
            if(id is null||node is null||!DateTimeOffset.TryParse(Text(e,"activation"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var start)||!DateTimeOffset.TryParse(Text(e,"expiry"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var end)||end<=start)continue;
            var region=catalog.Nodes.FirstOrDefault(n=>n.Id==node||$"{n.Name} ({n.Planet})"==node||n.Name==node.Split(" (")[0]||n.EnglishName==node.Split(" (")[0]);
            string code=key is not null&&apiCodes.TryGetValue(key,out var k)?k:region?.Mission??key??"Unknown";
            string label=region is null?node:$"{region.Planet} · {region.Name}";
            int tier=e.TryGetProperty("tierNum",out var t)&&t.ValueKind==JsonValueKind.Number&&t.TryGetInt32(out int number)?number:0;
            found.Add(new(id,label,code,tier,Flag(e,"isHard"),Flag(e,"isStorm"),start,end));
        }
        return found.DistinctBy(f=>f.Id).ToArray();
    }
    public static FissureMission[] FromOfficial(JsonElement root)
    {
        var found=new List<FissureMission>();
        foreach(var key in new[]{"ActiveMissions","VoidStorms"})
        {
            if(!root.TryGetProperty(key,out var array)||array.ValueKind!=JsonValueKind.Array)continue;
            foreach(var e in array.EnumerateArray())
            {
                var id=e.TryGetProperty("_id",out var oid)?Text(oid,"$oid"):null;var node=Text(e,"Node");var start=OfficialDate(e,"Activation");var end=OfficialDate(e,"Expiry");
                if(id is null||node is null||start is null||end is null||end<=start)continue;
                var region=catalog.Nodes.FirstOrDefault(r=>r.Id==node);string modifier=Text(e,"Modifier")??Text(e,"ActiveMissionTier")??"";int tier=modifier.Length>0&&int.TryParse(modifier.AsSpan(modifier.Length-1),out int n)?n:0;
                found.Add(new(id,region is null?node:$"{region.Planet} · {region.Name}",Text(e,"MissionType")??region?.Mission??"Unknown",tier,Flag(e,"Hard"),key=="VoidStorms",start.Value,end.Value));
            }
        }
        return found.DistinctBy(f=>f.Id).ToArray();
    }
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static bool Flag(JsonElement e,string key)=>e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
    private static DateTimeOffset? OfficialDate(JsonElement e,string key)
    {if(e.TryGetProperty(key,out var v)&&v.TryGetProperty("$date",out v)&&long.TryParse(Text(v,"$numberLong"),out long ms)&&ms is >=1577836800000 and <=4133980800000)return DateTimeOffset.FromUnixTimeMilliseconds(ms);return null;}
}

public sealed record FissureAlertRule(string Id,string[] Missions,int Mode=1,int Tier=0,bool Enabled=true)
{
    public bool Matches(FissureMission f)=>Enabled&&(Missions.Length==0||Missions.Contains(f.MissionCode,StringComparer.Ordinal))&&(Mode switch{1=>f.SteelPath&&!f.Storm,2=>!f.SteelPath&&!f.Storm,3=>f.Storm,_=>true})&&(Tier==0||Tier==f.Tier);
}
public static class FissureAlerts
{
    public static bool Fresh(WorldScheduleSnapshot? snapshot,DateTimeOffset now)=>snapshot?.FissuresUtc is {} at&&now-at<TimeSpan.FromMinutes(3)&&at-now<TimeSpan.FromMinutes(2);
    public static ScheduleAlert[] Due(WorldScheduleData data,IEnumerable<FissureAlertRule> rules,DateTimeOffset now,ISet<string> delivered)
    {
        if(data.Cached||!Fresh(data.Snapshot,now))return [];
        var active=rules.Where(r=>r.Enabled).ToArray();
        return data.Snapshot!.Fissures.Where(f=>f.Active(now)&&active.Any(r=>r.Matches(f))&&!delivered.Contains("fissure:"+f.Id))
            .Select(f=>new ScheduleAlert("fissure:"+f.Id,$"{f.Mode} · {f.Mission} 열림",$"{f.Node} · {f.TierLabel}\n종료 {WorldScheduleEngine.KoreaTime(f.EndUtc)}",f.EndUtc)).ToArray();
    }
}

public sealed record SchedulePreferences
{
    public string[] ExcludedMissions {get;init;}=[];
    public bool OverlayEnabled {get;init;}
    public string[] OverlayItems {get;init;}=["nextS","cetusCycle"];
    public FissureAlertRule[] FissureRules {get;init;}=[];
    public bool Popup {get;init;}=true;
    public bool Sound {get;init;}=true;
    public double SoundVolume {get;init;}=.65;
    public double BackgroundOpacity {get;init;}=.82;
    public bool TextShadow {get;init;}=true;
    public double ShadowStrength {get;init;}=.75;
    public double TextScale {get;init;}=1;
    public bool ShowCurrentFissures {get;init;}=true;
    public bool NotifyFarmingGoals {get;init;}=true;
    public bool ShowFarmingFissures {get;init;}=true;
    public int FarmingMode {get;init;}
    public string[] FarmingExcludedMissions {get;init;}=[];
    public int FissureView {get;init;}
    public bool ShowGameAlerts {get;init;}=true;
    public bool NotifyGameAlerts {get;init;}
    public string AlertRewardFilter {get;init;}="";
    public double X {get;init;}=.02;
    public double Y {get;init;}=.12;
    public double Width {get;init;}=480;
    public double Height {get;init;}=620;
    public SchedulePreferences Sanitize()=>this with{ExcludedMissions=ExcludedMissions??[],FarmingExcludedMissions=FarmingExcludedMissions??[],FarmingMode=Math.Clamp(FarmingMode,0,3),OverlayItems=(OverlayItems??[]).Distinct().Take(100).ToArray(),FissureRules=(FissureRules??[]).Where(r=>r is not null&&r.Missions is not null).Take(20).ToArray(),X=Finite(X,0,1,.02),Y=Finite(Y,0,1,.12),Width=Finite(Width,320,1100,480),Height=Finite(Height,260,1100,620),SoundVolume=Finite(SoundVolume,0,1,.65),BackgroundOpacity=Finite(BackgroundOpacity,0,1,.82),ShadowStrength=Finite(ShadowStrength,0,1,.75),TextScale=Finite(TextScale,.85,1.3,1),FissureView=Math.Clamp(FissureView,0,5)};
    private static double Finite(double value,double min,double max,double fallback)=>double.IsFinite(value)?Math.Clamp(value,min,max):fallback;
}
