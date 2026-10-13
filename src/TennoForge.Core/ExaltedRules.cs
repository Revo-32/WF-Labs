using System.Text.Json;

namespace TennoForge.Core;

public enum ExaltedFamily { Blade, Wind, Talons, Staff, Claws, Diwata, Landslide, Lash, Whipclaw, Clones }
public record ExaltedDefinition(string Id,ExaltedFamily Family,string? Stance,double[] RankDamage,string Source)
{
    public bool Pseudo=>Family is ExaltedFamily.Landslide or ExaltedFamily.Lash or ExaltedFamily.Whipclaw or ExaltedFamily.Clones;
    // Hotfix 38.5.3 re-enabled Diwata's Exilus slot for parry/block mods.
    public bool Exilus=>!Pseudo;
    public bool AttackSpeed=>Family is not (ExaltedFamily.Landslide or ExaltedFamily.Lash or ExaltedFamily.Whipclaw);
}

public static class ExaltedRules
{
    public const string GeneralSource="https://wiki.warframe.com/w/Exalted_Weapon?oldid=2814932";
    public const string CapacitySource="https://www.warframe.com/en/patch-notes/pc/38-5-0";
    public const string DiwataExilusSource="https://www.warframe.com/en/patch-notes/pc/38-5-3";
    private static readonly Dictionary<string,ExaltedDefinition> Definitions=Create();
    public static ExaltedDefinition? Find(string id)=>Definitions.GetValueOrDefault(id);
    public static IEnumerable<ExaltedDefinition> All=>Definitions.Values;
    private static Dictionary<string,ExaltedDefinition> Create()
    {
        var d=new Dictionary<string,ExaltedDefinition>();
        void Add(ExaltedFamily family,string? stance,double[] damage,string page,long rev,params string[] ids)
        {foreach(var id in ids)d[id]=new(id,family,stance,damage,$"https://wiki.warframe.com/w/{page}?oldid={rev}");}
        Add(ExaltedFamily.Blade,"Exalted Blade",[100,125,200,250],"Exalted_Blade",2817346,"/Lotus/Powersuits/Excalibur/DoomSword","/Lotus/Powersuits/Excalibur/DoomSwordPrime","/Lotus/Powersuits/Excalibur/DoomSwordUmbra");
        Add(ExaltedFamily.Wind,"Serene Storm",[100,125,200,250],"Serene_Storm",2816022,"/Lotus/Powersuits/Pacifist/PacifistFist","/Lotus/Powersuits/Pacifist/PacifistFistPrime");
        Add(ExaltedFamily.Talons,"Hysteria",[100,125,200,250],"Hysteria",2816219,"/Lotus/Powersuits/Berserker/BerserkerMelee","/Lotus/Powersuits/Berserker/BerserkerMeleePrime");
        Add(ExaltedFamily.Staff,"Primal Fury",[100,200,250,300],"Primal_Fury",2816241,"/Lotus/Powersuits/MonkeyKing/MonkeyKingStaff","/Lotus/Powersuits/MonkeyKing/WukongPrimeStaff");
        Add(ExaltedFamily.Claws,"Ravenous Wraith",[100,150,200,250],"Exalted_Shadow",2816185,"/Lotus/Powersuits/Wraith/ReaperMeleeWeapon","/Lotus/Powersuits/Wraith/SevagothShadowPrimeClawsWeapon");
        Add(ExaltedFamily.Diwata,null,[150,165,180,200],"Razorwing",2816208,"/Lotus/Powersuits/Fairy/FlightSword","/Lotus/Powersuits/Fairy/PrimeFlightSword");
        Add(ExaltedFamily.Landslide,null,[100,200,300,350],"Landslide",2815643,"/Lotus/Powersuits/Brawler/AtlasPunchWeapon","/Lotus/Powersuits/Brawler/AtlasPunchWeaponPrime");
        Add(ExaltedFamily.Lash,null,[250,300,350,400],"Shattered_Lash",2816059,"/Lotus/Powersuits/Glass/GaraShankWeapon","/Lotus/Powersuits/Glass/GaraShankWeaponPrime");
        Add(ExaltedFamily.Whipclaw,null,[100,112,125,150],"Whipclaw",2816105,"/Lotus/Powersuits/Khora/KhoraWhipclawWeapon","/Lotus/Powersuits/Khora/KhoraWhipclawWeaponPrime");
        Add(ExaltedFamily.Clones,null,[562,750,1125,1500],"Blade_Storm",2815642,"/Lotus/Powersuits/Ninja/NinjaStormWeapon","/Lotus/Powersuits/Ninja/NinjaStormWeaponPrime");
        return d;
    }
    public static Weapon[] Weapons()=>ReferenceCatalog.Load().Weapons.Where(w=>w.Category=="melee"&&Definitions.ContainsKey(w.InternalId)).Select(w=>{
        var a=w.Raw.GetProperty("Attacks")[0];return new Weapon(w.InternalId,w.Name,w.EnglishName,
            JsonSerializer.Deserialize<Dictionary<DamageType,double>>(a.GetProperty("Damage").GetRawText(),Catalog.Json)!,
            a.Number("CritChance"),a.Number("CritMultiplier"),a.Number("StatusChance"),a.Number("FireRate"),"Exalted Weapon",30,w.Source);
    }).ToArray();
    public static WeaponCapacityDefault Capacity(string id)
    {
        var raw=MeleeRuleCatalog.Load().Weapon(id).Raw;
        var slots=raw.Strings("Polarities").Select(p=>p switch{"Madurai"=>"AP_ATTACK","Vazarin"=>"AP_DEFENSE","Naramon"=>"AP_TACTIC","Umbra"=>"AP_UMBRA",_=>throw new InvalidOperationException("익절티드 기본 극성 원문 오류")}).ToList();
        while(slots.Count<8)slots.Add("None");return new(slots.ToArray(),"AP_POWER");
    }
    public static string DefaultAttack(string id)=>Find(id)?.Family==ExaltedFamily.Lash?"Damage":"Normal Attack";
    public static Stance ResolveStance(ExaltedDefinition d,Stance stance)
    {
        var name=d.Family switch {ExaltedFamily.Blade=>"익절티드 블레이드",ExaltedFamily.Talons=>"히스테리아",ExaltedFamily.Staff=>"프라이멀 퓨리",ExaltedFamily.Claws=>"래버너스 레이스",ExaltedFamily.Wind=>"서린 스톰",_=>stance.Name};
        if(d.Family!=ExaltedFamily.Blade)return stance with {Name=name};
        return stance with {Name=name,Combos=stance.Combos.Select(c=>c.Key is "Heavy" or "Slide"?c with {
            Hits=c.Hits.SelectMany(h=>h.AttackType is "" or "360"?new[]{h,h with {Count=h.Count*(c.Key=="Heavy"?2:1),AttackType="Ranged",ForcedProcs=c.Key=="Heavy"?["Lifted"]:["Stagger"]}}:new[]{h}).ToArray(),
            ResolutionNote="어빌리티 본문의 동반 파동: 강공격 입력마다 교차 파동2개, 슬라이드 파동1개. 검과 파동은 별도 피해·CO 브라켓. 원본 스탠스 표를 보존하고 파동을 연결했습니다. "+d.Source}:c).ToArray()};
    }
    public static string[] AdditionalAttacks(string id)=>Find(id)?.Family switch {ExaltedFamily.Blade=>["Exalted Wave","Exalted Heavy Wave"],ExaltedFamily.Wind=>["Exalted Wave"],_=>[]};
    public static AttackRule? WavePolicy(string id,string attack)
    {
        if(!AdditionalAttacks(id).Contains(attack))return null;
        var blade=Find(id)!.Family==ExaltedFamily.Blade;var heavy=attack=="Exalted Heavy Wave";
        return new(attack,Co:blade?(heavy?1:2):0,CoMultiplicative:heavy,Heavy:heavy,Combo:false,Code:"ExaltedWave",FollowThrough:false,RadiusMods:false,
            Notes:["익절티드 파동은 검/주먹 접촉과 별도 성분입니다. "+Find(id)!.Source,
                blade?"블레이드 라이트 파동 CO2 가산 · 헤비 파동 CO1 별도 곱. https://wiki.warframe.com/w/Condition_Overload_(Mechanic)?oldid=2815998":"데저트 윈드: 위키가 인용한 타격별 자료의 파동은 CO 피해 증가 없음. 주먹 접촉만 CO를 받습니다. 집계표60%/50%는 직접 타격과 파동을 합친 비율이므로 각 성분에 다시 곱하지 않습니다. https://wiki.warframe.com/w/Condition_Overload_(Mechanic)?oldid=2815998#Stances"]);
    }
    internal static JsonElement? WaveRaw(MeleeWeaponRule profile,string attack)
    {
        if(WavePolicy(profile.Id,attack) is null)return null;
        var values=JsonSerializer.Deserialize<Dictionary<string,object>>(profile.Raw.GetProperty("Attacks")[0].GetRawText(),Catalog.Json)!;
        values["AttackName"]=attack;values["ShotType"]="Projectile";
        if(Find(profile.Id)?.Family==ExaltedFamily.Wind)values["ForcedProcs"]=new[]{"Stagger"};
        if(attack=="Exalted Heavy Wave")
        {var damage=JsonSerializer.Deserialize<Dictionary<string,double>>(profile.Raw.GetProperty("Attacks")[0].GetProperty("Damage").GetRawText())!;foreach(var key in damage.Keys.ToArray())damage[key]*=5;values["Damage"]=damage;values["ForcedProcs"]=new[]{"Lifted"};}
        return JsonSerializer.SerializeToElement(values,Catalog.Json);
    }
    internal static double WaveFalloff(ExaltedDefinition d,MeleeSetup setup)
    {
        if(d.Family==ExaltedFamily.Wind)return setup.Distance<=20?1:0;
        var source=setup.Conditions?.GetValueOrDefault("BladeWaveFalloffModel")??0;
        var previous=setup.Attack=="Exalted Heavy Wave"?0:setup.TargetIndex-1;
        if(source is not (0 or 1 or 2))throw new ArgumentException("블레이드 파동 원문은 미선택0·시간식1·거리식2입니다.");
        if(source>0)
        {
            var limit=(source==1?75:70)-15*previous;
            if(setup.Distance>=limit)return 0;
            return Math.Max(0,1-setup.Distance/(source==1?75:70)-.2*previous);
        }
        if(setup.Distance>=75-15*(setup.Attack=="Exalted Heavy Wave"?0:setup.TargetIndex-1))return 0;
        // Both distance statements are preserved: 40%/s at 30m/s versus
        // a 0-70m linear interval. They disagree between endpoints.
        if(setup.Distance>0)throw new CombatRuleUnavailableException("익절티드 블레이드: 파동의 초당40%·30m/s(75m)와0~70m 선형 감쇠가 같은 위키 본문에서 충돌합니다. 0m 접촉 피해와최대75m 밖0은 확정되지만 중간 거리의 정확한 계수는 근거 충돌입니다. "+d.Source);
        return Math.Max(0,1-.2*(setup.Attack=="Exalted Heavy Wave"?0:setup.TargetIndex-1));
    }
    public static bool ModAllowed(string weaponId,MeleeModRule rule)
    {
        var d=Find(weaponId);if(d is null)return true;
        var effects=rule.Effects??[];
        if(effects.ContainsKey("Riven"))return false;
        if(d.Pseudo||d.Family==ExaltedFamily.Diwata)
        {
            if(effects.Keys.Any(k=>k is "Tennokai" or "HeavyLifeSteal" or "SlideCriticalChance"))return false;
            if(d.Pseudo&&effects.Keys.Any(k=>k is "BlockingComboChance" or "ParryChance" or "BlockAngle"))return false;
            // Mixed mods retain their usable effects: Elementalist, Amalgam
            // Organ Shatter and Galvanized Reflex are not pure heavy mods.
            if(effects.Count>0&&effects.Keys.All(k=>k is "HeavyDamage" or "HeavyEfficiency" or "Windup"))return false;
            if(d.Family!=ExaltedFamily.Clones&&effects.ContainsKey("FinisherDamage"))return false;
        }
        // Mixed mods such as Spoiled Strike keep their damage effect.
        if(!d.AttackSpeed&&effects.Count==1&&effects.ContainsKey("AttackSpeed"))return false;
        return true;
    }
    internal static void Validate(Build b,ExaltedDefinition d)
    {
        var setup=b.Melee!;MeleeEngine.Range(setup.AbilityRank,0,3,"어빌리티 랭크");MeleeEngine.Range(setup.AbilityRange,.01,100,"어빌리티 거리 배율");
        ExaltedAugments.Validate(d,setup);
        if(!d.Exilus&&setup.Exilus is not null)throw new ArgumentException("이 익절티드 무기에는 엑실러스 슬롯이 없습니다.");
        if((d.Pseudo||d.Family==ExaltedFamily.Diwata)&&setup.Finisher is not null)throw new ArgumentException("이 익절티드 무기는 별도의 마무리 동작을 선택할 수 없습니다. 블레이드 스톰의 고유 마무리 피해는 기본 공격으로 계산합니다.");
        if(d.Pseudo&&(setup.Attack.Contains("Heavy")||setup.Attack.Contains("Slide")||setup.Tennokai))throw new ArgumentException("간접 익절티드 무기는 강공격·슬라이드·텐노카이를 사용할 수 없습니다.");
        foreach(var slot in b.Slots.Append(setup.Exilus).Where(s=>s is not null))
        {var rule=MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==slot!.ModId);if(!ModAllowed(b.WeaponId,rule))throw new ArgumentException(rule.Name+": 이 익절티드 무기에 장착할 수 없습니다.");}
        if(b.Arcanes?.Melee is {} arcane)
        {var code=MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==arcane.Id).Code;if((d.Pseudo||d.Family==ExaltedFamily.Diwata)&&(code=="Animosity"||code=="Crescendo"&&d.Family!=ExaltedFamily.Clones))throw new ArgumentException("이 무기에서는 해당 아케인의 발동 동작을 사용할 수 없습니다.");}
    }
}
