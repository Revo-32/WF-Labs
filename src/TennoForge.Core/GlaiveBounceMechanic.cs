using System.Text.Json;

namespace TennoForge.Core;

/// <summary>A volatile collision explosion is a child of one throw contact.</summary>
public static class GlaiveBounceMechanic
{
    public const string QuickReturn="/Lotus/Upgrades/Mods/Melee/WeaponGlaiveExplodingReflectionDecreaseMod";
    public const string Rebound="/Lotus/Upgrades/Mods/Melee/WeaponGlaiveExplodingReflectionIncreaseMod";
    public const string Source="https://wiki.warframe.com/w/Volatile_Quick_Return?oldid=2719572";
    public const string ReboundSource="https://wiki.warframe.com/w/Volatile_Rebound?oldid=2677047";
    public static bool IsThrow(string attack)=>attack is "Throw" or "Charged Throw";
    public static string Explosion(string attack)=>IsThrow(attack)?attack+" Bounce Explosion":throw new ArgumentException("글레이브 직접 투척 명중이 필요합니다.");
    public static double Chance(Build build)
    {
        var slot=build.Slots.SingleOrDefault(s=>s?.ModId is QuickReturn or Rebound);
        return slot is null?0:(slot.Rank+1)/4d;
    }
    public static Calculation Calculate(Catalog catalog,Build build,string throwAttack,double radiusDistance=0,bool useInitialComboFloor=true)
    {
        if(MeleeRuleCatalog.Load().Weapon(build.WeaponId).Raw.Text("Class")!="Glaive"||Chance(build)<=0)throw new ArgumentException("볼래틸 모드를 장착한 글레이브가 필요합니다.");
        var name=Explosion(throwAttack);var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);
        var raw=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
        var data=JsonSerializer.Deserialize<Dictionary<string,object>>(raw.GetRawText(),Catalog.Json)!;
        // Only the quick-return article documents the Glaive Prime impact
        // exception. Do not transfer any manual-detonation forced status.
        if(!build.Slots.Any(s=>s?.ModId==QuickReturn))data["ForcedProcs"]=Array.Empty<string>();
        raw=JsonSerializer.SerializeToElement(data,Catalog.Json);
        var policy=MeleeEngine.AttackPolicy(build.WeaponId,name) with {Heavy=false,Combo=false,Co=0,HitCombo=false,FollowThrough=false,Tennokai=false};
        return MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Melee=build.Melee! with {Attack=name,Distance=radiusDistance,TargetIndex=1,Tennokai=false,Finisher=null}},useInitialComboFloor:useInitialComboFloor,component:new(raw,policy));
    }
}
