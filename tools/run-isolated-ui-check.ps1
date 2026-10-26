param(
    [Parameter(Mandatory=$true)][ValidateSet('--smoke','--ranged-smoke','--mod-card-check','--interaction-check','--search-interaction-check','--client-update-check','--ui-audit-check','--layout-probe','--installer-check','--riven-smoke','--workflow-smoke','--ux-review-check','--schedule-alpha-check','--schedule-marquee-check','--schedule-check','--schedule-live-check','--inventory-check','--inventory-live-workflow-check','--relic-auto-check','--relic-check','--relic-performance-check','--relic-resolution-check','--relic-overlay-check','--relic-workflow-check','--weapon-selection-check','--riven-capture-check')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$Manifest,
    [string]$Assembly,
    [string]$ExpectedVersion='0.10.0-qa.1'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet='E:\CODEX\Tools\dotnet8\dotnet.exe'
if(-not (Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
function Resolve-CheckPath([string]$value){[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($value)){$value}else{Join-Path $root $value}))}
$assembly=if($Assembly){Resolve-CheckPath $Assembly}else{Join-Path $root 'src\TennoForge.Desktop\bin\Release\net8.0-windows\WFLabs.dll'}
$destination=Resolve-CheckPath $Output
[IO.Directory]::CreateDirectory($destination)|Out-Null
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
    if($Mode -eq '--relic-workflow-check'){
        if(-not $Manifest){throw '스크린샷 목록 경로가 필요합니다.'}
        $arguments+=[IO.Path]::GetFullPath((Join-Path $root $Manifest))
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
    [WfIsolatedDesktop]::WaitForSingleObject($info.process,[uint32]::MaxValue)|Out-Null
    [uint32]$code=0;[WfIsolatedDesktop]::GetExitCodeProcess($info.process,[ref]$code)|Out-Null
    @{mode=$Mode;exitCode=$code;desktop=$desktopName;desktopSwitched=$false;nativeInputTests=$false;priority='BelowNormal'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'isolation.json') -Encoding UTF8
    if($code -ne 0){throw "격리 검사 실패: 종료 코드 $code. $destination 로그를 확인하세요."}
} finally {
    if($info.process -ne [IntPtr]::Zero){[WfIsolatedDesktop]::CloseHandle($info.process)|Out-Null}
    [WfIsolatedDesktop]::CloseDesktop($desktop)|Out-Null
    [Environment]::SetEnvironmentVariable('WF_LABS_NONINTERACTIVE',$prior,'Process')
}
if($code -eq 0){$global:LASTEXITCODE=0}
