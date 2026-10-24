using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TennoForge.Core;

public record RangedDeploymentRule(string WeaponId,int Limit,int DirectAttack,int[] FireModes,WikiSource Source,bool AllowBatchOverflow=false);
public static class RangedDeployments
{
    public static readonly RangedDeploymentRule[] Rules=Load();
    private static RangedDeploymentRule[] Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-deployments.json")!;
        return JsonSerializer.Deserialize<RangedDeploymentRule[]>(stream,Catalog.Json)!;
    }
    public static RangedDeploymentRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        // Castanas' first infobox row is a mid-air EXPLOSION, not a contact damage packet.
        // Keep the raw module intact and retain its projectile speed for the throw trajectory.
        var rule=Get(weapon.Id);if(rule is null||rule.DirectAttack>=0||!rule.FireModes.Contains(index))return attack;
        var normalized=JsonNode.Parse(attack.GetRawText())!;normalized["ShotType"]="AoE";
        normalized["ShotSpeed"]=RangedEngine.N(weapon.Raw.GetProperty("Attacks")[0],"ShotSpeed");
        return JsonSerializer.SerializeToElement(normalized);
    }
}
