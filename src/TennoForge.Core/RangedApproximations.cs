using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

/// <summary>Explicit, reproducible scenario defaults for facts absent/conflicting in pinned Wiki sources.
/// Never catches a failed calculation or replaces damage with zero. Explicit inputs always win.</summary>
public static class RangedApproximations
{
    public static RangedBuild Resolve(RangedBuild b)
    {
        var c=b.Conditions??new();
        if(!c.AllowApproximations||c.ApproximationResolved)return b;
        var w=RangedEngine.Resolve(b);var attacks=RangedEngine.Attacks(w);
        if(b.AttackIndex<0||b.AttackIndex>=attacks.Length)return b;
        var a=attacks[b.AttackIndex];var notes=new List<string>();
        void Note(string message)=>notes.Add("근사 · "+message);
        var effects=b.Slots.Append(b.Exilus).OfType<ModSlot>().SelectMany(s=>RangedEngine.ModCatalog.Mods.Where(m=>m.Id==s.ModId).SelectMany(m=>m.Effects.Keys)).ToHashSet();
        if(b.Distance>0&&RangedPentaAugments.Has(b,"TetherTargets")&&c.TetherFlightSeconds is null)
        {c=c with{TetherFlightSeconds=b.Distance/Math.Max(1,(RangedEngine.N(attacks[0],"ShotSpeed")??25)*.5)};Note("테더 유탄의 미기재 감속을 일반 탄속의 50%로 근사합니다. 착지·기폭 시각에 영향이 있습니다.");}
        if(w.Id==RangedPullWeapons.Scyotid)
        {
            if(b.Distance>0&&c.SnareFlightSeconds is null){c=c with{SnareFlightSeconds=b.Distance/25};Note("사이오티드 올가미의 미기재 탄속은 25m/s로 근사합니다.");}
            if(c.SnareTargetRounding=="Unspecified"){c=c with{SnareTargetRounding="Down"};Note("올가미 멀티샷의 소수 대상 제한은 버림으로 근사합니다.");}
            if(c.Roar!=0||c.Eclipse!=0)Note("올가미의 외부 피해 버프는 모드 비적용 규칙과 같게 제외하는 근사입니다. 로어·이클립스의 실제 적용 여부는 원문에 없습니다.");
        }
        if(w.Id=="wiki:primary/Paracyst"&&b.AttackIndex==1&&b.Distance>25){if(c.PullSeconds is null)c=c with{PullSeconds=1};Note("파라시스트 작살은 1초 또는 직접 지정한 시간에 사거리 절반인25m까지 등속으로 끌어오는 근사입니다. 정확한 속도·종료 거리는 원문에 없습니다. 후속 명중 거리·범위 피해에 영향이 있습니다.");}
        if(RangedVileDischarge.Applies(w)&&b.Distance>0&&c.DischargeFlightSeconds is null){c=c with{DischargeFlightSeconds=b.Distance/25};Note("바일 디스차지 주머니의 미기재 탄속은 25m/s로 근사합니다.");}
        if(effects.Contains("AdhesiveChance"))
        {
            if(c.AdhesiveNapalmFuse is null&&effects.Contains("PentaNapalm")){c=c with{AdhesiveNapalmFuse=4};Note("부착 네이팜은 상충하는 4초·약 5초 중 애드히시브 문서의 4초 신관을 사용합니다.");}
            if(c.AdhesiveFieldDelay is null&&w.Id is "wiki:primary/Torid" or "wiki:primary/Tenet Ferrox"){c=c with{AdhesiveFieldDelay=4};Note("부착 장판의 미기재 생성 지연은 애드히시브 일반 신관 4초로 근사합니다.");}
        }
        if(effects.Contains("HolsterReload")&&c.HolsterRounding=="Unspecified"){c=c with{HolsterRounding="Accumulate"};Note("수납 재장전의 소수 탄약은 다음 틱으로 누적하는 근사입니다.");}
        if(effects.Contains("BuckshotMagazineRestore"))
        {
            if(c.RefundRounding=="Unspecified"){c=c with{RefundRounding="Down"};Note("벅샷 탄약 회복의 소수 탄수는 버림으로 근사합니다.");}
            if(c.BuckshotRefillSource=="Unspecified"){c=c with{BuckshotRefillSource="Reserve"};Note("벅샷 탄창 회복은 예비 탄약에서 옮기는 근사입니다. 실제 탄약 생성 여부는 원문에 없습니다.");}
        }
        if(c.OrbShots?.Any(o=>o.HitscanContinues is null)==true){c=c with{OrbShots=c.OrbShots.Select(o=>o with{HitscanContinues=o.HitscanContinues??true}).ToArray()};Note("오브 회수 히트스캔은 오브 뒤로 계속 진행하는 근사입니다. 같은 발사의 적 명중에 영향이 있습니다.");}
        if(RangedInfestedOrbs.Applies(w)&&c.InfestedMergeOverflow=="Unspecified"){c=c with{InfestedMergeOverflow="Remainder"};Note("감염 구체 2+2 합체의 초과 1중첩은 별도 구체로 보존하는 근사입니다.");}
        if(w.Id==RangedKomorex.Id&&b.AttackIndex is 1 or 2)Note("코모렉스 최대 줌 폭발은 본문의 바이러스 106·반경3.5m를 사용합니다. 모듈 표의53·3.6m와 상충하여 근사로 구분합니다. 직격의 IPS +100%는 기본 피해와 별도이며 물리 모드에 가산합니다.");
        if(RangedQuanta.Applies(w))
        {
            if(c.CubeRoutes is null)Note("콴타의 미지정 멀티샷 큐브는 같은 직선 경로로 근사합니다. 상호 충돌에 따른 퍼짐은 재현하지 않으며 관통 후에도 경로를 유지합니다. 범위 명중·누적 피해에 영향이 있습니다.");
            if(c.CubeHitRadius is null||c.CubeBeamContinues is null){c=c with{CubeHitRadius=c.CubeHitRadius??.3,CubeBeamContinues=c.CubeBeamContinues??true};Note("큐브 충돌 반경은 0.3m·기폭 빔의 관통은 계속 진행으로 근사합니다.");}
            if(c.TargetBodyThickness is null)c=c with{TargetBodyThickness=.3};
        }
        if(RangedHema.Applies(w)&&c.HemaDrainPhase is null){c=c with{HemaDrainPhase=.5};Note("헤마 체력 소비는 재장전 50% 시점입니다. 시점 미기재로 회복·체력 부족 처리에 영향이 있습니다.");}
        if(RangedHema.Applies(w)&&c.HemaPartialRounding=="Unspecified"){c=c with{HemaPartialRounding="Down"};Note("헤마 부분 장전의 소수 탄수는 버림으로 근사합니다.");}
        if(RangedReloadGrenades.Get(w.Id) is not null&&(c.ReloadGrenadePhase is null||b.Distance>0&&c.GrenadeFlightSeconds is null))
        {c=c with{ReloadGrenadePhase=c.ReloadGrenadePhase??.5,GrenadeFlightSeconds=c.GrenadeFlightSeconds??b.Distance/25};Note("카타볼리스트 유탄은 재장전 50%에 투척하고 미기재 탄속은 팬타즈마 본탄의 25m/s를 참고합니다. 착탄·처치 시각에 영향이 있습니다.");}
        if(c.TargetsInLine&&c.GroupCount>1&&(c.TargetBodyThickness is null||c.PenetrationGapSeconds is null))
        {c=c with{TargetBodyThickness=c.TargetBodyThickness??Math.Min(.3,c.GroupSpacing),PenetrationGapSeconds=c.PenetrationGapSeconds??c.GroupSpacing/Math.Max(1,(RangedEngine.N(a,"ShotSpeed")??50)*.5)};Note("관통 대상의 유효 두께 0.3m·관통 후 탄속 50%를 기본 경로로 근사합니다. 실제 자세·감속량 미기재로 후속 명중·TTK에 영향이 있습니다.");}
        if(c.GroupCount>1&&c.PullMotion=="Unspecified"&&(w.Id==RangedPullWeapons.Proboscis||RangedPentaAugments.Has(b,"TetherTargets")||RangedSingularity.Equipped(b)))
        {c=c with{PullMotion="Linear",PullSeconds=c.PullSeconds??1};Note("끌어오기는 1초에 중심까지 등속 이동하는 근사입니다. 실제 가속·경로가 미기재로 범위 명중·상태에 영향이 있습니다.");}
        if(effects.Contains("ConditionOverload")&&c.PostureRecoverySeconds is null){c=c with{PostureRecoverySeconds=1};Note("강제 비틀거림·넘어짐 회복은 1초로 근사합니다. 적별 회복 시간 미기재로 조건부 피해의 유지에 영향이 있습니다.");}
        if(effects.Contains("OscillatingStatusChance")&&c.FluxOscillation is null){c=c with{FluxOscillation=.5};Note("플럭스 오버드라이브 변동항은 범위 중앙 50% 고정 시나리오입니다. 무작위 분포가 없어 실제 시간 평균이라고 보장하지 않습니다. 상태·CO·DPS에 영향이 있습니다.");}
        if(effects.Contains("OscillatingStatusChance")&&w.EnglishName=="Tenet Flux Rifle"&&c.TenetFluxDoubleBase is null){c=c with{TenetFluxDoubleBase=false};Note("테넷 플럭스의 상확 2배 설명은 변동항과 중복으로 해석하여 별도 기본 상확 2배를 추가하지 않습니다.");}
        if(effects.Contains("HigasaForcedStatus")&&c.HigasaForcedStatus is null){c=c with{HigasaForcedStatus=DamageType.Heat};Note("히가사 서레이션의 무작위 상태 풀·가중치 미기재: 고정 화염 결과 시나리오로 근사합니다. 실제 기대값이 아니며 상태·방어력·DPS에 영향이 있습니다.");}
        if(effects.Contains("KillExplosionDelayRequired")&&c.KillExplosionDelay is null){c=c with{KillExplosionDelay=.5};Note("처치 후 폭발 지연은 0.5초로 근사합니다. 시각 미기재로 주변 적 TTK에 영향이 있습니다.");}
        if(effects.Contains("ThunderboltChance")&&c.ThunderboltDirectRule=="Unspecified"){c=c with{ThunderboltDirectRule="Replace"};Note("썬더볼트 폭발은 원문의 화살 소멸에 따라 직격을 대체하는 해석입니다. 같은 명중의 직격 선후가 불명확해 피해에 영향이 있습니다.");}
        if(effects.Contains("FrenzyRate")&&c.FrenzyBurstPause is null){c=c with{FrenzyBurstPause=.1,RefundRounding=c.RefundRounding=="Unspecified"?"Down":c.RefundRounding};Note("와일드 프렌지의 재입력은 0.1초·탄약 환급 소수는 버림으로 근사합니다. 탄약 효율 적용 후 반복 DPS에 영향이 있습니다.");}
        if(RangedZymos.Applies(w)&&c.Headshot)
        {
            if(c.ZymosSpores is null){c=c with{ZymosSpores=Enumerable.Range(0,6).Select(i=>new RangedSporePath(i%c.GroupCount,.5)).ToArray()};Note("자이모스 포자는 0.5초 후 적들에 순서대로 분배합니다. 실제 무작위 대상·속도 미기재로 CO·분산 피해·TTK에 영향이 있습니다.");}
            if(c.ZymosBurrowTicks is null){c=c with{ZymosBurrowTicks=[new(.55,DamageType.Impact,attacks[0].GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble())*.1)]};Note("자이모스 굴착 피해는 수치·브라켓 미기재로 0.55초에 무모드 직격의 10%를 Impact 고정 피해 사건으로 근사합니다. 유의미한 정확도를 보장하지 않는 임시 모델이며 직접 입력으로 대체할 수 있습니다.");}
            if(c.ZymosSporeExplosion is null){c=c with{ZymosSporeExplosion=true};Note("자이모스 포자는 폭발 누락 버그 대신 본문의 폭발 발생 시나리오를 사용합니다.");}
        }
        if(RangedSingularity.Equipped(b))
        {
            if(c.SingularityPulses is null||c.SingularityIndependentOrbs is null||c.SingularityPullRadius is null||c.SingularityDamageBracket=="Unspecified"||c.SingularityLimit is null)
            {
                c=c with{SingularityPulses=c.SingularityPulses??Enumerable.Range(1,19).Select(i=>new RangedSingularityPulse(i*.25,DamageType.Magnetic,50,5)).ToArray(),SingularityIndependentOrbs=c.SingularityIndependentOrbs??true,SingularityPullRadius=c.SingularityPullRadius??5,SingularityDamageBracket=c.SingularityDamageBracket=="Unspecified"?"Additive":c.SingularityDamageBracket,SingularityLimit=c.SingularityLimit??4};
                Note("스폰테이니어스 특이점의 미기재 피해는 사이노이드의 자성 50/0.25초·5m로 차용하고 독립 멀티샷·가산 피해·활성 한도 4개로 근사합니다. 5초 지속과 확률은 원문을 유지합니다. DPS·처치·끌어오기에 큰 영향이 있습니다.");
            }
        }
        if(b.Kitgun is {Arcane:"PaxSeeker"} seeker)
        {
            if(c.PaxSeekerPaths is null){c=c with{PaxSeekerPaths=Enumerable.Range(0,seeker.ArcaneRank+1).Select(i=>new RangedSeekerPath(i%c.GroupCount,.5)).ToArray()};Note("팍스 시커는 0.5초 뒤 적에게 순서대로 도착하는 경로입니다. 실제 대상 가중치·탄속 미기재로 분산 피해·TTK에 영향이 있습니다.");}
            if(c.PaxSeekerRetrigger is null){c=c with{PaxSeekerRetrigger=false};Note("팍스 시커 자체 처치로 시커를 재생성하지 않는 시나리오입니다. 재귀 발동 여부 미기재로 연쇄 처치 피해에 영향이 있습니다.");}
            if(RangedKitguns.Get(w.Id)?.Chamber is "Sporelacer" or "Vermisplicer")Note("이 챔버의 팍스 시커 피해식은 미기재입니다. 총신·그립 피해를 모두 사용하는 래틀것/게이즈 식으로 근사합니다. 시커 피해·TTK에 큰 영향이 있습니다.");
        }
        if(b.Kitgun is {} parts&&RangedResidual.Is(parts.Arcane))
        {
            var s=c.Residual??new();
            if(s.OutputMultiplier is null||s.TickInterval is null||s.FirstTick is null||s.Activations is null||s.WeaponCredit is null||s.StatusTick is null)
            {
                c=c with{Residual=s with{FixedPacketScenario=true,OutputMultiplier=s.OutputMultiplier??1,TickInterval=s.TickInterval??1,FirstTick=s.FirstTick??1,Activations=s.Activations??Enumerable.Range(1,11).Select(i=>(double)i).ToArray(),BoilsApproachRadius=s.BoilsApproachRadius??10,BoilsFalloff=s.BoilsFalloff??.9,ViremiaCombines=s.ViremiaCombines??false,WeaponCredit=s.WeaponCredit??false,StatusTick=s.StatusTick??RangedResidual.Base(parts.Arcane)*.5}};
                Note("레지듀얼 미기재 규칙은 비치명 고정 출력 ×1·1초 간격·첫 발동 1초·상태 틱은 기본 피해의 50%로 근사합니다. 보일스 접근 10m/감쇠 90%·바이레미아 독립 원소·무기 처치 귀속 없음 시나리오입니다. 지속 피해·발동 횟수·TTK에 큰 영향이 있습니다.");
            }
        }
        if(effects.Contains("VoltageTrapDuration")&&(c.VoltageTrapLanding is null||c.VoltageTrapTargets is null||c.VoltageTrapDamage is null||c.VoltageTrapPhase is null))
        {c=c with{VoltageTrapLanding=c.VoltageTrapLanding??.5,VoltageTrapTargets=c.VoltageTrapTargets??1,VoltageTrapDamage=c.VoltageTrapDamage??6.5,VoltageTrapPhase=c.VoltageTrapPhase??0};Note("볼티지 함정은 0.5초 후 낙하·동시에 1명·문서의 6–7 중간값 6.5·착지 즉시 첫 틱으로 근사합니다. 발동 시각·대상 제한 미기재로 누적 피해에 영향이 있습니다.");}
        if(RangedQuanta.Applies(w)&&c.CubeLifetimeRule=="Unspecified"){c=c with{CubeLifetimeRule="AfterStop"};Note("콴타 큐브 수명은 상충하는 두 서술 중 정지 후 8초를 사용합니다. 만료 폭발 시각에 영향이 있습니다.");}
        if((RangedSimulor.Applies(w)||RangedOrdinarySimulor.Applies(w))&&b.Distance>0&&c.SimulorArrivalSeconds is null){c=c with{SimulorArrivalSeconds=Math.Min(.9,b.Distance/25)};Note("시뮬러 이동은 25m/s·최대 0.9초로 근사합니다. 감속·합체 시각이 미기재로 합체 순서·TTK에 영향이 있습니다.");}
        if(RangedInfestedOrbs.Applies(w)&&b.Distance>0&&c.InfestedStopSeconds is null){c=c with{InfestedStopSeconds=b.Distance/Math.Max(1,RangedEngine.N(a,"ShotSpeed")??15)};Note("감염 구체는 지정 거리까지 등속 이동 후 정지하는 근사입니다. 실제 감속 미기재로 위치·펄스 명중에 영향이 있습니다.");}
        if(b.Distance>0&&c.ProjectileFlightSeconds is null&&RangedEngine.S(a,"ShotType")=="Projectile"&&RangedEngine.N(a,"ShotSpeed") is null or <=0)
        {c=c with{ProjectileFlightSeconds=b.Distance/25};Note("미기재 투사체 탄속은 팬타즈마 본탄의 25m/s를 차용한 근사입니다. 착탄·상태·TTK에 영향이 있습니다.");}
        var nullifier=c.Nullifier??new(Kind:b.Enemy is {} enemy?RangedNullifiers.EnemyKind(enemy.EnemyId):"None");
        if(nullifier.Kind!="None"&&(nullifier.FrameSeconds is null||nullifier.CollapseRadius is null||nullifier.RadialBlocked is null||!nullifier.NoRegrowth&&(nullifier.RecoveryDelay is null||nullifier.GrowthPerSecond is null)))
        {
            c=c with{Nullifier=nullifier with{FrameSeconds=nullifier.FrameSeconds??1d/60,CollapseRadius=nullifier.CollapseRadius??.05,RecoveryDelay=nullifier.RecoveryDelay??1,GrowthPerSecond=nullifier.GrowthPerSecond??.1,RadialBlocked=nullifier.RadialBlocked??true}};
            Note("널리파이어는 60fps·반경 5%에서 붕괴·피격 중지 1초 후 초당 10% 재생·외부 폭발 차단으로 근사합니다. 미기재 붕괴·재생 규칙과 프레임 조건이 막 제거 시간·TTK에 영향이 있습니다.");
        }
        if(effects.Contains("CannonadeChance")&&c.Cannonade is null)
        {c=c with{Cannonade=new(OutputMultiplier:1,Falloff:[new(0,1),new(10,.5)],RadialTriggers:false,FixedNoncriticalPacket:true)};Note("구형 캐논네이드의 미기재 피해 브라켓·감쇠는 컨실드 폭발처럼 비치명 고정 피해·10m 끝점 50% 선형 감쇠·직격만 발동으로 근사합니다.");}
        if(RangedPhantasma.Applies(w)&&b.AttackIndex>0)
        {
            if(c.PhantasmaReferenceAmmo is null)notes.Add(RangedPhantasma.Assumption);
            if(w.EnglishName.EndsWith("Prime"))Note("팬타즈마 프라임: 일반형의 초당 11발 충전 규칙을 공유한다고 가정합니다. 프라임 본문은 충전 속도를 별도로 명시하지 않습니다.");
        }
        if(w.Id=="wiki:secondary/Aksomati Prime")Note("악소마티 프라임 발사 가속은 일반형의 시작 25%·발사당 15%p를 사용합니다. 프라임의 단계별 수치 미기재로 초기 DPS에 영향이 있습니다.");
        if(RangedKitguns.Get(w.Id) is {Category:"secondary"} kit)
        {
            if(kit.Chamber=="Tombfinger")Note("톰핑거 보조: 상충하는 예시 대신 본문의 방사능 직격 19.5%·폭발 80.5% 분할을 적용합니다. 총 기본 피해는 그립 표를 보존합니다. 역원뿔 각도가 미기재여서 폭발은 반경 내 대상을 포함하는 원형으로 근사하며 감쇠·CO·상태 분포·다중 명중에 영향이 있습니다.");
            if(kit.Chamber=="Sporelacer")Note("스포어레이서 자탄 피해는 그립 표에 없습니다. 자탄 3개의 총 독성 피해를 본탄 폭발과 같게 두고, 자탄별 직격:폭발은 팬타즈마의 1:6 비율을 참고합니다. 누적 피해·상태·TTK에 큰 영향이 있는 근사입니다.");
        }
        if(RangedClusters.Get(w.Id) is {} cluster&&b.AttackIndex==cluster.Parent&&c.ClusterLandings is null)
        {
            c=c with{ClusterLandings=Enumerable.Range(0,cluster.Count).Select(i=>new RangedCollision(.5,0)).ToArray()};
            Note($"자탄 {cluster.Count}개가 본탄 명중 0.5초 뒤 같은 적에 명중하는 배치입니다. 실제 퍼짐·유도 경로는 미기재이며 피해·상태·TTK에 영향이 있습니다. 착탄 배치에서 변경할 수 있습니다.");
        }
        if(RangedPassives.Get(w.Id)?.Kind=="Cedo"&&b.AttackIndex==1&&c.GlaiveReturnSeconds is null){c=c with{GlaiveReturnSeconds=5};Note("시도 회수는 본문의 약 5초를 사용합니다. 반복 발사 시각·DPS에 영향이 있습니다.");}
        var trigger=RangedEngine.S(a,"Trigger");
        if((trigger.Contains("Charge")&&RangedEngine.N(a,"ChargeTime") is null||RangedPassives.Get(w.Id)?.Kind=="Onos"&&b.AttackIndex>=1)&&c.ChargeCycleSeconds is null)
        {
            var charge=1/Math.Max(.1,RangedEngine.N(a,"FireRate")??1);
            c=c with{ChargeCycleSeconds=charge};Note($"충전 시간 미기재: 표의 발사 속도 역수 {charge:0.###}초를 충전 시간으로 사용합니다. 확정 게임 수치가 아니며 완충·반복 DPS·TTK에 영향이 있습니다.");
        }
        if(RangedPassives.Get(w.Id)?.Kind=="Onos"&&c.ChargeStackReset=="Unspecified"){c=c with{ChargeStackReset="OnRelease"};Note("오노스 완충 발사 뒤 충전 피해 중첩을 소비하는 시나리오입니다. 다음 충전 피해에 영향이 있습니다.");}
        if(RangedFields.Get(w.Id) is {RequireDuration:true}&&c.FieldEndSeconds is null){c=c with{FieldEndSeconds=5};Note("팍스 지속 시간은 본문 4초와 상충하는 모듈의 5초를 사용합니다. 장판 피해·상태 유지에 영향이 있습니다.");}
        if(w.Id==RangedPullWeapons.Proboscis&&b.AttackIndex==0&&(c.ProboscisTickOne is null||c.ProboscisTickTwo is null))
        {c=c with{ProboscisTickOne=c.ProboscisTickOne??1.7/3,ProboscisTickTwo=c.ProboscisTickTwo??1.7*2/3};Note("프로보시스 촉수 2타를 폭발 전 1.7초 구간의 1/3·2/3에 배치합니다. 개별 시각 미기재로 상태 적용·TTK가 달라질 수 있습니다.");}
        if(RangedVectis.Applies(w)&&b.AttackIndex==1&&!c.Headshot&&(c.VectisBodyTickOne is null||c.VectisBodyTickTwo is null))
        {c=c with{VectisBodyTickOne=c.VectisBodyTickOne??2.5/3,VectisBodyTickTwo=c.VectisBodyTickTwo??2.5*2/3};Note("벡티스 몸통 부착의 추가 2틱을 2.5초 구간의 1/3·2/3에 배치합니다. 개별 시각 미기재로 상태·TTK에 영향이 있습니다.");}
        if(RangedSimulor.Applies(w)&&c.SimulorFirstTick is null){c=c with{SimulorFirstTick=.25};Note("사이노이드 시뮬러 첫 틱을 생성 후 한 주기(0.25초)에 배치합니다. 첫 틱 위상 미기재로 짧은 교전 피해에 영향이 있습니다.");}
        if(RangedOrdinarySimulor.Applies(w))
        {
            var s=c.OrdinarySimulor??new();
            if(s.UnmergedLifetime is null||s.LimitedLifetime is null||s.FirstTick is null||s.VortexMerge=="Unspecified")
            {c=c with{OrdinarySimulor=s with{UnmergedLifetime=s.UnmergedLifetime??1,LimitedLifetime=s.LimitedLifetime??20,FirstTick=s.FirstTick??.25,VortexMerge=s.VortexMerge=="Unspecified"?"Separate":s.VortexMerge}};Note("일반 시뮬러 미기재 수명·첫 틱은 사이노이드의 1초/20초/0.25초를 참고하며 완성 특이점은 별개로 유지합니다. 피해 지속·합체 빈도에 영향이 있습니다.");}
            s=c.OrdinarySimulor!;
            if(s.Merge is null||s.Explosion is null||s.TickOutput is null||s.TickRadius is null||s.TickInterval is null)
            {
                c=c with{OrdinarySimulor=s with{TickOutput=s.TickOutput??50,TickRadius=s.TickRadius??5,TickInterval=s.TickInterval??.25}};
                Note("일반 시뮬러 미기재 합체 125·폭발 240×최대 4중첩·틱 50/0.25초·틱 반경 5m는 사이노이드에서 차용한 근사입니다. 일반형의 강제 자성·60% 폭발 감쇠·물리 멀티샷은 유지합니다. 피해·TTK에 큰 영향이 있습니다.");
            }
        }
        if(RangedTendrils.Applies(w))
        {
            if(c.TendrilAcquisitionSeconds is null){c=c with{TendrilAcquisitionSeconds=.2};Note("버미스플라이서 표적 획득은 0.2초 가정입니다. 획득 지연 미기재로 첫 피해·재조준 DPS에 영향이 있습니다.");}
            if(w.Category=="secondary"&&c.TendrilRoutes is null){c=c with{TendrilRoutes=Enumerable.Range(0,5).Select(_=>new RangedTendrilRoute(0)).ToArray()};Note("보조 버미스플라이서 촉수 5개가 조준한 같은 적을 획득하는 배치입니다. 촉수 경로에서 대상·빗나감을 바꿀 수 있습니다.");}
        }
        if(RangedReturningBlades.Get(w.Id) is not null&&c.ReturningBladeHits is null){c=c with{ReturningBladeHits=[new(.5)]};Note("칸타레 복귀 칼날이 재장전 시작 후 0.5초에 같은 적을 통과하는 배치입니다. 실제 경로 미기재로 복귀 피해·합창 발동에 영향이 있습니다.");}
        if(RangedStug.Applies(w)&&c.StugExplosionRadius is null)Note("스터그 중간 중첩 반경은 원문의 1중첩 0.3m와 10중첩 2.8m 사이를 선형 보간합니다. 증가 곡선 미기재로 주변 대상 명중·피해에 영향이 있습니다.");
        if(RangedStug.Applies(w)&&b.AttackIndex==2&&c.StugBounces is null){c=c with{StugBounces=[new(.25),new(.5),new(.75)]};Note("스터그 인카논의 3회 도탄을 0.25초 간격으로 같은 적에 배치합니다. 지형별 궤적 미기재로 누적 피해·TTK에 영향이 있습니다.");}
        if(RangedPentaAugments.Applies(w)&&RangedPentaAugments.Has(b,"PentaNapalm")&&c.NapalmUtilityMode=="Unspecified")
        {c=c with{NapalmUtilityMode="Fixed"};Note("네이팜 유탄은 상충하는 두 해석 중 고정 화염 100의 보조 피해를 사용합니다. 장판 피해 해석은 설정에서 변경할 수 있습니다.");}
        if(RangedPentaAugments.Has(b,"PentaNapalm")&&c.NapalmPatchTick is null&&!c.NapalmExcludePatchDamage){c=c with{NapalmPatchTick=50};Note("네이팜 장판의 미기재 상태 피해는 별도 화염 100의 50%인 50/틱으로 근사합니다. 모드 증폭을 가정하지 않으며 지속 피해·TTK에 영향이 있습니다.");}
        if(RangedInfestedOrbs.Applies(w)&&b.AttackIndex==1&&c.InfestedFirstPulse is null){c=c with{InfestedFirstPulse=1};Note("뮤탈리스트 콴타 첫 펄스는 생성 1초 후입니다. 첫 틱 위상 미기재로 짧은 교전 결과에 영향이 있습니다.");}
        if(RangedAzima.Applies(w)&&b.AttackIndex==2)
        {
            var s=c.Azima??new();
            if(s.LandingSeconds is null||s.UnmoddedLifetime is null||s.VolleyRate is null||s.FirstVolley is null||s.RotationSpeed is null||s.HitRadius is null||s.ExplosionRadius is null||s.ExplosionFalloff is null)
            {c=c with{Azima=s with{LandingSeconds=s.LandingSeconds??.5,UnmoddedLifetime=s.UnmoddedLifetime??10,VolleyRate=s.VolleyRate??10,FirstVolley=s.FirstVolley??.1,RotationSpeed=s.RotationSpeed??90,HitRadius=s.HitRadius??.3,ExplosionRadius=s.ExplosionRadius??2,ExplosionFalloff=s.ExplosionFalloff??.5}};Note("아지마는 약 10초 수명을 사용하고 미기재 값은 착지 0.5초·초당 10회·첫 사격 0.1초·회전 90°/초·명중 반경 0.3m·폭발 2m/감쇠 50%로 근사합니다. 회전별 명중·피해·TTK에 큰 영향이 있습니다.");}
        }
        if(RangedAzima.Applies(w)&&c.Azima is {InfiniteConsumesMagazine:null} azima&&c.InfiniteAmmoSeconds>0){c=c with{Azima=azima with{InfiniteConsumesMagazine=false}};Note("아지마 무한 탄약 투척은 탄창을 소비하지 않는 시나리오로 근사합니다.");}
        return b with{Conditions=c with{ApproximationNotes=notes.Distinct().ToArray(),ApproximationResolved=true}};
    }

    public static JsonElement Attack(RangedBuild b,JsonElement attack)
    {
        if(b.Conditions?.AllowApproximations==true&&b.WeaponId==RangedKomorex.Id&&b.AttackIndex==1)
        {
            var radial=JsonNode.Parse(attack.GetRawText())!;radial["Damage"]=new JsonObject{["Viral"]=106};radial["Range"]=3.5;radial["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=3.5,["Reduction"]=.4};return JsonSerializer.SerializeToElement(radial);
        }
        if(b.Conditions?.AllowApproximations!=true||RangedKitguns.Get(b.WeaponId) is not {Category:"secondary"} d)return attack;
        var n=JsonNode.Parse(attack.GetRawText())!.AsObject();var grip=b.Kitgun?.Grip??RangedKitguns.Default(d).Grip;
        var raw=d.Stats.GetProperty("Damage").GetProperty(grip);
        double Value(string key)=>raw.TryGetProperty(key,out var v)?v.GetDouble():0;
        if(d.Chamber=="Tombfinger"&&b.AttackIndex is 0 or 1)
            n["Damage"]=b.AttackIndex==0?new JsonObject{["Impact"]=Value("Impact"),["Puncture"]=Value("Puncture"),["Radiation"]=Value("Radiation")*.195}:new JsonObject{["Radiation"]=Value("Radiation")*.805};
        else if(d.Chamber=="Sporelacer"&&b.AttackIndex is 2 or 3)
            n["Damage"]=new JsonObject{["Toxin"]=Value("Toxin")/3*(b.AttackIndex==2?1d/7:6d/7)};
        else return attack;
        n.Remove("UnsupportedReason");return JsonSerializer.SerializeToElement(n);
    }
}
