using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private RivenWorkshopState? rivenSession;
    private RivenRoll? rivenBaseline;
    private readonly TextBox rivenCycles=new(){Text="0"},rivenAttempts=new(){Text="10"};
    private readonly ComboBox rivenLock=new(){DisplayMemberPath="Name",SelectedValuePath="Stat"};
    private readonly CheckBox preserveSplice=new(){Content="접합 옵션 유지",IsChecked=true};
    private readonly TextBlock workshopSummary=T("",14),rivenComparison=T("",14);
    private readonly StackPanel pendingRollPanel=new(){Visibility=Visibility.Collapsed};
    private readonly TextBlock rivenComparisonNotes=T("",14);
    private readonly Expander rivenNotesExpander=new(){Header="계산 근거 · 적용 조건",Visibility=Visibility.Collapsed};
    private readonly Expander rivenResultDetails=new(){Header="비교 수치 · TTK",IsExpanded=true};
    private bool hasRivenComparison;
    private CancellationTokenSource? rivenCompareCancel;
    private int rivenCompareGeneration;
    private Button rivenCompareButton=null!,rivenCancelButton=null!;
    private void InvalidateRivenComparison()
    {
        rivenCompareCancel?.Cancel();rivenCompareGeneration++;
        rivenNotesExpander.Visibility=Visibility.Collapsed;rivenDpsCharts.Children.Clear();
        rivenResultDetails.IsExpanded=true;
        rivenComparison.Text=hasRivenComparison?"입력이 변경되었습니다. 현재 조건으로 다시 비교해 주세요.":"현재 리벤을 비교 기준으로 지정한 뒤 후보를 비교하세요.";
    }

    private FrameworkElement CreateRivenSessionPanel()
    {
        rivenCompareButton=Button("현재 모딩 조건에서 비교",async()=>await CompareRivenAsync());
        rivenCancelButton=Button("비교 취소",()=>rivenCompareCancel?.Cancel());rivenCancelButton.IsEnabled=false;
        rivenNotesExpander.Content=rivenComparisonNotes;rivenResultDetails.Content=rivenComparison;
        var panel=SpacedStack(12,
            T("빌드 비교",20),Button("현재 리벤을 비교 기준으로 지정",()=>
            {try{InvalidateRivenComparison();rivenBaseline=ReadCostedRiven();rivenComparison.Text="비교 기준을 저장했습니다. 후보를 입력하거나 접합 예시를 선택하세요.";}catch(ArgumentException ex){rivenComparison.Text=ex.Message;}}),
            Columns(rivenCompareButton,rivenCancelButton,2,1),rivenDpsCharts,rivenResultDetails,rivenNotesExpander,
            T("접합 · 순환 작업대",20),T(RivenWorkshop.SamplingAssumption,14,"#596B85"),
            Columns(Field("현재 순환 횟수",rivenCycles),Field("예산 계산 횟수",rivenAttempts)),
            Field("일반 옵션 고정 · 순환 비용 2배",rivenLock),preserveSplice,workshopSummary,
            Button("순환 예시 생성",()=>WorkshopAction(s=>RivenWorkshop.CycleExample(s,Random.Shared))),pendingRollPanel,
            Columns(Button("작업 저장",SaveRivenSession),Button("작업 불러오기",LoadRivenSession)));
        foreach(var text in panel.Children.OfType<TextBlock>())text.TextWrapping=TextWrapping.Wrap;
        rivenCycles.TextChanged+=(_,_)=>RefreshWorkshopSummary();rivenAttempts.TextChanged+=(_,_)=>RefreshWorkshopSummary();
        rivenLock.SelectionChanged+=(_,_)=>RefreshWorkshopSummary();
        preserveSplice.Checked+=(_,_)=>RefreshWorkshopSummary();preserveSplice.Unchecked+=(_,_)=>RefreshWorkshopSummary();
        rivenCost.TextChanged+=(_,_)=>InvalidateRivenComparison();rivenPolarity.SelectionChanged+=(_,_)=>InvalidateRivenComparison();rivenDestination.SelectionChanged+=(_,_)=>InvalidateRivenComparison();
        Closed+=(_,_)=>rivenCompareCancel?.Cancel();
        return panel;
    }
    private RivenRoll ReadCostedRiven()
    {
        var roll=ReadRivenWorkbench();RivenValues.Validate(roll);
        var cost=Integer(rivenCost,"리벤 코스트")-roll.Rank;
        if(cost<0||cost>30||rivenPolarity.SelectedValue is not string polarity)throw new ArgumentException("표시 코스트와 극성을 입력하세요.");
        return roll with{BaseDrain=cost,Polarity=polarity};
    }
    private RivenRoll ReadRivenForWorkshop()
    {
        var roll=ReadRivenWorkbench();RivenValues.Validate(roll);int? drain=null;
        if(!string.IsNullOrWhiteSpace(rivenCost.Text)){drain=Integer(rivenCost,"리벤 코스트")-roll.Rank;if(drain<0||drain>30)throw new ArgumentException("표시 코스트를 확인하세요.");}
        return roll with{BaseDrain=drain,Polarity=rivenPolarity.SelectedValue as string};
    }
    private void RefreshWorkshopSummary()
    {
        try
        {
            var cycles=Integer(rivenCycles,"순환 횟수");var attempts=Integer(rivenAttempts,"예산 횟수");var locked=rivenLock.SelectedValue is RivenStat;
            workshopSummary.Text=$"다음 순환 {RivenWorkshop.NextCost(cycles,locked):N0} 쿠바 · {attempts:N0}회 예산 {RivenWorkshop.Budget(cycles,attempts,locked):N0}\n작업 중 소비 {rivenSession?.KuvaSpent??0:N0} 쿠바 · 접합기 {rivenSession?.SplicersSpent??0}개"+
                (preserveSplice.IsChecked==false?"\n접합 유지 해제: 다음 새 결과를 채택할 때 접합 옵션이 제거됩니다.":"\n접합 보존 자체에는 추가 쿠바 비용이 없습니다.");
        }catch(ArgumentException ex){workshopSummary.Text=ex.Message;}
    }
    private void RefreshRivenLocks()
    {
        var selected=rivenLock.SelectedValue;
        var options=new[]{new RivenOption(null,"고정 안 함")}.Concat(traits.Select(c=>c.SelectedItem as RivenOption).Where(o=>o?.Stat is {} s&&!RivenValues.IsSpliced(s)).Cast<RivenOption>()).ToArray();
        rivenLock.ItemsSource=options;rivenLock.SelectedItem=options.FirstOrDefault(o=>Equals(o.Stat,selected))??options[0];RefreshWorkshopSummary();
    }
    private void WorkshopAction(Func<RivenWorkshopState,RivenWorkshopState> action)
    {
        try
        {
            var currentRoll=ReadRivenForWorkshop();var state=rivenSession??new(currentRoll);
            state=state with{Current=currentRoll,Cycles=Integer(rivenCycles,"순환 횟수"),Locked=rivenLock.SelectedValue as RivenStat?,PreserveSplice=preserveSplice.IsChecked==true};
            rivenBaseline??=currentRoll;SetWorkshop(action(state));rivenSections?.Select(rivenSession?.Pending is null?0:3);
        }catch(ArgumentException ex){rivenInfo.Text=ex.Message;}
    }
    private void SetWorkshop(RivenWorkshopState state)
    {
        rivenSession=state;ApplyRivenWorkbench(state.Current);rivenCycles.Text=state.Cycles.ToString();rivenLock.SelectedValue=state.Locked;preserveSplice.IsChecked=state.PreserveSplice;
        pendingRollPanel.Children.Clear();
        pendingRollPanel.Visibility=state.Pending is null?Visibility.Collapsed:Visibility.Visible;
        if(state.Pending is {} pending)
        {
            pendingRollPanel.Children.Add(T("새 순환 예시 · 아직 미채택",16));
            foreach(var t in pending.Traits)pendingRollPanel.Children.Add(T($"{RivenEngine.Label(t.Stat)} {(t.Negative?"−":"+")}{t.Value:0.###}{RivenValues.Unit(t.Stat)}",14));
            pendingRollPanel.Children.Add(Columns(Button("현재 옵션 유지",()=>SetWorkshop(RivenWorkshop.Resolve(rivenSession!,false))),Button("새 결과 채택",()=>SetWorkshop(RivenWorkshop.Resolve(rivenSession!,true)))));
        }
        foreach(var control in traits.Cast<Control>().Concat(negatives).Concat(rivenValues).Concat(new Control[]{rivenWeapon,rivenRank,rivenCost,rivenPolarity,rivenCycles,rivenLock,preserveSplice}))control.IsEnabled=state.Pending is null;
        RefreshWorkshopSummary();
    }
    private void SaveRivenSession()
    {
        try
        {
            var state=(rivenSession??new(ReadRivenForWorkshop())) with{Current=ReadRivenForWorkshop(),Cycles=Integer(rivenCycles,"순환 횟수"),Locked=rivenLock.SelectedValue as RivenStat?,PreserveSplice=preserveSplice.IsChecked==true};RivenWorkshop.Validate(state);
            var dialog=new SaveFileDialog{Filter="WF Labs 리벤 작업|*.wfriven",FileName="리벤 작업.wfriven"};
            if(dialog.ShowDialog(this)==true)File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(state,Catalog.Json));
        }catch(Exception ex)when(ex is ArgumentException or IOException or UnauthorizedAccessException){rivenInfo.Text=ex.Message;}
    }
    private void LoadRivenSession()
    {
        var dialog=new OpenFileDialog{Filter="WF Labs 리벤 작업|*.wfriven"};if(dialog.ShowDialog(this)!=true)return;
        try{var state=JsonSerializer.Deserialize<RivenWorkshopState>(File.ReadAllText(dialog.FileName),Catalog.Json)??throw new ArgumentException("빈 작업 파일입니다.");RivenWorkshop.Validate(state);rivenBaseline=null;SetWorkshop(state with{Locked=null});}
        catch(Exception ex)when(ex is ArgumentException or IOException or JsonException or UnauthorizedAccessException){rivenInfo.Text=ex.Message;}
    }
    private async Task CompareRivenAsync()
    {
        if(rivenCompareCancel is not null){rivenComparison.Text="진행 중인 비교가 끝나거나 취소된 뒤 다시 비교해 주세요.";return;}
        var cancellation=new CancellationTokenSource();rivenCompareCancel=cancellation;var stamp=++rivenCompareGeneration;
        rivenCompareButton.IsEnabled=false;rivenCancelButton.IsEnabled=true;
        try
        {
            var after=ReadCostedRiven();var before=rivenBaseline??throw new ArgumentException("먼저 비교 기준 리벤을 지정하세요.");var build=ReadBuild();
            before=before with{WeaponId=build.WeaponId};after=after with{WeaponId=build.WeaponId};
            var destination=(int)rivenDestination.SelectedItem-1;
            Build With(RivenRoll roll)
            {
                var slots=build.Slots.ToArray();for(var i=0;i<slots.Length;i++)if(slots[i]?.ModId==RivenValues.ModId(roll.WeaponId))slots[i]=null;
                slots[destination]=new(RivenValues.ModId(roll.WeaponId),roll.Rank);return build with{Riven=roll,Slots=slots};
            }
            rivenResultDetails.IsExpanded=true;rivenDpsCharts.Children.Clear();rivenNotesExpander.Visibility=Visibility.Collapsed;var left=With(before);var right=With(after);rivenComparison.Text="같은 적·스탠스·교전 조건으로 계산 중…";
            var progress=new Progress<SimulationProgress>(p=>{if(stamp==rivenCompareGeneration)rivenComparison.Text=$"리벤 비교 계산 중 · {p.Phase} {p.Completed:N0}/{p.Total:N0}";});
            var pair=await Task.Run(()=>new[]{EncounterEngine.Calculate(catalog,left,stanceCatalog,cancellation:cancellation.Token,progress:progress),EncounterEngine.Calculate(catalog,right,stanceCatalog,cancellation:cancellation.Token,progress:progress)},cancellation.Token);
            if(stamp!=rivenCompareGeneration)return;
            UpdateRivenComparisonChart(pair[0],pair[1]);hasRivenComparison=true;
            rivenResultDetails.IsExpanded=pair.Any(p=>p.Unsupported.Length>0||p.Timeline is not {Unsupported.Length:0});
            string Capacity(Build b){var c=CapacityEngine.Calculate(catalog,b);return $"수용량 {c.Used}/{c.Total}"+(c.Used>c.Total?" · 초과: 게임에서 장착 불가, 가정 계산":"");}
            rivenComparison.Text=$"기준 · {Capacity(left)}\n{DescribeRivenResult(pair[0],false)}\n\n후보 · {Capacity(right)}\n{DescribeRivenResult(pair[1],false)}";
            rivenComparisonNotes.Text="현재 모드 탭의 교전 조건·표본 수를 사용합니다. 예시 후보는 실제 획득 결과가 아닙니다. 95% 오차폭은 표본 평균의 정규 근사이며, 미확정 타격 시각의 불확실성과 다릅니다.\n\n"+string.Join("\n\n",pair.SelectMany(p=>p.Notes).Distinct());
            rivenNotesExpander.Visibility=Visibility.Visible;
        }catch(OperationCanceledException){if(stamp==rivenCompareGeneration)rivenComparison.Text="비교를 취소했습니다.";}
        catch(ArgumentException ex){if(stamp==rivenCompareGeneration)rivenComparison.Text=ex.Message;}
        finally{if(ReferenceEquals(rivenCompareCancel,cancellation))rivenCompareCancel=null;rivenCompareButton.IsEnabled=true;rivenCancelButton.IsEnabled=false;cancellation.Dispose();}
    }
}
