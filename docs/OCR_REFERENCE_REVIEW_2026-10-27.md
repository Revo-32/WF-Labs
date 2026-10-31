# OCR 독립 코드 리뷰와 공식 구현 비교 · 2026-10-27

검토 기준은 RC2의 한국어 성유물·리벤 경로다. 이 문서는 정적 코드 검토와 공식 자료 조사 결과이며, 측정되지 않은 개선율을 주장하지 않는다. 게임 화면 캡처·입력·포커스 전환과 대형 벤치마크는 수행하지 않았다. 다른 검토자와 통합 담당자가 실행하는 성능 검사를 방해하지 않도록 빌드도 조율한다.

## 우선 결론

정확도에 영향을 주는 임계값·모델·전처리를 먼저 바꾸기보다, 같은 픽셀과 같은 점수 결과를 더 적은 변환·복사·할당으로 처리하는 편이 안전하다. 우선순위는 Gray8의 PNG 왕복 제거, 카탈로그의 반복 정규화·거리 계산 할당 제거, MR 위치 탐색의 원본 복사·마스크 수명 개선이다. 엔진 확대·상시 전체 화면 딥러닝·과도한 병렬화는 CPU·RAM을 늘릴 수 있으므로 먼저 채택하지 않는다.

## 공식 문서·공개 프로그램에서 확인한 점

