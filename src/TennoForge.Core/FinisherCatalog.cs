using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record FinisherRule(string WeaponClass,string Kind,double[] Hits,string Issue,string SourceText);
public record FinisherSource(WikiSource Source,FinisherRule[] Rules);
public static class FinisherCatalog
{
    private static readonly Lazy<FinisherSource> Data=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.finishers.json")!;
        return JsonSerializer.Deserialize<FinisherSource>(stream,Catalog.Json)!;
    });
    public static FinisherSource Source=>Data.Value;
    public static StanceCombo[] ForWeapon(Weapon weapon)
    {
        var specific=Source.Rules.Where(r=>r.WeaponClass==weapon.EnglishName).ToArray();
        var rules=specific.Length>0?specific:Source.Rules.Where(r=>r.WeaponClass==weapon.WeaponType).ToArray();
        return rules.Select(r=>new StanceCombo("Finisher "+r.Kind,Label(r.Kind),null,
            r.Hits.Select((m,i)=>new StanceHit(1,m,1,1,1,1,1,
                weapon.EnglishName=="Rumblejack"&&i==0?["Electricity"]:weapon.WeaponType=="Rapier"&&r.Kind is "Front" or "Back"?["Slash"]:[],"",
                ProcDamageMultiplier:weapon.WeaponType=="Rapier"&&r.Kind is "Front" or "Back"?.5:1)).ToArray(),
            string.IsNullOrEmpty(r.Issue)?[]:[r.Issue],ActionOverride:"Finisher",
            ResolutionNote:"위키 마무리 전용 표 · "+r.SourceText+" · 원소/물리 모드 보너스 50% · 마무리 피해로 전환 · 개별 타격·전체 동작 시간 미기재 · "+Source.Source.Url)).ToArray();
    }
    public static string Label(string kind)=>kind switch{"Front"=>"앞 마무리","Back"=>"뒤 마무리","Stealth"=>"은신 마무리","Universal"=>"특수 체형 마무리","Ground"=>"지상 마무리",_=>throw new ArgumentException("마무리 종류 오류")};
}
