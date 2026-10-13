using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class CombatHistoryPlot:Plot
{
    private CombatPoint[] points=[];private CombatEvent[] events=[];
    internal bool Defense {get;set;}
    internal void Set(CombatTimelineResult? value){points=value?.Points??[];events=value?.Events??[];Summary=Defense?"남은 체력·실드·오버가드 · 표본 평균":"누적 직접 피해·상태 피해 · 표본 평균";Changed();}
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(points.Length==0)return;
        var colors=Defense?new[]{ChartPalette.Damage(DamageType.Heat),ChartPalette.Comparison,ChartPalette.Damage(DamageType.Void)}:new[]{ChartPalette.Accent,ChartPalette.Damage(DamageType.Viral)};
        var names=Defense?new[]{"체력","실드","오버가드"}:new[]{"직접 피해","상태 피해"};
        double Value(CombatPoint p,int i)=>Defense?i switch{0=>p.Health,1=>p.Shields,_=>p.Overguard}:i==0?p.DirectDamage:p.StatusDamage;
        var max=Math.Max(1,points.Max(p=>Enumerable.Range(0,names.Length).Max(i=>Value(p,i))));var end=Math.Max(.01,points[^1].Time);
        var area=new Rect(52,30,Math.Max(1,ActualWidth-66),Math.Max(1,ActualHeight-70));
        Point At(double t,double v)=>new(area.Left+t/end*area.Width,area.Bottom-v/max*area.Height);
        for(var n=0;n<names.Length;n++)Label(dc,"● "+names[n],new(area.Left+n*100,2),13,colors[n]);
        for(var i=0;i<=3;i++){var y=max*i/3;dc.DrawLine(new Pen(GridInk,1),At(0,y),At(end,y));Label(dc,ChartPalette.Number(y),new(area.Left-6,At(0,y).Y-9),13,right:true);}
        for(var series=0;series<names.Length;series++)for(var i=1;i<points.Length;i++)dc.DrawLine(new Pen(colors[series],1.7),At(points[i-1].Time,Value(points[i-1],series)),At(points[i].Time,Value(points[i],series)));
        var width=Math.Max(8,area.Width/points.Length);
        foreach(var p in points)
        {
            var hint=$"{p.Time:0.00}초 · 표본 평균\n직접 {p.DirectDamage:N1} / 상태 {p.StatusDamage:N1}\n체력 {p.Health:N1} · 실드 {p.Shields:N1}\n오버가드 {p.Overguard:N1} · 방어력 {p.Armor:N1}\n상태 {p.StatusTypes:0.0}종 · 콤보 {p.Combo:0.0}× ({p.ComboPoints:0.0}점)";
            if(p.PlayerShields is {} own)hint+=$"\n내 실드 {own:N1} · 내 오버실드 {p.PlayerOvershields:N1}";
            if(p.IncarnonRemaining is {} remaining)hint+=$"\n인카논 잔여 {remaining:0.##}초 · 처치 관통 보너스 +{p.IncarnonPunctureBonus:P0} · 리질리언스 피해 감소 {p.IncarnonResilience:P2}";
            if(p.SoulCharges is {} souls)hint+=$"\n소울 스웜 저장 {souls:0.##}개 · 비행 중 {p.SoulsInFlight:0.##}개";
            if(p.MeleeRange is {} range)hint+=$"\n공격 사거리 {range:0.##}m · 활성 사거리 효과 반영";
            if(p.MagusAggressCharges is {} magus)hint+=$"\n마구스 어그레스 {magus:0.##}/4회 남음";
            if(p.HotShotStacks is {} hot)hint+=$"\n핫 샷 {hot:0.##}/50중첩";
            if(p.PowerThrowStacks is {} power)hint+=$"\n파워 스로우 연속 투척 {power:0.##}/3중첩";
            if(p.TennokaiRemainingMinimum is {} tennokai)hint+=$"\n텐노카이 기회 잔여 {tennokai:0.##}~{p.TennokaiRemainingMaximum:0.##}초 · 사용 {p.TennokaiUses:0.##}회\n트루스 성공 {p.TennokaiRefunds:0.##} · 실패 {p.TennokaiFailures:0.##}회 · 자기 화염 원본 총량 {p.TennokaiRawCurse:N1}";
            if(p.JadeStatusDurationBonus is {} jade)hint+=$"\n제이드 무기 버프 · 상태 지속 +{jade:P0}";
            if(p.AnimosityBonus is {} animosity)hint+=$"\n애니모시티 · 다음 강공격 치확 모드 보너스 +{animosity:P0}";
            if(p.FinisherParkourBonus is {} parkour)hint+=$"\n래피드 컨클루전 파쿠르 +{parkour:P1}";
            if(p.RubbleArmor is {} rubble)hint+=$"\n러블 {rubble:0.##} · 회복 {p.RubbleHealing:0.##} · 생성 {p.RubbleDrops:0.##}개";
            if(p.HologramCount is {} hc)hint+=$"\n분신 {hc:0.##}개 · 멀레볼런스 +{p.HallMalevolence:P0}";
            if(p.WarcryRemaining is {} wc)hint+=$"\n워크라이 {wc:0.##}초 · 방어력 +{p.WarcryArmor:N1}";
            if(p.PlayerArmor is {} armor)hint+=$"\n내 방어력 {armor:N1} · 포티피케이션 +{p.FortificationArmor:N1}";
            else if(p.FortificationArmor>0)hint+=$"\n포티피케이션 추가 방어력 {p.FortificationArmor:N1}";
            if(p.BraveEnergy is {} energy)hint+=$"\n브레이브 에너지 {energy:N1} · 재생 {p.BraveRate:0.##}/초";
            if(p.InvigoratedStacks>0)hint+=$"\n인비거레이티드 {p.InvigoratedStacks:0.00}중첩 · 기본 최대 체력 +{p.InvigoratedStacks*5:0.0}%";
            Targets.Add((new Rect(At(p.Time,0).X-width/2,area.Top,width,area.Height),hint));
        }
        foreach(var e in events.Where(e=>e.Kind is "layer" or "kill").Take(8)){var p=At(e.Time,0);dc.DrawEllipse(ChartPalette.Accent,null,p,3,3);Targets.Insert(0,(new Rect(p.X-6,p.Y-6,12,12),$"대표 표본 {e.Time:0.###}초\n{e.Description}"));}
        Label(dc,"0",new(area.Left,area.Bottom+10));Label(dc,$"{end:0.##}초",new(area.Right,area.Bottom+10),right:true);Label(dc,"경과 시간",new(area.Left+area.Width/2,area.Bottom+10),centered:true);
    }
}

