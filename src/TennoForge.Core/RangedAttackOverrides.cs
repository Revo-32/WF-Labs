using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public record RangedAttackOverride(string WeaponId,int Attack,Dictionary<string,JsonElement> Values,WikiSource Source,string Reason);
public static class RangedAttackOverrides
{
    public static readonly RangedAttackOverride[] Rules=Load();
    private static RangedAttackOverride[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-attack-overrides.json")!;return JsonSerializer.Deserialize<RangedAttackOverride[]>(stream,Catalog.Json)!;}
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        var rule=Rules.SingleOrDefault(r=>r.WeaponId==weapon.Id&&r.Attack==index);if(rule is null)return attack;
        var node=JsonNode.Parse(attack.GetRawText())!;
        foreach(var (key,value) in rule.Values)node[key]=JsonNode.Parse(value.GetRawText());
        return JsonSerializer.SerializeToElement(node);
    }
}
