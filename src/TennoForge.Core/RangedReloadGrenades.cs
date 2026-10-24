using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public record RangedReloadGrenadeRule(string WeaponId,int Count,WikiSource Source);
public static class RangedReloadGrenades
{
    public static readonly RangedReloadGrenadeRule[] Rules=Load();
    private static RangedReloadGrenadeRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-reload-grenades.json")!;return JsonSerializer.Deserialize<RangedReloadGrenadeRule[]>(stream,Catalog.Json)!;}
    public static RangedReloadGrenadeRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        if(Get(weapon.Id) is null||index==0)return attack;
        var node=JsonNode.Parse(attack.GetRawText())!;node["Trigger"]="Semi-Auto";
        return JsonSerializer.SerializeToElement(node);
    }
    public static double Reload(double reloadBonus,bool empty)
    {
        var bonus=reloadBonus-(empty?.2:0);
        if(1+bonus<=0)throw new ArgumentException("카타볼리스트 재장전 속도 합계가 0 이하입니다.");
        return 1.7/(1+bonus);
    }
    public static double Phase(RangedConditions c)
    {
        if(c.ReloadGrenadePhase is not {} phase)throw new CombatRuleUnavailableException("재장전 중 유탄을 던지는 정확한 시점은 위키에 없습니다. 유탄 투척 위치를 재장전 시간의 비율로 설정하세요.");
        if(!double.IsFinite(phase)||phase<0||phase>1)throw new ArgumentException("유탄 투척 위치는 재장전 시간의 0~1 범위입니다.");
        return phase;
    }
}
