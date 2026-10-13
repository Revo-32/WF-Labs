# WF Labs v0.4 가독성·차트 적용 기록

2026-10-10. 유명 앱의 외형을 임의로 측정한 수치라고 주장하지 않고, 공식 공개 자료의 원칙과 명시된 수치를 참고했다. 폰트는 사용자 지정 Wanted Sans를 유지한다.

| 참고 자료 | 확인한 기준 | 적용 |
|---|---|---|
| [Microsoft Fluent 글자 기준](https://fluent2.microsoft.design/typography) | Windows 본문 14/20, 보조 12/16, 제목의 명확한 계층 | 본문 14, 보조 12, 장착 모드 이름 13, 주요 숫자 32 DIP |
| [Fluent 여백](https://fluent2.microsoft.design/layout) | 4 단위 기반 간격과 요소 간 관계 | 패널 안쪽 16~20, 패널 사이 16~24, 카드 간 8 DIP |
| [Figma UI3 공개 사례](https://www.figma.com/blog/behind-our-redesign-ui3/) | 작업 내용을 중심에 두고 조작 구조를 유지 | 중복 제목 제거, 작은 보관함 카드, 넓은 수치 영역 |
| [Apple 글자 지침](https://developer.apple.com/design/human-interface-guidelines/typography) | 크기·굵기·대비로 계층을 만들고 문맥에서 가독성 확인 | 얇은 작은 글자 회피, 보조 글자 대비 상향, 작은 창 렌더링 점검 |

보관함 카드는 높이 88 DIP로 효과를 숨긴다. 이름은 14 DIP이며 300ms 호버 또는 키보드 포커스에서 공식 한국어 효과를 14 DIP 설명 패널로 보여 준다. 장착 카드는 144 DIP에 이름 13/18, 효과 12/16으로 배치한다. 긴 조건은 간결하게 나누되 공식 원문은 툴팁에 보존한다. 정렬은 장착 순서와 무관하다.

결과 배경은 다크 #121212, 카드 #1C1C1C이며 라이트는 #ECECEC/#FAFAFA이다. 배경에 청색/보라색 그라데이션을 넣지 않는다. 치명타는 위키의 게임 HUD 설명에 따라 일반 흰색, 1단계 노랑, 2단계 주황, 3단계 이상 빨강 계열을 사용한다. 근거는 capacity-source-records.json의 Critical Hit revision 2805152이다. 원소 RGB는 게임의 효과에서 연상되는 색을 사용한 앱 전용 선택이며 공식 고정 색상표라고 주장하지 않는다. 물리 피해처럼 비슷한 색은 밝기·색조·범례·수치를 함께 써 구분한다. 라이트 모드 그래프는 같은 계열의 어두운 색으로 바꾼다.

도넛은 비치명 직접 피해 구성, 선은 같은 빌드에서 방어력만 0~2,700으로 변경한 직접 DPS, 산점도는 방어력/치명타 기대값을 적용한 타격별 평균 피해다. 산점도 x축은 시간 대신 타격 순서이며 무작위 표본을 꾸며 내지 않는다. 그래프 원본 값과 엔진 수치 합계/선택 지점의 일치를 smoke에서 검사한다. 작은 창에서는 그래프 카드를 세로로 쌓는다.

차트는 WPF 벡터로 데이터·크기·테마가 바뀔 때 그린다. 차트 라이브러리, 브라우저, 상시 애니메이션 루프를 추가하지 않는다. 수치 변경에 따라 축 범위가 바뀌므로 서로 다른 빌드를 비교할 때는 축 눈금도 읽어야 한다.

창은 [WindowChrome](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome)과 [Windows DWM 모서리](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners)를 사용한다. 픽셀별 알파 레이어드 창을 사용하지 않는다. 실제 HWND의 본문 크기, 상단 이동 영역, 가장자리 리사이즈, 닫기 버튼 히트 테스트와 DWM 설정을 화면 밖에서 검사한다. 전체 마우스 조작·다중 모니터 DPI·게임 병행 성능 검증을 마쳤다는 뜻은 아니다.
