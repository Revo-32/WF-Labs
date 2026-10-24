using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;

public record RangedAirburstRule(string WeaponId,int Attack,double Lifetime,bool Manual,WikiSource Source);
public static class RangedAirbursts
{
    public static readonly RangedAirburstRule[] Rules=Load();
    private static RangedAirburstRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-airbursts.json")!;return JsonSerializer.Deserialize<RangedAirburstRule[]>(stream,Catalog.Json)!;}
    public static RangedAirburstRule? Get(string id,int attack)=>Rules.SingleOrDefault(r=>r.WeaponId==id&&r.Attack==attack);
    public static double Fuse(RangedAirburstRule rule,RangedConditions c)
    {
        if(!rule.Manual)return rule.Lifetime;
        var requested=c.AirburstTime??rule.Lifetime;
        if(!double.IsFinite(requested)||requested<=0||requested>rule.Lifetime)throw new ArgumentException($"공중 기폭 시점은 0초보다 크고 {rule.Lifetime}초 이하여야 합니다.");
        return requested;
    }
}
