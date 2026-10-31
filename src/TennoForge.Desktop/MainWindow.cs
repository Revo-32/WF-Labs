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
    private readonly ScrollViewer workspaceViewport=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,CanContentScroll=false,Padding=new(0),Focusable=false};
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
        Title = "WF Labs"; MinWidth = Math.Min(1280,SystemParameters.WorkArea.Width); MinHeight = Math.Min(720,SystemParameters.WorkArea.Height); Width = Math.Max(MinWidth,Math.Min(1920,SystemParameters.WorkArea.Width-24)); Height = Math.Max(MinHeight,Math.Min(1000,SystemParameters.WorkArea.Height-24));
        board = new ModBoard(catalog);
        SetResourceReference(FontFamilyProperty,"AppFont"); FontSize = 14;
        Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        root.Background = BackgroundBrush();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(WindowFrame.HeaderHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(CreateHeader());
        performanceSwitch.Checked += (_, _) => SetLightMode(true); performanceSwitch.Unchecked += (_, _) => SetLightMode(false);
        themeSwitch.Checked += (_, _) => SetDarkTheme(false); themeSwitch.Unchecked += (_, _) => SetDarkTheme(true);
        workspaceViewport.Margin=new(20,8,20,8);workspaceViewport.Content=page;Grid.SetRow(workspaceViewport,2);root.Children.Add(workspaceViewport);
        void FitWorkspace()
        {
            if(workspaceViewport.ActualWidth<=0||workspaceViewport.ActualHeight<=0)return;
            var availableHeight=ActualHeight>0?ActualHeight:Height;bool compact=availableHeight<860;double captionHeight=availableHeight<740?52:compact?56:WindowFrame.HeaderHeight;
            root.RowDefinitions[0].Height=new(captionHeight);workspaceViewport.Margin=new(20,compact?4:8,20,compact?4:8);workflowBar.Margin=new(20,compact?0:4,20,0);
            if(System.Windows.Shell.WindowChrome.GetWindowChrome(this) is {} chrome&&chrome.CaptionHeight!=captionHeight)chrome.CaptionHeight=captionHeight;
            // Pages own their scrolling; the workstation itself always fits the window.
            workspaceViewport.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;workspaceViewport.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;
            page.Width=Math.Max(0,workspaceViewport.ActualWidth);page.Height=Math.Max(0,workspaceViewport.ActualHeight);
        }
        workspaceViewport.SizeChanged+=(_,_)=>FitWorkspace();
        workspaceViewport.ScrollChanged+=(_,e)=>{if(e.ViewportWidthChange!=0||e.ViewportHeightChange!=0)FitWorkspace();};FitWorkspace();
        var workflowHost=new StackPanel();workflowHost.Children.Add(workflowBar);
        RivenRecognitionBar=new("리벤",RivenAutomatic,RivenAutoStatus){Margin=new(20,6,20,4),Visibility=Visibility.Collapsed};workflowHost.Children.Add(RivenRecognitionBar);
        RivenAutomatic.Checked+=(_,_)=>SetRivenAutomatic(true);RivenAutomatic.Unchecked+=(_,_)=>SetRivenAutomatic(false);
        Grid.SetRow(workflowHost,1);root.Children.Insert(1,workflowHost);
        WindowFrame.Attach(this, root);
        // Private validation desktops have an unrelated, often 1024x768 monitor.
        // Their requested native viewport must not race with production work-area
        // clamping. Real clients and physical-monitor checks retain that policy.
        if(UiFonts.PersistChanges||Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")!="1"||Environment.GetCommandLineArgs().Contains("--monitor-check"))MonitorWorkArea.Attach(this);
        modPage = CreateModPage(); rivenPage = CreateRivenPage(); ShowModding();
        board.EditRiven+=()=>{if(equippedRiven is {} r)ApplyRivenWorkbench(r);OpenRivenClass("Melee");};
        SourceInitialized += (_, _) => Backdrop.Apply(this, lightMode, Theme.IsDark);
        SourceInitialized += (_, _) => InitializeRelicHotkey();
        SourceInitialized += (_, _) => InitializeRivenCapture();
        Closed += (_, _) => { generation++; debounce.Stop(); encounterCancellation?.Cancel(); };
        Loaded+=(_,_)=>{EnsureSchedulePage();schedulePage!.StartMonitoring();};
        Deactivated+=(_,_)=>{equationSource=null;equationView.Highlight([]);};
        debounce.Tick += (_, _) => { debounce.Stop(); StartSimulation(); };
        initializing = false; SetLightMode(false); Recalculate();
    }
    private static SolidColorBrush B(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
    private static TextBlock T(string text, double size = 15, string color = "#172539")
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
    private static Brush BackgroundBrush() => B(Theme.IsDark ? "#101112" : "#F4F4F2");
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
            error.Text = ""; ttkMetric.Text = "계산 중…"; spread.Text = ""; PrepareEncounterResults();UpdateComparison(); debounce.Start();ScheduleWorkspaceSave();
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
        var dialog = new SaveFileDialog { Title="근접 빌드 저장", Filter = "WF Labs 빌드 (*.json)|*.json", FileName = "WF-Labs-build.json" };
        if (dialog.ShowDialog(this) == true) try { AtomicJsonFile.Write(dialog.FileName, ReadBuild()); } catch (Exception ex) when(ex is IOException or UnauthorizedAccessException) { error.Text = "저장 실패: "+ex.Message; }
    }
    private void LoadBuild()
    {
        var dialog = new OpenFileDialog { Title="근접 빌드 불러오기", Filter = "WF Labs 빌드 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1000000) throw new ArgumentException("빌드 파일은 1MB 이하여야 합니다.");
            var b = JsonSerializer.Deserialize<Build>(File.ReadAllText(dialog.FileName), Catalog.Json) ?? throw new ArgumentException("빌드 파일이 비어 있습니다.");
            if(b.Slots is not {Length:8}||b.Scenario is null)throw new ArgumentException("근접 빌드의 슬롯 또는 교전 조건을 확인하세요.");
            var migrated=b.DataVersion!=catalog.Version||b.SchemaVersion!=5||b.Melee?.RulesVersion!=MeleeEngine.RulesVersion;b=BuildMigration.ToMelee(catalog,b);EvaluateBuild(b); ApplyBuild(b);if(migrated)error.Text="이전 빌드를 확장 근접 규칙으로 변환하고 다시 계산했습니다. 저장된 슬롯·랭크·극성·스탠스·아케인은 유지했습니다.";
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { error.Text = "불러오기 실패: " + ex.Message; }
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
    private readonly Dictionary<string,RangedRivenWorkbench> rangedRivenPages=[];
    internal Task<object> CheckRangedWorkshop(string category)=>rangedRivenPages[category].CheckWorkshop();
    public void ShowRivens()
    {
        InvalidateRivenComparison();
        if(equipmentKind?.SelectedIndex is 1 or 2)
        {
            var category=equipmentKind.SelectedIndex==1?"primary":"secondary";
            if(!rangedRivenPages.TryGetValue(category,out var workshop))rangedRivenPages[category]=workshop=new RangedRivenWorkbench(category,(roll,slot)=>{ShowRanged(category);rangedPages[category].EquipRiven(roll,slot);},()=>rangedPages.TryGetValue(category,out var ranged)?ranged.Capture().Riven:null,()=>rangedPages.TryGetValue(category,out var current)?current.Capture():throw new ArgumentException("모딩 화면에서 무기와 대상을 먼저 설정하세요."));
            workshop.ContextChanged=()=>{RefreshWorkflowBar();ScheduleWorkspaceSave();};page.Content=workshop;workshop.RefreshContext();
        }
        else page.Content=rivenPage;
        SelectPage(1);
    }
    private void ShowRoadmap()
    {
        if (roadmapPage is not null) { page.Content = roadmapPage; SelectPage(3); return; }
        var version=T($"v{VelopackUpdateFeed.AppVersion} · 개발 미리보기",14);version.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        var p = Stack(T("개발 계획", 20), version, Spacer(12));
        string[] milestones = [
            "현재  모딩·교전·리벤 분석\n근접·주무기·보조무기 모딩과 DPS·TTK 비교, 조우·키트건 조립, 아케인·인카논·베일런스, 리벤 범위·접합·순환을 제공합니다. 확정 규칙·입력 시나리오·근사는 결과에서 구분합니다.",
            "현재  성유물 보상 인식·시세 비교\n한국어 보상 화면·파일·붙여넣기 → 후보 확인 → 플래티넘·두캇 비교. 애매한 이름은 직접 선택하며 원본 확대·영역 재인식, 마켓 접속 조건·조회 시각·오프라인 캐시를 제공합니다.",
            "현재  마스터리 제작·랭작 계획\n보유·숙련 장비와 진행률, 제작·구매 추천 순서, 부족 재료·선행 장비·획득처·구매 호가를 제공합니다. 보유 성유물의 개봉·정련 효율도 함께 비교합니다.",
            "이후  모딩 화면 인식\n게임 화면에서 무기·모드·랭크·리벤 후보를 읽고 사용자가 확인한 뒤 적용합니다. 기존 빌드와 비교하고 되돌릴 수 있도록 합니다.",
            "별도 단계  익절티드·워프레임 어빌리티\n전체 시전·자원·분신 시뮬레이션은 후속 단계입니다. 현재 로어·이클립스 같은 단순 외부 버프는 유지합니다.",
            "지속 검증\n게임 패치·위키 개정 대조, 계산 회귀와 UI 검수를 각 배포에 포함합니다. 정확도와 세부 계산을 유지한 뒤 최적화하며, 화면 인식은 사용자 요청 시점에 제한합니다."
        ];
        foreach (var m in milestones) p.Children.Add(Card(T(m, 16)));
        roadmapPage = Scroll(p); page.Content = roadmapPage; SelectPage(3);
    }
    public void ShowModding() { lastModdingKind=0;InvalidateRivenComparison();page.Content = modPage; SelectPage(0);if(!switchingEquipment&&equipmentKind is not null){switchingEquipment=true;equipmentKind.SelectedIndex=0;switchingEquipment=false;}RefreshWorkflowBar(); }
    private readonly Dictionary<string,RangedPage> rangedPages=[];
    internal void ShowRanged(string category)
    {
        lastModdingKind=category=="primary"?1:2;
        if(!rangedPages.TryGetValue(category,out var ranged))rangedPages[category]=ranged=CreateRangedPage(category);
        switchingEquipment=true;try{SelectPage(0);page.Content=ranged;equipmentKind.SelectedIndex=category=="primary"?1:2;buildActions.Visibility=Visibility.Collapsed;}finally{switchingEquipment=false;}RefreshWorkflowBar();
    }
    internal void PrepareRangedCombatAudit(string category)=>rangedPages[category].PrepareCombatAudit();
    internal void PrepareRangedRivenAudit(string category)=>rangedPages[category].PrepareRivenAudit();
    internal void PrepareRangedRivenWorkbenchAudit(string category)
    {
        ShowRanged(category);var roll=rangedPages[category].Capture().Riven??throw new InvalidOperationException("작업실 검사 리벤 없음");ShowRivens();
        rangedRivenPages[category].Set(roll);rangedRivenPages[category].CheckAndEquip();
        var result=rangedPages[category].Capture();if(result.Slots[3]?.ModId!=RangedRivens.Mod(RangedEngine.Weapons.Single(w=>w.Id==roll.WeaponId)).Id||!result.Riven!.Traits.SequenceEqual(roll.Traits))throw new InvalidOperationException("총기 작업실→모딩 리벤 장착 결과 오류");
        ShowRivens();if(page.Content!=rangedRivenPages[category]||navigation[1].IsChecked!=true)throw new InvalidOperationException("총기 리벤 탭 전환 오류");
    }
    internal void SelectRangedRivenSectionForCheck(string category,int index)=>rangedRivenPages[category].SelectSectionForCheck(index);
    internal void CloseRangedRivenAudit(string category)=>rangedPages[category].CloseRivenAudit();
    internal void PrepareRangedMechanicsAudit(string category,bool details)=>rangedPages[category].PrepareMechanicsAudit(details);
    internal Window? KitgunDialogForCheck()=>rangedPages["primary"].CreateKitgunDialog();
    internal void PrepareRangedSceneAudit(string category,string name)=>rangedPages[category].PrepareSceneAudit(name);
    internal void CheckRangedPage(string category){rangedPages[category].Check();}
    private void SelectPage(int index)
    {
        PopupLayout.Close(this);
        workspaceViewport.Margin=new(20,8,20,index==0?0:8);
        scenarioDrawer?.SetOpen(false,false);foreach(var ranged in rangedPages.Values)ranged.DrawerForCheck.SetOpen(false,false);
        if (navigation[index].IsChecked != true) navigation[index].IsChecked = true;
        buildActions.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshWorkflowBar();
    }
    public void SetDarkTheme(bool dark)
    {
        if (themeSwitch.IsChecked != !dark) { themeSwitch.IsChecked = !dark; return; }
        Theme.Apply(dark); SetLightMode(lightMode); RefreshRivenWorkbench(false);schedulePage?.Tick();
        equationView.Retheme();
        if(current is not null)RefreshDashboard(current,currentStance);
    }
    public void SetLightMode(bool enabled)
    {
        if (performanceSwitch.IsChecked != enabled) { performanceSwitch.IsChecked = enabled; return; }
        lightMode = enabled; root.Background = BackgroundBrush();
        equationView.ReduceMotion=enabled;ScenarioDrawer.ReduceMotion=enabled;UiMotion.ReduceMotion=enabled;
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
        ApplyRivenWorkbench(new(catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id,8,[new(RivenStat.Damage,195.7),new(RivenStat.StatusChance,106.9)],RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK"));
        if(spliceResults.Children.Count!=2||!rivenGrades[0].Text.Contains("범위 위치"))throw new Exception("무기별 리벤 범위·접합 예상 UI 갱신 실패");
        RunExpandedSmokeChecks();
        SetDarkTheme(false);SetDarkTheme(true);SetLightMode(false);
    }
}
