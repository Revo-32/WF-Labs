namespace TennoForge.Core;

public record CombatPoint(double Time,double DirectDamage,double StatusDamage,double Health,double Shields,double Overguard,double Armor,double StatusTypes,double Combo,double SnapshotDps=0,double WithoutCoDps=0,double StartDps=0,double ComboPoints=0,double? PlayerShields=null,double? PlayerOvershields=null,double InvigoratedStacks=0,double? BraveEnergy=null,double? BraveRate=null,double? PlayerArmor=null,double FortificationArmor=0,double? FinisherParkourBonus=null,double? AnimosityBonus=null,double? JadeStatusDurationBonus=null,double? IncarnonRemaining=null,double IncarnonPunctureBonus=0,double IncarnonResilience=0,double? SoulCharges=null,double? SoulsInFlight=null,double? TennokaiRemainingMinimum=null,double? TennokaiRemainingMaximum=null,double TennokaiUses=0,double TennokaiRefunds=0,double TennokaiFailures=0,double TennokaiRawCurse=0,double? MeleeRange=null,double? HotShotStacks=null,double? PowerThrowStacks=null,double? VinquibusPrecision=null,double? VinquibusFerocity=null,double? ValkyrRage=null,double? MagusAggressCharges=null,double? WarcryRemaining=null,double WarcryArmor=0,double? HologramCount=null,double HallMalevolence=0,double? RubbleArmor=null,double RubbleHealing=0,double RubbleDrops=0);
public record CombatRecovery(SampleEstimate? Energy=null,SampleEstimate? ExodiaHealing=null,SampleEstimate? HostFinisherHealing=null,SampleEstimate? HeavyHealing=null,SampleEstimate? HealingReturn=null,SampleEstimate? HealingReturnUpper=null,SampleEstimate? SanctiHealing=null,SampleEstimate? SanctiHealingUpper=null,SampleEstimate? HealthRegeneration=null,SampleEstimate? IncarnonHealing=null,SampleEstimate? HysteriaHealing=null,SampleEstimate? DroppedUniversalOrbs=null,SampleEstimate? NikanaHealing=null);
public record CombatEvent(double Time,string Kind,string Description,double Amount=0);
public record CombatTimelineResult(double Seconds,double DirectDamage,double StatusDamage,TtkResult Ttk,
    CombatPoint[] Points,CombatEvent[] Events,int Trials,string[] Notes,string[] Unsupported,SampleEstimate? DamageEstimate=null,SecondaryBurst[]? SecondaryBursts=null,double? CriticalHealing=null,CombatRecovery? Recovery=null,CombatEnemyResult[]? Enemies=null,bool HasSnapshotDps=true);
public record CombatInput(double Time,string Kind,double Value=1,DamageType? Element=null,double? Duration=null,int[]? RecentlyAwareTargets=null);
public sealed class CombatRuleUnavailableException(string message):ArgumentException(message);

/// <summary>Finite, cancellable single-target event simulation. No invented stance timestamps.</summary>
public static class CombatTimeline
{
    public const string Version="combat-events-0.7";
    public static bool AutomaticArcane(string code)=>code is "Eruption" or "UniversalFallout" or "Truculence" or "Fury" or "Strike" or "Avenger" or "BladeCharger" or "Exposure" or "Arachne" or "Assimilation" or "Might" or "Guardian" or "Reaper" or "Ultimatum" or "Tanker" or "Trickery";
    private static readonly HashSet<string> DynamicKeys=["Frenzy","GasBuff","HeavyInsight","SibearBuff","ExtendedClaws","Overheat","GraviticSlash","SlamStatus","Impaled","ProtonSnap","PennantKills","BerserkerStacks","GalvanizedStacks","OnEquip","FinisherBuff","SlideKill","HeavyKill","PunctureStacks","NeutralFinalHit","RuvoxKills","AirborneKills","CriticalStacks","ColdKills","ThalysStatus","ImpaledFive","SlideHitBuff","ShieldBreak","SpringStacks","HotShotStacks"];
    public static Build InitialState(Build build)
    {
        if(build.Melee is null||build.Encounter?.Mode is null or EncounterMode.FirstHit or EncounterMode.Primer)return build;
        ArcaneSlot? Clear(ArcaneSlot? a)=>a is not null&&AutomaticArcane(MeleeRuleCatalog.Load().Arcanes.Single(r=>r.Id==a.Id).Code)?a with {Active=false}:a;
        var a=build.Arcanes;
        return build with {Arcanes=a is null?null:a with {Melee=Clear(a.Melee),Warframe1=Clear(a.Warframe1),Warframe2=Clear(a.Warframe2),ExposureStacks=0,Frozen=false},Melee=build.Melee with {Exodia=Clear(build.Melee.Exodia),Conditions=(build.Melee.Conditions??[]).Where(p=>!DynamicKeys.Contains(p.Key)).ToDictionary(),Statuses=[],HeatArmorStrip=0},Scenario=build.Scenario with {ExistingStatusTypes=0}};
    }
    // Only these stack counts affect a direct-hit calculation. Other types affect CO by presence.
    internal static int DirectStackKey(DamageType type,int count)=>type is DamageType.Puncture or DamageType.Cold or DamageType.Tau or DamageType.Corrosive or DamageType.Viral or DamageType.Magnetic?count:Math.Min(1,count);
    public static double WithoutConditionOverloadDps(Calculation calculation)
    {
        var m=calculation.Melee??throw new ArgumentException("근접 계산 결과 필요");
        var extra=m.ExtraDamage>0?m.ExtraDamage*EnergyChannelState.TargetMultiplier(m.Setup,calculation.ArmorReduction,m.IncomingDamageMultiplier)/calculation.HitInterval:0;
        var bracket=m.Brackets!;
        if(bracket.ModdedBaseDamage<=0)return extra;
        var plain=bracket with {AdditiveConditionOverload=0,ConditionMultiplier=1};
        return (calculation.TargetDps-extra)*plain.ModdedBaseDamage/bracket.ModdedBaseDamage+extra;
    }
    private sealed class Proc
    {
        public DamageType Type;public double End,Next,Tick,Basis,Element=1,Faction=1,Duration;
        public bool WeaponSource=true;
        public double ReceiverFaction=1;
        public Calculation Source=null!;
    }
    // Enemy-owned clocks and damage are separate from the player's shared
    // combo, arcane timers, transformation and recovery state.
    private sealed class EnemyRuntime
    {
        public Build Initial {get;}
        public int Index {get;}
        public CombatEnemy? Position {get;}
        public Dictionary<string,double> Conditions {get;}
        public MeleeTtkEngine.TargetState Pools {get;}
        public List<Proc> Procs {get;}=[];
        public UniversalFalloutState? Fallout {get;set;}
        public double Armor {get;}
        public double RemovedArmor,DirectDamage,StatusDamage;
        public bool DisarmApplied;
        public readonly FinisherExposureState FinisherExposure=new();
        public double EruptionEnd=-1,PoolBlindEnd=-1,BladeBlindEnd=-1,PoolBlindUnknownSince=double.PositiveInfinity;
        public double HeatStart=-1,HeatEnd=-1,HeatDuration=6,FrozenEnd=-1,ImpaleEnd=-1;
        public double StealthReadyAt,SpinnerLastHit=double.NegativeInfinity,KillTime=double.PositiveInfinity;
        public EnemyRuntime(Build build,int index=0,CombatEnemy? position=null)
        {
            Index=index;Position=position;Conditions=(build.Melee!.Conditions??[]).Where(p=>CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();
            Initial=build;Pools=new(build.Scenario.Enemy?.Health??10000,build.Melee!.Shields,build.Melee.Overguard);
            Armor=build.Scenario.Enemy?.Armor??0;StealthReadyAt=build.Melee.Conditions?.GetValueOrDefault("StealthCooldown")??0;
        }
    }
    private record Step(Build Build,StanceHit? Hit,double Co,bool Finisher,bool Slide,bool WhipDistribution=false,ContagionPhase? Contagion=null,double Travel=0,bool Delayed=false,double? PrecisionAtShot=null,EpidemicPhase? Epidemic=null,string? BounceThrow=null,bool IchorCloud=false,bool CaustPool=false,bool? OkinaExplosion=null,int? CeramicDagger=null,bool CeramicExplosion=false,string? ZenistarAttack=null,double ZenistarWrathful=0,bool? OrviusExplosion=null,GlaiveFlight? Glaive=null,IncarnonBladeContact? Spectral=null,bool SpectralExplosion=false,double SpectralMotion=1,InnodemContact? Innodem=null,bool? VitricaOrigin=null,bool VitricaHeavy=false,ExaltedWaveContact? ExaltedWave=null,StanceHitDamage? WaveSource=null,int WaveTargetIndex=1,Build? AckBladeSource=null,bool Aerial=false,bool WolfBounce=false,Build? WolfHeavySource=null,Build? ShieldSource=null,bool ShieldExplosion=false,bool CobraFirst=false,double? ShieldFinisherSeconds=null,WeaponActionInput? WeaponAction=null,WeaponActionContact? WeaponContact=null,bool WeaponExplosion=false,int Hologram=0,DestrezaContact? Rapier=null);
    private record DelayedGunshot(double Time,EnemyRuntime Center,Step Step,Build Source);
    private record IchorField(double Created,EnemyRuntime Center,Build Source);
    private static readonly Dictionary<DamageType,double> Lifetimes=new()
    {
        [DamageType.Impact]=6,[DamageType.Puncture]=10,[DamageType.Slash]=6,[DamageType.Heat]=6,
        [DamageType.Cold]=6,[DamageType.Electricity]=6,[DamageType.Toxin]=6,[DamageType.Gas]=6,
        [DamageType.Blast]=1.5,[DamageType.Radiation]=12,[DamageType.Magnetic]=6,[DamageType.Viral]=6,
        [DamageType.Corrosive]=8,[DamageType.Void]=3,[DamageType.Tau]=8
    };
    public static CombatTimelineResult Calculate(Catalog catalog,Build build,StanceCatalog stances,int trials=2048,CancellationToken token=default,IProgress<SimulationProgress>? progress=null)
    {
        if(trials is <1 or >4096)throw new ArgumentException("전투 표본은 1~4096회입니다.");
        if(build.Melee is null)throw new ArgumentException("근접 설정이 필요합니다.");
        token.ThrowIfCancellationRequested();
        build.Melee.ContagionBuffs?.Validate();
        if(build.Melee.ContagionBuffs?.Active==true&&build.Encounter?.ScriptedAttacksOnly!=true)throw new ArgumentException("컨테이전 전용 외부 버프를 지정하면 ‘투사체만 계산’을 선택하세요. 이 입력은 일반 검 타격의 외부 버프 설정과 구분합니다.");
        var settings=build.Encounter??new();var mode=settings.Mode;var timed=mode is not (EncounterMode.FirstHit or EncounterMode.Primer);var requestedTennokai=build.Melee.Tennokai;
        var jetStream=build.Melee.ExternalEquipment is {JetStream:true} externalJet?new JetStreamTimeline(externalJet,settings.Inputs):null;jetStream?.ValidateFlights(settings);
        var targetOrder=settings.TargetOrder??[0];
        if(settings.AttackOrder is {Length:>0} attackOrder)
        {
            if(build.Stance is not null)throw new ArgumentException("공격 동작 순서는 단일 공격 방식에서 설정하세요. 스탠스 동작 시각과 혼합하지 않습니다.");
            if(attackOrder.Length>64||attackOrder.Any(a=>!MeleeEngine.Attacks(build.WeaponId).Contains(a)))throw new ArgumentException("공격 동작 순서에 이 무기의 지원 동작을 선택하세요.");
            build=build with {Melee=build.Melee with {Attack=attackOrder[0],Tennokai=build.Melee.Tennokai&&MeleeEngine.AttackPolicy(build.WeaponId,attackOrder[0]).Heavy}};
        }
        var enemyBuilds=CombatScene.Validate(build);
        enemyBuilds=enemyBuilds.Select(b=>TargetDebuffs.ControlApplies(b.Melee!)?b:b with {Melee=b.Melee! with {Debuffs=(b.Melee!.Debuffs??[]).Where(d=>d.Code=="SplinterStorm").ToArray()}}).ToArray();
        var rubblePickups=(settings.RubblePickups??[]).OrderBy(p=>p.Time).ToArray();AtlasRubbleState.Validate(rubblePickups,enemyBuilds.Length);
        if(rubblePickups.Length>0&&build.Melee!.ExternalEquipment?.AtlasRubble is null)throw new ArgumentException("러블 습득에는 아틀라스 러블 설정이 필요합니다.");
        if(!Enum.IsDefined(mode))throw new ArgumentException("교전 조건 오류");
        if(settings.BaseArmor is {} baseArmor)MeleeEngine.Range(baseArmor,.001,1e9,"적 기본 방어력");
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);
        var collisions=(settings.Contagion??[]).OrderBy(c=>c.Time).ToArray();
        if(collisions.Length>256)throw new ArgumentException("컨테이전 접촉은 최대256회입니다.");
        foreach(var collision in collisions)ContagionFlightState.Validate(collision,enemyBuilds.Length);
        if(collisions.Length>0)
        {
            _=ExodiaContagionMechanic.Calculate(catalog,build,collisions[0].Travel);
            if(build.Stance is not null)throw new ArgumentException("컨테이전 접촉 시나리오는 공격 방식 ‘단일 공격’에서 사용하세요. 장착 스탠스의 폭발 배율은 유지됩니다.");
        }
        var epidemic=(settings.Epidemic??[]).OrderBy(e=>e.Time).ToArray();
        if(epidemic.Length>256)throw new ArgumentException("에피데믹 명중은 최대256회입니다.");
        foreach(var impact in epidemic)ExodiaEpidemicMechanic.Validate(impact,enemyBuilds.Length);
        if(epidemic.Length>0)_=ExodiaEpidemicMechanic.Calculate(catalog,build);
        var cloudContacts=(settings.IchorClouds??[]).OrderBy(c=>c.Time).ToArray();
        if(cloudContacts.Length>256)throw new ArgumentException("독 구름 명중은 최대256회입니다.");
        if(cloudContacts.Length>0&&!DualIchorCloudMechanic.Applies(build.WeaponId))throw new ArgumentException("독 구름 명중은 듀얼 이코르에서 사용합니다.");
        foreach(var contact in cloudContacts)
        {MeleeEngine.Range(contact.Time,0,300,"독 구름 명중 시각");MeleeEngine.Range(contact.Cloud,1,10000,"독 구름 번호");MeleeEngine.Range(contact.Target,1,enemyBuilds.Length,"독 구름 대상 번호");}
        var okinaFlights=settings.OkinaDaggers??[];
        if(okinaFlights.Length>256||okinaFlights.Length>0&&!OkinaDaggerState.Applies(build.WeaponId))throw new ArgumentException("오키나 비행은 해당 무기에서 최대256회입니다.");
        foreach(var flight in okinaFlights)OkinaDaggerState.Validate(flight,enemyBuilds.Length);
        if(okinaFlights.GroupBy(f=>f.Dagger).Any(g=>g.Count()>1))throw new ArgumentException("같은 오키나 단검의 비행이 중복되었습니다.");
        var okinaEvents=okinaFlights.SelectMany(f=>new[]{(Time:f.Launch,Launch:true,Flight:f),(Time:f.Impact,Launch:false,Flight:f)}).OrderBy(e=>e.Time).ThenByDescending(e=>e.Launch).ToArray();
        var poolWindows=settings.CaustacystPools??[];var poolTicks=CaustacystMechanic.Schedule(build.WeaponId,poolWindows,enemyBuilds.Length);
        var ceramicContacts=settings.CeramicDaggers??[];
        CeramicDaggerMechanic.Validate(ceramicContacts,enemyBuilds.Length);
        if(ceramicContacts.Length>0&&build.WeaponId!=CeramicDaggerMechanic.WeaponId)throw new ArgumentException("세라믹 단검 비행은 세라믹 대거에서 사용합니다.");
        if(ceramicContacts.Length>0&&!settings.ScriptedAttacksOnly&&(build.Stance is not null||MeleeEngine.AttackPolicy(build.WeaponId,build.Melee!.Attack).Heavy))throw new ArgumentException("세라믹 발사는 강공격 한 번의 콤보를 사용합니다. ‘투사체만 계산’ 또는 별도의 일반 타격을 선택해 강공격을 이중 입력하지 않도록 하세요.");
        var ceramicEvents=ceramicContacts.Select(c=>(Time:c.Impact,Launch:false,Contact:c)).Concat(ceramicContacts.GroupBy(c=>c.Volley).Select(g=>(Time:g.First().Launch,Launch:true,Contact:g.First()))).OrderBy(e=>e.Time).ThenByDescending(e=>e.Launch).ToArray();
        var zenistarDeployments=settings.ZenistarDiscs??[];ZenistarDiscState.Validate(zenistarDeployments,enemyBuilds.Length);
        if(zenistarDeployments.Length>0&&build.WeaponId!=ZenistarDiscState.WeaponId)throw new ArgumentException("원반 배치는 제니스타에서 사용합니다.");
        if(zenistarDeployments.Length>0&&!settings.ScriptedAttacksOnly&&(build.Stance is not null||MeleeEngine.AttackPolicy(build.WeaponId,build.Melee!.Attack).Heavy))throw new ArgumentException("원반 발사/회수의 칼날 접촉 시각은 별도입니다. 투사체만 계산 또는 일반 단일 공격을 선택하세요.");
        var zenistarEvents=zenistarDeployments.SelectMany(d=>new[]{(Time:d.Launch,Kind:0,Disc:d),(Time:d.Land,Kind:1,Disc:d)}.Concat((d.AuraTimes??[]).Select(t=>(Time:t,Kind:2,Disc:d)))).OrderBy(e=>e.Time).ThenBy(e=>e.Kind).ToArray();
        var exaltedWaves=settings.ExaltedWaves??[];ExaltedWaveFlight.Validate(catalog,stances,build,exaltedWaves,enemyBuilds.Length);
        var waveSources=exaltedWaves.GroupBy(f=>f.Projectile).ToDictionary(g=>g.Key,g=>ExaltedWaveFlight.Resolve(catalog,stances,build,g.First()));
        var waveEvents=exaltedWaves.GroupBy(f=>f.Cast).Select(g=>(Time:g.First().Launch,Launch:true,Flight:g.First())).Concat(exaltedWaves.Select(f=>(Time:f.Impact,Launch:false,Flight:f))).OrderBy(e=>e.Time).ThenByDescending(e=>e.Launch).ToArray();
        var vitrify=(settings.VitricaGlasses??[]).OrderBy(g=>g.Time).ToArray();var glassSlams=(settings.VitricaSlams??[]).GroupBy(s=>s.Slam).OrderBy(g=>g.First().Time).ToArray();
        VitricaGlassState.Validate(vitrify,settings.VitricaSlams??[],enemyBuilds.Length);
        if((vitrify.Length+glassSlams.Length)>0&&build.WeaponId!=VitricaGlassState.WeaponId)throw new ArgumentException("유리화·슬램 접촉은 비트리카에서 사용합니다.");
        var rapierContacts=settings.DestrezaContacts??[];DestrezaFlight.Validate(rapierContacts,enemyBuilds.Length);
        if(rapierContacts.Length>0&&(build.WeaponId is not (MeleeMechanicScope.DestrezaId or MeleeMechanicScope.DestrezaPrimeId)||!settings.ScriptedAttacksOnly))throw new ArgumentException("레이피어 명중은 데스트레자의 투사체만 계산 조건에서 지정하세요. 칼날을 자동 중복 입력하지 않습니다.");
        var rapierEvents=rapierContacts.GroupBy(c=>c.Volley).Select(g=>(Time:g.First().Launch,Launch:true,Contact:g.First())).Concat(rapierContacts.Select(c=>(Time:c.Impact,Launch:false,Contact:c))).OrderBy(e=>e.Time).ThenByDescending(e=>e.Launch).ToArray();
        var innodemContacts=settings.InnodemContacts??[];InnodemFlight.Validate(innodemContacts,enemyBuilds.Length);
        if(innodemContacts.Length>0&&build.WeaponId!=InnodemFlight.WeaponId)throw new ArgumentException("인노뎀 비행은 인노뎀에서만 사용합니다.");
        var innodemEvents=innodemContacts.GroupBy(c=>c.Projectile).Select(g=>(Time:g.First().Launch,Launch:true,Contact:g.First())).Concat(innodemContacts.Select(c=>(Time:c.Impact,Launch:false,Contact:c))).OrderBy(e=>e.Time).ThenByDescending(e=>e.Launch).ToArray();
        var bladeFlights=settings.IncarnonBlades??[];IncarnonBladeFlight.Validate(bladeFlights,build.WeaponId,enemyBuilds.Length);
        if(bladeFlights.Any(f=>f.BladeImpact is not null)&&!settings.ScriptedAttacksOnly)throw new ArgumentException("칼날 접촉을 직접 지정했으면 자동 반복 공격을 끄세요. 같은 공격을 중복 입력하지 않습니다.");
        var physicalBladeHits=bladeFlights.Where(f=>f.BladeImpact is not null).ToDictionary(f=>f.Projectile,f=>IncarnonBladeFlight.PhysicalContact(catalog,stances,build,f));
        var bladeMotion=bladeFlights.ToDictionary(f=>f.Projectile,f=>IncarnonBladeFlight.MotionMultiplier(catalog,stances,build,f));
        var bladeEvents=bladeFlights.SelectMany(f=>new[]{(Time:Math.Min(f.Launch,f.BladeImpact??f.Launch),Kind:-1,Flight:f),(Time:f.Launch,Kind:0,Flight:f),(Time:f.Impact,Kind:1,Flight:f),(Time:f.Impact+IncarnonBladeFlight.Delay(build.WeaponId),Kind:2,Flight:f)}.Concat(f.BladeImpact is {} t?new[]{(Time:t,Kind:3,Flight:f)}:[])).OrderBy(e=>e.Time).ThenBy(e=>e.Kind).ToArray();
        var orviusHovers=settings.OrviusHovers??[];OrviusHoverState.Validate(orviusHovers,enemyBuilds.Length);
        var cobraSleep=ShieldThrowMechanic.IsCobra(build.WeaponId)&&(build.Melee!.Conditions?.GetValueOrDefault("Baruuk")??0)>0;
        HallOfMirrorsRules.ValidateContacts(build,enemyBuilds.Length);
        var hologramContacts=(settings.HologramContacts??[]).OrderBy(c=>c.Time).ToArray();
        var weaponActions=settings.WeaponActions??[];var weaponActionEvents=WeaponActionFlight.Schedule(build.WeaponId,weaponActions,enemyBuilds.Length,build.Melee.ExternalEquipment);
        if(weaponActions.Length>0&&!settings.ScriptedAttacksOnly)throw new ArgumentException("고유 공격의 칼날과 투사체 접촉을 지정했으면 자동 반복 공격을 끄세요.");
        var shieldFlights=settings.ShieldFlights??[];ShieldFlightRules.Validate(shieldFlights,build.WeaponId,enemyBuilds.Length);
        if(shieldFlights.Length>0&&!settings.ScriptedAttacksOnly)throw new ArgumentException("방패 비행 중 일반 칼날 입력/귀환 접촉 시각은 원문 미기재입니다. 방패 투척만 계산을 선택하세요.");
        var shieldEvents=shieldFlights.SelectMany(f=>new[]{(Time:f.Launch,Kind:0,Flight:f),(Time:f.Returned,Kind:3,Flight:f)}.Concat(f.Impact is {} impact?new[]{(Time:impact,Kind:1,Flight:f)}:[]).Concat(f.Detonation is {} detonation?new[]{(Time:detonation.Time,Kind:2,Flight:f)}:[])).OrderBy(e=>e.Time).ThenBy(e=>e.Kind).ToArray();
        var glaiveFlights=settings.GlaiveFlights??[];GlaiveFlightState.Validate(glaiveFlights,enemyBuilds.Length,build.WeaponId);
        if(build.WeaponId==WolfSledgeFlight.WeaponId)foreach(var flight in glaiveFlights)WolfSledgeFlight.Validate(flight,build);
        if(glaiveFlights.Length>0&&orviusHovers.Length>0)throw new ArgumentException("오르비우스 매달기와 충전 투척 비행은 별도 교전으로 설정하세요.");
        if(glaiveFlights.Length>0&&(settings.Inputs??[]).Any(a=>a.Kind is "GlaiveChainThrow" or "GlaiveNewThrow"))throw new ArgumentException("비행 경로에서 연속 투척을 자동 판정합니다. 별도 연속 투척 행동을 중복 입력하지 마세요.");
        var glaiveEvents=glaiveFlights.SelectMany(f=>new[]{(Time:f.Returned,Kind:0,Flight:f,Contact:(GlaiveContact?)null),(Time:f.Launch,Kind:1,Flight:f,Contact:(GlaiveContact?)null)}
            .Concat((f.Contacts??[]).Select(c=>(Time:c.Time,Kind:2,Flight:f,Contact:(GlaiveContact?)c)))
            .Concat(f.Detonation is {} d?[(Time:d.Time,Kind:3,Flight:f,Contact:(GlaiveContact?)null)]:[])).OrderBy(e=>e.Time).ThenBy(e=>e.Kind).ToArray();
        if(orviusHovers.Length>0&&build.WeaponId!=OrviusHoverState.WeaponId)throw new ArgumentException("매달기 투척은 오르비우스에서 사용합니다.");
        if(orviusHovers.Any(h=>!h.UseInfobox))throw new CombatRuleUnavailableException(OrviusHoverState.Conflict+" 표의 조건부 계산은 출처 기준을 선택해야 합니다. "+OrviusHoverState.Source);
        var orviusEvents=orviusHovers.SelectMany(h=>new[]{(Time:h.Launch,Kind:0,Hover:h),(Time:h.Attach,Kind:1,Hover:h),(Time:OrviusHoverState.End(h),Kind:3,Hover:h)}.Concat((h.TickTimes??[]).Select(t=>(Time:t,Kind:2,Hover:h))).Concat(h.Returned is {} returned?[(Time:returned,Kind:4,Hover:h)]:[])).OrderBy(e=>e.Time).ThenBy(e=>e.Kind).ToArray();
        if(settings.ScriptedAttacksOnly&&rapierContacts.Length+hologramContacts.Length+weaponActions.Length+shieldFlights.Length+collisions.Length+epidemic.Length+poolTicks.Length+ceramicContacts.Length+zenistarDeployments.Length+orviusHovers.Length+glaiveFlights.Length+bladeFlights.Length+innodemContacts.Length+vitrify.Length+glassSlams.Length+exaltedWaves.Length+(settings.Inputs??[]).Count(a=>a.Kind is "ExaltedSlideBlind" or "CobraNullifierBubble")==0)throw new ArgumentException("자동 반복 공격을 끈 경우 투사체/파동 명중 또는 실명 사건을 추가하세요.");
        var initialBuild=InitialState(build);
        var stance=settings.ScriptedAttacksOnly||build.Stance is null?null:StanceEngine.Calculate(catalog,initialBuild,stances);
        var initial=stance?.Stats??MeleeEngine.Calculate(catalog,initialBuild);
        if(timed&&stance is {Duration:null})return new(0,0,0,new(null,null,null,0,0,44061,0),[],[],0,[],["동작 전체 시간이 위키에 미기재입니다. 동작 1회 피해를 제공합니다."]);
        var seconds=mode switch{EncounterMode.StanceCycle=>stance?.Duration??initial.HitInterval,EncounterMode.FiveSeconds=>5,EncounterMode.TenSeconds=>10,_=>0};
        var issues=new List<string>();var notes=new List<string>{"단일 대상 · 몸통 · 모든 타격 명중 · 적 처치 시 공격과 피해 누적 중단", "표본별 치명타·상태·아케인 발동을 추첨합니다. 처치하지 못한 표본을 제외해 TTK를 축소하지 않습니다."};
        if(PathocystMaggots.Applies(build.WeaponId))notes.Add(PathocystMaggots.Scope(build.WeaponId));
        if(!settings.ScriptedAttacksOnly)notes.AddRange(initial.Limitations.Where(n=>n.StartsWith("미반영:")||n.StartsWith("미검증:")));
        if(collisions.Length>0)notes.Add("컨테이전: 실제 접촉 시각·누적 비행 거리는 사용자 시나리오입니다. 생존 대상에 부착 후1~4초 틱·5초 폭발을 자동 예약하며 직격 즉사는 계속 비행합니다. 폭발 중심 밖 감쇠율·부착 중 숙주 사망 처리·비행 중 버프 갱신 방식은 원문 미기재로 실제 해당 분기에서 구분합니다. "+ExodiaContagionMechanic.Source);
        if(collisions.Length>0)notes.Add("컨테이전 투사체는 일반 접촉 콤보를 획득하지 않습니다. 위키의 상태·치명타·피해 규칙과 별도로 Overframe의 컨테이전 빌드 설명을 교차 참고했습니다. https://overframe.gg/build/503493/rabvee/long-shlong-one-shot-build/ ");
        if(epidemic.Length>0)notes.AddRange(ExodiaEpidemicMechanic.Calculate(catalog,build).Notes.Append("에피데믹은 지정한 파동/폭발 명중만 계산합니다. 이동 속도·범위 경계·끝 폭발 감쇠가 미기재이므로 경로와 주변 대상의 자동 명중을 만들지 않습니다. 폭발 명중 입력은 중심 피해 조건입니다."));
        if(DualIchorCloudMechanic.Applies(build.WeaponId))notes.Add("듀얼 이코르: 변신 중 무기/상태 처치 위치에7초·4m 독 구름을 생성합니다. 위키에 최초 피해/틱 간격이 없어 명중 시각·구름 번호·대상을 지정한 시나리오만 합산합니다. 구름 번호는 실제 생성 순서이며, 지정한 명중 외 틱은 만들지 않습니다. "+DualIchorCloudMechanic.Source);
        if(okinaFlights.Length>0)notes.Add("오키나: 실제 처치로 최대6단검 생성·직격10냉기·반경4m 폭발1냉기·직격만 컨오 곱연산. 발사/명중 시각은 궤도 시간 미기재로 사용자 시나리오입니다. 지정한 비행 거리는18m/s 최단 시간과 대조하며, 근접 콤보·직접 단검 처치의 재생성은 제외합니다. "+OkinaDaggerState.Source);
        if(ceramicContacts.Length>0)notes.Add("세라믹: 한 강공격의 두 단검은 발사 콤보를 공유하고 한 번만 소모합니다. 직격→4m 폭발 순서로 상태·대상 방어를 갱신합니다. 두 단검 중 레드 라이트 핸드를 받는 것을 번호1로 지정하며 원문은 좌/우를 구별하지 않습니다. 발사/명중 시각은 사용자 시나리오이고 칼날 접촉은 포함하지 않습니다. "+CeramicDaggerMechanic.Source);
        if(poolTicks.Length>0)notes.Add("카우스타시스트 웅덩이: 입력한 진입 즉시1회·이후1초마다 접촉. 생성 후4초까지 본문/표 공통 구간만 계산합니다. 실명 지속시간 입력은 사용자 가정이며 위키 수치가 아닙니다. 겹친 웅덩이·생성 후 공격자 피해 버프 변경은 원문 불확실성을 따로 확인합니다. "+CaustacystMechanic.Source);
        if(cobraSleep)notes.Add("코브라 & 크레인: 바루크 사용 시 선택한 콤보의 첫 검 타격 후2초 수면·마무리 노출. 위키 Characteristics·Sleep 문서의 첫 콤보 타격 기준입니다. Notes의 ‘대상 첫타’ 표현과 구분합니다. 수면에 후속 피해를 준 뒤의 조기 기상 규칙은 미기재이며 그 다음 타격이 영향을 받는 경우 확정하지 않습니다. "+ShieldThrowMechanic.CobraSource+" "+FinisherExposureState.SleepSource);
        if(shieldFlights.Any(f=>f.FinisherWindowSeconds is not null))notes.Add("미검증: 방패 마무리 노출의 지속시간은 사용자가 지정한 조건부 값입니다. 충격/전기 상태가 발생하면 알려진 덮어쓰기 규칙으로 해제하며, 은신 보너스는 치명타와 가산합니다. "+ShieldThrowMechanic.SigmaSource+" "+MeleeStealth.Source);
        if(shieldFlights.Length>0)notes.Add("시그마 & 옥탄티스: 공중 방패 직격과 수동 회수 폭발을 별도로 계산합니다. 직격116·폭발50/4m·60m/s 최단 비행, 콤보 기반 통계 제외. 마무리 개방의 지속시간·다른 애니메이션에 의한 해제 시각과 귀환 피해는 원문 미기재이므로 자동 후속 마무리/귀환 타격을 생성하지 않습니다. "+ShieldThrowMechanic.SigmaSource);
        if(zenistarDeployments.Length>0)notes.Add("제니스타: 발사 입력에서10초×콤보 수명(100초 회수 버그)을 저장합니다. 배치/회수·오라 접촉 시각은 사용자 시나리오입니다. 오라 주기는 본문0.8초와 표1.2회/초가 상충하므로 지정하지 않은 틱을 만들지 않습니다. 사거리/공격자 버프는 접촉 때 갱신하고, 래스풀 최종 치확은 원반 발사 시 지정한 값만 오라에 보존합니다. "+ZenistarDiscState.Source);
        if(orviusHovers.Length>0)notes.Add(OrviusHoverState.Conflict+" 매달기 시작에 오버가드가 있으면 도중 파괴되어도 재투척까지 매달리지 않습니다. 3초 뒤까지 시체 위에 남습니다. 최초 틱/정확한 귀환 시각은 입력한 시나리오이며, 매달림을 별도 Lifted 상태나 컨오 종류로 더하지 않습니다. "+OrviusHoverState.Source);
        if(build.WeaponId==InnodemFlight.WeaponId&&(build.Melee!.Attack=="Dagger Projectile"||settings.AttackOrder?.Contains("Dagger Projectile")==true))notes.Add(InnodemFlight.Conditional+" · 미지정 위치1/컨오2는 참조 조건이며 자동 판정값이 아닙니다. "+InnodemFlight.Source);
        if(build.Melee.ContagionBuffs?.Active==true)notes.Add(build.Melee.ContagionBuffs.Description+" · 지정 구간 내 유지하는 투사체 전용 조건. "+ExodiaContagionMechanic.Source);
        if(exaltedWaves.Length>0)notes.Add(ExaltedRules.Find(build.WeaponId)!.Family==ExaltedFamily.Blade?(exaltedWaves.Any(f=>f.FalloffModel>0)?ExaltedWaveFlight.Conditional:"익절티드 블레이드 파동 · 감쇠 공통 지점")+" · 시간식75m/거리식70m · 30m/s · 일반 파동 관통당20%/15m 감소, 강 파동 제외. 두 교차 파동은 콤보를 한 번 소비하고 피해에 콤보를 곱하지 않습니다. 검 접촉은 별도입니다. 일반 파동은 명중 순간 동작 배율을 사용하며, 동작 없음은100%입니다. "+ExaltedWaveFlight.MotionSource:"데저트 윈드:20m·무한 관통·시야 확인·파동 CO 제외·실제 명중 콤보/텐노카이. 비행 속도 미기재로 접촉 시각은 사용자 시나리오이며 주먹 접촉은 별도입니다.");
        if(vitrify.Length>0&&enemyBuilds.Any(b=>b.Melee!.Debuffs?.Any(d=>d.Remaining>0&&d.Code is "Petrify" or "MassVitrify")==true))issues.Add("비트리카 유리 해제는 페트리파이/매스 비트리파이의 기절도 제거합니다. 이때 피해 취약도가 유지되는지 원문 미기재이므로 두 효과가 겹친 수치를 확정하지 않습니다.");
        if(vitrify.Length+glassSlams.Length>0)notes.Add(VitricaGlassState.Scope+" · 비트리카: 지정한 유리화 접촉 중심5m·7초·오버가드 면역·피해로 해제. 슬램 접촉은 원래 대상999 유리 폭발과6m 주변 폭발만 합산하며 기본 슬램과 지상 파동10 피해는 제외합니다. 파동 치명타/상태 기본 수치는 위키 미기재입니다. "+VitricaMechanic.Source);
        if(rapierContacts.Length>0){notes.Add(rapierContacts[0].AssumedStatusChance is null?DestrezaFlight.Partial:DestrezaFlight.Assumed);notes.Add("레이피어:지정한 발사/명중·2초 수명·각각 강제 충격/베기·대상별 컨오 갱신. 명중 순간 강공격 준비 동작과 현재 콤보를 사용합니다. 칼날/강공격 종료·콤보 소모를 자동 생성하지 않으며 전투 행동의 강공격 시작/종료/빗나감으로 명시합니다. "+DestrezaMechanic.Source);}
        if(innodemContacts.Length>0)notes.Add(InnodemFlight.Conditional+" · 조준 활공 발사·60m/s 직선 이동·21m·무한 관통·콤보 미획득. 위치와 컨오 분기의 자동 선택 규칙은 원문 미기재이며 입력하지 않은 재진입/굴절을 만들지 않습니다. "+InnodemFlight.Source);
        if(bladeFlights.Length>0)notes.Add("유령 칼날: 입력한 발사/접촉과60m/s 최소 비행을 대조합니다. 헤이트는 정지/전진의 발사 타격 배율을 직격·0.4초 뒤 폭발에 적용합니다. 액크 & 브런트는 공중×2/슬라이드×1·5m 즉발 폭발이며 두 번째 시각 효과는 피해로 중복 계산하지 않습니다. 두 무기 모두 프레셔/퓨리/변신+100%/진화II 피해 제외·범용 피해 별도. "+IncarnonBladeFlight.Source(build.WeaponId));
        if(glaiveFlights.Length>0&&build.WeaponId!=WolfSledgeFlight.WeaponId)notes.Add("글레이브: 입력한 발사·접촉·기폭·회수 경로를 계산합니다. 기본3회(케스트렐 프라임8회)+모드 반사 후 귀환, 귀환 경로는 관통, 볼래틸은 반사 접촉에만 발동합니다. 지상/공중 치확 모드 예외와 비행 중 텐노카이 기회 시간 정지를 반영합니다. 유도·지형·투척 간격을 자동 생성하지 않습니다. "+GlaiveFlightState.Source+" "+GlaiveFlightState.ReturnSource);
        if(weaponActions.Length>0)
        {
            notes.Add("무기 고유 입력: 발사와 칼날/투사체 접촉 시각은 지정한 시나리오입니다. 한 입력의 강공격 콤보·막기 충전을 각 성분에 공유하며 한 번 소모합니다. 주변 폭발은 대상별 방어층으로 계산합니다. "+WeaponStanceMechanic.Source(build.WeaponId));
            if(build.WeaponId==WeaponStanceMechanic.Agendus&&weaponActions.Any(v=>v.Contacts?.Any(c=>!c.Blade&&c.Travel>10)==true))notes.Add("미검증: 테넷 아젠더스 감쇠는 무기 본문 최저5%와 데이터6.95%가 상충합니다. 입력별로 명시한 출처 분기를 적용합니다.");
            if(build.WeaponId==WeaponStanceMechanic.Edun)notes.Add("미검증: 에둔 폭발은 위키 본문1초/5m와 데이터0.8초/4m가 상충합니다. 입력별로 지정한 출처 분기를 적용합니다.");
            if(build.WeaponId==WeaponStanceMechanic.Grigori&&weaponActions.Any(v=>v.ProjectileExpires is not null))notes.Add("미검증: 테넷 그리고리의 콤보별 원반 수명 수식은 위키 미기재입니다. 명시한 원반 만료 시각을 조건부로 사용합니다.");
            if(weaponActions.Any(WarfanSlam.Active))notes.Add("워팬 두 슬램은 각 명중 시각의 방어층·상태를 적용합니다. 강슬램의 둘째 타격은 일반 슬램 수치이며 입력당 콤보를 한 번 소비합니다. 두 명중 시각은 사용자 시나리오입니다.");
            if(weaponActions.Any(v=>v.IndependentDaggers))notes.Add("미검증: 콰서스의 같은 대상 다중 단검을 독립 타격/상태로 가정했습니다. 위키에 기재된 간헐적인 타격·베기 병합의 발생 규칙은 없습니다.");
        }
        if(stance is null&&!settings.ScriptedAttacksOnly)notes.Add("첫 명중을 0초로 두며 반복 간격은 입력한 기본 간격 ÷ 공속입니다. 실제 공격 애니메이션의 실측 시간이 아닙니다.");
        if(stance?.Combo.FinisherKind is not (null or "Ground"))notes.Add("앞·뒤·은신·특수 체형 마무리는 대상 재노출을 가정하지 않고 한 번만 수행합니다. 이후 구간에는 남아 있는 상태 피해만 계산합니다.");
        var steps=stance is null?[new Step(build,null,1,false,false)]:stance.Hits.SelectMany(h=>Enumerable.Repeat(new Step(h.Input!,h.SourceHit,h.CoFactor,stance.Combo.Key=="Finisher",stance.Combo.Key=="Slide",Aerial:stance.Combo.Key=="Aerial"),h.Count)).ToArray();
        if(stance is null&&settings.AttackOrder is {Length:>0})
        {
            steps=settings.AttackOrder.Select(a=>new Step(build with {Melee=build.Melee with {Attack=a,Tennokai=requestedTennokai&&MeleeEngine.AttackPolicy(build.WeaponId,a).Heavy}},null,1,false,a=="Slide Attack")).ToArray();
            notes.Add("공격 동작 순서는 사용자 시나리오입니다. 입력한 기본 간격을 사용하며 원문에 없는 일반→강공격 연결 시간을 추정하지 않습니다.");
        }
        if(!timed)steps=[steps[0]];
        if(build.WeaponId==VinquibusState.WeaponId&&((build.Melee!.Conditions?.GetValueOrDefault("VinquibusPrecision")??0)>0||(settings.Inputs??[]).Any(i=>i.Kind=="VinquibusHeadshot")))notes.Add("미검증: 빈퀴부스 정밀+250%는 기본 피해 가산으로 가정한 결과입니다. 위키·공식 설명에 브라켓이 명시되지 않아 확정 규칙과 구분합니다. "+VinquibusState.Source);
        if(build.WeaponId==VinquibusState.WeaponId&&weaponActions.Length>0)notes.Add("빈퀴부스: 지정한 발사/접촉 시각의 경로입니다. 생존 적 또는 표면에서 정지하며 처치한 적1명만 관통합니다. 발사+0.8초에 반경4m·가장자리70% 폭발을 계산하고, 동일 입력의 정밀 충전은 한 번만 소모합니다. 절대 비행 속도는 위키 미기재입니다. "+VinquibusGunshot.StanceSource+" "+VinquibusFlight.ModuleSource);
        if(steps.Any(s=>VinquibusGunshot.Applies(build.WeaponId,s.Build.Melee!.Attack)))notes.Add("빈퀴부스 사격: 거리0 발사·명중 동시 조건의 발사 후0.8초에 별도 폭발400·반경4m·가장자리30% 감쇠·강제 충격. 정밀 충전은 한 입력의 모든 성분이 공유하며 폭발 자체가 추가 충전을 소비하지 않습니다. 위키 무기 모듈의 AttackParentIndex/ExplosionDelay를 사용합니다. "+VinquibusGunshot.StanceSource);
        var times=settings.HitTimes;
        if(mode==EncounterMode.StanceCycle&&stance is null)issues.Add("스탠스 선택 필요");
        if(timed&&stance is not null)
        {
            if(times is null)issues.Add("위키에 개별 타격 시각이 없어 상태 틱·방어층 전이를 포함한 시간 결과를 확정할 수 없습니다. 아래 타격 순서 범위는 직접 피해만의 별도 결과입니다.");
            else if(times.Length!=steps.Length||times.Any(t=>!double.IsFinite(t)||t<0)||!times.SequenceEqual(times.Order())||times[^1]>=stance.Duration*initial.AttackSpeed)throw new ArgumentException("타격 시각은 타수와 같고 오름차순이며 기본 콤보 시간보다 작아야 합니다.");
            else notes.Add("개별 타격 시각은 저장된 사용자 시간 가정입니다. 위키 실측값으로 표시하지 않습니다.");
            if(stance.Hits.Any(h=>!h.CoVerified)&&initial.Melee!.Effects.GetValueOrDefault("ConditionOverload")>0)issues.Add("타격별 컨오 배율 미확인");
        }
        if(timed&&!settings.ScriptedAttacksOnly&&!(profile.Name=="Tak & Lug"&&stance?.Combo.AttackOverride=="Heavy Attack"))
            issues.AddRange(MeleeMechanicScope.TimeIssues(profile,steps.Select(s=>s.Build.Melee!),steps.Any(s=>s.Finisher),stance?.Combo.Hits.Any(h=>h.AttackType=="Slam")==true));
        PlayerShieldState? CreatePlayer()=>build.Arcanes?.MaximumShields is {} max?new(max,build.Arcanes.MaximumOvershields,build.Arcanes.Shields,build.Arcanes.Overshields,build.Arcanes.ShieldRechargeEnabled,build.Arcanes.ShieldRechargeBonus,build.Arcanes.ShieldRechargeDelayReduction,build.Arcanes.ShieldRechargeDelayRemaining):null;
        _=CreatePlayer();
        if(profile.Name=="Rakta Dark Dagger")
        {
            if(build.Arcanes?.MaximumShields is null&&build.Arcanes?.Melee is {Active:true} raktaArcane&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==raktaArcane.Id).Code=="Retaliation")issues.Add("락타 다크 대거와 리탈리에이션: 아케인 설정에서 워프레임 최대 실드를 입력하세요.");
            if(build.Arcanes?.MaximumShields is not null&&TargetStatusRules.WeaponPreset(initial,stance).Any(t=>t is DamageType.Viral or DamageType.Magnetic))issues.Add("락타 다크 대거의 바이러스·자성 비피해 상태 부여 시 추가 실드 회복량은 위키에 수식이 미기재입니다.");
            notes.Add(build.Arcanes?.MaximumShields is null?"락타 다크 대거의 실드 회복 수치는 워프레임 최대 실드를 입력하면 계산합니다.":"락타 다크 대거: 근접무기를 든 상태 · 기존 방사능 대상의 무기 직접/상태 피해 ×5% 실드 회복 · 일반 실드→오버실드 한도 · 자연 재생은 입력 설정 적용 · 피격 사건은 재생 대기만 갱신하고 피해량을 추정하지 않습니다.");
        }
        var formDefinition=IncarnonEngine.ForWeapon(build.WeaponId);
        if(formDefinition is not null)
        {
            var form=new IncarnonFormState(build);
            notes.Add($"인카논 지속{form.Duration:0}초 · 발동{form.RequiredCombo}× · 재사용으로 남은 시간 갱신 없음. 시작 잔여 시간과 별도 입력한 발동 동작 시각을 사용합니다. 타격 접촉을 준비 동작 시작 시각으로 대체하지 않습니다. "+formDefinition.Source.Url);
            notes.Add("변신 발동 행동은 그 입력의 소모 전 콤보를 사용하며 다른 대상 공격의 피해/콤보 소모를 현재 대상에 추가하지 않습니다. "+IncarnonFormState.RefreshSource);
        }
        var timedPerks=IncarnonCombatPerks.ForBuild(build);
        var nearbyFurax=formDefinition?.Code=="Furax"&&formDefinition.Perks.Any(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="NearbyEnemies");
        if(nearbyFurax)notes.Add("킬 조이: 플레이어6m 안의 살아 있는 적을 현재 장면에서 세며 최대5명입니다. 주변 적의 처치 후 공속을 다시 계산합니다. https://wiki.warframe.com/w/Furax_Incarnon_Genesis?oldid=2815777");
        if(formDefinition?.Code=="Anku")notes.Add("앙쿠: 변신 중 슬라이드 명중 후6초간 사거리+3m·강제 베기. 슬라이드가 새로 명중할 때만 갱신합니다. https://wiki.warframe.com/w/Anku_Incarnon_Genesis?oldid=2810581");
        var stackPerks=IncarnonStackTimers.RulesFor(build);
        foreach(var perk in stackPerks)notes.Add($"{IncarnonStackTimers.Label(perk.Condition)}: 발동당1중첩·{perk.Duration}초·최대{perk.Cap}. 반복 발동 후 만료 순서는 원문 미기재이며, 최초 잠재 만료 이후의 확정 전투 결과와 구분합니다. {perk.Source}");
        var rapidConclusion=IncarnonEngine.ForWeapon(build.WeaponId) is {Code:"Obex"} obex?obex.Perks.SingleOrDefault(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="FinisherStacks"):null;
        if(rapidConclusion is not null)notes.Add("래피드 컨클루전: 시작 임무 누적을 보존하고 이 무기의 마무리·파라존 처치가 같은 카운터에 누적됩니다. 처치당 파쿠르+2.5%, 최대16중첩, 사망까지 유지. 현재 교전은 플레이어가 살아 있는 구간입니다. https://wiki.warframe.com/w/Obex_Incarnon_Genesis?oldid=2803254");
        var piercingStature=IncarnonEngine.ForWeapon(build.WeaponId) is {Code:"Destreza"} destreza&&destreza.Perks.Any(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="PunctureStacks");
        if(piercingStature)notes.Add("피어싱 스테처: 이 무기의 관통 상태 부여마다 상확+20%·6초 독립 중첩·최대5. 최대 중첩에서는 가장 오래된 중첩을 교체합니다. 해당 타격의 추첨된 상태 횟수를 소급 늘리지 않습니다. "+PiercingStatureState.Source);
        foreach(var perk in timedPerks)notes.Add(perk.Label+": "+perk.Seconds+"초 · 현재 무기의 발동 사건으로 시작/갱신 · "+perk.Source);
        var sanctiMagistar=build.WeaponId==SanctiMagistarState.WeaponId;
        if(sanctiMagistar)notes.Add("생티 마기스타: 강공격 사용 시 소모 전 콤보당5%·최대60%의 임무 영구 흡혈. 오버가드를 포함한 직접 피해에 적용하며15m 아군에게 같은 회복 가능량을 공유합니다. 방어물은5초당500한도입니다. 서로 다른 콤보로 재발동했을 때 이전 값 유지/덮어쓰기와 상태 피해의 흡혈 여부는 원문 미기재이므로 회복량의 별도 근거 범위로 표시합니다. "+SanctiMagistarState.Source);
        var jadeEvents=build.WeaponId==JadeWeaponBuffs.HarmonyId||(settings.Inputs??[]).Any(i=>JadeWeaponBuffs.IsReceipt(i.Kind));
        if(jadeEvents)notes.Add("하모닉 레조넌스: 강공격 직접 처치/버프 수신 후30초·범용 상태 지속+30%. 세 제이드 무기 버프가 함께 있으면60초·+60%로 합쳐집니다. 새로 부여되는 상태에만 적용하며 기존 상태 시간을 소급 늘리지 않습니다. "+JadeWeaponBuffs.HarmonySource);
        var effects=initial.Melee!.Effects;
        if(build.WeaponId==WolfSledgeFlight.WeaponId&&glaiveFlights.Length>0)
        {
            notes.Add("울프 슬레지:지상 투척·충전 정도에 따른 피해 변화 없음·전진2접촉/각5m 폭발·지형 접촉 즉시 귀환·귀환 관통에는 자동 폭발 없음·기폭 뒤 귀환 강타. 첫 거리40m와 두 번째 대상20m를 검사하며 정확한 유도 경로/절대 속도/귀환 시각은 자동 생성하지 않습니다. "+WolfSledgeFlight.Source);
            if(effects.GetValueOrDefault("Tennokai")>0)throw new CombatRuleUnavailableException("울프 슬레지의 비행 중 텐노카이 시간 정지 적용 여부는 위키에 별도로 기재되지 않았습니다. 글레이브 전용 패치 규칙을 해머에 임의 적용하지 않습니다. "+WolfSledgeFlight.Source);
        }
        var hasAmanata=profile.Code=="Amanata";
        if(hasAmanata)
        {
            var startContacts=build.Melee.Conditions?.GetValueOrDefault("AmanataHits")??0;MeleeEngine.Range(startContacts,0,29,"아마나타 누적 명중");if(startContacts!=Math.Truncate(startContacts))throw new ArgumentException("아마나타 누적 명중은 정수입니다.");
            notes.Add("아마나타: 실제 명중30회마다 축복을 교체합니다. 슬램은 명중하지 않아도 입력당1회입니다. 시작 눈0은 무작위입니다. 위키에는 주사위 가중치와 프레셔 상태별 확률이 미기재되어 있어 표본 계산은6개 눈·13개 상태의 균등 확률을 가정합니다. 명시적인 원문 확률로 검증된 값이 아닙니다. "+AmanataState.Source);
            if(effects.GetValueOrDefault("AmanataRandomProc")>0)notes.Add("아마나타 프레셔: 소모 전8× 이상 강공격의 첫 명중 대상에게만 상태1개. 보이드/타우 제외, 일반 상태와 별개로 인플루언스 전파·어플릭션즈 이전에 적용. "+AmanataState.PressureSource);
        }
        var eruptionSlot=WarframeStatusArcanes.Find(build,WarframeStatusArcanes.Eruption);
        if(eruptionSlot is not null)notes.Add("이럽션: 실제 에너지/범용 오브 습득마다17/34/51/68/85/100% 추첨·플레이어 중심5~30m·벽 관통. 오브 생성만으로 발동하지 않습니다. 넉다운은 오버가드/완전 동결에 막히며 어플릭션즈를 발동하지 않습니다. 적별 회복시간은 원문 미기재로, 시간 교전의 컨오/힐링 리턴에 영향을 주면 사용자 지정 회복시간이 필요합니다. "+WarframeStatusArcanes.EruptionSource);
        var falloutSlot=WarframeStatusArcanes.Find(build,WarframeStatusArcanes.Fallout);
        if(falloutSlot is not null)notes.Add("유니버설 폴아웃: 어빌리티 방사능 횟수별 영구 처치 드롭 확률·모든 랭크60% 상한. 오브 생성은 자동 습득이나 회복을 뜻하지 않습니다. "+WarframeStatusArcanes.FalloutSource);
        var truculenceSlot=WarframeStatusArcanes.Find(build,WarframeStatusArcanes.Truculence);
        if(truculenceSlot is not null)notes.Add("트루큘런스: 실제 오버가드 획득 누적3000마다 플레이어 중심 바이러스10중첩·시야 검사·피해 없음. "+WarframeStatusArcanes.TruculenceSource);
        var tennokaiEnabled=timed&&effects.GetValueOrDefault("Tennokai")>0;
        var truthSlot=build.Slots.Append(build.Melee.Exilus).FirstOrDefault(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Condition=="TruthFlame");
        if(timed&&requestedTennokai&&!tennokaiEnabled)throw new ArgumentException("시간 교전에서 텐노카이로 시작하려면 텐노카이 모드를 장착하세요.");
        if(tennokaiEnabled)notes.Add("텐노카이 기회는 실제 명중 입력마다 판정합니다. 사용 행동은 준비 동작 시작 시각이며 다음 지정한 강공격의 명중 시각과 구분합니다. 시작 텐노카이 선택은 0초 첫 강공격 1회에만 적용됩니다. 열린 기회의 재발동 갱신은 원문 미기재여서 가능한 종료 시각 범위를 표시하며, 사용 여부가 그 범위에 걸릴 때 확정 계산을 거부합니다. "+TennokaiState.Source);
        if(tennokaiEnabled&&truthSlot is not null)notes.Add("트루스 플레임: 성공한 텐노카이 처치 후에만 다음 연쇄 피해를 가산하고 4초 기회를 반환합니다. 실패 판정은 ‘텐노카이 동작 종료’에 입력한 시각이며 명중 시각을 종료 시각으로 대신하지 않습니다. 즉시 구르기 취소에는 저주가 없고, 실패는 강공격 효율과 무관하게 콤보를 초기화합니다. 자기 화염 피해는 6초 총 원본량으로 별도 표시하며 대상 DPS에 합산하지 않습니다. "+TennokaiState.TruthSource);
        var storedFraction=effects.GetValueOrDefault("StoredDamageFraction");
        var springSlot=build.Slots.Append(build.Melee.Exilus).SingleOrDefault(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Condition=="SpringStacks");
        var magusSlot=build.Arcanes?.Operator;
        if(magusSlot is {Active:true})notes.Add("마구스 어그레스: 워프레임 근접 전이 후4공격·오르건 셔터와 가산.20초 쿨다운 기산점은 발동/마지막 소모의 범위로 보존하며 그 사이 재발동은 확정하지 않습니다. "+MagusAggressState.Source);
        var powerThrow=build.Slots.Any(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Condition=="ConsecutiveThrows");
        if(timed&&springSlot is not null)notes.Add("스프링 로디드 블레이드: 무기 상태 부여마다 사거리 중첩, 최대2개·랭크별4~24초 독립 만료. 최대 중첩 중 재발동의 갱신 대상은 원문 미기재여서 실제 결과가 달라지는 최초 만료부터 확정 계산을 제공하지 않습니다. "+SpringBladeState.Source);
        if(timed&&powerThrow)notes.Add("파워 스로우: 연속 투척은 회수 전 버튼을 누른 버퍼 입력으로만 누적됩니다. 시작 중첩과 전투 중 ‘연속 투척/새 투척’ 행동을 사용하고, 단순히 투사체가 적에게 맞았다고 중첩을 올리지 않습니다. https://wiki.warframe.com/w/Power_Throw?oldid=2787704");
        if(storedFraction>0)notes.Add("어벤징 트루스: 실제 막은 적 피해의 랭크별20/30/40/50%를 저장합니다. 다음 강공격 입력에 사용하고 일반 공격에는 유지합니다. 저장 피해는 피해 모드 뒤·원소 모드 앞이며 강공격 콤보를 적용합니다. https://wiki.warframe.com/w/Avenging_Truth?oldid=2654944");
        var heavyLifeSteal=effects.GetValueOrDefault("HeavyLifeSteal");var nikanaLifeSteal=effects.GetValueOrDefault("NikanaLifeSteal");var daggerStrip=effects.GetValueOrDefault("DaggerArmorStrip");
        if(nikanaLifeSteal>0)notes.Add("아말감 다이큐 흡혈:직접 피해만·상태 틱 제외·방어층 적용 후·회복 상한 적용 전. "+MeleeExternalRules.DaikyuSource);
        if(daggerStrip>0)notes.Add("아말감 아르고나크:단검 직접/상태 피해 사건마다 기본 방어력 감소. 전파된 상태 피해·주변 가스/전기/폭발에도 적용. "+MeleeExternalRules.ArgonakSource);
        if(heavyLifeSteal>0&&zenistarDeployments.Any(d=>d.Hologram==0&&d.Retrieve is not null&&d.RetrieveStarted is null&&(d.AuraTimes??[]).Any(t=>t<d.Retrieve)))throw new CombatRuleUnavailableException("제니스타 원반의 라이프 스트라이크는 회수 강공격 동작 중에만 발동합니다. 회수 동작 시작 시각을 지정하세요. 완료 시각으로 애니메이션 길이를 추정하지 않습니다. "+ZenistarDiscState.Source);
        if(heavyLifeSteal>0&&zenistarDeployments.Any(d=>d.Hologram==0&&d.RetrieveStarted is not null))notes.Add("제니스타 예외: 지정한 회수 동작 시작부터 회수 완료 전까지의 원반 직접 피해도 라이프 스트라이크로 회복합니다. 지속 상태 피해와 회수 동작 밖 오라는 제외하며, 원반 자체를 강공격으로 바꾸거나 콤보/킬링 블로우를 더하지 않습니다. "+ZenistarDiscState.Source);
        var healingPerStatus=effects.GetValueOrDefault("HealingPerStatus");
        var finisherRegen=effects.GetValueOrDefault("FinisherHealing");var toxinRegen=effects.GetValueOrDefault("ToxinKillHealing");
        if(finisherRegen>0)notes.Add("마스터즈 플러리: 이 무기의 마무리 처치 후 체력40/초·10초. 회복 가능량은 실제 최대 체력과 구분합니다. https://wiki.warframe.com/w/Nami_Solo_Incarnon_Genesis?oldid=2773651");
        if(toxinRegen>0)notes.Add("포이즌 패러사이트: 독성3중첩 이상인 적의 처치 후 체력33/초·9초. 이 무기의 상태 피해 처치도 대상의 기존 독성 중첩으로 판정합니다. https://wiki.warframe.com/w/Dual_Ichor_Incarnon_Genesis?oldid=2812453");
        var influenceSlot=build.Arcanes?.Melee is {Active:true} inf&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==inf.Id).Code=="Influence"?inf:null;
        var frameArcanes=new[]{build.Arcanes?.Warframe1,build.Arcanes?.Warframe2}.Where(a=>a?.Active==true).Cast<ArcaneSlot>().Select(a=>(Slot:a,Code:MeleeRuleCatalog.Load().Arcanes.Single(r=>r.Id==a.Id).Code)).ToArray();
        var reaperLevel=frameArcanes.Where(a=>a.Code=="Reaper").Select(a=>a.Slot.Rank+1).SingleOrDefault();
        var hasHotShot=frameArcanes.Any(a=>a.Code=="HotShot");
        var hasDemulcent=frameArcanes.Any(a=>a.Code=="Demulcent");
        if(hasDemulcent)notes.Add("시오럼 디멀센트: 레지듀얼 속성별 영역 진입·실제 영역 틱·마지막 영역 이탈을 입력합니다. 틱당1중첩(동시에 겹친 영역은 각각)·속성별 최대15, 이탈 후20초. 무기 원소 모드와 가산·재조합합니다. 영역 틱의 최초 위상은 임의로 만들지 않습니다. 시작 중첩의 잔여 시간은 별도 설정입니다. "+ResidualElementState.Source);
        var exaltedHotShot=hasHotShot&&profile.Raw.Text("Class")=="Exalted Weapon";
        if(timed&&hasHotShot)notes.Add("핫 샷: 어빌리티의 적별 화염 부여 1회당1중첩·최대50·10초 전체 갱신. 같은 부여의 복수 화염 프록은 중복하지 않으며 익절티드 무기의 화염은 직접 집계합니다. 일반 무기의 화염과 단순 원소 부여 버프는 제외합니다. 외부 어빌리티는 실제 부여한 대상 수를 행동으로 입력합니다. https://wiki.warframe.com/w/Arcane_Hot_Shot?oldid=2796809");
        if(timed&&frameArcanes.Any(a=>a.Code is "Aegis" or "Barrier"))
        {
            if(build.Arcanes?.MaximumShields is null)issues.Add("실드 회복 아케인: 워프레임 최대 실드를 입력하세요.");
            notes.Add("이지스·배리어는 실제 실드 피격 사건에만 발동합니다. 이지스는12초·활성 중 갱신 없음·기본5%에 랭크별5%를 가산한 재생·재생 대기0·자성 시 재생 중단입니다. 배리어는 랭크별1% 확률·(랭크+1)초 재사용 대기이며 오버실드를 만들지 않습니다.");
        }
        if(timed&&frameArcanes.Any(a=>WarframeCombatArcanes.ArmorCode(a.Code))&&build.Arcanes?.PlayerArmor is null&&formDefinition?.Perks.Any(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="Armor450")==true)
            issues.Add("방어력 아케인과 인카논 진화: 버프 적용 전 워프레임 방어력을 입력하세요.");
        if(healingPerStatus>0)notes.Add("힐링 리턴: 접촉/산탄마다 실제 상태 종류를 집계합니다. 생존한 대상에는 이번 명중으로 생긴 상태도 포함하고, 첫 타 즉사에는 새 상태 회복이 없습니다. 상태 중첩 수는 중복 합산하지 않습니다. 회복 상한 적용 전입니다. "+HealingReturnRules.Source);
        if(healingPerStatus>0&&steps.Any(s=>HealingReturnRules.GuaranteedFlinch(build,s.Build.Melee!.Attack)))notes.Add("힐링 리턴의 확정 움찔과 속성 상태가 함께 있을 때 중복 여부는 원문 미기재입니다. 회복량의 원문 불확실 범위는 표본 오차와 구분합니다.");
        if(heavyLifeSteal>0)notes.Add("라이프 스트라이크: 직접 강공격 피해만 흡혈하며 상태 피해는 제외합니다. 건블레이드 탄환·수동 글레이브 폭발에도 적용합니다. 방어 적용 후 피해 기준·회복 상한 적용 전입니다. https://wiki.warframe.com/w/Life_Strike?oldid=2685512");
        var invigorated=effects.GetValueOrDefault("InvigoratedHealth")>0;
        if(invigorated)notes.Add("히루도: 치명타 피해의 5% 흡혈(오버가드 포함) · 실제 회복 상한 적용 전 수치. 치명타마다 기본 최대 체력 +5%, 최대5중첩, 15초 전체 갱신. 체력 모드와 같은 가산 묶음이며 모딩 후 최대 체력 ×25%가 아닙니다.");
        if(MeleeStealth.Invisible(build.Melee))
        {
            notes.Add("은신: 대상별 실제 근접 접촉 후 3초 동안 은신 추가 피해가 중단됩니다. 치명타와 가산이며 원거리 근접·상태 틱은 이 대기를 갱신하지 않습니다. 실명 상태인 적은 접촉으로 은신 보너스를 잃지 않습니다.");
        }
        if(timed&&(MeleeStealth.Invisible(build.Melee)||frameArcanes.Any(a=>a.Code=="Trickery"))&&stance?.Stance.Id=="High Noon"&&stance.Combo.Key=="Neutral"&&profile.Name!="Stropha")issues.Add("하이 눈 정지 연사의 순간 은신 보너스 차단 시간은 위키 미기재입니다.");
        if(timed&&(build.Melee.Conditions?.GetValueOrDefault("Unalerted")??0)>0)issues.Add("미경계 적의 경계 전환 지연은 위키에 정확한 시간이 없어 지속 은신/실명 조건과 구분합니다.");
        var burningHate=build.Slots.Where(s=>s is not null).Select(s=>MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s!.ModId)).Any(m=>m.Condition=="BurningHateApplied");
        if(timed&&stance is not null&&(initial.Melee.ComboChance<1||stance.Stance.Id is "Swirling Tiger" or "Homing Fang"))issues.Add("음수 콤보 확률 또는 스탠스별 콤보 획득 예외 규칙");
        if(effects.GetValueOrDefault("BounceExplosion")>0)notes.Add("볼래틸: 지정한 직접 투척 명중마다 랭크별25~100% 반사 폭발·원 대상 포함·방어층/상태 갱신. 폭발은 콤보를 얻거나 소모하지 않으며 수동 기폭의 강제 상태를 복사하지 않습니다. 반사 경로·지형 충돌·실제 회수 시간은 자동 생성하지 않습니다. "+GlaiveBounceMechanic.Source);
        foreach(var key in new[]{"StaticDischarge","ExodiaContagion","Epidemic"})
            if(!(key=="ExodiaContagion"&&collisions.Length>0)&&!(key=="Epidemic"&&epidemic.Length>0)&&effects.GetValueOrDefault(key)!=0&&steps.Any(s=>MeleeMechanicScope.EffectCanTrigger(key,build.WeaponId,s.Build.Melee!)))issues.Add(MeleeEffectCoverage.Notes(new Dictionary<string,double>{{key,1}}).FirstOrDefault()??"시간별 사건 규칙 미연결: "+key);
        if(effects.GetValueOrDefault("StatusVulnerability")!=0&&!burningHate)issues.Add("외부 상태 피해 취약의 적용 사건이 없습니다.");
        if(timed&&steps.Any(s=>s.Hit?.ForcedProcs.Any(p=>p is "Knockdown" or "Lifted")==true)&&effects.GetValueOrDefault("ConditionOverload")>0)issues.Add("넉다운·띄우기 회복 시점 미기재 · 컨오 추가 0~1종 범위로 계산");
        var strip=effects.GetValueOrDefault("BaseArmorStrip");
        if(build.WeaponId==WeaponStanceMechanic.Nepheri&&((settings.ScriptedAttacksOnly&&weaponActions.All(v=>(v.Contacts??[]).All(c=>!c.Blade)))||steps.All(s=>s.Build.Melee!.Attack=="Fireballs")))strip=0;
        if(!settings.ScriptedAttacksOnly&&steps.All(s=>!FlatArmorReduction.ShatteringApplies(profile.Raw.Text("Class"),s.Build.Melee!.Attack,MeleeEngine.AttackPolicy(build.WeaponId,s.Build.Melee.Attack))))strip=0;
        if(strip>0||daggerStrip>0)foreach(var target in enemyBuilds.Where(t=>t.Scenario.Enemy?.Armor>0))
        {
            if(target.Encounter?.BaseArmor is not >0){issues.Add("기본 방어력 감소에 필요한 적 기본 방어력");continue;}
            try{_=FlatArmorReduction.Remaining(target,.001);}catch(CombatRuleUnavailableException e){issues.Add(e.Message);}
        }
        if(strip>0||daggerStrip>0)notes.Add("고정 기본 방어력 감소 → 레벨 성장 → 상한2700 → 비율 감소 순서. 초기 하한200을 매 타격에 다시 적용하지 않습니다. "+FlatArmorReduction.Source);
        if(strip>0&&profile.Name is "Destreza" or "Galatine" or "Galatine Prime"&&steps.Any(s=>!s.Build.Melee!.Attack.Contains("Slam")))issues.Add("충격 피해 양자화에 따른 섀터링 임팩트 예외");
        var dynamicKeys=DynamicKeys;
        foreach(var p in profile.Passives.Where(p=>p.Key!="Always"&&!dynamicKeys.Contains(p.Key)))
            if(p.Key is not ("Signature" or "Brokk" or "SlamCapacitor" or "BlockCharges" or "Vendetta"))notes.Add(p.Label+": 입력한 외부 발동 상태 고정 (자동 발동 아님)");
        if(profile.Name=="Arca Titron")notes.Add("슬램 캐패시터는 시작 충전량에서 출발합니다. 일반/강 슬램에 소모하며 스탠스 콤보 슬램에는 유지합니다. 이 무기의 처치·지속 피해 처치는 충전을 얻습니다.");
        if(profile.Passives.Any(p=>p.Key=="BlockCharges"))notes.Add("막기 충전은 막은 공격마다 최대 4회 쌓이고 다음 공격 입력에 전부 소모합니다.");
        if(profile.Name=="Dorrclave")notes.Add("라이징 벤데타를 시작 상태에서 켜면 10회 공격분을 부여하고 공격 입력마다 한 번 소모합니다. 이 대상의 처치와 입력한 다른 적 처치·도움을 집계합니다.");
        var ruvox=IncarnonEngine.ForWeapon(build.WeaponId)?.Code=="Ruvox";
        var ruvoxPerforation=ruvox&&IncarnonEngine.ForWeapon(build.WeaponId)!.Perks.Any(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Conditional.ContainsKey("ForcedPunctureStacks"));
        if(ruvox)notes.Add("루복스: 변신 강 슬램 이후 8m 안의 대상을 1.5 + (소모 전 콤보 − 1)초 꿰뚫습니다. 오버가드·이미 래그돌 상태의 대상 제외. 선택한 영구 천공/취약성 혈청은 이후 타격에 적용하며 주변 다른 대상의 연쇄 상태 전파는 합산하지 않습니다.");
        if(effects.GetValueOrDefault("Influence")>0)notes.Add("인플루언스의 주변 대상 전파는 이 단일 대상 그래프에 합산하지 않습니다.");
        foreach(var mod in build.Slots.Where(s=>s is not null).Select(s=>MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s!.ModId)).Where(m=>m.Conditional is not null&&m.Condition is not ("BurningHateApplied" or "ProtonSnap" or "BerserkerStacks" or "GalvanizedStacks")))notes.Add(mod.Name+": 시작 시 입력한 외부 발동 중첩 고정 · 이 대상 처치 후에는 교전 종료");
        var hasBerserker=build.Slots.Any(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Condition=="BerserkerStacks");
        var galvMods=build.Slots.Where(s=>s is not null).Select(s=>(Slot:s!,Rule:MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s!.ModId))).Where(x=>x.Rule.Condition=="GalvanizedStacks").ToArray();
        var galvComboPerStack=galvMods.Sum(x=>x.Rule.Conditional!.GetValueOrDefault("InitialCombo")*(x.Slot.Rank+1d)/(x.Rule.MaxRank+1d));
        if(galvMods.Length>0)notes.Add("근접 갈바나이즈드: 이 무기의 직접/상태 처치마다 최대4중첩·20초 갱신, 만료 때 한 중첩씩 감소합니다. 리플렉스의 초기 콤보는 랭크당1.85, 최대20.35씩이며 초당40 회복을 적용합니다. https://wiki.warframe.com/w/Galvanized_Mods?oldid=2745813");
        var isPennant=build.WeaponId==MeleeKillBuffs.PennantId;
        var animositySlot=build.Arcanes?.Melee is {Active:true} an&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==an.Id).Code=="Animosity"?an:null;
        if(animositySlot is not null)notes.Add("애니모시티: 시작 중첩 보존·일반 근접 입력당 명중 시1중첩·10상한. 슬램/강공격에는 새 중첩 없음. 강공격이 적에게 닿아야 소모하며 지상 글레이브 투척은 치확을 받지만 소모하지 않습니다. "+MeleeAnimosityRules.Source);
        var crescendoLevel=build.Arcanes?.Melee is {Active:true} crescendo&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==crescendo.Id).Code=="Crescendo"?crescendo.Rank+1:0;
        var fortificationSlot=build.Arcanes?.Melee is {Active:true} fs&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==fs.Id).Code=="Fortification"?fs:null;
        if(fortificationSlot is not null)
        {
            notes.Add("밀리 포티피케이션: 근접 처치마다 방어력 +"+(35*(fortificationSlot.Rank+1))+", 각10초 독립 만료. 추가 방어력은 입력한 워프레임 방어력에 더해 진화 조건을 판정합니다. "+MeleeFortificationState.Source);
            if(build.Arcanes?.PlayerArmor is null&&IncarnonEngine.ForWeapon(build.WeaponId)?.Perks.Any(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="Armor450")==true)
                issues.Add("포티피케이션과 방어력 조건 진화: 아케인 설정에 버프 적용 전 워프레임 방어력을 입력하세요.");
        }
        var gunAndBlade=IncarnonEngine.ForWeapon(build.WeaponId)?.Perks.SingleOrDefault(p=>(build.Melee.Evolutions??[]).Contains(p.Id)&&p.Condition=="PrimaryKills"&&p.Conditional.ContainsKey("InitialCombo"));
        if(crescendoLevel>0)notes.Add("크레센도: 시작 마무리 처치 횟수에 더해 이 무기의 마무리/파라존 처치마다 영구 초기 콤보를 쌓습니다. 다른 무기의 마무리는 제외합니다. 공식38.5.3 기준 https://www.warframe.com/en/patch-notes/pc/38-5-3");
        if(gunAndBlade is not null)notes.Add("세라믹 대거 · 건 앤 블레이드: 시작 중첩을 보존하고 전투 중 주무기 처치마다 초기 콤보 +1, 최대100회. 보조/근접 처치는 제외합니다.");
        if(hasBerserker)notes.Add("버서커 퓨리: 근접 처치마다 최대2중첩·10초 갱신, 만료 때 한 중첩씩 감소합니다. 시간 교전은 0중첩에서 시작하며 다른 적 처치는 전투 중 행동에 입력합니다.");
        if(isPennant)notes.Add("페넌트: 직접 강공격 처치마다 공속 +50%, 소모 전 콤보에 따라 2 + 4 × (콤보 − 1)초. 각 처치의 버프는 독립적으로 만료됩니다. "+MeleeKillBuffs.PennantSource);
        var exodia=build.Melee.Exodia is {Active:true} x?x:null;
        var exodiaCode=exodia is null?null:MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==exodia.Id).Code;
        var hasBrave=exodiaCode=="Brave";var mightRate=exodiaCode=="Might"?.075*(exodia!.Rank+1):0;
        if(hasBrave)notes.Add("엑소디아 브레이브: 직접 강공격 처치 후 4초·최대3중첩, 최대 중첩에서는 갱신 없음. 어빌리티 사용 후1초·지속형 어빌리티 유지 중 재생 중단. 표시량은 이 대상 처치/교전 종료까지 생성한 에너지이며 에너지 최대치·소모량은 적용하지 않습니다. "+ExodiaBraveState.Source);
        if(mightRate>0)notes.Add("엑소디아 마이트: 지상·일반 마무리·파라존 처치에 50% 발동·8초. 직접/상태 피해의 방어 적용 후 흡혈을 표본별 계산합니다. 처음 발동시킨 마무리의 회복은 호스트/솔로일 때만 추가되어 별도 표시합니다. 회복 상한은 적용하지 않습니다. 공식34.0 https://www.warframe.com/en/patch-notes/pc/34-0-0");
        var proton=build.Slots.SingleOrDefault(s=>s is not null&&MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==s.ModId).Condition=="ProtonSnap");
        if(proton is not null)notes.Add("프로톤 스냅: '벽 매달리기 2초 완료' 행동 시점부터 랭크별 5/10/15/20초 적용합니다. 독성은 장착 순서로 재조합하고 상태 확률은 스탠스 배율 뒤에 고정 가산합니다.");
        if(burningHate)notes.Add("버닝 헤이트: 화염 상태인 적에게 헤이트 피해가 들어간 이후의 새 상태만 증폭합니다. 기존 상태와 표식을 적용한 공격의 상태에는 소급하지 않으며 화염이 끝나도 표식은 남습니다.");
        if(profile.Name=="Spinnerex")notes.Add("스피네렉스 처치 폭발은 주변 적에게 가는 별도 결과입니다. 중심에서의 피해만 제공하며 위키에 없는 거리 감쇠 곡선과 주변 적의 방어층·연쇄 처치를 추정하지 않습니다.");
        var actions=settings.Inputs??[];
        var hasSouls=TatsuSoulState.Applies(build.WeaponId);
        TatsuSoulState? CreateSouls()
        {
            if(!hasSouls)return null;
            var count=build.Melee.Conditions?.GetValueOrDefault("SoulCharges")??0;
            if(count!=Math.Floor(count))throw new ArgumentException("소울 스웜 저장량은 정수입니다.");
            return new((build.Melee.Conditions?.GetValueOrDefault("Signature")??0)>0,(int)count,build.Melee.Conditions?.GetValueOrDefault("SoulRemaining",120)??120);
        }
        _=CreateSouls();
        if(hasSouls)notes.Add("소울 스웜: 이 무기 처치마다1개·일반5/레버넌트9개·120초 전체 갱신. 슬라이드 발사와 투사체 명중은 행동에서 시각/대상을 지정합니다. 20m/s·30m 유도 범위는 원문 값이며 휘어진 비행 경로·자동 대상 배정·슬라이드 입력과 검 접촉의 시간차는 추정하지 않습니다. 기절의 회복 시점은 미기재입니다. "+TatsuSoulState.Source);
        if(profile.Code=="AckBrunt")notes.Add("액크 & 브런트: 막은 기본 원소별17.5%·합계4충전·다음 공격 입력에 소모. 일렉트로마그네틱 실딩 전달 피해는 원소 충전만 생성하며 직접 막기 콤보로 세지 않습니다. "+AckBruntElementState.Source);
        foreach(var action in actions)
        {
            if(action.RecentlyAwareTargets is {} aware&&(action.Kind!="ExaltedSlideBlind"||aware.Any(i=>i<1||i>enemyBuilds.Length)||aware.Distinct().Count()!=aware.Length))throw new ArgumentException("직전 인지 대상은 실명 시전의 실제 대상 번호로 지정하세요.");
            if(action.Kind=="CobraNullifierBubble"&&(!cobraSleep||action.Value<1||action.Value>enemyBuilds.Length||action.Value!=Math.Floor(action.Value)))throw new ArgumentException("널리파이어 보호막 수면은 바루크의 코브라 & 크레인에서 실제 보호막 소유자 대상 번호를 지정하세요.");
            if(action.Kind=="ExaltedSlideBlind"&&ExaltedRules.Find(build.WeaponId)?.Family!=ExaltedFamily.Blade)throw new ArgumentException("슬라이드 실명에는 익절티드 블레이드가 필요합니다.");
            if(action.Duration is {} recovery)
            {
                if(action.Kind is not ("EnergyOrbPickup" or "UniversalOrbPickup"))throw new ArgumentException("이 행동에는 넉다운 회복시간을 지정하지 않습니다.");MeleeEngine.Range(recovery,.000001,300,"사용자 지정 넉다운 회복시간");
            }
            if(action.Kind is "EnergyOrbPickup" or "UniversalOrbPickup"&&eruptionSlot is null)throw new ArgumentException("현재 오브 습득 사건은 아케인 이럽션을 장착한 교전에서 계산합니다.");
            if(action.Kind is "BlockedElement" or "ShieldingElement")
            {
                if(profile.Code!="AckBrunt"||action.Element is not {} element||!AckBruntElementState.Elements.Contains(element)||action.Value!=Math.Truncate(action.Value))throw new ArgumentException("액크 & 브런트의 원소 흡수에는 기본 원소와 실제 정수 횟수를 지정하세요.");
                if(action.Kind=="ShieldingElement"&&!build.Slots.Any(s=>s?.ModId==AckBruntElementState.ShieldingMod))throw new ArgumentException("아군의 원소 피해를 전달받으려면 일렉트로마그네틱 실딩을 장착하세요.");
            }
            if(action.Kind=="AbilityRadiationInfliction"&&(action.Value!=Math.Floor(action.Value)||action.Value>10000))throw new ArgumentException("어빌리티 방사능은 실제 부여 횟수를 입력하세요.");
            if(action.Kind is "ExaltedActivate" or "ExaltedDeactivate"&&ExaltedRules.Find(build.WeaponId) is not {Pseudo:false})throw new ArgumentException("지속 소환하는 익절티드 무기에서 사용할 수 있는 행동입니다.");
            if(action.Kind is "OperatorOut" or "OperatorReturn"&&build.Melee.AbilityAugment?.ModId!=ExaltedAugments.Enraged)throw new ArgumentException("현재 이 전이 시간 규칙은 인레이지드 재사용 대기에 연결되어 있습니다.");
            if(action.Kind is "AbilityNullified" or "FallOutOfBounds"&&ExaltedRules.Find(build.WeaponId) is null&&build.Melee.ExternalEquipment?.Warcry is null&&build.Melee.ExternalEquipment?.Metronome is null&&build.Melee.ExternalEquipment?.JetStream!=true)throw new ArgumentException("익절티드 어빌리티 또는 외부 버프의 해제 행동입니다.");
            if(action.Kind=="PlayerOverguardGain"&&truculenceSlot is null)throw new ArgumentException("오버가드 획득 사건에는 아케인 트루큘런스를 장착하세요.");
            if(action.Kind=="ExaltedAbilityCast"&&ExaltedRules.Find(build.WeaponId)?.Pseudo!=true)throw new ArgumentException("간접 익절티드 어빌리티 시전 행동입니다.");
            if(action.Kind is "VinquibusHeadshot" or "VinquibusPrimaryShot"&&build.WeaponId!=VinquibusState.WeaponId)throw new ArgumentException("빈퀴부스 장착이 필요합니다.");
            if(action.Kind is "ResidualEnter" or "ResidualTick" or "ResidualLeave")
            {if(!hasDemulcent||action.Element is null||!ResidualElementState.Elements.Contains(action.Element.Value))throw new ArgumentException("레지듀얼 행동에는 시오럼 디멀센트와 화염·냉기·전기·독성 선택이 필요합니다.");}
            else if(action.Kind is not ("BlockedElement" or "ShieldingElement")&&action.Element is not null)throw new ArgumentException("이 행동에는 별도 속성을 지정하지 않습니다.");
            if(action.Kind=="WarframeMeleeTransfer"&&magusSlot is not {Active:true})throw new ArgumentException("마구스 어그레스를 장착해야 합니다.");
            if(action.Kind is "GlaiveChainThrow" or "GlaiveNewThrow"&&profile.Raw.Text("Class")!="Glaive")throw new ArgumentException("연속 글레이브 투척에는 글레이브 무기가 필요합니다.");
            if(action.Kind.StartsWith("Tennokai",StringComparison.Ordinal)&&!tennokaiEnabled)throw new ArgumentException("텐노카이 행동에는 텐노카이 모드를 장착한 시간 교전이 필요합니다.");
            if(action.Kind is "SoulSwarmSlideInput" or "SoulSwarmImpact")
            {
                if(!hasSouls)throw new ArgumentException("소울 스웜 행동에는 타츠 또는 타츠 프라임이 필요합니다.");
                if(action.Kind=="SoulSwarmImpact"&&(action.Value!=Math.Floor(action.Value)||action.Value<1||action.Value>enemyBuilds.Length))throw new ArgumentException("소울 스웜 명중에는 존재하는 대상 번호를 지정하세요.");
            }
            if(!double.IsFinite(action.Time)||action.Time<0||action.Time>300||!double.IsFinite(action.Value)||action.Value<=0||action.Value>(action.Kind is "BlockedDamage" or "PlayerShieldDamage" or "PlayerOverguardGain"?1e12:10000)||action.Kind is not ("RubbleNullifierEnter" or "RubbleNullifierExit" or "RubbleHostMigration" or "MirrorsCast" or "MirrorsEnd" or "MirrorNullified" or "JetStreamEnter" or "JetStreamLeave" or "JetStreamLost" or "OperaBuff" or "ForteBuff" or "MetronomeBuffsLost" or "WarcryCast" or "WarcryReenter" or "WarcryCasterDispel" or "WarcryCasterMeleeKill" or "HeavyAttackMiss" or "CobraNullifierBubble" or "ExaltedSlideBlind" or "WarframeMeleeTransfer" or "HeavyAnimationStart" or "HeavyAnimationEnd" or "AbilityEnergySpent" or "EnergyOrbPickup" or "UniversalOrbPickup" or "ExaltedActivate" or "ExaltedDeactivate" or "OperatorOut" or "OperatorReturn" or "AbilityNullified" or "FallOutOfBounds" or "BlockedElement" or "ShieldingElement" or "AbilityRadiationInfliction" or "PlayerOverguardGain" or "ExaltedAbilityCast" or "ValkyrFatalHit" or "VinquibusHeadshot" or "VinquibusPrimaryShot" or "ResidualEnter" or "ResidualTick" or "ResidualLeave" or "AbilityHeatInfliction" or "GlaiveChainThrow" or "GlaiveNewThrow" or "TennokaiHeavyInput" or "TennokaiFinish" or "TennokaiCancel" or "EquipPrimary" or "EquipSecondary" or "BlockedDamage" or "SoulSwarmSlideInput" or "SoulSwarmImpact" or "AbilityCast" or "IncomingHit" or "PrimaryKill" or "MeleeKill" or "HeavyMeleeKill" or "MeleeAssist" or "FinisherKill" or "GroundFinisherKill" or "ParazonKill" or "EquipArchgun" or "PlayerShieldDamage" or "PlayerMagnetic" or "WallLatch" or "WallLatchTwoSeconds" or "PlayerShieldBreak" or "RollThroughTarget" or "BlockedHit" or "AmmoPickup" or "GroundSlide" or "ChannelStart" or "ChannelStop" or "EquipMelee" or "FinisherHit" or "SlideMeleeKill" or "HarmonicResonance" or "EvensongDevotion" or "CantareChorale" or "JadeEnsemble" or "IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput"))throw new ArgumentException("외부 사건의 종류·시간·수량 오류");
            if(action.Kind is "VinquibusHeadshot" or "ResidualTick" or "AbilityHeatInfliction" or "BlockedHit" or "AmmoPickup"&&action.Value!=Math.Floor(action.Value))throw new ArgumentException("막은 공격·탄약 습득 횟수는 정수입니다.");
            if((action.Kind=="HeavyMeleeKill"||IncarnonFormState.IsActivationInput(action.Kind))&&(action.Value!=Math.Floor(action.Value)||action.Value>12))throw new ArgumentException("처치 강공격의 콤보 배수는 1~12입니다.");
            if(action.Kind is not ("MirrorNullified" or "CobraNullifierBubble" or "ExaltedSlideBlind" or "AbilityEnergySpent" or "BlockedElement" or "ShieldingElement" or "AbilityRadiationInfliction" or "PlayerOverguardGain" or "VinquibusHeadshot" or "ResidualTick" or "AbilityHeatInfliction" or "BlockedDamage" or "SoulSwarmImpact" or "PlayerShieldDamage" or "BlockedHit" or "AmmoPickup" or "GroundSlide" or "HeavyMeleeKill" or "IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput")&&action.Value!=1)throw new ArgumentException("이 행동은 사건 한 건씩 입력하세요.");
        }
        if(actions.Any(a=>a.Kind=="ExaltedSlideBlind"))notes.Add("슬라이드 실명: 실제 효과 발생 시각에5m×어빌리티 거리·6초×지속을 적용합니다. 오버가드 면역이며 실명 자체는 CO 상태 종류나 마무리 노출을 추가하지 않습니다. 가려진 적은 지정한 직전 인지 대상만 포함합니다. 정확한 인지 유효 시간은 위키 미기재라 해당 목록은 사용자 시나리오입니다. "+ExaltedBlind.Source+" "+ExaltedBlind.SightSource);
        if(actions.Any(a=>a.Kind is "RubbleNullifierEnter" or "RubbleNullifierExit" or "RubbleHostMigration")&&build.Melee.ExternalEquipment?.AtlasRubble is null)throw new ArgumentException("러블 행동에는 아틀라스 러블 설정이 필요합니다.");
        if(actions.Any(a=>a.Kind is "MirrorsCast" or "MirrorsEnd" or "MirrorNullified")&&build.Melee.ExternalEquipment?.HallOfMirrors is null)throw new ArgumentException("분신 행동에는 홀 오브 미러즈 설정이 필요합니다.");
        if(hologramContacts.Length>0||weaponActions.Any(a=>a.Hologram>0)||zenistarDeployments.Any(d=>d.Hologram>0)||glaiveFlights.Any(f=>f.Hologram>0)||shieldFlights.Any(f=>f.Hologram>0))notes.Add("분신 피해는 지정한 실제 명중만 합산합니다. 콤보·플레이어 근접 처치 효과는 획득하지 않습니다. 아케인 발동은 본문/버그 문구 충돌 시 명시한 본문 분기에서 제외합니다. "+HallOfMirrorsRules.Source);
        if(actions.Any(a=>a.Kind.StartsWith("JetStream",StringComparison.Ordinal))&&jetStream is null)throw new ArgumentException("제트 스트림 행동을 사용하려면 외부 지원에서 버프를 선택하세요.");
        if(jetStream is not null)notes.Add("제트 스트림:범위 밖으로 나간 뒤3초 유지·재진입으로 갱신·투사체 속도와 건블레이드 감쇠 거리 반영. 비행 도중 버프 전이는 원문 미기재라 수명을 임의로 다시 계산하지 않습니다. "+MeleeExternalRules.JetStreamSource);
        if(actions.Any(a=>a.Kind is "OperaBuff" or "ForteBuff" or "MetronomeBuffsLost")&&build.Melee.ExternalEquipment?.Metronome is null)throw new ArgumentException("메트로놈 행동을 사용하려면 오페라/포르테 지원을 설정하세요.");
        if(!settings.ScriptedAttacksOnly&&WeaponStanceMechanic.IsQuassus(build.WeaponId)&&(MetronomeRules.Opera(build.Melee.ExternalEquipment?.Metronome)>0||actions.Any(a=>a.Kind=="OperaBuff"))&&steps.Any(s=>s.Build.Melee!.Attack.Contains("Ethereal Daggers",StringComparison.Ordinal)))issues.Add("오페라로 추가되는 단검의 접촉 시각은 위키 미기재입니다. 고유 공격·접촉 경로에 가능한 추가 단검까지 지정하면 발사당 정수/소수 확률과 관통 경로를 함께 계산합니다.");
        if(actions.Any(a=>a.Kind.StartsWith("Warcry",StringComparison.Ordinal))&&build.Melee.ExternalEquipment?.Warcry is null)throw new ArgumentException("워크라이 행동을 사용하려면 외부 지원에서 워크라이를 설정하세요.");
        if(build.Melee.ExternalEquipment?.Warcry is not null)notes.Add("워크라이:현재 위키 기본10/14/17/20초·공속은 퓨리와 가산·방어력은 수신자 기본값 기준. 이터널 워는 시전자 근접 처치당 지속 증가·최대 잔여2배. 원반 처치는 자신의 시전만 유지합니다. 아군 무효화/재진입은 최신 어빌리티 문서를 우선합니다. "+WarcryRules.Source+" "+WarcryRules.EternalSource+" "+WarcryRules.ReworkSource);
        if(actions.Any(a=>a.Kind is "MeleeKill" or "HeavyMeleeKill" or "MeleeAssist"))notes.Add("다른 적 처치·도움은 현재 무기로 기록한 외부 사건입니다. 그 적의 피해·공격 동작·콤보 획득/소모를 이 대상에 합산하지 않습니다. 강공격 처치의 입력 배수는 그 공격이 콤보를 소모하기 전의 값입니다.");
        if(issues.Count>0)return new(seconds,0,0,new(null,null,null,0,0,44061,seconds),[],[],0,notes.ToArray(),issues.Distinct().ToArray());
        if(actions.Any(a=>IncarnonFormState.IsActivationInput(a.Kind)))
        {
            if(formDefinition is null)throw new ArgumentException("인카논이 없는 무기의 변신 행동입니다.");
            if(!settings.ScriptedAttacksOnly)issues.AddRange(MeleeMechanicScope.TimeIssues(profile,steps.Select(s=>s.Build.Melee! with {Incarnon=true}),steps.Any(s=>s.Finisher),stance?.Combo.Hits.Any(h=>h.AttackType=="Slam")==true));
            if(issues.Count>0)return new(seconds,0,0,new(null,null,null,0,0,44061,seconds),[],[],0,notes.ToArray(),issues.Distinct().ToArray());
        }
        var pointCount=seconds==0?1:101;var sums=new double[pointCount,47];var events=new List<CombatEvent>();
        var killTimes=new double[trials];Array.Fill(killTimes,double.PositiveInfinity);
        var random=new Random(44061);double totalDirect=0,totalStatus=0;
        double spinnerActivations=0,spinnerDamage=0,spinnerTick=0;
        var obexArea=formDefinition?.Code=="Obex";double obexWaves=0,obexDamage=0,obexStealthDamage=0;
        double totalCriticalHealing=0;
        var damageMoments=new SampleMoments();var energyMoments=new SampleMoments();var mightMoments=new SampleMoments();var hostMightMoments=new SampleMoments();var nikanaHealingMoments=new SampleMoments();var heavyHealingMoments=new SampleMoments();var returnMoments=new SampleMoments();var returnUpperMoments=new SampleMoments();var sanctiMoments=new SampleMoments();var sanctiUpperMoments=new SampleMoments();
        var enemySums=new double[enemyBuilds.Length,7];var enemyMoments=enemyBuilds.Select(_=>new SampleMoments()).ToArray();var enemyDeaths=enemyBuilds.Select(_=>new double[trials]).ToArray();
        if(enemyBuilds.Length>1)notes.Add("다중 대상 · 위치는 대상1 기준 좌표. 공격 순서는 입력별 목표이며 검의 직접 타격은 그 대상에 명중하고, 등록된 범위 공격은 입력한 중심/반경의 주변 적도 판정합니다. 범위 중심은 선택한 대상에서 입력 거리만큼 앞뒤 음수 방향입니다. 같은 폭발의 직접 피해는 발동 전 플레이어 버프를 공유하며, 적별 상태/처치 사건은 선택 대상부터 대상 번호 순서로 처리합니다. 살아있는 순서의 다음 목표로 전환하며 이동 시간·검 궤적은 가정하지 않습니다. 대상별 피해/처치 시간은 분리됩니다.");
        var computationCache=new Dictionary<string,Calculation>();
        var falloutMoments=new SampleMoments();var hysteriaMoments=new SampleMoments();var exalted=ExaltedRules.Find(build.WeaponId);
        if(exalted?.Family==ExaltedFamily.Wind)notes.Add("서린 스톰: 파동의 비틀거림 및 주먹/파동의 넉다운·띄우기·래그돌·비틀거림으로 영구 무장 해제 효과를 적용합니다. 오버가드가 남으면 적용되지 않습니다. 적별 발동 여부를 보존하며 무장 해제를 컨오 상태 종류로 더하지 않습니다. "+DesertWindControl.Source);
        var castAtContact=timed&&exalted?.Pseudo==true&&exalted.Family!=ExaltedFamily.Clones&&!(settings.Inputs??[]).Any(a=>a.Kind=="ExaltedAbilityCast");
        if(castAtContact)notes.Add("어빌리티 시전 시각을 별도 입력하지 않아 사용자 지정 반복 간격의 각 공격을 한 번의 시전으로 처리합니다. 별도 시전 시각을 입력하면 그 시각으로 재시전 콤보와 발동 효과를 계산합니다.");
        if(build.Melee.AbilityAugment?.ModId==ExaltedAugments.Enraged)notes.Add("인레이지드:15초×어빌리티 지속·해제/만료 뒤 실제 유지한 시간만큼 재사용 대기. 전이 중 종료하면 복귀할 때 대기를 시작하며 이미 시작한 대기를 멈추지는 않습니다. 다시 소환하는 실제 행동을 입력해야 공격이 재개됩니다. "+ExaltedLifetime.Source);
        var reaperMoments=new SampleMoments();var incarnonHealMoments=new SampleMoments();
        for(var trial=0;trial<trials;trial++)
        {
            token.ThrowIfCancellationRequested();
            if(trial%16==0)progress?.Report(new("시간별 전투",trial,trials));
            var enemies=enemyBuilds.Select((b,i)=>new EnemyRuntime(b,i,i==0?null:settings.NearbyEnemies![i-1])).ToArray();
            if(falloutSlot is not null)foreach(var target in enemies)target.Fallout=new(falloutSlot.Rank);
            double droppedOrbs=0;
            var primary=enemies[0];var orderIndex=0;var focus=enemies[targetOrder[0]];var enemy=focus;
            var form=formDefinition is null?null:new IncarnonFormState(build);
            var warcry=build.Melee.ExternalEquipment?.Warcry is {} warcrySetup?new WarcryState(warcrySetup):null;
            var metronome=build.Melee.ExternalEquipment?.Metronome is {} metronomeSetup?new MetronomeState(metronomeSetup):null;
            var mirrors=build.Melee.ExternalEquipment?.HallOfMirrors is {} mirrorSetup?new HallOfMirrorsState(mirrorSetup):null;var hologramIndex=0;var mirrorFlights=new Dictionary<string,int>();
            var rubble=build.Melee.ExternalEquipment?.AtlasRubble is {} rubbleSetup?new AtlasRubbleState(rubbleSetup):null;var rubbleIndex=0;
            var souls=CreateSouls();
            var abilityCasts=exalted?.Pseudo==true?new AbilityComboCastState(initial.Melee.ComboDuration):null;
            var valkyr=exalted?.Family==ExaltedFamily.Talons?new ValkyrRageState(build.Melee.Conditions?.GetValueOrDefault("ValkyrRage")??0,build.Melee.Conditions?.GetValueOrDefault("ValkyrRageGrace",5)??5):null;
            var startingRoll=(int)(build.Melee.Conditions?.GetValueOrDefault("Roll")??0);
            var amanata=hasAmanata?new AmanataState(startingRoll==0?random.Next(1,7):startingRoll,(int)(build.Melee.Conditions?.GetValueOrDefault("AmanataHits")??0)):null;
            var absorbedElements=profile.Code=="AckBrunt"?new AckBruntElementState(build.Melee.Conditions):null;
            var energyChannel=effects.GetValueOrDefault("EnergyChannel")>0?new EnergyChannelState(build.Melee.Conditions?.GetValueOrDefault("EnergySpent")??0):null;
            var truculence=truculenceSlot is not null?new TruculenceState(build.Melee.Conditions?.GetValueOrDefault("TruculenceProgress")??0):null;
            var augment=build.Melee.AbilityAugment;
            var primal=augment?.ModId==ExaltedAugments.Primal?new DelayedDecayBonus(150,1/build.Melee.AbilityDuration,5,build.Melee.Conditions?.GetValueOrDefault("PrimalRage")??0,build.Melee.Conditions?.GetValueOrDefault("PrimalRageGrace",5)):null;
            var accumulating=augment?.ModId==ExaltedAugments.Accumulating?new DelayedDecayBonus(350,2.5,10,build.Melee.Conditions?.GetValueOrDefault("AccumulatingWhipclaw")??0,build.Melee.Conditions?.GetValueOrDefault("WhipclawGrace",10)):null;
            var whipRecipients=new HashSet<int>();
            var exaltedLife=exalted is {Pseudo:false}?new ExaltedLifetime(augment?.ModId==ExaltedAugments.Enraged?15*build.Melee.AbilityDuration:null,augment?.ModId==ExaltedAugments.Enraged?build.Melee.Conditions?.GetValueOrDefault("EnragedElapsed")??0:0):null;
            double hysteriaHealing=0;double? cloneCastSpeed=exalted?.Family==ExaltedFamily.Clones?initial.AttackSpeed:null;
            var vinquibus=build.WeaponId==VinquibusState.WeaponId?new VinquibusState((int)(build.Melee.Conditions?.GetValueOrDefault("VinquibusPrecision")??0),(int)(build.Melee.Conditions?.GetValueOrDefault("VinquibusFerocity")??0)):null;
            var residual=hasDemulcent?new ResidualElementState(build.Melee.Conditions):null;
            var spring=timed&&springSlot is not null?new SpringBladeState(springSlot.Rank):null;
            var tennokai=tennokaiEnabled?new TennokaiState(initial.Melee.TennokaiChance,(int)effects.GetValueOrDefault("TennokaiEvery"),initial.Melee.TennokaiWindow,truthSlot?.Rank):null;
            var tennokaiContactUsed=false;
            if(tennokai is not null&&requestedTennokai){tennokai.StartingOpportunity(initial.Melee.TennokaiWindow);tennokai.Begin(0);}
            var influence=influenceSlot is null?null:new MeleeInfluenceState(influenceSlot.Rank);
            var shards=build.WeaponId==ThalysMechanic.WeaponId?new ThalysShardState(enemies.Length,effects.GetValueOrDefault("ShardImpale")>0,effects.GetValueOrDefault("ShardDetonation")>0,effects.GetValueOrDefault("ShardDuration")>15):null;
            long swingId=0;
            var jade=new JadeWeaponBuffs();var sancti=sanctiMagistar?new SanctiMagistarState(build.Melee.Conditions?.GetValueOrDefault("SanctiHealingCombo")??0):null;
            var buffs=new Dictionary<string,(double End,double Value)>();
            var buffNames=new Dictionary<string,string>();
            var conditions=new Dictionary<string,double>(build.Melee.Conditions??[]);
            double? heavyStoredDamage=null;
            if(timed)foreach(var key in dynamicKeys)conditions[key]=0;
            var arcane=build.Arcanes??new();var exposure=timed?0:arcane.ExposureStacks;var exposureEnd=timed?-1d:double.PositiveInfinity;
            var player=CreatePlayer();
            var healthRegen=new TimedHealthRegeneration();
            var incarnonStacks=new IncarnonStackTimers(build);
            var piercing=piercingStature?new PiercingStatureState():null;
            var fortification=fortificationSlot is null?null:new MeleeFortificationState(fortificationSlot.Rank);
            var brave=hasBrave?new ExodiaBraveState(exodia!.Rank,conditions.GetValueOrDefault("Channeling")>0):null;
            double nikanaHealing=0,mightHealing=0,hostMightHealing=0,heavyHealing=0,returnHealing=0,returnHealingUpper=0,sanctiHealing=0,sanctiHealingUpper=0;
            var time=0d;var nextHit=settings.ScriptedAttacksOnly?double.PositiveInfinity:0d;var hit=0;var point=0;var inputIndex=0;var budget=0;
            var contagionFlights=new ContagionFlightState();var collisionIndex=0;
            var epidemicIndex=0;
            var cloudIndex=0;var ichorFields=new List<IchorField>();
            var poolIndex=0;var poolSources=new Dictionary<int,Build>();
            var okina=OkinaDaggerState.Applies(build.WeaponId)?new OkinaDaggerState():null;var okinaIndex=0;var okinaSources=new Dictionary<int,Build>();
            var orvius=new OrviusHoverState();var orviusIndex=0;var orviusSources=new Dictionary<int,Build>();
            var waveIndex=0;var waveActors=new Dictionary<int,Build>();var wavePierces=new Dictionary<int,int>();var waveTennokaiUses=new Dictionary<int,int>();var waveHeavyConsumed=new HashSet<int>();
            var glass=new VitricaGlassState();var glassIndex=0;var glassSlamIndex=0;
            var rapierIndex=0;var rapierSources=new Dictionary<int,Build>();
            var innodemIndex=0;var innodemSources=new Dictionary<int,Build>();
            var bladeIndex=0;var bladeSources=new Dictionary<int,Build>();var bladeInputResources=new Dictionary<int,Build>();
            var magus=timed&&magusSlot is {Active:true}?new MagusAggressState():null;
            var heavyPassiveCombo=1d;
            var vinquibusShots=new Dictionary<int,VinquibusProjectileState>();
            var weaponActionIndex=0;var weaponActionSources=new Dictionary<int,Build>();var weaponActionConnected=new HashSet<int>();var weaponActionPellets=new Dictionary<int,int>();
            var hologramGlaives=Enumerable.Range(1,4).ToDictionary(n=>n,_=>new GlaiveFlightState());
            var shieldIndex=0;var shieldSources=new Dictionary<int,Build>();
            var glaive=new GlaiveFlightState();var glaiveIndex=0;var glaiveSources=new Dictionary<int,Build>();var wolfHeavySources=new Dictionary<int,Build>();var wolfAnimosityInputs=new HashSet<int>();
            var zenistar=new ZenistarDiscState(mirrors);var zenistarIndex=0;
            var ceramicIndex=0;var ceramicSources=new Dictionary<int,Build>();var ceramicAnimosityUsed=new HashSet<int>();
            var contagionOrigins=new Dictionary<int,Build>();
            var delayedGunshots=new List<DelayedGunshot>();
            double reaperTime=0,reaperHealing=0;
            double PlayerTime()=>Math.Min(time,enemies.Max(e=>e.KillTime));
            void AdvanceReaper()
            {
                var until=PlayerTime();if(buffs.TryGetValue("Reaper",out var reaper))reaperHealing+=4*reaperLevel*Math.Max(0,Math.Min(until,reaper.End)-reaperTime);reaperTime=until;
            }
            if(timed&&stance is not null)nextHit=times![0]/initial.AttackSpeed;
            var scheduledSpeed=initial.AttackSpeed;
            var threshold=profile.Code=="DexNikana"?11:20;var maxCombo=catalog.Weapons.Single(w=>w.Id==build.WeaponId).MaxCombo;
            var comboCounter=new MeleeComboCounter(initial.Melee.InitialCombo,build.Scenario.Combo,threshold,maxCombo,initial.Melee.ComboDuration);
            var gasThisHit=false;var animosity=(int)conditions.GetValueOrDefault("AnimosityStacks");
            var careenReady=0d;
            var barrierReady=0d;
            var invigoratedEnd=-1d;var invigoratedCount=0;
            var vendettaAttacks=profile.Name=="Dorrclave"&&conditions.GetValueOrDefault("Vendetta")>0?10:0;
            var vendettaKills=0;
            var killBuffs=new MeleeKillBuffs(isPennant,hasBerserker,galvMods.Length>0);
            var rapidStacks=conditions.GetValueOrDefault("FinisherStacks");
            var startFinisherKills=arcane.FinisherKills;var finisherKills=startFinisherKills;
            var startPrimaryKills=conditions.GetValueOrDefault("PrimaryKills");var primaryKills=startPrimaryKills;
            var groupConnected=false;var groupUsedVendetta=false;var groupFinisherForm=false;bool? contactAirborne=null;
            var orderedActions=actions.OrderBy(a=>a.Time).ToArray();
            void Log(string kind,string description,double amount=0){if(trial==0&&events.Count<320)events.Add(new(time,kind,(enemies.Length>1?$"대상{enemy.Index+1} · ":"")+description,amount));}
            void AtEnemy(EnemyRuntime destination,Action action){var old=enemy;enemy=destination;try{action();}finally{enemy=old;}}
            Dictionary<DamageType,int> Status()
            {
                var counts=enemy.Procs.Where(p=>p.End>time+1e-9).GroupBy(p=>p.Type).ToDictionary(g=>g.Key,g=>g.Count());
                // Primer mode evaluates only the first contact. Keep its selected
                // external statuses without inventing their weapon damage/ticks.
                if(mode==EncounterMode.Primer)foreach(var (type,count) in enemy.Initial.Melee!.Statuses??[])counts[type]=counts.GetValueOrDefault(type)+count;
                if(ruvoxPerforation&&enemy.ImpaleEnd>time&&enemy.Pools.Overguard==0)counts[DamageType.Puncture]=5;
                return counts.Where(p=>p.Value>0).ToDictionary(p=>p.Key,p=>Math.Min(p.Key==DamageType.Cold&&enemy.Pools.Overguard>0?4:TargetStatusRules.Maximum(p.Key),p.Value));
            }
            double Armor()
            {
                if(strip<=0&&daggerStrip<=0||enemy.Armor<=0)return enemy.Armor;
                return FlatArmorReduction.Remaining(enemy.Initial,enemy.RemovedArmor);
            }
            double HeatStrip()
            {
                if(enemy.HeatStart<0)return 0;
                var peak=EncounterEngine.HeatStrip(Math.Min(time,enemy.HeatEnd)-enemy.HeatStart,enemy.HeatDuration/6);
                if(time<=enemy.HeatEnd)return peak;
                return Math.Min(peak,(int)Math.Floor((time-enemy.HeatEnd)/1.5) switch{0=>.5,1=>.4,2=>.3,3=>.15,_=>0});
            }
            ArcaneSlot? Active(ArcaneSlot? slot)
            {
                if(slot is null||!slot.Active||!timed)return slot;
                var code=MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code;
                if(code=="Ultimatum")return slot with {Active=buffs.Any(b=>b.Key.StartsWith("Ultimatum:",StringComparison.Ordinal)&&b.Value.End>time)};
                return AutomaticArcane(code)?slot with {Active=buffs.TryGetValue(code,out var b)&&b.End>time}:slot;
            }
            void AdvanceForm()
            {
                if(form?.Advance(PlayerTime())==true)Log("incarnonEnd","인카논 종료 · 변신 처치 누적/리질리언스 해제");
            }
            Build Input(Step step)
            {
                foreach(var key in CombatScene.EnemyConditionKeys)conditions[key]=enemy.Conditions.GetValueOrDefault(key);
                if(enemy.EruptionEnd>PlayerTime()||!timed&&enemy.EruptionEnd==PlayerTime())conditions["Knockdown"]=1;
                AdvanceForm();
                if(form?.Definition.Code=="Destreza")conditions["IncarnonHeavyKills"]=form.HeavyKills;
                player?.Advance(PlayerTime());
                fortification?.Advance(PlayerTime());
                comboCounter.Advance(time);
                if(timed)foreach(var key in dynamicKeys)conditions[key]=buffs.TryGetValue(key,out var b)&&b.End>time?b.Value:0;
                if(amanata is not null)conditions["Roll"]=amanata.Roll;
                if(energyChannel is not null)conditions["EnergySpent"]=energyChannel.StoredEnergy;
                if(valkyr is not null){valkyr.Advance(PlayerTime());conditions["ValkyrRage"]=valkyr.DamagePercent;}
                if(primal is not null){primal.Advance(PlayerTime());conditions["PrimalRage"]=primal.Value;}
                if(accumulating is not null){accumulating.Advance(PlayerTime());conditions["AccumulatingWhipclaw"]=accumulating.Value;}
                if(exaltedLife is not null){if(exaltedLife.Advance(PlayerTime()))primal?.Reset(PlayerTime());conditions["ExaltedInactive"]=exaltedLife.CanAttack?0:1;}
                if(cloneCastSpeed is {} castSpeed)conditions["ClonesCastSpeed"]=castSpeed;
                if(abilityCasts is not null)conditions["AbilityComboSequence"]=Math.Max(1,abilityCasts.Sequence);
                if(vinquibus is not null)conditions["VinquibusPrecision"]=vinquibus.Precision;
                if(residual is not null)foreach(var type in ResidualElementState.Elements)conditions["Residual"+type]=residual.Count(type,PlayerTime());
                if(hasHotShot&&(timed||buffs.ContainsKey("HotShotStacks")))conditions["HotShotStacks"]=buffs.TryGetValue("HotShotStacks",out var hotState)&&hotState.End>time?hotState.Value:0;
                if(timed&&piercing is not null){piercing.Advance(PlayerTime());conditions["PunctureStacks"]=piercing.Stacks;}
                if(spring is not null)conditions["SpringStacks"]=spring.Count(PlayerTime());
                if(timed)foreach(var perk in stackPerks)conditions[perk.Condition]=incarnonStacks.Count(perk.Condition,PlayerTime());
                if(nearbyFurax&&timed)conditions["NearbyEnemies"]=Math.Min(5,enemies.Count(e=>e.Pools.Health>0&&CombatScene.PlayerDistance(build,e.Position)<=6));
                if(player is not null)conditions["Overshields"]=player.Overshields>0?1:0;
                if(timed)
                {killBuffs.Advance(PlayerTime());conditions["PennantKills"]=killBuffs.PennantStacks;conditions["BerserkerStacks"]=killBuffs.BerserkerStacks;conditions["GalvanizedStacks"]=killBuffs.GalvanizedStacks;}
                conditions["AnimosityStacks"]=animosity;
                if(magus is not null)conditions["MagusAggressCharges"]=magus.Charges;
                if(timed)conditions["UltimatumStacks"]=buffs.Count(b=>b.Key.StartsWith("Ultimatum:",StringComparison.Ordinal)&&b.Value.End>time);
                conditions["UniversalStatusDuration"]=(build.Melee.Conditions?.GetValueOrDefault("UniversalStatusDuration")??0)+100*jade.StatusDurationBonus(PlayerTime());
                if(gunAndBlade is not null)conditions["PrimaryKills"]=primaryKills;
                if(rapidConclusion is not null)conditions["FinisherStacks"]=rapidStacks;
                if(ruvox&&mode is not (EncounterMode.FirstHit or EncounterMode.Primer))conditions["Impaled"]=enemy.ImpaleEnd>time&&enemy.Pools.Overguard==0?1:0;
                if(ruvox&&timed)conditions["ImpaledFive"]=enemies.Count(e=>e.Pools.Health>0&&e.Pools.Overguard==0&&e.ImpaleEnd>time)>=5?1:0;
                if(profile.Name=="Dorrclave")conditions["Vendetta"]=vendettaAttacks>0?1:0;
                if(exposureEnd<=time)exposure=0;
                conditions["StealthCooldown"]=Math.Max(0,enemy.StealthReadyAt-time);
                var statuses=Status();
                // Skana Wartime Nerve: Overguard damage does not disqualify a
                // target. Its health AND shields must both still be undamaged.
                if(timed)conditions["Undamaged"]=enemy.Pools.Health==(enemy.Initial.Scenario.Enemy?.Health??10000)&&enemy.Pools.Shields==enemy.Initial.Melee!.Shields?1:0;
                var frozen=timed?enemy.FrozenEnd>time:enemy.Initial.Arcanes?.Frozen==true&&enemy.Pools.Overguard==0;
                if(frozen)statuses[DamageType.Cold]=10;
                var hitConditions=new Dictionary<string,double>(conditions);
                if(step.Aerial)hitConditions["Airborne"]=1;
                if(enemy.FinisherExposure.Active(time))hitConditions["FinisherExposed"]=1;
                if(enemy.BladeBlindEnd>time||enemy.PoolBlindEnd>time||enemy.PoolBlindUnknownSince==time)hitConditions["Blinded"]=1;
                if(step.Glaive is {} flight){hitConditions["AirborneThrow"]=flight.Airborne?1:0;if(flight.Hologram>0)hitConditions["ConsecutiveThrows"]=hologramGlaives[flight.Hologram].Consecutive;}
                if(step.PrecisionAtShot is {} precision)hitConditions["VinquibusPrecision"]=precision;
                if(timed)hitConditions["TruthFlame"]=tennokai?.ActiveBonus==true?1:0;
                if(heavyStoredDamage is {} stored&&MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack).Heavy)hitConditions["BlockedDamage"]=stored;
                var resolvedAttack=build.WeaponId==ZenistarDiscState.WeaponId&&zenistarDeployments.Length>0&&step.Build.Melee!.Attack is "Normal Attack" or "Attacks While Disc Deployed"?(zenistar.Deployed(time,step.Build.Melee!.Hologram?.Number??0)?"Attacks While Disc Deployed":"Normal Attack"):step.Build.Melee!.Attack;
                return step.Build with {Arcanes=arcane with {Melee=Active(arcane.Melee),Warframe1=Active(arcane.Warframe1),Warframe2=Active(arcane.Warframe2),PlayerArmor=arcane.PlayerArmor+(fortification?.Bonus??0),ExposureStacks=exposure,Frozen=frozen,FinisherKills=finisherKills,Shields=player?.Shields??arcane.Shields,Overshields=player?.Overshields??arcane.Overshields},
                    Melee=step.Build.Melee! with {ExternalEquipment=step.Build.Melee!.ExternalEquipment is {} gear?(jetStream?.At(gear,time)??gear) with {Warcry=CurrentWarcry(),Metronome=metronome?.At(time),HallOfMirrors=mirrors?.At(time),AtlasRubble=rubble?.At(time)}:null,Attack=resolvedAttack,Tennokai=timed?tennokai?.Active==true&&!tennokaiContactUsed&&MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack).Heavy:step.Build.Melee!.Tennokai,Faction=enemy.Initial.Melee!.Faction,Incarnon=form?.Active(time)??false,IncarnonSecondsRemaining=null,Exodia=Active(step.Build.Melee!.Exodia),Statuses=statuses,Debuffs=TargetDebuffs.AtTime(enemy.Initial.Melee!.Debuffs,PlayerTime()),Conditions=hitConditions,Shields=enemy.Pools.Shields,Overguard=enemy.Pools.Overguard,HeatArmorStrip=timed?HeatStrip():enemy.Initial.Melee!.HeatArmorStrip},
                    Scenario=step.Build.Scenario with {Combo=comboCounter.Multiplier,ExistingStatusTypes=mode==EncounterMode.Primer?enemy.Initial.Scenario.ExistingStatusTypes:0,Enemy=new(Math.Max(1,enemy.Pools.Health),Armor())}};
            }
            Calculation Evaluate(Step step,bool withoutCo=false)
            {
                if(withoutCo){var full=Evaluate(step);return full with {TargetDps=WithoutConditionOverloadDps(full)};}
                var input=Input(step);var m=input.Melee!;
                if(step.ShieldSource is {} shieldSource)return ShieldFlightRules.Calculate(catalog,ShieldFlightRules.WithCharges(input,shieldSource),step.ShieldExplosion,input.Melee!.Distance);
                if(step.Contagion is {} phase)return ExodiaContagionMechanic.Evaluate(catalog,input,step.Travel,phase,useInitialComboFloor:false);
                if(step.Epidemic is {} epidemicPhase)return ExodiaEpidemicMechanic.Evaluate(catalog,input,epidemicPhase,useInitialComboFloor:false);
                if(step.BounceThrow is {} thrown)return GlaiveBounceMechanic.Calculate(catalog,input,thrown,input.Melee!.Distance,useInitialComboFloor:false);
                if(step.WeaponAction is {} action)return WeaponActionFlight.Calculate(catalog,WeaponActionFlight.WithResources(input,step.Build),action,step.WeaponContact,step.WeaponExplosion,input.Melee!.Distance);
                if(step.WolfBounce)return WolfSledgeFlight.Bounce(catalog,WolfSledgeFlight.WithResources(input,step.Build),input.Melee!.Distance);
                if(step.WolfHeavySource is {} heavyResource)return WolfSledgeFlight.HeavyReturn(catalog,WolfSledgeFlight.WithResources(input,heavyResource));
                if(step.Glaive is not null)return GlaiveFlightState.Calculate(catalog,build.WeaponId==WolfSledgeFlight.WeaponId?WolfSledgeFlight.WithResources(input,step.Build):input,input.Melee!.Attack,useInitialComboFloor:false);
                if(step.ExaltedWave is {} wave)
                {
                    var heavyWave=step.WaveSource!.Input!.Melee!.Attack=="Exalted Heavy Wave";
                    return ExaltedWaveFlight.Calculate(catalog,ExaltedWaveFlight.WithResources(input,step.Build,heavyWave),step.WaveSource,wave,step.WaveTargetIndex,false);
                }
                if(step.VitricaOrigin is {} original)
                {
                    var resource=input with {Scenario=input.Scenario with {Combo=step.Build.Scenario.Combo}};
                    var glassResult=VitricaMechanic.Calculate(catalog,resource,step.VitricaHeavy,input.Melee!.Distance,useInitialComboFloor:false);return original?glassResult.GlassedTarget:glassResult.NearbyTarget;
                }
                if(step.Rapier is {} rapier)return DestrezaFlight.Calculate(catalog,input,rapier);
                if(step.Innodem is {} dart)return InnodemFlight.Calculate(catalog,input with {Melee=input.Melee! with {Incarnon=step.Build.Melee!.Incarnon}},dart,useInitialComboFloor:false);
                if(step.AckBladeSource is {} bladeResource)
                {
                    var physical=IncarnonBladeFlight.WithResources(input,bladeResource,preserveForm:false);
                    var flags=new Dictionary<string,double>(physical.Melee!.Conditions??[]){["Airborne"]=step.Build.Melee!.Conditions?.GetValueOrDefault("Airborne")??0};
                    physical=physical with {Melee=physical.Melee with {Conditions=flags}};
                    return MeleeEngine.CalculateAttack(catalog,physical,step.Hit,step.Co,step.Finisher,step.Slide,false);
                }
                if(step.Spectral is {} spectral)return IncarnonBladeFlight.Calculate(catalog,IncarnonBladeFlight.WithResources(input,step.Build),spectral,step.SpectralExplosion,input.Melee!.Distance,step.SpectralMotion,useInitialComboFloor:false);
                if(step.IchorCloud)return DualIchorCloudMechanic.Calculate(catalog,input,input.Melee!.Distance,useInitialComboFloor:false);
                if(step.CaustPool)return CaustacystMechanic.Pool(catalog,input);
                if(step.OkinaExplosion is {} burst)return OkinaDaggerState.Calculate(catalog,input,burst,input.Melee!.Distance,useInitialComboFloor:false);
                if(step.OrviusExplosion is {} orviusBlast)return OrviusHoverState.Calculate(catalog,input,orviusBlast,input.Melee!.Distance,useInitialComboFloor:false);
                if(step.ZenistarAttack is {} discAttack)return ZenistarDiscState.Calculate(catalog,input,discAttack,input.Melee!.Distance,step.ZenistarWrathful,useInitialComboFloor:false);
                if(step.CeramicDagger is {} dagger)return CeramicDaggerMechanic.Calculate(catalog,CeramicDaggerMechanic.OnTarget(step.Build,input),dagger,step.CeramicExplosion,input.Melee!.Distance,useInitialComboFloor:false);
                var key=string.Join(',',(m.Debuffs??[]).Select(d=>d.Code+":"+d.Rank+":"+d.Strength))+":"+enemy.Index+":"+Array.FindIndex(steps,s=>s.Hit==step.Hit&&s.Build.Melee!.Attack==step.Build.Melee!.Attack)+":"+m.Attack+":"+m.Distance+":"+m.Incarnon+":"+m.Tennokai+":"+withoutCo+":"+input.Scenario.Combo+":"+input.Scenario.Enemy!.Armor+":"+m.HeatArmorStrip+":"+(enemy.Pools.Shields>0)+":"+(enemy.Pools.Overguard>0)+":"+exposure+":"+(enemy.FrozenEnd>time)+":"+
                    string.Join(',',m.Statuses!.OrderBy(p=>p.Key).Select(p=>$"{(int)p.Key}={DirectStackKey(p.Key,p.Value)}"))+":"+string.Join(',',m.Conditions!.OrderBy(p=>p.Key).Select(p=>$"{p.Key}={p.Value}"))+":"+
                    string.Join(',',new[]{input.Arcanes!.Melee,input.Arcanes.Warframe1,input.Arcanes.Warframe2}.Select(a=>a?.Active))+":"+input.Arcanes.Shields+":"+input.Arcanes.Overshields+":"+input.Arcanes.FinisherKills+":"+input.Melee!.Exodia?.Active+":"+input.Arcanes.PlayerArmor;
                key+=":"+step.WhipDistribution+":"+WarcryRules.Speed(m.ExternalEquipment?.Warcry??new(Active:false))+":"+WarcryRules.Armor(m.ExternalEquipment?.Warcry)+":"+MetronomeRules.Opera(m.ExternalEquipment?.Metronome)+":"+MetronomeRules.Forte(m.ExternalEquipment?.Metronome)+":"+(m.ExternalEquipment?.JetStream==true)+":"+m.Hologram?.Number+":"+m.ExternalEquipment?.HallOfMirrors?.Strength+":"+m.ExternalEquipment?.HallOfMirrors?.Kills+":"+(m.ExternalEquipment?.AtlasRubble?.Armor>1400)+":"+(m.ExternalEquipment?.AtlasRubble?.Armor??0);
                if(computationCache.TryGetValue(key,out var found))return found;
                var c=MeleeEngine.CalculateAttack(catalog,input,step.Hit,step.Co,step.Finisher,step.Slide,false,sourceMultiplier:step.WhipDistribution?.5:1);
                if(computationCache.Count<16384)computationCache[key]=c;return c;
            }
            void Buff(string key,double duration,double value=1,int cap=1)
            {
                var previous=buffs.GetValueOrDefault(key);buffs[key]=(time+duration,Math.Min(cap,(previous.End>time?previous.Value:0)+value));
                var label=key switch{"HotShotStacks"=>"아케인 핫 샷","SlideHitBuff"=>"앙쿠 슬라이드 사거리/베기","ShieldBreak"=>"실드 파괴 진화","Guardian"=>"아케인 가디언","Reaper"=>"아케인 리퍼","Tanker"=>"아케인 탱커","Trickery"=>"아케인 트리커리",var u when u.StartsWith("Ultimatum:",StringComparison.Ordinal)=>"아케인 얼티메이텀 · "+(u.EndsWith("Ground")?"지상":u.EndsWith("Parazon")?"파라존":"일반 마무리"),"Fury"=>"아케인 퓨리","Strike"=>"아케인 스트라이크","Avenger"=>"아케인 어벤저","BladeCharger"=>"블레이드 차저","Exposure"=>"밀리 익스포저","Arachne"=>"아케인 아라크네","Assimilation"=>"밀리 어시밀레이션","Frenzy"=>"리션 격노","GasBuff"=>"듀얼 비시스 가스 강화","HeavyInsight"=>"에크하인 강공격 강화","SibearBuff"=>"사이베어 상태 확률","ExtendedClaws"=>"케라티노스 사거리","Overheat"=>"톤카트 화염 누적","GraviticSlash"=>"래스 치명타 누적","SlamStatus"=>"프라이드 상태 확률","ProtonSnap"=>"프로톤 스냅","Might"=>"엑소디아 마이트",_=>key};
                buffNames[key]=label;Log("buff",label+" 발동 · "+duration+"초"+(cap>1?$" · {buffs[key].Value:0}중첩":""));
            }
            void ApplyTimedPerks(string kind)
            {
                foreach(var perk in timedPerks.Where(p=>p.Trigger==kind))
                {Buff(perk.Condition,perk.Seconds);Log("incarnonPerk",perk.Label+" · "+perk.Seconds+"초");}
            }
            void StackPerk(string kind)
            {
                if(!timed||!stackPerks.Any(p=>p.Trigger==kind))return;
                incarnonStacks.Trigger(kind,time);
                foreach(var p in stackPerks.Where(p=>p.Trigger==kind))Log("incarnonStack",$"{IncarnonStackTimers.Label(p.Condition)} · {p.Duration}초 · 최대{p.Cap}",incarnonStacks.Count(p.Condition,time));
                if(kind=="ColdKill")UpdateInitialCombo();
            }
            void ReceiveJade(string kind)
            {
                var merged=jade.Receive(time,kind);
                Log("jadeBuff",merged?"제이드 앙상블 · 범용 상태 지속+60% · 60초":kind switch {"HarmonicResonance"=>"하모닉 레조넌스 · 범용 상태 지속+30% · 30초","EvensongDevotion"=>"이븐송 디보션 수신 · 30초",_=>"칸타레 코랄 수신 · 30초"},jade.StatusDurationBonus(time));
            }
            void Trigger(string kind,double value=1,DamageType? element=null,double? postureRecovery=null,int[]? recentlyAware=null)
            {
                if(kind=="CobraNullifierBubble")
                {
                    var target=enemies[(int)value-1];if(target.Pools.Health>0&&target.FinisherExposure.Apply(time,2,true,target.Pools.Overguard,target.Conditions.GetValueOrDefault("ControlImmune")>0))Log("cobraSleep",$"대상{target.Index+1} · 널리파이어 보호막 경유 · 사거리 무관2초 수면",2);return;
                }
                if(kind=="ExaltedSlideBlind")
                {
                    exaltedLife!.Advance(time);if(!exaltedLife.CanAttack)throw new ArgumentException("익절티드 블레이드가 활성화되어야 슬라이드 실명을 시전할 수 있습니다.");
                    foreach(var target in enemies.Where(e=>e.Pools.Health>0&&ExaltedBlind.Affects(CombatScene.PlayerDistance(build,e.Position),e.Pools.Overguard,CombatScene.HasSight(settings,-1,e.Index),recentlyAware?.Contains(e.Index+1)==true,build.Melee!)))
                    {
                        target.BladeBlindEnd=Math.Max(target.BladeBlindEnd,time+ExaltedBlind.Duration(build.Melee!));
                        Log("bladeBlind",$"대상{target.Index+1} · 슬라이드 실명 · 마무리 노출 없음",ExaltedBlind.Duration(build.Melee!));
                    }
                    energyChannel?.Spend(value);Log("bladeBlindEnergy","슬라이드 실명 · 입력한 실제 에너지 비용",value);
                }
                if(kind=="WarframeMeleeTransfer"){var active=magus!.Transfer(time);Log("magusAggress",active?"마구스 어그레스 · 근접 전이 · 4공격 충전":"마구스 어그레스 · 재사용 대기 중",magus.Charges);}
                if(kind is "BlockedDamage" or "BlockedHit"&&glaiveFlights.Any(f=>time>=f.Launch&&time<f.Returned))throw new ArgumentException("글레이브 비행 중에는 근접 방어를 할 수 없습니다.");
                if(kind is "HeavyAnimationStart" or "HeavyAnimationEnd")
                {conditions["HeavyAnimation"]=kind=="HeavyAnimationStart"?1:0;Log("heavyAnimation",kind=="HeavyAnimationStart"?"실제 강공격 동작 시작 · 비행 중 성분 판정":"실제 강공격 동작 종료");}
                if(kind=="AbilityEnergySpent")
                {energyChannel?.Spend(value);Log("energyChannelCharge","에너지 채널 · 실제 시전 비용 저장",energyChannel?.StoredEnergy??0);Trigger("AbilityCast");}
                if(kind is "EnergyOrbPickup" or "UniversalOrbPickup")
                {
                    var chance=WarframeStatusArcanes.EruptionChance(eruptionSlot!.Rank);var activated=chance>=1||random.NextDouble()<chance;
                    Log("eruptionPickup",kind=="EnergyOrbPickup"?"에너지 오브 실제 습득 · 이럽션 추첨":"범용 오브 실제 습득 · 이럽션 추첨",activated?1:0);
                    if(activated)foreach(var target in enemies.Where(e=>e.Pools.Health>0&&e.Pools.Overguard==0&&e.FrozenEnd<=time&&!(mode==EncounterMode.Primer&&e.Initial.Melee!.Statuses?.GetValueOrDefault(DamageType.Cold)>=10)&&!(!timed&&e.Initial.Arcanes?.Frozen==true)&&CombatScene.PlayerDistance(build,e.Position)<=WarframeStatusArcanes.EruptionRadius(eruptionSlot.Rank)).ToArray())AtEnemy(target,()=>{
                        if(timed&&postureRecovery is null&&(effects.GetValueOrDefault("ConditionOverload")>0||healingPerStatus>0))throw new CombatRuleUnavailableException("이럽션 넉다운의 적별 회복 시각은 위키 미기재입니다. 컨오/힐링 리턴을 포함한 시간 계산에는 해당 오브 습득 행동의 사용자 지정 회복시간을 입력하세요.");
                        enemy.EruptionEnd=Math.Max(enemy.EruptionEnd,time+(postureRecovery??0));
                        Log("eruptionKnockdown",$"대상{enemy.Index+1} · 이럽션 넉다운 · "+(postureRecovery is {} d?$"사용자 지정{d:0.###}초":"회복시간 원문 미기재"));
                    });
                }
                if(rubble is not null)
                {if(kind=="RubbleNullifierEnter")rubble.SetNullified(time,true);if(kind=="RubbleNullifierExit")rubble.SetNullified(time,false);if(kind=="RubbleHostMigration")rubble.HostMigration(time);}
                if(mirrors is not null)
                {
                    if(kind=="MirrorsCast")Log("mirrorsCast",mirrors.Cast(time)?"홀 오브 미러즈 소환":"활성 분신이 있어 재시전 불가");
                    if(kind=="MirrorsEnd"){mirrors.End();Log("mirrorsEnd","분신 전부 해제 · 증폭 초기화");}
                    if(kind=="MirrorNullified"){if(value!=Math.Floor(value))throw new ArgumentException("무효화할 분신 번호는 정수입니다.");mirrors.Remove((int)value);Log("mirrorNullified",$"분신{value:0} 무효화");}
                    if(kind is "PrimaryKill" or "MeleeKill" or "HeavyMeleeKill" or "SlideMeleeKill" or "FinisherKill" or "GroundFinisherKill" or "ParazonKill")mirrors.Kill(time);
                }
                if(kind.StartsWith("JetStream",StringComparison.Ordinal))Log("jetStream",kind=="JetStreamEnter"?"제트 스트림 범위 진입":kind=="JetStreamLeave"?"제트 스트림 범위 이탈 · 3초 유지":"제트 스트림 즉시 해제");
                if(metronome is not null)
                {
                    if(kind is "OperaBuff" or "ForteBuff"){metronome.Receive(time,kind=="OperaBuff");Log("metronome",kind=="OperaBuff"?"오페라 획득/갱신 · 멀티샷":"포르테 획득/갱신 · 기본 피해 가산");}
                    if(kind is "MetronomeBuffsLost" or "AbilityNullified" or "FallOutOfBounds"){metronome.Clear();Log("metronomeEnd","오페라·포르테 해제");}
                }
                if(warcry is not null)
                {
                    if(kind=="WarcryCast") {var cast=warcry.Cast(time);Log("warcryCast","워크라이 시전/재시전 · 지속시간 갱신",warcry.Remaining(time));if(cast&&build.Melee.ExternalEquipment!.Warcry!.SelfCast)Trigger("AbilityCast");}
                    if(kind is "AbilityNullified" or "FallOutOfBounds" or "WarcryCasterDispel"){warcry.Lose(kind=="WarcryCasterDispel");Log("warcryLost","워크라이 해제",0);}
                    if(kind=="WarcryReenter")Log("warcryReenter",warcry.Reenter(time)?"시전자 범위 재진입 · 워크라이 다시 받음":"유효한 시전자 버프 없음",warcry.Remaining(time));
                    if(kind=="WarcryCasterMeleeKill")ExtendWarcry(true);
                    if(kind is "MeleeKill" or "HeavyMeleeKill" or "SlideMeleeKill" or "FinisherKill" or "GroundFinisherKill")ExtendWarcry(build.Melee.ExternalEquipment!.Warcry!.SelfCast);
                }
                if(kind is "ExaltedActivate" or "ExaltedDeactivate" or "OperatorOut" or "OperatorReturn" or "AbilityNullified" or "FallOutOfBounds")
                {
                    if(kind=="ExaltedActivate")
                    {
                        var activated=exaltedLife!.Activate(time);Log("exaltedActivation",activated?"익절티드 소환 · 새 지속시간 시작":"익절티드 소환 불가 · 활성/재사용 대기/전이 상태",activated?1:0);
                        if(activated)Trigger("AbilityCast");
                    }
                    else if(kind is "OperatorOut" or "OperatorReturn")
                    {exaltedLife!.Transfer(time,kind=="OperatorOut");Log("exaltedTransfer",kind=="OperatorOut"?"오퍼레이터 전이 · 익절티드 공격 중단":"워프레임 복귀 · 미시작 재사용 대기 시작");}
                    else
                    {
                        exaltedLife?.Deactivate(time);primal?.Reset(time);
                        if(kind is "AbilityNullified" or "FallOutOfBounds")accumulating?.Reset(time);
                        Log("exaltedDeactivation",kind=="ExaltedDeactivate"?"익절티드 해제 · 프라이멀 레이지 초기화":kind=="AbilityNullified"?"어빌리티 무효화 · 소환/개조 초기화":"낙사 복귀 · 소환/개조 초기화");
                    }
                    if(exaltedLife is not null)Log("exaltedCooldown",exaltedLife.WaitingForReturn?"인레이지드 · 복귀할 때 재사용 대기 시작":"익절티드 · 남은 재사용 대기",exaltedLife.WaitingForReturn?0:Math.Max(0,exaltedLife.ReadyAt-time));
                }
                if(kind is "PrimaryKill" or "ParazonKill"&&primal is not null&&exaltedLife?.Active==true)primal.Gain(time,new[]{8d,10,12,15}[augment!.Rank]*build.Melee.AbilityStrength);
                if(kind is "BlockedElement" or "ShieldingElement")
                {
                    var accepted=absorbedElements!.Absorb(element!.Value,(int)value);absorbedElements.WriteTo(conditions);
                    Log("absorbedElement",(kind=="BlockedElement"?"직접 막기":"실딩 전달")+" · "+Labels.Damage(element.Value)+" · 새 충전",accepted);
                    if(kind=="BlockedElement")Trigger("BlockedHit",value);
                }
                if(kind=="AbilityRadiationInfliction")
                {
                    var source=Evaluate(steps[hit%steps.Length]);
                    if(enemy.Pools.Health>0){enemy.Fallout?.Apply((int)value);for(var n=0;n<Math.Min(10,value);n++)AddProc(DamageType.Radiation,source,1,tickOverride:0,basisOverride:0,durationOverride:12*Math.Max(0,1+(build.Melee.Conditions?.GetValueOrDefault("UniversalStatusDuration")??0)/100+jade.StatusDurationBonus(time)),weaponSource:false);}
                    Log("abilityRadiation",$"대상{enemy.Index+1} · 외부 어빌리티 방사능",value);
                }
                if(kind=="PlayerOverguardGain")
                {
                    var waves=truculence!.Gain(value);Log("truculenceGain","실제 오버가드 획득 · 다음3000까지 누적",truculence.Progress);
                    if(waves>0)
                    {
                        Log("truculenceWave","트루큘런스 · 바이러스 파동 · 같은 시각 반복은10중첩 상한",waves);
                        foreach(var target in enemies.Where(e=>e.Pools.Health>0&&CombatScene.PlayerDistance(build,e.Position)<=5*(truculenceSlot!.Rank+1)&&CombatScene.HasSight(settings,-1,e.Index)).ToArray())AtEnemy(target,()=>{
                            var source=Evaluate(steps[hit%steps.Length]);
                            var duration=6*Math.Max(0,1+(build.Melee.Conditions?.GetValueOrDefault("UniversalStatusDuration")??0)/100+jade.StatusDurationBonus(time));
                            for(var n=0;n<10;n++)AddProc(DamageType.Viral,source,1,tickOverride:0,basisOverride:0,durationOverride:duration,weaponSource:false);
                            Log("truculenceHit",$"대상{enemy.Index+1} · 바이러스10중첩",10);
                        });
                    }
                }
                if(kind=="ExaltedAbilityCast")
                {
                    if(cloneCastSpeed is not null){var castInput=Input(steps[hit%steps.Length]);castInput.Melee!.Conditions!.Remove("ClonesCastSpeed");cloneCastSpeed=MeleeEngine.CalculateAttack(catalog,castInput,useInitialComboFloor:false).AttackSpeed;}
                    var continued=abilityCasts!.Cast(time);if(continued)GainCombo(RollCombo(20*Evaluate(steps[hit%steps.Length]).Melee!.ComboChance),"어빌리티 재시전 · 기본20 콤보");
                    Trigger("AbilityCast");Log("abilityCombo",continued?"간접 익절티드 · 연속 시전":"간접 익절티드 · 첫 시전",abilityCasts.Sequence);
                }
                if(kind=="ValkyrFatalHit")
                {if(valkyr is null)throw new ArgumentException("발키르 고유 치명상 사건입니다.");var prevented=valkyr.PreventDeath(time);Log("valkyrDeath",prevented?"분노150 이상 · 전부 소모 · 체력100% 회복 · 5초 무적":"분노 부족 · 죽음 방지 미발동");}
                if(kind=="VinquibusHeadshot"){vinquibus!.Headshots((int)value);Log("vinquibusPrecision","빈퀴부스 · 주무기 헤드샷 충전",vinquibus.Precision);}
                if(kind=="VinquibusPrimaryShot"){var efficient=vinquibus!.PrimaryShot();Log("vinquibusFerocity",efficient?"빈퀴부스 · 탄약 효율100%로 발사 · 충전 소모":"빈퀴부스 · 일반 탄약 발사",vinquibus.Ferocity);}
                if(kind is "ResidualEnter" or "ResidualTick" or "ResidualLeave")
                {
                    var type=element!.Value;
                    if(kind=="ResidualEnter")residual!.Enter(type,time);else if(kind=="ResidualLeave")residual!.Leave(type,time);else residual!.Tick(type,time,(int)value);
                    Log("demulcent","시오럼 디멀센트 · "+Labels.Damage(type)+(kind=="ResidualEnter"?" 영역 진입":kind=="ResidualLeave"?" 마지막 영역 이탈 · 20초 잔여":" 영역 실제 틱 · 중첩 획득"),residual!.Count(type,time));
                }
                if(kind is "GlaiveChainThrow" or "GlaiveNewThrow")
                {conditions["ConsecutiveThrows"]=kind=="GlaiveNewThrow"?0:Math.Min(3,conditions.GetValueOrDefault("ConsecutiveThrows")+1);Log("powerThrow",kind=="GlaiveNewThrow"?"새 투척 · 파워 스로우 연속 보너스 해제":"회수 전 입력한 연속 투척 · 파워 스로우",conditions["ConsecutiveThrows"]);}
                if(kind=="AbilityHeatInfliction"&&hasHotShot)Buff("HotShotStacks",10,value,50);
                if(kind=="HeavyAttackMiss")
                {
                    var missed=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Heavy Attack"}},null,1,false,false);
                    var actor=Input(missed);var payload=MeleeEngine.Calculate(catalog,actor);HeavyPassive(actor.Scenario.Combo);
                    var spent=comboCounter.SpendHeavy(time,payload.Melee!.HeavyEfficiency,actor.Melee!.Tennokai);tennokaiContactUsed|=actor.Melee.Tennokai;
                    Log("heavyMiss","허공 강공격 · 피해/명중 효과 없이 콤보 소모",spent);
                    if(form?.TryActivate(time,(int)actor.Scenario.Combo,"Heavy Attack")==true)Log("incarnonStart",$"허공 강공격으로 인카논 발동 · {form.Duration:0}초",form.Duration);
                    if(magus?.Consume(build.WeaponId,time)==true)Log("magusCharge","마구스 어그레스 · 허공 강공격 입력 · 남은 공격",magus.Charges);
                    if(absorbedElements?.Total>0){Log("absorbedElementSpent","허공 강공격 입력에 원소 충전 소모",absorbedElements.Consume());absorbedElements.WriteTo(conditions);}
                    if(profile.Passives.Any(p=>p.Key=="BlockCharges"))conditions["BlockCharges"]=0;
                    conditions["FirstQuickMelee"]=0;
                }
                if(kind=="TennokaiHeavyInput")
                {var used=tennokai!.Begin(time);if(used)tennokaiContactUsed=false;Log("tennokaiUse",used?"기회 사용 · 다음 강공격 텐노카이":"기회 없음 · 일반 강공격");}
                if(kind is "TennokaiFinish" or "TennokaiCancel")
                {
                    var failedBefore=tennokai!.Failures;var raw=tennokai.Finish(time,kind=="TennokaiCancel",conditions.GetValueOrDefault("StatusImmune")>0);
                    Log("tennokaiEnd",kind=="TennokaiCancel"?"텐노카이 즉시 구르기 취소 · 저주 없음":"텐노카이 동작 종료");
                    if(tennokai.Failures>failedBefore){var lost=comboCounter.Reset(time);Log("tennokaiFailure","트루스 실패 · 효율 무시 콤보 초기화",lost);Log("tennokaiCurse","자기 화염 · 6초 총 원본 피해 · 대상 피해에서 제외",raw);}
                }
                if(kind is "EquipMelee" or "EquipPrimary" or "EquipSecondary")
                {
                    conditions["Equipped"]=kind=="EquipMelee"?1:0;
                    conditions["FirstQuickMelee"]=kind=="EquipPrimary"?1:0;
                    if(effects.GetValueOrDefault("HolsterPause")>0)comboCounter.SetPaused(time,kind!="EquipMelee");
                    Log("equip",kind=="EquipMelee"?"근접무기 완전 장착":kind=="EquipPrimary"?"주무기 장착 · 다음 첫 근접 입력":"보조무기 장착");
                }
                if(kind=="BlockedDamage")
                {
                    if(storedFraction>0){conditions["BlockedDamage"]=conditions.GetValueOrDefault("BlockedDamage")+value;Log("storedDamage","어벤징 트루스 · 실제 막은 피해 흡수",conditions["BlockedDamage"]*storedFraction);}
                    Trigger("BlockedHit");
                }
                if(kind=="SoulSwarmSlideInput")
                {
                    var launched=souls!.Release(time);Log("soulRelease","소울 스웜 · 슬라이드 입력에 전량 발사",launched);
                }
                if(kind=="SoulSwarmImpact")
                {
                    var recipient=enemies[(int)value-1];souls!.Impact(time,CombatScene.PlayerDistance(build,recipient.Position));
                    if(recipient.Pools.Health<=0)throw new CombatRuleUnavailableException("소울 스웜의 입력한 대상이 명중 전에 죽었습니다. 재유도 대상·시간 규칙이 위키에 없어 다른 적에게 임의 배정하지 않습니다.");
                    var groupState=(groupConnected,groupUsedVendetta,groupFinisherForm);
                    AtEnemy(recipient,()=>{
                        var shot=new Step(build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack="Soul Swarm Projectile",Tennokai=false,Distance=0,TargetIndex=1}},null,1,false,false);
                        Strike(shot);Log("soulImpact",$"소울 스웜 · 대상{recipient.Index+1} 명중 · 생존 시 기절/마무리 노출",1);
                    });
                    (groupConnected,groupUsedVendetta,groupFinisherForm)=groupState;
                }
                if(IncarnonFormState.IsActivationInput(kind))
                {
                    AdvanceForm();
                    var attack=kind=="IncarnonSlamInput"?"Slam Attack":kind=="IncarnonHeavySlamInput"?"Heavy Slam Attack":"Heavy Attack";
                    if(form?.TryActivate(time,(int)value,attack)==true)Log("incarnonStart",$"인카논 발동 · {form.Duration:0}초",form.Duration);
                    else Log("incarnonUnchanged","인카논 유지 · 이미 변신 중이거나 발동 콤보/동작 조건 미충족");
                }
                if(kind is "FinisherHit" or "FinisherKill" or "GroundFinisherKill")form?.Finisher(time);
                if(JadeWeaponBuffs.IsReceipt(kind))ReceiveJade(kind);
                ApplyTimedPerks(kind);
                if(kind is "FinisherHit" or "FinisherKill" or "GroundFinisherKill")ApplyTimedPerks("Finisher");
                player?.Advance(time);
                if(kind=="AbilityCast")brave?.Cast(time);
                if(kind is "ChannelStart" or "ChannelStop")
                {var active=kind=="ChannelStart";brave?.Channel(time,active);conditions["Channeling"]=active?1:0;Log("channel",active?"지속형 어빌리티 유지 시작":"지속형 어빌리티 유지 종료");}
                if(kind=="PrimaryKill"&&gunAndBlade is not null)
                {primaryKills=Math.Min(gunAndBlade.Cap,primaryKills+1);UpdateInitialCombo();Log("gunAndBlade","건 앤 블레이드 · 영구 초기 콤보",primaryKills);}
                if(kind is "FinisherKill" or "GroundFinisherKill" or "ParazonKill")RegisterFinisherKill(kind=="ParazonKill"?"Parazon":kind=="GroundFinisherKill"?"Ground":"Finisher");
                if(kind is "FinisherKill" or "GroundFinisherKill")RegisterMeleeKill(false,1,finisherKill:true);
                if(kind is "MeleeKill" or "HeavyMeleeKill" or "MeleeAssist" or "SlideMeleeKill")
                {
                    Log("externalKill",kind=="MeleeAssist"?"현재 근접무기 · 다른 적 처치 도움":kind=="HeavyMeleeKill"?$"현재 근접무기 · 다른 적 강공격 처치 · 소모 전 {(int)value}×":"현재 근접무기 · 다른 적 처치");
                    RegisterMeleeKill(kind=="HeavyMeleeKill",kind=="HeavyMeleeKill"?(int)value:1,kind=="MeleeAssist");
                }
                if(kind=="WallLatchTwoSeconds"&&proton is not null)Buff("ProtonSnap",5*(proton.Rank+1));
                var shieldDamaged=false;var shieldsBroken=false;
                if(kind=="IncomingHit")player?.IncomingHit();
                if(kind=="PlayerMagnetic")player?.Magnetic();
                if(kind=="PlayerShieldDamage"&&player is not null)
                {
                    var dealt=player.Damage(value);shieldDamaged=dealt>0;shieldsBroken=shieldDamaged&&player.Shields+player.Overshields==0;
                    Log("playerShieldDamage","워프레임 실드 피격 · 방어 적용 후 입력 피해",dealt);
                }
                if(kind=="PlayerShieldBreak")
                {shieldDamaged=player?.Shields+player?.Overshields>0;shieldsBroken=player is null||shieldDamaged;player?.Break();if(player is not null)Log("playerShield","워프레임 실드·오버실드 파괴",0);}
                if(shieldsBroken)ApplyTimedPerks("ShieldBroken");
                if(kind is "BlockedHit" or "AmmoPickup" or "GroundSlide")
                {
                    var calculation=Evaluate(steps[hit%steps.Length]);var details=calculation.Melee!;
                    if(kind=="BlockedHit")
                    {
                        for(var n=0;n<value;n++)GainCombo(RollCombo(details.ComboChance+details.Effects.GetValueOrDefault("BlockingComboChance")),"막기 · 콤보 획득");
                        if(profile.Passives.Any(p=>p.Key=="BlockCharges"))
                        {conditions["BlockCharges"]=Math.Min(4,conditions.GetValueOrDefault("BlockCharges")+value);Log("blockCharge","검과 방패 · 막기 충전",conditions["BlockCharges"]);}
                    }
                    else if(kind=="AmmoPickup")GainCombo(value*details.Effects.GetValueOrDefault("AmmoCombo"),"탄약 습득 · 진화 콤보");
                    // Each input is ONE completed continuous grounded slide,
                    // with this melee weapon held. Distances from interrupted
                    // slides are deliberately not pooled into another 10 m.
                    else GainCombo(Math.Floor(value/10)*details.Effects.GetValueOrDefault("SlideDistanceCombo"),"연속 지상 슬라이드 · 진화 콤보");
                }
                foreach(var slot in new[]{arcane.Melee,arcane.Warframe1,arcane.Warframe2}.Where(s=>s?.Active==true).Cast<ArcaneSlot>())
                {
                    var code=MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code;var rank=slot.Rank+1;
                    switch(code,kind)
                    {
                        case ("Guardian",_) when (kind=="IncomingHit"||shieldDamaged)&&(!buffs.TryGetValue(code,out var guardian)||guardian.End<=time)&&random.NextDouble()<.15:Buff(code,20);break;
                        case ("Aegis",_) when shieldDamaged&&player?.AegisActive==false&&random.NextDouble()<.03:
                            if(player.ActivateAegis(slot.Rank))Log("aegis","아케인 이지스 · 재생12초 · 지속 중 갱신 없음");break;
                        case ("Barrier",_) when shieldDamaged&&time>=barrierReady&&random.NextDouble()<.01*rank:
                            barrierReady=time+rank;Log("barrier","아케인 배리어 · 기본 실드 회복 · "+rank+"초 재사용",player!.Restore(player.Maximum,false));break;
                        case ("Tanker","EquipArchgun") when !buffs.TryGetValue(code,out var tanker)||tanker.End<=time:Buff(code,60);break;
                        case ("Fury","Critical") when random.NextDouble()<.6:Buff(code,18);break;
                        case ("Strike","Hit") when random.NextDouble()<.15:Buff(code,18);break;
                        case ("Avenger",_) when (kind=="IncomingHit"||shieldDamaged)&&random.NextDouble()<.21:Buff(code,12);break;
                        case ("BladeCharger","PrimaryKill") when random.NextDouble()<.3:Buff(code,12);break;
                        case ("Exposure","AbilityCast"):exposure=Math.Min(24,exposure+1);exposureEnd=time+25;Buff(code,25);break;
                        case ("Arachne","WallLatch") when !buffs.TryGetValue(code,out var arachne)||arachne.End<=time:Buff(code,30);break;
                        case ("Assimilation",_) when shieldsBroken:Buff(code,20);break;
                        case ("Careen","RollThroughTarget") when time>=careenReady:
                            careenReady=time+2;
                            var source=Evaluate(steps[hit%steps.Length]);
                            var duration=6*(1+(source.Melee!.Setup.Conditions?.GetValueOrDefault("UniversalStatusDuration")??0)/100);
                            for(var n=0;n<5+slot.Rank;n++)AddProc(DamageType.Cold,source,1,durationOverride:duration,weaponSource:false);
                            Log("careen",$"밀리 커린 · 대상 통과 · 냉기 {5+slot.Rank}중첩 · 재사용 2초");
                            break;
                    }
                }
            }
            WarcrySetup? CurrentWarcry()
            {
                var current=warcry?.At(time);
                return current is not null&&exalted?.Family==ExaltedFamily.Talons?current with {HysteriaActive=exaltedLife?.Active==true}:current;
            }
            void ExtendWarcry(bool casterKill,bool disc=false)
            {
                if(warcry?.Kill(time,casterKill,disc) is >0 and var added)Log("eternalWar",$"이터널 워 · +{added:0.###}초 · 잔여 {warcry.Remaining(time):0.###}초",added);
            }
            void RegisterMeleeKill(bool heavy,int combo,bool assist=false,bool capacitorEligible=true,bool finisherKill=false)
            {
                valkyr?.Gain(time,finisherKill?27:12);
                if(!assist&&primal is not null&&exaltedLife?.Active==true)primal.Gain(time,new[]{8d,10,12,15}[augment!.Rank]*build.Melee.AbilityStrength);
                if(!assist)
                {
                    if(vinquibus is not null){vinquibus.MeleeKill();Log("vinquibusFerocity","빈퀴부스 · 근접 처치로4충전",vinquibus.Ferocity);}
                    if(souls is not null){souls.Kill(time);Log("soulCharge","소울 스웜 · 처치 충전 · 120초 갱신",souls.Charges);}
                    StackPerk("Kill");if(contactAirborne??conditions.GetValueOrDefault("Airborne")>0)StackPerk("AirborneKill");
                    if(reaperLevel>0){AdvanceReaper();Buff("Reaper",10);}
                    killBuffs.Kill(time,heavy,Math.Min(12,combo));
                    if(heavy)form?.DirectHeavyKill(time);
                    if(heavy&&sancti is not null)sancti.Heavy(combo);
                    if(heavy&&build.WeaponId==JadeWeaponBuffs.HarmonyId)ReceiveJade("HarmonicResonance");
                    if(fortification is not null){fortification.Kill(time);Log("fortification",$"포티피케이션 · {fortification.Stacks}중첩 · 추가 방어력 {fortification.Bonus:0}",fortification.Bonus);}
                    if(heavy&&brave is not null){brave.HeavyKill(time);Log("brave",$"엑소디아 브레이브 · {brave.Stacks}중첩 · 에너지 {brave.Rate:0.##}/초",brave.Stacks);}
                    if(capacitorEligible&&profile.Passives.Any(p=>p.Key=="SlamCapacitor"))
                    {conditions["SlamCapacitor"]=Math.Min(5,conditions.GetValueOrDefault("SlamCapacitor")+1);Log("capacitor","슬램 캐패시터 · 처치 충전",conditions["SlamCapacitor"]);}
                    if(isPennant&&heavy)Log("pennant",$"페넌트 · 공속 +50% · {2+4*(combo-1)}초 · 독립 중첩",killBuffs.PennantStacks);
                    if(hasBerserker)Log("berserker",$"버서커 퓨리 · {killBuffs.BerserkerStacks}중첩 · 10초 갱신",killBuffs.BerserkerStacks);
                    if(galvMods.Length>0)Log("galvanized",$"갈바나이즈드 · {killBuffs.GalvanizedStacks}중첩 · 20초 갱신",killBuffs.GalvanizedStacks);
                    if(timed&&galvComboPerStack>0)UpdateInitialCombo();
                }
                if(profile.Passives.Any(p=>p.Key=="Vendetta")&&++vendettaKills==10)
                {vendettaKills=0;vendettaAttacks=10;groupUsedVendetta=false;Log("vendetta","라이징 벤데타 · 이후 명중 10회 활성화",10);}
            }
            void HeavyPassive(double startingCombo)
            {
                if(profile.Name=="Sibear")Buff("SibearBuff",4);
                if(build.WeaponId==KeratinosMechanic.WeaponId&&startingCombo>=12)Buff("ExtendedClaws",180);
            }
            void UpdateInitialCombo()=>comboCounter.SetInitial(time,initial.Melee!.InitialCombo+killBuffs.GalvanizedStacks*galvComboPerStack+
                (finisherKills-startFinisherKills)*crescendoLevel+(primaryKills-startPrimaryKills)*(gunAndBlade?.Conditional.GetValueOrDefault("InitialCombo")??0)+(timed&&stackPerks.Any(p=>p.Condition=="ColdKills")?15*incarnonStacks.Count("ColdKills",PlayerTime()):0));
            bool MightActive()=>mightRate>0&&(!timed||buffs.TryGetValue("Might",out var b)&&b.End>time);
            bool RegisterFinisherKill(string kind="Finisher")
            {
                if(kind=="Parazon")valkyr?.Gain(time,27);
                if(finisherRegen>0&&kind!="Parazon")
                {healthRegen.Activate(time,"FinisherHealing",finisherRegen,10);Log("finisherRegen","마스터즈 플러리 · 체력40/초 · 10초");}
                foreach(var a in frameArcanes)
                {
                    if(a.Code=="Trickery"&&random.NextDouble()<.15)Buff("Trickery",5*(a.Slot.Rank+1));
                    if(a.Code=="Ultimatum")Buff("Ultimatum:"+kind,45);
                }
                if(rapidConclusion is not null)
                {rapidStacks=Math.Min(rapidConclusion.Cap,rapidStacks+1);Log("rapidConclusion","래피드 컨클루전 · 임무 누적 마무리 처치",rapidStacks);}
                if(crescendoLevel>0)
                {finisherKills=Math.Min(10000,finisherKills+1);UpdateInitialCombo();Log("crescendo","크레센도 · 영구 초기 콤보",finisherKills*crescendoLevel);}
                var triggered=mightRate>0&&random.NextDouble()<.5;
                if(triggered)Buff("Might",8);
                return triggered;
            }
            double RollCombo(double expected)
            {
                expected=Math.Max(0,expected);
                var fraction=expected%1;
                return Math.Floor(expected)+(fraction>0&&random.NextDouble()<fraction?1:0);
            }
            void GainCombo(double points,string reason)
            {
                var previous=comboCounter.Multiplier;
                var gained=comboCounter.Gain(time,points);
                if(gained>0)Log("combo",reason,gained);
                if(profile.Name=="Tonkkatt"&&comboCounter.Multiplier>previous)Buff("Overheat",15,comboCounter.Multiplier-previous,30);
            }
            void SlashCombo(Calculation c)
            {
                var chance=c.Melee!.Effects.GetValueOrDefault("SlashComboChance");
                if(chance>0&&(chance>=1||random.NextDouble()<chance))GainCombo(RollCombo(c.Melee.ComboChance),"릴렌트리스 컴비네이션 · 베기 상태");
            }
            void Damage(Calculation c,double critical,bool status=false,bool bypass=false,bool weaponSource=true,bool criticalHit=false,bool delayDeathBlast=false,bool suppressOkinaCreation=false)
            {
                if(enemy.Pools.Health<=0)return;
                var before=Math.Max(0,enemy.Pools.Health)+enemy.Pools.Shields+enemy.Pools.Overguard;var og=enemy.Pools.Overguard;var shield=enemy.Pools.Shields;
                var irradiated=Status().GetValueOrDefault(DamageType.Radiation)>0;
                var mightWasActive=MightActive();
                enemy.Pools.Hit(c,critical,time,status,bypass);
                var dealt=before-enemy.Pools.Health-enemy.Pools.Shields-enemy.Pools.Overguard;
                if(dealt>0&&enemy.FinisherExposure.Damage(time))Log("cobraWakeUnknown","수면 중 피해 · 조기 기상 조건 원문 미기재");
                enemy.Pools.Health=Math.Max(0,enemy.Pools.Health);
                var amount=before-enemy.Pools.Health-enemy.Pools.Shields-enemy.Pools.Overguard;
                if(weaponSource&&sancti is {Maximum:>0})
                {
                    var low=status?0:dealt*sancti.Minimum;var high=dealt*sancti.Maximum;sanctiHealing+=low;sanctiHealingUpper+=high;
                    Log("sanctiHealing",status?"생티 마기스타 · 상태 피해 흡혈 여부 미기재":"생티 마기스타 · 직접 피해 흡혈 · 15m 아군 공유",low);
                }
                if(weaponSource&&!status&&nikanaLifeSteal>0){var heal=dealt*nikanaLifeSteal;nikanaHealing+=heal;Log("nikanaHealing","아말감 다이큐 · 직접 피해 흡혈",heal);}
                if(weaponSource&&daggerStrip>0&&dealt>0&&enemy.Armor>0){enemy.RemovedArmor+=daggerStrip;Log("daggerArmor",status?"아말감 아르고나크 · 상태 피해 방어력 감소":"아말감 아르고나크 · 직접 피해 방어력 감소",daggerStrip);}
                if(weaponSource&&!status&&criticalHit&&c.Melee!.Effects.GetValueOrDefault("CriticalLifeSteal")>0)
                {var heal=dealt*c.Melee.Effects["CriticalLifeSteal"];totalCriticalHealing+=heal;Log("criticalHealing","치명타 흡혈 · 회복 상한 적용 전",heal);}
                var retrievingDisc=build.WeaponId==ZenistarDiscState.WeaponId&&c.Melee!.Attack=="Disc Aura"&&zenistar.Retrieving(time);
                if(weaponSource&&!status&&heavyLifeSteal>0&&c.Melee!.Setup.Attack==c.Melee.Attack&&((c.Melee.Policy??MeleeEngine.AttackPolicy(build.WeaponId,c.Melee.Attack)).Heavy||retrievingDisc))
                {heavyHealing+=dealt*heavyLifeSteal;Log("heavyHealing",retrievingDisc?"라이프 스트라이크 · 제니스타 회수 동작 중 오라 흡혈":"라이프 스트라이크 · 직접 강공격 흡혈",dealt*heavyLifeSteal);}
                if(weaponSource&&mightWasActive)
                {mightHealing+=dealt*mightRate;Log("mightHealing",status?"마이트 · 지속 피해 흡혈":"마이트 · 직접 피해 흡혈",dealt*mightRate);}
                if(amount>0&&glass.Remove(enemy.Index))Log("vitricaGlassRemoved",status?"지속 피해로 유리화 해제 · 유리 폭발 없음":"일반 피해로 유리화 해제 · 유리 폭발 없음");
                if(status)enemy.StatusDamage+=amount;else enemy.DirectDamage+=amount;
                if(burningHate&&weaponSource&&amount>0&&conditions.GetValueOrDefault("BurningHateApplied")==0&&Status().GetValueOrDefault(DamageType.Heat)>0)
                {conditions["BurningHateApplied"]=1;enemy.Conditions["BurningHateApplied"]=1;Log("vulnerability","버닝 헤이트 · 이후 새 상태에 취약 적용");}
                if(og>0&&enemy.Pools.Overguard==0)Log("layer","오버가드 파괴");
                if(shield>0&&enemy.Pools.Shields==0)Log("layer","실드 파괴");
                var magnetic=enemy.Procs.Where(p=>p.Type==DamageType.Magnetic&&p.End>time).ToArray();
                if(magnetic.Length>0&&enemy.Pools.Health>0&&((og>0&&enemy.Pools.Overguard==0)||(shield>0&&enemy.Pools.Shields==0)))
                {
                    var source=magnetic[^1].Source;var m=source.Melee!;
                    var pool=(og>0&&enemy.Pools.Overguard==0?enemy.Initial.Melee!.Overguard:0)+(shield>0&&enemy.Pools.Shields==0?enemy.Initial.Melee!.Shields:0);
                    var tick=pool*.03*magnetic.Length/6*Math.Pow(m.StatusDamageMultiplier*m.FactionMultiplier,2)*(1+m.ElementBonuses.GetValueOrDefault(DamageType.Electricity))*(1+c.Melee!.Effects.GetValueOrDefault("StatusVulnerability"));
                    AddProc(DamageType.Electricity,source,1,tick,weaponSource:magnetic[^1].WeaponSource);Log("magnetic","자성 · 방어층 파괴 전기 상태",tick);
                }
                if(enemy.Pools.Health==0)
                {
                    if(rubble is not null&&enemy.Initial.Melee!.Overguard<=0&&(enemy.Initial.Melee.Conditions?.GetValueOrDefault("ControlImmune")??0)==0&&TargetDebuffs.AtTime(enemy.Initial.Melee.Debuffs,time).Any(d=>d.Code=="Petrify"&&!d.Allied))
                    {var byLandslide=weaponSource&&exalted?.Family==ExaltedFamily.Landslide;rubble.Drop(time,enemy.Index+1,byLandslide);Log("rubbleDrop",$"대상{enemy.Index+1} · 석화 처치 러블 · 생성30초 유지 · 습득은 별도",byLandslide?75:50);}

                    if(weaponSource&&okina is not null&&form?.Active(time)==true)
                    {
                        var directDagger=!status&&(suppressOkinaCreation||c.Melee!.Policy?.Code is "OkinaDagger" or "OkinaExplosion");
                        if(okina.Kill(time,directDagger) is {} id)
                        {
                            okinaSources[id]=Input(steps[hit%steps.Length]);Log("okinaCreate",$"오키나 단검{id} 생성 · 활성 {okina.Count}/6",id);
                            if(okinaFlights.Length==0&&enemies.Any(e=>e.Pools.Health>0&&CombatScene.PlayerDistance(build,e.Position)<=OkinaDaggerState.SeekRange))
                                throw new CombatRuleUnavailableException("오키나 단검의 궤도 대기시간·자동 대상 선택 순서는 위키 미기재입니다. ‘유령 단검 비행’에 발사/명중 시각과 대상을 지정하면 직격·폭발·상태 전이를 계산합니다. "+OkinaDaggerState.Source);
                        }
                    }
                    if(weaponSource&&DualIchorCloudMechanic.Applies(build.WeaponId)&&(form?.Active(time)??build.Melee!.Incarnon))
                    {
                        var source=Input(steps[hit%steps.Length]);ichorFields.Add(new(time,enemy,source));Log("ichorCloudCreate",$"독 구름{ichorFields.Count} · 대상{enemy.Index+1}의 처치 위치 · 7초/4m",ichorFields.Count);
                        if(cloudContacts.Length==0&&enemies.Any(e=>e.Pools.Health>0&&CombatScene.Distance(enemy.Position,e.Position)<=DualIchorCloudMechanic.Radius))
                            throw new CombatRuleUnavailableException("듀얼 이코르 구름에 맞을 주변 적이 있습니다. 피해·상확·7초 수명은 구현되어 있으나 원문에 최초 피해 시각/틱 간격이 없습니다. ‘독 구름 명중’에 지정한 시각·대상의 시나리오로 계산할 수 있습니다. "+DualIchorCloudMechanic.Source);
                    }
                    shards?.Remove(enemy.Index);
                    enemy.KillTime=time;mirrors?.Kill(time);
                    if(enemy.Fallout is {} fallout&&fallout.DropChance>0)
                    {var dropped=fallout.Drops(random.NextDouble());if(dropped)droppedOrbs++;Log("falloutDrop",$"대상{enemy.Index+1} · 영구 드롭 확률 {fallout.DropChance:P0} · "+(dropped?"범용 오브 생성 · 습득 전":"오브 없음"),dropped?1:0);}Log("kill","처치",enemy.DirectDamage+enemy.StatusDamage);
                    if(profile.Name=="Spinnerex")
                    {
                        var stacks=Status().GetValueOrDefault(DamageType.Toxin);
                        var chance=SpinnerexMechanic.Chance(stacks,enemy.SpinnerLastHit,time);
                        if(chance>0)
                        {
                            var explosion=SpinnerexMechanic.Calculate(catalog,Input(steps[hit%steps.Length]),stacks);
                            // Conditional expectation: this cannot damage the already
                            // dead original target. Do not pollute its DPS or TTK.
                            spinnerActivations+=chance;spinnerDamage+=chance*explosion.TotalDamage;spinnerTick+=chance*explosion.Toxin.DamagePerTick;
                            Log("secondaryOpportunity",$"스피네렉스 · 독성 {stacks}중첩 · 처치 폭발 확률 {chance:P0} · 중심 피해 {explosion.TotalDamage:N1}",chance);
                            var origin=enemy;
                            var recipients=enemies.Where(e=>e.Pools.Health>0&&CombatScene.Distance(origin.Position,e.Position)<=10).ToArray();
                            if(recipients.Any(e=>CombatScene.Distance(origin.Position,e.Position)>1e-9))throw new CombatRuleUnavailableException("스피네렉스 처치 폭발:중심 외10m 이내 대상의 거리 감쇠 곡선이 위키에 미기재입니다. 중심의 직접/강제 독성 피해와 대상별 방어층·기존 표식의 연쇄 처치는 계산됩니다. "+SpinnerexMechanic.Source);
                            if(recipients.Length>0&&(chance>=1||random.NextDouble()<chance))
                            {
                                var bursts=new List<(EnemyRuntime Target,Calculation Hit)>();
                                foreach(var target in recipients)AtEnemy(target,()=>bursts.Add((target,SpinnerexMechanic.Evaluate(catalog,Input(steps[hit%steps.Length])))));
                                foreach(var burst in bursts)AtEnemy(burst.Target,()=>{
                                    Damage(burst.Hit,1,delayDeathBlast:true);Log("spinnerexExplosion",$"스피네렉스 · 대상{origin.Index+1} 처치 폭발 · 중심",burst.Hit.Damage.Values.Sum());
                                    AddProc(DamageType.Toxin,burst.Hit,1);SpreadInfluence(DamageType.Toxin,burst.Hit,1);
                                    if(enemy.Pools.Health==0)DetonateBlast(false);
                                });
                            }
                        }
                    }
                    if(weaponSource&&exalted?.Pseudo!=true)
                        ExtendWarcry(build.Melee.ExternalEquipment?.Warcry?.SelfCast==true,build.WeaponId==ZenistarDiscState.WeaponId&&c.Melee!.Attack.StartsWith("Disc",StringComparison.Ordinal));
                    if(weaponSource&&!status)
                    {
                        if(c.Melee!.Setup.Tennokai&&tennokai?.Kill(time,TennokaiState.RefundEligible(profile,c.Melee.Attack))==true)Log("tennokaiRefund","트루스 처치 · 다음 연쇄 피해 활성 · 기회4초 반환",4);
                        var policy=(c.Melee!.Policy??MeleeEngine.AttackPolicy(build.WeaponId,c.Melee!.Attack));
                        if(policy.Heavy||IncarnonEngine.ForWeapon(build.WeaponId)?.Code=="Destreza"&&c.Melee.Attack=="Spectral Rapier")ApplyTimedPerks("HeavyMeleeKill");
                        if(c.Melee.Attack=="Slide Attack"||steps[hit%steps.Length].Slide)ApplyTimedPerks("SlideMeleeKill");
                    }
                    if(weaponSource)RegisterMeleeKill(!status&&c.Melee!.Setup.Attack==c.Melee.Attack&&(c.Melee.Policy??MeleeEngine.AttackPolicy(build.WeaponId,c.Melee.Attack)).Heavy,(int)c.Melee!.EffectiveCombo,capacitorEligible:status||!c.Melee!.Attack.Contains("Slam",StringComparison.Ordinal),finisherKill:!status&&(c.Melee.Setup.Finisher is not null||exalted?.Family==ExaltedFamily.Clones));
                    if(weaponSource&&toxinRegen>0&&Status().GetValueOrDefault(DamageType.Toxin)>=3)
                    {healthRegen.Activate(time,"ToxinKillHealing",toxinRegen,9);Log("toxinKillRegen","포이즌 패러사이트 · 독성3중첩 이상 처치 · 체력33/초 · 9초");}
                    if(weaponSource&&Status().GetValueOrDefault(DamageType.Cold)>=3)StackPerk("ColdKill");
                    if(weaponSource&&!status&&c.Damage.ContainsKey(DamageType.Finisher)&&RegisterFinisherKill(c.Melee!.Setup.Finisher=="Ground"||steps[hit%steps.Length].Finisher?"Ground":"Finisher")&&!mightWasActive)
                    {hostMightHealing+=dealt*mightRate;Log("mightHostHealing","마이트 · 최초 마무리 회복 · 호스트/솔로만",dealt*mightRate);}
                    if(!status&&c.Melee!.Effects.GetValueOrDefault("ShieldRestoreOnKill") is >0 and var restored)
                    {
                        Log("shieldRestore","밀리 어시밀레이션 · 최대 실드의 "+restored.ToString("P0")+" 회복 · 오버실드 생성 없음",restored);
                        if(player is not null)Log("playerShieldRestore","밀리 어시밀레이션 · 실제 실드 회복",player.Restore(player.Maximum*restored,false));
                    }
                    if(!delayDeathBlast)DetonateBlast(false);
                    if(weaponSource)DeathModExplosion(c,!status&&c.Damage.ContainsKey(DamageType.Finisher)&&(c.Melee!.Setup.Finisher=="Ground"||steps[hit%steps.Length].Finisher));
                }
                // Assimilation can seed shields from zero on this killing hit,
                // before Rakta's existing-shield requirement is checked.
                if(profile.Name=="Rakta Dark Dagger"&&player is not null&&weaponSource&&irradiated&&dealt>0)
                {
                    var restored=player.Restore(.05*dealt,true,true);
                    if(restored>0)Log("raktaShield","락타 다크 대거 · 실드 회복",restored);
                }
            }
            void Tick(Proc proc)
            {
                if(proc.Tick<=0||enemy.Pools.Health<=0&&proc.Type!=DamageType.Gas)return;
                var origin=enemy;var gasStacks=Math.Clamp(origin.Procs.Count(p=>p.Type==DamageType.Gas&&p.End>time),1,10);
                var destinations=proc.Type is DamageType.Electricity or DamageType.Gas?enemies.Where(e=>e.Pools.Health>0&&
                    (proc.Type==DamageType.Gas?StatusAreaRules.GasReaches(gasStacks,CombatScene.Distance(origin.Position,e.Position)):CombatScene.Distance(origin.Position,e.Position)<=3)).ToArray():[enemy];
                foreach(var destination in destinations)AtEnemy(destination,()=>{
                    var current=Evaluate(steps[hit%steps.Length]);
                    var amount=proc.Tick*(destination==origin?1:current.Melee!.FactionMultiplier/Math.Max(1e-12,proc.ReceiverFaction));
                    var tick=current with {Damage=new(){[proc.Type==DamageType.Slash?DamageType.Finisher:proc.Type]=amount},Melee=current.Melee! with {ExtraDamage=0}};
                    Damage(tick,1,true,proc.Type==DamageType.Slash,proc.WeaponSource);Log("tick",Labels.Damage(proc.Type)+(destination==origin?" 상태 피해":$" 범위 피해 · 대상{origin.Index+1}에서"),amount);
                    if(proc.Type==DamageType.Slash&&proc.WeaponSource)SlashCombo(current);
                });
            }
            void DetonateBlast(bool hitOrigin,Proc[]? added=null)
            {
                var blast=enemy.Procs.Where(p=>p.Type==DamageType.Blast).Concat(added??[]).ToArray();if(blast.Length==0)return;
                enemy.Procs.RemoveAll(p=>p.Type==DamageType.Blast);var origin=enemy;
                if(hitOrigin)foreach(var p in blast)Tick(p);
                foreach(var destination in enemies.Where(e=>e!=origin&&e.Pools.Health>0&&CombatScene.Distance(origin.Position,e.Position)<=5).ToArray())AtEnemy(destination,()=>{
                    if(blast.Select(p=>p.WeaponSource).Distinct().Count()>1)throw new CombatRuleUnavailableException("폭발 상태의 범위 피해에 무기 귀속/비귀속 상태가 함께 합쳐질 때 처치 귀속 규칙은 원문 미기재입니다.");
                    var c=Evaluate(steps[hit%steps.Length]);var amount=blast.Sum(p=>10*p.Tick*c.Melee!.FactionMultiplier/Math.Max(1e-12,p.ReceiverFaction));
                    Damage(c with {Damage=new(){[DamageType.Blast]=amount},Melee=c.Melee! with {ExtraDamage=0}},1,true,weaponSource:blast[0].WeaponSource);
                    Log("blastArea",$"폭발 · 대상{origin.Index+1}의 {blast.Length}중첩 · 5m 무감쇠",amount);
                });
            }
            void AddProc(DamageType type,Calculation c,double critical,double? tickOverride=null,double? basisOverride=null,double? durationOverride=null,bool weaponSource=true,double? receiverFaction=null)
            {
                if(!DamageTypeRules.HasStatus(type)||enemy.Pools.Health<=0)return;
                if(enemy.Pools.Overguard<=0&&enemy.FinisherExposure.Status(type,time))Log("finisherExposureClear","충격/전기 상태로 방패 마무리 노출 해제");
                var duration=durationOverride??Lifetimes[type]*Math.Max(0,1+c.Melee!.Effects.GetValueOrDefault("StatusDuration")+(type==DamageType.Slash?c.Melee.Effects.GetValueOrDefault("SlashDuration"):0));
                if(type==DamageType.Cold&&enemy.FrozenEnd>time)return;
                var snapshot=c.Melee!.StatusDamage.SingleOrDefault(p=>p.Type==type);
                var meanCritical=MeleeStealth.Mean(c);
                var tick=tickOverride??(snapshot?.DamagePerTick??0)*critical/meanCritical;
                var basis=basisOverride??c.Melee.ModdedBaseDamage*critical*c.Melee.StatusDamageMultiplier*(c.Melee.ForcedProcs.Contains(type.ToString())?c.Melee.ForcedProcDamageMultiplier:1);
                var p=new Proc{Type=type,End=time+duration,Duration=duration,Tick=tick,Basis=basis,Source=c,Element=1+c.Melee.ElementBonuses.GetValueOrDefault(type),Faction=c.Melee.FactionMultiplier,WeaponSource=weaponSource,ReceiverFaction=receiverFaction??c.Melee.FactionMultiplier};
                p.Next=type==DamageType.Blast?time+duration:type is DamageType.Electricity or DamageType.Gas?time:time+1;
                if(type==DamageType.Heat)
                {
                    var first=enemy.Procs.FirstOrDefault(x=>x.Type==type&&x.End>time);
                    if(first is null)
                    {
                        if(enemy.HeatEnd>=0&&time<enemy.HeatEnd+6&&enemy.Armor>0)throw new CombatRuleUnavailableException("화염 종료 후 방어력 회복 중 재점화 순서는 원문에서 확인되지 않았습니다. 이 교전의 정밀 시간 계산을 제공하지 않습니다.");
                        enemy.HeatStart=time;enemy.HeatDuration=duration;
                    }
                    else{enemy.HeatDuration=Math.Max(enemy.HeatDuration,duration);p.Tick=p.Tick/Math.Max(1e-12,p.Element*p.Faction)*first.Element*first.Faction;p.Element=first.Element;p.Faction=first.Faction;p.Next=first.Next;}
                    enemy.HeatEnd=time+enemy.HeatDuration;p.End=enemy.HeatEnd;
                    foreach(var old in enemy.Procs.Where(x=>x.Type==type))old.End=enemy.HeatEnd;
                }
                var cap=type==DamageType.Cold&&enemy.Pools.Overguard>0?4:TargetStatusRules.Maximum(type);
                if(enemy.Procs.Count(x=>x.Type==type)>=cap){var oldest=enemy.Procs.Where(x=>x.Type==type).MinBy(x=>x.End);if(oldest is not null)enemy.Procs.Remove(oldest);}
                if(type==DamageType.Radiation&&weaponSource&&exalted is not null)enemy.Fallout?.Apply();
                enemy.Procs.Add(p);Log("proc",Labels.Damage(type)+" 부여",p.Tick);
                if(weaponSource)StackPerk("Status");
                if(weaponSource&&type==DamageType.Puncture&&piercing is not null){piercing.Proc(time);Log("piercingStature","피어싱 스테처 · 상확+20% · 6초 독립 중첩",piercing.Stacks);}
                if(type==DamageType.Slash&&weaponSource)SlashCombo(c);
                if(type==DamageType.Gas)gasThisHit=true;
                if(type==DamageType.Blast&&enemy.Procs.Count(x=>x.Type==type)>=10)
                {DetonateBlast(true);Log("detonate","폭발 10중첩 조기 폭발");}
                if(type==DamageType.Cold&&enemy.Procs.Count(x=>x.Type==type)==10){enemy.FrozenEnd=time+duration/2;foreach(var x in enemy.Procs.Where(x=>x.Type==type))x.End=enemy.FrozenEnd;Log("freeze","동결");}
                if(weaponSource&&profile.Name=="Lesion"&&(!buffs.TryGetValue("Frenzy",out var frenzy)||frenzy.End<=time))Buff("Frenzy",6);
                
                if(weaponSource&&c.Melee.Effects.GetValueOrDefault("TauOnStatus")>0&&type is not (DamageType.Radiation or DamageType.Tau))AddProc(DamageType.Tau,c,critical);
            }
            Calculation OnCurrentEnemy(Calculation source,string attack)
            {
                var current=Evaluate(steps[hit%steps.Length]);var setup=source.Melee!.Setup with {
                    Attack=attack,Statuses=Status(),Faction=enemy.Initial.Melee!.Faction,Shields=enemy.Pools.Shields,Overguard=enemy.Pools.Overguard,HeatArmorStrip=HeatStrip()};
                return source with {ArmorReduction=current.ArmorReduction,Melee=source.Melee with {Setup=setup,Attack=attack,ExtraDamage=0,IncomingDamageMultiplier=current.Melee!.IncomingDamageMultiplier,Policy=new(attack,Heavy:false,HitCombo:false,HealingReturn:false)}};
            }
            void DeathModExplosion(Calculation cause,bool groundFinisher)
            {
                var origin=enemy;
                foreach(var kind in new[]{"Kill","GroundFinisher"})
                {
                    if(kind=="GroundFinisher"&&!groundFinisher)continue;
                    var flat=effects.GetValueOrDefault(kind+"Explosion");if(flat<=0)continue;
                    var radius=effects.GetValueOrDefault(kind+"ExplosionRadius");
                    var health=origin.Initial.Scenario.Enemy?.Health??10000;
                    var baseDamage=flat+health*effects.GetValueOrDefault(kind+"HealthExplosion");
                    foreach(var destination in enemies.Where(e=>e.Pools.Health>0&&e!=origin&&CombatScene.Distance(origin.Position,e.Position)<=radius&&CombatScene.HasSight(settings,origin.Index,e.Index)).ToArray())AtEnemy(destination,()=>{
                        var current=Evaluate(steps[hit%steps.Length]);var factor=1d;
                        if(kind=="Kill")factor=(1-CombatScene.Distance(origin.Position,destination.Position)/radius)*current.Melee!.FactionMultiplier*(current.Melee.StealthBonus>0?8:1);
                        var burst=OnCurrentEnemy(current,kind=="Kill"?"Vulcan Blitz Explosion":"Entropy Detonation Explosion");
                        burst=burst with {Damage=new(){[DamageType.Blast]=baseDamage*factor}};
                        Damage(burst,1);Log("deathModExplosion",(kind=="Kill"?"벌컨 블리츠":"엔트로피 디토네이션")+$" · 대상{origin.Index+1}의 최대 체력 기준 · 시야 확인",baseDamage*factor);
                    });
                }
            }
            void SpreadInfluence(DamageType type,Calculation source,double critical,bool eligible=true,bool canTrigger=true,bool canSpread=true)
            {
                if(influence is null||!eligible||enemy.Pools.Health<=0)return;
                if(canTrigger&&influence.TryTrigger(time,type,true,true,random.NextDouble))Log("influenceStart","밀리 인플루언스 · 발동 · 갱신 없음",influence.Duration);
                if(!canSpread||!influence.Active(time)||!MeleeInfluenceState.Spreads(type))return;
                var origin=enemy;
                foreach(var destination in enemies.Where(e=>e!=origin&&e.Pools.Health>0&&CombatScene.Distance(origin.Position,e.Position)<=influence.Radius).ToArray())AtEnemy(destination,()=>{
                    var recipient=Evaluate(steps[hit%steps.Length]);var faction=recipient.Melee!.FactionMultiplier;
                    var c=OnCurrentEnemy(source,"Melee Influence Explosion");
                    var amount=source.Damage.GetValueOrDefault(type)*critical*faction;
                    Damage(c with {Damage=new(){[type]=amount}},1,suppressOkinaCreation:source.Melee!.Policy?.Code is "OkinaDagger" or "OkinaExplosion");
                    if(enemy.Pools.Health>0)
                    {
                        var proc=source.Melee!.StatusDamage.SingleOrDefault(p=>p.Type==type);
                        var tick=(proc?.DamagePerTick??0)*critical/MeleeStealth.Mean(source)*faction;
                        AddProc(type,source,critical,tickOverride:tick,receiverFaction:faction);
                    }
                    Log("influenceSpread","밀리 인플루언스 · "+Labels.Damage(type)+$" · 대상{origin.Index+1}에서 전파",amount);
                });
            }
            void ShardStrike(ThalysShardEvent e,Step step,Calculation triggering)
            {
                var host=enemies[e.Target];
                if(e.Impale&&host.Pools.Overguard>0)throw new CombatRuleUnavailableException("탈리스 완전 성장: 오버가드로 꿰뚫기가 차단될 때 가시 제거·재부착 제한의 순서는 원문 미기재입니다.");
                var targets=e.Detonation?enemies.Where(t=>t==host||t.Pools.Health>0&&CombatScene.Distance(host.Position,t.Position)<=1).ToArray():[host];
                foreach(var destination in targets)
                {
                    var distance=CombatScene.Distance(host.Position,destination.Position);
                    if(destination!=host&&distance>0)throw new CombatRuleUnavailableException("탈리스 연쇄 폭발: 다른 적이 약1m 피해 반경 안에 있으나 거리 감쇠율이 위키 미기재입니다. 숙주 피해와 주변 감쇠 피해를 같은 값으로 합산하지 않습니다.");
                    if(destination.Pools.Health<=0)continue;
                    AtEnemy(destination,()=>{
                        var input=Input(step);var component=e.Detonation?ThalysMechanic.Detonation(catalog,input,e.Root):
                            e.Impale?ThalysMechanic.Calculate(catalog,input,triggering.Melee!.Brackets!.MotionMultiplier).Impale!:
                            ThalysMechanic.Calculate(catalog,input,triggering.Melee!.Brackets!.MotionMultiplier).Echo;
                        var tier=Math.Floor(component.CriticalChance)+(random.NextDouble()<component.CriticalChance%1?1:0);var critical=MeleeStealth.AtTier(component,tier);
                        Damage(component,critical,weaponSource:false);
                        if(triggering.Melee!.Effects.GetValueOrDefault("ShardCombo")>0)GainCombo(triggering.Melee.Effects["ShardCombo"],"에코즈 오브 레이지 · 가시 피해 콤보");
                        Log("thalysShard",e.Detonation?e.Root?"탈리스 · 최초 가시 폭발 · 콤보 적용":"탈리스 · 연쇄 가시 폭발 · 콤보 제외":e.Impale?"탈리스 · 완전 성장 · 5초 꿰뚫기":"탈리스 · 다른 대상의 새 가시로 재발동",component.Damage.Values.Sum()*critical);
                        var rolls=(int)Math.Floor(component.StatusChance)+(random.NextDouble()<component.StatusChance%1?1:0);
                        for(var n=0;n<rolls&&enemy.Pools.Health>0;n++)
                        {
                            var choice=random.NextDouble()*component.Damage.Values.Sum();
                            foreach(var (type,amount) in component.Damage){choice-=amount;if(choice<0){AddProc(type,component,critical,weaponSource:false);SpreadInfluence(type,component,critical,e.Detonation);break;}}
                        }
                    });
                }
            }
            bool MirrorFlight(int actor,string key,bool launching,bool payload)
            {
                if(actor==0)return true;
                if(launching){if(mirrors?.Active(actor,time)!=true){Log("mirrorMiss",$"분신{actor} 미활성 · 발사 없음");return false;}mirrorFlights[key]=mirrors.Generation;return true;}
                if(!mirrorFlights.TryGetValue(key,out var generation))return false;
                if(mirrors?.Active(actor,time)!=true||mirrors.Generation!=generation){if(payload)throw new CombatRuleUnavailableException("분신 소멸 뒤 이미 발사된 투사체의 유지 규칙은 위키 미기재입니다. 재시전한 분신으로 이전 투사체를 복원하지 않습니다. "+HallOfMirrorsRules.Source);return false;}
                return true;
            }
            void MirrorStrike(Calculation c,int number,bool shield=false,double? finisherSeconds=null)
            {
                if(!c.Melee!.HitConnects)return;
                if(enemy.Pools.Overguard<=0)enemy.FinisherExposure.EnsureKnown(time);
                if(c.Melee.ExtraDamage>0)throw new CombatRuleUnavailableException("분신의 에너지 채널 저장량 공유/소모는 원문 미기재입니다.");
                var floor=Math.Floor(c.CriticalChance);var tier=floor+(random.NextDouble()<c.CriticalChance-floor?1:0);var critical=MeleeStealth.AtTier(c,tier);var before=enemy.DirectDamage;
                Damage(c,critical,weaponSource:false);Log("hologramHit",$"분신{number} · 능력 피해/처치 귀속 · 콤보 획득 없음",enemy.DirectDamage-before);
                if(c.Melee.ForcedProcs.Any(p=>p is "Knockdown" or "Ragdoll" or "Lifted")&&(effects.GetValueOrDefault("ConditionOverload")>0||c.Melee.Effects.GetValueOrDefault("LiftedDamage")>0))throw new CombatRuleUnavailableException("분신 강제 넉다운/띄우기의 회복 시각이 없어 이후 컨오·띄우기 조건부 피해를 확정할 수 없습니다.");
                if(shield&&enemy.FinisherExposure.Apply(time,finisherSeconds,false,enemy.Pools.Overguard,enemy.Conditions.GetValueOrDefault("ControlImmune")>0))Log("shieldFinisherOpen",$"분신{number} 방패 · 마무리 노출");
                void CloneProc(DamageType type)
                {AddProc(type,c,critical,weaponSource:false);if(c.Melee.Effects.GetValueOrDefault("TauOnStatus")>0&&type is not (DamageType.Radiation or DamageType.Tau))AddProc(DamageType.Tau,c,critical,weaponSource:false);}
                var strips=c.Melee.Effects.GetValueOrDefault("ShatteringImpact");if(strips>0&&c.Damage.GetValueOrDefault(DamageType.Impact)>0)enemy.RemovedArmor+=strips;
                var rolls=(int)Math.Floor(c.StatusChance)+(random.NextDouble()<c.StatusChance%1?1:0);
                for(var i=0;i<rolls&&enemy.Pools.Health>0;i++)
                {
                    var selected=random.NextDouble()*c.Damage.Values.Sum();foreach(var (type,value) in c.Damage){selected-=value;if(selected<0){CloneProc(type);if(type==DamageType.Impact&&random.NextDouble()<c.Melee.Effects.GetValueOrDefault("ImpactToSlash"))CloneProc(DamageType.Slash);break;}}
                }
                foreach(var name in c.Melee.ForcedProcs)if(Enum.TryParse<DamageType>(name,out var type))CloneProc(type);
            }
            void HealReturn(Step step,Calculation c,int count,bool living)
            {
                if(healingPerStatus<=0||!(c.Melee!.Policy??MeleeEngine.AttackPolicy(build.WeaponId,c.Melee!.Attack)).HealingReturn)return;
                var flinch=living&&enemy.Pools.Overguard==0&&HealingReturnRules.GuaranteedFlinch(build,step.Build.Melee!.Attack);
                var range=HealingReturnRules.StatusCount(count,flinch);
                returnHealing+=range.Minimum*healingPerStatus;returnHealingUpper+=range.Maximum*healingPerStatus;
                if(range.Maximum>0)Log("healingReturn",range.Minimum==range.Maximum?$"힐링 리턴 · {count}종 · 접촉별 회복":$"힐링 리턴 · 움찔 중복 미기재 · {range.Minimum}~{range.Maximum}종",range.Minimum*healingPerStatus);
            }
            void AmanataContact(Step step,bool connected,bool duplicate)
            {
                if(amanata?.Contact(swingId,step.Build.Melee!.Attack.Contains("Slam",StringComparison.Ordinal),connected,duplicate,()=>random.Next(1,7))==true)
                    Log("amanataRoll","아마나타 · 30회 누적 · 이전 축복 교체",amanata.Roll);
            }
            string ContagionSourceSignature(Build source,double travel,ContagionPhase phase)
            {
                // Compare player-owned payloads with all recipient state removed.
                // A difference means that snapshot/live-buff semantics would
                // affect the answer; the Wiki does not specify those semantics.
                var neutral=source with {Scenario=source.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},
                    Arcanes=source.Arcanes is {} a?a with {Frozen=false}:null,
                    Melee=source.Melee! with {Statuses=[],Debuffs=[],Faction="Neutral",Shields=0,Overguard=0,HeatArmorStrip=0,
                        Conditions=(source.Melee.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)&&p.Key!="HeavyAnimation").ToDictionary()}};
                var c=ExodiaContagionMechanic.Evaluate(catalog,neutral,travel,phase,useInitialComboFloor:false);
                return System.Text.Json.JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
            }
            string GunshotSourceSignature(Build source,double precision)
            {
                var values=(source.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();values["VinquibusPrecision"]=precision;
                var neutral=source with {Stance=null,Scenario=source.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=source.Arcanes is {} a?a with {Frozen=false}:null,
                    Melee=source.Melee with {Attack="Gunshot Explosion",Tennokai=false,Statuses=[],Debuffs=[],Faction="Neutral",Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=values,Distance=0,TargetIndex=1}};
                var c=MeleeEngine.Calculate(catalog,neutral);
                return System.Text.Json.JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
            }
            void VinquibusAction(WeaponActionEvent e)
            {
                var v=e.Input;var hit=e.Contact;var explosion=e.Kind==2;
                if(!MirrorFlight(v.Hologram,"weapon:"+v.Input,e.Kind==0,e.Kind is 1 or 2))return;
                var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack=explosion?"Gunshot Explosion":"Gunshot",Distance=0,Tennokai=false,TargetIndex=1}},null,1,false,false,Delayed:explosion,WeaponAction:v,WeaponContact:hit,WeaponExplosion:explosion);
                if(e.Kind==0){weaponActionSources[v.Input]=Input(step);vinquibusShots[v.Input]=new();Log("gunshotLaunchPlan",$"사격{v.Input} · 발사{v.Launch:0.###}초 · 폭발{v.Launch+VinquibusGunshot.Delay:0.###}초");return;}
                var state=vinquibusShots[v.Input];var precision=state.Precision??vinquibus!.Precision;
                var source=weaponActionSources[v.Input];source=source with {Melee=source.Melee! with {Conditions=new(source.Melee.Conditions??[]){["VinquibusPrecision"]=precision}}};
                step=step with {Build=source with {Melee=source.Melee! with {Attack=explosion?"Gunshot Explosion":"Gunshot",Distance=0}},PrecisionAtShot=precision};
                var current=Input(step);
                if(WeaponActionFlight.Signature(catalog,source,v,hit,explosion)!=WeaponActionFlight.Signature(catalog,current,v,hit,explosion))throw new CombatRuleUnavailableException("빈퀴부스 입력·명중·폭발 사이 공격자 버프 저장 시점은 미기재입니다. 변한 버프를 자동 저장/갱신하지 않습니다.");
                var center=enemies[hit!.Target-1];groupConnected=false;
                if(!explosion)
                {
                    if(state.Stopped)throw new ArgumentException("빈퀴부스의 앞 접촉에서 탄환이 멈췄습니다. 생존 적/표면을 관통할 수 없습니다.");
                    var alive=center.Pools.Health>0;if(!hit.Surface&&!alive)throw new CombatRuleUnavailableException("빈퀴부스 도착 전에 대상이 이미 사망했습니다. 실제 후속 접촉을 지정하세요.");
                    if(!hit.Surface)AtEnemy(center,()=>{var before=enemy.DirectDamage;if(v.Hologram>0)MirrorStrike(Evaluate(step),v.Hologram);else Strike(step);Log("vinquibusImpact",$"사격{v.Input} · 대상{hit.Target}",enemy.DirectDamage-before);});
                    state.Contact(hit.Target,alive,center.Pools.Health>0,hit.Surface);
                    Log(hit.Surface?"vinquibusSurface":state.KilledContacts>0&&!state.Stopped?"vinquibusPierce":"vinquibusAttach",hit.Surface?"탄환 표면 정지 · 입력한 대상 좌표를 폭발 중심으로 사용":state.Stopped?"탄환 정지 · 발사 시점의0.8초 타이머 유지":"처치 관통 · 뒤 접촉까지 계속 비행");
                }
                else
                {
                    if(!state.Stopped)throw new CombatRuleUnavailableException("빈퀴부스가 첫 적을 처치·관통했습니다. 0.8초 만료 시점의 후속 대상/표면 위치가 필요합니다.");
                    if(!hit.Surface&&center.Pools.Health<=0&&enemies.Any(t=>t.Pools.Health>0))throw new CombatRuleUnavailableException("빈퀴부스 정지/부착 대상이 폭발 전에 사망했습니다. 사망 후 탄환의 위치 처리 규칙은 미기재입니다.");
                    var bursts=new List<(EnemyRuntime Enemy,Step Step,Calculation Hit)>();
                    foreach(var target in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(t.Position,center.Position)<=4+1e-9).ToArray())AtEnemy(target,()=>{var radial=step with {Build=source with {Melee=source.Melee! with {Attack="Gunshot Explosion",Distance=CombatScene.Distance(center.Position,enemy.Position)}}};bursts.Add((enemy,radial,Evaluate(radial)));});
                    foreach(var burst in bursts)AtEnemy(burst.Enemy,()=>{var before=enemy.DirectDamage;if(v.Hologram>0)MirrorStrike(burst.Hit,v.Hologram);else Strike(burst.Step,captured:burst.Hit);Log("vinquibusBlast",$"사격{v.Input} · 발사 후0.8초 폭발 · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                }
                if(groupConnected&&v.Hologram==0)
                {
                    state.Precision??=precision;
                    if(weaponActionConnected.Add(v.Input)){if(animositySlot is not null&&MeleeAnimosityRules.Builds(build.WeaponId,step.Build.Melee!)){animosity=Math.Min(10,animosity+1);Log("animosity","빈퀴부스 사격 입력 명중",animosity);}vinquibus!.FinishMeleeInput(200000+v.Input,true);Log("vinquibusPrecision","빈퀴부스 · 사격 입력 첫 명중에 정밀 충전 한 번 소모",vinquibus.Precision);}
                }
                groupConnected=false;swingId++;
            }
            void GunshotExplosion(DelayedGunshot shot)
            {
                var radius=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Gunshot Explosion").Number("Range");
                if(shot.Center.Pools.Health<=0&&enemies.Any(t=>t.Pools.Health>0&&CombatScene.Distance(shot.Center.Position,t.Position)<=radius))
                    throw new CombatRuleUnavailableException("빈퀴부스 사격은 처치한 적1명을 관통할 수 있습니다. 처치/부착 대상 사망 후 탄환의 실제 후속 충돌 위치가 없어 사망 위치에 폭발을 만들지 않습니다.");
                var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(shot.Center.Position,t.Position)<=radius).ToArray())AtEnemy(recipient,()=>{
                    var step=shot.Step with {Build=shot.Step.Build with {Melee=shot.Step.Build.Melee! with {Distance=CombatScene.Distance(shot.Center.Position,enemy.Position)}}};
                    var current=Input(step);var precision=step.PrecisionAtShot??0;
                    if(GunshotSourceSignature(shot.Source,precision)!=GunshotSourceSignature(current,precision))
                        throw new CombatRuleUnavailableException("빈퀴부스 사격의0.8초 지연 중 공격자 버프가 바뀌었습니다. 지연 폭발의 저장/실시간 갱신 방식은 위키 미기재로 구분합니다.");
                    contacts.Add((enemy,step,Evaluate(step)));
                });
                foreach(var contact in contacts)AtEnemy(contact.Target,()=>{
                    Strike(contact.Step,captured:contact.Hit);Log("gunshotExplosion",$"빈퀴부스 · 0.8초 지연 폭발 · 대상{enemy.Index+1} · 중심 거리{CombatScene.Distance(shot.Center.Position,enemy.Position):0.###}m");
                });
            }
            void ContagionStrike(ContagionEvent e,bool attached)
            {
                var host=enemies[e.Target-1];
                if(e.Phase==ContagionPhase.Contact&&host.Pools.Health<=0)throw new ArgumentException("이미 처치된 대상에게 컨테이전 직격을 지정할 수 없습니다. 다음 살아 있는 대상이나 지형 충돌을 지정하세요.");
                if(attached&&host.Pools.Health<=0)
                {
                    if(enemies.Any(t=>t.Pools.Health>0&&CombatScene.Distance(host.Position,t.Position)<=8))
                        throw new CombatRuleUnavailableException("컨테이전 부착 중 숙주 사망 이후의 폭발 시각/위치는 위키 미기재입니다. 직격 즉사 후 계속 비행하는 규칙과 구분합니다.");
                    return;
                }
                var destinations=e.Phase==ContagionPhase.Explosion?enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(host.Position,t.Position)<=8).ToArray():host.Pools.Health>0?[host]:[];
                if(e.Phase==ContagionPhase.Explosion&&destinations.Any(t=>CombatScene.Distance(host.Position,t.Position)>1e-9))
                    throw new CombatRuleUnavailableException("컨테이전8m 폭발의 중심 밖 거리 감쇠율은 위키 미기재입니다. 중심 피해를 주변 전체에 똑같이 적용하지 않습니다.");
                var contacts=new List<(EnemyRuntime Enemy,Step Step,Calculation Hit)>();
                foreach(var destination in destinations)AtEnemy(destination,()=>{
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,Finisher=null,Distance=0,TargetIndex=1}},null,1,false,false,Contagion:e.Phase,Travel:e.Travel);
                    var input=Input(step);
                    if(attached&&contagionOrigins.TryGetValue(e.Projectile,out var source)&&ContagionSourceSignature(source,e.Travel,e.Phase)!=ContagionSourceSignature(input,e.Travel,e.Phase))
                        throw new CombatRuleUnavailableException("컨테이전 부착 중 공격자 버프/콤보가 바뀌었습니다. 부착 틱·폭발의 최초 저장/실시간 갱신 규칙이 위키 미기재여서 두 값을 임의로 선택하지 않습니다.");
                    if(e.Phase==ContagionPhase.Contact)contagionOrigins[e.Projectile]=input;
                    contacts.Add((enemy,step,Evaluate(step)));
                });
                foreach(var contact in contacts)AtEnemy(contact.Enemy,()=>{
                    Strike(contact.Step,captured:contact.Hit);
                    Log("contagionPhase",$"투사체{e.Projectile} · {ExodiaContagionMechanic.Label(e.Phase)} · 대상{enemy.Index+1}");
                });
                if(e.Phase==ContagionPhase.Contact)
                {
                    contagionFlights.ResolveContact(e,host.Pools.Health>0);
                    Log(host.Pools.Health>0?"contagionAttached":"contagionContinue",host.Pools.Health>0?"컨테이전 부착 · 1~4초 틱·5초 폭발 예약":"컨테이전 직격 즉사 · 비행 계속 · 다음 충돌은 입력 시각 사용",e.Projectile);
                }
            }
            void BounceExplosion(Step cause,EnemyRuntime? center,Build attacker,GlaivePoint? surface=null)
            {
                var centerPosition=surface?.Position??center?.Position;
                var thrown=cause.Build.Melee!.Attack;
                var explosionName=GlaiveBounceMechanic.Explosion(thrown);
                var baseStep=cause with {Hit=null,Co=1,Finisher=false,Slide=false,Delayed=true,BounceThrow=thrown,
                    Build=cause.Build with {Stance=null,Melee=cause.Build.Melee! with {Attack=explosionName,Tennokai=false,Finisher=null,TargetIndex=1,Distance=0}}};
                var radius=GlaiveBounceMechanic.Calculate(catalog,attacker,thrown).Melee!.AreaRadius??0;
                var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                foreach(var target in enemies.Where(e=>e.Pools.Health>0&&CombatScene.Distance(centerPosition,e.Position)<=radius).ToArray())AtEnemy(target,()=>{
                    var step=baseStep with {Build=baseStep.Build with {Melee=baseStep.Build.Melee! with {Distance=CombatScene.Distance(centerPosition,enemy.Position)}}};
                    var receiver=Input(step);
                    var sourceConditions=(attacker.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();
                    foreach(var pair in receiver.Melee!.Conditions!.Where(p=>CombatScene.EnemyConditionKeys.Contains(p.Key)))sourceConditions[pair.Key]=pair.Value;
                    var input=receiver with {Arcanes=attacker.Arcanes! with {Frozen=receiver.Arcanes!.Frozen},Melee=receiver.Melee with {Conditions=sourceConditions}};
                    contacts.Add((enemy,step,GlaiveBounceMechanic.Calculate(catalog,input,thrown,input.Melee.Distance,useInitialComboFloor:false)));
                });
                foreach(var contact in contacts)AtEnemy(contact.Target,()=>{if(cause.Glaive is {Hologram:>0} flight)MirrorStrike(contact.Hit,flight.Hologram);else Strike(contact.Step,captured:contact.Hit);Log("glaiveBounce",$"볼래틸 반사 폭발 · 대상{enemy.Index+1} · 중심 거리{CombatScene.Distance(centerPosition,enemy.Position):0.###}m");});
            }
            void Strike(Step step,bool duplicate=false,Calculation? captured=null)
            {
                // An aerial stance proves the player's state at this contact.
                // Do not leak it into later DOT ticks or external kill inputs:
                // no landing time can be inferred from an untimed stance table.
                var previousAirborne=contactAirborne;
                if(step.Aerial)contactAirborne=true;
                try{StrikeCore(step,duplicate,captured);}finally{contactAirborne=previousAirborne;}
            }
            void StrikeCore(Step step,bool duplicate=false,Calculation? captured=null)
            {
                if(enemy.Pools.Overguard<=0)enemy.FinisherExposure.EnsureKnown(time);
                if(enemy.Pools.Overguard<=0&&enemy.PoolBlindUnknownSince<time&&enemy.PoolBlindEnd<=time&&enemy.Conditions.GetValueOrDefault("Blinded")<=0)
                    throw new CombatRuleUnavailableException("웅덩이의 실명 지속시간이 위키 미기재라 후속 명중의 은신 배수를 확정할 수 없습니다. 체류 구간의 실명 지속시간을 시나리오로 지정하세요.");

                gasThisHit=false;
                if(magus is {Charges:>0}&&step.ZenistarAttack is not null)throw new CombatRuleUnavailableException("제니스타 원반은 마구스 어그레스를 약2초마다 소모한다고만 기재되어 있습니다. 정확한 소모 사건 시점을 확정하지 않습니다. "+MagusAggressState.Source);
                var c=captured??Evaluate(step);if(energyChannel is not null)c=energyChannel.Apply(c);var floor=Math.Floor(c.CriticalChance);var tier=floor+(random.NextDouble()<c.CriticalChance-floor?1:0);var critical=MeleeStealth.AtTier(c,tier);
                if(!c.Melee!.HitConnects){AmanataContact(step,false,duplicate);Log("miss","공격 범위 밖 · 명중 효과 없음");return;}
                if(!duplicate&&step.WeaponAction is null&&VinquibusGunshot.Applies(build.WeaponId,c.Melee.Attack))
                {
                    if(CombatScene.PlayerDistance(build,enemy.Position)>1e-9)throw new CombatRuleUnavailableException("빈퀴부스 사격의 절대 비행 속도는 위키 미기재입니다. 발사→폭발0.8초와 도착 시각을 연결하는 자동 계산은 거리0의 접촉 조건에서 제공하며, 먼 대상의 비행 시간을 임의로 만들지 않습니다.");
                    var shot=Input(step);var delayed=new Step(shot with {Stance=null,Melee=shot.Melee! with {Attack="Gunshot Explosion",Tennokai=false,TargetIndex=1}},null,0,false,false,Delayed:true,PrecisionAtShot:shot.Melee!.Conditions!.GetValueOrDefault("VinquibusPrecision"));
                    delayedGunshots.Add(new(time+VinquibusGunshot.Delay,enemy,delayed,shot));Log("gunshotFuse","빈퀴부스 · 거리0 발사 후 지연 폭발 예약",VinquibusGunshot.Delay);
                }
                if(energyChannel is not null&&c.Melee.ExtraDamage>0){energyChannel.Consume();Log("energyChannelUse","에너지 채널 · 다음 한 타격에 전량 소비 · 치명 배율 제외",c.Melee.ExtraDamage);}
                groupConnected=true;if(accumulating is not null&&!step.WhipDistribution)whipRecipients.Add(enemy.Index);if(!duplicate)valkyr?.Gain(time,3);if(valkyr is not null)hysteriaHealing+=(70+10*build.Melee.AbilityRank)*build.Melee.AbilityStrength;
                if(vendettaAttacks>0)groupUsedVendetta=true;
                if(formDefinition?.Code=="Anku"&&c.Melee.Setup.Incarnon&&(step.Slide||step.Build.Melee!.Attack=="Slide Attack"))Buff("SlideHitBuff",6);
                if(stance?.Combo.Key=="Neutral"&&step.Hit?.Group==steps[^1].Hit?.Group)ApplyTimedPerks("NeutralFinalMove");
                var policy=c.Melee.Policy??MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack);
                if(!step.Delayed&&!step.IchorCloud&&!step.CaustPool&&step.OrviusExplosion is null&&step.ZenistarAttack is null&&step.OkinaExplosion is null&&step.Contagion is null&&step.Epidemic is null&&!duplicate&&tennokai?.Contact(time,step.WeaponAction is {} weaponInput?-weaponInput.Input:step.ExaltedWave is {} activeWave?100000+activeWave.Cast:swingId,step.ExaltedWave?.Projectile??hit,enemy.Index,(profile.Raw.Text("Class")=="Gunblade"||policy.Code=="ExaltedWave"),TennokaiState.Generates(profile,step.Build.Melee!.Attack,step.Finisher),random.NextDouble())==true)
                    Log("tennokaiOpportunity",tennokai.WindowAmbiguous?"텐노카이 재발동 · 종료 시각은 근거 범위":"텐노카이 기회 발생",initial.Melee.TennokaiWindow);
                var detonations=shards is not null&&c.Melee.Setup.Incarnon&&policy.Heavy?shards.Detonate(enemy.Index,time,(a,b)=>CombatScene.Distance(enemies[a].Position,enemies[b].Position)):[];
                DamageType? amanataProc=amanata is not null&&c.Melee.Effects.GetValueOrDefault("AmanataRandomProc")>0&&amanata.Pressure(swingId,policy.Heavy,c.Melee.EffectiveCombo,true)?AmanataState.PressureTypes[random.Next(AmanataState.PressureTypes.Length)]:null;
                if(amanataProc is {} selectedProc)Log("amanataPressure","아마나타 프레셔 · 첫 명중 대상 · "+Labels.Damage(selectedProc),enemy.Index+1);
                AmanataContact(step,true,duplicate);
                var returnBefore=healingPerStatus>0?TargetStatusRules.Count(Input(step)):0;
                // Conditions refer to the target before this hit's own procs are applied.
                // Heavy attacks do not award ordinary hit combo. Status-generated combo
                // (Relentless Combination) is a separate event and still applies.
                var noHitCombo=!policy.HitCombo||policy.Code=="InnodemProjectile";
                if(step.IchorCloud&&c.Melee.Setup.Statuses?.GetValueOrDefault(DamageType.Toxin)>0)
                    GainCombo(c.Melee.Effects.GetValueOrDefault("ComboOnToxin"),"알케미스트 · 기존 독성 대상의 구름 접촉");
                if(!policy.Heavy&&!noHitCombo)
                {
                    var baseCount=exalted?.Family==ExaltedFamily.Clones?3:1;
                    var gain=RollCombo((step.Hit?.Multiplier??1)-1+(baseCount+c.Melee!.Effects.GetValueOrDefault("RisingStormCombo"))*c.Melee.ComboChance);
                    var state=c.Melee!.Setup.Statuses??[];
                    foreach(var (type,key) in new[]{(DamageType.Cold,"ComboOnCold"),(DamageType.Slash,"ComboOnSlash"),(DamageType.Toxin,"ComboOnToxin")})
                        if(state.GetValueOrDefault(type)>0)gain+=c.Melee.Effects.GetValueOrDefault(key);
                    gain+=c.Melee.Effects.GetValueOrDefault("ExtraCombo");
                    if(step.Slide||c.Melee.Attack=="Slide Attack")gain+=c.Melee.Effects.GetValueOrDefault("SlideCombo");
                    GainCombo(gain,"명중 · 콤보 획득");
                }
                if(c.Melee!.Attack.Contains("Slam",StringComparison.Ordinal))
                    GainCombo(RollCombo(c.Melee.Effects.GetValueOrDefault("SlamCombo")*c.Melee.ComboChance),"슬램 범위 명중 · 진화 콤보");
                if(step.Finisher||step.Build.Melee!.Finisher is not null)GainCombo(c.Melee.Effects.GetValueOrDefault("FinisherCombo"),"마무리 · 진화 콤보");
                if(profile.Name=="Spinnerex")enemy.SpinnerLastHit=time;
                if(!groupFinisherForm&&(step.Finisher||c.Melee!.Setup.Finisher is not null))
                {form?.Finisher(time);groupFinisherForm=true;}
                if(obexArea&&c.Melee!.Setup.Incarnon&&(step.Finisher||c.Melee.Setup.Finisher is not null))
                {
                    var raw=c.Damage.Values.Sum();obexWaves++;obexDamage+=raw*(1+tier*(c.CriticalMultiplier-1));obexStealthDamage+=raw*critical;
                    Log("obexWave","오벡스 · 마무리 접촉의12m 파동 · 원래 대상 중복 합산 없음",raw*(1+tier*(c.CriticalMultiplier-1)));
                    var origin=enemy;
                    foreach(var destination in enemies.Where(e=>e!=origin&&e.Pools.Health>0&&CombatScene.Distance(origin.Position,e.Position)<=12).ToArray())AtEnemy(destination,()=>{
                        if(c.Melee.StealthBonus>0)throw new CombatRuleUnavailableException("오벡스 방사형 마무리의 은신 배율은 위키가 잠정 기술하고 있습니다. 성분 표의 범위와 구분하며 이 장면의 확정 처치 전이는 제공하지 않습니다.");
                        var wave=OnCurrentEnemy(c,"Obex Finisher Explosion");Damage(wave,critical,criticalHit:tier>0);Log("obexAreaHit",$"오벡스 · 대상{origin.Index+1}의12m 마무리 파동",raw*critical);
                    });
                }
                Damage(c,critical,criticalHit:tier>0,delayDeathBlast:true);Log(step.WhipDistribution?"whipDistribution":"hit",step.WhipDistribution?"윕클로 · 같은 스트랭글돔에50% 전파 · 치명타·상태 별도 판정":duplicate?"듀플리케이트 추가 타격":"직접 타격",c.Damage.Values.Sum()*critical);
                if(shards is not null&&c.Melee.Setup.Incarnon)
                {
                    foreach(var e in detonations)ShardStrike(e,step,c);
                    if(enemy.Pools.Health>0&&!policy.Heavy&&!c.Melee.Attack.Contains("Slam",StringComparison.Ordinal))
                        foreach(var e in shards.Embed(enemy.Index,time,swingId))ShardStrike(e,step,c);
                }
                if(invigorated&&tier>0)
                {invigoratedCount=Math.Min(5,(invigoratedEnd>time?invigoratedCount:0)+1);invigoratedEnd=time+15;Log("invigorated",$"인비거레이티드 · {invigoratedCount}중첩 · 15초 갱신",invigoratedCount*.05);}
                // On-hit/critical triggers also fire when this contact kills.
                // The captured calculation still determines this hit's own procs.
                Trigger("Hit");if(step.Finisher||step.Build.Melee!.Finisher is not null)ApplyTimedPerks("Finisher");if(tier>0){Trigger("Critical");StackPerk("Critical");}
                if(enemy.Pools.Health<=0)
                {
                    var lethalBlast=c.Melee!.ForcedProcs.Count(p=>p=="Blast")+(amanataProc==DamageType.Blast?1:0);
                    var lethalHeat=c.Melee.ForcedProcs.Contains("Heat")||amanataProc==DamageType.Heat;
                    var lethalRolls=(int)Math.Floor(c.StatusChance)+(random.NextDouble()<c.StatusChance%1?1:0);
                    for(var n=0;n<lethalRolls;n++){var choice=random.NextDouble()*c.Damage.Values.Sum();foreach(var (type,amount) in c.Damage){choice-=amount;if(choice<0){if(type==DamageType.Blast)lethalBlast++;if(type==DamageType.Heat)lethalHeat=true;break;}}}
                    if(exaltedHotShot&&lethalHeat)Buff("HotShotStacks",10,1,50);
                    var tick=(c.Melee.StatusDamage.SingleOrDefault(p=>p.Type==DamageType.Blast)?.DamagePerTick??0)*critical/MeleeStealth.Mean(c);
                    DetonateBlast(false,Enumerable.Range(0,lethalBlast).Select(_=>new Proc{Type=DamageType.Blast,Tick=tick,Source=c,ReceiverFaction=c.Melee.FactionMultiplier}).ToArray());
                    HealReturn(step,c,returnBefore,false);return;
                }
                if(!duplicate&&c.Melee!.Attack=="Shield Throw"&&enemy.FinisherExposure.Apply(time,step.ShieldFinisherSeconds,false,enemy.Pools.Overguard,enemy.Conditions.GetValueOrDefault("ControlImmune")>0))Log("shieldFinisherOpen",$"대상{enemy.Index+1} · 마무리 노출"+(step.ShieldFinisherSeconds is {} span?$" · 사용자 조건{span:0.###}초":" · 지속시간 미기재"));
                var rolls=(int)Math.Floor(c.StatusChance)+(random.NextDouble()<c.StatusChance%1?1:0);
                var heatInflicted=false;
                void Disarm(bool impactProc=false)
                {
                    if(enemy.Pools.Health<=0||enemy.DisarmApplied||!DesertWindControl.Applies(build.WeaponId,c.Melee!.ForcedProcs,enemy.Pools.Overguard,impactProc))return;
                    enemy.DisarmApplied=true;Log("disarm",$"서린 스톰 · 대상{enemy.Index+1} · 영구 무장 해제 효과 적용");
                }
                void WeaponProc(DamageType type,bool forced=false)
                {
                    if(enemy.Pools.Health>0){if(spring is not null){spring.Proc(time);Log("springBlade","스프링 로디드 · 독립 사거리 중첩",spring.Count(time));}if(type==DamageType.Heat)heatInflicted=true;}
                    AddProc(type,c,critical);
                    if(type==DamageType.Impact)Disarm(true);
                    if(!step.IchorCloud&&!(step.OkinaExplosion is not null&&forced&&type==DamageType.Cold))SpreadInfluence(type,c,critical,policy.Code is not ("DestrezaProjectile" or "DestrezaSpectral")&&!(profile.Name=="Rumblejack"&&(step.Finisher||c.Melee!.Setup.Finisher is not null)),canTrigger:!step.CaustPool,canSpread:step.OkinaExplosion!=false);
                    if(c.Melee!.Effects.GetValueOrDefault("ExodiaForceDamage")>0&&random.NextDouble()<.5&&enemy.Pools.Health>0)
                    {
                        foreach(var destination in enemies.Where(e=>e.Pools.Health>0&&CombatScene.PlayerDistance(build,e.Position)<6).ToArray())AtEnemy(destination,()=>{
                            var current=Evaluate(step);var sum=current.Damage.Values.Sum();
                            var force=MeleeSecondaryEngine.Describe(current).Single(x=>x.Name=="엑소디아 포스").Damage!.Value*(1-CombatScene.PlayerDistance(build,enemy.Position)/6);
                            var burst=OnCurrentEnemy(current,"Exodia Force Explosion") with {Damage=current.Damage.ToDictionary(p=>p.Key,p=>sum>0?p.Value/sum*force:0)};
                            Damage(burst,1);Log("secondary","엑소디아 포스 · 플레이어 중심6m · 비치명·상태 미발생",force);
                        });
                    }
                }
                for(var i=0;i<rolls;i++)
                {var selection=random.NextDouble()*c.Damage.Values.Sum();foreach(var (type,value) in c.Damage){selection-=value;if(selection<0){WeaponProc(type);if(type==DamageType.Impact&&random.NextDouble()<c.Melee!.Effects.GetValueOrDefault("ImpactToSlash"))WeaponProc(DamageType.Slash);break;}}}
                foreach(var proc in c.Melee!.ForcedProcs)if(Enum.TryParse<DamageType>(proc,out var type))WeaponProc(type,true);
                if(amanataProc is {} bonusStatus)WeaponProc(bonusStatus);
                if(exaltedHotShot&&heatInflicted)Buff("HotShotStacks",10,1,50);
                Disarm();
                HealReturn(step,c,enemy.Pools.Health>0?TargetStatusRules.Count(Input(step)):returnBefore,enemy.Pools.Health>0);
                var crowd=c.Melee.ForcedProcs.FirstOrDefault(p=>p is "Knockdown" or "Ragdoll" or "Lifted");
                if(c.Melee.Effects.GetValueOrDefault("Afflictions")>0&&crowd is not null&&enemy.Pools.Overguard==0)
                {
                    var current=Evaluate(step);var existing=enemy.Procs.Where(p=>p.End>time).Select(p=>new ProcSource(p.Type,p.Basis,p.Tick,Math.Max(0,p.End-p.Next+1),p.Element,p.Faction)).ToArray();
                    // Burning Hate's tag cannot retroactively empower procs
                    // caused by this same strike, including Afflictions.
                    var procEffects=new Dictionary<string,double>(current.Melee!.Effects){["StatusVulnerability"]=c.Melee.Effects.GetValueOrDefault("StatusVulnerability")};
                    current=current with {Melee=current.Melee with {Effects=procEffects}};
                    var extra=MeleeSecondaryEngine.Afflict(current,existing,crowd);
                    foreach(var added in extra.Added)AddProc(added.Type,current,1,added.DamagePerTick,added.ModdedBaseDamage);
                    foreach(var type in existing.Select(p=>p.Type).Distinct().Where(t=>t is not (DamageType.Slash or DamageType.Heat or DamageType.Toxin or DamageType.Electricity or DamageType.Gas or DamageType.Blast)))
                        for(var n=0;n<extra.AddedStacks;n++)AddProc(type,current,1);
                    if(extra.AddedStacks>0)Log("affliction","어플릭션즈 · 기존 상태 추가",extra.AddedStacks);
                }
                if(strip>0&&FlatArmorReduction.ShatteringApplies(profile.Raw.Text("Class"),c.Melee!.Attack,policy)&&c.Damage.GetValueOrDefault(DamageType.Impact)>0){enemy.RemovedArmor+=strip;Log("armor","기본 방어력 감소",strip);}
                if(profile.Name=="Dual Viciss"&&gasThisHit)Buff("GasBuff",10,1,5);
                if(step.Build.Melee!.Attack=="Heavy Attack")
                {
                    if(profile.Name=="Ekhein")Buff("HeavyInsight",8);

                    if(profile.Name=="Harmony")
                    {
                        foreach(var proc in enemy.Procs.Where(p=>p.Tick>0&&p.Type!=DamageType.Blast).ToArray())
                        {
                            var ticks=proc.Type is DamageType.Electricity or DamageType.Gas?Math.Max(0,Math.Ceiling(proc.End-proc.Next-1e-9)):Math.Max(0,Math.Floor(proc.End-proc.Next+1e-9)+1);
                            var saved=proc.Tick;proc.Tick*=ticks;Tick(proc);proc.Tick=saved;enemy.Procs.Remove(proc);
                        }
                        Log("detonate","하모니 · 남은 상태 피해 소모");
                    }
                }
                if(step.Build.Melee.Attack.Contains("Slam"))
                {if(profile.Name=="Wrath")Buff("GraviticSlash",10,1,10);if(profile.Name=="Pride")Buff("SlamStatus",10,1,10);}
                if(MeleeStealth.NeedsContactCooldown(c)){enemy.StealthReadyAt=time+3;Log("stealthCooldown","근접 접촉 · 이 대상의 은신 보너스 3초 대기",3);}
                if(cobraSleep&&step.CobraFirst&&!duplicate&&c.Melee.AreaRadius is null&&enemy.Pools.Health>0&&enemy.FinisherExposure.Apply(time,2,true,enemy.Pools.Overguard,enemy.Conditions.GetValueOrDefault("ControlImmune")>0))Log("cobraSleep",$"대상{enemy.Index+1} · 콤보 첫타 · 2초 수면·마무리 노출",2);
                if(!duplicate&&tier==1&&random.NextDouble()<c.DuplicateChance)Strike(step,true);
            }
            double Snapshot(bool noCo=false)
            {
                if(settings.ScriptedAttacksOnly||cobraSleep)return 0;
                var c=Evaluate(steps[0],noCo);
                if(stance is null&&steps.Length>1)return steps.Sum(s=>{var v=Evaluate(s,noCo);return v.TargetDps*v.HitInterval;})/steps.Sum(s=>s.Build.Scenario.BaseInterval/Evaluate(s,noCo).AttackSpeed);
                return !timed||stance is null?c.TargetDps:steps.Sum(s=>{var v=Evaluate(s,noCo);return v.TargetDps*v.HitInterval;})/(stance.Duration!.Value*initial.AttackSpeed/c.AttackSpeed);
            }
            var startDps=Snapshot();
            void UpdatePendingAttackSpeed()
            {
                if(settings.ScriptedAttacksOnly||enemy.Pools.Health<=0)return;
                var speed=Evaluate(steps[hit%steps.Length]).AttackSpeed;
                // Preserve the remaining animation work, rather than restarting
                // the swing or keeping a delay computed under an expired buff.
                // The underlying hit timestamps are still the explicit input.
                if(speed!=scheduledSpeed&&double.IsFinite(nextHit)&&nextHit>time)
                    nextHit=time+(nextHit-time)*scheduledSpeed/speed;
                scheduledSpeed=speed;
            }
            void Sample()
            {
                var savedEnemy=enemy;enemy=primary;
                AdvanceReaper();
                healthRegen.Advance(PlayerTime());
                brave?.Advance(PlayerTime());
                fortification?.Advance(PlayerTime());
                AdvanceForm();
                sums[point,22]+=form?.Remaining(PlayerTime())??0;
                sums[point,23]+=.1*(form?.HeavyKills??0);
                sums[point,24]+=form?.Resilience(PlayerTime())??0;
                souls?.Advance(PlayerTime());sums[point,25]+=souls?.Charges??0;sums[point,26]+=souls?.InFlight??0;
                sums[point,27]+=tennokai?.RemainingMinimum(time)??0;sums[point,28]+=tennokai?.RemainingMaximum(time)??0;
                sums[point,29]+=tennokai?.Consumed??0;sums[point,30]+=tennokai?.Refunds??0;sums[point,31]+=tennokai?.Failures??0;sums[point,32]+=tennokai?.RawCurseDamage??0;
                sums[point,33]+=spring is null&&build.WeaponId!=KeratinosMechanic.WeaponId?0:Evaluate(steps[0]).Melee!.Range;sums[point,34]+=buffs.TryGetValue("HotShotStacks",out var hot)&&hot.End>time?hot.Value:0;sums[point,35]+=conditions.GetValueOrDefault("ConsecutiveThrows");sums[point,36]+=vinquibus?.Precision??0;sums[point,37]+=vinquibus?.Ferocity??0;valkyr?.Advance(PlayerTime());sums[point,38]+=valkyr?.DamagePercent??0;
                sums[point,39]+=magus?.Charges??0;
                sums[point,40]+=warcry?.Remaining(time)??0;sums[point,41]+=WarcryRules.Armor(CurrentWarcry());
                if(mirrors is not null){sums[point,42]+=Enumerable.Range(1,4).Count(n=>mirrors.Active(n,time));sums[point,43]+=HallOfMirrorsRules.Malevolence(mirrors.At(time))-1;}
                if(rubble is not null){rubble.Advance(time);sums[point,44]+=rubble.Armor;sums[point,45]+=rubble.Healing;sums[point,46]+=rubble.Drops;}
                sums[point,21]+=jade.StatusDurationBonus(PlayerTime());
                sums[point,20]+=animosity*(animositySlot is null?0:.07*(animositySlot.Rank+1));
                sums[point,19]+=rapidStacks*(rapidConclusion?.Conditional.GetValueOrDefault("ParkourSpeed")??0);
                var frameArmor=WarframeCombatArcanes.ArmorBonus(arcane with {Warframe1=Active(arcane.Warframe1),Warframe2=Active(arcane.Warframe2)},timed?buffs.Count(b=>b.Key.StartsWith("Ultimatum:",StringComparison.Ordinal)&&b.Value.End>time):1);
                sums[point,17]+=arcane.PlayerArmor+(fortification?.Bonus??0)+frameArmor+WarcryRules.Armor(CurrentWarcry())+(rubble?.At(time).Armor??0)??0;sums[point,18]+=fortification?.Bonus??0;
                sums[point,15]+=brave?.Generated??0;sums[point,16]+=enemy.Pools.Health>0?brave?.Rate??0:0;
                sums[point,14]+=invigoratedEnd>PlayerTime()?invigoratedCount:0;
                player?.Advance(PlayerTime());
                var status=Status();var armor=MeleeTargetModel.EffectiveArmor(Armor(),build.Melee with {Statuses=status,Debuffs=TargetDebuffs.AtTime(enemy.Initial.Melee!.Debuffs,PlayerTime()),HeatArmorStrip=HeatStrip()});
                if(enemy.Pools.Health>0)
                {
                    sums[point,8]+=Snapshot();sums[point,9]+=Snapshot(true);
                }
                sums[point,10]+=startDps;
                comboCounter.Advance(time);
                sums[point,0]+=enemy.DirectDamage;sums[point,1]+=enemy.StatusDamage;sums[point,2]+=enemy.Pools.Health;sums[point,3]+=enemy.Pools.Shields;sums[point,4]+=enemy.Pools.Overguard;sums[point,5]+=armor;sums[point,6]+=status.Count;sums[point,7]+=comboCounter.Multiplier;sums[point,11]+=comboCounter.Points;sums[point,12]+=player?.Shields??0;sums[point,13]+=player?.Overshields??0;point++;
                enemy=savedEnemy;
            }
            while(point<pointCount)
            {
                if(++budget>50000)throw new ArgumentException("한 표본의 전투 사건이 50,000건을 넘었습니다. 입력한 공격 간격과 타격 시각을 확인하세요.");if(budget%32==0)token.ThrowIfCancellationRequested();
                var sampleTime=pointCount==1?0:seconds*point/(pointCount-1);
                if(enemies.All(e=>e.Pools.Health==0))
                {
                    if(rubble is not null)while(true)
                    {
                        var pickup=rubbleIndex<rubblePickups.Length?rubblePickups[rubbleIndex].Time:double.PositiveInfinity;
                        var action=inputIndex<orderedActions.Length?orderedActions[inputIndex].Time:double.PositiveInfinity;
                        var nextRubble=Math.Min(pickup,action);if(nextRubble>sampleTime+1e-9)break;time=nextRubble;
                        if(action<=pickup){var input=orderedActions[inputIndex++];if(input.Kind is "RubbleNullifierEnter" or "RubbleNullifierExit" or "RubbleHostMigration")Trigger(input.Kind);}
                        else{var p=rubblePickups[rubbleIndex++];var healed=rubble.Pickup(p);Log("rubblePickup",p.MissingHealth>0?"전투 후 러블 회복":"전투 후 러블 방어력",p.MissingHealth>0?healed:rubble.Armor);}
                    }
                    time=sampleTime;Sample();continue;
                }
                // Keep a pending attack outside the horizon: a later speed
                // buff may move it inside. Only exclude it from this queue scan.
                var queuedHit=focus.Pools.Health==0||seconds>0&&nextHit>=seconds-1e-9?double.PositiveInfinity:nextHit;
                var nextTick=enemies.SelectMany(e=>e.Procs.Where(p=>e.Pools.Health>0||p.Type==DamageType.Gas)).Where(p=>p.Tick>0&&p.Next<=p.End+1e-9).Select(p=>p.Next).DefaultIfEmpty(double.PositiveInfinity).Min();
                var nextAction=inputIndex<orderedActions.Length?orderedActions[inputIndex].Time:double.PositiveInfinity;
                var nextCollision=collisionIndex<collisions.Length?collisions[collisionIndex].Time:double.PositiveInfinity;
                if(epidemicIndex<epidemic.Length)nextCollision=Math.Min(nextCollision,epidemic[epidemicIndex].Time);
                if(cloudIndex<cloudContacts.Length)nextCollision=Math.Min(nextCollision,cloudContacts[cloudIndex].Time);
                if(poolIndex<poolTicks.Length)nextCollision=Math.Min(nextCollision,poolTicks[poolIndex].Time);
                if(okinaIndex<okinaEvents.Length)nextCollision=Math.Min(nextCollision,okinaEvents[okinaIndex].Time);
                if(ceramicIndex<ceramicEvents.Length)nextCollision=Math.Min(nextCollision,ceramicEvents[ceramicIndex].Time);
                if(waveIndex<waveEvents.Length)nextCollision=Math.Min(nextCollision,waveEvents[waveIndex].Time);
                if(glassIndex<vitrify.Length)nextCollision=Math.Min(nextCollision,vitrify[glassIndex].Time);
                if(glassSlamIndex<glassSlams.Length)nextCollision=Math.Min(nextCollision,glassSlams[glassSlamIndex].First().Time);
                if(rapierIndex<rapierEvents.Length)nextCollision=Math.Min(nextCollision,rapierEvents[rapierIndex].Time);
                if(innodemIndex<innodemEvents.Length)nextCollision=Math.Min(nextCollision,innodemEvents[innodemIndex].Time);
                if(bladeIndex<bladeEvents.Length)nextCollision=Math.Min(nextCollision,bladeEvents[bladeIndex].Time);
                if(weaponActionIndex<weaponActionEvents.Length)nextCollision=Math.Min(nextCollision,weaponActionEvents[weaponActionIndex].Time);
                if(hologramIndex<hologramContacts.Length)nextCollision=Math.Min(nextCollision,hologramContacts[hologramIndex].Time);
                if(shieldIndex<shieldEvents.Length)nextCollision=Math.Min(nextCollision,shieldEvents[shieldIndex].Time);
                if(glaiveIndex<glaiveEvents.Length)nextCollision=Math.Min(nextCollision,glaiveEvents[glaiveIndex].Time);
                if(orviusIndex<orviusEvents.Length)nextCollision=Math.Min(nextCollision,orviusEvents[orviusIndex].Time);
                if(zenistarIndex<zenistarEvents.Length)nextCollision=Math.Min(nextCollision,zenistarEvents[zenistarIndex].Time);
                nextCollision=Math.Min(nextCollision,zenistar.NextBoundary(time));
                if(rubbleIndex<rubblePickups.Length)nextCollision=Math.Min(nextCollision,rubblePickups[rubbleIndex].Time);
                var nextGunshot=delayedGunshots.Select(s=>s.Time).DefaultIfEmpty(double.PositiveInfinity).Min();
                var nextExpiry=Math.Min(Math.Min(killBuffs.NextExpiry,fortification?.NextExpiry??double.PositiveInfinity),enemies.Where(e=>e.Pools.Health>0).SelectMany(e=>e.Procs).Where(p=>p.End>time+1e-9).Select(p=>p.End).DefaultIfEmpty(double.PositiveInfinity).Min());
                if(timed)nextExpiry=Math.Min(nextExpiry,incarnonStacks.NextExpiry);
                nextExpiry=Math.Min(nextExpiry,piercing?.NextExpiry??double.PositiveInfinity);
                if(enemies.Any(e=>e.Pools.Health>0))nextExpiry=Math.Min(nextExpiry,spring?.NextExpiry??double.PositiveInfinity);
                if(enemies.Any(e=>e.Pools.Health>0))nextExpiry=Math.Min(nextExpiry,residual?.NextExpiry??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,jade.NextExpiry(time));
                nextExpiry=Math.Min(nextExpiry,warcry?.NextExpiry(time)??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,metronome?.NextExpiry(time)??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,jetStream?.NextTransition(time)??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,mirrors?.NextExpiry(time)??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,rubble?.NextBoundary(time)??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,glass.NextExpiry);
                nextExpiry=Math.Min(nextExpiry,enemies.SelectMany(e=>e.Initial.Melee!.Debuffs??[]).Where(d=>d.Remaining>time+1e-9).Select(d=>d.Remaining).DefaultIfEmpty(double.PositiveInfinity).Min());
                nextExpiry=Math.Min(nextExpiry,enemies.Where(e=>e.PoolBlindEnd>time+1e-9).Select(e=>e.PoolBlindEnd).DefaultIfEmpty(double.PositiveInfinity).Min());
                nextExpiry=Math.Min(nextExpiry,enemies.Where(e=>e.BladeBlindEnd>time+1e-9).Select(e=>e.BladeBlindEnd).DefaultIfEmpty(double.PositiveInfinity).Min());
                nextExpiry=Math.Min(nextExpiry,enemies.Where(e=>e.EruptionEnd>time+1e-9).Select(e=>e.EruptionEnd).DefaultIfEmpty(double.PositiveInfinity).Min());
                nextExpiry=Math.Min(nextExpiry,exaltedLife?.NextTransition??double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,form?.Active(time)==true?form.End:double.PositiveInfinity);
                nextExpiry=Math.Min(nextExpiry,buffs.Values.Where(b=>b.End>time+1e-9).Select(b=>b.End).DefaultIfEmpty(double.PositiveInfinity).Min());
                var next=Math.Min(nextGunshot,Math.Min(Math.Min(nextCollision,contagionFlights.NextTime),Math.Min(nextExpiry,Math.Min(queuedHit,Math.Min(nextTick,nextAction)))));
                if(next>sampleTime+1e-9){time=sampleTime;UpdatePendingAttackSpeed();Sample();continue;}
                time=next;
                if(exaltedLife?.Advance(time)==true){primal?.Reset(time);Log("exaltedEnd","익절티드 지속시간 종료 · 재사용 대기",exaltedLife.WaitingForReturn?0:Math.Max(0,exaltedLife.ReadyAt-time));}
                if(spring is not null&&enemies.Any(e=>e.Pools.Health>0)&&spring.NextExpiry<=time+1e-9)_=spring.Count(time);
                residual?.Advance(PlayerTime());
                foreach(var expiredGlass in glass.Expire(time))Log("vitricaGlassExpired",$"대상{expiredGlass+1} · 유리화7초 종료");
                foreach(var expired in buffs.Where(b=>Math.Abs(b.Value.End-time)<1e-9))Log("buffEnd",buffNames.GetValueOrDefault(expired.Key,expired.Key)+" 종료");
                if(timed&&incarnonStacks.NextExpiry<=time+1e-9)
                {foreach(var perk in stackPerks)_=incarnonStacks.Count(perk.Condition,PlayerTime());if(stackPerks.Any(p=>p.Condition=="ColdKills"))UpdateInitialCombo();}
                if(timed&&killBuffs.NextExpiry<=time+1e-9)
                {
                    var oldGalv=killBuffs.GalvanizedStacks;killBuffs.Advance(time);
                    if(galvComboPerStack>0&&oldGalv!=killBuffs.GalvanizedStacks)UpdateInitialCombo();
                }
                foreach(var destination in enemies.Where(e=>e.Pools.Health>0||e.Procs.Any(p=>p.Type==DamageType.Gas)))AtEnemy(destination,()=>{
                if(enemy.FrozenEnd>=0&&time>=enemy.FrozenEnd){var cold=enemy.Procs.FirstOrDefault(p=>p.Type==DamageType.Cold);enemy.Procs.RemoveAll(p=>p.Type==DamageType.Cold);if(cold is not null)for(var i=0;i<3;i++)enemy.Procs.Add(new(){Type=DamageType.Cold,End=time+cold.Duration,Next=double.PositiveInfinity,Source=cold.Source});enemy.FrozenEnd=-1;}
                // Scheduled ticks at the exact end of delayed statuses run before expiry.
                foreach(var proc in enemy.Procs.Where(p=>p.Tick>0&&p.Next<=time+1e-9&&p.Next<=p.End+1e-9).ToArray())
                {if(proc.Type is not (DamageType.Electricity or DamageType.Gas)||proc.Next<proc.End-1e-9)Tick(proc);proc.Next=proc.Type==DamageType.Blast?double.PositiveInfinity:proc.Next+1;if(proc.Type==DamageType.Blast)proc.End=time;}
                enemy.Procs.RemoveAll(p=>p.End<=time+1e-9);
                });
                while(inputIndex<orderedActions.Length&&orderedActions[inputIndex].Time<=time+1e-9)
                {var action=orderedActions[inputIndex];Trigger(action.Kind,action.Value,action.Element,action.Duration,action.RecentlyAwareTargets);inputIndex++;}
                foreach(var projectileEvent in contagionFlights.Due(time))ContagionStrike(projectileEvent,true);
                while(collisionIndex<collisions.Length&&collisions[collisionIndex].Time<=time+1e-9)
                {var collision=collisions[collisionIndex++];ContagionStrike(contagionFlights.Contact(collision,enemies.Length),false);}
                while(poolIndex<poolTicks.Length&&poolTicks[poolIndex].Time<=time+1e-9)
                {
                    var tick=poolTicks[poolIndex++];var window=poolWindows[tick.Window];var destination=enemies[tick.Target-1];
                    if(destination.Pools.Health<=0)continue;
                    AtEnemy(destination,()=>{
                        var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,Finisher=null}},null,1,false,false,CaustPool:true);
                        var input=Input(step);
                        if(tick.Entry)poolSources[tick.Window]=input;
                        else if(CaustacystMechanic.PayloadSignature(catalog,poolSources[tick.Window])!=CaustacystMechanic.PayloadSignature(catalog,input))
                            throw new CombatRuleUnavailableException("웅덩이 진입 이후 공격자 피해 버프가 바뀌었습니다. 생성/접촉 시점의 저장 또는 실시간 갱신 규칙은 위키 미기재입니다.");
                        Strike(step);Log("caustPoolHit",tick.Entry?"웅덩이 진입 · 즉시 피해":"웅덩이 체류 · 1초 간격 피해");
                        if(enemy.Pools.Health>0&&enemy.Pools.Overguard<=0)
                        {
                            if(window.BlindDuration is {} blind){enemy.PoolBlindEnd=Math.Max(enemy.PoolBlindEnd,time+blind);Log("caustBlind","웅덩이 실명 · 지속시간 사용자 가정",blind);}
                            else enemy.PoolBlindUnknownSince=Math.Min(enemy.PoolBlindUnknownSince,time);
                        }
                    });
                }
                while(epidemicIndex<epidemic.Length&&epidemic[epidemicIndex].Time<=time+1e-9)
                {
                    // Same-time recipients share pre-contact attacker state.
                    // Unknown travel/falloff is never inferred from this list.
                    var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    var stamp=epidemic[epidemicIndex].Time;
                    while(epidemicIndex<epidemic.Length&&epidemic[epidemicIndex].Time==stamp)
                    {
                        var impact=epidemic[epidemicIndex++];var destination=enemies[impact.Target-1];
                        if(destination.Pools.Health<=0)continue;
                        AtEnemy(destination,()=>{
                            var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,Finisher=null}},null,1,false,false,Epidemic:impact.Phase);
                            contacts.Add((enemy,step,Evaluate(step)));
                        });
                    }
                    foreach(var contact in contacts)AtEnemy(contact.Target,()=>{
                        Strike(contact.Step,captured:contact.Hit);Log("epidemicImpact",ExodiaEpidemicMechanic.Label(contact.Step.Epidemic!.Value)+$" · 지정 대상{enemy.Index+1}");
                        if(contact.Step.Epidemic==EpidemicPhase.Wave&&enemy.Pools.Health>0&&enemy.Pools.Overguard<=0&&enemy.FrozenEnd<=time)
                            Log("epidemicSuspension","에피데믹 부유 · 컨오/어플릭션즈 상태 제외 · 지속시간",build.Melee!.Exodia!.Rank+1);
                    });
                }
                foreach(var shot in delayedGunshots.Where(s=>s.Time<=time+1e-9).ToArray())
                {delayedGunshots.Remove(shot);GunshotExplosion(shot);}
                enemy=focus;
                if(nextHit<=time+1e-9&&enemy.Pools.Health>0&&(seconds==0||nextHit<seconds-1e-9))
                {
                    var step=steps[hit%steps.Length];if(cobraSleep&&hit%steps.Length==0&&!step.Finisher)step=step with {CobraFirst=true};
                    if(glaiveFlights.Any(f=>time>=f.Launch&&time<f.Returned))throw new ArgumentException("글레이브 비행 중에는 근접 타격을 할 수 없습니다. 회수 시각 또는 투척만 계산을 설정하세요.");
                    if(orviusHovers.Length>0&&orvius.InFlight(time))throw new ArgumentException("오르비우스 비행 중에는 근접 공격을 할 수 없습니다. 회수 완료 시각 또는 투척만 계산을 설정하세요.");
                    if(truthSlot is not null&&tennokai?.Active==true&&tennokaiContactUsed)
                        throw new CombatRuleUnavailableException("트루스 플레임의 실패 판정 애니메이션 종료 시각은 위키에 미기재입니다. 다음 공격 전에 ‘텐노카이 동작 종료’ 시각을 입력하세요. 마지막 접촉을 종료 시각으로 가정하지 않습니다.");
                    comboCounter.SetPaused(time,false);
                    if(storedFraction>0&&MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack).Heavy&&heavyStoredDamage is null)
                    {heavyStoredDamage=conditions.GetValueOrDefault("BlockedDamage");conditions["BlockedDamage"]=0;Log("storedDamageUse","어벤징 트루스 · 이번 강공격 입력에 저장량 사용",heavyStoredDamage.Value*storedFraction);}
                    if(sancti is not null&&MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack).Heavy&&(stance is null||hit%steps.Length==0||steps[(hit-1)%steps.Length].Hit!.Group!=step.Hit!.Group))
                    {sancti.Heavy(comboCounter.Multiplier);Log("sanctiBuff",$"생티 마기스타 · 임무 영구 흡혈 {sancti.Minimum:P0}~{sancti.Maximum:P0}",sancti.Minimum);}
                    if(step.Build.Melee!.Attack=="Heavy Attack"&&(stance is null||hit%steps.Length==0||steps[(hit-1)%steps.Length].Hit!.Group!=step.Hit!.Group))heavyPassiveCombo=comboCounter.Multiplier;
                    if(castAtContact)Trigger("ExaltedAbilityCast");
                    var c=Evaluate(step);
                    var impaleCombo=comboCounter.Multiplier;
                    var wasRagdolled=c.Melee!.Setup.Conditions?.GetValueOrDefault("Ragdoll")>0||c.Melee.Setup.Conditions?.GetValueOrDefault("Lifted")>0;
                    // Registered radial components all use one captured actor state.
                    // A Fury/kill proc on the first recipient must not strengthen
                    // the same explosion's direct damage on later recipients.
                    var contacts=new List<(EnemyRuntime Enemy,Step Step,Calculation? Calculation)>();
                    var radial=c.Melee.AreaRadius is >0&&c.Melee.Policy?.Code!="VitricaGlass";
                    if(radial)
                    {
                        var center=new CombatEnemy("area",focus.Position?.X??0,(focus.Position?.Y??0)-step.Build.Melee!.Distance,focus.Position?.Z??0);
                        foreach(var recipient in enemies.Where(e=>e.Pools.Health>0&&CombatScene.Distance(center,e.Position)<=c.Melee.AreaRadius).OrderBy(e=>e==focus?0:1).ToArray())AtEnemy(recipient,()=>{
                            var radialStep=step with {Build=step.Build with {Melee=step.Build.Melee! with {Distance=CombatScene.Distance(center,recipient.Position),TargetIndex=1}}};
                            contacts.Add((recipient,radialStep,Evaluate(radialStep)));
                        });
                    }
                    else contacts.Add((enemy,step,null));
                    if(exalted?.Family==ExaltedFamily.Whipclaw)
                    {
                        // Snapshot all recipients before any same-cast on-hit/kill
                        // buff. A recipient already hit by the radial explosion
                        // receives its full contact, not a duplicate half contact.
                        var domes=contacts.Where(t=>t.Calculation?.Melee?.HitConnects!=false)
                            .Select(t=>TargetDebuffs.Dome(t.Calculation?.Melee?.Setup??Input(t.Step).Melee!)).OfType<int>().ToHashSet();
                        var direct=contacts.Select(t=>t.Enemy.Index).ToHashSet();
                        foreach(var recipient in enemies.Where(e=>e.Pools.Health>0&&!direct.Contains(e.Index)).ToArray())AtEnemy(recipient,()=>{
                            var propagated=step with {WhipDistribution=true,Build=step.Build with {Melee=step.Build.Melee! with {Distance=0,TargetIndex=1}}};
                            var input=Input(propagated);if(TargetDebuffs.Dome(input.Melee!) is not {} dome||!domes.Contains(dome))return;
                            contacts.Add((recipient,propagated,Evaluate(propagated)));
                        });
                    }
                    if(contacts.Count==0){AmanataContact(step,false,false);Log("miss","공격 범위 밖 · 명중 효과 없음");}
                    int? rolledPellets=null;
                    foreach(var contact in contacts)AtEnemy(contact.Enemy,()=>{
                        var bounce=GlaiveBounceMechanic.IsThrow(contact.Step.Build.Melee!.Attack)&&GlaiveBounceMechanic.Chance(build)>0&&(contact.Calculation??c).Melee!.HitConnects;
                        var attacker=bounce?Input(contact.Step):null;
                        var pellets=rolledPellets??=MetronomeRules.Roll(c.Melee.Projectiles,random.NextDouble);
                        for(var pellet=0;pellet<pellets&&enemy.Pools.Health>0;pellet++)Strike(contact.Step,captured:contact.Calculation);
                        if(bounce&&random.NextDouble()<GlaiveBounceMechanic.Chance(build))BounceExplosion(contact.Step,contact.Enemy,attacker!);
                    });
                    if(ruvox&&c.Melee.Setup.Incarnon&&step.Build.Melee!.Attack=="Heavy Slam Attack")
                    {
                        foreach(var destination in enemies.Where(e=>e.Pools.Health>0&&e.Pools.Overguard==0&&CombatScene.PlayerDistance(build,e.Position)<=8&&e.Conditions.GetValueOrDefault("Ragdoll")==0&&e.Conditions.GetValueOrDefault("Lifted")==0).ToArray())AtEnemy(destination,()=>{
                            if(enemy==focus&&wasRagdolled)return;enemy.ImpaleEnd=time+Math.Min(12.5,1.5+impaleCombo-1);Log("impale",$"루복스 · 대상{enemy.Index+1} 공허 가시 · {enemy.ImpaleEnd-time:0.##}초",enemy.ImpaleEnd-time);
                        });
                    }
                    // A killing contact also finishes the current input: later contacts
                    // cannot run on this target, but its on-hit consumption still applies.
                    var groupEnds=enemy.Pools.Health==0||stance is null||(hit+1)%steps.Length==0||steps[(hit+1)%steps.Length].Hit!.Group!=step.Hit!.Group;
                    if(groupEnds&&step.Build.Melee!.Attack=="Heavy Attack")HeavyPassive(heavyPassiveCombo);
                    if(MeleeEngine.AttackPolicy(build.WeaponId,step.Build.Melee!.Attack).Heavy)
                    {
                        // Multiple contacts/projectiles from one heavy input spend once.
                        if(groupEnds){var spent=comboCounter.SpendHeavy(time,c.Melee.HeavyEfficiency,c.Melee.Setup.Tennokai);Log("heavyCombo","강공격 · 콤보 소모",spent);if(groupConnected&&!MeleeAnimosityRules.NonManualGlaiveThrow(build.WeaponId,step.Build.Melee.Attack)){if(animositySlot is not null&&animosity>0)Log("animosity","애니모시티 · 명중 강공격에 중첩 소모",animosity);animosity=0;}}
                    }
                    else if(groupEnds&&groupConnected&&animositySlot is not null&&MeleeAnimosityRules.Builds(build.WeaponId,step.Build.Melee!))
                    {animosity=Math.Min(10,animosity+1);Log("animosity","애니모시티 · 일반 입력 명중 중첩",animosity);}
                    if(groupEnds&&accumulating is not null){if(whipRecipients.Count>=3){accumulating.Gain(time,new[]{10d,15,20,35}[augment!.Rank]);Log("whipclawAugment","어큐뮬레이팅 윕클로 · 한 시전으로3명 이상 명중",accumulating.Value);}whipRecipients.Clear();}
                    if(groupEnds&&absorbedElements?.Total>0)
                    {Log("absorbedElementSpent","액크 & 브런트 · 다음 공격 입력에 원소 충전 소모",absorbedElements.Consume());absorbedElements.WriteTo(conditions);}
                    if(groupEnds&&profile.Passives.Any(p=>p.Key=="BlockCharges")&&conditions.GetValueOrDefault("BlockCharges")>0)
                    {Log("blockCharge","검과 방패 · 공격에 충전 소모",conditions["BlockCharges"]);conditions["BlockCharges"]=0;}
                    if(groupEnds&&profile.Name=="Dorrclave"&&vendettaAttacks>0&&groupConnected&&groupUsedVendetta)
                    {vendettaAttacks--;Log("vendetta","라이징 벤데타 · 남은 공격",vendettaAttacks);}
                    if(groupEnds&&tennokai?.Active==true&&truthSlot is null&&c.Melee!.Setup.Tennokai)tennokai.Finish(time);
                    if(groupEnds&&c.Melee!.Setup.Tennokai)tennokaiContactUsed=true;
                    if(groupEnds&&vinquibus is not null){vinquibus.FinishMeleeInput(swingId,groupConnected);Log("vinquibusPrecision","빈퀴부스 · 입력 종료 · 남은 정밀 충전",vinquibus.Precision);}
                    if(groupEnds&&magus?.Consume(build.WeaponId,time)==true)Log("magusCharge","마구스 어그레스 · 남은 공격",magus.Charges);
                    if(groupEnds){groupConnected=false;groupUsedVendetta=false;groupFinisherForm=false;heavyStoredDamage=null;conditions["FirstQuickMelee"]=0;swingId++;}
                    if(groupEnds&&profile.Name=="Arca Titron"&&step.Build.Melee!.Attack.Contains("Slam",StringComparison.Ordinal)&&(stance is null||stance.Combo.ActionKind is "Slam" or "Heavy"))
                    {Log("capacitor","슬램 캐패시터 · 슬램에 충전 소모",conditions.GetValueOrDefault("SlamCapacitor"));conditions["SlamCapacitor"]=0;}
                    hit++;
                    scheduledSpeed=Evaluate(step).AttackSpeed;
                    if(mode is EncounterMode.FirstHit or EncounterMode.Primer)nextHit=double.PositiveInfinity;
                    else if(stance is null)nextHit=time+step.Build.Scenario.BaseInterval/scheduledSpeed;
                    else
                    {var cycle=hit/steps.Length;var previous=(hit-1)%steps.Length;var delta=hit%steps.Length==0?stance.Duration!.Value*initial.AttackSpeed-times![previous]+times[0]:times![hit%steps.Length]-times[previous];nextHit=time+delta/scheduledSpeed;if(cycle>0&&(mode==EncounterMode.StanceCycle||stance.Combo.FinisherKind is not (null or "Ground")))nextHit=double.PositiveInfinity;}
                    if(groupEnds)
                    {
                        for(var n=0;n<targetOrder.Length;n++){orderIndex=(orderIndex+1)%targetOrder.Length;focus=enemies[targetOrder[orderIndex]];if(focus.Pools.Health>0)break;}
                        enemy=focus;
                    }
                }
                while(waveIndex<waveEvents.Length&&waveEvents[waveIndex].Time<=time+1e-9)
                {
                    var e=waveEvents[waveIndex++];var f=e.Flight;var source=waveSources[f.Projectile];var heavyWave=source.Input!.Melee!.Attack=="Exalted Heavy Wave";
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack=source.Input.Melee.Attack,Distance=f.Distance,Finisher=null}},source.SourceHit,source.CoFactor,false,false,ExaltedWave:f,WaveSource:source);
                    if(e.Launch)
                    {
                        var actor=Input(step);if(actor.Melee!.Conditions?.GetValueOrDefault("ExaltedInactive")>0)throw new ArgumentException("익절티드 비활성 상태에서 파동을 발사할 수 없습니다.");waveActors[f.Cast]=actor;
                        if(actor.Melee.Tennokai){tennokaiContactUsed=true;waveTennokaiUses[f.Cast]=tennokai!.Consumed;}
                        if(heavyWave){var c=ExaltedWaveFlight.Calculate(catalog,actor,source,f with {Distance=0},useInitialComboFloor:false);var spent=comboCounter.SpendHeavy(time,c.Melee!.HeavyEfficiency,c.Melee.Setup.Tennokai);Log("heavyCombo","익절티드 교차 파동 · 공격 입력당 한 번 소모",spent);}
                        Log("exaltedWaveLaunch",$"공격 입력{f.Cast} · {f.Combo} 파동 발사");continue;
                    }
                    void FinishWaveInput()
                    {
                        if(truthSlot is null&&waveTennokaiUses.TryGetValue(f.Cast,out var use)&&tennokai?.Consumed==use&&!waveEvents.Skip(waveIndex).Any(x=>!x.Launch&&x.Flight.Cast==f.Cast))tennokai.Finish(time);
                    }
                    var target=enemies[f.Target-1];if(target.Pools.Health<=0){Log("exaltedWaveCorpse",$"파동{f.Projectile} · 사망한 대상 통과");FinishWaveInput();continue;}
                    if(exalted?.Family==ExaltedFamily.Wind&&!CombatScene.HasSight(settings,-1,target.Index)){Log("exaltedWaveSight",$"데저트 윈드 · 대상{f.Target} 시야 차단 · 피해 없음");FinishWaveInput();continue;}
                    var launched=waveActors[f.Cast];var pierce=wavePierces.GetValueOrDefault(f.Projectile)+1;
                    step=step with {Build=launched,WaveTargetIndex=pierce};var now=ExaltedWaveFlight.WithResources(Input(step),launched,heavyWave);
                    if(ExaltedWaveFlight.Signature(catalog,launched,source,f,pierce)!=ExaltedWaveFlight.Signature(catalog,now,source,f,pierce))throw new CombatRuleUnavailableException("익절티드 파동 비행 중 공격자 버프/콤보가 바뀌었습니다. 저장/갱신 시점은 위키 미기재입니다.");
                    AtEnemy(target,()=>{var c=Evaluate(step);var before=enemy.DirectDamage;Strike(step,captured:c);if(c.Melee!.HitConnects)
                        {
                            wavePierces[f.Projectile]=pierce;
                            if(heavyWave&&waveHeavyConsumed.Add(f.Cast)){if(animosity>0)Log("animosity","익절티드 강공격 파동 · 첫 유효 명중에서 한 번 소모",animosity);animosity=0;}
                        }Log("exaltedWaveImpact",$"파동{f.Projectile} · 대상{f.Target} · 누적{f.Distance:0.###}m",enemy.DirectDamage-before);});
                    FinishWaveInput();
                    groupConnected=false;
                }
                while(glassIndex<vitrify.Length&&vitrify[glassIndex].Time<=time+1e-9)
                {
                    var g=vitrify[glassIndex++];var center=enemies[g.Target-1];
                    foreach(var target in enemies.Where(e=>e.Pools.Health>0&&CombatScene.Distance(center.Position,e.Position)<=5))
                    {
                        if(glass.Apply(target.Index,time,target.Pools.Overguard>0))Log("vitricaGlass",$"대상{target.Index+1} · 유리화7초 · 피해 없음");
                        else Log("vitricaGlassImmune",$"대상{target.Index+1} · 오버가드로 유리화 면역");
                    }
                }
                while(glassSlamIndex<glassSlams.Length&&glassSlams[glassSlamIndex].First().Time<=time+1e-9)
                {
                    var contacts=glassSlams[glassSlamIndex++].ToArray();var heavy=contacts[0].Heavy;var actor=Input(steps[0]);
                    var bursts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    var centers=contacts.Select(s=>enemies[s.Target-1]).Where(e=>e.Pools.Health>0&&glass.Active(e.Index,time)).ToArray();
                    foreach(var center in centers)glass.Remove(center.Index);
                    foreach(var center in centers)
                    {
                        foreach(var recipient in enemies.Where(e=>e.Pools.Health>0&&(e==center||CombatScene.Distance(center.Position,e.Position)<=6)))AtEnemy(recipient,()=>{
                            var step=new Step(actor with {Stance=null,Melee=actor.Melee! with {Attack="Glass Explosion",Tennokai=false,Distance=CombatScene.Distance(center.Position,recipient.Position)}},null,0,false,false,Delayed:true,VitricaOrigin:recipient==center,VitricaHeavy:heavy);
                            bursts.Add((recipient,step,Evaluate(step)));
                        });
                        Log("vitricaShatter",$"대상{center.Index+1} · "+(heavy?"강 슬램":"슬램")+" 유리 폭발");
                    }
                    foreach(var burst in bursts)AtEnemy(burst.Target,()=>{var before=enemy.DirectDamage;if(enemy.Pools.Health>0)Strike(burst.Step,captured:burst.Hit);Log("vitricaExplosion",$"대상{enemy.Index+1} · "+(burst.Step.VitricaOrigin==true?"원래 유리 대상":"주변 유리 폭발"),enemy.DirectDamage-before);});
                    if(heavy){var efficiency=VitricaMechanic.Calculate(catalog,actor,true,useInitialComboFloor:false).GlassedTarget.Melee!.HeavyEfficiency;var spent=comboCounter.SpendHeavy(time,efficiency,false);Log("heavyCombo","유리 파괴 강 슬램 · 소모 전 콤보를 폭발 전체에 공유",spent);UpdateInitialCombo();}
                    if(heavy&&bursts.Count>0&&animositySlot is not null&&animosity>0){Log("animosity","애니모시티 · 유리 파괴 강 슬램 한 번 소모",animosity);animosity=0;}
                    if(magus?.Consume(build.WeaponId,time)==true)Log("magusCharge","마구스 어그레스 · 슬램 입력 · 남은 공격",magus.Charges);
                    groupConnected=false;swingId++;
                }
                while(rapierIndex<rapierEvents.Length&&rapierEvents[rapierIndex].Time<=time+1e-9)
                {
                    var e=rapierEvents[rapierIndex++];var f=e.Contact;
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Heavy Attack",Finisher=null,Distance=0,TargetIndex=1}},null,1,false,false,Delayed:true,Rapier:f);
                    var now=Input(step);
                    if(e.Launch)
                    {
                        if(!now.Melee!.Incarnon)throw new ArgumentException("유령 레이피어 발사에는 변신이 필요합니다.");
                        rapierSources[f.Volley]=now;Log("rapierLaunch",$"데스트레자{f.Volley} · 유령 레이피어5개 발사");continue;
                    }
                    var source=rapierSources[f.Volley];
                    if(source.Melee!.Incarnon!=now.Melee!.Incarnon)throw new CombatRuleUnavailableException("레이피어 비행 중 변신이 끝났습니다. 이때 발사/명중 피해 저장 규칙은 위키 미기재입니다.");
                    if(DestrezaFlight.Signature(catalog,source,f)!=DestrezaFlight.Signature(catalog,now,f))throw new CombatRuleUnavailableException("레이피어 비행 중 공격자 버프가 바뀌었습니다. 명시된 콤보/준비/진화 보너스 이외의 발사·명중 저장 규칙은 위키 미기재입니다.");
                    var target=enemies[f.Target-1];if(target.Pools.Health<=0)continue;
                    AtEnemy(target,()=>{var before=enemy.DirectDamage;Strike(step);Log("rapierImpact",$"데스트레자{f.Volley}/{f.Rapier} · 대상{f.Target}",enemy.DirectDamage-before);});
                    groupConnected=false;swingId++;
                }
                while(innodemIndex<innodemEvents.Length&&innodemEvents[innodemIndex].Time<=time+1e-9)
                {
                    var e=innodemEvents[innodemIndex++];var f=e.Contact;
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Dagger Projectile",Distance=f.Distance,Finisher=null}},null,1,false,false,Innodem:f);
                    if(e.Launch)
                    {
                        var source=Input(step);if(!source.Melee!.Incarnon)throw new ArgumentException("인노뎀 투사체 발사에는 변신이 필요합니다.");innodemSources[f.Projectile]=source;
                        Log("innodemLaunch",$"인노뎀{f.Projectile} · 조준 활공 발사");continue;
                    }
                    var launched=innodemSources[f.Projectile];step=step with {Build=launched};
                    var now=Input(step);now=now with {Melee=now.Melee! with {Incarnon=launched.Melee!.Incarnon}};
                    if(InnodemFlight.Signature(catalog,launched,f)!=InnodemFlight.Signature(catalog,now,f))throw new CombatRuleUnavailableException("인노뎀 비행 중 공격자 피해 버프가 바뀌었습니다. 발사/명중 저장 규칙은 위키 미기재입니다.");
                    var target=enemies[f.Target-1];if(target.Pools.Health<=0){Log("innodemPassedCorpse",$"인노뎀{f.Projectile} · 사망한 대상 통과");continue;}
                    AtEnemy(target,()=>{var before=enemy.DirectDamage;Strike(step);Log("innodemImpact",$"인노뎀{f.Projectile} · 대상{f.Target} · {f.Distance:0.##}m",enemy.DirectDamage-before);});
                    groupConnected=false;swingId++;
                }
                while(bladeIndex<bladeEvents.Length&&bladeEvents[bladeIndex].Time<=time+1e-9)
                {
                    var e=bladeEvents[bladeIndex++];var f=e.Flight;var target=enemies[f.Target-1];var motion=bladeMotion[f.Projectile];
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack=IncarnonBladeFlight.Attack(build.WeaponId),Distance=0,Finisher=null}},null,1,false,false,Spectral:f,SpectralMotion:motion);
                    if(e.Kind==-1)
                    {
                        bladeInputResources[f.Projectile]=Input(step);
                        if(build.WeaponId==IncarnonBladeFlight.Ack&&absorbedElements?.Total>0)
                        {
                            if(!settings.ScriptedAttacksOnly)throw new CombatRuleUnavailableException("원소 충전을 공유하는 칼날은 유령 칼날 항목에서 같은 입력의 접촉으로 지정하고 자동 반복 공격을 끄세요.");
                            Log("absorbedElementSpent","액크 & 브런트 · 칼날/투사체 직격/폭발에 원소 충전 공유",absorbedElements.Consume());absorbedElements.WriteTo(conditions);
                        }
                        continue;
                    }
                    if(e.Kind==0)
                    {
                        var source=IncarnonBladeFlight.WithResources(Input(step),bladeInputResources[f.Projectile],preserveForm:false);if(!source.Melee!.Incarnon)throw new ArgumentException("유령 칼날을 발사할 때 인카논 변신이 필요합니다.");
                        bladeSources[f.Projectile]=source;
                        Log("incarnonBladeLaunch",$"유령 칼날{f.Projectile} · "+(build.WeaponId==IncarnonBladeFlight.Ack?(f.Motion=="Aerial"?"공중×2":"슬라이드×1"):$"발사 타격 배율×{motion:0.###}"));continue;
                    }
                    if(e.Kind==3)
                    {
                        var recipient=enemies[f.BladeTarget-1];if(recipient.Pools.Health<=0){Log("incarnonPhysicalMiss","액크 & 브런트 · 칼날 대상이 이미 처치됨");continue;}
                        var contact=physicalBladeHits[f.Projectile];var meleeStep=new Step(contact.Input!,contact.SourceHit,contact.CoFactor,false,f.Motion=="Slide",AckBladeSource:bladeInputResources[f.Projectile],Aerial:f.Motion=="Aerial");
                        groupConnected=false;
                        AtEnemy(recipient,()=>{var before=enemy.DirectDamage;Strike(meleeStep);Log("incarnonPhysicalHit",$"액크 & 브런트 · 같은 입력{f.Projectile} 칼날 · 대상{f.BladeTarget}",enemy.DirectDamage-before);});
                        if(groupConnected&&animositySlot is not null&&MeleeAnimosityRules.Builds(build.WeaponId,meleeStep.Build.Melee!)){animosity=Math.Min(10,animosity+1);Log("animosity","애니모시티 · 실제 칼날 입력 명중",animosity);}
                        groupConnected=false;conditions["FirstQuickMelee"]=0;swingId++;continue;
                    }
                    var launched=bladeSources[f.Projectile];step=step with {Build=launched with {Stance=null,Melee=launched.Melee! with {Attack=IncarnonBladeFlight.Attack(build.WeaponId,e.Kind==2),Distance=0}},SpectralExplosion=e.Kind==2};
                    var now=IncarnonBladeFlight.WithResources(Input(step),launched);
                    if(IncarnonBladeFlight.Signature(catalog,launched,f,e.Kind==2,motion)!=IncarnonBladeFlight.Signature(catalog,now,f,e.Kind==2,motion))throw new CombatRuleUnavailableException("유령 칼날의 발사/명중/폭발 사이 공격자 버프가 바뀌었습니다. 해당 저장 시점은 위키 미기재입니다.");
                    if(e.Kind==1)
                    {
                        if(f.Surface){Log("incarnonBladeSurface",$"유령 칼날{f.Projectile} · 표면 접촉 · 직접 피해 없음");continue;}
                        if(target.Pools.Health<=0)throw new CombatRuleUnavailableException("유령 칼날 접촉 전에 목표가 사망했습니다. 새 충돌 위치/대상을 입력하세요.");
                        AtEnemy(target,()=>{var before=enemy.DirectDamage;Strike(step);Log("incarnonBladeImpact",$"유령 칼날{f.Projectile} · 직격",enemy.DirectDamage-before);});continue;
                    }
                    if(build.WeaponId==IncarnonBladeFlight.Hate&&!f.Surface&&target.Pools.Health<=0)throw new CombatRuleUnavailableException("헤이트의0.4초 부착 지연 도중 숙주가 사망했습니다. 떨어진 칼날의 폭발 위치는 위키 미기재입니다.");
                    var radius=Evaluate(step).Melee!.AreaRadius??0;var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(target.Position,t.Position)<=radius+1e-9).ToArray())AtEnemy(recipient,()=>{
                        var radial=step with {Build=launched with {Melee=launched.Melee! with {Attack=IncarnonBladeFlight.Attack(build.WeaponId,true),Distance=CombatScene.Distance(target.Position,recipient.Position)}}};contacts.Add((recipient,radial,Evaluate(radial)));
                    });
                    foreach(var c in contacts)AtEnemy(c.Target,()=>{var before=enemy.DirectDamage;Strike(c.Step,captured:c.Hit);Log("incarnonBladeExplosion",$"유령 칼날{f.Projectile} · 폭발 · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                    groupConnected=false;swingId++;
                }
                while(hologramIndex<hologramContacts.Length&&hologramContacts[hologramIndex].Time<=time+1e-9)
                {
                    var contact=hologramContacts[hologramIndex++];if(mirrors?.Active(contact.Number,time)!=true){Log("mirrorMiss",$"분신{contact.Number} 미활성 · 명중 없음");continue;}
                    var target=enemies[contact.Target-1];if(target.Pools.Health<=0)continue;
                    AtEnemy(target,()=>{
                        var raw=build with {Stance=null,Melee=build.Melee! with {Attack=contact.Attack,Hologram=new(contact.Number),Distance=contact.Distance,TargetIndex=contact.TargetIndex,Finisher=null}};
                        var step=new Step(raw,null,1,false,false,Hologram:contact.Number);var actor=Input(step);Calculation hitCalculation;
                        if(contact.StanceHit>0)
                        {
                            if(build.Stance is null)throw new ArgumentException("분신 스탠스 타격에는 스탠스를 선택하세요.");
                            var sequence=StanceEngine.Calculate(catalog,actor with {Stance=build.Stance},stances).Hits.SelectMany(h=>Enumerable.Repeat(h.Calculation!,h.Count)).ToArray();
                            if(contact.StanceHit>sequence.Length)throw new ArgumentException("분신 스탠스 타격 번호가 콤보 타수를 넘었습니다.");hitCalculation=sequence[contact.StanceHit-1];
                        }
                        else hitCalculation=MeleeEngine.Calculate(catalog,actor);
                        var count=MetronomeRules.Roll(hitCalculation.Melee!.Projectiles,random.NextDouble);for(var i=0;i<count&&enemy.Pools.Health>0;i++)MirrorStrike(hitCalculation,contact.Number);
                    });
                }
                while(weaponActionIndex<weaponActionEvents.Length&&weaponActionEvents[weaponActionIndex].Time<=time+1e-9)
                {
                    var e=weaponActionEvents[weaponActionIndex++];if(build.WeaponId==VinquibusState.WeaponId){VinquibusAction(e);continue;}var v=e.Input;var contact=e.Contact;
                    if(!MirrorFlight(v.Hologram,"weapon:"+v.Input,e.Kind==0,contact is {Blade:false}))continue;
                    var explosion=WarfanSlam.Active(v)||e.Kind==2||contact?.Explosion==true||build.WeaponId==WeaponStanceMechanic.Korumm&&contact?.Blade==false;
                    var attack=WeaponActionFlight.Attack(build.WeaponId,v,contact,explosion);
                    var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack=attack,Distance=0,TargetIndex=1,Finisher=null}},null,1,false,false,WeaponAction:v,WeaponContact:contact,WeaponExplosion:explosion);
                    if(e.Kind==0)
                    {
                        var actor=Input(step);weaponActionSources[v.Input]=actor;
                        var c=WeaponActionFlight.Calculate(catalog,actor,v);
                        if(v.Hologram==0&&WeaponActionFlight.Heavy(build.WeaponId,v)){var spent=comboCounter.SpendHeavy(time,c.Melee!.HeavyEfficiency,actor.Melee!.Tennokai);Log("heavyCombo","고유 강공격 · 입력당 한 번 콤보 소모",spent);tennokaiContactUsed|=actor.Melee.Tennokai;}
                        if(v.Hologram==0&&conditions.GetValueOrDefault("BlockCharges")>0){Log("blockCharge","고유 공격 입력에 막기 충전 한 번 소모",conditions["BlockCharges"]);conditions["BlockCharges"]=0;}
                        Log("weaponActionInput",$"고유 입력{v.Input} · 순서{v.Sequence} · 공유 콤보 {actor.Scenario.Combo:0}×",actor.Scenario.Combo);continue;
                    }
                    if(e.Kind==3)
                    {
                        var expected=WeaponActionFlight.ProjectileCount(build.WeaponId,v.Sequence)*(1+MetronomeRules.Opera(metronome?.At(time)));
                        weaponActionPellets[v.Input]=MetronomeRules.Roll(expected,random.NextDouble);Log("operaVolley",$"단검 발사{v.Input} · 기대{expected:0.###}개 · 이번 표본{weaponActionPellets[v.Input]}개",weaponActionPellets[v.Input]);continue;
                    }
                    if(contact is {Blade:false}&&weaponActionPellets.TryGetValue(v.Input,out var fired)&&contact.Projectile>fired)continue;
                    var source=weaponActionSources[v.Input];var current=WeaponActionFlight.WithResources(Input(step),source);
                    if(WeaponActionFlight.Signature(catalog,source,v,contact,explosion)!=WeaponActionFlight.Signature(catalog,current,v,contact,explosion))throw new CombatRuleUnavailableException("고유 공격 입력과 명중 사이에 공격자 피해 버프가 바뀌었습니다. 이 성분의 버프 저장/갱신 시점은 위키 미기재입니다. 강공격 동작 여부는 실제 명중 시각으로 별도 적용합니다.");
                    var center=enemies[contact!.Target-1];var bladeOrder=contact.Blade?Array.IndexOf((v.Contacts??[]).Where(c=>c.Blade&&c.BladeHit==contact.BladeHit).OrderBy(c=>c.Time).ToArray(),contact)+1:1;step=step with {Build=source with {Melee=source.Melee! with {Attack=attack,Distance=contact.Blade?0:contact.Travel,TargetIndex=bladeOrder}}};
                    if(!explosion)
                    {
                        var payload=WeaponActionFlight.Calculate(catalog,current,v,contact,distance:contact.Travel);WeaponActionFlight.CheckTravel(current,v,contact,payload);
                        if(contact.Surface&&build.WeaponId==WeaponStanceMechanic.Grigori)Log("grigoriRicochet","그리고리 · 지정 표면 반사 · 피해 없음",contact.Travel);
                        if(!contact.Surface)
                        {
                            if(center.Pools.Health<=0)throw new CombatRuleUnavailableException("지정한 투사체 도착 전에 대상이 처치되었습니다. 관통 뒤 실제 대상 또는 표면 충돌을 지정하세요.");
                            AtEnemy(center,()=>{var before=enemy.DirectDamage;if(v.Hologram>0)MirrorStrike(Evaluate(step),v.Hologram);else Strike(step);Log(contact.Blade?"weaponBladeImpact":"weaponProjectileImpact",$"입력{v.Input} · {(contact.Blade?"칼날"+contact.BladeHit:"투사체"+contact.Projectile)} · 대상{contact.Target}",enemy.DirectDamage-before);});
                        }
                    }
                    else
                    {
                        var radius=WarfanSlam.Active(v)?WeaponActionFlight.Calculate(catalog,current,v,contact).Melee!.AreaRadius??0:WeaponActionFlight.Radius(build.WeaponId,v.ExplosionModel);var bursts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                        foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(center.Position,t.Position)<=radius+1e-9).ToArray())
                        {
                            if(build.WeaponId==WeaponStanceMechanic.Korumm&&(v.Contacts??[]).Any(c=>c.Blade&&c.Target==recipient.Index+1))continue;
                            AtEnemy(recipient,()=>{var radial=step with {Build=source with {Melee=source.Melee! with {Attack=attack,Distance=CombatScene.Distance(center.Position,recipient.Position)}}};bursts.Add((recipient,radial,Evaluate(radial)));});
                        }
                        foreach(var burst in bursts)AtEnemy(burst.Target,()=>{var before=enemy.DirectDamage;if(v.Hologram>0)MirrorStrike(burst.Hit,v.Hologram);else Strike(burst.Step,captured:burst.Hit);Log(WarfanSlam.Active(v)?"warfanSlam":"weaponProjectileExplosion",$"입력{v.Input} · {(WarfanSlam.Active(v)?"슬램"+contact.SlamHit:"주변 폭발")} · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                    }
                    if(v.Hologram==0&&groupConnected&&animositySlot is not null&&(WeaponActionFlight.Heavy(build.WeaponId,v)||MeleeAnimosityRules.Builds(build.WeaponId,step.Build.Melee!))&&weaponActionConnected.Add(v.Input))
                    {
                        if(WeaponActionFlight.Heavy(build.WeaponId,v)){Log("animosity","고유 강공격 첫 명중에 한 번 소모",animosity);animosity=0;}
                        else if(MeleeAnimosityRules.Builds(build.WeaponId,step.Build.Melee!)){animosity=Math.Min(10,animosity+1);Log("animosity","고유 일반 입력 칼날 명중",animosity);}
                    }
                    groupConnected=false;swingId++;
                }
                while(shieldIndex<shieldEvents.Length&&shieldEvents[shieldIndex].Time<=time+1e-9)
                {
                    var e=shieldEvents[shieldIndex++];var f=e.Flight;
                    if(!MirrorFlight(f.Hologram,"shield:"+f.Throw,e.Kind==0,e.Kind is 1 or 2))continue;
                    var shieldBuild=f.Hologram>0?HallOfMirrorsRules.Copy(build,f.Hologram):build;
                    if(e.Kind==3){Log("shieldReturn",$"방패{f.Throw} · 회수 완료");continue;}
                    var step=new Step(shieldBuild with {Stance=null,Melee=shieldBuild.Melee! with {Attack=e.Kind==2?"Shield Throw Explosion":"Shield Throw",Distance=0,Finisher=null}},null,1,false,false,Aerial:true);
                    void ConsumeShieldCharges(){if(f.Hologram==0&&conditions.GetValueOrDefault("BlockCharges")>0){Log("blockCharge","방패 · 공격 입력에 막기 충전 소모",conditions["BlockCharges"]);conditions["BlockCharges"]=0;}}
                    if(e.Kind==0)
                    {
                        var actor=Input(step);shieldSources[f.Throw]=actor;ShieldFlightRules.CheckTravel(actor,f,ShieldFlightRules.Calculate(catalog,actor));ConsumeShieldCharges();Log("shieldLaunch",$"방패{f.Throw} · 공중 투척 발사");continue;
                    }
                    if(e.Kind==1)
                    {
                        var launched=shieldSources[f.Throw];var current=ShieldFlightRules.WithCharges(Input(step),launched);
                        if(ShieldFlightRules.Signature(catalog,launched,false)!=ShieldFlightRules.Signature(catalog,current,false))throw new CombatRuleUnavailableException("방패 비행 중 공격자 버프가 바뀌었습니다. 발사/명중 저장 시점은 위키 미기재입니다.");
                        var target=enemies[f.Target-1];if(target.Pools.Health<=0)throw new ArgumentException("이미 처치된 적에 방패 직격을 지정했습니다. 실제 명중 대상을 지정하거나 직격을 비우세요.");
                        step=step with {ShieldSource=launched,ShieldFinisherSeconds=f.FinisherWindowSeconds,Build=step.Build with {Melee=step.Build.Melee! with {Distance=f.Travel??CombatScene.PlayerDistance(build,target.Position)}}};
                        AtEnemy(target,()=>{var before=enemy.DirectDamage;if(f.Hologram>0){MirrorStrike(Evaluate(step),f.Hologram,shield:true,finisherSeconds:f.FinisherWindowSeconds);}else Strike(step);Log("shieldImpact",$"방패{f.Throw} · 대상{f.Target} 직격",enemy.DirectDamage-before);});
                    }
                    else
                    {
                        var actor=Input(step);step=step with {ShieldSource=actor,ShieldExplosion=true};ConsumeShieldCharges();
                        var center=f.Detonation!.Position?.Position??enemies[f.Detonation.CenterTarget-1].Position;
                        var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                        foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(center,t.Position)<=4+1e-9).ToArray())AtEnemy(recipient,()=>{
                            var radial=step with {Build=step.Build with {Melee=step.Build.Melee! with {Distance=CombatScene.Distance(center,recipient.Position)}}};contacts.Add((recipient,radial,Evaluate(radial)));
                        });
                        foreach(var contact in contacts)AtEnemy(contact.Target,()=>{var before=enemy.DirectDamage;if(f.Hologram>0)MirrorStrike(contact.Hit,f.Hologram);else Strike(contact.Step,captured:contact.Hit);Log("shieldExplosion",$"방패{f.Throw} · 수동 회수 폭발 · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                    }
                    groupConnected=false;swingId++;
                }
                while(glaiveIndex<glaiveEvents.Length&&glaiveEvents[glaiveIndex].Time<=time+1e-9)
                {
                    var e=glaiveEvents[glaiveIndex++];var f=e.Flight;var attack=GlaiveFlightState.Attack(f,weaponId:build.WeaponId);
                    if(!MirrorFlight(f.Hologram,"glaive:"+f.Throw,e.Kind==1,e.Contact is not null||e.Kind==3))continue;
                    var flyingState=f.Hologram>0?hologramGlaives[f.Hologram]:glaive;var throwBuild=f.Hologram>0?HallOfMirrorsRules.Copy(build,f.Hologram):build;
                    var step=new Step(throwBuild with {Stance=null,Melee=throwBuild.Melee! with {Attack=attack,Distance=0,TargetIndex=1,Finisher=null}},null,1,false,false,Glaive:f);
                    if(e.Kind==0){if(f.Hologram==0)tennokai?.SetFlightPaused(time,false);Log("glaiveReturn",$"글레이브 투척{f.Throw} · 회수 완료");continue;}
                    if(e.Kind==1)
                    {
                        flyingState.Launch(f);if(f.Hologram==0){conditions["ConsecutiveThrows"]=glaive.Consecutive;if(build.WeaponId!=WolfSledgeFlight.WeaponId)tennokai?.SetFlightPaused(time,true);}
                        var actor=Input(step);glaiveSources[f.Throw]=actor;GlaiveFlightState.CheckFirstLeg(actor,f,Evaluate(step));
                        if(f.Hologram==0&&build.WeaponId==WolfSledgeFlight.WeaponId&&magus?.Consume(build.WeaponId,time)==true)Log("magusCharge","울프 슬레지 · 투척 입력에 마구스 어그레스1회 소모",magus.Charges);
                        Log("glaiveLaunch",$"글레이브 투척{f.Throw} · {(f.Charged?"충전":"일반")} · {(f.Airborne?"공중":"지상")}");
                        if(powerThrow)Log("powerThrow","회수 전 버퍼 입력으로 판정한 파워 스로우 중첩",flyingState.Consecutive);continue;
                    }
                    if(e.Contact is {} contact)
                    {
                        var wolf=build.WeaponId==WolfSledgeFlight.WeaponId;
                        if(wolf)step=step with {Build=glaiveSources[f.Throw] with {Stance=null,Melee=glaiveSources[f.Throw].Melee! with {Attack=attack,Distance=0,TargetIndex=1,Finisher=null}}};
                        var heavyReturn=wolf&&contact.Kind==GlaiveContactKind.Return&&f.Detonation is {} detonated&&time>=detonated.Time;
                        if(heavyReturn)step=step with {WolfHeavySource=wolfHeavySources[f.Throw],Build=step.Build with {Melee=step.Build.Melee! with {Attack="Throw Recall Explosion"}}};
                        var target=contact.Surface is null?enemies[contact.Target-1]:null;
                        var actor=target is null?Input(step):null;Calculation? calculated=null;
                        if(target is not null)AtEnemy(target,()=>{actor=Input(step);calculated=Evaluate(step);});
                        calculated??=Evaluate(step);var effect=calculated.Melee!.Effects;
                        flyingState.Contact(contact,(int)(GlaiveFlightState.BaseBounces(build.WeaponId)+effect.GetValueOrDefault("Bounces")),GlaiveFlightState.InnatePunchThrough(build.WeaponId)+effect.GetValueOrDefault("PunchThrough"),effect.GetValueOrDefault("DisablePunchThrough")>0);
                        if(wolf)actor=WolfSledgeFlight.WithResources(actor!,heavyReturn?wolfHeavySources[f.Throw]:glaiveSources[f.Throw]);
                        if(heavyReturn)
                        {
                            if(WolfSledgeFlight.ReturnSignature(catalog,wolfHeavySources[f.Throw])!=WolfSledgeFlight.ReturnSignature(catalog,actor!))throw new CombatRuleUnavailableException("울프 슬레지의 기폭 후 귀환 강타에 저장 콤보/버프를 재사용하는지는 위키 미기재입니다. 기폭 전후 값이 다른 이 경로를 임의 확정하지 않습니다. "+WolfSledgeFlight.Source);
                        }
                        else if(GlaiveFlightState.Signature(catalog,glaiveSources[f.Throw],attack)!=GlaiveFlightState.Signature(catalog,actor!,attack))throw new CombatRuleUnavailableException("투척 비행 도중 공격자 피해 버프가 바뀌었습니다. 발사/명중 시점 저장 규칙은 위키 미기재로 이 분기의 피해를 확정하지 않습니다.");
                        if(target is not null)
                        {
                            if(target.Pools.Health<=0)throw new ArgumentException("이미 사망한 적에 글레이브 직접 명중을 지정했습니다. 실제 지형 접촉 또는 다른 대상 경로를 설정하세요.");
                            AtEnemy(target,()=>{var before=enemy.DirectDamage;if(f.Hologram>0)MirrorStrike(calculated!,f.Hologram);else Strike(step,captured:calculated);Log("glaiveContact",$"투척{f.Throw} · 대상{enemy.Index+1} · "+(contact.Kind switch {GlaiveContactKind.Bounce=>"반사",GlaiveContactKind.PunchThrough=>"몸체 관통",_=>"귀환 관통"}),enemy.DirectDamage-before);});
                        }
                        else Log("glaiveSurface",$"투척{f.Throw} · 지형 반사 · 직접 피해 없음");
                        if(wolf&&contact.Kind==GlaiveContactKind.Bounce)
                        {
                            var wolfCenter=contact.Surface?.Position??target?.Position;
                            var blastStep=step with {WolfBounce=true,Build=actor! with {Melee=actor!.Melee! with {Attack="Throw Bounce Explosion",Distance=0,Tennokai=false}}};
                            var blastContacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                            foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(wolfCenter,t.Position)<=5+1e-9).ToArray())AtEnemy(recipient,()=>{
                                var radial=blastStep with {Build=blastStep.Build with {Melee=blastStep.Build.Melee! with {Distance=CombatScene.Distance(wolfCenter,recipient.Position)}}};blastContacts.Add((recipient,radial,Evaluate(radial)));
                            });
                            foreach(var burst in blastContacts)AtEnemy(burst.Target,()=>{var before=enemy.DirectDamage;if(f.Hologram>0)MirrorStrike(burst.Hit,f.Hologram);else Strike(burst.Step,captured:burst.Hit);Log("wolfBounce",$"울프 슬레지 · 전진 충돌 폭발 · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                            if(contact.Surface is not null)flyingState.Detonate(time);
                        }
                        else if(contact.Kind==GlaiveContactKind.Bounce&&random.NextDouble()<GlaiveBounceMechanic.Chance(build))BounceExplosion(step,target,actor!,contact.Surface);
                        if(f.Hologram==0&&wolf&&!heavyReturn&&groupConnected&&animositySlot is not null&&wolfAnimosityInputs.Add(f.Throw)){animosity=Math.Min(10,animosity+1);Log("animosity","울프 슬레지 · 일반 투척 입력 명중",animosity);}
                        groupConnected=false;continue;
                    }
                    // Manual detonation is a separate heavy input. Its combo is
                    // read once before any recipient can change the counter.
                    if(f.Hologram==0&&tennokai is {Active:false})Trigger("TennokaiHeavyInput");
                    flyingState.Detonate(time);step=step with {Build=step.Build with {Melee=step.Build.Melee! with {Attack=GlaiveFlightState.Attack(f,true,build.WeaponId)}}};
                    if(build.WeaponId==WolfSledgeFlight.WeaponId){var source=Input(step);wolfHeavySources[f.Throw]=source;step=step with {Build=source};}
                    var heavy=Evaluate(step);var center=f.Detonation!.Position?.Position??enemies[f.Detonation.CenterTarget-1].Position;
                    var radius=heavy.Melee!.AreaRadius??0;var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(center,t.Position)<=radius+1e-9).ToArray())AtEnemy(recipient,()=>{
                        var radial=step with {Build=step.Build with {Melee=step.Build.Melee! with {Distance=CombatScene.Distance(center,recipient.Position)}}};contacts.Add((recipient,radial,Evaluate(radial)));
                    });
                    foreach(var c in contacts)AtEnemy(c.Target,()=>{var before=enemy.DirectDamage;if(f.Hologram>0)MirrorStrike(c.Hit,f.Hologram);else Strike(c.Step,captured:c.Hit);Log("glaiveDetonation",$"투척{f.Throw} · 수동 기폭 · 대상{enemy.Index+1}",enemy.DirectDamage-before);});
                    if(f.Hologram==0){var spent=comboCounter.SpendHeavy(time,heavy.Melee.HeavyEfficiency,heavy.Melee.Setup.Tennokai);Log("heavyCombo","투척 수동 기폭 · 한 번의 콤보 소모",spent);}
                    if(f.Hologram==0&&build.WeaponId==WolfSledgeFlight.WeaponId&&magus?.Consume(build.WeaponId,time)==true)Log("magusCharge","울프 슬레지 · 수동 기폭 입력에 마구스 어그레스1회 소모",magus.Charges);
                    if(f.Hologram==0){if(contacts.Count>0)animosity=0;if(heavy.Melee!.Setup.Tennokai){tennokaiContactUsed=true;if(truthSlot is null)tennokai?.Finish(time);}}groupConnected=false;swingId++;
                }
                while(orviusIndex<orviusEvents.Length&&orviusEvents[orviusIndex].Time<=time+1e-9)
                {
                    var e=orviusEvents[orviusIndex++];var h=e.Hover;var target=enemies[h.Target-1];
                    var template=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Hover Attack",Distance=0,Finisher=null,Tennokai=false}},null,1,false,false,OrviusExplosion:false);
                    if(e.Kind==0)
                    {
                        orvius.Launch(h);tennokai?.SetFlightPaused(time,true);
                        if(h.Travel is {} travel){var speed=35*GlaiveFlightState.SpeedMultiplier(build,Evaluate(template));if(speed<=0||h.Attach+1e-9<h.Launch+travel/speed)throw new ArgumentException("오르비우스 도착이35m/s와 투사체 속도 모드로 정한 최단 비행보다 빠릅니다.");}
                        Log("orviusLaunch",$"오르비우스 투척{h.Throw} · 일반 투척");continue;
                    }
                    if(e.Kind==4){tennokai?.SetFlightPaused(time,false);Log("orviusReturn",$"오르비우스 투척{h.Throw} 회수 완료");continue;}
                    if(e.Kind==1)
                    {
                        if(target.Pools.Health<=0)throw new CombatRuleUnavailableException("매달기 시작 전에 목표가 사망했습니다. 새 유도 대상/도착 시각은 원문 미기재입니다.");
                        orvius.Attach(h.Throw,target.Pools.Overguard>0);AtEnemy(target,()=>{orviusSources[h.Throw]=Input(template);Log("orviusHold",orvius.Suspends(h.Throw,time)?"오르비우스 매달기 시작":"오버가드가 매달림 차단 · 이 투척 동안 재개 없음",orvius.Suspends(h.Throw,time)?1:0);});continue;
                    }
                    if(e.Kind==2)
                    {
                        if(!orvius.Tick(h.Throw,time)||target.Pools.Health<=0)continue;
                        AtEnemy(target,()=>{
                            var input=Input(template);
                            if(OrviusHoverState.Signature(catalog,orviusSources[h.Throw])!=OrviusHoverState.Signature(catalog,input))throw new CombatRuleUnavailableException("오르비우스 매달기 도중 공격자 피해 버프가 바뀌었습니다. 매달기 피해의 저장/갱신 규칙은 위키 미기재입니다.");
                            var before=enemy.DirectDamage;Strike(template);Log("orviusHover",$"오르비우스 투척{h.Throw} · 표 기준 매달기 피해",enemy.DirectDamage-before);
                        });continue;
                    }
                    if(h.Detonate is null)throw new CombatRuleUnavailableException("오르비우스3초 자동 폭발에 적용할 기초 피해 항목은 원문과 표의 대응이 불명확합니다. 수동 기폭은 표의 일반 투척 회수 폭발586폭발·강제냉기로 별도 계산합니다. "+OrviusHoverState.Source);
                    var step=template with {Build=build with {Stance=null,Melee=build.Melee! with {Attack="Throw Recall Explosion",Distance=0,Finisher=null}},OrviusExplosion=true};
                    var actor=Input(step);var c=Evaluate(step);var radius=c.Melee!.AreaRadius??4;
                    var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(target.Position,t.Position)<=radius+1e-9).ToArray())AtEnemy(recipient,()=>{
                        var radial=step with {Build=actor with {Melee=actor.Melee! with {Distance=CombatScene.Distance(target.Position,recipient.Position)}}};contacts.Add((recipient,radial,Evaluate(radial)));
                    });
                    foreach(var contact in contacts)AtEnemy(contact.Target,()=>{var before=enemy.DirectDamage;Strike(contact.Step,captured:contact.Hit);Log("orviusExplosion","오르비우스 수동 기폭 · 강제 냉기 · 무감쇠",enemy.DirectDamage-before);});
                    var spent=comboCounter.SpendHeavy(time,c.Melee!.HeavyEfficiency,c.Melee.Setup.Tennokai);Log("heavyCombo","오르비우스 수동 기폭 · 콤보 소모",spent);
                    if(contacts.Count>0)animosity=0;if(c.Melee.Setup.Tennokai){tennokaiContactUsed=true;if(truthSlot is null)tennokai?.Finish(time);}groupConnected=false;swingId++;
                }
                while(zenistarIndex<zenistarEvents.Length&&zenistarEvents[zenistarIndex].Time<=time+1e-9)
                {
                    var e=zenistarEvents[zenistarIndex++];var d=e.Disc;var center=enemies[d.CenterTarget-1];
                    if(d.Hologram>0&&mirrors?.Active(d.Hologram,time)!=true){Log("mirrorDiscEnd",$"분신{d.Hologram} 종료 · 원반{d.Disc} 피해 없음");continue;}
                    var discBuild=d.Hologram>0?HallOfMirrorsRules.Copy(build,d.Hologram):build;
                    if(e.Kind==0)
                    {
                        if((d.Hologram>0?d.HologramDistance:CombatScene.PlayerDistance(build,center.Position))>9+1e-9)throw new ArgumentException("제니스타의 기본 발사 거리는9m입니다. 내비게이터/매그니타이즈 이동은 이 고정 배치 시나리오에 포함되지 않습니다.");
                        var c=Evaluate(new(discBuild with {Stance=null,Melee=discBuild.Melee! with {Attack="Heavy Attack",Distance=0,Finisher=null}},null,1,false,false));
                        var disc=zenistar.Launch(d,comboCounter.Multiplier);
                        Log("zenistarLaunch",$"제니스타 원반{d.Disc} · {(d.Hologram>0?"분신"+d.Hologram:"플레이어")} · {disc.Combo}× · 수명 상한 {Math.Min(100,10*disc.Combo)}초",disc.Combo);
                        if(d.Hologram==0){var spent=comboCounter.SpendHeavy(time,c.Melee!.HeavyEfficiency,c.Melee.Setup.Tennokai);Log("heavyCombo","원반 발사 · 강공격 콤보 소모",spent);conditions["FirstQuickMelee"]=0;if(c.Melee.Setup.Tennokai)tennokaiContactUsed=true;}continue;
                    }
                    if(!zenistar.Aura(d.Disc,time))continue;
                    if(e.Kind==1&&d.Contact)
                    {
                        if(center.Pools.Health<=0)throw new CombatRuleUnavailableException("원반 직격 대상이 배치 전에 사망했습니다. 시체 충돌 위치는 원문 미기재입니다. 표면 배치에서는 직격을 해제하세요.");
                        AtEnemy(center,()=>{
                            var step=new Step(discBuild with {Stance=null,Melee=discBuild.Melee! with {Attack="Disc Impact",Distance=0,Tennokai=false,Finisher=null}},null,1,false,false,ZenistarAttack:"Disc Impact");
                            var before=enemy.DirectDamage;if(d.Hologram>0)MirrorStrike(Evaluate(step),d.Hologram);else Strike(step);Log("zenistarImpact",$"제니스타 원반{d.Disc} 직격",enemy.DirectDamage-before);
                        });
                    }
                    var attack=e.Kind==1?"Disc Explosion":"Disc Aura";
                    var template=new Step(discBuild with {Stance=null,Melee=discBuild.Melee! with {Attack=attack,Distance=0,Tennokai=false,Finisher=null}},null,1,false,false,ZenistarAttack:attack,ZenistarWrathful:d.WrathfulCriticalPercent);
                    var radius=Evaluate(template).Melee!.AreaRadius??4;
                    var contacts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(center.Position,t.Position)<=radius+1e-9).ToArray())AtEnemy(recipient,()=>{
                        var step=template with {Build=template.Build with {Melee=template.Build.Melee! with {Distance=CombatScene.Distance(center.Position,recipient.Position)}}};
                        contacts.Add((recipient,step,Evaluate(step)));
                    });
                    foreach(var contact in contacts)AtEnemy(contact.Target,()=>{
                        var before=enemy.DirectDamage;if(d.Hologram>0)MirrorStrike(contact.Hit,d.Hologram);else Strike(contact.Step,captured:contact.Hit);Log(e.Kind==1?"zenistarExplosion":"zenistarAura",$"제니스타 원반{d.Disc} · {(e.Kind==1?"배치 폭발":"지정 오라 명중")} · 반경 {radius:0.##}m",enemy.DirectDamage-before);
                    });
                    groupConnected=false;swingId++;
                }
                while(ceramicIndex<ceramicEvents.Length&&ceramicEvents[ceramicIndex].Time<=time+1e-9)
                {
                    var e=ceramicEvents[ceramicIndex++];var contact=e.Contact;
                    var liveStep=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Spectral Dagger",Finisher=null,Distance=0}},null,1,false,false,CeramicDagger:contact.Dagger);
                    if(e.Launch)
                    {
                        if(truthSlot is not null&&tennokai?.Active==true&&tennokaiContactUsed)throw new CombatRuleUnavailableException("다음 세라믹 발사 전에 트루스 플레임의 텐노카이 동작 종료 시각을 지정하세요.");
                        var source=Input(liveStep);if(!source.Melee!.Incarnon)throw new ArgumentException("세라믹 유령 단검 발사 시점에 인카논 변신이 필요합니다.");
                        ceramicSources[contact.Volley]=source;
                        var c=CeramicDaggerMechanic.Calculate(catalog,source,useInitialComboFloor:false);
                        var spent=comboCounter.SpendHeavy(time,c.Melee!.HeavyEfficiency,c.Melee.Setup.Tennokai);
                        conditions["FirstQuickMelee"]=0;tennokaiContactUsed|=c.Melee.Setup.Tennokai;
                        Log("ceramicLaunch",$"세라믹 발사{contact.Volley} · 두 단검 공유 콤보 {source.Scenario.Combo:0}×",source.Scenario.Combo);
                        Log("heavyCombo","세라믹 강공격 · 발사 한 번에 콤보 소모",spent);continue;
                    }
                    var launched=ceramicSources[contact.Volley];var destination=enemies[contact.Target-1];
                    var now=CeramicDaggerMechanic.WithCastResources(Input(liveStep),launched);
                    if(!now.Melee!.Incarnon||CeramicDaggerMechanic.PayloadSignature(catalog,launched)!=CeramicDaggerMechanic.PayloadSignature(catalog,now))
                        throw new CombatRuleUnavailableException("세라믹 발사 후 공격자 피해 버프가 변경되었습니다. 비행 중 저장/갱신 규칙은 위키 미기재입니다. 콤보와 첫 공격 보너스는 같은 발사 입력 안에서 공유합니다.");
                    if(!contact.Surface&&destination.Pools.Health<=0)throw new CombatRuleUnavailableException("세라믹 목표가 도착 전에 죽었습니다. 이후 충돌 위치는 확인할 수 없어 표면 충돌 또는 생존 대상을 지정해야 합니다.");
                    if(!contact.Surface)AtEnemy(destination,()=>{
                        var step=liveStep with {Build=launched};var before=enemy.DirectDamage;Strike(step);Log("ceramicImpact",$"세라믹 발사{contact.Volley} · 단검{contact.Dagger} 직격",enemy.DirectDamage-before);
                    });
                    var blasts=new List<(EnemyRuntime Target,Step Step,Calculation Hit)>();
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(destination.Position,t.Position)<=CeramicDaggerMechanic.Radius).ToArray())AtEnemy(recipient,()=>{
                        var distance=CombatScene.Distance(destination.Position,recipient.Position);
                        var step=liveStep with {Build=launched with {Melee=launched.Melee! with {Attack="Spectral Dagger Explosion",Distance=distance}},CeramicExplosion=true};
                        blasts.Add((recipient,step,Evaluate(step)));
                    });
                    foreach(var blast in blasts)AtEnemy(blast.Target,()=>{var before=enemy.DirectDamage;Strike(blast.Step,captured:blast.Hit);Log("ceramicExplosion",$"세라믹 단검{contact.Dagger} 폭발 · 4m · 강제 충격",enemy.DirectDamage-before);});
                    if((!contact.Surface||blasts.Count>0)&&ceramicAnimosityUsed.Add(contact.Volley)){if(animosity>0)Log("animosity","세라믹 강공격 명중 · 애니모시티 중첩 소모",animosity);animosity=0;}
                    if(launched.Melee!.Tennokai&&truthSlot is null&&!ceramicEvents.Skip(ceramicIndex).Any(x=>!x.Launch&&x.Contact.Volley==contact.Volley))tennokai?.Finish(time);
                    groupConnected=false;swingId++;
                }
                while(okinaIndex<okinaEvents.Length&&okinaEvents[okinaIndex].Time<=time+1e-9)
                {
                    var e=okinaEvents[okinaIndex++];var flight=e.Flight;var destination=enemies[flight.Target-1];
                    if(destination.Pools.Health<=0)throw new CombatRuleUnavailableException("오키나 유도 대상이 도착 전에 죽었습니다. 자동 재유도 대상·시간은 위키 미기재입니다.");
                    if(e.Launch){okina!.Launch(flight,CombatScene.PlayerDistance(build,destination.Position));Log("okinaLaunch",$"오키나 단검{flight.Dagger} 발사 → 대상{flight.Target}");continue;}
                    okina!.Impact(flight.Dagger);
                    AtEnemy(destination,()=>{
                        var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,Finisher=null,Distance=0}},null,1,false,false,OkinaExplosion:false);
                        var input=Input(step);
                        if(!input.Melee!.Incarnon||OkinaDaggerState.PayloadSignature(catalog,okinaSources[flight.Dagger])!=OkinaDaggerState.PayloadSignature(catalog,input))
                            throw new CombatRuleUnavailableException("오키나 단검 생성 후 공격자 피해 버프가 변경되었습니다. 생성/발사/명중 시점의 저장·갱신 규칙은 위키 미기재입니다.");
                        Strike(step);Log("okinaImpact",$"오키나 단검{flight.Dagger} 직격 · 강제 냉기10중첩");
                    });
                    foreach(var recipient in enemies.Where(t=>t.Pools.Health>0&&CombatScene.Distance(destination.Position,t.Position)<=OkinaDaggerState.Radius).ToArray())AtEnemy(recipient,()=>{
                        var distance=CombatScene.Distance(destination.Position,enemy.Position);
                        var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Tennokai=false,Finisher=null,Distance=distance}},null,1,false,false,OkinaExplosion:true);
                        Strike(step);Log("okinaExplosion",$"오키나 단검{flight.Dagger} 폭발 · 4m · 강제 냉기1중첩");
                    });
                }
                while(cloudIndex<cloudContacts.Length&&cloudContacts[cloudIndex].Time<=time+1e-9)
                {
                    var contact=cloudContacts[cloudIndex++];
                    if(contact.Cloud>ichorFields.Count)throw new ArgumentException("독 구름 번호가 아직 생성되지 않았습니다. 처치 이후 시각과 실제 생성 순서를 지정하세요.");
                    var field=ichorFields[contact.Cloud-1];
                    if(time>=field.Created+DualIchorCloudMechanic.Lifetime)throw new ArgumentException("이미7초 수명이 끝난 독 구름의 명중입니다.");
                    var destination=enemies[contact.Target-1];var distance=CombatScene.Distance(field.Center.Position,destination.Position);
                    if(distance>DualIchorCloudMechanic.Radius)throw new ArgumentException("독 구름 중심에서4m 밖의 명중입니다. 사거리 모드로 범위를 늘릴 수 없습니다.");
                    if(destination.Pools.Health>0)AtEnemy(destination,()=>{
                        var step=new Step(build with {Stance=null,Melee=build.Melee! with {Attack="Normal Attack",Distance=distance,Tennokai=false,Finisher=null}},null,1,false,false,IchorCloud:true);
                        var input=Input(step);
                        if(!input.Melee!.Incarnon||DualIchorCloudMechanic.PayloadSignature(catalog,field.Source)!=DualIchorCloudMechanic.PayloadSignature(catalog,input))
                            throw new CombatRuleUnavailableException("독 구름 생성 후 공격자 버프가 바뀌었습니다. 구름의 저장/실시간 피해 갱신 방식은 원문 미기재입니다.");
                        Strike(step);Log("ichorCloudHit",$"독 구름{contact.Cloud} · 지정 명중 · 대상{enemy.Index+1}");
                    });
                }
                while(rubbleIndex<rubblePickups.Length&&rubblePickups[rubbleIndex].Time<=time+1e-9)
                {var p=rubblePickups[rubbleIndex++];var healed=rubble!.Pickup(p);Log("rubblePickup",p.MissingHealth>0?"러블 회복 · 넘친 체력은 방어력으로 전환하지 않음":"러블 방어력 · 감소2초 유예",p.MissingHealth>0?healed:rubble.Armor);}
                UpdatePendingAttackSpeed();
            }
            foreach(var destination in enemies)AtEnemy(destination,()=>{
                var i=enemy.Index;enemySums[i,0]+=enemy.DirectDamage;enemySums[i,1]+=enemy.StatusDamage;enemySums[i,2]+=enemy.Pools.Health;enemySums[i,3]+=enemy.Pools.Shields;enemySums[i,4]+=enemy.Pools.Overguard;enemySums[i,5]+=Status().Count;
                enemySums[i,6]+=enemy.DisarmApplied?1:0;enemyDeaths[i][trial]=enemy.KillTime;enemyMoments[i].Add(enemy.DirectDamage+enemy.StatusDamage);
            });
            enemy=primary;killTimes[trial]=enemy.KillTime;
            reaperMoments.Add(reaperHealing);hysteriaMoments.Add(hysteriaHealing);falloutMoments.Add(droppedOrbs);
            incarnonHealMoments.Add(healthRegen.Generated("FinisherHealing")+healthRegen.Generated("ToxinKillHealing"));
            totalDirect+=enemy.DirectDamage;totalStatus+=enemy.StatusDamage;
            damageMoments.Add(enemy.DirectDamage+enemy.StatusDamage);energyMoments.Add(brave?.Generated??0);mightMoments.Add(mightHealing);hostMightMoments.Add(hostMightHealing);heavyHealingMoments.Add(heavyHealing);nikanaHealingMoments.Add(nikanaHealing);returnMoments.Add(returnHealing);returnUpperMoments.Add(returnHealingUpper);sanctiMoments.Add(sanctiHealing);sanctiUpperMoments.Add(sanctiHealingUpper);
        }
        Array.Sort(killTimes);double? Quantile(double p){var value=killTimes[(int)Math.Ceiling(p*trials)-1];return double.IsFinite(value)?value:null;}
        var points=Enumerable.Range(0,pointCount).Select(i=>new CombatPoint(pointCount==1?0:seconds*i/(pointCount-1),sums[i,0]/trials,sums[i,1]/trials,sums[i,2]/trials,sums[i,3]/trials,sums[i,4]/trials,sums[i,5]/trials,sums[i,6]/trials,sums[i,7]/trials,sums[i,8]/trials,sums[i,9]/trials,sums[i,10]/trials,sums[i,11]/trials,build.Arcanes?.MaximumShields is null?null:sums[i,12]/trials,build.Arcanes?.MaximumShields is null?null:sums[i,13]/trials,sums[i,14]/trials,hasBrave?sums[i,15]/trials:null,hasBrave?sums[i,16]/trials:null,build.Arcanes?.PlayerArmor is not null?sums[i,17]/trials:null,sums[i,18]/trials,rapidConclusion is null?null:sums[i,19]/trials,animositySlot is null?null:sums[i,20]/trials,jadeEvents?sums[i,21]/trials:null,formDefinition is null?null:sums[i,22]/trials,sums[i,23]/trials,sums[i,24]/trials,hasSouls?sums[i,25]/trials:null,hasSouls?sums[i,26]/trials:null,tennokaiEnabled?sums[i,27]/trials:null,tennokaiEnabled?sums[i,28]/trials:null,sums[i,29]/trials,sums[i,30]/trials,sums[i,31]/trials,sums[i,32]/trials,springSlot is null&&build.WeaponId!=KeratinosMechanic.WeaponId?null:sums[i,33]/trials,hasHotShot?sums[i,34]/trials:null,powerThrow?sums[i,35]/trials:null,build.WeaponId==VinquibusState.WeaponId?sums[i,36]/trials:null,build.WeaponId==VinquibusState.WeaponId?sums[i,37]/trials:null,exalted?.Family==ExaltedFamily.Talons?sums[i,38]/trials:null,magusSlot is {Active:true}?sums[i,39]/trials:null,build.Melee.ExternalEquipment?.Warcry is not null?sums[i,40]/trials:null,sums[i,41]/trials,build.Melee.ExternalEquipment?.HallOfMirrors is not null?sums[i,42]/trials:null,sums[i,43]/trials,build.Melee.ExternalEquipment?.AtlasRubble is null?null:sums[i,44]/trials,sums[i,45]/trials,sums[i,46]/trials)).ToArray();
        progress?.Report(new("시간별 전투",trials,trials));
        SecondaryBurst[] secondary=profile.Name=="Spinnerex"?[new("스피네렉스 · 처치 폭발",spinnerActivations/trials,spinnerDamage/trials,spinnerTick/trials,10,"주변 대상 1명 · 중심·방어 적용 전 · 발동 확률 포함 · 원래 대상의 DPS·TTK 제외 · 거리 감쇠 수식 미기재")]:[];
        if(obexWaves>0)secondary=secondary.Append(new SecondaryBurst("오벡스 · 마무리 파동",obexWaves/trials,obexDamage/trials,0,12,"주변 대상1명 · 방어 적용 전 · 거리 감쇠 없음 · 실제 발생한 마무리 접촉 수 반영 · 은신 적용 여부는 위키의 잠정 기술로 별도 범위 · 원래 대상 피해에 합산하지 않음",obexStealthDamage/trials)).ToArray();
        var enemyResults=Enumerable.Range(0,enemyBuilds.Length).Select(i=>{
            var deaths=enemyDeaths[i];Array.Sort(deaths);double? Q(double p){var value=deaths[(int)Math.Ceiling(p*trials)-1];return double.IsFinite(value)?value:null;}
            return new CombatEnemyResult(i==0?"primary":settings.NearbyEnemies![i-1].Id,enemySums[i,0]/trials,enemySums[i,1]/trials,enemySums[i,2]/trials,enemySums[i,3]/trials,enemySums[i,4]/trials,enemySums[i,5]/trials,new(Q(.1),Q(.5),Q(.9),deaths.Count(double.IsFinite)/(double)trials,trials,44061,seconds),enemyMoments[i].Estimate(),exalted?.Family==ExaltedFamily.Wind?enemySums[i,6]/trials:null);
        }).ToArray();
        return new(seconds,totalDirect/trials,totalStatus/trials,new(Quantile(.1),Quantile(.5),Quantile(.9),killTimes.Count(double.IsFinite)/(double)trials,trials,44061,seconds),points,events.ToArray(),trials,notes.ToArray(),[],damageMoments.Estimate(),secondary,effects.GetValueOrDefault("CriticalLifeSteal")>0?totalCriticalHealing/trials:null,hasBrave||mightRate>0||heavyLifeSteal>0||nikanaLifeSteal>0||healingPerStatus>0||sanctiMagistar||reaperLevel>0||finisherRegen>0||toxinRegen>0||exalted?.Family==ExaltedFamily.Talons||falloutSlot is not null?new(hasBrave?energyMoments.Estimate():null,mightRate>0?mightMoments.Estimate():null,mightRate>0?hostMightMoments.Estimate():null,heavyLifeSteal>0?heavyHealingMoments.Estimate():null,healingPerStatus>0?returnMoments.Estimate():null,healingPerStatus>0?returnUpperMoments.Estimate():null,sanctiMagistar?sanctiMoments.Estimate():null,sanctiMagistar?sanctiUpperMoments.Estimate():null,reaperLevel>0?reaperMoments.Estimate():null,finisherRegen>0||toxinRegen>0?incarnonHealMoments.Estimate():null,exalted?.Family==ExaltedFamily.Talons?hysteriaMoments.Estimate():null,falloutSlot is not null?falloutMoments.Estimate():null,nikanaLifeSteal>0?nikanaHealingMoments.Estimate():null):null,enemyResults,HasSnapshotDps:!settings.ScriptedAttacksOnly&&!cobraSleep);
    }
}

