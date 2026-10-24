using System.IO;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace TennoForge.Desktop;
public partial class App
{
    // Opt-in packaging test. Updating from a local feed is restricted to the separate QA package ID.
    private async void RunInstalledUpdateCheck(string[] args)
    {
        var report=Path.GetFullPath(args[2]);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try
        {
            var manager=new UpdateManager(new SimpleFileSource(new DirectoryInfo(Path.GetFullPath(args[1]))));
            if(manager.AppId!="WFLabs.Verify"||!manager.IsInstalled)throw new InvalidOperationException("격리된 검증용 설치본이 아닙니다.");
            var afterArgs=new[]{"--installed-update-check",args[1],report+".after.json"};
            using var updates=new ClientUpdates(new VelopackUpdateFeed(manager,afterArgs),()=>new());
            await updates.CheckAsync(automatic:true);
            if(updates.Phase is not (ClientUpdatePhase.Current or ClientUpdatePhase.Ready))throw new InvalidOperationException(updates.Status);
            var workspacePath=Path.Combine(Path.GetDirectoryName(report)!,"qa-update-workspace.json");
            var recoveryVerified=false;
            if(!args.Contains("--apply")){var window=new MainWindow();recoveryVerified=window.RestoreWorkspaceForUpdateCheck(workspacePath);}
            var data=new{passed=true,manager.AppId,manager.IsInstalled,manager.IsPortable,currentVersion=manager.CurrentVersion?.ToString(),availableVersion=updates.Release?.Version,downloaded=updates.Phase==ClientUpdatePhase.Ready,helperPresent=File.Exists(Path.Combine(AppContext.BaseDirectory,"WFLabs.Startup.exe")),recoveryVerified,controllerState=updates.Phase.ToString()};
            File.WriteAllText(report,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
            if(updates.Phase==ClientUpdatePhase.Ready&&args.Contains("--apply"))
            {
                var window=new MainWindow();await window.PrepareWorkspaceForUpdateCheck();
                updates.Apply(()=>window.SaveWorkspaceForUpdateCheck(workspacePath),()=>{});
                if(updates.Phase!=ClientUpdatePhase.Applying)throw new InvalidOperationException(updates.Status);
            }
            else Shutdown(0);
        }
        catch(Exception ex){File.WriteAllText(report,JsonSerializer.Serialize(new{passed=false,error=ex.ToString()}));Shutdown(1);}
    }
}
