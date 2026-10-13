param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetExecutable = 'E:\CODEX\Tools\dotnet8\dotnet.exe'
Push-Location -LiteralPath $projectRoot
try {
    & $dotnetExecutable build WFLabs.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
    & $dotnetExecutable run --project tests/TennoForge.Checks -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw '계산 검증 실패' }
    if ($OutputDirectory) {
        & $dotnetExecutable publish src/TennoForge.Desktop/TennoForge.Desktop.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) { throw '배포판 생성 실패' }
    }
} finally { Pop-Location }
