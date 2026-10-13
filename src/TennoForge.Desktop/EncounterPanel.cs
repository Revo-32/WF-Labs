using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private readonly EncounterDpsPlot encounterDpsPlot=new();
    private readonly TextBlock encounterDamageLabel=T("누적 직접 피해",14);
    private record EncounterChoice(EncounterMode Mode,string Name);
    private readonly ComboBox encounterChoice=new(){DisplayMemberPath="Name",SelectedValuePath="Mode"};
    private readonly TextBlock encounterModeHint=T("",14),encounterValue=T("—",32),encounterDamage=T("—",24),encounterStatus=T("—",24),encounterScope=T("",14),encounterHeading=T("첫 타격 피해",18);
    private readonly TextBox enemyBaseArmor=new(){Text=""};
    private readonly TextBlock encounterStatusLabel=T("타격 전 상태 종류",14);
    private readonly RangePlot encounterDamagePlot=new(),encounterArmorPlot=new();
    private readonly Dictionary<EncounterMode,RadioButton> encounterButtons=[];
    private FrameworkElement? primerControls,encounterCharts,encounterDamageGroup;
    private CancellationTokenSource? encounterCancellation;
    private EncounterResult? encounterResult;
    private FrameworkElement CreateEncounterChoice()
    {
        encounterChoice.ItemsSource=new[]{new EncounterChoice(EncounterMode.Primer,"프라이머 적용"),new EncounterChoice(EncounterMode.FirstHit,"첫 타격 · 프라이머 없음"),new EncounterChoice(EncounterMode.StanceCycle,"스탠스 한 사이클"),new EncounterChoice(EncounterMode.FiveSeconds,"5초 교전"),new EncounterChoice(EncounterMode.TenSeconds,"10초 교전")};
        encounterChoice.SelectedValue=EncounterMode.FiveSeconds;CompactInput(encounterChoice,36);
        encounterChoice.SelectionChanged+=(_,_)=>
        {
            if(initializing)return;
            if(SelectedEncounter!=EncounterMode.Primer)ClearTargetStatuses();
            if(SelectedEncounter==EncounterMode.StanceCycle)attackMode.SelectedIndex=0;
            RefreshEncounterChoice();Recalculate();
            RefreshMeleeConditions();
        };
        enemyBaseArmor.ToolTip="섀터링 임팩트용 레벨 보정 전 기본 방어력. 대상의 현재 방어력과 구분됩니다.";
        enemyBaseArmor.TextChanged+=(_,_)=>Recalculate();
        var buttons=new WrapPanel{Margin=new(0,0,0,6)};
        foreach(var (mode,label) in new[]{(EncounterMode.FirstHit,"첫 타격"),(EncounterMode.FiveSeconds,"5초"),(EncounterMode.TenSeconds,"10초"),(EncounterMode.StanceCycle,"스탠스 1회"),(EncounterMode.Primer,"프라이머 + 첫 타격")})
        {
            var b=new RadioButton{Content=label,GroupName="EncounterDuration",Style=(Style)FindResource("DifficultyChoice"),MinWidth=mode==EncounterMode.Primer?152:88,Height=34,Margin=new(0,0,6,6),IsChecked=mode==SelectedEncounter};
            b.Checked+=(_,_)=>{if(!updatingEncounterButtons)encounterChoice.SelectedValue=mode;};
            encounterButtons[mode]=b;buttons.Children.Add(b);
        }
        return Stack(T("계산할 교전 구간",14,"#596B85"),buttons,encounterModeHint);
    }
    private EncounterMode SelectedEncounter=>encounterChoice.SelectedValue is EncounterMode mode?mode:EncounterMode.FirstHit;
    private EncounterSetup ReadEncounter()=>new(SelectedEncounter,string.IsNullOrWhiteSpace(enemyBaseArmor.Text)?null:Number(enemyBaseArmor,"적 기본 방어력"),ReadHitTimes(),ReadCombatInputs(),SelectedSamples,ReadNearby(),ReadTargetOrder(),ReadAttackSequence(),ReadBlockedSight(),ReadContagion(),(RapierSelected&&rapierEditors.Count>0)||(mirrorsEnabled.IsChecked==true&&hologramOnly.IsChecked==true&&hologramEditors.Count>0)||(WeaponActionSelected&&weaponActionEditors.Count>0)||(ShieldSelected&&shieldEditors.Count>0)||(ContagionEquipped&&contagionOnly.IsChecked==true)||(EpidemicEquipped&&epidemicOnly.IsChecked==true)||(CaustacystSelected&&poolsOnly.IsChecked==true)||(CeramicSelected&&ceramicOnly.IsChecked==true)||(ZenistarSelected&&discOnly.IsChecked==true)||(OrviusSelected&&hoverOnly.IsChecked==true)||(GlaiveSelected&&glaiveEditors.Count>0&&glaiveOnly.IsChecked==true)||(BladeSelected&&bladeEditors.Count>0&&bladeOnly.IsChecked==true)||(InnodemSelected&&innodemEditors.Count>0&&innodemOnly.IsChecked==true)||(VitricaSelected&&glassEditors.Count+glassSlamEditors.Count>0&&glassOnly.IsChecked==true)||(WaveSelected&&waveEditors.Count>0&&wavesOnly.IsChecked==true),ReadEpidemic(),ReadIchorContacts(),ReadPoolWindows(),ReadOkinaFlights(),ReadCeramicContacts(),ReadDiscs(),ReadHovers(),ReadGlaives(),ReadBladeContacts(),ReadInnodemContacts(),ReadGlasses(),ReadGlassSlams(),ReadWaves(),ReadShieldFlights(),ReadWeaponActions(),ReadHologramContacts(),ReadRubblePickups(),ReadRapierContacts());
    private void ApplyEncounter(EncounterSetup? setup)
    {encounterChoice.SelectedValue=setup?.Mode??EncounterMode.Primer;enemyBaseArmor.Text=setup?.BaseArmor?.ToString(CultureInfo.InvariantCulture)??"";stanceHitTimes.Text=setup?.HitTimes is {} times?string.Join(", ",times.Select(t=>t.ToString(CultureInfo.InvariantCulture))):"";ApplyCombatInputs(setup?.Inputs??[]);ApplyContagion(setup);ApplyEpidemic(setup);ApplyIchorContacts(setup);ApplyPoolWindows(setup);ApplyOkinaFlights(setup);ApplyCeramicContacts(setup);ApplyDiscs(setup);ApplyHovers(setup);ApplyGlaives(setup);ApplyBladeContacts(setup);ApplyInnodemContacts(setup);ApplyGlass(setup);ApplyWaves(setup);ApplyShieldFlights(setup);ApplyWeaponActions(setup);ApplyHologramContacts(setup);ApplyRubblePickups(setup);ApplyRapierContacts(setup);ApplyCombatScene(setup);ApplySampleSetting(setup?.Samples??2048);RefreshEncounterChoice();}
    private bool updatingEncounterButtons;
    private void RefreshEncounterChoice()
    {
        updatingEncounterButtons=true;foreach(var pair in encounterButtons)pair.Value.IsChecked=pair.Key==SelectedEncounter;updatingEncounterButtons=false;
        RefreshArcaneConditions();
        var primer=SelectedEncounter==EncounterMode.Primer;
        if(primerControls is not null)primerControls.Visibility=primer?Visibility.Visible:Visibility.Collapsed;
        encounterModeHint.Text=SelectedEncounter switch
        {
            EncounterMode.Primer=>"미리 걸어 둔 상태로 첫 타격을 계산합니다.",
            EncounterMode.FirstHit=>"프라이머 상태이상 없음 · 자체 상태는 명중 후 부여",
            _=>"상태 생성·만료와 방어력 변화를 반영한 구간 평균"
        };
    }
    private FrameworkElement CreateEncounterResults()
    {
        encounterScope.TextWrapping=TextWrapping.Wrap;
        encounterValue.SetResourceReference(TextBlock.ForegroundProperty,"ChartAccentBrush");
        var extra=new UniformGrid{Columns=2};encounterDamageGroup=Stack(encounterDamageLabel,encounterDamage);extra.Children.Add(encounterDamageGroup);extra.Children.Add(Stack(encounterStatusLabel,encounterStatus));
        var chartHint=T("각 타격 직전의 상태를 고정한 전체 콤보 화력 · 누적 평균 DPS와 구분",14,"#596B85");chartHint.TextWrapping=TextWrapping.Wrap;
        encounterCharts=Stack(T("화력 증가 · 컨오 기여",16),encounterDpsPlot,chartHint,ChartPair(ResultCard(Stack(T("누적 직접 피해",16),encounterDamagePlot),0),ResultCard(Stack(T("남은 방어력",16),encounterArmorPlot),0)));
        var detail=new Expander{Header="교전 조건 · 계산 범위",Content=encounterScope,Visibility=Visibility.Collapsed};
        var warnings=T("",14);warnings.TextWrapping=TextWrapping.Wrap;warnings.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");
        void UpdateWarning()
        {
            var lines=(encounterScope.Text+"\n"+combatNotes.Text).Split('\n');
            warnings.Text=string.Join("\n",lines.Where(l=>l.Contains("미검증")||l.Contains("미반영")||l.Contains("미지원")||l.Contains("규칙 확인")||l.Contains("미확정")||l.Contains("미기재")||l.Contains("조건부")).Distinct());
            warnings.Visibility=warnings.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
            detail.Visibility=encounterScope.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
            if(encounterValue.Text=="계산 미지원"&&encounterScope.Text.Length>0)detail.IsExpanded=true;
        }
        var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));
        EventHandler update=(_,_)=>UpdateWarning();descriptor.AddValueChanged(encounterScope,update);descriptor.AddValueChanged(combatNotes,update);
        Closed+=(_,_)=>{descriptor.RemoveValueChanged(encounterScope,update);descriptor.RemoveValueChanged(combatNotes,update);};
        encounterValue.Margin=new(0,0,0,10);
        var hitDetails=new Expander{Header="타격별 변화 · 컨오 기여 · 방어력",Content=encounterCharts};
        hitDetails.SetBinding(VisibilityProperty,new System.Windows.Data.Binding("Visibility"){Source=encounterCharts});
        return ResultCard(Stack(CreateCalculationHeading(),encounterValue,extra,samplingPrecision,warnings,detail,CreateCombatHistory(),hitDetails),0);
    }
    internal object EncounterGeometryForCheck()
    {
        object Box(FrameworkElement x){var point=x.TranslatePoint(new Point(),(UIElement)Content);return new{x.Name,x.ActualWidth,x.ActualHeight,x.DesiredSize,point.X,point.Y,ParentWidth=(x.Parent as FrameworkElement)?.ActualWidth};}
        var left=encounterDamage.TranslatePoint(new Point(),(UIElement)Content);var right=encounterStatus.TranslatePoint(new Point(),(UIElement)Content);
        var root=(FrameworkElement)Content;
        return new {Damage=Box(encounterDamage),Status=Box(encounterStatus),Scope=Box(encounterScope),Dashboard=Box(resultsDashboard!),ScopeWrapping=encounterScope.TextWrapping.ToString(),Separated=right.X>=left.X+encounterDamage.DesiredSize.Width,WithinWindow=encounterScope.TranslatePoint(new Point(),root).X+encounterScope.ActualWidth<=root.ActualWidth};
    }
    private void PrepareEncounterResults()
    {
        encounterCancellation?.Cancel();encounterResult=null;encounterValue.Text="계산 중…";encounterDamage.Text=encounterStatus.Text="—";encounterScope.Text="";
        samplingPrecision.Text="";cancelCalculation.Visibility=Visibility.Collapsed;
        ShowCombatHistory(null);encounterDpsPlot.Set([]);encounterDamageLabel.Text="누적 직접 피해";
        encounterHeading.Text=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?"첫 타격 · 대상 피해":"교전 평균 직접 DPS";
        encounterStatusLabel.Text=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?"명중 후 상태 종류 · 기대값":"종료 시점 상태 종류 · 기대값";
        encounterDamageGroup!.Visibility=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?Visibility.Collapsed:Visibility.Visible;
        encounterDamagePlot.Clear();encounterArmorPlot.Clear();encounterCharts!.Visibility=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?Visibility.Collapsed:Visibility.Visible;
        RefreshEncounterChoice();
    }
    private void ShowEncounter(EncounterResult result)
    {
        encounterResult=result;
        cancelCalculation.Visibility=Visibility.Collapsed;ShowSamplingPrecision(result);
        var timed=ShowCombatHistory(result.Timeline);
        string Procs(EncounterSample s)=>string.Join(" · ",(s.ForcedProcs??[]).Select(p=>Enum.TryParse<DamageType>(p,out var t)?Labels.Damage(t):p switch{"Lifted"=>"띄우기","Knockdown"=>"넉다운","Ragdoll"=>"래그돌","Stagger"=>"비틀거림",_=>p}));
        string Range(EncounterRange r,string format="N1")=>Math.Abs(r.High-r.Low)<.05?r.Low.ToString(format):$"{r.Low.ToString(format)} – {r.High.ToString(format)}";
        if(result.Unsupported.Length>0)
        {
            encounterValue.Text="계산 미지원";encounterScope.Text="시간별 규칙 확인 필요: "+string.Join(" · ",result.Unsupported);encounterScope.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
            encounterCharts!.Visibility=Visibility.Collapsed;return;
        }
        if(result.DamageOnly){encounterHeading.Text="동작 1회 · 대상 피해";encounterValue.Text=Range(result.Damage);encounterDamageGroup!.Visibility=Visibility.Collapsed;encounterStatusLabel.Text="계산에 적용한 시작 상태";encounterStatus.Text=Range(result.EndStatusTypes,"0.00")+"종";encounterScope.Text=string.Join("\n",result.Notes);encounterScope.SetResourceReference(TextBlock.ForegroundProperty,result.Notes.Any(n=>n.StartsWith("미검증:"))?"AccentBrush":"MutedBrush");encounterCharts!.Visibility=Visibility.Collapsed;return;}
        if(timed&&result.Timeline!.Seconds==0){encounterHeading.Text="첫 접촉 · 직접 + 즉발 상태 피해";encounterValue.Text=Range(result.Damage);encounterStatus.Text=Range(result.EndStatusTypes,"0.00")+"종";encounterScope.Text="산탄·추가 타격 사이의 상태/방어층 변화 포함 · 지연된 상태 피해 제외";var scopeNotes=result.Timeline.Notes.Where(n=>n.StartsWith("미검증:")||n.StartsWith("미반영:")).ToArray();if(scopeNotes.Length>0)encounterScope.Text+="\n"+string.Join("\n",scopeNotes);encounterScope.SetResourceReference(TextBlock.ForegroundProperty,scopeNotes.Length>0?"AccentBrush":"MutedBrush");encounterCharts!.Visibility=Visibility.Collapsed;return;}
        if(timed){encounterHeading.Text="교전 평균 DPS · 직접 + 상태";encounterDamageLabel.Text="누적 피해 · 직접 + 상태";encounterValue.Text=Range(result.AverageDps!);encounterDamage.Text=Range(result.Damage);encounterStatus.Text=Range(result.EndStatusTypes,"0.00")+"종";encounterScope.Text=result.Timeline!.Notes.Any(n=>n.StartsWith("미반영:"))?string.Join("\n",result.Timeline.Notes.Where(n=>n.StartsWith("미반영:"))):result.Timeline!.Notes.Any(n=>n.StartsWith(ExaltedWaveFlight.Conditional))?ExaltedWaveFlight.Conditional+" · 파동만 계산 / 검 접촉 별도":result.Timeline!.Notes.Any(n=>n.StartsWith(VitricaGlassState.Scope))?VitricaGlassState.Scope:result.Timeline!.Notes.Any(n=>n.StartsWith(InnodemFlight.Conditional))?InnodemFlight.Conditional+" · 위치 배율/컨오 분기 자동 선택 규칙 미기재":result.Timeline!.Notes.Any(n=>n.StartsWith(OrviusHoverState.Conflict))?"위키 데이터 표를 선택한 조건부 결과 · 본문 수치와 상충\n매달기75냉기·상확60% / 수동 기폭586폭발·강제 냉기":result.Timeline.Notes.Any(n=>n.StartsWith("미검증:"))?"조건부 결과 · 위키에 없는 값은 입력한 시나리오 적용":"";encounterScope.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");encounterCharts!.Visibility=Visibility.Collapsed;return;}
        encounterValue.Text=Range(result.AverageDps??result.Damage);encounterDamage.Text=Range(result.Damage);encounterStatus.Text=Range(result.EndStatusTypes,"0.00")+"종";
        if(result.Mode is EncounterMode.FirstHit or EncounterMode.Primer)encounterStatusLabel.Text="타격 전 상태 종류";
        encounterScope.Text=result.Seconds is {} seconds?$"{seconds:0.###}초 평균 · 직접 피해만 · 자체 상태·콤보 누적 반영\n"+(result.Damage.High-result.Damage.Low>.05?"타격 시각 미기재 · 상태 유지 시나리오의 비교 범위이며 확정 상·하한은 아닙니다.":"살아 있는 단일 대상 기준 · 지속 피해 제외"):"치명타 기대값 · 이번 타격으로 생긴 상태는 다음 타격부터 반영";
        if(result.Timeline is {Unsupported.Length:>0} unresolved)encounterScope.Text+="\n"+string.Join("\n",unresolved.Unsupported);
        encounterScope.ToolTip=$"상태 표본 {result.Trials}회\n"+string.Join("\n",result.Notes);
        encounterCharts!.Visibility=result.Samples.Length>0?Visibility.Visible:Visibility.Collapsed;
        encounterDpsPlot.Set(result.Samples);
        encounterScope.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        encounterDamagePlot.SetData(result.Samples.Select(s=>new RangePoint(s.Hit,s.Damage.Low,s.Damage.High,$"{s.Hit}번째 타격 · {s.Earliest:0.##}–{s.Latest:0.##}초\n누적 직접 피해 {Range(s.Damage)}\n타격 전 상태 {Range(s.StatusTypes,"0.00")}종 · CO 배율 {s.CoFactor:0.###}×\n명중 후 강제 프록: {(Procs(s) is {Length:>0} procs?procs:"없음")}")).ToArray(),"타격 순서");
        encounterArmorPlot.SetData(result.Samples.Select(s=>new RangePoint(s.Hit,s.Armor.Low,s.Armor.High,$"{s.Hit}번째 타격 직전\n남은 방어력 {Range(s.Armor)}")).ToArray(),"타격 순서");
    }
}
