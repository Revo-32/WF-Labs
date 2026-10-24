using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RangedPage
{
    internal sealed record CombatSnapshot(RangedBuild Build,RangedCombatResult Result,RangedEncounter Encounter,RangedCombatResult? Without=null);
    internal sealed record SavedSession(CombatSnapshot? Baseline,CombatSnapshot? Current,string Engine);
    private bool comparisonCacheValid=true;
    private static readonly Lazy<string> EngineIdentity=new(()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(typeof(RangedEngine).Assembly.Location))));
    internal SavedSession CaptureSession()=>new(comparisonBaseline,lastCombat,comparisonCacheValid?EngineIdentity.Value:"invalidated");
    internal void RestoreSession(SavedSession saved)
    {
        if(saved.Baseline is {} baseline)ValidateBuildForRestore(baseline.Build);
        comparisonBaseline=saved.Baseline;comparisonCacheValid=saved.Baseline is null||saved.Engine==EngineIdentity.Value;
        if(saved.Engine==EngineIdentity.Value&&saved.Current is {} current&&JsonSerializer.Serialize(current.Build,Catalog.Json)==JsonSerializer.Serialize(Capture(),Catalog.Json))
        {
            lastCombat=current;ShowCombat(current.Result,current.Without??current.Result,current.Encounter);SetAccuracy(current.Result.Notes,true);pinComparison.IsEnabled=true;
            progressText.Text=$"이전 교전 결과 복원 · {current.Encounter.Trials:N0}표본";
        }
        RefreshComparison();
    }
    private CombatSnapshot? lastCombat,comparisonBaseline;
    private readonly ContentControl comparisonHost=new();
    private readonly Expander approximationDetails=new(){Header="적용한 값 · 근거 · 결과에 미치는 영향",Visibility=Visibility.Collapsed,Margin=new(0,0,0,12)};
    private readonly TextBlock accuracyStatus=Text("교전 계산 전",14);
    private Button pinComparison=null!,restoreComparison=null!,clearComparison=null!,accuracyDetailsButton=null!;

    private FrameworkElement BuildComparisonTools()
    {
        pinComparison=Button("비교 기준 지정",()=>
        {
            if(lastCombat is null)return;
            comparisonBaseline=lastCombat;comparisonCacheValid=true;RefreshComparison();resultScroller.ScrollToTop();WorkspaceChanged?.Invoke();
        });
        pinComparison.IsEnabled=false;pinComparison.ToolTip="계산이 끝난 빌드와 교전 결과를 A로 고정합니다. 모드를 바꾸고 다시 계산하면 B와 비교합니다.";
        restoreComparison=Button("기준 빌드 복원",()=>{if(comparisonBaseline is {} saved)Restore(CloneBuild(saved.Build));});
        clearComparison=Button("비교 해제",()=>{comparisonBaseline=null;comparisonCacheValid=true;RefreshComparison();WorkspaceChanged?.Invoke();});
        restoreComparison.Visibility=clearComparison.Visibility=Visibility.Collapsed;
        var tools=new WrapPanel{Margin=new(0,2,0,4)};
        tools.Children.Add(pinComparison);tools.Children.Add(restoreComparison);tools.Children.Add(clearComparison);return tools;
    }
    private FrameworkElement BuildAccuracyStatus()
    {
        var row=new DockPanel{Margin=new(0,0,0,8)};
        accuracyDetailsButton=Button("가정 보기",()=>
        {
            approximationDetails.IsExpanded=!approximationDetails.IsExpanded;resultScroller.ScrollToTop();
        });
        accuracyDetailsButton.Margin=new(8,0,0,0);accuracyDetailsButton.Visibility=Visibility.Collapsed;
        DockPanel.SetDock(accuracyDetailsButton,Dock.Right);row.Children.Add(accuracyDetailsButton);
        accuracyStatus.Margin=new(0);accuracyStatus.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(accuracyStatus);return row;
    }
    private void SetAccuracy(IEnumerable<string> notes,bool completed,bool invalid=false)
    {
        var assumptions=notes.Where(n=>n.StartsWith("근사 · ")).Distinct().ToArray();
        accuracyStatus.Text=invalid?"입력·규칙 확인 필요 · 교전 결과 없음":assumptions.Length>0
            ?$"{(completed?"근사 포함":"근사 예정")} · {assumptions.Length}개 가정 · {(completed?"계산 완료":"교전 계산 전")}"
            :$"기록된 근사 없음 · {(completed?"계산 완료":"교전 계산 전")}";
        if(!storedConditions.AllowApproximations&&!invalid)accuracyStatus.Text+=" · 근사 꺼짐";
        accuracyStatus.SetResourceReference(TextBlock.ForegroundProperty,invalid?"ErrorBrush":assumptions.Length>0?"AccentBrush":"MutedBrush");
        accuracyStatus.ToolTip=assumptions.Length>0?string.Join("\n\n",assumptions):"기록된 근사 가정의 유무입니다. 모든 규칙이 실측으로 확정되었다는 의미는 아닙니다.";
        accuracyDetailsButton.Visibility=approximationDetails.Visibility=assumptions.Length>0?Visibility.Visible:Visibility.Collapsed;
        approximationDetails.Content=Stack(assumptions.Select(n=>(UIElement)Text(n,14)).Append(Text("95% 통계 오차는 표본 추출 오차이며, 근사 모델 자체의 오차를 포함하지 않습니다.",13)).ToArray());
    }
    private void InvalidateCombat()
    {
        var pending=simulation;simulation=null;pending?.Cancel();
        lastCombat=null;calculateButton.IsEnabled=true;cancelButton.Visibility=Visibility.Collapsed;pinComparison.IsEnabled=false;
    }
    private static RangedBuild CloneBuild(RangedBuild build)=>JsonSerializer.Deserialize<RangedBuild>(JsonSerializer.Serialize(build,Catalog.Json),Catalog.Json)!;
    private static string ScenarioKey(CombatSnapshot snapshot)=>JsonSerializer.Serialize(new
    {
        snapshot.Build.Enemy,snapshot.Build.Distance,snapshot.Build.EncounterMode,
        snapshot.Build.Conditions,snapshot.Encounter.Duration,snapshot.Encounter.FirstShot
    },Catalog.Json);
    private static string TtkText(CombatSnapshot snapshot)=>snapshot.Result.Ttk.Median is {} ttk?$"{ttk:0.###}초":snapshot.Encounter.FirstShot?"첫 발 처치 미달":$"> {snapshot.Encounter.Duration:0.##}초";
    private static string SnapshotLabel(CombatSnapshot snapshot)
    {
        var build=snapshot.Build;var weapon=RangedEngine.Resolve(build);
        var target=build.Enemy is {} e?$"{EnemyCatalog.Load().Get(e.EnemyId).Name} Lv.{e.Level} · {(new[]{"일반","강철의 길","빙주령 · 6인"})[(int)e.Difficulty]}{(e.Eximus?" · 엑시무스":"")}":"대상 없음";
        var assumptions=snapshot.Result.Notes.Count(n=>n.StartsWith("근사 · "));
        return $"{weapon.Name} · 모드 {build.Slots.Count(s=>s is not null)}개\n{target}\n{(snapshot.Encounter.FirstShot?"첫 발사":$"{snapshot.Encounter.Duration:0.##}초 교전")} · {(assumptions>0?$"근사 {assumptions}개":"기록된 근사 없음")}";
    }
    private void RefreshComparison()
    {
        var baseline=comparisonBaseline;
        restoreComparison.Visibility=clearComparison.Visibility=baseline is null?Visibility.Collapsed:Visibility.Visible;
        comparisonHost.Content=null;if(baseline is null)return;
        var panel=Stack(Text("빌드 비교",18));
        if(!comparisonCacheValid)
        {
            panel.Children.Add(Text("계산 데이터가 변경되었습니다. 기준 빌드를 복원하고 다시 계산해 비교 기준으로 지정하세요.",14));
            comparisonHost.Content=RivenPresentation.Rule(panel);return;
        }
        if(lastCombat is not {} current)
        {
            panel.Children.Add(Text("A 기준 유지 · 현재 빌드의 DPS · TTK를 계산하면 B와 비교합니다.",14));
            panel.Children.Add(Text(SnapshotLabel(baseline),13));comparisonHost.Content=RivenPresentation.Rule(panel);return;
        }
        var sameScenario=ScenarioKey(baseline)==ScenarioKey(current);
        var columns=new UniformGrid{Columns=2};
        foreach(var (snapshot,label) in new[]{(baseline,"A · 기준"),(current,"B · 현재")})
        {
            var first=snapshot.Encounter.FirstShot;
            var estimate=first?snapshot.Result.Damage:snapshot.Result.Dps;
            var cell=Stack(Text(label,16),Text(SnapshotLabel(snapshot),13),Text(first?"첫 발사 기대 피해":"교전 평균 DPS",14),Text(estimate.Mean.ToString("N1"),26),Text($"처치 시간 {TtkText(snapshot)} · 처치 {snapshot.Result.Ttk.KillFraction:P0}",14));
            columns.Children.Add(new Border{Padding=new(0,0,16,0),Child=cell});
        }
        panel.Children.Add(columns);
        if(sameScenario)
        {
            var a=baseline.Encounter.FirstShot?baseline.Result.Damage:baseline.Result.Dps;
            var b=current.Encounter.FirstShot?current.Result.Damage:current.Result.Dps;
            var change=b.Mean-a.Mean;
            var delta=Text($"B − A  {change:+#,##0.0;-#,##0.0;0.0}"+(a.Mean>0?$"  ({change/a.Mean:+0.0%;-0.0%;0.0%})":" · 기준값 0, 비율 없음"),20);
            delta.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");panel.Children.Add(delta);
            panel.Children.Add(Text($"동일한 입력 교전 조건 · 각 {a.Samples:N0}/{b.Samples:N0}표본 · 평균의 95% 통계 오차 A ±{a.Approximate95HalfWidth:N1} / B ±{b.Approximate95HalfWidth:N1}",13));
            if(!current.Encounter.FirstShot)
            {
                var plot=new RangedDpsPlot();plot.Set(current.Result.Points,baseline.Result.Points,"B 현재","A 기준");
                panel.Children.Add(new Expander{Header="A / B 시간별 DPS 그래프",Content=plot});
            }
        }
        else
        {
            var warning=Text("교전 조건이 다릅니다 · 대상·거리·버프·교전 구간을 맞춰 다시 계산하세요. 증감률은 표시하지 않습니다.",14);
            warning.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");panel.Children.Add(warning);
        }
        panel.Children.Add(new Expander{Header="A / B 장착 모드 · 근사 가정",Content=Stack(new[]{(baseline,"A"),(current,"B")}.SelectMany(pair=>
        {
            var (snapshot,label)=pair;var list=snapshot.Build.Slots.Select((slot,i)=>$"{i+1}. "+(slot is null?"빈 슬롯":RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Name+$" · 랭크 {slot.Rank}"));
            return new UIElement[]{Text(label+" · "+string.Join(" / ",list),14),Text(string.Join("\n",snapshot.Result.Notes.Where(n=>n.StartsWith("근사 · "))),13)};
        }).ToArray())});
        comparisonHost.Content=RivenPresentation.Rule(panel);
    }
}
