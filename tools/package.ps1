param(
    [string]$Version = '',
    [string]$RepositoryUrl = 'https://github.com/Revo-32/WF-Labs',
    [string]$OutputDirectory = '',
    [string]$Dotnet = 'E:\CODEX\Tools\dotnet8\dotnet.exe',
    [string]$PackageId = 'WFLabs',
    [switch]$NoShortcuts
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $Dotnet)) { $Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not $Version) { [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/TennoForge.Desktop/TennoForge.Desktop.csproj'); $Version = $project.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw '올바른 SemVer 버전이 필요합니다.' }
if ($PackageId -notmatch '^[A-Za-z][A-Za-z0-9.-]+$') { throw '패키지 식별자가 잘못되었습니다.' }
if ($RepositoryUrl -and $RepositoryUrl -notmatch '^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$') { throw 'GitHub 저장소 주소를 확인하세요.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot "builds/WF-Labs-$Version" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath (Join-Path $OutputDirectory "$PackageId-$Version-full.nupkg")){throw '같은 버전의 기존 패키지를 보존합니다. 새 버전이나 빈 출력 폴더를 지정하세요.'}
# Never overwrite an earlier publish tree. A fresh staging directory prevents stale assemblies.
$stage = Join-Path $projectRoot ('artifacts/package-' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $stage 'app'
$helper = Join-Path $stage 'helper'
$env:DOTNET_ROOT = Split-Path -Parent $Dotnet
Push-Location -LiteralPath $projectRoot
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw '패키징 도구 복원 실패' }
    & $Dotnet publish src/TennoForge.Desktop/TennoForge.Desktop.csproj -c Release -r win-x64 --self-contained true -o $publish "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw '앱 publish 실패' }
    & $Dotnet publish src/WFLabs.Startup/WFLabs.Startup.csproj -c Release -r win-x64 --self-contained true -o $helper "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw '시작 도우미 publish 실패' }
    # Both projects target the same runtime; retain the WPF runtime and add the helper's entry files.
    Get-ChildItem -LiteralPath $helper -Filter 'WFLabs.Startup.*' -File | Copy-Item -Destination $publish
    @{RepositoryUrl=$RepositoryUrl.TrimEnd('/');Prerelease=$Version.Contains('-')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'release-config.json') -Encoding utf8NoBOM
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/THIRD_PARTY_NOTICES.txt') -Destination $publish
    Copy-Item -LiteralPath (Join-Path $projectRoot 'COPYRIGHT.md') -Destination $publish
    foreach($guide in @('PRIVACY.md','SUPPORT.md','RELEASE_1_0_0.md','OCR_RIVEN_CORPUS_2026-10-30.md','USER_ACCEPTANCE_2026-10-30.md','MASTERY_MATERIAL_GALLERY_2026-10-31.md','RANGED_APPROXIMATIONS.md')){Copy-Item -LiteralPath (Join-Path $projectRoot ('docs/'+$guide)) -Destination $publish}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/licenses/VELOPACK-LICENSE.txt') -Destination $publish
    $assets=Get-Content -LiteralPath (Join-Path $projectRoot 'src/TennoForge.Desktop/obj/project.assets.json') -Raw | ConvertFrom-Json
    $runtimeNames=@('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')
    foreach($dependency in $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -In $runtimeNames) {
        $runtimeVersion=[regex]::Match($dependency.version,'\d+\.\d+\.\d+').Value
        $relative=$dependency.name.ToLowerInvariant()+'/'+$runtimeVersion
        $runtimeFolder=$assets.packageFolders.PSObject.Properties.Name | ForEach-Object {Join-Path $_ $relative} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
        if(-not $runtimeFolder){throw '런타임 라이선스 폴더를 찾지 못했습니다.'}
        $notices=Join-Path $publish ('licenses/'+$dependency.name)
        New-Item -ItemType Directory -Path $notices -Force | Out-Null
        Get-ChildItem -LiteralPath $runtimeFolder -File | Where-Object Name -Match '^(LICENSE(?:\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' | Copy-Item -Destination $notices
        if(-not(Get-ChildItem -LiteralPath $notices -File)){throw '런타임 고지 파일이 비어 있습니다.'}
    }
    $shortcuts = if ($NoShortcuts) { 'None' } else { 'Desktop,StartMenuRoot' }
    $releaseNotes = Join-Path $projectRoot ('docs/releases/'+$Version+'.md')
    if(-not(Test-Path -LiteralPath $releaseNotes)){$releaseNotes=Join-Path $projectRoot 'docs/RELEASE_NOTES.md'}
    & $Dotnet vpk pack --packId $PackageId --packVersion $Version --packDir $publish --packTitle 'WF Labs' --packAuthors 'WF Labs' --mainExe WFLabs.exe --runtime win-x64 --framework vcredist143-x64 --icon src/TennoForge.Desktop/Assets/Brand/wf-labs.ico --shortcuts $shortcuts --outputDir $OutputDirectory --releaseNotes $releaseNotes
    if ($LASTEXITCODE -ne 0) { throw '설치 파일 생성 실패' }
    $hashes = Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object { $hash=Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $($_.Name)" }
    $hashes | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8NoBOM
    @{version=$Version;packageId=$PackageId;publish=$publish;output=$OutputDirectory;prerequisite='vcredist143-x64';createdUtc=[DateTimeOffset]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'build.json') -Encoding utf8NoBOM
    Write-Output "설치·업데이트 패키지: $OutputDirectory"
    Write-Output "검증용 앱 폴더: $publish"
} finally { Pop-Location }
