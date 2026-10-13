# WF Labs · Vector Core

세 개의 연결된 획으로 W와 F를 한 심볼 안에 구성했다. W의 마지막 대각선을 F의 기둥으로 공유하고, 시안색 가로획을 벡터·신호의 시각적 기준으로 사용한다. 로터스, 무기, 조준선, 육각형 배지 또는 공식 Warframe 이미지를 사용하지 않는다.

## 파일

| 파일 | 용도 |
| --- | --- |
| `wf-labs-symbol.svg` | 투명 배경 심볼 · 어두운 배경용 |
| `wf-labs-horizontal.svg` | 투명 배경 가로형 로고 · Wanted Sans SemiBold 아웃라인 |
| `wf-labs-icon.svg` | 256×256 정사각형 앱 아이콘 · Navy 타일, 투명 모서리 |
| `png/wf-labs-icon-{16,32,48,64,128,256}.png` | 요청한 여섯 해상도 · RGBA |
| `wf-labs.ico` | 동일한 여섯 해상도를 포함한 Windows 아이콘 |
| `wf-labs-symbol-light.svg` | 밝은 배경용 심볼 · Navy / Steel Blue |
| `wf-labs-horizontal-light.svg` | 밝은 배경용 가로형 로고 |
| `png/wf-labs-horizontal.png` | 가로형 로고 PNG 미리보기 · 투명 배경 |
| `preview/wf-labs-brand-sheet.png` | 디자인·실제 픽셀 크기 비교표 |
| `preview/render-check.html` | SVG·PNG를 브라우저에서 비교하는 검사 페이지 |
| `preview/asset-check.json` | PNG 치수·ICO 프레임·픽셀 일치 검사 결과 |

## 사용 기준

- 기본 색: Void Navy `#0C131D`, Ice White `#F0F5F8`, Electric Cyan `#64D9F2`, Steel Blue `#253547`.
- 심볼은 정사각형 viewBox `256×256`, 가로형은 `652×256`. 비율을 고정해서 확대·축소한다.
- 벡터는 모두 채워진 경로이며 외부 이미지·폰트·필터·그라디언트 의존성이 없다. 기본 심볼과 가로형 SVG 배경은 투명하다.
- 16px PNG는 획 두께와 내부 여백을 보정한 별도 마스터다. 작은 앱 아이콘은 SVG 자동 축소보다 제공한 PNG/ICO를 사용한다. 32px 이상은 기본 벡터 비율을 유지한다.
- ICO의 16·32·48·64px 프레임은 32-bit DIB와 AND 마스크, 128·256px 프레임은 PNG이다. 각 프레임을 디코딩해 해당 PNG와 픽셀을 대조한다.
- 어두운 배경에는 기본 SVG, 밝은 배경에는 `-light.svg`를 쓴다. 외곽 글로우나 테두리를 추가하지 않는다. 심볼의 기존 viewBox 여백을 유지한다.
- 워드마크는 프로젝트에 이미 포함된 Wanted Sans SemiBold에서 변환했다. SVG 사용 환경에 폰트를 설치할 필요가 없다. 폰트 원본의 라이선스·출처는 `../../Fonts/OFL.txt`, `../../Fonts/SOURCE.txt`에 보존한다.

## 재생성

프로젝트 루트에서 다음 명령을 실행한다.

```powershell
& 'E:/CODEX/Tools/dotnet8/dotnet.exe' run --project tools/BrandAssets -c Release -- 'E:/CODEX/Projects/WFLabs'
```

생성기는 Windows WPF와 저장소의 기존 폰트만 사용하며 추가 패키지가 필요 없다. 디자인 원본은 `tools/BrandAssets/Program.cs`의 세 경로와 팔레트이다. 앱 상단은 생성된 SVG 경로를 직접 읽으므로 별도 심볼 복제본을 유지하지 않는다.

## 앱 연결

- `TennoForge.Desktop.csproj`의 `ApplicationIcon`에 ICO를 지정해 실행 파일 리소스에 포함한다.
- `WindowFrame.Attach`에서 같은 ICO를 각 실제 앱 창에 지정한다. Windows 작업 표시줄과 창 전환에 사용된다.
- `Navigation.cs`에서 문자 기호 대신 `BrandIcon.CreateSymbol()`을 사용한다. WPF 벡터로 렌더링하며, 다크·라이트 테마 전환에는 전용 브랜드 브러시를 적용한다.
- 기존 계산식과 모딩 기능, 피해 유형 색상 및 금색 강조는 유지한다.

검증 범위는 프로젝트 루트의 `docs/BRAND_VALIDATION.md`에 기록한다.
