# 성유물 보상 인식·마켓 시세 비교

2026-10-24 · `0.10.0-preview.1` 로컬 개발본. 공개 설치·업데이트 채널의 `0.9.0-preview.1`은 변경하지 않았다.

후속 실제 한국어 화면 검수와 자동 확정 개선은 [2026-10-25 기록](RELIC_REAL_SCREENSHOTS_2026-10-25.md)을 참조한다. 아래 초기 회귀 수치와 최신 결과를 구분한다.

## 구현한 사용 흐름

1. 상단 **성유물**에서 **게임 화면 읽기**, **이미지 불러오기**, **붙여넣기**를 선택한다. PNG/JPG/BMP 파일 드롭과 이미지 Ctrl+V도 지원한다. 선택적으로 Ctrl+Shift+R 전역 단축키를 켤 수 있다.
2. 화면 왼쪽부터 최대 네 보상을 표시한다. **판매 최저 플래티넘 / 듀캇**을 큰 수치로 비교하며, 각각 높은 항목을 별도로 강조한다.
3. **이름 확인 필요** 항목은 후보 확인 버튼을 누르고 드롭다운에서 클릭하거나 방향키·Enter로 확정한다. 검색 중 자동으로 품목을 적용하지 않으며 Esc는 기존 선택으로 돌아간다. 확정된 항목도 이름 수정으로 바꿀 수 있다.
4. 원본을 펼치거나 **원본 크게 보기**에서 보상 이름 영역을 드래그하고 다시 읽을 수 있다. 보상 추가·제거, 인식 취소를 지원한다. 취소하면 이전 보상과 이미지가 유지된다.
5. 비교 후 **게임으로 돌아가기**로 게임 창을 활성화한다. 게임 내 보상을 자동 선택하지 않는다.

기본 접속 조건은 게임 중인 거래자, PC 마켓·크로스플레이 포함이다. 접속 조건·크로스플레이·단축키 설정을 재실행 후 복원한다. 이미지와 OCR 원문은 운영 중 자동 저장하거나 외부로 전송하지 않는다. 검사 모드에서만 검수 폴더에 이미지·결과를 저장한다.

## 인식과 데이터

- Tesseract 5.2.0 wrapper와 `tessdata_best` 한국어·영어 LSTM 모델을 로컬에서 실행한다. 원본·밝은 문자·색상·국소 대비와 보상 행/카드/글자 줄의 정밀 인식을 비교한다. 한국어 화면을 기준으로 검수한다. 상시 화면 감시나 전체 화면 반복 OCR은 하지 않는다.
- 품목 605개: Warframe.market 한국어 메타데이터 601개(프라임 부품·설계도 592개, 레퀴엠 8개·아야탄 앰버 별 1개)와 거래 불가 보상 4개다. 프라임 세트와 완성 무기는 보상 후보에서 제외한다.
- `data/relic-market-items.json`에 API 버전·수집 시각·원문 SHA-256을 기록했다. `tools/import_relic_catalog.py`로 재생성한다. 시세와 사용자 주문은 내장 데이터에 포함하지 않는다.
- 한글 분해 자모와 공백 정규화로 가까운 이름을 제안한다. 유사도는 정답 확률이 아니다. 정규화 이름·OCR 문자 신뢰도·경쟁 후보 차이와 여러 처리 결과를 함께 판단해 자동 확정한다. 작은 오탈자라도 품목이 명확하면 바로 시세를 조회하며, 불확실한 항목만 사용자가 선택한다.
- 줄바꿈 이름을 합치고 보상 행의 좌우 순서를 유지한다. 같은 품목이 여러 보상에 나오면 별도 카드로 남긴다. 읽지 못한 이름도 같은 보상 행에 있으면 수정 가능한 칸으로 유지한다.

## 가격의 의미와 오류 처리

