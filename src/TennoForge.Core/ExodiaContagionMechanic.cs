using System.Text.Json;

namespace TennoForge.Core;

public record ContagionPart(string Name,Dictionary<DamageType,double> Damage,double CriticalChance,double CriticalMultiplier,double StatusChance,double ExpectedDamage,string[] ForcedProcs,DamageBrackets Brackets,StatusDamage[] StatusDamage);
public record ContagionResult(ContagionPart Contact,ContagionPart EmbeddedTick,ContagionPart Explosion,double TravelMultiplier,double StanceMultiplier,string[] Notes);
public enum ContagionPhase { Contact, Embedded, Explosion }

/// <summary>Pinned Exodia Contagion rev.2808344 and CO component table rev.2815998.</summary>
public static class ExodiaContagionMechanic
{
    public const string Source="https://wiki.warframe.com/w/Exodia_Contagion?oldid=2808344";
    private static readonly Dictionary<string,double> StanceMultipliers=new()
    {
        ["Pointed Wind"]=2,["Homing Fang"]=1,["Stinging Thorn"]=1,
        ["Stalking Fan"]=1,["Reaping Spiral"]=1,["Cleaving Whirlwind"]=3,["Rending Crane"]=4,["Tempo Royale"]=3,
        ["Crushing Ruin"]=4,["Shattering Storm"]=4,["Bleeding Willow"]=1,["Shimmering Blight"]=1,["Twirling Spire"]=1,
        ["Clashing Forest"]=1,["Flailing Branch"]=3,["Cyclone Kraken"]=3,["Sundering Weave"]=2,
        ["Blind Justice"]=3,["Decisive Judgement"]=2,["Tranquil Cleave"]=1,
        ["Crimson Dervish"]=3,["Iron Phoenix"]=3,["Swooping Falcon"]=3,["Vengeful Revenant"]=3,["Vulpine Mask"]=1
    };
    public static double ExplosionStanceMultiplier(string weaponClass,string? stanceId)
    {
        if(stanceId is not null)
            return StanceMultipliers.TryGetValue(stanceId,out var value)?value:throw new CombatRuleUnavailableException("이 스탠스의 컨테이전 폭발 배율은 위키 표에 없습니다: "+stanceId);
        return weaponClass switch {"Scythe" or "Hammer"=>2,"Heavy Blade"=>3,"Dagger" or "Polearm" or "Staff" or "Machete" or "Nikana" or "Sword" or "Rapier"=>1,_=>throw new ArgumentException("컨테이전 무기군 오류")};
    }
    public static ContagionResult Calculate(Catalog catalog,Build build,double projectileTravel)
    {
        ContagionPart Part(ContagionPhase phase)
        {
            var c=Evaluate(catalog,build,projectileTravel,phase);
            return new(Label(phase),c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.ExpectedHit,c.Melee!.ForcedProcs,c.Melee.Brackets!,c.Melee.StatusDamage);
        }
        var contact=Part(ContagionPhase.Contact);var embedded=Part(ContagionPhase.Embedded);var explosion=Part(ContagionPhase.Explosion);
        var stanceMultiplier=ExplosionStanceMultiplier(catalog.Weapons.Single(w=>w.Id==build.WeaponId).WeaponType,build.Capacity!.StanceEquipped?build.Capacity.StanceId:null);
        var combo=MeleeEngine.Calculate(catalog,build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,TargetIndex=1}}).Melee!.EffectiveCombo;
        var travel=projectileTravel>=30?1+build.Melee!.Exodia!.Rank+(combo-1)/4:1;
        return new(contact,embedded,explosion,travel,stanceMultiplier,
            ["불릿/더블 점프 후 에임 글라이드 중 공중 근접으로 발사한 투사체 · 중심·방어 적용 전",
             "직격에서 생존한 대상에게 1초 간격 부착 피해 4회, 5초째 폭발 · 직격 즉사 시 계속 비행",
             "표시 성분은 각각의 시작 상태를 고정한 값입니다. 적 상태·치명타·방어층 전이에 따른 전체 비행 시뮬레이션과 구분합니다.",
             "직격 CO는 별도 곱연산 · 부착/폭발에는 CO 미적용 · 아라크네 등 범용 무기 피해는 근접 피해와 별도 곱연산",
             "폭발 반경 8m · 근접 사거리 모드 제외 · 폭발 스탠스 배율은 위키 표에서 선택",
             "직격/폭발이 맞는 순간 강공격 동작 중이면 킬링 블로우·강공격 치확 적용 · 부착 틱/일반 강공격 콤보 배수 제외",Source]);
    }
    public static string Label(ContagionPhase phase)=>phase switch {ContagionPhase.Contact=>"직격",ContagionPhase.Embedded=>"부착 피해 · 1회",ContagionPhase.Explosion=>"폭발",_=>throw new ArgumentException("컨테이전 성분 오류")};
    internal static Calculation Evaluate(Catalog catalog,Build build,double projectileTravel,ContagionPhase phase,bool useInitialComboFloor=true)
    {
        if(!ZawCatalog.IsZaw(build.WeaponId)||build.Melee?.Exodia is not {Active:true} slot||MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code!="Contagion")throw new ArgumentException("컨테이전을 장착·활성화한 조우가 필요합니다.");
        MeleeEngine.Range(projectileTravel,0,1000,"컨테이전 투사체 이동 거리");
        var conditions=build.Melee.Conditions??[];
        var buffs=build.Melee.ContagionBuffs??new();buffs.Validate();
        var weapon=catalog.Weapons.Single(w=>w.Id==build.WeaponId);var total=weapon.Damage.Values.Sum();
        var stance=build.Capacity!.StanceEquipped?build.Capacity.StanceId:null;
        var stanceMultiplier=ExplosionStanceMultiplier(weapon.WeaponType,stance);
        // Wiki's threshold is travel of at least 30 m; combo bonus joins this
        // one bracket at every arcane rank, and is not a heavy-attack multiplier.
        var combo=MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Melee=build.Melee with {Attack="Normal Attack",Tennokai=false,TargetIndex=1}},useInitialComboFloor:useInitialComboFloor).Melee!.EffectiveCombo;
        var travel=projectileTravel>=30?1+slot.Rank+(combo-1)/4:1;
        Calculation Part(string name,Dictionary<DamageType,double> damage,string[] forced,bool radial,double co,double multiplier)
        {
            var attackName="Exodia Contagion "+phase;
            var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>
            {
                ["AttackName"]=attackName,["Damage"]=damage,["CritChance"]=weapon.CriticalChance,["CritMultiplier"]=weapon.CriticalMultiplier,
                ["StatusChance"]=weapon.StatusChance,["ShotType"]=radial?"AoE":"Projectile",["ForcedProcs"]=forced
            });
            // The animation at impact is the trigger, not the attack used to
            // launch the projectile. This never adds a heavy combo multiplier.
            var heavy=phase!=ContagionPhase.Embedded&&conditions.GetValueOrDefault("HeavyAnimation")>0;
            var policy=new AttackRule(attackName,Co:co,CoMultiplicative:co>0,Heavy:heavy,Tennokai:false,EvolutionDamage:false,EvolutionStats:false,Code:"Contagion",HitCombo:false);
            var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=attackName,Tennokai=false,TargetIndex=1,Distance=0}};
            var result=MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,DamageMultiplier:travel*multiplier,MeleeDamageBonus:buffs.MeleeAdditive,WeaponDamageBonus:buffs.WeaponAdditive,SeparateDamageMultiplier:buffs.SeparateMultiplier));
            return buffs.Active?result with {Limitations=result.Limitations.Append(buffs.Description+" "+Source).ToArray()}:result;
        }
        return phase switch
        {
            ContagionPhase.Contact=>Part(Label(phase),new(){[DamageType.Impact]=total*2/3,[DamageType.Puncture]=total*2/3,[DamageType.Slash]=total*2/3},[],false,1,1),
            ContagionPhase.Embedded=>Part(Label(phase),new(){[DamageType.Blast]=total},["Impact"],false,0,1),
            ContagionPhase.Explosion=>Part(Label(phase),new(){[DamageType.Viral]=total*5},projectileTravel>=30?["Impact","Ragdoll"]:["Impact"],true,0,stanceMultiplier),
            _=>throw new ArgumentException("컨테이전 성분 오류")
        };
    }
}
