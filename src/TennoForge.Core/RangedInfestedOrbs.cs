using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public static class RangedInfestedOrbs
{
    public const string WeaponId="wiki:primary/Mutalist Quanta";
    public static bool Applies(ReferenceWeapon w)=>w.Id==WeaponId;
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)=>!Applies(w)?attacks:[..attacks,JsonSerializer.SerializeToElement(new{AttackName="Bursting Mass · stored explosion",ShotType="AoE",Trigger="Semi",FireRate=10,AmmoCost=1,CritChance=0,CritMultiplier=1,StatusChance=0,Multishot=1,Range=15,Damage=new{Toxin=1}})];
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult r)
    {
        if(!Applies(w))return r;var c=b.Conditions??new();Validate(c);
        var scope=r.Scope.Concat(new[]{"감염 구체: 초당 Radiation 타격, 정지 후 9초에 Toxin 폭발. 합체는 수명을 갱신하지 않으며 3중첩 구체는 합체하지 않습니다. 버스팅 매스는 별도 15m 저장 폭발입니다. 비행 정지 시각·첫 타격 위상은 지정 시나리오를 사용합니다."}).ToArray();
        if(b.AttackIndex==0)return r with{Scope=scope};
        r.Modifiers!["CriticalTierUpgradeChance"]=0;r.Modifiers["NonCriticalAttritionChance"]=0;r.Modifiers["ExtraMultishotDamage"]=0;r.Modifiers["ExtraProjectileDamageRatio"]=1;
        if(b.AttackIndex==1)
        {
            if(!c.InfestedPulse){r.Modifiers["CarrierProjectile"]=1;return r with{Damage=[],ExpectedShot=0,BurstDps=null,SustainedDps=null,Scope=scope};}
            r.Modifiers["EffectiveRange"]=1;
            var damage=c.RadialDistance<=1?r.Damage:new Dictionary<DamageType,double>();
            return r with{Damage=damage,CriticalChance=0,CriticalMultiplier=1,StatusChance=1,Multishot=1,ExpectedShot=damage.Values.Sum(),BurstDps=null,SustainedDps=null,Scope=scope};
        }
        if(b.AttackIndex==2)return r with{Multishot=1,ExpectedShot=r.Damage.Values.Sum()*RangedEngine.CriticalExpectation(r.CriticalChance,r.CriticalMultiplier),BurstDps=null,SustainedDps=null,Scope=scope,TimingIssues=["감염 구체를 발사하면 만료·재발사 시 폭발합니다. 구체 발사를 선택하세요."]};
        if(r.Modifiers.GetValueOrDefault("BurstingMass")<=0)throw new ArgumentException("버스팅 매스를 장착해야 저장 폭발을 계산할 수 있습니다.");
        var stored=c.InfestedStored??new Dictionary<DamageType,double>();if(stored.Any(p=>!Enum.IsDefined(p.Key)||!double.IsFinite(p.Value)||p.Value<0))throw new ArgumentException("감염 구체 저장 피해 오류");
        var range=15d;var factor=Math.Max(0,1-c.RadialDistance/range)*r.FactionFactor;
        var packet=stored.ToDictionary(p=>p.Key,p=>p.Value*factor);r.Modifiers["EffectiveRange"]=range;
        return r with{Damage=packet,ArsenalDamage=new(stored),CriticalChance=0,CriticalMultiplier=1,StatusChance=0,Multishot=1,ExpectedShot=packet.Values.Sum(),BurstDps=null,SustainedDps=null,BaseDamage=stored.Values.Sum(),BaseFactor=1,CoFactor=1,StatusBaseFactor=1,Scope=scope,TimingIssues=["저장 폭발은 감염 구체의 만료·재발사에서 발생합니다."]};
    }
    public static double Radius(int stacks)=>2.4+2*Math.Clamp(stacks,1,3);
    public static Dictionary<DamageType,double> Absorb(IReadOnlyDictionary<DamageType,double> nonCriticalWithoutFaction,double elementalBasis,int orbStacks,double coefficient)
    {
        if(!double.IsFinite(elementalBasis)||elementalBasis<0||!double.IsFinite(coefficient)||coefficient<0||nonCriticalWithoutFaction.Values.Any(v=>!double.IsFinite(v)||v<0))throw new ArgumentException("버스팅 매스 흡수 피해 입력 오류");
        var damage=nonCriticalWithoutFaction.ToDictionary(p=>p.Key,p=>p.Value);damage[DamageType.Electricity]=damage.GetValueOrDefault(DamageType.Electricity)+elementalBasis*RangedOrbBuffs.Electricity([orbStacks]);
        return damage.ToDictionary(p=>p.Key,p=>p.Value*.66*coefficient);
    }
    public static JsonElement Attack(ReferenceWeapon w,JsonElement attack,int index,RangedConditions c)
    {
        if(!Applies(w)||index==0)return attack;
        var n=JsonNode.Parse(attack.GetRawText())!;
        if(index==1){n["Range"]=1;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=1,["Reduction"]=0};n["CritMultiplier"]=1;if(c.InfestedPulse)n["ShotType"]="AoE";}
        if(index==2){var radius=Radius(c.InfestedOrbStacks);n["Damage"]=new JsonObject{["Toxin"]=100*c.InfestedOrbStacks};n["Range"]=radius;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=radius,["Reduction"]=.5};}
        return JsonSerializer.SerializeToElement(n);
    }
    public static void Validate(RangedConditions c)
    {
        if(c.InfestedOrbStacks is <1 or >3||c.InfestedFirstPulse is {} pulse&&(!double.IsFinite(pulse)||pulse<0||pulse>1)||c.InfestedStopSeconds is {} stop&&(!double.IsFinite(stop)||stop<0||stop>300)||c.InfestedMergeOverflow is not ("Unspecified" or "Discard" or "Remainder"))throw new ArgumentException("감염 구체 시간·합체 설정 오류");
    }
}
public record InfestedOrbEmission(int Component,int Stacks,int Shot,int Target,Dictionary<DamageType,double>? Stored=null,double X=0,double Y=0);
public record InfestedCrossing(double Time,int Orb);
/// <summary>Chosen shared landing point; merge never refreshes the recipient lifetime.</summary>
public sealed class RangedInfestedOrbState
{
    private sealed class Orb
    {
        public int Id,Value,Shot,Target;public double Spawn,X,Y,FromX,FromY,FromTime,Arrives,End,Tick;public bool Arrived,HasStopped;public Dictionary<DamageType,double> Stored=[];
    }
    private readonly List<Orb> orbs=[];private int sequence;
    static (double X,double Y) Position(Orb o,double time){var f=o.Arrives<=o.FromTime?1:Math.Clamp((time-o.FromTime)/(o.Arrives-o.FromTime),0,1);return(o.FromX+(o.X-o.FromX)*f,o.FromY+(o.Y-o.FromY)*f);}
    public double Next=>orbs.Select(o=>Math.Min(o.Arrived?double.PositiveInfinity:o.Arrives,Math.Min(o.End,o.Tick))).DefaultIfEmpty(double.PositiveInfinity).Min();
    public int Count=>orbs.Count;
    public int[] Stacks=>orbs.Where(o=>o.Arrived).Select(o=>o.Value).ToArray();
    static InfestedOrbEmission[] Explode(Orb o,double time){var p=Position(o,time);return o.Stored.Count==0?[new(2,o.Value,o.Shot,o.Target,X:p.X,Y:p.Y)]:[new(2,o.Value,o.Shot,o.Target,X:p.X,Y:p.Y),new(3,o.Value,o.Shot,o.Target,new(o.Stored),p.X,p.Y)];}
    public InfestedOrbEmission[] Launch(double time,double arrival,int rolled,double multishot,int shot,int target,double? firstPulse,bool bursting,double x=0,double y=0)
    {
        if(firstPulse is null)throw new CombatRuleUnavailableException("감염 구체의 첫 1초 주기 타격 위상은 원문에 없습니다. 첫 타격 시점을 지정하세요.");
        if(!double.IsFinite(time)||time<0||!double.IsFinite(arrival)||arrival<0||arrival>300||rolled<0||rolled>100||firstPulse<0||firstPulse>1)throw new ArgumentException("감염 구체 생성 입력 오류");
        var output=new List<InfestedOrbEmission>();
        if(bursting){foreach(var old in orbs)output.AddRange(Explode(old,time));orbs.Clear();}
        if(bursting&&multishot>3&&multishot<=4){output.Add(new(2,2,shot,target,X:0,Y:0));rolled=2;}
        while(rolled>0)
        {
            var value=Math.Min(3,rolled);rolled-=value;var orb=new Orb{Id=sequence++,Spawn=time,FromTime=time,X=x,Y=y,Value=value,Shot=shot,Target=target,Arrives=time+arrival,End=time+arrival+9,Tick=time+firstPulse.Value};
            if(bursting&&orbs.Count>0){output.AddRange(Explode(orb,time));continue;}orbs.Add(orb);
        }
        return output.ToArray();
    }
    public InfestedOrbEmission[] Advance(double time,bool merge,string overflow)
    {
        var output=new List<InfestedOrbEmission>();
        foreach(var o in orbs.ToArray())
        {
            if(!orbs.Contains(o))continue;
            if(o.End<=time+1e-9){output.AddRange(Explode(o,time));orbs.Remove(o);continue;}
            if(!o.Arrived&&o.Arrives<=time+1e-9)
            {
                o.Arrived=true;o.HasStopped=true;var other=merge&&o.Value<3?orbs.FirstOrDefault(v=>v!=o&&v.Arrived&&v.Value<3&&Math.Abs(v.X-o.X)<1e-6&&Math.Abs(v.Y-o.Y)<1e-6&&v.End>time+1e-9):null;
                if(other is not null)
                {
                    var sum=other.Value+o.Value;
                    if(sum>3&&overflow=="Unspecified")throw new CombatRuleUnavailableException("부분 합체 2+2의 초과 중첩 보존 여부는 원문에 없습니다. 초과분 처리 기준을 지정하세요.");
                    other.Value=Math.Min(3,sum);foreach(var p in o.Stored)other.Stored[p.Key]=other.Stored.GetValueOrDefault(p.Key)+p.Value;
                    if(sum>3&&overflow=="Remainder"){o.Value=sum-3;o.Stored.Clear();}else{orbs.Remove(o);continue;}
                }
            }
            if(o.Tick<=time+1e-9){var p=Position(o,time);output.Add(new(1,o.Value,o.Shot,o.Target,X:p.X,Y:p.Y));o.Tick+=1;}
        }
        return output.ToArray();
    }
    public void Absorb(int ordinal,Dictionary<DamageType,double> packet)
    {
        var active=orbs.Where(o=>o.Arrived).ToArray();if(ordinal<0||ordinal>=active.Length)throw new ArgumentException("흡수할 구체가 없습니다.");
        foreach(var p in packet)active[ordinal].Stored[p.Key]=active[ordinal].Stored.GetValueOrDefault(p.Key)+p.Value;
    }
    public InfestedOrbEmission[] Detonate(double time){var output=orbs.SelectMany(o=>Explode(o,time)).ToArray();orbs.Clear();return output;}
    public int? Value(int id)=>orbs.FirstOrDefault(o=>o.Id==id)?.Value;
    public bool Move(int id,double time,double x,double y,double travel)
    {
        var orb=orbs.SingleOrDefault(o=>o.Id==id);if(orb is null||orb.End<=time)return false;
        var p=Position(orb,time);var hadStopped=orb.HasStopped;orb.FromX=p.X;orb.FromY=p.Y;orb.FromTime=time;orb.X=x;orb.Y=y;orb.Arrives=time+travel;orb.Arrived=false;
        if(!hadStopped)orb.End=orb.Arrives+9;return true;
    }
    public (double X,double Y)? Coordinates(int id,double time)=>orbs.SingleOrDefault(o=>o.Id==id) is {} orb?Position(orb,time):null;
    public void AbsorbId(int id,IReadOnlyDictionary<DamageType,double> packet){var o=orbs.SingleOrDefault(o=>o.Id==id);if(o is null)return;foreach(var p in packet)o.Stored[p.Key]=o.Stored.GetValueOrDefault(p.Key)+p.Value;}
    // Intersect a constant-speed shot with each sphere during both moving and stationary phases.
    public InfestedCrossing[] Crossings(double time,double x,double y,double speed,double? from=null)
    {
        if(!double.IsFinite(speed)||speed<=0)throw new ArgumentException("탄환 속도 오류");var length=Math.Sqrt(x*x+y*y);if(length<=0)return orbs.Where(o=>time<o.End).Where(o=>{var p=Position(o,time);return p.X*p.X+p.Y*p.Y<=1;}).Select(o=>new InfestedCrossing(time,o.Id)).ToArray();
        var bx=x/length*speed;var by=y/length*speed;var output=new List<InfestedCrossing>();
        foreach(var o in orbs)
        {
            double found=double.PositiveInfinity;
            foreach(var phase in new[]{(Start:Math.Max(Math.Max(time,from??time),o.FromTime),End:Math.Min(o.Arrives,time+length/speed),Moving:true),(Start:Math.Max(Math.Max(time,from??time),o.Arrives),End:Math.Min(o.End,time+length/speed),Moving:false)})
            {
                if(phase.End<phase.Start||phase.Start>=o.End)continue;var p=Position(o,phase.Start);var ox=phase.Moving&&o.Arrives>o.FromTime?(o.X-o.FromX)/(o.Arrives-o.FromTime):0;var oy=phase.Moving&&o.Arrives>o.FromTime?(o.Y-o.FromY)/(o.Arrives-o.FromTime):0;
                var dx=bx*(phase.Start-time)-p.X;var dy=by*(phase.Start-time)-p.Y;var vx=bx-ox;var vy=by-oy;var a=vx*vx+vy*vy;var b=2*(dx*vx+dy*vy);var c=dx*dx+dy*dy-1;double after;
                if(c<=0)after=0;else{var discriminant=b*b-4*a*c;if(a<=0||discriminant<0)continue;after=(-b-Math.Sqrt(discriminant))/(2*a);}
                if(after>=0&&phase.Start+after<=phase.End&&phase.Start+after<o.End)found=Math.Min(found,phase.Start+after);
            }
            if(double.IsFinite(found))output.Add(new(found,o.Id));
        }
        return output.OrderBy(c=>c.Time).ThenBy(c=>c.Orb).ToArray();
    }
}
