using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TennoForge.Core;

public sealed record VendorBonus(string WeaponId,string Element,double Percent,DateTimeOffset RevisionUtc,long Revision,string Source,bool ChangedThisRotation);
public sealed record VendorReport(string Shop,DateTimeOffset FetchedUtc,VendorBonus[] Bonuses,string? Notice);
public static class VendorReportParser
{
    private static readonly Regex Tables=new("<table\\b[^>]*>.*?</table>",RegexOptions.Singleline|RegexOptions.IgnoreCase|RegexOptions.NonBacktracking,TimeSpan.FromMilliseconds(100));
    private static readonly Regex Rows=new(@"<tr>\s*<td>(?<name>.*?)</td>\s*<td>\s*\{\{D\|(?<element>[A-Za-z]+)\}\}\s*</td>\s*<td>\s*\{\{ValenceBonusPercentageColor\|(?<number>[0-9.]+)\}\}\s*</td>\s*</tr>",RegexOptions.Singleline|RegexOptions.NonBacktracking,TimeSpan.FromMilliseconds(100));
    public static Dictionary<string,(string Element,double Percent)> Values(string text,ScheduleCatalog catalog,string shop,string batch)
    {
        var result=new Dictionary<string,(string,double)>();var duplicates=new HashSet<string>();
        if(text.Length>2*1024*1024)return result;
        foreach(Match table in Tables.Matches(text))
        {
            if(!table.Value.Contains("Bonus %",StringComparison.Ordinal))continue;
            if(shop=="coda"&&!table.Value.Contains("Weapon (Batch "+batch+")",StringComparison.Ordinal))continue;
            foreach(Match row in Rows.Matches(table.Value))
            {
                var name=WebUtility.HtmlDecode(row.Groups["name"].Value).Trim();if(name.StartsWith("{{Weapon|",StringComparison.Ordinal)&&name.EndsWith("}}",StringComparison.Ordinal))name=name[9..^2].Trim();
                var weapon=catalog.Offers.SingleOrDefault(w=>w.Shop==shop&&(shop!="coda"||w.Batch==batch)&&w.EnglishName==name);
                var element=row.Groups["element"].Value;
                if(weapon is null||element is not ("Impact" or "Heat" or "Cold" or "Electricity" or "Toxin" or "Magnetic" or "Radiation")||!double.TryParse(row.Groups["number"].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var percent)||percent is <25 or >60)continue;
                if(duplicates.Contains(weapon.Id))continue;if(result.ContainsKey(weapon.Id)){result.Remove(weapon.Id);duplicates.Add(weapon.Id);continue;}result[weapon.Id]=(element,percent);
            }
        }
        return result;
    }
    public static VendorReport Compare(ScheduleCatalog catalog,ShopWindow window,string text,DateTimeOffset revisionUtc,long revision,string? previous,DateTimeOffset now)
    {
        var current=Values(text,catalog,window.Shop,window.Batch);var before=previous is null?null:Values(previous,catalog,window.Shop,window.Batch);
        bool within=revisionUtc>=window.StartUtc&&revisionUtc<window.EndUtc&&revisionUtc<=now.AddMinutes(2);
        var source="https://wiki.warframe.com/w/"+(window.Shop=="tenet"?"Tenet_Weapons":"Coda_Weapons")+"?oldid="+revision;
        var bonuses=current.Select(k=>new VendorBonus(k.Key,k.Value.Element,k.Value.Percent,revisionUtc,revision,source,
            within&&before is not null&&(!before.TryGetValue(k.Key,out var old)||old!=k.Value))).ToArray();
        return new(window.Shop,now,bonuses,bonuses.Any(b=>b.ChangedThisRotation)?"위키 제보 · 현회차 중 값 변경 확인":"현회차 제보 미확인 · 이전 참고값은 상세에서 확인");
    }
    public static string ElementKo(string element)=>element switch{"Impact"=>"충격","Heat"=>"화염","Cold"=>"냉기","Electricity"=>"전기","Toxin"=>"독성","Magnetic"=>"자성","Radiation"=>"방사능",_=>"미확인"};
}
public sealed class VendorReportClient : IDisposable
{
    private readonly HttpClient http;
    public VendorReportClient(HttpMessageHandler? handler=null){http=handler is null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(8);http.DefaultRequestHeaders.UserAgent.ParseAdd("WF-Labs/0.10.0 (+https://github.com/Revo-32/WF-Labs)");}
    private async Task<(string Text,long Revision,DateTimeOffset Utc)> ReadAsync(string page,DateTimeOffset? before,CancellationToken token)
    {
        string query="https://wiki.warframe.com/api.php?action=query&format=json&formatversion=2&prop=revisions&rvprop=ids%7Ctimestamp%7Ccontent&rvslots=main&rvlimit=1&titles="+Uri.EscapeDataString(page);
        if(before is {} time)query+="&rvstart="+Uri.EscapeDataString(time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture));
        var raw=await http.GetByteArrayAsync(query,token);if(raw.Length>2*1024*1024)throw new InvalidDataException("상점 제보 응답이 너무 큽니다.");using var doc=JsonDocument.Parse(raw);
        var pages=doc.RootElement.GetProperty("query").GetProperty("pages");if(pages.GetArrayLength()!=1)throw new InvalidDataException("상점 문서가 유일하지 않습니다.");var revisions=pages[0].GetProperty("revisions");if(revisions.GetArrayLength()==0)throw new InvalidDataException("상점 문서 개정이 없습니다.");var r=revisions[0];
        return(r.GetProperty("slots").GetProperty("main").GetProperty("content").GetString()!,r.GetProperty("revid").GetInt64(),r.GetProperty("timestamp").GetDateTimeOffset());
    }
    public async Task<VendorReport> FetchAsync(ScheduleCatalog catalog,string shop,DateTimeOffset now,CancellationToken token)
    {
        try
        {
            var window=WorldScheduleEngine.ShopAt(catalog,shop,now);string page=shop=="tenet"?"Tenet Weapons":"Coda Weapons";
            var latest=await ReadAsync(page,null,token);string? previous=null;
            if(latest.Utc>=window.StartUtc&&latest.Utc<window.EndUtc)
            {try{previous=(await ReadAsync(page,window.StartUtc.AddSeconds(-1),token)).Text;}catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidDataException){} }
            return VendorReportParser.Compare(catalog,window,latest.Text,latest.Utc,latest.Revision,previous,now);
        }
        catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidDataException or InvalidOperationException)
        {return new(shop,now,[],"상점 제보를 읽지 못했습니다. 교체 시각·판매 회차 예상은 계속 표시합니다.");}
    }
    public void Dispose()=>http.Dispose();
}
