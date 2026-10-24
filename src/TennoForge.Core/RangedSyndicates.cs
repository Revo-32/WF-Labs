using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedSyndicateRule(string WeaponId,string Code,DamageType DamageType,WikiSource Source);
public record RangedAffinityEnemy(string Id,double BaseAffinity,WikiSource Source);
public record RangedSyndicateCatalog(RangedSyndicateRule[] Rules,RangedAffinityEnemy[] Enemies,WikiSource[] Sources);
public static class RangedSyndicates
{
    public static readonly RangedSyndicateCatalog Data=Load();
    static RangedSyndicateCatalog Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-syndicates.json")!;return JsonSerializer.Deserialize<RangedSyndicateCatalog>(s,Catalog.Json)!;}
    public static (string Code,DamageType Type,double Points)? Resolve(ReferenceWeapon weapon,IReadOnlyDictionary<string,double> effects)
    {
        var innate=Data.Rules.SingleOrDefault(r=>r.WeaponId==weapon.Id);
        if(innate is not null)return(innate.Code,innate.DamageType,1);
        foreach(var (code,type) in new[]{("Justice",DamageType.Blast),("Truth",DamageType.Gas),("Entropy",DamageType.Magnetic),("Sequence",DamageType.Radiation),("Blight",DamageType.Viral),("Purity",DamageType.Corrosive)})
            if(effects.GetValueOrDefault("Syndicate"+code)>0)return(code,type,Math.Min(1,effects["Syndicate"+code]));
        return null;
    }
    public static double KillAffinity(EnemySelection enemy)
    {
        var value=Data.Enemies.Single(e=>e.Id==enemy.EnemyId).BaseAffinity;
        return Math.Floor(value*((enemy.Eximus?3:1)+.1425*Math.Sqrt(enemy.Level)));
    }
    public static void Validate(RangedConditions c)
    {
        if(!double.IsFinite(c.SyndicateCharge)||c.SyndicateCharge is <0 or >1||!double.IsFinite(c.AffinityMultiplier)||c.AffinityMultiplier<0||c.EquippedWeaponCount is <1 or >4||c.JusticeStatus is not ("Unspecified" or "Blast" or "StunOnly")||new[]{c.PlayerBaseHealth,c.PlayerBaseArmor,c.PlayerBaseEnergy,c.PlayerBaseShields}.Any(v=>v.HasValue&&(!double.IsFinite(v.Value)||v.Value<0)))throw new ArgumentException("신디케이트 숙련도·기본 능력치 설정 오류");
    }
}

/// <summary>Affinity is credited after weapon split. A capped holstered gauge needs new affinity to trigger.</summary>
public sealed class RangedSyndicateMeter(double points,double initialFraction=0)
{
    public double Threshold {get;}=1000/Math.Clamp(points,double.Epsilon,1);
    public double Affinity {get;private set;}=1000/Math.Clamp(points,double.Epsilon,1)*initialFraction;
    public double ReadyAt {get;private set;}=0;
    public bool Gain(double amount,double time,bool holstered)
    {
        if(!double.IsFinite(amount)||amount<0||!double.IsFinite(time)||time<0)throw new ArgumentException("숙련도 사건 범위 오류");
        if(amount==0||time<ReadyAt)return false;
        Affinity=Math.Min(Threshold,Affinity+amount);
        if(holstered||Affinity<Threshold)return false;
        Affinity=0;ReadyAt=time+30;return true;
    }
}
