param(
    [Parameter(Mandatory=$true)][string]$Directory,
    [Parameter(Mandatory=$true)][string]$TemplateZip,
    [switch]$RefreshLauncher
)
$ErrorActionPreference='Stop'
$destination=[IO.Path]::GetFullPath($Directory)
$template=[IO.Path]::GetFullPath($TemplateZip)
$app=Join-Path $destination 'current/WFLabs.dll'
if(-not(Test-Path -LiteralPath $app)){throw '검수본의 current/WFLabs.dll을 찾을 수 없습니다.'}
$version=([Diagnostics.FileVersionInfo]::GetVersionInfo($app).ProductVersion -split '\+')[0]
if($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$'){throw '검수본 앱 버전을 읽지 못했습니다.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($template)
try {
    $entry=$archive.GetEntry('current/sq.version')
    if($null -eq $entry){throw '원본 압축 배포본에 실행 메타데이터가 없습니다.'}
    $reader=[IO.StreamReader]::new($entry.Open())
    try{[xml]$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $metadata=$manifest.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
    foreach($key in @('id','version','mainExe')){
        if(-not $metadata.SelectSingleNode('*[local-name()="'+$key+'"]')){throw "원본 실행 메타데이터 항목 누락: $key"}
    }
    if($metadata.SelectSingleNode('*[local-name()="id"]').InnerText -ne 'WFLabs' -or $metadata.SelectSingleNode('*[local-name()="mainExe"]').InnerText -ne 'WFLabs.exe'){throw 'WF Labs 원본 압축 배포본이 아닙니다.'}
    $metadata.SelectSingleNode('*[local-name()="version"]').InnerText=$version
    foreach($key in @('releaseNotes','releaseNotesHtml')){
        $node=$metadata.SelectSingleNode('*[local-name()="'+$key+'"]')
        if($node){$node.InnerText="로컬 검수본 $version. 포함된 검수 보고서와 릴리즈 문서를 확인하세요."}
    }
    # Refreshing a publish folder must retain the native launcher's manifest.
    # The launcher hands off to Update.exe, which reads this to find mainExe.
    foreach($name in @('WF Labs.exe','Update.exe','.portable')){
        $entry=$archive.GetEntry($name)
        if($null -eq $entry){throw "원본 압축 배포본 구성요소 누락: $name"}
        $path=Join-Path $destination $name
        if(-not(Test-Path -LiteralPath $path)){
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$false)
        }elseif($name -ne '.portable'){
            $stream=$entry.Open()
            try{$expected=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected){
                if(-not $RefreshLauncher){throw "원본과 실행기 해시가 다릅니다: $name. 보존 후 -RefreshLauncher로 교체하세요."}
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
            }
        }
    }
    $settings=[Xml.XmlWriterSettings]::new();$settings.Encoding=[Text.UTF8Encoding]::new($false);$settings.Indent=$true
    $writer=[Xml.XmlWriter]::Create((Join-Path $destination 'current/sq.version'),$settings)
    try{$manifest.Save($writer)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Write-Output "압축 검수본 실행 메타데이터 준비 완료: $version"
