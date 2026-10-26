# 일정 안의 무기 순환

사용자의 확인에 따라 상단 별도 메뉴를 추가하지 않고 `일정 → 종합 일정 / 무기 순환`으로 구분했다. 종합 일정에는 다음 S티어·현재 진행 중 중재, 다섯 지역의 낮·밤 및 미션·팩션 필터를 유지했다. 무기 순환에는 두 영역을 나란히 배치한다.

- 리치 무기: 테넷·코다 상점의 교체 타이머, 코다 A/B 회차, 상점·찜 필터, 무기 목록, 가격·MR·원소 제보와 모딩 바로가기. 쿠바 라블링의 무작위 출현을 상점 일정으로 표시하지 않는다.
- 인카논: 강철의 길 순환로의 이번 주 5개·다음 주 5개, 주간 교체 타이머와 전체 9주 순환표. 무기는 한국어 Public Export 이름과 내부 ID로 연결하며 클릭하면 해당 무기의 모딩 화면을 연다.

두 하위 탭을 왕복해도 필터·선택을 유지한다. 선택 강조는 언더바다. 작은 창에서는 중복 제목과 설명을 줄이고, 상점 상세를 펼칠 때 본문 높이를 확보해 목록이 사라지지 않도록 했다. 이번 주 구성 버튼은 매초 다시 만들지 않아 키보드 초점을 유지한다.

## 자료와 주간 경계

Wiki Incarnon 문서 개정 [2821396](https://wiki.warframe.com/w/Incarnon?oldid=2821396)의 9주·45개 표를 가져왔다. [공식 월드 상태](https://api.warframe.com/cdn/worldState.php)의 `EndlessXpSchedule`에서 2026-10-19 00:00 UTC~2026-10-26 00:00 UTC의 Dera/Sybaris/Cestra/Sicarus/Okina 선택지와 대조해 8주차 기준을 연결했다. 한국 시각 월요일 오전 9시가 교체 경계다. `duviriCycle.activation/expiry`는 나선의 짧은 주기이므로 인카논 주간 날짜로 사용하지 않는다.

`IncarnonRotations`는 현재 범위의 온라인 선택지를 우선한다. 공식 또는 API 자료가 10분 미만이고 미래 시각이 비정상적이지 않을 때 확인 자료로 표시한다. 갱신 실패·저장 자료·미래 일정은 예상임을 표시한다. 새로운 미등록 구성이 들어오면 현재 선택지의 알려진 ID만 연결하고, 모르는 품목의 모딩 버튼과 임의의 다음 주 예측은 제공하지 않는다.

정규화 파일은 `data/incarnon-rotations.json`, 재생성 도구는 `tools/import_incarnon_rotations.py`다. 원본 URL·개정·해시와 공식 기준 시각을 보존한다. 원본 위키의 CC BY-NC-SA 3.0 고지는 기존 방식대로 유지한다.

## 검수 방식

계산 검사에는 45개 모딩 ID 연결, UTC/KST 교체 직전·정각, 9→1주차 및 과거 순환, 공식·중간 API 형식, 두비리 나선/주간 구분, 저장 자료의 예상 표시, 미등록 구성과 잘못된 응답 거부를 포함한다. 실제 WPF UIA로 하위 탭·무기 버튼·전체 순환표·필터·찜·모딩 이동을 조작하고, 1760×920·1536×820·1280×688과 100/125/150% DPI 및 두 테마의 겹침·잘림을 검사한다. 요청한 창 크기와 실제 측정 크기도 대조한다.

게임 입력을 보내거나 사용자 데스크톱으로 전환하지 않는 별도 Windows 데스크톱에서 실행한다. 오버레이 자동 세로 순환과 native alpha, 전체 앱 smoke 및 기존 UX 회귀를 함께 검수한다. 결과 파일·배포 기록은 최종 실행본 검증 뒤 아래에 덧붙인다.

## 최종 결과와 반영

- `artifacts/weapon-rotation-core.log`: 전체 계산·자료 1,707개 통과, 실패 0. 새 순환 관련 18개를 포함한다. 테스트한 Core DLL과 배포한 Core DLL의 SHA-256이 같다.
- `artifacts/weapon-rotation-delivery-ui/checks.json`: 일정·무기 순환·필터·모딩 이동·실제 크기·테마/배율·설정·오버레이 602개 통과.
- `artifacts/weapon-rotation-delivery-ux/checks.json`: 기존 전체 화면 UX 회귀 200개 통과.
- `artifacts/weapon-rotation-delivery-marquee/checks.json`: 자동 세로 순환 31개 통과.
- `artifacts/weapon-rotation-delivery-alpha/checks.json`: native 투명도 76개 통과.
- `artifacts/weapon-rotation-delivery-smoke`: 최종 self-contained 실행 파일로 전체 앱 smoke 종료 0, `ui-check.json.passed=true`.
- `artifacts/weapon-rotation-verified-live/incarnon-live.json`: 2026-10-26 00:16 UTC의 공식 주간 구성을 온라인으로 확인했다. 9주차 Vectis/Stug/Ballistica/Destreza/Obex이며 다음 주 예상은 1주차다.

최종 실행본은 `artifacts/weapon-rotation-delivery-app`이며 기존 `artifacts/riven-auto-side-app/WFLabs.exe`에 반영했다. .NET 8.0.30 자체 포함 x64 구성 513개 파일을 SHA-256으로 대조했다. 이전 실행 폴더는 `artifacts/riven-auto-side-before-weapon-rotations-20261026-092620`에 보존했다. 변경 파일은 Desktop/Core DLL과 각각의 PDB 4개이며 OCR·글꼴 파일은 동일하다. 기록은 `artifacts/weapon-rotation-deployment.json`, 파일 비교는 `artifacts/weapon-rotation-package-diff.json`에 있다.

공개 Release·설치 파일을 새로 생성하거나 사용자 앱을 자동 실행하지 않았다. 실제 게임 화면 위의 새 캡처나 실제 모니터 이동 검사를 한 것은 아니다. 이후 서버가 순환표를 바꿀 가능성, 상점의 미래 무작위 원소·보너스는 확정값으로 제공하지 않는다.
