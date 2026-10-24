namespace TennoForge.Core;
/// <summary>Official 44.1.3 overrides layered over the pinned, pre-patch Wiki catalog.</summary>
public static class RangedBalance4413
{
    public static RangedIncarnon Upgrade(RangedIncarnon weapon)=>weapon with{Perks=weapon.Perks.Select(p=>
    {
        if(p.Id.EndsWith("#enough-for-everyone",StringComparison.Ordinal))return p with{Effects=new(p.Effects){["CrowdAmmoEfficiency"]=.75}};
        if(p.Id.EndsWith("#crimson-overture",StringComparison.Ordinal))return p with{Triggers=p.Triggers?.Select(t=>t with{Cap=t.Effects.ContainsKey("BaseDamage")?t.Cap:3,Effects=new(t.Effects){["AmmoEfficiency"]=.25},EffectLimits=new(t.EffectLimits??[]){["AmmoEfficiency"]=.75}}).ToArray()};
        if(p.Id.StartsWith("wiki:secondary/Vasto",StringComparison.Ordinal)&&p.Id.EndsWith("#reaper-s-plenty",StringComparison.Ordinal))return p with{Triggers=p.Triggers?.Select(t=>t with{Effects=t.Effects.ToDictionary(e=>e.Key=="AmmoEfficiency"?"InfiniteMagazine":e.Key,e=>e.Value)}).ToArray()};
        return p;
    }).ToArray()};
    public static void Apply(Dictionary<string,double> effects)
    {
        effects["AmmoEfficiency"]=Math.Min(.75,effects.GetValueOrDefault("AmmoEfficiency"));
    }
}
