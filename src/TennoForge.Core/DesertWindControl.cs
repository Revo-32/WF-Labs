namespace TennoForge.Core;

public static class DesertWindControl
{
    public const string Source="https://wiki.warframe.com/w/Serene_Storm?oldid=2816022";
    public const string StatusSource="https://wiki.warframe.com/w/Condition_Overload_(Mechanic)?oldid=2815998";
    // This describes applying the disarm effect, not simulated enemy AI or
    // removing a weapon from an enemy archetype which never carried one.
    public static bool Applies(string weaponId,IEnumerable<string> forced,double overguard,bool impactProc=false)
        =>ExaltedRules.Find(weaponId)?.Family==ExaltedFamily.Wind&&overguard<=0&&
            (impactProc||forced.Any(p=>p is "Knockdown" or "Lifted" or "Ragdoll" or "Stagger" or "Disarmed"));
}
