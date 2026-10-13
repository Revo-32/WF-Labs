namespace TennoForge.Core;

/// <summary>The same selected target state is used by CO, stance evaluation and the UI.</summary>
public static class TargetStatusRules
{
    public static int Maximum(DamageType type)=>type switch
    {
        DamageType.Impact or DamageType.Puncture=>5, DamageType.Void=>1,
        DamageType.Heat or DamageType.Slash or DamageType.Toxin or DamageType.Electricity=>10000,
        DamageType.True or DamageType.Finisher=>0, _=>10
    };
    public static int Count(Build build)
    {
        var setup=build.Melee??new();var statuses=setup.Statuses??[];
        var frozen=build.Arcanes?.Frozen==true&&setup.Overguard==0;
        return Math.Max(build.Scenario.ExistingStatusTypes,statuses.Count(p=>p.Value>0)
            +(frozen&&statuses.GetValueOrDefault(DamageType.Cold)==0?1:0)
            +(setup.Overguard==0&&(setup.Conditions?.GetValueOrDefault("Lifted")>0||setup.Conditions?.GetValueOrDefault("Knockdown")>0)?1:0)
            +(setup.Conditions?.GetValueOrDefault("Microwave")>0?1:0));
    }
    // This is an explicit one-stack preset, not a simulation of procs over time.
    public static DamageType[] WeaponPreset(Calculation calculation,StanceCalculation? stance=null)
    {
        var hits=stance?.Hits.Select(h=>h.Calculation!).ToArray()??[calculation];
        return hits.SelectMany(c=>(c.StatusChance>0?c.Damage.Where(p=>p.Value>0).Select(p=>p.Key):[])
            .Concat((c.Melee?.ForcedProcs??[]).Where(p=>Enum.TryParse<DamageType>(p,out _)).Select(Enum.Parse<DamageType>))
            .Concat((c.Melee?.StatusDamage??[]).Where(s=>s.Chance>0).Select(s=>s.Type)))
            .Where(DamageTypeRules.HasStatus).Distinct().Order().ToArray();
    }
}
