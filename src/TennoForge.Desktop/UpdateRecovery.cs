using System.IO;
using System.Text.Json;
using TennoForge.Core;
using WFLabs.Client;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private record RivenDraft(string? WeaponId,int Rank,int Destination,string Cost,string? Polarity,
        RivenStat?[] Stats,string[] Values,bool[] Negatives,string Cycles,string Attempts,bool PreserveSplice);
    private record UpdateWorkspace(Build Build,RivenDraft Draft,RivenWorkshopState? Session,RivenRoll? Baseline,int Schema=1,
        Build? Comparison=null,int ActivePage=0,bool Light=false,bool Performance=false);
    private static string RecoveryPath=>Path.Combine(ClientPreferences.Folder,"update-workspace.json");
    private UpdateWorkspace CaptureUpdateWorkspace()
    {
        var draft=new RivenDraft((rivenWeapon.SelectedItem as Weapon)?.Id,(int)rivenRank.SelectedItem,(int)rivenDestination.SelectedItem,
            rivenCost.Text,rivenPolarity.SelectedValue as string,traits.Select(c=>(c.SelectedItem as RivenOption)?.Stat).ToArray(),
            rivenValues.Select(v=>v.Text).ToArray(),negatives.Select(v=>v.IsChecked==true).ToArray(),rivenCycles.Text,rivenAttempts.Text,preserveSplice.IsChecked==true);
        return new(ReadBuild(),draft,rivenSession,rivenBaseline,Comparison:comparisonBuild,
            ActivePage:navigation.FindIndex(t=>t.IsChecked==true),Light:!Theme.IsDark,Performance:lightMode);
    }
    private void SaveUpdateRecovery()
    {
        var workspace=CaptureUpdateWorkspace();
        Directory.CreateDirectory(ClientPreferences.Folder);
        File.WriteAllText(RecoveryPath+".tmp",JsonSerializer.Serialize(workspace,Catalog.Json));File.Move(RecoveryPath+".tmp",RecoveryPath,true);
    }
    private void ApplyUpdateWorkspace(UpdateWorkspace workspace)
    {
        var d=workspace.Draft;
        if(workspace.Schema!=1||d is null||d.Stats?.Length!=4||d.Values?.Length!=4||d.Negatives?.Length!=4||d.Rank is <0 or >8||d.Destination is <1 or >8)
            throw new ArgumentException("업데이트 작업 복원 형식을 확인하세요.");
        if(workspace.Session is {} saved)RivenWorkshop.Validate(saved);
        if(workspace.Baseline is {} baseline)RivenValues.Validate(baseline);
        ApplyBuild(workspace.Build);
        if(workspace.Session is {} session)SetWorkshop(session with{Locked=null});
        rivenBaseline=workspace.Baseline;
        changingRiven=true;
        try
        {
            rivenWeapon.SelectedItem=catalog.Weapons.SingleOrDefault(w=>w.Id==d.WeaponId);rivenRank.SelectedItem=d.Rank;rivenDestination.SelectedItem=d.Destination;
            rivenCost.Text=d.Cost;rivenPolarity.SelectedValue=d.Polarity;
            for(var i=0;i<4;i++)
            {
                traits[i].SelectedItem=traits[i].Items.Cast<RivenOption>().Single(o=>o.Stat==d.Stats[i]);
                rivenValues[i].Text=d.Values[i];negatives[i].IsChecked=d.Negatives[i];
            }
            rivenCycles.Text=d.Cycles;rivenAttempts.Text=d.Attempts;preserveSplice.IsChecked=d.PreserveSplice;
        }
        finally{changingRiven=false;}
        comparisonBuild=workspace.Comparison;UpdateComparison();SetDarkTheme(!workspace.Light);SetLightMode(workspace.Performance);
        if(workspace.ActivePage is >=0 and <4)navigation[workspace.ActivePage].IsChecked=true;
        RefreshRivenWorkbench();
    }
    internal void RestoreUpdateRecovery()
    {
        if(!File.Exists(RecoveryPath))return;
        try
        {
            var workspace=JsonSerializer.Deserialize<UpdateWorkspace>(File.ReadAllText(RecoveryPath),Catalog.Json)??throw new ArgumentException("업데이트 전 작업이 비어 있습니다.");
            ApplyUpdateWorkspace(workspace);File.Move(RecoveryPath,RecoveryPath+".restored",true);
        }
        catch(Exception ex)when(ex is IOException or ArgumentException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {error.Text="업데이트 전 작업 복원 확인 필요: "+ex.Message;}
    }
    internal void SaveWorkspaceForUpdateCheck(string path)
    {
        comparisonBuild=ReadBuild();rivenValues[0].Text="입력 중";rivenAttempts.Text="17";ShowRivens();
        File.WriteAllText(path,JsonSerializer.Serialize(CaptureUpdateWorkspace(),Catalog.Json));
    }
    internal bool RestoreWorkspaceForUpdateCheck(string path)
    {
        var saved=JsonSerializer.Deserialize<UpdateWorkspace>(File.ReadAllText(path),Catalog.Json)??throw new InvalidOperationException("검증 작업 파일 누락");
        ApplyUpdateWorkspace(saved);
        if(rivenValues[0].Text!="입력 중"||rivenAttempts.Text!="17"||comparisonBuild is null||navigation[1].IsChecked!=true||ReadBuild().WeaponId!=saved.Build.WeaponId)
            throw new InvalidOperationException("설치 업데이트 후 모딩·리벤 작업 복구 실패");
        return true;
    }
}
