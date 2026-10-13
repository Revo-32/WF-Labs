using System.Text.Json;

namespace TennoForge.Core;

public record ThalysShardResult(Calculation Echo,Calculation? Impale,double Duration,double TriggerMotion,string[] Notes);

/// <summary>Per-host shard components. The triggering attack belongs to a different target.</summary>
public static class ThalysMechanic
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Zariman/Melee/HeavyScythe/ZarimanHeavyScythe/ZarimanHeavyScytheWeapon";
    public const string Source="https://wiki.warframe.com/w/Thalys?oldid=2817401";
    public static Calculation Detonation(Catalog catalog,Build build,bool root)
    {
        if(build.WeaponId!=WeaponId||build.Melee is not {Incarnon:true} setup)throw new ArgumentException("변신 중인 탈리스 빌드가 필요합니다.");
        var attack="Thalys Chain Shatter Explosion";
        var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["AttackName"]=attack,
            ["Damage"]=new Dictionary<string,double>{{"Puncture",180},{"Slash",420}},["CritChance"]=0d,["CritMultiplier"]=1d,["StatusChance"]=.24,["ShotType"]="AoE"});
        var policy=new AttackRule(attack,DamageSource:"Combined",Co:1,Tennokai:false,Incarnon:true,EvolutionDamage:false,EvolutionStats:false,
            Faction:false,Combo:root,Stealth:false,FollowThrough:false,HitCombo:false,HealingReturn:false,Code:"ThalysDetonation");
        return MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Melee=setup with {Attack=attack,Tennokai=false,Finisher=null,TargetIndex=1,Distance=0},Scenario=build.Scenario with {MotionMultiplier=1}},
            component:new(raw,policy,NoSupplementalElements:true,NoDuplicate:true,WeaponDoughty:true,NoEnergyChannel:true));
    }
    public static ThalysShardResult Calculate(Catalog catalog,Build build,double triggerMotion=1)
    {
        if(build.WeaponId!=WeaponId||build.Melee is not {Incarnon:true} setup)throw new ArgumentException("변신 중인 탈리스 빌드가 필요합니다.");
        MeleeEngine.Range(triggerMotion,.01,100,"가시를 발동시킨 공격의 동작 배율");
        var perks=IncarnonEngine.ForWeapon(WeaponId)!.Perks.Where(p=>(setup.Evolutions??[]).Contains(p.Id)).ToArray();
        var splinters=perks.Any(p=>p.Effects.ContainsKey("ShardDamageMultiplier"));
        var growth=perks.Any(p=>p.Effects.ContainsKey("ShardImpale"));
        Calculation Part(bool impale)
        {
            var attack=impale?"Thalys Shard Impale":"Thalys Shard Echo";
            var scale=impale?2:1;
            var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["AttackName"]=attack,
                ["Damage"]=new Dictionary<string,double>{{"Puncture",67.5*scale},{"Slash",157.5*scale}},
                ["CritChance"]=0d,["CritMultiplier"]=1d,["StatusChance"]=0d});
            var policy=new AttackRule(attack,DamageSource:"Combined",Co:impale?1:0,Tennokai:false,Incarnon:true,EvolutionDamage:false,EvolutionStats:false,
                StatusMods:false,Elements:false,Faction:false,Ips:false,Stealth:false,FollowThrough:false,HitCombo:false,HealingReturn:false,Code:"ThalysShard");
            var component=new MeleeAttackComponent(raw,policy,DamageMultiplier:splinters?.5:1,NoSupplementalElements:true,
                NoWeaponStatusDuration:true,NoDuplicate:true,WeaponDoughty:true,NoEnergyChannel:true);
            var input=build with {Stance=null,Melee=setup with {Attack=attack,Tennokai=false,Finisher=null,TargetIndex=1,Distance=0},Scenario=build.Scenario with {MotionMultiplier=triggerMotion}};
            return MeleeEngine.CalculateAttack(catalog,input,component:component);
        }
        return new(Part(false),growth?Part(true):null,splinters?30:15,triggerMotion,[
            "새 대상에 심는 행위 자체는 그 대상에 가시 피해를 주지 않습니다. 다른 대상에 남은 가시 각각의 숙주 피해입니다.",
            "기본 가시225 · 심은 공격이 아닌 새 가시를 발동시킨 공격의 동작 배율 적용 · 기본 피해 보너스 적용 · 컨오/물리/원소/팩션 제외.",
            "고정 치확·상확과 치명 배수 모드는 적용 · 다우티는 일반 무기 수치 참조 · 듀플리케이트 제외 · 무기 상태 지속 모드 제외.",
            growth?"폭발적 성장: 완전 성장의 꿰뚫기는450 기반·컨오 적용. 성장 단계·입력당 한 번 성장·5초 꿰뚫기 사건은 별도입니다.":splinters?"보이드 스플린터: 가시 피해50% · 지속30초. 가시 약점을 겨냥한 다른 무기의 치확은 이 성분에 더하지 않습니다.":"가시 지속15초 · 대상마다 한 개. 재발동으로 가시를 소모하지 않습니다.",
            "성분별 시작 상태의 피해입니다. 여러 대상의 배치·성장·연쇄 폭발을 포함한 교전 총합은 아닙니다.",Source]);
    }
}
