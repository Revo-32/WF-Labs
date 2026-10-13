# WF Labs v0.5.3 상세 UI 수정·검증 — 2026-10-11

## 정렬 아이콘

기존 정렬 버튼에는 두 개의 Click 핸들러가 있었다. SmallButton의 첫 핸들러가 정렬 후 이벤트를 Handled로 처리하여, 두 번째 핸들러의 화살표 갱신이 실행되지 않았다. 정렬 상태·벡터 아이콘·툴팁·접근성 이름을 RenderLibrary의 같은 경로에서 갱신하도록 고쳤다. 폰트의 화살표 글리프를 벡터 아이콘으로 바꾸고 버튼 영역을 32 DIP로 맞췄다.

회귀 검사는 단순히 내부 descending 값을 바꾸지 않는다. 이름·코스트·극성 세 기준에서 실제 Button.Click 라우팅 이벤트를 두 번 발생시켜 아이콘 반전, 정렬 결과 변화, 툴팁 문구와 원래 순서 복원을 함께 검사한다. 프로그래밍 방식으로 기준을 바꿔도 같은 표시 갱신 경로를 사용한다.

## 모드 카드 배경

기존 고정 Wiki 스냅샷의 내부 ID와 Rarity·Class·Polarity를 연결했다. 보관함 122개 전체의 메타데이터를 확인했으며 한국어/영문 이름 접두사로 등급을 추정하지 않는다. 같은 ID를 공유하는 원본 별칭은 분류가 일치할 때만 같은 마감을 사용하고 불일치는 중립 마감으로 처리한다.

| 분류 | 앱의 마감 |
|---|---|
| 일반 | 구리색 |
| 고급 | 은색 |
| 레어 | 금색 |
| 레전더리/프라임 | 백금색 |
| 갈바나이즈드 | 차가운 금속색·사선 마감 |
| 움브라 | 짙은 흑색·금색 테두리 |
| 아말감 | 자주색과 금속색을 섞은 마감 |
| 리벤 | 보라색·결정 형태의 선 |

정적인 그라데이션·벡터 선을 동결한 DrawingBrush로 캐시한다. 상시 애니메이션이나 게임 원본 카드 아트를 사용하지 않는다. 게임의 등급/분류를 연상시키는 앱용 해석이며 게임의 정확한 RGB 또는 재질 복제라고 표시하지 않는다. 카드의 이름·코스트·극성 구성과 장착 카드의 요약 효과는 유지한다. 등급명은 툴팁에서도 확인할 수 있다.

## 커서 추적 툴팁

모드 카드와 그래프에 공통 CursorTooltip을 적용했다. ToolTipService의 표시 지연을 유지하며 열린 상태에서 MouseMove가 발생할 때 위치를 갱신한다. 커서 우하단 16 DIP를 기준으로 두고 오른쪽/아래 경계에서는 반대쪽으로 이동한다. Win32 MonitorFromPoint/GetMonitorInfo로 현재 모니터 작업 영역을 읽어 소유 컨트롤의 DIP 좌표로 변환한다. 음수 좌표 모니터도 처리한다.

그래프 데이터 구간이 바뀌면 내용과 크기·위치를 함께 갱신한다. 텍스트 본문은 14 DIP, 22 DIP 행간, 모드 효과는 23 DIP 행간이며 제목·메타정보·효과·미지원 안내를 띄웠다. 내부 여백은 14 DIP다. 클릭/드래그 시작·스크롤·영역 이탈·화면 해제 때 닫고 키보드 포커스에서는 항목 옆에 상세 설명을 제공한다.

툴팁의 좌표 계산만 검사하지 않고 투명한 테스트 창에서 실제 팝업 HWND를 열어 이동을 측정했다. 운영체제 커서를 이동시키지 않았으며 테스트 창과 팝업은 불투명도 0, 비활성, 작업 표시줄 숨김 상태다. 기준 좌표를 140×70 DIP 이동했을 때 96 DPI 환경의 HWND도 140×70 px 이동했다. 실제 우하단 반전, 드래그/스크롤/화면 해제 시 닫힘과 내용 변경을 확인했다. 순수 배치 검사는 100%·125%·150%, 네 모서리, 음수 X 작업 영역의 12개 조건이다.

## 글자 렌더링

Wanted Sans 내장 폰트를 유지했다. 96 DPI에서는 Display 메트릭으로 작은 글자를 픽셀에 맞추고, 더 높은 DPI에서는 Ideal 메트릭으로 윤곽을 유지한다. Grayscale을 사용해 색 번짐을 줄인다. 화면 이동에 따른 DPI 변경과 툴팁·직접 그리는 차트 텍스트에도 같은 규칙을 적용한다. 정렬·검색·필터는 글자를 14 DIP, 입력 높이를 32 DIP로 조정했다.

Display를 고배율까지 일괄 적용한 비교 렌더링은 Wanted Sans의 작은 글자가 거칠어져 채택하지 않았다. 125%·150% 검사에서는 RenderTargetBitmap 크기뿐 아니라 VisualTreeHelper.SetRootDpi로 시각 트리 DPI도 설정한다. layout-check.json에 실제 트리 DPI와 메트릭을 기록했다. 기존 배포의 배율 이미지와 검증 방식의 차이다. 실제 여러 모니터 사이를 이동시키거나 사용자의 패널에서 글자 품질을 확정한 검사는 아니다.

## 결과와 산출물

Release 빌드 경고 0·오류 0, 계산·저장 141개 통과. 배포 EXE smoke 종료 코드 0. 정렬 세 기준 왕복, 122개 카드 분류와 대표 8개 마감, 실제 팝업 이동/반전/닫힘, 12개 모서리 배치, 7개 레이아웃 조건을 통과했다. 배포 EXE의 PerMonitorV2·비레이어드 창·제목줄 제거·창 이동/리사이즈·상단 버튼 클릭 영역을 확인했다.

최소 1240×730에서 보관함 6개/2줄, 기본 1520×1000에서 20개/5줄, 1920×1080에서 30개/6줄을 유지한다. 목록 끝은 마지막 줄 두 항목을 포함해 5개/2줄이다. 장착 슬롯과 검색·정렬은 고정한다.

다크/라이트, 기본/최소/넓은 창, 125%/150%, 등급별 카드, 정렬 방향 두 상태, 그래프·모드 툴팁을 렌더링해 눈으로 확인했다. 원본은 preview 폴더의 interaction-check.json, layout-check.json, window-check.json, engine-check.json, ui-check.json과 PNG에 있다. 계산 엔진·자료 버전·저장 형식은 기존 범위다.

## 참고 근거

- 등급·분류: data/reference-catalog.json의 고정 Wiki 원본 Rarity·Class·Polarity, 개정과 해시는 data/full-source-records.json.
- 기본 구리/은/금 구분: [Warframe 공식 모딩 안내](https://support.warframe.com/hc/fr/articles/200500194-Guide-des-Mods-Utilisation-Fusion-Transmutation-Vente).
- 글자 메트릭: [Microsoft TextFormattingMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.textformattingmode?view=windowsdesktop-8.0).
- 불투명 배경과 렌더링 조건: [Microsoft ClearTypeHint](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cleartypehint).
- 시각 트리 DPI 검사: [Microsoft SetRootDpi](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visualtreehelper.setrootdpi).
