# Windows 배포 준비

## 1.0.0-rc.4 출시 검수

2026-10-27. 최신 로컬 후보는 `1.0.0-rc.4`다. 사용자 연속 플레이 검증과 별도로 설치형·압축형 내용 일치, 최상위 실행기, 배포 계산 DLL, 업데이트·재시작·작업 보존을 검사한다. 현재 공개 릴리즈를 변경하거나 정식 1.0을 발행한 상태는 아니다. [RC4 출시 검수 기록](RC4_RELEASE_CHECK_2026-10-27.md)을 따른다.

압축본은 폴더 전체를 풀어 `WF Labs.exe`를 실행한다. `current/sq.version`과 `Update.exe`는 필수 구성요소다. 앱 EXE만 따로 복사하지 않는다. `tools/verify-package.py`는 실행 메타데이터·버전·모델 고지와 설치형/압축형 모든 앱 파일의 해시를 검사한다. `tools/verify-packaged-app.ps1`는 ZIP에서 꺼낸 최상위 EXE로 실제 창을 열고, OCR·업데이트·전체 UI와 배포본 Core DLL을 검사한다. 두 GitHub Windows/배포 초안 workflow는 이를 통과해야 패키지를 업로드한다.

## 이전 1.0.0-rc.1 준비 기록

2026-10-27, 최신 기능을 통합한 출시 후보의 전체 검수·설치 파일을 준비했다. [출시 준비와 실제 환경의 남은 항목](RELEASE_READINESS_2026-10-27.md), [사용·문제 해결](SUPPORT.md), [데이터 처리](PRIVACY.md)를 참조한다. GitHub의 현재 공개 버전은 아래 0.10.0-preview.1이며 RC를 공개 발행한 것은 아니다.

## 0.10.0-preview.1 공개 미리보기

[공개 배포·익명 다운로드·온라인 업데이트 검증 결과](history/RELEASE_0_10_0_PUBLICATION.md)

