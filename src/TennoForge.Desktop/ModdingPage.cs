using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private readonly TextBlock calculationContext=T("",13),planningResult=T("",13);
    private Grid? moddingEditor;
    private ScenarioDrawer? scenarioDrawer;
    private Border? equipmentCard;
    private FrameworkElement? equipmentActions;
    private readonly ComboBox attackMode = new(){ItemsSource=new[]{"스탠스 콤보","단일 공격"},SelectedIndex=0};
    private readonly ComboBox stanceCombo = new();
    private readonly TextBlock chanceValue=T("—",22),criticalValue=T("—",22),statusValue=T("—",22),speedValue=T("—",22);
    private readonly TextBlock rateValue=T("—",22),armorValue=T("—",22),cycleDamageValue=T("—",22),durationValue=T("—",22);
    private readonly TextBlock cycleDamageLabel=T("콤보 피해",12,"#596B85"),durationLabel=T("콤보 시간",12,"#596B85");
    private readonly TextBlock damageHeading=T("피해 구성",16),ttkCaption=T("",12,"#596B85"),scopeLabel=T("",13,"#596B85"),conditionSummary=T("",13,"#596B85");
    private readonly StackPanel criticalBars=new();
    private readonly DamageDonut damageDonut=new();
    private readonly SeriesPlot armorPlot=new(),hitPlot=new();
    private readonly TextBlock armorPlotCaption=T("시작 상태 고정 · 대상 방어력만 변경",14,"#596B85");
    private FrameworkElement? resultsDashboard;
    private FrameworkElement? weaponStatStrip;
    private readonly Expander equationDetails=new(){Header="DPS 계산식 · 모드별 영향"};
    private ScrollViewer? resultsScroller;
    private FrameworkElement? primaryCharts;
    private StanceCalculation? currentStance;
    private readonly ComboBox weaponClass=new(){IsTextSearchEnabled=true};

    private FrameworkElement CreateModPage()
    {
        var left=new Grid();moddingEditor=left;left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        weapon.SuggestionFilter=w=>!ZawCatalog.IsZaw(w.Id);weapon.ItemsSource=catalog.Weapons;weapon.DisplayMemberPath="Name";weapon.SelectedIndex=0;
        weapon.ToolTip="무기 검색 · 한국어 / English";
        weaponClass.ItemTemplate=KoreanPresentation.ClassTemplate();weaponClass.ItemsSource=new[]{"전체 무기군"}.Concat(catalog.Weapons.Select(w=>w.WeaponType).Distinct().OrderBy(KoreanPresentation.WeaponClass)).ToArray();weaponClass.SelectedIndex=0;
        weaponClass.SelectionChanged+=(_,_)=>{var previous=weapon.SelectedItem as Weapon;var list=catalog.Weapons.Where(w=>weaponClass.SelectedIndex==0||w.WeaponType==weaponClass.SelectedItem as string).ToArray();weapon.ItemsSource=list;weapon.SelectedItem=list.FirstOrDefault(w=>w.Id==previous?.Id)??list.FirstOrDefault();};
        stance.DisplayMemberPath="SelectionLabel";stanceCombo.DisplayMemberPath="InputLabel";
        void RefreshCombos()
        {
            stanceCombo.ItemsSource=(stance.SelectedItem as Stance)?.Combos;
            if(stance.SelectedItem is Stance selected)stanceCombo.SelectedItem=selected.Combos.FirstOrDefault(c=>c.Unsupported.Length==0)??selected.Combos.First();
        }
        void RefreshStances(){if(weapon.SelectedItem is not Weapon w)return;var available=stanceCatalog.ForWeapon(w);stance.ItemsSource=available;if(available.Length==0)attackMode.SelectedIndex=1;stance.IsEnabled=ExaltedRules.Find(w.Id) is null;stance.SelectedItem=available.FirstOrDefault(s=>s.Id=="Blind Justice")??available.FirstOrDefault(s=>s.Combos.Any(c=>c.Unsupported.Length==0))??available.FirstOrDefault();RefreshCombos();if(!restoringBuild)ResetCapacityForWeapon();}
        RefreshStances();
        weapon.SelectionChanged+=(_,_)=>{RefreshStances();Recalculate();};
        stance.SelectionChanged+=(_,_)=>{RefreshCombos();Recalculate();};
        stanceCombo.SelectionChanged+=(_,_)=>Recalculate();
        attackMode.SelectionChanged+=(_,_)=>{motion.IsEnabled=interval.IsEnabled=attackMode.SelectedIndex==1;if(!initializing&&attackMode.SelectedIndex==0)stanceEquipped.IsChecked=true;Recalculate();};
        motion.IsEnabled=interval.IsEnabled=false;
        foreach(var control in new Control[]{weaponClass,weapon,stance,stanceCombo,attackMode})CompactInput(control,32);
        weaponClass.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"무기군");
        weapon.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"무기 이름");
        stance.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"스탠스");
        stanceCombo.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"스탠스 동작");
        attackMode.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"계산 방식");
        stance.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("SelectedItem.SelectionLabel"){Source=stance});
        stanceCombo.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("SelectedItem.SelectionLabel"){Source=stanceCombo});
        WatchEquationSource(stance,"stance",()=>stance.SelectedItem is Stance s?(s.Id,s.Name):null);
        WatchEquationSource(stanceCombo,"stance",()=>stance.SelectedItem is Stance s&&stanceCombo.SelectedItem is StanceCombo c?(s.Id,s.Name+" · "+c.InputLabel):null);
        board.HoveredModChanged+=m=>{if(m is not null)SetEquationSource("mod",m.Id,m.Name);else if(equationSource?.Kind=="mod"){equationSource=null;equationView.Highlight([]);}};
        var equipmentRow=new Grid();equipmentRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});equipmentRow.ColumnDefinitions.Add(new(){Width=new(6)});equipmentRow.ColumnDefinitions.Add(new(){Width=new(2,GridUnitType.Star)});
        equipmentRow.Children.Add(weaponClass);var weaponInput=WeaponWithZawBuilder();Grid.SetColumn(weaponInput,2);equipmentRow.Children.Add(weaponInput);
        equipmentActions=CreateModActions();equipmentActions.Margin=new(0,4,0,4);
        equipmentCard=Card(Stack(equipmentRow,equipmentActions,CreateCapacityView()),0);equipmentCard.Padding=new(0);equipmentCard.BorderThickness=new(0);equipmentCard.Margin=new(0,0,0,12);left.Children.Add(equipmentCard);
        string[] defaults=["pressure","shatter","fury"];
        var starterSlots=new ModSlot?[8];for(var i=0;i<defaults.Length;i++){var m=catalog.Mods.First(x=>x.Family==defaults[i]);starterSlots[i]=new(m.Id,m.MaxRank);}board.SetSlots(starterSlots,false);
        board.Changed+=Recalculate;Grid.SetRow(board.RackView,1);left.Children.Add(board.RackView);
        var libraryCard=Card(board.LibraryView,0);libraryCard.Padding=new(0,10,0,0);libraryCard.BorderThickness=new(0,1,0,0);libraryCard.Margin=new(0,2,0,0);Grid.SetRow(libraryCard,3);left.Children.Add(libraryCard);

        var scenario=CreateCombatPanel();
        ((StackPanel)equipmentCard.Child).Children.Insert(1,lichFields!);
        var upgrades=CreateEquipmentSlots();Grid.SetRow(upgrades,2);left.Children.Add(upgrades);
        var right=new StackPanel();
        var resultTitle=new DockPanel{Margin=new(0,0,0,10)};var resultHeading=T("계산 결과",20);resultHeading.Margin=new(0,0,0,2);scopeLabel.Margin=new(0);planningResult.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");CollapseEmpty(planningResult);calculationContext.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");resultTitle.Children.Add(Stack(resultHeading,calculationContext,planningResult,scopeLabel));right.Children.Add(resultTitle);error.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");right.Children.Add(error);CollapseEmpty(error);compare.Visibility=Visibility.Collapsed;right.Children.Add(compare);right.Children.Add(CreateEncounterResults());
        var heroes=new UniformGrid{Columns=2,Margin=new(-10,0,-10,0)};
        Border Metric(string label,TextBlock value,UIElement caption)
        {
            var heading=T(label,12,"#596B85");heading.Margin=new(0,0,0,4);value.Margin=new(0,0,0,4);((FrameworkElement)caption).Margin=new(0);
            var b=ResultCard(Stack(heading,value,caption),16);b.Padding=new(0,8,0,14);b.Margin=new(10,0,10,4);return b;
        }
        heroes.Children.Add(Metric("방어력 적용 전 DPS",rawMetric,T("직접 피해 / 초",12,"#596B85")));
        heroes.Children.Add(Metric("대상 직접 DPS",targetMetric,T("대상 방어력 적용",12,"#596B85")));
        heroes.Children.Add(Metric("타격당 평균 피해",hitMetric,T("치명타 기대값 포함 · 방어력 적용 전",12,"#596B85")));
        heroes.Children.Add(Metric("직접 피해 TTK · 중앙값",ttkMetric,ttkCaption));
        void ReflowMetrics(double width)
        {
            var singleRow=width>=960;heroes.Columns=singleRow?4:2;
            foreach(var value in new[]{rawMetric,targetMetric,hitMetric}){value.FontSize=singleRow?28:32;value.LineHeight=40;}
            ttkMetric.FontSize=singleRow?20:28;ttkMetric.LineHeight=40;
        }
        heroes.SizeChanged+=(_,e)=>ReflowMetrics(e.NewSize.Width);ReflowMetrics(0);
        var quick=new UniformGrid{Columns=4,Margin=new(0,4,0,10)};
        foreach(var (label,value) in new[]{("치명타 확률",chanceValue),("치명타 배수",criticalValue),("상태이상 확률",statusValue),("공격 속도",speedValue)})
        {
            value.FontSize=26;value.LineHeight=34;value.Margin=new(0);
            var name=T(label,14,"#596B85");name.Margin=new(0,0,0,3);
            var cell=Stack(name,value);cell.Margin=new(0,0,12,0);quick.Children.Add(cell);
        }
        var quickTitle=T("현재 모딩",18);quickTitle.Margin=new(0,0,0,4);
        var snapshotNote=T("교전 시작 기준 · 선택한 콤보와 활성 버프 반영",14,"#596B85");snapshotNote.Margin=new(0,0,0,4);
        weaponStatStrip=Stack(quickTitle,snapshotNote,quick);
        var detailed=new UniformGrid{Columns=4};
        foreach(var item in new[]{Stack(T("평균 타격 / 초",12,"#596B85"),rateValue),Stack(T("방어력 피해 감소",12,"#596B85"),armorValue),Stack(cycleDamageLabel,cycleDamageValue),Stack(durationLabel,durationValue)})
        {
            item.Margin=new(0,4,8,4);((FrameworkElement)item.Children[0]).Margin=new(0,0,0,4);((FrameworkElement)item.Children[1]).Margin=new(0,0,0,2);detailed.Children.Add(item);
        }
        spread.FontSize=12;spread.LineHeight=18;spread.Margin=new(0);
        targetMetric.SetResourceReference(TextBlock.ForegroundProperty,"ChartAccentBrush");
        right.Children.Add(ResultCard(new Expander{Header="시작 상태의 DPS · 타격 피해 · 처치 시간",Content=Stack(resultConditions,heroes,detailed)},16));
        primaryCharts=ChartPair(ResultCard(Stack(damageHeading,T("시작 상태 고정 · 직접 피해 · 비치명 비중",14,"#596B85"),damageDonut),16),ResultCard(Stack(T("치명타 단계 분포",16),T("시작 상태 고정 · 각 치명 단계의 확률",14,"#596B85"),criticalBars,Spacer(12),T("고정 상태 반복 TTK",14),spread),16));right.Children.Add(primaryCharts);
        armorPlotCaption.TextWrapping=TextWrapping.Wrap;
        right.Children.Add(ChartPair(ResultCard(Stack(T("방어력과 DPS",16),armorPlotCaption,armorPlot),16),ResultCard(Stack(T("타격별 피해",16),T("시작 상태 고정 · 새 프록 누적 제외",14,"#596B85"),hitPlot),16)));
        right.Children.Add(ResultCard(new Expander{Header="상태 피해 · 근접 상세 수치",Content=Stack(meleeDetails,statusDamageRows)},16));
        right.Children.Add(ResultCard(secondaryExpander=new Expander{Header="발동 · 범위 효과",Content=secondaryRows},16));
        foreach(var box in new[]{combo,status,health,armor,motion,interval})box.TextChanged+=(_,_)=>Recalculate();
        WatchEquationSource(armor,"condition",()=> ("armor","대상 방어력"));WatchEquationSource(combo,"condition",()=> ("combo","콤보 배수"));WatchEquationSource(status,"condition",()=> ("status","CO용 상태 종류"));
        right.Children.Add(ResultCard(new Expander{Header="계산식 · 출처 · 지원 범위",Content=Stack(traceText,notice)},16));
        var resultGrid=new Grid();resultGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});resultGrid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        equationDetails.Content=equationView;
        var pinned=Stack(weaponStatStrip,equationDetails);pinned.Margin=new(0,0,0,12);resultGrid.Children.Add(pinned);resultsScroller=Scroll(right);Grid.SetRow(resultsScroller,1);resultGrid.Children.Add(resultsScroller);
        var dashboard=new Border{Child=resultGrid};dashboard.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");
        dashboard.SetResourceReference(Border.BorderBrushProperty,"ResultsStrokeBrush");resultsDashboard=dashboard;
        var split=new Grid();
        foreach(var size in new[]{new GridLength(1,GridUnitType.Star),new GridLength(40),new GridLength(1,GridUnitType.Star)})split.ColumnDefinitions.Add(new(){Width=size});
        var line=new Border{Width=1,HorizontalAlignment=HorizontalAlignment.Center};line.SetResourceReference(Border.BackgroundProperty,"StrokeBrush");Grid.SetColumn(line,1);split.Children.Add(line);
        split.Children.Add(left);Grid.SetColumn(resultsDashboard,2);split.Children.Add(resultsDashboard);
        left.SizeChanged+=(_,_)=>
        {
            bool compact=left.ActualHeight<710;board.SetCompact(compact);split.ColumnDefinitions[1].Width=new(split.ActualWidth<1550?24:40);
            bool shortWorkspace=left.ActualHeight<560;
            equipmentCard.Margin=new(0,0,0,shortWorkspace?6:12);libraryCard.Padding=new(0,shortWorkspace?6:10,0,0);
            foreach(var slot in new[]{exilusSlotButton,meleeSlotButton,zawSlotButton})if(slot is not null)
            {
                slot.Height=compact?48:EquipmentSlotHeight;slot.Padding=compact?new(8,6,8,6):new(9,7,9,7);
                if(slot.Content is Grid content&&content.Children[0] is DockPanel band)foreach(var text in band.Children.OfType<TextBlock>()){text.FontSize=compact?12:13;text.LineHeight=compact?16:20;text.Margin=new(0);}
            }
            foreach(var name in new[]{exilusSlotName,meleeSlotName,zawSlotName})name.Height=compact?20:40;
        };
        scenarioDrawer=new ScenarioDrawer(split,scenario);scenarioDrawer.SetStatusSource(error);return scenarioDrawer;
    }
    private static void CollapseEmpty(TextBlock text)
    {
        var style=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));
        var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};empty.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);text.Style=style;
    }
    private static void CompactInput(Control control,double height)
    {control.Height=control.MinHeight=height;control.FontSize=14;control.Padding=new(8,3,8,3);control.VerticalAlignment=VerticalAlignment.Center;}
    private void RefreshDashboard(Calculation c,StanceCalculation? sc)
    {
        chanceValue.Text=c.CriticalChance.ToString("P1");criticalValue.Text=$"{c.CriticalMultiplier:0.00}×";statusValue.Text=c.StatusChance.ToString("P1");speedValue.Text=$"{c.AttackSpeed:0.00}×";
        var directHitCount=sc?.Hits.Sum(h=>h.Count)??1;
        rateValue.Text=(sc is null?1/c.HitInterval:directHitCount/sc.Duration)?.ToString("0.00")??"시간 미기재";armorValue.Text=c.ArmorReduction.ToString("P1");
        cycleDamageLabel.Text=sc is null?"단일 타격 피해":"콤보 전체 피해";durationLabel.Text=sc is null?"타격 간격":"콤보 시간";
        cycleDamageValue.Text=(sc?.ExpectedComboDamage??c.ExpectedHit).ToString("N0");durationValue.Text=sc is {Duration:null}?"위키 미기재":$"{sc?.Duration??c.HitInterval:0.00}초";
        scopeLabel.Text=sc is null?"사용자 지정 타격 · 직접 피해":$"{sc.Stance.Name} · {sc.Combo.InputLabel} · {directHitCount}타";
        scopeLabel.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        ttkCaption.Text=sc is null?"반복 타격 시뮬레이션":"처치 타격이 속한 콤보의 시간 범위";
        if(c.Melee!.ExtraDamage>0)ttkCaption.Text+=" · 시작 충전1회 소비·재충전 없음";
        damageHeading.Text=sc is null?"타격 피해 구성":"콤보 피해 구성";
        var damage=sc?.ComboDamage??c.Damage;damageDonut.SetData(damage);
        criticalBars.Children.Clear();var tier=(int)Math.Floor(c.CriticalChance);var fraction=c.CriticalChance-tier;
        string Tier(int t)=>t switch{0=>"일반",1=>"노랑",2=>"주황",3=>"빨강",_=>$"빨강 {t-2}"};
        if(1-fraction>1e-9)criticalBars.Children.Add(DistributionRow(Tier(tier),1-fraction,(1-fraction).ToString("P1"),ChartPalette.Critical(tier)));
        if(fraction>1e-9)criticalBars.Children.Add(DistributionRow(Tier(tier+1),fraction,fraction.ToString("P1"),ChartPalette.Critical(tier+1)));
        var b=ReadBuild();conditionSummary.Text=$"체력 {b.Scenario.Enemy!.Health:N0} · 방어력 {b.Scenario.Enemy.Armor:N0} · 콤보 {b.Scenario.Combo}× · 상태 {TargetStatusRules.Count(b)}종";RefreshCombatState(b,c,sc);
        var selectedArmor=b.Scenario.Enemy.Armor;
        if(c.Melee!.ExtraDamage>0||sc?.Hits.Any(h=>h.Calculation!.Melee!.ExtraDamage>0)==true)
        {armorPlot.Clear();armorPlotCaption.Text="에너지 채널의 피해 유형이 원문에 없어 방어력별 곡선은 표시하지 않습니다.";}
        else
        {
        armorPlotCaption.Text="시작 상태 고정 · 대상 방어력만 변경";
        var armorPoints=Enumerable.Range(0,61).Select(i=>i*45d).Append(selectedArmor).Distinct().Order().Select(a=>
        {var dps=sc is null?MeleeTargetModel.ExpectedHitAtArmor(c,a)/c.HitInterval:sc.Hits.Sum(h=>MeleeTargetModel.ExpectedHitAtArmor(h.Calculation!,a)*h.Count)/(sc.Duration??1);return new PlotPoint(a,dps,$"방어력 {a:N0}\n{(sc is {Duration:null}?"동작 1회 피해":"대상 직접 DPS")} {dps:N1}",a==selectedArmor);}).ToArray();
        armorPlot.SetData(armorPoints,true,"방깎 전 방어력",sc is {Duration:null}?"동작 피해":"DPS",0,2700);
        }
        var hits=sc?.Hits.SelectMany(h=>Enumerable.Repeat(h.Calculation!.TargetDps*h.Calculation.HitInterval,h.Count)).ToArray()??[c.TargetDps*c.HitInterval];
        hitPlot.SetData(hits.Select((value,i)=>new PlotPoint(i+1,value,$"타격 {i+1}\n대상 평균 피해 {value:N1}\n가로축은 시간 대신 타격 순서입니다.")).ToArray(),false,"타격 순서","평균 피해",.5,hits.Length+.5);
    }
    private static UIElement DistributionRow(string label,double fraction,string value,Brush brush)
    {
        var p=new StackPanel{Margin=new(0,5,0,6)};var line=new DockPanel();var number=T(value,12);number.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(number,Dock.Right);line.Children.Add(number);line.Children.Add(T(label,12,"#596B85"));p.Children.Add(line);
        var grid=new Grid{Height=6};grid.ColumnDefinitions.Add(new(){Width=new(Math.Max(fraction,.000001),GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new(Math.Max(1-fraction,.000001),GridUnitType.Star)});var bar=new Border{CornerRadius=new(1),Background=brush};grid.Children.Add(bar);p.Children.Add(grid);return p;
    }
    private void ClearDashboard(){foreach(var text in new[]{chanceValue,criticalValue,statusValue,speedValue,rateValue,armorValue,cycleDamageValue,durationValue})text.Text="—";damageDonut.SetData(null);armorPlot.Clear();hitPlot.Clear();criticalBars.Children.Clear();}
    private static Border ResultCard(UIElement child,double padding)
    {var b=new Border{Child=child,Padding=new(0,8,0,10),BorderThickness=new(0,0,0,1),Margin=new(0,0,0,6)};b.SetResourceReference(Border.BorderBrushProperty,"ResultsStrokeBrush");return b;}
    private static Grid ChartPair(FrameworkElement left,FrameworkElement right)
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new(16)});grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.Children.Add(left);grid.Children.Add(right);
        void Reflow(double width){var stacked=width<620;Grid.SetColumnSpan(left,stacked?3:1);Grid.SetColumn(right,stacked?0:2);Grid.SetColumnSpan(right,stacked?3:1);Grid.SetRow(right,stacked?1:0);}
        grid.SizeChanged+=(_,e)=>Reflow(e.NewSize.Width);Reflow(700);return grid;
    }
    public void ScrollChartsForCheck(){if(resultsScroller is {} scroller){scroller.UpdateLayout();scroller.ScrollToVerticalOffset(primaryCharts!.TransformToAncestor(scroller).Transform(new Point()).Y+scroller.VerticalOffset);scroller.UpdateLayout();}}
    public void ResetChartsScroll(){resultsScroller?.ScrollToTop();}
    public void CheckCompactScroll()
    {
        if(resultsDashboard!.ActualWidth<moddingEditor!.ActualWidth)throw new Exception("수치 영역이 모드 영역보다 좁습니다.");
        board.CheckIndependentLibraryScroll(root);
    }
    public void ResetModdingScroll()=>board.ResetLibraryScroll();
}
