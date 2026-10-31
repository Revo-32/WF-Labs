using System.Globalization;
using System.Text.RegularExpressions;

namespace TennoForge.Core;

// OCR is an input adapter. All ranges and splice inheritance use the workshop engine.
public record RivenScanOption(string Key,string Name,double Value,bool Negative,string Unit,int Decimals=1);
public record RivenScanValue(RivenScanOption Option,RivenRange Range,RollGrade? Grade,string? Error);
public record RivenScanRecipe(string Ingredients,string Result,double Minimum,double Maximum,int ReplacementOptions,bool Negative)
{
    public double MinimumQuality {get;init;}
    public double MaximumQuality {get;init;}
}
public record RivenScanReport(string Kind,int Rank,RivenScanOption[] Options,RivenScanValue[] Values,RivenScanRecipe[] Recipes,string[] Issues)
{
    // Literal OCR completeness is independent of historical disposition/rank
    // compatibility. It never authorizes grading or auto confirmation.
    public string[] TranscriptionIssues {get;init;}=[];
    public bool DetectedKind {get;init;}
    public string[] KindCandidates {get;init;}=[];
    public string KindBasis {get;init;}="직접 선택";
    public bool Valid=>Issues.Length==0&&Values.Length>=2&&Values.All(v=>v.Grade is not null);
}
public static class RivenScan
{
    public static RivenSpliceOpportunity[] Opportunities(RivenScanReport report)
    {
        if(!report.Valid||report.Options.Any(o=>report.Kind=="Melee"?RivenValues.IsSpliced(Enum.Parse<RivenStat>(o.Key)):RangedRivens.Attribute(o.Key).Spliced))return [];
        var keys=Keys(report.Kind).ToHashSet();
        var recipes=report.Kind=="Melee"?RivenEngine.Recipes.Select(r=>(A:r.A.ToString(),B:r.B.ToString(),Result:r.Result.ToString())):RangedRivens.Table.Recipes.Select(r=>(A:r.First,B:r.Second,Result:r.Result));
        var result=new List<RivenSpliceOpportunity>();
        foreach(var recipe in recipes)
        {
            if(!keys.Contains(recipe.A)||!keys.Contains(recipe.B)||!keys.Contains(recipe.Result))continue;
            var a=report.Values.SingleOrDefault(v=>v.Option.Key==recipe.A);var b=report.Values.SingleOrDefault(v=>v.Option.Key==recipe.B);
            if((a is null)==(b is null))continue;var held=a??b!;var missing=a is null?recipe.A:recipe.B;
            // Conditional path only: assume this ingredient's normalized roll is
            // retained. The absent ingredient may roll anywhere; never assign it a value.
            result.Add(new(held.Option.Key,held.Option.Name,held.Option.Negative,missing,Label(report.Kind,missing),recipe.Result,Label(report.Kind,recipe.Result),held.Grade!.PositionLow,1));
        }
        return result.ToArray();
    }
    private static readonly Regex Number=new(@"(?:(?<sign>[+−–－-])|(?<factor>[xX×]))\s*(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>%|ｍ|m(?![A-Za-z])|초(?![가-힣]))?",RegexOptions.Compiled);
    private static string Normalize(string text)=>Regex.Replace(text,@"[^가-힣A-Za-z0-9]","");
    // Lock/element icons can become a digit or punctuation before the Korean
    // label. Do not repair numbers or words; discard only that leading icon area.
    private static string OptionLabel(string text)=>Normalize(Regex.Replace(text,@"^[\d\s\p{P}\p{S}]*",""));
    public static string KindName(string kind)=>kind switch{"Melee"=>"근접","Rifle"=>"소총","Shotgun"=>"산탄총","Pistol"=>"권총",_=>throw new ArgumentException("리벤 종류를 선택하세요.")};
    public static string[] Keys(string kind)=>kind=="Melee"?Enum.GetNames<RivenStat>():RangedRivens.Table.Attributes.Where(a=>a.Bases.ContainsKey(kind)).Select(a=>a.Key).ToArray();
    public static string Label(string kind,string key)=>kind=="Melee"?RivenEngine.Label(Enum.Parse<RivenStat>(key)):RangedRivens.Attribute(key).Name;
    public static string Unit(string kind,string key)=>kind=="Melee"?RivenValues.Unit(Enum.Parse<RivenStat>(key)):RangedRivens.Attribute(key).Unit;
    public static string[] AcceptedLabels(string kind)=>Keys(kind).SelectMany(k=>Aliases(kind,k)).Distinct().ToArray();
    public static string WeaponId(string kind)=>kind=="Melee"?Catalog.Load().Weapons.First().Id:RangedEngine.Weapons.First(w=>w.Category is "primary" or "secondary"&&w.WeaponType!="Exalted Weapon"&&RangedEngine.N(w.Raw,"Disposition")>0&&RangedEngine.ModCategory(w)==kind).Id;
    private static IEnumerable<string> Aliases(string kind,string key)
    {
        yield return Label(kind,key);
        foreach(var s in key switch
        {
            "Damage"=>new[]{"피해","근접 피해"},"StatusChance"=>new[]{"상태 확률","상태 이상 확률"},
            "StatusDuration"=>new[]{"상태이상 지속 시간","상태 이상 지속 시간"},
            "StatusDamage"=>new[]{"상태 이상 피해"},
            "FinisherDamage"=>new[]{"마무리 일격 피해"},
            "SlamDamage"=>new[]{"지면 강타 피해"},
            "ComboDuration"=>new[]{"연속기 지속 시간"},"InitialCombo"=>new[]{"기본 연속기"},
            "AmmoMaximum"=>new[]{"탄약 최대량"},"ProjectileSpeed"=>new[]{"발사체 속도"},
            "PunchThrough"=>new[]{"두께 꿰뚫기"},"Zoom"=>new[]{"확대율 증가","확대율"},
            "VsGrineer" or "FactionGrineer"=>new[]{"그리니어에 대한 피해","그리니어 피해"},"VsCorpus" or "FactionCorpus"=>new[]{"코퍼스에 대한 피해","코퍼스 피해"},"VsInfested" or "FactionInfested"=>new[]{"인페스티드에 대한 피해","인페스티드 피해"},
            "VsOrokin" or "FactionOrokin"=>new[]{"오로킨에 대한 피해","오로킨 피해"},"VsTechrot" or "FactionTechrot"=>new[]{"테크병에 대한 피해","테크병 피해","테크롯에 대한 피해"},"VsScaldra" or "FactionScaldra"=>new[]{"스칼드라에 대한 피해","스칼드라 피해"},
            "ImpactDamage" or "Impact"=>new[]{"충격","충격 피해"},"PunctureDamage" or "Puncture"=>new[]{"관통","관통 피해"},"SlashDamage" or "Slash"=>new[]{"베기","베기 피해"},
            "Gas"=>new[]{"가스","가스 피해"},"Corrosive"=>new[]{"부식","부식 피해"},"Viral"=>new[]{"바이러스","바이러스 피해"},"Radiation"=>new[]{"방사능","방사능 피해"},"Blast"=>new[]{"폭발","폭발 피해"},"Magnetic"=>new[]{"자성","자성 피해"},
            "ComboChance"=>new[]{"추가 콤보 카운트 확률","추가 연속기 카운트 확률","연속기 카운트 확률","연속기 카운트 획득 확률"},"SlideCriticalChance"=>new[]{"슬라이딩 공격 시 치명타 확률","슬라이드 공격에 치명타 확률 추가","슬라이드 공격 시 치명타 확률"},
            "Recoil"=>new[]{"반동","무기 반동"},"FireRate"=>new[]{"연사력","발사 속도"},
            _=>Array.Empty<string>()
        })yield return s;
    }
    public static RivenScanReport Parse(string text,string kind,int rank)
    {
        _=KindName(kind);var options=new List<RivenScanOption>();var issues=new List<string>();
        foreach(var raw in LogicalLines(text,kind))
        {
            var line=raw.Trim();var match=Number.Match(line);
            if(!match.Success)
            {
                var normalized=Normalize(line);
                bool incompleteOption=Regex.IsMatch(line,@"\d.*(%|초|m)")||Keys(kind).Any(k=>Aliases(kind,k).Any(a=>normalized==Normalize(a)||Regex.IsMatch(line,@"\d")&&normalized.Contains(Normalize(a))));
                // OCR may render a Latin title suffix as digits or %. An exact
                // known weapon prefix identifies a header, not a missing stat.
                if(incompleteOption&&RivenKindDetection.WeaponKind(line) is null)issues.Add("부호·수치 또는 옵션 확인: "+line);
                continue;
            }
            var name=OptionLabel(Regex.Replace(line.Remove(match.Index,match.Length),@"\([^)]*\)",""));
            var keys=Keys(kind).Where(k=>Aliases(kind,k).Any(a=>Normalize(a)==name)).ToArray();
            if(keys.Length!=1)
            {
                if(match.Index>0&&RivenKindDetection.WeaponKind(line[..match.Index]) is not null)continue;
                issues.Add("옵션 이름 확인: "+line);continue;
            }
            var key=keys[0];var value=double.Parse(match.Groups["value"].Value.Replace(',','.'),CultureInfo.InvariantCulture);
            if(match.Groups["factor"].Success)
            {
                if(!IsFaction(key)||match.Groups["unit"].Length>0){issues.Add("배율 표기 확인: "+line);continue;}
                var numeric=match.Groups["value"].Value.Replace(',','.');int dot=numeric.IndexOf('.');int decimals=Math.Max(0,(dot<0?0:numeric.Length-dot-1)-2);
                options.Add(new(key,Label(kind,key),Math.Round(Math.Abs(value-1)*100,6),value<1,"%",decimals));continue;
            }
            var negative=match.Groups["sign"].Value!="+";if(key=="Recoil"&&!name.Contains("감소"))negative=!negative;
            var readUnit=match.Groups["unit"].Value.Replace('ｍ','m');var expected=Unit(kind,key);
            // Korean melee cards display e.g. "+2 사거리" without the metre suffix.
            // Accept only this unambiguous stat; a missing percent elsewhere is
            // still an OCR error, and an explicit wrong unit is never repaired.
            if(readUnit.Length==0&&(kind=="Melee"&&key=="Range"||key=="PunchThrough"))readUnit="m";
            if(readUnit.Length==0&&expected.Length>0||readUnit.Length>0&&readUnit!=expected){issues.Add("단위 확인: "+line);continue;}
            options.Add(new(key,Label(kind,key),value,negative,expected));
        }
        return Analyze(options.ToArray(),kind,rank,issues.ToArray());
    }
    private static bool IsFaction(string key)=>key.StartsWith("Vs",StringComparison.Ordinal)||key.StartsWith("Faction",StringComparison.Ordinal);
    public static string NormalizeOcrLabels(string text,string kind)
    {
        var aliases=Keys(kind).SelectMany(key=>Aliases(kind,key).Select(label=>(Key:key,Label:label,Normal:Normalize(label)))).ToArray();
        return string.Join("\n",LogicalLines(text,kind,normalizeAliasNoise:true).Select(line=>
        {
            var numbers=Number.Matches(line);if(numbers.Count!=1)return line;var number=numbers[0];
            var label=OptionLabel(Regex.Replace(line.Remove(number.Index,number.Length),@"\([^)]*\)",""));
            label=Regex.Replace(label,@"\d+$","");
            if(aliases.Any(a=>a.Normal==label))
            {var exact=aliases.First(a=>a.Normal==label);return number.Value+" "+exact.Label;}
            // A frame can add junk before the signed value or Latin marks after
            // a Korean label. Accept only a complete, exact known suffix label;
            // preserve every numeric character and its explicit unit unchanged.
            var after=OptionLabel(Regex.Replace(Regex.Replace(line[(number.Index+number.Length)..],@"\([^)]*\)",""),@"[A-Za-z\s\p{P}\p{S}]+$",""));
            var suffix=aliases.Where(a=>a.Normal==after).ToArray();
            if(suffix.Length>0&&suffix.Select(a=>a.Key).Distinct().Count()==1)return number.Value+" "+suffix[0].Label;
            // Only a unique long label one glyph away. Never infer a missing
            // number/sign/unit or fuzzy-match short damage-type names.
            if(label.Length<3)return line;
            var close=aliases.Where(a=>a.Normal.Length>=4&&OneEdit(label,a.Normal)).ToArray();
            return close.Select(a=>a.Key).Distinct().Count()==1?number.Value+" "+close[0].Label:line;
        }));
    }
    private static bool OneEdit(string a,string b)
    {
        if(Math.Abs(a.Length-b.Length)>1)return false;int i=0,j=0,edits=0;
        while(i<a.Length&&j<b.Length)
        {if(a[i]==b[j]){i++;j++;continue;}if(++edits>1)return false;if(a.Length>=b.Length)i++;if(b.Length>=a.Length)j++;}
        return edits+(i<a.Length?1:0)+(j<b.Length?1:0)==1;
    }
    // A displayed option may wrap before or after its signed value. Join only when
    // the result has exactly one number and a complete, known option label.
    public static string[] LogicalLines(string text,string kind,bool normalizeAliasNoise=false)
    {
        var lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).ToArray();var result=new List<string>();
        for(int i=0;i<lines.Length;i++)
        {
            var joined=lines[i];bool found=false;
            for(int count=1;count<=3&&i+count<=lines.Length;count++)
            {
                if(count>1)joined+=" "+lines[i+count-1];var matches=Number.Matches(joined);if(matches.Count>1)break;
                if(matches.Count!=1)continue;var match=matches[0];
                var label=OptionLabel(Regex.Replace(joined.Remove(match.Index,match.Length),@"\([^)]*\)",""));
                bool known=Keys(kind).Any(k=>Aliases(kind,k).Any(a=>Normalize(a)==label));
                if(!known&&normalizeAliasNoise&&label.Length>=3)
                {
                    var close=Keys(kind).Where(k=>Aliases(kind,k).Any(a=>Normalize(a).Length>=4&&OneEdit(label,Normalize(a)))).ToArray();
                    known=close.Length==1;
                }
                if(known)
                {result.Add(joined);i+=count-1;found=true;break;}
            }
            if(!found)result.Add(lines[i]);
        }
        return result.ToArray();
    }
    public static RivenRoll Melee(RivenScanReport report)=>new(WeaponId("Melee"),report.Rank,report.Options.Select(o=>new RivenTrait(Enum.Parse<RivenStat>(o.Key),o.Value,o.Negative,RivenValues.IsSpliced(Enum.Parse<RivenStat>(o.Key)))).ToArray(),RivenValues.Version);
    public static RangedRivenRoll Ranged(RivenScanReport report)=>new(WeaponId(report.Kind),report.Rank,report.Options.Select(o=>new RangedRivenTrait(o.Key,o.Value,o.Negative,RangedRivens.Attribute(o.Key).Spliced)).ToArray(),RangedRivens.Table.Version);
    public static RivenScanReport Analyze(RivenScanOption[] options,string kind,int rank,string[]? issues=null)
    {
        _=KindName(kind);var errors=new List<string>(issues??[]);var values=new List<RivenScanValue>();var recipes=new List<RivenScanRecipe>();
        var report=new RivenScanReport(kind,rank,options,[],[],[]){TranscriptionIssues=errors.ToArray()};
        if(errors.Count>0)
        {
            // A skipped option changes the positive/negative count and therefore
            // every range. Keep the recognized input, but never grade a partial
            // card as though its remaining options were the complete shape.
            errors.Add("옵션 구성 확인 전에는 수율을 계산하지 않습니다.");
            return report with{Issues=errors.Distinct().ToArray()};
        }
        try
        {
            if(kind=="Melee")RivenValues.Validate(Melee(report),false);else RangedRivens.Validate(Ranged(report),false,false);
            foreach(var o in options)
            {
                var range=kind=="Melee"?RivenValues.Range(Melee(report),new(Enum.Parse<RivenStat>(o.Key),o.Value,o.Negative)):RangedRivens.Range(Ranged(report),new(o.Key,o.Value,o.Negative));
                try{values.Add(new(o,range,RivenEngine.Grade(o.Value,range.Minimum,range.Maximum,o.Decimals),null));}
                catch(ArgumentException){values.Add(new(o,range,null,"범위 밖 · 부호·수치・종류・랭크 확인"));errors.Add(o.Name+": 범위 밖의 수치");}
            }
            if(errors.Count==0)
            {
                if(kind=="Melee")foreach(var p in RivenValues.Predict(Melee(report),options.Select(o=>o.Decimals).ToArray()))
                {
                    var c=p.Candidate;var keep=options.Where((_,i)=>i!=c.First&&i!=c.Second).Select(o=>Enum.Parse<RivenStat>(o.Key)).ToHashSet();
                    recipes.Add(new(options[c.First].Name+" + "+options[c.Second].Name,RivenEngine.Label(c.Result),p.Value.Low,p.Value.High,RivenWorkshop.Pool(c.RandomIsNegative).Count(s=>!keep.Contains(s)),c.RandomIsNegative){MinimumQuality=RivenRating.InheritedPosition(p.InheritedRoll.Low),MaximumQuality=RivenRating.InheritedPosition(p.InheritedRoll.High)});
                }
                else foreach(var p in RangedRivens.Predict(Ranged(report),options.Select(o=>o.Decimals).ToArray()))
                {
                    var keep=options.Where((_,i)=>i!=p.First&&i!=p.Second).Select(o=>o.Key).ToHashSet();
                    var count=RangedRivenWorkshop.Pool(Ranged(report),p.ReplacementNegative).Count(k=>!keep.Contains(k));
                    recipes.Add(new(options[p.First].Name+" + "+options[p.Second].Name,RangedRivens.Attribute(p.Result).Name,p.Value.Low,p.Value.High,count,p.ReplacementNegative){MinimumQuality=RivenRating.InheritedPosition(p.InheritedRoll.Low),MaximumQuality=RivenRating.InheritedPosition(p.InheritedRoll.High)});
                }
            }
        }
        catch(ArgumentException ex){errors.Add(ex.Message);}
        return report with{Values=values.ToArray(),Recipes=recipes.ToArray(),Issues=errors.Distinct().ToArray()};
    }
    public static string Editable(RivenScanReport report)=>string.Join("\n",report.Options.Select(o=>IsFaction(o.Key)&&o.Decimals==0?$"×{(1+(o.Negative?-o.Value:o.Value)/100).ToString("0.00",CultureInfo.InvariantCulture)} {o.Name}":$"{((o.Key=="Recoil"?!o.Negative:o.Negative)?"-":"+")}{o.Value.ToString("0.###",CultureInfo.InvariantCulture)}{o.Unit} {(o.Key=="Recoil"?"반동":o.Name)}"));
}
