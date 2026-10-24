using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedArcaneRule(string Id,string Name,string Code,string Category,int MaxRank,int Cap,double Duration,WikiSource? Source,string Description);
public static class RangedArcanes
{
    public static readonly RangedArcaneRule[] Rules=Load();
    private static RangedArcaneRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-arcanes.json")!;return JsonSerializer.Deserialize<RangedArcaneRule[]>(s,Catalog.Json)!;}
    public static RangedArcaneRule? Resolve(RangedBuild build,ReferenceWeapon weapon)
    {
        if(build.Arcane=="None"){if(build.ArcaneStacks!=0)throw new ArgumentException("빈 아케인의 중첩은 0이어야 합니다.");return null;}
        var rule=build.Arcane=="Merciless"?Rules.Single(r=>r.Code=="Merciless"&&r.Category==weapon.Category):Rules.SingleOrDefault(r=>r.Id==build.Arcane)??throw new ArgumentException("총기 아케인 식별자가 없습니다.");
        if(rule.Category!=weapon.Category||build.ArcaneRank<0||build.ArcaneRank>rule.MaxRank||build.ArcaneStacks<0||build.ArcaneStacks>rule.Cap)throw new ArgumentException("아케인 호환·랭크·중첩 범위 오류");
        if(rule.Code=="Sharpshot"&&weapon.WeaponType!="Bow"||rule.Code=="Vendetta"&&weapon.WeaponType!="Shotgun")throw new ArgumentException("이 무기군에 장착할 수 없는 아케인입니다.");
        if(rule.Code=="AkimboSlip"&&weapon.WeaponType is not ("Dual Pistols" or "Dual Shotguns"))throw new ArgumentException("아킴보 슬립 샷은 쌍권총·쌍산탄총 전용입니다.");
        return rule;
    }
    public static void Apply(RangedBuild build,ReferenceWeapon weapon,Dictionary<string,double> effects)
    {
        var rule=Resolve(build,weapon);if(rule is null)return;
        var c=build.Conditions??new();var rank=build.ArcaneRank;var k=rank+1;var age=c.ArcaneAge??c.Elapsed;var n=(double)build.ArcaneStacks;
        if(rule.Duration>0&&age>=rule.Duration)n=rule.Code is "Merciless" or "Deadhead" or "Dexterity" or "Exhilarate"?Math.Max(0,n-Math.Floor(age/rule.Duration)):0;
        void Add(string key,double value)=>effects[key]=effects.GetValueOrDefault(key)+value;
        switch(rule.Code)
        {
            case "AkimboSlip":if(c.Sliding||c.AimGlide)Add("AmmoEfficiency",.15+.1*rank);break;
            case "Merciless":Add("Damage",.05*k*n);if(rank==5)Add("ReloadSpeed",.3);break;
            case "Deadhead":Add("Damage",.2*k*n);if(rank==5){Add("HeadshotMultiplier",.3);Add("Recoil",-.5);}break;
            case "Dexterity":Add("Damage",.1*k*n);break;
            case "Frostbite":Add("CriticalDamage",.005*k*n);Add("Multishot",.00375*k*n);break;
            case "Blight":Add("CriticalDamage",.006*k*n);Add("Multishot",.003*k*n);break;
            case "Flare":Add("Damage",.02*k*n);break;
            case "Voltage":Add("ReloadSpeed",.0025*k*n);Add("Multishot",.005*k*n);break;
            case "Accuracy":if(n>0)Add("WeakpointCriticalChance",.5*k);break;
            case "Overcharge":if(c.Overshields)Add("CriticalChance",.5*k);break;
            case "Empowered":Add("StatusExtraDamage",250+100*rank);break;
            case "Shiver":Add("Shiver",.075*k);break;
            case "Fortifier":Add("OverguardMultiplier",rank+3);break;
            case "Encumber":Add("EncumberChance",.04*k);break;
            case "Enervate":Add("FlatCriticalChance",.1*n);Add("EnervateReset",k);break;
            case "Kinship":Add("CriticalChance",new[]{.0334,.0667,.1002,.1336,.167,.2004}[rank]*n);break;
            case "Outburst":var outburst=new[]{.05,.07,.09,.12,.16,.2}[rank];Add("CriticalChance",outburst*n);Add("CriticalDamage",outburst*n);break;
            case "Surge":Add("SurgeMultiplier",n>0?Math.Min(2+rank,c.StoredSurgeBonus):0);break;
            case "Cryogenic":Add("PunctureColdStacks",1+rank/2);break;
            case "Irradiate":Add("RadiationSpread",.8+.2*rank);break;
            case "Vendetta":if(n>0){Add("Multishot",.3*k);Add("ReloadSpeed",.125*k);}break;
            case "Bulwark":Add("Damage",Math.Clamp(c.PlayerArmor-1000,0,500)*(.005+.001*rank));break;
            case "Crux":Add("StatusChance",.05*k*n);Add("AmmoEfficiency",.01*k*n);break;
            case "Debilitate":Add("DebilitateChance",.5+.1*rank);break;
            case "Exhilarate":Add("EnergyRegen",.2*k*n);break;
            case "Obstruct":Add("MagneticJamRadius",15);Add("MagneticJamCooldown",60-10*rank);break;
            case "EnergyMultishot":if(c.MaxEnergy>0&&c.CurrentEnergy>=.9*c.MaxEnergy)Add("Multishot",Math.Min(1+.5*rank,.0035*c.MaxEnergy));break;
            case "PlatedRound":
                var magazine=Math.Max(1,Math.Round((RangedEngine.N(weapon.Raw,"Magazine")??1)*(1+effects.GetValueOrDefault("Magazine")),MidpointRounding.AwayFromZero));
                if(c.RecentReload&&c.ReloadAge<new[]{3,4,5,6,8,10}[rank]&&RangedEngine.S(weapon.Raw,"ReloadStyle")!="Regenerate"&&weapon.WeaponType!="Bow")Add("Damage",Math.Clamp(c.RoundsReloaded/magazine,0,1)*.15*Math.Sqrt(5*magazine));break;
            case "Sharpshot":if(n>0)Add("FinalDamage",.5*k);break;
            case "Compression":RangedCompression.Apply(build,weapon,effects);break;
        }
    }
}
