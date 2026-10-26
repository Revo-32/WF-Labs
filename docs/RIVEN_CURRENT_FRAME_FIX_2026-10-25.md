# 실제 화면과 다른 과거 리벤이 캡처되는 문제

## 실제 실행 중 확인한 원인

수정 전 앱은 최신 배포 해시 `0D92CBF9FBF3AF3368480E25FE1E574067255A327C0944DA3E4A6DA70E7859DF`로 실행 중이었다. 단순히 예전 실행 파일을 열었던 문제가 아니었다.

- 기존 `GetDC(gameWindow)` 경로의 연속 캡처에는 인노뎀이 남아 있었고 일부 주변 화면은 검은 영역으로 반환됐다. 16개 프리뷰 중 뒤쪽 프레임의 차이가 0이었다.
- 같은 게임을 화면 DC의 SRCCOPY로 한 번 읽자 실제 표시 중인 시암과 인노뎀 수치가 남은 오버레이를 확인했다.
- 수정한 캡처 경로로 다시 읽었을 때는 그 사이 사용자가 이동한 함선 내부가 반환됐다.
- 따라서 이번 실패는 OCR이나 변경 감지 조건에 앞서 **클라이언트 DC가 오래된 게임 렌더링을 반환하는 현상**이다. 저장된 이미지 재생 검사만으로 실시간 캡처의 최신성까지 확인했다고 판단하면 안 된다.

증거는 로컬 `artifacts/riven-stale-live-2026-10-25/observed/`, `desktop-once.png`, `corrected/game.png`에 있다. 게임 입력·창 활성화·데스크톱 전환은 하지 않았다.

## 수정

- 게임의 화면상 클라이언트 좌표를 `ClientToScreen`으로 구하고, `GetDC(NULL)`에서 해당 영역만 읽는다. 전체 크기 OCR 캡처와 640px 프리뷰가 동일한 화면 소스를 사용한다.
- `CAPTUREBLT`를 사용하지 않는다. 이전 Discord 간섭 관련 수정 중 창 순서 유지·변화 없는 위치 갱신 생략은 그대로 유지한다.
- 캡처 전후 전경·최소화 상태를 확인한다. 캡처 도중 창 위치나 크기가 달라져도 이미지를 폐기한다.
- 다른 모니터에서 원점 밖 좌표를 가진 창도 실제 화면 좌표로 처리한다.
- 추가로 검수 프로세스가 실제 앱의 named mutex를 점유하던 문제를 수정했다. `--check`/smoke/preview 실행은 실제 앱의 중복 실행 잠금·깨우기 이벤트를 만들지 않는다. 이전에는 검수 중 일반 앱 실행이 조용히 종료될 수 있었다.

공식 API 근거: [GetDC](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc)는 NULL일 때 화면 DC를 반환한다. [BitBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt)의 SRCCOPY는 지정 영역을 복사하며 CAPTUREBLT는 layered window 포함 옵션이다. 이 문서가 모든 GPU 게임에서의 오래된 화면 현상을 보장하거나 설명한다고 주장하지 않는다. 과거 화면 반환은 위 실제 비교에서 관찰한 현상이다.

## 검증 범위

- 리벤 OCR·UI·전환 회귀 64개 통과. 기존 61개에 네이티브 창의 원점 밖 좌표, DPI 적용 실제 크기, 창 이동 뒤 좌표 갱신을 추가했다.
- 격리 데스크톱은 실제 모니터에 표시되는 프레임버퍼가 없으므로, 그곳에서 최신 화면 캡처가 검증됐다고 계산하지 않는다. 캡처 최신성은 실제 게임의 위 비교를 근거로 한다.
- 성유물 오버레이 회귀도 같은 캡처 변경에 따른 별도 검사를 수행한다.
- 실제 클라이언트의 양쪽 오버레이 문자열을 읽기 전용 UI Automation으로 관찰하는 `tools/observe-riven-client.ps1`을 추가했다. 명시적으로 실행할 때만 제한된 횟수로 관찰하며 클릭·키 입력·활성화를 하지 않는다.

## 최종 실행 검수

- 전체 Core 1,587개 통과, 실패 0개. 전체 WPF smoke 통과. 성유물 오버레이 회귀 통과. 결과는 같은 artifacts 디렉터리의 `core.log`, `smoke/ui-check.json`, `relic/checks.json`에 기록했다.
- 실제 앱 PID 23652를 `--background`로 실행했다. 실행 경로는 기존 `artifacts/riven-auto-side-app/WFLabs.exe`이며 사용자 창 활성화·키 입력 없이 시작했다.
- 일반 게임 화면에서 상태가 `리벤 카드 대기 · 자동 인식 켜짐`으로 돌아오고 실제 양쪽 창이 0개임을 UI Automation으로 확인했다. `client-observation/client.jsonl` 참조.
- 게임에서 두 리벤을 직접 바꾸는 조작은 사용자에게 요청한 상태다. 이 문서 작성 시점에는 그 연속 전환을 확인했다고 기록하지 않는다.
- 마지막 실제 확인에서는 사용자가 임무 전투 중이었다. 새 캡처가 해당 최신 전투 화면을 반환하고 리벤 패널이 없는 것도 확인했다(`current-final/game.png`). 실전 리벤 간 연속 전환은 사용자가 임무 중이므로 아직 확인하지 못했다. 추가 검수 캡처·관찰 루프는 모두 종료했고, 수정된 일반 앱만 실행 중이다.
- 최종 빌드: `artifacts/riven-current-frame-release-app/WFLabs.exe`.
- 기존 실행 경로의 DLL과 위 빌드 SHA-256 일치: `E1A325FE750CE982A457F9A9D805913B888B47EA00262595A6417BA71C7BCE7C`.
- 교체 전 실행 폴더는 `artifacts/riven-auto-side-before-current-frame-2026-10-25/`에 보존했다. 사용자 설정을 초기화하지 않았다.
