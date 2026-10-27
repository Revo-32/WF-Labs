using System.Globalization;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace TennoForge.Core;

public sealed record RelicItem(string Id,string Slug,string Name,string EnglishName,int Ducats,bool Tradable=true,int? Charges=null,int? MarketRank=null)
{
    public string MarketUrl=>"https://warframe.market/items/"+Uri.EscapeDataString(Slug);
}
public sealed record RelicCandidate(RelicItem Item,double Similarity);
public sealed record RewardText(string Text,double X,double Y,double Width,double Height,double Confidence);
public sealed record RecognizedReward(string RawText,RewardText Bounds,RelicCandidate[] Candidates,bool Confident,string? Confirmation=null)
{
    public RelicItem? Item=>Confident?Candidates.FirstOrDefault()?.Item:null;
}
public sealed record RewardEvidence(string Family,RecognizedReward Reward);
public sealed record RelicCatalog(DateTimeOffset RetrievedUtc,RelicItem[] Items)
{
    private sealed class IndexedName
    {
        internal readonly string Normalized,Jamo;
        internal readonly string? PrimePrefix,PrimeComponent;
        internal readonly int PrimePrefixJamoLength;
        internal IndexedName(string name)
        {
            Normalized=Normalize(name);Jamo=Normalized.Normalize(NormalizationForm.FormD);
            var split=Normalized.Split("프라임",StringSplitOptions.None);
            if(split.Length==2&&split[0].Length>=2&&split[1].Length>=3&&split[1]!="설계도")
            {
                PrimePrefix=split[0];PrimeComponent=split[1].Normalize(NormalizationForm.FormD);
                PrimePrefixJamoLength=PrimePrefix.Normalize(NormalizationForm.FormD).Length;
            }
        }
    }
    private sealed record IndexedItem(RelicItem Item,IndexedName Korean,IndexedName English);
    private sealed class CatalogIndex
    {
        internal readonly IndexedItem[] Entries;
        internal CatalogIndex(RelicItem[] items)=>Entries=items.Select(item=>new IndexedItem(item,new(item.Name),new(item.EnglishName))).ToArray();
        internal bool Matches(RelicItem[] items)
        {
            if(items.Length!=Entries.Length)return false;
            for(int i=0;i<items.Length;i++)if(!ReferenceEquals(items[i],Entries[i].Item))return false;
            return true;
        }
    }
    // Weak keys prevent replaced catalogs from retaining their normalized names.
    // The index is not a record field: a `with` clone must not copy stale entries.
    private static readonly ConditionalWeakTable<RelicCatalog,CatalogIndex> Indexes=new();
    private CatalogIndex Index()
    {
        var index=Indexes.GetValue(this,static catalog=>new(catalog.Items));
        if(index.Matches(Items))return index;
        // Items is an exposed array. Also invalidate if an element is replaced,
        // even though normal catalog refreshes create a new catalog instance.
        lock(Indexes)
        {
            index=Indexes.GetValue(this,static catalog=>new(catalog.Items));
            if(!index.Matches(Items)){Indexes.Remove(this);index=new(Items);Indexes.Add(this,index);}
            return index;
        }
    }
    public static RelicCatalog Bundled()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.relic-market-items.json")!;
        using var doc=JsonDocument.Parse(stream);return Parse(doc.RootElement,doc.RootElement.GetProperty("retrievedUtc").GetDateTimeOffset());
    }
    public static RelicCatalog Parse(JsonElement root,DateTimeOffset fetched)
    {
        var items=new List<RelicItem>();
        foreach(var item in root.GetProperty("data").EnumerateArray())
        {
            var slug=item.GetProperty("slug").GetString()!;
            bool extra=slug is "lohk" or "xata" or "jahu" or "vome" or "ris" or "fass" or "netra" or "khra" or "ayatan_amber_star";
            var value=item.TryGetProperty("ducats",out var ducats)&&ducats.TryGetInt32(out var d)?d:0;
            if(!extra&&value<=0)continue;
            var tags=item.GetProperty("tags").EnumerateArray().Select(x=>x.GetString()).ToArray();
            if(!extra&&(!tags.Contains("prime")||tags.Contains("set")||!tags.Any(x=>x is "component" or "blueprint")))continue;
            var names=item.GetProperty("i18n");var en=names.GetProperty("en").GetProperty("name").GetString()!;
            var ko=names.TryGetProperty("ko",out var korean)?korean.GetProperty("name").GetString():null;
            if(string.IsNullOrWhiteSpace(ko))continue; // Never silently replace the Korean reward name with English.
            items.Add(new(item.GetProperty("id").GetString()!,slug,ko,en,value,Charges:item.TryGetProperty("maxCharges",out var charges)?charges.GetInt32():null,MarketRank:extra&&item.TryGetProperty("maxRank",out var rank)?rank.GetInt32():null));
        }
        if(items.Count<100)throw new InvalidDataException("마켓 품목 목록이 불완전합니다. 저장된 목록을 사용합니다.");
        items.Add(new("local:forma-blueprint","","포르마 설계도","Forma Blueprint",0,false));
        items.Add(new("local:kuva","","쿠바","Kuva",0,false));
        items.Add(new("local:riven-sliver","","리벤 조각","Riven Sliver",0,false));
        items.Add(new("local:exilus-weapon-adapter","","엑실러스 웨펀 어댑터 설계도","Exilus Weapon Adapter Blueprint",0,false));
        return new(fetched,items.DistinctBy(x=>x.Id).OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray());
    }
    public RelicCandidate[] Search(string query,int count=8)
    {
        var q=Normalize(query);if(q.Length==0)return [];
        var jamo=q.Normalize(NormalizationForm.FormD);
        return Index().Entries.Select(entry=>new RelicCandidate(entry.Item,Score(q,jamo,entry,true)))
            .Where(x=>x.Similarity>=.42).OrderByDescending(x=>x.Similarity).ThenBy(x=>x.Item.Name,StringComparer.Ordinal).Take(count).ToArray();
    }
    public static string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static string NormalizeOcr(string value)
    {
        // Korean catalog labels contain Hangul syllables. Latin fragments and isolated jamo
        // mixed into a Korean label are OCR artifacts, not part of these localized item names.
        // Keep the untouched raw text for inspection. English-only labels use the normal path.
        if(value.Count(c=>c is >= '\uac00' and <= '\ud7a3')>=2)
            {
                var cleaned=System.Text.RegularExpressions.Regex.Replace(value,@"[A-Za-z]+",m=>m.Length<=3?"":m.Value);
                return string.Concat(cleaned.Where(c=>c is >= '\uac00' and <= '\ud7a3'||c is >= 'a' and <= 'z'||c is >= 'A' and <= 'Z')).ToUpperInvariant();
            }
        return Normalize(value);
    }
    private static double Score(string query,string queryJamo,IndexedItem item,bool partial)
    {
        double Best(IndexedName name)
        {
            if(name.Normalized==query)return 1;
            if(partial&&name.Normalized.Contains(query,StringComparison.Ordinal))return .86+.1*query.Length/name.Normalized.Length;
            // Korean syllables decomposed into jamo give useful near-name candidates, never a probability.
            var similarity=1-(double)Distance(queryJamo,name.Jamo)/Math.Max(queryJamo.Length,name.Jamo.Length);
            if(!partial&&name.PrimePrefix is not null&&!query.Contains("프라임")&&query.StartsWith(name.PrimePrefix,StringComparison.Ordinal))
            {
                // A missing common Prime token is recoverable only when the distinct item prefix
                // is intact AND a specific component (not just generic Blueprint) is readable.
                var tail=queryJamo.AsSpan(name.PrimePrefixJamoLength);var component=name.PrimeComponent!;
                var match=1-(double)Distance(tail,component)/Math.Max(tail.Length,component.Length);
                if(match>=.85)similarity=Math.Max(similarity,.97*(.4+.6*match));
            }
            return similarity;
        }
        return Math.Max(Best(item.Korean),Best(item.English));
    }
    private static int Distance(ReadOnlySpan<char> a,ReadOnlySpan<char> b)
    {
        int columns=b.Length+1,size=columns*2;int[]? rented=null;
        Span<int> rows=size<=512?stackalloc int[size]:(rented=ArrayPool<int>.Shared.Rent(size));
        try
        {
            var previous=rows[..columns];var next=rows.Slice(columns,columns);
            for(int j=0;j<columns;j++)previous[j]=j;
            for(int i=1;i<=a.Length;i++)
            {
                next[0]=i;
                for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(next[j-1]+1,previous[j]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));
                var swap=previous;previous=next;next=swap;
            }
            return previous[b.Length];
        }
        finally{if(rented is not null)ArrayPool<int>.Shared.Return(rented);}
    }
    // One capture may produce hundreds of overlapping OCR strings. Cache complete rankings
    // within that capture only; candidate scores, ordering and confirmation rules are unchanged.
    public sealed class MatchSession
    {
        internal readonly Dictionary<string,RelicCandidate[]> Rankings=new(StringComparer.Ordinal);
        internal RelicCatalog? Owner;
        internal object? Index;
    }
    public RecognizedReward[] Recognize(IReadOnlyList<RewardText> lines,MatchSession? session=null,Func<RecognizedReward,bool>? corroborated=null)
    {
        var index=Index();
        if(session is not null&&(!ReferenceEquals(session.Owner,this)||!ReferenceEquals(session.Index,index))){session.Rankings.Clear();session.Owner=this;session.Index=index;}
        RelicCandidate[] Rank(string normalized)
        {
            if(session is not null&&session.Rankings.TryGetValue(normalized,out var cached))return cached;
            var jamo=normalized.Normalize(NormalizationForm.FormD);
            var ranked=index.Entries.Select(entry=>new RelicCandidate(entry.Item,Score(normalized,jamo,entry,false))).OrderByDescending(x=>x.Similarity).Take(8).ToArray();
            if(session is not null)session.Rankings[normalized]=ranked;return ranked;
        }
        var proposals=new List<RecognizedReward>();
        foreach(var line in lines)
        {
            Add(line);
            // Reward names can wrap. Join only vertically adjacent, horizontally aligned fragments.
            foreach(var below in lines.Where(x=>x.Y>line.Y+line.Height*.4&&x.Y<line.Y+line.Height+Math.Max(.025,line.Height*1.5)&&Math.Abs(x.X+x.Width/2-line.X-line.Width/2)<Math.Max(.035,line.Width*.35)))
                Add(new(line.Text+" "+below.Text,Math.Min(line.X,below.X),line.Y,Math.Max(line.X+line.Width,below.X+below.Width)-Math.Min(line.X,below.X),below.Y+below.Height-line.Y,Math.Min(line.Confidence,below.Confidence)));
        }
        void Add(RewardText text)
        {
            var normalized=NormalizeOcr(text.Text);
            if(normalized.Length<2||(!text.Text.Any(c=>c is >= '\uac00' and <= '\ud7a3')&&normalized.Length<4))return;
            if(normalized is "프라임" or "설계도" or "PRIME" or "BLUEPRINT")return;
            var candidates=Rank(normalized);if(candidates.Length==0||candidates[0].Similarity<.68)return;
            var top=candidates[0];var exact=Normalize(top.Item.Name)==normalized||Normalize(top.Item.EnglishName)==normalized;
            var margin=top.Similarity-(candidates.Length>1?candidates[1].Similarity:0);
            proposals.Add(new(text.Text,text,candidates,exact&&text.Confidence>=65&&margin>=.015));
        }
        if(proposals.Count==0)return [];
        // Rank non-overlapping labels, not the count of OCR fragments in a row. Different
        // preprocessing passes can report the same card with slightly different boxes.
        bool SameCard(RewardText a,RewardText b)
        {
            var overlap=Math.Min(a.X+a.Width,b.X+b.Width)-Math.Max(a.X,b.X);
            return overlap>Math.Min(a.Width,b.Width)*.55||Math.Abs(a.X+a.Width/2-b.X-b.Width/2)<.055;
        }
        List<RecognizedReward> Row(RecognizedReward anchor)
        {
            var result=new List<RecognizedReward>();
            foreach(var p in proposals.Where(x=>Math.Abs(x.Bounds.Y-anchor.Bounds.Y)<.065)
                .OrderByDescending(x=>x.Candidates[0].Similarity).ThenByDescending(x=>corroborated?.Invoke(x)??false).ThenByDescending(x=>x.Confident).ThenByDescending(x=>x.Bounds.Confidence))
                if(!result.Any(x=>SameCard(x.Bounds,p.Bounds)))result.Add(p);
            return result.Take(4).ToList();
        }
        var chosen=proposals.DistinctBy(x=>Math.Round(x.Bounds.Y,2)).Select(Row).OrderByDescending(row=>row.Sum(x=>(x.Confident?2:0)+Math.Pow(x.Candidates[0].Similarity,4))).First();
        var bestRow=chosen.OrderByDescending(x=>x.Candidates[0].Similarity).First();
        // Keep unread names in the same row, but never let an icon displace a recognized card.
        foreach(var line in lines.Where(x=>Math.Abs(x.Y-bestRow.Bounds.Y)<.035&&x.X>=.03&&x.X+x.Width<=.98&&Normalize(x.Text).Length>=4&&x.Text.Any(char.IsLetter)))
            if(chosen.Count<4&&!chosen.Any(x=>SameCard(x.Bounds,line)))
                chosen.Add(new(line.Text,line,Rank(NormalizeOcr(line.Text)).Where(x=>x.Similarity>=.42).ToArray(),false));
        return chosen.OrderBy(x=>x.Bounds.X).Take(4).ToArray();
    }
    public static RecognizedReward[] ConfirmCorroborated(IEnumerable<RecognizedReward> rewards,IEnumerable<RewardEvidence> evidence)
    {
        var observations=evidence.ToArray();
        return rewards.Select(reward=>
        {
            if(reward.Confident||reward.Candidates.Length==0)return reward;
            var top=reward.Candidates[0];var margin=top.Similarity-(reward.Candidates.Length>1?reward.Candidates[1].Similarity:0);
            var strong=top.Similarity>=.9&&margin>=.05&&reward.Bounds.Confidence>=40;
            var moderate=top.Similarity>=.85&&margin>=.07&&reward.Bounds.Confidence>=60;
            var localized=Normalize(top.Item.Name);var query=NormalizeOcr(reward.RawText);
            var componentRecovery=!query.Contains("프라임")&&localized.Contains("프라임")&&
                localized.Split("프라임")[1]!="설계도"&&query.StartsWith(localized.Split("프라임")[0],StringComparison.Ordinal);
            var clear=(top.Similarity>=.95&&margin>=.12&&reward.Bounds.Confidence>=65)||
                (componentRecovery&&top.Similarity>=.93&&margin>=.18&&reward.Bounds.Confidence>=75)||
                (top.Similarity==1&&NormalizeOcr(reward.RawText).Length>=8&&reward.Bounds.Confidence>=45);
            if(!strong&&!moderate&&!clear)return reward;
            bool SamePosition(RewardText other)
            {
                double overlap=Math.Min(other.X+other.Width,reward.Bounds.X+reward.Bounds.Width)-Math.Max(other.X,reward.Bounds.X);
                return Math.Abs(other.Y-reward.Bounds.Y)<.065&&overlap>Math.Min(other.Width,reward.Bounds.Width)*.55;
            }
            var nearby=observations.Where(x=>SamePosition(x.Reward.Bounds)&&x.Reward.Candidates.Length>0).ToArray();
            var supporters=nearby.Where(x=>x.Reward.Candidates[0].Item.Id==top.Item.Id&&x.Reward.Candidates[0].Similarity>=.82&&x.Reward.Bounds.Confidence>=35)
                .Select(x=>x.Family).Distinct().Count();
            // Repeated overlapping crops of one preprocessing image are one vote, not independent proof.
            var conflict=nearby.Where(x=>x.Reward.Candidates[0].Item.Id!=top.Item.Id&&x.Reward.Candidates[0].Similarity>=.94)
                .GroupBy(x=>x.Reward.Candidates[0].Item.Id).Any(g=>g.Select(x=>x.Family).Distinct().Count()>=2);
            return (clear||supporters>=2)&&!conflict?reward with{Confident=true,Confirmation=clear?"한글 이름·부품 일치 및 경쟁 후보 구분 확인":$"서로 다른 문자 처리 {supporters}종에서 같은 품목 확인"}:reward;
        }).ToArray();
    }
}

