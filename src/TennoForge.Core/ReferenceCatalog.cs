using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record WikiSource(string Title,long Revision,string Timestamp,string Url,string Sha256);
public record ReferenceWeapon(string Id,string InternalId,string Name,string EnglishName,string Category,string WeaponType,string Source,bool Localized,bool Calculable,string[] Limitations,JsonElement Raw);
public record ReferenceMod(string Id,string Name,string EnglishName,string Category,string Source,string Description,string[][] RankText,bool Localized,JsonElement Raw);
public record ArcaneDefinition(string Id,string Name,string EnglishName,string Category,int MaxRank,string Description,string[][] RankText,string Source,bool Localized,string Rule,JsonElement Raw)
{
    public string SelectionLabel=>Name+(Rule=="Unsupported"?" · 효과 미지원":"");
    public string Describe(int rank)=>RankText.Length>rank&&rank>=0?string.Join("\n",RankText[rank].SelectMany(s=>s.Replace("\r","").Split('\n')).Distinct()):Description;
}
public record ReferenceCatalog(string Version,WikiSource[] Sources,ReferenceWeapon[] Weapons,ArcaneDefinition[] Arcanes,ReferenceMod[] Mods,JsonElement Audit)
{
    private static readonly Lazy<ReferenceCatalog> Instance=new(()=>{
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.reference-catalog.json")??throw new InvalidOperationException("전체 자료가 없습니다.");
        return JsonSerializer.Deserialize<ReferenceCatalog>(stream,Catalog.Json)??throw new InvalidOperationException("전체 자료 형식 오류");
    });
    public static ReferenceCatalog Load()=>Instance.Value;
}
