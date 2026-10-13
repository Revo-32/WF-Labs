namespace TennoForge.Core;

/// <summary>Judged Severance: actual blind event, separate from blade contact.
/// No finisher exposure and no invented offset within the slide animation.</summary>
public static class ExaltedBlind
{
    public const string Source="https://wiki.warframe.com/w/Exalted_Blade?oldid=2817346";
    public const string SightSource="https://wiki.warframe.com/w/Radial_Blind?oldid=2815181";
    public static double Radius(MeleeSetup setup)=>5*setup.AbilityRange;
    public static double Duration(MeleeSetup setup)=>6*setup.AbilityDuration;
    public static bool Affects(double distance,double overguard,bool sight,bool recentlyAware,MeleeSetup setup)
        =>overguard<=0&&distance<=Radius(setup)&&(sight||recentlyAware);
}
