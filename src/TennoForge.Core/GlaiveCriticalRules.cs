namespace TennoForge.Core;

public static class GlaiveCriticalRules
{
    public const string Source="https://wiki.warframe.com/w/Glaive_(Weapon_Type)?oldid=2792460";
    public static bool Airborne(MeleeSetup setup)=>(setup.Conditions?.TryGetValue("AirborneThrow",out var captured)==true?captured:(setup.Conditions?.GetValueOrDefault("Airborne")??0))>0;
    public static double ModMultiplier(string weaponId,MeleeSetup setup,bool ordinaryHeavyBonus)
    {
        var p=MeleeRuleCatalog.Load().Weapon(weaponId);var glaive=p.Raw.Text("Class")=="Glaive";
        if(!glaive&&p.Name!="Wolf Sledge")return ordinaryHeavyBonus?2:1;
        if(glaive&&setup.Attack.Contains("Throw Recall Explosion"))return Airborne(setup)?1:2;
        if(setup.Attack.Contains("Throw Bounce Explosion"))return Airborne(setup)?1:2;
        return ordinaryHeavyBonus?2:1;
    }
}
