using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record RangedAugmentField(string WeaponId,int Parent,int Child,string Kind,double BaseDamage,double StatusChance,double StatusDuration,WikiSource Source);
public static class RangedAugmentFields
{
    public static readonly RangedAugmentField[] Rules=Load();
    private static RangedAugmentField[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-augment-fields.json")!;return JsonSerializer.Deserialize<RangedAugmentField[]>(stream,Catalog.Json)!;}
    public static RangedAugmentField? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement[] Append(ReferenceWeapon weapon,JsonElement[] attacks)
    {
        if(Get(weapon.Id) is not {} rule)return attacks;
        var fire=JsonNode.Parse(attacks[rule.Parent].GetRawText())!;fire["AttackName"]="Nightwatch Napalm";fire["AttackIndex"]=rule.Child+1;
        fire["Damage"]=JsonSerializer.SerializeToNode(new Dictionary<string,double>{{"Heat",rule.BaseDamage}});fire["CritChance"]=0;fire["CritMultiplier"]=1;fire["StatusChance"]=rule.StatusChance;fire["Multishot"]=1;
        fire.AsObject().Remove("Falloff");fire.AsObject().Remove("ForcedProcs");
        return [..attacks,JsonSerializer.SerializeToElement(fire)];
    }
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult ordinary)
    {
        if(Get(w.Id) is not {} rule||b.AttackIndex!=rule.Child)return ordinary;
        var effects=new Dictionary<string,double>(ordinary.Modifiers!);var ticks=effects.GetValueOrDefault("NapalmTicks");
        if(ticks<=0)throw new ArgumentException("나이트워치 네이팜을 장착해야 화염 장판을 계산할 수 있습니다.");
        var baseDamage=rule.BaseDamage*(1+(b.Progenitor?.Fraction??0));var baseFactor=Math.Max(0,1+effects.GetValueOrDefault("Damage"));
        var radius=RangedEngine.N(RangedEngine.Attacks(w)[rule.Parent],"Range")!.Value*(1+effects.GetValueOrDefault("BlastRadius"))*effects.GetValueOrDefault("NapalmArea");
        var packet=new Dictionary<DamageType,double>{{DamageType.Heat,(b.Conditions?.RadialDistance??0)<=radius?baseDamage*baseFactor*ordinary.FactionFactor:0}};
        effects["EffectiveRange"]=radius;effects["HeatBaseDuration"]=rule.StatusDuration;effects["CriticalTierUpgradeChance"]=0;effects["NativeMultishot"]=1;effects["ExtraProjectileDamageRatio"]=1;effects["NonCriticalAttritionChance"]=0;effects["ExtraMultishotDamage"]=0;
        return ordinary with{Damage=packet,ArsenalDamage=new(){{DamageType.Heat,baseDamage*baseFactor}},BaseDamage=baseDamage,BaseFactor=baseFactor,StatusBaseFactor=baseFactor,CoFactor=1,CriticalChance=0,CriticalMultiplier=1,StatusChance=rule.StatusChance,Multishot=1,ExpectedShot=packet.Values.Sum(),BurstDps=null,SustainedDps=null,Modifiers=effects,Scope=["나이트워치 네이팜 1틱 · 기본 150 화염. 기본 피해·팩션·쿠바 보너스만 직접 피해에 적용합니다.","상태 확률 68% 고정 · 화염 상태 기본 3초. 원소 모드는 직접 틱이 아닌 화염 상태 피해에 적용합니다.","설치 장판은 발사 멀티샷만큼 생성되며 폭발 중심에 고정됩니다. 프라이머리 컴프레션은 장판 피해·범위에 적용하지 않습니다."]};
    }
}
