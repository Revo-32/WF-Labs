using System.Text.Json;

namespace TennoForge.Core;

/// <summary>An independently resolved attack component, never a repeatable weapon action.</summary>
internal record MeleeAttackComponent(JsonElement Raw,AttackRule Policy,bool NoRandomStatus=false,double DamageMultiplier=1,
    bool NoSupplementalElements=false,bool NoWeaponStatusDuration=false,bool NoDuplicate=false,bool WeaponDoughty=false,bool NoEnergyChannel=false,
    bool UnknownStatusUpperBound=false,bool FlatCriticalOnly=false,bool FixedStatusChance=false,double FlatCriticalBonus=0,
    double MeleeDamageBonus=0,double WeaponDamageBonus=0,double SeparateDamageMultiplier=1,bool SingleProjectile=false)
{
    internal static MeleeAttackComponent SpinnerexExplosion()=>new(
        JsonSerializer.SerializeToElement(new Dictionary<string,object>
        {
            ["AttackName"]="Spinnerex Death Explosion",["Damage"]=new Dictionary<string,double>{{"Toxin",480}},
            ["CritChance"]=0d,["CritMultiplier"]=1d,["StatusChance"]=0d,["ShotType"]="AoE",
            ["ForcedProcs"]=new[]{"Toxin"}
        }),
        new("Spinnerex Death Explosion",Co:0,Tennokai:false,EvolutionDamage:false,EvolutionStats:false,
            StatusMods:false,CriticalMods:false,Combo:false,Stealth:false),true,NoEnergyChannel:true);
}
