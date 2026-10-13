namespace TennoForge.Core;

public enum EncounterMode { FirstHit, Primer, StanceCycle, FiveSeconds, TenSeconds }
public record EncounterSetup(EncounterMode Mode=EncounterMode.FirstHit,double? BaseArmor=null,double[]? HitTimes=null,CombatInput[]? Inputs=null,int Samples=2048,CombatEnemy[]? NearbyEnemies=null,int[]? TargetOrder=null,string[]? AttackOrder=null,CombatSightBlock[]? BlockedSight=null,ContagionCollision[]? Contagion=null,bool ScriptedAttacksOnly=false,EpidemicImpact[]? Epidemic=null,IchorCloudContact[]? IchorClouds=null,CaustacystPoolWindow[]? CaustacystPools=null,OkinaDaggerFlight[]? OkinaDaggers=null,CeramicDaggerContact[]? CeramicDaggers=null,ZenistarDeployment[]? ZenistarDiscs=null,OrviusHover[]? OrviusHovers=null,GlaiveFlight[]? GlaiveFlights=null,IncarnonBladeContact[]? IncarnonBlades=null,InnodemContact[]? InnodemContacts=null,VitricaGlassContact[]? VitricaGlasses=null,VitricaSlamContact[]? VitricaSlams=null,ExaltedWaveContact[]? ExaltedWaves=null,ShieldFlight[]? ShieldFlights=null,WeaponActionInput[]? WeaponActions=null,HologramContact[]? HologramContacts=null,RubblePickup[]? RubblePickups=null,DestrezaContact[]? DestrezaContacts=null);
public record EncounterRange(double Low,double High);
public record EncounterSample(int Hit,double Earliest,double Latest,EncounterRange Damage,EncounterRange StatusTypes,EncounterRange Armor,double Combo=1,string[]? ForcedProcs=null,double CoFactor=1,EncounterRange? SnapshotDps=null,EncounterRange? WithoutCoDps=null,EncounterRange? CoGain=null,double StartDps=0,EncounterRange? Health=null,EncounterRange? Shields=null,EncounterRange? Overguard=null);
public record EncounterResult(EncounterMode Mode,double? Seconds,EncounterRange Damage,EncounterRange? AverageDps,
    EncounterRange EndStatusTypes,EncounterSample[] Samples,int Trials,string[] Notes,string[] Unsupported,CombatTimelineResult? Timeline=null,bool DamageOnly=false,EncounterPrecision? Precision=null);

