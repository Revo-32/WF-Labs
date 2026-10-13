using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private record ArcaneChoice(ArcaneDefinition? Definition)
    {public string Name=>Definition?.Name??"장착 안 함";}
    private sealed class ArcaneEditor
    {
        internal ComboBox Choice=new(){DisplayMemberPath="Name"};
        internal ComboBox Rank=new(){Width=64};
        internal CheckBox Active=new(){Content="발동",IsChecked=true,VerticalAlignment=VerticalAlignment.Center};
        internal TextBlock Description=T("",12,"#596B85");
    }
    private readonly ArcaneEditor[] arcaneEditors=[new(),new(),new(),new()];
    private readonly TextBox exposureStacks=new(){Text="0"},currentShields=new(){Text="0"},currentOvershields=new(){Text="0"},finisherKills=new(){Text="0"};
    private readonly TextBox maximumPlayerShields=new(){Text="",ToolTip="워프레임의 모딩 후 최대 실드를 입력하세요. 빈칸이면 기존 고정 실드 조건을 사용합니다."},maximumPlayerOvershields=new(){Text="1200"},shieldRechargeBonus=new(){Text="0"},shieldDelayReduction=new(){Text="0"},shieldDelayRemaining=new(){Text="0"};
    private readonly CheckBox shieldRechargeEnabled=new(){Content="자연 실드 재생",IsChecked=true};
    private FrameworkElement? playerShieldFields,playerArmorField;
    private readonly TextBox playerArmor=new(){Text="",ToolTip="방어력 아케인과 포티피케이션의 버프를 제외한 모딩 후 방어력입니다. 발동/만료에 따라 방어력450초과 진화를 자동 판정합니다."};
    private readonly CheckBox frozenTarget=new(){Content="대상 냉기 10중첩 · 동결"};
    private readonly TextBlock arcaneSummary=T("장착 안 함",12,"#596B85");
    private readonly TextBlock arcaneTimeNote=T("",14,"#596B85");
    private Expander? arcaneExpander;
    private readonly Dictionary<string,FrameworkElement> arcaneConditionFields=[];
    private FrameworkElement CreateArcaneView()
    {
        var panel=new StackPanel();var source=ReferenceCatalog.Load();
        for(var i=0;i<arcaneEditors.Length;i++)
        {
            var editor=arcaneEditors[i];var category=i==0?"Melee":i==3?"Operator":"Warframe";
            editor.Choice.ItemsSource=new[]{new ArcaneChoice(null)}.Concat(source.Arcanes.Where(a=>a.Category==category&&(category!="Operator"||a.Id==MagusAggressState.Id)).OrderBy(a=>a.Name).Select(a=>new ArcaneChoice(a))).ToArray();
            editor.Choice.SelectedIndex=0;editor.Rank.ItemsSource=new[]{0};editor.Rank.SelectedIndex=0;
            var row=new DockPanel();DockPanel.SetDock(editor.Active,Dock.Right);editor.Active.Margin=new(12,0,0,0);row.Children.Add(editor.Active);
            if(i>0){DockPanel.SetDock(editor.Rank,Dock.Right);editor.Rank.Margin=new(8,0,0,0);row.Children.Add(editor.Rank);row.Children.Add(editor.Choice);}
            else {var equipped=T("",14);equipped.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("SelectedItem.Name"){Source=editor.Choice});row.Children.Add(equipped);}
            WatchEquationSource(row,"arcane",()=>(editor.Choice.SelectedItem as ArcaneChoice)?.Definition is {} a?(a.Id,a.Name):null);
            WatchEquationSource(editor.Description,"arcane",()=>(editor.Choice.SelectedItem as ArcaneChoice)?.Definition is {} a?(a.Id,a.Name):null);
            panel.Children.Add(Field(i==0?"근접 아케인":i==3?"오퍼레이터 아케인":$"워프레임 아케인 {i}",new ContentControl{Content=row}));panel.Children.Add(editor.Description);
            void Describe(){var a=(editor.Choice.SelectedItem as ArcaneChoice)?.Definition;editor.Description.Text=a?.Describe(editor.Rank.SelectedItem is int rank?rank:a.MaxRank)??"";editor.Description.Visibility=a is null?Visibility.Collapsed:Visibility.Visible;editor.Rank.Visibility=editor.Active.Visibility=a is null?Visibility.Collapsed:Visibility.Visible;UpdateArcaneSummary();RefreshEquipmentSlots();RefreshArcaneConditions();RefreshMeleeConditions();Recalculate();}
            editor.Choice.SelectionChanged+=(_,_)=>{var a=(editor.Choice.SelectedItem as ArcaneChoice)?.Definition;editor.Rank.ItemsSource=Enumerable.Range(0,(a?.MaxRank??0)+1).ToArray();editor.Rank.SelectedIndex=a?.MaxRank??0;Describe();};
            editor.Rank.SelectionChanged+=(_,_)=>Describe();editor.Active.Checked+=(_,_)=>Describe();editor.Active.Unchecked+=(_,_)=>Describe();
            editor.Rank.Visibility=editor.Active.Visibility=editor.Description.Visibility=Visibility.Collapsed;
        }
        var conditions=new StackPanel();
        foreach(var (key,label,input,max) in new[]{("Exposure","익스포저 발동 횟수",exposureStacks,24d),("Retaliation","교전 시작 실드 (오버실드 제외)",currentShields,100000d),("RetaliationOvershield","교전 시작 오버실드",currentOvershields,100000d),("Crescendo","교전 시작 전 마무리 처치",finisherKills,1000d)})
        {var f=(FrameworkElement)Field(label,new ContentControl{Content=Stepper(input,0,max,1,label)});arcaneConditionFields[key]=f;conditions.Children.Add(f);}
        var recharge=Stack(shieldRechargeEnabled,Field("재생 속도 보너스 (%)",shieldRechargeBonus),Field("재생 대기 감소 (%) · 최대 80",shieldDelayReduction),Field("교전 시작 시 재생 대기 (초)",shieldDelayRemaining));
        playerShieldFields=Stack(Field("워프레임 최대 실드 · 모딩 후",maximumPlayerShields),Field("오버실드 한도 · 일반 1,200",maximumPlayerOvershields),new Expander{Header="실드 자연 재생 설정",Content=recharge});conditions.Children.Add(playerShieldFields);
        playerArmorField=(FrameworkElement)Field("워프레임 방어력 · 버프 적용 전",playerArmor);conditions.Children.Add(playerArmorField);
        panel.Children.Add(conditions);arcaneTimeNote.TextWrapping=TextWrapping.Wrap;panel.Children.Add(arcaneTimeNote);RefreshArcaneConditions();
        foreach(var input in new[]{exposureStacks,currentShields,currentOvershields,finisherKills,maximumPlayerShields,maximumPlayerOvershields,shieldRechargeBonus,shieldDelayReduction,shieldDelayRemaining,playerArmor})input.TextChanged+=(_,_)=>Recalculate();
        playerArmor.TextChanged+=(_,_)=>RefreshMeleeConditions();
        shieldRechargeEnabled.Checked+=(_,_)=>Recalculate();shieldRechargeEnabled.Unchecked+=(_,_)=>Recalculate();
        frozenTarget.Checked+=(_,_)=>Recalculate();frozenTarget.Unchecked+=(_,_)=>Recalculate();
        var heading=new DockPanel();var title=T("아케인",14);title.Margin=new(0,0,14,0);title.VerticalAlignment=VerticalAlignment.Center;heading.Children.Add(title);
        arcaneSummary.Margin=new(0);arcaneSummary.VerticalAlignment=VerticalAlignment.Center;arcaneSummary.TextWrapping=TextWrapping.NoWrap;arcaneSummary.TextTrimming=TextTrimming.CharacterEllipsis;heading.Children.Add(arcaneSummary);
        title.Text="아케인 발동 · 외부 버프";
        return arcaneExpander=new Expander{Header=heading,Content=panel};
    }
    private void RefreshArcaneConditions()
    {
        var codes=arcaneEditors.Select(e=>(e.Choice.SelectedItem as ArcaneChoice)?.Definition).Where(a=>a is not null).Select(a=>MeleeRuleCatalog.Load().Arcanes.Single(r=>r.Id==a!.Id).Code).ToHashSet();
        foreach(var (key,field) in arcaneConditionFields)field.Visibility=codes.Contains(key.Replace("Overshield",""))?Visibility.Visible:Visibility.Collapsed;
        if(playerArmorField is not null)playerArmorField.Visibility=codes.Contains("Fortification")||codes.Any(WarframeCombatArcanes.ArmorCode)||!string.IsNullOrWhiteSpace(playerArmor.Text)?Visibility.Visible:Visibility.Collapsed;
        var usesPlayerShield=weapon.SelectedItem is Weapon w&&MeleeRuleCatalog.Load().Weapon(w.Id).Name=="Rakta Dark Dagger"||codes.Contains("Retaliation")||codes.Contains("Assimilation")||codes.Contains("Aegis")||codes.Contains("Barrier");
        if(playerShieldFields is not null)playerShieldFields.Visibility=usesPlayerShield?Visibility.Visible:Visibility.Collapsed;
        if(usesPlayerShield)foreach(var key in new[]{"Retaliation","RetaliationOvershield"})if(arcaneConditionFields.TryGetValue(key,out var field))field.Visibility=Visibility.Visible;
        var timed=SelectedEncounter is EncounterMode.StanceCycle or EncounterMode.FiveSeconds or EncounterMode.TenSeconds;
        foreach(var e in arcaneEditors){var a=(e.Choice.SelectedItem as ArcaneChoice)?.Definition;var code=a is null?null:MeleeRuleCatalog.Load().Arcanes.Single(r=>r.Id==a.Id).Code;var auto=code is not null&&(CombatTimeline.AutomaticArcane(code)||code is "Fortification" or "MagusAggress");e.Active.Content=timed&&auto?"자동 발동":"발동";}
        if(timed&&arcaneConditionFields.TryGetValue("Exposure",out var exposure))exposure.Visibility=Visibility.Collapsed;
        arcaneTimeNote.Text=timed?"자동 발동 아케인은 미발동에서 시작합니다. 익스포저·어벤저 등의 외부 조건은 ‘전투 중 행동’에서 추가하세요. 나머지 수동 버프 조건은 고정합니다.":"선택한 발동 상태를 고정한 첫 타격을 계산합니다.";
    }
    private void UpdateArcaneSummary()=>arcaneSummary.Text=string.Join(" · ",arcaneEditors.Select(e=>(e.Choice.SelectedItem as ArcaneChoice)?.Definition?.Name).Where(x=>x is not null)) is {Length:>0} names?names:"장착 안 함";
    private ArcaneSetup? ReadArcanes()
    {
        ArcaneSlot? Read(ArcaneEditor e)=>(e.Choice.SelectedItem as ArcaneChoice)?.Definition is {} a?new(a.Id,e.Rank.SelectedItem is int r?r:a.MaxRank,e.Active.IsChecked==true):null;
        var slots=arcaneEditors.Select(Read).ToArray();
        var maximum=string.IsNullOrWhiteSpace(maximumPlayerShields.Text)?(double?)null:Number(maximumPlayerShields,"워프레임 최대 실드");
        if(slots.All(s=>s is null)&&frozenTarget.IsChecked!=true&&maximum is null&&string.IsNullOrWhiteSpace(playerArmor.Text))return null;
        return new(slots[0],slots[1],slots[2],Integer(exposureStacks,"익스포저 횟수"),Number(currentShields,"현재 실드"),Number(currentOvershields,"오버실드"),frozenTarget.IsChecked==true,Integer(finisherKills,"마무리 처치"),maximum,Number(maximumPlayerOvershields,"오버실드 한도"),shieldRechargeEnabled.IsChecked==true,Number(shieldRechargeBonus,"실드 재생 보너스")/100,Number(shieldDelayReduction,"재생 대기 감소")/100,Number(shieldDelayRemaining,"시작 재생 대기"),string.IsNullOrWhiteSpace(playerArmor.Text)?null:Number(playerArmor,"워프레임 방어력"),slots[3]);
    }
    private void ApplyArcanes(ArcaneSetup? setup)
    {
        setup??=new();var slots=new[]{setup.Melee,setup.Warframe1,setup.Warframe2,setup.Operator};
        for(var i=0;i<arcaneEditors.Length;i++)
        {var e=arcaneEditors[i];var s=slots[i];e.Choice.SelectedItem=e.Choice.Items.Cast<ArcaneChoice>().Single(c=>c.Definition?.Id==s?.Id);e.Rank.SelectedItem=s?.Rank??0;e.Active.IsChecked=s?.Active??true;}
        exposureStacks.Text=setup.ExposureStacks.ToString();currentShields.Text=setup.Shields.ToString(System.Globalization.CultureInfo.InvariantCulture);currentOvershields.Text=setup.Overshields.ToString(System.Globalization.CultureInfo.InvariantCulture);finisherKills.Text=setup.FinisherKills.ToString();frozenTarget.IsChecked=setup.Frozen;
        playerArmor.Text=setup.PlayerArmor?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
        maximumPlayerShields.Text=setup.MaximumShields?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";maximumPlayerOvershields.Text=setup.MaximumOvershields.ToString(System.Globalization.CultureInfo.InvariantCulture);
        shieldRechargeEnabled.IsChecked=setup.ShieldRechargeEnabled;shieldRechargeBonus.Text=(100*setup.ShieldRechargeBonus).ToString(System.Globalization.CultureInfo.InvariantCulture);shieldDelayReduction.Text=(100*setup.ShieldRechargeDelayReduction).ToString(System.Globalization.CultureInfo.InvariantCulture);shieldDelayRemaining.Text=setup.ShieldRechargeDelayRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public void ArcanePreviewForCheck(bool enabled)
    {
        if(enabled)
        {
            var source=ReferenceCatalog.Load();ArcaneSlot A(string n)=>new(source.Arcanes.Single(a=>a.EnglishName==n).Id,5);
            var build=ReadBuild();ApplyBuild(build with{Arcanes=new(Melee:A("Melee Exposure"),Warframe1:A("Arcane Fury"),ExposureStacks:4)});
        }
        else ApplyBuild(ReadBuild() with{Arcanes=null});
        arcaneExpander!.IsExpanded=enabled;
        if(scenarioScroller is {} scroller){scroller.UpdateLayout();var offset=enabled?arcaneExpander.TransformToAncestor(scroller).Transform(new Point()).Y+scroller.VerticalOffset-8:0;scroller.ScrollToVerticalOffset(Math.Max(0,offset));scroller.UpdateLayout();}
    }
}
