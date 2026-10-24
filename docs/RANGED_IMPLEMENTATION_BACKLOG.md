## 2026-10-24 추가 연결 및 근사 계산

최종 검증: [2026-10-24 계산·UI 검증 보고서](RANGED_VERIFICATION_2026-10-24.md) — 계산1,519개 통과·실패0, 실제 UI92+46개 통과.
팬타즈마·프라임의 빔 시작 배율 15%, 일반형의 멀티샷 모드 기준 5, 탄약 비례 충전, 초당 11발 충전, 본탄→폭발→5개 자탄→자탄 폭발 경로를 연결했다. 발사 시점 충전 탄약은 후속 탄창 변화와 분리한다. 톰핑거 보조의 직격→폭발 연결도 추가했다.

최신 사용자 지시에 따라 원문 공백을 일괄 거부하는 대신 명시적인 근사 모델을 제공한다. 앱의 기본 근사 모드는 미지정 값만 채우고 결과 상단에 근거·값·영향을 표시한다. 엄격 모드를 선택하거나 직접 입력하면 기존 동작을 유지한다. 상세 구현·가정·검증은 [총기 근사 모델](RANGED_APPROXIMATIONS.md)을 기준으로 한다.

아래의 10월 23일 구현 기록은 이력이다. '시나리오 필요'는 코드 미연결을 뜻하지 않으며, 최신 기본 근사 지원 여부는 위 문서와 `RangedApproximations`에 기록한다. 전체 목록 실행은 코드 경로 검사이고 게임 실측 일치의 증명은 아니다.

---

# 총기 후속 연결 작업 — 2026-10-23

10월 16일 목록의 연결 상태를 갱신했다. 아래의 완료는 명시한 사건 경로의 구현과 회귀 검사 완료를 뜻하며, 알려지지 않은 수치를 게임 상수로 확정했다는 뜻은 아니다. 실제 원문 공백·상충은 `RANGED_SOURCE_GAPS.md`에 분리한다.

| 항목 | 연결한 내용 | 근거·검증 위치 |
|---|---|---|
| 일반 Simulor | 멀티샷/합체/중첩/수명/수 제한, 특이점과 폭발, 중복 합체 후보·이동 중 틱의 불확실성 검출 | RangedSimulor, RangedChecks |
| Spontaneous Singularity | 생성 확률·5초 수명·생성 때 폭발 제외·도탄/합체 제거, 아군 부착 위치 갱신 | RangedSingularity, RangedChecks |
| Neutralizing Justice | 최대 90% 생성기 영구 파괴, Nullifier 프레임별 피해 합산·상한·자성·막 축소 | RangedNullifiers, RangedChecks |
| Cannonade | 구형 30%/250 Blast/10m, 미기재 브라켓·감쇠는 명시적 출력 조건 | RangedFlatExplosions, RangedChecks |
| Azima | 탄창 소비 투척, 복수 터렛, 4방향 회전·명중·현재 버프·탄약 효율·만료 폭발 | RangedAzima, RangedChecks |
| Residual 4종 | 처치 생성 20%, 랭크별 수명, 두 키트건 공통 2개 제한, 종류별 발동/상태/방어층 | RangedResidual, RangedChecks |
| Mutalist Quanta | 외부 탄환 흡수·저장 폭발, 도탄·근접 밀치기 이동, 이동 후 탄환 교차 재계산 | RangedOrbInteraction, RangedChecks |
| Quanta | 본체 빔과 실제 큐브 교차·지정 기폭 | RangedQuanta, RangedChecks |
| Penta/Tether | 공중 기폭 좌표·착지·대상/사체 부착·물/타르 접촉·조기 폭발·중복 폭발 방지 | RangedGrenadeEvent, RangedChecks |
| Vermisplicer | 촉수별 획득 지연·경로·미부착, 주무기의 가장 먼 관통 대상 분기 | RangedTendrils, RangedChecks |
| 패치 44.1.3 | 리벤 공통 1.2, 일반 탄약 효율 75%, 무한 탄약 분리, 관련 진화/증강/아케인 변경 | RivenBalance, RangedBalance4413, RangedChecks |

전체 회귀 결과와 실제 UI 확인은 `PATCH_44_1_3_VERIFICATION.md`에 기록한다. 카탈로그 356개 무기의 736개 공격 구성요소 정적 감사는 누락·오류 탐색용이며 모든 전장/무기 조합의 동등성을 증명하는 검사가 아니다.

공백 없이 확정할 수 없는 대표 항목은 보조 Tombfinger의 직격/폭발 분할, 보조 Sporelacer 자탄 출력, 일부 특수 공격의 시간/경로와 원문끼리 상충하는 버그다. 숨겨진 임의 계수나 0 피해로 성공 처리하지 않는다. 실제 지형 물리, NPC AI, 전체 워프레임 어빌리티·익절티드 시스템은 합의된 이번 작업 범위가 아니다.
