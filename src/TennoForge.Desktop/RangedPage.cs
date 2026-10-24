using System.Windows.Documents;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RangedPage : Grid
{
    private readonly string category;
    private readonly WeaponSearchBox weapon=new();
    private sealed record ArcaneChoice(string Id,string Name);
    private readonly ComboBox attack=new(),arcane=new(){DisplayMemberPath="Name"},stacks=new(){ItemsSource=Enumerable.Range(0,13).ToArray(),SelectedIndex=0},arcaneRank=new(){ItemsSource=Enumerable.Range(0,6).ToArray(),SelectedIndex=5};
    private readonly TextBox distance=new(){Text="0"},query=new();
    private readonly CheckBox catalyst=new(){Content="오로킨 카탈리스트",IsChecked=true};
    private readonly UniformGrid slotsPanel=new(){Columns=4},library=new(){Columns=4,VerticalAlignment=VerticalAlignment.Top};
    private readonly StackPanel results=new();
    private readonly Grid slotEditor=new(){Margin=new(0,4,6,12)};
    private readonly ContentControl combatResultHost=new();
    private readonly TextBlock capacity=Text(""),message=Text("");
    private readonly TextBlock noResults=new(){Text="검색 결과 없음",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new(12,30,12,0),IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private readonly ModSlot?[] slots=new ModSlot?[8];
    private readonly string[] polarities=Enumerable.Repeat("None",8).ToArray();
    private readonly ComboBox sort=new(){ItemsSource=new[]{"이름순","코스트순","극성순"},SelectedIndex=0};
    private readonly ComboBox faction=new(),enemy=new(){DisplayMemberPath="Name"},difficulty=new(){ItemsSource=new[]{"일반","강철의 길","빙주령 · 6인"},SelectedIndex=0};
    private readonly TextBox level=new(){Text="200"};
    private readonly CheckBox eximus=new(){Content="엑시무스"};
    private readonly ComboBox encounter=new(){ItemsSource=new[]{"프라이머 적용 · 첫 발사","상태 없이 첫 발사","탄창 한 번","5초 교전","10초 교전"},SelectedIndex=3};
    private readonly ComboBox killStacks=new(){ItemsSource=Enumerable.Range(0,6).ToArray(),SelectedIndex=0};
    private readonly WrapPanel primerPanel=new();
    private FrameworkElement primerSection=null!;
    private Button calculateButton=null!,cancelButton=null!;
    private ScenarioDrawer? scenarioDrawer;
    internal ScenarioDrawer DrawerForCheck=>scenarioDrawer!;
    internal Func<int> CombatSamples {get;set;}=()=>256;
    internal event Action? WorkspaceChanged;
    private RangedBuild? lastValidBuild;
    internal RangedBuild CaptureForSession()=>lastValidBuild??Capture();
    internal void ReportWorkspaceIssue(string text)=>message.Text=text;
    internal void RefreshSampling()=>Refresh();
    private ScrollViewer conditionScroller=null!,resultScroller=null!;
    private readonly Dictionary<DamageType,int> primer=[];
    private RangedConditions storedConditions=new(AllowApproximations:true);
    private sealed record ElementChoice(DamageType Type,string Name);
    private readonly ComboBox progenitor=new(){DisplayMemberPath="Name",ItemsSource=new[]{DamageType.Impact,DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin,DamageType.Magnetic,DamageType.Radiation}.Select(t=>new ElementChoice(t,Labels.Damage(t))).ToArray(),SelectedIndex=4};
    private readonly TextBox bonusPercent=new(){Text="60"};
    private FrameworkElement bonusRow=null!;
    private readonly StackPanel conditionPanel=new();
    private readonly ComboBox exilusMod=new(){DisplayMemberPath="Name"},exilusRank=new(),exilusPolarity=new(){SelectedValuePath="Tag"};
    private readonly Border exilusIcon=new(){Width=34,Height=26,IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new(16,0,0,0)};
    private sealed record ExilusChoice(string Id,string Name);
    private bool descending;
    private string[] evolutions=[];
    private bool autoTransform;
    private int? incarnonAmmo;
    private RangedRivenRoll? riven;
    private KitgunAssembly? kitgun;
    private readonly Button kitgunButton=new(){Content="키트건",Margin=new(6,0,0,0),Padding=new(10,4,10,4)};
    private Grid? rivenOverlay;
    private System.Threading.CancellationTokenSource? simulation;
    private readonly TextBlock progressText=Text("");
    private bool loading;private int active;
    private ReferenceWeapon? Selected=>weapon.SelectedItem is Weapon w?RangedKitguns.Get(w.Id) is {} d?RangedKitguns.Assemble(d,kitgun):RangedEngine.Weapons.Single(x=>x.Id==w.Id):null;
    private static TextBlock Text(string s,double size=15){var t=new TextBlock{Text=s,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,8),LineHeight=Math.Ceiling(size*1.5)};if(size>=18)t.FontWeight=FontWeights.SemiBold;Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);t.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");return t;}
    private static StackPanel Stack(params UIElement[] items){var p=new StackPanel();foreach(var i in items)p.Children.Add(i);return p;}
    private static Button Button(string label,Action action){var b=new Button{Content=label,Margin=new(0,0,6,6),Padding=new(8,5,8,5)};b.Click+=(_,_)=>action();return b;}
    private static void LabelEquipmentRow(Grid row,params string[] labels)
    {
        row.RowDefinitions.Add(new(){Height=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});
        foreach(UIElement child in row.Children)Grid.SetRow(child,1);
        for(int i=0;i<labels.Length;i++){var label=Text(labels[i],13);label.Margin=new(i==0?0:6,0,0,4);label.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");Grid.SetColumn(label,i);row.Children.Add(label);}
    }
    private static ScrollViewer Scroll(UIElement child)=>new(){Content=child,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,8,0)};
    private static FrameworkElement Field(string label,UIElement control)=>Stack(Text(label),control,new Border{Height=12});
    internal RangedPage(string category)
    {
        this.category=category;UseLayoutRounding=true;Unloaded+=(_,_)=>pointerDrag?.Dispose();
        library.SizeChanged+=(_,e)=>{if(e.WidthChanged)library.Columns=ModCardLayout.LibraryColumns(library.ActualWidth);};
        var messageStyle=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));
        var emptyMessage=new Trigger{Property=TextBlock.TextProperty,Value=""};emptyMessage.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));messageStyle.Triggers.Add(emptyMessage);message.Style=messageStyle;
        ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});ColumnDefinitions.Add(new(){Width=new(0)});ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var left=new Grid{Margin=new(0,0,20,0)};left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var toolbar=new WrapPanel();toolbar.Children.Add(Button("저장",Save));toolbar.Children.Add(Button("불러오기",Load));toolbar.Children.Add(Button("8칸 비우기",()=>{Array.Clear(slots);RefreshConditionControls();Refresh();}));
        kitgunButton.Click+=(_,_)=>EditKitgun();kitgunButton.ToolTip="챔버 · 그립 · 로더로 키트건 조립";
        var weaponRow=new DockPanel();DockPanel.SetDock(kitgunButton,Dock.Right);weaponRow.Children.Add(kitgunButton);weaponRow.Children.Add(weapon);
        weapon.SuggestionFilter=w=>RangedKitguns.Get(w.Id) is null;
        query.ToolTip="모드 검색 · 한국어 / English";query.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"모드 검색");
        arcane.ItemsSource=new[]{new ArcaneChoice("None","아케인 없음")};arcane.SelectedIndex=0;
        var arcaneRow=new Grid();arcaneRow.ColumnDefinitions.Add(new(){Width=new(3,GridUnitType.Star)});arcaneRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});arcaneRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});arcaneRow.Children.Add(arcane);Grid.SetColumn(arcaneRank,1);arcaneRank.Margin=new(6,0,0,0);arcaneRank.ToolTip="아케인 랭크";arcaneRow.Children.Add(arcaneRank);Grid.SetColumn(stacks,2);stacks.Margin=new(6,0,0,0);stacks.ToolTip="교전 시작 시 활성 중첩";arcaneRow.Children.Add(stacks);
        var exilusRow=new Grid();exilusRow.ColumnDefinitions.Add(new(){Width=new(3,GridUnitType.Star)});exilusRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});exilusRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        foreach(var (id,code,label) in new[]{("None","None","극성 없음"),("Madurai","AP_ATTACK","마두라이"),("Vazarin","AP_DEFENSE","바자린"),("Naramon","AP_TACTIC","나라몬"),("Zenurik","Zenurik","제누릭")})
            exilusPolarity.Items.Add(new ComboBoxItem{Tag=id,Content=PolarityIcon.Create(code),ToolTip=label});
        exilusIcon.SetResourceReference(Border.BackgroundProperty,"ControlBrush");
        exilusPolarity.SelectedIndex=0;
        exilusRow.Children.Add(exilusMod);Grid.SetColumn(exilusRank,1);Grid.SetColumn(exilusPolarity,2);exilusRow.Children.Add(exilusRank);var polarityHost=new Grid();polarityHost.Children.Add(exilusPolarity);polarityHost.Children.Add(exilusIcon);Grid.SetColumn(polarityHost,2);exilusRow.Children.Add(polarityHost);
        void RefreshPolarityIcon(){exilusPolarity.ApplyTemplate();if(exilusPolarity.Template?.FindName("SelectionText",exilusPolarity) is FrameworkElement selectionText)selectionText.Visibility=Visibility.Collapsed;var id=exilusPolarity.SelectedValue as string??"None";exilusIcon.Child=PolarityIcon.Create(id switch{"Madurai"=>"AP_ATTACK","Vazarin"=>"AP_DEFENSE","Naramon"=>"AP_TACTIC",_=>id});}
        exilusPolarity.Loaded+=(_,_)=>{if(exilusPolarity.Template.FindName("SelectionText",exilusPolarity) is FrameworkElement text)text.Visibility=Visibility.Collapsed;RefreshPolarityIcon();};exilusPolarity.SelectionChanged+=(_,_)=>RefreshPolarityIcon();
        exilusRank.Margin=exilusPolarity.Margin=new(6,0,0,0);exilusMod.ToolTip="엑실러스 슬롯";exilusRank.ToolTip="엑실러스 랭크";exilusPolarity.ToolTip="엑실러스 슬롯 극성";
        LabelEquipmentRow(exilusRow,"엑실러스","랭크","슬롯 극성");LabelEquipmentRow(arcaneRow,"무기 아케인","랭크","시작 중첩");
        var libraryTools=new Grid();libraryTools.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});libraryTools.ColumnDefinitions.Add(new(){Width=new(108)});libraryTools.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var searchField=new SearchField(query,"모드 검색");libraryTools.Children.Add(searchField);Grid.SetColumn(sort,1);libraryTools.Children.Add(sort);sort.Margin=new(6,0,6,0);var direction=Button("↑",()=>{});direction.Click+=(_,_)=>{descending=!descending;direction.Content=descending?"↓":"↑";direction.ToolTip=descending?"내림차순":"오름차순";RefreshLibrary();};direction.Margin=new(0);direction.ToolTip="오름차순";Grid.SetColumn(direction,2);libraryTools.Children.Add(direction);
        var valence=new DockPanel{Margin=new(0,4,0,4)};var valenceLabel=Text("베일런스",14);valenceLabel.VerticalAlignment=VerticalAlignment.Center;valenceLabel.Margin=new(0,0,10,0);valence.Children.Add(valenceLabel);var percentUnit=Text("%",14);percentUnit.VerticalAlignment=VerticalAlignment.Center;percentUnit.Margin=new(6,0,0,0);DockPanel.SetDock(percentUnit,Dock.Right);valence.Children.Add(percentUnit);bonusPercent.Width=80;bonusPercent.Height=32;bonusPercent.Padding=new(8,3,8,3);bonusPercent.ToolTip="게임에 표시된 보너스 피해 25–60%";DockPanel.SetDock(bonusPercent,Dock.Right);valence.Children.Add(bonusPercent);progenitor.Height=32;progenitor.MinHeight=32;progenitor.Margin=new(0,0,8,0);progenitor.ToolTip="보너스 원소";valence.Children.Add(progenitor);bonusRow=valence;
        var actions=new DockPanel();capacity.VerticalAlignment=VerticalAlignment.Center;capacity.Margin=new(12,0,0,0);DockPanel.SetDock(capacity,Dock.Right);actions.Children.Add(capacity);actions.Children.Add(toolbar);
        left.Children.Add(Stack(weaponRow,bonusRow,new Border{Height=8},actions,slotsPanel,slotEditor,exilusRow,new Border{Height=10},arcaneRow,catalyst,new Border{Height=8},Text("모드 보관함",17),libraryTools));
        var libScroll=libraryScroller=Scroll(library);libScroll.Margin=new(0,8,0,0);Grid.SetRow(libScroll,1);left.Children.Add(libScroll);Children.Add(left);
        noResults.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");Grid.SetRow(noResults,1);left.Children.Add(noResults);
        var controls=new WrapPanel();calculateButton=Button("DPS · TTK 계산",()=>StartCombat());UiHierarchy.Primary(calculateButton);calculateButton.Margin=new(0,0,8,0);controls.Children.Add(calculateButton);cancelButton=Button("취소",()=>simulation?.Cancel());cancelButton.Visibility=Visibility.Collapsed;controls.Children.Add(cancelButton);
        
        primerSection=Stack(Text("미리 걸어 둔 상태",17),primerPanel);
        var targetSettings=Stack(Text("대상",18),Field("팩션",faction),Field("적",enemy),Field("적 레벨",level),Field("난이도",difficulty),eximus);
        var attackSettings=Stack(Text("공격과 교전",18),Field("공격 방식 · 구성요소",attack),Field("표적까지 거리 (m)",distance),Field("교전 구간",encounter),primerSection);
        conditionScroller=Scroll(Stack(Text("버프 · 추가 조건",18),conditionPanel,new Expander{Header="처치로 얻는 시작 버프",Content=Field("교전 전에 처치한 적 수",killStacks)}));
        var center=ScenarioDrawer.Columns(Scroll(targetSettings),Scroll(attackSettings),conditionScroller);
        var right=new Grid{Margin=new(20,0,0,0)};right.RowDefinitions.Add(new(){Height=GridLength.Auto});right.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var heading=new DockPanel();DockPanel.SetDock(controls,Dock.Right);heading.Children.Add(controls);heading.Children.Add(Text("계산 결과",22));right.Children.Add(Stack(heading,BuildComparisonTools(),BuildAccuracyStatus(),progressText,message));
        var resultScroll=resultScroller=Scroll(results);Grid.SetRow(resultScroll,1);right.Children.Add(resultScroll);Grid.SetColumn(right,2);Children.Add(right);
        var workspace=new Grid();foreach(var column in ColumnDefinitions)workspace.ColumnDefinitions.Add(new(){Width=column.Width});var workspaceItems=Children.Cast<UIElement>().ToArray();Children.Clear();ColumnDefinitions.Clear();foreach(var item in workspaceItems)workspace.Children.Add(item);
        scenarioDrawer=new ScenarioDrawer(workspace,center);scenarioDrawer.SetStatusSource(message);Children.Add(scenarioDrawer);
        weapon.ItemsSource=RangedEngine.Weapons.Where(w=>w.Category==category).Select(w=>new Weapon(w.Id,w.Name,w.EnglishName,[],0,1,0,1,w.WeaponType)).ToArray();
        weapon.SelectionChanged+=(_,_)=>SelectWeapon();attack.SelectionChanged+=(_,_)=>{if(!loading){storedConditions=storedConditions with{Collisions=null};RefreshConditionControls();Refresh();}};arcane.SelectionChanged+=(_,_)=>{var id=(arcane.SelectedItem as ArcaneChoice)?.Id;var rule=RangedArcanes.Rules.SingleOrDefault(a=>a.Id==id);stacks.ItemsSource=Enumerable.Range(0,(rule?.Cap??0)+1).ToArray();stacks.SelectedIndex=0;arcane.ToolTip=KoreanPresentation.ArcaneDescription(rule,Math.Max(0,arcaneRank.SelectedIndex));Refresh();};arcaneRank.SelectionChanged+=(_,_)=>Refresh();stacks.SelectionChanged+=(_,_)=>Refresh();distance.TextChanged+=(_,_)=>Refresh();catalyst.Checked+=(_,_)=>Refresh();catalyst.Unchecked+=(_,_)=>Refresh();query.TextChanged+=(_,_)=>RefreshLibrary();sort.SelectionChanged+=(_,_)=>RefreshLibrary();
        encounter.SelectionChanged+=(_,_)=>{if(encounter.SelectedIndex!=0)primer.Clear();RefreshPrimer();Refresh();};killStacks.SelectionChanged+=(_,_)=>Refresh();RefreshPrimer();
        progenitor.SelectionChanged+=(_,_)=>Refresh();bonusPercent.TextChanged+=(_,_)=>Refresh();
        exilusMod.SelectionChanged+=(_,_)=>{var m=RangedEngine.ModCatalog.Mods.SingleOrDefault(m=>m.Id==(exilusMod.SelectedItem as ExilusChoice)?.Id);exilusRank.ItemsSource=Enumerable.Range(0,(m?.MaxRank??0)+1).ToArray();exilusRank.SelectedIndex=m?.MaxRank??0;Refresh();};exilusRank.SelectionChanged+=(_,_)=>Refresh();exilusPolarity.SelectionChanged+=(_,_)=>Refresh();
        library.AllowDrop=true;library.Drop+=(_,e)=>{if(e.Data.GetData("WFLabs.RangedSlot") is int i){slots[i]=null;RefreshConditionControls();Refresh();e.Handled=true;}};
        faction.ItemsSource=MeleeRuleCatalog.Load().EnemyProfiles.Where(f=>EnemyCatalog.Load().Enemies.Any(e=>e.Faction==f.Id)).ToArray();faction.DisplayMemberPath="Name";
        faction.SelectionChanged+=(_,_)=>{enemy.ItemsSource=EnemyCatalog.Load().Enemies.Where(e=>e.Faction==(faction.SelectedItem as EnemyDamageProfile)?.Id).ToArray();enemy.SelectedIndex=0;};
        enemy.SelectionChanged+=(_,_)=>{eximus.IsEnabled=(enemy.SelectedItem as EnemyDefinition)?.SupportsEximus==true;if(!eximus.IsEnabled)eximus.IsChecked=false;if(!loading){storedConditions=storedConditions with{Nullifier=null};RefreshConditionControls();}Refresh();};level.TextChanged+=(_,_)=>Refresh();difficulty.SelectionChanged+=(_,_)=>{if(difficulty.SelectedIndex==2)level.Text="999";Refresh();};eximus.Checked+=(_,_)=>Refresh();eximus.Unchecked+=(_,_)=>Refresh();faction.SelectedIndex=0;
        weapon.SelectedItem=weapon.Items.Cast<Weapon>().FirstOrDefault(w=>w.EnglishName==(category=="primary"?"Braton":"Lato"))??weapon.Items.Cast<Weapon>().First();
    }
    private void SelectWeapon()
    {
        pointerDrag?.Dispose();kitgun=null;if(Selected is not {} w)return;loading=true;riven=null;Array.Clear(slots);Array.Fill(polarities,"None");
        bonusRow.Visibility=w.Raw.TryGetProperty("IsLichWeapon",out var lich)&&lich.ValueKind==JsonValueKind.True?Visibility.Visible:Visibility.Collapsed;
        exilusMod.ItemsSource=new[]{new ExilusChoice("None","엑실러스 · 빈 슬롯")}.Concat(RangedEngine.ModCatalog.Mods.Where(m=>m.Exilus&&m.Limitations.Length==0&&RangedEngine.Compatible(m,w)).OrderBy(m=>m.Name).Select(m=>new ExilusChoice(m.Id,m.Name))).ToArray();exilusMod.SelectedIndex=0;exilusPolarity.SelectedValue=RangedEngine.S(w.Raw,"ExilusPolarity") is {Length:>0} ep?ep:"None";
        var previous=(arcane.SelectedItem as ArcaneChoice)?.Id;
        arcane.ItemsSource=new[]{new ArcaneChoice("None","아케인 없음")}.Concat(RangedArcanes.Rules.Where(a=>a.Category==category&&(a.Code!="Sharpshot"||w.WeaponType=="Bow")&&(a.Code!="Vendetta"||w.WeaponType=="Shotgun")&&(a.Code!="AkimboSlip"||w.WeaponType is "Dual Pistols" or "Dual Shotguns")).OrderBy(a=>a.Name).Select(a=>new ArcaneChoice(a.Id,a.Name))).ToArray();arcane.SelectedItem=arcane.Items.Cast<ArcaneChoice>().FirstOrDefault(a=>a.Id==previous)??arcane.Items[0];
        if(w.Raw.TryGetProperty("Polarities",out var p)&&p.ValueKind==JsonValueKind.Array){int i=0;foreach(var v in p.EnumerateArray().Take(8))polarities[i++]=v.GetString()??"None";}
        storedConditions=storedConditions with{ZoomIndex=0,WeaponBuffStacks=0,SniperComboHits=0,ReserveAmmo=null,Collisions=null,GlaiveReturnSeconds=null,ClusterLandings=null,FieldEndSeconds=null,FieldFirstPulse=0};
        evolutions=[];autoTransform=false;incarnonAmmo=null;
        attack.ItemsSource=RangedEngine.Attacks(w).Select((a,i)=>$"{i+1}. {KoreanPresentation.Attack(RangedEngine.S(a,"AttackName"))}").ToArray();attack.SelectedIndex=0;loading=false;RefreshConditionControls();Refresh();
    }
    private void RefreshConditionControls()
    {
        if(Selected is null)return;
        conditionPanel.Children.Clear();var c=storedConditions;
        CheckBox Toggle(string label,bool value,Action<bool> change){var control=new CheckBox{Content=label,IsChecked=value,Margin=new(0,0,12,8)};control.Checked+=(_,_)=>{change(true);Refresh();};control.Unchecked+=(_,_)=>{change(false);Refresh();};return control;}
        FrameworkElement Number(string label,double value,Action<double> change){var control=new TextBox{Text=value.ToString()};control.TextChanged+=(_,_)=>{change(double.TryParse(control.Text,out var v)?v:double.NaN);Refresh();};return Field(label,control);}
        FrameworkElement OptionalNumber(string label,double? value,Action<double?> change){var control=new TextBox{Text=value?.ToString()??"",ToolTip="빈칸은 미지정입니다. 원문에 없는 시간은 직접 정한 시나리오 값으로 사용합니다."};control.TextChanged+=(_,_)=>{change(string.IsNullOrWhiteSpace(control.Text)?null:double.TryParse(control.Text,out var v)?v:double.NaN);Refresh();};return Field(label,control);}
        conditionPanel.Children.Add(Toggle("미기재 규칙은 근사 계산",c.AllowApproximations,v=>{storedConditions=storedConditions with{AllowApproximations=v,ApproximationResolved=false,ApproximationNotes=null};}));
        conditionPanel.Children.Add(Text("근사 가정은 결과에 표시합니다. 직접 입력한 조건을 우선 사용합니다.",13));
        if(RangedPhantasma.Applies(Selected)&&attack.SelectedIndex>0)
        {
            conditionPanel.Children.Add(Number("충전할 탄약 · 0은 남은 탄창 완충",c.PhantasmaChargeAmmo,v=>storedConditions=storedConditions with{PhantasmaChargeAmmo=(int)v}));
            conditionPanel.Children.Add(OptionalNumber("피해 표의 기준 탄약 · 기본 근사 2발",c.PhantasmaReferenceAmmo,v=>storedConditions=storedConditions with{PhantasmaReferenceAmmo=v}));
            conditionPanel.Children.Add(Text("초당 11발 충전 · 연사력과 독립 · 본탄마다 자탄 5개",13));
        }
        if(slots.OfType<ModSlot>().Any(slot=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Effects.ContainsKey("DodgeHeadshotCriticalMultiplier")))conditionPanel.Children.Add(Number("데들리 매뉴버스 · 남은 강화 발사 (0–2)",c.DodgeShotsRemaining,v=>storedConditions=storedConditions with{DodgeShotsRemaining=(int)v}));
        var crowd=Stack(Number("같은 적의 수 (1–32)",c.GroupCount,v=>storedConditions=storedConditions with{GroupCount=(int)v}),Number("적 사이 간격 (m)",c.GroupSpacing,v=>storedConditions=storedConditions with{GroupSpacing=v}),Toggle("모든 적에 프라이머 적용",c.PrimerAllTargets,v=>storedConditions=storedConditions with{PrimerAllTargets=v}),Toggle("도탄도 약점에 명중",c.RicochetHeadshot,v=>storedConditions=storedConditions with{RicochetHeadshot=v}),Text("횡대는 좌우 배치, 일렬은 조준선 앞뒤 배치입니다. 현재 적을 처치하면 다음 적을 조준합니다."));
        crowd.Children.Add(Toggle("조준선에 일렬 배치 · 관통 적용",c.TargetsInLine,v=>{storedConditions=storedConditions with{TargetsInLine=v};RefreshConditionControls();}));
        if(c.TargetsInLine){crowd.Children.Add(OptionalNumber("몸통 유효 두께 (m) · 일반 관통",c.TargetBodyThickness,v=>storedConditions=storedConditions with{TargetBodyThickness=v}));crowd.Children.Add(OptionalNumber("관통 후 다음 적까지 (초) · 일반 투사체",c.PenetrationGapSeconds,v=>storedConditions=storedConditions with{PenetrationGapSeconds=v}));crowd.Children.Add(Text("무한 몸통 관통은 두께를 소비하지 않습니다. 일반 투사체는 관통 후 감속량이 원문에 없어 지정한 시간을 사용합니다. 히트스캔은 지연 없이 처리합니다."));}
        conditionPanel.Children.Add(new Expander{Header=$"적 무리 · {c.GroupCount}명",Content=crowd,IsExpanded=c.TargetsInLine});
        if(slots.OfType<ModSlot>().Any(slot=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Effects.GetValueOrDefault("KillExplosionDelayRequired")>0))
        {
            var delay=new TextBox{Text=c.KillExplosionDelay?.ToString()??""};delay.TextChanged+=(_,_)=>{storedConditions=storedConditions with{KillExplosionDelay=string.IsNullOrWhiteSpace(delay.Text)?null:double.TryParse(delay.Text,out var value)?value:double.NaN};Refresh();};
            conditionPanel.Children.Add(Field("처치 후 폭발 지연 가정 (초)",delay));conditionPanel.Children.Add(Text("컴버스천 빔: 위키에 지연 수치가 없어 직접 지정한 시간으로 계산합니다."));
        }
        var toggles=new WrapPanel();toggles.Children.Add(Toggle("조준",c.Aiming,v=>storedConditions=storedConditions with{Aiming=v}));toggles.Children.Add(Toggle("약점 명중",c.Headshot,v=>storedConditions=storedConditions with{Headshot=v}));conditionPanel.Children.Add(toggles);
        if(Selected is {} deployed&&RangedDeployments.Get(deployed.Id) is not null)
        {
            conditionPanel.Children.Add(Text("투척 → 목표 도착 → 기폭",16));
            conditionPanel.Children.Add(Number("목표 도착 후 기폭까지 (초)",c.DetonationDelay,v=>storedConditions=storedConditions with{DetonationDelay=v}));
        }
        var rule=Selected is {} w?RangedPassives.Get(w.Id):null;
        if(rule?.Zooms.Length>0){var zoom=new ComboBox{ItemsSource=rule.Zooms.Select(z=>z.Name).ToArray(),SelectedIndex=c.ZoomIndex};zoom.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{ZoomIndex=zoom.SelectedIndex};Refresh();};conditionPanel.Children.Add(Field("조준경 배율",zoom));}
        if(Selected is {} selected&&RangedIncarnons.Get(selected.Id) is {} incarnon)
        {
            var evolutionPanel=new StackPanel();
            foreach(var group in incarnon.Perks.GroupBy(p=>p.Stage))
            {
                var choices=new[]{new ArcaneChoice("None","선택 안 함")}.Concat(group.Select(p=>new ArcaneChoice(p.Id,p.Name))).ToArray();var selector=new ComboBox{DisplayMemberPath="Name",ItemsSource=choices};selector.SelectedItem=choices.FirstOrDefault(p=>evolutions.Contains(p.Id))??choices[0];
                selector.SelectionChanged+=(_,_)=>{evolutions=evolutions.Where(id=>!group.Any(p=>p.Id==id)).Concat(selector.SelectedItem is ArcaneChoice {Id:not "None"} choice?new[]{choice.Id}:[]).ToArray();RefreshConditionControls();Refresh();};evolutionPanel.Children.Add(Field($"진화 {group.Key}",selector));
            }
            foreach(var (id,trigger) in RangedEvolutionEvents.Selected(Read()).Where(t=>incarnon.Perks.Any(p=>t.Id.StartsWith(p.Id+"/",StringComparison.Ordinal))))
            {
                var state=c.EvolutionStates?.GetValueOrDefault(id)??new();
                var name=incarnon.Perks.Single(p=>id.StartsWith(p.Id+"/",StringComparison.Ordinal)).Name;
                void SetState(bool age,double value)
                {
                    var values=new Dictionary<string,RangedEvolutionState>(storedConditions.EvolutionStates??[]);
                    var old=values.GetValueOrDefault(id)??new();values[id]=age?old with{Age=value}:old with{Stacks=(int)value};
                    storedConditions=storedConditions with{EvolutionStates=values};
                }
                evolutionPanel.Children.Add(Number($"{name} · 시작 중첩 (0–{trigger.Cap})",state.Stacks,v=>SetState(false,v)));
                if(trigger.Duration>0)evolutionPanel.Children.Add(Number($"{name} · 발동 후 경과 (초)",state.Age,v=>SetState(true,v)));
            }
            evolutionPanel.Children.Add(Toggle("완충 시 인카논으로 전환",autoTransform,v=>autoTransform=v));
            evolutionPanel.Children.Add(Number("인카논 시작 탄약 · 0은 완충",incarnonAmmo??0,v=>incarnonAmmo=v==0?null:(int)v));
            evolutionPanel.Children.Add(Number("시작 전 연속 발사 횟수",c.FiringStacks,v=>storedConditions=storedConditions with{FiringStacks=(int)v}));
            evolutionPanel.Children.Add(Number("빈 탄창 재장전 · 시작 중첩",c.EmptyReloadStacks,v=>storedConditions=storedConditions with{EmptyReloadStacks=(int)v}));
            conditionPanel.Children.Add(new Expander{Header="인카논 진화 · 변신",Content=evolutionPanel,Margin=new(0,4,0,12)});
        }
        if(slots.OfType<ModSlot>().Any(slot=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Effects.GetValueOrDefault("DoubleTapDamage")>0))
            conditionPanel.Children.Add(new Expander{Header="더블 탭 · 시작 상태",Content=Stack(Number("시작 적중 중첩 (0–20)",c.DoubleTapStacks,v=>storedConditions=storedConditions with{DoubleTapStacks=(int)v}),Number("마지막 명중 이후 (초)",c.DoubleTapAge,v=>storedConditions=storedConditions with{DoubleTapAge=v}))});
        if(Selected is {} duplexWeapon&&RangedEngine.S(duplexWeapon.Raw,"Trigger")=="Duplex")conditionPanel.Children.Add(Number("누른 뒤 뗄 때까지 (초) · 0 = 즉시",c.DuplexReleaseDelay,v=>storedConditions=storedConditions with{DuplexReleaseDelay=v}));
        if(Selected is {} gaugeWeapon&&RangedAlternates.Get(gaugeWeapon.Id) is {} gauge)
        {
            conditionPanel.Children.Add(new Expander{Header="보조 공격 · 충전 게이지",Content=Stack(Number($"시작 충전 ({gauge.Required}개 이상 사용 / 최대 {gauge.Capacity})",c.AlternateCharge*gauge.Capacity,v=>storedConditions=storedConditions with{AlternateCharge=v/gauge.Capacity}),Toggle("완충하면 보조 공격 후 일반 사격으로 복귀",c.AutoAlternate,v=>storedConditions=storedConditions with{AutoAlternate=v}),Text("일반 사격의 처치로 충전합니다. 보조 공격만 선택하면 시작 충전량을 한 번 사용합니다."))});
        }
        if(Selected is {} frenzyWeapon&&RangedBurstAugments.IsFrenzy(frenzyWeapon,attack.SelectedIndex))
        {
            var rounding=new ComboBox{ItemsSource=new[]{new ArcaneChoice("Unspecified","위키 미기재 · 소수 탄수면 계산 중지"),new ArcaneChoice("Down","시나리오 · 소수 탄수 버림"),new ArcaneChoice("Up","시나리오 · 소수 탄수 올림")},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.RefundRounding};
            rounding.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{RefundRounding=rounding.SelectedValue as string??"Unspecified"};Refresh();};
            conditionPanel.Children.Add(new Expander{Header="와일드 프렌지 · 연사와 환급",IsExpanded=true,Content=Stack(Text("연사 중 조준은 해제되며 연사 모드·버프는 적용하지 않습니다. 2명 이상 처치한 연사를 완료하면 소비 탄약을 예비 탄약으로 환급합니다."),OptionalNumber("탄약 효율 적용 시 다음 연사 대기 (초)",c.FrenzyBurstPause,v=>storedConditions=storedConditions with{FrenzyBurstPause=v}),Field("소수 탄약 환급 처리",rounding))});
        }
        if(Selected is {} chargeWeapon&&RangedMagazineCharge.Applies(chargeWeapon,attack.SelectedIndex))
        {
            var chargeCount=new ComboBox{ItemsSource=new[]{new ArcaneChoice("0","탄창 전체 충전")}.Concat(Enumerable.Range(1,Math.Max(3,(int)(RangedEngine.N(chargeWeapon.Raw,"Magazine")??3)+10)).Select(n=>new ArcaneChoice(n.ToString(),$"로켓 {n}발"))).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.ChargedRounds.ToString()};
            chargeCount.SelectionChanged+=(_,_)=>{if(int.TryParse(chargeCount.SelectedValue as string,out var rounds)){storedConditions=storedConditions with{ChargedRounds=rounds};Refresh();}};
            conditionPanel.Children.Add(Field("충전해서 발사할 로켓",chargeCount));
        }
        if(slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey("VeloxStrengthPerHit")))conditionPanel.Children.Add(Number("벨록스 · 시작 명중 횟수 (0–100)",c.VeloxHits,v=>storedConditions=storedConditions with{VeloxHits=(int)v}));
        if(Selected is {} ordinarySimulor&&RangedOrdinarySimulor.Applies(ordinarySimulor)&&!RangedSingularity.Equipped(Read()))AddOrdinarySimulorControls(c,OptionalNumber);
        if(Selected is {} simulorWeapon&&RangedSimulor.Applies(simulorWeapon)&&!RangedSingularity.Equipped(Read()))
        {
            conditionPanel.Children.Add(new Expander{Header="구체 배치 · 합체",IsExpanded=true,Content=Stack(Toggle("같은 지점의 구체를 합체",c.SimulorMerge,v=>storedConditions=storedConditions with{SimulorMerge=v}),OptionalNumber("발사 후 목표 지점 도착 (초) · 1초 미만",c.SimulorArrivalSeconds,v=>storedConditions=storedConditions with{SimulorArrivalSeconds=v}),OptionalNumber("특이점 생성 후 첫 틱 (초) · 0–0.25",c.SimulorFirstTick,v=>storedConditions=storedConditions with{SimulorFirstTick=v}),Text("멀티샷은 구체 값으로 반영합니다. 중첩 전 1초, 합체 후 20초에 폭발합니다. 이동·합체 시간과 첫 틱은 원문에 없으므로 입력한 배치를 사용합니다. 수동 기폭은 예약 사건의 보조 공격으로 설정합니다."))});
        }
        if(Selected is {} vectisWeapon&&RangedVectis.Applies(vectisWeapon)&&attack.SelectedIndex>0)conditionPanel.Children.Add(new Expander{Header="벡티스 인카논 · 부착과 약점 폭발",IsExpanded=true,Content=Stack(OptionalNumber("몸통 부착 후 추가 첫 틱 (초)",c.VectisBodyTickOne,v=>storedConditions=storedConditions with{VectisBodyTickOne=v}),OptionalNumber("몸통 부착 후 추가 둘째 틱 (초)",c.VectisBodyTickTwo,v=>storedConditions=storedConditions with{VectisBodyTickTwo=v}),Toggle("간헐적인 약점 폭발 보너스 적용 시나리오",c.VectisHeadshotAoeBonus,v=>storedConditions=storedConditions with{VectisHeadshotAoeBonus=v}),Text("몸통은 즉시 1틱과 2.5초 내 추가 2틱, 약점은 즉시 1틱과 0.4초 후 동시 2회 폭발입니다. 본문의 부착 반경 약 0.5m를 사용합니다."))});
        if(Selected is {} stugWeapon&&RangedStug.Applies(stugWeapon))AddStugControls(c,Number,OptionalNumber,Toggle);
        AddTendrilControls(c,OptionalNumber);
        AddSeekerControls(c,OptionalNumber);
        AddSyndicateControls(c,Number,OptionalNumber);
        if(Selected is {} hemaWeapon&&RangedHema.Applies(hemaWeapon))AddHemaControls(c,Number,OptionalNumber,Toggle);
        if(Selected is {} bladeWeapon&&RangedReturningBlades.Get(bladeWeapon.Id) is not null)AddReturningBladeControls(c,Number,Toggle);
        if(Selected is {} bubbleWeapon&&RangedAttackLinks.Children(bubbleWeapon.Id,attack.SelectedIndex).Any(l=>l.MaximumDelay is not null))
            conditionPanel.Children.Add(Stack(Number("물방울 명중 후 폭발까지 (초) · 0.6–0.8",c.BubbleDelay,v=>storedConditions=storedConditions with{BubbleDelay=v}),Text("위키의 지연 범위 내에서 선택한 시점을 사용합니다. 기본 0.6초는 무기 데이터의 값이며, 무작위 분포를 가정하지 않습니다.")));
        if(Selected is {} airburstWeapon&&RangedAirbursts.Get(airburstWeapon.Id,attack.SelectedIndex) is {} airburst)
        {
            if(airburst.Manual)conditionPanel.Children.Add(OptionalNumber("공중 기폭 시점 (초) · 빈칸은 2초 자동 기폭",c.AirburstTime,v=>storedConditions=storedConditions with{AirburstTime=v}));
            else conditionPanel.Children.Add(Text("발사 후 0.25초 공중 기폭 · 기본 이동 거리 20m. 기폭 전 직격하면 폭발하지 않습니다."));
            conditionPanel.Children.Add(Number("폭발 중심에서 대상까지 거리 (m)",c.RadialDistance,v=>storedConditions=storedConditions with{RadialDistance=v}));
        }
        if(Selected is {} grenadeWeapon&&RangedReloadGrenades.Get(grenadeWeapon.Id) is not null)
        {
            conditionPanel.Children.Add(Number("크리티컬 뮤테이션 · 시작 버프 (%)",c.CriticalMutationBonus*100,v=>storedConditions=storedConditions with{CriticalMutationBonus=v/100}));
            conditionPanel.Children.Add(new Expander{Header="재장전 유탄 · 투척 시간",IsExpanded=true,Content=Stack(Text("탄창을 비우면 바로 재장전하는 사격 패턴입니다. 유탄 투척 시점·비행 시간은 위키에 없어 설정값을 사용합니다."),OptionalNumber("투척 시점 · 재장전 진행률 (%)",c.ReloadGrenadePhase*100,v=>storedConditions=storedConditions with{ReloadGrenadePhase=v/100}),OptionalNumber("목표까지 유탄 비행 시간 (초)",c.GrenadeFlightSeconds,v=>storedConditions=storedConditions with{GrenadeFlightSeconds=v}))});
        }
        if(Selected is {} unknownCharge&&RangedPassives.Get(unknownCharge.Id)?.Kind!="Onos"&&RangedEngine.Attacks(unknownCharge).Any(a=>RangedEngine.S(a,"Trigger").Contains("Charge")&&RangedEngine.N(a,"ChargeTime") is null))
            conditionPanel.Children.Add(new Expander{Header="충전 시간 · 원문 미기재",IsExpanded=true,Content=Stack(Text("충전 시간과 발사 후 대기는 위키에서 구분되어 있지 않습니다. 아래는 모드 적용 후 실제 시간의 시나리오이며, 폭발 지연은 무기별 확정값을 적용합니다."),OptionalNumber("충전 시간 (초)",c.ChargeCycleSeconds,v=>storedConditions=storedConditions with{ChargeCycleSeconds=v}),Number("발사 후 다음 충전까지 (초)",c.ChargeRecoverySeconds,v=>storedConditions=storedConditions with{ChargeRecoverySeconds=v}))});
        if(Selected is {} charging&&RangedPassives.Get(charging.Id)?.Kind=="Onos")
        {
            var timing=Stack(Text("인카논 충전 중 타격을 선택하면 완충 발사까지 함께 계산합니다. 아래 시간은 위키 확정값이 아닌 시나리오 입력입니다."),Number("모드 적용 후 충전 시간 가정 (초) · 0은 미지정",c.ChargeCycleSeconds??0,v=>storedConditions=storedConditions with{ChargeCycleSeconds=v==0?null:v}),Number("완충 발사 후 다음 충전 대기 (초)",c.ChargeRecoverySeconds,v=>storedConditions=storedConditions with{ChargeRecoverySeconds=v}));
            timing.Children.Add(Number("캐스케이드 · 시작 중첩 (0–50)",c.ChargeDamageStacks,v=>storedConditions=storedConditions with{ChargeDamageStacks=(int)v}));
            var reset=new ComboBox{ItemsSource=new[]{new ArcaneChoice("Unspecified","미지정 · 첫 사이클만 확정"),new ArcaneChoice("OnRelease","발사 때 소비하는 시나리오"),new ArcaneChoice("Persistent","다음 충전에도 유지하는 시나리오")},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.ChargeStackReset};reset.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{ChargeStackReset=reset.SelectedValue as string??"Unspecified"};Refresh();};timing.Children.Add(Field("완충 발사 후 중첩 · 원문 미기재",reset));
            conditionPanel.Children.Add(new Expander{Header="오노스 · 충전 사이클",Content=timing,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Selected is {} fieldWeapon&&RangedFields.Get(fieldWeapon.Id) is {} field&&field.Parents.Contains(attack.SelectedIndex))
        {
            var settings=Stack(Text($"장판 유지 {field.Duration:0.##}초 · 기본 틱 간격 {field.Interval:0.##}초"),Number("첫 틱 위치 가정 · 주기의 비율 (0–1)",c.FieldFirstPulse,v=>storedConditions=storedConditions with{FieldFirstPulse=v}),Text("0 = 생성 즉시, 1 = 한 주기 후. 위키에 첫 틱 시점은 명시되어 있지 않습니다."));
            if(field.RepeatAfterReload)settings.Children.Add(Toggle("적에게 꽂기 · 이전 장판 유지",c.SpearEmbeddedInEnemy,v=>storedConditions=storedConditions with{SpearEmbeddedInEnemy=v}));
            if(field.RequireDuration){settings.Children.Add(Text("팍스 구름 지속 시간이 본문(4초)과 모듈(5초)에서 다릅니다."));settings.Children.Add(Stack(Button("본문 · 4초",()=>{storedConditions=storedConditions with{FieldEndSeconds=4};RefreshConditionControls();Refresh();}),Button("모듈 · 5초",()=>{storedConditions=storedConditions with{FieldEndSeconds=5};RefreshConditionControls();Refresh();}),Text(c.FieldEndSeconds is {} chosen?$"선택한 지속 시간: {chosen:0.##}초":"계산 기준 미선택")));}
            if(field.Single)settings.Children.Add(Number("회수·조기 기폭 (초) · 0은 끝까지 유지",c.FieldEndSeconds??0,v=>storedConditions=storedConditions with{FieldEndSeconds=v==0?null:v}));
            conditionPanel.Children.Add(new Expander{Header="지속 장판 · 시간 조건",Content=settings,Margin=new(0,4,0,12)});
        }
        if(Selected is {} clusterWeapon&&RangedClusters.Get(clusterWeapon.Id) is {} clusters&&attack.SelectedIndex==clusters.Parent)
        {
            var panel=Stack(Text($"주탄마다 자탄 {clusters.Count}개 생성. 아래에 지정한 자탄만 적에게 맞는 배치로 계산합니다. 착탄 시간은 위키에 없어 설정값을 사용합니다."));
            var paths=c.ClusterLandings??[];
            for(int i=0;i<paths.Length;i++)
            {
                int index=i;var landing=paths[i];
                void Change(Func<RangedCollision,RangedCollision> edit){var values=storedConditions.ClusterLandings!.ToArray();values[index]=edit(values[index]);storedConditions=storedConditions with{ClusterLandings=values};}
                panel.Children.Add(Stack(Text($"자탄 {i+1}",15),Number("주탄 폭발 후 (초)",landing.AfterImpact,v=>Change(x=>x with{AfterImpact=v})),Number("대상 번호 · 1부터",landing.Target+1,v=>Change(x=>x with{Target=(int)v-1})),Toggle("적 옆 지면에 떨어짐 · 폭발만 적용",landing.Surface,v=>Change(x=>x with{Surface=v})),Button("자탄 삭제",()=>{storedConditions=storedConditions with{ClusterLandings=(storedConditions.ClusterLandings??[]).Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();})));
            }
            var add=Button("＋ 적 근처 자탄 착탄",()=>{storedConditions=storedConditions with{ClusterLandings=[..storedConditions.ClusterLandings??[],new(1,Surface:true)]};RefreshConditionControls();Refresh();});add.IsEnabled=paths.Length<clusters.Count;panel.Children.Add(add);
            panel.Children.Add(Button("자탄은 모두 적 밖에 착탄",()=>{storedConditions=storedConditions with{ClusterLandings=[]};RefreshConditionControls();Refresh();}));
            if(c.ClusterLandings is null)panel.Children.Add(Text("착탄 배치 미지정 · 근사 계산 시 자탄 전부가 0.5초 뒤 같은 적에 명중합니다."));
            conditionPanel.Children.Add(new Expander{Header="집속탄 · 자탄 착탄 위치",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Selected is {} ricochetWeapon&&RangedCollisions.Get(ricochetWeapon.Id,attack.SelectedIndex) is {} ricochet)
        {
            var path=new StackPanel();path.Children.Add(Text("추가로 맞는 충돌만 입력하세요. 위키에는 지형별 충돌 시각이 없어 아래 경로를 가정합니다. 멀티샷도 같은 경로를 사용합니다."));
            var contacts=c.Collisions??[];
            for(int i=0;i<contacts.Length;i++)
            {
                int index=i;var hit=contacts[i];
                void Change(Func<RangedCollision,RangedCollision> edit){var items=storedConditions.Collisions!.ToArray();items[index]=edit(items[index]);storedConditions=storedConditions with{Collisions=items};}
                path.Children.Add(Stack(Text($"추가 충돌 {i+1}",15),Number("첫 충돌 후 (초)",hit.AfterImpact,v=>Change(x=>x with{AfterImpact=v})),Number("대상 번호 · 1부터",hit.Target+1,v=>Change(x=>x with{Target=(int)v-1})),Toggle("적 옆 지형에 충돌 · 폭발만 적용",hit.Surface,v=>Change(x=>x with{Surface=v})),Toggle("직접 충돌은 약점 적중",hit.Headshot,v=>Change(x=>x with{Headshot=v})),Button("충돌 삭제",()=>{storedConditions=storedConditions with{Collisions=(storedConditions.Collisions??[]).Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();})));
            }
            path.Children.Insert(0,Toggle("첫 충돌은 적이 아닌 지형",c.FirstImpactSurface,v=>storedConditions=storedConditions with{FirstImpactSurface=v}));
            if(RangedEngine.S(ricochetWeapon.Raw,"Family")=="Tetra")path.Children.Add(Toggle("입력한 충돌까지 투사체가 유지되는 경로로 가정",c.BouncePathConfirmed,v=>storedConditions=storedConditions with{BouncePathConfirmed=v}));
            var add=Button("＋ 추가 충돌",()=>{storedConditions=storedConditions with{Collisions=[..storedConditions.Collisions??[],new((storedConditions.Collisions??[]).Select(x=>x.AfterImpact).DefaultIfEmpty(0).Max()+1)]};RefreshConditionControls();Refresh();});add.IsEnabled=contacts.Length<RangedCollisions.MaximumContacts(Capture(),ricochet)-1;path.Children.Add(add);
            if(RangedPassives.Get(ricochetWeapon.Id)?.Kind=="Cedo")path.Children.Add(Number("반복 사용 · 회수 시간 가정 (초) · 0은 미지정",c.GlaiveReturnSeconds??0,v=>storedConditions=storedConditions with{GlaiveReturnSeconds=v==0?null:v}));
            conditionPanel.Children.Add(new Expander{Header="도탄 · 추가 충돌 경로",Content=path,IsExpanded=contacts.Length>0,Margin=new(0,4,0,12)});
        }
        AddVariableAugmentControls(c,OptionalNumber);
        AddOrbBuffControls(c);
        if(Selected is {} zymosWeapon&&RangedZymos.Applies(zymosWeapon))AddZymosControls(c,OptionalNumber);
        if(Selected is {} infestedWeapon&&RangedInfestedOrbs.Applies(infestedWeapon))AddInfestedControls(c,OptionalNumber);
        if(Selected is {} pullWeapon&&RangedPullWeapons.Applies(pullWeapon))AddPullControls(c,OptionalNumber);
        if(Selected is {} quanta&&RangedQuanta.Applies(quanta))AddQuantaControls(c,Number,OptionalNumber);
        if(slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey("SentientBarrageCritical")))conditionPanel.Children.Add(Toggle("센티언트 배라지 · 적중 충전으로 다음 방전 강화됨",c.SentientBarrageReady,v=>storedConditions=storedConditions with{SentientBarrageReady=v}));
        if(Selected is {} tome&&RangedPassives.Get(tome.Id)?.Kind=="Grimoire")
        {
            var orb=Stack(Number("시작 보조 공격 게이지 (%)",c.AlternateCharge*100,v=>storedConditions=storedConditions with{AlternateCharge=v/100}),Toggle("구체가 표적 위치에서 멈추는 배치",c.OrbAnchored,v=>storedConditions=storedConditions with{OrbAnchored=v}),Number("첫 방전까지 지연 가정 (0–1초)",c.OrbFirstPulseDelay,v=>storedConditions=storedConditions with{OrbFirstPulseDelay=v}),Text("구체는 발사 시점부터 6초 뒤 폭발합니다. 첫 방전 시각은 원문에 없어 설정한 지연을 사용합니다. 기본 배치는 직선 이동입니다."));
            conditionPanel.Children.Add(new Expander{Header="그리모어 · 구체와 충전",Content=orb});
        }
        if((c.ExternalEvents??[]).Any(e=>e.Kind=="Holster"))
        {
            conditionPanel.Children.Add(Number("외부 신스 세트 모드 수",c.ExternalSynthMods,v=>storedConditions=storedConditions with{ExternalSynthMods=(int)v}));
            var roundingKeys=new[]{"Unspecified","Down","Up","Accumulate"};var rounding=new ComboBox{ItemsSource=new[]{"원문에 없는 소수 탄수는 계산 중지","가정: 매초 버림","가정: 매초 올림","가정: 소수 탄수를 다음 초로 누적"},SelectedIndex=Array.IndexOf(roundingKeys,c.HolsterRounding)};
            rounding.SelectionChanged+=(_,_)=>{if(rounding.SelectedIndex>=0){storedConditions=storedConditions with{HolsterRounding=roundingKeys[rounding.SelectedIndex]};Refresh();}};
            conditionPanel.Children.Add(Field("수납 재장전 · 소수 탄수 처리",rounding));
        }
        if((c.ExternalEvents??[]).Any(e=>e.Kind is "PrimaryAmmoDrop" or "SecondaryAmmoDrop"))conditionPanel.Children.Add(Toggle("다른 무기의 예비 탄약이 가득 참 · 남는 탄약 변환 허용",c.OtherWeaponAmmoFull,v=>storedConditions=storedConditions with{OtherWeaponAmmoFull=v}));
        var eventPanel=new StackPanel();
        if(RangedEngine.ModCatalog.Mods.Any(m=>m.Effects.ContainsKey("TomeDefenseStrip")&&(slots.OfType<ModSlot>().Any(s=>s.ModId==m.Id)||exilusMod.SelectedItem is ExilusChoice choice&&choice.Id==m.Id)))conditionPanel.Children.Add(Number("공유 경험치 범위 (m)",c.AffinityRange,v=>storedConditions=storedConditions with{AffinityRange=v}));
        var actions=c.ExternalEvents??[];
        for(int i=0;i<actions.Length;i++)
        {
            var index=i;var action=actions[i];var row=new Grid{Margin=new(0,0,0,6)};
            row.ColumnDefinitions.Add(new(){Width=new GridLength(70)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(68)});row.ColumnDefinitions.Add(new(){Width=new GridLength(34)});
            void Change(Func<RangedExternalEvent,RangedExternalEvent> change)
            {var values=(storedConditions.ExternalEvents??[]).ToArray();values[index]=change(values[index]);storedConditions=storedConditions with{ExternalEvents=values};Refresh();}
            var at=new TextBox{Text=action.Time.ToString(),ToolTip="교전 시작 후 경과 시간 (초)"};
            at.TextChanged+=(_,_)=>Change(old=>old with{Time=double.TryParse(at.Text,out var v)?v:double.NaN});
            var kinds=RangedEventLabels.Kinds.Where(k=>(k!="AlternateFire"||Selected is {} selectedTome&&(RangedPassives.Get(selectedTome.Id)?.Kind=="Grimoire"||RangedAlternates.Get(selectedTome.Id) is not null||RangedAzima.Applies(selectedTome)||RangedQuanta.Applies(selectedTome)||RangedInfestedOrbs.Applies(selectedTome)||RangedPentaAugments.Applies(selectedTome)||RangedVileDischarge.Applies(selectedTome)||RangedSimulor.Applies(selectedTome)||RangedOrdinarySimulor.Applies(selectedTome)||selectedTome.Id==RangedPullWeapons.Scyotid||RangedEngine.S(selectedTome.Raw,"Family")=="EFV-5 Jupiter")||action.Kind=="AlternateFire")&&(k!="BlockedShot"||Selected is {} shieldWeapon&&RangedAlternates.Get(shieldWeapon.Id)?.BlockSeconds>0||action.Kind=="BlockedShot")).Select(k=>new ArcaneChoice(k,RangedEventLabels.Name(k))).ToArray();
            var kind=new ComboBox{ItemsSource=kinds,DisplayMemberPath="Name",SelectedItem=kinds.Single(k=>k.Id==action.Kind)};
            kind.SelectionChanged+=(_,_)=>{if(kind.SelectedItem is ArcaneChoice value){Change(old=>old with{Kind=value.Id});RefreshConditionControls();}};
            var amount=new TextBox{Text=action.Value.ToString(),IsEnabled=action.Kind is "ArrowPickup" or "WeaponAffinity" or "AllyKill" or "Holster" or "EnergySpent" or "EnergyRestore" or "AmmoPickup" or "PrimaryAmmoDrop" or "SecondaryAmmoDrop" or "UniversalAmmoDrop",ToolTip="무기 숙련도 양, 외부 처치 대상 번호(1부터), 수납 지속 시간(초), 에너지 소모·회복량 또는 획득 탄약 수 / 상자 개수"};
            amount.TextChanged+=(_,_)=>Change(old=>old with{Value=double.TryParse(amount.Text,out var v)?v:double.NaN});
            var remove=Button("×",()=>{storedConditions=storedConditions with{ExternalEvents=(storedConditions.ExternalEvents??[]).Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();});remove.ToolTip="발동 시점 삭제";
            UIElement[] controls=[at,kind,amount,remove];for(int column=0;column<controls.Length;column++){Grid.SetColumn(controls[column],column);row.Children.Add(controls[column]);}eventPanel.Children.Add(row);
        }
        eventPanel.Children.Insert(0,Text("시간(초) · 발동 조건 · 에너지량",13));
        eventPanel.Children.Add(Button("＋ 발동 시점",()=>{storedConditions=storedConditions with{ExternalEvents=[..storedConditions.ExternalEvents??[],new(0,"AbilityCast")]};RefreshConditionControls();Refresh();}));
        conditionPanel.Children.Add(new Expander{Header="외부 버프 발동 시점",Content=eventPanel,Margin=new(0,4,0,12),IsExpanded=actions.Length>0});
        var advanced=new StackPanel();
        advanced.Children.Add(Toggle("에너지를 소모하는 채널링 활성",c.Channeling,v=>storedConditions=storedConditions with{Channeling=v}));
        advanced.Children.Add(Toggle("공중 상태",c.Airborne,v=>storedConditions=storedConditions with{Airborne=v}));
        advanced.Children.Add(Toggle("에임 글라이드",c.AimGlide,v=>storedConditions=storedConditions with{AimGlide=v}));
        advanced.Children.Add(Toggle("슬라이딩",c.Sliding,v=>storedConditions=storedConditions with{Sliding=v}));
        advanced.Children.Add(Toggle("벽 붙잡기",c.WallLatch,v=>storedConditions=storedConditions with{WallLatch=v}));
        if(category=="secondary")advanced.Children.Add(Toggle("주무기도 장착한 로드아웃",c.PrimaryEquipped,v=>storedConditions=storedConditions with{PrimaryEquipped=v}));
        advanced.Children.Add(Toggle("예비 탄약 계속 보급",c.ReplenishReserve,v=>storedConditions=storedConditions with{ReplenishReserve=v}));
        var reserveInput=new TextBox{Text=c.ReserveAmmo?.ToString()??"",ToolTip="비워 두면 최대 예비 탄약으로 시작"};reserveInput.TextChanged+=(_,_)=>{storedConditions=storedConditions with{ReserveAmmo=string.IsNullOrWhiteSpace(reserveInput.Text)?null:double.TryParse(reserveInput.Text,out var value)?value:double.NaN};Refresh();};advanced.Children.Add(Field("시작 예비 탄약 · 빈칸은 최대",reserveInput));
        advanced.Children.Add(Toggle("교전 시작 시 무기 꺼내기",c.NewlyEquipped,v=>storedConditions=storedConditions with{NewlyEquipped=v}));
        if(slots.OfType<ModSlot>().Any(slot=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Effects.ContainsKey("MagazineHitBonus")))
            advanced.Children.Add(Number("이전 탄창에서 명중한 발사 수 (0–15)",c.PreviousMagazineHits,v=>storedConditions=storedConditions with{PreviousMagazineHits=(int)v}));
        advanced.Children.Add(Number("다른 장비의 비질란티 세트 개수",c.ExternalVigilanteMods,v=>storedConditions=storedConditions with{ExternalVigilanteMods=(int)v}));
        advanced.Children.Add(Number("로어 · 실제 피해 증가 (%)",c.Roar*100,v=>storedConditions=storedConditions with{Roar=v/100}));
        advanced.Children.Add(Number("이클립스 · 실제 피해 증가 (%)",c.Eclipse*100,v=>storedConditions=storedConditions with{Eclipse=v/100}));
        var pistoleer=new ComboBox{ItemsSource=new[]{"장착 안 함","0 랭크 · 10%","1 랭크 · 20%","2 랭크 · 30%","3 랭크 · 40%","4 랭크 · 50%","5 랭크 · 60%"},SelectedIndex=c.PistoleerRank+1};pistoleer.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{PistoleerRank=pistoleer.SelectedIndex-1};Refresh();};advanced.Children.Add(Field("아케인 피스톨리어 · 보조무기 약점 처치 시 탄약 무한 12초",pistoleer));
        advanced.Children.Add(Number("외부 탄약 무한 · 시작 시 남은 시간 (초)",c.InfiniteAmmoSeconds,v=>storedConditions=storedConditions with{InfiniteAmmoSeconds=v}));
        advanced.Children.Add(Number("자세 이상 회복 시간 가정 (초) · 0은 미지정",c.PostureRecoverySeconds??0,v=>storedConditions=storedConditions with{PostureRecoverySeconds=v==0?null:v}));
        advanced.Children.Add(Number("시작 시 대상의 남은 체력 (%)",c.TargetHealthFraction*100,v=>storedConditions=storedConditions with{TargetHealthFraction=v/100}));
        advanced.Children.Add(Number("내 주변 6m 이내 적 수",c.EnemiesWithinSixMeters,v=>storedConditions=storedConditions with{EnemiesWithinSixMeters=(int)v}));
        advanced.Children.Add(Number("다른 적까지의 최소 거리 (m)",c.NearestOtherEnemyDistance,v=>storedConditions=storedConditions with{NearestOtherEnemyDistance=v}));
        advanced.Children.Add(Number("폭발 중심에서 대상까지 (m)",c.RadialDistance,v=>storedConditions=storedConditions with{RadialDistance=v}));
        advanced.Children.Add(Number("대상의 기본 약점 배율",c.WeakpointMultiplier,v=>storedConditions=storedConditions with{WeakpointMultiplier=v}));
        advanced.Children.Add(Toggle("투명화",c.Invisible,v=>storedConditions=storedConditions with{Invisible=v}));
        advanced.Children.Add(Toggle("오버실드 활성",c.Overshields,v=>storedConditions=storedConditions with{Overshields=v}));
        advanced.Children.Add(Toggle("질주 중 재장전",c.Sprinting,v=>storedConditions=storedConditions with{Sprinting=v}));
        if(RangedEngine.S(Selected!.Raw,"Family") is "Dread" or "Despair")advanced.Children.Add(Toggle("드레드 · 디스페어 · 헤이트 함께 장착",c.StalkerSet,v=>storedConditions=storedConditions with{StalkerSet=v}));
        advanced.Children.Add(Toggle("시그니처 워프레임 사용",c.SignatureOwner,v=>storedConditions=storedConditions with{SignatureOwner=v}));
        advanced.Children.Add(Number("워프레임 질주 속도",c.SprintSpeed,v=>storedConditions=storedConditions with{SprintSpeed=v}));
        advanced.Children.Add(Toggle("장애물·다른 적을 관통한 뒤 대상에 명중",c.AfterPunchThrough,v=>storedConditions=storedConditions with{AfterPunchThrough=v}));
        var misses=new TextBox{Text=string.Join(", ",c.MissedShots??[]),ToolTip="예: 2, 5 · 지정 발사는 직격과 폭발 모두 빗나감"};misses.TextChanged+=(_,_)=>{var parts=misses.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);storedConditions=storedConditions with{MissedShots=parts.Select(p=>int.TryParse(p,out var n)?n:-1).ToArray()};Refresh();};advanced.Children.Add(Field("빗나갈 발사 번호 · 빈칸은 전부 명중",misses));
        var pierced=new CheckBox{Content="투사체가 대상을 관통해 통과",IsChecked=c.PiercedTarget,Margin=new(0,0,0,12)};pierced.Checked+=(_,_)=>{storedConditions=storedConditions with{PiercedTarget=true};Refresh();};pierced.Unchecked+=(_,_)=>{storedConditions=storedConditions with{PiercedTarget=false};Refresh();};advanced.Children.Add(pierced);
        advanced.Children.Add(Number("내 최대 실드",c.PlayerMaxShields,v=>storedConditions=storedConditions with{PlayerMaxShields=v}));
        advanced.Children.Add(Number("내 부족한 실드량",c.PlayerShieldDeficit,v=>storedConditions=storedConditions with{PlayerShieldDeficit=v}));
        advanced.Children.Add(Number("내 최대 체력",c.PlayerMaxHealth,v=>storedConditions=storedConditions with{PlayerMaxHealth=v}));
        advanced.Children.Add(Number("내 현재 체력",c.PlayerHealth,v=>storedConditions=storedConditions with{PlayerHealth=v}));
        advanced.Children.Add(Number("내 워프레임 방어력",c.PlayerArmor,v=>storedConditions=storedConditions with{PlayerArmor=v}));
        advanced.Children.Add(Number("내 최대 에너지",c.MaxEnergy,v=>storedConditions=storedConditions with{MaxEnergy=v}));
        advanced.Children.Add(Number("내 현재 에너지",c.CurrentEnergy,v=>storedConditions=storedConditions with{CurrentEnergy=v}));
        advanced.Children.Add(Number("세컨더리 서지 · 저장 피해 증가 (%)",c.StoredSurgeBonus*100,v=>storedConditions=storedConditions with{StoredSurgeBonus=v/100}));
        advanced.Children.Add(Number("무기 고유 버프 · 시작 중첩",c.WeaponBuffStacks,v=>storedConditions=storedConditions with{WeaponBuffStacks=(int)v}));
        advanced.Children.Add(Number("고유 버프 발동 후 경과 (초)",c.WeaponBuffAge,v=>storedConditions=storedConditions with{WeaponBuffAge=v}));
        if(rule?.ComboMinimum>0)advanced.Children.Add(Number("저격 콤보 · 시작 명중 횟수",c.SniperComboHits,v=>storedConditions=storedConditions with{SniperComboHits=(int)v}));
        conditionPanel.Children.Add(new Expander{Header="외부 버프 · 시작 조건",Content=advanced,Margin=new(0,4,0,12)});
    }
    private RangedBuild Read()=>new(Selected?.Id??"",attack.SelectedIndex,slots.ToArray(),polarities.ToArray(),catalyst.IsChecked==true,(arcane.SelectedItem as ArcaneChoice)?.Id??"None",stacks.SelectedIndex,double.TryParse(distance.Text,out var d)?d:double.NaN,Enemy:enemy.SelectedItem is EnemyDefinition e?new(e.Id,int.TryParse(level.Text,out var l)?l:0,(EnemyDifficulty)difficulty.SelectedIndex,Eximus:eximus.IsChecked==true):null,Conditions:storedConditions with {Statuses=new(primer),Kills=killStacks.SelectedIndex},ArcaneRank:arcaneRank.SelectedIndex,Progenitor:bonusRow.Visibility==Visibility.Visible?new(((ElementChoice)progenitor.SelectedItem).Type,double.TryParse(bonusPercent.Text,out var percent)?percent/100:double.NaN):null,EncounterMode:encounter.SelectedIndex,Exilus:exilusMod.SelectedItem is ExilusChoice {Id:not "None"} em?new(em.Id,exilusRank.SelectedIndex):null,ExilusPolarity:exilusPolarity.SelectedValue as string??"None",Evolutions:evolutions.ToArray(),AutoTransform:autoTransform,IncarnonAmmo:incarnonAmmo,Riven:riven,Kitgun:kitgun);
    private void Refresh()
    {
        if(loading||Selected is null)return;InvalidateCombat();RefreshSlots();RefreshLibrary();results.Children.Clear();combatResultHost.Content=null;progressText.Text="모딩 수치 갱신됨 · 교전 결과는 DPS · TTK 계산으로 갱신";
        exilusRank.IsEnabled=exilusMod.SelectedItem is ExilusChoice {Id:not "None"};
        arcaneRank.IsEnabled=arcane.SelectedItem is ArcaneChoice {Id:not "None"};arcane.ToolTip=KoreanPresentation.ArcaneDescription(RangedArcanes.Rules.SingleOrDefault(a=>a.Id==(arcane.SelectedItem as ArcaneChoice)?.Id),Math.Max(0,arcaneRank.SelectedIndex));stacks.IsEnabled=arcaneRank.IsEnabled&&stacks.Items.Count>1;
        scenarioDrawer?.SetSummary($"{(enemy.SelectedItem as EnemyDefinition)?.Name??"대상"} · Lv. {level.Text}{(eximus.IsChecked==true?" · 엑시무스":"")} · {difficulty.SelectedItem} · {encounter.SelectedItem}");
        try
        {
            var firingAttack=RangedAttackSelection.FiringAttack(Selected,attack.SelectedIndex);calculateButton.Content=firingAttack!=attack.SelectedIndex?"전체 공격 DPS · TTK 계산":"DPS · TTK 계산";
            var r=RangedEngine.Calculate(Read());SetAccuracy(r.Scope,false);results.Children.Add(approximationDetails);RefreshComparison();capacity.Text=$"수용량  {r.Drain} / {r.Capacity}"+(r.Drain>r.Capacity?" · 초과 / 계획용 계산":"");
            var selectedBuild=Read();var carrier=RangedSimulor.Applies(Selected)&&selectedBuild.AttackIndex==0||RangedStug.Launch(Selected,selectedBuild.AttackIndex);
            var turret=RangedAzima.Applies(Selected)&&selectedBuild.AttackIndex==2;
            var presented=turret?RangedEngine.Calculate(selectedBuild with{AttackIndex=0}):carrier?RangedEngine.Calculate(selectedBuild with{AttackIndex=RangedSimulor.Applies(Selected)?2:selectedBuild.AttackIndex==2?3:1}):r;
            results.Children.Add(Text(turret?"터렛과 공유하는 기본 사격 수치":carrier?"폭발 기준 모딩 · 구체 1값":"현재 모딩",20));var stats=new UniformGrid{Columns=4};
            foreach(var (name,value) in new[]{("치명타 확률",$"{presented.CriticalChance:P1}"),("치명타 배수",$"{presented.CriticalMultiplier:0.00}×"),("상태 확률 / 탄환",!storedConditions.AllowApproximations&&r.Modifiers!.GetValueOrDefault("OscillatingStatusChance")>0&&(storedConditions.FluxOscillation is null||Selected.EnglishName=="Tenet Flux Rifle"&&storedConditions.TenetFluxDoubleBase is null)?"변동항 설정 필요":$"{presented.StatusChance:P1}"),("멀티샷",$"{r.Multishot:0.00}")})stats.Children.Add(Stack(Text(name),Text(value,value.Contains("설정")?16:28)));
            results.Children.Add(RivenPresentation.Rule(stats));results.Children.Add(comparisonHost);results.Children.Add(combatResultHost);results.Children.Add(Text(turret?"탄창 투척 → 4방향 사격 → 만료 폭발":carrier?"구체 발사 → 부착·합체 → 폭발":"직접 피해 · 선택한 공격 구성요소",20));
            if(turret)results.Children.Add(Text("투척 자체는 직접 피해가 없습니다. 아래는 멀티샷을 포함한 기본 사격 1회의 모딩 수치입니다. 투척 시 멀티샷은 터렛 수에 반영됩니다. 회전·명중 경로·복수 터렛·만료 폭발을 포함한 DPS는 교전 분석에서 계산합니다."));
            if(carrier)results.Children.Add(Text("발사체 자체는 피해가 없습니다. 아래는 구체 1값의 폭발 수치이며, 실제 중첩과 주기 피해를 포함한 DPS는 교전 분석으로 계산합니다."));
            var metrics=new UniformGrid{Columns=3};foreach(var (name,value) in new[]{(turret?"기본 사격 기대 피해":carrier?"구체 1값 폭발 기대 피해":"발사당 기대 피해",presented.ExpectedShot.ToString("N1")),("발사 중 DPS",r.BurstDps?.ToString("N1")??(r.TimingIssues.Length==0?"교전 분석에서 계산":"계산 조건 확인")),("재장전 포함 DPS",r.SustainedDps?.ToString("N1")??(r.TimingIssues.Length==0?"교전 분석에서 계산":"계산 조건 확인"))})metrics.Children.Add(new Border{Padding=new(0,0,16,0),Child=Stack(Text(name),Text(value,value.Any(char.IsLetter)?16:25))});results.Children.Add(RivenPresentation.Rule(metrics));
            results.Children.Add(Text($"연사력 {r.FireRate:0.##}/초 · 탄창 {r.Magazine:0} · {(RangedHema.Applies(Selected)?"체력으로 재장전":$"예비 탄약 {r.Modifiers!["MaximumReserveAmmo"]:0}")} · 재장전 {r.Reload:0.###}초"));
            if(Read().Enemy is {} target)
            {
                var defense=EnemyScaling.Calculate(target);var hit=RangedEngine.TargetShot(presented,target,Read().Conditions);var factor=r.ExpectedShot>0?hit/r.ExpectedShot:0;
                results.Children.Add(Text($"{(turret?"대상 직접 피해 / 기본 사격":carrier?"대상 폭발 피해 / 구체 1값":"대상 직접 피해 / 발사")}   {hit:N1}",22));results.Children.Add(Text($"체력 {defense.Health:N0} · 실드 {defense.Shields:N0} · 방어력 {defense.Armor:N0} · 오버가드 {defense.Overguard:N0}"));
                results.Children.Add(Text("교전 시작 방어층·상성 고정. 방어층 파괴·상태 변화·TTK는 이 결과에 포함하지 않습니다."));
                if(r.SustainedDps is {} sustained)
                {
                    results.Children.Add(Text($"현재 대상 지속 직접 DPS   {sustained*factor:N1}",22));
                    if(RangedArcanes.Resolve(Read(),Selected!) is {} equipped&&equipped.Cap>1)
                    {
                        results.Children.Add(Text($"{equipped.Name} · 현재 랭크 중첩별 비교",17));
                        var plot=new SeriesPlot();var points=Enumerable.Range(0,equipped.Cap+1).Select(n=>
                        {
                            var state=Read() with{ArcaneStacks=n,Conditions=Read().Conditions! with{ArcaneAge=0}};
                            var v=RangedEngine.Calculate(state);var multiplier=v.ExpectedShot>0?RangedEngine.TargetShot(v,target,state.Conditions)/v.ExpectedShot:0;
                            var dps=(v.SustainedDps??0)*multiplier;
                            return new PlotPoint(n,dps,$"{equipped.Name} {n}중첩 · 대상 지속 직접 DPS {dps:N1}",stacks.SelectedIndex==n);
                        }).ToArray();
                        plot.SetData(points,true,"고정 중첩 · 지속시간 내","대상 지속 직접 DPS",0,equipped.Cap);results.Children.Add(plot);
                    }
                }
            }
            if(presented.Damage.Values.Any(value=>value>0)){var donut=new DamageDonut();donut.SetData(presented.Damage);results.Children.Add(donut);}
            else results.Children.Add(Text("선택한 구성요소에는 직접 피해가 없습니다. 폭발·후속 공격의 피해는 교전 분석에서 확인하세요."));
            var breakdown=Stack(Text($"기본 피해 {presented.BaseDamage:N1} · 가산 피해 ×{presented.BaseFactor:0.###} · 팩션/로어 ×{presented.FactionFactor:0.###} · CO 계수 {presented.CoFactor:0.###}"),Text("CO 가산·곱연산과 감쇠 예외는 공격별 위키 규칙 적용 · 물리/원소 1/32 · 기본 치피 12비트 양자화"));
            foreach(var item in presented.Damage.Where(p=>p.Value!=0))breakdown.Children.Add(Text($"{Labels.Damage(item.Key)}   {item.Value:N2} / 탄환 · 비치명"));
            results.Children.Add(new Expander{Header="피해 계산식 · 속성별 상세",Content=breakdown});
            
            lastValidBuild=CloneBuild(Read());WorkspaceChanged?.Invoke();message.Text=string.Join("\n\n",r.TimingIssues);results.Children.Add(Text("계산 범위",18));foreach(var s in r.Scope)results.Children.Add(Text(s));
            var source=Button("위키 원문",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Selected!.Source){UseShellExecute=true}));results.Children.Add(source);
        }
        catch(ArgumentException e){capacity.Text="수용량 · 입력 확인 필요";message.Text=e.Message;SetAccuracy([],false,true);results.Children.Add(Text("계산할 수 없는 입력",20));results.Children.Add(Text(e.Message));}
    }
    private void RefreshSlots()
    {
        slotsPanel.Children.Clear();for(int i=0;i<8;i++)
        {
            int index=i;var m=slots[i] is {} slot?RangedEngine.ModCatalog.Mods.Single(x=>x.Id==slot.ModId):null;
            if(m is not null&&RangedRivens.IsRiven(m)&&riven is not null)m=RangedRivens.Resolve(m,riven,Selected!.Id,slots[i]!.Rank);
            var b=Button(m is null?$"{i+1}\n＋":m.Name,()=>{active=index;RefreshSlots();});b.Height=ModCardLayout.EquippedHeight;b.Margin=new(0,0,i%4==3?0:12,8);b.Content=new TextBlock{Text=b.Content.ToString(),TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};b.ToolTip=m is null?"모드를 놓거나 보관함에서 선택":KoreanPresentation.ModDescription(m,slots[i]!.Rank);b.BorderThickness=new(1);b.AllowDrop=true;
            b.Drop+=(_,e)=>{if(e.Data.GetData("WFLabs.RangedSlot") is int from)CommitDrop(from,null,index,false);else if(e.Data.GetData("WFLabs.RangedMod") is string id)CommitDrop(null,id,index,false);e.Handled=true;};
            var menu=new ContextMenu();var remove=new MenuItem{Header="해제",IsEnabled=m is not null};remove.Click+=(_,_)=>{slots[index]=null;RefreshConditionControls();Refresh();};menu.Items.Add(remove);
            if(m is not null&&RangedRivens.IsRiven(m)){var edit=new MenuItem{Header="리벤 옵션 편집"};edit.Click+=(_,_)=>{active=index;OpenRiven();};menu.Items.Add(edit);}
            else if(m is not null){var rank=new MenuItem{Header="모드 랭크"};for(int n=0;n<=m.MaxRank;n++){int value=n;var item=new MenuItem{Header=n.ToString(),IsChecked=slots[index]!.Rank==n};item.Click+=(_,_)=>{slots[index]=slots[index]! with {Rank=value};Refresh();};rank.Items.Add(item);}menu.Items.Add(rank);}
            var polarity=new MenuItem{Header="포르마 · 슬롯 극성"};foreach(var p in new[]{"None","Madurai","Vazarin","Naramon","Zenurik"}){var item=new MenuItem{Header=p switch{"Madurai"=>"마두라이","Vazarin"=>"바자린","Naramon"=>"나라몬","Zenurik"=>"제누릭",_=>"극성 없음"},IsChecked=polarities[index]==p};item.Click+=(_,_)=>{polarities[index]=p;Refresh();};polarity.Items.Add(item);}menu.Items.Add(polarity);b.ContextMenu=menu;
            if(m is not null)Decorate(b,m,slots[i]!.Rank,SlotDrain(m,slots[i]!.Rank,polarities[i]));else ModCardLayout.Apply(b,false,ModFinish.Neutral);
            if(active==i)b.SetResourceReference(Control.BorderBrushProperty,"AccentBrush");
            System.Windows.Automation.AutomationProperties.SetName(b,$"슬롯 {i+1} · {m?.Name??"빈 슬롯"}");
            if(m is not null)Drag(b,"WFLabs.RangedSlot",index);slotsPanel.Children.Add(Stack(b));
        }
        RefreshSlotEditor();
    }
    private static int SlotDrain(RangedMod mod,int rank,string polarity)
    {
        var drain=mod.BaseDrain+rank;
        return polarity=="None"||mod.Polarity=="None"?drain:(int)Math.Ceiling(drain*(polarity==mod.Polarity?.5:1.25));
    }
    private void RefreshSlotEditor()
    {
        slotEditor.Children.Clear();slotEditor.ColumnDefinitions.Clear();
        foreach(var width in new[]{new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto,GridLength.Auto})slotEditor.ColumnDefinitions.Add(new(){Width=width});
        var index=active;var slot=slots[index];var mod=slot is null?null:RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId);
        var title=Text($"슬롯 {index+1} · {mod?.Name??"빈 슬롯"}",13);title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextWrapping=TextWrapping.NoWrap;title.VerticalAlignment=VerticalAlignment.Center;title.Margin=new(0,0,12,0);title.ToolTip=title.Text;slotEditor.Children.Add(title);
        var rankControls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,12,0)};
        var rankLabel=Text("랭크",13);rankLabel.Margin=new(0,0,6,0);rankLabel.VerticalAlignment=VerticalAlignment.Center;rankControls.Children.Add(rankLabel);
        var rank=new ComboBox{Width=68,MinHeight=32,ItemsSource=Enumerable.Range(0,(mod?.MaxRank??0)+1).ToArray(),SelectedIndex=slot?.Rank??0,IsEnabled=mod is not null&&!RangedRivens.IsRiven(mod),ToolTip="선택한 모드의 랭크"};
        System.Windows.Automation.AutomationProperties.SetName(rank,"선택한 모드 랭크");rank.SelectionChanged+=(_,_)=>{if(rank.SelectedItem is int value&&slots[index] is {} current){slots[index]=current with{Rank=value};RefreshConditionControls();Refresh();}};rankControls.Children.Add(rank);Grid.SetColumn(rankControls,1);slotEditor.Children.Add(rankControls);
        var polarity=new ComboBox{Width=120,MinHeight=32,ItemsSource=new[]{new ArcaneChoice("None","극성 없음"),new ArcaneChoice("Madurai","마두라이"),new ArcaneChoice("Vazarin","바자린"),new ArcaneChoice("Naramon","나라몬"),new ArcaneChoice("Zenurik","제누릭")},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=polarities[index],ToolTip="포르마 · 선택한 슬롯의 극성",Margin=new(0,0,8,0)};
        System.Windows.Automation.AutomationProperties.SetName(polarity,"선택한 슬롯 극성");polarity.SelectionChanged+=(_,_)=>{if(polarity.SelectedValue is string value){polarities[index]=value;Refresh();}};Grid.SetColumn(polarity,2);slotEditor.Children.Add(polarity);
        var commands=new StackPanel{Orientation=Orientation.Horizontal};
        if(mod is not null&&RangedRivens.IsRiven(mod)){var edit=Button("옵션 편집",OpenRiven);edit.Margin=new(0,0,6,0);commands.Children.Add(edit);}
        var remove=Button("해제",()=>{slots[index]=null;RefreshConditionControls();Refresh();});remove.Margin=new(0);remove.IsEnabled=slot is not null;commands.Children.Add(remove);Grid.SetColumn(commands,3);slotEditor.Children.Add(commands);
    }
    private void RefreshPrimer()
    {
        primerSection.Visibility=encounter.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;
        primerPanel.Children.Clear();
        foreach(var type in Enum.GetValues<DamageType>().Where(DamageTypeRules.HasStatus))
        {
            var n=primer.GetValueOrDefault(type);var button=Button((n>0?"✓ ":"＋ ")+Labels.Damage(type)+(n>0?$" ×{n}":""),()=>
            {
                if(encounter.SelectedIndex!=0){loading=true;encounter.SelectedIndex=0;loading=false;}
                if(primer.ContainsKey(type))primer.Remove(type);else primer[type]=1;RefreshPrimer();Refresh();
            });
            button.ToolTip="클릭: 적용/해제 · 우클릭: 시작 중첩";var menu=new ContextMenu();
            foreach(var count in new[]{1,2,3,4,5,10}.Where(v=>v<=TargetStatusRules.Maximum(type)))
            {var item=new MenuItem{Header=$"{count}중첩",IsChecked=n==count};item.Click+=(_,_)=>{loading=true;encounter.SelectedIndex=0;loading=false;primer[type]=count;RefreshPrimer();Refresh();};menu.Items.Add(item);}
            button.ContextMenu=menu;primerPanel.Children.Add(button);
        }
    }
    private async void StartCombat()
    {
        if(simulation is not null)return;
        if(Selected is {} selected){var firingAttack=RangedAttackSelection.FiringAttack(selected,attack.SelectedIndex);if(firingAttack!=attack.SelectedIndex)attack.SelectedIndex=firingAttack;}
        simulation?.Cancel();var source=new System.Threading.CancellationTokenSource();simulation=source;
        calculateButton.IsEnabled=false;cancelButton.Visibility=Visibility.Visible;lastCombat=null;pinComparison.IsEnabled=false;combatResultHost.Content=null;RefreshComparison();
        try
        {
            var rawBuild=CloneBuild(Capture());var b=RangedApproximations.Resolve(rawBuild);var initial=RangedEngine.Calculate(b);var cadence=initial.Cadence!;var first=encounter.SelectedIndex<2;
            var duration=encounter.SelectedIndex switch{0 or 1=>cadence.Charge+(cadence.BurstCount-1)*cadence.BurstDelay+RangedAttackLinks.FlightTime(Selected!,b.AttackIndex,b,initial)+RangedAttackLinks.LastDelay(b.WeaponId,b.AttackIndex,b.Conditions?.DetonationDelay??0,b.Conditions,b)+(RangedDeployments.Get(b.WeaponId) is {DirectAttack:<0}?b.Conditions?.DetonationDelay??0:0)+1e-6,2=>RangedMagazineCharge.MagazineCycle(Selected!,b,initial),4=>10,_=>5};
            if(first&&Selected is {} firstWeapon&&RangedPassives.Get(firstWeapon.Id)?.Kind=="Grimoire"&&b.AttackIndex==2)duration=45*(1-(b.Conditions?.AlternateCharge??1))+6+1e-6;
            if(!double.IsFinite(duration)||duration<=0)throw new ArgumentException("이 공격의 교전 시간 규칙을 연결해야 합니다.");
            if(first&&RangedPassives.Get(b.WeaponId)?.Kind=="Onos"&&b.AttackIndex==1)duration+=b.Conditions?.ChargeCycleSeconds??0;
            if(first&&RangedFields.Get(b.WeaponId) is {} field&&field.Parents.Contains(b.AttackIndex))duration+=field.SpawnDelay+RangedFields.End(field,b.Conditions??new());
            if(first&&Selected is {} vectis&&RangedVectis.Applies(vectis)&&b.AttackIndex==1)duration+=b.Conditions?.Headshot==true?.4:RangedVectis.BodyTicks(b.Conditions??new()).Max();
            if(first&&Selected is {} stug&&RangedStug.Launch(stug,b.AttackIndex))duration+=1.5+(b.Conditions?.StugBounces??[]).Select(x=>x.AfterImpact).DefaultIfEmpty(0).Max();
            if(first&&Selected is {} simulor&&RangedSimulor.Applies(simulor))duration=Math.Max(duration,1+1e-6);
            if(first&&initial.Modifiers!.GetValueOrDefault("NapalmTicks")>0)duration+=Math.Max(0,initial.Modifiers!["NapalmTicks"]-1);
            if(first)duration+=Math.Max((b.Conditions?.Collisions??[]).Select(x=>x.AfterImpact).DefaultIfEmpty(0).Max(),(b.Conditions?.ClusterLandings??[]).Select(x=>x.AfterImpact).DefaultIfEmpty(0).Max());
            var settings=new RangedEncounter(duration,CombatSamples(),FirstShot:first);SetAccuracy(initial.Scope,false);accuracyStatus.Text=accuracyStatus.Text.Replace("교전 계산 전","계산 중");progressText.Text="교전 계산 중…";message.Text="";
            var progress=new Progress<SimulationProgress>(p=>{if(simulation==source&&!source.IsCancellationRequested)progressText.Text=$"{p.Phase}  {p.Completed} / {p.Total}";});
            var result=await Task.Run(()=>RangedCombat.Simulate(b,settings,source.Token,progress),source.Token);
            if(source.IsCancellationRequested||simulation!=source)return;
            var without=await Task.Run(()=>RangedCombat.Simulate(b with{Conditions=(b.Conditions??new()) with{DisableConditionOverload=true}},settings,source.Token),source.Token);
            if(source.IsCancellationRequested||simulation!=source)return;
            lastCombat=new(rawBuild,result,settings,without);ShowCombat(result,without,settings);SetAccuracy(result.Notes,true);pinComparison.IsEnabled=true;RefreshComparison();resultScroller.ScrollToTop();progressText.Text=$"교전 분석 완료 · {settings.Trials:N0}표본";WorkspaceChanged?.Invoke();
        }
        catch(OperationCanceledException){if(simulation==source){progressText.Text="계산 취소 · 다시 계산할 수 있습니다";accuracyStatus.Text=accuracyStatus.Text.Replace("계산 중","계산 취소 · 결과 없음");}}
        catch(ArgumentException ex){if(simulation==source){progressText.Text="교전 계산에 필요한 조건 확인";message.Text=ex.Message;SetAccuracy([],false,true);}}
        finally{if(simulation==source){simulation=null;calculateButton.IsEnabled=true;cancelButton.Visibility=Visibility.Collapsed;}source.Dispose();}
    }
    private static void AddApproximationSummary(Panel panel,string[] notes)
    {
        if(notes.Length==0)return;
        var title=Text($"근사 포함 · {notes.Length}개 가정",16);title.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
        panel.Children.Add(title);
        panel.Children.Add(new Expander{Header="적용한 값 · 근거 · 결과에 미치는 영향",Content=Stack(notes.Select(n=>(UIElement)Text(n,14)).ToArray()),Margin=new(0,4,0,12)});
    }
    private void ShowCombat(RangedCombatResult result,RangedCombatResult without,RangedEncounter settings)
    {
        var panel=new StackPanel();panel.Children.Add(Text(settings.FirstShot?"첫 발사 · 직접 명중과 연결된 파생 공격":$"{settings.Duration:0.##}초 교전 · 직접 명중과 연결된 파생 공격",20));
        var stats=new UniformGrid{Columns=3};
        foreach(var (label,value) in new[]{(settings.FirstShot?"첫 발사 기대 피해":"교전 평균 DPS",$"{(settings.FirstShot?result.Damage.Mean:result.Dps.Mean):N1}"),("상태 피해 포함 누적",$"{result.Damage.Mean:N1}"),(storedConditions.GroupCount>1?"전원 처치 시간 · 중앙값":"처치 시간 · 중앙값",result.Ttk.Median is {} ttk?$"{ttk:0.###}초":settings.FirstShot?"첫 발 처치 미달":$"> {settings.Duration:0.##}초")})stats.Children.Add(Stack(Text(label),Text(value,25)));
        panel.Children.Add(stats);if(Selected is {} healthWeapon&&(RangedHema.Applies(healthWeapon)||storedConditions.OrbShots is {Length:>0}))panel.Children.Add(Text($"종료 시 체력 {result.Points[^1].PlayerHealth:N1} · 회복·소모 사건 반영"));if((storedConditions.ExternalEvents??[]).Any(e=>e.Kind=="AllyKill"))panel.Children.Add(Text("외부 처치 포함 시나리오 · 해당 피해는 DPS에 합산하지 않으며 처치 시간에는 포함됩니다."));if(result.Points[^1].VeloxStrength>0)panel.Children.Add(Text($"벨록스 · 다음 시전 위력 +{result.Points[^1].VeloxStrength:P1} (표본 평균)"));if(storedConditions.MaxEnergy>0||result.Points[^1].PlayerOverguard>0)panel.Children.Add(Text($"종료 시 에너지 {result.Points[^1].Energy:N1} / {storedConditions.MaxEnergy:N0} · 획득 오버가드 {result.Points[^1].PlayerOverguard:N1}"));var estimate=settings.FirstShot?result.Damage:result.Dps;panel.Children.Add(Text($"{estimate.Samples:N0}표본 · 평균 {(settings.FirstShot?"피해":"DPS")} 95% 통계 오차 약 ±{estimate.Approximate95HalfWidth:N1} · 처치 비율 {result.Ttk.KillFraction:P1}"));
        if(!settings.FirstShot)
        {
            panel.Children.Add(Text("시간별 누적 평균 DPS · CO 비교",17));
            var plot=new RangedDpsPlot();plot.Set(result.Points,without.Points);panel.Children.Add(plot);
            panel.Children.Add(Text("같은 시작 조건에서 CO만 제외해 별도로 계산합니다. 처치 이후에는 피해가 누적되지 않습니다."));
        }
        if(result.Points.Any(p=>p.DischargeStored>0)){panel.Children.Add(Text("바일 디스차지 · 저장 피해 변화",17));var chargePlot=new SeriesPlot();chargePlot.SetData(result.Points.Select(p=>new PlotPoint(p.Time,p.DischargeStored,$"{p.Time:0.00}초 · 저장 피해 {p.DischargeStored:N1}",false)).ToArray(),true,"교전 시간 (초)","저장 피해",0,settings.Duration);panel.Children.Add(chargePlot);}
        var log=Stack(result.Events.Select(e=>(UIElement)Text($"{e.Time:0.000}초  [적 {e.Target+1}] {RangedEventLabels.Detail(e.Detail)}  {e.Value:0.###}",13)).ToArray());
        panel.Children.Add(new Expander{Header="발사·상태·처치 이벤트 · 첫 표본",Content=Scroll(log),MaxHeight=400});
        foreach(var note in result.Notes.Where(n=>!n.StartsWith("근사 · ")))panel.Children.Add(Text(note));
        combatResultHost.Content=RivenPresentation.Rule(panel);
    }
    private void RefreshLibrary()
    {
        if(Selected is not {} w)return;library.Children.Clear();var mods=RangedEngine.ModCatalog.Mods.Where(m=>RangedEngine.Compatible(m,w)&&(m.Name.Contains(query.Text,StringComparison.OrdinalIgnoreCase)||m.EnglishName.Contains(query.Text,StringComparison.OrdinalIgnoreCase)));
        mods=sort.SelectedIndex switch{1=>mods.OrderBy(m=>m.Limitations.Length>0).ThenBy(m=>m.BaseDrain+m.MaxRank),2=>mods.OrderBy(m=>m.Limitations.Length>0).ThenBy(m=>m.Polarity),_=>mods.OrderBy(m=>m.Limitations.Length>0).ThenBy(m=>m.Name)};
        if(descending)mods=mods.Reverse();
        foreach(var m in mods){var b=Button("",()=>{CommitDrop(null,m.Id,active,false);});Decorate(b,m,m.MaxRank,libraryCard:true);b.IsEnabled=m.Limitations.Length==0||RangedRivens.IsRiven(m);ToolTipService.SetShowOnDisabled(b,true);b.ToolTip=m.Name+"\n"+(RangedRivens.IsRiven(m)?"내 리벤의 옵션과 수치 입력":string.Join("\n",new[]{KoreanPresentation.ModDescription(m,m.MaxRank)}.Concat(m.Limitations)));if(b.IsEnabled)Drag(b,"WFLabs.RangedMod",m.Id);library.Children.Add(b);}
        noResults.Visibility=library.Children.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    internal void EquipRiven(RangedRivenRoll roll,int destination)
    {
        RangedRivens.Validate(roll);if(destination is <0 or >7)throw new ArgumentException("리벤 슬롯 범위 오류");
        weapon.SelectedItem=weapon.Items.Cast<Weapon>().Single(w=>w.Id==roll.WeaponId);riven=roll;var mod=RangedRivens.Mod(Selected!);
        for(int i=0;i<slots.Length;i++)if(slots[i]?.ModId==mod.Id)slots[i]=null;
        active=destination;slots[destination]=new(mod.Id,roll.Rank);RefreshConditionControls();Refresh();
    }
    private void OpenRiven()
    {
        if(Selected is not {} selected||rivenOverlay is not null)return;
        var overlay=new Grid{Background=new SolidColorBrush(Color.FromArgb(160,0,0,0))};rivenOverlay=overlay;Grid.SetColumnSpan(overlay,3);Panel.SetZIndex(overlay,100);
        void Close(){Children.Remove(overlay);rivenOverlay=null;}
        var editor=new RangedRivenEditor(selected,riven,roll=>
        {
            riven=roll;var mod=RangedRivens.Mod(selected);
            for(int i=0;i<slots.Length;i++)if(slots[i]?.ModId==mod.Id)slots[i]=null;
            slots[active]=new(mod.Id,roll.Rank);Close();RefreshConditionControls();Refresh();
        },Close,capture:Capture,destination:()=>active){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(12)};
        overlay.SizeChanged+=(_,_)=>{editor.MaxWidth=Math.Max(240,overlay.ActualWidth-24);editor.MaxHeight=Math.Max(240,overlay.ActualHeight-24);};
        overlay.Children.Add(editor);Children.Add(overlay);editor.Focusable=true;editor.Focus();
        overlay.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
    }
    private static void Decorate(Button button,RangedMod mod,int rank,int? drain=null,bool libraryCard=false)
    {
        button.Tag=(mod,rank);var finish=ModAppearance.ForId(mod.Id);button.SetResourceReference(BackgroundProperty,ModAppearance.BackgroundKey(finish));button.SetResourceReference(Control.BorderBrushProperty,ModAppearance.StrokeKey(finish));
        ModCardLayout.Apply(button,libraryCard,finish);
        var cost=drain??(mod.BaseDrain+rank);
        var meta=ModCardLayout.Header(cost.ToString(),mod.Polarity switch{"Madurai"=>"AP_ATTACK","Vazarin"=>"AP_DEFENSE","Naramon"=>"AP_TACTIC",_=>mod.Polarity},cost<mod.BaseDrain+rank?"CapacityGoodBrush":cost>mod.BaseDrain+rank?"ErrorBrush":"AccentBrush");
        button.Content=new ModCardFace(mod.Name,meta,libraryCard);button.ToolTip=mod.Name+"\n"+KoreanPresentation.ModDescription(mod,rank);
    }
    private void Save(){var d=new SaveFileDialog{Title="총기 빌드 저장",Filter="WF Labs 총기 빌드|*.wfgun.json",FileName="총기 빌드.wfgun.json"};if(d.ShowDialog(Window.GetWindow(this))!=true)return;try{AtomicJsonFile.Write(d.FileName,Capture());message.Text="빌드를 저장했습니다.";}catch(Exception e)when(e is ArgumentException or IOException or UnauthorizedAccessException){message.Text="저장 실패: "+e.Message;}}
    private void Load(){var d=new OpenFileDialog{Title="총기 빌드 불러오기",Filter="WF Labs 총기 빌드|*.wfgun.json"};if(d.ShowDialog(Window.GetWindow(this))!=true)return;try{Restore(AtomicJsonFile.Read<RangedBuild>(d.FileName,1024*1024));message.Text="빌드를 불러왔습니다.";}catch(Exception e)when(e is ArgumentException or IOException or JsonException or InvalidOperationException or UnauthorizedAccessException){message.Text="불러오기 실패: "+e.Message;}}
    internal (ReferenceWeapon Weapon,RangedRivenRoll? Riven) RivenContext => (Selected??throw new ArgumentException("모딩할 무기를 선택하세요."),riven);
    private ModSlot?[]? stressSlots;
    internal void StressLongNames(bool enable)
    {
        if(enable){stressSlots=slots.ToArray();var mods=RangedEngine.ModCatalog.Mods.Where(m=>m.Category==RangedEngine.ModCategory(Selected!)&&!RangedRivens.IsRiven(m)).OrderByDescending(m=>m.Name.Length).Take(8).ToArray();for(int i=0;i<mods.Length;i++)slots[i]=new(mods[i].Id,mods[i].MaxRank);}
        else if(stressSlots is {} saved)Array.Copy(saved,slots,8);
        RefreshSlots();
    }
    internal RangedBuild Capture(){var b=Read();RangedEngine.Calculate(b);if(b.Enemy is {} e)EnemyScaling.Calculate(e);return b;}
    internal void PrepareRivenAudit()
    {
        var b=Capture();var roll=new RangedRivenRoll(b.WeaponId,8,[new("Damage",0),new("Multishot",0),new("Zoom",0,true)],RangedRivens.Table.Version,10,"Madurai");
        roll=roll with{Traits=roll.Traits.Select(t=>t with{Value=Math.Round(RangedRivens.Range(roll,t).Minimum/.9,1)}).ToArray()};
        var mod=RangedRivens.Mod(Selected!);b=b with{Slots=new ModSlot?[8],Riven=roll};b.Slots[0]=new(mod.Id,8);Restore(b);active=0;
        var selectedSlot=(StackPanel)slotsPanel.Children[0];var card=(Button)selectedSlot.Children[0];
        card.ContextMenu!.Items.OfType<MenuItem>().Single(i=>i.Header?.ToString()=="리벤 옵션 편집").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var editor=rivenOverlay!.Children.OfType<RangedRivenEditor>().Single();editor.CheckAndApply();
        if(rivenOverlay is not null||!Capture().Riven!.Traits.SequenceEqual(roll.Traits))throw new InvalidOperationException("리벤 편집 후 장착 결과 불일치");
        OpenRiven();
    }
    internal void CloseRivenAudit(){if(rivenOverlay is {} overlay)Children.Remove(overlay);rivenOverlay=null;}
    internal void Restore(RangedBuild b)
    {
        ValidateBuildForRestore(b);
        RangedEngine.Calculate(b);if(b.Enemy is {} target)EnemyScaling.Calculate(target);
        var item=weapon.Items.Cast<Weapon>().SingleOrDefault(w=>w.Id==b.WeaponId)??throw new ArgumentException("다른 무기 구분의 빌드입니다.");weapon.SelectedItem=item;loading=true;
        try {kitgun=b.Kitgun;attack.ItemsSource=RangedEngine.Attacks(RangedEngine.Resolve(b)).Select((a,i)=>$"{i+1}. {KoreanPresentation.Attack(RangedEngine.S(a,"AttackName"))}").ToArray();riven=b.Riven;evolutions=b.Evolutions??[];autoTransform=b.AutoTransform;incarnonAmmo=b.IncarnonAmmo;exilusMod.SelectedItem=exilusMod.Items.Cast<ExilusChoice>().Single(m=>m.Id==(b.Exilus?.ModId??"None"));exilusRank.SelectedIndex=b.Exilus?.Rank??0;exilusPolarity.SelectedValue=b.ExilusPolarity;storedConditions=b.Conditions??new();encounter.SelectedIndex=b.EncounterMode;if(b.Progenitor is {} bonus){progenitor.SelectedItem=progenitor.Items.Cast<ElementChoice>().Single(e=>e.Type==bonus.Type);bonusPercent.Text=(bonus.Fraction*100).ToString();}primer.Clear();foreach(var p in storedConditions.Statuses??[])primer[p.Key]=p.Value;killStacks.SelectedIndex=Math.Clamp(storedConditions.Kills,0,5);RefreshPrimer();Array.Copy(b.Slots,slots,8);Array.Copy(b.Polarities,polarities,8);attack.SelectedIndex=b.AttackIndex;catalyst.IsChecked=b.Catalyst;var savedArcane=b.Arcane=="Merciless"?RangedArcanes.Rules.Single(a=>a.Code=="Merciless"&&a.Category==category).Id:b.Arcane;arcane.SelectedItem=arcane.Items.Cast<ArcaneChoice>().Single(a=>a.Id==savedArcane);arcaneRank.SelectedIndex=b.ArcaneRank;stacks.SelectedIndex=b.ArcaneStacks;distance.Text=b.Distance.ToString();if(b.Enemy is {} e){var def=EnemyCatalog.Load().Get(e.EnemyId);faction.SelectedItem=faction.Items.Cast<EnemyDamageProfile>().Single(f=>f.Id==def.Faction);enemy.SelectedItem=enemy.Items.Cast<EnemyDefinition>().Single(x=>x.Id==def.Id);difficulty.SelectedIndex=(int)e.Difficulty;level.Text=e.Level.ToString();eximus.IsChecked=e.Eximus;}}
        finally{loading=false;}RefreshConditionControls();Refresh();
    }
    private void ValidateBuildForRestore(RangedBuild b)
    {
        if(b.Slots is not {Length:8}||b.Polarities is not {Length:8}||b.EncounterMode is <0 or >4)throw new ArgumentException("슬롯 수 또는 교전 구간 형식이 잘못되었습니다.");
        var selected=RangedEngine.Resolve(b);if(selected.Category!=category)throw new ArgumentException("다른 무기 구분의 빌드입니다.");
        RangedEngine.Calculate(b);if(b.Enemy is {} e)EnemyScaling.Calculate(e);
        if(b.Arcane is not ("None" or "Merciless")&&!RangedArcanes.Rules.Any(a=>a.Id==b.Arcane&&a.Category==category))throw new ArgumentException("장착할 수 없는 아케인입니다.");
        if(b.Exilus is {} ex&&!RangedEngine.ModCatalog.Mods.Any(m=>m.Id==ex.ModId&&m.Exilus&&m.Limitations.Length==0&&RangedEngine.Compatible(m,selected)))throw new ArgumentException("장착할 수 없는 엑실러스입니다.");
        if(b.ExilusPolarity is not ("None" or "Madurai" or "Vazarin" or "Naramon" or "Zenurik"))throw new ArgumentException("엑실러스 극성을 확인하세요.");
    }
    internal void Check()
    {
        if(Selected is null||results.Children.Count<5||library.Children.Count<10)throw new Exception("총기 모딩 화면 구성 실패");
        if(library.ActualWidth<250||ActualHeight<700)throw new Exception("총기 보관함 최소 공간 실패");
        DependencyObject? ancestor=VisualTreeHelper.GetParent(library);while(ancestor is not null&&ancestor is not ScrollViewer)ancestor=VisualTreeHelper.GetParent(ancestor);
        if(ancestor is not ScrollViewer viewport||library.Children.Count<library.Columns*2)throw new Exception("총기 보관함 스크롤 구조 오류");
        var secondRow=(FrameworkElement)library.Children[library.Columns];var bounds=secondRow.TransformToAncestor(viewport).TransformBounds(new Rect(secondRow.RenderSize));
        if(bounds.Bottom>viewport.ActualHeight+.5)throw new Exception("최소 창에서 총기 모드 두 줄이 온전히 보이지 않습니다.");
        var original=Capture();var mod=RangedEngine.ModCatalog.Mods.First(m=>m.EnglishName==(category=="primary"?"Serration":"Hornet Strike"));var equipped=original with{Slots=original.Slots.ToArray(),Arcane="Merciless",ArcaneStacks=4};equipped.Slots[0]=new(mod.Id,mod.MaxRank);Restore(equipped);
        var restored=Capture();if(restored.Slots[0]?.ModId!=mod.Id||restored.ArcaneStacks!=4||RangedEngine.Calculate(restored).ExpectedShot<=RangedEngine.Calculate(original).ExpectedShot)throw new Exception("총기 모딩·복원 실패");Restore(original);UpdateLayout();CheckDragFlow();
    }
    internal void PrepareCombatAudit()
    {
        var b=Capture() with{IncarnonAmmo=3,EncounterMode=3,Conditions=(Capture().Conditions??new()) with{Kills=2,ExternalEvents=[new(.5,"AbilityCast")],Statuses=new(){[DamageType.Electricity]=1}}};
        var coMod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==(category=="primary"?"Galvanized Aptitude":"Galvanized Shot"));b=b with{Slots=b.Slots.ToArray()};b.Slots[5]=new(coMod.Id,coMod.MaxRank);
        Restore(b);var settings=new RangedEncounter(Duration:2,Trials:8);
        var result=RangedCombat.Simulate(b,settings);var without=RangedCombat.Simulate(b with{Conditions=b.Conditions! with{DisableConditionOverload=true}},settings);
        ShowCombat(result,without,settings);results.UpdateLayout();resultScroller.ScrollToTop();scenarioDrawer?.SetOpen(false,false);
    }
    internal void PrepareSceneAudit(string name)
    {
        var scene=name;if(scene.StartsWith("Residual",StringComparison.Ordinal))name="Rattleguts (Primary)";if(scene=="Singularity ally")name="Simulor";if(scene=="Nullifier")name="Miter";
        var w=RangedEngine.Weapons.Single(w=>w.EnglishName==name);var b=Capture();
        var a=name is "Trumna" or "Grimoire" or "Tenet Ferrox"?2:name is "Cedo" or "Alternox" or "Onos"?1:0;
        var c=new RangedConditions(GroupCount:3,GroupSpacing:2,MaxEnergy:300);
        if(name is "Cedo" or "Trumna")c=c with{Collisions=[new(.5,1),new(1,2,Surface:true)],GlaiveReturnSeconds=name=="Cedo"?5:null};
        if(name=="Zarr")c=c with{ClusterLandings=[new(.5,1,Surface:true),new(.8,2)]};
        if(name=="Grimoire")c=c with{OrbAnchored=true};
        if(name=="Cantare")c=c with{ReturningBladeHits=[new(.5)]};
        if(name=="Proboscis Cernos")c=c with{ProboscisTickOne=.5,ProboscisTickTwo=1.2,PullMotion="Linear",PullSeconds=.8};
        if(name=="Scyotid"){a=1;c=c with{PullMotion="Linear",PullSeconds=.5,SnareFlightSeconds=.3};}
        if(name=="Penta")c=c with{ManualGrenades=true,ExternalEvents=[new(2,"AlternateFire")],DetonationDelay=10,PullMotion="Blocked",TetherFlightSeconds=.4};
        if(name=="Secura Penta")c=c with{ManualGrenades=true,AdhesiveNapalmFuse=4,NapalmUtilityMode="Fixed",NapalmExcludePatchDamage=true};
        if(name=="Prisma Gorgon"){a=1;c=c with{ChargeCycleSeconds=.6,ChargeRecoverySeconds=.2};}
        if(name.StartsWith("Vermisplicer"))c=c with{TendrilAcquisitionSeconds=.15,TendrilRoutes=Enumerable.Range(0,5).Select(n=>new RangedTendrilRoute(n%3,AutoTarget:n!=0)).ToArray()};
        if(name=="Sporelacer (Secondary)")c=c with{ClusterLandings=[]};
        if(name=="Lato")c=c with{InfestedOrbs=[1,2],SimulorOrbs=[4],MixedOrbCriticalStacks=false};
        if(name=="Boltor")c=c with{PlayerHealth=100,OrbShots=[new(1)]};
        if(name=="Bolto")c=c with{MaxEnergy=300,CurrentEnergy=30,OrbShots=[new(1)]};
        if(name=="Embolist")c=c with{DischargeStored=10000,DischargeFlightSeconds=.2};
        if(name=="Hikou")c=c with{FirstImpactSurface=true};
        if(name=="Zymos")c=c with{Headshot=true,ZymosBurrowTicks=[],ZymosSporeExplosion=true,ZymosSpores=Enumerable.Range(0,6).Select(_=>new RangedSporePath(0,.1)).ToArray()};
        if(name=="Lanka")c=c with{FlyingTarget=true,VoltageTrapLanding=.1,VoltageTrapTargets=1,VoltageTrapDamage=6,VoltageTrapPhase=0};
        if(name=="Mutalist Quanta")c=c with{InfestedFirstPulse=.5,InfestedStopSeconds=0,InfestedShootThrough=true,ExternalEvents=[new(0,"AlternateFire"),new(1,"AlternateFire")]};
        if(name=="Evensong")c=c with{ThunderboltDirectRule="Replace"};
        if(name=="Hema")c=c with{HemaDrainPhase=.5,HemaPartialRounding="Down",PlayerMaxHealth=720,PlayerHealth=720};
        if(name is "Quanta" or "Quanta Vandal" or "Tenet Quanta"){a=1;c=c with{CubeLifetimeRule="AfterStop",CubeRoutes=[new(2,1,Contacts:[new(.2)],DamagedAfterLaunch:.1)]};}
        if(name=="Dread"){a=1;c=c with{TargetsInLine=true,TargetBodyThickness=.5,PenetrationGapSeconds=.1};}
        if(name=="Battacor"){a=1;c=c with{TargetsInLine=true,SentientBarrageReady=true};}
        if(name=="Kuva Drakgoon")c=c with{FirstImpactSurface=true,Collisions=[new(.2)]};
        if(name=="Tenet Tetra")c=c with{FirstImpactSurface=true,Collisions=[new(.2)],BouncePathConfirmed=true};
        if(name=="Flux Rifle")c=c with{FluxOscillation=1};
        if(name=="Higasa"){a=1;c=c with{HigasaForcedStatus=DamageType.Corrosive};}
        if(name=="EFV-5 Jupiter"){a=1;c=c with{BuckshotReady=true,RefundRounding="Down",BuckshotRefillSource="Created"};}
        if(name=="Pox")c=c with{FieldEndSeconds=4};
        if(name=="Vectis Prime"){a=1;c=c with{VectisBodyTickOne=.8,VectisBodyTickTwo=1.6};}
        if(name=="Vaykor Hek")c=c with{JusticeStatus="StunOnly",PlayerBaseArmor=100,ExternalEvents=[new(.5,"WeaponAffinity",1000)]};
        if(name=="Rakta Ballistica")c=c with{PlayerBaseEnergy=100,ExternalEvents=[new(.5,"WeaponAffinity",1000)]};
        if(name=="Stug")c=c with{StugExplosionRadius=2.8,StugBounces=[new(.5),new(1),new(1.5)]};
        if(name=="Synoid Simulor")c=c with{SimulorArrivalSeconds=.15,SimulorFirstTick=.25};
        if(name=="Grakata")a=0;
        if(name=="Corinth Prime")a=2;
        if(name=="Catabolyst")c=c with{ReloadGrenadePhase=.5,GrenadeFlightSeconds=.5};
        if(name=="Onos")c=c with{ChargeCycleSeconds=4,ChargeStackReset="Persistent"};
        if(name=="Azima"){a=2;c=c with{Azima=new(.2,CenterX:2,UnmoddedLifetime:10,VolleyRate:2,FirstVolley:.2,RotationSpeed:30,HitRadius:.2,ExplosionRadius:2,ExplosionFalloff:.5,InfiniteConsumesMagazine:true)};}
        if(scene.StartsWith("Residual",StringComparison.Ordinal))c=c with{Residual=new(OutputMultiplier:1,TickInterval:1,FirstTick:1,Activations:[1,2],BoilsApproachRadius:2,BoilsFalloff:.9,ViremiaCombines:false,WeaponCredit:false,FixedPacketScenario:true,StatusTick:1)};
        if(name=="Simulor")c=c with{OrdinarySimulor=new(UnmergedLifetime:2,Merge:[new(2,100,4),new(3,130,4),new(4,150,5)],Explosion:[new(2,250,5),new(3,300,6),new(4,400,7)],TickOutput:10,TickRadius:4,TickInterval:.5,FirstTick:.5,VortexMerge:"Separate")};
        if(scene=="Singularity ally")c=c with{SingularityDamageBracket="Additive",SingularityIndependentOrbs=true,SingularityPullRadius=2,SingularityPulses=[],SingularityAllyPosition=0,SingularityAllySpeed=1,SingularityAllyFailedExplosion=false,SingularityLimit=4,PullMotion="None"};
        if(scene=="Nullifier")c=c with{Nullifier=new("Crewman",CollapseRadius:.1,FrameSeconds:1/60d,NoRegrowth:true,RadialBlocked:true)};
        if(name=="Mutalist Quanta")c=c with{OrbInteractions=[new(.5,0,"MeleePush",X:2,TravelSeconds:.5)]};
        if(name=="Penta")c=c with{GrenadeEvents=[new(2,1,"Position",X:0,Y:2)]};
        var augmentName=name switch{"Secura Penta"=>"Napalm Grenades","Lanka"=>"Voltage Sequence","Mutalist Quanta"=>"Bursting Mass","Penta"=>"Tether Grenades","Boltor"=>"Mending Shot","Bolto"=>"Energizing Shot","Embolist"=>"Vile Discharge","Hikou"=>"Concealed Explosives","Evensong"=>"Thunderbolt","Hema"=>"Necrophagic Vigor","Dread"=>"Unseen Dread","Battacor"=>"Sentient Barrage","Kuva Drakgoon"=>"Fomorian Accelerant","Tenet Tetra"=>"Kinetic Ricochet","Flux Rifle"=>"Flux Overdrive","Higasa"=>"Higasa Serration","EFV-5 Jupiter"=>"Overpressured Rounds",_=>null};
        if(scene=="Singularity ally")augmentName="Spontaneous Singularity";if(scene=="Nullifier")augmentName="Neutralizing Justice";
        var sceneSlots=new ModSlot?[8];if(augmentName is not null){var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==augmentName);sceneSlots[0]=new(mod.Id,mod.MaxRank);}
        if(name=="Secura Penta"){var m=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName=="Adhesive Blast");sceneSlots[1]=new(m.Id,m.MaxRank);}
        Restore(b with{WeaponId=w.Id,AttackIndex=a,Kitgun=scene.StartsWith("Residual",StringComparison.Ordinal)?new("Brash","Splat",scene):name=="Rattleguts (Secondary)"?new("Haymaker","Splat","PaxSeeker"):null,Slots=sceneSlots,Exilus=null,Arcane="None",ArcaneStacks=0,Evolutions=[],IncarnonAmmo=null,AutoTransform=false,Riven=null,Conditions=c,Distance=0,Progenitor=null});
        if(name=="Rattleguts (Secondary)"){kitgun=new("Haymaker","Splat","PaxSeeker");storedConditions=storedConditions with{Headshot=true,PaxSeekerPaths=Enumerable.Range(0,4).Select(_=>new RangedSeekerPath(1,.2)).ToArray(),PaxSeekerRetrigger=false};RefreshConditionControls();Refresh();}
        if(name is "Velox" or "Grakata"){var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==(name=="Velox"?"Velox Conclusion":"Wild Frenzy"));slots[0]=new(mod.Id,mod.MaxRank);if(name=="Grakata")attack.SelectedIndex=1;Refresh();RefreshConditionControls();}
        if(name=="Kuva Ogris"){var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName=="Nightwatch Napalm");slots[0]=new(mod.Id,mod.MaxRank);Refresh();}
        var expandedHeaders=new HashSet<string>(StringComparer.Ordinal){"도탄 · 추가 충돌 경로","집속탄 · 자탄 착탄 위치","지속 장판 · 시간 조건","그리모어 · 구체와 게이지","보조 공격 · 충전 게이지","오노스 · 충전 사이클","재장전 유탄 · 투척 시간","칸타레 · 복귀 경로와 합창","구체 배치 · 합체","와일드 프렌지 · 연사와 환급","스터그 · 부착과 중첩","벡티스 인카논 · 부착과 약점 폭발","신디케이트 · Justice","신디케이트 · Blight","헤마 · 체력과 재장전","플럭스 오버드라이브 · 변동항","히가사 서레이션 · 강제 상태","오버프레셔드 라운즈 · 다음 벅샷","콴타 · 큐브와 연쇄 기폭","감염 구체 · 이동·합체·저장","볼티지 시퀀스 · 비행 적 처치","자이모스 · 약점과 유도 포자","테더 그레네이즈 · 부착과 기폭","펜타 · 유탄 배치와 기폭","네이팜 그레네이즈 · 직격·폭발·장판","외부 감염 구체 · 탄환 통과","외부 시뮬러 구체 · 탄환 통과","충전 시간 · 원문 미기재","애드히시브 블래스트 · 부착 신관","팍스 시커 · 유도탄 경로","버미스플라이서 · 촉수 조준","아지마 · 회전 터렛","레지듀얼 · 생성과 범위 피해","일반 시뮬러 · 구체와 특이점","스폰테이니어스 싱귤래리티","널리파이어 보호막","감염 구체 · 외부 사격과 이동","펜타 · 이동 중 기폭·부착·환경 접촉"};
        foreach(var section in conditionPanel.Children.OfType<Expander>())section.IsExpanded=section.Header?.ToString()?.StartsWith("적 무리")==true&&c.TargetsInLine||expandedHeaders.Contains(section.Header?.ToString()??"")&&(section.Header?.ToString()!="널리파이어 보호막"||scene=="Nullifier");
        UpdateLayout();
        var preferred=scene.StartsWith("Residual",StringComparison.Ordinal)?"레지듀얼 · 생성과 범위 피해":scene switch{"Azima"=>"아지마 · 회전 터렛","Simulor"=>"일반 시뮬러 · 구체와 특이점","Singularity ally"=>"스폰테이니어스 싱귤래리티","Nullifier"=>"널리파이어 보호막","Mutalist Quanta"=>"감염 구체 · 외부 사격과 이동","Penta"=>"펜타 · 이동 중 기폭·부착·환경 접촉",_=>""};
        var focusedSection=conditionPanel.Children.OfType<Expander>().FirstOrDefault(x=>x.Header?.ToString()==preferred)??conditionPanel.Children.OfType<Expander>().FirstOrDefault(x=>x.IsExpanded);
        scenarioDrawer?.SetOpen(true,false);UpdateLayout();if(focusedSection is not null)conditionScroller.ScrollToVerticalOffset(focusedSection.TranslatePoint(new Point(0,0),(UIElement)conditionScroller.Content).Y);else conditionScroller.ScrollToTop();
        RangedEngine.Calculate(Capture());
    }
    internal void PrepareMechanicsAudit(bool details)
    {
        var name=category=="primary"?"Soma Prime":"Laetum";
        var w=RangedEngine.Weapons.Single(w=>w.EnglishName==name);
        var mods=(category=="primary"?new[]{"Serration","Split Chamber","Hata-Satya","Cryo Rounds","Infected Clip"}:new[]{"Hornet Strike","Barrel Diffusion","Lethal Torrent","Pistol Pestilence","Frostbite"}).Select(n=>RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==n)).ToArray();
        var equipped=new ModSlot?[8];for(int i=0;i<mods.Length;i++)equipped[i]=new(mods[i].Id,mods[i].MaxRank);
        var incarnon=RangedIncarnons.Get(w.Id)!;
        var perks=incarnon.Perks.GroupBy(p=>p.Stage).Select(g=>g.First().Id).ToArray();
        Restore(Capture() with{WeaponId=w.Id,AttackIndex=incarnon.Forms[0],Slots=equipped,Evolutions=perks,IncarnonAmmo=3,AutoTransform=true});
        foreach(var expander in conditionPanel.Children.OfType<Expander>())expander.IsExpanded=true;
        UpdateLayout();
        scenarioDrawer?.SetOpen(true,false);UpdateLayout();if(details)conditionScroller.ScrollToBottom();else conditionScroller.ScrollToTop();
        if(Capture().IncarnonAmmo!=3||Capture().Evolutions?.Length!=perks.Length)throw new Exception("인카논 설정 복원 실패");
    }
}
