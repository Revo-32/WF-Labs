using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private record EnemyChoice(EnemyDefinition? Enemy){public string Name=>Enemy?.Name??"직접 입력";}
    private readonly ComboBox enemyChoice=new(){DisplayMemberPath="Name"};
    private readonly TextBox enemyLevel=new(){Text="200"};
    private readonly CheckBox enemyEximus=new(){Content="엑시무스",Margin=new(0,0,0,10)};
    private readonly ComboBox enemySquad=new(){ItemsSource=Enumerable.Range(1,6).ToArray(),SelectedItem=6};
    private readonly Dictionary<EnemyDifficulty,RadioButton> enemyDifficulties=[];
    private readonly TextBlock enemyStats=T("",14),enemyModeNote=T("",14,"#596B85"),enemyLimit=T("",14,"#596B85");
    private FrameworkElement? enemyLevelRow,enemySquadRow,manualEnemyFields;
    private Button? enemyWikiButton;
    private bool updatingEnemy;
    private EnemyDifficulty selectedEnemyDifficulty;
    private FrameworkElement CreateEnemyView()
    {
        var panel=new StackPanel();
        factionChoice.DisplayMemberPath="Name";factionChoice.SelectedValuePath="Id";
        factionChoice.ItemsSource=MeleeRuleCatalog.Load().EnemyProfiles.Select(p=>EnemyCatalog.Load().Enemies.Any(e=>e.Faction==p.Id)?p:p with {Name=p.Name+" · 직접 입력"}).ToArray();
        foreach(var c in new[]{factionChoice,enemyChoice,enemySquad})CompactInput(c,34);
        factionChoice.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("Text"){Source=factionChoice});
        var targetTitle=T("대상",16);targetTitle.Margin=new(0,0,0,8);panel.Children.Add(targetTitle);
        panel.Children.Add(Columns(Field("팩션",factionChoice),Field("적",enemyChoice),1,1.45));
        enemyLevelRow=(FrameworkElement)Field("적 레벨 · 게임에 표시되는 레벨",new ContentControl{Content=Stepper(enemyLevel,1,9999,1,"적 레벨")});panel.Children.Add(enemyLevelRow);
        panel.Children.Add(enemyEximus);AutomationProperties.SetName(enemyEximus,"엑시무스 · 체력·실드·오버가드 자동 계산");
        var modes=new UniformGrid{Columns=3,Margin=new(0,0,0,8)};
        foreach(var (id,label) in new[]{(EnemyDifficulty.Normal,"일반"),(EnemyDifficulty.SteelPath,"강철의 길"),(EnemyDifficulty.Icebind,"빙주령")})
        {
            var button=new RadioButton{Content=label,GroupName="EnemyDifficulty",Style=(Style)FindResource("DifficultyChoice"),Height=34,Margin=new(0,0,4,0)};
            AutomationProperties.SetName(button,"대상 난이도 · "+label);enemyDifficulties[id]=button;modes.Children.Add(button);
            button.Checked+=(_,_)=>
            {
                if(updatingEnemy)return;selectedEnemyDifficulty=id;
                if(id==EnemyDifficulty.Icebind&&int.TryParse(enemyLevel.Text,out var level)&&level<999)enemyLevel.Text="999";
                RefreshEnemyTarget();
            };
        }
        panel.Children.Add(modes);
        enemySquadRow=(FrameworkElement)Field("빙주령 분대 인원",enemySquad);panel.Children.Add(enemySquadRow);
        enemyModeNote.TextWrapping=TextWrapping.Wrap;enemyStats.TextWrapping=TextWrapping.Wrap;enemyLimit.TextWrapping=TextWrapping.Wrap;
        panel.Children.Add(enemyModeNote);panel.Children.Add(enemyStats);panel.Children.Add(enemyLimit);
        var fields=new UniformGrid{Columns=2};
        foreach(var (label,input) in new[]{("체력",health),("방어력",armor),("실드",targetShields),("오버가드",targetOverguard)})
        {CompactInput(input,34);var f=(FrameworkElement)Field(label,input);f.Margin=new(0,0,6,0);fields.Children.Add(f);}
        manualEnemyFields=new Expander{Header="체력·방어력 직접 입력",Content=fields,IsExpanded=true};panel.Children.Add(manualEnemyFields);
        var details=T("몸통 기준입니다. 엑시무스는 전용 체력·실드와 레벨별 오버가드를 계산합니다. 엑시무스 종류별 오라, 보스 고유 저항과 빙주령 특수 결속은 제외합니다. 선택한 무기·공격의 방어층 전이와 시간별 상태 계산 범위는 결과에서 확인할 수 있습니다.",14,"#596B85");details.TextWrapping=TextWrapping.Wrap;
        var sourceButton=Button("선택한 적 · 위키",()=>{if((enemyChoice.SelectedItem as EnemyChoice)?.Enemy is {} e)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.WikiUrl){UseShellExecute=true});});sourceButton.HorizontalAlignment=HorizontalAlignment.Left;enemyWikiButton=sourceButton;
        panel.Children.Add(new Expander{Header="대상 정보 · 출처와 계산 범위",Content=Stack(details,sourceButton)});
        factionChoice.SelectionChanged+=(_,_)=>{if(!updatingEnemy&&!changingMelee&&!initializing){RefreshEnemyChoices(null);RefreshEnemyTarget();}};
        enemyChoice.SelectionChanged+=(_,_)=>{if(!updatingEnemy&&!initializing)RefreshEnemyTarget();};
        enemyLevel.TextChanged+=(_,_)=>{if(!updatingEnemy&&!initializing)RefreshEnemyTarget();};
        enemySquad.SelectionChanged+=(_,_)=>{if(!updatingEnemy&&!initializing)RefreshEnemyTarget();};
        enemyEximus.Checked+=(_,_)=>{if(!updatingEnemy&&!initializing)RefreshEnemyTarget();};enemyEximus.Unchecked+=(_,_)=>{if(!updatingEnemy&&!initializing)RefreshEnemyTarget();};
        foreach(var input in new[]{targetShields,targetOverguard})input.TextChanged+=(_,_)=>Recalculate();
        WatchEquationSource(factionChoice,"condition",()=> ("faction","팩션 · 약점·저항"));WatchEquationSource(enemyChoice,"condition",()=> ("armor","대상 방어력"));
        WatchEquationSource(enemyStats,"condition",()=> ("armor","대상 방어력"));WatchEquationSource(targetShields,"condition",()=> ("shields","대상 실드"));WatchEquationSource(targetOverguard,"condition",()=> ("overguard","대상 오버가드"));
        updatingEnemy=true;factionChoice.SelectedValue="Grineer";updatingEnemy=false;
        RefreshEnemyChoices(EnemyCatalog.Load().Enemies.Single(e=>e.EnglishName=="Heavy Gunner").Id);enemyDifficulties[EnemyDifficulty.Normal].IsChecked=true;RefreshEnemyTarget();
        return panel;
    }
    private static FrameworkElement SectionTitle(string text)
    {
        var title=T(text,16);title.Margin=new(0,10,0,8);
        var border=new Border{Child=title,BorderThickness=new(0,1,0,0),Margin=new(0,8,0,0)};border.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");return border;
    }
    private void RefreshEnemyChoices(string? selectedId,bool manual=false)
    {
        updatingEnemy=true;
        try
        {
            var options=EnemyCatalog.Load().Enemies.Where(e=>e.Faction==(string?)factionChoice.SelectedValue).Select(e=>new EnemyChoice(e)).Append(new EnemyChoice(null)).ToArray();
            enemyChoice.ItemsSource=options;enemyChoice.SelectedItem=manual?options[^1]:selectedId is null?options[0]:options.FirstOrDefault(e=>e.Enemy?.Id==selectedId)??options[0];
        }
        finally{updatingEnemy=false;}
    }
    private EnemySelection? ReadEnemySelection()
    {
        if((enemyChoice.SelectedItem as EnemyChoice)?.Enemy is not {} e)return null;
        return new(e.Id,Integer(enemyLevel,"적 레벨"),selectedEnemyDifficulty,enemySquad.SelectedItem is int squad?squad:6,Eximus:enemyEximus.IsChecked==true);
    }
    private void RefreshEnemyTarget()
    {
        var automatic=(enemyChoice.SelectedItem as EnemyChoice)?.Enemy is not null;
        enemyEximus.IsEnabled=(enemyChoice.SelectedItem as EnemyChoice)?.Enemy?.SupportsEximus==true;
        enemyEximus.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
        enemyEximus.ToolTip=enemyEximus.IsEnabled?"전용 체력·실드 보정 및 레벨에 따른 오버가드":"이 적의 엑시무스 기본 수치가 연결되지 않았습니다.";
        if(!enemyEximus.IsEnabled&&enemyEximus.IsChecked==true){updatingEnemy=true;enemyEximus.IsChecked=false;updatingEnemy=false;}
        if(enemyWikiButton is not null)enemyWikiButton.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
        enemyLevelRow!.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
        manualEnemyFields!.Visibility=automatic?Visibility.Collapsed:Visibility.Visible;
        foreach(var button in enemyDifficulties.Values)button.IsEnabled=automatic;
        var iceAvailable=(enemyChoice.SelectedItem as EnemyChoice)?.Enemy?.Faction is "Grineer" or "KuvaGrineer" or "Corpus" or "CorpusAmalgam" or "Infested" or "InfestedDeimos" or "Orokin" or "TheMurmur";
        enemyDifficulties[EnemyDifficulty.Icebind].IsEnabled=automatic&&iceAvailable;
        enemyDifficulties[EnemyDifficulty.Icebind].ToolTip=iceAvailable?"빙주령 · 분대 인원에 따른 적 강화":"이 팩션은 빙주령 등장 팩션이 아닙니다.";
        if(automatic&&!iceAvailable&&selectedEnemyDifficulty==EnemyDifficulty.Icebind){updatingEnemy=true;selectedEnemyDifficulty=EnemyDifficulty.Normal;enemyDifficulties[EnemyDifficulty.Normal].IsChecked=true;updatingEnemy=false;}
        enemyBaseArmor.IsReadOnly=automatic;enemyBaseArmor.IsEnabled=!automatic;
        enemySquadRow!.Visibility=automatic&&selectedEnemyDifficulty==EnemyDifficulty.Icebind?Visibility.Visible:Visibility.Collapsed;
        enemyStats.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;enemyLimit.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
        enemyModeNote.Text=automatic?selectedEnemyDifficulty switch{EnemyDifficulty.Normal=>"일반 난이도 · 레벨에 따라 자동 계산",EnemyDifficulty.SteelPath=>"체력·실드 ×2.5 · 입력한 레벨 그대로 계산",_=>$"체력·실드·방어력 +{200+100*(enemySquad.SelectedItem is int s?s:6)}%"}:"직접 입력한 수치 사용 · 난이도 자동 보정 없음";
        enemyModeNote.Visibility=automatic&&selectedEnemyDifficulty==EnemyDifficulty.Normal?Visibility.Collapsed:Visibility.Visible;
        try
        {
            if(ReadEnemySelection() is {} selection)
            {
                var e=EnemyCatalog.Load().Get(selection.EnemyId);var stats=EnemyScaling.Calculate(selection);
                var before=initializing;initializing=true;updatingEnemy=true;
                try{health.Text=stats.Health.ToString("R",CultureInfo.InvariantCulture);armor.Text=stats.Armor.ToString("R",CultureInfo.InvariantCulture);targetShields.Text=stats.Shields.ToString("R",CultureInfo.InvariantCulture);targetOverguard.Text=stats.Overguard.ToString("R",CultureInfo.InvariantCulture);enemyBaseArmor.Text=stats.BaseArmor>0?stats.BaseArmor.ToString("R",CultureInfo.InvariantCulture):"";}
                finally{updatingEnemy=false;initializing=before;}
                enemyStats.Text=$"체력 {stats.Health:N0}   실드 {stats.Shields:N0}\n방어력 {stats.Armor:N0} · 피해 감소 {DamageEngine.EnemyArmorReduction(stats.Armor):P0}";
                if(selection.Eximus)enemyStats.Text+=$"\n오버가드 {stats.Overguard:N0}";
                enemyStats.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");enemyChoice.ToolTip=e.Name+" · "+e.EnglishName;
                enemyStats.ToolTip=e.EnglishName+"\n"+string.Join("\n",stats.Notes);
                enemyLimit.Text=selection.Difficulty==EnemyDifficulty.Icebind?"방어력 상한 2,700 · 위키 계산 코드 확인\n특수 결속 효과 제외":"";
                enemyLimit.Visibility=enemyLimit.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
            }
        }
        catch(ArgumentException ex){enemyStats.Text=ex.Message;enemyStats.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");enemyLimit.Visibility=Visibility.Collapsed;}
        Recalculate();
    }
    private void ApplyEnemySelection(EnemySelection? selection)
    {
        if(selection is not null)
        {
            var enemy=EnemyCatalog.Load().Get(selection.EnemyId);EnemyScaling.Calculate(selection);
            updatingEnemy=true;factionChoice.SelectedValue=enemy.Faction;enemyLevel.Text=selection.Level.ToString();enemySquad.SelectedItem=selection.SquadSize;enemyEximus.IsChecked=selection.Eximus;selectedEnemyDifficulty=selection.Difficulty;enemyDifficulties[selection.Difficulty].IsChecked=true;updatingEnemy=false;
            RefreshEnemyChoices(selection.EnemyId);
        }
        else {selectedEnemyDifficulty=EnemyDifficulty.Normal;updatingEnemy=true;enemyDifficulties[EnemyDifficulty.Normal].IsChecked=true;updatingEnemy=false;RefreshEnemyChoices(null,true);}
        RefreshEnemyTarget();
    }
}
