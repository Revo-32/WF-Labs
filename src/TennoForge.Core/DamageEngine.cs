namespace TennoForge.Core;

public static class DamageEngine
{
    public const string RulesVersion = "melee-direct-0.5.0";
    private static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max) throw new ArgumentException($"{name}: {min}~{max} 범위로 입력하세요.");
    }
    public static Calculation Calculate(Catalog catalog, Build build)
    {
        if(build.SchemaVersion==5)return MeleeEngine.Calculate(catalog,build);
        if(build.Melee is not null)throw new ArgumentException("확장 근접 조건은 버전 5 빌드에 저장해야 합니다.");
        if (build.Stance is not null) throw new ArgumentException("스탠스 빌드는 스탠스 계산 엔진에서 처리해야 합니다.");
        if (build.SchemaVersion is not (1 or 3 or 4)) throw new ArgumentException("지원하지 않는 빌드 저장 형식입니다.");
        if (build.SchemaVersion>=3&&build.Capacity is null) throw new ArgumentException("빌드에 수용량 설정이 없습니다.");
        if (build.SchemaVersion==1&&build.Capacity is not null) throw new ArgumentException("수용량 설정은 버전 3 빌드에 저장해야 합니다.");
        if (build.SchemaVersion>=3) CapacityEngine.Calculate(catalog,build);
        if(build.SchemaVersion<4&&build.Arcanes is not null)throw new ArgumentException("아케인은 버전 4 빌드에 저장해야 합니다.");
        if (build.DataVersion != catalog.Version) throw new ArgumentException("빌드와 현재 데이터 버전이 다릅니다. 원본 데이터를 확인한 뒤 변환해야 합니다.");
        if (build.Riven is not null) throw new ArgumentException("리벤의 모딩 반영은 아직 지원하지 않습니다. 리벤 연구실에서 접합 후보를 확인하세요.");
        var w = catalog.Weapons.SingleOrDefault(w => w.Id == build.WeaponId) ?? throw new ArgumentException("지원하지 않는 무기입니다.");
        if (build.Slots is null || build.Slots.Length != 8) throw new ArgumentException("모드 슬롯은 8개여야 합니다.");
        var s = build.Scenario ?? throw new ArgumentException("계산 조건이 없습니다.");
        var enemy = s.Enemy ?? new Target();
        var arcane=ArcaneEngine.Resolve(build.Arcanes);
        Range(s.Combo, 1, w.MaxCombo, "콤보 배수"); Range(s.ExistingStatusTypes, 0, 13, "기존 상태이상 종류");
        Range(s.MotionMultiplier, .1, 20, "타격 배율"); Range(s.BaseInterval, .1, 10, "기준 타격 간격");
        Range(enemy.Health, 1, 1e12, "체력"); Range(enemy.Armor, 0, 2700, "일반 적 유효 방어력");
        var totals = new Dictionary<Stat, double>();
        var elements = new List<(DamageType Type, double Amount)>();
        var families = new HashSet<string>();
        foreach (var slot in build.Slots)
        {
            if (slot is null) continue;
            var mod = catalog.Mods.SingleOrDefault(m => m.Id == slot.ModId) ?? throw new ArgumentException("지원하지 않는 모드입니다.");
            if(mod.CalculationIssues is {Length:>0})throw new ArgumentException(mod.Name+": "+string.Join(" ",mod.CalculationIssues));
            if (!families.Add(mod.Family)) throw new ArgumentException($"{mod.Name}: 동일 계열 모드는 중복 장착할 수 없습니다.");
            Range(slot.Rank, 0, mod.MaxRank, mod.Name + " 랭크");
            var scale = (slot.Rank + 1d) / (mod.MaxRank + 1d);
            foreach (var (stat, value) in mod.Effects) totals[stat] = totals.GetValueOrDefault(stat) + value * scale;
            if (mod.Element is { } element) elements.Add((element, mod.ElementBonus * scale));
        }
        double V(Stat stat) => totals.GetValueOrDefault(stat);
        var baseTotal = w.Damage.Values.Sum();
        var effectiveStatuses=Math.Max(s.ExistingStatusTypes,build.Arcanes?.Frozen==true?1:0);
        var damageBonus = 1 + V(Stat.Damage) + V(Stat.ConditionOverload) * effectiveStatuses + arcane.Damage;
        var basicElements=new[]{DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin};
        var damage = w.Damage.Where(p=>!basicElements.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value * damageBonus * s.MotionMultiplier);
        // Innate basic elements combine at the end; already-combined innate elements stay separate.
        foreach(var type in basicElements)if(w.Damage.GetValueOrDefault(type)>0)elements.Add((type,w.Damage[type]/baseTotal));
        // Duplicate elements join their first occurrence BEFORE pairing; slot order is meaningful.
        var ordered = elements.Select(e => e.Type).Distinct().ToArray();
        var amounts = elements.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        for (var i = 0; i < ordered.Length; i += 2)
        {
            var type = ordered[i]; var amount = amounts[type];
            if (i + 1 < ordered.Length) { amount += amounts[ordered[i + 1]]; type = Combine(type, ordered[i + 1]); }
            damage[type] = damage.GetValueOrDefault(type) + baseTotal * damageBonus * s.MotionMultiplier * amount;
        }
        foreach(var (type,stat) in new[]{(DamageType.Impact,Stat.ImpactDamage),(DamageType.Puncture,Stat.PunctureDamage),(DamageType.Slash,Stat.SlashDamage)})
            if(damage.ContainsKey(type))damage[type]*=1+V(stat);
        if(arcane.Corrosive>0)damage[DamageType.Corrosive]=damage.GetValueOrDefault(DamageType.Corrosive)+baseTotal*damageBonus*s.MotionMultiplier*arcane.Corrosive;
        var effectiveCombo=Math.Min(12,Math.Max(s.Combo,arcane.InitialCombo/20+1));
        var cc = w.CriticalChance * (1 + V(Stat.CriticalChance) + V(Stat.BloodRush) * (effectiveCombo - 1)) + arcane.FlatCriticalChance;
        var cd = w.CriticalMultiplier * (1 + V(Stat.CriticalDamage));
        var sc = w.StatusChance * (1 + V(Stat.StatusChance) + V(Stat.WeepingWounds) * (effectiveCombo - 1));
        cd+=ArcaneEngine.DoughtyBonus(damage,sc,arcane.DoughtyPerStep);
        if(build.Arcanes?.Frozen==true)cd+=1; // Fixed 10-stack Cold target: +1.0x after modded critical damage.
        if(arcane.FrozenMultiplier!=1)foreach(var key in damage.Keys.ToArray())damage[key]*=arcane.FrozenMultiplier;
        var speed = w.AttackSpeed * (1 + V(Stat.AttackSpeed)+arcane.Speed);
        var interval = s.BaseInterval / speed;
        var critExpected = ArcaneEngine.ExpectedMultiplier(cc,cd,arcane.DuplicateChance);
        var hit = damage.Values.Sum() * critExpected;
        var dr = EnemyArmorReduction(enemy.Armor);
        TraceLine[] trace = [
            new("기본 피해", "무기 물리·원소 피해 합계", baseTotal, catalog.Version),
            new("기본 피해 배수", "1 + 피해 모드 + CO × 고정 상태 종류 + 아케인 피해", damageBonus, "U30.5 + 근접 기본식; 고정 조건 모델"),
            new("치명타 확률", "기본 확률 × (1 + 치확 모드 + BR × (실효 콤보 − 1)) + 아케인 고정 치확", cc, "U30.5 + 근접 기본식; 고정 조건 모델"),
            new("치명타·추가 타격 기대 배수", "(1 + 치확 × (최종 치명 배수 − 1)) × (1 + 노란 치명타 확률 × 듀플리케이트 확률)", critExpected, "치명타 단계별 확률 가중합 + 독립 재추첨 추가 타격"),
            new("최종 치명타 배수", "기본 치피 × (1 + 치피 모드) + 도우티 + 동결 보너스", cd, ArcaneEngine.RulesVersion),
            new("상태이상 확률", "기본 확률 × (1 + 상태 모드 + WW × (실효 콤보 − 1))", sc, "데이터 카드 + 근접 기본식; 고정 조건 모델"),
            new("타격 간격(초)", "기준 간격 ÷ (무기 공속 × (1 + 공속 모드 + 아케인 공속))", interval, "사용자 지정 반복 타격; 스탠스 아님"),
            new("적 방어력 피해 감소", "0.9 × √(유효 방어력 / 2700)", dr, "U36 이후 일반 적 모델; 고정 조건 모델")
        ];
        string[] limits = [
            "직접 피해의 고정 조건 모델입니다. 아래 미지원 효과는 결과에 포함되지 않습니다.",
            "상태이상 피해·바이러스 증폭·방어력 감소·강제 발동은 미반영입니다. 상태이상 확률은 정보로만 표시합니다.",
            "콤보와 CO용 상태 종류는 입력값으로 고정합니다. 전투 중 증가·만료·선행 준비 시간은 미반영입니다.",
            "사거리·콤보 지속 시간·콤보 획득 확률·강공격 준비 속도는 고정 콤보의 명중한 일반 타격 피해를 변경하지 않습니다.",
            "타격 배율·간격은 사용자 가정입니다. 첫 타격도 1회 간격 후 발생하며 명중률은 100%입니다.",
            "상성 중립, 체력만 있는 일반 적 모델입니다. 실드·오버가드·보스·피해 감쇠·강공격·리벤·인카논 진화·무기 고유 발동은 미지원입니다.",
            "쿠바·테넷·코다의 추가 원소 보너스와 조우 부품 조립은 아직 설정할 수 없습니다. 해당 무기는 위키의 기본 수치만 사용합니다.",
            "아케인은 입력한 발동 상태·중첩·현재 실드를 고정합니다. 발동까지의 시간·유지율·전투 중 중첩 변화는 자동 시뮬레이션하지 않습니다.",
            "수용량 설정이 있는 빌드는 별도 수용량 표시에서 장착 가능 여부를 확인하세요. 용량 초과 빌드도 계획용 피해 계산을 허용하며 보유 여부는 검사하지 않습니다."
        ];
        trace=[..trace,new("BR·WW 실효 콤보","입력 콤보와 크레센도 초기 콤보 중 큰 값, BR·WW는 최대 12배",effectiveCombo,ArcaneEngine.RulesVersion),new("아케인 기본 피해 보너스","피해 모드와 가산",arcane.Damage,ArcaneEngine.RulesVersion),new("아케인 부식 보너스","기본 피해에 곱하는 부식 보너스 비율, 기존 원소 결합과 독립",arcane.Corrosive,ArcaneEngine.RulesVersion),new("듀플리케이트 발동 확률","노란 치명타에서만 랭크별 확률로 추가 타격",arcane.DuplicateChance,ArcaneEngine.RulesVersion)];
        limits=[..limits,..arcane.Notes,..(w.InnateNotes??[])];
        if(build.Arcanes?.Frozen==true)limits=[..limits,"대상 동결을 고정: 냉기 10중첩의 최종 치명타 배수 +1.0, CO 상태 수 최소 1종. 동결 만료는 계산하지 않습니다."];
        return new(damage, cc, cd, sc, speed, interval, hit, hit / interval, hit / interval * (1 - dr), dr, trace, limits,arcane.DuplicateChance);
    }
    public static double EnemyArmorReduction(double armor)
    {
        Range(armor, 0, 2700, "일반 적 유효 방어력");
        return .9 * Math.Sqrt(armor / 2700);
    }
    public static DamageType Combine(DamageType a, DamageType b) => (a, b) switch
    {
        (DamageType.Heat, DamageType.Cold) or (DamageType.Cold, DamageType.Heat) => DamageType.Blast,
        (DamageType.Heat, DamageType.Electricity) or (DamageType.Electricity, DamageType.Heat) => DamageType.Radiation,
        (DamageType.Heat, DamageType.Toxin) or (DamageType.Toxin, DamageType.Heat) => DamageType.Gas,
        (DamageType.Cold, DamageType.Electricity) or (DamageType.Electricity, DamageType.Cold) => DamageType.Magnetic,
        (DamageType.Cold, DamageType.Toxin) or (DamageType.Toxin, DamageType.Cold) => DamageType.Viral,
        (DamageType.Electricity, DamageType.Toxin) or (DamageType.Toxin, DamageType.Electricity) => DamageType.Corrosive,
        _ => throw new ArgumentException("잘못된 기본 속성 조합입니다.")
    };
    public static TtkResult Simulate(Calculation c, double health, int trials = 2048, uint seed = 20261010, double horizon = 120)
    {
        if(c.Melee is not null)return MeleeTtkEngine.Simulate(c,health,trials,seed,horizon);
        Range(health, 1, 1e12, "체력"); Range(trials, 1, 10000, "시행 횟수"); Range(horizon, .01, 300, "제한 시간");
        if (seed == 0) throw new ArgumentException("시드는 0보다 커야 합니다.");
        var state = seed;
        double Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / 4294967296d; }
        var floor = (int)Math.Floor(c.CriticalChance); var fraction = c.CriticalChance - floor;
        var baseHit = c.Melee?.TargetDamage.Values.Sum()??c.Damage.Values.Sum() * (1 - c.ArmorReduction);
        var times = new double[trials]; var maxHits = (int)Math.Floor(horizon / c.HitInterval); var killed = 0;
        for (var trial = 0; trial < trials; trial++)
        {
            var remaining = health; var time = double.PositiveInfinity;
            for (var hit = 1; hit <= maxHits; hit++)
            {
                var tier = floor + (Next() < fraction ? 1 : 0);
                remaining -= baseHit * (1 + tier * (c.CriticalMultiplier - 1));
                if(remaining>1e-9&&tier==1&&c.DuplicateChance>0&&Next()<c.DuplicateChance)
                {var extraTier=floor+(Next()<fraction?1:0);remaining-=baseHit*(1+extraTier*(c.CriticalMultiplier-1));}
                if (remaining <= 1e-9) { time = hit * c.HitInterval; killed++; break; }
            }
            times[trial] = time;
        }
        Array.Sort(times);
        double? Q(double p) { var v = times[(int)Math.Ceiling(p * trials) - 1]; return double.IsFinite(v) ? v : null; }
        // Censored trials stay in the distribution: never report a completed-only median.
        return new(Q(.1), Q(.5), Q(.9), killed / (double)trials, trials, seed, horizon);
    }
}
