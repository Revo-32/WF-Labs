# 일정·다음 S티어 중재·테넷/코다 상점

상단 `일정` 탭의 첫 화면에 다음 S티어 중재의 지역·노드·미션·시작 시각과 카운트다운을 고정한다. S티어가 진행 중이면 종료까지 남은 시간을 별도로 표시한다. `낮·밤 주기`, `중재`, `무기 상점`을 전환해도 다음 S티어를 계속 볼 수 있다. 시각은 PC의 시간대와 관계없이 한국 시간(KST), 내부 계산은 UTC다.

## 구현 범위

- 지역 주기: 지구/아이돌론 평원, 오브 협곡, 캠비온 퇴적지, 자리만, 두비리. 현재 상태·다음 전환·남은 시간·주기 진행률을 표시한다.
- 중재: S티어 / S+A / 모든 등급, 24시간 / 3일 / 7일 필터, 목록 선택 후 지역·미션·진영·시작/종료·출처 확인. 다음 S는 시작 시각이 현재보다 미래인 회차이며 진행 중인 회차와 구분한다. 같은 노드가 연속 S 일정에 다시 등장해도 서로 다른 회차다.
- 무기 상점: 테넷 5종(40 커럽티드 홀로키), 코다 14종(A/B 각 7종, 10 라이브 심장 세포). 현재 판매 회차 예상·다음 판매·교체 타이머·가격·MR·찜한 무기 필터·모딩 바로가기를 제공한다. 일반 쿠바 무기의 개인 거래 일정은 포함하지 않는다.
- 찜은 `%LOCALAPPDATA%\WF Labs\schedules\wishlist.json`에 저장해 재시작 후 복원한다.
- 화면을 보는 동안 초 단위 카운트다운을 로컬 계산하고 지역 자료는 1분, 중재 표는 6시간, 위키 보너스 제보는 1시간 주기로 갱신한다. 탭을 떠나면 주기 타이머가 멈춘다. 사용자 새로고침은 즉시 요청한다. 시스템 알림과 게임 오버레이는 이번 일정 화면의 범위가 아니다.

## 자료 기준과 정확성

