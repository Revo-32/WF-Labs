using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private record AttackChoice(string Id,string Name);
    private record AbilityAugmentChoice(ExaltedAugmentDefinition? Definition){public string Name=>Definition?.Name??"개조 모드 없음";}
    private readonly ComboBox abilityAugmentChoice=new(){DisplayMemberPath="Name"},abilityAugmentRank=new(){ItemsSource=new[]{0,1,2,3},SelectedIndex=3},chromaticElementChoice=new(){ItemsSource=new[]{"화염","냉기","전기","독성"},SelectedIndex=0};
    private static readonly DamageType[] ChromaticElements=[DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin];
    private readonly TextBox abilityDurationInput=new(){Text="100"};
    private UIElement? chromaticField,abilityAugmentRankField;
    private record ExilusChoice(Mod? Mod){public string Name=>Mod?.Name??"장착 안 함";}
    private record EvolutionChoice(IncarnonPerk? Perk){public string Name=>Perk?.Name??"선택 안 함";}
    private readonly ComboBox meleeAttack=new(){DisplayMemberPath="Name"},factionChoice=new(),bonusElementChoice=new(),exilusChoice=new(){DisplayMemberPath="Name"},exilusRank=new(),exilusPolarity=new();
    private readonly CheckBox tennokaiChoice=new(){Content="텐노카이 강공격"},incarnonChoice=new(){Content="시작할 때 인카논 변신 중"};
    private readonly TextBox incarnonRemaining=new(){ToolTip="비워 두면 이 무기의 전체 변신 시간부터 시작합니다. 교전 중 만료되면 일반 형태로 돌아갑니다."};
    private FrameworkElement? incarnonTimeField,exaltedFields;
    private readonly TextBox abilityStrengthInput=new(){Text="100"},abilityRangeInput=new(){Text="100"};
    private readonly ComboBox abilityRankInput=new(){ItemsSource=new[]{0,1,2,3},SelectedIndex=3};
    private readonly TextBox bonusPercent=new(){Text="25"},targetShields=new(){Text="0"},targetOverguard=new(){Text="0"},attackDistance=new(){Text="0"},targetOrder=new(){Text="1"},comboDurationOverride=new(){Text=""};
    private readonly StackPanel evolutionFields=new(),dynamicConditions=new();
    private readonly Dictionary<int,ComboBox> evolutionChoices=[];
    private readonly Dictionary<string,TextBox> meleeConditions=[];
    private readonly Dictionary<DamageType,TextBox> targetStatuses=[];
    private readonly TextBlock meleeSummary=T("",12),meleeDetails=T("",13),weaponMechanics=T("",13);
    private readonly StackPanel statusDamageRows=new(),secondaryRows=new();
    private Expander? secondaryExpander;
    private string? meleeWeaponId;
    private bool changingMelee;
    private Dictionary<string,double> preservedConditions=[];
    private FrameworkElement? lichFields,stanceActionField,singleAttackField;
    private Expander? dynamicExpander,weaponEffectsExpander;
    private FrameworkElement CreateValenceFields()
    {
        var row=new DockPanel{Margin=new(0,4,0,4),Visibility=Visibility.Collapsed};
        var label=T("베일런스",14);label.Margin=new(0,0,10,0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
        var unit=T("%",14);unit.Margin=new(6,0,0,0);unit.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(unit,Dock.Right);row.Children.Add(unit);
        CompactInput(bonusPercent,32);bonusPercent.Width=80;bonusPercent.ToolTip="게임에 표시된 보너스 피해 25–60%";DockPanel.SetDock(bonusPercent,Dock.Right);row.Children.Add(bonusPercent);
        CompactInput(bonusElementChoice,32);bonusElementChoice.Margin=new(0,0,8,0);bonusElementChoice.ToolTip="보너스 원소 · 없음은 비교용";row.Children.Add(bonusElementChoice);return row;
    }
    private FrameworkElement CreateMeleeView()
    {
        var panel=new StackPanel();
        exaltedFields=Stack(T("익절티드 어빌리티",15),Field("어빌리티 랭크",abilityRankInput),Columns(Field("어빌리티 위력 (%)",abilityStrengthInput),Field("어빌리티 거리 (%)",abilityRangeInput)),Field("어빌리티 지속 (%)",abilityDurationInput),Field("워프레임 개조 모드",abilityAugmentChoice),abilityAugmentRankField=Field("개조 모드 랭크",abilityAugmentRank),chromaticField=Field("프레임 발광 원소",chromaticElementChoice));
        panel.Children.Add(exaltedFields);
        abilityAugmentChoice.SelectionChanged+=(_,_)=>{RefreshAbilityAugmentFields();if(!changingMelee){RefreshMeleeConditions();Recalculate();}};
        abilityAugmentRank.SelectionChanged+=(_,_)=>{if(!changingMelee)Recalculate();};chromaticElementChoice.SelectionChanged+=(_,_)=>{if(!changingMelee)Recalculate();};
        abilityStrengthInput.ToolTip="섀도우 클로는 섀도우 소환 당시 세바고스의 위력을 사용합니다. 섀도우 자체의 위력 모드는 클로 기본 피해를 올리지 않습니다.";
        abilityRankInput.SelectionChanged+=(_,_)=>{if(!changingMelee)Recalculate();};
        panel.Children.Add(tennokaiChoice);
        panel.Children.Add(incarnonChoice);incarnonTimeField=(FrameworkElement)Field("시작 인카논 잔여 시간 (초)",incarnonRemaining);panel.Children.Add(incarnonTimeField);panel.Children.Add(evolutionFields);
        WatchEquationSource(tennokaiChoice,"condition",()=> ("tennokai","텐노카이"));
        var geometry=Columns(Field("거리 (m)",attackDistance),Field("관통 대상 순서",new ContentControl{Content=Stepper(targetOrder,1,100,1,"관통 대상 순서")}));panel.Children.Add(new Expander{Header="거리 · 관통",Content=geometry});
        comboDurationOverride.ToolTip="위키에 시간이 없는 동작에만 사용합니다. 빈칸이면 시간을 추정하지 않습니다.";
        lichFields=CreateValenceFields();
        bonusElementChoice.ItemsSource=new[]{"없음","충격","화염","냉기","전기","독성","자성","방사능"};bonusElementChoice.SelectedIndex=0;
        exilusChoice.ItemsSource=new[]{new ExilusChoice(null)}.Concat(catalog.Mods.Where(m=>ReferenceCatalog.Load().Mods.Single(r=>r.Id==m.Id).Raw.TryGetProperty("IsExilus",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.True).Select(m=>new ExilusChoice(m))).ToArray();exilusChoice.SelectedIndex=0;
        WatchEquationSource(exilusChoice,"mod",()=> (exilusChoice.SelectedItem as ExilusChoice)?.Mod is {} m?(m.Id,m.Name):null);
        exilusRank.ItemsSource=new[]{0};exilusRank.SelectedIndex=0;exilusPolarity.ItemsSource=CapacityEngine.OrdinaryPolarities.Select(id=>new PolarityChoice(id,ModBoard.PolarityLabel(id))).ToArray();exilusPolarity.DisplayMemberPath="Name";exilusPolarity.SelectedValuePath="Id";exilusPolarity.SelectedIndex=0;
        dynamicExpander=new Expander{Header="발동 조건",Content=dynamicConditions,IsExpanded=true};panel.Children.Add(dynamicExpander);
        panel.Children.Add(new Expander{Header="무기 고유 효과 · 위키 원문",Content=weaponMechanics});
        void Changed(){if(changingMelee)return;RefreshAttackVisibility();RefreshEquipmentSlots();RefreshMeleeConditions();Recalculate();}
        meleeAttack.SelectionChanged+=(_,_)=>Changed();incarnonChoice.Checked+=(_,_)=>Changed();incarnonChoice.Unchecked+=(_,_)=>Changed();tennokaiChoice.Checked+=(_,_)=>Changed();tennokaiChoice.Unchecked+=(_,_)=>Changed();
        bonusElementChoice.SelectionChanged+=(_,_)=>Changed();exilusPolarity.SelectionChanged+=(_,_)=>Changed();exilusRank.SelectionChanged+=(_,_)=>Changed();
        exilusChoice.SelectionChanged+=(_,_)=>{var m=(exilusChoice.SelectedItem as ExilusChoice)?.Mod;exilusRank.ItemsSource=Enumerable.Range(0,(m?.MaxRank??0)+1);exilusRank.SelectedIndex=m?.MaxRank??0;Changed();};
        foreach(var box in new[]{bonusPercent,attackDistance,targetOrder,comboDurationOverride,incarnonRemaining,abilityStrengthInput,abilityRangeInput,abilityDurationInput})box.TextChanged+=(_,_)=>{if(!changingMelee)Recalculate();};
        board.Changed+=RefreshMeleeConditions;weapon.SelectionChanged+=(_,_)=>RefreshMeleeWeapon();RefreshMeleeWeapon();
        return weaponEffectsExpander=new Expander{Header="무기 고유 효과 · 발동 조건",Content=panel,IsExpanded=false};
    }
    private FrameworkElement CreateTargetMeleeView()
    {
        var panel=CreateEnemyView();
        foreach(var type in Enum.GetValues<DamageType>().Where(DamageTypeRules.HasStatus))
        {
            var input=new TextBox{Text="0"};targetStatuses[type]=input;input.TextChanged+=(_,_)=>{if(!changingMelee)Recalculate();};
            var button=Button("+ "+Labels.Damage(type),()=>input.Text=Integer(input,"중첩")>0?"0":"1");button.Padding=new(8,5,8,5);button.Margin=new(0,0,5,5);button.FontSize=14;
            button.Foreground=ChartPalette.Damage(type);button.ToolTip=Labels.Damage(type)+" 1중첩 적용 / 해제";statusButtons[type]=button;statusChips.Children.Add(button);
        }
        return panel;
    }
    private void RefreshAbilityAugmentFields()
    {
        var d=(abilityAugmentChoice.SelectedItem as AbilityAugmentChoice)?.Definition;
        if(chromaticField is not null)chromaticField.Visibility=d?.Id==ExaltedAugments.Chromatic?Visibility.Visible:Visibility.Collapsed;
        if(abilityAugmentRankField is not null)abilityAugmentRankField.Visibility=d is null?Visibility.Collapsed:Visibility.Visible;
    }
    private void RefreshAttackVisibility()
    {
        if(meleeWeaponId is null)return;
        if(exaltedFields is not null)exaltedFields.Visibility=ExaltedRules.Find(meleeWeaponId) is not null?Visibility.Visible:Visibility.Collapsed;
        var single=attackMode.SelectedIndex==1;meleeAttack.Visibility=single?Visibility.Visible:Visibility.Collapsed;stanceCombo.Visibility=single?Visibility.Collapsed:Visibility.Visible;
        if(stanceActionField is not null)stanceActionField.Visibility=single?Visibility.Collapsed:Visibility.Visible;
        if(singleAttackField is not null)singleAttackField.Visibility=single?Visibility.Visible:Visibility.Collapsed;
        tennokaiChoice.Visibility=SelectedAttackAllowsTennokai()?Visibility.Visible:Visibility.Collapsed;
        tennokaiChoice.Content=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?"텐노카이 강공격":"첫 강공격을 텐노카이로 시작";
        tennokaiChoice.ToolTip="시간 교전에서는 시작 1회만 적용합니다. 이후 기회는 실제 명중으로 생성되며 전투 중 행동에서 사용할 시각을 지정할 수 있습니다.";
        incarnonChoice.Visibility=incarnonChoice.IsEnabled?Visibility.Visible:Visibility.Collapsed;
        if(incarnonTimeField is not null)incarnonTimeField.Visibility=incarnonChoice.IsEnabled&&incarnonChoice.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
        if(lichFields is not null)lichFields.Visibility=bonusElementChoice.IsEnabled?Visibility.Visible:Visibility.Collapsed;
    }
    private bool SelectedAttackAllowsTennokai()
    {
        if(attackMode.SelectedIndex==0)return (stanceCombo.SelectedItem as StanceCombo)?.ActionKind=="Heavy";
        if(meleeWeaponId is null||meleeAttack.SelectedItem is not AttackChoice choice)return false;
        var policy=MeleeEngine.AttackPolicy(meleeWeaponId,choice.Id);
        return policy.Heavy&&policy.Tennokai;
    }
    private void RefreshMeleeWeapon()
    {
        if(weapon.SelectedItem is not Weapon w||w.Id==meleeWeaponId)return;
        changingMelee=true;
        try
        {
            meleeWeaponId=w.Id;board.SetWeapon(w.Id);abilityRankInput.SelectedIndex=3;abilityStrengthInput.Text="100";abilityRangeInput.Text="100";abilityDurationInput.Text="100";preservedConditions.Clear();meleeConditions.Clear();evolutionChoices.Clear();evolutionFields.Children.Clear();
            var exalted=ExaltedRules.Find(w.Id);abilityAugmentChoice.ItemsSource=new[]{new AbilityAugmentChoice(null)}.Concat(ExaltedAugments.All.Where(a=>a.Family==exalted?.Family).Select(a=>new AbilityAugmentChoice(a))).ToArray();abilityAugmentChoice.SelectedIndex=0;abilityAugmentRank.SelectedIndex=3;chromaticElementChoice.SelectedIndex=0;RefreshAbilityAugmentFields();if(exalted is not null){catalyst.IsChecked=true;stanceEquipped.IsChecked=exalted.Stance is not null;capacityStancePolarity.SelectedValue="AP_POWER";}
            catalyst.IsEnabled=exalted is null;stanceEquipped.IsEnabled=exalted is null;capacityStancePolarity.IsEnabled=exalted is null;
            if(exalted is {Exilus:false})exilusChoice.SelectedIndex=0;
            if(!ZawCatalog.IsZaw(w.Id)&&!restoringBuild)selectedExodia=null;
            meleeAttack.ItemsSource=MeleeEngine.Attacks(w.Id).Select(a=>new AttackChoice(a,AttackName(a))).ToArray();meleeAttack.SelectedIndex=0;
            RefreshAttackSequence(w.Id);
            var profile=MeleeRuleCatalog.Load().Weapon(w.Id);weaponMechanics.Text=profile.Notes+"\n\n"+profile.Source?.Url;
            bonusElementChoice.IsEnabled=profile.Raw.TryGetProperty("IsLichWeapon",out var lich)&&lich.ValueKind==System.Text.Json.JsonValueKind.True;bonusPercent.IsEnabled=bonusElementChoice.IsEnabled;bonusElementChoice.SelectedIndex=0;
            var definition=IncarnonEngine.ForWeapon(w.Id);incarnonChoice.IsEnabled=definition is not null;incarnonChoice.IsChecked=false;incarnonRemaining.Text="";
            if(definition is not null)foreach(var group in definition.Perks.GroupBy(p=>p.Stage))
            {
                var choice=new ComboBox{DisplayMemberPath="Name",ItemsSource=new[]{new EvolutionChoice(null)}.Concat(group.Select(p=>new EvolutionChoice(p))).ToArray(),SelectedIndex=0};
                evolutionChoices[group.Key]=choice;evolutionFields.Children.Add(Field("진화 "+group.Key,choice));
                WatchEquationSource(choice,"evolution",()=>(choice.SelectedItem as EvolutionChoice)?.Perk is {} p?(p.Id,p.Name):null);
                choice.SelectionChanged+=(_,_)=>{if(changingMelee)return;RefreshMeleeConditions();Recalculate();};
            }
        }
        finally{changingMelee=false;}
        RefreshMeleeConditions();RefreshArcaneConditions();
        if(!initializing)Recalculate();
    }
    private void RefreshMeleeConditions()
    {
        if(changingMelee||weapon.SelectedItem is not Weapon w)return;
        foreach(var (key,input) in meleeConditions)if(double.TryParse(input.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value))preservedConditions[key]=value;
        var fields=new Dictionary<string,(string Label,double Max)>();
        foreach(var passive in MeleeRuleCatalog.Load().Weapon(w.Id).Passives.Where(p=>p.Key!="Always"))fields[passive.Key]=(passive.Label,passive.Maximum);
        foreach(var slot in board.Slots.Where(s=>s is not null).Append((exilusChoice.SelectedItem as ExilusChoice)?.Mod is {} ex?new(ex.Id,ex.MaxRank):null).Where(s=>s is not null))
        {
            var rule=MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==slot!.ModId);
            if(rule.Condition is {} condition)fields[condition]=(ConditionName(condition),rule.Cap);
            if(rule.Effects?.ContainsKey("StoredDamageFraction")==true)fields["BlockedDamage"]=("흡수 전 막은 피해",100000);
        }
        foreach(var selected in evolutionChoices.Values.Select(c=>(c.SelectedItem as EvolutionChoice)?.Perk).Where(p=>p?.Condition is not null))fields[selected!.Condition!]=(ConditionName(selected.Condition!),selected.Cap);
        if(ShieldThrowMechanic.IsCobra(w.Id))fields["Baruuk"]=("바루크 / 바루크 프라임 사용 · 첫타 수면",1);
        fields["Airborne"]=("플레이어 공중 상태",1);fields["ExternalGladiator"]=("다른 장비의 글래디에이터 모드",3);
        if(w.Id==SanctiMagistarState.WeaponId)fields["SanctiHealingCombo"]=("이전 강공격 콤보 · 0은 흡혈 미발동",12);
        if(TatsuSoulState.Applies(w.Id))
        {
            fields["Signature"]=("레버넌트 사용 · 소울9개 상한",1);
            fields["SoulCharges"]=("교전 시작 소울 저장량",9);
            fields["SoulRemaining"]=("저장 소울 잔여 시간 (초)",120);
            if(!preservedConditions.ContainsKey("SoulRemaining"))preservedConditions["SoulRemaining"]=120;
        }
        if(w.Id==VinquibusState.WeaponId){fields["VinquibusPrecision"]=("교전 시작 정밀 충전 · 강화할 근접 공격 수",16);fields["VinquibusFerocity"]=("교전 시작 맹렬 충전 · 탄약을 아낄 발사 수",16);}
        if(ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Talons){fields["ValkyrRage"]=("교전 시작 발키르 분노 (%)",300);fields["ValkyrRageGrace"]=("분노 감소까지 남은 시간 (초)",5);if(!preservedConditions.ContainsKey("ValkyrRageGrace"))preservedConditions["ValkyrRageGrace"]=5;}
        var abilityAugment=(abilityAugmentChoice.SelectedItem as AbilityAugmentChoice)?.Definition;
        if(abilityAugment?.Id==ExaltedAugments.Primal){fields["PrimalRage"]=("시작 프라이멀 레이지 치확 보너스 (%)",150);fields["PrimalRageGrace"]=("프라이멀 감소까지 남은 시간 (초)",5);if(!preservedConditions.ContainsKey("PrimalRageGrace"))preservedConditions["PrimalRageGrace"]=5;}
        if(abilityAugment?.Id==ExaltedAugments.Accumulating){fields["AccumulatingWhipclaw"]=("시작 윕클로 누적 피해 보너스 (%)",350);fields["WhipclawGrace"]=("윕클로 감소까지 남은 시간 (초)",10);if(!preservedConditions.ContainsKey("WhipclawGrace"))preservedConditions["WhipclawGrace"]=10;}
        if(abilityAugment?.Id==ExaltedAugments.Rubble)fields["RubbleHeap"]=("러블 1400 초과 · 러블 힙 발동",1);
        if(abilityAugment?.Id==ExaltedAugments.Enraged)fields["EnragedElapsed"]=("현재 히스테리아 경과 시간 (초)",1500);
        if(ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Landslide&&SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer){fields["AbilityComboSequence"]=("랜드슬라이드 연속 시전 순서",3);if(!preservedConditions.ContainsKey("AbilityComboSequence"))preservedConditions["AbilityComboSequence"]=1;}
        fields["UniversalStatusDuration"]=("외부 버프 · 상태 지속시간 추가 (%)",1000);
        fields["Invisible"]=("워프레임 투명 상태 유지",1);
        fields["Blinded"]=("외부 효과로 적 실명 유지",1);
        if(preservedConditions.GetValueOrDefault("Invisible")>0)fields["StealthCooldown"]=("시작 시 은신 피해 대기 (초)",3);
        if(MeleeRuleCatalog.Load().Weapon(w.Id).Attacks.Any(a=>a.HeavyAnimationOnly||a.HeavyDamageAnimationOnly))fields["HeavyAnimation"]=(ConditionName("HeavyAnimation"),1);
        var mods=board.Slots.Where(s=>s is not null).Select(s=>MeleeRuleCatalog.Load().Mods.Single(r=>r.Id==s!.ModId)).ToArray();
        if(mods.Any(m=>m.Effects?.ContainsKey("EnergyChannel")==true))fields["EnergySpent"]=(ConditionName("EnergySpent"),100);
        if(mods.Any(m=>m.Effects?.ContainsKey("KillHealthExplosion")==true||m.Effects?.ContainsKey("GroundFinisherHealthExplosion")==true))fields["KilledTargetHealth"]=("처치 대상 최대 체력",100000);
        foreach(var arcane in arcaneEditors.Select(e=>(e.Choice.SelectedItem as ArcaneChoice)?.Definition).Where(a=>a is not null))
        {
            var arcaneCode=MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==arcane!.Id).Code;
            if(arcaneCode=="Truculence")fields["TruculenceProgress"]=("트루큘런스 · 시작 시 누적 획득량",2999);
            if(arcaneCode=="Animosity")fields["AnimosityStacks"]=("교전 시작 애니모시티 중첩",10);
            if(arcaneCode=="HotShot")fields["HotShotStacks"]=(ConditionName("HotShotStacks"),50);
            if(arcaneCode=="Demulcent")foreach(var type in ResidualElementState.Elements)
            {
                fields["Residual"+type]=("시작 레지듀얼 "+Labels.Damage(type)+" 중첩",15);
                fields["Residual"+type+"Remaining"]=("시작 "+Labels.Damage(type)+" 중첩 잔여 (초)",20);
                if(!preservedConditions.ContainsKey("Residual"+type+"Remaining"))preservedConditions["Residual"+type+"Remaining"]=20;
            }
        }
        var code=MeleeRuleCatalog.Load().Weapon(w.Id).Code;
        if(code=="Amanata"){fields["Roll"]=("시작 주사위 · 0은 무작위",6);fields["AmanataHits"]=("시작 시 누적 명중 · 30회마다 재추첨",29);}
        if(code=="AckBrunt")foreach(var type in new[]{DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin})fields["Block"+type]=("시작 흡수 충전 · "+Labels.Damage(type),4);
        var inc=IncarnonEngine.ForWeapon(w.Id);
        var autoImpale=inc?.Code=="Ruvox"&&SelectedEncounter is not (EncounterMode.FirstHit or EncounterMode.Primer);
        if(autoImpale){fields.Remove("Impaled");fields.Remove("ImpaledFive");}
        if(!string.IsNullOrWhiteSpace(playerArmor.Text))fields.Remove("Armor450");
        var autoProton=mods.Any(m=>m.Condition=="ProtonSnap")&&SelectedEncounter is not (EncounterMode.FirstHit or EncounterMode.Primer);
        if(autoProton)fields.Remove("ProtonSnap");
        var autoKillBuffs=SelectedEncounter is not (EncounterMode.FirstHit or EncounterMode.Primer);
        var hasKillBuffs=autoKillBuffs&&(fields.ContainsKey("PennantKills")||fields.ContainsKey("BerserkerStacks")||fields.ContainsKey("GalvanizedStacks"));
        if(autoKillBuffs)foreach(var perk in IncarnonCombatPerks.ForSelections(w.Id,evolutionChoices.Values.Select(c=>(c.SelectedItem as EvolutionChoice)?.Perk?.Id).Where(id=>id is not null).Cast<string>()))fields.Remove(perk.Condition);
        if(autoKillBuffs){foreach(var key in new[]{"RuvoxKills","AirborneKills","CriticalStacks","ColdKills","ThalysStatus"})fields.Remove(key);fields.Remove("PennantKills");fields.Remove("BerserkerStacks");fields.Remove("GalvanizedStacks");if(inc?.Code=="Destreza")fields.Remove("PunctureStacks");}
        if(inc?.Code=="Furax"&&autoKillBuffs)fields.Remove("NearbyEnemies");
        if(autoKillBuffs){fields.Remove("SpringStacks");fields.Remove("HotShotStacks");}
        if(autoKillBuffs&&fields.Remove("TruthFlame"))fields["StatusImmune"]=("내 워프레임 상태 면역 · 트루스 화염 방지",1);
        if(inc?.Code=="Anku"&&!autoKillBuffs)fields["SlideHitBuff"]=("슬라이드 명중 버프",1);
        if(inc?.Code=="Destreza")fields["IncarnonHeavyKills"]=("시작 변신의 강공격 처치 누적",30);
        changingMelee=true;
        try
        {
            dynamicConditions.Children.Clear();meleeConditions.Clear();var grid=new StackPanel();
            foreach(var (key,field) in fields)
            {
                var input=new TextBox{Text=preservedConditions.GetValueOrDefault(key).ToString(CultureInfo.InvariantCulture)};meleeConditions[key]=input;
                input.TextChanged+=(_,_)=>{if(!changingMelee){if(key=="Invisible")RefreshMeleeConditions();Recalculate();}};
                if(field.Max==1)
                {var toggle=new CheckBox{Content=field.Label,IsChecked=preservedConditions.GetValueOrDefault(key)>0,Margin=new(0,3,0,7)};toggle.Checked+=(_,_)=>input.Text="1";toggle.Unchecked+=(_,_)=>input.Text="0";grid.Children.Add(toggle);}
                else
                {var label=key=="ConsecutiveThrows"&&autoKillBuffs?"교전 시작 연속 투척 중첩":key=="PrimaryKills"&&autoKillBuffs?"교전 시작 전 주무기 처치 중첩":key=="FinisherStacks"&&autoKillBuffs?"교전 시작 전 마무리 처치 누적":field.Label;var row=Field(label,new ContentControl{Content=Stepper(input,key=="UniversalStatusDuration"?-100:0,field.Max,1,label)});grid.Children.Add(row);}
            }
            dynamicConditions.Children.Add(grid);
            if(autoImpale)dynamicConditions.Children.Add(T("꿰뚫기와 지속시간은 변신 강 슬램 명중 후 자동 계산합니다.",13));
            if(autoProton)dynamicConditions.Children.Add(T("프로톤 스냅은 전투 중 행동의 ‘벽 매달리기 2초 완료’ 시점부터 적용합니다.",13));
            if(hasKillBuffs)dynamicConditions.Children.Add(T("처치 버프는 교전 중 발동하고 지속시간에 따라 만료됩니다. 다른 적 처치는 ‘전투 중 행동’에서 추가할 수 있습니다.",13));
        }
        finally{changingMelee=false;}
    }
    private MeleeSetup ReadMelee()
    {
        var conditions=new Dictionary<string,double>(preservedConditions);foreach(var (key,input) in meleeConditions)conditions[key]=Number(input,ConditionName(key));
        var statuses=targetStatuses.ToDictionary(p=>p.Key,p=>Integer(p.Value,Labels.Damage(p.Key))).Where(p=>p.Value!=0).ToDictionary();
        DamageType? bonus=bonusElementChoice.SelectedIndex switch{1=>DamageType.Impact,2=>DamageType.Heat,3=>DamageType.Cold,4=>DamageType.Electricity,5=>DamageType.Toxin,6=>DamageType.Magnetic,7=>DamageType.Radiation,_=>null};
        var ex=(exilusChoice.SelectedItem as ExilusChoice)?.Mod;
        var isHeavy=SelectedAttackAllowsTennokai();
        return new((meleeAttack.SelectedItem as AttackChoice)?.Id??"Normal Attack",isHeavy&&tennokaiChoice.IsChecked==true,bonus,bonus is null?0:Number(bonusPercent,"추가 피해")/100,conditions,factionChoice.SelectedValue as string??"Neutral",statuses,Number(attackDistance,"거리"),Integer(targetOrder,"타격 순서"),Number(targetShields,"실드"),Number(targetOverguard,"오버가드"),incarnonChoice.IsChecked==true,evolutionChoices.Values.Select(c=>(c.SelectedItem as EvolutionChoice)?.Perk?.Id).Where(id=>id is not null).Cast<string>().ToArray(),ex is null?null:new(ex.Id,exilusRank.SelectedItem is int rank?rank:ex.MaxRank),Exodia:selectedExodia,ContagionBuffs:ReadContagionBuffs(),ExternalEquipment:ReadExternalEquipment(),Buffs:ReadWeaponBuffs(),AbilityStrength:Number(abilityStrengthInput,"어빌리티 위력")/100,AbilityRank:abilityRankInput.SelectedItem is int abilityRank?abilityRank:3,AbilityRange:Number(abilityRangeInput,"어빌리티 거리")/100,AbilityDuration:Number(abilityDurationInput,"어빌리티 지속")/100,AbilityAugment:(abilityAugmentChoice.SelectedItem as AbilityAugmentChoice)?.Definition is {} augment?new(augment.Id,(int)abilityAugmentRank.SelectedItem):null,ChromaticElement:(abilityAugmentChoice.SelectedItem as AbilityAugmentChoice)?.Definition?.Id==ExaltedAugments.Chromatic?ChromaticElements[chromaticElementChoice.SelectedIndex]:null,Debuffs:ReadDebuffs(primaryDebuffs),ComboDurationOverride:string.IsNullOrWhiteSpace(comboDurationOverride.Text)?null:Number(comboDurationOverride,"동작 시간"),ExilusPolarity:exilusPolarity.SelectedValue as string??"None",IncarnonSecondsRemaining:incarnonChoice.IsChecked==true&&!string.IsNullOrWhiteSpace(incarnonRemaining.Text)?Number(incarnonRemaining,"인카논 잔여 시간"):null);
    }
    private void ApplyMelee(MeleeSetup? setup)
    {
        setup??=new();selectedExodia=setup.Exodia;RefreshMeleeWeapon();changingMelee=true;
        try
        {
            ApplyPrimaryDebuffs(setup.Debuffs);ApplyContagionBuffs(setup.ContagionBuffs);ApplyExternalEquipment(setup.ExternalEquipment);ApplyWeaponBuffs(setup.Buffs);
            meleeAttack.SelectedItem=meleeAttack.Items.Cast<AttackChoice>().Single(a=>a.Id==setup.Attack);tennokaiChoice.IsChecked=setup.Tennokai;incarnonChoice.IsChecked=setup.Incarnon;incarnonRemaining.Text=setup.IncarnonSecondsRemaining?.ToString(CultureInfo.InvariantCulture)??"";
            foreach(var choice in evolutionChoices.Values)choice.SelectedItem=choice.Items.Cast<EvolutionChoice>().FirstOrDefault(c=>c.Perk is {} p&&(setup.Evolutions??[]).Contains(p.Id))??choice.Items[0];
            abilityStrengthInput.Text=(setup.AbilityStrength*100).ToString(CultureInfo.InvariantCulture);abilityRangeInput.Text=(setup.AbilityRange*100).ToString(CultureInfo.InvariantCulture);abilityRankInput.SelectedItem=setup.AbilityRank;abilityDurationInput.Text=(setup.AbilityDuration*100).ToString(CultureInfo.InvariantCulture);abilityAugmentChoice.SelectedItem=abilityAugmentChoice.Items.OfType<AbilityAugmentChoice>().FirstOrDefault(a=>a.Definition?.Id==setup.AbilityAugment?.ModId);abilityAugmentRank.SelectedItem=setup.AbilityAugment?.Rank??3;chromaticElementChoice.SelectedIndex=Math.Max(0,Array.IndexOf(ChromaticElements,setup.ChromaticElement??DamageType.Heat));RefreshAbilityAugmentFields();
            attackDistance.Text=setup.Distance.ToString(CultureInfo.InvariantCulture);targetOrder.Text=setup.TargetIndex.ToString();comboDurationOverride.Text=setup.ComboDurationOverride?.ToString(CultureInfo.InvariantCulture)??"";
            targetShields.Text=setup.Shields.ToString(CultureInfo.InvariantCulture);targetOverguard.Text=setup.Overguard.ToString(CultureInfo.InvariantCulture);factionChoice.SelectedValue=setup.Faction;
            bonusElementChoice.SelectedIndex=setup.BonusElement switch{DamageType.Impact=>1,DamageType.Heat=>2,DamageType.Cold=>3,DamageType.Electricity=>4,DamageType.Toxin=>5,DamageType.Magnetic=>6,DamageType.Radiation=>7,_=>0};bonusPercent.Text=(setup.Bonus==0?25:setup.Bonus*100).ToString(CultureInfo.InvariantCulture);
            foreach(var (type,input) in targetStatuses)input.Text=(setup.Statuses?.GetValueOrDefault(type)??0).ToString();
            exilusChoice.SelectedItem=exilusChoice.Items.Cast<ExilusChoice>().Single(c=>c.Mod?.Id==setup.Exilus?.ModId);exilusRank.SelectedItem=setup.Exilus?.Rank??0;exilusPolarity.SelectedValue=setup.ExilusPolarity;
            meleeConditions.Clear();preservedConditions=new(setup.Conditions??[]);
        }
        finally{changingMelee=false;}
        RefreshMeleeConditions();RefreshEquipmentSlots();RefreshAttackVisibility();
    }
    private void RefreshMeleeResult(Calculation c)
    {
        if(c.Melee is not {} m)return;
        meleeSummary.Text=AttackName(m.Attack)+(m.Setup.Incarnon?" · 인카논":"")+(m.Setup.Tennokai?" · 텐노카이":"");
        meleeDetails.Text=$"사거리 {m.Range:0.##}m · 관통 유지 {m.FollowThrough:P0} · 연속기 시간 {m.ComboDuration:0.##}초\n초기 콤보 {m.InitialCombo:0.##} · 콤보 획득 {m.ComboChance:0.##} · 강공격 효율 {m.HeavyEfficiency:P0}\n준비 {m.Windup:0.###}초 · 텐노카이 기회 {m.TennokaiChance:P1} · 시작 상태 회복 {m.Healing:N1}";
        if(m.AreaRadius is {} radius)meleeDetails.Text+=$"\n범위 피해 반경 {radius:0.##}m · 사거리와 별도 · 반경 보너스와 거리 감쇠 반영";
        statusDamageRows.Children.Clear();
        void Row(params string[] values)
        {
            var row=new Grid{Margin=new(0,6,0,6)};
            for(var i=0;i<values.Length;i++){row.ColumnDefinitions.Add(new(){Width=new(i==0?1:1.4,GridUnitType.Star)});var t=T(values[i],i==0?13:12);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;t.Margin=new(0,0,8,0);if(i>0)t.TextAlignment=TextAlignment.Right;Grid.SetColumn(t,i);row.Children.Add(t);}statusDamageRows.Children.Add(row);
        }
        Row("유형","1중첩 / 틱","지속 · 피해 틱","기대 중첩 / 공격");
        foreach(var s in m.StatusDamage.Where(s=>s.Chance>0))Row(Labels.Damage(s.Type),s.DamagePerTick.ToString("N1"),$"{s.Duration:0.##}초 · {s.Ticks}회",(s.Chance*m.Projectiles).ToString("0.###"));
        statusDamageRows.Children.Add(T("방어력 적용 전 · 치명타 기대값 포함 · 기존 상태 고정",12));
        if(currentStance is not null)statusDamageRows.Children.Add(T("현재 표는 일반 타격 기준입니다. 스탠스의 각 타격별 상태 피해와는 구분됩니다.",12));
        secondaryRows.Children.Clear();var secondary=MeleeSecondaryEngine.Describe(c);
        var secondaryBuild=ReadBuild();secondary=secondary.Concat(UnresolvedMeleeComponents.Describe(secondaryBuild,c)).ToArray();
        if(secondaryBuild.WeaponId==JatKusarMechanic.WeaponId&&(secondaryBuild.Melee!.Attack=="Heavy Attack"||currentStance?.Combo.ActionKind=="Heavy"))secondary=secondary.Append(JatKusarMechanic.Describe(c)).ToArray();
        if(DestrezaMechanic.Applies(secondaryBuild))
        {
            string Range(SourceInterval v,string format="N1")=>v.Exact?v.Minimum.ToString(format):v.Minimum.ToString(format)+"–"+v.Maximum.ToString(format);
            foreach(var windup in new[]{false,true})
            {
                var part=DestrezaMechanic.Calculate(catalog,secondaryBuild,windup);
                secondary=secondary.Append(new("데스트레자 · 유령 레이피어",windup?"명중 순간: 강공격 준비 동작 중":"명중 순간: 강공격 준비 동작 아님",part.Damage.Exact?part.Damage.Minimum:null,
                    Note:$"투사체1개 {Range(part.Damage)} · 5개 발사 · 강제 충격·베기\n방어 적용 후 {Range(part.TargetDamage)} · 치확 {Range(part.CriticalChance,"P1")} · 치피 {Range(part.CriticalMultiplier,"0.##")}×\n"+
                    $"강제 베기 1중첩/틱 {Range(part.ForcedSlashTick)} · {part.SlashDuration:0.##}초 · {part.SlashTicks}틱\n"+
                    "기본 상확 미기재 · 상확 의존 치명 보너스는 근거 범위로 표시\n2초 비행 · 무한 신체 관통 · 감쇠 없음 · 진화II 기본 피해/듀플리케이트/인플루언스 제외\n명중 시 상태 고정 · 실제 5연속 명중의 컨오/방어층 변화와 별도")).ToArray();
            }
        }
        if(ObexFinisherMechanic.Applies(secondaryBuild))
        {
            if(currentStance?.Combo.FinisherKind is not null)
            {
                var area=ObexFinisherMechanic.Calculate(catalog,secondaryBuild,stanceCatalog,secondaryBuild.Melee!.Distance);
                foreach(var wave in area.Waves)
                    secondary=secondary.Append(new("오벡스 · 마무리 파동 "+wave.Contact,"현재 대상과 같은 조건의 주변 적1명",wave.RawDamage,Radius:12,
                        Note:$"방어 적용 후 {wave.TargetDamage:N1} · 거리 감쇠 없음 · 중심에서 {secondaryBuild.Melee.Distance:0.##}m"+
                        (wave.StealthRawDamage is {} stealth?$"\n은신 적용 가능 값 {stealth:N1} · 위키가 잠정 기술한 별도 결과":"")+
                        "\n원래 마무리 대상 피해와 중복 합산하지 않음 · 각 접촉마다 한 파동")).ToArray();
            }
            else secondary=secondary.Append(new("오벡스 · 마무리 파동","마무리 동작을 선택하면 접촉별 피해 표시",Radius:12,Note:"거리 감쇠 없음 · 파라존 파동은 호스트에서만 발생하며 기초 피해 미기재")).ToArray();
        }
        if(secondaryBuild.WeaponId==ThalysMechanic.WeaponId&&secondaryBuild.Melee is {Incarnon:true})
        {
            var motions=currentStance?.Hits.Where(h=>h.Input!.Melee!.Attack is "Normal Attack" or "Slide Attack"||h.Input.Melee.Finisher is not null)
                .Select(h=>h.Input!.Scenario.MotionMultiplier).Distinct().Order().ToArray()??[];
            if(motions.Length==0)motions=[1];
            foreach(var motion in motions)
            {
                var shard=ThalysMechanic.Calculate(catalog,secondaryBuild,motion);
                foreach(var (name,part) in new[]{("가시 재발동",shard.Echo),("완전 성장 꿰뚫기",shard.Impale)})
                {
                    if(part is null)continue;
                    secondary=secondary.Append(new("탈리스 · "+name,"다른 대상에 새 가시 생성 · 발동 동작 "+motion.ToString("0.##")+"배",part.ExpectedHit,
                        Note:$"기존 가시 숙주1명 · 치확 {part.CriticalChance:P1} · 치피 {part.CriticalMultiplier:0.##}× · 상확 {part.StatusChance:P1}\n"+
                        (name=="가시 재발동"?"컨오/물리/원소/팩션 제외":"컨오 적용 · 물리/원소/팩션 제외")+
                        $" · 가시 유지 {shard.Duration:0}초\n방어 적용 전 · 고정 시작 상태의 성분 피해 · 새로 타격한 대상의 피해와 합산하지 않음")).ToArray();
                }
            }
        }
        if(MeleeRuleCatalog.Load().Weapon(secondaryBuild.WeaponId).Name=="Vitrica")
        {
            var heavy=m.Attack=="Heavy Slam Attack";
            var glass=VitricaMechanic.Calculate(catalog,secondaryBuild,heavy,secondaryBuild.Melee!.Distance);
            foreach(var (name,part) in new[]{("유리화된 원래 대상",glass.GlassedTarget),("주변 대상 1명",glass.NearbyTarget)})
                secondary=secondary.Append(new("비트리카 · "+name,heavy?"강 슬램 파동으로 유리화 파괴":"슬램으로 유리화 파괴",part.ExpectedHit,Radius:name=="주변 대상 1명"?6:0,
                    Note:$"치확 {part.CriticalChance:P1} · 치피 {part.CriticalMultiplier:0.##}× · 상확 {part.StatusChance:P1}\n"+
                    (name=="유리화된 원래 대상"?"기본 상확 100% · 일반 상확 모드 제외 · 물리/원소 피해 모드 적용":"기본 상확 33% · 상확 모드 적용 · 물리/원소 피해 모드 제외 · 중심에서 "+secondaryBuild.Melee.Distance.ToString("0.##")+"m")+
                    "\n시작 상태 고정 · 방어 적용 전 · 유리화는 7초 · 두 대상의 피해는 합산하지 않음")).ToArray();
        }
        if(secondaryBuild.WeaponId==SpinnerexMechanic.WeaponId)
        {
            var explosion=SpinnerexMechanic.Calculate(catalog,secondaryBuild,c.Melee!.Setup.Statuses?.GetValueOrDefault(DamageType.Toxin)??0);
            secondary=secondary.Append(new("스피네렉스 · 처치 폭발","5초 이내 무기 명중 후 독성 상태인 대상 사망",explosion.TotalDamage,Radius:10,DamagePerTick:explosion.Toxin.DamagePerTick,Chance:explosion.Chance,
                Note:"폭발 중심 · 발동 1회당 · 방어 적용 전 · 강제 독성 1중첩. 컨오·치명타·모딩 상태 발생·강공격 콤보 제외. 발동 확률은 표시된 시작 상태 기준이며, 교전 중 실제 독성 누적은 시간별 결과에 반영합니다. 거리 감쇠의 정확한 곡선은 위키 미기재.")).ToArray();
        }
        if(ZawCatalog.IsZaw(secondaryBuild.WeaponId)&&secondaryBuild.Melee?.Exodia is {Active:true} exodia&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==exodia.Id).Code=="Contagion")
        {
            try
            {
                var contagion=ExodiaContagionMechanic.Calculate(catalog,secondaryBuild,secondaryBuild.Melee.Distance);
                foreach(var part in new[]{contagion.Contact,contagion.EmbeddedTick,contagion.Explosion})
                    secondary=secondary.Append(new("컨테이전 · "+part.Name,"점프 후 에임 글라이드 중 공중 근접 발사",part.ExpectedDamage,Radius:part==contagion.Explosion?8:0,
                        Note:$"투사체 이동 {secondaryBuild.Melee.Distance:0.##}m · 거리/콤보 배수 {contagion.TravelMultiplier:0.##}× · 치확 {part.CriticalChance:P1} · 치피 {part.CriticalMultiplier:0.##}× · 상확 {part.StatusChance:P1}\n"+
                        (part==contagion.Contact?"직격: 컨오 별도 곱연산":part==contagion.EmbeddedTick?"1초 간격 4회 부착 피해 · 컨오 제외":$"5초째 폭발 · 스탠스 배수 {contagion.StanceMultiplier:0.##}× · 컨오 제외")+"\n시작 상태 고정 · 치명타 기대값 · 방어 적용 전 · 성분별 결과이며 교전 총합과 구분")).ToArray();
            }
            catch(CombatRuleUnavailableException ex){secondary=secondary.Append(new("컨테이전","성분 계산의 출처 확인 필요",Note:ex.Message)).ToArray();}
        }
        if(ZawCatalog.IsZaw(secondaryBuild.WeaponId)&&secondaryBuild.Melee?.Exodia is {Active:true} epidemic&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==epidemic.Id).Code=="Epidemic")
        {
            try
            {
                var result=ExodiaEpidemicMechanic.Calculate(catalog,secondaryBuild);
                foreach(var (part,label) in new[]{(result.Wave,"파동"),(result.Explosion,"끝 폭발 · 위키 근삿값")})
                    secondary=secondary.Append(new("에피데믹 · "+label,"점프 직후 슬램 · 별도 성분",part.ExpectedHit,
                        Note:$"치확 {part.CriticalChance:P1} · 치피 {part.CriticalMultiplier:0.##}× · 상확 {part.StatusChance:P1}\n시작 상태 고정·방어 적용 전. 파동 부유 {result.SuspensionSeconds:0}초·컨오/어플릭션즈 제외.\n"+string.Join("\n",result.Notes.Take(3)))).ToArray();
            }
            catch(CombatRuleUnavailableException ex){secondary=secondary.Append(new("에피데믹","성분 계산의 출처 확인 필요",Note:ex.Message)).ToArray();}
        }
        if(secondaryBuild.WeaponId==CeramicDaggerMechanic.WeaponId&&secondaryBuild.Melee?.Incarnon==true)
        {
            foreach(var (dagger,blast) in new[]{(1,false),(2,false),(1,true)})
            {
                var part=CeramicDaggerMechanic.Calculate(catalog,secondaryBuild,dagger,blast);
                secondary=secondary.Append(new(blast?"세라믹 · 폭발1회":"세라믹 · 단검"+dagger+" 직격","강공격마다 단검2개 · 콤보 공유",part.ExpectedHit,Radius:blast?4:0,
                    Note:$"{(blast?"강제 충격 · 컨오 제외 · 가장자리30% 감쇠":"컨오 곱연산 · 단검1만 첫 공격 치피 보너스")}\n일반 근접 피해·킬링 블로우 제외 / 범용 피해·강공격 콤보·치확 적용\n시간별 교전은 ‘유령 단검’에서 발사/명중을 지정합니다.")).ToArray();
            }
        }
        if(OkinaDaggerState.Applies(secondaryBuild.WeaponId)&&secondaryBuild.Melee?.Incarnon==true)
        {
            foreach(var blast in new[]{false,true})
            {
                var part=OkinaDaggerState.Calculate(catalog,secondaryBuild,blast);
                secondary=secondary.Append(new(blast?"오키나 · 단검 폭발":"오키나 · 단검 직격","인카논 중 처치로 생성 · 최대6개",part.ExpectedHit,Radius:blast?4:0,
                    Note:$"강제 냉기 {(blast?1:10)}중첩 · {(blast?"컨오 제외 · 무작위 상태만 인플루언스 전파":"컨오 별도 곱연산 · 인플루언스 발동만 가능")}\n일반 근접 피해/변신 피해 제외 · 범용 피해 적용 · 콤보 획득 없음\n‘유령 단검 비행’에서 실제 순서의 직격·폭발·방어층 전이를 계산합니다.")).ToArray();
            }
        }
        if(CaustacystMechanic.Applies(secondaryBuild.WeaponId))
        {
            var pool=CaustacystMechanic.Pool(catalog,secondaryBuild);
            var waves=CaustacystMechanic.Wave(catalog,secondaryBuild,secondaryBuild.Melee!.Distance/15,secondaryBuild.Melee.TargetIndex-1);
            secondary=secondary.Append(new("카우스타시스트 · 웅덩이1회","진입 즉시 · 이후1초마다",pool.ExpectedHit,Note:$"상확 {pool.StatusChance:P0} · 일반 근접 피해 적용·범용 피해/컨오 제외\n전체 교전은 ‘웅덩이 체류’에서 계산합니다. 생성 후4초/8초 지속시간 상충.")).ToArray();
            secondary=secondary.Append(new("카우스타시스트 · 파동 출처 비교","거리·이전 관통 대상 적용",Note:$"본문 {waves.Article.ExpectedHit:N1} / 데이터 표 {waves.Infobox.ExpectedHit:N1} · 본문 기준 남은 수명 {waves.RemainingLifetime:0.##}초\n"+string.Join("\n",waves.Differences))).ToArray();
        }
        if(DualIchorCloudMechanic.Applies(secondaryBuild.WeaponId)&&secondaryBuild.Melee?.Incarnon==true)
        {
            var cloud=DualIchorCloudMechanic.Calculate(catalog,secondaryBuild);
            secondary=secondary.Append(new("듀얼 이코르 · 독 구름1회 접촉","인카논 중 무기·상태·구름으로 처치",cloud.ExpectedHit,Radius:4,
                Note:$"상확 {cloud.StatusChance:P1} · 치명타 없음 · 수명7초 · 구름 상태는 인플루언스 제외\n원문에 최초 피해 시각·틱 간격이 없어 초당 피해로 바꾸지 않습니다. ‘독 구름 명중’에서 지정한 시나리오는 교전 결과에 합산됩니다.")).ToArray();
        }
        foreach(var effect in secondary)
        {
            var values=new List<string>();if(effect.Damage is {} d)values.Add($"{effect.DamageLabel} {d:N1}");if(effect.DamagePerTick is {} tick)values.Add($"1중첩 / 틱 {tick:N1}");if(effect.ExtraStacks is {} count)values.Add($"추가 중첩 {count:0.###}");if(effect.Chance is {} chance)values.Add($"발동 {chance:P0}");if(effect.Radius>0)values.Add($"반경 {effect.Radius:0.##}m");
            var title=T(effect.Name,14);title.Margin=new(0,12,0,6);secondaryRows.Children.Add(title);secondaryRows.Children.Add(T(effect.Trigger,12));if(values.Count>0)secondaryRows.Children.Add(T(string.Join(" · ",values),13));var note=T(effect.Note,12);note.LineHeight=20;secondaryRows.Children.Add(note);
        }
        if(secondary.Length==0)secondaryRows.Children.Add(T("현재 장착·발동 조건에서 별도로 표시할 범위 효과가 없습니다.",12));
        secondaryRows.Children.Add(T("직접 DPS·TTK에 범위 피해를 합산하지 않습니다. 각 대상의 상태와 발동 조건은 별도로 적용해야 합니다.",12));
    }
    private static string AttackName(string value)=>value switch{"Normal Attack"=>"일반 타격","Heavy Attack"=>"강공격","Slide Attack"=>"슬라이드","Slam Attack"=>"슬램","Heavy Slam Attack"=>"강공격 슬램","Ranged Attack"=>"건블레이드 사격","Charged Projectile"=>"충전 투사체","Energy Disk"=>"에너지 원반","Corrosive Wave"=>"부식 파동","Corrosive Pool"=>"부식 웅덩이","Exalted Wave"=>"익절티드 일반 파동","Exalted Heavy Wave"=>"익절티드 강공격 파동","Damage"=>"섀터드 래시 · 찌르기","Arcing Damage"=>"섀터드 래시 · 휘두르기",_=>value};
    private static string ConditionName(string key)=>key switch
    {
        "UniversalStatusDuration"=>"외부 상태 지속시간 보너스 (%)",
        "GalvanizedStacks"=>"갈바나이즈드 처치 중첩","BerserkerStacks"=>"버서커 처치 중첩","Lifted"=>"공중에 띄워짐","ProtonSnap"=>"프로톤 스냅","Cysts"=>"스트레인 낭종","SpringStacks"=>"스프링 로디드 중첩","TruthFlame"=>"트루스 발동","HeavyAnimation"=>"명중 순간 강공격 동작 중","Airborne"=>"공중 상태","ExternalGladiator"=>"외부 글래디에이터 개수","AnimosityStacks"=>"애니모시티 중첩","HotShotStacks"=>"핫 샷 중첩","Invisible"=>"은신","EnergySpent"=>"에너지 채널 저장 소모량","Armor450"=>"방어력 450 초과","ShieldBreak"=>"실드 파괴 후 버프","Equipped"=>"근접무기 단독 장착","Overshields"=>"오버실드 보유","PrimaryKills"=>"주무기 처치","FirstQuickMelee"=>"전환 후 첫 근접","HeavyKill"=>"강공격 처치 버프","PunctureStacks"=>"관통 발동 중첩","NearbyEnemies"=>"주변 적 수","Sliding"=>"슬라이딩 중","DreadDespair"=>"드레드·디스페어 동시 장착","Channeling"=>"지속형 어빌리티 유지","OnEquip"=>"무기 전환 버프","FinisherStacks"=>"마무리 처치 중첩","CriticalStacks"=>"치명타 발동 중첩","ColdKills"=>"냉기 대상 처치 중첩","Undamaged"=>"적의 체력·실드가 손상되지 않음","AirborneKills"=>"공중 처치 중첩","FinisherBuff"=>"마무리 공격 버프","SlideKill"=>"슬라이드 처치 버프","RuvoxKills"=>"루복스 처치 중첩","ImpaledFive"=>"5명 관통 발동","Impaled"=>"가시에 꿰뚫린 대상","ThalysStatus"=>"탈리스 상태 발동 중첩","NeutralFinalHit"=>"정지 콤보 마지막 명중","Roll"=>"아마나타 주사위","BurningHateApplied"=>"버닝 헤이트 표식 · 교전 시작 상태",_=>key
    };
}
