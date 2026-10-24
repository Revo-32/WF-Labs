namespace TennoForge.Core;
/// <summary>Thunderbolt and Concealed Explosives: independent fixed-damage contact explosions.</summary>
public static class RangedFlatExplosions
{
    public static bool Equipped(RangedBuild b)=>b.Slots.OfType<ModSlot>().Any(s=>{var e=RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects;return e.ContainsKey("ThunderboltChance")||e.ContainsKey("ConcealedExplosionChance");});
    public static double Chance(ReferenceWeapon w,RangedResult r,bool surface)
    {
        if(w.Id is "wiki:secondary/Aegrit" or "wiki:secondary/Cantare" or "wiki:primary/Zhuge Prime")return 0;
        return r.Modifiers!.GetValueOrDefault("ThunderboltChance")+(surface?r.Modifiers!.GetValueOrDefault("ConcealedExplosionChance"):0);
    }
    public static bool IsConcealed(RangedResult r)=>r.Modifiers!.GetValueOrDefault("ConcealedExplosionChance")>0;
    public static bool SurfaceStops(ReferenceWeapon w,int attack,RangedResult r,RangedConditions c)
    {
        var pt=RangedPenetration.Geometry(w,attack,r);if(pt<=0)return true;
        var wall=c.ImpactSurfaceThickness??throw new CombatRuleUnavailableException("지형을 관통한 뒤에야 컨실드 폭발이 가능합니다. 명중한 표면의 유효 두께를 지정하세요.");
        if(!double.IsFinite(wall)||wall<=0)throw new ArgumentException("표면 두께는 0보다 커야 합니다.");return wall>pt;
    }
    public static double Radius(RangedResult r)=>10*Math.Max(0,1+r.Modifiers!.GetValueOrDefault("BlastRadius"));
    public static void Validate(RangedConditions c)
    {
        if(c.ThunderboltDirectRule is not ("Unspecified" or "Replace" or "Add"))throw new ArgumentException("썬더볼트 직접 피해 기준 오류");
        if(c.ImpactSurfaceThickness is {} t&&(!double.IsFinite(t)||t<=0))throw new ArgumentException("표면 두께 오류");
        if((c.ExplosionObstructedTargets??[]).Any(i=>i<0||i>=c.GroupCount))throw new ArgumentException("폭발 시야 차단 대상 번호 오류");
    }
}
