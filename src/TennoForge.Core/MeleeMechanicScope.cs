namespace TennoForge.Core;

/// <summary>
/// Scope unresolved compound mechanics to the action that can trigger them.
/// A missing wave model must not invalidate an unrelated ordinary blade hit.
/// The catalog's pending entries are retained as a separate coverage ledger.
/// </summary>
public static class MeleeMechanicScope
{
    public const string DestrezaId="/Lotus/Weapons/Tenno/Melee/Swords/TnoRapier/TnoRapier";
    public const string DestrezaPrimeId="/Lotus/Weapons/Tenno/Melee/Swords/PRapier/DestrezaPrime";
    public static string[] AdditionalComponents(MeleeWeaponRule profile,MeleeSetup setup)
        =>PathocystMaggots.Applies(profile.Id)?[PathocystMaggots.Scope(profile.Id)]:profile.Id==JatKusarMechanic.WeaponId&&setup.Attack=="Heavy Attack"
            ?["미반영: 잿 쿠사르 칼날만의 수치입니다. 플레이어 중심218폭발은 슬램 피해만 적용한 별도 성분으로 표시합니다. 반경·치명/상태 확률은 미기재입니다. "+JatKusarMechanic.Source]
            :WarfanSlam.Applies(profile.Id)&&setup.Attack is "Slam Attack" or "Heavy Slam Attack"
            ?["현재 수치는 슬램1타 성분입니다. 워팬은2타이며 강슬램의 둘째 타격은 일반 슬램 수치입니다. 전체는 스탠스 슬램2타 또는 ‘고유 공격 · 접촉 경로’에서 확인하세요."]
            :IncarnonBladeFlight.Applies(profile.Id)&&setup.Incarnon
            ?["칼날과 유령 투사체를 구분합니다. 투사체 발사/명중은 ‘인카논 · 유령 칼날’에서 지정한 시나리오만 시간 결과에 합산합니다. 헤이트는 스탠스 배율·0.4초 지연, 액크 & 브런트는 공중2배·즉발이며 위키에 없는 발사 시점을 자동 생성하지 않습니다."]
            :VinquibusGunshot.Applies(profile.Id,setup.Attack)
            ?["사격 직격의 고정 수치입니다. 0.8초 지연 폭발은 시간별 교전 결과에 따로 포함하며 여기의 직격 DPS/TTK에는 합산하지 않습니다. 거리0 접촉 조건·반경4m·가장자리30% 감쇠. "+VinquibusGunshot.StanceSource]
            :profile.Id is DestrezaId or DestrezaPrimeId&&setup.Incarnon&&setup.Attack=="Heavy Attack"
            ?["데스트레자 변신 강공격: 유령 레이피어5개의 피해·강제 베기·준비 동작별 보너스는 성분 표에 표시. 기본 상확·절대 비행 속도 미기재로 실제 명중 순서와 전체 DPS는 확정 불가 · 검 타격과 구분"]:[];
    public static bool EffectCanTrigger(string key,string weaponId,MeleeSetup setup)=>key switch
    {
        "StaticDischarge" or "AmanataRandomProc"=>MeleeEngine.AttackPolicy(weaponId,setup.Attack).Heavy,
        "BounceExplosion"=>setup.Attack.Contains("Throw",StringComparison.Ordinal),
        "ExodiaContagion"=>(setup.Conditions?.GetValueOrDefault("Airborne")??0)>0,
        "Epidemic"=>setup.Attack.Contains("Slam",StringComparison.Ordinal),
        "ShardDetonation" or "ShardDamageMultiplier" or "ShardDuration" or "ShardImpale" or "ShardWeakspotCritical"=>setup.Incarnon,
        _=>true
    };
    public static bool CompoundApplies(MeleeWeaponRule profile,MeleeSetup setup,bool finisher=false,bool stanceSlam=false)
    {
        if(profile.PendingMechanics is not {Length:>0})return false;
        var attack=setup.Attack;
        var slam=attack.Contains("Slam",StringComparison.Ordinal)||stanceSlam;
        var heavy=MeleeEngine.AttackPolicy(profile.Id,attack).Heavy;
        var conditions=setup.Conditions??[];
        return profile.Name switch
        {
            "Azothane"=>stanceSlam,
            "Caustacyst" or "Coda Caustacyst"=>attack=="Heavy Attack"||attack.Contains("Corrosive",StringComparison.Ordinal),
            "Ether Daggers"=>false, // rank-30 stealth is resolved in the additive critical bracket
            "Orvius"=>attack.Contains("Throw",StringComparison.Ordinal)||attack.Contains("Hover",StringComparison.Ordinal),
            "Rumblejack"=>false, // contextual jab/finisher sequence from Finisher table
            "Secura Lecta"=>false, // finisher-only speed and global cap are resolved
            "Sampotes" or "Tenet Exec"=>slam,
            "Tak & Lug"=>heavy||attack.Contains("Throw",StringComparison.Ordinal),
            "Tatsu" or "Tatsu Prime"=>attack=="Slide Attack"||attack=="Soul Swarm Projectile",
            "Thalys"=>false, // per-host shard lifetime/growth and chain events in CombatTimeline
            "Vitrica"=>slam||attack.Contains("Glass",StringComparison.Ordinal),
            "Zenistar"=>heavy||attack.StartsWith("Disc ",StringComparison.Ordinal),
            "Spinnerex"=>false, // death event + center damage are separately resolved
            "Harmony" or "Sancti Magistar"=>false, // condensation, buff receipts, and recovery are separate timeline results
            "Rakta Dark Dagger"=>false, // shield state is resolved by the time engine
            // These include kill/utility effects. The time engine handles the
            // supported part separately; do not erase them from the ledger.
            _=>true
        };
    }

