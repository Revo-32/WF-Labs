namespace TennoForge.Core;

/// <summary>Every imported effect is either consumed by a calculator, shown as utility, or explicitly unresolved.</summary>
public static class MeleeEffectCoverage
{
    private static readonly HashSet<string> Calculated=new(("RisingStormCombo SlashStatusDamage Damage CriticalChance CriticalDamage AttackSpeed StatusChance BloodRush WeepingWounds ConditionOverload ImpactDamage PunctureDamage SlashDamage " +
        "Range ComboDuration ComboDurationPercent InitialCombo FinisherDamage FinisherSpeed HeavyEfficiency SlamDamage ComboChance Windup HeavyDamage StatusDamage StatusDuration " +
        "SlideCriticalChance HealingPerStatus HeavyLifeSteal EnergyChannel AerialRange Tennokai TennokaiStatus TennokaiCriticalDamage TennokaiChance TennokaiDamage TennokaiRange TennokaiWindow " +
        "StoredDamageFraction StatusVulnerability ThrowWeaponDamage AdaptiveElement Riven SpeedMultiplier FlatStatus FollowThrough CriticalLifeSteal BaseStatus SlashDuration SeparateHeat PassiveCriticalChance InfiniteCombo " +
        "BaseDamage BaseCritical BaseCriticalDamage FlatCritical FlatCriticalDamage PostStanceStatus ArcaneCriticalChance UniversalDamage Exposure Doughty Duplicate FinalDamage StatusFinalMultiplier " +
        "CoExcludedBase ImpactToPuncture ImpactToSlash StatusToBaseCritical SetBaseCriticalDamage IncarnonCriticalDamage SlideRange HeavySlamEfficiency ForcedSlash ForcedPunctureStacks LifeSteal " +
        "Influence InfluenceDuration InfluenceRadius Afflictions VortexChance VortexRadius ArmorPerKill ShieldRestoreOnKill CareenColdStacks FreezeRadius " +
        "ExodiaForceDamage ExodiaForceChance HuntChance HuntRadius EnergyOnHeavyKill KillExplosion KillHealthExplosion KillExplosionRadius GroundFinisherExplosion GroundFinisherHealthExplosion GroundFinisherExplosionRadius TauOnStatus").Split(' ',StringSplitOptions.RemoveEmptyEntries));
    private static readonly Dictionary<string,string> Utility=new()
    {
        ["ShardWeakspotCritical"]="가시 약점에 대한 외부 무기의 치명타 조건",
        ["BlockAngle"]="막기 각도 추가 (도)",["BlockAnglePercent"]="막기 각도 비율 보너스",["BlockingComboChance"]="막기 시 추가 콤보 확률",
        ["ParryChance"]="반격 확률",["MovementSpeed"]="이동 속도 보너스",["SprintSpeed"]="질주 속도 보너스",
        ["ParkourSpeed"]="파쿠르 속도 보너스",["DoubleJump"]="더블 점프 보너스",["ExtraJump"]="추가 점프 횟수",["JumpHeight"]="점프 높이 보너스",
        ["HolsterPause"]="수납 중 콤보 타이머 정지",["HeavyMovementSpeed"]="강공격 명중 후 이동 속도",["HeavyTeleportRange"]="강공격 순간이동 거리 (m)",
        ["ProjectileSpeed"]="투사체 속도 보너스",["PunchThrough"]="두께 관통 (m)",["Bounces"]="반사 횟수 변화",["DisablePunchThrough"]="투척 두께 관통 비활성",
        ["ExplosionRadius"]="폭발 반경 추가 (m)",["SlamRange"]="슬램 반경 추가 (m)",["SlamRadiusPercent"]="슬램 반경 보너스",
        ["SecondaryFireRate"]="보조무기 연사력",["ShotgunReload"]="샷건 재장전 속도",["GoreChance"]="절단 연출 확률",
        ["RedirectDamage"]="아군 피해 흡수 비율",["RedirectRadius"]="피해 흡수 반경 (m)",
        ["FinisherStunRadius"]="마무리 기절 반경 (m)",
        ["GroundFinisherKnockdownRadius"]="지상 마무리 넉다운 반경 (m)",["NeutralFinisherOpenChance"]="정지 콤보 마무리 개방 확률",
        ["KillReload"]="처치 시 재장전 보너스",["MovementPerCombo"]="콤보 배수당 이동 속도",["TennokaiEvery"]="텐노카이 확정 타격 주기",
        ["TransformCombo"]="인카논 변신 필요 콤보",["FuraxKillRadius"]="퓨랙스 처치 범위 (m)"
    };
    private static readonly Dictionary<string,string> Timed=new()
    {
        ["AmanataRandomProc"]="아마나타 프레셔 · 8×강공격 첫 명중 대상의 추가 상태",
        ["ShardCombo"]="탈리스 가시 피해 성분의 콤보 획득",
        ["ShardDuration"]="대상별 탈리스 가시 수명",["ShardDetonation"]="탈리스 강공격과 주변 숙주 연쇄 폭발",
        ["ShardImpale"]="탈리스 입력당 성장과 완전 성장 꿰뚫기",["ShardDamageMultiplier"]="탈리스 별도 가시 피해 배수",
        ["FinisherHealing"]="마무리 처치 후 체력 재생",["ToxinKillHealing"]="독성3중첩 대상 처치 후 체력 재생",
        ["BaseArmorStrip"]="섀터링 임팩트 · 충격 피해 명중마다 기본 방어력 감소",
        ["InvigoratedHealth"]="히루도 치명타 명중 시 기본 최대 체력 +5% · 15초·최대5중첩·전체 지속 갱신",
        ["AmmoCombo"]="탄약 습득 사건당 콤보",
        ["ExtraCombo"]="체력·실드가 손상되지 않은 적 명중 시 추가 콤보 (오버가드 피해 제외)",
        ["FinisherCombo"]="마무리 명중 시 추가 콤보",
        ["SlamCombo"]="슬램 범위 명중 시 추가 콤보 (추가 콤보 확률 적용)",
        ["SlideCombo"]="슬라이드 명중 시 추가 콤보",
        ["SlideDistanceCombo"]="무기를 든 채 연속 지상 슬라이드 10m마다 콤보",
        ["ComboOnCold"]="명중 전 냉기 상태인 대상의 추가 콤보",
        ["ComboOnSlash"]="명중 전 베기 상태인 대상의 추가 콤보",
        ["ComboOnToxin"]="명중 전 독성 상태인 대상의 추가 콤보",
        ["SlashComboChance"]="자신이 건 베기의 부여·피해 틱마다 콤보 획득 확률"
    };
    private static readonly Dictionary<string,string> Pending=new()
    {
        ["StaticDischarge"]="스태틱 디스차지: 두 시작 순서·0.2초 틱/단발 계수·고정4m는 성분 표에 표시. D의 모드 브라켓·치명/상확·애니메이션 피해 창은 원문 미기재"
    };
    private static readonly Dictionary<string,string> Components=new()
    {
        ["BounceExplosion"]="볼래틸 반사 폭발: 직접 투척 명중 시 랭크별 확률·대상별 방어층/상태·콤보 미획득/미소모. 지형/반사 비행 경로는 별도",
        ["Epidemic"]="에피데믹 파동/끝 폭발: 성분 표와 지정한 명중 시각·대상으로 계산. 비행 속도·정확한 범위 경계·끝 폭발 감쇠는 원문 미기재",
        ["ExodiaContagion"]="컨테이전 직격/부착/폭발: 성분 표와 투사체 접촉 경로로 계산. 자동 발사·비행 시각은 원문 미기재"
    };
    public static string[] Notes(IReadOnlyDictionary<string,double> effects)=>effects.Where(p=>p.Value!=0).Select(p=>
        Calculated.Contains(p.Key)||p.Key.StartsWith("Vs",StringComparison.Ordinal)||Enum.TryParse<DamageType>(p.Key,out _)?null:
        Components.TryGetValue(p.Key,out var component)?"별도 성분: "+component+". 일반 검의 고정 DPS와 구분합니다.":
        Timed.TryGetValue(p.Key,out var timed)?$"전투 사건: {timed} {p.Value:G6}. 시간 계산의 해당 전투 사건과 후속 상태에 반영합니다. 시작 상태의 고정 DPS에는 소급하지 않습니다.":
        Utility.TryGetValue(p.Key,out var label)?$"보조 수치: {label} {p.Value:G6}. 이동·발동 이벤트에 해당하며 현재 단일 대상 직접 DPS와는 별도입니다.":
        $"미반영: {Pending.GetValueOrDefault(p.Key,p.Key)} ({p.Value:G6}). 이 효과의 전투 이벤트는 현재 직접 DPS·TTK에 포함되지 않습니다.").Where(x=>x is not null).Cast<string>().ToArray();
}
