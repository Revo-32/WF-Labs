using System.Text.Json;

namespace TennoForge.Core;

public record MeleeArea(double Radius,double Falloff,bool Outside);

/// <summary>Source attack range and falloff share the expanded radial coordinate.</summary>
public static class MeleeAreaRules
{
    internal static MeleeArea Resolve(JsonElement attack,AttackRule policy,string weaponClass,MeleeSetup setup,IReadOnlyDictionary<string,double> effects,string weaponId)
    {
        double E(string key)=>effects.GetValueOrDefault(key);
        var original=attack.Number("Range");var radius=original;
        var aoe=attack.Text("ShotType")=="AoE";
        var slam=setup.Attack.Contains("Slam",StringComparison.Ordinal);
        if((MeleeExternalRules.SpeedExtendsRange(weaponId)||policy.RangeProfile=="Stropha")&&!aoe)radius*=Math.Max(0,1+E("ProjectileSpeed"));
        if(aoe&&slam&&policy.Code!="KorummTacticalSlam")radius=original*(1+E("SlamRadiusPercent"))+E("SlamRange");
        else if(aoe&&weaponClass=="Glaive"&&setup.Attack.Contains("Explosion",StringComparison.Ordinal))radius+=E("ExplosionRadius");
        if(policy.RadiusMods)radius+=E("Range")+((setup.Conditions?.GetValueOrDefault("Airborne")??0)>0?E("AerialRange"):0);
        radius=Math.Max(0,radius);var factor=1d;
        if(attack.TryGetProperty("Falloff",out var fall))
        {
            var expansion=aoe&&original>0?radius/original:attack.Text("ShotType") is "Projectile" or "Hit-Scan"?Math.Max(0,1+E("ProjectileSpeed")):1;
            var start=fall.Number("StartRange")*expansion;var end=fall.Number("EndRange")*expansion;
            if(end>start)factor=1-Math.Clamp((setup.Distance-start)/(end-start),0,1)*fall.Number("Reduction");
        }
        var outside=(aoe||attack.Text("ShotType")=="Projectile"||policy.Code.StartsWith("Zenistar",StringComparison.Ordinal))&&radius>0&&setup.Distance>radius;
        return new(radius,outside?0:factor,outside);
    }
}
