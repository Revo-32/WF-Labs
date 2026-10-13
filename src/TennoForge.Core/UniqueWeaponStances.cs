namespace TennoForge.Core;

/// <summary>Pinned source IDs, never localized labels. Only non-inherited
/// weapon rows replace an equipped stance; shared class rows stay intact.</summary>
public static class UniqueWeaponStances
{
    public static readonly IReadOnlyDictionary<string,string> Links=new Dictionary<string,string> {
        ["/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/DaxDuviriMaceShieldWeapon"]="Argo & Vel",
        ["/Lotus/Weapons/Infested/Melee/InfWarfan/InfWarfanWeapon"]="Arum Spinosa",
        ["/Lotus/Weapons/Tenno/Melee/Swords/DaxDuviriTwoHandedKatana/DaxDuviriTwoHandedKatanaWeapon"]="Azothane",
        ["/Lotus/Weapons/Infested/Melee/InfEmbolistScythe/InfestedScythe"]="Caustacyst",
        ["/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/PacifistShieldAndSword/PacifistShieldSword"]="Cobra & Crane",
        ["/Lotus/Weapons/Tenno/Melee/Polearms/DaxDuviriPolearm/DaxDuviriPolearmWeapon"]="Edun",
        ["/Lotus/Weapons/Grineer/Melee/GrnKusarigama/GrnKusarigamaWeapon"]="Jat Kusar",
        ["/Lotus/Weapons/Infested/Melee/InfTransformClaw/InfTransformClawsWeapon"]="Keratinos",
        ["/Lotus/Weapons/Archon/Melee/Trident/ArchonTridentPlayerWep"]="Korumm",
        ["/Lotus/Weapons/Archon/Melee/DualDaggers/ArchonDualDaggersPlayerWep"]="Nepheri",
        ["/Lotus/Weapons/Tenno/Melee/Warfan/TnBrokenFrameWarfan/TnBrokenFrameWarfanWeapon"]="Quassus",
        ["/Lotus/Weapons/Tenno/Melee/Hammer/DaxDuviriHammer/DaxDuviriHammerWeapon"]="Sampotes",
        ["/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/SundialSwordBoard/SundialBoardSword"]="Sigma & Octantis",
        ["/Lotus/Weapons/Tenno/Melee/Swords/DaxDuviriKatana/DaxDuviriKatanaWeapon"]="Syam",
        ["/Lotus/Weapons/Tenno/Melee/Swords/TnTwoHandedKatana/TnTwoHandedKatana"]="Tatsu",
        ["/Lotus/Weapons/Syndicates/ArbitersOfHexis/Melee/AHBoltace"]="Telos Boltace",
        ["/Lotus/Weapons/Corpus/Melee/ShieldAndSword/CrpHammerShield/CrpHammerShield"]="Tenet Agendus",
        ["/Lotus/Weapons/Tenno/Melee/Swords/CrpBigSlash/CrpBigSlash"]="Tenet Exec",
        ["/Lotus/Weapons/Corpus/Melee/CrpBriefcaseScythe/CrpBriefcaseScythe"]="Tenet Grigori",
        ["/Lotus/Weapons/Archon/Melee/Whip/ArchonWhipPlayerWep"]="Verdilac",
        ["/Lotus/Weapons/Tenno/Melee/Swords/NWIIIOrokinSword/NWOrokinSword"]="Vitrica",
        ["/Lotus/Weapons/Tenno/Melee/SunDialAxe/SundialAxeWeapon"]="Zenistar",
        ["/Lotus/Weapons/Infested/InfestedLich/Melee/CodaCaustacyst/CodaCaustacyst"]="Caustacyst",
        ["/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/PrimeCobraAndCrane/PrimeCobraAndCraneWeapon"]="Cobra & Crane",
        ["/Lotus/Weapons/Tenno/Melee/Warfan/TnBrokenFrameWarfan/TnBrokenFramePrimeWarfanWeapon"]="Quassus",
        ["/Lotus/Weapons/Tenno/Melee/Swords/PrimeTatsu/PrimeTatsuWeapon"]="Tatsu",
    };
    public static string? SourceId(string id,string key,Stance[] source)=>Links.TryGetValue(id,out var stance)&&source.Single(s=>s.Id==stance).Combos.Any(c=>c.Key==key&&c.InheritedFrom is null)?stance:null;
    public static Stance Resolve(string id,Stance stance,Stance[] source)
    {
        if(!Links.TryGetValue(id,out var specialId))return stance;
        var rows=source.Single(s=>s.Id==specialId).Combos.Where(c=>c.InheritedFrom is null).ToDictionary(c=>c.Key);
        return stance with {Combos=stance.Combos.Select(c=>rows.GetValueOrDefault(c.Key,c)).Concat(rows.Values.Where(c=>!stance.Combos.Any(o=>o.Key==c.Key))).ToArray()};
    }
}
