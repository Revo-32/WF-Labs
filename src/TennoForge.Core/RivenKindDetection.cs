using System.Text.RegularExpressions;

namespace TennoForge.Core;

/// <summary>Classify the card, never the page from which capture was opened.</summary>
public static class RivenKindDetection
{
    public static readonly string[] Kinds=["Melee","Rifle","Shotgun","Pistol"];
    private static string Normal(string value)=>Regex.Replace(value,@"[^가-힣A-Za-z0-9]","").ToLowerInvariant();
    private static readonly Lazy<(string Name,string Kind)[]> Names=new(()=>
        ReferenceCatalog.Load().Weapons.Where(w=>w.Category is "melee" or "primary" or "secondary")
            .SelectMany(w=>new[]{w.Name,w.EnglishName}.Select(name=>(Name:Normal(name),Kind:w.Category=="melee"?"Melee":RangedEngine.ModCategory(w))))
            .Where(w=>w.Name.Length>=2||Regex.IsMatch(w.Name,@"^[가-힣]$")).Distinct().OrderByDescending(w=>w.Name.Length).ToArray());

    public static string? WeaponKind(string text)
    {
        // Only a title prefix can identify a weapon. Do not search for short
        // weapon names inside arbitrary stat labels, user names or the footer.
        var lines=text.Split('\n');
        var headers=lines.SelectMany((line,i)=>Enumerable.Range(1,Math.Min(3,lines.Length-i)).Select(count=>string.Join(" ",lines.Skip(i).Take(count))))
            .Where(l=>!Regex.IsMatch(l,@"^\s*(?:[+−–－]?\s*\d|[xX×]\s*\d|MR\b)",RegexOptions.IgnoreCase)).ToArray();
        var matches=Names.Value.Where(w=>headers.Any(h=>w.Name.Length==1?Regex.IsMatch(h,@"^\s*"+Regex.Escape(w.Name)+@"(?=\s|[A-Za-z]|$)"):Normal(h).StartsWith(w.Name,StringComparison.Ordinal))).ToArray();
        if(matches.Length==0)return null;
        int longest=matches.Max(m=>m.Name.Length);
        var kinds=matches.Where(m=>m.Name.Length==longest).Select(m=>m.Kind).Distinct().ToArray();
        return kinds.Length==1?kinds[0]:null;
    }

    public static RivenScanReport Resolve(string text,int rank,string? title=null)
    {
        var named=WeaponKind(title??text);
        var reports=Kinds.Select(kind=>RivenScan.Parse(RivenScan.NormalizeOcrLabels(text,kind),kind,rank)).ToArray();
        var valid=reports.Where(r=>r.Valid).ToArray();
        var candidates=valid.Select(r=>r.Kind).ToArray();
        if(named is not null)
        {
            // An exact title and incompatible values signal a recognition error;
            // never discard the title to fit the numbers to another class.
            var report=reports.Single(r=>r.Kind==named);
            return report with{DetectedKind=true,KindCandidates=[named],KindBasis="무기 이름"};
        }
        if(valid.Length==1)return valid[0] with{DetectedKind=true,KindCandidates=candidates,KindBasis="옵션·수치 범위"};
        var best=reports.OrderByDescending(r=>r.Options.Length).ThenBy(r=>r.Issues.Length).First();
        string issue=valid.Length>1?"종류 확인 필요: "+string.Join(" · ",candidates.Select(RivenScan.KindName)):"종류를 확정할 수 없습니다. 옵션 인식 내용을 확인하세요.";
        return best with{DetectedKind=true,KindCandidates=candidates,KindBasis="확인 필요",Issues=best.Issues.Append(issue).ToArray(),Values=best.Values.Select(v=>v with{Grade=null,Error=issue}).ToArray(),Recipes=[]};
    }
}
