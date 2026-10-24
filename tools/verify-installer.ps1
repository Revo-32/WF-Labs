param([string]$ExpectedVersion='0.8.0-qa.2',[string]$QaDirectory='artifacts/installer-qa')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$qa=[IO.Path]::GetFullPath((Join-Path $root $QaDirectory))
if(-not $qa.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '검증 폴더는 프로젝트 artifacts 내부여야 합니다.'}
$install=Join-Path $qa ('installed-' + [Guid]::NewGuid().ToString('N'))
$setup=Join-Path $qa 'v1/WFLabs.Verify-win-Setup.exe'
$feed=Join-Path $qa 'v2'
$report=Join-Path $qa 'update-check.json'
$after=$report+'.after.json'
if(Test-Path -LiteralPath $report){throw '기존 검증 결과를 보존합니다. 다른 검증 폴더를 사용하세요.'}
$runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
function Read-StartupValue { (Get-ItemProperty -LiteralPath $runKey -Name 'WF Labs' -ErrorAction SilentlyContinue).'WF Labs' }
$startupBefore=Read-StartupValue
$preferences=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WF Labs/client-settings.json'
function Read-SettingsHash { if(Test-Path -LiteralPath $preferences){(Get-FileHash -LiteralPath $preferences).Hash}else{''} }
$settingsBefore=Read-SettingsHash
$profileFolder=Split-Path -Parent $preferences
function Read-WorkspaceHashes {
    $state=[ordered]@{}
    foreach($name in @('workspace.json','workspace.json.bak','update-workspace.json','update-workspace.json.restored','appearance.json')) {
        $path=Join-Path $profileFolder $name
        $state[$name]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path).Hash}else{''}
    }
    $state | ConvertTo-Json -Compress
}
$workspaceBefore=Read-WorkspaceHashes
$uninstalled=$false
try {
    $args=@('--silent','--installto',('"'+$install+'"'),'--log',('"'+(Join-Path $qa 'setup.log')+'"'))
    $process=Start-Process -FilePath $setup -ArgumentList $args -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(60000)){throw '설치 검증 시간 초과'}
    if($process.ExitCode -ne 0){throw "설치 실패: $($process.ExitCode)"}
    $checkArgs=@('--installed-update-check',('"'+$feed+'"'),('"'+$report+'"'),'--apply')
    Start-Process -FilePath (Join-Path $install 'current/WFLabs.exe') -ArgumentList $checkArgs -WindowStyle Hidden | Out-Null
    $deadline=[DateTime]::UtcNow.AddMinutes(2)
    while(-not(Test-Path -LiteralPath $after) -and [DateTime]::UtcNow -lt $deadline){
        if(Test-Path -LiteralPath $report){
            try{$currentResult=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json}catch{$currentResult=$null}
            if($null -ne $currentResult -and -not $currentResult.passed){throw "업데이트 진단 실패: $($currentResult.error)"}
        }
        Start-Sleep -Milliseconds 500
    }
    if(-not(Test-Path -LiteralPath $after)){throw '업데이트 후 재시작 보고서가 없습니다.'}
    $beforeResult=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $afterResult=Get-Content -LiteralPath $after -Raw | ConvertFrom-Json
    if(-not $beforeResult.passed -or -not $afterResult.passed -or $afterResult.currentVersion -ne $ExpectedVersion -or -not $afterResult.helperPresent -or $afterResult.isPortable -or -not $afterResult.recoveryVerified){throw '설치/업데이트 결과가 예상과 다릅니다.'}
    if((Read-StartupValue) -ne $startupBefore -or (Read-SettingsHash) -ne $settingsBefore){throw '검증용 설치가 사용자 자동 실행 설정을 변경했습니다.'}
} finally {
    $updater=Join-Path $install 'Update.exe'
    if(Test-Path -LiteralPath $updater){
        if(-not([IO.Path]::GetFullPath($install).StartsWith([IO.Path]::GetFullPath($qa)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw '검증 설치 경로 이탈'}
        $cleanup=Start-Process -FilePath $updater -ArgumentList @('--silent','uninstall') -WindowStyle Hidden -PassThru
        if(-not $cleanup.WaitForExit(60000) -or $cleanup.ExitCode -ne 0){throw '검증 설치 제거 실패'}
        $uninstalled=$true
    }
}
if((Read-StartupValue) -ne $startupBefore -or (Read-SettingsHash) -ne $settingsBefore){throw '제거 후 사용자 설정이 바뀌었습니다.'}
if((Read-WorkspaceHashes) -ne $workspaceBefore){throw '격리 설치 검증이 실제 사용자 작업 파일을 변경했습니다.'}
@{passed=$true;installedVersion=$beforeResult.currentVersion;updatedVersion=$afterResult.currentVersion;downloaded=$beforeResult.downloaded;restarted=$true;uninstalled=$uninstalled;startupSettingsPreserved=$true;userWorkspacePreserved=$true;helperPresent=$afterResult.helperPresent;recoveryVerified=$afterResult.recoveryVerified} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qa 'installer-verification.json') -Encoding utf8NoBOM
Get-Content -LiteralPath (Join-Path $qa 'installer-verification.json')
