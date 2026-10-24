using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private FrameworkElement? scenarioPanel;
    private ScrollViewer? scenarioScroller;
    private readonly TextBlock coState=T("",14),selectedStatusText=T("상태 없음",14),resultConditions=T("",14);
    private readonly WrapPanel statusChips=new();
    private readonly Dictionary<DamageType,Button> statusButtons=[];
    private readonly CheckBox liftedTarget=new(){Content="적 공중에 띄움"};
    private readonly CheckBox knockedTarget=new(){Content="적 넉다운 상태"},microwaveTarget=new(){Content="누코어 마이크로웨이브"};
    private readonly ComboBox comboChoice=new();
    private Button weaponStatusPreset=null!,clearStatusButton=null!;
    private Expander? targetStatusExpander;
    private bool updatingCombat;

    private FrameworkElement CreateCombatPanel()
    {
        TextBlock ColumnTitle(string text){var title=T(text,16);title.Margin=new(0,0,0,8);return title;}
        var body=new StackPanel();body.Children.Add(CreateTargetMeleeView());primaryDebuffs=CreateDebuffEditor();body.Children.Add(ColumnTitle("공격과 교전"));body.Children.Add(CreateEncounterChoice());
        attackMode.Width=double.NaN;attackMode.Margin=new(0);body.Children.Add(Field("공격 방식",attackMode));
        comboChoice.DisplayMemberPath="Name";comboChoice.SelectedValuePath="Value";CompactInput(comboChoice,32);
        comboChoice.SelectionChanged+=(_,_)=>{if(!updatingCombat&&comboChoice.SelectedValue is int n)combo.Text=n.ToString();};
        AutomationProperties.SetName(comboChoice,"교전 시작 콤보 배수");comboChoice.ToolTip="교전 시작 시점의 콤보 배수. 예열 계산에서는 명중에 따라 콤보가 누적됩니다.";WatchEquationSource(comboChoice,"condition",()=> ("combo","시작 콤보 배수"));
        stanceCombo.Margin=new(0);stanceActionField=(FrameworkElement)Field("스탠스 동작",stanceCombo);singleAttackField=(FrameworkElement)Field("공격 동작",meleeAttack);body.Children.Add(stanceActionField);body.Children.Add(singleAttackField);
        body.Children.Add(Field("공격 시작 시 콤보 배수",comboChoice));
        var buffsHeading=ColumnTitle("버프 · 추가 조건");body.Children.Add(buffsHeading);body.Children.Add(CreateWeaponBuffPanel());
        body.Children.Add(CreateAttackSequence());

        weaponStatusPreset=Button("모든 속성 적용",ApplyWeaponStatuses);clearStatusButton=Button("상태 없음",ClearTargetStatuses);
        weaponStatusPreset.ToolTip="최대치 비교용: 현재 무기가 부여할 수 있는 모든 상태를 동시에 1중첩씩 가정합니다. 실전 예열 결과가 아닙니다.";
        var presetRow=new WrapPanel{Margin=new(0,6,0,6)};
        foreach(var button in new[]{weaponStatusPreset,clearStatusButton}){button.Padding=new(10,5,10,5);button.Margin=new(0,0,6,0);presetRow.Children.Add(button);}
        var primer=Stack(SectionTitle("프라이머 · 미리 걸어 둔 상태"),selectedStatusText,presetRow,statusChips);primerControls=primer;body.Children.Insert(3,primer);
        coState.Margin=new(0,8,0,8);primer.Children.Add(coState);WatchEquationSource(coState,"condition",()=> ("status","대상 상태이상"));
        liftedTarget.Checked+=(_,_)=>{if(!updatingCombat){preservedConditions["Lifted"]=1;preservedConditions["Knockdown"]=0;Recalculate();}};
        liftedTarget.Unchecked+=(_,_)=>{if(!updatingCombat){preservedConditions["Lifted"]=0;Recalculate();}};
        knockedTarget.Checked+=(_,_)=>{if(!updatingCombat){preservedConditions["Knockdown"]=1;preservedConditions["Lifted"]=0;Recalculate();}};
        knockedTarget.Unchecked+=(_,_)=>{if(!updatingCombat){preservedConditions["Knockdown"]=0;Recalculate();}};
        microwaveTarget.Checked+=(_,_)=>{if(!updatingCombat){preservedConditions["Microwave"]=1;Recalculate();}};
        microwaveTarget.Unchecked+=(_,_)=>{if(!updatingCombat){preservedConditions["Microwave"]=0;Recalculate();}};
        var stackRows=new UniformGrid{Columns=2};
        foreach(var (type,input) in targetStatuses)
        {
            var field=Field(Labels.Damage(type),new ContentControl{Content=Stepper(input,0,TargetStatusRules.Maximum(type),1,Labels.Damage(type)+" 중첩")});
            ((FrameworkElement)field).Margin=new(0,0,8,0);stackRows.Children.Add(field);
        }
        targetStatusExpander=new Expander{Header="상태별 중첩 · 추가 조건",Content=Stack(stackRows,liftedTarget,knockedTarget,microwaveTarget,frozenTarget,Field("외부 상태 종류 수 (기존 빌드)",status))};
        status.ToolTip="기존 빌드와의 호환용 수동 종류 수입니다. 선택한 상태 종류 수와 합산하지 않고 더 큰 수를 사용합니다.";
        primer.Children.Add(targetStatusExpander);body.Children.Add(Spacer(10));
        body.Children.Add(ResultCard(CreateMeleeView(),0));body.Children.Add(ResultCard(CreateArcaneView(),0));
        body.Children.Add(CreateCombatScene());body.Children.Add(primaryDebuffs.View);
        body.Children.Add(CreateExternalEquipmentPanel());
        body.Children.Add(CreateContagionPanel());
        body.Children.Add(CreateEpidemicPanel());
        body.Children.Add(CreateIchorPanel());
        body.Children.Add(CreateCaustacystPanel());
        body.Children.Add(CreateOkinaPanel());
        body.Children.Add(CreateCeramicPanel());
        body.Children.Add(CreateZenistarPanel());
        body.Children.Add(CreateOrviusPanel());
        body.Children.Add(CreateGlaivePanel());
        body.Children.Add(CreateBladePanel());
        body.Children.Add(CreateInnodemPanel());body.Children.Add(CreateDestrezaPanel());
        body.Children.Add(CreateWeaponActionPanel());
        body.Children.Add(CreateShieldFlightPanel());
        body.Children.Add(CreateVitricaPanel());
        body.Children.Add(CreateExaltedWavePanel());
        var advanced=Columns(Field("타격 배율",motion),Field("기본 간격 (초)",interval));
        body.Children.Add(new Expander{Header="고급 · 직접 계산 조건",Content=Stack(advanced,Field("위키 미기재 콤보 시간 (초)",comboDurationOverride),Field("적 기본 방어력 · 섀터링 임팩트",enemyBaseArmor))});
        body.Children.Add(CreateCombatTimeInputs());
        var targetColumn=new StackPanel();var attackColumn=new StackPanel();var detailColumn=new StackPanel();
        var items=body.Children.Cast<UIElement>().ToArray();body.Children.Clear();var inDetails=false;
        for(var i=0;i<items.Length;i++){if(items[i]==buffsHeading)inDetails=true;(i==0?targetColumn:inDetails?detailColumn:attackColumn).Children.Add(items[i]);}
        scenarioScroller=Scroll(detailColumn);var panel=ScenarioDrawer.Columns(Scroll(targetColumn),Scroll(attackColumn),scenarioScroller);
        scenarioPanel=panel;RefreshEncounterChoice();return panel;
    }
    private record ComboLevel(int Value){public string Name=>Value==1?"1× · 콤보 없음":$"{Value}× · 콤보 누적 상태";}
    private void RefreshCombatState(Build build,Calculation c,StanceCalculation? stanceResult)
    {
        scenarioDrawer?.SetSummary($"{(enemyChoice.SelectedItem as EnemyChoice)?.Name??"대상"} · Lv. {enemyLevel.Text}{(enemyEximus.IsChecked==true?" · 엑시무스":"")} · {selectedEnemyDifficulty switch{EnemyDifficulty.SteelPath=>"강철의 길",EnemyDifficulty.Icebind=>"빙주령",_=>"일반"}} · {(encounterChoice.SelectedItem as EncounterChoice)?.Name}");
        updatingCombat=true;
        try
        {
            var max=catalog.Weapons.Single(w=>w.Id==build.WeaponId).MaxCombo;
            if(comboChoice.Items.Count!=max)comboChoice.ItemsSource=Enumerable.Range(1,max).Select(n=>new ComboLevel(n)).ToArray();
            comboChoice.SelectedValue=build.Scenario.Combo;
            liftedTarget.IsChecked=build.Melee?.Conditions?.GetValueOrDefault("Lifted")>0;
            knockedTarget.IsChecked=build.Melee?.Conditions?.GetValueOrDefault("Knockdown")>0;
            microwaveTarget.IsChecked=build.Melee?.Conditions?.GetValueOrDefault("Microwave")>0;
            var state=build.Melee?.Statuses??[];var count=TargetStatusRules.Count(build);
            var selected=state.Where(p=>p.Value>0).Select(p=>$"{Labels.Damage(p.Key)} ×{p.Value}").ToList();
            if(liftedTarget.IsChecked==true)selected.Add("공중에 띄움");
            if(knockedTarget.IsChecked==true)selected.Add("넉다운");
            if(microwaveTarget.IsChecked==true)selected.Add("마이크로웨이브");
            if(build.Arcanes?.Frozen==true&&!state.ContainsKey(DamageType.Cold))selected.Add("동결");
            if(build.Scenario.ExistingStatusTypes>0)selected.Add($"수동 {build.Scenario.ExistingStatusTypes}종");
            selectedStatusText.Text=selected.Count>0?string.Join(" · ",selected):"상태 없음";
            foreach(var (type,button) in statusButtons)
            {
                var n=state.GetValueOrDefault(type);var label=button.Content as TextBlock??T("",14);label.Margin=new(0);label.Text=(n>0?"✓ ":"+ ")+Labels.Damage(type)+(n>1?$" {n}":"");label.Foreground=ChartPalette.Damage(type);button.Content=label;
                button.SetResourceReference(Control.BackgroundProperty,n>0?"AccentSurfaceBrush":"ControlBrush");
                button.BorderBrush=n>0?ChartPalette.Damage(type):(Brush)FindResource("StrokeBrush");
                AutomationProperties.SetName(button,$"{Labels.Damage(type)} {n}중첩 · "+(n>0?"해제":"적용"));
            }
            var coSlots=build.Slots.Select((s,i)=>(s,i)).Where(p=>p.s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==p.s.ModId).Effects?.ContainsKey("ConditionOverload")==true).ToArray();
            coState.Visibility=coSlots.Length==0?Visibility.Collapsed:Visibility.Visible;
            if(coSlots.Length>0)
            {
                var without=(ModSlot?[])build.Slots.Clone();foreach(var (_,i) in coSlots)without[i]=null;
                var baseline=EvaluateBuild(build with {Slots=without});var baseDps=baseline.Stance?.TargetDps??baseline.Stats.TargetDps;var dps=stanceResult?.TargetDps??c.TargetDps;
                var delta=baseDps>0?dps/baseDps-1:0;
                coState.Text=count==0?"컨오 미발동 · 대상 상태 없음":delta>1e-8?$"컨오 적용 · {count}종 · DPS +{delta:P1}":"컨오 적용 제외 · 선택한 공격";
                coState.SetResourceReference(TextBlock.ForegroundProperty,delta>1e-8?"CapacityGoodBrush":"AccentBrush");
                coState.ToolTip="같은 대상·상태·스탠스에서 컨디션 오버로드를 장착/해제한 직접 DPS 차이입니다. 상태 종류당 모드 수치는 기본 피해 브라켓에 적용하며 공격별 예외를 따릅니다.";
            }
            resultConditions.Text=$"고정 상태 기준 · 콤보 {c.Melee?.EffectiveCombo??build.Scenario.Combo:0}× · 상태 {count}종"+(coSlots.Length>0?"\n"+coState.Text:"");
            weaponStatusPreset.IsEnabled=TargetStatusRules.WeaponPreset(c,stanceResult).Length>0;
            RefreshAttackVisibility();
            if(contagionPanel is not null)contagionPanel.Visibility=ContagionEquipped?Visibility.Visible:Visibility.Collapsed;
            if(epidemicPanel is not null)epidemicPanel.Visibility=EpidemicEquipped?Visibility.Visible:Visibility.Collapsed;
            if(wavePanel is not null)wavePanel.Visibility=ExaltedWaveFlight.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
            if(glassPanel is not null)glassPanel.Visibility=build.WeaponId==VitricaGlassState.WeaponId?Visibility.Visible:Visibility.Collapsed;
            RefreshWeaponActionVisibility(build.WeaponId);
            if(shieldPanel is not null)shieldPanel.Visibility=build.WeaponId==ShieldThrowMechanic.Sigma?Visibility.Visible:Visibility.Collapsed;
            if(rapierPanel is not null)rapierPanel.Visibility=RapierSelected?Visibility.Visible:Visibility.Collapsed;
            if(innodemPanel is not null)innodemPanel.Visibility=build.WeaponId==InnodemFlight.WeaponId?Visibility.Visible:Visibility.Collapsed;
            if(bladePanel is not null)bladePanel.Visibility=IncarnonBladeFlight.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
            if(glaivePanel is not null)glaivePanel.Visibility=GlaiveFlightState.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
            if(hoverPanel is not null)hoverPanel.Visibility=build.WeaponId==OrviusHoverState.WeaponId?Visibility.Visible:Visibility.Collapsed;
            if(discPanel is not null)discPanel.Visibility=build.WeaponId==ZenistarDiscState.WeaponId?Visibility.Visible:Visibility.Collapsed;
            if(ceramicPanel is not null)ceramicPanel.Visibility=build.WeaponId==CeramicDaggerMechanic.WeaponId?Visibility.Visible:Visibility.Collapsed;
            if(okinaPanel is not null)okinaPanel.Visibility=OkinaDaggerState.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
            if(caustPoolPanel is not null)caustPoolPanel.Visibility=CaustacystMechanic.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
            if(ichorPanel is not null)ichorPanel.Visibility=DualIchorCloudMechanic.Applies(build.WeaponId)?Visibility.Visible:Visibility.Collapsed;
        }
        finally{updatingCombat=false;}
    }
    private void ApplyWeaponStatuses()
    {
        if(current is null)return;
        var types=TargetStatusRules.WeaponPreset(current,currentStance);updatingCombat=true;changingMelee=true;
        try{foreach(var (type,input) in targetStatuses)input.Text=types.Contains(type)?"1":"0";status.Text="0";frozenTarget.IsChecked=false;foreach(var key in new[]{"Lifted","Knockdown","Microwave"})preservedConditions[key]=0;}
        finally{changingMelee=false;updatingCombat=false;}
        Recalculate();
    }
    private void ClearTargetStatuses()
    {
        updatingCombat=true;changingMelee=true;
        try{foreach(var input in targetStatuses.Values)input.Text="0";status.Text="0";frozenTarget.IsChecked=false;foreach(var key in new[]{"Lifted","Knockdown","Microwave"})preservedConditions[key]=0;}
        finally{changingMelee=false;updatingCombat=false;}
        Recalculate();
    }
    private FrameworkElement Stepper(TextBox input,double minimum,double maximum,double step,string label)
    {
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new(30)});row.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new(30)});
        void Change(double delta)
        {var old=double.TryParse(input.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:minimum;input.Text=Math.Clamp(old+delta,minimum,maximum).ToString(CultureInfo.InvariantCulture);}
        var minus=Button("−",()=>Change(-step));var plus=Button("+",()=>Change(step));
        foreach(var b in new[]{minus,plus}){b.Padding=new(0);b.Height=32;}
        AutomationProperties.SetName(minus,label+" 줄이기");AutomationProperties.SetName(plus,label+" 늘리기");AutomationProperties.SetName(input,label);
        CompactInput(input,32);input.TextAlignment=TextAlignment.Center;input.Margin=new(3,0,3,0);
        row.Children.Add(minus);Grid.SetColumn(input,1);row.Children.Add(input);Grid.SetColumn(plus,2);row.Children.Add(plus);
        return row;
    }
}
