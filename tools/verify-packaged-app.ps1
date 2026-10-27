param(
    [Parameter(Mandatory=$true)][string]$Directory,
    [string]$OutputDirectory='artifacts/packaged-app',
    [string]$Dotnet='E:\CODEX\Tools\dotnet8\dotnet.exe',
    [string]$VerifiedCoreDirectory
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
function Absolute([string]$path){[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($path)){$path}else{Join-Path $root $path}))}
$package=Absolute $Directory
$report=Absolute $OutputDirectory
if(-not $report.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '검수 출력은 프로젝트 artifacts 아래여야 합니다.'}
if(Test-Path -LiteralPath $report){throw '기존 패키지 검수를 보존합니다. 새 출력 폴더를 사용하세요.'}
if(-not(Test-Path -LiteralPath $Dotnet)){$Dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$checks=Join-Path $root 'tests/TennoForge.Checks/bin/Release/net8.0'
if(-not(Test-Path -LiteralPath (Join-Path $checks 'TennoForge.Checks.dll'))){throw '먼저 Release 계산 검사 실행기를 빌드하세요.'}
New-Item -ItemType Directory -Path $report | Out-Null
$passed=$false
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $portable=Join-Path $report 'portable'
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $package 'WFLabs-win-Portable.zip'),$portable)
    [xml]$manifest=Get-Content -LiteralPath (Join-Path $portable 'current/sq.version') -Raw
    $version=$manifest.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="version"]').InnerText
    $launcher=Join-Path $portable 'WF Labs.exe'
    $app=Join-Path $portable 'current/WFLabs.exe'
    # Validate the code in the shipping ZIP, including the real top-level stub.
    & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode --weapon-selection-check -Output (Join-Path $report 'launcher') -Assembly $launcher -PortableLauncher
    if($LASTEXITCODE -ne 0){throw '압축 배포본 최상위 실행기 검수 실패'}
    foreach($entry in @(@('--riven-capture-check','riven'),@('--relic-check','relic'),@('--client-update-check','updates'),@('--smoke','ui'))){
        & (Join-Path $PSScriptRoot 'run-isolated-ui-check.ps1') -Mode $entry[0] -Output (Join-Path $report $entry[1]) -Assembly $app
        if($LASTEXITCODE -ne 0){throw "배포 실행 파일 검수 실패: $($entry[0])"}
    }
    # The publish command can alter assembly version/build flags. Run the full
    # calculator checks against the DLL from the actual distribution payload.
    $coreFolder=Join-Path $report 'core'
    New-Item -ItemType Directory -Path $coreFolder | Out-Null
    Get-ChildItem -LiteralPath $checks -Force | Copy-Item -Destination $coreFolder -Recurse
    $core=Join-Path $portable 'current/TennoForge.Core.dll'
    Copy-Item -LiteralPath $core -Destination (Join-Path $coreFolder 'TennoForge.Core.dll')
    $coreHash=(Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash
    if((Get-FileHash -LiteralPath (Join-Path $coreFolder 'TennoForge.Core.dll') -Algorithm SHA256).Hash -ne $coreHash){throw '배포 계산 DLL 복사 해시 불일치'}
    $coreReport=Join-Path $report 'core-checks.json'
    if($VerifiedCoreDirectory){
        $previous=Absolute $VerifiedCoreDirectory
        $verified=Get-Content -LiteralPath (Join-Path $previous 'packaged-app-verification.json') -Raw | ConvertFrom-Json
        $previousCore=Get-Content -LiteralPath (Join-Path $previous 'core-checks.json') -Raw | ConvertFrom-Json
        if(-not $verified.passed -or $verified.coreSha256 -ne $coreHash.ToLowerInvariant() -or $previousCore.failed.Count -gt 0 -or $previousCore.passed -ne $verified.coreChecks){throw '동일 배포 계산 DLL의 완료된 검수 근거가 아닙니다.'}
        Copy-Item -LiteralPath (Join-Path $previous 'core-checks.json') -Destination $coreReport
        @{directory=$previous;sha256=$coreHash.ToLowerInvariant();unchangedBinary=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $report 'core-provenance.json') -Encoding utf8NoBOM
    }else{
        $process=Start-Process -FilePath $Dotnet -ArgumentList @(('"'+(Join-Path $coreFolder 'TennoForge.Checks.dll')+'"'),('"'+$coreReport+'"')) -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $report 'core.log') -RedirectStandardError (Join-Path $report 'core-error.log') -PassThru
        try{$process.PriorityClass='BelowNormal'}catch{}
        $process.WaitForExit()
        if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $coreReport)){throw '배포 계산 DLL의 전체 회귀 검사 실패'}
    }
    $result=Get-Content -LiteralPath $coreReport -Raw | ConvertFrom-Json
    if(-not $result.passed -or $result.failed.Count -gt 0){throw '배포 계산 검사 보고서에 실패가 있습니다.'}
    $launch=Get-Content -LiteralPath (Join-Path $report 'launcher/portable-launch.json') -Raw | ConvertFrom-Json
    $passed=$true
    @{passed=$true;version=$version;coreChecks=$result.passed;coreSha256=$coreHash.ToLowerInvariant();launcherWindowVerified=$launch.visibleWindowObserved;checks=@('launcher','riven','relic','updates','ui','packaged-core');desktopSwitched=$false;gameInputChanged=$false;cleanWindowsVerified=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'packaged-app-verification.json') -Encoding utf8NoBOM
}catch{
    @{passed=$false;error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $report 'packaged-app-verification.json') -Encoding utf8NoBOM
    throw
}
if($passed){Get-Content -LiteralPath (Join-Path $report 'packaged-app-verification.json');$global:LASTEXITCODE=0}