    public static string[] TimeIssues(MeleeWeaponRule profile,IEnumerable<MeleeSetup> actions,bool finisher=false,bool stanceSlam=false)
    {
        var selected=actions.ToArray();var extra=selected.Where(a=>!VinquibusGunshot.Applies(profile.Id,a.Attack)&&!IncarnonBladeFlight.Applies(profile.Id)&&!PathocystMaggots.Applies(profile.Id)).SelectMany(a=>AdditionalComponents(profile,a)).Distinct().ToArray();
        if(WarfanSlam.Applies(profile.Id)&&selected.Any(a=>a.Attack is "Slam Attack" or "Heavy Slam Attack"))return ["워팬 슬램2타는 각각의 명중 시각이 필요합니다. ‘고유 공격 · 접촉 경로’에서 슬램/강슬램과 두 접촉 시각을 지정하세요. "+WarfanSlam.Source(profile.Id)];
        if(WeaponStanceMechanic.Applies(profile.Id)&&(selected.Any(a=>a.Attack is not ("Normal Attack" or "Slide Attack" or "Heavy Attack" or "Slam Attack" or "Heavy Slam Attack"))||profile.Id==WeaponStanceMechanic.Korumm&&stanceSlam&&selected.Any(a=>a.Attack=="Normal Attack")))return ["무기 고유 공격의 칼날/발사/명중/폭발 시각은 ‘고유 공격 · 접촉 경로’에서 지정하세요. 별도 성분을 일반 근접 간격으로 무한 반복하지 않습니다. "+WeaponStanceMechanic.Source(profile.Id)];
        if(profile.Id==ShieldThrowMechanic.Sigma&&selected.Any(a=>a.Attack.StartsWith("Shield Throw")))return ["공중 방패의 반복 간격은 위키 미기재입니다. 방패 투척에서 발사·직격·수동 기폭·회수 시각을 지정하세요. "+ShieldThrowMechanic.SigmaSource];
        if(profile.Id==WolfSledgeFlight.WeaponId&&selected.Any(a=>a.Attack.Contains("Throw",StringComparison.Ordinal)))return ["울프 슬레지의 투척·전진 폭발·기폭·귀환 강타는 ‘투척 경로’에서 지정하세요. 투사체 속도·유도/귀환 시간을 일반 공격 간격으로 대체하지 않습니다. "+WolfSledgeFlight.Source];
        if(IncarnonBladeFlight.Applies(profile.Id)&&selected.Any(a=>a.Attack.StartsWith("Spectral ")))return ["유령 투사체를 일반 공격 간격으로 무한 반복하지 않습니다. ‘인카논 · 유령 칼날’에서 발사 동작·접촉 시각을 지정하세요."];
        if(OkinaDaggerState.Applies(profile.Id)&&selected.Any(a=>a.Attack.StartsWith("Spectral Dagger")))return ["유령 단검을 일반 공격 간격으로 무한 반복하지 않습니다. 일반 공격과 ‘유령 단검 비행’에서 실제 처치 생성·발사·명중 순서를 지정하세요. "+OkinaDaggerState.Source];
        if(CaustacystMechanic.Applies(profile.Id)&&selected.Any(a=>CompoundApplies(profile,a,finisher,stanceSlam)))
            return ["파동/웅덩이의 피해 성분·1초 간격 접촉은 구현되어 있습니다. 자동 전체 경로는 위키 폭·거리·수명 상충 및 실명 지속시간 미기재로 확정하지 않습니다. ‘웅덩이 체류’에서는 공통 수명 구간과 직접 지정한 실명 시나리오를 계산합니다. "+(profile.Id==CaustacystMechanic.CodaId?CaustacystMechanic.CodaSource:CaustacystMechanic.Source)];
        if(TatsuSoulState.Applies(profile.Id)&&selected.Any(a=>a.Attack is "Slide Attack" or "Soul Swarm Projectile"))return ["소울 스웜 자동 발사/명중: 슬라이드 입력과 검 접촉 사이 시간·유도 대상 배정·곡선 비행 시간은 위키 미기재입니다. 전투 중 행동의 발사/명중 시각·대상 입력 경로는 계산합니다."];
        if(profile.PendingMechanics is not {Length:>0})return extra;
        // On this single target, Harmony's after-kill buff cannot affect a later
        // attack because the encounter ends on death. Healing is a separate result.
        if(profile.Name is "Harmony" or "Rakta Dark Dagger" or "Sancti Magistar" or "Spinnerex")return extra;
        return selected.Any(a=>CompoundApplies(profile,a,finisher,stanceSlam))?extra.Concat(profile.PendingMechanics).ToArray():extra;
    }
}
