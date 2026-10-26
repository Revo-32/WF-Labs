param([string]$Dotnet='E:\CODEX\Tools\dotnet8\dotnet.exe',[string]$OutputDirectory='artifacts/validation')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not(Test-Path -LiteralPath $Dotnet)){$Dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location -LiteralPath $projectRoot
try {
    $report=[IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $report -Force | Out-Null
    & $Dotnet build WFLabs.sln -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Release 빌드 실패'}
    & $Dotnet tests/TennoForge.Checks/bin/Release/net8.0/TennoForge.Checks.dll (Join-Path $report 'checks.json')
    if($LASTEXITCODE -ne 0){throw '계산 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --schedule-check -Output (Join-Path $report 'schedules')
    if($LASTEXITCODE -ne 0){throw '일정·중재·상점 실제 UI 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --ranged-smoke -Output (Join-Path $report 'ranged')
    if($LASTEXITCODE -ne 0){throw '주무기·보조무기 UI 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --mod-card-check -Output (Join-Path $report 'mod-cards')
    if($LASTEXITCODE -ne 0){throw '모드 카드 프레임·글자 영역 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --interaction-check -Output (Join-Path $report 'interactions')
    if($LASTEXITCODE -ne 0){throw '모드 장착·랭크·극성·드래그 실제 동작 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --search-interaction-check -Output (Join-Path $report 'search-interactions')
    if($LASTEXITCODE -ne 0){throw '검색 포커스·IME·지우기·드롭다운 상태 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --relic-check -Output (Join-Path $report 'relics')
    if($LASTEXITCODE -ne 0){throw '성유물 한국어 OCR·오인식 수정·시세 비교 UI 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --client-update-check -Output (Join-Path $report 'updates')
    if($LASTEXITCODE -ne 0){throw '클라이언트 업데이트 흐름 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --riven-smoke -Output (Join-Path $report 'riven')
    if($LASTEXITCODE -ne 0){throw '리벤 작업 흐름 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --ui-audit-check -Output (Join-Path $report 'ui-audit')
    if($LASTEXITCODE -ne 0){throw '창·팝업·테마·스크롤 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --layout-probe -Output (Join-Path $report 'layout')
    if($LASTEXITCODE -ne 0){throw '배율별 겹침·잘림·리벤 수치 열 정렬 검증 실패'}
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --smoke -Output (Join-Path $report 'ui')
    if($LASTEXITCODE -ne 0){throw '전체 UI 회귀 검증 실패'}
} finally {Pop-Location}
