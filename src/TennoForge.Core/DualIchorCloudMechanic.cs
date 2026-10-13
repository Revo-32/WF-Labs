using System.Text.Json;

namespace TennoForge.Core;

public record IchorCloudContact(double Time,int Cloud=1,int Target=1);
/// <summary>Known per-contact payload; the Wiki does not state a damage tick interval.</summary>
public static class DualIchorCloudMechanic
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Axe/DualInfestedAxesWeapon";
    public const string Source="https://wiki.warframe.com/w/Dual_Ichor_Incarnon_Genesis?oldid=2812453";
    public const double Lifetime=7,Radius=4;
    public static bool Applies(string weaponId)=>IncarnonEngine.ForWeapon(weaponId)?.Code=="DualIchor";
    public static string PayloadSignature(Catalog catalog,Build build)
    {
        var neutral=build with {Arcanes=(build.Arcanes??new()) with {Frozen=false},Scenario=build.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},
            Melee=build.Melee! with {Statuses=[],Faction="Neutral",Shields=0,Overguard=0,Debuffs=[],HeatArmorStrip=0,Conditions=(build.Melee.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,useInitialComboFloor:false);
        return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
    public static Calculation Calculate(Catalog catalog,Build build,double distance=0,bool useInitialComboFloor=true)
    {
        if(!Applies(build.WeaponId)||build.Melee?.Incarnon!=true)throw new ArgumentException("인카논 변신 중인 듀얼 이코르가 필요합니다.");
        var definition=IncarnonEngine.ForWeapon(build.WeaponId)!;
        var evolved=definition.Perks.Any(p=>p.Stage==2&&(build.Melee.Evolutions??[]).Contains(p.Id));
        var name="Dual Ichor Toxin Field";
        var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>
        {
            ["AttackName"]=name,["Damage"]=new Dictionary<string,double>{{"Toxin",evolved?440:350}},
            ["CritChance"]=0d,["CritMultiplier"]=1d,["StatusChance"]=.5,["Range"]=Radius,["ShotType"]="AoE"
        });
        var policy=new AttackRule(name,Co:0,Tennokai:false,EvolutionDamage:false,EvolutionStats:false,StatusMods:false,CriticalMods:false,
            Combo:false,Code:"IchorCloud",FollowThrough:false,HitCombo:false);
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=name,Tennokai=false,Finisher=null,Distance=distance,TargetIndex=1}};
        var result=MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,NoEnergyChannel:true,FixedStatusChance:true));
        return result with {Limitations=result.Limitations.Concat(new[]{
            "인카논 독 구름1회 접촉: 350/진화II440 · 변신+100% 피해·원소·팩션·상태 피해·은신 적용 · 컨오/치명/상확 모드 제외",
            "구름7초·반경4m 고정. 원문에 최초 피해 시각·틱 간격이 없어 총 틱 수나 초당 피해로 바꾸지 않습니다.",
            "구름의 상태는 인플루언스를 발동/전파하지 않습니다. 실제 처치는 후속 구름을 만들며 독성 대상 접촉은 알케미스트 진화의+5콤보 대상입니다.",Source}).ToArray()};
    }
}
