using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record ScheduleNode(string Id,string Name,string Planet,string Mission,string Faction,string Tier);
public sealed record ArbitrationFilter(string Tier="S",string? Mission=null,string? Faction=null,string[]? ExcludedMissions=null)
{
    public bool Matches(ScheduleNode node)=>(Tier.Length==0||Tier.Contains(node.Tier,StringComparison.Ordinal))
        &&(Mission is null||node.Mission==Mission)
        &&!(ExcludedMissions?.Contains(node.Mission,StringComparer.Ordinal)??false)
        &&(Faction is null||node.Faction.Split('·',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Contains(Faction,StringComparer.Ordinal));
}
public sealed record ScheduledWeapon(string Id,string Name,string EnglishName,string Shop,string Batch,int Price,string Currency,int MasteryRequired);
public sealed record ScheduleCatalog(int Version,DateTimeOffset RetrievedUtc,ScheduleNode[] Nodes,ScheduledWeapon[] Offers,DateTimeOffset TenetEpoch,DateTimeOffset CodaEpoch,string TierSource)
{
    public static ScheduleCatalog Load()
    {using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.world-schedules.json")!;return JsonSerializer.Deserialize<ScheduleCatalog>(s,Catalog.Json)!;}
}
public sealed record ArbitrationSlot(DateTimeOffset StartUtc,string NodeId)
{
    public DateTimeOffset EndUtc=>StartUtc.AddHours(1);
}
public sealed class ArbitrationSchedule
{
    public ArbitrationSlot[] Slots {get;}
    public DateTimeOffset RetrievedUtc {get;}
    public string Source {get;}
    private ArbitrationSchedule(ArbitrationSlot[] slots,DateTimeOffset fetched,string source){Slots=slots;RetrievedUtc=fetched;Source=source;}
    public static ArbitrationSchedule Bundled(ScheduleCatalog catalog)
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.arbitration-schedule.csv")!;using var r=new StreamReader(s);return Parse(r.ReadToEnd(),catalog.RetrievedUtc,"browse.wf · 내장 일정표");
    }
    public static ArbitrationSchedule Parse(string text,DateTimeOffset fetched,string source)
    {
        if(text.Length>4*1024*1024)throw new InvalidDataException("중재 일정표가 너무 큽니다.");
        var slots=new List<ArbitrationSlot>();
        foreach(var raw in text.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            var pair=raw.Trim().Split(',');
            if(pair.Length!=2||!long.TryParse(pair[0],NumberStyles.None,CultureInfo.InvariantCulture,out var epoch)||epoch is <1577836800 or >4133980800||epoch%3600!=0||pair[1].Length is 0 or >100||!pair[1].All(char.IsAsciiLetterOrDigit))throw new InvalidDataException("중재 일정표 형식이 올바르지 않습니다.");
            var start=DateTimeOffset.FromUnixTimeSeconds(epoch);
            if(slots.Count>0&&start!=slots[^1].StartUtc.AddHours(1))throw new InvalidDataException("중재 일정표에 중복·역순·누락 시간이 있습니다.");
            slots.Add(new(start,pair[1]));
        }
        if(slots.Count<24)throw new InvalidDataException("중재 일정표가 불완전합니다.");return new(slots.ToArray(),fetched,source);
    }
    public ArbitrationSlot? Current(DateTimeOffset now)=>Window(now,1).FirstOrDefault(s=>s.StartUtc<=now&&now<s.EndUtc);
    public IEnumerable<ArbitrationSlot> Window(DateTimeOffset now,int hours)
    {
        if(hours is <1 or >744)throw new ArgumentOutOfRangeException(nameof(hours));
        int lo=0,hi=Slots.Length;while(lo<hi){int mid=(lo+hi)/2;if(Slots[mid].EndUtc<=now)lo=mid+1;else hi=mid;}
        var end=now.AddHours(hours);for(int i=lo;i<Slots.Length&&Slots[i].StartUtc<end;i++)yield return Slots[i];
    }
    public ArbitrationSlot? NextS(ScheduleCatalog catalog,DateTimeOffset now,ArbitrationFilter? filter=null)
    {
        var ids=catalog.Nodes.Where(n=>n.Tier=="S"&&(filter is null||filter.Matches(n with{Tier="S"}))).Select(n=>n.Id).ToHashSet();return Slots.FirstOrDefault(s=>s.StartUtc>now&&ids.Contains(s.NodeId));
    }
}
public sealed record CycleAnchor(string Id,string State,DateTimeOffset ExpiryUtc,DateTimeOffset? SourceUtc=null,string Source="warframestat.us");
public sealed record WorldScheduleSnapshot(DateTimeOffset Timestamp,CycleAnchor[] Cycles,string? LiveArbitrationNode,DateTimeOffset? LiveArbitrationStart,
    DateTimeOffset? LiveArbitrationEnd,DateTimeOffset? BaroStart,DateTimeOffset? BaroEnd,string? BaroLocation,string BuildLabel)
{
    public IncarnonRotationAnchor? Incarnon {get;init;}
    public FissureMission[] Fissures {get;init;}=[];
    public DateTimeOffset? FissuresUtc {get;init;}
    public string FissuresSource {get;init;}="warframestat.us";
    public WorldAlertMission[] Alerts {get;init;}=[];
    public DateTimeOffset? AlertsUtc {get;init;}
    public string AlertsSource {get;init;}="warframestat.us";
}
public sealed record CycleDefinition(string Id,string Name,string[] States,string[] Labels,int[] Seconds,string[] Colors,string Detail);
public sealed record CycleWindow(CycleDefinition Definition,int StateIndex,DateTimeOffset StartUtc,DateTimeOffset EndUtc,bool Projected)
{
    public string State=>Definition.States[StateIndex];public string Label=>Definition.Labels[StateIndex];
    public double Progress(DateTimeOffset now)=>Math.Clamp((now-StartUtc).TotalSeconds/(EndUtc-StartUtc).TotalSeconds,0,1);
}
public sealed record ShopWindow(string Shop,string Name,string Batch,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string Basis);
public static class WorldScheduleEngine
{
    public static readonly CycleDefinition[] Definitions=[
        new("cetusCycle","지구 · 아이돌론 평원",["day","night"],["낮","밤"],[6000,3000],["#E6C65D","#86ACE7"],"지구 숲의 식물도 평원과 같은 주기 · 밤에는 아이돌론 사냥"),
        new("vallisCycle","오브 협곡",["warm","cold"],["따뜻함","추움"],[400,1200],["#E6A560","#8CC9DF"],"따뜻함 6분 40초 · 추움 20분"),
        new("cambionCycle","캠비온 퇴적지",["fass","vome"],["파스","봄"],[6000,3000],["#E3A25D","#8FB1E4"],"파스 100분 · 봄 50분"),
        new("zarimanCycle","자리만",["grineer","corpus"],["그리니어","코퍼스"],[9000,9000],["#D5AC71","#8BC2E7"],"진영마다 2시간 30분"),
        new("duviriCycle","두비리",["sorrow","fear","joy","anger","envy"],["슬픔","공포","기쁨","분노","질투"],[7200,7200,7200,7200,7200],["#82A7E0","#AD96D7","#E6C55E","#E18B82","#91BD9B"],"감정 나선마다 2시간")
    ];
    public static WorldScheduleSnapshot Parse(JsonElement root)
    {
        var timestamp=Date(root,"timestamp")??throw new InvalidDataException("월드 상태의 기준 시각을 확인할 수 없습니다.");var cycles=new List<CycleAnchor>();
        foreach(var def in Definitions)
        {
            if(!root.TryGetProperty(def.Id,out var value))continue;
            var state=Text(value,"state");var expiry=Date(value,"expiry");int index=Array.IndexOf(def.States,state);
            if(index>=0&&expiry is {} end&&end>=timestamp.AddMinutes(-1)&&end<=timestamp.AddSeconds(def.Seconds.Max()+120))cycles.Add(new(def.Id,state!,end,timestamp));
        }
        if(cycles.Count==0)throw new InvalidDataException("유효한 낮·밤 주기를 받지 못했습니다.");
        string? node=null;DateTimeOffset? start=null,finish=null;
        if(root.TryGetProperty("arbitration",out var a))
        {
            var s=Date(a,"activation");var e=Date(a,"expiry");var id=Text(a,"nodeKey");
            if(id is not null&&id!="SolNode000"&&Text(a,"typeKey")!="Unknown"&&s is {} ss&&e is {} ee&&ee>ss&&ee-ss<=TimeSpan.FromHours(1.1)&&timestamp>=ss.AddMinutes(-2)&&timestamp<ee)
            {node=id;start=ss;finish=ee;}
        }
        DateTimeOffset? bs=null,be=null;string? location=null;
        if(root.TryGetProperty("voidTrader",out var baro)){bs=Date(baro,"activation");be=Date(baro,"expiry");location=Text(baro,"location");if(bs is null||be is null||be<=bs||be-bs>TimeSpan.FromDays(4)){bs=be=null;location=null;}}
        return new(timestamp,cycles.ToArray(),node,start,finish,bs,be,location,Text(root,"buildLabel")??""){Fissures=FissureParser.FromApi(root),FissuresUtc=root.TryGetProperty("fissures",out _)?timestamp:null,Alerts=GameAlerts.FromApi(root),AlertsUtc=root.TryGetProperty("alerts",out _)?timestamp:null,Incarnon=IncarnonRotations.FromApi(root,timestamp)};
    }
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static DateTimeOffset? Date(JsonElement e,string key)=>DateTimeOffset.TryParse(Text(e,key),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date)&&date.Year is >=2020 and <=2100?date:null;
    public static bool Fresh(WorldScheduleSnapshot? snapshot,DateTimeOffset now)=>snapshot is not null&&now-snapshot.Timestamp<TimeSpan.FromMinutes(10)&&snapshot.Timestamp-now<TimeSpan.FromMinutes(2);
    public static WorldScheduleSnapshot WithOfficialCycles(WorldScheduleSnapshot? previous,JsonElement root)
    {
        var epoch=root.GetProperty("Time").GetInt64();if(epoch is <1577836800 or >4133980800)throw new InvalidDataException("공식 월드 상태 시각이 올바르지 않습니다.");var at=DateTimeOffset.FromUnixTimeSeconds(epoch);
        var cetus=root.GetProperty("SyndicateMissions").EnumerateArray().FirstOrDefault(s=>Text(s,"Tag")=="CetusSyndicate");
        if(cetus.ValueKind!=JsonValueKind.Object||!long.TryParse(cetus.GetProperty("Expiry").GetProperty("$date").GetProperty("$numberLong").GetString(),NumberStyles.None,CultureInfo.InvariantCulture,out var millis)||millis is <1577836800000 or >4133980800000)throw new InvalidDataException("공식 평원 주기의 만료 시각이 없습니다.");
        var nightEnd=DateTimeOffset.FromUnixTimeMilliseconds(millis);if(at<nightEnd.AddMinutes(-150)||at>=nightEnd)throw new InvalidDataException("공식 평원 의뢰가 현재 주기를 포함하지 않습니다.");
        bool day=at<nightEnd.AddMinutes(-50);var end=day?nightEnd.AddMinutes(-50):nightEnd;var cycles=(previous?.Cycles??[]).ToDictionary(c=>c.Id);
        foreach(var pair in new[]{("cetusCycle",day?"day":"night"),("cambionCycle",day?"fass":"vome")})
        {if(!cycles.TryGetValue(pair.Item1,out var old)||at>(old.SourceUtc??previous!.Timestamp))cycles[pair.Item1]=new(pair.Item1,pair.Item2,end,at,"공식 월드 상태");}
        var updated=previous is null?new WorldScheduleSnapshot(at,cycles.Values.ToArray(),null,null,null,null,null,null,""):previous with{Cycles=cycles.Values.ToArray()};
        if(root.TryGetProperty("ActiveMissions",out _)&&(updated.FissuresUtc is null||at>=updated.FissuresUtc))updated=updated with{Fissures=FissureParser.FromOfficial(root),FissuresUtc=at,FissuresSource="공식 월드 상태"};
        if(root.TryGetProperty("Alerts",out _)&&(updated.AlertsUtc is null||at>=updated.AlertsUtc))updated=updated with{Alerts=GameAlerts.FromOfficial(root),AlertsUtc=at,AlertsSource="공식 월드 상태"};
        if(IncarnonRotations.FromOfficial(root,at) is {} incarnon&&(updated.Incarnon is null||at>=updated.Incarnon.SourceUtc))updated=updated with{Incarnon=incarnon};
        return updated;
    }
    public static CycleWindow? CycleAt(CycleAnchor anchor,DateTimeOffset now)
    {
        var d=Definitions.SingleOrDefault(d=>d.Id==anchor.Id);if(d is null)return null;int index=Array.IndexOf(d.States,anchor.State);if(index<0)return null;
        var origin=anchor.ExpiryUtc.AddSeconds(-d.Seconds[index]);if(now<origin)return null;
        long period=d.Seconds.Sum();long elapsed=(long)Math.Floor((now-origin).TotalSeconds);var start=origin.AddSeconds(elapsed/period*period);long offset=elapsed%period;int current=index;
        for(int i=0;i<d.States.Length;i++)
        {if(offset<d.Seconds[current])return new(d,current,start,start.AddSeconds(d.Seconds[current]),now>=anchor.ExpiryUtc);offset-=d.Seconds[current];start=start.AddSeconds(d.Seconds[current]);current=(current+1)%d.States.Length;}
        return null;
    }
    public static CycleWindow? NextState(CycleAnchor anchor,string state,DateTimeOffset now)
    {
        var current=CycleAt(anchor,now);if(current is null)return null;var d=current.Definition;var start=current.EndUtc;int index=(current.StateIndex+1)%d.States.Length;
        for(int i=0;i<d.States.Length;i++){var end=start.AddSeconds(d.Seconds[index]);if(d.States[index]==state)return new(d,index,start,end,true);start=end;index=(index+1)%d.States.Length;}return null;
    }
    public static ShopWindow ShopAt(ScheduleCatalog catalog,string shop,DateTimeOffset now)
    {
        bool coda=shop=="coda";if(!coda&&shop!="tenet")throw new ArgumentException("알 수 없는 무기 상점입니다.");
        var epoch=coda?catalog.CodaEpoch:catalog.TenetEpoch;long n=(long)Math.Floor((now-epoch).TotalDays/4);var start=epoch.AddDays(n*4);
        var batch=coda?((n%2+2)%2==0?"A":"B"):"";return new(shop,coda?"엘레노어 · 코다":"에르고 글래스트 · 테넷",batch,start,start.AddDays(4),"위키 주기 기준 예상");
    }
    public static DateTimeOffset WeaponNextStart(ScheduledWeapon weapon,ShopWindow window)=>weapon.Shop=="tenet"||weapon.Batch==window.Batch?window.StartUtc:window.EndUtc;
    public static readonly TimeZoneInfo Korea=TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
    public static string KoreaTime(DateTimeOffset value)=>TimeZoneInfo.ConvertTime(value,Korea).ToString("MM-dd (ddd) HH:mm",CultureInfo.GetCultureInfo("ko-KR"));
    public static string Countdown(DateTimeOffset until,DateTimeOffset now)
    {
        var s=TimeSpan.FromSeconds(Math.Max(0,Math.Ceiling((until-now).TotalSeconds)));return s.Days>0?$"{s.Days}일 {s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}":$"{s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}";
    }
}
