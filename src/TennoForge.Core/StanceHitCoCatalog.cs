using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;
public record StanceHitCoRule(string StanceId,string Combo,double[] Factors,int SourceRow);
public record StanceHitCoCatalog(string Version,JsonElement Source,StanceHitCoRule[] Rules)
{
    private static readonly Lazy<StanceHitCoCatalog> Data=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.stance-hit-co.json")!;
        return JsonSerializer.Deserialize<StanceHitCoCatalog>(stream,Catalog.Json)!;
    });
    public static StanceHitCoCatalog Load()=>Data.Value;
    public StanceHitCoRule? Find(string stance,string combo)=>Rules.SingleOrDefault(r=>r.StanceId==stance&&r.Combo==combo);
}
