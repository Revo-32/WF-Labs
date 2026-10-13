namespace TennoForge.Core;

public static class MeleeAttackSpeed
{
    public const string Source="https://wiki.warframe.com/w/Attack_Speed?oldid=2728743";
    // Wiki: unmodded melee animation speeds are X/60; the displayed/module
    // values can be rounded to two or three decimal places. Normalize only
    // this base stat, never a modded speed, projectile frequency or duration.
    public static double FromWiki(double displayed)
    {
        if(!double.IsFinite(displayed)||displayed<=0)throw new ArgumentException("유효한 기본 근접 공속이 필요합니다.");
        var numerator=Math.Round(displayed*60,MidpointRounding.AwayFromZero);
        if(numerator<1)throw new ArgumentException("기본 공속이 위키의 X/60 단위보다 작습니다.");
        return numerator/60;
    }
}
