using System.Text.Json;
using System.Text.Json.Nodes;

namespace TennoForge.Core;

/// <summary>Weapon-specific motions replace the same input in every equipped stance.
/// Component damage in the weapon infobox is not the ordinary blade's damage.</summary>
public static class WeaponStanceMechanic
{
    public const string Argo="/Lotus/Weapons/Tenno/Melee/SwordsAndBoards/DaxDuviriMaceShieldWeapon";
    public const string Edun="/Lotus/Weapons/Tenno/Melee/Polearms/DaxDuviriPolearm/DaxDuviriPolearmWeapon";
    public const string Syam="/Lotus/Weapons/Tenno/Melee/Swords/DaxDuviriKatana/DaxDuviriKatanaWeapon";
    public const string Quassus="/Lotus/Weapons/Tenno/Melee/Warfan/TnBrokenFrameWarfan/TnBrokenFrameWarfanWeapon";
    public const string QuassusPrime="/Lotus/Weapons/Tenno/Melee/Warfan/TnBrokenFrameWarfan/TnBrokenFramePrimeWarfanWeapon";
    public const string Verdilac="/Lotus/Weapons/Archon/Melee/Whip/ArchonWhipPlayerWep";
    public const string Korumm="/Lotus/Weapons/Archon/Melee/Trident/ArchonTridentPlayerWep";
    public const string Arum="/Lotus/Weapons/Infested/Melee/InfWarfan/InfWarfanWeapon";
    public const string Nepheri="/Lotus/Weapons/Archon/Melee/DualDaggers/ArchonDualDaggersPlayerWep";
    public const string Agendus="/Lotus/Weapons/Corpus/Melee/ShieldAndSword/CrpHammerShield/CrpHammerShield";
    public const string Grigori="/Lotus/Weapons/Corpus/Melee/CrpBriefcaseScythe/CrpBriefcaseScythe";
    public static bool Applies(string id)=>id is Argo or Edun or Syam or Quassus or QuassusPrime or Verdilac or Korumm or Arum or Nepheri or Agendus or Grigori;
    public static bool IsQuassus(string id)=>id is Quassus or QuassusPrime;
    public static bool IsWarfan(string id)=>IsQuassus(id)||id==Arum;
    public static string Key(string id)=>id==Nepheri?"Neutral":id is Verdilac or Korumm?"Block":"Heavy";
    public static bool IsSpecial(string id,string combo)=>Applies(id)&&(combo==Key(id)||id is Edun or Syam&&combo=="Heavy Sliding");
    public static string Source(string id)=>MeleeRuleCatalog.Load().Weapon(id).Source?.Url??throw new InvalidOperationException("고유 동작 출처 누락");
    public static Stance Resolve(string id,Stance stance,Stance[] originals)
    {
        var name=MeleeRuleCatalog.Load().Weapon(id).Name;
        var key=Key(id);
        var original=originals.Single(s=>s.Id==(id==QuassusPrime?"Quassus":name)).Combos.Single(c=>c.Key==key);
        var hits=original.Hits.Select(h=>
        {
            if(h.AttackType=="Slam"&&id==Edun)return h with {AttackOverride="Polearm Explosion",Multiplier=1};
            if(h.AttackType=="Slam"&&id==Korumm)return h with {AttackOverride="Tactical Combo Slam Attack",Multiplier=1};
            if(h.AttackType!="Ranged")return h;
            return id switch
            {
                Argo=>h with {AttackOverride="Heavy Attack Glaive",Multiplier=1},
                Edun=>h with {AttackOverride="Polearm Throw",Multiplier=1},
                Syam=>h with {AttackOverride=h.Group==1?"First Shockwave":"Second Shockwave",Multiplier=1},
                Quassus or QuassusPrime=>h with {AttackOverride=h.Group==1?"First Heavy Attack - Ethereal Daggers":"Second Heavy Attack - Ethereal Daggers",Multiplier=1,Count=id==QuassusPrime?(h.Group==1?18:9):(h.Group==1?12:6)},
                Verdilac=>h with {AttackOverride="Energy Wave"},
                Arum=>h with {AttackOverride=h.Group==1?"First Heavy Attack - Toxic Spines":"Second Heavy Attack - Toxic Spines",Multiplier=1},
                Nepheri=>h with {AttackOverride="Fireballs",Multiplier=1},
                Agendus or Grigori=>h with {AttackOverride="Energy Disk",Multiplier=1},
                _=>h
            };
        }).ToArray();
        var special=original with {Hits=hits,ResolutionNote=$"{name} 고유 {original.InputLabel}: 장착 스탠스와 관계없이 적용. 투사체는 무기 본문의 별도 기초 피해·발사 개수와 배율을 사용합니다. 직격 대상과 주변 폭발은 구분합니다. "+Source(id)};
        var combos=stance.Combos.Select(c=>c.Key==key?special:c).ToList();
        if(id is Edun or Syam)
        {
            var sliding=originals.Single(s=>s.Id==(id==Edun?"Polearm":"Nikana")).Combos.Single(c=>c.Key=="Heavy");
            combos.Add(sliding with {Key="Heavy Sliding",ActionOverride="Heavy",ResolutionNote="슬라이딩 중 강공격은 고유 투척/파동 대신 일반 무기군 강공격을 사용합니다. 샤암의 이 동작은 강제 베기를 유지합니다. "+Source(id)});
        }
        return stance with {Combos=combos.ToArray()};
    }
    internal static JsonElement? Raw(MeleeWeaponRule profile,string name,StanceHit? hit)
    {
        if(!Applies(profile.Id))return null;
        var a=profile.Raw.GetProperty("Attacks").EnumerateArray().FirstOrDefault(a=>a.Text("AttackName")==name);
        if(a.ValueKind==JsonValueKind.Undefined)return null;
        var single=hit?.AttackOverride is not null;
        var verdilac=profile.Id==Verdilac&&name.StartsWith("Energy Wave");
        var syam=profile.Id==Syam&&name.EndsWith("Shockwave");
        var argo=profile.Id==Argo&&name.StartsWith("Heavy Attack Glaive");
        var nepheri=profile.Id==Nepheri&&name=="Fireballs";
        if(!single&&!verdilac&&!syam&&!argo&&!nepheri)return null;
        var node=JsonNode.Parse(a.GetRawText())!.AsObject();
        // Count in the stance row is already the number of individual daggers.
        if(single)node["Multishot"]=1;
        if(verdilac)node["ShotType"]=name.EndsWith("Explosion")?"AoE":"Projectile";
        if(syam){node["ShotType"]="Projectile";node["Range"]=230;}
        if(nepheri)node["ShotType"]="Projectile";
        if(argo)foreach(var key in node["Damage"]!.AsObject().Select(p=>p.Key).ToArray())node["Damage"]![key]=node["Damage"]![key]!.GetValue<double>()*3;
        return JsonSerializer.SerializeToElement(node);
    }
    public static AttackRule? Policy(MeleeWeaponRule profile,string attack)
    {
        if(profile.Id==Korumm&&attack=="Tactical Combo Slam Attack")return profile.Attacks.Single(a=>a.Key==attack) with {
            DamageSource="None",Code="KorummTacticalSlam",Co=0,Combo=false,ComboStats=false,EvolutionDamage=false,EvolutionStats=false,
            CriticalMods=false,StatusMods=false,Elements=false,Ips=false,Stealth=false,FollowThrough=false};
        if(profile.Id==Verdilac&&attack.StartsWith("Energy Wave"))return profile.Attacks.Single(a=>a.Key==attack) with {Co=attack.EndsWith("Explosion")?0:1,HitCombo=false,FollowThrough=false};
        if(profile.Id==Nepheri&&attack=="Fireballs")return profile.Attacks.Single(a=>a.Key==attack) with {HitCombo=false,FollowThrough=false};
        if(Applies(profile.Id)&&profile.Raw.GetProperty("Attacks").EnumerateArray().Any(a=>a.Text("AttackName")==attack&&a.Text("ShotType")=="Projectile"))return profile.Attacks.Single(a=>a.Key==attack) with {FollowThrough=false};
        return null;
    }
}
