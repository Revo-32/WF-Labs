using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;
public record RangedDisarmTarget(string EnemyId,bool? CanDisarm,WikiSource Source,bool VisualOnly=false);
public static class RangedDisarm
{
    public static readonly RangedDisarmTarget[] Targets=Load();
    private static RangedDisarmTarget[] Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-disarm.json")!;return JsonSerializer.Deserialize<RangedDisarmTarget[]>(stream,Catalog.Json)!;}
    public static bool Alternate(string id,int attack)=>id=="wiki:primary/Panthera"&&attack==1||id=="wiki:primary/Panthera Prime"&&attack==2;
    public static bool VisualOnly(string id)=>Targets.SingleOrDefault(t=>t.EnemyId==id)?.VisualOnly==true;
    public static bool? Target(string id)=>Targets.SingleOrDefault(t=>t.EnemyId==id)?.CanDisarm;
}
