param(
    [Parameter(Mandatory=$true)][ValidateSet('--riven-probe-replay-check','--ocr-benchmark-check','--resource-check','--ocr-pixel-check','--equipment-navigation-check','--riven-latency-check','--farming-check','--smoke','--ranged-smoke','--mod-card-check','--interaction-check','--search-interaction-check','--client-update-check','--ui-audit-check','--layout-probe','--installer-check','--riven-smoke','--workflow-smoke','--ux-review-check','--schedule-alpha-check','--schedule-marquee-check','--schedule-check','--schedule-live-check','--inventory-check','--inventory-live-workflow-check','--relic-auto-check','--relic-check','--relic-performance-check','--relic-resolution-check','--relic-overlay-check','--relic-workflow-check','--weapon-selection-check','--riven-capture-check')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$Manifest,
    [string]$Assembly,
    [string]$ExpectedVersion='0.10.0-qa.1',
    [switch]$SampleResources,
    [switch]$PortableLauncher
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet='E:\CODEX\Tools\dotnet8\dotnet.exe'
if(-not (Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
function Resolve-CheckPath([string]$value){[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($value)){$value}else{Join-Path $root $value}))}
$assembly=if($Assembly){Resolve-CheckPath $Assembly}else{Join-Path $root 'src\TennoForge.Desktop\bin\Release\net8.0-windows\WFLabs.dll'}
$destination=Resolve-CheckPath $Output
[IO.Directory]::CreateDirectory($destination)|Out-Null
if($PortableLauncher){
    if($Mode -notin @('--weapon-selection-check','--riven-capture-check')){throw '최상위 실행기 검수는 실제 창을 여는 무기 선택/리벤 검사로 실행하세요.'}
    $portableRoot=Split-Path -Parent $assembly
    $manifestPath=Join-Path $portableRoot 'current/sq.version'
    if(-not(Test-Path -LiteralPath $manifestPath)){throw '최상위 실행기가 필요한 current/sq.version이 없습니다.'}
    [xml]$packageManifest=Get-Content -LiteralPath $manifestPath -Raw
    $mainExe=$packageManifest.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="mainExe"]').InnerText
    if($mainExe -ne 'WFLabs.exe'){throw '압축 배포본 mainExe가 WFLabs.exe와 일치하지 않습니다.'}
    $childPath=[IO.Path]::GetFullPath((Join-Path $portableRoot ('current/'+$mainExe)))
    $manifestVersion=$packageManifest.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="version"]').InnerText
    $actualVersion=([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $portableRoot 'current/WFLabs.dll')).ProductVersion -split '\+')[0]
    if($manifestVersion -ne $actualVersion){throw '압축 배포본 실행 메타데이터와 실제 앱 버전이 다릅니다.'}
    if(Test-Path -LiteralPath (Join-Path $destination 'checks.json')){throw '이전 검사 결과를 재사용하지 않도록 빈 출력 폴더를 사용하세요.'}
}
# A separate Win32 desktop, never switched to. Windows, activation, and popups
# remain off the user's input desktop. Native input/capture tests are disabled.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WfIsolatedDesktop {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Startup {
  public int cb; public string reserved, desktop, title; public int x,y,width,height,xChars,yChars,fill,flags; public short show,cbReserved; public IntPtr reserved2,input,output,error;
 }
 [StructLayout(LayoutKind.Sequential)] public struct Info { public IntPtr process,thread; public int processId,threadId; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool CreateProcess(string app,System.Text.StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr environment,string cwd,ref Startup startup,out Info info);
 [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll")] public static extern bool GetExitCodeProcess(IntPtr process,out uint code);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 public delegate bool EnumWindow(IntPtr window,IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr desktop,EnumWindow callback,IntPtr data);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
 public static bool HasVisibleWindow(IntPtr desktop,int process) {
  bool found=false;
  EnumDesktopWindows(desktop,(window,data)=>{uint owner;GetWindowThreadProcessId(window,out owner);if(owner==process&&IsWindowVisible(window))found=true;return true;},IntPtr.Zero);
  return found;
 }
}
'@
$desktopName='WFLabsValidation-'+[Guid]::NewGuid().ToString('N')
$desktop=[WfIsolatedDesktop]::CreateDesktop($desktopName,[IntPtr]::Zero,[IntPtr]::Zero,0,0x01FF,[IntPtr]::Zero)
if($desktop -eq [IntPtr]::Zero){throw "격리 데스크톱 생성 실패: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"}
$prior=[Environment]::GetEnvironmentVariable('WF_LABS_NONINTERACTIVE','Process')
$info=New-Object WfIsolatedDesktop+Info
try {
    [Environment]::SetEnvironmentVariable('WF_LABS_NONINTERACTIVE','1','Process')
    $startup=New-Object WfIsolatedDesktop+Startup
    $startup.cb=[Runtime.InteropServices.Marshal]::SizeOf($startup)
    $startup.desktop='WinSta0\'+$desktopName
    $nativeHost=[IO.Path]::GetExtension($assembly) -eq '.exe'
    $runner=if($nativeHost){$assembly}else{$dotnet}
    $arguments=if($nativeHost){@($assembly,$Mode)}else{@($dotnet,$assembly,$Mode)}
    if($Mode -in @('--relic-workflow-check','--ocr-benchmark-check','--riven-probe-replay-check')){
        if(-not $Manifest){throw '스크린샷 목록 경로가 필요합니다.'}
        $arguments+=Resolve-CheckPath $Manifest
    }
    $arguments+=$destination
    if($Mode -eq '--installer-check'){
        $runner=(Get-Process -Id $PID).Path
        $arguments=@($runner,'-NoProfile','-NonInteractive','-File',(Join-Path $PSScriptRoot 'verify-installer.ps1'),'-ExpectedVersion',$ExpectedVersion,'-QaDirectory',$destination)
    }
    # Paths cannot contain a literal quote. No shell evaluates this command line.
    if($arguments | Where-Object { $_.Contains('"') }){throw '인수에 큰따옴표를 사용할 수 없습니다.'}
    $command=New-Object Text.StringBuilder (($arguments | ForEach-Object { '"'+$_+'"' }) -join ' ')
    if(-not [WfIsolatedDesktop]::CreateProcess($runner,$command,[IntPtr]::Zero,[IntPtr]::Zero,$false,0x08004000,[IntPtr]::Zero,$root,[ref]$startup,[ref]$info)){
        throw "격리 검사 실행 실패: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    [WfIsolatedDesktop]::CloseHandle($info.thread)|Out-Null
    if($PortableLauncher){
        # The stub returning zero proves only that Update.exe was spawned.
        # Keep this desktop alive until the actual app renders and finishes.
        $clock=[Diagnostics.Stopwatch]::StartNew();$child=$null;$windowSeen=$false
        try{
            while($clock.Elapsed.TotalSeconds -lt 180){
                if($null -eq $child){
                    $candidate=Get-CimInstance Win32_Process -Filter "Name='WFLabs.exe'" | Where-Object { $_.ExecutablePath -eq $childPath -and $_.CommandLine.Contains($destination) } | Select-Object -First 1
                    if($candidate){$child=[Diagnostics.Process]::GetProcessById($candidate.ProcessId);$null=$child.Handle}
                }
                if($child){
                    if([WfIsolatedDesktop]::HasVisibleWindow($desktop,$child.Id)){$windowSeen=$true}
                    if($child.HasExited){break}
                }
                Start-Sleep -Milliseconds 100
            }
            if($null -eq $child){throw '최상위 실행기는 종료됐지만 실제 앱 프로세스가 시작되지 않았습니다.'}
            if(-not $child.HasExited){$child.Kill();$child.WaitForExit();throw '최상위 실행기에서 시작한 앱 검사가 제한 시간 안에 끝나지 않았습니다.'}
            $reportPath=Join-Path $destination 'checks.json'
            if(-not(Test-Path -LiteralPath $reportPath)){throw "실제 앱의 검사 결과가 없습니다. 앱 종료 코드: $($child.ExitCode)"}
            $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            if($child.ExitCode -ne 0 -or -not $windowSeen -or -not $report.passed){throw '실제 앱 창 표시·검사·정상 종료 확인에 실패했습니다.'}
            @{passed=$true;launcher=$assembly;app=$childPath;appProcess=$child.Id;appExitCode=$child.ExitCode;visibleWindowObserved=$windowSeen;elapsedSeconds=$clock.Elapsed.TotalSeconds;desktopSwitched=$false;nativeInputTests=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'portable-launch.json') -Encoding utf8NoBOM
        }finally{if($child){$child.Dispose()}}
    }
    if($SampleResources){
        $samples=[Collections.Generic.List[object]]::new();$clock=[Diagnostics.Stopwatch]::StartNew()
        $sampledProcess=[Diagnostics.Process]::GetProcessById($info.processId)
        try{
            while([WfIsolatedDesktop]::WaitForSingleObject($info.process,200) -eq 258){
                $sampledProcess.Refresh();if($sampledProcess.HasExited){break}
                if($samples.Count -lt 6000){$samples.Add([pscustomobject]@{milliseconds=$clock.Elapsed.TotalMilliseconds;cpuMilliseconds=$sampledProcess.TotalProcessorTime.TotalMilliseconds;workingSetBytes=$sampledProcess.WorkingSet64;privateBytes=$sampledProcess.PrivateMemorySize64;handles=$sampledProcess.HandleCount})}
            }
        }finally{$sampledProcess.Dispose()}
        $samples | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'process-samples.json') -Encoding UTF8
        $samples | Export-Csv -LiteralPath (Join-Path $destination 'process-samples.csv') -NoTypeInformation -Encoding UTF8
    }else{[WfIsolatedDesktop]::WaitForSingleObject($info.process,[uint32]::MaxValue)|Out-Null}
    [uint32]$code=0;[WfIsolatedDesktop]::GetExitCodeProcess($info.process,[ref]$code)|Out-Null
    @{mode=$Mode;exitCode=$code;desktop=$desktopName;desktopSwitched=$false;nativeInputTests=$false;priority='BelowNormal'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'isolation.json') -Encoding UTF8
    if($code -ne 0){throw "격리 검사 실패: 종료 코드 $code. $destination 로그를 확인하세요."}
} finally {
    if($info.process -ne [IntPtr]::Zero){[WfIsolatedDesktop]::CloseHandle($info.process)|Out-Null}
    [WfIsolatedDesktop]::CloseDesktop($desktop)|Out-Null
    [Environment]::SetEnvironmentVariable('WF_LABS_NONINTERACTIVE',$prior,'Process')
}
if($code -eq 0){$global:LASTEXITCODE=0}
