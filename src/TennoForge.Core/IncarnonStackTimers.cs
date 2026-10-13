namespace TennoForge.Core;

public record IncarnonStackTimerRule(string Condition,string Trigger,double Duration,int Cap,string Source);

/// <summary>
/// Source articles give these trigger amounts, caps and durations, but do not specify
/// shared refresh versus independent/one-at-a-time expiry. Preserve that boundary.
/// A single trigger has an exact expiry; repeated triggers are exact until the first
/// potentially expiring stack. Never silently choose an undocumented renewal policy.
/// </summary>
public sealed class IncarnonStackTimers
{
    public static IncarnonStackTimerRule[] RulesFor(Build build)
    {
        var definition=IncarnonEngine.ForWeapon(build.WeaponId);if(definition is null)return [];
        return definition.Perks.Where(p=>(build.Melee?.Evolutions??[]).Contains(p.Id)).Select(p=>(definition.Code,p.Condition) switch
        {
            ("Ruvox","RuvoxKills")=>new IncarnonStackTimerRule("RuvoxKills","Kill",15,3,"https://wiki.warframe.com/w/Ruvox?oldid=2816913"),
            ("Innodem","AirborneKills")=>new("AirborneKills","AirborneKill",10,3,"https://wiki.warframe.com/w/Innodem?oldid=2816908"),
            ("Okina","CriticalStacks")=>new("CriticalStacks","Critical",10,5,"https://wiki.warframe.com/w/Okina_Incarnon_Genesis?oldid=2797886"),
            ("Sibear","ColdKills")=>new("ColdKills","ColdKill",10,4,"https://wiki.warframe.com/w/Sibear_Incarnon_Genesis?oldid=2773653"),
            ("Thalys","ThalysStatus")=>new("ThalysStatus","Status",8,20,ThalysMechanic.Source),
            _=>null
        }).Where(r=>r is not null).Cast<IncarnonStackTimerRule>().ToArray();
    }
    public static string Label(string condition)=>condition switch{"RuvoxKills"=>"리썰 임페투스","AirborneKills"=>"스우핑 런지","CriticalStacks"=>"시너지스트 슈어티","ColdKills"=>"마운팅 아발란체","ThalysStatus"=>"드레드풀 리치",_=>condition};
    private sealed class Counter{public double First,Last;public int Triggers;}
    private readonly Dictionary<string,Counter> counters=[];
    public IncarnonStackTimerRule[] Rules{get;}
    public IncarnonStackTimers(Build build){Rules=RulesFor(build);}
    public double NextExpiry=>Rules.Where(r=>counters.TryGetValue(r.Condition,out var c)&&c.Triggers>0).Select(r=>counters[r.Condition].First+r.Duration).DefaultIfEmpty(double.PositiveInfinity).Min();
    public int Count(string condition,double time)
    {
        var rule=Rules.Single(r=>r.Condition==condition);
        if(!counters.TryGetValue(condition,out var count)||count.Triggers==0)return 0;
        if(time<count.First+rule.Duration-1e-9)return Math.Min(rule.Cap,count.Triggers);
        if(count.Triggers>1)throw new CombatRuleUnavailableException($"{Label(condition)}: {rule.Duration}초 중첩의 전체 갱신/독립 만료/단계 감소 규칙이 위키에 미기재입니다. 최초 잠재 만료 {count.First+rule.Duration:0.###}초 이후를 고정 중첩으로 연장하지 않습니다. {rule.Source}");
        count.Triggers=0;return 0;
    }
    public void Trigger(string trigger,double time)
    {
        foreach(var rule in Rules.Where(r=>r.Trigger==trigger))
        {
            _=Count(rule.Condition,time);
            if(!counters.TryGetValue(rule.Condition,out var count))counters[rule.Condition]=count=new();
            if(count.Triggers==0)count.First=time;
            count.Last=time;count.Triggers++;
        }
    }
}
