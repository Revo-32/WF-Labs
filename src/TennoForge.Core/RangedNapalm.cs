using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public static class RangedNapalm
{
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)
    {
        if(!RangedPentaAugments.Applies(w))return attacks;var n=JsonNode.Parse(attacks[0].GetRawText())!;n["AttackIndex"]=3;n["AttackName"]="Napalm Grenades · direct Heat";n["Damage"]=new JsonObject{["Heat"]=100};n["ShotType"]="Hit-Scan";n["ForcedProcs"]=new JsonArray("Heat");n["StatusChance"]=0;n["Multishot"]=1;return [..attacks,JsonSerializer.SerializeToElement(n)];
    }
    public static JsonElement Attack(ReferenceWeapon w,RangedBuild b,JsonElement attack)
    {
        if(!RangedPentaAugments.Applies(w)||b.AttackIndex>1||!RangedPentaAugments.Has(b,"PentaNapalm"))return attack;
        var n=JsonNode.Parse(attack.GetRawText())!;n["Damage"]=new JsonObject{["Heat"]=325};return JsonSerializer.SerializeToElement(n);
    }
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult r)
    {
        if(!RangedPentaAugments.Applies(w))return r;var effects=r.Modifiers!;if(effects.GetValueOrDefault("PentaNapalm")<=0){if(b.AttackIndex==2)throw new ArgumentException("네이팜 그레네이즈를 장착해야 추가 화염을 계산할 수 있습니다.");return r;}
        var c=b.Conditions??new();if(c.NapalmUtilityMode is not ("Unspecified" or "Fixed" or "Modded")||!double.IsFinite(c.NapalmPatchRadius)||c.NapalmPatchRadius<0||c.NapalmPatchRadius>100||c.NapalmPatchTick is {} tick&&(!double.IsFinite(tick)||tick<0))throw new ArgumentException("네이팜 추가 화염·장판 시나리오 입력 오류");
        if(c.ManualGrenades&&effects.GetValueOrDefault("AdhesiveChance")<=0)throw new ArgumentException("네이팜은 충돌 즉시 기폭합니다. 배치 후 기폭하려면 애드히시브 블래스트를 장착하세요.");
        r=r with{Scope=[..r.Scope,"네이팜 그레네이즈: 직격·폭발 각각 325 Heat, 모드 적용 뒤 상확 +30%p(최대 랭크). 별도 직격 Heat 100과 장판 진입 시 Heat를 분리합니다. 장판은 직접 피해가 없고 약 5초 유지, 반경 약 2m의 보조 위키 값을 사용하되 변경 가능하며 반경·상태 지속 모드는 적용하지 않습니다.",c.NapalmExcludePatchDamage?"장판 Heat 상태와 방어력 변화는 포함하되 수치가 없는 장판의 상태 피해는 제외한 시나리오입니다.":"장판 상태 피해는 원문 수치가 아닌 지정한 1틱 출력을 사용합니다."]};
        if(b.AttackIndex<2)
        {
            if(b.AttackIndex==1&&c.Headshot&&c.AttachedTarget&&c.NapalmHeadshot){var multiplier=c.WeakpointMultiplier;var critical=c.HeadCritical?r.CriticalMultiplier*2:r.CriticalMultiplier;var packet=r.Damage.ToDictionary(p=>p.Key,p=>p.Value*multiplier);return r with{Damage=packet,StatusBaseFactor=r.StatusBaseFactor*multiplier,CriticalMultiplier=critical,ExpectedShot=packet.Values.Sum()*r.Multishot*RangedEngine.CriticalExpectation(r.CriticalChance,critical)};}
            return r;
        }
        if(c.NapalmUtilityMode=="Unspecified")throw new CombatRuleUnavailableException("별도 Heat 100의 모드·치명 배율은 원문에 없습니다. 고정 출력 또는 피해·화염·치명 모드 적용 시나리오를 선택하세요.");
        var fixedOutput=c.NapalmUtilityMode=="Fixed";var factor=fixedOutput?1:r.StatusBaseFactor;var faction=fixedOutput?1:r.FactionFactor;var value=100*factor*faction*(fixedOutput?1:1+effects.GetValueOrDefault("Heat"));var cc=fixedOutput?0:r.CriticalChance;var cd=fixedOutput?1:r.CriticalMultiplier;
        if(fixedOutput)effects=[];else effects=new(effects);effects["CriticalTierUpgradeChance"]=0;effects["NonCriticalAttritionChance"]=0;effects["ExtraMultishotDamage"]=0;effects["ExtraProjectileDamageRatio"]=1;
        return r with{Damage=new(){[DamageType.Heat]=value},ArsenalDamage=new(){[DamageType.Heat]=value},BaseDamage=100,BaseFactor=factor,StatusBaseFactor=factor,FactionFactor=faction,CoFactor=1,CriticalChance=cc,CriticalMultiplier=cd,StatusChance=0,Multishot=1,Modifiers=effects,ExpectedShot=value*RangedEngine.CriticalExpectation(cc,cd),BurstDps=null,SustainedDps=null,TimingIssues=["직격에 부가되는 화염입니다. 유탄 발사를 선택하세요."]};
    }
}
