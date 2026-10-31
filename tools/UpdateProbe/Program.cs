using System.Security.Cryptography;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

if(args.Length!=3)throw new ArgumentException("사용법: UpdateProbe <이전 버전> <공개 버전> <보고서 경로>");
var current=args[0];var expected=args[1];var reportPath=Path.GetFullPath(args[2]);
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
var folder=Path.Combine(Path.GetDirectoryName(reportPath)!,"online-package-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var source=new GithubSource("https://github.com/Revo-32/WF-Labs",null,true);
var manager=new UpdateManager(source,locator:new TestVelopackLocator("WFLabs",current,folder));
var update=await manager.CheckForUpdatesAsync();
if(update?.TargetFullRelease.Version.ToString()!=expected)throw new InvalidOperationException("공개 업데이트 버전 불일치");
await manager.DownloadUpdatesAsync(update);
var file=Path.Combine(folder,update.TargetFullRelease.FileName);
using var stream=File.OpenRead(file);var hash=Convert.ToHexString(SHA256.HashData(stream));
if(!hash.Equals(update.TargetFullRelease.SHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("온라인 다운로드 SHA-256 불일치");
var stableSource=new GithubSource("https://github.com/Revo-32/WF-Labs",null,false);
var stable=new UpdateManager(stableSource,locator:new TestVelopackLocator("WFLabs",current,folder));
if(await stable.CheckForUpdatesAsync() is not {} stableUpdate||stableUpdate.TargetFullRelease.Version.ToString()!=expected)
    throw new InvalidOperationException("안정 채널 공개 업데이트 버전 불일치");
var latest=new UpdateManager(stableSource,locator:new TestVelopackLocator("WFLabs",expected,folder));
if(await latest.CheckForUpdatesAsync() is not null)throw new InvalidOperationException("최신 버전 재설치 판정 오류");
var report=new{passed=true,anonymous=true,repository="https://github.com/Revo-32/WF-Labs",currentVersion=current,availableVersion=expected,
    downloadedBytes=new FileInfo(file).Length,sha256=hash,latestVersionReturnsNoUpdate=true,stableAndPreviewChannelsVerified=true,installedContextSimulated=true,
    note="클라이언트와 같은 Velopack GithubSource로 익명 확인·다운로드·해시 검증. 설치 버전 정보만 TestVelopackLocator로 주입하며 사용자 설치본은 변경하지 않음. 실제 설치·재시작·복원은 별도 격리 설치 QA에서 검증."};
File.WriteAllText(reportPath,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(report));
