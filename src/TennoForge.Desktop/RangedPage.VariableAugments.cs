using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddVariableAugmentControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> OptionalNumber)
    {
        bool Has(string effect)=>slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey(effect))||exilusMod.SelectedItem is ExilusChoice choice&&RangedEngine.ModCatalog.Mods.Any(m=>m.Id==choice.Id&&m.Effects.ContainsKey(effect));
        if(Has("CannonadeChance"))AddCannonadeControls(c,OptionalNumber);
        if(Selected is {} flightWeapon&&attack.SelectedIndex>=0&&RangedEngine.Attacks(flightWeapon)[attack.SelectedIndex] is {} flightAttack&&RangedEngine.S(flightAttack,"ShotType")=="Projectile"&&RangedEngine.N(flightAttack,"ShotSpeed") is null)
            conditionPanel.Children.Add(new Expander{Header="투사체 비행 시간 · 원문 미기재",Content=OptionalNumber("목표까지 비행 시간 (초) · 모드 적용 후 시나리오",c.ProjectileFlightSeconds,v=>storedConditions=storedConditions with{ProjectileFlightSeconds=v}),IsExpanded=true,Margin=new(0,4,0,12)});
        AddNullifierControls(c,OptionalNumber);
        if(Selected is {} grenadeWeapon&&RangedPentaAugments.Applies(grenadeWeapon))AddGrenadeEventControls(c,OptionalNumber);
        if(Selected is {} orbWeapon&&RangedInfestedOrbs.Applies(orbWeapon))AddOrbInteractionControls(c,OptionalNumber);
        if(Selected?.Id==RangedAzima.Id)AddAzimaControls(c,OptionalNumber);
        if(RangedResidual.Is(kitgun?.Arcane))AddResidualControls(c,OptionalNumber);
        if(Has("SpontaneousChance"))AddSingularityControls(c,OptionalNumber);
        if(Has("AdhesiveChance"))
        {
            var panel=Stack(Text("탄환별 부착 확률을 적용하며 붙은 탄환은 도탄을 멈춥니다. 일반 신관은 4초, 자르의 주탄은 3초이며 폭발 후 자탄이 생성됩니다. 테더 유탄은 원래의 지형 부착을 유지합니다."));
            if(Has("PentaNapalm"))panel.Children.Add(OptionalNumber("부착 네이팜 신관 (초) · 문서 간 4 / 약 5 충돌",c.AdhesiveNapalmFuse,v=>storedConditions=storedConditions with{AdhesiveNapalmFuse=v}));
            if(Selected?.Id=="wiki:primary/Tonkor")panel.Children.Add(OptionalNumber("발사 후 기폭 (초) · 위키 약 7초",c.AdhesiveTonkorFuse,v=>storedConditions=storedConditions with{AdhesiveTonkorFuse=v??7}));
            if(Selected?.Id is "wiki:primary/Torid" or "wiki:primary/Tenet Ferrox")panel.Children.Add(OptionalNumber("부착 뒤 장판 생성 지연 (초) · 원문 미기재",c.AdhesiveFieldDelay,v=>storedConditions=storedConditions with{AdhesiveFieldDelay=v}));
            conditionPanel.Children.Add(new Expander{Header="애드히시브 블래스트 · 부착 신관",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Selected is {} penta&&RangedPentaAugments.Applies(penta))
        {
            var panel=Stack();var manual=new CheckBox{Content="여러 유탄을 배치한 뒤 수동 기폭",IsChecked=c.ManualGrenades,Margin=new(0,4,0,8)};manual.Checked+=(_,_)=>{storedConditions=storedConditions with{ManualGrenades=true};RefreshConditionControls();Refresh();};manual.Unchecked+=(_,_)=>{storedConditions=storedConditions with{ManualGrenades=false};RefreshConditionControls();Refresh();};panel.Children.Add(manual);
            if(c.ManualGrenades){double detonateAt=2;panel.Children.Add(OptionalNumber("수동 기폭 시점 (초)",detonateAt,v=>detonateAt=v??2));panel.Children.Add(Button("＋ 기폭 예약",()=>{storedConditions=storedConditions with{ExternalEvents=[..storedConditions.ExternalEvents??[],new(detonateAt,"AlternateFire")]};RefreshConditionControls();Refresh();}));panel.Children.Add(Text("활성 유탄 5개에서 발사가 멈추고 기폭 후 재개합니다. 탄창 크기는 설치 제한을 늘리지 않습니다. 마지막 발사의 멀티샷은 제한을 넘어 함께 배치됩니다. 이동 중 기폭 위치는 없으므로 설정한 도착 시점 이후에 예약하세요."));}
            conditionPanel.Children.Add(new Expander{Header="펜타 · 유탄 배치와 기폭",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("PentaNapalm"))
        {
            var panel=Stack(Text("직격과 폭발은 각각 325 Heat입니다. 별도 직격 Heat 100의 배율과 장판 Heat의 틱 피해는 원문에 없어서 아래 가정을 구분합니다."));
            var mode=new ComboBox{ItemsSource=new[]{"추가 100 Heat · 배율 시나리오 선택 필요","고정 100 Heat · 모드·치명 배율 제외 가정","100 Heat에 피해·화염·팩션·치명 모드 적용 가정"},SelectedIndex=c.NapalmUtilityMode=="Fixed"?1:c.NapalmUtilityMode=="Modded"?2:0,Margin=new(0,8,0,8)};mode.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{NapalmUtilityMode=mode.SelectedIndex==1?"Fixed":mode.SelectedIndex==2?"Modded":"Unspecified"};Refresh();};panel.Children.Add(mode);
            var exclude=new CheckBox{Content="장판의 수치 미확정 피해 제외 · Heat 상태는 유지",IsChecked=c.NapalmExcludePatchDamage,Margin=new(0,0,0,8)};exclude.Checked+=(_,_)=>{storedConditions=storedConditions with{NapalmExcludePatchDamage=true};Refresh();};exclude.Unchecked+=(_,_)=>{storedConditions=storedConditions with{NapalmExcludePatchDamage=false};Refresh();};panel.Children.Add(exclude);
            panel.Children.Add(OptionalNumber("장판 Heat 1틱 출력 · 방어 적용 전 가정",c.NapalmPatchTick,v=>storedConditions=storedConditions with{NapalmPatchTick=v}));panel.Children.Add(OptionalNumber("장판 반경 (m) · 보조 위키 약 2m",c.NapalmPatchRadius,v=>storedConditions=storedConditions with{NapalmPatchRadius=v??2}));
            var head=new CheckBox{Content="약점 조준 시 폭발도 부착 대상의 약점에 명중",IsChecked=c.NapalmHeadshot,Margin=new(0,4,0,8)};head.Checked+=(_,_)=>{storedConditions=storedConditions with{NapalmHeadshot=true};Refresh();};head.Unchecked+=(_,_)=>{storedConditions=storedConditions with{NapalmHeadshot=false};Refresh();};panel.Children.Add(head);
            panel.Children.Add(Text("장판은 별도 직접 피해를 가하지 않고 진입한 적에게 Heat를 부여합니다. 장판 반경과 자체 상태 지속에는 범위·상태 지속 모드를 적용하지 않습니다. 현재 고정된 적 배치에서 진입을 판정합니다."));
            conditionPanel.Children.Add(new Expander{Header="네이팜 그레네이즈 · 직격·폭발·장판",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("VoltageTrapDuration"))
        {
            var panel=Stack(Text("비행 적을 처치하면 4m 범위의 함정이 낙하합니다. 틱 피해 6~7과 초당 60틱은 확인된 수치이며, 아래 나머지 값은 원문에 없는 시나리오 조건입니다."));
            var flying=new CheckBox{Content="선택한 적은 비행하는 적",IsChecked=c.FlyingTarget,Margin=new(0,4,0,8)};flying.Checked+=(_,_)=>{storedConditions=storedConditions with{FlyingTarget=true};Refresh();};flying.Unchecked+=(_,_)=>{storedConditions=storedConditions with{FlyingTarget=false};Refresh();};panel.Children.Add(flying);
            panel.Children.Add(OptionalNumber("처치 후 함정 착지까지 (초)",c.VoltageTrapLanding,v=>storedConditions=storedConditions with{VoltageTrapLanding=v}));panel.Children.Add(OptionalNumber("함정 하나의 최대 대상 수",c.VoltageTrapTargets,v=>storedConditions=storedConditions with{VoltageTrapTargets=v is null?null:v>=1&&v<=32&&v==Math.Truncate(v.Value)?(int)v:-1}));panel.Children.Add(OptionalNumber("틱 피해 · 확인된 6~7 범위 중 가정",c.VoltageTrapDamage,v=>storedConditions=storedConditions with{VoltageTrapDamage=v}));panel.Children.Add(OptionalNumber("착지 후 첫 틱 (초) · 0~0.01667",c.VoltageTrapPhase,v=>storedConditions=storedConditions with{VoltageTrapPhase=v}));
            conditionPanel.Children.Add(new Expander{Header="볼티지 시퀀스 · 비행 적 처치",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("TetherTargets"))
        {
            var panel=Stack(Text("적 몸통에는 직격 피해 없이 통과하고, 지형에 부착하면 주변 적을 끌어옵니다. 착지 후 기폭 대기는 최대 10초이며, 더 긴 입력도 10초 자동 기폭으로 제한합니다."));
            var air=new CheckBox{Content="착지 전에 공중 기폭 · 폭발 피해 ×1.2",IsChecked=c.TetherAirburst,Margin=new(0,4,0,8)};air.Checked+=(_,_)=>{storedConditions=storedConditions with{TetherAirburst=true};Refresh();};air.Unchecked+=(_,_)=>{storedConditions=storedConditions with{TetherAirburst=false};Refresh();};panel.Children.Add(air);
            panel.Children.Add(OptionalNumber("감속 후 유탄 비행 시간 (초)",c.TetherFlightSeconds,v=>storedConditions=storedConditions with{TetherFlightSeconds=v}));
            panel.Children.Add(OptionalNumber("래그돌 대상에 부착 · 착지 후 자세 회복까지 (초)",c.TetherRagdollRecovery,v=>storedConditions=storedConditions with{TetherRagdollRecovery=v}));
            panel.Children.Add(Button("착지 후 10초 자동 기폭",()=>{storedConditions=storedConditions with{DetonationDelay=10,TetherAirburst=false};RefreshConditionControls();Refresh();}));
            var immune=new CheckBox{Content="끌어오기 면역 · 생포/신디시스/데몰리스트",IsChecked=c.TetherTargetImmune,Margin=new(0,4,0,8)};immune.Checked+=(_,_)=>{storedConditions=storedConditions with{TetherTargetImmune=true};Refresh();};immune.Unchecked+=(_,_)=>{storedConditions=storedConditions with{TetherTargetImmune=false};Refresh();};panel.Children.Add(immune);
            conditionPanel.Children.Add(new Expander{Header="테더 그레네이즈 · 부착과 기폭",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
            AddPullControls(c,OptionalNumber);
        }
        if(Has("HealthOrbShotBonus")||Has("EnergyOrbShotBonus"))AddOrbShotControls(c,OptionalNumber);
        if(Has("ThunderboltChance")||Has("ConcealedExplosionChance"))
        {
            var panel=Stack(Text("폭발은 고정 250 피해이며 팩션 보너스·반경 모드만 적용합니다. 컨실드는 위키에 기재된 현재 버그에 따라 지형 충돌에서만 폭발합니다."));
            var surface=new CheckBox{Content="적 주변 바닥·벽에 사격",IsChecked=c.FirstImpactSurface,Margin=new(0,4,0,8)};surface.Checked+=(_,_)=>{storedConditions=storedConditions with{FirstImpactSurface=true,TargetsInLine=false};Refresh();};surface.Unchecked+=(_,_)=>{storedConditions=storedConditions with{FirstImpactSurface=false};Refresh();};panel.Children.Add(surface);
            panel.Children.Add(OptionalNumber("표면 유효 두께 (m) · 관통 모드 장착 시",c.ImpactSurfaceThickness,v=>storedConditions=storedConditions with{ImpactSurfaceThickness=v}));
            if(Has("ThunderboltChance"))
            {
                var choices=new[]{new ArcaneChoice("Unspecified","폭발 시 직격 처리 · 원문 미명시"),new ArcaneChoice("Replace","화살 소멸 · 폭발 피해만"),new ArcaneChoice("Add","직격 후 소멸 · 직격과 폭발")};
                var choice=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=choices.Single(x=>x.Id==c.ThunderboltDirectRule),Margin=new(0,4,0,8)};choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{ThunderboltDirectRule=v.Id};Refresh();}};panel.Children.Add(choice);
            }
            if(Has("ConcealedExplosionChance"))for(int i=0;i<c.GroupCount;i++){var index=i;var blocked=new CheckBox{Content=$"대상 {i+1} · 폭발 중심에서 시야 차단",IsChecked=(c.ExplosionObstructedTargets??[]).Contains(i),Margin=new(0,4,0,4)};void Set(bool v){var set=(storedConditions.ExplosionObstructedTargets??[]).ToHashSet();if(v)set.Add(index);else set.Remove(index);storedConditions=storedConditions with{ExplosionObstructedTargets=set.ToArray()};Refresh();}blocked.Checked+=(_,_)=>Set(true);blocked.Unchecked+=(_,_)=>Set(false);panel.Children.Add(blocked);}
            conditionPanel.Children.Add(new Expander{Header="폭발 모드 · 충돌과 시야",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("DischargeCapacity"))
        {
            var panel=Stack(OptionalNumber("시작 시 저장 피해",c.DischargeStored,v=>storedConditions=storedConditions with{DischargeStored=v??0}),OptionalNumber("주머니 비행 시간 (초) · 거리 0이면 즉시",c.DischargeFlightSeconds,v=>storedConditions=storedConditions with{DischargeFlightSeconds=v}),Text("기본 사격과 상태 피해가 저장 피해를 채웁니다. 외부 버프 발동 시점에서 ‘보조 공격 사용’을 추가하면 해당 시점에 저장 피해를 방출한 뒤 기본 사격으로 돌아갑니다. 방어·적 피해 증폭 전 출력 중 초과 처치 피해는 제외합니다."));
            conditionPanel.Children.Add(new Expander{Header="바일 디스차지 · 저장과 방출",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("BuckshotFalloff"))
        {
            var panel=Stack(Text("일반 사격 처치는 다음 벅샷의 감쇠 거리를 강화합니다. 아래 기준은 위키에 명시되지 않은 탄약 회복 처리를 위한 시나리오입니다."));
            var ready=new CheckBox{Content="시작 시 다음 벅샷 강화 준비됨",IsChecked=c.BuckshotReady,Margin=new(0,4,0,8)};ready.Checked+=(_,_)=>{storedConditions=storedConditions with{BuckshotReady=true};Refresh();};ready.Unchecked+=(_,_)=>{storedConditions=storedConditions with{BuckshotReady=false};Refresh();};panel.Children.Add(ready);
            var sources=new[]{new ArcaneChoice("Unspecified","탄약 회복 출처 미선택"),new ArcaneChoice("Reserve","예비 탄약에서 가져옴"),new ArcaneChoice("Created","예비 탄약 소비 없이 생성")};
            var source=new ComboBox{ItemsSource=sources,DisplayMemberPath="Name",SelectedItem=sources.Single(x=>x.Id==c.BuckshotRefillSource),Margin=new(0,4,0,8)};source.SelectionChanged+=(_,_)=>{if(source.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{BuckshotRefillSource=v.Id};Refresh();}};panel.Children.Add(source);
            var rounds=new[]{new ArcaneChoice("Unspecified","소수 탄수 처리 미선택"),new ArcaneChoice("Down","소수 탄수 버림"),new ArcaneChoice("Up","소수 탄수 올림")};var rounding=new ComboBox{ItemsSource=rounds,DisplayMemberPath="Name",SelectedItem=rounds.FirstOrDefault(x=>x.Id==c.RefundRounding)??rounds[0]};rounding.SelectionChanged+=(_,_)=>{if(rounding.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{RefundRounding=v.Id};Refresh();}};panel.Children.Add(rounding);
            conditionPanel.Children.Add(new Expander{Header="오버프레셔드 라운즈 · 다음 벅샷",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("OscillatingStatusChance"))
        {
            var panel=Stack(Text("변동항 발생 분포가 원문에 없어 특정 값에 고정한 시나리오를 계산합니다. 0%는 변동항 없음, 100%는 해당 랭크의 변동항 최대값입니다."),OptionalNumber("변동항 위치 고정 (%)",c.FluxOscillation*100,v=>storedConditions=storedConditions with{FluxOscillation=v/100}));
            var presets=new WrapPanel();foreach(var value in new[]{0d,1d})presets.Children.Add(Button(value==0?"변동항 없음":"변동항 최대",()=>{storedConditions=storedConditions with{FluxOscillation=value};RefreshConditionControls();Refresh();}));panel.Children.Add(presets);
            if(Selected?.EnglishName=="Tenet Flux Rifle")
            {
                var options=new[]{"테넷 본문 2배 설명 · 해석 선택", "모드 변동항에 포함된 설명", "변동항 외에 기본 상확 ×2"};
                var choice=new ComboBox{ItemsSource=options,SelectedIndex=c.TenetFluxDoubleBase is null?0:c.TenetFluxDoubleBase.Value?2:1,Margin=new(0,8,0,0)};
                choice.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{TenetFluxDoubleBase=choice.SelectedIndex==0?null:choice.SelectedIndex==2};Refresh();};panel.Children.Add(choice);
            }
            conditionPanel.Children.Add(new Expander{Header="플럭스 오버드라이브 · 변동항",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
        }
        if(Has("HigasaForcedStatus"))
        {
            var options=new[]{new ArcaneChoice("None","강제 상태 결과 선택")}.Concat(Enum.GetValues<DamageType>().Where(t=>t<=DamageType.Corrosive).Select(t=>new ArcaneChoice(t.ToString(),Labels.Damage(t)))).ToArray();
            var choice=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedItem=options.Single(x=>x.Id==(c.HigasaForcedStatus?.ToString()??"None"))};
            choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is ArcaneChoice value){storedConditions=storedConditions with{HigasaForcedStatus=value.Id=="None"?null:Enum.Parse<DamageType>(value.Id)};Refresh();}};
            conditionPanel.Children.Add(new Expander{Header="히가사 서레이션 · 강제 상태",Content=Stack(Text("원문에 무작위 종류·가중치가 없어 선택한 상태가 나온 시나리오를 계산합니다. 무작위 평균값이 아닙니다."),choice),IsExpanded=true,Margin=new(0,4,0,12)});
        }
    }
}
