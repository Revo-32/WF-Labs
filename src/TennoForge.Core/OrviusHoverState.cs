using System.Text.Json;

namespace TennoForge.Core;

public record OrviusHover(double Launch,double Attach,int Target=1,int Throw=1,double? Detonate=null,double? Returned=null,double[]? TickTimes=null,bool UseInfobox=false,double? Travel=null);
public sealed class OrviusHoverState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Glaives/TeshinGlaive/TnTeshinGlaiveWep";
    public const string Source="https://wiki.warframe.com/w/Orvius?oldid=2796804";
    public const string Conflict="오르비우스 매달기: 본문은 초당10베기·상확 미기재, 데이터 표는75냉기·치확20%·치피2.2·상확60%로 상충합니다. 데이터 표를 선택한 결과는 조건부 계산입니다.";
    public record Flight(OrviusHover Input,double End,bool SuspensionBlocked);
    private readonly Dictionary<int,Flight> flights=[];
    public static double End(OrviusHover h)=>Math.Min(h.Attach+3,h.Detonate??double.PositiveInfinity);
    public static void Validate(OrviusHover[] hovers,int targets)
    {
        if(hovers.Length>64||hovers.GroupBy(h=>h.Throw).Any(g=>g.Count()>1))throw new ArgumentException("오르비우스 투척 번호는 중복 없이 최대64개입니다.");
        foreach(var h in hovers)
        {
            MeleeEngine.Range(h.Launch,0,300,"오르비우스 발사");MeleeEngine.Range(h.Attach,h.Launch,300,"오르비우스 매달기 시작");MeleeEngine.Range(h.Target,1,targets,"오르비우스 대상");MeleeEngine.Range(h.Throw,1,10000,"오르비우스 투척 번호");
            if(h.Detonate is {} d)MeleeEngine.Range(d,h.Attach,h.Attach+3,"오르비우스 수동 기폭");
            if(h.Returned is {} r)MeleeEngine.Range(r,End(h),300,"오르비우스 회수 완료");
            if(h.TickTimes is null||h.TickTimes.Length!=3||h.TickTimes.Distinct().Count()!=3)throw new ArgumentException("3초 동안의 매달기 피해3회 시각을 각각 입력하세요. 수동 기폭 이후의 틱은 자동 취소됩니다.");
            foreach(var t in h.TickTimes)MeleeEngine.Range(t,h.Attach,h.Attach+3,"매달기 피해 시각");
            if(h.Travel is {} distance)MeleeEngine.Range(distance,0,30,"오르비우스 비행 거리");
        }
    }
    public void Launch(OrviusHover h)
    {
        if(flights.Values.Any(f=>f.Input.Returned is null||h.Launch<f.Input.Returned))throw new ArgumentException("다음 오르비우스를 던지기 전에 이전 회수 완료 시각을 지정하세요.");
        flights.Add(h.Throw,new(h,End(h),false));
    }
    public void Attach(int id,bool overguard)
    {
        var f=Get(id);flights[id]=f with {SuspensionBlocked=overguard};
    }
    public Flight Get(int id)=>flights.TryGetValue(id,out var f)?f:throw new ArgumentException("아직 발사하지 않은 오르비우스입니다.");
    public bool Suspends(int id,double time)=>Get(id) is var f&&!f.SuspensionBlocked&&time>=f.Input.Attach&&time<f.End;
    public bool Tick(int id,double time)=>Get(id) is var f&&time>=f.Input.Attach&&(f.Input.Detonate is null?time<=f.End:time<f.End);
    public bool InFlight(double time)=>flights.Values.Any(f=>time>=f.Input.Launch&&(f.Input.Returned is null||time<f.Input.Returned));
    public static Calculation Calculate(Catalog catalog,Build build,bool explosion=false,double distance=0,bool useInitialComboFloor=true)
    {
        if(build.WeaponId!=WeaponId)throw new ArgumentException("오르비우스가 필요합니다.");
        var name=explosion?"Throw Recall Explosion":"Hover Attack";
        var raw=MeleeRuleCatalog.Load().Weapon(WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee! with {Attack=name,Distance=distance,TargetIndex=1,Finisher=null,Tennokai=explosion&&build.Melee.Tennokai}};
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,MeleeEngine.AttackPolicy(WeaponId,name) with {FollowThrough=false,Tennokai=explosion},NoEnergyChannel:true));
    }
    internal static string Signature(Catalog catalog,Build b)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
