namespace TennoForge.Core;

public sealed record FarmingMissionMatch(FissureMission Mission,string[] GoalIds,string[] RelicNames);
public static class FarmingMissions
{
    public static int Tier(InventoryRelic relic)
    {
        var leaf=relic.Family.Split('/').Last();
        return leaf.Length>=2&&leaf[0]=='T'&&leaf[1] is >='1' and <='5'?leaf[1]-'0':0;
    }
    public static FarmingMissionMatch[] Match(InventoryCatalog catalog,InventorySnapshot inventory,FarmingPlan plan,IEnumerable<FissureMission> fissures,DateTimeOffset now,bool notificationsOnly=false)
    {
        var targets=plan.Goals.Where(g=>(!notificationsOnly||g.Goal.Notify)&&!g.Secured).Select(g=>(Goal:g,Missing:g.Requirements.Where(r=>r.Missing>0).Select(r=>r.ItemId).ToHashSet())).ToArray();
        var relics=catalog.Relics.Where(r=>inventory.Count(r.Id)>0&&Tier(r)>0).ToArray();var result=new List<FarmingMissionMatch>();
        foreach(var fissure in fissures.Where(f=>f.Active(now)))
        {
            // Official Update 35.5: Omnia accepts Lith/Meso/Neo/Axi, not Requiem.
            var usable=relics.Where(r=>fissure.Tier==Tier(r)||fissure.Tier==6&&Tier(r) is >=1 and <=4).ToArray();
            var goals=targets.Where(g=>usable.Any(r=>r.Rewards.Any(p=>g.Missing.Contains(p.Id)))).ToArray();
            if(goals.Length==0)continue;
            var needed=goals.SelectMany(g=>g.Missing).ToHashSet();
            result.Add(new(fissure,goals.Select(g=>g.Goal.Goal.ItemId).ToArray(),usable.Where(r=>r.Rewards.Any(p=>needed.Contains(p.Id))).Select(r=>r.Name).Distinct().ToArray()));
        }
        return result.ToArray();
    }
    public static ScheduleAlert[] Due(InventoryCatalog catalog,InventorySnapshot inventory,FarmingPlan plan,WorldScheduleData data,DateTimeOffset now,ISet<string> delivered,Func<FissureMission,bool>? allowed=null)
    {
        if(data.Cached||!FissureAlerts.Fresh(data.Snapshot,now))return [];
        return Match(catalog,inventory,plan,data.Snapshot!.Fissures,now,true).Where(m=>(allowed?.Invoke(m.Mission)??true)&&!delivered.Contains("goal-fissure:"+m.Mission.Id)&&!delivered.Contains("fissure:"+m.Mission.Id))
            .Select(m=>new ScheduleAlert("goal-fissure:"+m.Mission.Id,$"목표 성유물 · {m.Mission.Mode} {m.Mission.Mission}",string.Join(" · ",m.GoalIds.Select(catalog.Name))+"\n"+m.Mission.Node+" · "+string.Join(" · ",m.RelicNames.Take(3)),m.Mission.EndUtc)).ToArray();
    }
}