Warframe.market v2 공개 읽기 API를 사용한다. [주문 API](https://docs.warframe.market/docs/api/orders/)와 [모델 정의](https://docs.warframe.market/docs/data-models/)를 기준으로 품목 ID, 공개 여부, 재고, 접속 상태, 플랫폼·크로스플레이, 레퀴엠 랭크·충전을 필터링한다. 동일 판매자의 중복 주문을 제거하고 `perTrade`가 있으면 개당 가격으로 환산한다.

- 기본 표시: 등록된 **판매 최저 호가**. 거래 완료 가격이나 판매 보장이 아니다.
- 상세 표시: 구매 최고 호가, 저가 판매자 최대 5명의 중앙값, 판매·구매 인원 수, 조회 시각, 캐시·오류 상태, 마켓 바로가기.
- 레퀴엠은 현재 API의 `maxRank=3`과 주문의 `rank=3`을 연결한다. 충전 필드가 제공되는 품목은 해당 필드도 필터링한다. 랭크 조건을 상세에 표시한다.
- 주문 없음·확인 전·조회 실패는 `—`, 포르마 등은 `거래 불가`로 표시한다. 가격이 없는 항목을 0플래티넘으로 오인시키지 않는다.
- 가격 캐시 90초, 품목 목록 24시간. 연결 실패 시 최대 24시간 이내 이전 가격에 **오래된 시세**와 원래 조회 시각을 표시한다. 오래된 가격은 최고 가격 강조에서 제외한다.
- 전역 요청 간격 400ms, 같은 품목 동시 조회 합치기, 타임아웃, 429/509와 Retry-After 처리를 적용한다. [공식 API 규칙](https://docs.warframe.market/docs/rules/overview/)의 초당 3회보다 낮게 제한한다.
- 계정 로그인, 거래 게시, 주문 수정, 채팅 발송은 포함하지 않는다.

## 초기 구현 검증 결과 · 최신 실제 화면 검수는 후속 문서 참조

| 검사 | 결과 | 근거 |
|---|---|---|
| .NET 8 Release 빌드·Windows x64 self-contained publish | 통과 | `artifacts/relic-build-publish.log`, `relic-publish-final.log` |
| 전체 계산 회귀 + 초기 성유물 검사 | 1,534개 통과, 실패 0 | `artifacts/relic-full-core.json` / `.log` |
| 이후 보강한 성유물·마켓 집중 검사 | 20개 통과, 실패 0 | `artifacts/relic-core-expanded.json` |
| 최종 실행 파일의 실제 WPF 버튼·후보 선택·취소·필터·오프라인·확대 검사 | 65개 통과 | `artifacts/relic-published-final-02/checks.json` |
| 글꼴 3종 × 테마 2종 × WPF DPI 3종 | 18상태, 배치 오류 0 | `artifacts/relic-published-final-02/layout.json` |
| 기존 화면 포함 배치 회귀 | 375상태, 오류 0 | `artifacts/relic-regression/layout-probe/issues-*.json` |
| 기존 화면 포함 테마·스크롤·버튼 간격 | 198화면 통과 | `artifacts/relic-regression/ui-audit-check/ui-audit-*.json` |
| 기존 전체 UI smoke | 통과 | `artifacts/relic-regression/smoke/ui-check.json` |
| 실제 v2 한국어 목록 + 프라임 세 품목 호가 조회 | 통과 | `artifacts/relic-published-relic-online-check/checks.json` |
| OCR 모델·라이선스 해시 / 네이티브 DLL·.NET 구성요소 | 13개 확인 통과 | `artifacts/relic-payload-verification.json` |

전체 1,534개에는 기존 계산 1,519개와 초기 성유물 15개가 포함된다. 이후 미인식 슬롯 순서·레퀴엠 랭크·동시 조회·응답 오류·429 검사를 보강하여 성유물 집중 검사 20개를 별도 실행했다. 중복 실행 수를 서로 다른 테스트로 합산하지 않는다.

OCR 평가는 **합성 한국어 보상 화면** 4장(1280×720/1개, 1920×1080/2개, 2560×1440/3개, 3440×1440/4개)에서 실제 엔진을 실행했다. 총 10개 품목의 최상위 후보 ID가 정답과 일치했다. 불확실한 후보는 자동 확정되지 않으며, 이 결과를 실제 게임 인식률 100%로 해석하지 않는다.

실제 Windows 마우스 입력으로 확대 원본의 영역 선택 → 재인식을 검수했고 전경 WPF 창을 네이티브 GDI로 캡처했다. PNG 파일을 실제 WIC로 디코딩한 뒤 OCR하는 경로에서 발생한 스레드 소속 오류와, 초점 이탈 후 미확정 OCR 이름이 검색창에서 사라지는 문제를 수정했다. 취소 시 이전 이미지 복원, 오래된 비동기 응답의 덮어쓰기 방지, 전역 단축키 충돌·해제·재등록도 확인했다. 파일 선택·클립보드 입력 공급은 검사에서 주입하여 사용자의 실제 클립보드를 변경하지 않았다.

## 확인하지 못한 환경과 공개 배포 전 조건

- 초기 구현 시에는 게임 화면을 확보하지 못했으나 후속 검수에서 공개된 실제 한국어 보상 장면 4개·16칸을 검사하고 개선했다. [실제 화면·자동 확정 기록](RELIC_REAL_SCREENSHOTS_2026-10-25.md)에서 결과와 범위를 구분한다. 실행 중 게임 복귀·독점 전체화면/HDR·물리 DPI 모니터 이동과 모든 테마·UI 배율의 일반 인식률은 여전히 별도 검증이 필요하다.
- 캡처는 전경 게임 창의 클라이언트 영역을 사용한다. 전경 전환 실패 시 다른 앱을 대신 읽지 않고 오류를 표시한다. 게임에서 정상 캡처되지 않으면 이미지 불러오기·붙여넣기·영역 선택을 사용할 수 있다.
- OCR 네이티브 엔진은 Microsoft Visual C++ 2015–2022 x64 런타임이 필요하다. 현재 PC에서 실행은 확인했으나 런타임이 없는 새 Windows 설치에서 자동 설치까지 검증한 것은 아니다. 누락 시 한국어 오류를 표시하고 수동 품목 검색은 유지한다.
- [Warframe.market 이용약관](https://warframe.market/tos)의 API 사용 앱 공개 전 운영진 사전 연락 조건을 배포 전에 해결해야 한다. 외부 운영진에 메시지를 보내거나 이 기능을 공개 배포하지 않았다.

로컬 검수 실행 파일: `artifacts/relic-app-0.10.0-preview.1/WFLabs.exe`. .NET 런타임·OCR 모델·네이티브 DLL은 같은 폴더에 포함되어 있으므로 EXE만 따로 이동하지 않는다. 공개 설치 파일 생성·GitHub 업로드·업데이트 feed 변경은 이번 기능 구현에서 수행하지 않았다.

## 주요 구현 위치

- `src/TennoForge.Core/RelicRewards.cs`: 품목 검색·이름 판정·시세 집계
- `src/TennoForge.Core/RelicMarketClient.cs`: v2 API·캐시·조회 제한
- `src/TennoForge.Desktop/RelicOcr.cs`, `RelicCapture.cs`: 로컬 OCR·게임 캡처·단축키
- `src/TennoForge.Desktop/RelicPage.cs`, `RewardSearchBox.cs`: 비교·교정·원본 영역 UI
- `src/TennoForge.Desktop/RelicNavigation.cs`, `RelicPreferences.cs`: 탭·게임 복귀·설정 복원
- `tests/TennoForge.Checks/RelicChecks.cs`, `src/TennoForge.Desktop/RelicChecks.cs`: 계산·UI·실제 API 검사

모델과 네이티브 라이선스는 `src/TennoForge.Desktop/Assets/Ocr/`, 마켓 데이터 고지는 `data/WARFRAME-MARKET-NOTICE.txt`에 보존한다.
