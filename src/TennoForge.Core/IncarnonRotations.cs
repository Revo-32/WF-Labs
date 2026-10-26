using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record IncarnonRotationWeapon(string Id,string Name,string EnglishName,string Category);
public sealed record IncarnonRotationGroup(int Week,IncarnonRotationWeapon[] Weapons);
public sealed record IncarnonRotationCatalog(int Version,DateTimeOffset RetrievedUtc,DateTimeOffset AnchorUtc,int AnchorWeek,string WikiUrl,long Revision,IncarnonRotationGroup[] Weeks);
public sealed record IncarnonRotationAnchor(DateTimeOffset StartUtc,DateTimeOffset EndUtc,string[] Choices,DateTimeOffset SourceUtc,string Source);
public sealed record IncarnonRotationWindow(int Week,DateTimeOffset StartUtc,DateTimeOffset EndUtc,IncarnonRotationWeapon[] Weapons,bool Confirmed,string Source);

public static class IncarnonRotations
{
    private static readonly Lazy<IncarnonRotationCatalog> table=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.incarnon-rotations.json")!;
        var data=JsonSerializer.Deserialize<IncarnonRotationCatalog>(stream,Catalog.Json)!;
        if(data.Weeks.Length!=9||!data.Weeks.Select(w=>w.Week).SequenceEqual(Enumerable.Range(1,9))||data.Weeks.Any(w=>w.Weapons.Length!=5)||data.Weeks.SelectMany(w=>w.Weapons).Select(w=>w.Id).Distinct().Count()!=45)throw new InvalidDataException("인카논 순환표 형식을 확인하세요.");
        return data;
    });
    private static readonly Lazy<InventoryCatalog> inventory=new(InventoryCatalog.Load);
    public static IncarnonRotationCatalog Table=>table.Value;
    public static DateTimeOffset WeekStart(DateTimeOffset now)
    {var utc=now.ToUniversalTime();var date=new DateTimeOffset(utc.Year,utc.Month,utc.Day,0,0,0,TimeSpan.Zero);return date.AddDays(-((int)date.DayOfWeek+6)%7);}
    private static string Key(string value)=>string.Concat(value.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
    private static int Match(string[] choices)=>Table.Weeks.FirstOrDefault(g=>g.Weapons.Select(w=>Key(w.EnglishName)).ToHashSet().SetEquals(choices.Select(Key)))?.Week??0;
    public static IncarnonRotationWindow Current(DateTimeOffset now,IncarnonRotationAnchor? live=null)
    {
        if(live is not null&&live.StartUtc<=now&&now<live.EndUtc)
        {
            int week=Match(live.Choices);var weapons=week==0?live.Choices.Select(Resolve).ToArray():Table.Weeks[week-1].Weapons;
            bool fresh=now-live.SourceUtc<TimeSpan.FromMinutes(10)&&live.SourceUtc<=now.AddMinutes(2);
            return new(week,live.StartUtc,live.EndUtc,weapons,fresh,live.Source);
        }
        var start=WeekStart(now);long elapsed=(long)Math.Floor((start-Table.AnchorUtc).TotalDays/7);int index=(int)(((Table.AnchorWeek-1+elapsed)%Table.Weeks.Length+Table.Weeks.Length)%Table.Weeks.Length);
        return new(index+1,start,start.AddDays(7),Table.Weeks[index].Weapons,false,"위키 순환표 예상");
    }
    public static IncarnonRotationWindow[] Upcoming(DateTimeOffset now,int count,IncarnonRotationAnchor? live=null)
    {
        if(count is <1 or >18)throw new ArgumentOutOfRangeException(nameof(count));var current=Current(now,live);if(current.Week==0)return [];
        return Enumerable.Range(1,count).Select(i=>{int index=(current.Week-1+i)%Table.Weeks.Length;var start=current.EndUtc.AddDays((i-1)*7);return new IncarnonRotationWindow(index+1,start,start.AddDays(7),Table.Weeks[index].Weapons,false,"위키 순환표 예상");}).ToArray();
    }
    private static IncarnonRotationWeapon Resolve(string code)
    {
        var known=Table.Weeks.SelectMany(w=>w.Weapons).FirstOrDefault(w=>Key(w.EnglishName)==Key(code));if(known is not null)return known;
        var item=inventory.Value.Items.FirstOrDefault(i=>Key(i.EnglishName)==Key(code)&&i.Masterable);return item is null?new("","이름 확인 필요 · "+code,code,""):new(item.Id,item.Name,item.EnglishName,item.Category);
    }
    public static IncarnonRotationAnchor? FromApi(JsonElement root,DateTimeOffset at)
    {
        if(!Property(root,"duviriCycle",out var cycle)||!Property(cycle,"choices",out var categories)||categories.ValueKind!=JsonValueKind.Array)return null;
        foreach(var category in categories.EnumerateArray())
        {
            string? kind=String(category,"categoryKey")??String(category,"category");
            if(kind is not ("EXC_HARD" or "hard"))continue;var choices=Choices(category,"choices");if(choices is null)return null;var start=WeekStart(at);return new(start,start.AddDays(7),choices,at,"월드 상태");
        }
        return null;
    }
    public static IncarnonRotationAnchor? FromOfficial(JsonElement root,DateTimeOffset at)
    {
        if(!Property(root,"EndlessXpSchedule",out var schedules)||schedules.ValueKind!=JsonValueKind.Array)return null;
        var candidates=new List<IncarnonRotationAnchor>();
        foreach(var entry in schedules.EnumerateArray())
        {
            var start=Date(entry,"Activation");var end=Date(entry,"Expiry");if(start is null||end is null||end-start!=TimeSpan.FromDays(7)||start!=WeekStart(start.Value)||!Property(entry,"CategoryChoices",out var categories)||categories.ValueKind!=JsonValueKind.Array)continue;
            foreach(var category in categories.EnumerateArray())if(String(category,"Category")=="EXC_HARD"&&Choices(category,"Choices") is {} choices)candidates.Add(new(start.Value,end.Value,choices,at,"공식 월드 상태"));
        }
        return candidates.OrderByDescending(c=>c.StartUtc<=at&&at<c.EndUtc).ThenByDescending(c=>c.StartUtc).FirstOrDefault();
    }
    private static string[]? Choices(JsonElement category,string key)
    {
        if(!Property(category,key,out var data)||data.ValueKind!=JsonValueKind.Array||data.GetArrayLength()!=5)return null;
        var values=data.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).ToArray();return values.Length==5&&values.All(v=>v.Length is >0 and <100)&&values.Select(Key).Distinct().Count()==5?values:null;
    }
    private static DateTimeOffset? Date(JsonElement value,string name)
    {if(Property(value,name,out var wrapper)&&Property(wrapper,"$date",out var date)&&long.TryParse(String(date,"$numberLong"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)&&n is >=1577836800000 and <=4133980800000)return DateTimeOffset.FromUnixTimeMilliseconds(n);return null;}
    private static bool Property(JsonElement value,string name,out JsonElement property)
    {property=default;return value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out property);}
    private static string? String(JsonElement value,string name)=>Property(value,name,out var property)&&property.ValueKind==JsonValueKind.String?property.GetString():null;
}
