namespace TennoForge.Core;

public static class BuildMigration
{
    public static Build ToMelee(Catalog catalog,Build build)
    {
        build=Upgrade(catalog,build);
        if(build.SchemaVersion is <1 or >5)throw new ArgumentException("지원하지 않는 저장 형식입니다.");
        if(build.Melee?.RulesVersion is "melee-conditional-0.6.0" or "melee-conditional-0.6.1" or "melee-conditional-0.6.2")build=build with {Melee=build.Melee with {RulesVersion=MeleeEngine.RulesVersion}};
        return build with {SchemaVersion=5,Melee=build.Melee??new(),Capacity=build.Capacity??CapacityCatalog.Load().Default(build.WeaponId,build.Stance?.Id)};
    }
    public const string LegacyVersion="wfcd-4a73b2fb016d7a79ec0694aec42a66778d762288";
    public static Build Upgrade(Catalog catalog,Build build)
    {
        if(build.DataVersion==catalog.Version)return build;
        if(build.DataVersion!=LegacyVersion||build.SchemaVersion is <1 or >3||build.Arcanes is not null)throw new ArgumentException("자동 변환할 수 없는 데이터/저장 버전입니다.");
        string[] ids=["/Lotus/Weapons/Tenno/Melee/Swords/PrimeKatana/PrimeNikana","/Lotus/Weapons/Tenno/Melee/Tonfa/TonfaContestWinnerPrime/TonfaContestWinnerPrimeWeapon","/Lotus/Weapons/Tenno/Melee/Swords/PrimeGram/PrimeGram"];
        if(!ids.Contains(build.WeaponId)||build.Slots is null||build.Slots.Length!=8||build.Slots.Any(s=>s is not null&&!catalog.Mods.Take(17).Any(m=>m.Id==s.ModId)))throw new ArgumentException("이전 버전에 없는 무기/모드가 포함되어 있습니다.");
        // Preserve all user choices; caller explicitly reports recalculation against the new Wiki snapshot.
        return build with{DataVersion=catalog.Version};
    }
}