public enum MarketPresence { InGame, Online }
public sealed record MarketQuote(string ItemId,DateTimeOffset FetchedUtc,MarketPresence Presence,bool Crossplay,
    decimal? SellMin,decimal? LowFiveMedian,decimal? BuyMax,int SellOrders,int BuyOrders,bool Cached=false,bool Stale=false,string? Error=null,bool Snapshot=false);
public static class RelicMarketMath
{
    public static MarketQuote Summarize(JsonElement root,string id,DateTimeOffset fetched,MarketPresence presence,bool crossplay,int? charges=null,int? rank=null)
    {
        var rows=new List<(string User,string Type,decimal Price)>();
        foreach(var order in root.GetProperty("data").EnumerateArray())
        {
            if(order.GetProperty("itemId").GetString()!=id||!order.TryGetProperty("visible",out var visible)||!visible.GetBoolean()||order.GetProperty("quantity").GetInt32()<1)continue;
            if(charges is not null&&(!order.TryGetProperty("charges",out var c)||c.ValueKind!=JsonValueKind.Number||!c.TryGetInt32(out var chargeValue)||chargeValue!=charges))continue;
            if(rank is not null&&(!order.TryGetProperty("rank",out var r)||r.ValueKind!=JsonValueKind.Number||!r.TryGetInt32(out var rankValue)||rankValue!=rank))continue;
            var user=order.GetProperty("user");var state=user.GetProperty("status").GetString();
            if(state!="ingame"&&(presence!=MarketPresence.Online||state!="online"))continue;
            if(user.GetProperty("platform").GetString()!="pc"&&(!crossplay||!user.TryGetProperty("crossplay",out var cp)||!cp.GetBoolean()))continue;
            var per=order.TryGetProperty("perTrade",out var perTrade)?perTrade.GetInt32():1;
            var platinum=order.GetProperty("platinum").GetDecimal();if(per<=0||platinum<=0)continue;
            var type=order.GetProperty("type").GetString();if(type is not ("sell" or "buy"))continue;
            rows.Add((user.GetProperty("id").GetString()!,type,platinum/per));
        }
        var sells=rows.Where(x=>x.Type=="sell").GroupBy(x=>x.User).Select(g=>g.Min(x=>x.Price)).Order().ToArray();
        var buys=rows.Where(x=>x.Type=="buy").GroupBy(x=>x.User).Select(g=>g.Max(x=>x.Price)).OrderDescending().ToArray();
        var low=sells.Take(5).ToArray();decimal? median=low.Length==0?null:low.Length%2==1?low[low.Length/2]:(low[low.Length/2-1]+low[low.Length/2])/2;
        return new(id,fetched,presence,crossplay,sells.Length==0?null:sells[0],median,buys.Length==0?null:buys[0],sells.Length,buys.Length);
    }
}