중재 S티어는 **Arbitration Goons / browse.wf**의 커뮤니티 판단이다. 게임 공식 등급이나 특정 빌드의 수익 보장이 아니다. [원본 일정](https://browse.wf/arbys), [자료 이용·출처 안내](https://browse.wf/about), [등급 원본](https://github.com/calamity-inc/browse.wf/blob/senpai/supplemental-data/arbyTiers.js)을 사용한다. 내장 예측표는 44,056개의 연속 1시간 회차, 2024-10-02 16:00 UTC부터 2029-10-12 08:00 UTC 전까지다. 끝 이후를 순환해 만들지 않는다. 갱신 표의 중복·역순·누락·잘못된 ID 형식을 거부한다. 실시간 API가 유효한 중재를 주면서 현재 예측 노드와 다르면 불일치를 표시한다. 현재 API의 SolNode000/Unknown 및 비정상 먼 미래 만료 값은 유효한 실시간 중재로 취급하지 않는다.

월드 상태는 [PC world state API](https://api.warframestat.us/pc?language=ko)를 사용한다. 지구 숲의 예전 4시간 `earthCycle`은 [공식 Update 38.5](https://www.warframe.com/en/patch-notes/pc/38-5-0)의 평원 동기화와 충돌하므로 사용하지 않는다. 지구/평원은 낮 100분·밤 50분, 오브 협곡은 따뜻함 400초·추움 1200초다. 제공 API의 전환 기준에 따른 타이머로 게임 서버의 서브초 정밀도 측정이 아니다. 기준 시각이 10분 이상 오래되거나 온라인 갱신이 실패하면 마지막 정상 자료로 계산하고 **저장 자료 예상**을 표시한다.

실제 온라인 검수 중 API가 약 20분 전 자료를 반환했고 공식 자료의 시각은 최신이었다. 이 경우 [공식 worldState.php](https://api.warframe.com/cdn/worldState.php)의 `CetusSyndicate.Expiry`(밤 종료)에서 낮/밤을 구분해 평원과 동기화된 캠비온만 보완한다. 밤 종료 50분 전이 낮→밤 경계다. 만료된 의뢰와 범위를 벗어난 시각은 거부한다. 각 지역에 별도 `SourceUtc`를 보존해 다른 세 지역이나 중재의 오래된 기준을 새 시각으로 바꿔 표시하지 않는다. 공식 보완 2지역과 저장 기준 예상 3지역이 한 화면에 공존할 수 있다. 보완한 원문도 별도 캐시로 보존하고 재시작 시 복원한다.

테넷·코다 상점은 [Tenet Weapons](https://wiki.warframe.com/w/Tenet_Weapons), [Coda Weapons](https://wiki.warframe.com/w/Coda_Weapons)의 현재 주기식을 사용한다. 4일마다 00:00 UTC(한국 09:00) 교체, 코다 A/B 교대다. 코다의 현행 위키 식은 2025-03-18 A 기준이며 초기 출시 공지의 첫 교체 날짜와 차이가 있어 **위키 주기 예상**으로 표시한다. 다음 회차의 무작위 원소·보너스는 계산하지 않는다.

원소·보너스는 Wiki의 현재 개정과 회차 시작 전 마지막 개정의 **해당 무기 행**을 비교한다. 현회차 안에 그 값이 달라진 근거가 있어야 현회차 제보로 표시한다. 문서의 다른 문구만 바뀐 개정, 이전 회차 제보, 전 회차 원문 미확인은 현재 보너스 확정 근거로 쓰지 않는다. 이전 값은 출처 펼치기 안에서만 참고값으로 표시한다. 중복 행, 불가능 원소, 25~60% 밖 수치는 버린다. Wiki 제보이므로 게임 상점에서 읽은 값과 구분한다.

정규화 데이터와 출처·개정·SHA-256은 `data/world-schedules.json`, `data/world-schedule-sources.json`, `data/arbitration-schedule.csv`에 보존한다. 노드·품목·한국어 태그는 MIT의 warframe-public-export-plus를 이용하며 라이선스와 browse.wf 출처 고지를 앱에 포함한다. 게임 내 고유 노드명이 공식 한국어 사전에서도 영문인 경우 그대로 표시한다.

## 검수

검수 원본은 `artifacts/world-schedules-final-app/WFLabs.exe`다. 작업 중에는 기존 앱이 열려 있어 보존했고, 최종 단계에서 종료된 것을 확인한 뒤 기존 `artifacts/riven-auto-side-app/WFLabs.exe` 경로에 같은 빌드를 반영했다. 이전 폴더는 `artifacts/riven-auto-side-before-schedules-20261026-004004`에 보존했다. 자동 실행하지 않았다. 자체 포함 Windows x64 빌드이며 설치 파일·공개 Release는 생성하지 않았다.

검수 결과 파일은 `artifacts/world-schedules-core.json`(집중 23개), `artifacts/world-schedules-final-calculation.json`(전체 1,642개), `artifacts/world-schedules-final-ui/checks.json`(실제 UI 조작·레이아웃 116개, 배율/테마 18조합), `artifacts/world-schedules-final-smoke/isolation.json`(전체 UI smoke 종료 0), `artifacts/world-schedules-final-live/live.json`(실제 API·공식 보완·Wiki 제보)에 있다. 모두 실패 0이다. 최종 검증 요약은 `artifacts/schedule-verification-summary.json`, 빌드·파일 해시는 `artifacts/world-schedules-build.json`에 기록한다. 앱의 Core DLL은 전체 계산 검사에 사용한 Core DLL과 바이트 단위로 같다. 배포 폴더의 509개 파일과 x64, 자체 포함 런타임, 원문 출처 파일, 라이선스 파일을 확인했고 기존 OCR 두 모델은 변경되지 않았다.

핵심 검증은 고정 원본 일정의 알려진 회차, 현재/다음 회차 경계, KST 날짜 변경, 지역 상태 전환, 코다 A/B 경계, 19종 전체 모딩 ID 연결, 잘못된 자료와 연결 실패 시 캐시 보존, 찜 재시작 복원, 실제 WPF UI Automation 필터·탭·선택·버튼 이벤트, 최소 1760×920 창의 100/125/150% DPI와 두 테마다. 버튼 겹침·텍스트 잘림 진단과 스크롤 끝 도달을 검사하고 렌더링 이미지도 직접 검토한다. UI 검수는 별도 Win32 desktop에서 실행하며 사용자의 화면으로 전환하거나 게임 입력을 보내지 않는다.

실제 온라인 API에서 지역 주기 5종과 중재 표, 테넷 5종/현재 코다 7종의 현회차 Wiki 보너스 제보를 읽는 것까지 확인했다. 실제 게임 상점과 같은 순간의 대조, 새 운영체제에서의 알림·오버레이 검증을 뜻하지 않는다. 시스템 알림은 이번 UI에 제공하지 않는다. 공개 Release와 설치 파일은 이 작업에서 생성하지 않는다.
