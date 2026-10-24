# 44.1.3 패치 및 총기 후속 연결 검증 — 2026-10-23

## 반영한 공식 변경

[공식 Hotfix 44.1.3](https://www.warframe.com/en/patch-notes/pc/44-1-3)은 무기별 리벤 기질을 폐지하고 모든 리벤의 가능한 수치 범위를 구형 기질 1.2에 해당하는 공통 배율로 통일했다. 근접·소총·산탄총·권총 리벤 범위, 등급 그래프, 접합 계산에 적용했다. 과거 원본 데이터는 보존하고 `official-riven-44.1.3` 규칙을 우선 적용한다.

구형 리벤 파일은 현재 값으로 자동 승인하지 않는다. 과거의 반올림된 표시값만으로 새 값 하나를 정확히 복원할 수 없으므로 원본 파일을 보존하고 패치 후 게임 표시값을 입력하도록 안내한다.

일반 탄약 효율 75% 상한과 의도된 무한 탄약을 분리했다. AX-52, Pistoleer, Brain Storm, Skull Shots, Bronco Enough for Everyone, Dera/Boltor Crimson Overture, Vasto Reaper's Plenty, Acid Shells 반경 10m를 반영했다. Crimson Overture의 별도 기본 피해 중첩은 기존 상한을 유지하며 탄약 효율만 새 75% 한도를 적용한다.

원본 URL·수집일·SHA-256: `data/official-patches/44.1.3-source.json`. 추가 위키 개정·해시: `data/ranged-expansion-sources.json` 및 `data/ranged-sources/manifest.json`.

## 후속 메커니즘

연결 목록은 `RANGED_IMPLEMENTATION_BACKLOG.md`에 기록했다. Azima 복수 회전 터렛, Residual 4종의 생성·수명·범위 공격, Perigale의 무한 탄약 발동, 일반 Simulor와 Spontaneous Singularity, Nullifier/Neutralizing Justice, Cannonade, Mutalist Quanta 외부 탄환·도탄·근접 밀치기, Penta의 이동/부착/물/타르 기폭, Quanta 빔 교차와 Vermisplicer 경로를 포함한다.

미기재 시간을 임의 상수로 넣지 않고 시나리오 입력을 받는다. 외부 탄환 자체의 피해는 내 무기 DPS에 더하지 않으며 구체에 저장된 뒤 방출한 피해만 포함한다. 파생 공격 단독 선택의 발사 주기 안내와, 투척 운반체의 0 피해를 빈 빌드로 오인시키던 UI를 수정했다. 아지마는 기본 사격 수치와 실제 터렛 교전 DPS를 구분한다.

## 실행한 검사

모든 .NET 명령은 `E:\CODEX\Tools\dotnet8\dotnet.exe` / SDK 8.0.424를 사용했다.

| 검사 | 결과 | 기록 |
|---|---|---|
| Core·Checks Release 및 전체 수식 회귀 | 1,403개 통과 / 실패 0 | `artifacts/patch-ranged-verified-build.log`, `artifacts/patch-ranged-verified-full.json`, `.log` |
| 최종 Desktop Release | 경고 0 / 오류 0 | `artifacts/patch-final-ui-build.log` |
| 기본 `--smoke` | 통과, PNG 153개 | `artifacts/patch-44.1.3-smoke/` |
| 최종 `--ranged-smoke` | 통과, 상태 154개 | `artifacts/ranged-44.1.3-ui-final/ranged-ui.json` |
| 최종 `--riven-smoke` | 버튼·검색·비교 흐름 37개 통과 | `artifacts/riven-44.1.3-ui-final/riven-workshop-check.json` |
| 고정 원문 무결성 | 고정 개정·시간·본문 SHA-256 일치 | `tools/ranged_source_lib.py` 로딩 검사 |
| 총기 구성요소 정적 감사 | 356개 무기 / 736개 구성요소 확인 | `artifacts/ranged-44.1.3-coverage.json` |

전체 회귀는 최신 계산식 변경 후 실행했다. 이후 변경은 파생 공격 안내문 및 투척/빈 그래프 UI 표시이며 최종 Desktop 빌드와 총기 UI 검사를 다시 실행했다.

기본 smoke에서 포르마·장착/해제·슬롯 교환·반투명 드래그·정렬·툴팁·수식 강조·적 설정·테마·한글 글리프·창 프레임·DPI를 검사했다. 리벤 검사는 실제 접합/순환/채택/유지 버튼, 동일 빌드 비교, 직접/상태 DPS 분리, IME 조합·방향키·Enter/Esc 검색 확정과 미완성 텍스트 복원을 포함한다.

PNG 생성만으로 시각 검증을 대신하지 않았다. 최소 1760×920의 근접/총기 화면, 다크·라이트, 150% 총기 화면, 리벤 편집·비교, Azima·Residual·Penta·Mutalist Quanta·Singularity 아군 설정을 직접 열어 확인했다. 보관함 두 줄, 열 너비, 세로 스크롤, 줄바꿈, 명암과 속성별 색상을 확인했다. 지원 DPI의 WPF 시각 트리를 사용한 검사이며 실제 다중 모니터 이동이나 사용자 모니터의 서브픽셀 배열 검사는 아니다.

## 정확성 한계와 보류 항목

상세 원문 공백은 `RANGED_SOURCE_GAPS.md`를 기준으로 한다. 대표적으로 보조 Tombfinger의 직격/폭발 방사능 분할, 보조 Sporelacer 자탄 출력, Aksomati Prime 가속 계수, 일부 Residual/Simulor 출력·시점, Azima 회전·발사 시간, Zymos의 상충하는 버그 기록이 남아 있다. 명시적 시나리오로 계산할 수 있는 항목과 출력 자체를 확정할 수 없어 거부하는 항목을 구분한다.

736개 정적 구성요소 감사나 1,403개 회귀 통과가 모든 무기·모드·전장 조합의 게임 실측 일치 증명은 아니다. 실제 지형 물리·NPC AI·전체 워프레임 어빌리티·익절티드 시스템은 이번 범위가 아니다. 공식 패치의 Elemental Ammo 유발 상태 피해에 대한 처치 폭발 재귀 제한을 일반 처치 폭발 전부의 금지로 확대하지 않았다.

이번 작업은 개발 빌드 검증까지이며 설치 파일 생성·공개 릴리스·온라인 업데이트 게시를 수행하지 않았다. 기존 미커밋 변경과 원본 데이터는 보존했다.
