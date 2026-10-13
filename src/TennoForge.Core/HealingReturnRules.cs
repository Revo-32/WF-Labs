namespace TennoForge.Core;

public static class HealingReturnRules
{
    public const string Source="https://wiki.warframe.com/w/Healing_Return?oldid=2685510";
    private static readonly HashSet<string> GuaranteedFlinchWeapons=[
        "/Lotus/Weapons/Tenno/Melee/GreatSword/GreatSword",
        "/Lotus/Weapons/Cephalon/Melee/Hammer/CephHammerWeapon",
        "/Lotus/Weapons/Tenno/Melee/Nunchaku/TnoNunchaku/TnoNunchaku"];
    public static bool GuaranteedFlinch(Build build,string attack)
        =>attack is "Normal Attack" or "Slide Attack"&&(GuaranteedFlinchWeapons.Contains(build.WeaponId)||build.Stance?.Id=="Gnashing Payara");
    // The pinned article confirms flinch alone heals. It does not define whether
    // flinch and a typed proc overlap. Keep that source uncertainty separate from sampling.
    public static (int Minimum,int Maximum) StatusCount(int knownTypes,bool flinch)
        =>flinch?(Math.Max(1,knownTypes),knownTypes+1):(knownTypes,knownTypes);
}
