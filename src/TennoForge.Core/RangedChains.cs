using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;

public record RangedChainRule(string WeaponId,int Attack,int Count,double Radius,double Decay,bool Fan,double Chance,bool VoidPrevents,bool Ricochet,WikiSource Source,double ImpactRadius=0,int AutoBeams=1,bool ExcludeRagdoll=false);
public static class RangedChains
{
    public static readonly RangedChainRule[] Rules=Load();
    private static RangedChainRule[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-chains.json")!;return JsonSerializer.Deserialize<RangedChainRule[]>(stream,Catalog.Json)!;}
    public static RangedChainRule? Get(string weapon,int attack)=>Rules.SingleOrDefault(r=>r.WeaponId==weapon&&r.Attack==attack);
    // The encounter places equal enemies on a lateral line. Chain range is measured between enemies.
    public static IEnumerable<(int Target,double Scale)> Traverse(RangedChainRule rule,int origin,IReadOnlyList<bool> alive,double spacing,IReadOnlyList<double>? positions=null)
    {
        var seen=new HashSet<int>{origin};var current=origin;
        for(int hop=1;hop<=rule.Count;hop++)
        {
            var source=rule.Fan?origin:current;
            var candidates=Enumerable.Range(0,alive.Count).Where(i=>alive[i]&&!seen.Contains(i)&&(positions is null?Math.Abs(i-source)*spacing:Math.Abs(positions[i]-positions[source]))<=rule.Radius+1e-9).OrderBy(i=>positions is null?Math.Abs(i-source)*spacing:Math.Abs(positions[i]-positions[source])).ThenBy(i=>i).ToArray();
            if(candidates.Length==0)yield break;
            current=candidates[0];seen.Add(current);yield return(current,rule.Fan?rule.Decay:Math.Pow(rule.Decay,hop));
        }
    }
}
