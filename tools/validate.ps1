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
    & $Dotnet src/TennoForge.Desktop/bin/Release/net8.0-windows/WFLabs.dll --client-update-check (Join-Path $report 'updates')
    if($LASTEXITCODE -ne 0){throw '클라이언트 업데이트 흐름 검증 실패'}
    & $Dotnet src/TennoForge.Desktop/bin/Release/net8.0-windows/WFLabs.dll --riven-smoke (Join-Path $report 'riven')
    if($LASTEXITCODE -ne 0){throw '리벤 작업 흐름 검증 실패'}
    & $Dotnet src/TennoForge.Desktop/bin/Release/net8.0-windows/WFLabs.dll --ui-audit-check (Join-Path $report 'ui-audit')
    if($LASTEXITCODE -ne 0){throw '창·팝업·테마·스크롤 검증 실패'}
    & $Dotnet src/TennoForge.Desktop/bin/Release/net8.0-windows/WFLabs.dll --smoke (Join-Path $report 'ui')
    if($LASTEXITCODE -ne 0){throw '전체 UI 회귀 검증 실패'}
} finally {Pop-Location}
