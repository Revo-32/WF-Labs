namespace TennoForge.Core;

/// <summary>Weapon aerial overrides must also replace the aerial row in an equipped stance.</summary>
public static class ShieldThrowMechanic
{
    public const string Sigma="/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/SundialSwordBoard/SundialBoardSword";
    public const string Cobra="/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/PacifistShieldAndSword/PacifistShieldSword";
    public const string CobraPrime="/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/PrimeCobraAndCrane/PrimeCobraAndCraneWeapon";
    public const string SigmaSource="https://wiki.warframe.com/w/Sigma_%26_Octantis?oldid=2808876";
    public const string CobraSource="https://wiki.warframe.com/w/Cobra_%26_Crane_Prime?oldid=2779541";
    public static bool IsCobra(string id)=>id is Cobra or CobraPrime;
    public static bool Applies(string id)=>id==Sigma||IsCobra(id);
    public static AttackRule? Policy(MeleeWeaponRule profile,string attack)
    {
        if(profile.Id!=Sigma||attack is not ("Shield Throw" or "Shield Throw Explosion"))return null;
        // The article explicitly excludes combo-dependent weapon statistics.
        // This does not disable ordinary critical/status mods or blocking charges.
        return profile.Attacks.Single(a=>a.Key==attack) with {ComboStats=false,Combo=false,Tennokai=false,FollowThrough=false};
    }
    public static Stance Resolve(string id,Stance stance,Stance[] originals)
    {
        var special=originals.Single(s=>s.Id==(id==Sigma?"Sigma & Octantis":"Cobra & Crane")).Combos.Single(c=>c.Key=="Aerial");
        if(id==Sigma)special=special with {
            Name="공중 · 방패 투척",AttackOverride="Shield Throw",ActionOverride="Aerial",
            // The infobox provides 116 direct damage independently of the 174
            // blade. Do not rescale this to the blade via the old 100% stance row.
            Hits=[new(1,1,1,1,1,1,1,[],"")],
            ResolutionNote="시그마 & 옥탄티스 고유 공중 방패:116 기본 피해·28% 치확·2.2배 치피·16% 상확·강제 기절. 별도 투척 수치를 사용합니다. 마무리 개방 시간과 다른 상태에 의한 덮어쓰기 시각은 원문 미기재입니다. "+SigmaSource
        };
        else special=special with {Name="공중 · 방패 투척",ResolutionNote="코브라 & 크레인 공통 고유 공중 방패:100% 타격·넉다운. 위키 무기 데이터에 별도 투사체 치명/상태 확률이 없습니다. "+CobraSource};
        return stance with {Combos=stance.Combos.Select(c=>c.Key=="Aerial"?special:c).ToArray()};
    }
}
