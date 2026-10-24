using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

/// <summary>Synoid notes override the arsenal's per-stack upper bound and falloff display.</summary>
public static class RangedSimulor
{
    // https://wiki.warframe.com/w/Synoid_Simulor?oldid=2723712 (hash and normalized rules: data/ranged-simulor.json)
    public static bool Applies(ReferenceWeapon w)=>w.InternalId=="/Lotus/Weapons/Syndicates/CephalonSuda/LongGuns/CSSimulor";
    public static JsonElement Attack(ReferenceWeapon w,JsonElement attack,int index,RangedConditions c)
    {
        if(!Applies(w))return attack;
        if(c.SimulorValue is <1 or >10000||c.SimulorArrivalSeconds is {} delay&&(!double.IsFinite(delay)||delay<0||delay>=1)||c.SimulorFirstTick is {} tick&&(!double.IsFinite(tick)||tick<0||tick>.25))throw new ArgumentException("구체 도착 시간은 0 이상 1초 미만, 첫 틱은 0–0.25초입니다.");
        var n=JsonNode.Parse(attack.GetRawText())!;n["Trigger"]="Semi-Auto";
        if(index==1){n["Damage"]=new JsonObject{["Magnetic"]=c.SimulorEqualPair?150:125};n["Range"]=c.SimulorEqualPair?5:4;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=c.SimulorEqualPair?5:4,["Reduction"]=0};}
        if(index==2){var value=c.SimulorValue;var radius=c.SimulorUnstacked?4+value:4+Math.Min(4,value);n["Damage"]=new JsonObject{["Magnetic"]=240*Math.Min(4,value)};n["Range"]=radius;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=radius,["Reduction"]=1};}
        if(index==3)n["ShotType"]="AoE";
        return JsonSerializer.SerializeToElement(n);
    }
    public static double Arrival(RangedBuild b)
    {
        if(b.Conditions?.SimulorArrivalSeconds is {} delay)return delay;
        if(b.Distance==0)return 0;
        throw new CombatRuleUnavailableException("시뮬러의 구체 이동·합체 시간은 원문에 없습니다. 목표 지점 도착 시간을 교전 조건에 지정하세요.");
    }
}

public record SimulorEmission(int Component,int Value,bool Unstacked,bool EqualPair,int Shot,int Target,bool LaunchPoint=false);

/// <summary>Four physical orbs; multishot changes orb value, never creates independent damage pellets.</summary>
public sealed class RangedSimulorState(OrdinarySimulorScenario? ordinaryScenario=null)
{
    private readonly OrdinarySimulorState? ordinary=ordinaryScenario is null?null:new(ordinaryScenario);
    private sealed class Orb
    {
        public int Value,Shot,Target;public double Arrival,End,NextTick=double.PositiveInfinity;public bool Arrived,Merged;
        public bool Vortex=>Merged&&Value>=4;
    }
    private readonly List<Orb> orbs=[];
    public int Count=>ordinary?.Count??orbs.Count;
    public double Next=>ordinary?.Next??orbs.Select(o=>Math.Min(o.Arrived?double.PositiveInfinity:o.Arrival,Math.Min(o.End,o.NextTick))).DefaultIfEmpty(double.PositiveInfinity).Min();
    public SimulorEmission[] Launch(double time,double delay,int value,int shot,int target)
    {
        if(ordinary is not null)return ordinary.Launch(time,delay,value,shot,target,true);
        if(orbs.Count>=4)throw new InvalidOperationException("활성 구체는 최대 4개입니다.");
        if(!double.IsFinite(time)||time<0||!double.IsFinite(delay)||delay<0||delay>=1||value<1)throw new ArgumentException("구체 생성 조건 오류");
        orbs.Add(new(){Value=value,Shot=shot,Target=target,Arrival=time+delay,End=time+1});return [];
    }
    private static SimulorEmission Emit(Orb orb,int component,bool equal=false)=>new(component,orb.Value,!orb.Merged,equal,orb.Shot,orb.Target);
    public SimulorEmission[] Advance(double time,bool merge,double? firstTick)
    {
        if(ordinary is not null)return ordinary.Advance(time,merge);
        var output=new List<SimulorEmission>();
        foreach(var orb in orbs.ToArray())
        {
            if(!orbs.Contains(orb))continue;
            // Expiration wins at equal time: an expired orb cannot receive another merge.
            if(orb.End<=time+1e-9){output.Add(Emit(orb,2));orbs.Remove(orb);continue;}
            if(!orb.Arrived&&orb.Arrival<=time+1e-9)
            {
                orb.Arrived=true;
                var recipient=merge&&orb.Value<5?orbs.FirstOrDefault(o=>o!=orb&&o.Arrived&&o.End>time+1e-9&&(o.Merged||o.Value<5)&&o.Target==orb.Target):null;
                if(recipient is not null)
                {
                    orbs.Remove(orb);
                    if(recipient.Vortex)continue; // Consumed without refresh or a merge explosion.
                    var equal=recipient.Value==2&&orb.Value==2;
                    recipient.Value=Math.Min(4,recipient.Value+orb.Value);recipient.Merged=true;recipient.End=time+20;recipient.Shot=orb.Shot;
                    output.Add(Emit(recipient,1,equal));
                    if(recipient.Vortex)
                    {
                        if(firstTick is null)throw new CombatRuleUnavailableException("특이점은 0.25초마다 피해를 줍니다. 생성 후 첫 틱의 위치는 위키에 없으므로 0–0.25초 사이의 시나리오 값을 지정하세요.");
                        recipient.NextTick=time+firstTick.Value;
                    }
                }
            }
        }
        foreach(var orb in orbs.Where(o=>o.NextTick<=time+1e-9).ToArray()){output.Add(Emit(orb,3));orb.NextTick+=.25;}
        return output.ToArray();
    }
    public SimulorEmission[] Detonate(double time)
    {
        if(ordinary is not null)return ordinary.Detonate(time);
        if(orbs.Any(o=>!o.Arrived&&o.Arrival>time+1e-9))throw new CombatRuleUnavailableException("이동 중 구체의 수동 폭발 위치는 현재 목표 지점과 다릅니다. 구체 도착 이후에 기폭을 예약하세요.");
        var result=orbs.Select(o=>Emit(o,2)).ToArray();orbs.Clear();return result;
    }
}