/// <summary>
/// One shared encounter path for proc lifetime, CO and permanent armor changes.
/// Stance events carry time windows, never invented hit timestamps. Direct damage
/// is evaluated before procs from the same hit. A living single target is assumed.
/// </summary>
public static class EncounterEngine
{
    public const string Version="encounter-events-0.7";
    private record Step(Build Input,StanceHit? Hit,double CoFactor,Calculation Initial,bool Finisher,bool Slide,bool CoVerified=true);
    private record Proc(DamageType Type,double Earliest,double Latest,double Duration);
    private static readonly Dictionary<DamageType,double> Durations=new()
    {
        [DamageType.Impact]=6,[DamageType.Puncture]=10,[DamageType.Slash]=6,[DamageType.Heat]=6,
        [DamageType.Electricity]=6,[DamageType.Toxin]=6,[DamageType.Radiation]=12,[DamageType.Gas]=6,
        [DamageType.Magnetic]=6,[DamageType.Viral]=6,[DamageType.Corrosive]=8
    };
    public static double Lifetime(DamageType type,Calculation c)
    {
        if(!Durations.TryGetValue(type,out var seconds))throw new ArgumentException(Labels.Damage(type)+"의 전투 중 특수 상태 전이를 아직 지원하지 않습니다.");
        var effects=c.Melee!.Effects;
        return seconds*Math.Max(0,1+effects.GetValueOrDefault("StatusDuration")+(type==DamageType.Slash?effects.GetValueOrDefault("SlashDuration"):0));
    }
    public static double HeatStrip(double elapsed,double durationMultiplier=1)
    {
        if(durationMultiplier<=0)return 0;
        var stage=(int)Math.Floor(Math.Max(0,elapsed)/(.5*durationMultiplier)+1e-10);
        return stage switch{0=>0,1=>.15,2=>.3,3=>.4,_=>.5};
    }
    public static EncounterResult CalculateOrdered(Catalog catalog,Build build,StanceCatalog stances,int trials=2048,CancellationToken cancellation=default,IProgress<SimulationProgress>? progress=null)
    {
        if(trials<1||trials>4096)throw new ArgumentException("교전 표본 수는 1~4096이어야 합니다.");
        build=CombatTimeline.InitialState(build);
        if(build.Melee is null)throw new ArgumentException("교전 계산에는 근접 설정이 필요합니다.");
        if(build.Encounter?.BaseArmor is {} enteredArmor)MeleeEngine.Range(enteredArmor,.001,1e9,"적 기본 방어력");
        var mode=build.Encounter?.Mode??EncounterMode.FirstHit;
        if(!Enum.IsDefined(mode))throw new ArgumentException("교전 조건 오류");
        if(mode!=EncounterMode.Primer)build=build with {Scenario=build.Scenario with {ExistingStatusTypes=0},Melee=build.Melee! with {Statuses=[],Conditions=(build.Melee.Conditions??[]).Where(p=>p.Key is not ("Lifted" or "Knockdown" or "Microwave")).ToDictionary()},Arcanes=build.Arcanes is {} a?a with {Frozen=false}:null};
        var sc=build.Stance is null?null:StanceEngine.Calculate(catalog,build,stances);
        var initial=sc?.Stats??MeleeEngine.Calculate(catalog,build);
        var steps=sc is null?new[]{new Step(build,null,1,initial,false,false)}:sc.Hits.SelectMany(h=>Enumerable.Repeat(new Step(h.Input!,h.SourceHit,h.CoFactor,h.Calculation!,sc.Combo.Key=="Finisher",sc.Combo.Key=="Slide",h.CoVerified),h.Count)).ToArray();
        var first=steps[0].Initial;var firstDamage=first.TargetDps*first.HitInterval;
        if(mode is EncounterMode.FirstHit or EncounterMode.Primer)
        {
            var blocked=mode==EncounterMode.Primer&&TargetStatusRules.Count(build)>0&&first.Melee!.Effects.GetValueOrDefault("ConditionOverload")>0&&!steps[0].CoVerified;
            return new(mode,null,new(firstDamage,firstDamage),null,new(TargetStatusRules.Count(build),TargetStatusRules.Count(build)),[],0,["선택 동작의 첫 타격 · 치명타 기대값 · 상태이상은 타격 이후 부여됩니다."],blocked?["이 동작의 타격별 CO 예외 배율은 아직 연결되지 않았습니다. 강제 프록 데이터 누락과는 별개입니다."]:[]);
        }
        if(sc is {Duration:null})return new(mode,null,new(sc.TargetDamage,sc.TargetDamage),null,new(TargetStatusRules.Count(build),TargetStatusRules.Count(build)),[],0,new[]{"동작 전체 시간은 위키 미기재 · 시작 상태를 고정한 동작 1회 피해와 강제 상태 순서 제공 · 시간당 피해와 처치 시간은 표시하지 않습니다."}.Concat(sc.Stats.Limitations.Where(n=>n.StartsWith("미검증:")||n.StartsWith("미반영:"))).Distinct().ToArray(),[],DamageOnly:true);
        var duration=sc?.Duration??initial.HitInterval;
        var horizon=mode switch{EncounterMode.StanceCycle=>duration,EncounterMode.FiveSeconds=>5,_=>10};
        var unsupported=new List<string>();var notes=new List<string>{"살아 있는 단일 대상의 직접 피해 · 자체 상태·일반 콤보 누적 반영 · 지속 피해와 처치 후 전환은 제외합니다.","수동 선택한 외부 버프는 고정합니다. 표본 간 변동이 있는 기대값 추정입니다."};
        if(steps.Any(s=>VinquibusGunshot.Applies(build.WeaponId,s.Input.Melee!.Attack)))unsupported.Add("빈퀴부스의0.8초 지연 폭발을 포함하려면 사격의 실제 접촉 시각이 필요합니다. 시간별 전투 경로에서 직접/폭발 피해를 함께 계산합니다.");
        if(build.Melee.ExternalEquipment is {JetStream:true,JetStreamRemaining:>0} jet&&jet.JetStreamRemaining<horizon)unsupported.Add("제트 스트림 만료에 따른 감쇠 거리 변화를 계산하려면 개별 타격 시각이 필요합니다.");
        if(build.Melee.ExternalEquipment?.Metronome is {} metro&&((MetronomeRules.Opera(metro)>0&&(metro.OperaRemaining??MetronomeRules.Lifetime(metro))<horizon)||(MetronomeRules.Forte(metro)>0&&(metro.ForteRemaining??MetronomeRules.Lifetime(metro))<horizon)))unsupported.Add("메트로놈 버프 만료와 타격 순서를 구분하려면 개별 타격 시각이 필요합니다.");
        if(build.Melee.ExternalEquipment?.AtlasRubble is {Armor:>0}||(build.Encounter?.RubblePickups?.Length??0)>0)unsupported.Add("러블 감소·습득과 타격 순서를 구분하려면 개별 타격 시각이 필요합니다.");
        if(build.Melee.ExternalEquipment?.Warcry is {} warcry&&WarcryRules.Active(warcry)&&(warcry.Remaining??WarcryRules.BaseDuration(warcry))<horizon)unsupported.Add("워크라이 만료 전후 공속·방어력을 구분하려면 개별 타격 시각이 필요합니다. 시작 버프를 교전 내내 고정하지 않습니다.");
        if(build.Melee.Incarnon&&new IncarnonFormState(build).Remaining(0)<horizon)unsupported.Add("인카논 만료와 타격의 순서를 확정하려면 개별 타격 시각이 필요합니다. 변신 버프를 교전 끝까지 고정하지 않습니다.");
        var rules=MeleeRuleCatalog.Load();var profile=rules.Weapon(build.WeaponId);
        if(steps.Any(s=>MeleeStealth.NeedsContactCooldown(s.Initial)))unsupported.Add("은신의 접촉 후 3초 재적용을 계산하려면 개별 타격 시각이 필요합니다. 실명 상태는 접촉 대기가 없습니다.");
        if(MeleeStealth.Invisible(build.Melee)&&sc?.Stance.Id=="High Noon"&&sc.Combo.Key=="Neutral"&&profile.Name!="Stropha")unsupported.Add("하이 눈 정지 연사의 순간 은신 보너스 차단 시간은 위키 미기재입니다.");
        if((build.Melee.Conditions?.GetValueOrDefault("Unalerted")??0)>0)unsupported.Add("미경계 적의 경계 전환 지연 미기재");
        if(build.Encounter?.Inputs is {Length:>0})unsupported.Add("전투 중 행동과 타격의 앞뒤 순서를 확정하려면 개별 타격 시각이 필요합니다.");
        var selectedPerks=IncarnonEngine.ForWeapon(build.WeaponId)?.Perks.Where(p=>(build.Melee.Evolutions??[]).Contains(p.Id)).ToArray()??[];
        if(selectedPerks.Any(p=>p.Conditional.ContainsKey("ExtraCombo")))unsupported.Add("적 체력·실드의 손상 여부에 따른 진화 콤보는 시간별 전투 경로에서 지원합니다.");
        if(new[]{build.Arcanes?.Melee,build.Arcanes?.Warframe1,build.Arcanes?.Warframe2}.Any(a=>a?.Active==true&&CombatTimeline.AutomaticArcane(rules.Arcanes.Single(r=>r.Id==a.Id).Code)))unsupported.Add("자동 발동 아케인의 발동 시각은 개별 타격 시각이 필요합니다.");
        var possible=TargetStatusRules.WeaponPreset(initial,sc);
        foreach(var type in possible.Where(t=>!Durations.ContainsKey(t)))unsupported.Add(Labels.Damage(type)+" 특수 전이");
        var postureProcs=steps.SelectMany(s=>s.Initial.Melee!.ForcedProcs).Where(p=>p is "Lifted" or "Knockdown").Distinct().ToArray();
        foreach(var proc in steps.SelectMany(s=>s.Initial.Melee!.ForcedProcs).Distinct().Where(p=>!Enum.TryParse<DamageType>(p,out _)&&p is not ("Lifted" or "Knockdown" or "Ragdoll" or "Stagger")))unsupported.Add(proc+" 강제 상태의 규칙");
        if(postureProcs.Length>0)notes.Add("넉다운·띄우기는 동시에 2종으로 세지 않습니다. 회복 시점이 확정되지 않은 구간은 CO 추가 0~1종의 보수적 범위로 표시합니다. 전 타격 명중을 가정하며 밀려남·헛스윙은 추정하지 않습니다.");
        if(postureProcs.Length>0&&build.Slots.Any(s=>s is not null&&rules.Mods.Single(m=>m.Id==s.ModId).Condition=="Lifted"))unsupported.Add("띄우기 조건부 모드의 상태 확률·콤보 전이");
        if(profile.Passives.Any(p=>p.Key!="Always"))unsupported.Add("무기 고유 버프의 시간별 발동 · "+profile.Name);
        unsupported.AddRange(MeleeMechanicScope.TimeIssues(profile,steps.Select(s=>s.Input.Melee!),steps.Any(s=>s.Finisher),sc?.Combo.Hits.Any(h=>h.AttackType=="Slam")==true));
        if(initial.Limitations.Any(s=>s.StartsWith("미반영:")&&!s.Contains("기본 방어력")))unsupported.Add("미구현 효과가 포함된 빌드");
        if(steps.Any(s=>s.Initial.Melee!.Projectiles!=1))unsupported.Add("다중 투사체의 상태 적용 순서");
        if(steps.Any(s=>s.Initial.DuplicateChance>0||s.Initial.Melee!.ExtraDamage>0))unsupported.Add("추가 타격·저장 피해 소비");
        if(sc is not null&&sc.Combo.ActionKind is "Heavy" or "Slam" or "Finisher")unsupported.Add("강공격·슬램·마무리 반복 전이");
        if(sc is null&&build.Melee.Attack!="Normal Attack")unsupported.Add("일반 공격 외 시간별 입력 전이");
        if(mode==EncounterMode.StanceCycle&&sc is null)unsupported.Add("스탠스 선택 필요");
        if(sc is not null&&initial.Melee!.Effects.GetValueOrDefault("ConditionOverload")>0&&steps.Any(s=>!s.CoVerified))unsupported.Add("아직 연결되지 않은 타격별 CO 예외 배율 (강제 프록과 별개)");
        if(sc is not null&&duration*(mode==EncounterMode.StanceCycle?1:2)>initial.Melee!.ComboDuration)unsupported.Add("개별 타격 시각이 없어 확정할 수 없는 콤보 만료");
        if(sc is not null&&(initial.Melee!.ComboChance<1||sc.Stance.Id is "Swirling Tiger" or "Homing Fang"))unsupported.Add("음수 콤보 획득 확률 또는 스탠스별 콤보 획득 예외 규칙");
        var effectKeys=steps.SelectMany(s=>s.Initial.Melee!.Effects.Where(p=>p.Value!=0&&MeleeMechanicScope.EffectCanTrigger(p.Key,build.WeaponId,s.Input.Melee!)).Select(p=>p.Key)).Distinct().ToHashSet();
        foreach(var key in new[]{"ImpactToSlash","RandomProc","ForcedPuncture","StatusVulnerability","KillHealthExplosion","GroundFinisherHealthExplosion","StaticDischarge","ExtraCombo","ComboOnCold","ComboOnSlash","ComboOnToxin","SlashComboChance"})if(effectKeys.Contains(key))unsupported.Add(MeleeEffectCoverage.Notes(new Dictionary<string,double>{[key]=1}).FirstOrDefault()??key);
        var strip=initial.Melee!.Effects.GetValueOrDefault("BaseArmorStrip");
        if(strip>0&&build.Encounter?.BaseArmor is not >0)unsupported.Add("섀터링 임팩트 계산에 필요한 적 기본 방어력 미입력");
        if(strip>0&&profile.Name is "Destreza" or "Galatine" or "Galatine Prime")unsupported.Add("충격 피해 양자화에 따른 섀터링 임팩트 예외");
        if(unsupported.Count>0)return new(mode,horizon,new(0,0),null,new(0,0),[],0,notes.ToArray(),unsupported.Distinct().ToArray());
        var cycleCount=Math.Ceiling(horizon/duration);
        if(cycleCount*steps.Length>50000)throw new ArgumentException("한 표본의 교전 타격이 50,000회를 넘었습니다. 입력한 동작 시간을 확인하세요.");
        var cycles=(int)cycleCount;var count=cycles*steps.Length;
        var totalsLow=new double[count];var totalsHigh=new double[count];var typesLow=new double[count];var typesHigh=new double[count];var armorLow=new double[count];var armorHigh=new double[count];
        var comboValues=new double[count];var threshold=profile.Code=="DexNikana"?11:20;var maximumCombo=catalog.Weapons.Single(w=>w.Id==build.WeaponId).MaxCombo;
        var endLow=0d;var endHigh=0d;var random=new Random(44061);var cache=new Dictionary<string,Calculation>();
        var dpsLow=new double[count];var dpsHigh=new double[count];var noCoLow=new double[count];var noCoHigh=new double[count];var gainLow=new double[count];var gainHigh=new double[count];
        var healthLow=new double[count];var healthHigh=new double[count];var shieldsLow=new double[count];var shieldsHigh=new double[count];var guardLow=new double[count];var guardHigh=new double[count];
        var layered=build.Melee.Shields>0||build.Melee.Overguard>0;
        if(layered)notes.Add("방어층은 타격 순서대로 소진하며 각 표본의 치명타 단계를 추첨합니다. 개별 타격 시각이 없는 경로는 직접 피해의 상태 만료 시나리오 범위이며 지속 피해·처치 확률은 포함하지 않습니다. 실드 게이트는 타격 구간과 겹칠 때 적용/종료 양쪽을 비교합니다.");
        var criticalRandom=new Random(90371);
        var bounds=sc is not null;
        if(bounds)notes.Add("개별 타격 시각이 없어 상태를 짧게 유지한 시나리오와 길게 유지한 시나리오를 비교합니다. 표시 범위가 가능한 모든 타이밍의 엄밀한 최솟값·최댓값을 보장하지는 않습니다. 표본의 통계 오차는 별도로 표시합니다.");
        else notes.Add($"첫 명중을 0초로 두고 사용자 지정 간격 {duration:0.###}초를 적용합니다.");
        if(possible.Contains(DamageType.Heat)&&bounds)notes.Add("스탠스의 화염 방깎 전개 시점도 범위로 반영합니다.");
        if(strip>0&&(build.Scenario.Enemy?.Armor is 200 or 2700))notes.Add("방어력 상·하한에 걸린 적은 제한 이전 방어력이 없어 기본 방어력 감소 결과를 범위로 표시합니다.");
        if(possible.Contains(DamageType.Heat))notes.Add("화염 종료 후 방어력 회복 도중 재점화 전개는 미검증으로 0~50% 방깎 범위에 포함합니다.");
        var firstMoments=new SampleMoments();var secondMoments=new SampleMoments();
        for(var trial=0;trial<trials;trial++)
        {
            if(trial%16==0)progress?.Report(new("타격 순서 시나리오",trial,trials));
            cancellation.ThrowIfCancellationRequested();var history=new List<Proc>();var removedArmor=0d;var sumLow=0d;var sumHigh=0d;var possiblePosture=false;
            var targetLow=new MeleeTtkEngine.TargetState(build.Scenario.Enemy?.Health??10000,build.Melee.Shields,build.Melee.Overguard);
            var targetHigh=new MeleeTtkEngine.TargetState(targetLow.Health,targetLow.Shields,targetLow.Overguard);
            var lastShieldBreakEarly=double.NegativeInfinity;var lastShieldBreakLate=double.NegativeInfinity;
            var comboPoints=Math.Max(initial.Melee!.InitialCombo,(build.Scenario.Combo-1)*threshold);
            for(var index=0;index<count;index++)
            {
                if(index%16==0)cancellation.ThrowIfCancellationRequested();
                var step=steps[index%steps.Length];var cycle=index/steps.Length;
                var early=cycle*duration;var late=bounds?(cycle+1)*duration:early;
                var low=Counts(history,early,late,false);var high=Counts(history,early,late,true);
                var baseArmor=build.Scenario.Enemy?.Armor??0;
                var fraction=strip>0?Math.Max(0,1-removedArmor/build.Encounter!.BaseArmor!.Value):1;
                var armor=baseArmor*fraction;var armorMinimum=armor;var armorMaximum=armor;
                if(strip>0&&fraction is >0 and <1){if(baseArmor==2700)armorMaximum=2700;if(baseArmor==200)armorMinimum=0;}
                var heatLow=HeatRange(history,early,late,false);var heatHigh=HeatRange(history,early,late,true);
                if(!bounds&&index>0&&duration>initial.Melee!.ComboDuration)comboPoints=initial.Melee.InitialCombo;
                var combo=Math.Min(maximumCombo,(int)Math.Floor(comboPoints/threshold)+1);comboValues[index]+=combo;
                var cLow=Evaluate(step,index%steps.Length,low,armorMaximum,heatLow,combo,false,false,targetLow);var cHigh=Evaluate(step,index%steps.Length,high,armorMinimum,heatHigh,combo,possiblePosture,false,targetHigh);
                var nLow=Evaluate(step,index%steps.Length,low,armorMaximum,heatLow,combo,false,true,targetLow);var nHigh=Evaluate(step,index%steps.Length,high,armorMinimum,heatHigh,combo,possiblePosture,true,targetHigh);
                double Snapshot(Dictionary<DamageType,int> state,double a,double h,bool posture,bool noCo,Calculation c,MeleeTtkEngine.TargetState target)
                    =>layered&&target.Health<=0?0:sc is null?c.TargetDps:steps.Select((s,i)=>Evaluate(s,i,state,a,h,combo,posture,noCo,target)).Sum(v=>v.TargetDps*v.HitInterval)/(duration*initial.AttackSpeed/c.AttackSpeed);
                var dl=Snapshot(low,armorMaximum,heatLow,false,false,cLow,targetLow);var dh=Snapshot(high,armorMinimum,heatHigh,possiblePosture,false,cHigh,targetHigh);
                var nl=Snapshot(low,armorMaximum,heatLow,false,true,nLow,targetLow);var nh=Snapshot(high,armorMinimum,heatHigh,possiblePosture,true,nHigh,targetHigh);
                dpsLow[index]+=dl;dpsHigh[index]+=dh;noCoLow[index]+=nl;noCoHigh[index]+=nh;
                gainLow[index]+=dl-nl;gainHigh[index]+=dh-nh;
                var criticalRoll=criticalRandom.NextDouble();
                double Apply(Calculation calculation,MeleeTtkEngine.TargetState target,bool lower)
                {
                    if(!layered)return calculation.TargetDps*calculation.HitInterval;
                    if(target.Health<=0)return 0;
                    var before=target.Health+target.Shields+target.Overguard;var hadShield=target.Shields>0;
                    target.ShieldGateUntil=lower&&early<lastShieldBreakLate+.1||!lower&&late<lastShieldBreakEarly+.1?double.PositiveInfinity:-1;
                    var tier=Math.Floor(calculation.CriticalChance)+(criticalRoll<calculation.CriticalChance%1?1:0);
                    target.Hit(calculation,MeleeStealth.AtTier(calculation,tier),lower?early:late);
                    target.Health=Math.Max(0,target.Health);
                    if(hadShield&&target.Shields==0){lastShieldBreakEarly=early;lastShieldBreakLate=late;}
                    return before-target.Health-target.Shields-target.Overguard;
                }
                if(late<=horizon+1e-9)sumLow+=Apply(cLow,targetLow,true);
                if(early<horizon-1e-9)sumHigh+=Apply(cHigh,targetHigh,false);
                totalsLow[index]+=sumLow;totalsHigh[index]+=sumHigh;typesLow[index]+=low.Count;typesHigh[index]+=high.Count+(possiblePosture?1:0);
                healthLow[index]+=Math.Min(targetLow.Health,targetHigh.Health);healthHigh[index]+=Math.Max(targetLow.Health,targetHigh.Health);
                shieldsLow[index]+=Math.Min(targetLow.Shields,targetHigh.Shields);shieldsHigh[index]+=Math.Max(targetLow.Shields,targetHigh.Shields);
                guardLow[index]+=Math.Min(targetLow.Overguard,targetHigh.Overguard);guardHigh[index]+=Math.Max(targetLow.Overguard,targetHigh.Overguard);
                armorLow[index]+=MeleeTargetModel.EffectiveArmor(armorMinimum,cHigh.Melee!.Setup);armorHigh[index]+=MeleeTargetModel.EffectiveArmor(armorMaximum,cLow.Melee!.Setup);
                // Proc distribution is independent of CO/armor. Tau and conditional
                // changes to status chance are rejected above instead of approximated.
                var procs=RollProcs(cLow,random);
                foreach(var type in procs){var lifetime=Lifetime(type,cLow);if(lifetime>0)history.Add(new(type,early,late,lifetime));}
                if((!layered||targetLow.Overguard==0||targetHigh.Overguard==0)&&step.Initial.Melee!.ForcedProcs.Any(p=>p is "Lifted" or "Knockdown"))possiblePosture=true;
                if(strip>0&&step.Initial.Damage.GetValueOrDefault(DamageType.Impact)>0)removedArmor+=strip;
                var gained=cLow.Melee!.HitConnects?Math.Max(0,(step.Hit?.Multiplier??1)-1+cLow.Melee.ComboChance):0;comboPoints+=Math.Floor(gained)+(random.NextDouble()<gained%1?1:0);
            }
            endLow+=Counts(history.Where(p=>p.Latest<=horizon).ToList(),horizon,horizon,false).Count;endHigh+=Counts(history.Where(p=>p.Earliest<horizon).ToList(),horizon,horizon,true).Count+(possiblePosture?1:0);
            firstMoments.Add(sumLow);secondMoments.Add(sumHigh);
        }
        var samples=Enumerable.Range(0,count).Select(i=>new EncounterSample(i+1,i/steps.Length*duration,bounds?(i/steps.Length+1)*duration:i*duration,new(Math.Min(totalsLow[i],totalsHigh[i])/trials,Math.Max(totalsLow[i],totalsHigh[i])/trials),new(typesLow[i]/trials,typesHigh[i]/trials),new(armorLow[i]/trials,armorHigh[i]/trials),comboValues[i]/trials,steps[i%steps.Length].Initial.Melee!.ForcedProcs,steps[i%steps.Length].CoFactor,new(Math.Min(dpsLow[i],dpsHigh[i])/trials,Math.Max(dpsLow[i],dpsHigh[i])/trials),new(Math.Min(noCoLow[i],noCoHigh[i])/trials,Math.Max(noCoLow[i],noCoHigh[i])/trials),new(Math.Min(gainLow[i],gainHigh[i])/trials,Math.Max(gainLow[i],gainHigh[i])/trials),sc?.TargetDps??first.TargetDps,layered?new(healthLow[i]/trials,healthHigh[i]/trials):null,layered?new(shieldsLow[i]/trials,shieldsHigh[i]/trials):null,layered?new(guardLow[i]/trials,guardHigh[i]/trials):null)).ToArray();
        var damage=samples[^1].Damage;
        progress?.Report(new("타격 순서 시나리오",trials,trials));
        return new(mode,horizon,damage,new(damage.Low/horizon,damage.High/horizon),new(endLow/trials,endHigh/trials),samples,trials,notes.ToArray(),[],Precision:new(firstMoments.Estimate(),secondMoments.Estimate()));

        Calculation Evaluate(Step step,int stepId,Dictionary<DamageType,int> state,double armor,double heat,int combo,bool posture,bool withoutCo=false,MeleeTtkEngine.TargetState? target=null)
        {
            if(target?.Overguard>0)posture=false;
            if(withoutCo){var full=Evaluate(step,stepId,state,armor,heat,combo,posture,false,target);return full with {TargetDps=CombatTimeline.WithoutConditionOverloadDps(full)};}
            var key=stepId+":"+combo+":"+armor+":"+heat+":"+posture+":"+(target?.Overguard>0)+":"+(target?.Shields>0)+":"+string.Join(',',state.OrderBy(p=>p.Key).Select(p=>$"{(int)p.Key}={CombatTimeline.DirectStackKey(p.Key,p.Value)}"));
            if(cache.TryGetValue(key,out var found))return found;
            var conditions=new Dictionary<string,double>(step.Input.Melee!.Conditions??[]){["Knockdown"]=posture?1:0,["Lifted"]=0};
            var input=step.Input with {Melee=step.Input.Melee! with {Statuses=state,HeatArmorStrip=heat,Conditions=conditions,Shields=target?.Shields??step.Input.Melee.Shields,Overguard=target?.Overguard??step.Input.Melee.Overguard},Scenario=step.Input.Scenario with {Combo=combo,ExistingStatusTypes=0,Enemy=(step.Input.Scenario.Enemy??new()) with {Armor=armor}}};
            var result=MeleeEngine.CalculateAttack(catalog,input,step.Hit,step.CoFactor,step.Finisher,step.Slide);
            if(cache.Count<8192)cache[key]=result;return result;
        }
    }
    public static EncounterResult Calculate(Catalog catalog,Build build,StanceCatalog stances,int? trials=null,CancellationToken cancellation=default,IProgress<SimulationProgress>? progress=null)
    {
        var sampleCount=trials??build.Encounter?.Samples??2048;
        if(sampleCount is <1 or >4096)throw new ArgumentException("교전 표본 수는 1~4096이어야 합니다.");
        var mode=build.Encounter?.Mode??EncounterMode.FirstHit;
        if(mode is EncounterMode.FirstHit or EncounterMode.Primer)
        {
            if(mode==EncounterMode.FirstHit)build=build with {Scenario=build.Scenario with {ExistingStatusTypes=0},Melee=build.Melee! with {Statuses=[],Conditions=(build.Melee!.Conditions??[]).Where(p=>p.Key is not ("Lifted" or "Knockdown" or "Microwave")).ToDictionary()},Arcanes=build.Arcanes is {} a?a with {Frozen=false}:null};
            var baseline=CalculateOrdered(catalog,build,stances,sampleCount,cancellation,progress);
            if(baseline.Unsupported.Length>0)return baseline;
            CombatTimelineResult first;
            try{first=CombatTimeline.Calculate(catalog,build,stances,sampleCount,cancellation,progress);}
            catch(CombatRuleUnavailableException ex){return baseline with {Notes=baseline.Notes.Append(ex.Message).ToArray(),Unsupported=build.Encounter?.NearbyEnemies is {Length:>0}||build.Encounter?.Contagion is {Length:>0}||build.Encounter?.Epidemic is {Length:>0}||build.Encounter?.IchorClouds is {Length:>0}||build.Encounter?.CaustacystPools is {Length:>0}||build.Encounter?.OkinaDaggers is {Length:>0}||build.Encounter?.CeramicDaggers is {Length:>0}||build.Encounter?.ZenistarDiscs is {Length:>0}||build.Encounter?.OrviusHovers is {Length:>0}?[ex.Message]:baseline.Unsupported};}
            if(first.Unsupported.Length>0)return baseline with {Timeline=first,Notes=baseline.Notes.Concat(first.Unsupported).ToArray(),Unsupported=build.Encounter?.NearbyEnemies is {Length:>0}||build.Encounter?.Contagion is {Length:>0}||build.Encounter?.Epidemic is {Length:>0}||build.Encounter?.IchorClouds is {Length:>0}||build.Encounter?.CaustacystPools is {Length:>0}||build.Encounter?.OkinaDaggers is {Length:>0}||build.Encounter?.CeramicDaggers is {Length:>0}||build.Encounter?.ZenistarDiscs is {Length:>0}||build.Encounter?.OrviusHovers is {Length:>0}?first.Unsupported:baseline.Unsupported};
            var dealt=first.DirectDamage+first.StatusDamage;var after=first.Points[^1].StatusTypes;
            return new(mode,null,new(dealt,dealt),null,new(after,after),[],sampleCount,
                ["첫 접촉의 실제 대상 피해 · 산탄/듀플리케이트는 순서대로 처리 · 생존 대상의 같은 접촉 상태를 힐링 리턴에 반영 · 지연된 상태 틱은 이후 시간에 발생",..first.Notes],[],first,Precision:first.DamageEstimate is {} firstEstimate?new(firstEstimate):null);
        }
        build=build with {Scenario=build.Scenario with {ExistingStatusTypes=0},Melee=build.Melee! with {Statuses=[]}};
        CombatTimelineResult timeline;
        try{timeline=CombatTimeline.Calculate(catalog,build,stances,sampleCount,cancellation,progress);}
        catch(CombatRuleUnavailableException ex){var seconds=mode==EncounterMode.FiveSeconds?5:mode==EncounterMode.TenSeconds?10:0;timeline=new(seconds,0,0,new(null,null,null,0,0,44061,seconds),[],[],0,[],[ex.Message]);}
        if(timeline.Unsupported.Length>0)
        {
            if(build.Encounter?.NearbyEnemies is {Length:>0}||build.Encounter?.Contagion is {Length:>0}||build.Encounter?.Epidemic is {Length:>0}||build.Encounter?.IchorClouds is {Length:>0}||build.Encounter?.CaustacystPools is {Length:>0}||build.Encounter?.OkinaDaggers is {Length:>0}||build.Encounter?.CeramicDaggers is {Length:>0}||build.Encounter?.ZenistarDiscs is {Length:>0}||build.Encounter?.OrviusHovers is {Length:>0})return new(mode,timeline.Seconds,new(0,0),null,new(0,0),[],sampleCount,timeline.Notes,timeline.Unsupported,timeline);
            var ordered=CalculateOrdered(catalog,build,stances,sampleCount,cancellation,progress);
            if(ordered.DamageOnly)return ordered;
            return ordered with {Timeline=timeline,Notes=ordered.Notes.Concat(timeline.Unsupported).ToArray()};
        }
        var total=timeline.DirectDamage+timeline.StatusDamage;
        var end=timeline.Points[^1].StatusTypes;
        return new(mode,timeline.Seconds,new(total,total),new(total/timeline.Seconds,total/timeline.Seconds),new(end,end),[],sampleCount,timeline.Notes,[],timeline,Precision:timeline.DamageEstimate is {} estimate?new(estimate):null);
    }
    private static Dictionary<DamageType,int> Counts(List<Proc> history,double early,double late,bool maximum)
    {
        return history.Where(p=>(maximum?p.Latest+ p.Duration>early+1e-9:p.Earliest+p.Duration>late+1e-9))
            .GroupBy(p=>p.Type).ToDictionary(g=>g.Key,g=>Math.Min(TargetStatusRules.Maximum(g.Key),g.Count()));
    }
    private static double HeatRange(List<Proc> history,double early,double late,bool maximum)
    {
        var heat=history.Where(p=>p.Type==DamageType.Heat).ToArray();if(heat.Length==0)return 0;
        if(early!=late||heat.Any(p=>p.Earliest!=p.Latest))
        {
            if(!maximum)return 0;
            var possible=heat.Where(p=>p.Latest+p.Duration+6>early).ToArray();
            return possible.Length==0?0:HeatStrip(late-possible.Min(p=>p.Earliest),possible.Min(p=>p.Duration)/6);
        }
        var start=heat[0].Earliest;var end=start+heat[0].Duration;var multiplier=heat[0].Duration/6;var reignited=false;
        foreach(var p in heat.Skip(1))
        {if(p.Earliest>end){reignited=p.Earliest<end+6;start=p.Earliest;multiplier=p.Duration/6;}else multiplier=Math.Max(multiplier,p.Duration/6);end=p.Earliest+6*multiplier;}
        if(reignited&&early<end+6)return maximum?.5:0;
        var peak=HeatStrip(Math.Min(early,end)-start,multiplier);
        if(early<=end)return peak;
        var stage=(int)Math.Floor((early-end)/1.5);
        return Math.Min(peak,stage switch{0=>.5,1=>.4,2=>.3,3=>.15,_=>0});
    }
    private static IEnumerable<DamageType> RollProcs(Calculation c,Random random)
    {
        var total=c.Damage.Values.Sum();var rolls=(int)Math.Floor(c.StatusChance)+(random.NextDouble()<c.StatusChance%1?1:0);
        for(var i=0;i<rolls;i++)
        {var at=random.NextDouble()*total;foreach(var (type,amount) in c.Damage){at-=amount;if(at<0){if(DamageTypeRules.HasStatus(type))yield return type;break;}}}
        foreach(var name in c.Melee!.ForcedProcs)if(Enum.TryParse<DamageType>(name,out var type)&&DamageTypeRules.HasStatus(type))yield return type;
    }
}