2026-10-26, [0.10.0-preview.1](https://github.com/Revo-32/WF-Labs/releases/tag/v0.10.0-preview.1)을 공개 미리보기로 배포한다. [0.10.0 검수 기록](RELEASE_0_10_0_VERIFICATION.md)과 [릴리즈 노트](RELEASE_NOTES.md)를 참조한다. 설치 프로그램은 .NET 런타임 외에 OCR의 Visual C++ x64 런타임도 확인한다. 아래 버전별 기록은 과거 배포의 검수 이력이다.

앱 표시 이름은 **WF Labs**, GitHub 저장소는 `https://github.com/Revo-32/WF-Labs`다. 소스 공개 및 설치·업데이트 배포를 같은 저장소에서 관리한다. 자체 코드는 공개 열람용이며 별도의 재사용 라이선스를 부여하지 않는다. `COPYRIGHT.md`를 참조한다.

## 설치와 실행

Windows x64용 `WFLabs-win-Setup.exe`를 실행하면 사용자 계정 범위에 설치한다. .NET 8과 Windows Desktop Runtime을 포함하므로 사용자가 개발 SDK를 설치할 필요가 없다. `WFLabs-win-Portable.zip`은 압축 배포본이며 압축을 푼 최상위의 `WF Labs.exe`로 실행한다. WF Labs의 앱 내 업데이트 버튼은 설치형 버전에서 제공한다.

현재 공개 버전은 [0.10.0-preview.1](https://github.com/Revo-32/WF-Labs/releases/tag/v0.10.0-preview.1)이다. 기존 설치본은 상단 **업데이트 → 확인/다운로드 → 설치 후 다시 시작**으로 갱신한다. 자동 확인·다운로드를 켜 놓았다면 다운로드 완료 뒤 설치를 선택하면 된다.

상단 **업데이트** 또는 **설정 → 앱 실행 · 업데이트**에서 Windows 로그인 자동 시작과 Warframe 실행 자동 시작을 각각 켜고 저장한다. 기본값은 모두 꺼져 있다.

- Windows 자동 시작: HKCU Run의 `WF Labs` 항목으로 작은 `WFLabs.Startup.exe --login` 도우미가 앱을 실행한다.
- Warframe 자동 시작: 도우미가 동일 Windows 세션의 `Warframe.x64` / `Warframe` 프로세스 이름을 3초 간격으로 확인한다. 게임 메모리나 화면을 읽지 않는다. 이를 사용하면 앱 자동 시작을 꺼도 도우미는 로그인 시 시작한다.
- 게임이 실행되는 전이에서 한 번 실행한다. 게임이 켜진 상태에서 WF Labs를 수동 종료했다고 계속 다시 띄우지 않는다.
- 설정 해제·업데이트·제거 때 해당 설치본이 소유한 시작 항목/도우미를 처리한다. 다른 위치의 설치본에 연결된 시작 항목을 임의 삭제하지 않는다.

설정은 `%LocalAppData%\WF Labs\client-settings.json`에 저장한다. 빌드 파일과 리벤 작업 파일은 사용자가 저장한 위치를 유지한다.

## 업데이트

Velopack 1.2.161을 고정해 사용한다. 설치본의 `release-config.json`에 공개 GitHub 저장소를 연결해 새 Release 확인 → 다운로드 → 현재 작업 보관 → 적용/재시작으로 진행한다. 토큰을 클라이언트에 내장하지 않는다.

자동 확인·자동 다운로드는 기본적으로 켜져 있다. 앱 시작 3초 후와 사용 중 4시간마다 확인한다. 설정에서 각각 끌 수 있으며 자동 확인을 꺼도 수동 확인은 가능하다. 중복 요청은 실행하지 않고, 설정창을 닫아도 다운로드를 유지한다. 상단 버튼은 새 업데이트·다운로드 진행률·설치 준비 상태를 표시한다. 사용자가 **설치 후 다시 시작**을 선택해야 적용하며 작업 중 강제 재시작하지 않는다. 완료한 다운로드는 다음 실행에서도 인식한다. 개발·압축 버전은 설치형 업데이트 대상에서 제외한다.

기본 주소는 `https://github.com/Revo-32/WF-Labs`다. 미리보기 패키지는 공개 미리보기도 확인한다. 공개 전 draft Release와 비공개 저장소는 익명 업데이트 대상이 아니다. 첫 Release가 공개되기 전에는 온라인 최신 버전 확인이 실패할 수 있으며 현재 설치본은 계속 사용 가능하다.

업데이트 전 모딩 빌드와 비교 기준, 리벤 작업 기록·입력 중인 텍스트·예산·비교 기준, 활성 탭·테마·경량 모드를 `update-workspace.json`에 원자적으로 보관한다. 재시작 시 복구하고 복구 파일을 `.restored`로 남긴다. 비교 결과 자체는 새 버전의 엔진으로 다시 계산한다. 일반 옵션 고정은 세션 종료에 따라 해제한다. 유효한 빌드를 읽거나 복구 파일을 쓰지 못하면 업데이트 적용을 시작하지 않는다.

## 로컬 생성

PowerShell 7에서 저장소 루트 기준:

```powershell
./tools/validate.ps1
./tools/package.ps1 -RepositoryUrl https://github.com/Revo-32/WF-Labs
python -X utf8 tools/release-preflight.py
```

복구 프로젝트의 SDK는 `E:\CODEX\Tools\dotnet8\dotnet.exe`다. GitHub 러너 등 해당 경로가 없는 환경은 `global.json`의 .NET 8.0.424를 설치한 뒤 `-Dotnet (Get-Command dotnet).Source`를 전달한다. 새 SDK 주 버전으로 임의 변경하지 않는다.

출력에는 Setup, Portable, 전체 nupkg, `releases.win.json`, `RELEASES`, `assets.win.json`, `SHA256SUMS.txt`가 포함된다. 클라이언트 업데이트에는 Setup만이 아니라 feed와 nupkg도 같은 공개 Release에 필요하다. 현재 workflow는 전체 패키지 업데이트를 제공하며 과거 패키지를 내려받아 만드는 delta 최적화는 아직 적용하지 않았다.

## GitHub 준비 상태

- `windows.yml`: main push/PR/수동 실행에서 Release 빌드, 전체 계산 검사, 리벤 작업 흐름 검사, 전체 WPF UI 검사, 설치 파일 생성과 artifact 보관.
- `release.yml`: main에서 수동 실행하면 같은 검사 후 **draft Release**를 만든다. 자동으로 공개하지 않는다. 첫 공개 배포는 아래에 기록한 Windows Actions 검증과 로컬 설치 전이 검증을 통과한 파일을 게시했다.
- `.gitignore`: 빌드 결과, 검증 산출물, 사용자 리벤 작업, 일반적인 서명키·환경 파일을 제외한다.
- `release-preflight.py`: 포함 예정 파일의 크기와 특정 자격 증명 형식을 읽기 전용으로 검사한다. 자동 Git add/commit/push를 하지 않는다.

사용자 결정에 따라 자체 코드는 공개 열람용이며 재사용 라이선스는 부여하지 않았다. 기존 위키 데이터·폰트·WFCD·Public Export의 고지는 유지한다. 인증서가 제공되지 않아 현재 설치 파일은 Authenticode 서명이 없다.

## 설치 경로 검증

`tools/verify-installer.ps1`는 별도 패키지 ID `WFLabs.Verify`와 프로젝트 `artifacts` 안의 설치 경로를 사용한다. `-QaDirectory`로 기존 결과를 보존한 별도 폴더를 지정할 수 있다. QA 버전 A를 설치하고 실제 클라이언트의 자동 확인·다운로드 controller를 통해 로컬 feed에서 B를 적용·재시작한 뒤 제거한다. 업데이트 후 모딩 비교 기준·미완성 리벤 입력·예산·활성 탭을 복구하고 사용자 자동 실행 항목과 설정 파일이 바뀌지 않았는지 확인한다.

Velopack 1.2.161 Setup의 진단용 `-- EXE_ARGS` 전달에서 오류를 관찰해, 검증 스크립트는 설치를 마친 뒤 진단 앱을 별도로 실행한다. 일반 설치·업데이트 적용/재시작은 이 경로에 의존하지 않는다. 실제 Windows 로그아웃/로그인 및 실제 Warframe 실행을 동반한 종단 검증은 이 자동 테스트와 구분한다.

참고: [Velopack C# 통합](https://docs.velopack.io/getting-started/csharp), [GitHub 배포](https://docs.velopack.io/distributing/github-actions), [Setup 명령](https://docs.velopack.io/reference/cli/content/setup-windows).

## 0.9.0-preview.1 공개 배포와 검증

2026-10-24에 소스 `7131a3995f847d9c6ed65516479d4e89089efe9b`를 기준으로 공개했다. 최종 파일은 `builds/WF-Labs-0.9.0-preview.1-verified/`에 있다. Setup·Portable·전체 업데이트 패키지·feed·체크섬 7개를 제공하며, 공개 서버의 크기와 SHA-256이 최종 로컬 검증본과 일치한다.

[최종 Windows CI](https://github.com/Revo-32/WF-Labs/actions/runs/40068661779)에서 전체 계산·실제 WPF UI 검사와 패키지 생성·검증을 통과했다. 별도 설치 ID를 사용한 구 공개 버전 및 새 기능 QA 두 경로에서 실제 설치·업데이트·재시작·작업 복원을 검증했다. 온라인 검사는 인증 없이 `0.8.0-preview.2`에서 새 버전을 찾고 82,628,803바이트 nupkg를 다운로드해 해시를 확인했으며, 최신 버전에서 재업데이트가 제안되지 않는지 확인했다.

44.1.3 이전 규칙의 리벤을 포함한 작업은 자동 복원이 중단될 수 있다. 원본은 보존하고 패치 후 게임 표시 수치를 다시 입력해야 한다. 구형 리벤 자동 변환은 이번 배포의 완료 항목이 아니다. 설치 파일은 Authenticode 서명이 없다. 전체 검사 수치와 실제 환경 검증의 한계는 [0.9.0 배포 검증](RELEASE_0_9_0_VERIFICATION.md)에 기록한다. 아래 0.8.0 기록은 과거 배포 이력이다.

## 0.8.0-preview.2 로컬 배포 검증

최종 배포 파일은 `builds/WF-Labs-0.8.0-preview.2-win-x64/`에 있다. 설치 파일은 약 81 MiB이며 앱·.NET 8 Desktop Runtime·시작 도우미·타사 고지를 포함한다. 설치와 업데이트 메타데이터에 `win-x64`를 명시했다.

- `artifacts/distribution-build.log`: Release 빌드 경고 0 / 오류 0.
- `artifacts/distribution-core.json`: 계산 엔진 918개 통과.
- `artifacts/distribution-final-updates/`: 자동·수동 확인, 중복 요청, 다운로드·실패·취소·재시도, 창 닫기, 작업 보관 및 탐색/복원 총 25개 통과.
- `artifacts/distribution-final-riven/`: 검색·리벤 37개 통과.
- `artifacts/distribution-final-audit/`: 다크·라이트와 100/125/150%의 UI 174개 상태 통과.
- `artifacts/distribution-final-smoke/`: 전체 UI 검증 보고서 10개 통과, 렌더링 PNG 153개. 배포 폴더의 self-contained `WFLabs.exe`를 실행해 검사했다.
- `artifacts/distribution-package-verification.json`: feed/파일 SHA-256, ZIP 무결성, x64 실행 파일과 메타데이터, 내장 구성요소·고지·업데이트 주소 33개 검사 통과.
- `artifacts/distribution-installer-qa/installer-verification.json`: 격리 설치 `0.8.0-qa.3` → 자동 다운로드 → `0.8.0-qa.4` 설치/재시작 → 미완성 리벤 입력·예산·모딩 비교 기준·활성 탭 복원 → 제거 성공. 기존 사용자 설정·자동 실행 항목 보존 확인.

이 설치 전이 검사는 로컬 feed를 사용했다. GitHub Release의 익명 접근·온라인 feed 확인과 구분한다. 실제 Windows 로그아웃/로그인과 Warframe 실행, 서로 다른 실제 모니터 간 이동은 이 자동 검증에 포함하지 않았다. 인증서가 없어 Authenticode 서명은 미적용 상태다.

## 0.8.0-preview.2 공개 배포와 온라인 확인

2026-10-13, [v0.8.0-preview.2](https://github.com/Revo-32/WF-Labs/releases/tag/v0.8.0-preview.2)를 공개 미리보기로 게시했다. 자체 소스 공개와 함께 설치 파일·압축 버전·전체 업데이트 패키지·feed·체크섬 7개 파일을 제공한다. 업로드한 모든 파일의 크기와 GitHub SHA-256이 로컬 검증본과 일치한다.

- [GitHub Windows Actions](https://github.com/Revo-32/WF-Labs/actions/runs/38384138493): 배포 기준 커밋 `162828f4dd569ecb4b54c17bcc88b181fd5a089e`에서 전체 검사, 설치·업데이트 패키지 생성, 패키지 검증과 artifact 업로드 성공.
- `artifacts/distribution-public-release.json`: 인증 없이 공개 Release 조회와 설치 파일 다운로드 URL의 HTTP 200 확인.
- `artifacts/distribution-online-update.json`: 앱과 같은 Velopack 1.2.161 `GithubSource`로 인증 토큰 없이 새 버전 확인, 77,310,027바이트 전체 업데이트 패키지 다운로드와 SHA-256 일치 확인. `0.8.0-preview.1`에서는 새 버전 `0.8.0-preview.2`를 찾고, `0.8.0-preview.2`에서는 업데이트 없음으로 판정했다.

온라인 검사는 별도 진단 프로그램에서 설치 버전 정보만 `TestVelopackLocator`로 주입했다. 실제 설치본 적용·재시작·작업 복구는 위의 격리 설치 QA에서 검증했다. 온라인 검사에서 사용자의 설치본을 변경하거나 재시작하지 않았다. `release.yml` 자체의 수동 draft 생성 경로는 아직 별도로 실행하지 않았다.

[Windows 설치 파일](https://github.com/Revo-32/WF-Labs/releases/download/v0.8.0-preview.2/WFLabs-win-Setup.exe)의 SHA-256:

```text
e4fcaffcdfe423ceb6c5da2de0e015fb2b3fe5d6074f6a51af23f8f50598a8e1
```
