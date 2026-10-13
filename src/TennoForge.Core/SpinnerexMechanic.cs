namespace TennoForge.Core;

public record SecondaryBurst(string Name,double ExpectedActivations,double ExpectedCenterDamage,double ExpectedToxinTick,double Radius,string Note,double? ExpectedCenterDamageUpper=null);
public record SpinnerexExplosion(Dictionary<DamageType,double> Damage,StatusDamage Toxin,double Chance,double Radius,string Source)
{
    public double TotalDamage=>Damage.Values.Sum();
}

/// <summary>Spinnerex rev.2804787: center damage only; the source does not give the falloff curve.</summary>
public static class SpinnerexMechanic
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Whips/SpiderWhip/SpiderWhipWeapon";
    public const string Source="https://wiki.warframe.com/w/Spinnerex?oldid=2804787";
    public static double Chance(int toxinStacks,double lastDirectHit,double deathTime)
    {
        if(toxinStacks<0||!double.IsFinite(deathTime)||deathTime<0||double.IsNaN(lastDirectHit))throw new ArgumentException("스피네렉스 표식·처치 시간·독성 중첩 오류");
        return lastDirectHit<=deathTime&&deathTime-lastDirectHit<5?Math.Min(1,toxinStacks*.1):0;
    }
    public static SpinnerexExplosion Calculate(Catalog catalog,Build build,int toxinStacks)
    {
        if(build.WeaponId!=WeaponId||build.Melee is null)throw new ArgumentException("스피네렉스 빌드가 필요합니다.");
        if(toxinStacks<0)throw new ArgumentException("독성 중첩은 음수일 수 없습니다.");
        var c=Evaluate(catalog,build);
        return new(c.Damage,c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Toxin),Math.Min(1,toxinStacks*.1),10,Source);
    }
    internal static Calculation Evaluate(Catalog catalog,Build build)
    {
        if(build.WeaponId!=WeaponId||build.Melee is null)throw new ArgumentException("스피네렉스 빌드가 필요합니다.");
        // This component belongs to the victim's death, not its preceding
        // heavy/slam/stance hit. It inherits the build's applicable buffs only.
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},
            Melee=build.Melee with {Attack="Spinnerex Death Explosion",Tennokai=false,Distance=0,TargetIndex=1}};
        return MeleeEngine.CalculateAttack(catalog,input,component:MeleeAttackComponent.SpinnerexExplosion());
    }
}
