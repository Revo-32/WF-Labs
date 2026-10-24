using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public record SimulorStackOutput(int Stacks,double Damage,double Radius);
public record OrdinarySimulorScenario(double? UnmergedLifetime=null,double? LimitedLifetime=null,SimulorStackOutput[]? Merge=null,SimulorStackOutput[]? Explosion=null,double? TickOutput=null,double? TickRadius=null,double? TickInterval=null,double? FirstTick=null,string VortexMerge="Unspecified");
public static class RangedOrdinarySimulor
{
    public static bool Applies(ReferenceWeapon w)=>w.Id=="wiki:primary/Simulor";
    public static void Validate(OrdinarySimulorScenario? s)
    {
        if(s is null)return;
        if(new[]{s.UnmergedLifetime,s.LimitedLifetime,s.TickInterval}.Any(v=>v is {} x&&(!double.IsFinite(x)||x<=0||x>300))||new[]{s.TickOutput,s.TickRadius,s.FirstTick}.Any(v=>v is {} x&&(!double.IsFinite(x)||x<0))||s.VortexMerge is not ("Unspecified" or "Separate" or "Merge"))throw new ArgumentException("일반 시뮬러 시나리오의 시간·피해·합체 조건 오류");
        foreach(var rows in new[]{s.Merge,s.Explosion})if(rows is not null&&(rows.Length>1000||rows.Select(r=>r.Stacks).Distinct().Count()!=rows.Length||rows.Any(r=>r.Stacks<1||r.Stacks>10000||!double.IsFinite(r.Damage)||r.Damage<=0||!double.IsFinite(r.Radius)||r.Radius<=0||r.Radius>1000)))throw new ArgumentException("중첩별 피해·반경은 양수이며 중복 중첩을 입력할 수 없습니다.");
    }
    public static JsonElement Attack(ReferenceWeapon w,JsonElement attack,int index,RangedConditions c)
    {
        if(!Applies(w))return attack;Validate(c.OrdinarySimulor);var n=JsonNode.Parse(attack.GetRawText())!;n["Trigger"]="Semi-Auto";
        if(index==0)n["CarrierProjectile"]=1;
        if(index==1)n["ForcedProcs"]=new JsonArray("Magnetic");
        if(index>0&&!c.SimulorUnstacked)
        {
            var rows=index==1?c.OrdinarySimulor?.Merge:c.OrdinarySimulor?.Explosion;
            var row=rows?.SingleOrDefault(r=>r.Stacks==c.SimulorValue)??(c.AllowApproximations?new SimulorStackOutput(c.SimulorValue,index==1?125:240*Math.Min(4,c.SimulorValue),index==1?4:4+Math.Min(4,c.SimulorValue)):null)??throw new CombatRuleUnavailableException($"일반 시뮬러 {c.SimulorValue}중첩의 {(index==1?"합체":"폭발")} 피해·반경 증가식은 원문에 없습니다. 중첩별 시나리오 수치를 지정하세요.");
            n["Damage"]=new JsonObject{["Magnetic"]=row.Damage};n["Range"]=row.Radius;
            if(index==2)n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=row.Radius,["Reduction"]=.6};
        }
        return JsonSerializer.SerializeToElement(n);
    }
}

