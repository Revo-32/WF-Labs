using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal record RangedRivenDocument(RangedRivenWorkshopState Workshop,RangedRivenRoll? Baseline=null);

internal sealed partial class RangedRivenEditor
{
    private readonly Func<RangedBuild>? captureBuild;
    private readonly Func<int> captureDestination;
    private RangedRivenWorkshopState? session;
    private RangedRivenRoll? baseline;
    private bool applyingWorkshop,updatingControls;
    private CancellationTokenSource? comparing;
    private int comparisonGeneration;
    private readonly ComboBox locked=new(){DisplayMemberPath="Name"};
    private readonly CheckBox preserve=new(){Content="접합 옵션 유지",IsChecked=true,Margin=new(0,10,0,10)};
    private readonly TextBox cycles=new(){Text="0",MinWidth=100};
    private readonly TextBox attempts=new(){Text="10",MinWidth=100};
    private readonly ComboBox duration=new(){ItemsSource=new[]{"5초","10초","30초","60초"},SelectedIndex=0};
    private readonly ComboBox samples=new(){ItemsSource=new[]{256,1024,4096},SelectedIndex=0};
    private readonly TextBlock costs=Text(""),comparisonStatus=Text(""),baselineLabel=Text("비교 기준을 먼저 지정하세요.");
    private readonly StackPanel pending=new(),comparisonResults=new();
    private Button compareButton=null!,cancelButton=null!,cycleButton=null!,baselineButton=null!;
    private static Button ActionButton(string label,Action action)
    {
        var button=new Button{Content=label,Padding=new(12,8,12,8),Margin=new(0,0,8,8)};
        button.Click+=(_,_)=>action();return button;
    }
    private FrameworkElement CreateWorkshop()
    {
        void Guard(Action action){try{action();}catch(ArgumentException e){message.Text=e.Message;}}
        baselineButton=ActionButton("현재 리벤을 비교 기준으로 지정",()=>Guard(()=>{baseline=ReadWorkshop();InvalidateComparison();RefreshWorkshopControls();}));
        compareButton=ActionButton("DPS · TTK 비교",async()=>await CompareAsync());
        compareButton.SetResourceReference(BorderBrushProperty,"AccentBrush");
        cancelButton=ActionButton("계산 취소",()=>comparing?.Cancel());cancelButton.IsEnabled=false;
        cycleButton=ActionButton("순환 예시 생성",()=>WorkshopAction(state=>RangedRivenWorkshop.CycleExample(state,new Random())));
        var actions=new WrapPanel();actions.Children.Add(baselineButton);actions.Children.Add(compareButton);actions.Children.Add(cancelButton);
        var conditions=new UniformGrid{Columns=2,Margin=new(0,8,0,12)};
        conditions.Children.Add(Stack(Text("비교할 전투 시간"),duration));conditions.Children.Add(new Border{Margin=new(16,0,0,0),Child=Stack(Text("계산 표본 수"),samples)});
        var rerollSettings=new UniformGrid{Columns=2,Margin=new(0,8,0,12)};rerollSettings.Children.Add(Stack(Text("기존 순환 횟수"),cycles));rerollSettings.Children.Add(new Border{Margin=new(16,0,0,0),Child=Stack(Text("예산을 볼 순환 횟수"),attempts)});
        duration.SelectionChanged+=(_,_)=>InvalidateComparison();samples.SelectionChanged+=(_,_)=>InvalidateComparison();
        locked.SelectionChanged+=(_,_)=>{if(!updatingControls){InvalidateComparison();RefreshCosts();}};
        cycles.TextChanged+=(_,_)=>RefreshCosts();attempts.TextChanged+=(_,_)=>RefreshCosts();
        Unloaded+=(_,_)=>InvalidateComparison();
        var comparison=Stack(Text("DPS · TTK 비교",24),Text("모딩 화면의 모드·적·버프를 그대로 사용하고 리벤만 같은 슬롯에서 교체합니다."),baselineLabel,conditions,actions,comparisonStatus,comparisonResults);
        var workshop=Stack(Text("순환 작업대",22),Text(RivenWorkshop.SamplingAssumption,14),rerollSettings,Text("고정할 일반 옵션 · 쿠바 비용 2배"),locked,preserve,costs,cycleButton,pending);
        var layout=new Grid();layout.ColumnDefinitions.Add(new(){Width=new(1.65,GridUnitType.Star)});layout.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var side=new Border{Child=workshop,Padding=new(20,0,0,0),Margin=new(20,0,0,0),BorderThickness=new(1,0,0,0)};side.SetResourceReference(BorderBrushProperty,"StrokeBrush");layout.Children.Add(comparison);Grid.SetColumn(side,1);layout.Children.Add(side);
        layout.SizeChanged+=(_,_)=>
        {
            bool wide=layout.ActualWidth>=950;Grid.SetColumnSpan(comparison,wide?1:2);Grid.SetColumn(side,wide?1:0);Grid.SetColumnSpan(side,wide?1:2);Grid.SetRow(side,wide?0:1);
            side.Margin=wide?new(20,0,0,0):new(0,24,0,0);side.Padding=wide?new(20,0,0,0):new(0,20,0,0);side.BorderThickness=wide?new(1,0,0,0):new(0,1,0,0);
        };
        return layout;
    }
    private RangedRivenRoll ReadWorkshop()
    {
        if(!string.IsNullOrWhiteSpace(cost.Text)&&!int.TryParse(cost.Text,out _))throw new ArgumentException("코스트는 정수로 입력하거나 비워두세요.");
        var roll=Read();RangedRivens.Validate(roll,requireMetadata:false);return roll;
    }
    private RangedRivenWorkshopState ReadSession()
    {
        if(session?.Pending is not null)return session;
        if(!int.TryParse(cycles.Text,out var count)||count<0)throw new ArgumentException("순환 횟수는 0 이상의 정수로 입력하세요.");
        var state=(session??new(ReadWorkshop())) with{Current=ReadWorkshop(),Cycles=count,Locked=(locked.SelectedItem as Choice)?.Key is {Length:>0} key?key:null,PreserveSplice=preserve.IsChecked==true};
        RangedRivenWorkshop.Validate(state);return state;
    }
    internal RangedRivenDocument Export()=>new(ReadSession(),baseline);
    internal void Import(RangedRivenDocument document)
    {
        RangedRivenWorkshop.Validate(document.Workshop);
        if(document.Workshop.Current.WeaponId!=weapon.Id)throw new ArgumentException("작업 파일의 무기가 다릅니다.");
        if(document.Baseline is {} old){RangedRivens.Validate(old,requireMetadata:false);if(old.WeaponId!=weapon.Id)throw new ArgumentException("비교 기준의 무기가 다릅니다.");}
        baseline=document.Baseline;SetWorkshop(document.Workshop);
    }
    private void WorkshopAction(Func<RangedRivenWorkshopState,RangedRivenWorkshopState> action)
    {
        try{var state=ReadSession();baseline??=state.Current;SetWorkshop(action(state));sections.Select(3);}
        catch(ArgumentException ex){message.Text=ex.Message;}
    }
    private void SetWorkshop(RangedRivenWorkshopState state)
    {
        RangedRivenWorkshop.Validate(state);session=state;applyingWorkshop=true;
        try
        {
            var roll=state.Current;rank.SelectedIndex=roll.Rank;cost.Text=(roll.BaseDrain+roll.Rank)?.ToString()??"";polarity.SelectedItem=polarity.Items.Cast<Choice>().SingleOrDefault(c=>c.Key==roll.Polarity);
            for(int i=0;i<4;i++){var trait=roll.Traits.ElementAtOrDefault(i);rows[i].Stat.SelectedItem=rows[i].Stat.Items.Cast<Choice>().Single(c=>c.Key==(trait?.Stat??""));rows[i].Value.Text=trait?.Value.ToString(CultureInfo.InvariantCulture)??"";rows[i].Sign.SelectedIndex=trait?.Negative==true?1:0;}
            cycles.Text=state.Cycles.ToString();preserve.IsChecked=state.PreserveSplice;
        }
        finally{applyingWorkshop=false;}
        Refresh();updatingControls=true;locked.SelectedItem=locked.Items.Cast<Choice>().SingleOrDefault(c=>c.Key==(state.Locked??""));updatingControls=false;RefreshCosts();
    }
    private void RefreshWorkshopControls()
    {
        if(locked is null||cycleButton is null)return;
        updatingControls=true;var previous=(locked.SelectedItem as Choice)?.Key??session?.Locked??"";
        var roll=Read();locked.ItemsSource=new[]{new Choice("","고정 안 함")}.Concat(roll.Traits.Where(t=>!t.Spliced).Select(t=>new Choice(t.Stat,(t.Negative?"− ":"+ ")+RangedRivens.Attribute(t.Stat).Name))).ToArray();locked.SelectedItem=locked.Items.Cast<Choice>().FirstOrDefault(c=>c.Key==previous)??locked.Items[0];updatingControls=false;
        bool editable=session?.Pending is null;
        foreach(var row in rows){row.Stat.IsEnabled=editable;row.Sign.IsEnabled=editable;row.Value.IsEnabled=editable;}foreach(var endpoint in endpoints)endpoint.IsEnabled=editable;
        rank.IsEnabled=cost.IsEnabled=polarity.IsEnabled=cycles.IsEnabled=locked.IsEnabled=preserve.IsEnabled=cycleButton.IsEnabled=baselineButton.IsEnabled=editable;
        baselineLabel.Text=baseline is null?"비교 기준을 먼저 지정하세요.":"기준: "+Describe(baseline);
        compareButton.IsEnabled=captureBuild is not null&&baseline is not null&&comparing is null;
        pending.Children.Clear();
        if(session?.Pending is {} next)
        {
            pending.Children.Add(Text("새 순환 결과 · 아직 채택하지 않음",20));
            pending.Children.Add(Text("유지할 리벤을 선택하세요. 비교 버튼은 새 결과를 후보로 계산합니다."));
            var pair=new UniformGrid{Columns=2};pair.Children.Add(Stack(Text("현재",17),Text(Describe(session.Current))));pair.Children.Add(Stack(Text("새 결과",17),Text(Describe(next))));pending.Children.Add(pair);
            var choices=new WrapPanel();choices.Children.Add(ActionButton("현재 옵션 유지",()=>WorkshopAction(s=>RangedRivenWorkshop.Resolve(s,false))));choices.Children.Add(ActionButton("새 결과 채택",()=>WorkshopAction(s=>RangedRivenWorkshop.Resolve(s,true))));pending.Children.Add(choices);
        }
        RefreshCosts();
    }
    private static string Describe(RangedRivenRoll roll)=>string.Join(" · ",roll.Traits.Select(t=>$"{RangedRivens.Attribute(t.Stat).Name} {(t.Negative?"−":"+")}{t.Value:0.###}{RangedRivens.Attribute(t.Stat).Unit}"));
    private void RefreshCosts()
    {
        if(applyingWorkshop)return;
        try
        {
            if(!int.TryParse(cycles.Text,out var count)||!int.TryParse(attempts.Text,out var times))throw new ArgumentException("순환 횟수와 예산 횟수를 정수로 입력하세요.");
            bool hasLock=(locked.SelectedItem as Choice)?.Key is {Length:>0};
            costs.Text=$"다음 순환 {RivenWorkshop.NextCost(count,hasLock):N0} 쿠바 · {times:N0}회 예산 {RivenWorkshop.Budget(count,times,hasLock):N0} 쿠바\n작업 중 소비: {session?.KuvaSpent??0:N0} 쿠바 · 접합기 {session?.SplicersSpent??0:N0}개";
        }catch(ArgumentException e){costs.Text=e.Message;}
    }
    internal void InvalidateComparison()
    {
        comparisonGeneration++;comparing?.Cancel();comparisonResults.Children.Clear();comparisonStatus.Text="";
    }
    private async Task CompareAsync()
    {
        InvalidateComparison();var generation=comparisonGeneration;var source=new CancellationTokenSource();comparing=source;
        compareButton.IsEnabled=false;cancelButton.IsEnabled=true;
        try
        {
            var old=baseline??throw new ArgumentException("먼저 비교 기준을 지정하세요.");var candidate=session?.Pending??ReadWorkshop();
            var build=captureBuild?.Invoke()??throw new ArgumentException("모딩 화면에서 무기와 대상을 먼저 설정하세요.");
            if(bindToCurrentBuild){old=RivenBuildBinding.Bind(old,build);candidate=RivenBuildBinding.Bind(candidate,build);}
            // Freeze nested input collections before leaving the UI thread.
            build=JsonSerializer.Deserialize<RangedBuild>(JsonSerializer.Serialize(build,Catalog.Json),Catalog.Json)!;
            int slot=captureDestination();var settings=new RangedEncounter(new[]{5d,10,30,60}[duration.SelectedIndex],(int)samples.SelectedItem);
            comparisonStatus.Text="기준과 후보를 계산하고 있습니다…";
            var progress=new Progress<SimulationProgress>(p=>{if(generation==comparisonGeneration)comparisonStatus.Text=$"계산 중 · {p.Phase} {p.Completed:N0}/{p.Total:N0}";});
            var result=await Task.Run(()=>RangedRivenComparison.Calculate(build,old,candidate,slot,settings,source.Token,progress),source.Token);
            if(generation!=comparisonGeneration||source.IsCancellationRequested)return;
            ShowComparison(result,build,slot);comparisonStatus.Text="비교 완료 · "+(session?.Pending is null?"현재 리벤이 후보입니다.":"미채택 순환 결과가 후보입니다.");
        }
        catch(OperationCanceledException){if(generation==comparisonGeneration)comparisonStatus.Text="계산을 취소했습니다.";}
        catch(ArgumentException ex){if(generation==comparisonGeneration)comparisonStatus.Text=ex.Message;}
        finally{if(comparing==source){comparing=null;cancelButton.IsEnabled=false;compareButton.IsEnabled=captureBuild is not null&&baseline is not null;}source.Dispose();}
    }
    private void ShowComparison(RangedRivenComparisonResult result,RangedBuild build,int slot)
    {
        comparisonResults.Children.Clear();
        var enemy=build.Enemy is {} e?EnemyCatalog.Load().Get(e.EnemyId).Name:"";
        comparisonResults.Children.Add(Text($"{RangedEngine.Weapons.Single(w=>w.Id==build.WeaponId).Name} · {enemy} · 레벨 {build.Enemy?.Level} · 슬롯 {slot+1} · {result.Settings.Duration:0}초 · {result.Settings.Trials:N0}표본",14));
        var values=new[]{result.Baseline,result.Candidate};double maximum=values.Max(r=>r.Dps.Mean);
        for(int i=0;i<2;i++)
        {
            var value=values[i];var stats=i==0?result.BaselineStats:result.CandidateStats;
            var row=new UniformGrid{Columns=3};row.Children.Add(Stack(Text(i==0?"기준 DPS":"후보 DPS"),Text($"{value.Dps.Mean:N1}",28)));
            row.Children.Add(Stack(Text("처치 시간 · 중앙값"),Text(value.Ttk.Median is {} median?$"{median:0.###}초":$"> {result.Settings.Duration:0}초",24)));
            row.Children.Add(Stack(Text("처치 비율"),Text($"{value.Ttk.KillFraction:P1}",24)));comparisonResults.Children.Add(row);
            var track=new Grid{Height=12,Margin=new(0,0,0,12)};track.SetResourceReference(Panel.BackgroundProperty,"StrokeBrush");double share=maximum>0?value.Dps.Mean/maximum:0;track.ColumnDefinitions.Add(new(){Width=new(share,GridUnitType.Star)});track.ColumnDefinitions.Add(new(){Width=new(1-share,GridUnitType.Star)});var fill=new Border();fill.SetResourceReference(BackgroundProperty,i==0?"MutedBrush":"AccentBrush");track.Children.Add(fill);comparisonResults.Children.Add(track);
            comparisonResults.Children.Add(Text($"치명타 {stats.CriticalChance:P1} · 상태 이상 {stats.StatusChance:P1} · 멀티샷 {stats.Multishot:0.##} · DPS 95% 오차폭 ±{value.Dps.Approximate95HalfWidth:N1}",14));
            if(stats.Drain>stats.Capacity)comparisonResults.Children.Add(RivenPresentation.Text($"{(i==0?"기준":"후보")} 수용량 {stats.Drain}/{stats.Capacity} 초과 · 실제 장착 불가, 가정 비교입니다.",14,"ErrorBrush"));
        }
        comparisonResults.Children.Add(Text(result.Baseline.Dps.Mean>0?$"DPS 변화  {(result.Candidate.Dps.Mean/result.Baseline.Dps.Mean-1)*100:+0.0;-0.0;0.0}%":"기준 DPS가 0이므로 증감률을 계산할 수 없습니다.",20));
        comparisonResults.Children.Add(Text("누적 피해 · 같은 시간축",18));var plot=new RivenCombatPlot();plot.Set(result.Baseline.Points,result.Candidate.Points);comparisonResults.Children.Add(plot);
        comparisonResults.Children.Add(Text("직접 + 상태 피해 포함 · 대상 처치 후에는 피해가 늘지 않습니다. TTK는 이 전투 시간 안에서의 처치 분포입니다.",14));
        var details=new StackPanel();
        var difficulty=build.Enemy?.Difficulty switch{EnemyDifficulty.SteelPath=>"강철의 길",EnemyDifficulty.Icebind=>"빙주령",_=>"일반"};
        details.Children.Add(Text($"{difficulty} · {(build.Enemy?.Eximus==true?"엑시무스":"일반 적")} · 대상 {build.Conditions?.GroupCount??1}명 · 거리 {build.Distance:0.##}m · 시드 {result.Settings.Seed}"));
        details.Children.Add(Text("공격: "+RangedEngine.S(RangedEngine.Attacks(RangedEngine.Resolve(build))[build.AttackIndex],"AttackName")));
        foreach(var note in result.BaselineStats.Scope.Concat(result.CandidateStats.Scope).Distinct())details.Children.Add(Text(note));
        foreach(var (label,value) in new[]{("기준",result.Baseline),("후보",result.Candidate)})
        {
            details.Children.Add(Text($"{label} · 누적 피해 {value.Damage.Mean:N1} · TTK P10 {Time(value.Ttk.P10)} / P90 {Time(value.Ttk.P90)}",16));
            foreach(var note in value.Notes)details.Children.Add(Text(note));
            details.Children.Add(new Expander{Header=label+" 발사·상태·처치 이벤트 · 첫 표본",MaxHeight=300,Content=Scroll(Stack(value.Events.Select(e=>(UIElement)Text($"{e.Time:0.000}초 · 적 {e.Target+1} · {RangedEventLabels.Detail(e.Detail)} · {e.Value:N1}")).ToArray()))});
        }
        comparisonResults.Children.Add(new Expander{Header="통계 · 계산 범위 · 사건 기록",Content=details});
        string Time(double? time)=>time is {} t?$"{t:0.###}초":$">{result.Settings.Duration:0}초";
    }
}
