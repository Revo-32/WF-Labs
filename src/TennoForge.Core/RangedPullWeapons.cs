using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public static class RangedPullWeapons
{
    public const string Proboscis="wiki:primary/Proboscis Cernos",Scyotid="wiki:secondary/Scyotid";
    public static bool Applies(ReferenceWeapon w)=>w.Id is Proboscis or Scyotid;
    public static JsonElement Normalize(ReferenceWeapon w,JsonElement attack,int index)
    {
        if(w.Id==Proboscis&&index==1)
        {
            var a=JsonNode.Parse(attack.GetRawText())!;a["Damage"]=JsonSerializer.SerializeToNode(new{Slash=50.625,Viral=39.375});a["CritChance"]=0;a["CritMultiplier"]=1;return JsonSerializer.SerializeToElement(a);
        }
        if(w.Id==Scyotid&&index==1)
        {
            var a=JsonNode.Parse(attack.GetRawText())!;a["Range"]=15;a["AmmoCost"]=4;a["CritChance"]=0;a["CritMultiplier"]=1;return JsonSerializer.SerializeToElement(a);
        }
        return attack;
    }
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult r)
    {
        if(!Applies(w))return r;
        var c=b.Conditions??new();var effects=r.Modifiers!;
        if(c.PullMotion is not ("Unspecified" or "None" or "Blocked" or "Linear")||c.PullSeconds is {} t&&(!double.IsFinite(t)||t<=0||t>300))throw new ArgumentException("끌어오기 경로 설정 오류");
        if(w.Id==Proboscis&&b.AttackIndex==0)
            return r with{BurstDps=null,SustainedDps=null,Scope=[..r.Scope,"프로보시스 서노스: 명중 후 1.7초 폭발과 두 번의 촉수 타격을 연결합니다. 개별 촉수 타격 시각과 끌어오기 속도는 원문에 없어 명시한 경로·시간 시나리오를 사용합니다."]};
        if(w.Id==Proboscis&&b.AttackIndex==1)
        {
            effects["EffectiveRange"]=9;effects["CriticalTierUpgradeChance"]=0;effects["NonCriticalAttritionChance"]=0;
            var damage=c.RadialDistance<=9?r.Damage:new Dictionary<DamageType,double>();
            return r with{Damage=damage,CriticalChance=0,CriticalMultiplier=1,StatusChance=.43,ExpectedShot=damage.Values.Sum()*r.Multishot,BurstDps=null,SustainedDps=null};
        }
        if(w.Id!=Scyotid||b.AttackIndex!=1)return r;
        if((c.Roar!=0||c.Eclipse!=0)&&!c.AllowApproximations)throw new CombatRuleUnavailableException("사이오티드 올가미는 모드 비적용이 명시되어 있지만 외부 피해 버프의 적용 여부는 원문에 없습니다. 외부 피해 버프가 없는 시나리오를 계산할 수 있습니다.");
        effects["SnareTargetLimit"]=8*r.Multishot;effects["CriticalTierUpgradeChance"]=0;effects["NonCriticalAttritionChance"]=0;effects["ExtraElectricBaseChance"]=0;effects["OverguardMultiplier"]=1;effects["EffectiveRange"]=15;
        effects["NativeMultishot"]=1;effects["ExtraMultishotDamage"]=0;effects["ExtraProjectileDamageRatio"]=1;
        var value=c.RadialDistance<=15?40:0;
        return r with{Damage=new(){[DamageType.Toxin]=value},ArsenalDamage=new(){[DamageType.Toxin]=40},CriticalChance=0,CriticalMultiplier=1,StatusChance=r.StatusChance,Multishot=1,ExpectedShot=value,BaseDamage=40,BaseFactor=1,FactionFactor=1,CoFactor=1,StatusBaseFactor=1,BurstDps=null,SustainedDps=null,Scope=[..r.Scope,"사이오티드 올가미: 4발 소비, 고정 Toxin 40, 치명타 없음, 반경 15m·약 6초 마비. 모드는 직접 피해·피해 원소에 적용하지 않고 멀티샷은 8명 제한에 곱합니다. 이동 경로·비행 시간·소수 대상 제한은 원문 미명시 항목입니다."]};
    }
    public static double[] TendrilTimes(RangedConditions c)
    {
        if(c.ProboscisTickOne is not {} first||c.ProboscisTickTwo is not {} second)throw new CombatRuleUnavailableException("촉수는 명중 후 폭발까지 두 번 타격하지만 시각은 위키에 없습니다. 1.7초 이내의 두 타격 시점을 지정하세요.");
        if(!double.IsFinite(first)||!double.IsFinite(second)||first<0||first>=second||second>=1.7)throw new ArgumentException("촉수 타격은 0 ≤ 첫 타격 < 두 번째 타격 < 1.7초여야 합니다.");return [first,second];
    }
    public static int TargetLimit(RangedResult r,RangedConditions c)
    {
        var limit=r.Modifiers!.GetValueOrDefault("SnareTargetLimit");if(limit>=c.GroupCount)return c.GroupCount;if(Math.Abs(limit-Math.Round(limit))<1e-9)return (int)Math.Round(limit);
        return c.SnareTargetRounding switch{"Down"=>(int)Math.Floor(limit),"Up"=>(int)Math.Ceiling(limit),_=>throw new CombatRuleUnavailableException("멀티샷으로 늘어난 올가미 대상 수의 소수 처리는 원문에 없습니다. 버림/올림 기준을 선택하세요.")};
    }
}
