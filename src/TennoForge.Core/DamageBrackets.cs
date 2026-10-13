namespace TennoForge.Core;

/// <summary>
/// A damage label never determines a bracket. Attack policies and reviewed effect rules do.
/// All members are coefficients (0.6 = +60%) except the explicitly named multipliers.
/// Critical and element multipliers are evaluated after this base-damage stage.
/// </summary>
public record DamageBrackets(
    double BaseDamage,
    double AdditiveDamage,
    double HeavyAttackDamage,
    double AdditiveConditionOverload,
    double ConditionExcludedBase,
    double ConditionMultiplier,
    double MotionMultiplier,
    double ComboMultiplier,
    double SlamMultiplier,
    double TennokaiMultiplier,
    double FollowThroughMultiplier,
    double FactionMultiplier,
    double FinalMultiplier,
    bool ConditionUsesCombo=true,
    double RangeFalloff=1,
    double PostBaseDamage=0,
    double WeaponDamageMultiplier=1,
    bool ConditionUsesMotion=true,
    double EclipseMultiplier=1)
{
    public double AdditiveStage => Math.Max(0,BaseDamage*(1+AdditiveDamage+HeavyAttackDamage)*EclipseMultiplier
        +Math.Max(0,BaseDamage-ConditionExcludedBase)*AdditiveConditionOverload);
    public double BeforeFaction => Math.Max(0,
        (BaseDamage*(1+AdditiveDamage+HeavyAttackDamage)+PostBaseDamage)*EclipseMultiplier*ComboMultiplier*RangeFalloff*MotionMultiplier
        +Math.Max(0,BaseDamage-ConditionExcludedBase)*AdditiveConditionOverload*(ConditionUsesCombo?ComboMultiplier:1)*(ConditionUsesMotion?MotionMultiplier:1))
        *ConditionMultiplier*SlamMultiplier*TennokaiMultiplier*FollowThroughMultiplier*FinalMultiplier*WeaponDamageMultiplier;
    public double ModdedBaseDamage => BeforeFaction*FactionMultiplier;
    public TraceLine[] Trace(string source)=>[
        new("기본 피해 가산 브라켓","기본 피해 × (1 + 피해 버프 + 강공격 피해) × 이클립스 + CO 적용 기본 피해 × CO",AdditiveStage,source),
        new("이클립스", "가산 CO 제외 · 컨테이전/글레이브 폭발은 두 번 적용",EclipseMultiplier,WeaponBuffs.EclipseSource),
        new("CO 별도 곱연산","공격별 규칙에 따라 적용 · 일반 근접에서는 1",ConditionMultiplier,source),
        new("피해 모드 이후 추가량","어벤징 트루스 저장 피해 · 원소 적용 전",PostBaseDamage,source),
        new("투척 무기 피해 브라켓","파워 스로우와 해당 범용 무기 피해 가산",WeaponDamageMultiplier,source),
        new("강공격 콤보 브라켓","일반 공격에서는 1 · 강공격에 콤보 배수",ComboMultiplier,source),
        new("슬램 브라켓","1 + 슬램 피해 보너스",SlamMultiplier,source),
        new("텐노카이 피해 분류","피해 모드와 가산 · 별도 곱셈 없음",TennokaiMultiplier,source),
        new("팩션 브라켓","1 + 대상 팩션 피해 보너스 + 로어",FactionMultiplier,source),
        new("최종 피해 브라켓","케어린 등 별도 곱연산 효과",FinalMultiplier,source),
        new("상태 피해의 기반","모딩 기본 피해 · 물리/원소 모드 및 치명타 적용 전",ModdedBaseDamage,source)
    ];
}
