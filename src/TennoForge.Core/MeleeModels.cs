using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record MeleeSetup(string Attack="Normal Attack",bool Tennokai=false,DamageType? BonusElement=null,double Bonus=0,
    Dictionary<string,double>? Conditions=null,string Faction="Neutral",Dictionary<DamageType,int>? Statuses=null,
    double Distance=0,int TargetIndex=1,double Shields=0,double Overguard=0,bool Incarnon=false,string[]? Evolutions=null,
    ModSlot? Exilus=null,ArcaneSlot? Exodia=null,double AbilityStrength=1,double? ComboDurationOverride=null,string ExilusPolarity="None",string RulesVersion=MeleeEngine.RulesVersion,double? HeatArmorStrip=null,string? Finisher=null,double? IncarnonSecondsRemaining=null,int AbilityRank=3,double AbilityRange=1,double AbilityDuration=1,ModSlot? AbilityAugment=null,DamageType? ChromaticElement=null,TargetDebuff[]? Debuffs=null,ContagionDamageBuffs? ContagionBuffs=null,MeleeExternalEquipment? ExternalEquipment=null,HologramCopy? Hologram=null,WeaponBuffs? Buffs=null);
public record MeleeModRule(string Id,string Name,int MaxRank,Dictionary<string,double>? Effects,DamageType? Element,double ElementBonus,
    Dictionary<string,double>? Conditional,string? Condition,int Cap,string? SetEffect,string? Utility,string[] Unsupported,string[] Incompatible,WikiSource? Source,string[] CompatibleWeapons,Dictionary<string,double>[]? RankEffects=null,Dictionary<string,double>[]? RankConditional=null);
public record MeleePassive(string Key,string Label,int Maximum,Dictionary<string,double> Effects,string Attack);
public record MeleeWeaponRule(string Id,string Name,WikiSource? Source,string Notes,JsonElement Raw,MeleePassive[] Passives,string Code,AttackRule[] Attacks,string[]? PendingMechanics=null);
public record AttackRule(string Key,string DamageSource="Melee",double Co=1,bool CoMultiplicative=false,bool Heavy=false,
    bool HeavyAnimationOnly=false,bool HeavyDamageAnimationOnly=false,bool Tennokai=true,bool Incarnon=false,bool EvolutionDamage=true,
    bool EvolutionStats=true,bool StatusMods=true,bool CriticalMods=true,bool Elements=true,bool Faction=true,bool Ips=true,bool Combo=false,string Code="",string[]? Notes=null,bool CoCombo=true,string RangeProfile="",double PhysicalMultiplier=1,bool Stealth=true,
    bool FollowThrough=true,bool HitCombo=true,bool HealingReturn=true,bool RadiusMods=false,string? CoSourceConflict=null,bool ComboStats=true);
public record MeleeArcaneRule(string Id,string Code,WikiSource? Source);
public record IncarnonPerk(string Id,int Stage,string Name,Dictionary<string,double> Effects,string? Condition,int Cap,Dictionary<string,double> Conditional);
public record IncarnonDefinition(string WeaponId,string Code,Dictionary<string,double> Form,IncarnonPerk[] Perks,WikiSource Source);
public record StanceCoRule(string StanceId,string Combo,double Factor,WikiSource Source);
public record EnemyDamageProfile(string Id,string Name,string Faction,Dictionary<DamageType,double> Modifiers,WikiSource Source)
{
    public double Multiplier(DamageType type)=>Modifiers.GetValueOrDefault(type,1);
}
public record MeleeRuleCatalog(string Version,WikiSource[] Sources,MeleeWeaponRule[] Weapons,MeleeModRule[] Mods,MeleeArcaneRule[] Arcanes,IncarnonDefinition[] Incarnons,StanceCoRule[] StanceCo,Mod[] AdditionalMods,EnemyDamageProfile[] EnemyProfiles)
{
    private static readonly Lazy<MeleeRuleCatalog> Instance=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.melee-rules.json")??throw new InvalidOperationException("근접 규칙 자료가 없습니다.");
        return JsonSerializer.Deserialize<MeleeRuleCatalog>(stream,Catalog.Json)??throw new InvalidOperationException("근접 규칙 형식 오류");
    });
    public static MeleeRuleCatalog Load()=>Instance.Value;
    public MeleeWeaponRule Weapon(string id)=>ZawCatalog.IsZaw(id)?ZawCatalog.Rule(id):Weapons.SingleOrDefault(w=>w.Id==id)??throw new ArgumentException("근접 무기 규칙 식별자 오류");
}
public record StatusDamage(DamageType Type,double Chance,double DamagePerTick,double Duration,int Ticks,bool IgnoresArmor);
public record MeleeDetails(string Attack,double ModdedBaseDamage,double FactionMultiplier,double StatusDamageMultiplier,
    Dictionary<DamageType,double> ElementBonuses,Dictionary<DamageType,double> TargetDamage,StatusDamage[] StatusDamage,
    double Range,double FollowThrough,double ComboDuration,double InitialCombo,double ComboChance,double HeavyEfficiency,
    double Windup,double TennokaiChance,double TennokaiWindow,double Healing,double ExtraDamage,string[] ForcedProcs,
    Dictionary<string,double> Effects,MeleeSetup Setup,double Projectiles=1,DamageBrackets? Brackets=null,
    double EffectiveCombo=1,EffectContribution[]? Contributions=null,double StealthBonus=0,bool RevealsOnHit=false,double StealthReadyBonus=0,bool HitConnects=true,double ForcedProcDamageMultiplier=1,AttackRule? Policy=null,double? AreaRadius=null,double IncomingDamageMultiplier=1);
public record EffectContribution(string SourceId,string Name,int Rank,string Effect,string Bracket,double Amount,string SourceUrl);

internal static class WikiValue
{
    internal static double Number(this JsonElement e,string key,double fallback=0)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetDouble(out var n)?n:fallback;
    internal static string Text(this JsonElement e,string key,string fallback="")=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??fallback:fallback;
    internal static bool Flag(this JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.True;
    internal static string[] Strings(this JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Array?p.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).ToArray():[];
}
