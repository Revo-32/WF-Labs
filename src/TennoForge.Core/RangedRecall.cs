using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;

public record RangedRecallRule(string WeaponId,int Contact,int Explosion,int Limit,WikiSource Source);
public static class RangedRecall
{
    public static readonly RangedRecallRule[] Rules=Load();
    private static RangedRecallRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-recall.json")!;return JsonSerializer.Deserialize<RangedRecallRule[]>(stream,Catalog.Json)!;}
    public static RangedRecallRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
}