public sealed partial class MainWindow
{
    private readonly CombatDpsPlot combatDpsChart=new();
    private readonly CombatHistoryPlot combatDamageChart=new(),combatDefenseChart=new(){Defense=true};
    private readonly TextBlock combatTtk=T("",20),combatSplit=T("",14),combatNotes=T("",14),combatEventText=T("",14),combatSecondary=T("",14);
    private FrameworkElement? combatHistory,combatPlots,combatSnapshotPlot;
    private record CombatActionChoice(string Id,string Name);
    private static readonly CombatActionChoice[] CombatActions=[new("RubbleNullifierEnter","러블 · 무효화 영역 진입"),new("RubbleNullifierExit","러블 · 무효화 영역 이탈"),new("RubbleHostMigration","러블 · 호스트 이전으로 초기화"),new("MirrorsCast","홀 오브 미러즈 소환"),new("MirrorsEnd","홀 오브 미러즈 해제"),new("MirrorNullified","분신 개별 무효화 · 분신 번호"),new("JetStreamEnter","제트 스트림 범위 진입"),new("JetStreamLeave","제트 스트림 범위 이탈 · 3초 유지"),new("JetStreamLost","제트 스트림 버프 즉시 해제"),new("OperaBuff","오페라 버프 획득/갱신 · 멀티샷"),new("ForteBuff","포르테 버프 획득/갱신 · 근접 피해"),new("MetronomeBuffsLost","오페라·포르테 버프 해제"),new("WarcryCast","워크라이 시전 · 새 지속시간"),new("WarcryReenter","아군 워크라이 범위 재진입"),new("WarcryCasterDispel","워크라이 시전자 버프 해제 · 전원 해제"),new("WarcryCasterMeleeKill","아군 워크라이 시전자가 다른 적 근접 처치"),new("HeavyAttackMiss","허공에 강공격 · 콤보 소모·명중 없음"),new("CobraNullifierBubble","코브라 & 크레인 · 널리파이어 보호막 명중"),new("ExaltedSlideBlind","익절티드 블레이드 · 슬라이드 실명"),new("WarframeMeleeTransfer","워프레임 근접 전이 · 마구스 어그레스"),new("HeavyAnimationStart","강공격 동작 시작 · 비행 성분 판정"),new("HeavyAnimationEnd","강공격 동작 종료 · 비행 성분 판정"),new("AbilityEnergySpent","에너지 채널 · 어빌리티 시전·소모"),new("EnergyOrbPickup","에너지 오브 실제 습득 · 이럽션"),new("UniversalOrbPickup","범용 오브 실제 습득 · 이럽션"),new("ExaltedActivate","익절티드 어빌리티 소환"),new("ExaltedDeactivate","익절티드 어빌리티 해제"),new("OperatorOut","인레이지드 · 오퍼레이터 전이"),new("OperatorReturn","인레이지드 · 워프레임 복귀"),new("AbilityNullified","어빌리티 무효화 · 개조 초기화"),new("FallOutOfBounds","낙사 후 복귀 · 개조 초기화"),new("BlockedElement","액크 & 브런트 · 원소 공격 막기"),new("ShieldingElement","액크 & 브런트 · 실딩 원소 전달"),new("AbilityRadiationInfliction","현재 적에게 어빌리티 방사능 부여"),new("PlayerOverguardGain","오버가드 실제 획득 · 트루큘런스"),new("ExaltedAbilityCast","간접 익절티드 어빌리티 시전 · 재시전 콤보"),new("ValkyrFatalHit","발키르 치명상 · 분노로 죽음 방지"),new("VinquibusHeadshot","빈퀴부스 주무기 헤드샷 · 명중 수"),new("VinquibusPrimaryShot","빈퀴부스 주무기1발 · 명중 여부 무관"),new("ResidualEnter","레지듀얼 영역 진입 · 디멀센트"),new("ResidualTick","안에 있는 레지듀얼 영역의 실제 틱"),new("ResidualLeave","마지막 레지듀얼 영역 이탈 · 20초 잔여"),new("AbilityHeatInfliction","다른 적들에게 어빌리티 화염 적용 · 핫 샷"),new("GlaiveChainThrow","글레이브 회수 전 입력한 연속 투척"),new("GlaiveNewThrow","글레이브 새 투척 · 연속 입력 끊김"),new("TennokaiHeavyInput","텐노카이 기회 사용 · 다음 강공격 준비"),new("TennokaiFinish","텐노카이 동작 종료 · 트루스 성공/실패 판정"),new("TennokaiCancel","텐노카이 즉시 구르기 취소"),new("EquipPrimary","주무기로 전환"),new("EquipSecondary","보조무기로 전환"),new("BlockedDamage","적의 공격을 막음 · 피해량 입력"),new("SoulSwarmSlideInput","타츠 · 슬라이드로 저장 소울 발사"),new("SoulSwarmImpact","타츠 · 발사한 소울 1개 명중"),new("PlayerShieldDamage","내 실드가 피해를 받음"),new("PlayerMagnetic","내 워프레임에 자성 상태 발생 · 6초"),new("GroundFinisherKill","이 무기의 지상 마무리로 다른 적 처치"),new("EquipArchgun","아크윙 총기 장착 · 탱커"),new("IncarnonHeavyInput","인카논 발동용 강공격 사용"),new("IncarnonSlamInput","인카논 발동용 일반 슬램 사용"),new("IncarnonHeavySlamInput","인카논 발동용 강 슬램 사용"),new("HarmonicResonance","하모닉 레조넌스 버프 받음"),new("EvensongDevotion","이븐송 디보션 버프 받음"),new("CantareChorale","칸타레 코랄 버프 받음"),new("JadeEnsemble","제이드 앙상블 버프 발동"),new("AbilityCast","어빌리티 사용"),new("ChannelStart","지속형 어빌리티 유지 시작"),new("ChannelStop","지속형 어빌리티 유지 종료"),new("IncomingHit","적에게 피해를 받음"),new("PrimaryKill","주무기로 다른 적 처치"),new("MeleeKill","이 근접무기로 다른 적 처치"),new("HeavyMeleeKill","이 무기의 일반 강공격으로 다른 적 직접 처치"),new("MeleeAssist","이 근접무기로 다른 적 처치 도움"),new("SlideMeleeKill","이 무기의 슬라이드로 다른 적 처치"),new("FinisherHit","이 무기로 다른 적에게 마무리 명중"),new("EquipMelee","현재 근접무기로 전환"),new("FinisherKill","이 무기의 마무리로 다른 적 처치"),new("ParazonKill","파라존으로 다른 적 처치"),new("WallLatch","벽에 매달리기"),new("WallLatchTwoSeconds","벽 매달리기 2초 완료 · 프로톤 스냅"),new("PlayerShieldBreak","내 워프레임의 실드 파괴"),new("RollThroughTarget","구르기로 대상 통과 · 밀리 커린"),new("BlockedHit","공격 막기"),new("AmmoPickup","탄약 습득"),new("GroundSlide","근접무기를 든 채 연속 지상 슬라이드")];
    private readonly StackPanel encounterInputRows=new();
    private Expander? combatInputsExpander;
    private readonly List<(TextBox Time,ComboBox Kind,TextBox Value,ComboBox Element,TextBox Duration,TextBox Aware)> combatInputEditors=[];
    private readonly TextBox stanceHitTimes=new(){ToolTip="공속 1.0에서 타격 시각을 초 단위로 쉼표 구분. 사용자 가정이며 위키 출처로 표시하지 않습니다."};
    private FrameworkElement CreateCombatHistory()
    {
        combatSplit.TextWrapping=TextWrapping.Wrap;combatSplit.LineHeight=22;combatSplit.Margin=new(0,0,0,16);
        combatNotes.TextWrapping=TextWrapping.Wrap;combatEventText.TextWrapping=TextWrapping.Wrap;combatSecondary.TextWrapping=TextWrapping.Wrap;combatSecondary.LineHeight=22;
        var hint=T("현재 상태의 기본 타격 DPS · 지속/추가 범위 피해 제외 · 처치 이후 0",14,"#596B85");hint.TextWrapping=TextWrapping.Wrap;
        combatSnapshotPlot=Stack(T("시간별 화력 · 컨오 기여",16),combatDpsChart,hint);
        combatPlots=Stack(combatSnapshotPlot,ChartPair(Stack(T("누적 피해",16),combatDamageChart),Stack(T("남은 방어층",16),combatDefenseChart)));
        var panel=Stack(combatTtk,combatPlots,
            new Expander{Header="피해·회복·효과 상세 수치",Content=Stack(combatSplit,sceneResults,combatSecondary)},
            new Expander{Header="시간 계산 근거 · 지원 범위",Content=combatNotes},
            new Expander{Header="대표 표본의 전투 기록 · 사건 순서",Content=combatEventText});
        panel.Visibility=Visibility.Collapsed;combatHistory=panel;return panel;
    }
    private FrameworkElement CreateCombatTimeInputs()
    {
        stanceHitTimes.TextChanged+=(_,_)=>Recalculate();
        var add=Button("＋ 전투 중 행동 추가",()=>{AddCombatInput(new(0,"AbilityCast"));Recalculate();});
        var note=T("입력한 시점에 행동과 장비 효과를 적용합니다. 0초는 첫 타격 직전입니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        return combatInputsExpander=new Expander{Header="전투 중 행동 · 콤보와 아케인",Content=Stack(note,encounterInputRows,add,new Expander{Header="고급 · 위키에 없는 개별 타격 시각",Content=Field("공속 1.0 기준 · 쉼표로 구분 (초)",stanceHitTimes)})};
    }
    private void AddCombatInput(CombatInput value)
    {
        var time=new TextBox{Text=value.Time.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        var amount=new TextBox{Text=value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        var choice=new ComboBox{ItemsSource=CombatActions,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=value.Kind};
        var element=new ComboBox{ItemsSource=ResidualElementState.Elements.Select(t=>new KeyValuePair<DamageType,string>(t,Labels.Damage(t))).ToArray(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=value.Element??DamageType.Heat};
        var duration=new TextBox{Text=value.Duration?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"",ToolTip="적별 넉다운 회복시간은 위키에 없습니다. 시간 교전에서 컨오/힐링 리턴에 영향을 줄 때 사용할 사용자 지정 조건입니다. 첫 접촉만 계산할 때는 비워둘 수 있습니다."};
        var aware=new TextBox{Text=string.Join(", ",value.RecentlyAwareTargets??[]),ToolTip="가려진 적 중 직전에 나를 인지한 대상 번호를 쉼표로 입력합니다. 비어 있으면 가려진 적에게는 실명을 적용하지 않습니다. 정확한 인지 유효 시간은 원문에 없어 직접 지정하는 상황 조건입니다."};
        var awareField=(FrameworkElement)Field("가려져도 직전 인지한 적 · 대상 번호",aware);
        var durationField=(FrameworkElement)Field("넉다운 회복까지 · 사용자 지정 (초)",duration);
        var elementField=(FrameworkElement)Field("속성",element);
        var row=new StackPanel{Margin=new(0,4,0,12)};
        var actionRow=new DockPanel();var remove=Button("×",()=>{combatInputEditors.Remove((time,choice,amount,element,duration,aware));encounterInputRows.Children.Remove(row);Recalculate();});remove.ToolTip="행동 삭제";remove.Width=30;remove.Padding=new(4);remove.Margin=new(6,0,0,0);DockPanel.SetDock(remove,Dock.Right);actionRow.Children.Add(remove);actionRow.Children.Add(choice);row.Children.Add(actionRow);
        var parameters=new Grid();parameters.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});parameters.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var seconds=(FrameworkElement)Field("시점 (초)",time);seconds.Margin=new(0,0,6,0);parameters.Children.Add(seconds);
        var valueLabel=T("횟수",14);var valueField=Stack(valueLabel,amount);Grid.SetColumn(valueField,1);parameters.Children.Add(valueField);row.Children.Add(parameters);row.Children.Add(elementField);row.Children.Add(durationField);row.Children.Add(awareField);
        void RefreshValue()
        {
            var kind=choice.SelectedValue as string;var usesValue=kind is "MirrorNullified" or "CobraNullifierBubble" or "ExaltedSlideBlind" or "AbilityEnergySpent" or "BlockedElement" or "ShieldingElement" or "AbilityRadiationInfliction" or "PlayerOverguardGain" or "VinquibusHeadshot" or "ResidualTick" or "AbilityHeatInfliction" or "BlockedDamage" or "SoulSwarmImpact" or "PlayerShieldDamage" or "BlockedHit" or "AmmoPickup" or "GroundSlide" or "HeavyMeleeKill" or "IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput";
            valueField.Visibility=usesValue?Visibility.Visible:Visibility.Collapsed;valueLabel.Text=kind switch{"MirrorNullified"=>"무효화할 분신 번호 (1~4)","CobraNullifierBubble"=>"보호막 소유자의 대상 번호 (1부터)","ExaltedSlideBlind"=>"실명 실제 에너지 비용 · 기본25","AbilityEnergySpent"=>"효율 적용 후 실제 에너지 비용","BlockedElement" or "ShieldingElement"=>"실제로 흡수한 공격 횟수","AbilityRadiationInfliction"=>"실제 방사능 부여 횟수","PlayerOverguardGain"=>"획득한 오버가드","VinquibusHeadshot"=>"실제 헤드샷 명중 수 · 한 번에2충전","ResidualTick"=>"이 시각에 틱이 발생한 영역 수","AbilityHeatInfliction"=>"화염을 부여한 적 수 · 프록 수 아님","BlockedDamage"=>"막은 적 피해량 · 흡수 비율 적용 전","SoulSwarmImpact"=>"명중 대상 번호 (1부터)","PlayerShieldDamage"=>"실드 피해량 · 방어 적용 후","GroundSlide"=>"연속 이동 거리 (m)","HeavyMeleeKill" or "IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput"=>"동작 사용 전 콤보 (1~12×)",_=>"횟수"};
            amount.ToolTip=kind switch{"MirrorNullified"=>"선택한 분신만 제거합니다. 모두 사라져야 재시전할 수 있습니다.","ExaltedSlideBlind"=>"효율 적용 후 실제 비용입니다. 시점은 슬라이드 타격 시점이 아니라 실명이 발생한 시각입니다. 5m×어빌리티 거리, 6초×지속시간을 자동 적용합니다. 실명은 마무리 공격 노출을 만들지 않습니다.","AbilityEnergySpent"=>"실제 시전 비용을 저장하고 어빌리티 시전 발동 효과를 한 번 처리합니다. 지속 에너지 소모를 시전 횟수로 세지 않습니다.","BlockedElement" or "ShieldingElement"=>"화염·냉기·전기·독성의 기본 원소 공격을 실제 흡수한 횟수입니다. 충전당 원소17.5%, 합계4개까지 저장합니다. 피해량의17.5%를 저장하는 효과가 아닙니다.","AbilityRadiationInfliction"=>"현재 공격 대상에게 다른 어빌리티로 실제 부여한 방사능 횟수입니다. 폴아웃의 처치 드롭 확률은 상태 만료 후에도 유지되며, 현재 방사능 중첩은10개까지만 적용합니다. 생성된 오브는 자동으로 습득하지 않습니다.","PlayerOverguardGain"=>"실제로 새로 얻은 오버가드입니다. 획득 누적3000마다 플레이어 중심에서 바이러스10중첩 파동을 만듭니다. 보유한 총량이나 적에게 받은 피해량을 넣지 않습니다.","VinquibusHeadshot"=>"주무기의 산탄·관통으로 발생한 실제 머리 명중 횟수입니다. 스탠스 사격은 이 충전을 만들지 않습니다.","ResidualTick"=>"직접 서 있는 영역의 실제 피해 틱 시각입니다. 동시에 겹친 같은 속성 영역의 틱이면 그 개수를 입력합니다. 자동으로 첫 틱 시각을 만들지 않습니다.","AbilityHeatInfliction"=>"어빌리티가 같은 적에게 화염을 여러 중첩 부여해도 1회입니다. 다른 적들에게 발동한 핫 샷을 계산하며 현재 대상의 상태/피해를 임의로 추가하지 않습니다.","BlockedDamage"=>"실제로 막은 적의 피해입니다. 어벤징 트루스에 랭크별 흡수 비율을 적용하고, 막기 1회로 콤보와 검/방패 충전도 처리합니다. 아군 피해는 넣지 않습니다.","SoulSwarmImpact"=>"먼저 저장 소울을 발사한 뒤 실제 명중 시각과 대상 번호를 지정합니다. 30m 유도 범위·20m/s 최단 비행 시간을 검사하며 자동 대상 배정이나 곡선 비행 시간을 추정하지 않습니다.","PlayerShieldDamage"=>"오버실드부터 차감합니다. 실드에 실제 피해가 들어간 경우만 이지스·배리어를 추첨합니다. 남은 피해를 체력 피해로 추정하지 않습니다.","GroundSlide"=>"지면에서 한 번에 이어서 미끄러진 전체 거리입니다. 멈췄다가 다시 시작한 거리는 다른 행동으로 입력하세요.","IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput"=>"이 동작을 사용한 시각과 소모 전 콤보입니다. 발동 조건을 자동 검사하며 이미 변신 중이면 시간을 갱신하지 않습니다. 다른 대상 공격의 피해·콤보 소모는 현재 대상에 합산하지 않습니다.","HeavyMeleeKill"=>"다른 적을 처치한 강공격의 소모 전 콤보입니다. 현재 계산 대상의 콤보를 이 값으로 바꾸지는 않습니다.",_=>"이 시점에 막은 공격 또는 주운 탄약 묶음의 수입니다."};
            elementField.Visibility=kind is "BlockedElement" or "ShieldingElement" or "ResidualEnter" or "ResidualTick" or "ResidualLeave"?Visibility.Visible:Visibility.Collapsed;
            awareField.Visibility=kind=="ExaltedSlideBlind"?Visibility.Visible:Visibility.Collapsed;
            durationField.Visibility=kind is "EnergyOrbPickup" or "UniversalOrbPickup"?Visibility.Visible:Visibility.Collapsed;
            if(!usesValue)amount.Text="1";
        }
        RefreshValue();encounterInputRows.Children.Add(row);combatInputEditors.Add((time,choice,amount,element,duration,aware));
        duration.TextChanged+=(_,_)=>Recalculate();aware.TextChanged+=(_,_)=>Recalculate();
        element.SelectionChanged+=(_,_)=>Recalculate();time.TextChanged+=(_,_)=>Recalculate();amount.TextChanged+=(_,_)=>Recalculate();choice.SelectionChanged+=(_,_)=>{if(choice.SelectedValue is "ExaltedSlideBlind")amount.Text="25";RefreshValue();Recalculate();};
    }
    private void ApplyCombatInputs(CombatInput[] values){encounterInputRows.Children.Clear();combatInputEditors.Clear();foreach(var value in values)AddCombatInput(value);}
    private CombatInput[] ReadCombatInputs()=>combatInputEditors.Select(e=>new CombatInput(Number(e.Time,"행동 시각"),(string)e.Kind.SelectedValue,Number(e.Value,"행동 수량·거리"),e.Kind.SelectedValue is "BlockedElement" or "ShieldingElement" or "ResidualEnter" or "ResidualTick" or "ResidualLeave"?(DamageType)e.Element.SelectedValue:null,e.Kind.SelectedValue is "EnergyOrbPickup" or "UniversalOrbPickup"&&!string.IsNullOrWhiteSpace(e.Duration.Text)?Number(e.Duration,"사용자 지정 넉다운 회복시간"):null,e.Kind.SelectedValue is "ExaltedSlideBlind"?ReadAwareTargets(e.Aware.Text):null)).ToArray();
    private static int[] ReadAwareTargets(string text)=>text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(s=>int.TryParse(s,out var index)?index:throw new ArgumentException("직전 인지 대상은 번호를 쉼표로 구분하세요.")).ToArray();
    private double[]? ReadHitTimes()=>string.IsNullOrWhiteSpace(stanceHitTimes.Text)?null:stanceHitTimes.Text.Split(',').Select(value=>double.TryParse(value.Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds)?seconds:throw new ArgumentException("타격 시각은 초 단위 숫자를 쉼표로 구분하세요.")).ToArray();
    private bool ShowCombatHistory(CombatTimelineResult? result)
    {
        combatHistory!.Visibility=result is {Unsupported.Length:0}?Visibility.Visible:Visibility.Collapsed;
        ShowSceneResults(result);
        combatDpsChart.Set(result);combatDamageChart.Set(result);combatDefenseChart.Set(result);
        if(result is null||result.Unsupported.Length>0){combatNotes.Text="";return false;}
        string Q(double? value)=>value is {} x?$"{x:0.00}초":$"> {result.Seconds:0.##}초";
        combatPlots!.Visibility=result.Seconds>0?Visibility.Visible:Visibility.Collapsed;
        combatSnapshotPlot!.Visibility=result.HasSnapshotDps?Visibility.Visible:Visibility.Collapsed;
        if(!result.HasSnapshotDps||result.Events.Any(e=>e.Kind is "gunshotExplosion" or "epidemicImpact" or "glaiveBounce" or "ichorCloudHit" or "caustPoolHit" or "okinaImpact" or "okinaExplosion" or "ceramicImpact" or "ceramicExplosion" or "zenistarAura" or "zenistarExplosion" or "zenistarImpact" or "orviusHover" or "orviusExplosion" or "glaiveDetonation" or "glaiveContact")){equationBuild=null;equationHoverCache.Clear();equationView.SetEquation(DpsEquation.FromTimeline(result));}
        combatTtk.Text=$"처치 시간 · 중앙값 {Q(result.Ttk.Median)}";
        combatSplit.Text=$"직접 {result.DirectDamage:N1} + 상태 {result.StatusDamage:N1}\nP10 {Q(result.Ttk.P10)} · P90 {Q(result.Ttk.P90)} · 구간 내 처치 {result.Ttk.KillFraction:P1}";
        if(result.Seconds==0){combatTtk.Text="첫 접촉의 회복·명중 효과";combatSplit.Text=$"직접 피해 {result.DirectDamage:N1} · 즉발 상태 피해 {result.StatusDamage:N1}\n명중 후 상태 {result.Points.Last().StatusTypes:0.00}종 · 표본 평균";}
        if(result.Points.LastOrDefault() is {PlayerShields:{} own} end)combatSplit.Text+=$"\n교전 종료 · 내 실드 {own:N1} / 오버실드 {end.PlayerOvershields:N1} · 표본 평균";
        if(result.CriticalHealing is {} heal)combatSplit.Text+=$"\n치명타 흡혈 {heal:N1} · 회복 상한 적용 전\n인비거레이티드 {result.Points.Last().InvigoratedStacks:0.00}중첩 · 기본 최대 체력 +{result.Points.Last().InvigoratedStacks*5:0.0}% · 표본 평균";
        if(result.Points.LastOrDefault() is {PlayerArmor:{} armor} armorEnd)combatSplit.Text+=$"\n교전 종료 · 내 방어력 {armor:N1}"+(armorEnd.FortificationArmor>0?$" · 포티피케이션 +{armorEnd.FortificationArmor:N1}":"");
        else if(result.Points.LastOrDefault() is {FortificationArmor:>0} fort)combatSplit.Text+=$"\n포티피케이션 추가 방어력 {fort.FortificationArmor:N1}";
        if(result.Points.LastOrDefault() is {IncarnonRemaining:{} left} formEnd)combatSplit.Text+=$"\n인카논 잔여 {left:0.##}초"+(formEnd.IncarnonPunctureBonus>0?$" · 처치 관통 보너스 +{formEnd.IncarnonPunctureBonus:P0}":"")+(formEnd.IncarnonResilience>0?$" · 리질리언스 피해 감소 {formEnd.IncarnonResilience:P2}":"");
        if(result.Points.LastOrDefault()?.FinisherParkourBonus is {} parkour)combatSplit.Text+=$"\n래피드 컨클루전 · 파쿠르 +{parkour:P1} · 임무 누적 {parkour/.025:0.##}/16중첩";
        if(result.Points.LastOrDefault()?.JadeStatusDurationBonus is {} jade)combatSplit.Text+=$"\n제이드 무기 버프 · 상태 지속 +{jade:P0} · 이후 부여 상태에 적용";
        if(result.Points.LastOrDefault()?.AnimosityBonus is {} animosity)combatSplit.Text+=$"\n남은 애니모시티 · 다음 강공격 치확 모드 보너스 +{animosity:P0}";
        if(result.Points.LastOrDefault() is {SoulCharges:{} souls} soulEnd)combatSplit.Text+=$"\n소울 스웜 · 저장 {souls:0.##}개 / 비행 중 {soulEnd.SoulsInFlight:0.##}개 · 표본 평균";
        if(result.Points.LastOrDefault()?.MeleeRange is {} range)combatSplit.Text+=$"\n활성 효과 반영 사거리 {range:0.##}m · 표본 평균";
        if(result.Points.LastOrDefault() is {RubbleArmor:{} endingRubble} rubbleEnd)combatSplit.Text+=$"\n러블 {endingRubble:0.##} · 실제 회복 {rubbleEnd.RubbleHealing:0.##} · 생성 {rubbleEnd.RubbleDrops:0.##}개";
        if(result.Points.LastOrDefault() is {HologramCount:{} hc} mirrorsEnd)combatSplit.Text+=$"\n분신 {hc:0.##}개 · 멀레볼런스 +{mirrorsEnd.HallMalevolence:P0}";
        if(result.Points.LastOrDefault()?.WarcryRemaining is {} wc)combatSplit.Text+=$"\n워크라이 · 잔여 {wc:0.##}초 · 실제 처치·만료 반영";
        if(result.Points.LastOrDefault()?.ValkyrRage is {} rage)combatSplit.Text+=$"\n발키르 분노 · 피해 +{rage:0.##}% · 프레셔/컨오와 가산";
        if(result.Points.LastOrDefault()?.VinquibusPrecision is {} precision)combatSplit.Text+=$"\n빈퀴부스 · 정밀 {precision:0.##}/16 · 맹렬 {result.Points.Last().VinquibusFerocity:0.##}/16";
        if(result.Points.LastOrDefault()?.MagusAggressCharges is {} magus)combatSplit.Text+=$"\n마구스 어그레스 · {magus:0.##}/4회 남음 · 치명타 피해 모드와 가산";
        if(result.Enemies?.FirstOrDefault()?.DisarmFraction is {} disarm)combatSplit.Text+=$"\n무장 해제 효과 발동 {disarm:P0} · 대상1 · 전체 표본 중 비율";
        if(result.Points.LastOrDefault()?.HotShotStacks is {} hot)combatSplit.Text+=$"\n핫 샷 {hot:0.##}/50중첩 · 어빌리티 화염 · 표본 평균";
        if(result.Points.LastOrDefault()?.PowerThrowStacks is {} power)combatSplit.Text+=$"\n파워 스로우 · 연속 투척 {power:0.##}/3중첩";
        if(result.Points.LastOrDefault() is {TennokaiRemainingMinimum:not null} tennokaiEnd)
        {
            combatSplit.Text+=$"\n텐노카이 사용 {tennokaiEnd.TennokaiUses:0.##}회 · 기회 잔여 {tennokaiEnd.TennokaiRemainingMinimum:0.##}~{tennokaiEnd.TennokaiRemainingMaximum:0.##}초 · 표본 평균";
            if(tennokaiEnd.TennokaiRefunds+tennokaiEnd.TennokaiFailures>0)combatSplit.Text+=$"\n트루스 연쇄 성공 {tennokaiEnd.TennokaiRefunds:0.##} / 실패 {tennokaiEnd.TennokaiFailures:0.##}회 · 자기 화염 6초 원본 총량 {tennokaiEnd.TennokaiRawCurse:N1}";
        }
        if(result.Recovery is {} recovery)
        {
            string Estimate(SampleEstimate x)=>$"{x.Mean:N1}"+(x.Approximate95HalfWidth is >0 and var error?$" ±{error:N1} (평균의 정규 근사95%)":"");
            if(recovery.SanctiHealing is {} sancti)
            {
                var upper=recovery.SanctiHealingUpper!;
                combatSplit.Text+=Math.Abs(upper.Mean-sancti.Mean)>1e-8?$"\n생티 마기스타 흡혈 {sancti.Mean:N1}~{upper.Mean:N1} · 원문 불확실 범위":$"\n생티 마기스타 흡혈 {Estimate(sancti)}";
                combatSplit.Text+=" · 15m 아군 공유 · 회복 상한 적용 전";
            }
            if(recovery.HealingReturn is {} returned)
            {
                var upper=recovery.HealingReturnUpper!;
                combatSplit.Text+=Math.Abs(upper.Mean-returned.Mean)>1e-8?$"\n힐링 리턴 {returned.Mean:N1}~{upper.Mean:N1} · 움찔 중복의 원문 불확실 범위":$"\n힐링 리턴 회복 {Estimate(returned)} · 새 상태·접촉별 반영";
            }
            if(recovery.HeavyHealing is {} heavyHeal)combatSplit.Text+=$"\n라이프 스트라이크 흡혈 {Estimate(heavyHeal)} · {(result.Events.Any(e=>e.Kind=="heavyHealing"&&e.Description.Contains("제니스타"))?"직접 강공격·회수 중 원반 오라":"직접 강공격만")} · 회복 상한 적용 전";
            if(recovery.Energy is {} energy)combatSplit.Text+=$"\n브레이브 에너지 생성 {Estimate(energy)} · 최대치 적용 전";
            if(recovery.ExodiaHealing is {} healing)combatSplit.Text+=$"\n마이트 흡혈 {Estimate(healing)} · 회복 상한 적용 전";
            if(recovery.HostFinisherHealing is {Mean:>0} host)combatSplit.Text+=$"\n호스트/솔로 첫 마무리 추가 회복 {Estimate(host)}";
            if(recovery.DroppedUniversalOrbs is {} orbs)combatSplit.Text+=$"\n유니버설 폴아웃 · 범용 오브 {Estimate(orbs)}개 생성 · 각 체력50/에너지50 · 습득 전";
            if(recovery.NikanaHealing is {} nikana)combatSplit.Text+=$"\n아말감 다이큐 · 직접 피해 흡혈 {Estimate(nikana)} · 상태 틱 제외 · 최대 체력 상한 적용 전";
            if(recovery.HysteriaHealing is {} hysteria)combatSplit.Text+=$"\n히스테리아 · 명중 회복 {Estimate(hysteria)} · 최대 체력 상한 적용 전";
            if(recovery.IncarnonHealing is {} incarnonHealing)combatSplit.Text+=$"\n인카논 진화 · 체력 재생 {Estimate(incarnonHealing)} · 최대 체력 상한 적용 전";
            if(recovery.HealthRegeneration is {} regenerated)combatSplit.Text+=$"\n아케인 리퍼 · 체력 재생 {Estimate(regenerated)} · 최대 체력 상한 적용 전";
        }
        combatSecondary.Visibility=result.SecondaryBursts is {Length:>0}?Visibility.Visible:Visibility.Collapsed;
        combatSecondary.Text=string.Join("\n\n",(result.SecondaryBursts??[]).Select(s=>$"{s.Name} · 기대 발동 {s.ExpectedActivations:0.###}회\n주변 적에게 기대 피해 {s.ExpectedCenterDamage:N1}"+(s.ExpectedCenterDamageUpper is {} upper&&upper>s.ExpectedCenterDamage?$"~{upper:N1} (원문 불확실 범위)":"")+(s.ExpectedToxinTick>0?$" · 독성 틱 {s.ExpectedToxinTick:N1}":"")+$" · 반경 {s.Radius:0.#}m\n{s.Note}"));
        combatNotes.Text=result.Seconds==0?$"{result.Trials}회 표본 · 명중 직후까지 계산 · 지연된 상태 틱 제외 · 회복은 최대 체력 상한 적용 전":$"{result.Trials}회 표본 · 적이 죽으면 피해 누적 중단 · 평균 DPS는 선택한 전체 구간으로 나눈 값\n"+string.Join("\n",result.Notes);
        combatEventText.Text="아래는 첫 번째 표본의 기록입니다. 그래프의 표본 평균과 다릅니다.\n"+string.Join("\n",result.Events.Select(e=>$"{e.Time,6:0.000}초  {e.Description}"));
        return true;
    }
}
