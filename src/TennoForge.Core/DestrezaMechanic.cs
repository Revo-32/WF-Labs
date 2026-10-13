using System.Text.Json;

namespace TennoForge.Core;

/// <summary>A source-uncertainty interval, not a Monte Carlo confidence interval.</summary>
public record SourceInterval(double Minimum,double Maximum)
{
    public bool Exact=>Math.Abs(Maximum-Minimum)<=1e-9*Math.Max(1,Math.Abs(Maximum));
}
public record DestrezaProjectileResult(SourceInterval Damage,SourceInterval TargetDamage,SourceInterval CriticalChance,SourceInterval CriticalMultiplier,
    SourceInterval ForcedSlashTick,double SlashDuration,int SlashTicks,int Count,double Lifetime,bool HeavyWindup,double? StatusChance,string[] Notes);

public static class DestrezaMechanic
{
    public const string Source="https://wiki.warframe.com/w/Destreza_Incarnon_Genesis?oldid=2816743";
    public const string CoSource="https://wiki.warframe.com/w/Condition_Overload_(Mechanic)?oldid=2815998";
    public static bool Applies(Build build)=>build.WeaponId is MeleeMechanicScope.DestrezaId or MeleeMechanicScope.DestrezaPrimeId&&build.Melee?.Incarnon==true;

    public static DestrezaProjectileResult Calculate(Catalog catalog,Build build,bool heavyWindup)
    {
        if(!Applies(build))throw new ArgumentException("변신 중인 데스트레자 빌드가 필요합니다.");
        var lower=Evaluate(catalog,build,heavyWindup);var upper=Evaluate(catalog,build,heavyWindup,upper:true);
        SourceInterval Bounds(double a,double b)=>new(Math.Min(a,b),Math.Max(a,b));
        var slash=lower.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash);
        var cc=Bounds(lower.CriticalChance,upper.CriticalChance);var cd=Bounds(lower.CriticalMultiplier,upper.CriticalMultiplier);
        // Enclose the independently bounded critical stats. Endpoint damage
        // alone is insufficient if a negative modifier reverses one slope.
        var minCritical=1+lower.Melee.StealthBonus+cc.Minimum*(cd.Minimum-1);
        var maxCritical=1+lower.Melee.StealthBonus+cc.Maximum*(cd.Maximum-1);
        var currentCritical=1+lower.Melee.StealthBonus+lower.CriticalChance*(lower.CriticalMultiplier-1);
        SourceInterval Damage(double basis)=>new(basis*minCritical,basis*maxCritical);
        return new(Damage(lower.Damage.Values.Sum()),Damage(lower.Melee.TargetDamage.Values.Sum()),cc,cd,
            Damage(slash.DamagePerTick/currentCritical),slash.Duration,slash.Ticks,5,2,heavyWindup,null,[
            "투사체1개·명중 시점의 상태 고정. 5개가 각각 강제 충격·베기를 부여하므로 실제 연속 명중의 컨오는 이후 증가합니다.",
            "기본711/765 · 컨오 가산 계수2 · 진화II 기본 피해 제외 · 변신 피해/관통·원소·팩션·치명·상태 피해 모드 적용.",
            "명중할 때 강공격 준비 동작 중인 경우에만 콤보 배수·트루 스틸2배·킬링 블로우 적용. 이미 발사된 투사체도 명중 시점으로 판정합니다.",
            "2초 비행·무한 신체 관통·거리 감쇠 없음. 절대 비행 속도는 위키 미기재라 거리를 임의의 명중 시각으로 바꾸지 않습니다.",
            "듀플리케이트·인플루언스 발동/전파 제외. 기본 상확은 본문과 무기 모듈에 미기재이며 일반 칼날 상확으로 대체하지 않습니다.",
            "상확을 읽는 디사이시브 스테처/다우티는 확인된 최솟값과 각 효과의 명시적 상한을 따로 계산합니다. 이 범위는 표본 오차가 아니며 무작위 상태 피해 총합을 뜻하지 않습니다.",Source,CoSource]);
    }
    internal static Calculation Evaluate(Catalog catalog,Build build,bool heavyWindup,bool upper=false,double? assumedStatusChance=null,bool useInitialComboFloor=true)
    {
        if(!Applies(build))throw new ArgumentException("변신 중인 데스트레자 빌드가 필요합니다.");
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);
        var normal=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Normal Attack");
        var conditions=new Dictionary<string,double>(build.Melee!.Conditions??[]){["HeavyAnimation"]=heavyWindup?1:0};
        conditions.Remove("ProjectilesHit");
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack="Spectral Rapier",Tennokai=false,Finisher=null,TargetIndex=1,Distance=0,Conditions=conditions}};
        // The Wiki omits base status chance. Zero is only the mathematical
        // endpoint of that unknown, never exposed as the projectile's SC.
        var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["AttackName"]="Spectral Rapier",
            ["Damage"]=normal.GetProperty("Damage").EnumerateObject().ToDictionary(p=>p.Name,p=>4.5*p.Value.GetDouble()),
            ["CritChance"]=normal.Number("CritChance"),["CritMultiplier"]=normal.Number("CritMultiplier"),["StatusChance"]=assumedStatusChance??0d,
            ["ForcedProcs"]=new[]{"Impact","Slash"}});
        var policy=new AttackRule("Spectral Rapier",Co:2,Heavy:true,HeavyAnimationOnly:true,Tennokai:false,Incarnon:true,HitCombo:false,
            EvolutionDamage:false,Combo:heavyWindup,FollowThrough:false,Code:"DestrezaSpectral");
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,NoRandomStatus:assumedStatusChance is null,NoDuplicate:true,NoEnergyChannel:true,UnknownStatusUpperBound:upper));
    }

}
