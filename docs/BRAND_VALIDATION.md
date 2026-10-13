# Vector Core 브랜드 자산 검증

검증일: 2026-10-12. 이번 변경은 WF Labs의 심볼, 워드마크, 앱 아이콘과 해당 연결 코드에 한정한다.

## 결과

| 항목 | 결과 |
| --- | --- |
| 투명 심볼 / 가로형 로고 | SVG 생성, 외부 폰트·이미지 의존성 없음 |
| 밝은 배경용 버전 | Navy / Steel Blue 조합으로 별도 SVG 제공 |
| 정사각형 아이콘 | SVG 및 여섯 크기 PNG 생성 |
| 16px 보정 | 획 두께·내부 여백·시안 가로획 보정 |
| PNG 크기 | 16 / 32 / 48 / 64 / 128 / 256px 확인 |
| ICO | 여섯 프레임을 디코딩해 PNG 픽셀과 대조, 모두 일치 |
| EXE 리소스 | 개발 빌드와 self-contained 배포 빌드의 RT_GROUP_ICON/RT_ICON을 원본 ICO와 바이트 단위 대조, 모두 일치 |
| Windows 아이콘 추출 | `Icon.ExtractAssociatedIcon`으로 실행 파일의 32px 아이콘 추출·육안 확인 |
| 브라우저 SVG | Chrome에서 기본/밝은 배경용 SVG와 실제 크기 PNG, 투명 모서리 렌더링 확인 |
| Release 빌드 | 성공, 경고 0 / 오류 0 |
| 기존 계산 회귀 검사 | 908개 통과 / 0개 실패 |
| 전체 UI smoke | 종료 코드 0, 검사 보고서 10개 모두 `passed: true`, 오류 파일 없음 |
| UI 확장 상호작용 | 191개 검사 통과 |
| 레이아웃 | 7개 창 크기·배율·보관함 스크롤 사례 통과 |
| 배포 실행 | self-contained `WFLabs.exe --accuracy-preview ... weapon-buffs` 직접 실행, 종료 코드 0 |

UI smoke는 PNG 153장을 생성했다. 이 중 브랜드 적용과 직접 관련된 최소 1760×920 창, 다크/라이트 테마, 125%/150% 배율 화면을 열어 상단 심볼, 메뉴 간격과 잘림 여부를 확인했다. 모든 153장을 개별 육안 검수했다는 의미는 아니다. 브라우저 확대 검사에서 발견한 획 연결부의 안티앨리어싱 경계는 같은 색 획을 겹치도록 수정해 제거했다.

## 통합 변경

- `src/TennoForge.Desktop/Assets/Brand/`: SVG, PNG, ICO, 색상·크기 사용 안내 및 디자인 비교표.
- `tools/BrandAssets/`: .NET 8 WPF 생성 도구와 표준 라이브러리 기반 PE 아이콘 검사 도구.
- `TennoForge.Desktop.csproj`: `ApplicationIcon` 및 WPF 리소스 포함.
- `BrandIcon.cs`: SVG 경로로 표시하는 상단 벡터 심볼과 캐시된 창 아이콘.
- `Navigation.cs`: 기존 `◈` 문자 기호 교체.
- `Theme.cs`: 브랜드 전용 다크/라이트 색상 브러시 추가.
- `WindowFrame.cs`: 실제 앱 창에 동일한 ICO 적용.

계산 엔진, 무기 데이터, 모딩 흐름을 수정하지 않았다. 기존 배포 폴더는 보존하고 `builds/WF-Labs-20261012-VectorCore/`에 새 배포본을 생성했다.

## 재현과 증적

```powershell
& 'E:/CODEX/Tools/dotnet8/dotnet.exe' run --project tools/BrandAssets -c Release -- 'E:/CODEX/Projects/WFLabs'
python -X utf8 tools/BrandAssets/verify_icon.py builds/WF-Labs-20261012-VectorCore/WFLabs.exe src/TennoForge.Desktop/Assets/Brand/wf-labs.ico artifacts/brand/validation/published-icon-check.json
```

- 원본 검증 자료: `E:/CODEX/Workspaces/2026-10-10/sork/work/vector-core/`.
- 프로젝트 내부 보고서 사본과 주요 앱 화면: `artifacts/brand/validation/`.
- 자산 전달 묶음: `artifacts/brand/WF-Labs-Vector-Core.zip`.
- 배포 실행 파일: `builds/WF-Labs-20261012-VectorCore/WFLabs.exe`.

## 검증 범위

요청한 자산과 앱 연결의 미완료 항목은 없다. 렌더링 검사는 이 PC의 Windows WPF 및 Chrome 환경에서 수행했다. 물리적으로 다른 모니터 사이를 이동하는 검사와 기존 Windows 고정 바로가기의 아이콘 캐시 갱신은 수행하지 않았다. 검증을 위해 사용자의 바로가기나 작업 표시줄 설정을 변경하지 않았다.
