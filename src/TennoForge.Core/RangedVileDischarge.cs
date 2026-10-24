using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;
public static class RangedVileDischarge
{
    public const string WeaponId="wiki:secondary/Embolist";
    public static bool Applies(ReferenceWeapon w)=>w.Id==WeaponId;
    public static readonly WikiSource Source=Load();
    private static WikiSource Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-discharge.json")!;return JsonSerializer.Deserialize<WikiSource>(stream,Catalog.Json)!;}
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)
    {
        if(!Applies(w))return attacks;
        return [..attacks,JsonSerializer.SerializeToElement(new{AttackIndex=2,AttackName="Vile Discharge · sac impact",ShotType="Projectile",Trigger="Semi-Auto",AmmoCost=1,FireRate=2.5,CritChance=0,CritMultiplier=1,StatusChance=0,Multishot=1,Damage=new{Impact=10}}),JsonSerializer.SerializeToElement(new{AttackIndex=3,AttackName="Vile Discharge · stored explosion",ShotType="AoE",Trigger="Semi-Auto",AmmoCost=1,FireRate=2.5,CritChance=0,CritMultiplier=1,StatusChance=0,Multishot=1,Range=6,Falloff=new{StartRange=0,EndRange=6,Reduction=.8},Damage=new{Toxin=1}})];
    }
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult r)
    {
        if(!Applies(w))return r;
        var effects=r.Modifiers!;var cap=effects.GetValueOrDefault("DischargeCapacity");if(cap<=0&&b.AttackIndex==0)return r;if(cap<=0)throw new ArgumentException("바일 디스차지를 장착해야 주머니를 발사할 수 있습니다.");
        var c=b.Conditions??new();if(!double.IsFinite(c.DischargeStored)||c.DischargeStored<0||c.DischargeStored>cap)throw new ArgumentException("저장 피해가 바일 디스차지의 현재 랭크 한도를 벗어납니다.");
        if(b.AttackIndex==0)return r;
        var cadence=r.Cadence! with{NoReload=true,Reload=0,Magazine=1,AmmoCost=1};
        effects["Multishot"]=0;effects["NativeMultishot"]=1;effects["CriticalTierUpgradeChance"]=0;
        effects["NonCriticalAttritionChance"]=0;effects["NonCriticalAttritionBonus"]=0;
        effects["ExtraMultishotDamage"]=0;effects["ExtraProjectileDamageRatio"]=1;
        var result=r with{CriticalChance=0,CriticalMultiplier=1,StatusChance=0,Multishot=1,ExpectedShot=r.Damage.Values.Sum(),Cadence=cadence,Reload=0,Magazine=1,BurstDps=null,SustainedDps=null,Scope=[..r.Scope,"바일 디스차지 · 저장 피해는 기본 사격·그 상태 피해와 캐스케이디아 임파워드에서 누적합니다. 방어·적 피해 증폭 전 출력 중 실제 방어층·체력을 소모한 비율만 누적하며 초과 처치 피해는 제외합니다. 방출 직격 10 Impact, 폭발은 저장 피해의 Toxin이며 멀티샷·치명타·상태 효과를 적용하지 않습니다."]};
        if(b.AttackIndex==1)return result;
        var range=6*Math.Max(0,1+effects.GetValueOrDefault("BlastRadius"));
        var value=range>0&&c.RadialDistance<=range?c.DischargeStored*(1-.8*Math.Clamp(c.RadialDistance/range,0,1)):0;
        effects["EffectiveRange"]=range;effects["ActiveCO"]=0;effects["InnateCO"]=0;
        return result with{TimingIssues=[..result.TimingIssues,"저장 폭발은 방출 주머니의 명중에서 발생합니다. 주머니 발사를 선택하세요."],Damage=new(){[DamageType.Toxin]=value},ArsenalDamage=new(){[DamageType.Toxin]=c.DischargeStored},ExpectedShot=value,BaseDamage=c.DischargeStored,BaseFactor=1,FactionFactor=1,CoFactor=1,StatusBaseFactor=1};
    }
}
