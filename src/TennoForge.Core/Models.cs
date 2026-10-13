using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TennoForge.Core;

public enum DamageType { Impact, Puncture, Slash, Heat, Cold, Electricity, Toxin, Blast, Radiation, Gas, Magnetic, Viral, Corrosive, Void, Tau, True, Finisher }
public static class DamageTypeRules
{
    // Damage rev.2816587 distinguishes DT_FINISHER from DT_HEALTH_DRAIN.
    public static bool HasStatus(DamageType type)=>type is not (DamageType.True or DamageType.Finisher);
    public static bool BypassesArmor(DamageType type)=>type is DamageType.True or DamageType.Finisher;
    public static bool BypassesShields(DamageType type)=>type is DamageType.Toxin or DamageType.True;
}
public enum Stat { Damage, CriticalChance, CriticalDamage, AttackSpeed, StatusChance, BloodRush, WeepingWounds, ConditionOverload, ImpactDamage, PunctureDamage, SlashDamage }
public record Weapon(string Id, string Name, string EnglishName, Dictionary<DamageType, double> Damage, double CriticalChance, double CriticalMultiplier, double StatusChance, double AttackSpeed, string WeaponType="", int MaxRank=30, string Source="", string[]? InnateNotes=null,int MaxCombo=12);
public record Mod(string Id, string Name, string EnglishName, int MaxRank, int BaseDrain, string Family, Dictionary<Stat, double> Effects, DamageType? Element = null, double ElementBonus = 0, string Polarity = "None", string[][]? RankText = null, string[]? CalculationIssues=null, string Source="");
public record Catalog(string Version, string Source, string RetrievedAt, Weapon[] Weapons, Mod[] Mods)
{
    public static Catalog LoadMelee()
    {
        var baseCatalog=Load();return baseCatalog with {
            Weapons=baseCatalog.Weapons.Select(w=>MeleeRuleCatalog.Load().Weapon(w.Id).Code=="DexNikana"?w with {MaxCombo=11}:w).Concat(ExaltedRules.Weapons()).Concat(ZawCatalog.All()).ToArray(),
            Mods=baseCatalog.Mods.Concat(MeleeRuleCatalog.Load().AdditionalMods).Select(m=>m with {MaxRank=MeleeRuleCatalog.Load().Mods.Single(r=>r.Id==m.Id).MaxRank}).ToArray()};
    }
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static Catalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.catalog.json") ?? throw new InvalidOperationException("데이터 파일을 찾을 수 없습니다.");
        return JsonSerializer.Deserialize<Catalog>(stream, Json) ?? throw new InvalidOperationException("데이터 파일이 비어 있습니다.");
    }
}
public record ModSlot(string ModId, int Rank);
public record Target(double Health = 10000, double Armor = 2700);
// BaseInterval is the time between hits at attack speed 1.0, not a measured stance animation.
public record Scenario(int Combo = 1, int ExistingStatusTypes = 0, double MotionMultiplier = 1, double BaseInterval = 1, Target? Enemy = null);
public record StanceSelection(string Id, string ComboKey, long Revision);
public record Build(string WeaponId, ModSlot?[] Slots, Scenario Scenario, string DataVersion, int SchemaVersion = 1, RivenRoll? Riven = null, StanceSelection? Stance = null, CapacitySetup? Capacity = null, ArcaneSetup? Arcanes=null,MeleeSetup? Melee=null,EncounterSetup? Encounter=null,EnemySelection? EnemySelection=null);
public record TraceLine(string Name, string Formula, double Value, string Evidence);
public record Calculation(Dictionary<DamageType, double> Damage, double CriticalChance, double CriticalMultiplier, double StatusChance, double AttackSpeed, double HitInterval, double ExpectedHit, double RawDps, double TargetDps, double ArmorReduction, TraceLine[] Trace, string[] Limitations, double DuplicateChance=0,MeleeDetails? Melee=null);
public record TtkResult(double? P10, double? Median, double? P90, double KillFraction, int Trials, uint Seed, double Horizon);

public static class Labels
{
    public static string Damage(DamageType t) => t switch { DamageType.Impact => "충격", DamageType.Puncture => "관통", DamageType.Slash => "베기", DamageType.Heat => "화염", DamageType.Cold => "냉기", DamageType.Electricity => "전기", DamageType.Toxin => "독성", DamageType.Blast => "폭발", DamageType.Radiation => "방사능", DamageType.Gas => "가스", DamageType.Magnetic => "자성", DamageType.Viral => "바이러스", DamageType.Corrosive => "부식", DamageType.Void=>"보이드",DamageType.Tau=>"타우",DamageType.True=>"고정 피해",DamageType.Finisher=>"마무리",_ => t.ToString() };
}
