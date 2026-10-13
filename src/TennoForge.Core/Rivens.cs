namespace TennoForge.Core;

public enum RivenStat { Damage, CriticalChance, CriticalDamage, AttackSpeed, Range, StatusChance, StatusDuration, ComboDuration, ComboChance, HeavyEfficiency, Heat, Cold, Electricity, Toxin, VsCorpus, VsGrineer, VsInfested, HeavyDamage, Windup, ParryAngle, SlamDamage, StatusDamage, Gas, Corrosive, Viral, Radiation, Blast, Magnetic, VsOrokin, VsTechrot, VsScaldra, FinisherDamage, InitialCombo, ImpactDamage, PunctureDamage, SlashDamage, SlideCriticalChance }
public record RivenTrait(RivenStat Stat, double Value, bool Negative = false, bool Spliced = false);
public record RivenRoll(string WeaponId, int Rank, RivenTrait[] Traits, string DispositionVersion, string RulesVersion = "44.1.0",int? BaseDrain=null,string? Polarity=null);
public record SpliceCandidate(int First, int Second, RivenStat Result, bool RandomIsNegative);
public record RollGrade(double Position, double PositionLow, double PositionHigh, bool MaxCompatible, bool MinCompatible);
public static class RivenEngine
{
    public const string Source = "https://www.warframe.com/en/patch-notes/pc/44-1-0";
    // Official compatibility table. Numeric inheritance is in RivenValues; replacement RNG remains unverified.
    public static readonly (RivenStat A, RivenStat B, RivenStat Result)[] Recipes = [
        (RivenStat.HeavyEfficiency,RivenStat.ComboChance,RivenStat.HeavyDamage),
        (RivenStat.HeavyEfficiency,RivenStat.ComboDuration,RivenStat.Windup),
        (RivenStat.AttackSpeed,RivenStat.Range,RivenStat.ParryAngle),
        (RivenStat.Damage,RivenStat.AttackSpeed,RivenStat.SlamDamage),
        (RivenStat.Damage,RivenStat.StatusChance,RivenStat.StatusDamage),
        (RivenStat.Toxin,RivenStat.Heat,RivenStat.Gas),
        (RivenStat.Toxin,RivenStat.Electricity,RivenStat.Corrosive),
        (RivenStat.Toxin,RivenStat.Cold,RivenStat.Viral),
        (RivenStat.Heat,RivenStat.Electricity,RivenStat.Radiation),
        (RivenStat.Heat,RivenStat.Cold,RivenStat.Blast),
        (RivenStat.Electricity,RivenStat.Cold,RivenStat.Magnetic),
        (RivenStat.VsCorpus,RivenStat.VsGrineer,RivenStat.VsOrokin),
        (RivenStat.VsCorpus,RivenStat.VsInfested,RivenStat.VsTechrot),
        (RivenStat.VsInfested,RivenStat.VsGrineer,RivenStat.VsScaldra)
    ];
    public static SpliceCandidate[] Preview(RivenRoll roll)
    {
        if (roll.RulesVersion != "44.1.0") throw new ArgumentException("지원하지 않는 접합 규칙 버전입니다.");
        if (roll.Rank < 0 || roll.Rank > 8) throw new ArgumentException("리벤 랭크는 0~8입니다.");
        var t = roll.Traits ?? throw new ArgumentException("리벤 옵션이 없습니다.");
        if (t.Any(x => !Enum.IsDefined(x.Stat) || !double.IsFinite(x.Value))) throw new ArgumentException("유효하지 않은 리벤 옵션입니다.");
        if (t.Count(x => !x.Negative) is < 2 or > 3 || t.Count(x => x.Negative) > 1) throw new ArgumentException("양옵 2~3개, 음옵 0~1개를 입력하세요.");
        if (t.Select(x => x.Stat).Distinct().Count() != t.Length) throw new ArgumentException("동일 옵션을 중복 입력할 수 없습니다.");
        if (t.Any(x => x.Spliced)) throw new ArgumentException("기존 접합 옵션 제거 후 재접합하는 흐름은 후속 구현 대상입니다.");
        var available = new List<SpliceCandidate>();
        for (var i = 0; i < t.Length; i++) for (var j = i + 1; j < t.Length; j++)
            foreach (var r in Recipes)
                if ((t[i].Stat == r.A && t[j].Stat == r.B) || (t[i].Stat == r.B && t[j].Stat == r.A))
                    available.Add(new(i, j, r.Result, t[i].Negative || t[j].Negative));
        return available.ToArray();
    }
    public static RollGrade Grade(double displayed, double min, double max, int decimals = 1)
    {
        if (!double.IsFinite(displayed) || !double.IsFinite(min) || !double.IsFinite(max) || max <= min || decimals < 0 || decimals > 6)
            throw new ArgumentException("유효한 표시값과 최솟값 < 최댓값 범위를 입력하세요.");
        var half = .5 * Math.Pow(10, -decimals);
        if (displayed + half < min || displayed - half > max) throw new ArgumentException("표시값이 기준 범위를 벗어납니다. 기질·옵션 구성·랭크·단위를 확인하세요.");
        double P(double value) => Math.Clamp((value - min) / (max - min), 0, 1);
        return new(P(displayed), P(displayed - half), P(displayed + half), displayed + half >= max, displayed - half <= min);
    }
    public static string Label(RivenStat s) => s switch
    {
        RivenStat.Damage => "근접 피해", RivenStat.CriticalChance => "치명타 확률", RivenStat.CriticalDamage => "치명타 피해",
        RivenStat.AttackSpeed => "공격 속도", RivenStat.Range => "사거리", RivenStat.StatusChance => "상태이상 확률", RivenStat.StatusDuration => "상태 지속 시간",
        RivenStat.ComboDuration => "콤보 지속 시간", RivenStat.ComboChance => "추가 콤보 확률", RivenStat.HeavyEfficiency => "강공격 효율",
        RivenStat.Heat => "화염", RivenStat.Cold => "냉기", RivenStat.Electricity => "전기", RivenStat.Toxin => "독성",
        RivenStat.VsCorpus => "코퍼스 피해", RivenStat.VsGrineer => "그리니어 피해", RivenStat.VsInfested => "인페스티드 피해",
        RivenStat.HeavyDamage => "강공격 피해", RivenStat.Windup => "강공격 준비 속도", RivenStat.ParryAngle => "방어 각도", RivenStat.SlamDamage => "슬램 피해",
        RivenStat.StatusDamage => "상태이상 피해", RivenStat.Gas => "가스 피해", RivenStat.Corrosive => "부식 피해", RivenStat.Viral => "바이러스 피해",
        RivenStat.Radiation => "방사능 피해", RivenStat.Blast => "폭발 피해", RivenStat.Magnetic => "자성 피해", RivenStat.VsOrokin => "오로킨 피해",
        RivenStat.VsTechrot => "테크병 피해", RivenStat.VsScaldra => "스칼드라 피해", RivenStat.FinisherDamage => "마무리 피해", RivenStat.InitialCombo => "초기 콤보", RivenStat.ImpactDamage => "충격 피해", RivenStat.PunctureDamage => "관통 피해", RivenStat.SlashDamage => "베기 피해", RivenStat.SlideCriticalChance => "슬라이드 치명타 확률", _ => s.ToString()
    };
}