public sealed class OrdinarySimulorState(OrdinarySimulorScenario scenario)
{
    private sealed class Orb{public int Value=1,Shot,Target;public double Arrival,End,Tick=double.PositiveInfinity;public bool Arrived;public bool Vortex=>Value>=4;}
    private readonly List<Orb> orbs=[];
    public int Count=>orbs.Count;
    public double Next=>orbs.Select(o=>Math.Min(o.Arrived?double.PositiveInfinity:o.Arrival,Math.Min(o.End,o.Tick))).DefaultIfEmpty(double.PositiveInfinity).Min();
    private static SimulorEmission Emit(Orb o,int component)=>new(component,o.Value,o.Value==1,false,o.Shot,o.Target);
    public SimulorEmission[] Launch(double time,double delay,int pellets,int shot,int target,bool merge)
    {
        RangedOrdinarySimulor.Validate(scenario);if(Count>=4)throw new InvalidOperationException("활성 구체는 최대 4개입니다.");
        if(!double.IsFinite(time)||time<0||!double.IsFinite(delay)||delay<0||pellets<1)throw new ArgumentException("구체 생성 조건 오류");
        var lifetime=(orbs.Count(o=>o.Vortex)>=3?scenario.LimitedLifetime:scenario.UnmergedLifetime)??throw new CombatRuleUnavailableException("일반 시뮬러의 미합체 수명 또는 특이점 3개일 때의 단축 수명이 원문에 없습니다. 시나리오 수명을 지정하세요.");
        if(delay>=lifetime)throw new CombatRuleUnavailableException("지정한 도착 시점보다 먼저 구체가 만료됩니다. 만료 위치에 맞는 배치를 지정하세요.");
        var orb=new Orb{Shot=shot,Target=target,Arrival=time+delay,End=time+lifetime};orbs.Add(orb);
        // Perfect accuracy merges duplicate pellets with their original at launch, before travel.
        var result=new List<SimulorEmission>();for(int n=1;n<pellets;n++)Merge(orb,time,shot,result);
        return result.Select(e=>e with{LaunchPoint=true}).ToArray();
    }
    private void Merge(Orb into,double time,int shot,List<SimulorEmission> output)
    {
        into.Value++;into.End=time+20;into.Shot=shot;output.Add(Emit(into,1));
        if(into.Value==4)
        {
            if(orbs.Count(o=>o.Vortex)>3)throw new CombatRuleUnavailableException("네 번째 특이점 생성 시 초과 멀티샷의 처리 규칙은 원문에 없습니다.");
            var phase=scenario.FirstTick??throw new CombatRuleUnavailableException("일반 시뮬러 특이점의 첫 틱 시점은 원문에 없습니다.");
            var interval=scenario.TickInterval??throw new CombatRuleUnavailableException("일반 시뮬러 특이점의 틱 주기는 원문에 없습니다.");
            if(phase>interval)throw new ArgumentException("첫 틱은 설정한 주기 이내여야 합니다.");into.Tick=time+phase;
        }
    }
    public SimulorEmission[] Advance(double time,bool merge)
    {
        var result=new List<SimulorEmission>();
        foreach(var o in orbs.ToArray())
        {
            if(!orbs.Contains(o))continue;
            if(o.End<=time+1e-9){result.Add(Emit(o,2));orbs.Remove(o);continue;}
            if(!o.Arrived&&o.Arrival<=time+1e-9)
            {
                o.Arrived=true;
                if(merge&&orbs.Count(v=>v.Vortex)<3)
                {
                    var candidates=orbs.Where(v=>v!=o&&v.Arrived&&v.End>time+1e-9&&v.Target==o.Target&&(scenario.VortexMerge!="Separate"||!v.Vortex)).ToArray();if(candidates.Length>1)throw new CombatRuleUnavailableException("같은 위치에 여러 합체 후보가 있습니다. 구체 간 충돌 순서는 원문에서 정해지지 않아 다른 위치·시점 배치가 필요합니다.");var recipient=candidates.SingleOrDefault();
                    if(recipient?.Vortex==true&&scenario.VortexMerge=="Unspecified")throw new CombatRuleUnavailableException("완성된 일반 시뮬러 특이점에 새 구체가 닿을 때의 세부 규칙이 없습니다. 합체/별도 배치 시나리오를 선택하세요.");
                    if(recipient is not null&&(!recipient.Vortex||scenario.VortexMerge=="Merge"))
                    {orbs.Remove(o);for(int i=0;i<o.Value;i++)Merge(recipient,time,o.Shot,result);}
                }
            }
        }
        foreach(var o in orbs.Where(o=>o.Tick<=time+1e-9)){if(!o.Arrived)throw new CombatRuleUnavailableException("이동 중 형성된 특이점의 틱 위치를 확정할 수 없습니다. 도착 시간과 첫 틱 시나리오를 확인하세요.");result.Add(Emit(o,3));o.Tick+=scenario.TickInterval!.Value;}
        return result.ToArray();
    }
    public SimulorEmission[] Detonate(double time)
    {
        if(orbs.Any(o=>!o.Arrived&&o.Arrival>time+1e-9))throw new CombatRuleUnavailableException("이동 중 일반 시뮬러 구체의 위치가 지정되지 않았습니다. 도착 후 기폭을 예약하세요.");
        var result=orbs.Select(o=>Emit(o,2)).ToArray();orbs.Clear();return result;
    }
}
