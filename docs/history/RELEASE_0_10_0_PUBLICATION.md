# WF Labs 0.10.0-preview.1 공개 배포

2026-10-26 12:01:12 KST, [v0.10.0-preview.1](https://github.com/Revo-32/WF-Labs/releases/tag/v0.10.0-preview.1)을 공개 미리보기로 발행했다. 자체 소스는 공개 열람용이며 별도의 재사용 라이선스를 부여하지 않았다. 기존 타사 자료·폰트·모델·런타임 고지를 유지한다.

## 소스와 검증 기준

- 배포 태그와 설치 파일 코드: `3ca46ea6368a878185cb23ae682c9b88125a87dd`.
- [릴리즈 준비 Windows 전체 CI](https://github.com/Revo-32/WF-Labs/actions/runs/40256658536): 계산·데이터 1,707개, 실제 WPF 화면·상호작용, 설치 파일 생성·패키지 무결성 검사 모두 통과.
- [공개 main Windows 전체 CI](https://github.com/Revo-32/WF-Labs/actions/runs/40260387768): `ad5866f8cab64ba92b41f0bc9f177f477fba7930`에서 모든 단계 통과. main 반영 뒤 검증을 실행했다.
- [소스 PR #1](https://github.com/Revo-32/WF-Labs/pull/1)을 main에 병합했다. 준비 단계의 실패와 수정, 설치 전이 QA·환경 제한은 [검수 기록](../RELEASE_0_10_0_VERIFICATION.md)에 보존한다.

## 공개 파일과 온라인 업데이트

인증 없는 GitHub API로 공개 상태(`draft=false`, `prerelease=true`), 태그의 코드 커밋, 공개 자산 7개의 크기와 SHA-256을 확인했다. [Windows 설치 파일](https://github.com/Revo-32/WF-Labs/releases/download/v0.10.0-preview.1/WFLabs-win-Setup.exe)의 익명 요청은 HTTP 200이며 공개 `releases.win.json`은 버전 0.10.0-preview.1의 전체 업데이트 패키지를 가리킨다.

앱과 동일한 Velopack 1.2.161 `GithubSource`로 인증 토큰 없이 0.9.0-preview.1 → 0.10.0-preview.1을 검색하고, 전체 업데이트 패키지 **134,597,990바이트**를 실제 다운로드했다. SHA-256은 공개 feed 및 로컬 검증본과 일치한다. 현재 버전이 0.10.0-preview.1이면 추가 업데이트가 없는 것으로 판정했다.

온라인 확인은 `TestVelopackLocator`로 설치 버전 정보만 주입하는 별도 진단 프로그램이다. 사용자 설치본·작업 파일·자동 시작 설정은 변경하지 않았으며 실제 사용자 앱을 종료하거나 재시작하지 않았다. 설치·적용·재시작·작업 복원은 앞선 격리 설치 전이 QA로 확인했다.

| 파일 | 바이트 | SHA-256 |
|---|---:|---|
| `assets.win.json` | 201 | `8ccf785b2bd90dd8b62373d84df8100fd987d49f6325fbfd5de461906eea17b0` |
| `RELEASES` | 88 | `b0bd5ad348934d87d1c49a3f2673642f163926fb4dbc1881512a8b77feddd91e` |
| `releases.win.json` | 10,462 | `ed0d5b7f92196def9eae23e6192a03bc202a54848f14ce9a72189b3087ca8f27` |
| `SHA256SUMS.txt` | 525 | `8b7424d3500f8f8331511683f17415f991ac794fc9a58c1d56b209727bdccc2b` |
| `WFLabs-0.10.0-preview.1-full.nupkg` | 134,597,990 | `5c380435d0ba35cc95cafcbb9ef4768304fe6d591475b1cee672b97440707978` |
| `WFLabs-win-Portable.zip` | 134,580,969 | `249d20cc0feedcf4707be60c0cd87c2b42e8ad08953c8aa392f183ceef86df70` |
| `WFLabs-win-Setup.exe` | 142,155,110 | `c5ad7de15b77ab23342e4dabb43cb0d6eca7ef29e256707a9f41f34b2e5416df` |

## 사용자 업데이트 경로

기존 설치형 사용자는 앱 상단 **업데이트 → 확인/다운로드 → 설치 후 다시 시작**으로 갱신한다. 자동 확인·다운로드가 켜진 경우에도 적용은 사용자가 선택한다. 새 사용자는 설치 EXE를 사용한다. Portable 배포본은 설치형 자동 업데이트 대상이 아니다.

설치 파일은 Authenticode 미서명 상태다. 실제 게임 병행 성능·물리 모니터 이동·실제 계정 인벤토리의 연속 동기화는 별도 환경 검증이 필요하다. OCR과 계산·일정의 범위 및 근사는 [릴리즈 노트](../RELEASE_NOTES.md)와 앱의 설명을 따른다.
