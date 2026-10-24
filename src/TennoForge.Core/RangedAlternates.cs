using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;
public record RangedAlternateRule(string WeaponId,int[] Primary,int[] Alternate,int Capacity,int Required,bool DamagePerCharge,bool NoAmmo,int LockTargets,double BlockSeconds,WikiSource Source);
public static class RangedAlternates
{
    public static readonly RangedAlternateRule[] Rules=Load();
    private static RangedAlternateRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-alternates.json")!;return JsonSerializer.Deserialize<RangedAlternateRule[]>(stream,Catalog.Json)!;}
    public static RangedAlternateRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static int Charges(RangedAlternateRule rule,double fraction)=>(int)Math.Floor(rule.Capacity*fraction+1e-9);
}
