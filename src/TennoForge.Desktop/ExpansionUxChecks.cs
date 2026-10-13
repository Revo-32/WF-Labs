using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    public object CheckExpansionUx()
    {
        var saved=ReadBuild();var checks=new List<string>();
        void Yes(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
        try
        {
            var id=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
            var roll=new RivenRoll(id,8,[new(RivenStat.Damage,97.8),new(RivenStat.StatusChance,53.5)],RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK");
            board.ClearMods();ApplyRivenWorkbench(roll);ShowRivens();
            rivenPage!.Measure(new Size(1760,840));rivenPage.Arrange(new Rect(0,0,1760,840));rivenPage.UpdateLayout();
            Descendants(rivenPage!).OfType<Button>().Single(b=>b.Content is string s&&s=="선택한 슬롯에 장착").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(page.Content==modPage&&ReadBuild().Riven?.Traits.SequenceEqual(roll.Traits)==true&&board.Slots[0]?.Rank==8&&current is not null,"리벤 실제 버튼 클릭 → 슬롯 장착·계산·모딩 복귀");
            var restored=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(saved);ApplyBuild(restored);Yes(ReadBuild().Riven!.Traits.SequenceEqual(roll.Traits)&&ReadBuild().Riven!.BaseDrain==6,"리벤 수치·코스트·극성 저장 복원");
            board.ChangeRank(0,7);Yes(page.Content==rivenPage&&board.Slots[0]!.Rank==8,"리벤 랭크 변경은 실제 표시 수치 편집으로 연결");ShowModding();
            AddCombatInput(new(1.25,"AbilityCast"));combatInputEditors[^1].Time.Text="2.5";combatInputEditors[^1].Kind.SelectedValue="WallLatch";
            Yes(ReadBuild().Encounter!.Inputs![^1]==new CombatInput(2.5,"WallLatch"),"한국어 행동 선택·시각 입력 → 저장 모델 연결");
            AddCombatInput(new(.75,"GroundSlide",27.5));
            var actionsSaved=JsonSerializer.Deserialize<CombatInput[]>(JsonSerializer.Serialize(ReadCombatInputs(),Catalog.Json),Catalog.Json)!;
            ApplyCombatInputs(actionsSaved);Yes(ReadCombatInputs()[^1]==new CombatInput(.75,"GroundSlide",27.5),"슬라이드 거리 소수값과 행동 종류 저장 복원");
            combatInputEditors[^1].Kind.SelectedValue="AmmoPickup";combatInputEditors[^1].Value.Text="3";
            Yes(ReadCombatInputs()[^1].Value==3,"탄약 습득 횟수 편집 → 사건 모델 연결");
            combatInputEditors[^1].Kind.SelectedValue="AbilityCast";Yes(ReadCombatInputs()[^1].Value==1,"수량이 없는 행동으로 변경하면 한 사건으로 초기화");
            combatInputEditors[^1].Kind.SelectedValue="HeavyMeleeKill";combatInputEditors[^1].Value.Text="12";
            var killInputs=JsonSerializer.Deserialize<CombatInput[]>(JsonSerializer.Serialize(ReadCombatInputs(),Catalog.Json),Catalog.Json)!;
            ApplyCombatInputs(killInputs);Yes(ReadCombatInputs()[^1]==new CombatInput(.75,"HeavyMeleeKill",12),"다른 적 강공격 처치의 소모 전 콤보와 시점 저장 복원");
            ApplyCombatInputs([]);Yes(ReadCombatInputs().Length==0,"전투 행동 초기화");
            ApplySampleSetting(4096);SetLightMode(true);
            Yes(ReadBuild().Encounter!.Samples==4096,"경량 모드는 계산 표본 수와 독립");
            var precisionSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplySampleSetting(512);ApplyBuild(precisionSaved);Yes(SelectedSamples==4096,"빌드 저장 복원 시 계산 표본 설정 유지");SetLightMode(false);
            ApplyBuild(saved);board.ClearMods();weaponClass.SelectedIndex=0;weapon.SelectedItem=catalog.Weapons.Single(w=>w.Id==ZawCatalog.Id("Sepfahn","Peye","Jai"));
            OpenEquipmentPicker("zaw",false);var panel=(FrameworkElement)equipmentPicker!.Child;panel.Measure(new Size(420,600));panel.Arrange(new Rect(0,0,420,600));
            var name=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Triumph").Name;
            Descendants(panel).OfType<Button>().Single(b=>b.Content is StackPanel p&&p.Children.OfType<TextBlock>().Any(t=>t.Text==name)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(ReadBuild().Melee!.Exodia is not null&&zawSlotName.Text==name,"조우 선택 → 엑소디아 클릭 장착·상시 슬롯 표시");
            OpenEquipmentPicker("zaw",false);panel=(FrameworkElement)equipmentPicker!.Child;panel.Measure(new Size(420,600));panel.Arrange(new Rect(0,0,420,600));
            Yes(Descendants(panel).OfType<TextBlock>().Any(t=>t.Text=="✓  "+name),"조우 아케인 선택창을 다시 열면 현재 장착 표시");CloseEquipmentPicker();
            weapon.SelectedItem=catalog.Weapons.Single(w=>w.Id==id);Yes(ReadBuild().Melee!.Exodia is null,"일반 무기로 전환하면 조우 전용 장착 해제");
            ApplyBuild(saved);EncounterPreviewForCheck(true);Yes(combatHistory!.Visibility==Visibility.Visible&&encounterResult!.Timeline!.Points.Any(p=>p.SnapshotDps>=p.WithoutCoDps),"5초 교전 → 시간 화력·컨오 기여 차트 표시");
            armor.Text="NaN";Yes(combatHistory.Visibility==Visibility.Collapsed&&encounterCharts!.Visibility==Visibility.Collapsed,"잘못된 입력 후 이전 전투 그래프 숨김");
            ApplyBuild(saved);AccuracyPreviewForCheck("eximus");
            Yes(encounterResult is {Unsupported.Length:0}&&encounterResult.Samples.Any(s=>s.Overguard is not null)&&encounterValue.Text!="계산 미지원","회귀 화면: 강철의 길 엑시무스·컨오·5초 교전 결과 표시");
            Yes(encounterResult!.Samples.All(s=>s.SnapshotDps is {} range&&range.Low<=range.High),"방어층 전환 이후 화력 비교 범위의 순서 유지");
            AccuracyPreviewForCheck("untimed");
            Yes(currentStance is {Duration:null}&&encounterResult is {DamageOnly:true}&&encounterHeading.Text=="동작 1회 · 대상 피해"&&rawMetric.Text=="시간 미기재","시간 없는 조우 강공격: 피해 결과 표시, 가짜 DPS 없음");
            AccuracyPreviewForCheck("glaive");
            Yes(currentStance?.Combo.AttackOverride=="Charged Throw Recall Explosion"&&currentStance.Combo.InputLabel.Contains("충전")&&currentStance.Hits.Length==1,"글레이브 충전 회수: 명시적 동작 선택·피해 결과 연결");
            var glaiveSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(saved);ApplyBuild(glaiveSaved);SettleSimulationForPreview();
            Yes(currentStance?.Combo.Key=="Heavy Charged"&&tennokaiChoice.Visibility==Visibility.Visible,"글레이브 회수 동작 저장 복원 및 텐노카이 설정 노출");
            attackMode.SelectedIndex=1;meleeAttack.SelectedItem=meleeAttack.Items.Cast<AttackChoice>().Single(c=>c.Id=="Charged Throw Recall Explosion");
            Yes(tennokaiChoice.Visibility==Visibility.Visible,"단일 회수 폭발도 공격 규칙에 따라 텐노카이 노출");
            AccuracyPreviewForCheck("tak");
            Yes(currentStance?.Combo.AttackOverride=="Heavy Attack"&&currentStance.Hits.Sum(h=>h.Count)==4&&currentStance.Hits.Where(h=>h.Calculation!.Melee!.ForcedProcs.Contains("Blast")).Sum(h=>h.Count)==3,"탁 앤 러그 고유 강공격: 4타·강제 폭발3회와 선택 UI 연결");
            AccuracyPreviewForCheck("spinner");
            Yes(encounterResult?.Timeline?.SecondaryBursts?.Single().ExpectedActivations>0&&combatSecondary.Visibility==Visibility.Visible&&combatSecondary.Text.Contains("중심"),"스피네렉스: 처치 폭발을 원래 대상 DPS와 구분해 주변 결과 표시");
            AccuracyPreviewForCheck("contagion");
            Yes(secondaryRows.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("컨테이전 · "))==3,"컨테이전: 직격·부착·폭발의 세 성분 결과 표시");
            AccuracyPreviewForCheck("contagion-flight");
            Yes(contagionPanel!.Visibility==Visibility.Visible&&ReadContagion().Single().Travel==30&&ReadEncounter().ScriptedAttacksOnly&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="contagionPhase")==6&&combatSnapshotPlot!.Visibility==Visibility.Collapsed,"컨테이전: 경로 편집·부착4틱/5초 폭발·자동 검 공격 제외·누적 피해 시각화");
            var projectileSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(projectileSaved);SettleSimulationForPreview();Yes(ReadContagion().Single()==new ContagionCollision(0,Travel:30)&&ReadEncounter().ScriptedAttacksOnly,"컨테이전 접촉 경로와 투사체 전용 계산 저장 복원");
            Yes(equationView.Equation?.Terms.Any(t=>t.Id=="time")==true&&Math.Abs(equationView.Equation.Evaluate()-encounterResult!.AverageDps!.Low)<1e-6,"투사체 교전 공식은 일반 검 DPS 대신 실제 누적 피해/시간과 일치");
            AccuracyPreviewForCheck("contagion-buffs");Yes(ReadContagionBuffs() is {VexArmor:3.5,Eclipse:2,VoidStrike:10}&&encounterResult?.Timeline?.Notes.Any(n=>n.Contains("외부 버프"))==true,"컨테이전:세 브라켓 버프 입력·계산 연결");
            var buffSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(buffSaved);Yes(ReadContagionBuffs()==buffSaved.Melee!.ContagionBuffs,"컨테이전:외부 버프 비율 저장 복원");
            AccuracyPreviewForCheck("epidemic");
            Yes(epidemicPanel!.Visibility==Visibility.Visible&&ReadEpidemic().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="epidemicImpact")==2,"에피데믹: 파동/끝 폭발 입력·서로 다른 피해 성분·시간 계산 연결");
            var epidemicSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(epidemicSaved);SettleSimulationForPreview();Yes(ReadEpidemic().SequenceEqual(epidemicSaved.Encounter!.Epidemic!)&&ReadEncounter().ScriptedAttacksOnly,"에피데믹 명중 시각·대상·성분 저장 복원");
            AccuracyPreviewForCheck("vinquibus-shot");
            Yes(equationView.Equation?.Terms.Any(t=>t.Id=="time")==true&&equationView.Equation.Limitations?.Any(n=>n.Contains("정밀"))==true&&Math.Abs(equationView.Equation.Evaluate()-encounterResult!.AverageDps!.Low)<1e-6,"빈퀴부스 공식: 지연 폭발 포함 실제 교전 DPS와 일치");
            AccuracyPreviewForCheck("glaive-bounce");
            Yes(encounterResult?.Timeline?.Unsupported.Length==0&&encounterResult.Timeline.Events.Count(e=>e.Kind=="glaiveBounce")==2&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"볼래틸: 투척 선택·주변 적 반사 폭발·집계 공식과 결과 일치");
            AccuracyPreviewForCheck("ichor-cloud");
            Yes(ichorPanel!.Visibility==Visibility.Visible&&encounterResult?.Timeline?.Unsupported.Length==0&&encounterResult.Timeline.Events.Count(e=>e.Kind=="ichorCloudCreate")==2&&encounterResult.Timeline.Events.Count(e=>e.Kind=="ichorCloudHit")==2&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"독 구름: 명중 입력·처치 연쇄·집계 공식과 결과 일치");
            var ichorSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(ichorSaved);SettleSimulationForPreview();Yes(ReadIchorContacts().SequenceEqual(ichorSaved.Encounter!.IchorClouds!),"독 구름 명중 시각·번호·대상 저장 복원");
            AccuracyPreviewForCheck("caust-pool");
            Yes(caustPoolPanel!.Visibility==Visibility.Visible&&ReadEncounter().ScriptedAttacksOnly&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="caustPoolHit")==4&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"카우스타시스트:체류 입력·1초 피해·실명 시나리오·실제 교전 공식");
            var poolSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(poolSaved);SettleSimulationForPreview();Yes(ReadPoolWindows().SequenceEqual(poolSaved.Encounter!.CaustacystPools!)&&ReadEncounter().ScriptedAttacksOnly,"웅덩이 생성·체류·실명 가정 저장 복원");
            AccuracyPreviewForCheck("okina");
            Yes(okinaPanel!.Visibility==Visibility.Visible&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="okinaCreate")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="okinaExplosion")==2&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"오키나:처치 생성·유도 입력·직격/주변 폭발·교전 공식");
            var okinaSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(okinaSaved);SettleSimulationForPreview();Yes(ReadOkinaFlights().SequenceEqual(okinaSaved.Encounter!.OkinaDaggers!),"오키나 발사·명중·단검/대상 번호·비행 거리 저장 복원");
            AccuracyPreviewForCheck("exalted-wave-flight");
            Yes(wavePanel!.Visibility==Visibility.Visible&&ReadWaves().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="exaltedWaveImpact")==2&&encounterScope.Text.Contains("조건부"),"익절티드:실제 스탠스 파동·관통·조건부 감쇠 결과 표시");
            var wavesSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(wavesSaved);SettleSimulationForPreview();Yes(ReadWaves().SequenceEqual(wavesSaved.Encounter!.ExaltedWaves!),"익절티드 파동:스탠스·입력 묶음·시각/거리·감쇠 분기 저장 복원");
            AccuracyPreviewForCheck("wolf-sledge");Yes(glaivePanel!.Visibility==Visibility.Visible&&glaiveEditors.Single().Airborne.IsEnabled==false&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="wolfBounce")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="glaiveDetonation")==1,"울프 슬레지:지상 투척·전진 폭발·기폭/귀환 강타 입력과 결과");
            var wolfSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(wolfSaved);Yes(ReadGlaives().Single().Contacts!.Last().Kind==GlaiveContactKind.Return&&ReadGlaives().Single().Detonation!.Time==.3,"울프 슬레지:귀환 접촉/기폭 시각 저장 복원");
            AccuracyPreviewForCheck("ack-shared");Yes(ReadBladeContacts().Single().BladeImpact==0&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="incarnonPhysicalHit")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="absorbedElementSpent")==1,"액크:같은 공격 칼날/투사체 입력·공동 원소 충전 소비");
            var ackSharedSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(ackSharedSaved);Yes(ReadBladeContacts().Single().BladeImpact==0&&bladeEditors.Single().Physical.IsChecked==true,"액크:칼날 시각/대상·체크박스 저장 복원");
            AccuracyPreviewForCheck("wind-control");Yes(encounterResult?.Timeline?.Enemies?.Select(e=>e.DisarmFraction).SequenceEqual(new double?[]{1,0})==true&&combatSplit.Text.Contains("무장 해제 효과"),"데저트 윈드:대상별 무장 해제와 오버가드 차단 결과 표시");
            AccuracyPreviewForCheck("blade-blind");
            Yes(ReadCombatInputs().Single().Kind=="ExaltedSlideBlind"&&ReadCombatInputs().Single().RecentlyAwareTargets!.SequenceEqual(new[]{2})&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="bladeBlind")==2,"실명:직전 인지·거리/지속·시간별 피해 연결");
            var blindSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(blindSaved);Yes(ReadCombatInputs().Single().RecentlyAwareTargets!.SequenceEqual(new[]{2}),"실명:시각·에너지 비용·직전 인지 대상 저장 복원");
            AccuracyPreviewForCheck("diwata-exilus");Yes(exilusSlotButton!.IsEnabled&&ReadBuild().Melee!.Exilus is not null&&exilusSlotName.Text!="슬롯 없음","디와타:엑실러스 상시 슬롯과 패리 장착 표시");
            AccuracyPreviewForCheck("vitrica-glass");
            Yes(glassPanel!.Visibility==Visibility.Visible&&ReadGlasses().Length==1&&ReadGlassSlams().Length==1&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="vitricaShatter")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="vitricaExplosion")==2&&encounterScope.Text.Contains("지상 파동 제외"),"비트리카:유리화/슬램 접촉·대상별 성분·제외 범위를 결과에 표시");
            var glassSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(glassSaved);SettleSimulationForPreview();Yes(ReadGlasses().SequenceEqual(glassSaved.Encounter!.VitricaGlasses!)&&ReadGlassSlams().SequenceEqual(glassSaved.Encounter.VitricaSlams!),"비트리카:유리화·슬램 번호·강 슬램 저장 복원");
            AccuracyPreviewForCheck("innodem-flight");
            Yes(innodemPanel!.Visibility==Visibility.Visible&&ReadInnodemContacts().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="innodemImpact")==2&&encounterScope.Text.Contains("조건부")&&equationView.Equation!.Context!.Contains("조건부"),"인노뎀:비행 자동 시각·관통·위치/컨오 가정을 결과와 공식에 표시");
            var innodemSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(innodemSaved);SettleSimulationForPreview();Yes(ReadInnodemContacts().SequenceEqual(innodemSaved.Encounter!.InnodemContacts!),"인노뎀:거리/발사 번호·위치/컨오 분기 저장 복원");
            AccuracyPreviewForCheck("incarnon-blade");
            Yes(bladePanel!.Visibility==Visibility.Visible&&ReadBladeContacts().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="incarnonBladeImpact")==2&&encounterResult.Timeline.Events.Count(e=>e.Kind=="incarnonBladeExplosion")==4,"유령 칼날:공중/슬라이드·직격/대상별 폭발·교전 연결");
            var bladeSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(bladeSaved);SettleSimulationForPreview();Yes(ReadBladeContacts().SequenceEqual(bladeSaved.Encounter!.IncarnonBlades!),"유령 칼날:발사 동작·시각·번호·대상·거리 저장 복원");
            AccuracyPreviewForCheck("magus-aggress");
            Yes(ReadArcanes()?.Operator?.Id==MagusAggressState.Id&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="magusCharge")==4&&combatSplit.Text.Contains("마구스"),"마구스 어그레스:오퍼레이터 슬롯·근접 전이·잔여 충전 연결");
            var magusSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(magusSaved);SettleSimulationForPreview();Yes(ReadArcanes()!.Operator==magusSaved.Arcanes!.Operator,"마구스 어그레스:오퍼레이터 슬롯·랭크 저장 복원");
            AccuracyPreviewForCheck("glaive-flight");
            Yes(glaivePanel!.Visibility==Visibility.Visible&&ReadGlaives().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="glaiveContact")==2&&encounterResult.Timeline.Events.Count(e=>e.Kind=="glaiveDetonation")==4,"글레이브:지정투척2회·직격/볼래틸/기폭·대상별피해 연결");
            var flightSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(flightSaved);SettleSimulationForPreview();Yes(JsonSerializer.Serialize(ReadGlaives(),Catalog.Json)==JsonSerializer.Serialize(flightSaved.Encounter!.GlaiveFlights!,Catalog.Json),"글레이브:발사·회수·입력·접촉·기폭 저장 복원");
            glaiveEditors[0].Airborne.IsChecked=true;Yes(ReadGlaives()[0].Airborne,"글레이브:공중 투척 토글 이벤트 연결");
            AccuracyPreviewForCheck("orvius");
            Yes(hoverPanel!.Visibility==Visibility.Visible&&ReadHovers().Single().UseInfobox&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="orviusHover")==2&&encounterResult.Timeline.Events.Count(e=>e.Kind=="orviusExplosion")==2&&encounterScope.Text.Contains("조건부")&&equationView.Equation!.Context!.Contains("상충"),"오르비우스:매달기·수동기폭·출처 상충을 결과와 공식에 표시");
            var hoverSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(hoverSaved);SettleSimulationForPreview();Yes(JsonSerializer.Serialize(ReadHovers(),Catalog.Json)==JsonSerializer.Serialize(hoverSaved.Encounter!.OrviusHovers!,Catalog.Json),"오르비우스:피해 시각·기폭·회수·데이터 표 선택 저장 복원");
            AccuracyPreviewForCheck("keratinos-source");Yes(currentStance?.Combo.Key=="Heavy Article"&&currentStance.Hits.Sum(h=>h.Count)==7&&currentStance.Stats.Limitations.Any(n=>n.StartsWith("미검증:")),"케라티노스:본문7타 선택·상충근거 표시");
            AccuracyPreviewForCheck("keratinos-miss");Yes(ReadCombatInputs().Any(e=>e.Kind=="HeavyAttackMiss")&&encounterResult?.Timeline?.Points.Last().MeleeRange>4,"허공강공격:입력종류·사거리확장 결과 연결");
            foreach(var componentMode in new[]{"pathocyst-components","sampotes-components","exec-components","azothane-components","discharge-components"}){AccuracyPreviewForCheck(componentMode);Yes(secondaryRows.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("미기재")),componentMode+": 확인된 성분과 미기재 값 구분");}
            AccuracyPreviewForCheck("vinquibus-flight");Yes(ReadWeaponActions().Single().Sequence==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="vinquibusImpact")==2,"빈퀴부스: 실제 위젯의 두 접촉/막기3배와 계산 연결");
            var shotSaved=ReadBuild();ApplyBuild(shotSaved);Yes(ReadWeaponActions().Single().Contacts![1].Time==.5,"빈퀴부스: 접촉 시각 저장 복원");
            AccuracyPreviewForCheck("jat-kusar");Yes(secondaryRows.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("잿 쿠사르")),"잿쿠사르:218폭발성분과미기재치명/상확 구분표시");
            AccuracyPreviewForCheck("destreza-flight");Yes(ReadRapierContacts().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="rapierImpact")==2&&encounterScope.Text.Contains("무작위 상태"),"레이피어:실제 명중 입력·컨오·상확 미기재 범위를 결과에 표시");
            rapierStatusAssumption.Text="100";Recalculate();var rapierSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(rapierSaved);SettleSimulationForPreview();
            Yes(ReadRapierContacts().All(c=>c.AssumedStatusChance==1)&&encounterResult?.Timeline?.Notes.Contains(DestrezaFlight.Assumed)==true,"레이피어:기본상확 가정 편집·저장복원·조건부 결과");
            AccuracyPreviewForCheck("weapon-buffs");
            Yes(ReadBuild().Melee?.Buffs==new WeaponBuffs(.5,2)&&encounterResult?.Timeline?.Unsupported.Length==0,"버프:실제 받은 증가율과 시간별 교전 연결");
            roarPercent.Text="75";eclipsePercent.Text="150";Recalculate();
            var weaponBuffSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(saved);ApplyBuild(weaponBuffSaved);Yes(ReadBuild().Melee?.Buffs==new WeaponBuffs(.75,1.5),"버프:실제 입력 편집·저장·복원");
            foreach(var item in new[]{(roarEnabled,"faction"),(eclipseEnabled,"eclipse")})
            {var row=(FrameworkElement)item.Item1.Parent;row.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.MouseEnterEvent});Yes(equationView.Highlighted.Contains(item.Item2),"버프:"+item.Item1.Content+" 실제 호버 수식 강조");row.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.MouseLeaveEvent});}
            roarEnabled.IsChecked=false;eclipseEnabled.IsChecked=false;
            Yes(ReadBuild().Melee?.Buffs is null&&!roarPercent.IsEnabled&&!eclipsePercent.IsEnabled,"버프:토글 해제·입력 비활성화·계산 제거");
            AccuracyPreviewForCheck("atlas-rubble");Yes(ReadRubblePickups().Length==2&&encounterResult?.Timeline?.Points.Last().RubbleHealing==1,"러블:실제습득/감소/회복 UI 연결");
            rubbleArmor.Text="1450";var rubbleSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(rubbleSaved);Yes(ReadMelee().ExternalEquipment?.AtlasRubble?.Armor==1450&&ReadRubblePickups()[1].MissingHealth==1,"러블:시작량과부족체력 저장복원");
            foreach(var mirrorMode in new[]{"mirrors-disc","mirrors-glaive","mirrors-wolf","mirrors-shield","mirrors-vinquibus"})
            {
                AccuracyPreviewForCheck(mirrorMode);Yes(encounterResult?.Timeline?.Events.Any(e=>e.Kind=="hologramHit")==true,mirrorMode+":분신투사체 실제UI 연결");var mirrorProjectileSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(mirrorProjectileSaved);
                Yes(mirrorMode switch{"mirrors-disc"=>ReadDiscs().Count(d=>d.Hologram>0)==4,"mirrors-glaive" or "mirrors-wolf"=>ReadGlaives().Count(f=>f.Hologram>0)==4,"mirrors-shield"=>ReadShieldFlights().Single().Hologram==1,_=>ReadWeaponActions().Single().Hologram==1},mirrorMode+":공격주체 저장복원");
            }
            AccuracyPreviewForCheck("mirrors");Yes(ReadHologramContacts().Length==4&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="hologramHit")==3,"미라지:실제분신4명중/무효화/만료 연결");
            mirrorsStrength.Text="175";var mirrorSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(mirrorSaved);Yes(ReadMelee().ExternalEquipment?.HallOfMirrors?.Strength==1.75&&ReadHologramContacts().Length==4,"미라지:위력·실제명중 저장복원");
            AccuracyPreviewForCheck("mirrors-flight");Yes(ReadWeaponActions().Single().Hologram==2&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="hologramHit")==true,"미라지:고유투사체 공격주체 연결");
            var mirrorFlightSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(mirrorFlightSaved);Yes(ReadWeaponActions().Single().Hologram==2,"미라지:투사체 공격주체 저장복원");
            AccuracyPreviewForCheck("jetstream-clock");Yes(ReadMelee().ExternalEquipment?.JetStreamRemaining==3&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="jetStream")==true,"제트스트림:3초잔여·재진입·거리감쇠 연결");
            jetStreamRemaining.Text="1.5";var jetSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(jetSaved);Yes(ReadMelee().ExternalEquipment?.JetStreamRemaining==1.5,"제트스트림:남은시간 실제편집 저장복원");
            AccuracyPreviewForCheck("metronome");Yes(ReadMelee().ExternalEquipment?.Metronome is {Opera:true,Forte:true}&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="metronome")==2,"메트로놈:별도버프·시간만료/재획득 연결");
            metronomeStrength.Text="150";var metroSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(metroSaved);Yes(ReadMelee().ExternalEquipment?.Metronome?.Strength==1.5,"메트로놈:실제위력변경 저장복원");
            AccuracyPreviewForCheck("opera-flight");Yes(ReadWeaponActions()[0].Contacts!.Last().Projectile==16&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="operaVolley")==true,"오페라:추가16번째단검과발사확률 연결");
            AccuracyPreviewForCheck("warcry");Yes(ReadMelee().ExternalEquipment?.Warcry is {EternalWarRank:3}&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="eternalWar")==true,"워크라이:실제설정/처치/시간만료 연결");
            warcryHelminth.IsChecked=true;warcryStrength.Text="150";var warcrySaved=ReadBuild();ApplyBuild(saved);ApplyBuild(warcrySaved);Yes(ReadMelee().ExternalEquipment?.Warcry is {Helminth:true,Strength:1.5},"워크라이:헬민스·위력 변경 저장복원");
            AccuracyPreviewForCheck("external-daikyu");Yes(ReadMelee().ExternalEquipment?.Primary=="Daikyu"&&encounterResult?.Timeline?.Recovery?.NikanaHealing is not null,"다이큐:주무기/아말감 선택과 실제 회복 연결");
            externalPrimaryRank.SelectedItem=0;Yes(ReadMelee().ExternalEquipment?.Rank==0,"다이큐:실제 랭크 변경");var gearSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(gearSaved);Yes(ReadMelee().ExternalEquipment==gearSaved.Melee!.ExternalEquipment,"외부 장비 저장 복원");
            AccuracyPreviewForCheck("external-argonak");Yes(encounterResult?.Timeline?.Events.Any(e=>e.Kind=="daggerArmor")==true,"아르고나크:기본 방어력 시간 변화 연결");
            AccuracyPreviewForCheck("external-jetstream");Yes(ReadMelee().ExternalEquipment?.JetStream==true&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="weaponProjectileImpact")==true,"제트 스트림:30m 네페리 명중 경로 연결");
            AccuracyPreviewForCheck("warfan-slam");Yes(ReadWeaponActions()[0].Mode=="HeavySlam"&&ReadWeaponActions()[0].Contacts![1].SlamHit==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="warfanSlam")==2,"워팬:두타격·모드·방어층 계산 연결");
            var fanSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(fanSaved);Yes(ReadWeaponActions()[0].Mode=="HeavySlam"&&ReadWeaponActions()[0].Contacts![1].SlamHit==2,"워팬:슬램 모드/둘째타 저장 복원");
            AccuracyPreviewForCheck("weapon-arum");Yes(ReadWeaponActions()[0].Contacts![0].PenetratedMeters==1.3&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="weaponProjectileImpact")==1,"아룸:관통두께/49m/s 단일가시 연결");
            AccuracyPreviewForCheck("weapon-nepheri");Yes(ReadWeaponActions()[0].Contacts!.Select(c=>c.Launched).SequenceEqual(new double?[]{.1,.35,.6,.85})&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="weaponProjectileImpact")==4,"네페리:4개 개별발사·각0.2초수명 경로");
            var consecutiveSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(consecutiveSaved);Yes(JsonSerializer.Serialize(ReadWeaponActions(),Catalog.Json)==JsonSerializer.Serialize(consecutiveSaved.Encounter!.WeaponActions,Catalog.Json),"화염구 개별 발사시각 저장 복원");
            AccuracyPreviewForCheck("weapon-agendus");Yes(ReadWeaponActions()[0].ExplosionModel=="Article"&&encounterScope.Text.Contains("조건부"),"아젠더스:본문5%/데이터6.95% 감쇠 분기 표시");
            AccuracyPreviewForCheck("weapon-grigori");Yes(ReadWeaponActions()[0].ProjectileExpires==1&&encounterScope.Text.Contains("조건부"),"그리고리:수명 가정·2번째 입력 표시");weaponActionEditors[0].Sliding.IsChecked=true;Yes(ReadWeaponActions()[0].Sliding,"그리고리:슬라이딩 발사 실제 토글");
            AccuracyPreviewForCheck("weapon-quassus");
            Yes(weaponActionPanel!.Visibility==Visibility.Visible&&ReadWeaponActions().Single().Contacts!.Length==4&&ReadEncounter().ScriptedAttacksOnly&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="weaponProjectileImpact")==2&&encounterScope.Text.Contains("조건부"),"콰서스:칼날/단검 공유 입력과 독립타격 조건 표시");
            var weaponActionSaved=ReadBuild();ApplyBuild(saved);ApplyBuild(weaponActionSaved);SettleSimulationForPreview();Yes(JsonSerializer.Serialize(ReadWeaponActions(),Catalog.Json)==JsonSerializer.Serialize(weaponActionSaved.Encounter!.WeaponActions,Catalog.Json),"고유 공격:입력/발사/단검/칼날 접촉 저장 복원");
            AccuracyPreviewForCheck("weapon-edun");Yes(ReadWeaponActions()[0].ExplosionModel=="Module"&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="weaponProjectileExplosion"&&Math.Abs(e.Time-1)<1e-6)==true,"에둔:출처 분기와 지연 폭발 연결");
            weaponActionEditors[0].Model.SelectedValue="Article";SettleSimulationForPreview();Yes(ReadWeaponActions()[0].ExplosionModel=="Article"&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="weaponProjectileExplosion"&&Math.Abs(e.Time-1.2)<1e-6)==true,"에둔:실제 출처 드롭다운 변경으로1초/5m 전환");
            AccuracyPreviewForCheck("weapon-korumm");Yes(encounterResult?.Timeline?.Events.Count(e=>e.Kind=="weaponBladeImpact")==2&&encounterResult.Timeline.Events.Count(e=>e.Kind=="weaponProjectileExplosion")==1,"코룸:직격과 주변 파동 대상 구분");
            AccuracyPreviewForCheck("cobra-sleep");Yes(ReadMelee().Conditions?.GetValueOrDefault("Baruuk")==1&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="cobraSleep")==1&&!encounterResult.Timeline.HasSnapshotDps,"코브라:바루크 선택·첫타 수면·불확실한 가상 후속 DPS 제외");
            AccuracyPreviewForCheck("shield-flight");
            Yes(shieldPanel!.Visibility==Visibility.Visible&&ReadShieldFlights().Length==1&&ReadShieldFlights()[0].FinisherWindowSeconds==.5&&encounterScope.Text.Contains("조건부")&&equationView.Equation!.Context!.Contains("조건부")&&ReadEncounter().ScriptedAttacksOnly&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="shieldImpact")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="shieldExplosion")==2,"시그마:방패 직격/수동 회수 폭발 입력과 다중 대상 결과");
            var shieldSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(shieldSaved);Yes(ReadShieldFlights().Single()==shieldSaved.Encounter!.ShieldFlights!.Single(),"시그마:투척 경로 저장 복원");
            AccuracyPreviewForCheck("zenistar-healing");
            Yes(ReadDiscs().Single().RetrieveStarted==1&&encounterResult?.Timeline?.Recovery?.HeavyHealing?.Mean>0&&combatSplit.Text.Contains("회수 중 원반 오라"),"제니스타:회수 시작·완료와 오라 흡혈 표시");
            var discHealingSaved=ReadBuild();ApplyBuild(discHealingSaved);Yes(ReadDiscs().Single().RetrieveStarted==1,"제니스타 회수 시작 저장 복원");
            AccuracyPreviewForCheck("zenistar");
            Yes(discPanel!.Visibility==Visibility.Visible&&ReadEncounter().ScriptedAttacksOnly&&ReadDiscs().Length==1&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="zenistarLaunch")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="zenistarAura")==6&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"제니스타:원반 입력·주변 대상 오라·회수 뒤 명중 제외·교전 공식");
            var discSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(discSaved);SettleSimulationForPreview();Yes(JsonSerializer.Serialize(ReadDiscs(),Catalog.Json)==JsonSerializer.Serialize(discSaved.Encounter!.ZenistarDiscs!,Catalog.Json),"제니스타:발사/배치/회수·접촉 시각·래스풀 저장 치확 복원");
            AccuracyPreviewForCheck("ceramic");
            Yes(ceramicPanel!.Visibility==Visibility.Visible&&ReadEncounter().ScriptedAttacksOnly&&ReadCeramicContacts().Length==2&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="ceramicLaunch")==1&&encounterResult.Timeline.Events.Count(e=>e.Kind=="ceramicImpact")==2&&Math.Abs(equationView.Equation!.Evaluate()-encounterResult.AverageDps!.Low)<1e-6,"세라믹:발사1회·단검2개·직격/폭발·교전 공식 연결");
            var ceramicSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(ceramicSaved);SettleSimulationForPreview();Yes(ReadCeramicContacts().SequenceEqual(ceramicSaved.Encounter!.CeramicDaggers!)&&ReadEncounter().ScriptedAttacksOnly,"세라믹:발사 묶음·단검·대상·시각·거리·표면 충돌 저장 복원");
            AccuracyPreviewForCheck("rakta");
            Yes(playerShieldFields!.Visibility==Visibility.Visible&&ReadArcanes() is {MaximumShields:1000,Shields:199,ShieldRechargeEnabled:false}&&encounterResult?.Timeline?.Points.Last().PlayerShields>200,"락타·리탈리에이션: 내 실드 입력과 전투 중 증가 연결");
            var raktaSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(raktaSaved);SettleSimulationForPreview();
            Yes(ReadArcanes() is {MaximumShields:1000,MaximumOvershields:1200,ShieldRechargeEnabled:false},"워프레임 실드·한도·재생 설정 저장 복원");
            AccuracyPreviewForCheck("stealth");
            Yes(current?.Melee is {StealthBonus:7}&&dynamicConditions.Children.Count>0&&meleeConditions.ContainsKey("StealthCooldown"),"은신 보너스 표시식과 시작 대기 입력 연결");
            var toggle=Descendants(dynamicConditions).OfType<CheckBox>().Single(x=>x.Content is string s&&s=="워프레임 투명 상태 유지");toggle.IsChecked=false;
            Yes(ReadBuild().Melee!.Conditions!.GetValueOrDefault("Invisible")==0&&current?.Melee?.StealthBonus==0&&!meleeConditions.ContainsKey("StealthCooldown"),"투명 상태 해제 → 은신 보너스와 대기 입력 즉시 갱신");
            AccuracyPreviewForCheck("stealth");var stealthSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(stealthSaved);SettleSimulationForPreview();
            Yes(ReadBuild().Melee!.Conditions!.GetValueOrDefault("Invisible")==1&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="stealthCooldown")==true,"은신 설정 저장 복원 및 시간 경로 대기 사건 연결");
            AccuracyPreviewForCheck("vitrica");
            Yes(secondaryRows.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("비트리카 · "))==2&&current?.StatusChance==1.5,"비트리카 원래 대상/주변 대상 구분 및 프로톤 스냅 고정 상확 연결");
            AddCombatInput(new(2,"WallLatchTwoSeconds"));Yes(ReadCombatInputs().Last().Kind=="WallLatchTwoSeconds","프로톤 스냅 벽 유지 완료 행동 선택과 저장 연결");
            AccuracyPreviewForCheck("hirudo");Yes(encounterResult?.Timeline is {CriticalHealing:>0}&&combatSplit.Text.Contains("기본 최대 체력")&&combatSplit.Text.Contains("회복 상한"),"히루도: 치명타 흡혈·기본 체력 중첩과 실제 회복 상한 구분 표시");
            AccuracyPreviewForCheck("killbuffs");Yes(encounterResult?.Timeline?.Events.Any(e=>e.Kind=="pennant")==true&&!meleeConditions.ContainsKey("PennantKills")&&!meleeConditions.ContainsKey("BerserkerStacks")&&ReadCombatInputs().Length==2,"처치 버프: 행동 입력→화력 그래프, 영구 수동 중첩 입력 제거");
            AccuracyPreviewForCheck("lifestrike");Yes(encounterResult?.Timeline?.Recovery?.HeavyHealing?.Mean>0&&combatSplit.Text.Contains("직접 강공격만"),"라이프 스트라이크: 지속 피해를 제외한 회복량 표시");
            AccuracyPreviewForCheck("harmony");Yes(Math.Abs((encounterResult?.Timeline?.Points.Last().JadeStatusDurationBonus??-1)-.6)<1e-9&&combatSplit.Text.Contains("제이드 무기 버프")&&ReadCombatInputs().Length==3,"제이드 버프: 행동 입력·세 버프 결합·지속 보너스 표시");
            AccuracyPreviewForCheck("firstrecovery");Yes(encounterResult?.Timeline is {Seconds:0,Recovery.HealingReturn.Mean:>0}&&combatSplit.Text.Contains("새 상태·접촉별 반영")&&combatPlots!.Visibility==Visibility.Collapsed&&samplingPrecision.Text.Contains("평균 피해"),"첫 접촉: 회복·산탄 상태 전이·평균 오차를 표시하고 시간 그래프 숨김");
            var historySections=Descendants(combatHistory!).OfType<Expander>().ToArray();
            var historyOpen=historySections.Select(e=>e.IsExpanded).ToArray();foreach(var section in historySections)section.IsExpanded=true;
            var layoutRoot=(FrameworkElement)Content;layoutRoot.Measure(new Size(1760,920));layoutRoot.Arrange(new Rect(0,0,1760,920));layoutRoot.UpdateLayout();
            Yes(combatSplit.ActualHeight>0&&combatNotes.TranslatePoint(new(0,0),combatHistory!).Y>=combatSplit.TranslatePoint(new(0,combatSplit.ActualHeight),combatHistory!).Y+8,"회복 수치와 계산 주석 사이 간격·줄바꿈 검증");
            for(var i=0;i<historySections.Length;i++)historySections[i].IsExpanded=historyOpen[i];
            AccuracyPreviewForCheck("healingreturn");Yes(encounterResult?.Timeline?.Recovery?.HealingReturn?.Mean>0&&combatSplit.Text.Contains("새 상태·접촉별 반영"),"힐링 리턴: 실제 상태·산탄별 회복량 표시");
            AccuracyPreviewForCheck("sancti");Yes(encounterResult?.Timeline?.Recovery?.SanctiHealing?.Mean>0&&ReadBuild().Melee!.Conditions!.GetValueOrDefault("SanctiHealingCombo")==12&&combatSplit.Text.Contains("15m 아군 공유"),"생티 마기스타: 기존 임무 흡혈 입력·회복·공유 반경 표시");
            AccuracyPreviewForCheck("obexwave");Yes(secondaryRows.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("오벡스 · 마무리 파동"))==currentStance!.Combo.HitCount,"오벡스: 실제 선택한 마무리 접촉마다 주변 파동 표시");
            AccuracyPreviewForCheck("incarnonclock");Yes(ReadBuild().Melee!.IncarnonSecondsRemaining==2&&Math.Abs((encounterResult?.Timeline?.Points.Last().IncarnonRemaining??0)-88)<1e-8&&combatSplit.Text.Contains("인카논 잔여"),"인카논 잔여 시간 입력·만료·다시 발동·결과 표시");
            AccuracyPreviewForCheck("scene");Yes(encounterResult?.Timeline?.Enemies is {Length:3}&&nearbyEditors.Count==2&&sceneResults.Visibility==Visibility.Visible,"주변 적: 실제 장면 계산·대상별 피해·설정 행 연결");
            var sceneSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(sceneSaved);SettleSimulationForPreview();
            Yes(ReadBuild().Encounter!.TargetOrder!.SequenceEqual(new[]{0,1,2})&&ReadBuild().Encounter!.NearbyEnemies![1].X==10,"주변 적 좌표·공격 순서 저장 복원");
            blockedSightInput.Text="1-";
            Descendants(nearbyEditors[0].Row).OfType<Button>().Single(b=>b.Content is string s&&s=="×").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(nearbyEditors.Count==2,"잘못 입력한 시야 차단: 삭제 중 예외 없이 입력 오류 보존");blockedSightInput.Text="";
            var staleNearby=nearbyEditors[0];nearbyEditors[0]=staleNearby with {Seed=staleNearby.Seed with {Shields=123,Overguard=456}};
            staleNearby.Enemy.SelectedIndex=1;
            Yes(ReadNearby()[0].Shields is null&&ReadNearby()[0].Overguard is null,"적 선택 변경: 이전 파일의 숨은 방어 수치가 새 적을 덮어쓰지 않음");
            ApplyCombatScene(sceneSaved.Encounter);
            Descendants(nearbyEditors[0].Row).OfType<Button>().Single(b=>b.Content is string s&&s=="×").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));SettleSimulationForPreview();
            Yes(nearbyEditors.Count==1&&ReadBuild().Encounter!.TargetOrder!.SequenceEqual(new[]{0,1})&&ReadBuild().Encounter!.NearbyEnemies![0].X==10,"주변 적 실제 삭제 버튼·번호와 공격 순서 재정렬");
            AddAttackSequence("Normal Attack");AddAttackSequence("Heavy Attack");SettleSimulationForPreview();Yes(ReadBuild().Encounter!.AttackOrder!.SequenceEqual(new[]{"Normal Attack","Heavy Attack"}),"공격 동작 순서: 일반/강공격 실제 선택 모델 연결");
            var sequenceSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(sequenceSaved);SettleSimulationForPreview();Yes(attackSequenceChoices.Count==2&&ReadBuild().Encounter!.AttackOrder![1]=="Heavy Attack","공격 동작 순서 저장 복원");
            AccuracyPreviewForCheck("destreza");Yes(secondaryRows.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("데스트레자 · 유령 레이피어"))==2&&secondaryRows.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("기본 상확 미기재")),"데스트레자: 준비 동작별 피해와 상확 근거 범위를 별도 표시");
            AccuracyPreviewForCheck("animosity");Yes(Math.Abs((encounterResult?.Timeline?.Points.Last().AnimosityBonus??-1)-2.52)<1e-9&&ReadBuild().Melee!.Conditions!.GetValueOrDefault("AnimosityStacks")==6&&combatSplit.Text.Contains("남은 애니모시티"),"애니모시티: 시작 중첩 저장·글레이브 투척 후 보존과 치확 보너스 표시");
            var animositySaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(animositySaved);SettleSimulationForPreview();
            Yes(ReadBuild().Melee!.Conditions!.GetValueOrDefault("AnimosityStacks")==6&&meleeConditions.ContainsKey("AnimosityStacks"),"애니모시티 시작 중첩 저장 복원");
            AccuracyPreviewForCheck("obex");Yes(Math.Abs((encounterResult?.Timeline?.Points.Last().FinisherParkourBonus??-1)-.4)<1e-9&&ReadBuild().Melee!.Conditions!.GetValueOrDefault("FinisherStacks")==14&&combatSplit.Text.Contains("래피드 컨클루전"),"오벡스: 임무 누적·마무리/파라존 공동 카운터·파쿠르 보너스 표시");
            AccuracyPreviewForCheck("fortification");Yes(encounterResult?.Timeline?.Points.Last().PlayerArmor>300&&ReadArcanes()?.PlayerArmor==300&&!meleeConditions.ContainsKey("Armor450"),"포티피케이션: 방어력 입력 저장·진화 조건 자동 판정·중복 수동 토글 제거");
            AccuracyPreviewForCheck("framearmor");Yes(encounterResult?.Timeline?.Recovery?.HealthRegeneration?.Mean>0&&combatSplit.Text.Contains("리퍼")&&playerArmorField?.Visibility==Visibility.Visible&&ReadArcanes()?.PlayerArmor==300,"방어력 아케인: 발동 전 방어력 입력·탱커/리퍼 후 실제 방어력·재생량 표시");
            AccuracyPreviewForCheck("tatsu");Yes(encounterResult?.Timeline?.Events.Count(e=>e.Kind=="soulImpact")==2&&combatSplit.Text.Contains("소울 스웜")&&meleeConditions.ContainsKey("SoulCharges")&&ReadCombatInputs().Length==4,"타츠: 저장 소울·발사/명중 행동·대상 번호 저장·계산 결과 연결");
            AccuracyPreviewForCheck("tennokai");Yes(encounterResult?.Timeline?.Points.Last().TennokaiUses==1&&combatSplit.Text.Contains("텐노카이 사용")&&ReadCombatInputs().Single().Kind=="TennokaiHeavyInput","텐노카이: 실제 입력별 기회 생성·사용 행동·콤보 보존·화면 표시");
            AccuracyPreviewForCheck("target-debuffs");Yes(ReadMelee().Debuffs is {Length:2}&&ReadNearby().Single().Debuffs?.Single().Strength==2&&encounterResult?.Timeline?.Unsupported.Length==0,"대상 효과:기본 대상과 주변 적의 개별 설정·계산 연결");
            AccuracyPreviewForCheck("whip-dome");Yes(ReadNearby().Last().Debuffs?.Single().Group==2&&encounterResult?.Timeline?.Enemies?.Single(e=>e.Id=="same-dome").DirectDamage>0&&encounterResult.Timeline.Enemies.Single(e=>e.Id=="other-dome").DirectDamage==0,"윕클로:돔 번호 저장과 전파 대상 분리");
            AccuracyPreviewForCheck("energy-channel");Yes(ReadCombatInputs().Single().Value==25&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="energyChannelUse")==2,"에너지 채널:시전 실제 비용 저장·시간 계산 소비 연결");
            AccuracyPreviewForCheck("target-debuffs");var debuffSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(debuffSaved);SettleSimulationForPreview();Yes(ReadMelee().Debuffs!.Single(d=>d.Code=="Petrify").Remaining==3&&ReadNearby().Single().Debuffs!.Single().Code=="SplinterStorm","대상 어빌리티 효과·시전자 위력·잔여시간 저장 복원");
            AccuracyPreviewForCheck("eruption");Yes(ReadCombatInputs().Single() is {Kind:"EnergyOrbPickup",Duration:1}&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="eruptionKnockdown")==true,"이럽션:실제 오브 습득·사용자 지정 회복시간·넉다운 사건");
            var eruptionSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(eruptionSaved);SettleSimulationForPreview();Yes(ReadCombatInputs().Single().Duration==1,"넉다운 사용자 조건 저장 복원");
            AccuracyPreviewForCheck("enraged-lifetime");Yes(ReadCombatInputs().Length==3&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="exaltedActivation")==2&&encounterResult.Timeline.Events.Any(e=>e.Kind=="exaltedActivation"&&e.Amount==0)&&encounterResult.Timeline.Events.Any(e=>e.Kind=="exaltedActivation"&&e.Amount==1),"인레이지드:조기 해제·재사용 대기·소환 재개 동작");
            var lifetimeSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(lifetimeSaved);SettleSimulationForPreview();Yes(ReadCombatInputs()[1] is {Kind:"ExaltedActivate",Time:1.5},"익절티드 수명 행동 저장 복원");
            AccuracyPreviewForCheck("amanata");Yes(ReadBuild().Melee!.Conditions!.GetValueOrDefault("AmanataHits")==29&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="amanataRoll")==true&&encounterResult.Timeline.Events.Any(e=>e.Kind=="amanataPressure"),"아마나타: 시작 누적·주사위 교체·프레셔 첫 대상 상태 사건");
            var amanataSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(amanataSaved);SettleSimulationForPreview();Yes(ReadBuild().Melee!.Conditions!.GetValueOrDefault("AmanataHits")==29,"아마나타 누적 명중 저장 복원");
            AccuracyPreviewForCheck("ackbrunt");Yes(ReadCombatInputs().Single() is {Kind:"BlockedElement",Value:4,Element:DamageType.Heat}&&encounterResult?.Timeline?.Events.Any(e=>e.Kind=="absorbedElementSpent")==true,"액크 & 브런트: 막은 원소·횟수 선택·다음 공격 소비");
            var ackSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(ackSaved);SettleSimulationForPreview();Yes(ReadCombatInputs().Single().Element==DamageType.Heat,"막은 원소 저장 복원");
            AccuracyPreviewForCheck("fallout");Yes(ReadCombatInputs().Single() is {Kind:"AbilityRadiationInfliction",Value:60}&&encounterResult?.Timeline?.Recovery?.DroppedUniversalOrbs?.Mean>0&&combatSplit.Text.Contains("범용 오브"),"폴아웃: 실제 어빌리티 방사능·오브 생성 결과·습득 구분");
            var falloutSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(falloutSaved);SettleSimulationForPreview();Yes(ReadCombatInputs().Single().Value==60,"폴아웃 실제 방사능 횟수 저장 복원");
            AccuracyPreviewForCheck("truculence");Yes(ReadCombatInputs().Single() is {Kind:"PlayerOverguardGain",Value:100}&&ReadBlockedSight().Single()==new CombatSightBlock(-1,1)&&encounterResult?.Timeline?.Events.Count(e=>e.Kind=="truculenceHit")==1,"트루큘런스: 실제 오버가드 입력·플레이어 시야·바이러스 파동");
            var trucSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(trucSaved);SettleSimulationForPreview();Yes(ReadCombatInputs().Single().Value==100&&blockedSightInput.Text.Contains("나-2"),"트루큘런스 행동·플레이어 시야 저장 복원");
            AccuracyPreviewForCheck("exalted-augment");Yes(ReadBuild().Melee is {AbilityAugment.ModId:ExaltedAugments.Chromatic,ChromaticElement:DamageType.Heat,AbilityDuration:1.5},"개조 모드는 무기 슬롯과 별도·발광 원소 설정");
            // Smoke renders an offscreen Window. IsVisible remains false at
            // that root; check the selected page and explicit visibility,
            // then inspect the separately rendered full-window artifact.
            Yes(page.Content==modPage&&chromaticField!.Visibility==Visibility.Visible&&exaltedFields!.Visibility==Visibility.Visible&&weaponEffectsExpander!.IsExpanded,"모딩 페이지·개조 펼침·발광 원소 표시 상태");
            chromaticElementChoice.SelectedIndex=3;abilityAugmentRank.SelectedIndex=1;SettleSimulationForPreview();var augmentedSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(augmentedSaved);SettleSimulationForPreview();Yes(ReadBuild().Melee is {AbilityAugment.Rank:1,ChromaticElement:DamageType.Toxin,AbilityDuration:1.5},"개조 랭크·원소·어빌리티 지속 저장 복원");
            AccuracyPreviewForCheck("exalted");Yes(ReadBuild().Melee is {AbilityRank:2,AbilityStrength:1.75}&&encounterResult?.Timeline?.Recovery?.HysteriaHealing?.Mean>0&&combatSplit.Text.Contains("발키르 분노")&&!catalyst.IsEnabled&&!stance.IsEnabled,"익절티드: 랭크·위력·분노·회복·고정 스탠스 연결");
            var exaltedSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(exaltedSaved);SettleSimulationForPreview();Yes(ReadBuild().Melee is {AbilityRank:2,AbilityStrength:1.75}&&ReadCombatInputs().Single().Kind=="ValkyrFatalHit","익절티드 설정과 전투 행동 저장 복원");
            AccuracyPreviewForCheck("vinquibus");Yes(ReadBuild().Capacity!.SlotPolarities[1]=="Omni"&&ReadCombatInputs()[0].Value==2&&encounterResult?.Timeline?.Points.Last().VinquibusFerocity==3&&combatSplit.Text.Contains("빈퀴부스"),"빈퀴부스:옴니 슬롯·충전·주무기 입력·결과 연결");
            AccuracyPreviewForCheck("residual");Yes(encounterResult?.Timeline?.Events.Count(e=>e.Kind=="demulcent")==3&&ReadCombatInputs()[1].Element==DamageType.Toxin&&ReadCombatInputs()[1].Value==3&&combatSplit.Text.Contains("핫 샷 20"),"핫 샷·디멀센트: 어빌리티 대상 수·영역 속성·실제 틱·결과 표시");
            var residualSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(residualSaved);SettleSimulationForPreview();Yes(ReadCombatInputs()[1].Element==DamageType.Toxin&&ReadCombatInputs()[1].Value==3,"레지듀얼 행동 속성·수량 저장 복원");
            AccuracyPreviewForCheck("brave");Yes(encounterResult?.Timeline?.Recovery?.Energy?.Mean>0&&combatSplit.Text.Contains("브레이브 에너지 생성")&&ReadCombatInputs().Any(i=>i.Kind=="ChannelStart"),"브레이브: 행동 입력·에너지 생성량·채널링 중단 표시");
            AccuracyPreviewForCheck("might");Yes(encounterResult?.Timeline?.Recovery?.ExodiaHealing?.Mean>0&&combatSplit.Text.Contains("마이트 흡혈"),"마이트: 파라존 발동과 직접/상태 흡혈 표시");
            AccuracyPreviewForCheck("thalys");Yes(secondaryRows.Children.OfType<TextBlock>().Count(t=>t.Text.StartsWith("탈리스 · "))==2,"탈리스: 가시 재발동과 성장 꿰뚫기의 다른 CO 브라켓을 별도 성분으로 표시");
            AccuracyPreviewForCheck("finisher");Yes(currentStance is {Combo.Key:"Finisher Front",Duration:null}&&currentStance.Hits.Length==2&&encounterResult is {DamageOnly:true},"앞 마무리 선택 → 럼블잭2타·시간 미기재 동작 피해 표시");
            var finisherSaved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;ApplyBuild(saved);ApplyBuild(finisherSaved);SettleSimulationForPreview();Yes(currentStance?.Combo.Key=="Finisher Front","마무리 동작 저장·복원");
            AccuracyPreviewForCheck(null);
            return new{passed=true,checks};
        }
        finally{CloseEquipmentPicker();ApplyBuild(saved);ShowModding();SettleSimulationForPreview();}
    }
    private void ScrollScenarioTo(FrameworkElement section)
    {
        if(scenarioScroller is null)return;scenarioScroller.UpdateLayout();
        var top=section.TranslatePoint(new Point(0,0),scenarioScroller).Y+scenarioScroller.VerticalOffset;
        scenarioScroller.ScrollToVerticalOffset(Math.Max(0,top));scenarioScroller.UpdateLayout();
    }
    private Build? accuracyPreviewSaved;
    private FrameworkElement? pendingScenarioPreview;
    private bool pendingSecondaryPreview;
    private void ScrollScenarioForPreview(FrameworkElement target)
    {
        pendingScenarioPreview=target;
        if(scenarioScroller is null)return;
        UpdateLayout();scenarioScroller.UpdateLayout();
        var offset=target.TranslatePoint(new Point(0,0),scenarioScroller).Y+scenarioScroller.VerticalOffset;
        scenarioScroller.ScrollToVerticalOffset(offset);scenarioScroller.UpdateLayout();
    }
    internal void SettleScenarioPreviewForCheck()
    {
        if(pendingScenarioPreview is {} target){ScrollScenarioForPreview(target);pendingScenarioPreview=null;}
        if(pendingSecondaryPreview){ScrollSecondaryForCheck();pendingSecondaryPreview=false;}
    }
    public void AccuracyPreviewForCheck(string? mode)
    {
        pendingSecondaryPreview=false;
        ShowModding();
        if(mode is null)
        {
            pendingScenarioPreview=null;
            if(accuracyPreviewSaved is {} restore)ApplyBuild(restore);
            accuracyPreviewSaved=null;SettleSimulationForPreview();return;
        }
        accuracyPreviewSaved??=ReadBuild();
        if(mode=="whip-dome")
        {
            var whipId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Whipclaw).Id;
            ApplyBuild(new(whipId,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(whipId,null),Melee:new(Debuffs:[new("Strangledome")]),Encounter:new(EncounterMode.FirstHit,Samples:64,NearbyEnemies:[new("same-dome",7,Debuffs:[new("Strangledome")]),new("other-dome",8,Debuffs:[new("Strangledome",Group:2)])])));
            SettleSimulationForPreview();ResetChartsScroll();primaryDebuffs!.View.IsExpanded=true;ScrollScenarioTo(primaryDebuffs.View);return;
        }
        if(mode=="energy-channel")
        {
            var energyId=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;var energySlots=new ModSlot?[8];var channel=catalog.Mods.Single(m=>m.EnglishName=="Energy Channel");energySlots[0]=new(channel.Id,channel.MaxRank);
            ApplyBuild(new(energyId,energySlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(energyId,null),Melee:new(Conditions:new(){["EnergySpent"]=100}),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(.5,"AbilityEnergySpent",25)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="target-debuffs")
        {
            var debuffId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Whipclaw).Id;
            ApplyBuild(new(debuffId,new ModSlot?[8],new(Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(debuffId,null),Melee:new(Debuffs:[new("Ensnare",Remaining:5),new("Petrify",Remaining:3)]),Encounter:new(EncounterMode.FirstHit,Samples:64,NearbyEnemies:[new("other",1,Debuffs:[new("SplinterStorm",Strength:2,Remaining:5)])])));
            SettleSimulationForPreview();ResetChartsScroll();primaryDebuffs!.View.IsExpanded=true;ScrollScenarioTo(primaryDebuffs.View);return;
        }
        if(mode=="eruption")
        {
            var eruptionId=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;var eruptionSlots=new ModSlot?[8];var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");eruptionSlots[0]=new(co.Id,co.MaxRank);
            ApplyBuild(new(eruptionId,eruptionSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(eruptionId,null),Melee:new(),Arcanes:new(Warframe1:new(WarframeStatusArcanes.Eruption,5)),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(0,"EnergyOrbPickup",Duration:1)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="enraged-lifetime")
        {
            var lifeId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Talons).Id;
            ApplyBuild(new(lifeId,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(lifeId,null),Melee:new(AbilityAugment:new(ExaltedAugments.Enraged,3)),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(1,"ExaltedDeactivate"),new(1.5,"ExaltedActivate"),new(2,"ExaltedActivate")])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="amanata")
        {
            var amanataId=catalog.Weapons.Single(w=>w.EnglishName=="Amanata").Id;var amanataSlots=new ModSlot?[8];var pressure=catalog.Mods.Single(m=>m.EnglishName=="Amanata Pressure");amanataSlots[0]=new(pressure.Id,pressure.MaxRank);
            ApplyBuild(new(amanataId,amanataSlots,new(Combo:8,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(amanataId,null),Melee:new(Attack:"Heavy Attack",Conditions:new(){["Roll"]=4,["AmanataHits"]=29}),Encounter:new(EncounterMode.FirstHit,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();weaponEffectsExpander!.IsExpanded=true;ScrollScenarioTo(weaponEffectsExpander);return;
        }
        if(mode=="ackbrunt")
        {
            var ackId=catalog.Weapons.Single(w=>w.EnglishName=="Ack & Brunt").Id;var ackSlots=new ModSlot?[8];ackSlots[0]=new(catalog.Mods.Single(m=>m.EnglishName=="Shocking Touch").Id,5);
            ApplyBuild(new(ackId,ackSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(ackId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(0,"BlockedElement",4,DamageType.Heat)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="fallout")
        {
            var falloutWeapon=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
            ApplyBuild(new(falloutWeapon,new ModSlot?[8],new(Enemy:new(1,0)),catalog.Version,5,Capacity:capacityCatalog.Default(falloutWeapon,null),Melee:new(),Arcanes:new(Warframe1:new(WarframeStatusArcanes.Fallout,0)),Encounter:new(EncounterMode.FirstHit,Samples:256,Inputs:[new(0,"AbilityRadiationInfliction",60)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="truculence")
        {
            var trucWeapon=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
            ApplyBuild(new(trucWeapon,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(trucWeapon,null),Melee:new(Conditions:new(){["TruculenceProgress"]=2900}),Arcanes:new(Warframe1:new(WarframeStatusArcanes.Truculence,0)),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(.2,"PlayerOverguardGain",100)],NearbyEnemies:[new("blocked",1)],BlockedSight:[new(-1,1)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="exalted-augment")
        {
            var augmentedId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Blade).Id;
            var augmentedSlots=new ModSlot?[8];augmentedSlots[0]=new(catalog.Mods.Single(m=>m.EnglishName=="North Wind").Id,5);
            ApplyBuild(new(augmentedId,augmentedSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(augmentedId,null),Melee:new(AbilityRank:2,AbilityStrength:1.3,AbilityDuration:1.5,AbilityAugment:new(ExaltedAugments.Chromatic,3),ChromaticElement:DamageType.Heat),Encounter:new(EncounterMode.FirstHit,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();weaponEffectsExpander!.IsExpanded=true;ScrollScenarioTo(weaponEffectsExpander);return;
        }
        if(mode=="exalted")
        {
            var exaltedId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Talons).Id;
            ApplyBuild(new(exaltedId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(exaltedId,null),Melee:new(AbilityRank:2,AbilityStrength:1.75,Conditions:new(){["ValkyrRage"]=150}),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(.5,"ValkyrFatalHit")])));
            SettleSimulationForPreview();ResetChartsScroll();weaponEffectsExpander!.IsExpanded=true;ScrollScenarioTo(weaponEffectsExpander);return;
        }
        if(mode=="vinquibus")
        {
            ApplyBuild(new(VinquibusState.WeaponId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(VinquibusState.WeaponId,null),Melee:new(Conditions:new(){["VinquibusPrecision"]=2}),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(.1,"VinquibusHeadshot",2),new(.2,"MeleeKill"),new(.3,"VinquibusPrimaryShot")])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="residual")
        {
            var rw=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");ArcaneSlot Ra(string n)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==n).Id,5);
            ApplyBuild(new(rw.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(rw.Id,null),Melee:new(),Arcanes:new(Warframe1:Ra("Theorem Demulcent"),Warframe2:Ra("Arcane Hot Shot")),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Inputs:[new(.1,"ResidualEnter",Element:DamageType.Toxin),new(.1,"ResidualTick",3,DamageType.Toxin),new(1,"ResidualLeave",Element:DamageType.Toxin),new(2,"AbilityHeatInfliction",20)])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="tennokai")
        {
            var tw=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");var tm=catalog.Mods.Single(m=>m.EnglishName=="Discipline's Merit");
            ApplyBuild(new(tw.Id,new ModSlot?[8],new(BaseInterval:.216,Combo:4,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(tw.Id,null),Melee:new(Exilus:new(tm.Id,3)),Encounter:new(EncounterMode.FiveSeconds,Samples:64,AttackOrder:["Normal Attack","Normal Attack","Normal Attack","Normal Attack","Heavy Attack"],Inputs:[new(.7,"TennokaiHeavyInput")])));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="tatsu")
        {
            ApplyBuild(new(TatsuSoulState.PrimeId,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(TatsuSoulState.PrimeId,null),Melee:new(Conditions:new(){["SoulCharges"]=1,["SoulRemaining"]=120}),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(.1,"MeleeKill"),new(1,"SoulSwarmSlideInput"),new(1.2,"SoulSwarmImpact"),new(1.4,"SoulSwarmImpact")],Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();dynamicExpander!.IsExpanded=true;scenarioScroller?.UpdateLayout();dynamicExpander.BringIntoView();return;
        }
        if(mode=="framearmor")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Sibear");var perk=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition=="Armor450");
            ArcaneSlot FrameArcane(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,5);
            ApplyBuild(new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Evolutions:[perk.Id]),Arcanes:new(Warframe1:FrameArcane("Arcane Tanker"),Warframe2:FrameArcane("Arcane Reaper"),PlayerArmor:300),Encounter:new(EncounterMode.TenSeconds,Inputs:[new(1,"EquipArchgun"),new(2,"MeleeKill")],Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();arcaneExpander!.IsExpanded=true;scenarioScroller?.UpdateLayout();playerArmorField!.BringIntoView();return;
        }
        if(mode=="scene")
        {
            var sceneWeaponId=ThalysMechanic.WeaponId;
            ApplyBuild(new(sceneWeaponId,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(sceneWeaponId,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("second",5),new("third",10)],TargetOrder:[0,1,2])));
            SettleSimulationForPreview();nearbyExpander!.IsExpanded=true;ResetChartsScroll();scenarioScroller?.ScrollToTop();return;
        }
        if(mode=="firstrecovery")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Redeemer Prime");var firstContactSlots=new ModSlot?[8];var names=new[]{"Healing Return","Condition Overload"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);firstContactSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w.Id,firstContactSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Heavy Attack"),Encounter:new(EncounterMode.FirstHit,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();return;
        }
        if(mode=="incarnonclock")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Praedos");
            ApplyBuild(new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Incarnon:true,IncarnonSecondsRemaining:2),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(1,"IncarnonHeavyInput",5),new(3,"IncarnonHeavyInput",5)],Samples:64)));
            SettleSimulationForPreview();weaponEffectsExpander!.IsExpanded=true;combatInputsExpander!.IsExpanded=true;ResetChartsScroll();scenarioScroller?.ScrollToTop();return;
        }
        if(mode=="destreza")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Destreza Prime");var evo=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Effects.ContainsKey("StatusToBaseCritical"));
            ApplyBuild(new(w.Id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Incarnon:true,Evolutions:[evo.Id]),Encounter:new(EncounterMode.FirstHit,Samples:64)));
            SettleSimulationForPreview();return;
        }
        if(mode=="obexwave")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Prisma Obex");var s=stanceCatalog.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));
            ApplyBuild(new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,2700)),catalog.Version,5,Stance:new(s.Id,"Finisher",stanceCatalog.Revision),Capacity:capacityCatalog.Default(w.Id,s.IsDefault?null:s.Id),Melee:new(Incarnon:true),Encounter:new(EncounterMode.StanceCycle,Samples:64)));
            SettleSimulationForPreview();return;
        }
        if(mode=="sancti")
        {
            ApplyBuild(new(SanctiMagistarState.WeaponId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(SanctiMagistarState.WeaponId,null),Melee:new(Conditions:new(){["SanctiHealingCombo"]=12}),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();scenarioScroller?.ScrollToTop();return;
        }
        if(mode is "harmony" or "healingreturn")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==(mode=="harmony"?"Harmony":"Redeemer Prime"));var recoveryMods=new ModSlot?[8];var m=catalog.Mods.Single(m=>m.EnglishName=="Healing Return");recoveryMods[0]=new(m.Id,m.MaxRank);
            CombatInput[] inputs=mode=="harmony"?[new(0,"HeavyMeleeKill"),new(0,"EvensongDevotion"),new(0,"CantareChorale")]:[];
            ApplyBuild(new(w.Id,recoveryMods,new(BaseInterval:1,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Heavy Attack"),Encounter:new(EncounterMode.FiveSeconds,Inputs:inputs,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();scenarioScroller?.ScrollToTop();return;
        }
        if(mode=="animosity")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Glaive Prime");var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Animosity");
            ApplyBuild(new(w.Id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Charged Throw",Conditions:new(){["AnimosityStacks"]=6}),Arcanes:new(Melee:new(a.Id,5)),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();scenarioScroller?.ScrollToTop();return;
        }
        if(mode=="lifestrike")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Hate");var m=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");var mods=new ModSlot?[8];mods[0]=new(m.Id,m.MaxRank);
            ApplyBuild(new(w.Id,mods,new(BaseInterval:1,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Heavy Attack"),Encounter:new(EncounterMode.TenSeconds,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="obex")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Prisma Obex");var perk=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition=="FinisherStacks");
            ApplyBuild(new(w.Id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Evolutions:[perk.Id],Conditions:new(){["FinisherStacks"]=14}),Encounter:new(EncounterMode.TenSeconds,Inputs:[new(0,"FinisherKill"),new(1,"ParazonKill")],Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="fortification")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Sibear");var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Fortification");var perk=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition=="Armor450");
            ApplyBuild(new(w.Id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Evolutions:[perk.Id]),Arcanes:new(Melee:new(a.Id,5),PlayerArmor:300),Encounter:new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeKill"),new(2,"MeleeKill")],Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();arcaneExpander!.IsExpanded=true;scenarioScroller?.UpdateLayout();playerArmorField!.BringIntoView();return;
        }
        if(mode is "brave" or "might")
        {
            var zawId=ZawCatalog.Id("Sepfahn","Peye","Jai");var name=mode=="brave"?"Exodia Brave":"Exodia Might";var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name);
            CombatInput[] inputs=mode=="brave"?[new(0,"HeavyMeleeKill",12),new(1,"AbilityCast"),new(2.5,"ChannelStart"),new(3.5,"ChannelStop")]:[new(0,"ParazonKill"),new(1,"FinisherKill")];
            ApplyBuild(new(zawId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(zawId,null),Melee:new(Exodia:new(a.Id,3)),Encounter:new(EncounterMode.TenSeconds,Inputs:inputs,Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="thalys")
        {
            var mods=new ModSlot?[8];var names=new[]{"Pressure Point","Condition Overload","Proton Snap","Buzz Kill","Fever Strike"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);mods[i]=new(m.Id,m.MaxRank);}
            var growth=IncarnonEngine.ForWeapon(ThalysMechanic.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("ShardImpale"));
            ApplyBuild(new(ThalysMechanic.WeaponId,mods,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(ThalysMechanic.WeaponId,null),
                Melee:new(Incarnon:true,Evolutions:[growth.Id],Conditions:new(){["ProtonSnap"]=1},Statuses:new(){[DamageType.Viral]=1,[DamageType.Heat]=1}),Encounter:new(EncounterMode.Primer,Samples:64)));
            SettleSimulationForPreview();return;
        }
        if(mode=="killbuffs")
        {
            var w=catalog.Weapons.Single(w=>w.Id==MeleeKillBuffs.PennantId);var m=catalog.Mods.Single(m=>m.EnglishName=="Berserker Fury");var mods=new ModSlot?[8];mods[0]=new(m.Id,m.MaxRank);
            ApplyBuild(new(w.Id,mods,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds,Inputs:[new(0,"HeavyMeleeKill",1),new(1,"HeavyMeleeKill",2)],Samples:64)));
            SettleSimulationForPreview();ResetChartsScroll();combatInputsExpander!.IsExpanded=true;ScrollScenarioTo(combatInputsExpander);return;
        }
        if(mode=="finisher")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Rumblejack");var s=stanceCatalog.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher Front"));var finisherSlots=new ModSlot?[8];
            var names=new[]{"Pressure Point","Fever Strike","Finishing Touch"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);finisherSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w.Id,finisherSlots,new(Enemy:new(1e9,2700)),catalog.Version,5,Stance:new(s.Id,"Finisher Front",stanceCatalog.Revision),Capacity:capacityCatalog.Default(w.Id,s.IsDefault?null:s.Id),Melee:new(),Encounter:new(EncounterMode.StanceCycle,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="hirudo")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Hirudo");var mod=catalog.Mods.Single(m=>m.EnglishName=="Blood Rush");var hirudoSlots=new ModSlot?[8];hirudoSlots[0]=new(mod.Id,mod.MaxRank);
            ApplyBuild(new(w.Id,hirudoSlots,new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="vitrica")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Vitrica");var glassSlots=new ModSlot?[8];var names=new[]{"Pressure Point","Melee Prowess","Proton Snap","Buzz Kill"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);glassSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w.Id,glassSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Glass Explosion",Conditions:new(){["ProtonSnap"]=1},Distance:3),Encounter:new(EncounterMode.Primer,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="stealth")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");var stealthSlots=new ModSlot?[8];var names=new[]{"Pressure Point","True Steel","Organ Shatter"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);stealthSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w.Id,stealthSlots,new(BaseInterval:1.08,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),
                Melee:new(Conditions:new(){["Invisible"]=1}),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();weaponEffectsExpander!.IsExpanded=true;dynamicExpander!.IsExpanded=true;arcaneExpander!.IsExpanded=false;
            scenarioScroller?.UpdateLayout();dynamicConditions.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="rakta")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Rakta Dark Dagger");var mods=new ModSlot?[8];var mod=catalog.Mods.Single(m=>m.EnglishName=="Weeping Wounds");mods[0]=new(mod.Id,mod.MaxRank);
            var retaliation=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Retaliation");
            ApplyBuild(new(w.Id,mods,new(Combo:12,BaseInterval:.2,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),
                Arcanes:new(Melee:new(retaliation.Id,5),Shields:199,MaximumShields:1000,ShieldRechargeEnabled:false),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();arcaneExpander!.IsExpanded=true;scenarioScroller?.UpdateLayout();arcaneExpander.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="vinquibus-shot")
        {
            var w=VinquibusState.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Attack:"Gunshot",Conditions:new(){["VinquibusPrecision"]=1}),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("edge",4,Defense:new(1000000,0))])));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="glaive-bounce")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Glaive").Id;var mod=catalog.Mods.Single(m=>m.EnglishName=="Volatile Quick Return");var bounceSlots=new ModSlot?[8];bounceSlots[0]=new(mod.Id,3);
            ApplyBuild(new(w,bounceSlots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Attack:"Charged Throw"),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("옆 대상",3,Defense:new(1e6,0))])));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="wolf-sledge")
        {
            var w=WolfSledgeFlight.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e6,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,ScriptedAttacksOnly:true,GlaiveFlights:[new(0,1,Contacts:[new(.1),new(.4,Kind:GlaiveContactKind.Return)],Detonation:new(.3))])));
            SettleSimulationForPreview();glaivePanel!.IsExpanded=true;ScrollScenarioForPreview(glaivePanel);ResetChartsScroll();return;
        }
        if(mode=="ack-shared")
        {
            var w=IncarnonBladeFlight.Ack;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e6,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true,Conditions:new(){["BlockHeat"]=4}),Encounter:new(EncounterMode.FiveSeconds,Samples:64,ScriptedAttacksOnly:true,IncarnonBlades:[new(.1,.2,BladeImpact:0)])));
            SettleSimulationForPreview();bladePanel!.IsExpanded=true;ScrollScenarioForPreview(bladePanel);ResetChartsScroll();return;
        }
        if(mode=="wind-control")
        {
            var w=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Wind).Id;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e6,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("오버가드 적",4,Defense:new(1e6,0),Overguard:100000)],ScriptedAttacksOnly:true,ExaltedWaves:[new(0,.1,6),new(0,.2,10,Target:2)])));
            SettleSimulationForPreview();wavePanel!.IsExpanded=true;ScrollScenarioForPreview(wavePanel);ResetChartsScroll();return;
        }
        if(mode=="exalted-wave-flight")
        {
            var w=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Blade).Id;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("관통 대상",15,Defense:new(1e9,0))],ScriptedAttacksOnly:true,ExaltedWaves:[new(0,.5,15,FalloffModel:1,ImpactCombo:"Neutral",ImpactGroup:1),new(0,1,30,Target:2,FalloffModel:1)])));
            SettleSimulationForPreview();wavePanel!.IsExpanded=true;ScrollScenarioForPreview(wavePanel);ResetChartsScroll();return;
        }
        if(mode=="blade-blind")
        {
            var w=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Blade).Id;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(AbilityDuration:.5),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("직전 인지한 적",3,Defense:new(1e9,0))],BlockedSight:[new(-1,1)],Inputs:[new(.5,"ExaltedSlideBlind",25,RecentlyAwareTargets:[2])])));
            SettleSimulationForPreview();combatInputsExpander!.IsExpanded=true;ScrollScenarioForPreview(combatInputsExpander);ResetChartsScroll();return;
        }
        if(mode=="diwata-exilus")
        {
            var w=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Diwata).Id;var parry=catalog.Mods.Single(m=>m.EnglishName=="Parry");
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Exilus:new(parry.Id,5))));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="vitrica-glass")
        {
            var w=VitricaGlassState.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(Combo:4,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("폭발 가장자리",6,Defense:new(1e9,0))],ScriptedAttacksOnly:true,VitricaGlasses:[new(0)],VitricaSlams:[new(.5,Heavy:true)])));
            SettleSimulationForPreview();glassPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();glassPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="innodem-flight")
        {
            var w=InnodemFlight.WeaponId;var flightSlots=new ModSlot?[8];var names=new[]{"Pressure Point","Condition Overload","Fever Strike"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);flightSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w,flightSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("다음 관통 대상",14,Defense:new(1e9,0))],ScriptedAttacksOnly:true,InnodemContacts:[new(0,.1,6,MotionMultiplier:2,LocationMultiplier:1,CoMultiplier:2),new(0,1d/3,20,Target:2,MotionMultiplier:2,LocationMultiplier:1,CoMultiplier:2)])));
            SettleSimulationForPreview();innodemPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();innodemPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="incarnon-blade")
        {
            var w=IncarnonBladeFlight.Ack;var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");var bladeSlots=new ModSlot?[8];bladeSlots[0]=new(co.Id,co.MaxRank);
            ApplyBuild(new(w,bladeSlots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("폭발 가장자리",5,Defense:new(1e9,0))],ScriptedAttacksOnly:true,IncarnonBlades:[new(0,.1,Travel:6),new(1,1.2,Projectile:2,Motion:"Slide",Travel:12)])));
            SettleSimulationForPreview();bladePanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();bladePanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="magus-aggress")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Galatine Prime").Id;var organ=catalog.Mods.Single(m=>m.EnglishName=="Organ Shatter");var magusSlots=new ModSlot?[8];magusSlots[0]=new(organ.Id,organ.MaxRank);
            ApplyBuild(new(w,magusSlots,new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Arcanes:new(Operator:new(MagusAggressState.Id,5)),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(0,"WarframeMeleeTransfer")],Samples:64)));
            SettleSimulationForPreview();arcaneExpander!.IsExpanded=true;scenarioScroller?.UpdateLayout();arcaneExpander.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="glaive-flight")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Glaive Prime").Id;var flightSlots=new ModSlot?[8];var names=new[]{"Volatile Quick Return","Power Throw","True Steel","Killing Blow"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);flightSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w,flightSlots,new(Combo:4,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("기폭 반경 안",3,Defense:new(1e9,0))],ScriptedAttacksOnly:true,GlaiveFlights:[new(0,1.5,Contacts:[new(.4)],Detonation:new(.8)),new(1.7,3.2,Throw:2,BufferedAt:1.2,Contacts:[new(2.1)],Detonation:new(2.5))])));
            SettleSimulationForPreview();glaivePanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();glaivePanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="orvius")
        {
            var w=OrviusHoverState.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Overguard:1),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("폭발 가장자리",4,Defense:new(1e9,0))],ScriptedAttacksOnly:true,OrviusHovers:[new(0,.1,Detonate:2,Returned:2.2,TickTimes:[.2,1.2,2.2],UseInfobox:true)])));
            SettleSimulationForPreview();hoverPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();hoverPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="cobra-sleep")
        {
            var w=ShieldThrowMechanic.CobraPrime;
            ApplyBuild(new(w,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Conditions:new(){["Baruuk"]=1}),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();weaponEffectsExpander!.IsExpanded=true;dynamicExpander!.IsExpanded=true;ScrollScenarioForPreview(dynamicExpander);ResetChartsScroll();return;
        }
        if(mode=="pathocyst-components")
        {
            ApplyBuild(new(PathocystMaggots.Normal,new ModSlot?[8],new(),catalog.Version,5,Capacity:capacityCatalog.Default(PathocystMaggots.Normal,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds)));Recalculate();SettleSimulationForPreview();ScrollSecondaryForCheck();return;
        }
        if(mode is "sampotes-components" or "exec-components" or "azothane-components" or "discharge-components")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==(mode=="sampotes-components"?"Sampotes":mode=="exec-components"?"Tenet Exec":mode=="azothane-components"?"Azothane":"Prova"));
            var b=new Build(w.Id,new ModSlot?[8],new(),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Attack:"Heavy Attack"));
            if(mode=="discharge-components"){var m=catalog.Mods.Single(m=>m.EnglishName=="Static Discharge");b.Slots[0]=new(m.Id,m.MaxRank);}
            ApplyBuild(b);Recalculate();SettleSimulationForPreview();ScrollSecondaryForCheck();return;
        }
        if(mode=="vinquibus-flight")
        {
            var b=new Build(VinquibusState.WeaponId,new ModSlot?[8],new(Enemy:new(1,0)),catalog.Version,5,Capacity:capacityCatalog.Default(VinquibusState.WeaponId,null),Melee:new(Conditions:new(){["VinquibusPrecision"]=1}),Encounter:new(EncounterMode.FiveSeconds,NearbyEnemies:[new("관통 뒤 대상",10,Defense:new(1e9,0)),new("폭발 가장자리",14,Defense:new(1e9,0))],ScriptedAttacksOnly:true,WeaponActions:[new(0,.1,Sequence:2,Contacts:[new(.3),new(.5,Target:2,Travel:10)])]));
            ApplyBuild(b);weaponActionPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(weaponActionPanel);ResetChartsScroll();return;
        }
        if(mode is "keratinos-source" or "keratinos-miss" or "jat-kusar" or "jat-kusar-component")
        {
            var traitWeapon=catalog.Weapons.Single(w=>w.EnglishName==(mode.StartsWith("jat-kusar")?"Jat Kusar":"Keratinos"));
            var traitBuild=new Build(traitWeapon.Id,new ModSlot?[8],new(Combo:12,BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(traitWeapon.Id,null),Melee:new(Attack:mode.StartsWith("jat-kusar")?"Heavy Attack":"Normal Attack"),Encounter:new(EncounterMode.FiveSeconds,Inputs:mode=="keratinos-miss"?[new(.5,"HeavyAttackMiss")]:[]));
            if(mode=="keratinos-source"){var stance=stanceCatalog.ForWeapon(traitWeapon).First();traitBuild=traitBuild with {Stance=new(stance.Id,"Heavy Article",stanceCatalog.Revision),Capacity=CapacityCatalog.Load().Default(traitWeapon.Id,stance.Id),Encounter=new(EncounterMode.StanceCycle)};}
            if(mode.StartsWith("jat-kusar"))traitBuild=traitBuild with {Encounter=new(EncounterMode.FirstHit)};
            ApplyBuild(traitBuild);Recalculate();SettleSimulationForPreview();
            if(mode=="keratinos-miss"){combatInputsExpander!.IsExpanded=true;ScrollScenarioForPreview(combatInputsExpander);}else ScrollScenarioForPreview(singleAttackField??scenarioPanel!);if(mode=="jat-kusar-component")ScrollSecondaryForCheck();return;
        }
        if(mode=="destreza-flight")
        {
            var w=MeleeMechanicScope.DestrezaPrimeId;var rapierSlots=new ModSlot?[8];var coMod=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");rapierSlots[0]=new(coMod.Id,coMod.MaxRank);
            ApplyBuild(new(w,rapierSlots,new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,ScriptedAttacksOnly:true,DestrezaContacts:[new(0,.1),new(0,.2,Rapier:2)],Inputs:[new(0,"HeavyAnimationStart"),new(.15,"HeavyAnimationEnd")])));
            rapierPanel!.IsExpanded=true;SettleSimulationForPreview();ScrollScenarioForPreview(rapierPanel);ResetChartsScroll();return;
        }
        if(mode=="weapon-buffs")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");var weaponBuffSlots=new ModSlot?[8];
            var names=new[]{"Pressure Point","Condition Overload","Fury","Fever Strike"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);weaponBuffSlots[i]=new(m.Id,m.MaxRank);}
            ApplyBuild(new(w.Id,weaponBuffSlots,new(Enemy:new(1e9,1000)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(Buffs:new(.5,2)),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();ScrollScenarioForPreview(weaponBuffPanel!);ResetChartsScroll();return;
        }
        if(mode=="atlas-rubble")
        {
            var rubbleWeaponId=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Landslide).Id;var b=new Build(rubbleWeaponId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(rubbleWeaponId,null),Melee:new(AbilityAugment:new(ExaltedAugments.Rubble,3),ExternalEquipment:new(AtlasRubble:new(1410))),Encounter:new(EncounterMode.FiveSeconds,RubblePickups:[new(3,75),new(4,50,1)]));
            ApplyBuild(b);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(rubbleEnabled);return;
        }
        if(mode is "mirrors-disc" or "mirrors-glaive" or "mirrors-wolf" or "mirrors-shield" or "mirrors-vinquibus")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==(mode switch{"mirrors-disc"=>"Zenistar","mirrors-glaive"=>"Glaive Prime","mirrors-wolf"=>"Wolf Sledge","mirrors-shield"=>"Sigma & Octantis",_=>"Vinquibus (Melee)"}));
            var b=new Build(w.Id,new ModSlot?[8],new(Combo:12,BaseInterval:1.5,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(ExternalEquipment:new(HallOfMirrors:new())),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true));
            b=b with {Encounter=b.Encounter! with {ZenistarDiscs=mode=="mirrors-disc"?Enumerable.Range(0,5).Select(n=>new ZenistarDeployment(0,.1,Disc:n+1,Hologram:n,AuraTimes:[.5,1])).ToArray():[],GlaiveFlights=mode is "mirrors-glaive" or "mirrors-wolf"?Enumerable.Range(0,5).Select(n=>new GlaiveFlight(0,1,Throw:n+1,Hologram:n,Contacts:[new(.2)])).ToArray():[],ShieldFlights=mode=="mirrors-shield"?[new(0,1,Impact:.2,Hologram:1,FinisherWindowSeconds:.2)]:[],WeaponActions=mode=="mirrors-vinquibus"?[new(0,0,Hologram:1,Contacts:[new(.2)])]:[]}};
            ApplyBuild(b);var panel=mode=="mirrors-disc"?discPanel:mode=="mirrors-shield"?shieldPanel:mode=="mirrors-vinquibus"?weaponActionPanel:glaivePanel;panel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(panel);return;
        }
        if(mode is "mirrors" or "mirrors-flight")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==(mode=="mirrors"?"Nikana Prime":"Quassus"));
            var b=new Build(w.Id,new ModSlot?[8],new(BaseInterval:1.5,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(ExternalEquipment:new(HallOfMirrors:new(Remaining:3,MalevolenceRank:3))),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,Inputs:[new(1,"MirrorNullified",2)],HologramContacts:[new(.1,1,Attack:"Heavy Attack"),new(.2,2,Attack:"Heavy Attack"),new(2,2),new(2,3)]));
            if(mode=="mirrors-flight")b=b with {Encounter=b.Encounter! with {Inputs=[],HologramContacts=[],WeaponActions=[new(0,0,Hologram:2,Contacts:[new(.2)])]}};
            ApplyBuild(b);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(mirrorsEnabled);return;
        }
        if(mode=="jetstream-clock")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Redeemer Prime");var b=new Build(w.Id,new ModSlot?[8],new(BaseInterval:1.5,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:"Ranged Attack",Distance:15,ExternalEquipment:new(JetStream:true,JetStreamRemaining:3)),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(4,"JetStreamEnter")]));
            ApplyBuild(b);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(jetStreamActive);return;
        }
        if(mode is "metronome" or "opera-flight")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==(mode=="metronome"?"Redeemer Prime":"Quassus"));
            var b=new Build(w.Id,new ModSlot?[8],new(BaseInterval:1.5,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:mode=="metronome"?"Ranged Attack":"Normal Attack",ExternalEquipment:new(Metronome:new(Opera:true,Forte:true,OperaRemaining:1,ForteRemaining:2))),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(3,"OperaBuff"),new(3,"ForteBuff")]));
            if(mode=="opera-flight")b=b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=true,Inputs=[],WeaponActions=[new(0,0,IndependentDaggers:true,Contacts:Enumerable.Range(1,16).Select(i=>new WeaponActionContact(.2+i*.01,Projectile:i)).ToArray())]}};
            ApplyBuild(b);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(metronomeEnabled);return;
        }
        if(mode=="warcry")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Skana");
            var b=new Build(w.Id,new ModSlot?[8],new(BaseInterval:1.5,Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(ExternalEquipment:new(Warcry:new(EternalWarRank:3,Remaining:1))),Arcanes:new(PlayerArmor:1710),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(2,"WarcryCast"),new(3,"MeleeKill")]));
            ApplyBuild(b);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(warcryEnabled);return;
        }
        if(mode is "external-daikyu" or "external-argonak" or "external-jetstream")
        {
            var gearWeapon=catalog.Weapons.Single(w=>w.EnglishName==(mode=="external-daikyu"?"Nikana Prime":mode=="external-argonak"?"Karyst":"Nepheri"));
            var gear=new MeleeExternalEquipment(mode=="external-daikyu"?"Daikyu":mode=="external-argonak"?"Argonak":"None",JetStream:mode=="external-jetstream");
            var gearBuild=new Build(gearWeapon.Id,new ModSlot?[8],new(Enemy:new(1e9,1000)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(gearWeapon.Id,null),Melee:new(Attack:mode=="external-jetstream"?"Normal Attack":"Heavy Attack",ExternalEquipment:gear),Encounter:new(EncounterMode.FiveSeconds,BaseArmor:500));
            if(mode=="external-jetstream")gearBuild=gearBuild with {Encounter=gearBuild.Encounter! with {ScriptedAttacksOnly=true,WeaponActions=[new(0,0,Sequence:2,Contacts:[new(.15,Travel:30)])]}};
            ApplyBuild(gearBuild);externalEquipmentPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(externalEquipmentPanel);return;
        }
        if(mode=="warfan-slam")
        {
            var fanId=WarfanSlam.Gunsen;
            var b=new Build(fanId,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(fanId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[new(0,0,Contacts:[new(.1,SlamHit:1),new(.4,SlamHit:2)],Mode:"HeavySlam")]));
            ApplyBuild(b);weaponActionPanel!.IsExpanded=true;Recalculate();SettleSimulationForPreview();ScrollScenarioForPreview(weaponActionPanel);return;
        }
        if(mode is "weapon-arum" or "weapon-nepheri" or "weapon-agendus" or "weapon-grigori")
        {
            var specialWeaponId=mode switch {"weapon-arum"=>WeaponStanceMechanic.Arum,"weapon-nepheri"=>WeaponStanceMechanic.Nepheri,"weapon-agendus"=>WeaponStanceMechanic.Agendus,_=>WeaponStanceMechanic.Grigori};
            var specialAction=mode=="weapon-nepheri"?new WeaponActionInput(0,.1,Sequence:2,Contacts:Enumerable.Range(0,4).Select(i=>new WeaponActionContact(.2+i*.25,Projectile:i+1,Travel:10,Launched:.1+i*.25)).ToArray()):
                mode=="weapon-agendus"?new WeaponActionInput(0,0,Contacts:[new(.25,Travel:20)],ExplosionModel:"Article"):
                mode=="weapon-grigori"?new WeaponActionInput(0,0,Sequence:2,Contacts:[new(.2,Travel:.2)],ProjectileExpires:1):
                new WeaponActionInput(0,0,Contacts:[new(.2,Travel:9.8,PenetratedMeters:1.3)]);
            ApplyBuild(new(specialWeaponId,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(specialWeaponId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,ScriptedAttacksOnly:true,WeaponActions:[specialAction])));
            SettleSimulationForPreview();weaponActionPanel!.IsExpanded=true;ScrollScenarioForPreview(weaponActionPanel);ResetChartsScroll();return;
        }
        if(mode is "weapon-quassus" or "weapon-edun" or "weapon-korumm")
        {
            var actionWeaponId=mode=="weapon-edun"?WeaponStanceMechanic.Edun:mode=="weapon-korumm"?WeaponStanceMechanic.Korumm:WeaponStanceMechanic.QuassusPrime;
            var action=mode=="weapon-edun"?new WeaponActionInput(0,0,Contacts:[new(.2,Travel:10)],ExplosionModel:"Module"):
                mode=="weapon-korumm"?new WeaponActionInput(0,0,Contacts:[new(.1,Blade:true),new(.3,Blade:true,BladeHit:2),new(.35,Explosion:true)]):
                new WeaponActionInput(0,.1,Contacts:[new(.02,Blade:true),new(.05,Blade:true,BladeHit:2),new(.4,Travel:10),new(.4,Projectile:2,Travel:10)],IndependentDaggers:true);
            ApplyBuild(new(actionWeaponId,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(actionWeaponId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("주변 대상",2,Defense:new(1e9,0))],ScriptedAttacksOnly:true,WeaponActions:[action])));
            SettleSimulationForPreview();weaponActionPanel!.IsExpanded=true;ScrollScenarioForPreview(weaponActionPanel);ResetChartsScroll();return;
        }
        if(mode=="shield-flight")
        {
            var w=ShieldThrowMechanic.Sigma;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("기폭 중심에서2m",2,Defense:new(1e9,0))],ScriptedAttacksOnly:true,ShieldFlights:[new(0,1,.2,Travel:12,Detonation:new(.4),FinisherWindowSeconds:.5)])));
            SettleSimulationForPreview();shieldPanel!.IsExpanded=true;ScrollScenarioForPreview(shieldPanel);ResetChartsScroll();return;
        }
        if(mode is "zenistar" or "zenistar-healing")
        {
            var w=ZenistarDiscState.WeaponId;var mods=new ModSlot?[8];if(mode=="zenistar-healing"){var life=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");mods[0]=new(life.Id,life.MaxRank);}
            ApplyBuild(new(w,mods,new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("오라 가장자리",4,Defense:new(1e9,0))],ScriptedAttacksOnly:true,ZenistarDiscs:[new(0,.1,Retrieve:2,AuraTimes:[.5,1,1.5,2,3],WrathfulCriticalPercent:200,RetrieveStarted:1)])));
            SettleSimulationForPreview();discPanel!.IsExpanded=true;ScrollScenarioForPreview(discPanel);ResetChartsScroll();return;
        }
        if(mode=="ceramic")
        {
            var w=CeramicDaggerMechanic.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("폭발 가장자리",4,Defense:new(1e9,0))],ScriptedAttacksOnly:true,CeramicDaggers:[new(0,.1,Travel:6),new(0,.2,Dagger:2,Travel:12)])));
            SettleSimulationForPreview();ceramicPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();ceramicPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="okina")
        {
            var w=OkinaDaggerState.PrimeId;
            ApplyBuild(new(w,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("직격 대상",3,Defense:new(1e9,0)),new("폭발 대상",7,Defense:new(1e9,0))],OkinaDaggers:[new(.1,1,Target:2,Travel:3)])));
            SettleSimulationForPreview();okinaPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();okinaPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="caust-pool")
        {
            var w=CaustacystMechanic.CodaId;
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64,ScriptedAttacksOnly:true,CaustacystPools:[new(0,4,BlindDuration:1.1)])));
            SettleSimulationForPreview();caustPoolPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();caustPoolPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="ichor-cloud")
        {
            var w=DualIchorCloudMechanic.WeaponId;
            ApplyBuild(new(w,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,Samples:64,NearbyEnemies:[new("연쇄 처치 대상",1,Defense:new(1,0)),new("마지막 대상",2,Defense:new(1e9,0))],IchorClouds:[new(.1,Target:2),new(.2,Cloud:2,Target:3)])));
            SettleSimulationForPreview();ichorPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();ichorPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="epidemic")
        {
            var w=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Epidemic");
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,"Blind Justice"),Melee:new(Exodia:new(exodia.Id,3)),Encounter:new(EncounterMode.FiveSeconds,Samples:64,Epidemic:[new(0),new(1,EpidemicPhase.Explosion)],ScriptedAttacksOnly:true)));
            SettleSimulationForPreview();epidemicPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();epidemicPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="contagion-buffs")
        {
            var w=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e12,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,"Blind Justice"),Melee:new(Exodia:new(exodia.Id,3),ContagionBuffs:new(VexArmor:3.5,VoidStrike:10,Eclipse:2)),Encounter:new(EncounterMode.TenSeconds,Samples:64,Contagion:[new(0,Travel:30)],ScriptedAttacksOnly:true)));
            SettleSimulationForPreview();contagionPanel!.IsExpanded=true;contagionBuffPanel!.IsExpanded=true;ScrollScenarioForPreview(contagionBuffPanel);ResetChartsScroll();return;
        }
        if(mode=="contagion-flight")
        {
            var w=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
            ApplyBuild(new(w,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,"Blind Justice"),Melee:new(Exodia:new(exodia.Id,3)),Encounter:new(EncounterMode.TenSeconds,Samples:64,Contagion:[new(0,Travel:30)],ScriptedAttacksOnly:true)));
            SettleSimulationForPreview();contagionPanel!.IsExpanded=true;scenarioScroller?.UpdateLayout();contagionPanel.BringIntoView();ResetChartsScroll();return;
        }
        if(mode=="contagion")
        {
            var w=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var mods=new ModSlot?[8];var names=new[]{"Pressure Point","Condition Overload"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);mods[i]=new(m.Id,m.MaxRank);}
            var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
            var arachne=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne");
            ApplyBuild(new(w,mods,new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w,"Blind Justice"),Arcanes:new(Warframe1:new(arachne.Id,5)),
                Melee:new(Exodia:new(exodia.Id,3),Distance:30,Statuses:new(){[DamageType.Viral]=1,[DamageType.Toxin]=1}),Encounter:new(EncounterMode.Primer,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        if(mode=="spinner")
        {
            var w=catalog.Weapons.Single(w=>w.Id==SpinnerexMechanic.WeaponId);var m=catalog.Mods.Single(m=>m.EnglishName=="Weeping Wounds");var mods=new ModSlot?[8];mods[0]=new(m.Id,m.MaxRank);
            ApplyBuild(new(w.Id,mods,new(Combo:12,BaseInterval:2,Enemy:new(1000,0)),catalog.Version,5,Capacity:capacityCatalog.Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,Samples:64)));
            SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();return;
        }
        var id=mode=="untimed"?ZawCatalog.Id("Balla","Peye","Jai"):catalog.Weapons.Single(w=>w.EnglishName==(mode=="glaive"?"Glaive Prime":mode=="tak"?"Tak & Lug":"Nikana Prime")).Id;
        var stanceId=mode=="untimed"?"Dagger":mode=="glaive"?"Glaive":mode=="tak"?"Sword and Shield":"Blind Justice";
        var slots=new ModSlot?[8];
        if(mode=="eximus")
        {
            var names=new[]{"Pressure Point","Blood Rush","Organ Shatter","Fury","Virulent Scourge","Vicious Frost","Weeping Wounds","Condition Overload"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);slots[i]=new(m.Id,m.MaxRank);}
        }
        var enemy=EnemyCatalog.Load().Enemies.Single(e=>e.EnglishName=="Heavy Gunner");
        ApplyBuild(new(id,slots,new(Enemy:new(10000,2700)),catalog.Version,5,
            Stance:new(stanceId,mode is "untimed" or "tak"?"Heavy":mode=="glaive"?"Heavy Charged":"Neutral",stanceCatalog.Revision),Capacity:capacityCatalog.Default(id,stanceId),
            Melee:new(),Encounter:new(EncounterMode.FiveSeconds),EnemySelection:new(enemy.Id,200,EnemyDifficulty.SteelPath,Eximus:true)));
        SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();
    }
    public void ScrollCombatHistoryForCheck(){combatHistory?.BringIntoView();resultsScroller?.UpdateLayout();}
    public FrameworkElement CombatSecondaryForCheck()=>combatSecondary;
    public FrameworkElement SecondaryDetailsForCheck()=>secondaryRows;
    public void ScrollSecondaryForCheck(){pendingSecondaryPreview=true;secondaryExpander!.IsExpanded=true;UpdateLayout();resultsScroller?.UpdateLayout();if(resultsScroller is not null){var offset=secondaryExpander.TranslatePoint(new Point(0,0),resultsScroller).Y+resultsScroller.VerticalOffset;resultsScroller.ScrollToVerticalOffset(offset);resultsScroller.UpdateLayout();}}
    public FrameworkElement CalculationSettingsForCheck()=>(FrameworkElement)settingsPopup.Child;
    private CombatInput[]? combatPreviewSaved;
    public FrameworkElement CombatInputsForCheck(bool show)
    {
        if(show){combatPreviewSaved=ReadCombatInputs();ApplyCombatInputs([new(.5,"BlockedHit",2),new(1,"AmmoPickup",3),new(2,"GroundSlide",27.5)]);}
        else{ApplyCombatInputs(combatPreviewSaved??[]);combatPreviewSaved=null;}
        return encounterInputRows;
    }
}

