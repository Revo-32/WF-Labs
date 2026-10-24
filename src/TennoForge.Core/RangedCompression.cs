using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;
public record RangedCompressionRule(string WeaponId,int AttackIndex,double Radius,string Mode,bool Shrink,bool RangeMod,WikiSource Source);
public static class RangedCompression
{
    public static readonly RangedCompressionRule[] Rules=Load();
    private static RangedCompressionRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-compression.json")!;return JsonSerializer.Deserialize<RangedCompressionRule[]>(s,Catalog.Json)!;}
    public static void Apply(RangedBuild build,ReferenceWeapon weapon,Dictionary<string,double> effects)
    {
        if(build.Conditions?.Aiming==false)return;
        if(RangedAugmentFields.Get(weapon.Id) is {} field&&build.AttackIndex==field.Child)return;
        var component=weapon.Id==RangedKomorex.Id&&build.AttackIndex==2?0:build.AttackIndex;
        var rule=Rules.SingleOrDefault(r=>r.WeaponId==weapon.Id&&r.AttackIndex==component);
        if(rule is null)
        {
            if(RangedEngine.Attacks(weapon).Any(a=>RangedEngine.S(a,"ShotType")=="AoE"))throw new CombatRuleUnavailableException("컴프레션의 선택한 공격별 반경 예외 규칙 연결 필요");
            return;
        }
        if(rule.Mode=="None")return;
        var lost=rule.Radius*.8*(rule.RangeMod?1+effects.GetValueOrDefault("BlastRadius"):1);
        var key=rule.Mode=="Add"?"Damage":"CompressionMultiplier";
        effects[key]=effects.GetValueOrDefault(key)+lost*(.5+.1*build.ArcaneRank);
        effects["AmmoEfficiency"]=effects.GetValueOrDefault("AmmoEfficiency")+lost*(.03+.005*build.ArcaneRank);
        effects["CompressionRadius"]=rule.Shrink?.2:1;
    }
}
