using System.Reflection;
using System.Text;
using System.Text.Json;
using TennoForge.Core;

internal static class RelicMatchingEquivalenceChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=RelicCatalog.Bundled();
        var sampled=catalog.Items.Where((_,i)=>i%23==0).ToArray();
        var queries=sampled.SelectMany(item=>new[]{item.Name,item.EnglishName,item.Name.Replace("프라임",""),
            item.Name.Replace("설계도","설개도"),item.Name.Length>2?item.Name[..^2]:item.Name,
            item.EnglishName.ToLowerInvariant(),string.Join(" ",item.Name.ToCharArray())})
            .Concat(new[]{""," ","가","가라 프라임","스티아낙스 프라임 섀시 설계도","스티아낙스 프라임 설계도",
                "니카나 프라임 블래이드","라보스 뉴로옵틱스 설개도","뉴로옵틱스 설계도","전혀다른이름!","PRIME","BLUEPRINT","Vome","2 X 포 르마 설계도","가라 프라임 hi 뉴로옵틱스 설계도 ㅣ"})
            .Distinct(StringComparer.Ordinal).ToArray();
        check("성유물 매칭 최적화: 한글·영문·오탈자·누락·부분 검색 후보와 점수의 비트 단위 동등성",()=>
        {
            foreach(var query in queries)
            {
                var expected=ReferenceSearch(catalog,query);
                Equal(expected,catalog.Search(query),query);
            }
            foreach(int count in new[]{-1,0,1,3,20})Equal(ReferenceSearch(catalog,"프라임",count),catalog.Search("프라임",count),"검색 개수 "+count);
        });
        check("성유물 매칭 최적화: OCR Top8·동점 순서·자동 확정·좌표 동등성",()=>
        {
            var session=new RelicCatalog.MatchSession();
            var rankings=(Dictionary<string,RelicCandidate[]>)typeof(RelicCatalog.MatchSession).GetField("Rankings",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
            foreach(var query in queries)
            {
                RewardText[] lines=[new(query,.2,.4,.4,.03,80)];
                var actual=catalog.Recognize(lines,session);
                EqualRecognition(ReferenceRecognize(catalog,lines),actual,query);
                var normalized=RelicCatalog.NormalizeOcr(query);
                if(rankings.TryGetValue(normalized,out var cached))
                    Equal(catalog.Items.Select(item=>new RelicCandidate(item,ReferenceScore(normalized,item,false))).OrderByDescending(x=>x.Similarity).Take(8).ToArray(),cached,"OCR 전체 순위 "+query);
                EqualRecognition(actual,catalog.Recognize(lines,session),"세션 캐시 "+query);
            }
        });
        check("성유물 매칭 최적화: 여러 보상·줄바꿈·독립 확인 우선순위의 동등성",()=>
        {
            var styanax=catalog.Items.Single(x=>x.Slug=="styanax_prime_chassis_blueprint");
            var nikana=catalog.Items.Single(x=>x.Slug=="nikana_prime_blade");
            RewardText[][] fixtures=[
                [new("스티아낙스 프라임",.4,.4,.18,.02,98),new("설계도",.45,.43,.07,.02,98),new(styanax.Name,.4,.4,.22,.05,59)],
                [new("니카나 프라임",.1,.4,.16,.02,94),new("블레이드",.14,.43,.08,.02,94),new("읽지못한보상",.4,.4,.18,.03,10),new(nikana.Name,.7,.4,.2,.03,96)],
                Enumerable.Range(0,4).Select(i=>new RewardText(nikana.Name,.06+i*.24,.4,.18,.025,96)).ToArray(),
                [new("플레이어123 체력",.1,.1,.1,.03,90),new("Vo",.4,.4,.03,.03,90)],
                [new(styanax.Name,.4,.4,.22,.05,20)],
                [new("스티아낙스 프라임 섀시 설계도",.4,.4,.22,.05,95),new("스티아낙스 프라임 시스템 설계도",.4,.4,.22,.05,95)]
            ];
            foreach(var lines in fixtures)
            foreach(bool prioritize in new[]{false,true})
            {
                Func<RecognizedReward,bool>? priority=prioritize?r=>r.Candidates[0].Item.Id==styanax.Id:null;
                EqualRecognition(ReferenceRecognize(catalog,lines,priority),catalog.Recognize(lines,corroborated:priority),"복합 보상 "+prioritize);
            }
        });
        check("성유물 매칭 최적화: 카탈로그 교체·with 복제·배열 원소 변경·긴 이름 동등성",()=>
        {
            var items=new[]{new RelicItem("first","","가라 프라임 뉴로옵틱스 설계도","Gara Prime Neuroptics Blueprint",45),
                new RelicItem("second","","가라 프라임 뉴로옵틱스 설계도","Gara Prime Neuroptics Blueprint",45),
                new RelicItem("long","",new string('가',300),new string('a',300),15),
                new RelicItem("multiple","","가라 프라임 프라임 섀시 설계도","Gara Prime Prime Chassis Blueprint",45),
                new RelicItem("unicode","","가라 프라임 섀시 설계도","Café Ｐｒｉｍｅ 𐐀 Chassis",45),
                new RelicItem("blank","","","",0)};
            var mutable=new RelicCatalog(DateTimeOffset.UnixEpoch,items);
            var session=new RelicCatalog.MatchSession();
            string[] customQueries=["가라 프라임 뉴로옵틱스 설계도","가라 뉴로옵틱스 설개도","가라 프라임 프라임 섀시 설계도",new string('가',299)+"나",new string('a',299)+"b","Café Prime 𐐀 Chassis","가라 섀시 설계도"];
            void Verify(RelicCatalog value)
            {
                foreach(var query in customQueries)
                {
                    Equal(ReferenceSearch(value,query),value.Search(query),"교체 검색 "+query);
                    RewardText[] lines=[new(query,.2,.4,.4,.03,80)];
                    EqualRecognition(ReferenceRecognize(value,lines),value.Recognize(lines,session),"교체 인식 "+query);
                }
            }
            Verify(mutable);Verify(mutable with{Items=items.Reverse().ToArray()});Verify(mutable);
            Verify(mutable with{RetrievedUtc=DateTimeOffset.UnixEpoch.AddDays(1)});
            items[0]=items[0] with{Name="달라진 프라임 뉴로옵틱스 설계도",EnglishName="Changed Prime Neuroptics Blueprint"};
            Verify(mutable);
            Verify(mutable with{Items=[]});
        });
    }

    private static void Equal(RelicCandidate[] expected,RelicCandidate[] actual,string context)
    {
        if(expected.Length!=actual.Length)throw new Exception(context+" 후보 개수 불일치");
        for(int i=0;i<expected.Length;i++)
            if(expected[i].Item!=actual[i].Item||BitConverter.DoubleToInt64Bits(expected[i].Similarity)!=BitConverter.DoubleToInt64Bits(actual[i].Similarity))
                throw new Exception(context+$" 후보 {i}·점수 불일치");
    }
    private static void EqualRecognition(RecognizedReward[] expected,RecognizedReward[] actual,string context)
    {
        if(JsonSerializer.Serialize(expected,Catalog.Json)!=JsonSerializer.Serialize(actual,Catalog.Json))throw new Exception(context+" 인식·좌표·확정 결과 불일치");
    }
    // Frozen RC2 scoring and row-selection reference, intentionally unoptimized.
    // Production must never call it; it protects the exact numerical semantics.
    private static RelicCandidate[] ReferenceSearch(RelicCatalog catalog,string query,int count=8)
    {
        var q=RelicCatalog.Normalize(query);if(q.Length==0)return [];
        return catalog.Items.Select(item=>new RelicCandidate(item,ReferenceScore(q,item))).Where(x=>x.Similarity>=.42)
            .OrderByDescending(x=>x.Similarity).ThenBy(x=>x.Item.Name,StringComparer.Ordinal).Take(count).ToArray();
    }
    private static double ReferenceScore(string query,RelicItem item,bool partial=true)
    {
        double Best(string name)
        {
            name=RelicCatalog.Normalize(name);if(name==query)return 1;
            if(partial&&name.Contains(query,StringComparison.Ordinal))return .86+.1*query.Length/name.Length;
            var a=query.Normalize(NormalizationForm.FormD);var b=name.Normalize(NormalizationForm.FormD);
            var similarity=1-(double)ReferenceDistance(a,b)/Math.Max(a.Length,b.Length);
            if(!partial&&name.Contains("프라임")&&!query.Contains("프라임"))
            {
                var split=name.Split("프라임",StringSplitOptions.None);
                if(split.Length==2&&split[0].Length>=2&&split[1].Length>=3&&split[1]!="설계도"&&query.StartsWith(split[0],StringComparison.Ordinal))
                {
                    var tail=query[split[0].Length..].Normalize(NormalizationForm.FormD);var component=split[1].Normalize(NormalizationForm.FormD);
                    var match=1-(double)ReferenceDistance(tail,component)/Math.Max(tail.Length,component.Length);
                    if(match>=.85)similarity=Math.Max(similarity,.97*(.4+.6*match));
                }
            }
            return similarity;
        }
        return Math.Max(Best(item.Name),Best(item.EnglishName));
    }
    private static int ReferenceDistance(string a,string b)
    {
        var previous=Enumerable.Range(0,b.Length+1).ToArray();var next=new int[b.Length+1];
        for(int i=1;i<=a.Length;i++){next[0]=i;for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(next[j-1]+1,previous[j]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));(previous,next)=(next,previous);}
        return previous[b.Length];
    }
    private static RecognizedReward[] ReferenceRecognize(RelicCatalog catalog,IReadOnlyList<RewardText> lines,Func<RecognizedReward,bool>? corroborated=null)
    {
        var proposals=new List<RecognizedReward>();
        foreach(var line in lines)
        {
            Add(line);
            foreach(var below in lines.Where(x=>x.Y>line.Y+line.Height*.4&&x.Y<line.Y+line.Height+Math.Max(.025,line.Height*1.5)&&Math.Abs(x.X+x.Width/2-line.X-line.Width/2)<Math.Max(.035,line.Width*.35)))
                Add(new(line.Text+" "+below.Text,Math.Min(line.X,below.X),line.Y,Math.Max(line.X+line.Width,below.X+below.Width)-Math.Min(line.X,below.X),below.Y+below.Height-line.Y,Math.Min(line.Confidence,below.Confidence)));
        }
        RelicCandidate[] Rank(string normalized)=>catalog.Items.Select(item=>new RelicCandidate(item,ReferenceScore(normalized,item,false))).OrderByDescending(x=>x.Similarity).Take(8).ToArray();
        void Add(RewardText text)
        {
            var normalized=RelicCatalog.NormalizeOcr(text.Text);
            if(normalized.Length<2||(!text.Text.Any(c=>c is >= '\uac00' and <= '\ud7a3')&&normalized.Length<4))return;
            if(normalized is "프라임" or "설계도" or "PRIME" or "BLUEPRINT")return;
            var candidates=Rank(normalized);if(candidates.Length==0||candidates[0].Similarity<.68)return;
            var top=candidates[0];var exact=RelicCatalog.Normalize(top.Item.Name)==normalized||RelicCatalog.Normalize(top.Item.EnglishName)==normalized;
            var margin=top.Similarity-(candidates.Length>1?candidates[1].Similarity:0);
            proposals.Add(new(text.Text,text,candidates,exact&&text.Confidence>=65&&margin>=.015));
        }
        if(proposals.Count==0)return [];
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
        foreach(var line in lines.Where(x=>Math.Abs(x.Y-bestRow.Bounds.Y)<.035&&x.X>=.03&&x.X+x.Width<=.98&&RelicCatalog.Normalize(x.Text).Length>=4&&x.Text.Any(char.IsLetter)))
            if(chosen.Count<4&&!chosen.Any(x=>SameCard(x.Bounds,line)))chosen.Add(new(line.Text,line,Rank(RelicCatalog.NormalizeOcr(line.Text)).Where(x=>x.Similarity>=.42).ToArray(),false));
        return chosen.OrderBy(x=>x.Bounds.X).Take(4).ToArray();
    }
}
