using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed record ScheduleOverlayEntry(string Id,string Title,string Detail,string Value,string Badge="",bool Highlight=false,string Kind="timer");
internal sealed record ScheduleOverlayData(ScheduleOverlayEntry[] Timers,ScheduleOverlayEntry[] Fissures,bool ShowFissures,string Notice,string Source)
{
    internal string FissureFilter {get;init;}="전체";
    internal string PlainText=>string.Join("\n\n",Timers.Concat(Fissures).Select(r=>$"{r.Badge} · {r.Title}\n{r.Detail} · {r.Value}"))+(Notice.Length>0?"\n"+Notice:"");
}
internal sealed partial class SchedulePage
{
    private bool FissureAllowed(FissureMission f)=>Preferences.FissureView switch{1=>f.SteelPath&&!f.Storm,2=>!f.SteelPath&&!f.Storm,3=>f.Storm,4=>Preferences.FissureRules.Any(r=>r.Matches(f)),_=>true};
    internal ScheduleOverlayData OverlayData(DateTimeOffset now)
    {
        var timers=new List<ScheduleOverlayEntry>();var ids=Preferences.OverlayItems.ToHashSet();
        if(Preferences.ShowGameAlerts)
        foreach(var a in (Data.Snapshot?.Alerts??[]).Where(a=>a.Active(now)).OrderBy(a=>a.EndUtc))timers.Add(new("alert:"+a.Id,$"얼럿 · {a.Mission}",$"{a.Reward} · {a.Node}",WorldScheduleEngine.Countdown(a.EndUtc,now),GameAlerts.Fresh(Data.Snapshot,now)&&!Data.Cached?"":"저장 자료",true,"alert"));
        if(ids.Contains("nextS"))
        {
            var next=Data.Arbitrations.NextS(CatalogData,now,CurrentFilter() with{Tier="S"});var n=CatalogData.Nodes.FirstOrDefault(n=>n.Id==next?.NodeId);
            timers.Add(n is null?new("nextS","다음 S티어 중재","해당 일정 없음","—"):new("nextS","다음 S티어 중재",$"{n.Mission} · {n.Planet} / {n.Name}",WorldScheduleEngine.Countdown(next!.StartUtc,now),Data.ArbitrationConflict(now)?"일정 불일치":"예상",true));
        }
        if(ids.Contains("arbitration"))
        {var slot=Data.Arbitrations.Current(now);var n=CatalogData.Nodes.FirstOrDefault(n=>n.Id==slot?.NodeId);if(n is not null&&CurrentFilter().Matches(n))timers.Add(new("arbitration","현재 중재",$"{n.Tier} · {n.Mission} · {n.Planet} / {n.Name}",WorldScheduleEngine.Countdown(slot!.EndUtc,now),"예상"));}
        foreach(var def in WorldScheduleEngine.Definitions.Where(d=>ids.Contains(d.Id)))
        {
            var a=Data.Snapshot?.Cycles.FirstOrDefault(c=>c.Id==def.Id);var phase=a is null?null:WorldScheduleEngine.CycleAt(a,now);
            timers.Add(phase is null?new(def.Id,def.Name,"자료 갱신 필요","—"):new(def.Id,def.Name,phase.Label,WorldScheduleEngine.Countdown(phase.EndUtc,now),Data.CycleFresh(a!,now)?"":"저장 자료 예상"));
        }
        foreach(var id in new[]{"tenet","coda"}.Where(ids.Contains))
        {var w=WorldScheduleEngine.ShopAt(CatalogData,id,now);timers.Add(new(id,id=="tenet"?"테넷 상점":"코다 상점",(w.Batch.Length>0?w.Batch+" 회차 · ":"")+"보너스 교체",WorldScheduleEngine.Countdown(w.EndUtc,now),"예상"));}
        bool show=Preferences.ShowCurrentFissures||ids.Contains("fissures")||ids.Any(id=>id.StartsWith("fissure:",StringComparison.Ordinal));
        bool fresh=!Data.Cached&&FissureAlerts.Fresh(Data.Snapshot,now);var fissures=new List<ScheduleOverlayEntry>();
        foreach(var f in (Data.Snapshot?.Fissures??[]).Where(f=>f.Active(now)))
        {
            bool match=Preferences.FissureRules.Any(r=>r.Matches(f));bool pinned=ids.Contains("fissure:"+f.Id);
            bool allowed=FissureAllowed(f);
            if(!(Preferences.ShowCurrentFissures?allowed:pinned||ids.Contains("fissures")&&match))continue;
            fissures.Add(new(f.Id,$"{f.TierLabel} · {f.Mission}",f.Node,WorldScheduleEngine.Countdown(f.EndUtc,now),f.Mode+(match?" · 알림 대상":""),match,"fissure"));
        }
        var sorted=fissures.OrderByDescending(f=>f.Highlight).ThenBy(f=>f.Badge.StartsWith("강철의 길")?0:1).ThenBy(f=>f.Value,StringComparer.Ordinal).ToArray();
        string notice=!show?"":!fresh?"균열 자료 갱신 필요 · 저장된 일정은 참고용":sorted.Length==0?"현재 조건에 맞는 열린 균열이 없습니다.":"";
        return new(timers.ToArray(),sorted,show,notice,fresh?"":"저장 자료"){FissureFilter=Preferences.FissureView switch{1=>"강철의 길",2=>"일반",3=>"보이드 폭풍",4=>"알림 대상",_=>"전체"}};
    }
}
