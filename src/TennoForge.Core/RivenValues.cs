namespace TennoForge.Core;

public record RivenRange(double Minimum,double Maximum,double Disposition,double BaseValue,double Weight);
public record SpliceValues(SpliceCandidate Candidate,EncounterRange Value,EncounterRange InheritedRoll,string Source);

/// <summary>Displayed magnitudes, not hidden rolls or drop percentiles. Pinned Wiki revision 2817521.</summary>
public static class RivenValues
{
    public const string Version="wiki-riven-2817521";
    public const string Source="https://wiki.warframe.com/w/Riven_Mods?oldid=2817521";
    public static readonly IReadOnlyDictionary<RivenStat,double> Bases=new Dictionary<RivenStat,double>
    {
        [RivenStat.Damage]=164.7,[RivenStat.CriticalChance]=180,[RivenStat.CriticalDamage]=90,
        [RivenStat.AttackSpeed]=54.9,[RivenStat.Range]=1.94,[RivenStat.StatusChance]=90,
        [RivenStat.StatusDuration]=99.99,[RivenStat.ComboDuration]=8.1,[RivenStat.ComboChance]=58.77,
        [RivenStat.HeavyEfficiency]=73.44,[RivenStat.Heat]=90,[RivenStat.Cold]=90,
        [RivenStat.Electricity]=90,[RivenStat.Toxin]=90,[RivenStat.VsCorpus]=45,[RivenStat.VsGrineer]=45,
        [RivenStat.VsInfested]=45,[RivenStat.HeavyDamage]=119.7,[RivenStat.Windup]=119.7,
        [RivenStat.ParryAngle]=81,[RivenStat.SlamDamage]=119.7,[RivenStat.StatusDamage]=90,
        [RivenStat.Gas]=90,[RivenStat.Corrosive]=90,[RivenStat.Viral]=90,[RivenStat.Radiation]=90,
        [RivenStat.Blast]=90,[RivenStat.Magnetic]=90,[RivenStat.VsOrokin]=45,
        [RivenStat.VsTechrot]=45,[RivenStat.VsScaldra]=45,[RivenStat.FinisherDamage]=119.7,
        [RivenStat.InitialCombo]=24.5,[RivenStat.ImpactDamage]=119.7,[RivenStat.PunctureDamage]=119.997,
        [RivenStat.SlashDamage]=119.7,[RivenStat.SlideCriticalChance]=120
    };
    public static bool IsSpliced(RivenStat stat)=>RivenEngine.Recipes.Any(r=>r.Result==stat);
    public static string Unit(RivenStat stat)=>stat is RivenStat.Range?"m":stat is RivenStat.ComboDuration?"초":stat is RivenStat.InitialCombo?"":"%";
    public static string ModId(string weaponId)=>MeleeRuleCatalog.Load().Mods.Single(m=>m.Name==(ZawCatalog.IsZaw(weaponId)?"Zaw Riven Mod":"Melee Riven Mod")).Id;
    public static double Disposition(string weaponId)=>MeleeRuleCatalog.Load().Weapon(weaponId).Raw.Number("Disposition");
    public static void Validate(RivenRoll roll,bool values=true)
    {
        if(roll is null||roll.Traits is null||roll.Traits.Any(t=>t is null)||string.IsNullOrWhiteSpace(roll.WeaponId))throw new ArgumentException("리벤 무기·옵션 데이터가 비어 있습니다.");
        if(roll.Rank is <0 or >8||roll.RulesVersion!="44.1.0")throw new ArgumentException("리벤 랭크 또는 규칙 버전 오류");
        if(roll.DispositionVersion!=Version)throw new ArgumentException("리벤 기질·수치 기준을 현재 위키 자료로 확인하세요.");
        var p=roll.Traits.Count(t=>!t.Negative);var n=roll.Traits.Count(t=>t.Negative);
        if(p is <2 or >3||n>1||roll.Traits.Select(t=>t.Stat).Distinct().Count()!=roll.Traits.Length||roll.Traits.Count(t=>t.Spliced)>1)throw new ArgumentException("리벤은 중복 없이 양옵 2~3개, 음옵 최대 1개, 접합 최대 1개입니다.");
        foreach(var t in roll.Traits)
        {
            if(!Bases.ContainsKey(t.Stat)||!double.IsFinite(t.Value)||t.Value<0)throw new ArgumentException("리벤 옵션과 표시 수치를 확인하세요. 수치는 양수 크기로 입력합니다.");
            if(IsSpliced(t.Stat)!=t.Spliced||t.Spliced&&t.Negative)throw new ArgumentException("접합 전용 옵션은 양옵이며 접합 표시가 필요합니다.");
            if(t.Negative&&t.Stat is RivenStat.Damage or RivenStat.Heat or RivenStat.Cold or RivenStat.Electricity or RivenStat.Toxin)throw new ArgumentException(RivenEngine.Label(t.Stat)+"은 음옵으로 생성되지 않습니다.");
            if(values){var r=Range(roll,t);RivenEngine.Grade(t.Value,r.Minimum,r.Maximum);}
        }
    }
    public static RivenRange Range(RivenRoll roll,RivenTrait trait)
    {
        var p=roll.Traits.Count(t=>!t.Negative);var n=roll.Traits.Count(t=>t.Negative);
        var weight=(p,n,trait.Negative) switch{(2,0,false)=>.99,(2,1,false)=>1.2375,(2,1,true)=>.495,(3,0,false)=>.75,(3,1,false)=>.9375,(3,1,true)=>.75,_=>throw new ArgumentException("리벤 옵션 구성 오류")};
        var disposition=Disposition(roll.WeaponId);
        if(disposition<=0)throw new ArgumentException("선택한 무기의 기질 자료가 없습니다.");
        var basis=trait.Stat==RivenStat.ComboChance&&trait.Negative?104.85:Bases[trait.Stat];
        var center=basis*weight*disposition*(roll.Rank+1)/9;
        return new(.9*center,1.1*center,disposition,basis,weight);
    }
    public static SpliceValues[] Predict(RivenRoll roll)
    {
        Validate(roll);
        if(roll.Traits.Any(t=>t.Spliced))return [];
        return RivenEngine.Preview(roll).Select(candidate=>
        {
            EncounterRange Hidden(int i)
            {var t=roll.Traits[i];var range=Range(roll,t);var center=range.Minimum/.9;return new(Math.Max(.9,(t.Value-.05)/center),Math.Min(1.1,(t.Value+.05)/center));}
            var a=Hidden(candidate.First);var b=Hidden(candidate.Second);
            var inherited=new EncounterRange(Math.Max(a.Low,b.Low),Math.Max(a.High,b.High));
            var result=new RivenTrait(candidate.Result,0,Spliced:true);var baseValue=Range(roll,result).Minimum/.9;
            return new SpliceValues(candidate,new(baseValue*inherited.Low,baseValue*inherited.High),inherited,Source);
        }).ToArray();
    }
}
