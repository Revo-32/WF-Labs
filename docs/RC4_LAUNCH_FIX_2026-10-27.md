# RC4 최상위 실행기 복구

RC4 검수 폴더의 `current/sq.version`이 누락되어 최상위 `WF Labs.exe`를 더블클릭하면 앱이 열리지 않았다. 최상위 Velopack 실행기는 `Update.exe`로 실행을 넘기고, Update.exe는 이 메타데이터의 `mainExe`를 읽어 실제 앱을 시작한다. 이전 검수는 `current/WFLabs.exe`를 직접 실행했으므로 이 문제를 발견하지 못했다. 실제 오류 로그에서도 메타데이터/전체 패키지를 찾지 못해 실행을 중단한 것을 확인했다.

기존 검증된 압축 배포본의 실행 메타데이터를 기반으로 실제 앱의 `ProductVersion`인 `1.0.0-rc.4`와 `WFLabs.exe` 연결을 복구했다. 원본 최상위 실행기 파일은 artifacts에 보존하고, 템플릿과 일치하는 실행기로 정리했다. 앱 DLL·OCR 모델·사용자 설정은 변경하지 않았다.

- `tools/prepare-portable-test.ps1`은 기존 검증 ZIP에서 메타데이터를 가져와 실제 앱 버전을 적용하고 최상위 구성요소를 확인한다.
- `tools/verify-package.py`는 압축본에 `sq.version`이 포함됐는지, mainExe·버전이 업데이트 패키지와 일치하는지 검사한다.
- `tools/run-isolated-ui-check.ps1 -PortableLauncher`는 최상위 실행기에서 시작한 실제 앱 프로세스·보이는 창·검사 보고서·정상 종료까지 기다린다. 실행기만 종료 코드 0을 반환한 것을 앱 실행 성공으로 세지 않는다. 메타데이터 누락·버전 불일치는 실행 전에 거부한다.

사용자 데스크톱으로 전환하지 않은 별도 Win32 데스크톱에서 최상위 실행기 → 실제 앱 창 → 무기 선택 검사를 완료했다. 결과는 `artifacts/rc4-launcher-20261027/fixed-launch`와 후속 `final-launch`에 보존한다. 사용자의 게임 입력·포커스는 변경하지 않았다. 실제 사용자 바탕화면에서의 수동 더블클릭 자체를 자동화한 것은 아니며, 같은 최상위 EXE를 별도 데스크톱에서 직접 시작해 실행 인수 전달·창 표시·기능 검사 완료를 확인한 것이다.
