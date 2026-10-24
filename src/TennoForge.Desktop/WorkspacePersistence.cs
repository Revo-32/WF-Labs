using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TennoForge.Core;
using WFLabs.Client;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private string? workspacePath,lastWorkspaceJson;
    private Build? lastWorkspaceBuild;
    private bool restoringWorkspace;
    private readonly DispatcherTimer workspaceSaveTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private RangedPage CreateRangedPage(string category)
    {
        var ranged=new RangedPage(category){CombatSamples=()=>SelectedSamples};
        ranged.WorkspaceChanged+=ScheduleWorkspaceSave;return ranged;
    }
    internal void EnableWorkspacePersistence(string? isolatedPath=null)
    {
        workspacePath=isolatedPath??Path.Combine(ClientPreferences.Folder,"workspace.json");
        restoringWorkspace=true;
        var original=CaptureUpdateWorkspace();
        try
        {
            foreach(var path in new[]{workspacePath,workspacePath+".bak"})
            {
                if(!File.Exists(path))continue;
                try
                {
                    ApplyUpdateWorkspace(AtomicJsonFile.Read<UpdateWorkspace>(path));
                    if(path.EndsWith(".bak",StringComparison.Ordinal))ReportWorkspaceIssue("이전 자동 저장본에서 작업을 복원했습니다.");
                    break;
                }
                catch(Exception ex)when(IsWorkspaceException(ex))
                {
                    ApplyUpdateWorkspace(original);
                    // Preserve the unreadable file before any future atomic replacement.
                    try{File.Copy(path,path+".unreadable-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),true);}
                    catch(Exception copyError)when(copyError is IOException or UnauthorizedAccessException)
                    {
                        workspacePath=null;
                        ReportWorkspaceIssue("원본 보존을 위해 자동 저장을 중지했습니다. 저장 폴더 확인 필요: "+copyError.Message);
                        continue;
                    }
                    ReportWorkspaceIssue("자동 저장 복원 확인 필요: "+ex.Message);
                }
            }
            lastWorkspaceBuild=ReadBuild();
        }
        finally{restoringWorkspace=false;}
        workspaceSaveTimer.Tick+=(_,_)=>{workspaceSaveTimer.Stop();SaveWorkspace();};
        AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,_)=>ScheduleWorkspaceSave()),true);
        AddHandler(Selector.SelectionChangedEvent,new SelectionChangedEventHandler((_,_)=>ScheduleWorkspaceSave()),true);
        AddHandler(ButtonBase.ClickEvent,new RoutedEventHandler((_,_)=>ScheduleWorkspaceSave()),true);
        Closing+=(_,_)=>{workspaceSaveTimer.Stop();SaveWorkspace();};
    }
    private static bool IsWorkspaceException(Exception ex)=>ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException or FormatException or OverflowException;
    private void ScheduleWorkspaceSave()
    {
        if(workspacePath is null||restoringWorkspace)return;
        workspaceSaveTimer.Stop();workspaceSaveTimer.Start();
    }
    private void ReportWorkspaceIssue(string message)
    {
        error.Text=message;
        if(page.Content is RangedPage ranged)ranged.ReportWorkspaceIssue(message);
    }
    internal void SaveWorkspace()
    {
        if(workspacePath is null||restoringWorkspace)return;
        try
        {
            var state=CaptureUpdateWorkspace();var json=JsonSerializer.Serialize(state,Catalog.Json);
            if(json==lastWorkspaceJson)return;
            AtomicJsonFile.Write(workspacePath,state);lastWorkspaceJson=json;
        }
        catch(Exception ex)when(IsWorkspaceException(ex)){ReportWorkspaceIssue("자동 저장 실패 · 기존 저장본은 유지됩니다: "+ex.Message);}
    }
}
