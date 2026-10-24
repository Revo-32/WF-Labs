using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public record RangedReturningBladeRule(string WeaponId,int Limit,double CriticalChance,double ReloadBonus,double Duration,double EnsembleBonus,double EnsembleDuration,WikiSource Source);
public static class RangedReturningBlades
{
    public static readonly RangedReturningBladeRule[] Rules=Load();
    private static RangedReturningBladeRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-returning-blades.json")!;return JsonSerializer.Deserialize<RangedReturningBladeRule[]>(stream,Catalog.Json)!;}
    public static RangedReturningBladeRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement[] Append(ReferenceWeapon weapon,JsonElement[] attacks)
    {
        if(Get(weapon.Id) is not {} rule)return attacks;
        var returning=JsonNode.Parse(attacks[0].GetRawText())!;
        returning["AttackName"]="Returning Blades";returning["CritChance"]=rule.CriticalChance;returning["AttackIndex"]=2;
        return [..attacks,JsonSerializer.SerializeToElement(returning)];
    }
    public static RangedCollision[] Path(RangedConditions c)
    {
        if(c.ReturningBladeHits is not {} path)throw new CombatRuleUnavailableException("칸타레의 복귀 경로를 설정하세요. 자동 명중을 가정하지 않습니다. 경로가 비어 있으면 복귀 칼날은 모두 빗나갑니다.");
        if(path.Any(h=>!double.IsFinite(h.AfterImpact)||h.AfterImpact<0||h.AfterImpact>300||h.Target<0||h.Target>=c.GroupCount||h.Surface)||path.Select(h=>h.Target).Distinct().Count()!=path.Length)throw new ArgumentException("복귀 경로는 칼날 한 개당 적별 한 번의 명중 시점·대상 번호로 지정하세요.");
        return path;
    }
}
