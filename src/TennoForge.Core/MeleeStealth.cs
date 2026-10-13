namespace TennoForge.Core;

/// <summary>Stealth rev.2786867, DoT scaling rev.2804374. Stealth and crit are ADDITIVE.</summary>
public static class MeleeStealth
{
    public const string Source="https://wiki.warframe.com/w/Stealth?oldid=2786867";
    public const string DotSource="https://wiki.warframe.com/w/Damage_over_Time?oldid=2804374";
    public static bool Invisible(MeleeSetup setup)=>(setup.Conditions?.GetValueOrDefault("Invisible")??0)>0;
    public static bool Blinded(MeleeSetup setup)=>setup.Overguard==0&&(setup.Conditions?.GetValueOrDefault("Blinded")??0)>0;
    public static bool Exposed(MeleeSetup setup)=>setup.Overguard==0&&(setup.Conditions?.GetValueOrDefault("FinisherExposed")??0)>0;
    public static double Bonus(Build build,bool ignoreCooldown=false)
    {
        var setup=build.Melee!;var values=setup.Conditions??[];
        var finisher=setup.Finisher is "Front" or "Back" or "Stealth" or "Universal";
        if(!finisher&&!Blinded(setup)&&!Exposed(setup)&&!(Invisible(setup)&&(ignoreCooldown||values.GetValueOrDefault("StealthCooldown")<=0))&&values.GetValueOrDefault("Stealth")<=0&&values.GetValueOrDefault("Unalerted")<=0)return 0;
        var rank=build.Capacity!.WeaponRank;
        if(MeleeRuleCatalog.Load().Weapon(build.WeaponId).Name=="Ether Daggers")
        {
            if(rank<30)throw new CombatRuleUnavailableException("에테르 대거의 은신 보너스 +900%는 확인되지만 30랭크 미만의 보간 규칙은 위키 미기재입니다.");
            return 9;
        }
        // Finisher rev.2812039 uses rank40 normalization for contextual
        // finishers; ordinary stealth uses Stealth's explicit rank30 cap.
        var maxRank=finisher?MeleeRuleCatalog.Load().Weapon(build.WeaponId).Raw.Number("MaxRank",30):30;
        return 1+6*Math.Min(maxRank,rank)/maxRank;
    }
    public static double Mean(double chance,double multiplier,double bonus)=>1+bonus+chance*(multiplier-1);
    public static double CriticalOnlyMean(double chance,double multiplier,double bonus)=>Math.Min(1,chance)*(1+bonus)+chance*(multiplier-1);
    public static double Mean(Calculation c)=>Mean(c.CriticalChance,c.CriticalMultiplier,c.Melee?.StealthBonus??0);
    public static double Expected(Calculation c)=>Mean(c)+ArcaneEngine.YellowChance(c.CriticalChance)*c.DuplicateChance*(Mean(c)-(NeedsContactCooldown(c)&&!Exposed(c.Melee!.Setup)?c.Melee!.StealthBonus:0));
    public static double AtTier(Calculation c,double tier)=>1+(c.Melee?.StealthBonus??0)+tier*(c.CriticalMultiplier-1);
    public static bool NeedsContactCooldown(Calculation c)=>c.Melee is {} m&&m.RevealsOnHit&&Invisible(m.Setup)&&!Blinded(m.Setup);
}
