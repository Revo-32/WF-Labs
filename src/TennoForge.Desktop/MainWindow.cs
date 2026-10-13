using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly Catalog catalog = Catalog.LoadMelee();
    private readonly Grid root = new();
    private readonly ContentControl page = new();
    private readonly StackPanel buildActions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly List<RadioButton> navigation = [];
    private readonly CheckBox performanceSwitch = new() { Content = "경량 모드", Margin = new Thickness(0, 0, 0, 10) };
    private readonly CheckBox themeSwitch = new() { Content = "라이트 테마" };
    private readonly WeaponSearchBox weapon = new();
    private readonly ModBoard board;
    private readonly StanceCatalog stanceCatalog = StanceCatalog.Load();
    private readonly ComboBox stance = new();
    private readonly TextBox combo = new() { Text = "1" }, status = new() { Text = "0" }, health = new() { Text = "10000" }, armor = new() { Text = "2700" }, motion = new() { Text = "1" }, interval = new() { Text = "1" };
    private readonly TextBlock hitMetric = T("—", 32), rawMetric = T("—", 32), targetMetric = T("—", 32), ttkMetric = T("—", 28), spread = T("", 13), stats = T("", 14), damageText = T("", 14), traceText = T("", 13), error = T("", 14), notice = T("", 13), compare = T("비교할 빌드를 기준으로 저장하세요.", 13);
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly StackPanel spliceResults = new();
    private readonly ComboBox[] traits = new ComboBox[4];
    private readonly CheckBox[] negatives = new CheckBox[4];
    private FrameworkElement? modPage, rivenPage, roadmapPage;
    private Calculation? current;
    private Build? comparisonBuild;
    private bool initializing = true, lightMode;
    private long generation;
    private Task? simulationTask;

    public MainWindow()
    {
        Title = "WF Labs"; MinWidth = 1760; MinHeight = 920; Width = Math.Max(MinWidth,Math.Min(1920,SystemParameters.WorkArea.Width-24)); Height = Math.Max(MinHeight,Math.Min(1000,SystemParameters.WorkArea.Height-24));
        board = new ModBoard(catalog);
        FontFamily = (FontFamily)Application.Current.Resources["Wanted"]; FontSize = 14;
        Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        root.Background = BackgroundBrush();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(WindowFrame.HeaderHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(CreateHeader());
        performanceSwitch.Checked += (_, _) => SetLightMode(true); performanceSwitch.Unchecked += (_, _) => SetLightMode(false);
        themeSwitch.Checked += (_, _) => SetDarkTheme(false); themeSwitch.Unchecked += (_, _) => SetDarkTheme(true);
        page.Margin = new Thickness(20, 16, 20, 20); Grid.SetRow(page, 1); root.Children.Add(page);
        WindowFrame.Attach(this, root);
        modPage = CreateModPage(); rivenPage = CreateRivenPage(); ShowModding();
        board.EditRiven+=()=>{if(equippedRiven is {} r)ApplyRivenWorkbench(r);ShowRivens();};
        SourceInitialized += (_, _) => Backdrop.Apply(this, lightMode, Theme.IsDark);
        Closed += (_, _) => { generation++; debounce.Stop(); encounterCancellation?.Cancel(); };
        Deactivated+=(_,_)=>{equationSource=null;equationView.Highlight([]);};
        debounce.Tick += (_, _) => { debounce.Stop(); StartSimulation(); };
        initializing = false; SetLightMode(false); Recalculate();
    }
    private static SolidColorBrush B(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
    private static TextBlock T(string text, double size = 14, string color = "#172539")
    {
        size=Math.Max(14,size);
        var lineHeight=size<=12?16:size<=14?20:size<=16?24:size<=20?28:size<=24?32:size<=28?36:40;
        var t=new TextBlock { Text=text,FontSize=size,Margin=new Thickness(0,0,0,8),LineHeight=lineHeight,LineStackingStrategy=LineStackingStrategy.BlockLineHeight };
        if(size>=16)t.FontWeight=FontWeights.SemiBold;
        Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);
        var key=color switch {"#596B85" or "#627896"=>"MutedBrush","#4264D9"=>"AccentBrush","#5D4ABA"=>"PurpleBrush","#A62C38"=>"ErrorBrush",_=>"TextBrush"};
        t.SetResourceReference(TextBlock.ForegroundProperty,key);return t;
    }
    private static FrameworkElement Spacer(double h) => new Border { Height = h };
    private static Button Button(string text, Action action)
    {
        var b = new Button { Content = text }; b.Click += (_, _) => action(); return b;
    }
    private static Brush BackgroundBrush() => B(Theme.IsDark ? "#0C131D" : "#F0F5F8");
    private Border Card(UIElement content, double padding = 20)
    {
        var border = new Border { Child = content, Padding = new Thickness(0, 8, 0, padding), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 16) };
        border.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
        return border;
    }
    private static StackPanel Stack(params UIElement[] children) { var p = new StackPanel(); foreach (var c in children) p.Children.Add(c); return p; }
    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 10, 0) };
    private static Grid Columns(UIElement a, UIElement b, double left = 1, double right = 1)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right, GridUnitType.Star) });
        grid.Children.Add(a); Grid.SetColumn(b, 2); grid.Children.Add(b); return grid;
    }
    private static UIElement Field(string label, Control input)
    {
        var title=T(label,14,"#596B85");title.Margin=new(0,0,0,6);
        System.Windows.Automation.AutomationProperties.SetName(input,label);
        return Stack(title,input,Spacer(10));
    }
    private static double Number(TextBox t, string name)
    {
        if (!double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new ArgumentException(name + "에 유효한 숫자를 입력하세요."); return n;
    }
    private static int Integer(TextBox t, string name)
    {
        var n = Number(t, name); if (n != Math.Truncate(n) || n < int.MinValue || n > int.MaxValue) throw new ArgumentException(name + "는 정수여야 합니다."); return (int)n;
    }
    private Build ReadBuild()
    {
        var selectedWeapon=weapon.SelectedItem as Weapon??throw new ArgumentException("목록에서 무기를 선택하세요.");
        StanceSelection? selection=null;
        if(attackMode.SelectedIndex==0)
        {
            if(stance.SelectedItem is not Stance st||stanceCombo.SelectedItem is not StanceCombo comboItem)throw new ArgumentException("스탠스 동작을 선택하세요.");
            selection=new(st.Id,comboItem.Key,stanceCatalog.Revision);
        }
        var melee=ReadMelee();var arcanes=ReadArcanes();var primer=SelectedEncounter==EncounterMode.Primer;
        if(!primer){melee=melee with {Statuses=[],Conditions=(melee.Conditions??[]).Where(p=>p.Key is not ("Lifted" or "Knockdown" or "Microwave")).ToDictionary()};if(arcanes is not null)arcanes=arcanes with {Frozen=false};}
        var build=new Build(selectedWeapon.Id,board.Slots,
            new(Integer(combo,"콤보"),primer?Integer(status,"상태 종류"):0,selection is null?Number(motion,"타격 배율"):1,selection is null?Number(interval,"간격"):1,new(Number(health,"체력"),Number(armor,"방어력"))),catalog.Version,5,Riven:board.Slots.Any(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Effects?.GetValueOrDefault("Riven")>0)?equippedRiven:null,Stance:selection,Capacity:ReadCapacity(),Arcanes:arcanes,Melee:melee,Encounter:ReadEncounter());
        return ReadEnemySelection() is {} target?EnemyScaling.Apply(build,target):build;
    }
    private (Calculation Stats,StanceCalculation? Stance) EvaluateBuild(Build build)
    {
        build=CombatTimeline.InitialState(build);
        if(build.Stance is null)return (DamageEngine.Calculate(catalog,build),null);
        var result=StanceEngine.Calculate(catalog,build,stanceCatalog);return(result.Stats,result);
    }
    private void Recalculate()
    {
        if (initializing||changingMelee||updatingCombat) return;
        generation++; debounce.Stop();
        var validCapacity=false;
        try
        {
            var build=ReadBuild();UpdateCapacity(CapacityEngine.Calculate(catalog,capacityCatalog,build));validCapacity=true;
            var evaluated=EvaluateBuild(build);current=evaluated.Stats;currentStance=evaluated.Stance;var c=current;
            hitMetric.Text=(currentStance?.ExpectedHit??c.ExpectedHit).ToString("N1");rawMetric.Text=currentStance is {Duration:null}?"시간 미기재":(currentStance?.RawDps??c.RawDps).ToString("N1");targetMetric.Text=currentStance is {Duration:null}?"시간 미기재":(currentStance?.TargetDps??c.TargetDps).ToString("N1");
            RefreshDashboard(c,currentStance);
            RefreshMeleeResult(c);
            UpdateEquation(build,c,currentStance);
            var timing=currentStance is {} selectedStance?(selectedStance.Duration is {} seconds?$"동작 전체 {seconds:0.###}초":"동작 시간: 위키 미기재"):$"타격 간격 {c.HitInterval:F3}초";
            stats.Text = $"치명타 {c.CriticalChance:P1}   /   배수 {c.CriticalMultiplier:F2}×\n상태이상 {c.StatusChance:P1}   /   {timing}\n적 방어력 피해 감소 {c.ArmorReduction:P1}";
            damageText.Text = string.Join("\n", c.Damage.Where(d => d.Value > 0).Select(d => $"{Labels.Damage(d.Key),-6}   {d.Value:N1}   ·   {d.Value / c.Damage.Values.Sum():P1}"));
            traceText.Text = string.Join("\n\n", c.Trace.Where(t=>currentStance is null||t.Name!="타격 간격(초)").Select(t => $"{t.Name}  =  {t.Value:G6}\n{t.Formula}\n근거: {t.Evidence}"));
            notice.Text=string.Join("\n\n",c.Limitations);
            if(c.Melee?.Contributions is {} contributions)traceText.Text+="\n\n모드별 입력 브라켓\n"+string.Join("\n",contributions.Select(p=>$"{p.Name} · {p.Rank}랭크 → {p.Bracket}: {p.Amount:G6}"));
            if(currentStance is {} sc)
            {
                var durationText=sc.Duration is null?"위키에 동작 전체 시간 미기재 · DPS·TTK 계산 없음":$"동작 전체 시간 = {(sc.Combo.Duration is null?"사용자 입력":"위키 시간")} {sc.Combo.Duration??build.Melee?.ComboDurationOverride:0.###}초 ÷ 공속 {c.AttackSpeed:0.###}";
                traceText.Text+=$"\n\n스탠스 직접 피해 = 각 타격의 유형별 피해 × 타격 배율 × 물리 유형 보정 × 타격 횟수의 합\n{durationText}\n출처: WF Wiki revision {stanceCatalog.Revision} · {sc.Stance.Id} / {sc.Combo.Key}";
                notice.Text=(sc.Duration is null?"동작의 타격별 피해와 강제 상태 순서를 표시합니다. 동작 시간이 없어 초당 피해와 처치 시간은 계산하지 않습니다.":"직접 피해 DPS는 선택한 상태를 고정한 콤보 기준입니다. 개별 타격 시각은 위키에 없어 상태 피해를 포함한 정밀 TTK를 추정하지 않습니다.")+"\n\n"+string.Join("\n\n",c.Limitations);
            }
            notice.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
            error.Text = ""; ttkMetric.Text = "계산 중…"; spread.Text = ""; PrepareEncounterResults();UpdateComparison(); debounce.Start();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowCombatHistory(null);encounterDpsPlot.Set([]);encounterCharts!.Visibility=Visibility.Collapsed;
            encounterCancellation?.Cancel();cancelCalculation.Visibility=Visibility.Collapsed;samplingPrecision.Text="";encounterValue.Text="—";encounterDamage.Text=encounterStatus.Text="—";encounterScope.Text=ex.Message;encounterDamagePlot.Clear();encounterArmorPlot.Clear();
            current = null;currentStance=null;ClearDashboard();if(!validCapacity)ClearCapacity(); hitMetric.Text = rawMetric.Text = targetMetric.Text = ttkMetric.Text = "—";
            equationBuild=null;equationHoverCache.Clear();equationView.SetEquation(null);
            stats.Text=spread.Text=damageText.Text=traceText.Text=compare.Text=scopeLabel.Text=ttkCaption.Text=notice.Text="";error.Text=ex.Message;
            scopeLabel.Text=ex.Message;scopeLabel.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");
        }
    }
    private void StartSimulation()
    {
        if (current is null) return;
        var version = generation; var c = current; var build=ReadBuild();var hp = build.Scenario.Enemy!.Health;
        // One worker at a time. New requests supersede old results and wait for worker availability.
        if (simulationTask is { IsCompleted: false }) { debounce.Start(); return; }
        encounterCancellation?.Dispose();encounterCancellation=new();
        simulationTask = SimulateAsync(c,currentStance,hp,version,build,encounterCancellation.Token);
    }
    private async Task SimulateAsync(Calculation c,StanceCalculation? sc,double hp,long version,Build build,CancellationToken cancellation)
    {
        try
        {
        cancelCalculation.Visibility=Visibility.Visible;
        var progress=new Progress<SimulationProgress>(p=>{if(version==generation&&!cancellation.IsCancellationRequested&&encounterResult is null&&cancelCalculation.Visibility==Visibility.Visible){encounterScope.Text=$"{p.Phase} · {p.Completed:N0} / {p.Total:N0}회 · {(double)p.Completed/p.Total:P0}";}});
        var encounter=await Task.Run(()=>EncounterEngine.Calculate(catalog,build,stanceCatalog,cancellation:cancellation,progress:progress));if(version!=generation)return;ShowEncounter(encounter);
        if(sc is {Duration:null}){ttkMetric.Text="시간 미기재";spread.Text="위키에 동작 전체 시간이 없습니다. 타격별 피해와 동작 1회 피해를 확인할 수 있습니다.";}
        else if(sc is null){var result=await Task.Run(()=>DamageEngine.Simulate(c,hp));if(version==generation)ShowTtk(result);}
        else {var result=await Task.Run(()=>StanceEngine.Simulate(sc,hp));if(version==generation)ShowStanceTtk(result);}
        }
        catch(OperationCanceledException){}
        catch(ArgumentException ex){if(version==generation){ttkMetric.Text="—";spread.Text=ex.Message;if(encounterResult is null){encounterValue.Text="계산 미지원";encounterScope.Text=ex.Message;}}}
        finally{if(version==generation)cancelCalculation.Visibility=Visibility.Collapsed;}
    }
    private void ShowStanceTtk(StanceTtkResult result)
    {
        string Seconds(TimeBounds x)=>x.Latest.HasValue?$"{x.Earliest:0.00}–{x.Latest:0.00}초":$">{result.Horizon:0.0}초";
        ttkMetric.Text=Seconds(result.Median);
        spread.Text=$"P10  {Seconds(result.P10)}\nP90  {Seconds(result.P90)}\n{result.Horizon:0.0}초 내 처치 {result.KillFraction:P1}\n{result.Trials:N0}회 · 완료 콤보 기준";
    }
    private void ShowTtk(TtkResult t)
    {
        static string Seconds(double? x) => x.HasValue ? $"{x:F2}초" : ">120초";
        ttkMetric.Text = Seconds(t.Median); spread.Text = $"P10 {Seconds(t.P10)}   —   P90 {Seconds(t.P90)}\n120초 내 처치율 {t.KillFraction:P1}";
    }
    private void UpdateComparison()
    {
        if (comparisonBuild is null || current is null){compare.Visibility=Visibility.Collapsed;return;}
        compare.Visibility=Visibility.Visible;
        var saved=EvaluateBuild(comparisonBuild);
        if(saved.Stance is {Duration:null}||currentStance is {Duration:null}){compare.Text="선택 동작의 시간이 미기재여서 DPS 비교는 제공하지 않습니다.";return;}
        var savedDps=saved.Stance?.TargetDps??saved.Stats.TargetDps;
        compare.Text = $"시작 상태 직접 DPS · 기준 대비 {(currentStance?.TargetDps??current.TargetDps) / savedDps - 1:+0.0%;-0.0%;0.0%}\n기준: {catalog.Weapons.Single(w => w.Id == comparisonBuild.WeaponId).Name}, 콤보 {comparisonBuild.Scenario.Combo}×, 상태 {comparisonBuild.Scenario.ExistingStatusTypes}종";
        if (comparisonBuild.Scenario != ReadBuild().Scenario||comparisonBuild.Stance!=ReadBuild().Stance) compare.Text += "\n계산 조건이 다릅니다. 모드 효율 비교 전에 조건을 맞추세요.";
    }
    private void SaveBuild()
    {
        if (current is null) return;
        var dialog = new SaveFileDialog { Filter = "WF Labs 빌드 (*.json)|*.json", FileName = "WF-Labs-build.json" };
        if (dialog.ShowDialog(this) == true) try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(ReadBuild(), Catalog.Json)); } catch (IOException ex) { error.Text = ex.Message; }
    }
    private void LoadBuild()
    {
        var dialog = new OpenFileDialog { Filter = "WF Labs 빌드 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1000000) throw new ArgumentException("빌드 파일은 1MB 이하여야 합니다.");
            var b = JsonSerializer.Deserialize<Build>(File.ReadAllText(dialog.FileName), Catalog.Json) ?? throw new ArgumentException("빌드 파일이 비어 있습니다.");
            var migrated=b.DataVersion!=catalog.Version||b.SchemaVersion!=5||b.Melee?.RulesVersion!=MeleeEngine.RulesVersion;b=BuildMigration.ToMelee(catalog,b);EvaluateBuild(b); ApplyBuild(b);if(migrated)error.Text="이전 빌드를 확장 근접 규칙으로 변환하고 다시 계산했습니다. 저장된 슬롯·랭크·극성·스탠스·아케인은 유지했습니다.";
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException or InvalidOperationException) { error.Text = "불러오기 실패: " + ex.Message; }
    }
    private void ApplyBuild(Build b)
    {
        if(b.EnemySelection is {} selectedTarget)b=EnemyScaling.Apply(b,selectedTarget);
        initializing = true; restoringBuild=true;
        try
        {
        weaponClass.SelectedIndex=0;
        weapon.SelectedItem = catalog.Weapons.Single(w => w.Id == b.WeaponId);
        attackMode.SelectedIndex=b.Stance is null?1:0;
        if(b.Stance is {} saved){stance.SelectedItem=stanceCatalog.ForWeapon((Weapon)weapon.SelectedItem).Single(s=>s.Id==saved.Id);stanceCombo.SelectedItem=((Stance)stance.SelectedItem).Combos.Single(c=>c.Key==saved.ComboKey);}
        equippedRiven=b.Riven;
        board.SetRiven(b.Riven);
        board.SetSlots(b.Slots, false);
        combo.Text = b.Scenario.Combo.ToString(); status.Text = b.Scenario.ExistingStatusTypes.ToString(); motion.Text = b.Scenario.MotionMultiplier.ToString(CultureInfo.InvariantCulture); interval.Text = b.Scenario.BaseInterval.ToString(CultureInfo.InvariantCulture);
        health.Text = (b.Scenario.Enemy ?? new Target()).Health.ToString(CultureInfo.InvariantCulture); armor.Text = (b.Scenario.Enemy ?? new Target()).Armor.ToString(CultureInfo.InvariantCulture);
        if(b.Capacity is {} savedCapacity)
        {
            if(savedCapacity.StanceId is {} id)stance.SelectedItem=stanceCatalog.ForWeapon((Weapon)weapon.SelectedItem).Single(s=>s.Id==id);
            ApplyCapacity(savedCapacity);
        }
        else ApplyCapacity(capacityCatalog.Default(b.WeaponId,(stance.SelectedItem as Stance)?.Id));
        ApplyArcanes(b.Arcanes);
        ApplyMelee(b.Melee);
        ApplyEncounter(b.Encounter);
        ApplyEnemySelection(b.EnemySelection);
        }
        finally {restoringBuild=false;initializing = false;}
        Recalculate();
    }
    private FrameworkElement CreateRivenPage()=>CreateRivenWorkbench();
    public void ShowRivens() { InvalidateRivenComparison();page.Content = rivenPage; SelectPage(1); }
    private void ShowRoadmap()
    {
        if (roadmapPage is not null) { page.Content = roadmapPage; SelectPage(3); return; }
        var p = Stack(T("개발 계획", 20), T("v0.6.0 미리보기 · 2026-10-11", 12, "#596B85"), Spacer(12));
        string[] milestones = [
            "01  현재 구현\n자료: 무기·변형·부품 911개, 스탠스 표 134개, 아케인 166개, 모드 1,550개 · 일반 근접 229개의 기본 직접 피해 · 정지/전진/방어 콤보 · 지원 아케인의 고정 조건 계산 · 수용량·포르마·저장 복원",
            "02  확장 근접 엔진\n공격별 피해 브라켓, 강공격·슬램·개별 투사체, 인카논 진화, 기존 상태·팩션·실드·오버가드의 고정 조건 계산을 추가했습니다. 상태 피해는 별도 표로 제공합니다. 동적 상태 TTK·조우 조립·엑절티드·일부 고유 기믹은 남아 있으며, 미반영 효과는 결과에 표시합니다.",
            "03  리벤 시뮬레이터\n무기별 기질 · 양옵/음옵 구성별 범위 · 표시 반올림 구간 · 접합 수치 분포 · 고정/순환 상태 · 일반 모딩에 반영 · 출처 규칙 대조",
            "04  화면 인식과 성장 계획\n한국어 모딩 화면 읽기 · 성유물 보상 OCR + 마켓 시세 · 보유/마스터리 기록 · 제작 재료와 랭작 효율",
            "성능 기준\n상시 폴링과 애니메이션 없이 변경 시 계산. 기본 계산과 반복 시뮬레이션 분리. 화면 인식은 영역/주기를 제한. 실제 게임 병행 CPU·GPU·메모리 측정 후 목표 확정."
        ];
        foreach (var m in milestones) p.Children.Add(Card(T(m, 16)));
        roadmapPage = Scroll(p); page.Content = roadmapPage; SelectPage(3);
    }
    public void ShowModding() { InvalidateRivenComparison();page.Content = modPage; SelectPage(0); }
    private void SelectPage(int index)
    {
        if (navigation[index].IsChecked != true) navigation[index].IsChecked = true;
        buildActions.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    public void SetDarkTheme(bool dark)
    {
        if (themeSwitch.IsChecked != !dark) { themeSwitch.IsChecked = !dark; return; }
        Theme.Apply(dark); SetLightMode(lightMode); RefreshRivenWorkbench();
        equationView.Retheme();
        if(current is not null)RefreshDashboard(current,currentStance);
    }
    public void SetLightMode(bool enabled)
    {
        if (performanceSwitch.IsChecked != enabled) { performanceSwitch.IsChecked = enabled; return; }
        lightMode = enabled; root.Background = BackgroundBrush();
        equationView.ReduceMotion=enabled;
        Backdrop.Apply(this, enabled, Theme.IsDark);
        damageDonut.InvalidateVisual();armorPlot.InvalidateVisual();hitPlot.InvalidateVisual();
    }
    public Window StanceForCheck() => new StanceWindow(stanceCatalog.Stances.Single(s=>s.Id=="Blind Justice"), stanceCatalog);
    public Window ModCardForCheck()
    {
        var border=new Border{Child=board.CardForCheck(),Padding=new(12)};
        border.SetResourceReference(Border.BackgroundProperty,"SolidSurfaceBrush");return new Window{Content=border};
    }
    public Window LibraryDetailsForCheck()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(16)};
        var card=board.LibraryCardForCheck();card.Width=152;row.Children.Add(card);
        var detail=new Border{Child=board.DetailsForCheck(),Padding=new(16),CornerRadius=new(6),Width=300,Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Top};detail.SetResourceReference(Border.BackgroundProperty,"PopupBrush");row.Children.Add(detail);
        return new Window{Content=row};
    }
    public void FormaForCheck(bool enabled)=>board.SetFormaEditing(enabled);
    internal void ResetLibraryOrderForCheck()=>board.SortForCheck(ModSort.Name);
    public void RunSmokeChecks()
    {
        var original=ReadBuild();var before=current!.ExpectedHit;
        if(!Theme.IsDark)throw new Exception("기본 다크 테마 실패");
        var oldCosts=capacityResult!.Used;var polarities=board.SlotPolarities;polarities[1]=catalog.Mods.Single(m=>m.Id==board.Slots[1]!.ModId).Polarity;board.SetSlotPolarities(polarities);
        if(capacityResult is null||capacityResult.Used>=oldCosts)throw new Exception("포르마 극성 변경 수용량 갱신 실패");
        var savedPolarities=board.SlotPolarities;board.ClearMods();
        if(board.Slots.Any(s=>s is not null)||capacityResult!.Used!=0||!board.SlotPolarities.SequenceEqual(savedPolarities))throw new Exception("8칸 비우기 또는 슬롯 극성 보존 실패");ApplyBuild(original);
        if(currentStance is not {} chartStance||Math.Abs(damageDonut.Total-chartStance.ComboDamage.Values.Sum())>.001||Math.Abs(hitPlot.Points.Sum(p=>p.Y)-chartStance.TargetDamage)>.001)throw new Exception("팩션·상태·방어력을 포함한 차트와 계산 수치 일치 실패");
        if(Math.Abs(armorPlot.Points.Single(p=>p.Selected).Y-(currentStance.TargetDps??currentStance.TargetDamage))>.001)throw new Exception("방어력 그래프 현재 DPS 일치 실패");
        navigation[1].IsChecked=true;
        if(page.Content!=rivenPage||navigation[0].IsChecked==true||buildActions.Visibility!=Visibility.Collapsed)throw new Exception("Riven Labs 탐색/선택 표시 실패");
        navigation[2].IsChecked=true;
        if(page.Content!=catalogPage||navigation[1].IsChecked==true)throw new Exception("자료 탭 탐색 실패");
        navigation[3].IsChecked=true;
        if(page.Content!=roadmapPage||navigation[1].IsChecked==true)throw new Exception("Roadmap 탐색 실패");
        navigation[0].IsChecked=true;
        if(page.Content!=modPage||buildActions.Visibility!=Visibility.Visible)throw new Exception("Mod Labs 복귀 실패");
        performanceSwitch.IsChecked=true;
        if(!lightMode)throw new Exception("경량 모드 토글 실패");
        performanceSwitch.IsChecked=false;
        themeSwitch.IsChecked=true;if(Theme.IsDark)throw new Exception("라이트 테마 토글 실패");
        themeSwitch.IsChecked=false;if(!Theme.IsDark)throw new Exception("다크 테마 토글 실패");
        if(weapon.Text!=catalog.Weapons[0].Name)throw new Exception("무기 표시명 오류");
        board.ChangeRank(0,0);if(current is null||current.ExpectedHit>=before)throw new Exception("카드 랭크 실시간 갱신 실패");
        var slot0=board.Slots[0]!;
        if(!board.DropOnSlot(new(slot0.ModId,0),1)||board.Slots[1]?.Rank!=0)throw new Exception("카드 교환/랭크 보존 실패");
        if(!board.ReturnToLibrary(new(slot0.ModId,1))||board.Slots[1] is not null)throw new Exception("보관함 해제 실패");
        if(!board.DropOnSlot(new(slot0.ModId),1))throw new Exception("보관함 장착 실패");
        ApplyBuild(original);
        var primed=catalog.Mods.First(m=>m.EnglishName=="Primed Pressure Point");
        if(board.DropOnSlot(new(primed.Id),1))throw new Exception("중복 계열 드롭 거부 실패");
        if(Math.Abs(current!.ExpectedHit-before)>.0001)throw new Exception("거부한 드롭이 빌드를 변경함");
        board.SearchForCheck("blood");if(board.VisibleLibraryCount!=1)throw new Exception("영문 모드 검색 실패");
        board.SearchForCheck("없는모드");if(board.VisibleLibraryCount!=0)throw new Exception("빈 검색 결과 실패");board.SearchForCheck("");
        var slotsBeforeSort=board.Slots;
        board.SortForCheck(ModSort.Cost,true);if(catalog.Mods.Single(m=>m.Id==board.VisibleModIds[0]).BaseDrain+catalog.Mods.Single(m=>m.Id==board.VisibleModIds[0]).MaxRank!=catalog.Mods.Max(m=>m.BaseDrain+m.MaxRank))throw new Exception("코스트 정렬 UI 실패");
        board.SortForCheck(ModSort.Polarity);if(catalog.Mods.Single(m=>m.Id==board.VisibleModIds[0]).Polarity!="AP_TACTIC")throw new Exception("극성 정렬 UI 실패");
        if(!board.Slots.SequenceEqual(slotsBeforeSort))throw new Exception("정렬이 장착 순서를 바꿈");board.SortForCheck(ModSort.Name);
        ApplyBuild(JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(original,Catalog.Json),Catalog.Json)!);
        if(Math.Abs(current!.ExpectedHit-before)>.0001)throw new Exception("기존 빌드 저장 형식 복원 실패");
        weapon.SelectedIndex=1;if(stance.Items.Cast<Stance>().Count(s=>s.ModId is not null&&!s.UniqueToWeapon&&!s.IsDefault)!=2)throw new Exception("무기별 스탠스 필터 실패");ApplyBuild(original);
        combo.Text="1.5";if(current is not null)throw new Exception("잘못된 정수 입력 거부 실패");ApplyBuild(original);
        armor.Text="NaN";if(current is not null)throw new Exception("NaN 입력 거부 실패");ApplyBuild(original);
        var invalid=board.Slots;invalid[1]=invalid[0];board.SetSlots(invalid);if(current is not null)throw new Exception("불러온 중복 모드 결과 숨김 실패");ApplyBuild(original);
        stanceCombo.SelectedItem=((Stance)stance.SelectedItem).Combos.Single(c=>c.Key=="Forward Block");if(current is null)throw new Exception("전진 방어 스탠스 계산 실패");ApplyBuild(original);
        attackMode.SelectedIndex=1;if(currentStance is not null||current is null)throw new Exception("반복 타격 모드 전환 실패");
        var legacy=ReadBuild() with{SchemaVersion=1,Capacity=null};if(legacy.Stance is not null)throw new Exception("이전 저장 스키마 호환 실패");ApplyBuild(legacy);ApplyBuild(original);
        if(currentStance is null||ReadBuild().SchemaVersion!=5)throw new Exception("스탠스/극성 저장 복원 실패");
        debounce.Stop();if(currentStance is {} sc)ShowStanceTtk(StanceEngine.Simulate(sc,original.Scenario.Enemy!.Health));else ShowTtk(DamageEngine.Simulate(current!,original.Scenario.Enemy!.Health));
        ApplyRivenWorkbench(new(catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id,8,[new(RivenStat.Damage,97.8),new(RivenStat.StatusChance,53.5)],RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK"));
        if(spliceResults.Children.Count!=2||!rivenGrades[0].Text.Contains("범위 위치"))throw new Exception("무기별 리벤 범위·접합 예상 UI 갱신 실패");
        RunExpandedSmokeChecks();
        SetDarkTheme(false);SetDarkTheme(true);SetLightMode(false);
    }
}