| 자료 | 확인한 구현·방법 | WF Labs 적용 판단 |
|---|---|---|
| [Tesseract 입력 품질 안내](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html) | 작은 영역은 문서 전체와 다른 PSM이 필요하고, 지나치게 좁은 crop에는 여백이 도움이 된다. 확대·이진화·잡음 처리는 입력 상태에 따라 달라진다. | 성유물 행 전체에는 SparseText, 분리된 카드에는 SingleBlock, 실제 한 줄에는 SingleLine을 비교한다. 모든 화면에 하나의 PSM을 강제하지 않는다. |
| [charlesw/tesseract 5.2.0 Pix](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/Pix.cs), [PixData](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/PixData.cs) | Pix 생성과 직접 데이터 접근, 행 stride·해상도 설정·byte order 처리를 제공한다. | 현재 패키지와 같은 5.2.0 태그에서 API를 확인했다. Gray8만 직접 전송하고 픽셀·DPI 동등성을 검증하는 것이 첫 실험이다. |
| [Tesseract API 예제](https://tesseract-ocr.github.io/tessdoc/APIExample.html) | 같은 이미지의 지정 영역을 읽는 API와 좌표·신뢰도 출력을 제공한다. | 이름 영역·카드 영역을 분리하되 원본 좌표계를 유지한다. 정상 부품과 일반 설계도가 섞이지 않게 완전한 이름 교차 확인을 보존한다. |
| [WFInfo OCR](https://github.com/WFCD/WFinfo/blob/master/WFInfo/Ocr.cs), [TesseractService](https://github.com/WFCD/WFinfo/blob/master/WFInfo/Services/TesseractService.cs) | 엔진들을 재사용하고 보상 영역을 분리해 처리한다. CJK 판독은 SingleBlock/SingleColumn 결과를 비교하며, 카드별 분리 엔진으로 작업을 나눈다. | 영역 분리·엔진 소유권·입력별 PSM 선택은 참고할 수 있다. 영문 위주 설정이나 사전·문자 제한을 한국어에 그대로 복사하지 않는다. 한국어 옵션 수치·부호 검증은 WF Labs 기준을 유지한다. |
| [ONNX Runtime 스레드 관리](https://onnxruntime.ai/docs/performance/tune-performance/threading.html) | 스레드 수와 spinning은 추론 지연과 CPU 소비 사이의 선택이다. | 기존 KoreanLineRecognizer의 Intra=2, Inter=1, sequential, intra spinning 끔은 보수적인 설정이다. 세션을 카드 수만큼 늘리는 변경은 별도 자원 측정이 먼저다. |
| [PaddleOCR 한국어 인식 모듈](https://paddlepaddle.github.io/PaddleOCR/main/en/version3.x/module_usage/text_recognition.html) | 한국어 PP-OCRv5 인식 모델을 제공하며 텍스트 검출과 줄 인식을 구분한다. | 이미 사용하는 줄 인식 모델을 보조로 유지한다. 공식 일반 문서 벤치마크를 게임 화면의 품질·응답 시간으로 환산하지 않는다. |

참고한 charlesw 5.2.0 태그의 commit은 `2c993543f7fa66576a8890a6c4ab053c4598aaed`다. 공개 코드 사본과 출처 목록은 로컬 `artifacts/ocr-reference-20261027`에 보존했다. 제품에 외부 프로그램의 코드를 복사하지 않았다.

## 직접 Pix 전송의 구현 조건

현재 RelicOcr와 RivenOcr.ReadLines는 전처리된 BitmapSource를 PNG로 인코딩하고 ToArray로 복제한 뒤 Leptonica가 다시 decode한다. Gray8이면 이미 필요한 픽셀을 갖고 있으므로 파일 형식 왕복을 생략할 여지가 있다.

- `Pix.Create(width,height,8)`의 logical width와 `GetData().WordsPerLine * 4` stride를 구분한다. 폭이 4의 배수가 아닌 마지막 word를 검사한다.
- PixData의 byte 접근은 Leptonica의 word 내부 byte 순서를 다룬다. SetDataByte 또는 행 복사 후 한 번의 EndianByteSwap 중 하나를 쓰며 이중 반전하지 않는다.
- 원래 PNG decode의 XRes/YRes를 확인한다. WPF 300dpi 저장의 반올림 결과까지 baseline과 일치시키고 숫자만 고정해 비교를 흐리지 않는다.
- 픽셀 값을 채널별로 검증하고 폭 1·2·3·5·319·321, 여러 행, 검정·흰색·중간 회색, 마지막 픽셀과 padding을 포함한다.
- 직접 경로가 성립하지 않는 pixel format은 기존 경로를 유지한다. 진단용 PNG 저장은 명시적으로 켰을 때만 수행한다.
- 입력 픽셀 동등성 뒤에 텍스트·bounding box·confidence·보상/옵션 확정 결과를 대조한다. 전송이 같다고 다른 이미지 변환까지 동시에 바꾸지 않는다.

## 코드별 병목과 위험

| 위치 | 관찰 | 권고·검증 |
|---|---|---|
| RelicOcr.Read | PNG encode가 끝난 뒤 OCR stopwatch가 시작된다. | OcrMilliseconds만 보면 전송 개선을 놓친다. TotalMilliseconds, 프로세스 CPU 시간, allocated bytes와 별도 변환 시간을 함께 기록한다. |
| RelicCatalog 점수 계산 | 같은 품목의 한국어·영문 이름과 FormD를 query마다 다시 만들고, 편집 거리의 두 int 배열을 모든 비교마다 할당한다. | 카탈로그별 사전 정규화 인덱스, query FormD 1회, 2행 span/ArrayPool을 적용하되 점수 double 비트값·Top8 순서·확정 결과를 구버전과 비교한다. |
| Relic/Riven ReadAsync | gate 대기 전에 전체 BGRA를 복사한다. | 취소된 요청은 복사 전에 확인할 수 있다. 단, WPF 소유 BitmapSource를 worker로 그대로 옮기면 dispatcher 문제가 생길 수 있으므로 수명과 스레드 경계를 지킨다. |
| RivenFooterAnchor | FindAsync 복사 후 normal/dim FindCore에서 BGRA·mask·seen을 각각 만든다. | 같은 입력 pixels와 색상 판정을 공유하고, 이미 끝난 pass의 버퍼를 재사용할 수 있다. MR·R·separator·어두운 footer의 모든 기하 검증은 유지한다. |
| KoreanLineRecognizer | 출력 tensor 전체에 ToArray를 호출한 후 CTC argmax를 계산한다. | 실제 output이 연속 DenseTensor인지 확인해 복사 없는 span 접근을 검토한다. 출력 문자·신뢰도가 같아야 하며 비연속 tensor는 안전한 fallback을 둔다. |
| RivenOcr.ReadAutomaticAsync | ROI→대체 ROI→전체 화면 재판독이 누적될 수 있다. | 앞 단계가 이미 교차 확인됐으면 종료하는 현재 원칙을 유지한다. 읽기 재사용은 완전히 같은 crop·모델·PSM·kind·rank·위치 문맥에서만 허용한다. |

## 엔진 수명과 병렬 처리

성유물은 요청마다 Tesseract를 생성하지만 리벤은 두 개의 독립 worker/gate를 재사용한다. charlesw 엔진은 동시에 하나의 Page만 허용하므로 같은 엔진을 여러 Task가 함께 쓰면 안 된다. 같은 페이지와 iterator의 Dispose 순서도 유지해야 한다. [동일 패키지의 TesseractEngine 구현](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/TesseractEngine.cs)

장수명 엔진은 cold start를 줄이지만 idle RAM과 이미지 사이의 상태를 고려해야 한다. Tesseract 공식 FAQ는 같은 API 객체의 반복 결과가 달라질 때 adaptive classifier 학습 설정 또는 초기화를 확인하도록 안내한다. LSTM 전용 경로에 실제 영향이 있는지는 현재 빌드로 비교해야 하며 무조건 설정을 바꾸지 않는다. [Tesseract FAQ](https://tesseract-ocr.github.io/tessdoc/FAQ.html)

OpenMP 스레드 확대는 자동으로 빠른 결과를 보장하지 않는다. 공식 벤치마크도 일부 OpenMP 빌드의 CPU 낭비를 언급한다. 배포된 DLL이 OpenMP를 사용하는지 먼저 확인하고, 전역 환경 설정은 실제 배포 경로의 측정 없이 추가하지 않는다. [Tesseract 벤치마크](https://tesseract-ocr.github.io/tessdoc/Benchmarks.html)

기존 리벤 비교 측정에서도 병렬은 벽시계 시간을 줄였지만 CPU 시간은 늘었다. 따라서 사용자에게 보이는 지연 감소와 CPU·RAM 절감을 같은 성과로 보고하지 않는다. 근거는 기존 `docs/RIVEN_PAIR_LATENCY_2026-10-26.md`에 있다.

## 정확도와 반응 시간의 통과 기준

1. 이미 맞게 읽히는 실전 회귀 자료의 품목·옵션 키·수치·부호·좌우 순서·확정 여부를 유지한다. 새로 복구한 사례는 별도로 표시한다.
2. 불확실한 결과를 낮은 임계값으로 확정하거나, 수치 범위에서 적당한 값을 추측해 채우지 않는다. 보조 모델의 confidence를 자동 확정 확률로 표시하지 않는다.
3. 같은 이미지를 반복한 순서와 다른 카드로 전환한 순서를 모두 비교한다. 중간 결과에도 잘못된 품목이나 이전 카드 수치가 게시되지 않아야 한다.
4. 보상 1/2/3/4개, 중복 품목, 두 줄 부품명, FHD/울트라와이드/4K, 금색/흰색/붉은 테마, 어두운 이전 리벤과 밝은 새 리벤을 분리해 검사한다.
5. cold/warm을 구분하고 baseline/candidate 실행 순서를 번갈아 측정한다. OCR만의 시간과 감지 대기·캡처·시세·오버레이까지의 시간을 혼합하지 않는다.
6. 대기·한 카드·두 카드·화면 이탈 후의 프로세스 CPU 시간, 정규화 CPU%, working set, private bytes, GC/할당, peak를 함께 남긴다. 서로 다른 프로세스나 하드웨어의 working set만으로 누수 또는 개선율을 단정하지 않는다.
7. 기존 모델 평가의 실제 보류 표본은 적다. 새 학습을 할 때 같은 원본의 crop/배율 변형을 train/test 양쪽에 나누지 않고, 별도 원본의 실제 게임 글자를 평가에 남긴다. 문자 오류율뿐 아니라 보상 전체 일치율·옵션 수치/부호 일치율·잘못된 자동 확정률을 기록한다.

## 이번 독립 검토에서 작성한 변경

통합 담당자의 요청에 따라 RelicRewards의 이름 매칭만 최적화했다. 점수식과 판정 기준은 유지하고 CWT 인덱스·query FormD 재사용·할당 없는 작은 DP 행을 추가했다. `with` 복제·카탈로그 교체·공개 Items 배열 원소 교체에 캐시를 분리/무효화하며, 이전 카탈로그가 cache 때문에 영구 보존되지 않게 weak key를 사용한다.

새 `RelicMatchingEquivalenceChecks`는 RC2 reference scoring/row selection을 테스트에만 보관하고 검색 후보·OCR Top8·점수 비트값·좌표·자동 확정·복합 보상·카탈로그 교체·동점·긴 이름·Unicode를 비교한다. 빌드와 실행 결과는 통합 검수 기록을 따른다. 이 독립 리뷰만으로 성능 개선율이나 새 OCR 정확도를 확정하지 않는다.

추가 요청으로 RivenFooterAnchor의 원본 준비도 공유하도록 변경했다. normal/dim의 기하·색상 임계값·정렬은 그대로 두고 같은 2560px BGRA 배열을 읽는다. 비동기 경로는 WPF 소유 스레드에서 기존 순서의 변환·축소·CopyPixels를 마치고 plain 배열만 넘긴다. 전체 해상도 BGRA 복사와 96DPI detached 이미지 재생성은 생략한다. 작업 완료·취소·예외에는 준비 배열을 지운다. `footer-preparation` trace를 분리해 비용이 다른 단계로 이동한 것을 감추지 않는다.

이 변경의 중요한 검증 대상은 DPI metadata를 다시 만들지 않아도 픽셀 보간이 같은지 여부다. 새 `RivenFooterPreparationChecks.RunAsync`는 구 경로의 full copy→96DPI detach→scale을 reference로 만들어 FHD·울트라와이드·4K, 여러 DPI, BGRA/PBGRA/BGR24, 실제 MR 화면 두 장에서 준비 픽셀·좌표·정렬을 비교하고 취소 후 배열 삭제도 확인한다. 실행 결과가 다르면 비동기 경로의 기존 detach만 유지하는 보수적인 대안이 있다. 축소를 소유 스레드에서 준비하는 만큼 UI 스레드 작업 시간도 별도로 관찰해야 한다. 현재 문서의 정적 구현만으로 이를 통과했다고 표시하지 않는다.
