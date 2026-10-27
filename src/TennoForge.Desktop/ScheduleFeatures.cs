using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    internal SchedulePreferences Preferences {get;private set;}=new();
    internal Button FeaturesButton=null!;
    private CheckBox? dialogOverlaySwitch;
    private bool syncingOverlay;
    private ScheduleOverlay? overlay;
    private ScheduleToast? toast;
    private Window? featureWindow;
    internal readonly Dictionary<string,RadioButton> FeatureTabs=new();
    private Action? refreshFeaturePreview;
    private readonly Dictionary<string,DateTimeOffset> delivered=new();
    private bool alertsLoaded;
    internal Action<ScheduleAlert[]>? AlertSink {get;set;}
    internal bool BackgroundNeeded=>Preferences.OverlayEnabled||Preferences.NotifyGameAlerts||Preferences.FissureRules.Any(r=>r.Enabled)||Preferences.NotifyFarmingGoals&&Farming?.Document.Goals.Any(g=>g.Enabled&&g.Notify)==true;
    private bool GoalMissionAllowed(FissureMission f)=>!Preferences.FarmingExcludedMissions.Contains(f.Mission,StringComparer.Ordinal)&&(Preferences.FarmingMode switch{1=>f.SteelPath&&!f.Storm,2=>!f.SteelPath&&!f.Storm,3=>f.Storm,_=>true});
    private FarmingMissionMatch[] cachedGoalMissions=[];
    private object? cachedGoalPlan,cachedGoalWorld;
    private DateTimeOffset nextGoalBoundary;
    private FarmingMissionMatch[] GoalMissions(DateTimeOffset now)
    {
        if(Farming is not {} goals)return [];
        if(!ReferenceEquals(cachedGoalPlan,goals.Plan)||!ReferenceEquals(cachedGoalWorld,Data.Snapshot)||now>=nextGoalBoundary)
        {
            cachedGoalMissions=FarmingMissions.Match(goals.Catalog,goals.Inventory,goals.Plan,Data.Snapshot?.Fissures??[],now);
            cachedGoalPlan=goals.Plan;cachedGoalWorld=Data.Snapshot;
            nextGoalBoundary=(Data.Snapshot?.Fissures??[]).SelectMany(f=>new[]{f.StartUtc,f.EndUtc}).Where(t=>t>now).DefaultIfEmpty(now.AddMinutes(1)).Min();
        }
        return cachedGoalMissions;
    }
    internal void ShowFarmingMissions(){Preferences=Preferences with{FissureView=5,ShowCurrentFissures=true};SavePreferences();OpenFeatures();}
    private SchedulePreferences ReadPreferences()
    {if(!writePreferences)return new();try{return (JsonSerializer.Deserialize<SchedulePreferences>(File.ReadAllText(Path.Combine(folder,"preferences.json")),Catalog.Json)??new()).Sanitize();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){return new();}}
    internal void SavePreferences()
    {
        Preferences=Preferences.Sanitize();if(writePreferences)try{Directory.CreateDirectory(folder);var p=Path.Combine(folder,"preferences.json");File.WriteAllText(p+".tmp",JsonSerializer.Serialize(Preferences,Catalog.Json));File.Move(p+".tmp",p,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status.Text="일정 설정 저장 실패 · 이번 실행에서는 유지됩니다.";}
        if(persist){if(BackgroundNeeded)StartMonitoring();else if(!IsLoaded)timer.Stop();}
    }
    internal void StartMonitoring()
    {if(!persist||disposed||!IsLoaded&&!BackgroundNeeded)return;if(!timer.IsEnabled){timer.Start();_=RefreshAsync(false);}}
    internal string OverlayText(DateTimeOffset now)=>OverlayData(now).PlainText;
    private void EnsureDelivered()
    {
        if(alertsLoaded)return;alertsLoaded=true;if(!writePreferences)return;
        try{var saved=JsonSerializer.Deserialize<Dictionary<string,DateTimeOffset>>(File.ReadAllText(Path.Combine(folder,"delivered.json")),Catalog.Json);foreach(var p in saved??[])if(p.Value>Clock())delivered[p.Key]=p.Value;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}
    }
    internal ScheduleAlert[] CollectAlerts()
    {
        EnsureDelivered();var now=Clock();foreach(var key in delivered.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())delivered.Remove(key);
        var seen=delivered.Keys.ToHashSet();var goalAlerts=Preferences.NotifyFarmingGoals&&Farming is {} goals?FarmingMissions.Due(goals.Catalog,goals.Inventory,goals.Plan,Data,now,seen,GoalMissionAllowed):[];
        var goalIds=goalAlerts.Select(a=>a.Id["goal-fissure:".Length..]).ToHashSet();
        var due=FissureAlerts.Due(Data,Preferences.FissureRules,now,seen).Where(a=>!goalIds.Contains(a.Id["fissure:".Length..])).Concat(GameAlerts.Due(Data,Preferences,now,seen)).Concat(goalAlerts).ToArray();if(due.Length==0)return [];
        foreach(var a in due)delivered[a.Id]=a.EventUtc;
        foreach(var a in goalAlerts)delivered["fissure:"+a.Id["goal-fissure:".Length..]]=a.EventUtc;
        if(writePreferences)try{Directory.CreateDirectory(folder);var p=Path.Combine(folder,"delivered.json");File.WriteAllText(p+".tmp",JsonSerializer.Serialize(delivered,Catalog.Json));File.Move(p+".tmp",p,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
        return due;
    }
    internal void TickFeatures()
    {
        if(disposed)return;
        refreshFeatureFilters?.Invoke();refreshFeaturePreview?.Invoke();
        syncingOverlay=true;if(dialogOverlaySwitch is not null)dialogOverlaySwitch.IsChecked=Preferences.OverlayEnabled;syncingOverlay=false;
        var now=Clock();var due=CollectAlerts();if(due.Length>0){if(AlertSink is {} sink)sink(due);else if(persist&&Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")!="1")Notify(due);}
        if(!persist||Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")=="1")return;
        if(!Preferences.OverlayEnabled&&overlay?.Editing!=true){overlay?.Conceal();return;}
        // The editing window must remain live outside the game. Passive overlays
        // need neither a new visual tree nor per-row text updates while concealed.
        if(overlay?.Editing==true){overlay.Update(TimeZoneInfo.ConvertTime(now,WorldScheduleEngine.Korea).ToString("HH:mm:ss"),OverlayData(now),Preferences);return;}
        var game=RelicCapture.ForegroundGameWindow();if(game==IntPtr.Zero){overlay?.Conceal();return;}
        overlay??=new(Preferences,p=>{Preferences=p;SavePreferences();TickFeatures();});overlay.Update(TimeZoneInfo.ConvertTime(now,WorldScheduleEngine.Korea).ToString("HH:mm:ss"),OverlayData(now),Preferences);
        try{overlay.PresentAt(RelicCapture.ClientBounds(game));}catch(InvalidOperationException){overlay.Conceal();}
    }
    private void Notify(ScheduleAlert[] due)
    {
        if(Preferences.Popup){toast?.Close();Int32Rect? bounds=null;try{var game=RelicCapture.GameWindow();if(RelicCapture.GetForegroundWindow()==game)bounds=RelicCapture.ClientBounds(game);}catch(InvalidOperationException){}toast=new(due.Length==1?due[0].Title:$"새 일정 알림 {due.Length}개",string.Join("\n\n",due.Take(4).Select(a=>a.Title+"\n"+a.Detail)),bounds,Preferences);toast.Show();}
        if(Preferences.Sound)_=ScheduleNotificationSound.Play(Preferences.SoundVolume);
    }
    private void DisposeFeatures(){featureWindow?.Close();overlay?.Close();toast?.Close();}
}
