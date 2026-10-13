namespace TennoForge.Core;

public record StanceHitDamage(int Group, int Count, Dictionary<DamageType,double> Damage,Calculation? Calculation=null,Build? Input=null,StanceHit? SourceHit=null,double CoFactor=1,bool CoVerified=true);
public record StanceCalculation(Calculation Stats, Stance Stance, StanceCombo Combo, StanceHitDamage[] Hits,
    Dictionary<DamageType,double> ComboDamage, double? Duration, double ExpectedComboDamage,
    double? RawDps, double? TargetDps)
{
    // The raw Wiki combo may also contain radial slams which are excluded
    // from the directly struck target. Count the resolved contacts here.
    public double ExpectedHit => ExpectedComboDamage / Hits.Sum(h=>h.Count);
    public double TargetDamage=>Hits.Sum(h=>(h.Calculation is {} c?c.TargetDps*c.HitInterval:h.Damage.Values.Sum())*h.Count);
}
public record TimeBounds(double? Earliest, double? Latest);
public record StanceTtkResult(TimeBounds P10, TimeBounds Median, TimeBounds P90, double KillFraction, int Trials, double Horizon);

public static class StanceEngine
{
    public const string RulesVersion = "wiki-stance-direct-0.5.0";
    public static StanceCalculation Calculate(Catalog catalog, Build build, StanceCatalog source)
    {
        if(build.SchemaVersion==5)return MeleeStanceEngine.Calculate(catalog,build,source);
        if(build.SchemaVersion is not (2 or 3 or 4))throw new ArgumentException("스탠스 빌드 저장 형식 오류");
        if(build.SchemaVersion>=3&&build.Capacity is null)throw new ArgumentException("빌드에 수용량 설정이 없습니다.");
        if(build.SchemaVersion==2&&build.Capacity is not null)throw new ArgumentException("수용량 설정은 버전 3 빌드에 저장해야 합니다.");
        if(build.SchemaVersion>=3)CapacityEngine.Calculate(catalog,build);
        if(build.SchemaVersion<4&&build.Arcanes is not null)throw new ArgumentException("아케인은 버전 4 빌드에 저장해야 합니다.");
        var selection = build.Stance ?? throw new ArgumentException("스탠스를 선택하세요.");
        if (selection.Revision != source.Revision) throw new ArgumentException("저장한 스탠스와 위키 개정이 다릅니다.");
        var weapon = catalog.Weapons.SingleOrDefault(w => w.Id == build.WeaponId) ?? throw new ArgumentException("지원하지 않는 무기입니다.");
        if(selection.ComboKey.StartsWith("Finisher ",StringComparison.Ordinal))throw new ArgumentException("앞·뒤·은신 마무리는 버전5의 근접 계산 규칙이 필요합니다.");
        var stance = source.ForWeapon(weapon).SingleOrDefault(s => s.Id == selection.Id) ?? throw new ArgumentException("무기와 스탠스가 호환되지 않습니다.");
        var combo = stance.Combos.SingleOrDefault(c => c.Key == selection.ComboKey) ?? throw new ArgumentException("지원하지 않는 스탠스 동작입니다.");
        if(combo.AttackOverride is not null||combo.Hits.Any(h=>h.AttackOverride is not null))throw new ArgumentException("고유 타격 성분은 버전5 근접 계산 규칙이 필요합니다.");
        if (combo.Unsupported.Length > 0 || combo.Hits.Any(h => h.AttackType is not ("" or "Sweep" or "360" or "Thrust" or "Direct")))
            throw new ArgumentException(combo.InputLabel+": "+string.Join(" ",combo.Unsupported));
        if (combo.Duration is not > 0 || !double.IsFinite(combo.Duration.Value)) throw new ArgumentException("위키에 유효한 콤보 시간이 없습니다.");
        if (build.Scenario is null) throw new ArgumentException("계산 조건이 없습니다.");
        var stats = DamageEngine.Calculate(catalog, build with { Stance = null, Capacity = build.SchemaVersion>=3?build.Capacity:null, SchemaVersion = build.SchemaVersion>=3?build.SchemaVersion:1, Scenario = build.Scenario with { MotionMultiplier = 1, BaseInterval = 1 } });
        var hits = new List<StanceHitDamage>();
        var total = new Dictionary<DamageType,double>();
        foreach (var hit in combo.Hits)
        {
            if (hit.Count < 1 || !double.IsFinite(hit.Multiplier) || hit.Multiplier < 0 ||
                new[]{hit.Impact,hit.Puncture,hit.Slash}.Any(x => !double.IsFinite(x) || x <= 0))
                throw new ArgumentException("위키 타격 데이터가 유효하지 않습니다.");
            // A zero-damage entry denotes a recovery/stumble animation. Its time remains in Duration.
            if(hit.Multiplier==0)continue;
            // Stance physical bonuses multiply only their physical component, never elemental damage.
            var damage = stats.Damage.ToDictionary(p => p.Key, p => p.Value * hit.Multiplier *
                (p.Key switch { DamageType.Impact => hit.Impact, DamageType.Puncture => hit.Puncture, DamageType.Slash => hit.Slash, _ => 1 }));
            hits.Add(new(hit.Group,hit.Count,damage));
            foreach (var (type,value) in damage) total[type] = total.GetValueOrDefault(type) + value * hit.Count;
        }
        if (hits.Count == 0) throw new ArgumentException("콤보에 타격이 없습니다.");
        var duration = combo.Duration.Value / stats.AttackSpeed;
        var expected = total.Values.Sum() * ArcaneEngine.ExpectedMultiplier(stats);
        return new(stats,stance,combo,hits.ToArray(),total,duration,expected,expected/duration,expected/duration*(1-stats.ArmorReduction));
    }
    public static StanceTtkResult Simulate(StanceCalculation c, double health, int trials = 2048, uint seed = 20261010, double horizon = 120)
    {
        if(c.Stats.Melee is not null)return MeleeTtkEngine.Simulate(c,health,trials,seed,horizon);
        if(c.Duration is not {} comboDuration)throw new ArgumentException("위키에 동작 시간이 없어 처치 시간은 확정할 수 없습니다. 타격 피해는 계산됩니다.");
        if (!double.IsFinite(health) || health <= 0 || health > 1e12 || trials < 1 || trials > 10000 || seed == 0 || !double.IsFinite(horizon) || horizon <= 0 || horizon > 300)
            throw new ArgumentException("TTK 입력 범위가 올바르지 않습니다.");
        var cycles = (int)Math.Floor(horizon/comboDuration);
        var effectiveHorizon = cycles*comboDuration;
        var completed = new int[trials];var killed=0;var state=seed;
        double Next(){state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296d;}
        var tier=(int)Math.Floor(c.Stats.CriticalChance);var fraction=c.Stats.CriticalChance-tier;
        var baseHits=c.Hits.SelectMany(h=>Enumerable.Repeat(h.Damage.Values.Sum()*(1-c.Stats.ArmorReduction),h.Count)).ToArray();
        for(var trial=0;trial<trials;trial++)
        {
            var remaining=health;completed[trial]=int.MaxValue;
            for(var cycle=1;cycle<=cycles;cycle++)
            {
                foreach(var hit in baseHits)
                {
                    var criticalTier=tier+(Next()<fraction?1:0);
                    remaining-=hit*(1+criticalTier*(c.Stats.CriticalMultiplier-1));
                    if(remaining>1e-9&&criticalTier==1&&c.Stats.DuplicateChance>0&&Next()<c.Stats.DuplicateChance)
                    {var extraTier=tier+(Next()<fraction?1:0);remaining-=hit*(1+extraTier*(c.Stats.CriticalMultiplier-1));}
                    if(remaining<=1e-9)break;
                }
                if(remaining<=1e-9){completed[trial]=cycle;killed++;break;}
            }
        }
        Array.Sort(completed);
        TimeBounds Q(double p){var cycle=completed[(int)Math.Ceiling(p*trials)-1];return cycle==int.MaxValue?new(null,null):new((cycle-1)*comboDuration,cycle*comboDuration);}
        // No invented per-hit timestamps. Report the enclosing combo interval, including censored trials.
        return new(Q(.1),Q(.5),Q(.9),killed/(double)trials,trials,effectiveHorizon);
    }
}
