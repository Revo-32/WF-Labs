using System.Text.Json;

namespace TennoForge.Core;

/// <summary>Hammer-specific flight rules; a Wolf Sledge is not a Glaive mod class.</summary>
public static class WolfSledgeFlight
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Hammer/ThrowingHammer";
    public const string Source="https://wiki.warframe.com/w/Wolf_Sledge?oldid=2789902";
    public static void Validate(GlaiveFlight f,Build build)
    {
        if(f.Airborne)throw new ArgumentException("울프 슬레지는 지상에서만 투척할 수 있습니다.");
        if(f.FirstLegDistance is {} distance&&distance>40*(1+MeleeEngine.Calculate(Catalog.LoadMelee(),build).Melee!.Effects.GetValueOrDefault("ProjectileSpeed")))throw new ArgumentException("울프 슬레지의 첫 비행은 기본40m까지입니다.");
        var outward=(f.Contacts??[]).Where(c=>c.Kind!=GlaiveContactKind.Return).OrderBy(c=>c.Time).ToArray();
        if(outward.Length>2)throw new ArgumentException("울프 슬레지는 전진 중 최대 두 대상에 접촉한 후 귀환합니다.");
        if(outward.Any(c=>c.Kind==GlaiveContactKind.PunchThrough))throw new ArgumentException("울프 슬레지는 전진 몸체 관통 없이 다음 한 대상으로 반사합니다.");
        if(outward.Take(Math.Max(0,outward.Length-1)).Any(c=>c.Surface is not null))throw new ArgumentException("울프 슬레지는 지형 접촉 즉시 귀환합니다.");
        if(outward.Length==2&&outward.All(c=>c.Surface is null))
        {
            CombatEnemy? Position(int n)=>n==1?null:build.Encounter!.NearbyEnemies![n-2];
            if(CombatScene.Distance(Position(outward[0].Target),Position(outward[1].Target))>20+1e-9)throw new ArgumentException("울프 슬레지의 두 번째 유도 대상은 첫 대상에서20m 안에 있어야 합니다.");
        }
    }
    internal static Build WithResources(Build current,Build input)
    {
        var c=new Dictionary<string,double>(current.Melee!.Conditions??[]);
        c["MagusAggressCharges"]=input.Melee!.Conditions?.GetValueOrDefault("MagusAggressCharges")??0;
        return current with {Melee=current.Melee with {Conditions=c}};
    }
    public static Calculation Bounce(Catalog catalog,Build build,double distance=0)
    {
        var attack="Throw Bounce Explosion";var policy=MeleeEngine.AttackPolicy(WeaponId,attack) with {Co=0,Heavy=false,Combo=false,HitCombo=false,FollowThrough=false,Tennokai=false};
        var b=build with {Stance=null,Melee=build.Melee! with {Attack=attack,Distance=distance,TargetIndex=1,Finisher=null,Tennokai=false}};
        return MeleeEngine.CalculateAttack(catalog,b,useInitialComboFloor:false,component:new(GlaiveFlightState.Raw(build,attack),policy));
    }
    public static Calculation HeavyReturn(Catalog catalog,Build build)
    {
        // The projectile stays the 777-damage hammer; the 1554-damage radial
        // attack is not reused as a returning body collision.
        var attack="Throw Recall Explosion";
        var policy=MeleeEngine.AttackPolicy(WeaponId,attack) with {Co=1,Heavy=true,Combo=true,HitCombo=false,FollowThrough=false,RadiusMods=false};
        var b=build with {Stance=null,Melee=build.Melee! with {Attack=attack,Distance=0,TargetIndex=1,Finisher=null}};
        return MeleeEngine.CalculateAttack(catalog,b,useInitialComboFloor:false,component:new(GlaiveFlightState.Raw(build,"Throw"),policy));
    }
    internal static string ReturnSignature(Catalog catalog,Build b)
    {
        var m=b.Melee!;
        var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=HeavyReturn(catalog,neutral);return JsonSerializer.Serialize(new{c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance},Catalog.Json);
    }
}
