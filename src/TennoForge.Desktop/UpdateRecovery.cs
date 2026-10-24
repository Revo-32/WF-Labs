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
        Build? Comparison=null,int ActivePage=0,bool Light=false,bool Performance=false,Dictionary<string,RangedBuild>? Ranged=null,int EquipmentKind=0,Dictionary<string,RangedRivenWorkspace>? RangedRivens=null,int? LastModdingKind=null,
        Dictionary<string,RangedPage.SavedSession>? RangedComparisons=null,int? Samples=null);
    private static string RecoveryPath=>Path.Combine(ClientPreferences.Folder,"update-workspace.json");
    private UpdateWorkspace CaptureUpdateWorkspace()
    {
        var draft=new RivenDraft((rivenWeapon.SelectedItem as Weapon)?.Id,(int)rivenRank.SelectedItem,(int)rivenDestination.SelectedItem,
            rivenCost.Text,rivenPolarity.SelectedValue as string,traits.Select(c=>(c.SelectedItem as RivenOption)?.Stat).ToArray(),
            rivenValues.Select(v=>v.Text).ToArray(),negatives.Select(v=>v.IsChecked==true).ToArray(),rivenCycles.Text,rivenAttempts.Text,preserveSplice.IsChecked==true);
        Build build;try{build=ReadBuild();EvaluateBuild(build);lastWorkspaceBuild=build;}catch(ArgumentException)when(lastWorkspaceBuild is not null){build=lastWorkspaceBuild;}
        return new(build,draft,rivenSession,rivenBaseline,Comparison:comparisonBuild,
            ActivePage:navigation.FindIndex(t=>t.IsChecked==true),Light:!Theme.IsDark,Performance:lightMode,Ranged:rangedPages.ToDictionary(p=>p.Key,p=>p.Value.CaptureForSession()),EquipmentKind:equipmentKind.SelectedIndex,RangedRivens:rangedRivenPages.ToDictionary(p=>p.Key,p=>p.Value.CaptureWorkspace()),LastModdingKind:lastModdingKind,
            RangedComparisons:rangedPages.ToDictionary(p=>p.Key,p=>p.Value.CaptureSession()),Samples:SelectedSamples);
    }
    private void SaveUpdateRecovery()
    {
        var workspace=CaptureUpdateWorkspace();
        Directory.CreateDirectory(ClientPreferences.Folder);
        AtomicJsonFile.Write(RecoveryPath,workspace);
    }
    private void ApplyUpdateWorkspace(UpdateWorkspace workspace)
    {
        var d=workspace.Draft;
        if(workspace.Schema!=1||d is null||d.Stats?.Length!=4||d.Values?.Length!=4||d.Negatives?.Length!=4||d.Rank is <0 or >8||d.Destination is <1 or >8)
            throw new ArgumentException("업데이트 작업 복원 형식을 확인하세요.");
        if(workspace.Session is {} saved)RivenWorkshop.Validate(saved);
        if(workspace.Baseline is {} baseline)RivenValues.Validate(baseline);
        EvaluateBuild(workspace.Build);if(workspace.Comparison is {} comparison)EvaluateBuild(comparison);
        ApplyBuild(workspace.Build);
        if(workspace.Samples is {} samples)ApplySampleSetting(samples);
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
        foreach(var (category,build) in workspace.Ranged??[]){if(category is not ("primary" or "secondary"))throw new ArgumentException("총기 복구 구분 오류");if(!rangedPages.TryGetValue(category,out var ranged))rangedPages[category]=ranged=CreateRangedPage(category);ranged.Restore(build);}
        foreach(var (category,workshop) in workspace.RangedRivens??[])
        {
            if(category is not ("primary" or "secondary"))throw new ArgumentException("리벤 복구 구분 오류");
            ShowRanged(category);ShowRivens();rangedRivenPages[category].RestoreWorkspace(workshop);
        }
        equipmentKind.SelectedIndex=workspace.EquipmentKind;
        switch(workspace.ActivePage)
        {
            case 0:if(workspace.EquipmentKind is 1 or 2)ShowRanged(workspace.EquipmentKind==1?"primary":"secondary");else ShowModding();break;
            case 1:ShowRivens();break;
            case 2:ShowCatalog();break;
            case 3:ShowRoadmap();break;
        }
        lastModdingKind=workspace.LastModdingKind??workspace.EquipmentKind;RefreshWorkflowBar();
        RefreshRivenWorkbench();
        foreach(var (category,sessionState) in workspace.RangedComparisons??[])
            if(rangedPages.TryGetValue(category,out var ranged))ranged.RestoreSession(sessionState);
    }
    internal object CheckRangedRivenRecovery()
    {
        rangedRivenPages["primary"].PrepareRecoveryCheck(true);rangedRivenPages["secondary"].PrepareRecoveryCheck(false);
        ShowRanged("primary");ShowRivens();var saved=JsonSerializer.Deserialize<UpdateWorkspace>(JsonSerializer.Serialize(CaptureUpdateWorkspace(),Catalog.Json),Catalog.Json)!;
        for(int index=0;index<4;index++)
        {
            ApplyUpdateWorkspace(saved with{ActivePage=index});
            if(navigation.FindIndex(t=>t.IsChecked==true)!=index)throw new InvalidOperationException("리벤 복원 뒤 현재 페이지 불일치");
            foreach(var (category,expected) in saved.RangedRivens!)if(JsonSerializer.Serialize(expected,Catalog.Json)!=JsonSerializer.Serialize(rangedRivenPages[category].CaptureWorkspace(),Catalog.Json))throw new InvalidOperationException("총기 리벤의 미채택 결과·입력 중 텍스트 복원 누락");
        }
        return new{passed=true,checks=new[]{"주무기 미채택 순환·기준·슬롯 보존","보조무기 입력 중 텍스트 보존","모드·리벤·자료·개발 페이지 복원"}};
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
    internal async Task PrepareWorkspaceForUpdateCheck()
    {
        ApplySampleSetting(32);
        foreach(var category in new[]{"primary","secondary"})
        {
            ShowRanged(category);UpdateLayout();await rangedPages[category].PreparePersistentComparison();
            ShowRivens();
            var shape=new RangedRivenRoll(rangedPages[category].Capture().WeaponId,8,[new("Damage",0),new("StatusChance",0)],RivenBalance.Version);
            var roll=shape with{Traits=shape.Traits.Select(t=>{var range=RangedRivens.Range(shape,t);return t with{Value=Math.Round((range.Minimum+range.Maximum)/2,1)};}).ToArray()};
            rangedRivenPages[category].Set(roll);rangedRivenPages[category].PrepareRecoveryCheck(category=="primary");
        }
        equipmentKind.SelectedIndex=0;
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
        // Legacy releases have no ranged workspace. New releases must preserve it in full.
        var restored=CaptureUpdateWorkspace();
        foreach(var (category,build) in saved.Ranged??[])
        {
            if(JsonSerializer.Serialize(build,Catalog.Json)!=JsonSerializer.Serialize(restored.Ranged![category],Catalog.Json))
                throw new InvalidOperationException(category+" 업데이트 후 빌드 불일치");
            if(saved.RangedComparisons?.ContainsKey(category)==true)
                rangedPages[category].CheckUpdatedComparison(saved.RangedComparisons[category]);
        }
        foreach(var (category,draft) in saved.RangedRivens??[])
            if(JsonSerializer.Serialize(draft,Catalog.Json)!=JsonSerializer.Serialize(restored.RangedRivens![category],Catalog.Json))
                throw new InvalidOperationException(category+" 업데이트 후 미채택 순환·리벤 초안 불일치");
        return true;
    }
}
