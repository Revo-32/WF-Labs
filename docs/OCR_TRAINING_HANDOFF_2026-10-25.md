# OCR 학습 재개 기록

후속 재개 완료: 같은 날 한국어 공용 모델을 실제 추가 학습하고 제품에 연결했다. 이 파일 아래는 중지 당시의 기록이다. 현재 실행본·결과·한계는 `docs/OCR_TRAINED_MODEL_2026-10-25.md` 및 `Assets/Ocr/wfkor.model.json`을 기준으로 확인한다. 미검증 상태였던 팽·시암 회귀는 최종 리벤 78개 검사에서 해결됐다.

사용자 요청으로 2026-10-25 작업을 중지했다. 다음 재개 시 성유물·리벤 양쪽에 활용하는 한국어 OCR 추가 학습을 우선한다. 자동 재개나 예약은 설정하지 않았다.

## 현재 실행본과 변경 상태

- 실제 실행 중인 앱은 `artifacts/riven-auto-side-app/WFLabs.exe`, PID 27412. `riven-range-fixed-app`과 동일한 self-contained 빌드이며 사거리 단위 생략 수정과 창 재생성 방지까지 적용됐다.
- 그 빌드의 Core 1,589개, 리벤 UI 68개, WPF smoke는 통과했다. 히루도 사거리 2m가 정상 인식됐다.
- 이후 팽 스크린샷의 접합 상피 141.8%, 전기 140.7%, 마무리 일격 피해 −76.1% 실패를 분석했다. 현재 실행본에는 이 후속 수정이 아직 배치되지 않았다.
- 후속 소스에는 한국어 명칭 사전 보강, 긴 명칭의 유일한 한 글자 오류 보정, 한글/영문 숫자 별도 판독, 줄 중앙을 이용한 세로 분리, 테두리·떨어진 장식 제거가 들어 있다. 숫자나 부호를 추측해서 보정하지 않는다.
- `riven-labels-tight-app`에서 팽 전체/잘라낸 이미지 둘 다 세 수치가 교차 확인됐다. 뒤의 전체 리벤 회귀에서 시암 첫 수치가 잘리는 문제가 발견되어 고정 가로 inset을 0으로 바꾼 빌드는 `artifacts/riven-labels-unclipped-app`이다. 최종 검증 완료나 배포 완료로 취급하지 않는다.
- 사용자 중지 요청에 따라 진행 중이던 Core 전체 검사 PID 15276, WPF smoke PID 18104를 종료했다. 일반 앱은 종료하지 않았다. 테스트가 중단됐으므로 최종 후속 소스의 전체 회귀 통과를 주장하지 않는다.
- 중지 직전 생성된 `unclipped-ui/error.txt`도 시암 교차 확인 실패를 기록했다. 고정 inset 제거만으로 해결되지 않았으므로 다음 재개 때 기존 성공 판독 경로와 비교해야 한다.

## 파일과 증거

- 팽 원본: `artifacts/riven-labels-2026-10-25/fang.png`.
- 히루도 원본: `artifacts/riven-range-2026-10-25/hirudo.png`.
- 기존 실제 인노뎀: `artifacts/riven-auto-2026-10-25/innodem-real.png`.
- 기존 시암/목록: `artifacts/riven-refresh-2026-10-25/siam.png`, `inventory.png`.
- 단계별 OCR 원문, 이미지, 판독 결과: `artifacts/riven-labels-2026-10-25/`의 before/alias-only/clean/debug/rows/normalized/native/latin/tight 및 ui/unclipped-ui.
- 한국어 명칭의 공식 모드 표기 근거: `data/riven-ocr-ko-evidence.json`. Public Export 한국어 모드 설명과 해시를 보존했다. 리벤 전용 원문 데이터라고 혼동하지 않는다.
- 관련 변경: `RivenScan.cs`, `RivenOcr.cs`, `RivenWatchState.cs`, `RivenCaptureNavigation.cs`, `RivenOverlay.cs`, `RivenRefreshChecks.cs`, `RivenScanChecks.cs`.

## 내일 학습 방향

1. 실제 스크린샷의 글자 줄과 정확한 정답을 짝지어 성유물·리벤 공용 학습/평가 목록을 만든다. 이름, 숫자, 소수점, 부호, %, ×, 잠금 아이콘, 흰색/보라색 글자를 포함한다.
2. 동일 원본의 확대·축소·색상 변형은 같은 분할에 묶어 학습/평가 데이터 누출을 막는다. 평가에는 학습에 사용하지 않은 카드/보상 화면을 남긴다.
3. 게임 폰트를 확보하면 합성 데이터에 활용할 수 있지만, 현재 폰트를 추출하거나 모델을 학습한 상태는 아니다. 실제 게임의 배경·안티앨리어싱·효과를 담은 화면도 필요하다.
4. Tesseract 추가 학습은 float 모델을 기반으로 검토한다. 현재 배포된 모델의 종류와 학습 가능 여부부터 확인한다. https://github.com/tesseract-ocr/tesstrain 및 https://github.com/tesseract-ocr/tessdoc/blob/main/tess5/TrainingTesseract-5.md 참고.
5. 문자 오류율 외에 성유물 이름 전체 일치율, 리벤 옵션/수치/부호 전체 일치율, 잘못된 자동 확정 비율, 인식 지연을 기존 모델과 비교한다. 공용 글자 인식 모델을 쓰더라도 성유물/리벤의 영역 탐지·의미 검증은 각각 유지한다.
6. 모델 교체 전 팽·히루도·시암·인노뎀 및 실제 성유물 화면 회귀, 오버레이 유지/종료, 최종 Core·UI 검증을 수행한다.

사용자의 게임 입력·포커스를 건드리지 않는다. 새 영구 프로젝트가 필요하면 E:/CODEX/Projects, 실험은 E:/CODEX/Workspaces를 사용한다. .NET은 E:/CODEX/Tools/dotnet8/dotnet.exe를 사용하고 기존 미커밋 변경을 보존한다.
