namespace TennoForge.Core;

public record RangedEncounter(double Duration=5,int Trials=256,uint Seed=73519,bool FirstShot=false,bool IncludeStatusDamage=true);
public record RangedCombatPoint(double Time,double DirectDamage,double StatusDamage,double Health,double Shields,double Overguard,double Armor,double StatusTypes,double Energy=0,double PlayerOverguard=0,double VeloxStrength=0,double PlayerHealth=0,double DischargeStored=0);
public record RangedCombatEvent(double Time,string Kind,string Detail,double Value,int Target=0);
public record RangedCombatResult(RangedCombatPoint[] Points,RangedCombatEvent[] Events,SampleEstimate Damage,SampleEstimate Dps,TtkResult Ttk,string[] Notes);

/// <summary>Per-target event simulation with shared weapon resources. A proc from a hit affects subsequent pellets, never that hit.</summary>
public static class RangedCombat
{
    private static bool IsControl(string name)=>name is "Stagger" or "Big Stagger" or "Knockdown" or "Lifted" or "Ragdoll";
    private sealed class Proc
    {
        public DamageType Type;public double End,Next,Seed,Element,Faction,StatusBonus,Duration;public double? FixedTick;public RangedResult? Source;
        public bool Headshot,Ability;public int Attack;
        public bool HasTick=>Type is DamageType.Electricity or DamageType.Gas?Next<End-1e-9:Next<=End+1e-9;
    }
    private sealed class Victim
    {
        public RangedNullifierField? Nullifier;public required RangedTargetState State;public List<Proc> Procs=[];public int Index,Hits,MetamorphicHits;
        public double ForwardFrom,ForwardTo;
        public double Forward(double time)=>MoveEnd>MoveStart?ForwardFrom+(ForwardTo-ForwardFrom)*Math.Clamp((time-MoveStart)/(MoveEnd-MoveStart),0,1):ForwardTo;
        public double PositionFrom,PositionTo,MoveStart,MoveEnd;public int PullGrenade=-1;
        public double Position(double time)=>MoveEnd>MoveStart?PositionFrom+(PositionTo-PositionFrom)*Math.Clamp((time-MoveStart)/(MoveEnd-MoveStart),0,1):PositionTo;
        public double HeatFloor;public double HeatStart=-1,HeatEnd=-1,HeatDuration=6,FrozenEnd=-1,PostureUntil=-1,RagdollUntil=-1;public bool Microwave,Killed,WeaponTagged,Disarmed,DisarmUnknownLogged;
    }
    public static RangedCombatResult Simulate(RangedBuild build,RangedEncounter settings,CancellationToken cancellation=default,IProgress<SimulationProgress>? progress=null)
    {
        build=RangedApproximations.Resolve(build);
        if(!double.IsFinite(settings.Duration)||settings.Duration<=0||settings.Duration>300||settings.Trials is <1 or >10000||settings.Seed==0)throw new ArgumentException("교전 시간·표본 수·시드 범위 오류");
        if(build.Enemy is not {} selection)throw new ArgumentException("교전 대상을 선택하세요.");
        var weapon=RangedEngine.Resolve(build);var attack=RangedEngine.Attacks(weapon)[build.AttackIndex];
        RangedCollisions.Validate(build);RangedClusters.Validate(build,true);var initial=RangedEngine.Calculate(build);var cadence=initial.Cadence!;
        var syndicate=RangedSyndicates.Resolve(weapon,initial.Modifiers!);
        var arcane=RangedArcanes.Resolve(build,weapon);var passive=RangedPassives.Get(weapon.Id);var incarnon=RangedIncarnons.Get(weapon.Id);
        if(initial.TimingIssues.Length>0)throw new CombatRuleUnavailableException(string.Join("\n",initial.TimingIssues));
        var forced=attack.TryGetProperty("ForcedProcs",out var forcedJson)?forcedJson.EnumerateArray().Select(p=>p.GetString()!).ToArray():[];
        if(forced.Any(p=>!IsControl(p)&&!Enum.TryParse<DamageType>(p,out _)))throw new CombatRuleUnavailableException("특수 강제 상태의 지속·중첩 규칙을 연결해야 합니다: "+string.Join(", ",forced));
        if(RangedPassives.Get(weapon.Id)?.Kind=="Cedo"&&build.AttackIndex==1&&!settings.FirstShot&&build.Conditions?.GlaiveReturnSeconds is null)throw new CombatRuleUnavailableException("반복 글레이브의 회수 시간을 설정하세요. 위키는 약 5초로만 기재하며 정확한 고정 시간은 없습니다.");
        if(passive?.Kind=="Onos"&&(build.AttackIndex==1||build.AutoTransform)&&build.Conditions?.ChargeCycleSeconds is null)throw new CombatRuleUnavailableException("오노스 충전 시간은 위키에 별도 기재되어 있지 않습니다. 모드 적용 후 충전 시간 가정을 설정하면 충전 중 타격과 완충 발사를 함께 계산합니다.");
        var spontaneous=RangedSingularity.Equipped(build);var simulorWeapon=(RangedSimulor.Applies(weapon)||RangedOrdinarySimulor.Applies(weapon))&&!spontaneous;
        var recall=RangedRecall.Get(weapon.Id);
        var returningBlades=RangedReturningBlades.Get(weapon.Id);
        var reloadGrenade=RangedReloadGrenades.Get(weapon.Id);
        if(reloadGrenade is not null&&build.AttackIndex>0&&!settings.FirstShot)throw new CombatRuleUnavailableException("재장전 유탄을 반복해서 계산하려면 기본 사격을 선택하세요. 유탄 구성요소는 첫 발사 조건에서 한 번 계산할 수 있습니다.");
        var alternateRule=RangedAlternates.Get(weapon.Id);
        var defense=EnemyScaling.Calculate(selection);var enemy=EnemyCatalog.Load().Get(selection.EnemyId);
        int count=101;var sums=new double[count,12];var times=new double[settings.Trials];Array.Fill(times,double.PositiveInfinity);
        var runtimeApproximations=new HashSet<string>();
        var damageMoments=new SampleMoments();var dpsMoments=new SampleMoments();var events=new List<RangedCombatEvent>();uint state=settings.Seed;
        double Random(){state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296d;}
        int Roll(double value)=>(int)Math.Floor(value)+(Random()<value-Math.Floor(value)?1:0);
        for(int trial=0;trial<settings.Trials;trial++)
        {
            cancellation.ThrowIfCancellationRequested();
            var syndicateMeter=syndicate is {} sr?new RangedSyndicateMeter(sr.Points,build.Conditions?.SyndicateCharge??0):null;bool syndicatePending=false;double syndicateUntil=-1;
            var shotTargets=new Dictionary<int,HashSet<int>>();double unseenUntil=-1,punchBuffUntil=build.Conditions?.PunchThroughBuffSeconds??0;
            double hemaAt=double.PositiveInfinity,hemaCompletion=-1,hemaRounds=0,hemaBonus=build.Conditions?.HemaCriticalBonus??0;
            double dischargeStored=build.Conditions?.DischargeStored??0,dischargeAmmo=0;bool manualDischarge=false,manualSnare=false,manualCube=false;var dischargeSnapshots=new Dictionary<int,double>();var dissolvedProjectiles=new Dictionary<int,int>();var collectedOrbs=new HashSet<(int Shot,int Attack)>();int activeGroundSpear=-1;var pullCenters=new Dictionary<int,double>();
            var adhesiveLaunchTimes=new Dictionary<int,double>();double tendrilStarted=double.NaN;int tendrilAim=-1;var grenadeRack=new RangedGrenadeRack();var grenadePositions=new Dictionary<int,RangedGrenadeEvent>();var grenadeEvents=new PriorityQueue<RangedGrenadeEvent,(double Time,int Order)>();foreach(var (entry,index) in (build.Conditions?.GrenadeEvents??[]).Select((entry,index)=>(entry,index)))grenadeEvents.Enqueue(entry,(entry.Time,index));var manualGrenades=RangedPentaAugments.Applies(weapon)&&build.Conditions?.ManualGrenades==true;
            var singularityEnds=new List<double>();var singularityPulses=new PriorityQueue<(RangedSingularityPulse Pulse,int Origin),(double Time,int Order)>();int singularityOrder=0;
            var azimaDiscs=new List<RangedAzimaDisc>();bool azimaHit=false,manualAzima=false;int azimaOrder=0;
            var residualPools=new RangedResidualPools();foreach(var end in build.Conditions?.Residual?.OtherKitgunPoolEnds??[])residualPools.AddExternal(end);
            var residualPulses=new PriorityQueue<(int Pool,double Center,double Scale),(double Time,int Order)>();int residualOrder=0;
            var seekers=new PriorityQueue<(int Target,int Shot),(double Time,int Order)>();int seekerOrder=0;
            var zymosEvents=new PriorityQueue<(int Kind,int Shot,int Host,int Contacts,int Target,double Distance,bool Headshot,RangedBurrowTick? Tick),(double Time,int Order)>();int zymosOrder=0;
            var voltageTraps=new PriorityQueue<(double Center,double End,int Tick,double Start,int Cap,double Damage),(double Time,int Order)>();int trapOrder=0;
            var orbInteractions=new PriorityQueue<RangedOrbInteraction,(double Time,int Order)>();foreach(var (entry,index) in (build.Conditions?.OrbInteractions??[]).Select((entry,index)=>(entry,index)))orbInteractions.Enqueue(entry,(entry.Time,index));
            var infested=new RangedInfestedOrbState();bool manualInfested=false;var crossedOrbs=new Dictionary<int,List<int>>();var infestedShotPaths=new Dictionary<int,(double Time,double X,double Y,double Speed,int Contacts,int Target)>();var crossedOrbIds=new HashSet<(int Shot,int Orb)>();var orbCrossings=new PriorityQueue<(int Orb,int Shot,int Contacts,int Target),(double Time,int Order)>();int crossingOrder=0;
            var cubes=new RangedQuantaState();var simulor=new RangedSimulorState(RangedOrdinarySimulor.Applies(weapon)?build.Conditions?.OrdinarySimulor??new():null);var stug=new RangedStugState();
            var blobArrivals=new PriorityQueue<(int Stacks,int Shot,int Target,bool Form),(double Time,int Order)>();int blobOrder=0;
            var victims=Enumerable.Range(0,build.Conditions?.GroupCount??1).Select(i=>new Victim{Index=i,PositionFrom=i*(build.Conditions?.GroupSpacing??2),PositionTo=i*(build.Conditions?.GroupSpacing??2),State=new RangedTargetState(defense.Health*(build.Conditions?.TargetHealthFraction??1),defense.Shields,defense.Overguard,defense.Armor,enemy.Faction)}).ToArray();
            var nullifierScenario=build.Conditions?.Nullifier??new(Kind:RangedNullifiers.EnemyKind(enemy.Id));
            if(nullifierScenario.Kind!="None")foreach(var v in victims)v.Nullifier=new(nullifierScenario);
            var victim=victims[0];var target=victim.State;var procs=victim.Procs;double time=0,direct=0,dot=0;int shotIndex=0,impactComponent=build.AttackIndex;bool tendrilHit=false;bool impactHeadshot=build.Conditions?.Headshot==true;
            double PlayerDistance(int index){var p=TargetPoint(victims[index]);return Math.Sqrt(p.X*p.X+p.Y*p.Y);}
            double Between(int first,int second){var a=TargetPoint(victims[first]);var z=TargetPoint(victims[second]);return Math.Sqrt(Math.Pow(a.X-z.X,2)+Math.Pow(a.Y-z.Y,2));}

            void SelectVictim(Victim value){victim=value;target=value.State;procs=value.Procs;}
            double chargeStarted=double.NaN,chargeEnds=double.PositiveInfinity;int chargeCycles=0,chargeDamageStacks=build.Conditions?.ChargeDamageStacks??0;var chargeSnapshots=new Dictionary<int,int>();
            var doubleTapStacks=build.Conditions?.DoubleTapStacks??0;double doubleTapAt=-(build.Conditions?.DoubleTapAge??0);
            var alternateCharges=alternateRule is null?0:RangedAlternates.Charges(alternateRule,build.Conditions?.AlternateCharge??1);var alternateShotCharges=new Dictionary<int,int>();var buckshotReady=build.Conditions?.BuckshotReady??false;var buckshotSnapshots=new Dictionary<int,bool>();var barrageReady=build.Conditions?.SentientBarrageReady??false;var barrageSnapshots=new Dictionary<int,bool>();double alternateNormalAmmo=0;bool manualAlternate=false;double blockUntil=-1,nextBlockTick=double.PositiveInfinity;
            var kills=build.Conditions?.Kills??0;var headshotKills=build.Conditions?.HeadshotKills??0;double lastKill=-1;double cantareUntil=build.Conditions?.CantareBuffSeconds??0;double paxBoltUntil=-1;var mutation=build.Conditions?.CriticalMutationBonus??0;var dodgeShots=build.Conditions?.DodgeShotsRemaining??0;var dodgeSnapshots=new Dictionary<int,int>();var chargedSalvos=new Dictionary<int,int>();var komorexLastTargets=new Dictionary<int,int>();int frenzyKills=0,frenzyShots=0;double frenzySpent=0;
            var killExplosions=new PriorityQueue<(int Origin,Dictionary<DamageType,double> Damage,double Radius),(double Time,int Order)>();int killExplosionOrder=0;
            if(victims.All(v=>v.State.Health<=0))times[trial]=0;
            var arcaneStacks=build.ArcaneStacks;double arcaneRefreshed=-(build.Conditions?.ArcaneAge??build.Conditions?.Elapsed??0),lastEncumber=-1,lastEnervate=-1,lastBigCrit=-1;int bigCrits=0;
            var spool=RangedSpool.Get(weapon.Id);var spoolStart=build.Conditions?.SpoolShots??(build.AttackIndex==1&&spool is not null?spool.PelletSpoolMaximum-1:0);
            var fire=new RangedFireState(cadence);var ammo=new RangedAmmo(weapon,initial,build.Conditions?.ReserveAmmo,build.Conditions?.ReplenishReserve??false);var currentAttack=build.AttackIndex;double incarnonGauge=0;double normalAmmo=RangedIncarnons.IsForm(weapon,currentAttack)?RangedEngine.Calculate(build with{AttackIndex=0,IncarnonAmmo=null}).Cadence!.Magazine:fire.Ammo;
            alternateNormalAmmo=alternateRule is null?fire.Ammo:RangedEngine.Calculate(build with{AttackIndex=alternateRule.Primary[0]}).Cadence!.Magazine;
            double alternateReadyAt=45*(1-(build.Conditions?.AlternateCharge??1));int orbId=-1;
            var orbs=new PriorityQueue<(double Spawn,int Pulse,int Origin,int Shot,RangedBuild Snapshot),(double Time,int Order)>();int orbOrder=0;
            if(passive?.Kind=="Grimoire"&&currentAttack==2)fire.WaitUntil(alternateReadyAt);
            void LaunchOrb(RangedBuild snapshot)
            {
                if(time+1e-9<alternateReadyAt)throw new ArgumentException("그리모어 보조 공격 게이지가 아직 충전되지 않았습니다.");
                alternateReadyAt=time+45;var id=orbId--;
                snapshot=snapshot with{AttackIndex=2,Distance=0};
                for(int pulse=0;pulse<6;pulse++)orbs.Enqueue((time,pulse,victim.Index,id,snapshot),(time+(build.Conditions?.OrbFirstPulseDelay??0)+pulse,orbOrder++));
                orbs.Enqueue((time,6,victim.Index,id,snapshot),(time+6,orbOrder++));Log("orbLaunch","그리모어 구체 · 6초",id);if(arcane?.Code=="Surge")arcaneStacks=0;
            }
            var weaponStacks=build.Conditions?.WeaponBuffStacks??0;double weaponRefreshed=-(build.Conditions?.WeaponBuffAge??0);var comboHits=build.Conditions?.SniperComboHits??0;double comboAt=0;
            var evoHeadshots=build.Conditions?.EvolutionHeadshotStacks??0;double evoHeadshotAt=-(build.Conditions?.EvolutionHeadshotAge??0);var attrition=build.Conditions?.AttritionStacks??0;double attritionAt=-(build.Conditions?.AttritionAge??0);var momentum=build.Conditions?.MomentumStacks??0;var chamber=build.Conditions?.DualModePunchThrough??false;double lingeringUntil=build.Conditions?.LingeringSeconds??0,lastPairedHeadshot=-100;double processedReload=-1;
            var evolutionRules=RangedEvolutionEvents.Selected(build).ToArray();
            var evolutionStates=(build.Conditions?.EvolutionStates??[]).ToDictionary(p=>p.Key,p=>(Stacks:p.Value.Stacks,At:-p.Value.Age));
            var firstMagazineShots=new HashSet<int>();var finalBurstShots=new HashSet<int>();var sniperComboDuration=(passive?.ComboDuration??0)+(build.Conditions?.Aiming!=false?initial.Modifiers!.GetValueOrDefault("SniperComboDuration"):0);var doubledFieldShots=new HashSet<int>();var missedShots=new HashSet<int>();var magazineHits=new HashSet<int>();var previousMagazineHits=build.Conditions?.PreviousMagazineHits??0;
            var shotOrigins=new Dictionary<int,(int Attack,int Target)>();
            var shotBursts=new Dictionary<int,(int Group,int Size,int Attack)>();var burstContacts=new Dictionary<int,HashSet<int>>();int burstGroup=0;var burstHeadContacts=new Dictionary<int,int>();
            var externalEvents=new Queue<RangedExternalEvent>((build.Conditions?.ExternalEvents??[]).OrderBy(e=>e.Time));
            double energySpent=0,abilityAt=-(build.Conditions?.AbilityAge??0),currentEnergy=build.Conditions?.CurrentEnergy??0;var recentAbility=build.Conditions?.RecentAbility??false;
            var recentReload=build.Conditions?.RecentReload??false;var completedReloadAt=-(build.Conditions?.ReloadAge??0);var completedRounds=build.Conditions?.RoundsReloaded??0;
            double playerOverguard=0,energyClock=0;int veloxHits=build.Conditions?.VeloxHits??0;
            void VeloxHit(){if(initial.Modifiers!.GetValueOrDefault("VeloxStrengthPerHit")<=0)return;veloxHits=Math.Min(100,veloxHits+1);Log("veloxStack","벨록스 컨클루전 · 다음 시전 위력",veloxHits*initial.Modifiers!["VeloxStrengthPerHit"]);}
            var overshields=build.Conditions?.Overshields??false;var shieldDeficit=build.Conditions?.PlayerShieldDeficit??0;var playerHealth=build.Conditions?.PlayerHealth??300;var healingProgress=build.Conditions?.StatusHealingProgress??0;
            var holstered=false;var holsterEnd=double.PositiveInfinity;var nextHolsterTick=double.PositiveInfinity;var holsterReload=new RangedHolsterReload();
            var firingStacks=build.Conditions?.FiringStacks??0;var emptyReloadStacks=build.Conditions?.EmptyReloadStacks??0;
            double MaxEnergy()=>(build.Conditions?.MaxEnergy??0)+(syndicate?.Code=="Entropy"&&time<syndicateUntil?.25*(build.Conditions?.PlayerBaseEnergy??0):0);
            double MaxHealth()=>(build.Conditions?.PlayerMaxHealth??300)+(syndicate?.Code=="Purity"&&time<syndicateUntil?.25*(build.Conditions?.PlayerBaseHealth??0):0);
            void AdvanceEnergy(double until)
            {
                if(until<=energyClock)return;
                if(build.Conditions?.Channeling==true){energyClock=until;return;}
                var start=Math.Max(energyClock,recentAbility?abilityAt+1:energyClock);
                var cuts=new SortedSet<double>{start,until};
                foreach(var (id,rule) in evolutionRules.Where(p=>p.Rule.Effects.GetValueOrDefault("EnergyRegen")>0))
                    if(evolutionStates.TryGetValue(id,out var value)&&rule.Duration>0&&value.At+rule.Duration>start&&value.At+rule.Duration<until)cuts.Add(value.At+rule.Duration);
                if(arcane?.Code=="Exhilarate")for(int n=1;n<=arcaneStacks;n++){var end=arcaneRefreshed+arcane.Duration*n;if(end>start&&end<until)cuts.Add(end);}
                var points=cuts.ToArray();double restored=0;
                if(start<until)for(int i=1;i<points.Length;i++)
                {
                    var middle=(points[i]+points[i-1])/2;double regen=0;
                    foreach(var (id,rule) in evolutionRules)
                        if(rule.Effects.GetValueOrDefault("EnergyRegen")>0&&evolutionStates.TryGetValue(id,out var value))regen+=rule.Effects["EnergyRegen"]*RangedEvolutionEvents.Active(rule,new(value.Stacks,Math.Max(0,middle-value.At)));
                    if(arcane?.Code=="Exhilarate")regen+=.2*(build.ArcaneRank+1)*Math.Max(0,arcaneStacks-(int)Math.Floor(Math.Max(0,middle-arcaneRefreshed)/arcane.Duration));
                    restored+=regen*(points[i]-points[i-1]);
                }
                currentEnergy=Math.Min(MaxEnergy(),currentEnergy+restored);energyClock=until;
            }
            void EvolutionEvent(string kind)
            {
                foreach(var (id,rule) in evolutionRules)
                {
                    if(rule.RequiresStalkerSet&&build.Conditions?.StalkerSet!=true)continue;
                    if(kind=="Miss"&&rule.ResetOnMiss){evolutionStates.Remove(id);continue;}
                    if(kind=="Bodyshot"&&rule.ResetOnBodyshot){evolutionStates.Remove(id);continue;}
                    if(kind=="BlastDetonation"&&rule.ResetOnBlast){evolutionStates.Remove(id);continue;}
                    if(rule.Event!=kind||rule.NormalOnly&&RangedIncarnons.IsForm(weapon,currentAttack))continue;
                    if(Random()>=rule.Chance)continue;
                    var previous=evolutionStates.GetValueOrDefault(id);
                    var stacks=RangedEvolutionEvents.Active(rule,new(previous.Stacks,time-previous.At));
                    if(stacks>0&&!rule.Refreshable)continue;
                    evolutionStates[id]=(Math.Min(rule.Cap,stacks+1),time);
                    if(rule.Effects.GetValueOrDefault("RestoreEnergy")>0){var oldEnergy=currentEnergy;currentEnergy=Math.Min(MaxEnergy(),currentEnergy+rule.Effects["RestoreEnergy"]);Log("energyRestore","에너지 회복",currentEnergy-oldEnergy);}
                    if(rule.Effects.GetValueOrDefault("RestoreShields")>0){var amount=rule.Effects["RestoreShields"];if(amount>shieldDeficit&&(build.Conditions?.PlayerMaxShields??300)>0)overshields=true;shieldDeficit=Math.Max(0,shieldDeficit-amount);Log("restoreShields","실드 회복 · 남는 양은 오버실드",amount);}
                    if(!RangedIncarnons.IsForm(weapon,currentAttack))
                    {
                        var capacity=RangedEngine.Calculate(Current()).Cadence!.Magazine;
                        if(rule.Effects.GetValueOrDefault("ReserveFraction")>0)
                        {
                            var amount=ammo.Maximum*rule.Effects["ReserveFraction"];
                            if(Math.Abs(amount-Math.Round(amount))>1e-9)throw new CombatRuleUnavailableException("최대 예비 탄약 비례 회복의 소수 탄수 처리 규칙이 원문에 없습니다.");
                            Log("ammoPickup","최대 예비 탄약 비례 회복",ammo.Pickup(Math.Round(amount)));
                        }
                        if(rule.Effects.GetValueOrDefault("ReservePickup")>0)Log("ammoPickup","예비 탄약 획득",ammo.Pickup(rule.Effects["ReservePickup"]));
                        var requested=rule.Effects.GetValueOrDefault("InstantReload")>0?capacity-fire.Ammo:rule.Effects.GetValueOrDefault("RestoreMagazine");
                        if(requested>0){var restored=ammo.Spend(Math.Min(requested,capacity-fire.Ammo));if(restored>0)fire.InstantReload(fire.Ammo+restored,time);Log("ammoRestore","예비 탄약에서 탄창 회복",restored);}
                    }
                    Log("evolution",kind+" · "+RangedEvolutionEvents.DisplayName(build,id),Math.Min(rule.Cap,stacks+1));
                }
            }
            void ArcaneStack()
            {
                if(arcane is null)return;
                if(arcane.Duration>0&&time-arcaneRefreshed>=arcane.Duration)arcaneStacks=arcane.Code is "Merciless" or "Deadhead" or "Dexterity" or "Exhilarate"?Math.Max(0,arcaneStacks-(int)Math.Floor((time-arcaneRefreshed)/arcane.Duration)):0;
                arcaneStacks=Math.Min(arcane.Cap,arcaneStacks+1);arcaneRefreshed=time;Log("arcaneStack",arcane.Code,arcaneStacks);
            }
            void Approximate(string detail){runtimeApproximations.Add("근사 · "+detail);Log("approximation",detail);}
            void Log(string kind,string detail,double value=0){if(trial==0)events.Add(new(time,kind,detail,value,victim.Index));}
            Dictionary<DamageType,int> Status()=>procs.Where(p=>p.End>time+1e-9).GroupBy(p=>p.Type).ToDictionary(g=>g.Key,g=>g.Count());
            double HeatStrip()
            {
                if(victim.HeatStart<0)return 0;
                var peak=Math.Max(victim.HeatFloor,EncounterEngine.HeatStrip(Math.Max(0,Math.Min(time,victim.HeatEnd)-victim.HeatStart),victim.HeatDuration/6));
                if(time<=victim.HeatEnd)return peak;
                return Math.Min(peak,(int)Math.Floor((time-victim.HeatEnd)/1.5) switch{0=>.5,1=>.4,2=>.3,3=>.15,_=>0});
            }
            double lastHeadshot=-1;
            RangedBuild Current()
            {
                var c=build.Conditions??new();
                var currentRaw=RangedEngine.Attacks(weapon)[currentAttack];var currentCost=RangedEngine.N(currentRaw,"AmmoCost")??1;var currentBurst=RangedEngine.N(currentRaw,"BurstCount")??1;

                return build with {Distance=c.TargetsInLine||weapon.Id=="wiki:primary/Paracyst"?PlayerDistance(victim.Index):build.Distance,AttackIndex=currentAttack,IncarnonAmmo=null,ArcaneStacks=arcaneStacks,Conditions=c with {InfiniteAmmoSeconds=Math.Max(0,c.InfiniteAmmoSeconds-time),DischargeStored=dischargeStored,BuckshotReady=buckshotReady,SentientBarrageReady=barrageReady,Invisible=c.Invisible||time<unseenUntil,HemaCriticalBonus=hemaBonus,LoadedAmmo=fire.Ammo>0?fire.Ammo:null,CantareBuffSeconds=Math.Clamp(cantareUntil-time,0,c.JadeEnsemble?60:30),DodgeShotsRemaining=dodgeShots,CriticalMutationBonus=mutation,ChargeDamageStacks=chargeDamageStacks,AlternateCharge=alternateRule is null?c.AlternateCharge:alternateCharges/(double)alternateRule.Capacity,Headshot=impactHeadshot,DoubleTapStacks=doubleTapStacks,DoubleTapAge=time-doubleTapAt,Kills=kills,HeadshotKills=headshotKills,Statuses=Status(),Overshields=overshields,PlayerShieldDeficit=shieldDeficit,RecentAbility=recentAbility,CurrentEnergy=currentEnergy,PreviousMagazineHits=previousMagazineHits,PlayerHealth=playerHealth,PlayerMaxHealth=MaxHealth(),MaxEnergy=MaxEnergy(),PlayerArmor=c.PlayerArmor+(syndicate?.Code=="Justice"&&time<syndicateUntil?.15*(c.PlayerBaseArmor??0):0),SprintSpeed=c.SprintSpeed+(syndicate?.Code=="Blight"&&time<syndicateUntil?.1:0),StatusHealingProgress=healingProgress,EvolutionStates=evolutionStates.ToDictionary(p=>p.Key,p=>new RangedEvolutionState(p.Value.Stacks,time-p.Value.At)),SpoolShots=spool is null?c.SpoolShots:spoolStart+fire.ShotIndex,TargetUndamaged=c.TargetUndamaged&&target.Health>=defense.Health&&target.Shields>=defense.Shields,FiringStacks=firingStacks,EmptyReloadStacks=emptyReloadStacks,EvolutionHeadshotStacks=evoHeadshots,EvolutionHeadshotAge=time-evoHeadshotAt,AttritionStacks=attrition,AttritionAge=time-attritionAt,MomentumStacks=momentum,DualModePunchThrough=chamber,LingeringSeconds=Math.Max(0,lingeringUntil-time),PunchThroughBuffSeconds=Math.Max(0,punchBuffUntil-time),WeaponBuffStacks=weaponStacks,WeaponBuffAge=time-weaponRefreshed,SniperComboHits=Math.Max(0,comboHits-(sniperComboDuration>0?(int)Math.Floor((time-comboAt)/sniperComboDuration):0)),HiddenStatusCount=c.HiddenStatusCount+(victim.Microwave?1:0)+(target.Overguard==0&&victim.PostureUntil>time?1:0),ArcaneAge=time-arcaneRefreshed,Elapsed=lastKill>=0?time-lastKill:c.Elapsed+time,RecentHeadshot=c.RecentHeadshot||lastHeadshot>=0,HeadshotAge=lastHeadshot>=0?time-lastHeadshot:c.HeadshotAge+time,RecentReload=recentReload,ReloadAge=time-completedReloadAt,RoundsReloaded=completedRounds,AbilityAge=time-abilityAt,JumpAge=c.JumpAge+time,TargetHealthFraction=target.Health/defense.Health,FinalBurst=fire.Ammo<=Math.Max(1,currentBurst-fire.BurstShot)*currentCost,FinalShot=RangedMagazineCharge.Applies(weapon,currentAttack)?fire.Ammo<=RangedMagazineCharge.Rounds(weapon,c with{Invisible=c.Invisible||time<unseenUntil,HemaCriticalBonus=hemaBonus,LoadedAmmo=fire.Ammo>0?fire.Ammo:null},initial.Modifiers!):fire.Ammo<=currentCost}};
            }
            void GainAffinity(double amount)
            {
                if(syndicateMeter is null)return;
                var gained=amount*(build.Conditions?.AffinityMultiplier??1);
                if(syndicateMeter.Gain(gained,time,holstered))syndicatePending=true;
                Log("weaponAffinity","무기 숙련도 · 신디케이트 게이지",syndicateMeter.Affinity/syndicateMeter.Threshold);
            }
            void ExpireSyndicate()
            {
                if(syndicate?.Code=="Purity")playerHealth=Math.Max(0,playerHealth-.25*(build.Conditions?.PlayerBaseHealth??0));
                syndicateUntil=-1;currentEnergy=Math.Min(MaxEnergy(),currentEnergy);
                Log("syndicateEnd","신디케이트 능력치 증가 종료");
            }
            void SyndicateExplosion()
            {
                syndicatePending=false;var rule=syndicate!.Value;var c=build.Conditions??new();
                if(rule.Code=="Justice"&&c.JusticeStatus=="Unspecified")throw new CombatRuleUnavailableException("Justice의 Blast 강제 상태는 위키 문서 간 내용이 다릅니다. 신디케이트 설정에서 두 원문 중 계산 기준을 선택하세요.");
                if(rule.Code=="Purity"&&c.PlayerBaseHealth is null)throw new CombatRuleUnavailableException("Purity의 회복·최대 체력 증가를 계산할 기본 체력을 입력하세요.");
                if(rule.Code is "Entropy" or "Blight"&&c.MaxEnergy>0&&c.PlayerBaseEnergy is null)throw new CombatRuleUnavailableException("신디케이트 에너지 회복은 랭크 0 에너지 기준입니다. 기본 에너지를 입력하세요.");
                if(rule.Code=="Justice"&&c.PlayerArmor>0&&c.PlayerBaseArmor is null)throw new CombatRuleUnavailableException("Justice의 방어력 증가를 계산할 기본 방어력을 입력하세요.");
                if(rule.Code=="Sequence"&&c.PlayerBaseShields is null)throw new CombatRuleUnavailableException("Sequence의 실드 증가를 계산할 기본 실드를 입력하세요.");
                syndicateUntil=time+30;
                if(rule.Code is "Justice" or "Truth" or "Purity")
                {
                    var before=playerHealth;playerHealth=Math.Min(MaxHealth(),playerHealth+.25*(rule.Code=="Purity"?c.PlayerBaseHealth!.Value:c.PlayerMaxHealth));Log("healing","신디케이트 체력 회복",playerHealth-before);
                }
                if(rule.Code is "Entropy" or "Blight")
                {
                    var before=currentEnergy;currentEnergy=Math.Min(MaxEnergy(),currentEnergy+.25*(c.PlayerBaseEnergy??0));Log("energyRestore","신디케이트 · 랭크 0 에너지 기준 회복",currentEnergy-before);
                }
                if(rule.Code=="Sequence")
                {
                    var restored=.25*(c.PlayerMaxShields+.5*c.PlayerBaseShields!.Value);shieldDeficit=Math.Max(0,shieldDeficit-restored);Log("restoreShields","시퀀스 · 최대 실드 증가 후 회복",restored);
                }
                Log("syndicate","신디케이트 "+rule.Code+" · 반경 25m / 30초 재사용 대기",1000*(1+c.Roar));
                Log("syndicateBuff",rule.Code switch{"Justice"=>"기본 방어력 +15% · 30초","Truth"=>"파쿠르 속도·에임 글라이드·벽 매달리기 지속 +25% · 30초","Entropy"=>"기본 최대 에너지 +25% · 30초","Sequence"=>"기본 최대 실드 +50% · 30초","Blight"=>"이동 속도 +10% · 30초",_=>"기본 최대 체력 +25% · 30초"},30);
                var beforeVictim=victim;var beforeHead=impactHeadshot;impactHeadshot=false;
                // Ability source: weapon mods, faction mods, Eclipse and weapon kill effects are excluded.
                var source=initial with{BaseDamage=1000,StatusBaseFactor=1,FactionFactor=1+c.Roar,Modifiers=[]};
                foreach(var other in victims.Where(v=>v.State.Health>0&&PlayerDistance(v.Index)<=25+1e-9).ToArray())
                {
                    SelectVictim(other);Damage(new(){[rule.Type]=1000},1+c.Roar,radial:true,ability:true);
                    if(rule.Code!="Justice"||c.JusticeStatus=="Blast")Add(rule.Type,source,1,1,ability:true);
                    if(rule.Code=="Justice"&&target.Overguard==0)Log("finisherOpening","Justice · 마무리 공격 가능 (지속 시간 원문 미기재)");
                }
                SelectVictim(beforeVictim);impactHeadshot=beforeHead;
            }
            int FlatExplosionCount(RangedResult result,int contacts,bool surface)
            {
                var chance=RangedFlatExplosions.Chance(weapon,result,surface);if(chance<=0)return 0;
                if(surface&&RangedFlatExplosions.IsConcealed(result)&&!RangedFlatExplosions.SurfaceStops(weapon,impactComponent,result,build.Conditions??new()))return 0;
                int n=0;for(int i=0;i<contacts;i++)if(Random()<chance)n++;return n;
            }
            void FlatExplosions(RangedResult result,int amount)
            {
                var origin=victim;var previousHead=impactHeadshot;impactHeadshot=false;
                var concealed=RangedFlatExplosions.IsConcealed(result);var radius=RangedFlatExplosions.Radius(result);
                var faction=result.FactionFactor;
                for(int n=0;n<amount;n++)
                {
                    Log("flatExplosion",concealed?"컨실드 익스플로시브 · 지형 폭발":"썬더볼트 · 화살 폭발",250*faction);
                    foreach(var other in victims.Where(v=>v.State.Health>0&&Math.Sqrt(Math.Pow(Between(v.Index,origin.Index),2)+Math.Pow(build.Conditions?.RadialDistance??0,2))<=radius+1e-9).ToArray())
                    {
                        if(concealed&&(build.Conditions?.ExplosionObstructedTargets??[]).Contains(other.Index))continue;
                        SelectVictim(other);Damage(new(){[DamageType.Blast]=250},faction,radial:true);EvolutionEvent("Hit");
                        if(weapon.Id=="wiki:primary/Evensong")for(int p=0;p<7;p++)Add(DamageType.Puncture,result,1,1);
                        if(arcane?.Code=="Enervate"&&time-lastEnervate>=1/30d-1e-9){ArcaneStack();lastEnervate=time;}
                    }
                }
                SelectVictim(origin);impactHeadshot=previousHead;
            }
            void Cannonade(RangedResult source,int contacts,bool radial)
            {
                var chance=source.Modifiers!.GetValueOrDefault("CannonadeChance");if(chance<=0||contacts<=0)return;var c=build.Conditions?.Cannonade;
                if(radial&&(c?.RadialTriggers??throw new CombatRuleUnavailableException("캐노네이드의 방사형 공격 발동 여부가 원문에 없습니다. 발동 시나리오를 지정하세요."))==false)return;
                var origin=victim;var head=impactHeadshot;impactHeadshot=false;
                for(int n=0;n<contacts;n++)if(Random()<chance)
                {
                    Log("cannonade","캐노네이드 · 독립 폭발",250);
                    foreach(var other in victims.Where(v=>v.State.Health>0&&Between(v.Index,origin.Index)<=10+1e-9).ToArray())
                    {var distance=Math.Sqrt(Math.Pow(Between(other.Index,origin.Index),2)+Math.Pow(build.Conditions?.RadialDistance??0,2));var output=RangedCannonade.Output(c,distance);SelectVictim(other);Damage(new(){[DamageType.Blast]=output},1,radial:true);Log("cannonadeHit","지정 감쇠·배율 적용 후 방어 전 피해",output);}
                }
                SelectVictim(origin);impactHeadshot=head;
            }
            int CollectShotOrb(int component,int shot,int contacts,bool radial)
            {
                if(contacts<=0||collectedOrbs.Contains((shot,component)))return contacts;
                var orb=(build.Conditions?.OrbShots??[]).SingleOrDefault(o=>o.Shot==shot&&(o.Attack<0?build.AttackIndex:o.Attack)==component);if(orb is null)return contacts;
                var source=RangedEngine.Calculate(Current() with{AttackIndex=component});
                var health=source.Modifiers!.GetValueOrDefault("HealthOrbShotBonus")>0;var energy=source.Modifiers!.GetValueOrDefault("EnergyOrbShotBonus")>0;
                if(!health&&!energy)throw new ArgumentException("오브 사격에는 멘딩 샷 또는 에너자이징 샷이 필요합니다.");
                if(RangedEngine.S(weapon.Raw,"Family")=="Ferrox"&&RangedFields.Get(weapon.Id)?.Child==component)throw new ArgumentException("페록스의 투척 장판은 체력 오브를 회수하지 않습니다.");
                if(radial&&orb.RadialDistance>source.Modifiers!.GetValueOrDefault("EffectiveRange"))return contacts;
                if(health&&playerHealth>=MaxHealth()||energy&&currentEnergy>=MaxEnergy()){Log("orbFull","해당 자원이 가득 차 오브를 회수하지 않음");return contacts;}
                var projectile=RangedEngine.S(RangedEngine.Attacks(weapon)[component],"ShotType")=="Projectile";
                if(!radial&&!projectile&&orb.HitscanContinues is null)throw new CombatRuleUnavailableException("히트스캔의 오브 통과 여부는 무기별 원문 수치가 없습니다. 오브 사격에서 기준을 지정하세요.");
                collectedOrbs.Add((shot,component));var amount=RangedOrbPickups.Amount(orb,health,source);
                if(health){var before=playerHealth;playerHealth=Math.Min(MaxHealth(),playerHealth+amount);Log("shotHealthOrb","멘딩 샷 · 오브 회수",playerHealth-before);EvolutionEvent("HealthOrbPickup");}
                else{var before=currentEnergy;currentEnergy=Math.Min(MaxEnergy(),currentEnergy+amount);Log("shotEnergyOrb","에너자이징 샷 · 오브 회수",currentEnergy-before);EvolutionEvent("EnergyOrbPickup");}
                if(!radial&&(projectile||orb.HitscanContinues==false))
                {
                    dissolvedProjectiles[shot]=dissolvedProjectiles.GetValueOrDefault(shot)+1;
                    if(weapon.WeaponType=="Speargun"&&component==2){activeGroundSpear=-1;Log("spearRecall","오브 직격으로 스피어건 즉시 회수");}
                    return contacts-1;
                }
                return contacts;
            }
            void PullTarget(Victim pulled,double center,double duration,bool panic=false)
            {
                if(pulled.State.Health<=0||duration<=0)return;
                if(pulled.State.Overguard>0){if(panic)Log("panic","프로보시스 직격 · 오버가드에도 제자리 공포",duration);return;}
                Log("paralyze",weapon.Id==RangedPullWeapons.Scyotid?"올가미 · 약 6초 마비":RangedPentaAugments.Applies(weapon)?"테더 유탄 · 기폭까지 연결":"촉수 · 폭발까지 구속",duration);
                if(Math.Abs(pulled.Position(time)-center)<1e-9)return;
                var c=build.Conditions??new();
                if(c.PullMotion=="None"){Log("pullExcluded","명시한 끌어오기 제외 시나리오");return;}
                if(c.PullMotion=="Blocked"){Log("pullBlocked","지형에 가로막혀 이동하지 않는 시나리오");return;}
                if(c.PullMotion!="Linear"||c.PullSeconds is not {} seconds)throw new CombatRuleUnavailableException("끌어오기 속도·개별 이동 경로는 원문에 없습니다. 이동 차단 또는 지정 시간의 직선 이동 시나리오를 선택하세요.");
                if(c.TargetsInLine)throw new CombatRuleUnavailableException("끌어오기 경로는 옆으로 배치한 적에서 계산합니다. 이동 중 일렬 관통의 교차 시각은 별도 경로가 필요합니다.");
                pulled.PositionFrom=pulled.Position(time);pulled.PositionTo=pulled.PositionFrom+(center-pulled.PositionFrom)*Math.Min(1,duration/seconds);pulled.MoveStart=time;pulled.MoveEnd=time+Math.Min(seconds,duration);Log("pull","설정한 시간의 직선 이동 시나리오",seconds);
            }
            void EntropySpike(RangedResult result)
            {
                if(Random()>=result.Modifiers!.GetValueOrDefault("EntropyExplosionChance"))return;
                var origin=victim;var radius=10*(1+result.Modifiers!.GetValueOrDefault("BlastRadius"));
                var faction=Math.Max(0,result.FactionFactor-(build.Conditions?.Roar??0));
                Log("entropySpike","엔트로피 스파이크 · 250 폭발 / 팩션 모드만 적용",250*faction);
                foreach(var other in victims.Where(v=>v.State.Health>0&&Between(v.Index,origin.Index)<=radius+1e-9).ToArray())
                {SelectVictim(other);Damage(new(){[DamageType.Blast]=250},faction,radial:true);}
                SelectVictim(origin);
            }
            void SpawnVoltageTrap()
            {
                if(build.Conditions?.FlyingTarget!=true||initial.Modifiers!.GetValueOrDefault("VoltageTrapDuration")<=0)return;
                var parameters=RangedVoltageTraps.Parameters(build.Conditions);var start=time+parameters.Landing;
                voltageTraps.Enqueue((victim.Position(time),start+initial.Modifiers!["VoltageTrapDuration"],0,start+parameters.Phase,parameters.Targets,parameters.Damage),(start+parameters.Phase,trapOrder++));
                Log("voltageTrapSpawn","볼티지 시퀀스 · 비행 적 처치로 함정 낙하",parameters.Landing);
            }
            void TaggedDeath()
            {
                if(!victim.WeaponTagged)return;
                var coldChance=initial.Modifiers!.GetValueOrDefault("ColdKillEnergyOrbChance")*Math.Min(10,Status().GetValueOrDefault(DamageType.Cold));
                if(coldChance>0&&Random()<coldChance)Log("energyOrbDrop","포톤 오버차지 · 에너지 오브 생성 (미습득)",1);
                if(holstered)return;
                EvolutionEvent("TomeKill");
                if(initial.Modifiers!.GetValueOrDefault("TomeUniversalOrbChance") is >0 and var chance&&Random()<chance)Log("universalOrbDrop","크라 캔티클 · 범용 오브 생성 (미습득)",1);
                if(initial.Modifiers!.GetValueOrDefault("TomeDefenseStrip") is >0 and var strip)
                    foreach(var other in victims.Where(v=>v.State.Health>0&&PlayerDistance(v.Index)<=(build.Conditions?.AffinityRange??50)))
                    {other.State.StripBaseArmor(strip);other.State.StripBaseShields(strip);Log("tomeStrip",$"자후 캔티클 · 대상 {other.Index+1} 방어력·실드 제거",strip);}
            }
            void SpawnSeeker(bool status,bool radial,DamageType? type)
            {
                if(build.Kitgun is not {Arcane:"PaxSeeker"} pax||!impactHeadshot||!(!status&&!radial||type is DamageType.Electricity or DamageType.Gas)||victims.All(v=>v.State.Health<=0))return;
                if(RangedPaxSeeker.Is(RangedEngine.Attacks(weapon)[impactComponent]))
                {
                    if(build.Conditions?.PaxSeekerRetrigger is null)throw new CombatRuleUnavailableException("팍스 시커 자체 처치가 다시 시커를 생성하는지는 원문에 없습니다. 재발동 시나리오를 선택하세요.");
                    if(build.Conditions.PaxSeekerRetrigger==false)return;
                }
                var paths=build.Conditions?.PaxSeekerPaths??throw new CombatRuleUnavailableException("팍스 시커의 유도 속도·무작위 대상 가중치는 원문에 없습니다. 유도탄별 대상과 비행 시간 또는 빗나감을 설정하세요.");
                if(paths.Length!=pax.ArcaneRank+1||paths.Any(p=>!p.Miss&&p.Flight is null))throw new ArgumentException("팍스 시커의 랭크 + 1개 유도탄마다 경로를 지정하세요.");
                Log("paxSeekerSpawn","팍스 시커 · 약점 처치 유도탄 생성",pax.ArcaneRank+1);
                foreach(var path in paths.Where(p=>!p.Miss))seekers.Enqueue((path.Target,-1000000-seekerOrder),(time+path.Flight!.Value,seekerOrder++));
            }
            void Damage(Dictionary<DamageType,double> packet,double multiplier,bool status=false,bool radial=false,DamageType? statusType=null,bool ability=false)
            {
                var oldOverguard=target.Overguard;var oldShields=target.Shields;
                var dealt=target.Hit(packet,multiplier,time,Status(),HeatStrip(),status,radial,impactHeadshot&&!radial,ability||weapon.Id==RangedPullWeapons.Scyotid&&impactComponent==1||RangedVileDischarge.Applies(weapon)&&impactComponent==2?1:Math.Max(1,initial.Modifiers!.GetValueOrDefault("OverguardMultiplier")));
                if(dealt>0&&!ability)victim.WeaponTagged=true;
                if(!ability&&impactComponent==0&&initial.Modifiers!.GetValueOrDefault("DischargeCapacity") is >0 and var dischargeCap){dischargeStored=Math.Min(dischargeCap,dischargeStored+target.LastConsumedOutput);Log("dischargeCharge","바일 디스차지 · 방어 전 출력 누적",dischargeStored);}

                if(dealt>0&&!ability&&statusType==DamageType.Blast)VeloxHit();
                if(status)dot+=dealt;else direct+=dealt;Log(status?"statusDamage":"directDamage",statusType?.ToString()??"직접 피해",dealt);
                var magnetic=procs.Where(p=>p.Type==DamageType.Magnetic&&p.End>time).ToArray();
                var broken=(oldOverguard>0&&target.Overguard==0?defense.Overguard:0)+(oldShields>0&&target.Shields==0?defense.Shields:0);
                if(broken>0&&magnetic.Length>0&&target.Health>0)
                {
                    var origin=magnetic[^1].Source!;var effects=origin.Modifiers!;
                    var tick=RangedStatusMath.MagneticBreakTick(broken,Math.Min(10,magnetic.Length),origin.FactionFactor,1+effects.GetValueOrDefault("StatusDamage"),1+effects.GetValueOrDefault("Electricity"));
                    Add(DamageType.Electricity,origin,1,1,fixedTick:tick,ability:magnetic[^1].Ability);Log("magneticBreak","자성 · 방어층 파괴 전기 상태",tick);
                }
                if(!ability&&arcane?.Code=="Fortifier"&&oldOverguard>target.Overguard){var old=playerOverguard;playerOverguard=Math.Min(15000,playerOverguard+(oldOverguard-target.Overguard)*.01);Log("overguardSteal","오버가드 획득",playerOverguard-old);}
                if(ability)
                {
                    if(target.Health<=0&&!victim.Killed){victim.Killed=true;TaggedDeath();Log("abilityKill","독립 효과 처치 · 무기 처치 효과·숙련도 제외",dealt);if(victims.All(v=>v.State.Health<=0))times[trial]=time;DetonateBlast();}
                    return;
                }
                if(dealt>0)
                {
                    if((initial.Modifiers!.GetValueOrDefault("LifeSteal")+(RangedHema.Applies(weapon)&&!status&&!radial&&impactHeadshot?.05:0)) is >0 and var lifeSteal){var before=playerHealth;playerHealth=Math.Min(MaxHealth(),playerHealth+target.LastDamageBeforeHealthCap*lifeSteal);Log("healing","무기 흡혈 · 대상 방어층 적용 후 피해로 체력 회복",playerHealth-before);if(RangedHema.Applies(weapon)&&playerHealth>before)fire.ResumeAfterPickup(RangedEngine.Calculate(Current()).Cadence!,time);}
                    if(initial.Modifiers!.GetValueOrDefault("DisarmChance")>0&&!victim.Disarmed&&target.Health>0&&target.Overguard<=0&&((!status&&RangedDisarm.Alternate(weapon.Id,impactComponent))||statusType==DamageType.Blast))
                    {
                        var eligible=RangedDisarm.Target(selection.EnemyId);
                        if(eligible==true&&Random()<initial.Modifiers!["DisarmChance"]){victim.Disarmed=true;Log("disarm",RangedDisarm.VisualOnly(selection.EnemyId)?"디스아밍 퓨리티 · 감염체 팔 제거, 공격 능력 유지":"디스아밍 퓨리티 · 영구 무장 해제"+(selection.Eximus?" · 엑시무스 오라 비활성":""),1);}
                        else if(eligible is null&&!victim.DisarmUnknownLogged){victim.DisarmUnknownLogged=true;Log("disarmUnknown","이 적의 무장 해제 가능 여부는 미확인 · 총기 피해와 분리",0);}
                    }
                    var stunRange=initial.Modifiers!.GetValueOrDefault("FinisherStunRange");
                    if(stunRange>0&&target.Overguard==0&&PlayerDistance(victim.Index)<=stunRange+1e-9)Log("finisherOpening","디지잉 라운즈 · 기절 및 마무리 공격 가능",stunRange);
                    EvolutionEvent("Damage");
                    if(passive?.Kind=="Onos"&&impactComponent>=1&&initial.Modifiers!.GetValueOrDefault("ChargeBlastCritical")>0){chargeDamageStacks=Math.Min(50,chargeDamageStacks+1);Log("chargeStack","데버스테이션 캐스케이드",chargeDamageStacks);}
                    if(passive?.Kind=="Scisco"&&!status){weaponStacks=Math.Min(5,Math.Max(0,weaponStacks-(int)Math.Floor((time-weaponRefreshed)/2))+1);weaponRefreshed=time;Log("weaponBuff","타깃 어낼리시스",weaponStacks);}
                    if(passive?.Kind=="Magnus"&&Random()<.04){weaponStacks=1;weaponRefreshed=time;Log("weaponBuff","챔버 캐스케이드 · 탄약 무한 4초",1);}
                }
                if(target.Health<=0&&!victim.Killed&&passive?.Kind=="Athodai"&&!status&&!radial&&impactHeadshot&&impactComponent==0&&(weaponStacks==0||time-weaponRefreshed>=8))
                {weaponStacks=1;weaponRefreshed=time;Log("weaponBuff","오버드라이브 · 8초 / 갱신 불가",1);}
                if(target.Health<=0&&!victim.Killed){victim.Killed=true;TaggedDeath();SpawnVoltageTrap();SpawnResidual();if(initial.Modifiers!.GetValueOrDefault("BuckshotFalloff")>0){if(impactComponent==0){buckshotReady=true;Log("buckshotReady","일반 사격 처치 · 다음 벅샷 감쇠 거리 강화",initial.Modifiers!["BuckshotFalloff"]);}else if(impactComponent==1){var capacity=RangedEngine.Calculate(Current()).Cadence!.Magazine;var restore=RangedBurstAugments.Refund(capacity,initial.Modifiers!["BuckshotMagazineRestore"],build.Conditions?.RefundRounding??"Unspecified");restore=Math.Min(capacity-fire.Ammo,restore);var source=build.Conditions?.BuckshotRefillSource??"Unspecified";if(source=="Reserve")restore=ammo.Spend(restore);else if(source!="Created")throw new CombatRuleUnavailableException("벅샷 처치 회복이 예비 탄약을 사용하는지는 원문에 없습니다. 전용 모드 설정에서 계산 기준을 선택하세요.");fire.InstantReload(fire.Ammo+restore,time);Log("buckshotRefill","벅샷 처치 · 탄창 회복",restore);}}if(initial.Modifiers!.GetValueOrDefault("CriticalMutationStep") is >0 and var mutationStep){mutation=Math.Min(3,mutation+mutationStep);Log("criticalMutation","크리티컬 뮤테이션 · 처치",mutation);}if(alternateRule?.Primary.Contains(impactComponent)==true){alternateCharges=Math.Min(alternateRule.Capacity,alternateCharges+1);Log("alternateCharge","보조 공격 충전",alternateCharges);}if(passive?.Kind=="Ocucor"){if(status||!tendrilHit)weaponStacks=Math.Min(4,weaponStacks+1);var capacity=RangedEngine.Calculate(Current()).Cadence!.Magazine;var restored=ammo.Spend(Math.Min(capacity-fire.Ammo,capacity*initial.Modifiers!.GetValueOrDefault("KillMagazineRefill")));if(restored>0){fire.InstantReload(fire.Ammo+restored,time);Log("ammoRestore","센티언트 서지 · 예비 탄약에서 회복",restored);}}if(RangedBurstAugments.IsFrenzy(weapon,impactComponent))frenzyKills++;kills++;lastKill=time;if(!status&&!radial&&impactHeadshot)headshotKills++;if(arcane?.Code=="Merciless"||arcane?.Code=="Deadhead"&&!status&&!radial&&impactHeadshot)ArcaneStack();if(victims.All(v=>v.State.Health<=0))times[trial]=time;EvolutionEvent("Kill");if(statusType==DamageType.Slash)EvolutionEvent("SlashStatusKill");if(!status&&!radial&&impactHeadshot){EvolutionEvent("HeadshotKill");if(weapon.Category=="secondary")EvolutionEvent("PistolWeakpointKill");}if(!status&&!radial&&impactHeadshot&&build.Kitgun is {Arcane:"PaxBolt"} pax&&Random()<.25*(pax.ArcaneRank+1)){paxBoltUntil=time+pax.ArcaneRank+1;Log("paxBolt","팍스 볼트 · 다음 어빌리티 위력·효율 증가",.075*(pax.ArcaneRank+1));}if(impactHeadshot&&(!status&&!radial||statusType is DamageType.Electricity or DamageType.Gas))EvolutionEvent("WeakpointKillWithDot");Log("kill","대상 처치",direct+dot);QueueKillExplosion(status,radial);DetonateBlast();GainAffinity(RangedSyndicates.KillAffinity(selection)*.5);SpawnSeeker(status,radial,statusType);}
            }
            void CubeDamage()
            {
                var before=victim;
                double Radius(int component)=>RangedEngine.Calculate(Current() with{AttackIndex=component}).Modifiers!.GetValueOrDefault("EffectiveRange");
                foreach(var hit in cubes.Advance(time,index=>victims[index].State.Health>0,build.Conditions?.TargetBodyThickness,Radius))
                {
                    Log("cube",hit.Component==1?"큐브 직격":hit.Component==2?"큐브 자연 폭발":"큐브 플레이어 피해·연쇄 기폭",hit.Cube);
                    if(hit.Component==1){Impact(1,1,1,1,arcaneStacks,false,hit.Shot,hit.Target,pierced:hit.Pierced,headshotOverride:hit.Headshot);continue;}
                    foreach(var recipient in victims.Where(v=>v.State.Health>0).ToArray())
                    {
                        var point=TargetPoint(recipient);var x=point.X;var y=point.Y;
                        var distance=Math.Sqrt(Math.Pow(x-hit.X,2)+Math.Pow(y-hit.Y,2));
                        Impact(hit.Component,1,1,1,arcaneStacks,false,hit.Shot,recipient.Index,spread:true,radialDistance:distance);
                    }
                }
                SelectVictim(before);
            }
            void AzimaDamage(RangedAzimaDisc disc,bool volley)
            {
                var scenario=build.Conditions!.Azima!;var before=victim;azimaHit=true;
                var active=Current() with{AttackIndex=volley?0:1,Conditions=Current().Conditions! with{Headshot=false}};
                var statistics=RangedEngine.Calculate(active);var range=volley?statistics.Modifiers!.GetValueOrDefault("EffectiveRange",300):scenario.ExplosionRadius!.Value;
                var candidates=victims.Where(v=>v.State.Health>0).Select(v=>{var point=TargetPoint(v);var dx=point.X-scenario.CenterX;var dy=point.Y-scenario.CenterY;return (Victim:v,X:point.X,Y:point.Y,Distance:Math.Sqrt(dx*dx+dy*dy));}).Where(v=>v.Distance<=range+1e-9).ToArray();
                if(volley)
                {
                    for(int ray=0;ray<4;ray++)
                    {
                        var angle=(scenario.InitialAngle+scenario.RotationSpeed!.Value*(time-disc.Start)+ray*90)*Math.PI/180;
                        var along=candidates.Select(v=>(Target:v,Along:(v.X-scenario.CenterX)*Math.Cos(angle)+(v.Y-scenario.CenterY)*Math.Sin(angle),Across:Math.Abs(-(v.X-scenario.CenterX)*Math.Sin(angle)+(v.Y-scenario.CenterY)*Math.Cos(angle)))).Where(v=>v.Along>=0&&v.Across<=scenario.HitRadius!.Value+1e-9).OrderBy(v=>v.Along).ToArray();
                        var penetration=statistics.Modifiers!.GetValueOrDefault("PunchThrough");
                        foreach(var hit in along)
                        {
                            Impact(0,1,1,1,arcaneStacks,false,-2000000-(azimaOrder++),hit.Target.Victim.Index,spread:true,snapshot:active with{Distance=hit.Target.Distance},headshotOverride:false);
                            if(penetration<=0)break;var thickness=build.Conditions?.TargetBodyThickness??throw new CombatRuleUnavailableException("터렛 관통탄은 대상 두께가 필요합니다. 관통 설정에서 적의 두께를 지정하세요.");penetration-=thickness;if(penetration<0)break;
                        }
                    }
                    Log("azimaVolley","터렛 4방향 사격 · 회전 속도는 연사 모드와 독립",4);
                }
                else
                {
                    foreach(var hit in candidates){var scale=range>0?1-scenario.ExplosionFalloff!.Value*hit.Distance/range:1;Impact(1,1,scale,1,arcaneStacks,false,disc.Shot,hit.Victim.Index,spread:true,snapshot:active with{Distance=0,Conditions=active.Conditions! with{RadialDistance=0}},headshotOverride:false);}
                    Log("azimaExpiry","터렛 만료 · 폭발",75);
                }
                azimaHit=false;SelectVictim(before);
            }
            void SpawnResidual()
            {
                if(build.Kitgun is not {} kit||!RangedResidual.Is(kit.Arcane))return;
                if(residualPools.TrySpawn(time,kit.ArcaneRank,Random()) is not {} pool)return;
                var scenario=build.Conditions?.Residual??throw new CombatRuleUnavailableException("장착한 레지듀얼의 발동·피해 시나리오를 지정하세요.");
                foreach(var pulse in RangedResidual.Pulses(kit.Arcane,kit.ArcaneRank,scenario))residualPulses.Enqueue((pool,victim.Position(time),pulse.Scale),(time+pulse.Time,residualOrder++));
                Log("residualSpawn","레지듀얼 생성 · 전체 키트건 공통 최대 2개",3*(kit.ArcaneRank+1));
            }
            void ResidualDamage(double center,double scale)
            {
                var id=build.Kitgun!.Arcane;var scenario=build.Conditions!.Residual!;
                var credited=scenario.WeaponCredit??throw new CombatRuleUnavailableException("레지듀얼 처치가 무기 처치로 인정되는지 원문에 없습니다. 처치 귀속 시나리오를 선택하세요.");
                if(id=="ResidualBoils"&&!victims.Any(v=>v.State.Health>0&&Math.Abs(v.Position(time)-center)<=(scenario.BoilsApproachRadius??throw new CombatRuleUnavailableException("하이브 폭발을 유발하는 접근 반경을 지정하세요."))))return;
                var before=victim;var head=impactHeadshot;impactHeadshot=false;
                foreach(var recipient in victims.Where(v=>v.State.Health>0&&Math.Abs(v.Position(time)-center)<=RangedResidual.Radius(id)).ToArray())
                {
                    SelectVictim(recipient);var packet=RangedResidual.Packet(build,id,scale,Math.Abs(victim.Position(time)-center));
                    Damage(packet,1,radial:true,ability:!credited);Log("residualHit","레지듀얼 · 지정 비치명 출력 · 방어 적용 전",packet.Values.Sum());
                    if(id!="ResidualViremia"&&Random()<.2)
                    {
                        var type=RangedResidual.Type(id);var fixedTick=type==DamageType.Cold?0:(scenario.StatusTick??throw new CombatRuleUnavailableException("레지듀얼 상태 피해의 모드 브라켓이 미기재입니다. 상태 1틱 출력을 지정하세요."))*scale;
                        var source=initial with{BaseDamage=0,StatusBaseFactor=1,FactionFactor=1,Modifiers=[]};Add(type,source,1,1,fixedTick:fixedTick,ability:!credited);
                    }
                }
                SelectVictim(before);impactHeadshot=head;
            }
            void QueueKillExplosion(bool status,bool radial)
            {
                var effects=initial.Modifiers!;var radius=effects.GetValueOrDefault("KillExplosionRadius");
                if(radius<=0||effects.GetValueOrDefault("KillExplosionRejectFirstHit")>0&&!status&&!radial&&victim.Hits<=1)return;
                var delay=effects.GetValueOrDefault("KillExplosionDelayRequired")>0?build.Conditions?.KillExplosionDelay??throw new CombatRuleUnavailableException("컴버스천 빔의 처치 후 폭발 지연 수치가 위키에 없습니다. 상황 설정에서 지연 시간 가정을 지정하세요."):0;
                var maximum=effects.GetValueOrDefault("KillExplosionExcludeDifficulty")>0?EnemyScaling.Calculate(selection with{Difficulty=EnemyDifficulty.Normal}).Health:defense.Health;
                var packet=new Dictionary<DamageType,double>{[DamageType.Corrosive]=effects.GetValueOrDefault("KillExplosionCorrosive"),[DamageType.Magnetic]=effects.GetValueOrDefault("KillExplosionMagnetic"),[DamageType.Blast]=effects.GetValueOrDefault("KillExplosionBlast")+maximum*effects.GetValueOrDefault("KillExplosionHealthBlast")};
                killExplosions.Enqueue((victim.Index,packet,radius),(time+delay,killExplosionOrder++));
            }
            void DetonateBlast()
            {
                var origin=victim;var blast=procs.Where(p=>p.Type==DamageType.Blast&&p.End>=time).ToArray();
                if(blast.Length==0)return;
                foreach(var p in blast)procs.Remove(p);
                var amount=blast.Sum(p=>(float)((float)p.Seed*3f*(float)p.Faction*(float)(1+p.StatusBonus)));
                foreach(var group in blast.GroupBy(p=>p.Ability))Tick(group);if(blast.Any(p=>!p.Ability))EvolutionEvent("BlastDetonation");
                if(settings.IncludeStatusDamage)foreach(var other in victims.Where(v=>v.Index!=origin.Index&&v.State.Health>0&&Between(v.Index,origin.Index)<=5+1e-9).ToArray())
                {SelectVictim(other);foreach(var group in blast.GroupBy(p=>p.Ability)){var part=group.Sum(p=>(float)((float)p.Seed*3f*(float)p.Faction*(float)(1+p.StatusBonus)));Damage(new(){[DamageType.Blast]=part},1,true,true,DamageType.Blast,ability:group.Key);}Log("blastArea","폭발 상태 · 주변 5m",amount);}
                SelectVictim(origin);
            }
            void Tick(IEnumerable<Proc> group)
            {
                var entries=group.ToArray();if(entries.Length==0)return;var previousHeadshot=impactHeadshot;var previousComponent=impactComponent;var first=entries[0];impactHeadshot=first.Headshot;impactComponent=first.Attack;var normal=entries.FirstOrDefault(p=>p.FixedTick is null)??first;
                var amount=first.Type==DamageType.Blast?entries.Sum(p=>(float)((float)p.Seed*.3f*(float)p.Faction*(float)(1+p.StatusBonus))):(entries.Any(p=>p.FixedTick is null)?RangedStatusMath.Tick(first.Type,entries.Where(p=>p.FixedTick is null).Select(p=>p.Seed),normal.Element,normal.Faction,normal.StatusBonus,!first.Ability):0)+entries.Sum(p=>p.FixedTick??0);
                if(first.Type==DamageType.Gas&&!first.Ability)amount*=1+RangedEngine.Calculate(Current()).Modifiers!.GetValueOrDefault("LiveGasBonus");
                
                if(settings.IncludeStatusDamage&&amount>0)
                {
                    var origin=victim;var gasStacks=Math.Clamp(procs.Count(p=>p.Type==DamageType.Gas&&p.End>time),1,10);
                    foreach(var other in victims.Where(v=>v.State.Health>0).ToArray())
                    {
                        var distance=Between(other.Index,origin.Index);
                        bool GasReaches(){var range=StatusAreaRules.GasRadius(gasStacks);if(build.Conditions?.AllowApproximations==true&&distance>range.Minimum+1e-9&&distance<=range.Maximum+1e-9){Approximate("가스 중간 중첩 반경은 3m+0.3m×(중첩−1), 10중첩은 원문의6m를 사용하는 근사입니다. 상충 구간의 주변 적 피해에 영향이 있습니다.");return false;}return StatusAreaRules.GasReaches(gasStacks,distance);}
                        var reaches=other.Index==origin.Index||first.Type==DamageType.Electricity&&distance<=3+1e-9||first.Type==DamageType.Gas&&GasReaches();
                        if(!reaches)continue;
                        SelectVictim(other);impactHeadshot=first.Headshot&&other.Index==origin.Index;Damage(new(){[first.Type==DamageType.Slash?DamageType.Finisher:first.Type]=amount},1,true,statusType:first.Type,ability:first.Ability);Log("tick",Labels.Damage(first.Type),amount);
                    }
                    SelectVictim(origin);
                }
                foreach(var p in entries)p.Next=first.Type==DamageType.Blast?double.PositiveInfinity:p.Next+1;impactHeadshot=previousHeadshot;impactComponent=previousComponent;
            }
            void Add(DamageType type,RangedResult source,double critical,double scale,bool primer=false,double seedMultiplier=1,double? fixedTick=null,bool spread=false,bool ability=false)
            {
                if(!DamageTypeRules.HasStatus(type))return;
                if(type==DamageType.Cold&&victim.FrozenEnd>time)return;
                var effects=source.Modifiers!;var duration=(type==DamageType.Heat&&effects.GetValueOrDefault("HeatBaseDuration")>0?effects["HeatBaseDuration"]:RangedStatusMath.Duration(type))*(primer?1:Math.Max(0,1+effects.GetValueOrDefault("StatusDuration")));
                if(duration<=0&&type!=DamageType.Blast)return;
                var p=new Proc{Ability=ability,Attack=impactComponent,Headshot=!primer&&impactHeadshot,Type=type,FixedTick=fixedTick,Source=primer?source with{Modifiers=[],FactionFactor=1,StatusBaseFactor=1}:source,End=time+duration,Duration=duration,Next=type==DamageType.Blast?time+duration:type is DamageType.Electricity or DamageType.Gas?time:time+1,
                    Seed=primer?0:source.BaseDamage*source.StatusBaseFactor*source.FactionFactor*critical*scale*seedMultiplier,
                    Element=1+effects.GetValueOrDefault(type.ToString())-(type==DamageType.Gas?effects.GetValueOrDefault("LiveGasBonus"):0),Faction=source.FactionFactor,StatusBonus=effects.GetValueOrDefault("StatusDamage")};
                if(primer){p.Next=double.PositiveInfinity;p.Element=1;p.Faction=1;}
                if(type==DamageType.Heat)
                {
                    var first=procs.FirstOrDefault(x=>x.Type==type&&x.End>time);
                    if(first is null){var recovering=victim.HeatEnd>=0&&time<victim.HeatEnd+6&&target.Armor>0;if(recovering&&build.Conditions?.AllowApproximations!=true)throw new CombatRuleUnavailableException("화염 종료 후 방어력 회복 중 재점화의 정확한 시간 규칙은 위키에서 확인되지 않았습니다.");victim.HeatFloor=recovering?HeatStrip():0;if(recovering)Approximate("화염 재점화: 남은 방어력 감소를 유지한 채 새 감소 단계를 진행합니다. 회복 중 재점화 규칙 미기재로 방어력·DPS·TTK에 영향이 있습니다.");victim.HeatStart=time;victim.HeatDuration=duration;}
                    else{victim.HeatDuration=Math.Max(victim.HeatDuration,duration);p.Element=first.Element;p.Faction=first.Faction;p.Next=double.IsFinite(first.Next)?first.Next:time+1;}
                    victim.HeatEnd=time+victim.HeatDuration;p.End=victim.HeatEnd;foreach(var old in procs.Where(x=>x.Type==type))old.End=victim.HeatEnd;
                }
                var cap=type==DamageType.Puncture&&weapon.Id=="wiki:primary/Evensong"?7:type==DamageType.Cold&&target.Overguard>0?4:TargetStatusRules.Maximum(type);
                var same=procs.Where(x=>x.Type==type&&x.End>time).ToArray();
                if(same.Length>=cap)procs.Remove(same.MinBy(x=>x.End)!);
                procs.Add(p);Log("proc",Labels.Damage(type),Status().GetValueOrDefault(type));
                if(type==DamageType.Cold&&Status().GetValueOrDefault(type)==10){victim.FrozenEnd=time+duration/2;foreach(var cold in procs.Where(x=>x.Type==type))cold.End=victim.FrozenEnd;}
                if(type==DamageType.Blast&&(Status().GetValueOrDefault(type)>=10||target.Health<=0))DetonateBlast();
                if(!primer&&!ability)
                {
                    if(type==DamageType.Cold&&!spread&&effects.GetValueOrDefault("ColdSpreadChance")>0&&Random()<effects["ColdSpreadChance"])
                    {
                        var origin=victim;
                        foreach(var other in victims.Where(v=>v.Index!=origin.Index&&v.State.Health>0&&Between(v.Index,origin.Index)<=6+1e-9).ToArray())
                        {SelectVictim(other);Add(type,source,critical,scale,seedMultiplier:seedMultiplier,spread:true);}
                        SelectVictim(origin);
                    }
                    EvolutionEvent("Status");if(type==DamageType.Puncture)EvolutionEvent("Puncture");
                    if(arcane?.Code switch{"Flare"=>type==DamageType.Heat,"Frostbite"=>type==DamageType.Cold,"Blight"=>type==DamageType.Toxin,"Voltage"=>type==DamageType.Electricity,"Exhilarate"=>type==DamageType.Impact,_=>false})ArcaneStack();
                    if(effects.GetValueOrDefault("StatusExtraDamage") is {} extra&&extra>0)Damage(new(){[type]=extra*source.FactionFactor},1);
                    if(type==DamageType.Puncture&&effects.GetValueOrDefault("PunctureArmorStrip")>0){target.StripBaseArmor(effects["PunctureArmorStrip"]);Log("armorStrip","플렌싱 스파이크 방어력 제거",target.Armor);}
                    if(type==DamageType.Puncture)for(int n=0;n<effects.GetValueOrDefault("PunctureColdStacks");n++)Add(DamageType.Cold,source,critical,scale);
                }
            }
            var collisions=new PriorityQueue<(RangedCollision Collision,int Component,int Contacts,double Scale,int Pellets,int Arcane,bool Final,int Shot),(double Time,int Order)>();int collisionOrder=0;
            var impacts=new PriorityQueue<(int Component,int Contacts,double Scale,int Pellets,int ArcaneStacks,bool FinalShot,int Shot,int Target,bool Chained),(double Time,int Order)>();int eventOrder=0;
            var penetrations=new PriorityQueue<(int Component,int Contacts,double Scale,int Pellets,int Arcane,bool Final,int Shot,int Target),(double Time,int Order)>();int penetrationOrder=0;
            var grenadeLaunches=new PriorityQueue<(double Completion,int Attack,int Shot),(double Time,int Order)>();
            var grenadeShotIds=new HashSet<int>();
            var blades=new Queue<(int Shot,int Arcane)>();
            var embedded=new Queue<(int Target,int Shot,bool Final,int Arcane)>();
            void RecallRound((int Target,int Shot,bool Final,int Arcane) round)
            {
                impacts.Enqueue((recall!.Explosion,1,1,1,round.Arcane,round.Final,round.Shot,round.Target,false),(time,eventOrder++));
                Log("recall","회수 탄환 폭발",1);
            }
            (double X,double Y) TargetPoint(Victim v)=>(build.Conditions?.TargetsInLine??false)?(build.Distance+v.Position(time)+v.Forward(time),0):(build.Distance+v.Forward(time),v.Position(time));
            void DetonatePlacedGrenades(bool manual,int? selectedShot=null)
            {
                var previous=victim;
                foreach(var grenade in selectedShot is {} chosen?grenadeRack.TakeShot(chosen):grenadeRack.Detonate(time,manual,shot=>grenadePositions.TryGetValue(shot,out var p)&&(p.Kind!="Position"||Math.Abs(p.Time-time)<1e-9)))
                {
                    foreach(var pulled in victims.Where(v=>v.PullGrenade==grenade.Shot)){var releasedPosition=pulled.Position(time);pulled.PositionFrom=pulled.PositionTo=releasedPosition;pulled.MoveStart=pulled.MoveEnd=time;pulled.PullGrenade=-1;}
                    Log("grenadeDetonate",manual?"펜타 · 배치한 유탄 수동 기폭":"유탄 · 신관 자동 기폭",grenade.Count);
                    if(grenadePositions.TryGetValue(grenade.Shot,out var position))
                    {
                        if(position.Kind=="Position"&&Math.Abs(position.Time-time)>1e-9)throw new CombatRuleUnavailableException("이동 중 유탄의 기폭 시점 좌표를 지정하세요. 이전 좌표에서 멈췄다고 가정하지 않습니다.");
                        var point=position.Kind=="Attach"?TargetPoint(victims[position.Target!.Value]):(X:position.X,Y:position.Y);
                        foreach(var recipient in victims.Where(v=>v.State.Health>0).ToArray()){var targetPoint=TargetPoint(recipient);var distance=Math.Sqrt(Math.Pow(targetPoint.X-point.X,2)+Math.Pow(targetPoint.Y-point.Y,2));Impact(1,grenade.Count,1,1,arcaneStacks,false,grenade.Shot,recipient.Index,spread:true,radialDistance:distance,headshotOverride:false);}
                    }
                    else Impact(1,grenade.Count,1,1,arcaneStacks,false,grenade.Shot,grenade.Target,headshotOverride:false);
                }
                SelectVictim(previous);fire.WaitUntil(time);
            }
            void InfestedDamage(InfestedOrbEmission[] emitted)
            {
                var previous=victim;
                foreach(var orb in emitted)
                {
                    Log(orb.Component==1?"infestedPulse":orb.Component==2?"infestedExplosion":"burstingMass",orb.Component==1?"감염 구체 · 초당 타격":orb.Component==2?"감염 구체 · 만료/재발사 폭발":"버스팅 매스 · 저장 폭발",orb.Stored?.Values.Sum()??orb.Stacks);
                    foreach(var recipient in victims.Where(v=>v.State.Health>0).ToArray())
                    {
                        var point=TargetPoint(recipient);var distance=Math.Sqrt(Math.Pow(point.X-orb.X,2)+Math.Pow(point.Y-orb.Y,2));
                        var snapshot=Current() with{AttackIndex=orb.Component,Conditions=Current().Conditions! with{InfestedOrbStacks=orb.Stacks,InfestedPulse=orb.Component==1,InfestedStored=orb.Stored,InfestedOrbs=null,Headshot=false,RadialDistance=distance}};
                        var result=RangedEngine.Calculate(snapshot);if(distance>result.Modifiers!.GetValueOrDefault("EffectiveRange"))continue;
                        Impact(orb.Component,1,1,1,arcaneStacks,false,orb.Shot,recipient.Index,spread:true,radialDistance:distance,snapshot:snapshot,headshotOverride:false);
                    }
                }
                SelectVictim(previous);
            }
            void StugDamage(StugEmission[] emitted)
            {
                foreach(var blob in emitted)
                {
                    var snapshot=Current() with{AttackIndex=blob.Component,Conditions=Current().Conditions! with{StugStacks=blob.Stacks,Headshot=false}};
                    if(blob.Explosion&&blob.Component==1&&victims[blob.Target].State.Health>0&&incarnon is not null){incarnonGauge+=4;Log("incarnonCharge","스터그 · 부착 대상 폭발로 게이지 충전",4);}
                    Log("stug",blob.Explosion?"부착 구체 폭발":"부착 구체 주기 피해",blob.Stacks);
                    Impact(blob.Component,1,1,1,arcaneStacks,false,blob.Shot,blob.Target,snapshot:snapshot,headshotOverride:false);
                    if(blob.Explosion&&blob.Component==3)
                    {
                        var path=build.Conditions?.StugBounces??throw new CombatRuleUnavailableException("인카논 스터그의 자탄 도탄 위치·시간을 설정하세요. 빈 경로는 자탄 전부 빗나감입니다.");
                        if(path.Length>3||path.Any(x=>!double.IsFinite(x.AfterImpact)||x.AfterImpact<0||x.Target<0||x.Target>=victims.Length))throw new ArgumentException("스터그 자탄 도탄은 최대 3회이며 유효한 대상과 시간을 지정해야 합니다.");
                        foreach(var bounce in path)collisions.Enqueue((bounce,4,Math.Max(2,blob.Stacks),1,1,arcaneStacks,false,blob.Shot),(time+bounce.AfterImpact,collisionOrder++));
                    }
                }
            }
            void SimulorDamage(SimulorEmission[] emitted)
            {
                foreach(var orb in emitted)
                {
                    if(RangedOrdinarySimulor.Applies(weapon)&&orb.Component==3)
                    {
                        var rule=build.Conditions?.OrdinarySimulor??new();var output=rule.TickOutput??throw new CombatRuleUnavailableException("일반 시뮬러 특이점의 방어 전 틱 출력은 원문에 없습니다.");var radius=rule.TickRadius??throw new CombatRuleUnavailableException("일반 시뮬러 특이점의 피해 반경은 원문에 없습니다.");
                        var before=victim;var oldHead=impactHeadshot;impactHeadshot=false;foreach(var other in victims.Where(v=>v.State.Health>0&&Between(v.Index,orb.Target)<=radius+1e-9).ToArray()){SelectVictim(other);Damage(new(){[DamageType.Magnetic]=output},1,radial:true);Log("simulor","일반 특이점 · 지정 틱 출력",output);}SelectVictim(before);impactHeadshot=oldHead;continue;
                    }
                    var snapshot=Current() with{AttackIndex=orb.Component,Conditions=Current().Conditions! with{SimulorValue=orb.Value,SimulorEqualPair=orb.EqualPair,SimulorUnstacked=orb.Unstacked,Headshot=false}};
                    Log("simulor",orb.Component==1?"구체 합체":orb.Component==3?"특이점 주기 피해":"구체 폭발",orb.Value);
                    if(orb.LaunchPoint){var radius=RangedEngine.Calculate(snapshot).Modifiers!.GetValueOrDefault("EffectiveRange");foreach(var recipient in victims.Where(v=>v.State.Health>0&&PlayerDistance(v.Index)<=radius+1e-9).ToArray())Impact(orb.Component,1,1,1,arcaneStacks,false,orb.Shot,recipient.Index,spread:true,radialDistance:PlayerDistance(recipient.Index),snapshot:snapshot,headshotOverride:false);}
                    else Impact(orb.Component,1,1,1,arcaneStacks,false,orb.Shot,orb.Target,snapshot:snapshot,headshotOverride:false);
                }
            }
            void ApplyNapalmPatch(int shot,int contacts,int origin)
            {
                if(!RangedPentaAugments.Applies(weapon)||initial.Modifiers!.GetValueOrDefault("PentaNapalm")<=0||contacts<=0)return;
                var c=build.Conditions??new();var recipients=victims.Where(v=>v.State.Health>0&&Math.Sqrt(Math.Pow(Between(v.Index,origin),2)+Math.Pow(c.RadialDistance,2))<=c.NapalmPatchRadius+1e-9).ToArray();if(recipients.Length==0)return;
                var tick=c.NapalmExcludePatchDamage?0:c.NapalmPatchTick??throw new CombatRuleUnavailableException("네이팜 장판이 부여하는 Heat의 피해 수치는 원문에 없습니다. 장판 상태 피해 제외 또는 지정한 틱 출력을 선택하세요.");
                var previous=victim;var oldHead=impactHeadshot;impactHeadshot=false;var source=initial with{BaseDamage=0,BaseFactor=1,StatusBaseFactor=1,FactionFactor=1,Modifiers=[]};
                foreach(var recipient in recipients){SelectVictim(recipient);for(int n=0;n<contacts;n++)Add(DamageType.Heat,source,1,1,fixedTick:tick);Log("napalmPatchEntry","네이팜 장판 진입 · Heat 상태 / 직접 피해 없음",contacts);}
                SelectVictim(previous);impactHeadshot=oldHead;
            }
            int StickyContacts(int component,int contacts)
            {
                if(!RangedAdhesive.Relevant(weapon,component)||initial.Modifiers!.GetValueOrDefault("TetherTargets")>0)return 0;
                var chance=initial.Modifiers!.GetValueOrDefault("AdhesiveChance");if(chance<=0||contacts<=0)return 0;
                int stuck=0;for(int p=0;p<contacts;p++)if(Random()<chance)stuck++;
                if(stuck>0)Log("adhesiveStick","애드히시브 · 부착한 투사체",stuck);return stuck;
            }
            void ScheduleChildren(int component,int contacts,double scale,int pellets,int stacks,bool final,int shot,int destination,int stuck,bool surface=false)
            {
                if(weapon.Id==RangedKomorex.Id&&component==2&&komorexLastTargets.GetValueOrDefault(shot,destination)!=destination)return;
                var stickyDelay=stuck>0?RangedAdhesive.Delay(weapon,build,component,time-adhesiveLaunchTimes.GetValueOrDefault(shot,time)):0;
                var groups=stuck>0?new[]{(Count:contacts-stuck,Delay:0d),(Count:stuck,Delay:stickyDelay)}:new[]{(Count:contacts,Delay:0d)};
                foreach(var group in groups.Where(g=>g.Count>0))
                {
                    foreach(var child in RangedAttackLinks.Children(weapon.Id,component).Where(c=>build.Distance>=c.MinimumDistance&&!(component==1&&initial.Modifiers!.GetValueOrDefault("SentientBarrageCritical")>0)))
                    {
                        if(manualGrenades&&component==0&&child.ManualDetonation)continue;
                        var ticks=child.SingleTickOnPierce&&build.Conditions?.PiercedTarget==true?1:child.TickCount;if(child.TickInterval>0&&doubledFieldShots.Contains(shot))ticks*=2;
                        var gaze=RangedKitguns.Get(weapon.Id) is {Chamber:"Gaze",Category:"primary"};
                        for(int tick=0;tick<ticks;tick++)impacts.Enqueue((child.Child,group.Count,gaze?scale/Math.Max(1,pellets):scale,gaze?1:pellets,stacks,final,shot,destination,false),(time+group.Delay+RangedAttackLinks.Delay(child,build.Conditions,build)+tick*child.TickInterval+(child.ManualDetonation?RangedPentaAugments.Delay(build):0),eventOrder++));
                    }
                    if(RangedClusters.Get(weapon.Id) is {} cluster&&component==cluster.Parent)
                        foreach(var landing in build.Conditions?.ClusterLandings??[])collisions.Enqueue((landing,cluster.Contact,group.Count,scale,pellets,stacks,final,shot),(time+group.Delay+landing.AfterImpact,collisionOrder++));
                }
                if(manualGrenades&&component==0&&initial.Modifiers!.GetValueOrDefault("PentaNapalm")>0)
                {
                    if(contacts>stuck)impacts.Enqueue((1,contacts-stuck,scale,pellets,stacks,final,shot,destination,false),(time,eventOrder++));
                    grenadeRack.RemoveMoving(shot);
                }
            }
            void ScheduleField(int component,int contacts,double scale,int pellets,int stacks,bool final,int shot,int destination,int stuck)
            {
                if(contacts<=0||RangedFields.Get(weapon.Id) is not {} field||!field.Parents.Contains(component))return;
                    var effects=RangedEngine.Calculate(Current()).Modifiers!;
                    var fieldGroups=stuck>0?new[]{(Count:contacts-stuck,Delay:0d),(Count:stuck,Delay:RangedAdhesive.Delay(weapon,build,component,time-adhesiveLaunchTimes.GetValueOrDefault(shot,time)))}:new[]{(Count:contacts,Delay:0d)};
                    foreach(var group in fieldGroups.Where(g=>g.Count>0))foreach(var pulse in RangedFields.Pulses(field,build.Conditions??new(),effects.GetValueOrDefault("FireRate")))
                        impacts.Enqueue((field.Child,field.Single?1:group.Count,scale,pellets,stacks,final,shot,destination,false),(time+pulse+group.Delay,eventOrder++));
                    if(field.Explosion>=0)impacts.Enqueue((field.Explosion,1,scale,pellets,stacks,final,shot,destination,false),(time+field.SpawnDelay+RangedFields.End(field,build.Conditions??new()),eventOrder++));
            }
            void Impact(int component,int contacts,double beamScale,int beamPellets,int shotArcaneStacks,bool finalShot,int shot,int targetIndex,bool chained=false,bool spread=false,double? radialDistance=null,bool secondaryRoot=false,RangedBuild? snapshot=null,bool irradiated=false,bool? headshotOverride=null,bool pierced=false)
            {
                if(manualGrenades&&component==0&&!grenadeRack.Contains(shot))return;
                if(spontaneous&&component==0&&!chained&&!spread&&contacts>0)
                {
                    var before=victim;SelectVictim(victims[targetIndex]);var c=build.Conditions??new();var allyCenter=c.SingularityAllyPosition+c.SingularityAllySpeed*time;var impactRange=allyCenter is {} ally?Math.Sqrt(build.Distance*build.Distance+ally*ally):PlayerDistance(targetIndex);if(impactRange>initial.Modifiers!.GetValueOrDefault("EffectiveRange",12)){SelectVictim(before);return;}var independent=c.SingularityIndependentOrbs??(beamPellets<=1?true:throw new CombatRuleUnavailableException("이 모드에서 멀티샷이 독립 구체인지 구체 값인지 원문에 없습니다. 멀티샷 시나리오를 선택하세요."));
                    var count=independent?contacts:1;
                    for(int n=0;n<count;n++)
                    {
                        if(Random()<initial.Modifiers!["SpontaneousChance"])
                        {
                            var radius=c.SingularityPullRadius??throw new CombatRuleUnavailableException("5초 특이점의 끌어오기 반경은 원문에 없습니다. 시나리오 반경을 지정하세요.");var pulses=c.SingularityPulses??throw new CombatRuleUnavailableException("모드 특이점의 피해·주기·배율은 원문에 없습니다. 피해 사건 또는 해당 피해 제외 시나리오를 지정하세요.");
                            singularityEnds.Add(time+5);Log("spontaneousCreate","5초 특이점 · 해당 구체 폭발 없음",5);
                            foreach(var pulse in pulses)singularityPulses.Enqueue((pulse,allyCenter is null?targetIndex:-1),(time+pulse.AfterSpawn,singularityOrder++));
                            if(allyCenter is not null&&c.SingularityAllySpeed!=0&&c.PullMotion!="None")throw new CombatRuleUnavailableException("이동하는 아군 특이점의 지속 끌어오기 속도·추적 규칙은 원문에 없습니다. 위치를 고정하거나 끌어오기를 제외한 시나리오를 사용하세요.");var center=allyCenter??victims[targetIndex].Position(time);foreach(var pulled in victims.Where(v=>v.State.Health>0&&Math.Abs(v.Position(time)-center)<=radius+1e-9).ToArray())PullTarget(pulled,center,5);SelectVictim(victims[targetIndex]);
                        }
                        else
                        {
                            Log("spontaneousExplosion","스폰테이니어스 · 즉시 기폭",1);var source=Current() with{AttackIndex=2,Conditions=Current().Conditions! with{SimulorValue=independent?1:beamPellets,SimulorUnstacked=true}};
                            if(allyCenter is {} at)
                            {
                                if(c.SingularityAllyFailedExplosion??throw new CombatRuleUnavailableException("아군 접촉에서 특이점이 생성되지 않을 때의 폭발 여부는 원문에 없습니다. 실패 시나리오를 선택하세요."))foreach(var recipient in victims.Where(v=>v.State.Health>0).ToArray())Impact(2,1,1,1,shotArcaneStacks,finalShot,shot,recipient.Index,spread:true,radialDistance:Math.Abs(recipient.Position(time)-at),snapshot:source,headshotOverride:false);
                            }
                            else Impact(2,1,1,1,shotArcaneStacks,finalShot,shot,targetIndex,snapshot:source);
                        }
                    }
                    SelectVictim(before);return;
                }
                var persistentField=RangedFields.Get(weapon.Id);
                if(persistentField is {RepeatAfterReload:true}&&component==persistentField.Child&&build.Conditions?.SpearEmbeddedInEnemy!=true&&shot!=activeGroundSpear)return;
                var previousVictim=victim;var previousHeadshot=impactHeadshot;var previousComponent=impactComponent;impactComponent=component;var previousTendril=tendrilHit;tendrilHit=chained&&passive?.Kind=="Ocucor";
                SelectVictim(victims[targetIndex]);impactHeadshot=headshotOverride??(chained?build.Conditions?.RicochetHeadshot==true:build.Conditions?.Headshot==true);
                var chain=RangedChains.Get(weapon.Id,component);if(passive?.Kind=="Grimoire"&&component==2&&snapshot is not null)chain=chain! with{Count=2+(int)Math.Floor(Math.Max(0,RangedEngine.Calculate(snapshot).Multishot-1)*3+1e-9)};if(irradiated||secondaryRoot||chained&&chain?.Ricochet!=true)impactHeadshot=false;
                var voidBlocked=chain?.VoidPrevents==true&&Status().GetValueOrDefault(DamageType.Void)>0;
                if(contacts==0&&(build.Conditions?.MissedShots??[]).Contains(shot)&&missedShots.Add(shot)){doubleTapStacks=0;EvolutionEvent("Miss");Log("miss","지정한 발사 빗나감",shot);}
                if(!chained&&!spread&&component==0&&contacts>0&&RangedTendrils.Applies(weapon))
                {
                    var c=build.Conditions??new();if(double.IsNaN(tendrilStarted)||weapon.Category=="primary"&&tendrilAim!=targetIndex){tendrilStarted=time;tendrilAim=targetIndex;}
                    var source=Current() with{AttackIndex=component};var statistics=RangedEngine.Calculate(source);var range=statistics.Modifiers!.GetValueOrDefault("EffectiveRange",double.PositiveInfinity);
                    if(weapon.Category=="primary")
                    {
                        if(time-tendrilStarted+1e-9>=RangedTendrils.Delay(null,c))
                        {
                            var directTargets=new[]{targetIndex}.Concat(RangedPenetration.Plan(weapon,0,statistics,c,targetIndex).Select(p=>p.Target)).Where(i=>PlayerDistance(i)<=range+1e-9).Distinct().ToArray();
                            foreach(var index in directTargets)Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,index,spread:true,pierced:index!=targetIndex);
                            var positions=victims.Select(v=>v.Position(time)).ToArray();var alive=victims.Select(v=>v.State.Health>0).ToArray();var rule=RangedChains.Get(weapon.Id,0)!;
                            if(RangedTendrils.ChainOrigin(directTargets,alive,positions,rule.Radius) is {} origin)
                            {
                                foreach(var index in directTargets)alive[index]=false;
                                foreach(var hop in RangedChains.Traverse(rule,origin,alive,c.GroupSpacing,positions))Impact(component,1,beamScale*hop.Scale,beamPellets,shotArcaneStacks,finalShot,shot,hop.Target,chained:true);
                            }
                        }
                        else Log("tendrilAcquiring","촉수 표적 획득 중 · 탄약 소비 유지",time-tendrilStarted);
                    }
                    else
                    {
                        var routes=c.TendrilRoutes??throw new CombatRuleUnavailableException("보조 버미스플라이서의 5개 촉수가 붙을 대상·빗나감·조준 방식을 설정하세요.");
                        var ramp=beamPellets>0?beamScale/beamPellets:0;
                        foreach(var route in routes)
                        {
                            if(route.Miss||victims[route.Target].State.Health<=0||PlayerDistance(route.Target)>range+1e-9)continue;
                            if(time-tendrilStarted+1e-9<RangedTendrils.Delay(route.AcquisitionSeconds,c)){Log("tendrilAcquiring","촉수 표적 획득 중 · 탄약 소비 유지",time-tendrilStarted);continue;}
                            var multi=Roll(statistics.Multishot/5);if(multi==0)continue;
                            var routeBuild=source with{Distance=PlayerDistance(route.Target),Conditions=source.Conditions! with{Headshot=!route.AutoTarget&&c.Headshot}};
                            Impact(component,1,multi*ramp,multi,shotArcaneStacks,finalShot,shot,route.Target,spread:true,snapshot:routeBuild,headshotOverride:!route.AutoTarget&&c.Headshot);
                            foreach(var penetrated in RangedPenetration.Plan(weapon,0,statistics,c,route.Target).Where(p=>PlayerDistance(p.Target)<=range+1e-9))Impact(component,1,multi*ramp,multi,shotArcaneStacks,finalShot,shot,penetrated.Target,spread:true,snapshot:routeBuild with{Distance=PlayerDistance(penetrated.Target)},headshotOverride:false,pierced:true);
                        }
                    }
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(!chained&&!spread&&contacts>0&&passive?.Kind=="Ocucor")
                {
                    var tendrils=Math.Min(4,weaponStacks);var range=20+RangedEngine.Calculate(Current()).Modifiers!.GetValueOrDefault("BeamRange");
                    var roots=victims.Where(v=>v.Index!=targetIndex&&v.State.Health>0&&Math.Atan2(Between(v.Index,targetIndex),build.Distance)*180/Math.PI<=40+1e-9&&Math.Sqrt(Math.Pow(build.Distance,2)+Math.Pow(Between(v.Index,targetIndex),2))<=range+1e-9).OrderBy(v=>Math.Abs(v.Index-targetIndex)).Take(tendrils).ToArray();
                    Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,targetIndex,false,true);
                    foreach(var root in roots)Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,root.Index,true);
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(!chained&&!spread&&contacts>0&&chain is {ImpactRadius:>0})
                {
                    Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,targetIndex,false,true);
                    foreach(var other in victims.Where(v=>v.Index!=targetIndex&&v.State.Health>0&&Between(v.Index,targetIndex)<=chain.ImpactRadius+1e-9).ToArray())
                        Impact(component,contacts,beamScale/Math.Max(1,beamPellets),1,shotArcaneStacks,finalShot,shot,other.Index,false,true,secondaryRoot:true);
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(!chained&&!spread&&contacts>0&&chain is {AutoBeams:>1})
                {
                    var roots=victims.Where(v=>v.State.Health>0&&Math.Atan2(Between(v.Index,targetIndex),build.Distance)*180/Math.PI<=10+1e-9).OrderBy(v=>Math.Abs(v.Index-targetIndex)).Take(chain.AutoBeams).ToArray();
                    foreach(var root in roots)Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,root.Index,false,true,secondaryRoot:root.Index!=targetIndex);
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                var componentAttack=RangedSimulor.Attack(weapon,RangedEngine.Attacks(weapon)[component],component,snapshot?.Conditions??build.Conditions??new());componentAttack=RangedOrdinarySimulor.Attack(weapon,componentAttack,component,snapshot?.Conditions??build.Conditions??new());componentAttack=RangedStug.Attack(weapon,componentAttack,component,snapshot?.Conditions??build.Conditions??new());componentAttack=RangedInfestedOrbs.Attack(weapon,componentAttack,component,snapshot?.Conditions??build.Conditions??new());var isRadial=RangedEngine.S(componentAttack,"ShotType")=="AoE";
                if(contacts>0&&victim.Nullifier is {Active:true} bubble)
                {
                    var blocked=!isRadial||(nullifierScenario.RadialBlocked??throw new CombatRuleUnavailableException("널리파이어의 방사 피해 차단은 폭발 중심 위치에 달려 있습니다. 막 외부 차단 / 내부 폭발 시나리오를 선택하세요."));
                    if(blocked)
                    {
                        var chance=weapon.Id=="wiki:primary/Miter"&&component is 0 or 1&&RangedNullifiers.Neutralizable(nullifierScenario.Kind)?initial.Modifiers!.GetValueOrDefault("NeutralizingChance"):0;var popped=false;
                        for(int n=0;n<contacts;n++)if(Random()<chance){popped=true;break;}
                        if(popped){bubble.Destroy();contacts--;dissolvedProjectiles[shot]=dissolvedProjectiles.GetValueOrDefault(shot)+1;Log("nullifierDestroyed","중화 저스티스 · 생성기 영구 파괴",1);}
                        else
                        {
                            var hit=Current() with{AttackIndex=component,Conditions=Current().Conditions! with{Headshot=false,Statuses=[],RadialDistance=radialDistance??0}};var damage=RangedEngine.Calculate(hit);
                            bubble.Hit(time,damage.Damage.Values.Sum()*contacts*beamScale,damage.Damage.GetValueOrDefault(DamageType.Magnetic)>0);dissolvedProjectiles[shot]=dissolvedProjectiles.GetValueOrDefault(shot)+contacts;Log("nullifierBlocked","널리파이어 막 · 비치명 피해 프레임 합산",contacts);
                            SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                        }
                    }
                }
                if(!chained&&!spread&&!irradiated)contacts=CollectShotOrb(component,shot,contacts,isRadial);
                if(RangedAugmentFields.Get(weapon.Id) is {} augmentField&&component==augmentField.Parent&&!spread&&!chained&&initial.Modifiers!.GetValueOrDefault("NapalmTicks")>0)
                    for(int tick=0;tick<(int)Math.Round(initial.Modifiers!["NapalmTicks"]);tick++)impacts.Enqueue((augmentField.Child,contacts,1,1,shotArcaneStacks,false,shot,targetIndex,false),(time+tick,eventOrder++));
                if(weapon.Id==RangedPullWeapons.Scyotid&&component==1&&!spread)
                {
                    var stats=RangedEngine.Calculate(Current());var maximum=RangedPullWeapons.TargetLimit(stats,build.Conditions??new());var center=victim.Position(time);pullCenters[shot]=center;
                    var recipients=victims.Where(v=>v.State.Health>0&&Math.Sqrt(Math.Pow(v.Position(time)-center,2)+Math.Pow(build.Conditions?.RadialDistance??0,2))<=15+1e-9).OrderBy(v=>Math.Abs(v.Position(time)-center)).ThenBy(v=>v.Index).Take(maximum).ToArray();
                    foreach(var other in recipients){var distance=Math.Sqrt(Math.Pow(other.Position(time)-center,2)+Math.Pow(build.Conditions?.RadialDistance??0,2));Impact(component,contacts>0?1:0,1,1,shotArcaneStacks,finalShot,shot,other.Index,false,true,distance);if(contacts>0){SelectVictim(other);PullTarget(other,center,6);}}
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(isRadial&&!spread&&reloadGrenade is not null&&component is 2 or 4&&initial.Modifiers!.GetValueOrDefault("CriticalMutationStep")>0)
                {
                    var radius=RangedEngine.N(componentAttack,"Range")!.Value;
                    for(int grenade=0;grenade<contacts;grenade++)
                    {
                        var recipients=victims.Where(v=>v.State.Health>0&&Math.Sqrt(Math.Pow(Between(v.Index,targetIndex),2)+Math.Pow(build.Conditions?.RadialDistance??0,2))<=radius+1e-9).ToArray();
                        foreach(var recipient in recipients)
                        {
                            var radial=Math.Sqrt(Math.Pow(Between(recipient.Index,targetIndex),2)+Math.Pow(build.Conditions?.RadialDistance??0,2));
                            Impact(component,1,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,recipient.Index,false,true,radial);
                        }
                        if(recipients.Length<3){mutation=Math.Max(0,mutation-initial.Modifiers!["CriticalMutationStep"]);Log("criticalMutationLoss","크리티컬 뮤테이션 · 유탄 적중 3명 미만",mutation);}
                    }
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(isRadial&&!spread&&victims.Length>1)
                {
                    foreach(var other in victims.Where(v=>v.State.Health>0).ToArray())
                    {
                        var lateral=(weapon.Id==RangedPullWeapons.Proboscis||RangedPentaAugments.Applies(weapon))&&pullCenters.TryGetValue(shot,out var fixedCenter)?Math.Abs(other.Position(time)-fixedCenter):Between(other.Index,targetIndex);
                        var distance=Math.Sqrt(lateral*lateral+Math.Pow(build.Conditions?.RadialDistance??0,2));
                        Impact(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,other.Index,chained,true,distance,snapshot:snapshot);
                    }
                    if(component==1)ApplyNapalmPatch(shot,contacts,targetIndex);
                    SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;
                }
                if(RangedAugmentFields.Get(weapon.Id) is {} napalm&&component==napalm.Child&&victim.RagdollUntil>time){SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;}
                int flatCount=0;RangedResult? flatSource=null;
                if(!isRadial&&!chained&&!irradiated&&contacts>0&&initial.Modifiers!.GetValueOrDefault("ThunderboltChance")>0)
                {
                    flatSource=RangedEngine.Calculate(Current());flatCount=FlatExplosionCount(flatSource,contacts,false);
                    if(flatCount>0)
                    {
                        if(build.Conditions?.ThunderboltDirectRule is null or "Unspecified")throw new CombatRuleUnavailableException("썬더볼트 원문은 화살 소멸을 명시하지만 명중 직격의 선후를 명시하지 않습니다. 직격 대체/유지 시나리오를 선택하세요.");
                        if(build.Conditions.ThunderboltDirectRule=="Replace")contacts-=flatCount;
                        dissolvedProjectiles[shot]=dissolvedProjectiles.GetValueOrDefault(shot)+flatCount;
                    }
                }
                var zymosHead=RangedZymos.Applies(weapon)&&component==0&&impactHeadshot&&target.Overguard<=0&&build.Conditions?.ZymosStunImmune!=true;
                var componentForced=componentAttack.TryGetProperty("ForcedProcs",out var fj)?fj.EnumerateArray().Where(p=>!IsControl(p.GetString()!)).Select(p=>Enum.TryParse<DamageType>(p.GetString(),out var t)?t:throw new CombatRuleUnavailableException("특수 강제 상태 시간 규칙 미연결: "+p.GetString())).ToArray():[];
                if(RangedZymos.Applies(weapon)&&component==3&&(snapshot?.Conditions?.ZymosSporeDistance??0)>7)componentForced=[DamageType.Impact];
                if(RangedKitguns.Get(weapon.Id)?.Chamber=="Catchmoon"&&!RangedPaxSeeker.Is(componentAttack)&&build.Distance>9)componentForced=componentForced.Where(p=>p!=DamageType.Impact).ToArray();
                if(persistentField is {BlockedByDefenses:true}&&component==persistentField.Child&&(target.Overguard>0||target.Shields>0&&MeleeTargetModel.EffectiveArmor(target.Armor,new(Statuses:Status(),HeatArmorStrip:HeatStrip()))>0))
                {Log("fieldBlocked","테넷 페록스 · 오버가드 또는 실드와 방어력으로 장판 차단",0);SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;}
                var controls=componentAttack.TryGetProperty("ForcedProcs",out var controlsJson)?controlsJson.EnumerateArray().Select(p=>p.GetString()!).Where(IsControl).ToArray():[];
                if(contacts>0&&!isRadial&&magazineHits.Add(shot)&&initial.Modifiers!.GetValueOrDefault("MagazineHitBonus")>0)Log("magazineHit","현재 탄창 명중 발사 수",Math.Min(15,magazineHits.Count));
                double radiationSpreadContacts=0;int successfulHeadPellets=0,actualContacts=0;
                var useDoubleTap=!isRadial&&initial.Modifiers!.GetValueOrDefault("DoubleTapDamage")>0;
                var doubleTapNext=0;
                if(useDoubleTap&&contacts>0)
                {
                    var active=time-doubleTapAt<2?doubleTapStacks:0;
                    var hits=contacts*(Status().GetValueOrDefault(DamageType.Void)>0?2:1);
                    doubleTapNext=Math.Min(20,active+hits);doubleTapStacks=Math.Min(20,active+hits-1);doubleTapAt=time;
                }
                if(isRadial&&radialDistance is {} actualRadius)
                {
                    var radialBuild=(snapshot??Current()) with{AttackIndex=component};
                    var radialStats=RangedEngine.Calculate(radialBuild with{Conditions=radialBuild.Conditions! with{RadialDistance=actualRadius}});
                    if(radialStats.Modifiers!.TryGetValue("EffectiveRange",out var radius)&&actualRadius>radius+1e-9)
                    {SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;return;}
                }
                Log(chained?"chain":isRadial?"explosion":"impact",RangedEngine.S(componentAttack,"AttackName"),contacts);
                    for(int pellet=0;pellet<contacts&&target.Health>0;pellet++)
                    {
                        var hitBuild=(snapshot??Current()) with{AttackIndex=component};if(chargedSalvos.TryGetValue(shot,out var salvo))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{ChargedRounds=salvo,PhantasmaChargeAmmo=salvo,PhantasmaChargeSnapshot=salvo,LoadedAmmo=null}};if(dodgeSnapshots.TryGetValue(shot,out var dodgeAtLaunch))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{DodgeShotsRemaining=dodgeAtLaunch}};if(shotOrigins.TryGetValue(shot,out var origin))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{SourceAttack=origin.Attack,AttachedTarget=targetIndex==origin.Target}};if(component is 2 or 3&&passive?.Kind=="Onos"&&chargeSnapshots.TryGetValue(shot,out var chargeSnapshot))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{ChargeDamageStacks=chargeSnapshot}};if(snapshot is not null)hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{Statuses=Status(),Headshot=impactHeadshot}};if(alternateRule?.Alternate.Contains(component)==true&&alternateShotCharges.TryGetValue(shot,out var chargesAtLaunch))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{AlternateCharge=chargesAtLaunch/(double)alternateRule.Capacity}};if(dischargeSnapshots.TryGetValue(shot,out var dischargeAtLaunch))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{DischargeStored=dischargeAtLaunch}};if(buckshotSnapshots.TryGetValue(shot,out var buckshotAtLaunch))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{BuckshotReady=buckshotAtLaunch}};if(barrageSnapshots.TryGetValue(shot,out var barrageAtLaunch))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{SentientBarrageReady=barrageAtLaunch}};if(pierced)hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{AfterPunchThrough=true}};if(radialDistance is {} radius)hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{RadialDistance=radius}};if(snapshot is null&&arcane?.Code is "Sharpshot" or "Surge")hitBuild=hitBuild with{ArcaneStacks=shotArcaneStacks};
                        if(RangedInfestedOrbs.Applies(weapon)&&component==0&&crossedOrbs.TryGetValue(shot,out var through))hitBuild=hitBuild with{Conditions=hitBuild.Conditions! with{InfestedOrbs=[..hitBuild.Conditions.InfestedOrbs??[],..through]}};
                        var result=RangedEngine.Calculate(hitBuild with{Conditions=hitBuild.Conditions! with{FinalShot=finalShot,FirstMagazineShot=firstMagazineShots.Contains(shot),FinalBurst=finalBurstShots.Contains(shot)}},pellet>=RangedEngine.Calculate(hitBuild).Modifiers!.GetValueOrDefault("NativeMultishot"),irradiated);if(result.Damage.Values.Sum()<=0)continue;var tier=Roll(result.CriticalChance);if(tier>0&&Random()<result.Modifiers!.GetValueOrDefault("CriticalTierUpgradeChance"))tier++;var critical=Math.Max(0,1+tier*(result.CriticalMultiplier-1));if(tier==0&&Random()<result.Modifiers!.GetValueOrDefault("NonCriticalAttritionChance"))critical*=1+result.Modifiers!.GetValueOrDefault("NonCriticalAttritionBonus");
                        var beforeStatus=Status();if(!irradiated&&!isRadial&&arcane?.Code=="Irradiate"&&beforeStatus.GetValueOrDefault(DamageType.Radiation)>=10)radiationSpreadContacts++;
                        var projectileBonus=pellet>=result.Modifiers!.GetValueOrDefault("NativeMultishot")?1+result.Modifiers!.GetValueOrDefault("ExtraMultishotDamage"):1;
                        if(!irradiated)VeloxHit();
                        critical*=projectileBonus;victim.Hits++;actualContacts++;Damage(result.Damage,critical*beamScale,radial:isRadial);
                        if(component==1&&alternateRule is not null&&result.Modifiers!.GetValueOrDefault("SentientBarrageCritical")>0){alternateCharges=Math.Min(alternateRule.Capacity,alternateCharges+1);if(alternateCharges==alternateRule.Capacity)barrageReady=true;Log("barrageCharge","센티언트 배라지 · 적중 충전",alternateCharges);}
                        if(!isRadial){if(!shotTargets.TryGetValue(shot,out var distinctTargets)){distinctTargets=[];shotTargets[shot]=distinctTargets;}distinctTargets.Add(victim.Index);if(distinctTargets.Count>=3){if(result.Modifiers!.GetValueOrDefault("ThreeTargetInvisibilityDuration") is >0 and var invisibleDuration){unseenUntil=time+invisibleDuration;Log("invisible","언씬 드레드 · 한 발사로 서로 다른 적 3명",invisibleDuration);}if(pierced&&result.Modifiers!.GetValueOrDefault("PunchThroughAmmoBuff")>0){punchBuffUntil=time+20;Log("punchThroughBuff","적 3명 관통 · 탄약 효율 20초",result.Modifiers!["PunchThroughAmmoBuff"]);}}}
                        if(!isRadial&&result.Modifiers!.GetValueOrDefault("EntropyExplosionChance")>0)EntropySpike(result);if(result.Modifiers!.GetValueOrDefault("PetrifyHits") is >0 and var threshold&&++victim.MetamorphicHits>=threshold){victim.MetamorphicHits=0;if(target.Overguard<=0&&target.Health>0)Log("petrify","메타모픽 매거진 · 석화 (초)",result.Modifiers!["PetrifyDuration"]);}EvolutionEvent("Hit");if(passive?.Kind=="Grimoire"&&component>=2)EvolutionEvent("TomeAlternateHit");if(beforeStatus.GetValueOrDefault(DamageType.Electricity)>0)EvolutionEvent("ElectricTargetHit");if(!isRadial){EvolutionEvent("DirectHit");if(RangedEngine.S(weapon.Raw,"Family")=="Harpak"&&component==1)EvolutionEvent("HarpoonHit");if(passive?.Kind=="Grimoire"&&component==0)alternateReadyAt=Math.Max(time,alternateReadyAt-1);if(hitBuild.Conditions?.AfterPunchThrough==true)EvolutionEvent("PunchThrough");if(!impactHeadshot)EvolutionEvent("Bodyshot");}
                        if(incarnon is not null&&!isRadial&&!RangedIncarnons.IsForm(weapon,component)&&(incarnon.ChargeOnBody||impactHeadshot))incarnonGauge+=incarnon.ChargeGain*(1+result.Modifiers!.GetValueOrDefault("IncarnonChargeBonus"));
                        if(impactHeadshot&&!isRadial)
                        {
                            if(passive?.Kind=="Perigale")
                            {
                                var group=shotBursts.TryGetValue(shot,out var galeBurst)?galeBurst.Group:shot;
                                var heads=burstHeadContacts.GetValueOrDefault(group)+1;burstHeadContacts[group]=heads;
                                if(heads>=4||target.Health<=0){weaponStacks=1;weaponRefreshed=time;Log("weaponBuff","게일 포스 · 한 점사 4회 약점 적중 또는 약점 처치 · 탄약 무한 4초",heads);}
                            }
                            if(++successfulHeadPellets==5)EvolutionEvent("FiveHeadPellets");lastHeadshot=time;EvolutionEvent("Headshot");if(result.Cadence!.BurstCount>1)EvolutionEvent("BurstHeadshot");if(selection.Eximus)EvolutionEvent("EximusHeadshot");if(component==0)EvolutionEvent("PrimaryHeadshot");
                            if(result.Modifiers!.GetValueOrDefault("HeadshotReloadStacks")>0){evoHeadshots=Math.Min(3,Math.Max(0,evoHeadshots-(int)Math.Floor((time-evoHeadshotAt)/12))+1);evoHeadshotAt=time;}
                            if(result.Modifiers!.GetValueOrDefault("LingeringHeadshot")>0){if(time-lastPairedHeadshot<=2){lingeringUntil=time+8;lastPairedHeadshot=-100;}else lastPairedHeadshot=time;}

                            if(passive?.Kind is "Knell" or "Toxocyst" or "Spirex")
                            {
                                if(passive.Kind=="Knell")weaponStacks=Math.Min(3,Math.Max(0,weaponStacks-(int)Math.Floor((time-weaponRefreshed)/2))+1);
                                else if(passive.Kind=="Spirex")weaponStacks=Math.Min(2,(time-weaponRefreshed>=4?0:weaponStacks)+1);
                                else weaponStacks=1;
                                weaponRefreshed=time;Log("weaponBuff","무기 고유 약점 적중 효과",weaponStacks);
                            }
                        }
                        if(!isRadial&&passive?.ComboMinimum>0&&build.Conditions?.Aiming!=false){comboHits=hitBuild.Conditions!.SniperComboHits+1;comboAt=time;}
                        if(passive?.Kind=="Microwave")victim.Microwave=true;
                        if(arcane?.Code=="Crux"&&impactHeadshot&&!isRadial)ArcaneStack();
                        if(arcane?.Code=="Enervate"&&!azimaHit)
                        {
                            if(time-lastEnervate>=1/30d-1e-9){ArcaneStack();lastEnervate=time;}
                            if(tier>=2&&time-lastBigCrit>=1/30d-1e-9){bigCrits++;lastBigCrit=time;if(bigCrits>=build.ArcaneRank+1){arcaneStacks=0;bigCrits=0;Log("arcaneReset","이너베이트 치확 초기화");}}
                        }
                        var total=result.Damage.Values.Sum();if(total<=0)continue;
                        var cedoExplosion=passive?.Kind=="Cedo"&&component==2;var types=new List<DamageType>();for(int n=0,roll=cedoExplosion?1:Roll(result.StatusChance*(result.Cadence!.Beam&&!isRadial?beamPellets:1));n<(irradiated?Math.Min(1,roll):roll);n++)
                        {
                            var pick=Random()*total;
                            foreach(var pair in result.Damage){pick-=pair.Value;if(pick<0){types.Add(pair.Key);break;}}
                        }
                        if(component==1&&result.Modifiers!.GetValueOrDefault("HigasaForcedStatus")>0)types.Add(build.Conditions?.HigasaForcedStatus??throw new CombatRuleUnavailableException("히가사 강제 상태 결과 시나리오를 선택하세요. 원문의 무작위 종류·가중치가 없습니다."));
                        if(cedoExplosion)types.Add(new[]{DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin}[(int)(Random()*4)]);
                        for(int n=0;n<Roll(result.Modifiers!.GetValueOrDefault("ExtraElectricBaseChance")*Math.Max(0,1+result.Modifiers!.GetValueOrDefault("StatusChance")));n++)types.Add(DamageType.Electricity);
                        if(!irradiated)types.AddRange(componentForced);
                        if(tier==0&&types.Count==0&&result.Modifiers!.GetValueOrDefault("OverwhelmingAttrition")>0){attrition=Math.Min(3,Math.Max(0,attrition-(int)Math.Floor((time-attritionAt)/10))+1);attritionAt=time;Log("evolution","오버웰밍 어트리션",attrition);}
                        var bonusSlash=tier>0&&Random()<result.Modifiers!.GetValueOrDefault("CriticalSlashChance");
                        var impact=types.Count(t=>t==DamageType.Impact);
                        for(int n=0;n<impact;n++)
                        {
                            var doubleChance=result.FireRate<2.5?2:1;
                            if(Random()<result.Modifiers!.GetValueOrDefault("ImpactSlashChance")*doubleChance)bonusSlash=true;
                            if(Random()<result.Modifiers!.GetValueOrDefault("ImpactMagneticChance")*doubleChance)types.Add(DamageType.Magnetic);
                        }
                        if(bonusSlash)types.Add(DamageType.Slash);
                        foreach(var type in types)Add(type,result,critical,beamScale);
                        if(types.Count>0&&result.Modifiers!.GetValueOrDefault("HealPerThreeStatusHits")>0)
                        {
                            healingProgress++;if(healingProgress==3){healingProgress=0;var before=playerHealth;playerHealth=Math.Min(MaxHealth(),playerHealth+result.Modifiers!["HealPerThreeStatusHits"]);Log("healing","비샤즈 발 · 상태 명중 3회 회복",playerHealth-before);}
                        }
                        if(target.Overguard==0)foreach(var control in controls)
                        {
                            if(control is "Knockdown" or "Lifted" or "Ragdoll")
                            {
                                var recovery=build.Conditions?.PostureRecoverySeconds;
                                if(recovery is null&&(result.Modifiers!.GetValueOrDefault("ActiveCO")+result.Modifiers!.GetValueOrDefault("InnateCO"))>0)
                                    throw new CombatRuleUnavailableException("강제 자세 이상 회복 시간이 위키에 기재되지 않아 이후 CO 피해를 확정할 수 없습니다. 상황 설정에서 회복 시간을 명시하면 해당 가정으로 계산합니다.");
                                victim.PostureUntil=time+(recovery??0);if(control=="Ragdoll")victim.RagdollUntil=victim.PostureUntil;
                            }
                            Log("control",control+" · 명중 위치 고정 가정");
                        }
                        var compound=types.FirstOrDefault(t=>beforeStatus.GetValueOrDefault(t)>=10&&t is DamageType.Blast or DamageType.Radiation or DamageType.Gas or DamageType.Magnetic or DamageType.Viral or DamageType.Corrosive);
                        if(beforeStatus.GetValueOrDefault(compound)>=10&&Random()<result.Modifiers!.GetValueOrDefault("DebilitateChance"))
                        {
                            var pair=compound switch{DamageType.Blast=>new[]{DamageType.Heat,DamageType.Cold},DamageType.Radiation=>[DamageType.Heat,DamageType.Electricity],DamageType.Gas=>[DamageType.Heat,DamageType.Toxin],DamageType.Magnetic=>[DamageType.Cold,DamageType.Electricity],DamageType.Viral=>[DamageType.Cold,DamageType.Toxin],_=>new[]{DamageType.Electricity,DamageType.Toxin}};
                            Add(pair[Random()<.5?0:1],result,critical,beamScale,seedMultiplier:result.FactionFactor);
                        }
                        // Encumber rolls at most once per damage instance and succeeds at most once at the same instant.
                        if(types.Count>0&&lastEncumber!=time&&Random()<result.Modifiers!.GetValueOrDefault("EncumberChance"))
                        {lastEncumber=time;var choices=Enum.GetValues<DamageType>().Where(t=>t<=DamageType.Corrosive).ToArray();Add(choices[(int)(Random()*choices.Length)],result,critical,beamScale);}
                    }

                if(useDoubleTap&&contacts>0){doubleTapStacks=doubleTapNext;Log("doubleTap","더블 탭 적중 중첩",doubleTapStacks);}
                if(!chained&&contacts>0&&!isRadial&&shotBursts.TryGetValue(shot,out var burst)&&burst.Attack==component)
                {
                    if(!burstContacts.TryGetValue(burst.Group,out var hits))burstContacts[burst.Group]=hits=[];
                    if(hits.Add(shot)&&hits.Count==burst.Size)
                    {
                        EvolutionEvent("FullBurstHit");if(impactHeadshot)EvolutionEvent("FullBurstHeadshot");
                        Log("fullBurstHit","점사 전체 명중",burst.Size);
                    }
                }
                if(returningBlades is not null&&!chained&&contacts>0)
                {
                    if(component==0)
                    {
                        for(int blade=0;blade<contacts;blade++)blades.Enqueue((shot,shotArcaneStacks));
                        if(blades.Count>returningBlades.Limit)
                        {
                            if(build.Conditions?.AllowApproximations!=true)throw new CombatRuleUnavailableException("칸타레 설치 칼날 30개 초과 시 자동 회수 순서는 위키에서도 일관적이지 않다고 명시되어 있습니다. 30개 이하에서 재장전하세요.");
                            Approximate("칸타레 칼날 한도 초과는 가장 오래된 칼날부터 회수하는 근사입니다. 문서도 회수 순서 불일치를 기록하며 복귀 피해·합창 시각에 영향이 있습니다.");
                            while(blades.Count>returningBlades.Limit){var returning=blades.Dequeue();foreach(var hit in RangedReturningBlades.Path(build.Conditions??new()))collisions.Enqueue((hit,1,1,1,1,returning.Arcane,false,returning.Shot),(time+hit.AfterImpact,collisionOrder++));}
                        }
                    }
                    else if(component==1&&actualContacts>0){cantareUntil=time+(build.Conditions?.JadeEnsemble==true?returningBlades.EnsembleDuration:returningBlades.Duration);Log("cantareChorale","복귀 칼날 명중 · 재장전 속도 버프 갱신",build.Conditions?.JadeEnsemble==true?returningBlades.EnsembleBonus:returningBlades.ReloadBonus);}
                }
                if(recall is not null&&component==recall.Contact&&!chained)
                {
                    for(int bullet=0;bullet<contacts;bullet++){embedded.Enqueue((targetIndex,shot,finalShot,shotArcaneStacks));if(embedded.Count>recall.Limit)RecallRound(embedded.Dequeue());}
                }
                if(actualContacts>0)Cannonade(RangedEngine.Calculate(Current() with{AttackIndex=component}),actualContacts,isRadial);
                if(flatCount>0){FlatExplosions(flatSource!,flatCount);if(build.Conditions?.ThunderboltDirectRule=="Add")contacts-=flatCount;}
                if(RangedZymos.Applies(weapon)&&component==0&&actualContacts>0)
                {
                    if(zymosHead)
                    {
                        var c=build.Conditions??new();RangedZymos.RequireHead(c);Log("zymosBurrow","자이모스 · 약점 굴착",1.1);
                        foreach(var tick in c.ZymosBurrowTicks!)zymosEvents.Enqueue((1,shot,targetIndex,actualContacts,targetIndex,0,true,tick),(time+tick.Time,zymosOrder++));
                        zymosEvents.Enqueue((2,shot,targetIndex,actualContacts,targetIndex,0,true,null),(time+1.1,zymosOrder++));
                    }
                    else
                    {
                        if(impactHeadshot&&!zymosHead){if(build.Conditions?.AllowApproximations!=true)throw new CombatRuleUnavailableException("기절 면역 대상은 포자를 생성하지 않습니다. 이때 초기 약점 폭발이 몸통 폭발로 대체되는지는 원문에 없으므로 몸통 조준으로 계산하세요.");Approximate("자이모스 기절 면역 약점 명중은 포자 없이 몸통 명중 폭발로 근사합니다. 대체 폭발 규칙 미기재로 직접 피해·TTK에 영향이 있습니다.");}
                        Impact(1,actualContacts,1,1,shotArcaneStacks,finalShot,shot,targetIndex,headshotOverride:false);
                    }
                }
                if(RangedPentaAugments.Applies(weapon)&&initial.Modifiers!.GetValueOrDefault("PentaNapalm")>0)
                {
                    if(component==0&&actualContacts>0)Impact(2,actualContacts,1,1,shotArcaneStacks,finalShot,shot,targetIndex,headshotOverride:impactHeadshot);
                    if(component==1&&!spread)ApplyNapalmPatch(shot,contacts,targetIndex);
                }
                var adhesiveStuck=!chained&&!spread?StickyContacts(component,contacts):0;
                if(manualGrenades&&component==0&&contacts>0)grenadeRack.Land(shot,time,initial.Modifiers!.GetValueOrDefault("TetherTargets")>0?RangedPentaAugments.Delay(build):initial.Modifiers!.GetValueOrDefault("PentaNapalm")>0&&adhesiveStuck>0?RangedAdhesive.Delay(weapon,build,component,time-adhesiveLaunchTimes[shot]):null,targetIndex,adhesiveStuck);
                if(RangedPentaAugments.Applies(weapon)&&component==0&&contacts>0&&initial.Modifiers!.GetValueOrDefault("TetherTargets")>0)
                {
                    pullCenters[shot]=victim.Position(time);
                    if(build.Conditions?.TetherAirburst!=true&&build.Conditions?.TetherTargetImmune!=true)
                    {
                        var origin=victim;var radius=initial.Modifiers!["TetherRadius"];var limit=(int)initial.Modifiers!["TetherTargets"];
                        foreach(var pulled in victims.Where(v=>v.State.Health>0&&Between(v.Index,origin.Index)<=radius+1e-9).OrderBy(v=>Between(v.Index,origin.Index)).ThenBy(v=>v.Index).Take(limit).ToArray()){SelectVictim(pulled);PullTarget(pulled,origin.Position(time),RangedPentaAugments.Delay(build));pulled.PullGrenade=shot;}SelectVictim(origin);
                    }
                    Log("tetherLand",build.Conditions?.TetherAirburst==true?"테더 유탄 · 착지 전 기폭":"테더 유탄 · 지형 부착",RangedPentaAugments.Delay(build));
                }
                if(weapon.Id==RangedPullWeapons.Proboscis&&component==0&&contacts>0&&!chained)
                {
                    var center=victim.Position(time);pullCenters[shot]=center;
                    foreach(var tick in RangedPullWeapons.TendrilTimes(build.Conditions??new()))impacts.Enqueue((1,contacts,1,1,shotArcaneStacks,false,shot,targetIndex,false),(time+tick,eventOrder++));
                    impacts.Enqueue((2,contacts,1,1,shotArcaneStacks,false,shot,targetIndex,false),(time+1.7,eventOrder++));
                    var origin=victim;foreach(var pulled in victims.Where(v=>v.State.Health>0&&Math.Abs(v.Position(time)-center)<=9+1e-9).ToArray()){SelectVictim(pulled);PullTarget(pulled,center,1.7,panic:pulled.Index==targetIndex);}SelectVictim(origin);
                }

                if(weapon.Id=="wiki:primary/Paracyst"&&component==1&&contacts>0&&!chained&&target.Overguard<=0&&target.Health>0&&PlayerDistance(targetIndex)>25)
                {
                    var c=build.Conditions??new();var duration=c.PullSeconds??throw new CombatRuleUnavailableException("작살의 끌어오기 속도는 원문에 없습니다. 근사 계산을 켜거나 이동 시간을 지정하세요.");
                    var point=TargetPoint(victim);var ratio=25/PlayerDistance(targetIndex);victim.ForwardFrom=victim.Forward(time);victim.PositionFrom=victim.Position(time);
                    if(c.TargetsInLine){victim.ForwardTo=point.X*ratio-build.Distance-victim.PositionFrom;victim.PositionTo=victim.PositionFrom;}
                    else{victim.ForwardTo=point.X*ratio-build.Distance;victim.PositionTo=point.Y*ratio;}
                    victim.MoveStart=time;victim.MoveEnd=time+duration;Log("pull","파라시스트 작살 · 지정 시간에25m까지 이동",duration);
                }
                if(!chained)ScheduleChildren(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,targetIndex,adhesiveStuck);
                if(!chained&&contacts>0&&RangedVectis.Applies(weapon)&&component==1)
                {
                    impacts.Enqueue((3,contacts,1,1,shotArcaneStacks,finalShot,shot,targetIndex,false),(time,eventOrder++));
                    if(impactHeadshot)impacts.Enqueue((2,contacts*2,1,1,shotArcaneStacks,finalShot,shot,targetIndex,false),(time+.4,eventOrder++));
                    else foreach(var tick in RangedVectis.BodyTicks(build.Conditions??new()))impacts.Enqueue((3,contacts,1,1,shotArcaneStacks,finalShot,shot,targetIndex,false),(time+tick,eventOrder++));
                }
                if(!chained)ScheduleField(component,contacts,beamScale,beamPellets,shotArcaneStacks,finalShot,shot,targetIndex,adhesiveStuck);
                if(radiationSpreadContacts>0)
                {
                    var radius=4.5+.5*build.ArcaneRank;
                    var recipients=victims.Where(v=>v.Index!=targetIndex&&v.State.Health>0&&Between(v.Index,targetIndex)<=radius+1e-9).ToArray();
                    if(recipients.Length>0&&RangedEngine.S(componentAttack,"ShotType")=="Projectile"){if(build.Conditions?.AllowApproximations!=true)throw new CombatRuleUnavailableException("세컨더리 이라디에이트: 투사체별 전파 피해 계수가 서로 다르며, 위키의 사례만으로 이 공격의 정확한 계수를 정할 수 없습니다.");Approximate("세컨더리 이라디에이트 투사체 전파는 히트스캔과 같은 랭크별 계수를 사용합니다. 투사체별 계수 미기재로 전파 피해·TTK에 영향이 있습니다.");}
                    foreach(var other in recipients)Impact(component,1,beamScale*radiationSpreadContacts*(.8+.2*build.ArcaneRank),1,shotArcaneStacks,finalShot,shot,other.Index,true,irradiated:true);
                    SelectVictim(victims[targetIndex]);
                }
                if(!chained&&contacts>0&&chain is not null&&!voidBlocked&&!(RangedTendrils.Applies(weapon)&&component==0&&spread))
                {
                    for(int pellet=0;pellet<contacts;pellet++)
                    {
                        if(chain.Chance<1&&Random()>=chain.Chance)continue;
                        var path=RangedChains.Traverse(chain,targetIndex,victims.Select(v=>v.State.Health>0&&(!chain.ExcludeRagdoll||v.RagdollUntil<=time)).ToArray(),build.Conditions?.GroupSpacing??2,victims.Select(v=>v.Position(time)).ToArray()).ToArray();
                        foreach(var hop in path)
                        {
                            var speed=RangedEngine.S(componentAttack,"ShotType")=="Projectile"?RangedEngine.N(componentAttack,"ShotSpeed")??0:0;
                            if(speed>0)
                            {
                                var distance=Between(hop.Target,targetIndex);var effects=RangedEngine.Calculate(Current()).Modifiers!;
                                impacts.Enqueue((component,1,beamScale*hop.Scale,beamPellets,shotArcaneStacks,finalShot,shot,hop.Target,true),(time+distance/(speed*(1+effects.GetValueOrDefault("ProjectileSpeed"))),eventOrder++));
                            }
                            else Impact(component,1,beamScale*hop.Scale,beamPellets,shotArcaneStacks,finalShot,shot,hop.Target,true,snapshot:snapshot);
                        }
                    }
                }
                SelectVictim(previousVictim);impactHeadshot=previousHeadshot;tendrilHit=previousTendril;impactComponent=previousComponent;
            }
            if(build.Conditions?.NewlyEquipped==true)EvolutionEvent("Equip");
            foreach(var primed in victims.Take(build.Conditions?.PrimerAllTargets==true?victims.Length:1))
            {SelectVictim(primed);foreach(var pair in build.Conditions?.Statuses??[])for(int n=0;n<Math.Min(pair.Value,TargetStatusRules.Maximum(pair.Key));n++)Add(pair.Key,initial,1,1,true);}
            SelectVictim(victims[0]);
            for(int point=0;point<count;point++)
            {
                var limit=settings.Duration*point/(count-1);
                while(victims.Any(v=>v.State.Health>0))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(spontaneous&&singularityEnds.Count>=(build.Conditions?.SingularityLimit??4)){if(build.Conditions?.SingularityLimit is null)throw new CombatRuleUnavailableException("이 모드의 활성 특이점 한도는 원문에 없습니다. 활성 한도 시나리오를 지정하세요.");fire.WaitUntil(singularityEnds.Min());}
                    if(simulorWeapon&&simulor.Count>=4&&fire.Next<simulor.Next)fire.WaitUntil(simulor.Next);
                    if(passive?.Kind=="Grimoire"&&currentAttack==2&&fire.Next<alternateReadyAt)fire.WaitUntil(alternateReadyAt);
                    if(azimaDiscs.Any(d=>!d.Ended)){var rate=RangedEngine.Calculate(Current() with{AttackIndex=0}).FireRate!.Value/10;foreach(var disc in azimaDiscs.Where(d=>!d.Ended))disc.Refresh(time,rate);}
                    var nextShot=(!manualGrenades||grenadeRack.Count<5)&&!holstered&&fire.Next<settings.Duration&&(!settings.FirstShot||(passive?.Kind=="Onos"&&build.AttackIndex==1?chargeCycles<1&&currentAttack!=0:shotIndex<cadence.BurstCount))?fire.Next:double.PositiveInfinity;
                    var tickingVictim=victims.MinBy(v=>v.Procs.Where(p=>p.HasTick&&(v.State.Health>0||p.Type==DamageType.Gas)).Select(p=>p.Next).DefaultIfEmpty(double.PositiveInfinity).Min())!;
                    var nextTick=tickingVictim.Procs.Where(p=>p.HasTick&&(tickingVictim.State.Health>0||p.Type==DamageType.Gas)).Select(p=>p.Next).DefaultIfEmpty(double.PositiveInfinity).Min();
                    var thawingVictim=victims.Where(v=>v.State.Health>0).MinBy(v=>v.FrozenEnd>=0?v.FrozenEnd:double.PositiveInfinity)!;
                    var nextThaw=thawingVictim.FrozenEnd>=0?thawingVictim.FrozenEnd:double.PositiveInfinity;
                    var nextImpact=impacts.TryPeek(out _,out var impactPriority)?impactPriority.Time:double.PositiveInfinity;
                    var nextCollision=collisions.TryPeek(out _,out var collisionPriority)?collisionPriority.Time:double.PositiveInfinity;
                    var nextOrb=orbs.TryPeek(out _,out var orbPriority)?orbPriority.Time:double.PositiveInfinity;
                    var nextKillExplosion=killExplosions.TryPeek(out _,out var killPriority)?killPriority.Time:double.PositiveInfinity;
                    var nextReload=holstered?double.PositiveInfinity:fire.PendingReloadStart;
                    var nextExternal=externalEvents.TryPeek(out var external)?external.Time:double.PositiveInfinity;
                    var next=Math.Min(nextBlockTick,Math.Min(nextOrb,Math.Min(nextKillExplosion,Math.Min(holsterEnd,Math.Min(nextHolsterTick,Math.Min(nextExternal,Math.Min(nextReload,Math.Min(nextImpact,Math.Min(nextThaw,Math.Min(nextShot,nextTick))))))))));var nextGrenade=grenadeLaunches.TryPeek(out _,out var grenadePriority)?grenadePriority.Time:double.PositiveInfinity;var nextBlob=blobArrivals.TryPeek(out _,out var blobPriority)?blobPriority.Time:double.PositiveInfinity;next=Math.Min(Math.Min(nextBlob,stug.Next),Math.Min(simulor.Next,Math.Min(next,Math.Min(nextCollision,nextGrenade))));var nextPenetration=penetrations.TryPeek(out _,out var penetrationPriority)?penetrationPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextPenetration);next=Math.Min(next,cubes.Next);next=Math.Min(next,infested.Next);next=Math.Min(next,grenadeRack.Next);var nextZymos=zymosEvents.TryPeek(out _,out var zymosPriority)?zymosPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextZymos);var nextTrap=voltageTraps.TryPeek(out _,out var trapPriority)?trapPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextTrap);var nextCrossing=orbCrossings.TryPeek(out _,out var crossingPriority)?crossingPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextCrossing);var nextSeeker=seekers.TryPeek(out _,out var seekerPriority)?seekerPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextSeeker);var nextSingularity=singularityPulses.TryPeek(out _,out var singularityPriority)?singularityPriority.Time:double.PositiveInfinity;var nextSingularityEnd=singularityEnds.DefaultIfEmpty(double.PositiveInfinity).Min();next=Math.Min(next,Math.Min(nextSingularity,nextSingularityEnd));next=Math.Min(next,victims.Select(v=>v.Nullifier?.Next??double.PositiveInfinity).DefaultIfEmpty(double.PositiveInfinity).Min());var nextResidual=residualPulses.TryPeek(out _,out var residualPriority)?residualPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextResidual);var nextAzima=azimaDiscs.Select(d=>d.Next).DefaultIfEmpty(double.PositiveInfinity).Min();next=Math.Min(next,nextAzima);var nextOrbInteraction=orbInteractions.TryPeek(out _,out var interactionPriority)?interactionPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextOrbInteraction);var nextGrenadeEvent=grenadeEvents.TryPeek(out _,out var grenadeEventPriority)?grenadeEventPriority.Time:double.PositiveInfinity;next=Math.Min(next,nextGrenadeEvent);next=Math.Min(next,hemaAt);next=Math.Min(next,syndicatePending?time:double.PositiveInfinity);next=Math.Min(next,syndicateUntil>=time?syndicateUntil:double.PositiveInfinity);if(next>limit+1e-9)break;AdvanceEnergy(next);time=next;
                    if(time==nextGrenadeEvent)
                    {
                        var entry=grenadeEvents.Dequeue();if(!manualGrenades)throw new ArgumentException("유탄 경로 사건은 펜타 다중 배치 모드에서 사용합니다.");
                        if(!grenadeRack.Contains(entry.Shot)){Log("grenadeEventMiss","유탄이 이미 기폭되었거나 아직 발사되지 않음",entry.Shot);continue;}
                        grenadePositions[entry.Shot]=entry;
                        var tether=initial.Modifiers!.GetValueOrDefault("TetherTargets")>0;
                        if(entry.Kind=="Land")grenadeRack.Land(entry.Shot,time,tether?10:null);
                        if(entry.Kind=="Attach")grenadeRack.Land(entry.Shot,time,tether?10:null,entry.Target,grenadeRack.Moving(entry.Shot));
                        if(entry.Kind=="Tar"||entry.Kind=="Water"&&(tether||(build.Conditions?.PentaWaterDetonates??throw new CombatRuleUnavailableException("일반 펜타의 물 접촉은 위키의 정지/즉시 폭발 서술이 상충합니다. 물 접촉 시나리오를 선택하세요."))))DetonatePlacedGrenades(false,entry.Shot);
                        else if(entry.Kind=="Water"){grenadePositions[entry.Shot]=entry with{Kind="Land"};grenadeRack.Land(entry.Shot,time,null);}
                        Log("grenadeMotion","유탄 경로·부착·환경 접촉",entry.Shot);continue;
                    }
                    if(time==nextOrbInteraction)
                    {
                        var interaction=orbInteractions.Dequeue();
                        if(!RangedInfestedOrbs.Applies(weapon))throw new ArgumentException("감염 구체 사건은 뮤탈리스트 콴타 전용입니다.");
                        if(infested.Value(interaction.Orb) is not {} stacks){Log("orbInteractionMiss","지정한 구체가 존재하지 않음 · 외부 사건 미적용",interaction.Orb+1);continue;}
                        if(interaction.Kind=="Absorb")
                        {
                            if(initial.Modifiers!.GetValueOrDefault("BurstingMass")<=0)throw new ArgumentException("외부 피해 저장에는 버스팅 매스가 필요합니다.");
                            var packet=RangedInfestedOrbs.Absorb(interaction.Packet!,interaction.ElementalBasis,stacks,interaction.Coefficient*initial.Modifiers!["BurstingMass"]);infested.AbsorbId(interaction.Orb,packet);Log("orbExternalAbsorb","외부 탄환 흡수 · 자체 사격 DPS에 포함하지 않음",packet.Values.Sum());
                        }
                        else{infested.Move(interaction.Orb,time,interaction.X,interaction.Y,interaction.TravelSeconds);
                            orbCrossings.Clear();foreach(var (pathShot,path) in infestedShotPaths.Where(p=>p.Value.Time+Math.Sqrt(p.Value.X*p.Value.X+p.Value.Y*p.Value.Y)/p.Value.Speed>=time))foreach(var crossing in infested.Crossings(path.Time,path.X,path.Y,path.Speed,time).Where(c=>!crossedOrbIds.Contains((pathShot,c.Orb))))orbCrossings.Enqueue((crossing.Orb,pathShot,path.Contacts,path.Target),(crossing.Time,crossingOrder++));Log("orbMove",interaction.Kind=="Bounce"?"지형 도탄 · 지정 경로":"근접 타격 · 지정 경로",interaction.Orb+1);}
                        continue;
                    }
                    if(time==nextAzima){foreach(var disc in azimaDiscs.Where(d=>d.Next<=time+1e-9).ToArray())AzimaDamage(disc,disc.Advance(time));continue;}
                    if(time==nextResidual){var pulse=residualPulses.Dequeue();ResidualDamage(pulse.Center,pulse.Scale);continue;}
                    if(victims.Any(v=>v.Nullifier?.Next<=time+1e-9))
                    {
                        var before=victim;foreach(var v in victims.Where(v=>v.Nullifier is not null)){SelectVictim(v);var absorbed=v.Nullifier!.Advance(time);if(absorbed>0){Log("nullifierShrink","막에 흡수된 비치명 피해",absorbed);Log("nullifierRadius","막의 잔여 반경 비율",v.Nullifier.Radius);}}SelectVictim(before);continue;
                    }
                    if(time==nextSingularityEnd){singularityEnds.RemoveAll(end=>end<=time+1e-9);Log("spontaneousEnd","특이점 만료 · 추가 폭발 없음",0);continue;}
                    if(time==nextSingularity)
                    {
                        var pulse=singularityPulses.Dequeue();var before=victim;var oldHead=impactHeadshot;impactHeadshot=false;var center=pulse.Origin<0?build.Conditions!.SingularityAllyPosition!.Value+build.Conditions.SingularityAllySpeed*time:victims[pulse.Origin].Position(time);
                        foreach(var recipient in victims.Where(v=>v.State.Health>0&&Math.Abs(v.Position(time)-center)<=pulse.Pulse.Radius+1e-9).ToArray()){SelectVictim(recipient);Damage(new(){[pulse.Pulse.Type]=pulse.Pulse.Output},1,radial:true);Log("spontaneousPulse","지정한 특이점 피해 사건 · 방어 전 출력",pulse.Pulse.Output);}
                        SelectVictim(before);impactHeadshot=oldHead;continue;
                    }
                    if(time==nextSeeker)
                    {
                        var seeker=seekers.Dequeue();var previous=victim;SelectVictim(victims[seeker.Target]);
                        if(target.Health<=0){Log("paxSeekerMiss","지정한 유도 대상이 이미 사망 · 경로 종료",1);SelectVictim(previous);continue;}
                        var component=Array.FindIndex(RangedEngine.Attacks(weapon),RangedPaxSeeker.Is);var source=Current() with{AttackIndex=component,Distance=0,Conditions=Current().Conditions! with{Headshot=true,InfestedOrbs=null,SimulorOrbs=null}};
                        var statistics=RangedEngine.Calculate(source);var rolled=Roll(statistics.Modifiers!["PaxSourceMultishot"]);
                        source=source with{Conditions=source.Conditions! with{PaxSeekerRoll=rolled}};
                        Log("paxSeekerHit","팍스 시커 · 멀티샷 피해 정수 배수",rolled);Impact(component,1,1,1,arcaneStacks,false,seeker.Shot,seeker.Target,snapshot:source,headshotOverride:true);SelectVictim(previous);continue;
                    }
                    if(time==grenadeRack.Next){DetonatePlacedGrenades(false);continue;}
                    if(time==nextZymos)
                    {
                        var ze=zymosEvents.Dequeue();var previous=victim;var previousHead=impactHeadshot;var previousComponent=impactComponent;
                        SelectVictim(victims[ze.Target]);
                        if(ze.Kind==1)
                        {
                            impactHeadshot=false;impactComponent=0;if(target.Health>0)Damage(new(){[ze.Tick!.Type]=ze.Tick.Output},ze.Contacts);Log("zymosBurrowTick","지정한 굴착 피해 사건 · 원문 미확정",ze.Tick!.Output*ze.Contacts);
                        }
                        else if(ze.Kind==2)
                        {
                            var origin=victims[ze.Host];Log("zymosHeadExplosion","자이모스 · 1.1초 후 약점 폭발",ze.Contacts);
                            foreach(var other in victims.Where(v=>v.State.Health>0).ToArray())
                            {
                                var distance=Between(other.Index,ze.Host);var snapshot=Current() with{Conditions=Current().Conditions! with{Headshot=other.Index==ze.Host,AttachedTarget=other.Index==ze.Host}};
                                Impact(2,ze.Contacts,1,1,arcaneStacks,false,ze.Shot,other.Index,spread:true,radialDistance:distance,snapshot:snapshot,headshotOverride:other.Index==ze.Host);
                            }
                            foreach(var path in build.Conditions!.ZymosSpores!)
                            {
                                if(path.Miss)continue;var distance=Between(path.Target,ze.Host);if(distance>20+1e-9)throw new ArgumentException("자이모스 포자의 표적은 숙주로부터 20m 이내여야 합니다.");
                                zymosEvents.Enqueue((3,ze.Shot,ze.Host,ze.Contacts,path.Target,distance,path.Headshot,null),(time+path.Flight!.Value,zymosOrder++));
                            }
                        }
                        else
                        {
                            var snapshot=Current() with{Conditions=Current().Conditions! with{ZymosSporeDistance=ze.Distance,InfestedOrbs=build.Conditions?.ZymosSporeOrbs,Headshot=ze.Headshot}};
                            Impact(3,ze.Contacts,1,1,arcaneStacks,false,ze.Shot,ze.Target,snapshot:snapshot,headshotOverride:ze.Headshot);
                            if(build.Conditions?.ZymosSporeExplosion==true)Impact(4,ze.Contacts,1,1,arcaneStacks,false,ze.Shot,ze.Target,snapshot:snapshot,headshotOverride:false);
                        }
                        SelectVictim(previous);impactHeadshot=previousHead;impactComponent=previousComponent;continue;
                    }
                    if(time==nextTrap)
                    {
                        var trap=voltageTraps.Dequeue();if(time>=trap.End-1e-9)continue;
                        var previous=victim;var oldHead=impactHeadshot;impactHeadshot=false;
                        var recipients=victims.Where(v=>v.State.Health>0&&Math.Abs(v.Position(time)-trap.Center)<=4+1e-9).OrderBy(v=>Math.Abs(v.Position(time)-trap.Center)).ThenBy(v=>v.Index).Take(trap.Cap).ToArray();
                        Log("voltageTrapTick","아크 트랩 · 고정 Electric 틱",recipients.Length);
                        var source=RangedVoltageTraps.Source(initial,trap.Damage);
                        foreach(var recipient in recipients){SelectVictim(recipient);Damage(source.Damage,1,ability:true);if(target.Health>0)Add(DamageType.Electricity,source,1,1,ability:true);}
                        SelectVictim(previous);impactHeadshot=oldHead;
                        var nextTime=trap.Start+(trap.Tick+1)/60d;if(nextTime<trap.End-1e-9)voltageTraps.Enqueue(trap with{Tick=trap.Tick+1},(nextTime,trapOrder++));continue;
                    }
                    if(time==infested.Next){InfestedDamage(infested.Advance(time,build.Conditions?.InfestedMerge??true,build.Conditions?.InfestedMergeOverflow??"Unspecified"));continue;}
                    if(time==nextCrossing)
                    {
                        var cross=orbCrossings.Dequeue();crossedOrbIds.Add((cross.Shot,cross.Orb));if(infested.Value(cross.Orb) is not {} value)continue;var previous=victim;SelectVictim(victims[cross.Target]);
                        if(!crossedOrbs.TryGetValue(cross.Shot,out var through)){through=[];crossedOrbs[cross.Shot]=through;}
                        if(initial.Modifiers!.GetValueOrDefault("BurstingMass")>0)
                        {
                            var input=Current() with{AttackIndex=0,Conditions=Current().Conditions! with{InfestedOrbs=[..build.Conditions?.InfestedOrbs??[],..through],Headshot=false}};
                            var stats=RangedEngine.Calculate(input);if(stats.Modifiers!.GetValueOrDefault("ActiveCO")>0)throw new CombatRuleUnavailableException("버스팅 매스에 흡수되는 탄환의 명중 전 조건부 CO 기여는 원문에 없습니다. CO 미발동 조건으로 계산하세요.");
                            var packet=RangedInfestedOrbs.Absorb(stats.Damage.ToDictionary(p=>p.Key,p=>p.Value/stats.FactionFactor),stats.BaseDamage*stats.StatusBaseFactor,value,initial.Modifiers!["BurstingMass"]);
                            infested.AbsorbId(cross.Orb,packet.ToDictionary(p=>p.Key,p=>p.Value*cross.Contacts));Log("infestedAbsorb","버스팅 매스 · 비치명·팩션 제외 피해 흡수",packet.Values.Sum()*cross.Contacts);
                        }
                        through.Add(value);Log("infestedCross","탄환 · 감염 구체 통과",value);SelectVictim(previous);continue;
                    }
                    if(time==cubes.Next){CubeDamage();continue;}
                    if(time==hemaAt)
                    {
                        hemaAt=double.PositiveInfinity;
                        if(holstered||fire.LastReloadAt!=hemaCompletion)continue;
                        var currentResult=RangedEngine.Calculate(Current());var capacity=currentResult.Cadence!.Magazine;
                        var purchase=RangedHema.Reload(MaxHealth(),playerHealth,capacity,Math.Max(0,capacity-fire.Ammo),initial.Modifiers!.GetValueOrDefault("HealthReloadCriticalStep"),build.Conditions?.HemaHost!=false,build.Conditions?.HemaPartialRounding??"Unspecified");
                        hemaRounds=purchase.Rounds;playerHealth-=purchase.HealthCost;hemaBonus=purchase.CriticalBonus;
                        Log("healthAmmo","헤마 · 체력으로 탄약 생성",purchase.HealthCost);Log("hemaCritical","네크로패직 비거 · 치확·치피 모드 가산",hemaBonus);
                        if(hemaRounds==0){fire.Stop();Log("healthAmmoEmpty","체력 부족 · 재장전 중단");}
                        continue;
                    }
                    if(time==syndicateUntil){ExpireSyndicate();continue;}
                    if(syndicatePending){SyndicateExplosion();continue;}
                    if(nextPenetration==next&&nextImpact>next)
                    {
                        var hit=penetrations.Dequeue();Log("penetration","일렬 배치 · 다음 대상 관통",hit.Target+1);
                        Impact(hit.Component,Math.Max(0,hit.Contacts-dissolvedProjectiles.GetValueOrDefault(hit.Shot)),hit.Scale,hit.Pellets,hit.Arcane,hit.Final,hit.Shot,hit.Target,pierced:true);continue;
                    }
                    if(stug.Next==next){StugDamage(stug.Advance(time));continue;}
                    if(nextBlob==next){var blob=blobArrivals.Dequeue();if(!blob.Form&&victims[blob.Target].State.Health>0&&incarnon is not null){incarnonGauge+=4;Log("incarnonCharge","스터그 · 직격으로 게이지 충전",4);}StugDamage(stug.Embed(time,blob.Stacks,blob.Shot,blob.Target,blob.Form,build.Conditions?.StugMerge??true));continue;}
                    if(simulor.Next==next){SimulorDamage(simulor.Advance(time,build.Conditions?.SimulorMerge??true,build.Conditions?.SimulorFirstTick));continue;}
                    if(nextGrenade==next)
                    {
                        var grenade=grenadeLaunches.Dequeue();
                        if(holstered||fire.LastReloadAt!=grenade.Completion){Log("grenadeCancelled","투척 전 재장전 중단");continue;}
                        var grenadeBuild=Current() with{AttackIndex=grenade.Attack};var stats=RangedEngine.Calculate(grenadeBuild);
                        var grenadeFlight=RangedAttackLinks.FlightTime(weapon,grenade.Attack,grenadeBuild,stats);
                        var destination=victims.First(v=>v.State.Health>0).Index;
                        impacts.Enqueue((grenade.Attack,reloadGrenade!.Count,1,1,arcaneStacks,grenade.Attack==3,grenade.Shot,destination,false),(time+grenadeFlight,eventOrder++));
                        Log("reloadGrenade","재장전 유탄 투척",reloadGrenade.Count);continue;
                    }
                    if(nextCollision==next)
                    {
                        var hit=collisions.Dequeue();var contact=hit.Collision;if(manualGrenades&&hit.Component==0){hit.Contacts=Math.Min(hit.Contacts,grenadeRack.Moving(hit.Shot));if(hit.Contacts==0)continue;}
                        SelectVictim(victims[contact.Target]);Log("collision",contact.Surface?"지정 경로 · 지형 충돌":"지정 경로 · 적 충돌",contact.AfterImpact);
                        if(RangedStug.Applies(weapon)&&hit.Component==4){Impact(4,hit.Contacts,1,1,hit.Arcane,false,hit.Shot,contact.Target);continue;}
                        if(contact.Surface)
                        {
                            if(RangedPentaAugments.Applies(weapon)&&hit.Component==0&&initial.Modifiers!.GetValueOrDefault("TetherTargets")>0){Impact(0,hit.Contacts,hit.Scale,hit.Pellets,hit.Arcane,hit.Final,hit.Shot,contact.Target,headshotOverride:false);continue;}
                            var sticky=StickyContacts(hit.Component,hit.Contacts);
                            if(manualGrenades&&hit.Component==0)grenadeRack.Land(hit.Shot,time,initial.Modifiers!.GetValueOrDefault("PentaNapalm")>0&&sticky>0?RangedAdhesive.Delay(weapon,build,hit.Component,time-adhesiveLaunchTimes[hit.Shot]):null,contact.Target,sticky);
                            if(RangedZymos.Applies(weapon)&&hit.Component==0)Impact(1,hit.Contacts,1,1,hit.Arcane,false,hit.Shot,contact.Target,headshotOverride:false);
                            if(RangedFlatExplosions.Equipped(build)){var beforeComponent=impactComponent;impactComponent=hit.Component;var source=RangedEngine.Calculate(Current() with{AttackIndex=hit.Component});var amount=FlatExplosionCount(source,hit.Contacts,true);FlatExplosions(source,amount);impactComponent=beforeComponent;if(source.Modifiers!.GetValueOrDefault("ThunderboltChance")>0)hit.Contacts-=amount;}

                            ScheduleChildren(hit.Component,hit.Contacts,hit.Scale,hit.Pellets,hit.Arcane,hit.Final,hit.Shot,contact.Target,sticky,true);
                            ScheduleField(hit.Component,hit.Contacts,hit.Scale,hit.Pellets,hit.Arcane,hit.Final,hit.Shot,contact.Target,sticky);
                        }
                        else Impact(hit.Component,hit.Contacts,hit.Scale,hit.Pellets,hit.Arcane,hit.Final,hit.Shot,contact.Target,headshotOverride:contact.Headshot);
                        continue;
                    }
                    if(nextBlockTick==next)
                    {
                        if(time<=blockUntil+1e-9&&alternateRule is not null){alternateCharges=Math.Min(alternateRule.Capacity,alternateCharges+1);Log("alternateCharge","방패 차단으로 충전",alternateCharges);}
                        nextBlockTick=time+1<=blockUntil+1e-9?time+1:double.PositiveInfinity;continue;
                    }
                    if(nextOrb==next)
                    {
                        var orb=orbs.Dequeue();var before=victim;var frozen=RangedEngine.Calculate(orb.Snapshot);var speed=6*(1+frozen.Modifiers!.GetValueOrDefault("ProjectileSpeed"));
                        var travel=speed*(time-orb.Spawn);if(build.Conditions?.OrbAnchored==true)travel=Math.Min(travel,build.Distance);
                        double Distance(Victim v)=>build.Conditions?.TargetsInLine==true?Math.Abs(PlayerDistance(v.Index)-travel):Math.Sqrt(Math.Pow(Between(v.Index,orb.Origin),2)+Math.Pow(build.Distance-travel,2));
                        if(orb.Pulse==6)
                        {
                            foreach(var other in victims.Where(v=>v.State.Health>0).ToArray())Impact(3,1,1,1,orb.Snapshot.ArcaneStacks,false,orb.Shot,other.Index,false,true,Distance(other),snapshot:orb.Snapshot);
                            Log("orbEnd","그리모어 구체 수명 종료 · 폭발");
                        }
                        else if(victims.Where(v=>v.State.Health>0&&Distance(v)<=6+1e-9).OrderBy(Distance).FirstOrDefault() is {} closest)
                            Impact(2,1,1,1,orb.Snapshot.ArcaneStacks,false,orb.Shot,closest.Index,snapshot:orb.Snapshot);
                        SelectVictim(before);continue;
                    }
                    if(nextKillExplosion==next)
                    {
                        var blast=killExplosions.Dequeue();var origin=victim;
                        foreach(var other in victims.Where(v=>v.Index!=blast.Origin&&v.State.Health>0).ToArray())
                        {
                            var distance=Between(other.Index,blast.Origin);if(distance>=blast.Radius)continue;
                            SelectVictim(other);Damage(blast.Damage,initial.FactionFactor*(1-distance/blast.Radius),radial:true);Log("killExplosion","처치 폭발 · 선형 거리 감쇠",distance);
                        }
                        SelectVictim(origin);continue;
                    }
                    if(nextExternal==next)
                    {
                        var action=externalEvents.Dequeue();
                        if(action.Kind=="WeaponAffinity")GainAffinity(action.Value);
                        if(action.Kind=="ArrowPickup")
                        {
                            var chance=initial.Modifiers!.GetValueOrDefault("ArrowRecoveryChance");if(chance<=0||action.Value!=Math.Floor(action.Value)||action.Value>100000)throw new ArgumentException("화살 회수는 아말감 다이큐 장착 시 실제 접촉한 화살 개수를 입력해야 합니다.");
                            int recovered=0;for(int arrow=0;arrow<action.Value;arrow++)if(Random()<chance)recovered++;
                            Log("arrowRecovery","접촉한 화살의 확률 회수 · 멀티샷 화살 포함 가능",ammo.Pickup(recovered));fire.ResumeAfterPickup(RangedEngine.Calculate(Current()).Cadence!,time);
                        }
                        if(action.Kind=="AllyKill")
                        {
                            if(action.Value!=Math.Floor(action.Value)||action.Value>victims.Length)throw new ArgumentException("외부 처치 대상 번호는 1부터 대상 수까지입니다.");
                            var previous=victim;SelectVictim(victims[(int)action.Value-1]);
                            if(!victim.Killed&&target.Health>0){target.ExternalKill();victim.Killed=true;TaggedDeath();if(PlayerDistance(victim.Index)<=(build.Conditions?.AffinityRange??50))GainAffinity(RangedSyndicates.KillAffinity(selection)*.75/(build.Conditions?.EquippedWeaponCount??3));Log("allyKill","외부 처치 · 총기 피해 합계에서 제외",action.Value);if(victims.All(v=>v.State.Health<=0))times[trial]=time;}
                            SelectVictim(previous);
                        }
                        if(action.Kind=="BlockedShot"){if(alternateRule is not {BlockSeconds:>0})throw new ArgumentException("이 무기는 차단으로 보조 공격을 충전하지 않습니다.");blockUntil=time+alternateRule.BlockSeconds;if(!double.IsFinite(nextBlockTick))nextBlockTick=time+1;}
                        if(action.Kind=="PrimaryFire")
                        {if(RangedEngine.S(weapon.Raw,"Family")!="EFV-5 Jupiter"&&weapon.Id!=RangedPullWeapons.Scyotid&&!RangedInfestedOrbs.Applies(weapon)&&!RangedQuanta.Applies(weapon))throw new ArgumentException("이 무기의 일반 사격 전환 사건은 연결되지 않았습니다.");currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                        if(action.Kind=="AlternateFire")
                        {
                            if(RangedAzima.Applies(weapon)){if(fire.Ammo<1||fire.LastReloadAt>time)throw new ArgumentException("터렛 투척은 재장전 중이 아니며 탄창에 1발 이상 있어야 합니다.");currentAttack=2;manualAzima=true;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                            else if(manualGrenades){DetonatePlacedGrenades(true);}
                            else if(RangedVileDischarge.Applies(weapon)&&initial.Modifiers!.GetValueOrDefault("DischargeCapacity")>0){if(dischargeStored<=0)throw new ArgumentException("방출할 저장 피해가 없습니다.");if(fire.LastReloadAt>time)throw new ArgumentException("바일 디스차지는 재장전 중 사용할 수 없습니다.");dischargeAmmo=fire.Ammo;manualDischarge=true;currentAttack=1;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),1);}
                            else if(RangedQuanta.Applies(weapon)){currentAttack=1;manualCube=true;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                            else if(RangedInfestedOrbs.Applies(weapon)){currentAttack=1;manualInfested=true;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                            else if(weapon.Id==RangedPullWeapons.Scyotid){currentAttack=1;manualSnare=true;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                            else if(RangedEngine.S(weapon.Raw,"Family")=="EFV-5 Jupiter"){currentAttack=1;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,Math.Max(time,double.IsFinite(fire.Next)?fire.Next:time),fire.Ammo);}
                            else if(simulorWeapon)SimulorDamage(simulor.Detonate(time));
                            else if(passive?.Kind=="Grimoire")LaunchOrb(Current());
                            else if(alternateRule is not null){if(alternateCharges<alternateRule.Required)throw new ArgumentException("보조 공격 게이지가 부족합니다.");alternateNormalAmmo=fire.Ammo;currentAttack=alternateRule.Alternate[0];manualAlternate=true;var mode=RangedEngine.Calculate(Current()).Cadence!;fire.SwitchMode(mode,time,alternateRule.NoAmmo?mode.Magazine:alternateNormalAmmo);}
                            else throw new ArgumentException("이 무기의 예약 보조 공격을 연결해야 합니다.");
                        }
                        if(action.Kind=="Holster")
                        {
                            if(holstered)throw new ArgumentException("무기 수납 시간이 서로 겹칩니다.");
                            tendrilStarted=double.NaN;tendrilAim=-1;frenzyKills=0;frenzySpent=0;frenzyShots=0;chargeStarted=double.NaN;chargeEnds=double.PositiveInfinity;holstered=true;holsterEnd=time+action.Value;nextHolsterTick=time+1;holsterReload=new();momentum=0;fire.Holster(time);
                        }
                        if(passive?.Kind=="Grimoire"&&!holstered&&action.Kind is "SecondaryAmmoDrop" or "UniversalAmmoDrop")alternateReadyAt=Math.Max(time,alternateReadyAt-10*action.Value);
                        if(action.Kind is "Equip" or "EquipFromPrimary"&&holstered){holstered=false;holsterEnd=nextHolsterTick=double.PositiveInfinity;fire.Equip(RangedEngine.Calculate(Current()).Cadence!,time);}

                        if(action.Kind=="PlayerShieldBreak"){overshields=false;shieldDeficit=build.Conditions?.PlayerMaxShields??300;}
                        if(action.Kind is "PlayerShieldBreak" or "PlayerOverguardBreak")EvolutionEvent("PlayerDefenseBreak");
                        if(action.Kind=="EquipFromPrimary")EvolutionEvent("Equip");
                        if(action.Kind=="Reload"&&!holstered&&fire.Ammo<RangedEngine.Calculate(Current()).Cadence!.Magazine&&!RangedIncarnons.IsForm(weapon,currentAttack)&&RangedEngine.S(weapon.Raw,"ReloadStyle")!="Regenerate")fire.RequestReload(time);
                        if(action.Kind=="Dodge"){if(RangedBurstAugments.IsFrenzy(weapon,currentAttack)){frenzyKills=0;frenzySpent=0;frenzyShots=0;fire.WaitUntil(time+action.Value);Log("frenzyCancel","회피로 연사 취소 · 환급 조건 초기화");}dodgeShots=2;Log("dodge","회피 · 다음 2회 발사 강화",2);}
                        if(action.Kind=="AbilityCast"){if(veloxHits>0&&initial.Modifiers!.GetValueOrDefault("VeloxStrengthPerHit")>0){Log("veloxConsumed","벨록스 컨클루전 · 시전 위력 소비",veloxHits*initial.Modifiers!["VeloxStrengthPerHit"]);veloxHits=0;}recentAbility=true;abilityAt=time;if(time<paxBoltUntil){Log("paxBoltConsumed","팍스 볼트 · 다음 어빌리티 위력·효율 증가",.075*((build.Kitgun?.ArcaneRank??3)+1));paxBoltUntil=-1;}}
                        if(action.Kind=="EnergySpent")
                        {
                            if(action.Value>currentEnergy+1e-9)throw new ArgumentException("외부 사건의 에너지 소모가 현재 보유량을 초과합니다.");
                            currentEnergy-=action.Value;energySpent+=action.Value;
                            while(energySpent>=50){energySpent-=50;EvolutionEvent("Energy50");}
                        }
                        if(action.Kind=="AmmoPickup"){var gained=ammo.Pickup(action.Value);if(gained>0)fire.ResumeAfterPickup(RangedEngine.Calculate(Current()).Cadence!,time);Log("ammoPickup","예비 탄약 획득",gained);}
                        if(action.Kind is "PrimaryAmmoDrop" or "SecondaryAmmoDrop" or "UniversalAmmoDrop")
                        {
                            var gained=ammo.PickupDrop(weapon,RangedEngine.Calculate(Current()),action.Kind,action.Value,build.Conditions?.OtherWeaponAmmoFull??false,holstered);
                            if(gained>0&&!holstered)fire.ResumeAfterPickup(RangedEngine.Calculate(Current()).Cadence!,time);Log("ammoPickup","탄약 상자 획득·변환",gained);
                        }
                        if(action.Kind=="EnergyRestore")currentEnergy=Math.Min(MaxEnergy(),currentEnergy+action.Value);
                        EvolutionEvent(action.Kind);Log("external",RangedEventLabels.Name(action.Kind),action.Value);continue;
                    }
                    if(nextHolsterTick==next)
                    {
                        nextHolsterTick+=1;
                        if(!RangedIncarnons.IsForm(weapon,currentAttack)&&!ammo.DirectReserve)
                        {
                            var holsterResult=RangedEngine.Calculate(Current());var capacity=holsterResult.Cadence!.Magazine;var rate=holsterResult.Modifiers!.GetValueOrDefault("HolsterReload");
                            if(rate>0&&fire.Ammo<capacity&&(ammo.Infinite||ammo.Reserve>0))
                            {
                                var restored=ammo.Spend(Math.Min(capacity-fire.Ammo,holsterReload.Rounds(capacity,rate,build.Conditions?.HolsterRounding??"Unspecified")));
                                fire.SetLoadedAmmo(fire.Ammo+restored);momentum=Math.Min(99,momentum+(int)restored);if(((int)restored)%2!=0)chamber=!chamber;
                                Log("holsterReload","수납 중 재장전 · 회복 탄수",restored);
                            }
                        }
                        continue;
                    }
                    if(holsterEnd==next)
                    {
                        holstered=false;holsterEnd=nextHolsterTick=double.PositiveInfinity;fire.Equip(RangedEngine.Calculate(Current()).Cadence!,time);EvolutionEvent("Equip");Log("equip","수납 종료 · 무기 꺼내기");continue;
                    }
                    if(nextReload<nextImpact&&nextReload<=nextThaw&&nextReload<=nextShot&&nextReload<=nextTick)
                    {
                        tendrilStarted=double.NaN;tendrilAim=-1;var reloadCadence=RangedEngine.Calculate(Current()).Cadence!;
                        foreach(var (id,rule) in evolutionRules)if(rule.ResetOnReload)evolutionStates.Remove(id);
                        if(RangedBurstAugments.IsFrenzy(weapon,currentAttack))
                        {
                            if(frenzyKills>=2){var refund=RangedBurstAugments.Refund(frenzySpent,initial.Modifiers!.GetValueOrDefault("FrenzyRefund"),build.Conditions?.RefundRounding??"Unspecified");Log("frenzyRefund","와일드 프렌지 · 예비 탄약 환급",ammo.Pickup(refund));}
                            frenzyKills=0;frenzySpent=0;frenzyShots=0;
                        }
                        previousMagazineHits=Math.Min(15,magazineHits.Count);magazineHits.Clear();firingStacks=0;spoolStart=0;if(!ammo.Infinite&&ammo.Reserve<=0&&!RangedIncarnons.IsForm(weapon,currentAttack)){fire.Stop();Log("ammoEmpty","예비 탄약 소진");continue;}
                        if(returningBlades is not null&&blades.Count>0)
                        {
                            var path=RangedReturningBlades.Path(build.Conditions??new());var empty=fire.Ammo<=1e-9;
                            Log("bladeReturn","재장전 · 칼날 복귀 시작",blades.Count);
                            while(blades.TryDequeue(out var blade))foreach(var hit in path)
                                collisions.Enqueue((hit,1,1,1,1,blade.Arcane,empty,blade.Shot),(time+hit.AfterImpact,eventOrder++));
                        }
                        if(recall is not null)while(embedded.TryDequeue(out var round))RecallRound(round);
                        if(reloadGrenade is not null&&currentAttack==0)
                        {
                            var phase=RangedReloadGrenades.Phase(build.Conditions??new());
                            var empty=fire.Ammo<=1e-9;
                            reloadCadence=reloadCadence with{Reload=RangedReloadGrenades.Reload(RangedEngine.Calculate(Current()).Modifiers!.GetValueOrDefault("ReloadSpeed"),empty)};
                            var grenadeId=-(grenadeShotIds.Count+1);grenadeShotIds.Add(grenadeId);
                            grenadeLaunches.Enqueue((time+reloadCadence.Reload,empty?3:1,grenadeId),(time+reloadCadence.Reload*phase,eventOrder++));
                        }
                        if(RangedHema.Applies(weapon))
                        {
                            var phase=build.Conditions?.HemaDrainPhase??throw new CombatRuleUnavailableException("헤마의 재장전 중 정확한 체력 소비 시점은 위키에 없습니다. 재장전 진행률 시나리오를 지정하세요.");
                            hemaAt=time+reloadCadence.Reload*phase;hemaCompletion=time+reloadCadence.Reload;hemaRounds=0;
                        }
                        if(passive?.Kind=="Ocucor")weaponStacks=0;fire.BeginReload(reloadCadence);Log("reloadStart","재장전 시작",reloadCadence.Reload);continue;
                    }
                    if(nextImpact<=nextThaw&&nextImpact<=nextShot&&nextImpact<=nextTick){var hit=impacts.Dequeue();Impact(hit.Component,hit.Contacts,hit.Scale,hit.Pellets,hit.ArcaneStacks,hit.FinalShot,hit.Shot,hit.Target,hit.Chained);continue;}
                    if(nextThaw<=nextShot&&nextThaw<=nextTick)
                    {SelectVictim(thawingVictim);
                        var cold=procs.FirstOrDefault(p=>p.Type==DamageType.Cold);procs.RemoveAll(p=>p.Type==DamageType.Cold);
                        if(cold is not null)for(int n=0;n<3;n++)procs.Add(new(){Type=DamageType.Cold,End=time+cold.Duration,Duration=cold.Duration,Next=double.PositiveInfinity});
                        victim.FrozenEnd=-1;Log("thaw","동결 해제 · 냉기 3중첩 유지");continue;
                    }
                    foreach(var cleanup in victims)cleanup.Procs.RemoveAll(p=>p.End<time-1e-9);
                    if(nextTick<=nextShot)
                    {SelectVictim(tickingVictim);
                        var ready=procs.Where(p=>p.Next<=time+1e-9&&p.HasTick&&(target.Health>0||p.Type==DamageType.Gas)).ToArray();
                        foreach(var group in ready.GroupBy(p=>(p.Type,p.Ability)))
                        {if(group.Key.Type is DamageType.Heat or DamageType.Electricity or DamageType.Gas)Tick(group);else foreach(var proc in group)Tick([proc]);}
                        continue;
                    }
                    SelectVictim(victims.First(v=>v.State.Health>0));
                    if(fire.LastReloadAt>=0&&fire.LastReloadAt<=time&&fire.LastReloadAt>processedReload)
                    {
                        processedReload=fire.LastReloadAt;recentReload=true;completedReloadAt=fire.LastReloadAt;completedRounds=fire.RoundsReloaded;spoolStart=0;firingStacks=0;if(fire.LastReloadFromEmpty)emptyReloadStacks++;else emptyReloadStacks=0;
                        var desired=RangedEngine.Calculate(Current()).Cadence!.Magazine;var loaded=RangedHema.Applies(weapon)?hemaRounds:ammo.DirectReserve?0:ammo.Spend(Math.Max(0,desired-fire.Ammo));
                        fire.SetLoadedAmmo(ammo.DirectReserve?desired:fire.Ammo+loaded);completedRounds=loaded;momentum=Math.Min(99,(int)loaded);if(((int)loaded)%2!=0)chamber=!chamber;
                        Log("reload","재장전 완료",loaded);
                    }
                    if(build.AutoTransform&&incarnon is not null&&!RangedIncarnons.IsForm(weapon,currentAttack)&&incarnonGauge>=RangedEngine.N(RangedEngine.Attacks(weapon)[incarnon.Forms[0]],"IncarnonCharges"))
                    {
                        foreach(var (id,rule) in evolutionRules)if(rule.ResetOnReload)evolutionStates.Remove(id);
                        var before=RangedEngine.Calculate(Current());var animation=before.Reload??0;normalAmmo=ammo.DirectReserve?before.Cadence!.Magazine:fire.Ammo+ammo.Spend(Math.Max(0,before.Cadence!.Magazine-fire.Ammo));firingStacks=0;currentAttack=incarnon.Forms[0];incarnonGauge=0;
                        fire.Transform(RangedEngine.Calculate(Current()).Cadence!,animation);Log("transform","비행 중 명중으로 인카논 완충 · 다음 발사 전 변신");continue;
                    }
                    if(alternateRule is not null)
                    {
                        if(build.Conditions?.AutoAlternate==true&&alternateRule.Primary.Contains(currentAttack)&&alternateCharges>=alternateRule.Capacity)
                        {
                            alternateNormalAmmo=fire.Ammo;currentAttack=alternateRule.Alternate[0];var mode=RangedEngine.Calculate(Current());if(mode.TimingIssues.Length>0)throw new CombatRuleUnavailableException(string.Join("\n",mode.TimingIssues));fire.SwitchMode(mode.Cadence!,time,alternateRule.NoAmmo?mode.Cadence!.Magazine:alternateNormalAmmo);continue;
                        }
                        if(alternateRule.Alternate.Contains(currentAttack)&&alternateCharges<alternateRule.Required)
                        {
                            if(build.Conditions?.AutoAlternate==true){currentAttack=alternateRule.Primary[0];fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time,alternateNormalAmmo);}else fire.Stop();
                            Log("chargeEmpty","보조 공격 게이지 부족");continue;
                        }
                    }
                    if(passive?.Kind=="Onos"&&currentAttack==1)
                    {
                        if(double.IsNaN(chargeStarted)){chargeStarted=time;chargeEnds=time+build.Conditions!.ChargeCycleSeconds!.Value;Log("chargeStart","오노스 충전 시작 · 설정한 충전 시간",chargeEnds-time);}
                        if(time>=chargeEnds-1e-9){
                            if(chargeCycles>0&&initial.Modifiers!.GetValueOrDefault("ChargeBlastCritical")>0&&build.Conditions?.ChargeStackReset=="Unspecified")throw new CombatRuleUnavailableException("데버스테이션 캐스케이드의 완충 발사 후 중첩 유지/소비 규칙은 원문에 없습니다. 반복 사이클은 중첩 유지 방식을 시나리오 조건으로 선택해야 합니다.");
                            currentAttack=2;var full=RangedEngine.Calculate(Current()).Cadence!;fire.SwitchMode(full with{Charge=0,NoReload=true},time,fire.Ammo);}
                    }
                    if(RangedFields.Get(weapon.Id) is {RepeatAfterReload:true} thrown&&thrown.Parents.Contains(currentAttack))activeGroundSpear=shotIndex+1;
                    EvolutionEvent("Fire");firingStacks++;var current=Current();var result=RangedEngine.Calculate(current);var currentCadence=result.Cadence!;var pellets=Roll(result.Multishot);
                    var spending=currentCadence;if(RangedIncarnons.IsForm(weapon,currentAttack)&&result.Modifiers!.GetValueOrDefault("ExtraMultishotAmmo")>0)spending=spending with{AmmoCost=spending.AmmoCost+Math.Max(0,pellets-result.Modifiers!.GetValueOrDefault("NativeMultishot"))};
                    var efficiency=RangedAmmo.Efficiency(result.Modifiers!);if(alternateRule is {NoAmmo:true}&&alternateRule.Alternate.Contains(currentAttack))efficiency=1;if(result.Modifiers!.GetValueOrDefault("AmmoOnlyOnDamage")>0&&((build.Conditions?.MissedShots??[]).Contains(shotIndex+1)||result.Damage.Values.Sum()==0))efficiency=1;var formActive=RangedIncarnons.IsForm(weapon,currentAttack);
                    if(RangedAzima.Applies(weapon)&&currentAttack==2)
                    {
                        var scenario=build.Conditions?.Azima;RangedAzima.Validate(scenario,true);var launchEfficiency=efficiency;
                        if(efficiency>=1&&scenario!.InfiniteConsumesMagazine is null)throw new CombatRuleUnavailableException("아지마와 피스톨리어 문서의 무한 탄약 투척 비용이 상충합니다. 탄창 소비 여부를 명시하세요.");
                        var loaded=fire.Ammo;if(loaded<1)throw new ArgumentException("아지마 탄창에 최소 1발이 필요합니다.");
                        spending=spending with{AmmoCost=loaded};efficiency=launchEfficiency>=1&&scenario!.InfiniteConsumesMagazine==false?1:0;
                        for(int disc=0;disc<pellets;disc++)azimaDiscs.Add(new(shotIndex+1,time,loaded,launchEfficiency,result.FireRate!.Value/10,scenario!));
                        Log("azimaLaunch","탄창 투척 · 멀티샷별 독립 터렛",pellets);
                    }
                    var required=fire.PreviewConsumption(spending.AmmoCost,efficiency);
                    if(!formActive&&ammo.DirectReserve&&!ammo.Infinite&&ammo.Reserve+1e-9<required){fire.Stop();Log("ammoEmpty","예비 탄약 소진");continue;}
                    if(!formActive&&!ammo.DirectReserve&&fire.Ammo+1e-9<required){fire.Stop();Log("ammoEmpty","장전 가능한 탄약 소진");continue;}
                    if(!formActive&&result.Modifiers!.GetValueOrDefault("ExtraMultishotAmmo")>0)
                    {
                        var extras=Math.Max(0,pellets-(int)result.Modifiers!.GetValueOrDefault("NativeMultishot"));var supplied=ammo.Spend(extras);pellets-=extras-(int)supplied;
                    }
                    var doubleField=fire.ShotIndex==0&&fire.LastReloadFromEmpty&&result.Modifiers!.GetValueOrDefault("EmptyReloadDoubleField")>0;var beforeAmmo=fire.Ammo;var shot=fire.Consume(spending,efficiency);if(RangedBurstAugments.IsFrenzy(weapon,currentAttack)){frenzySpent+=beforeAmmo-fire.Ammo;frenzyShots++;}if(passive?.Kind=="Ocucor"&&fire.Ammo<=0)weaponStacks=0;if(!formActive&&ammo.DirectReserve)ammo.Spend(beforeAmmo-fire.Ammo);shotIndex++;dodgeSnapshots[shotIndex]=dodgeShots;dodgeShots=Math.Max(0,dodgeShots-1);if(Math.Abs(fire.Ammo-(currentCadence.Magazine-1))<1e-9)firstMagazineShots.Add(shotIndex);if(current.Conditions!.FinalBurst)finalBurstShots.Add(shotIndex);if(doubleField)doubledFieldShots.Add(shotIndex);
                    if(currentCadence.BurstCount>1){if(fire.BurstShot==0)burstGroup++;shotBursts[shotIndex]=(burstGroup,currentCadence.BurstCount,currentAttack);}
                    shotOrigins[shotIndex]=(currentAttack,victim.Index);adhesiveLaunchTimes[shotIndex]=time;
                    if(RangedMagazineCharge.Applies(weapon,currentAttack)||RangedPhantasma.Applies(weapon)&&currentAttack==1)chargedSalvos[shotIndex]=(int)currentCadence.AmmoCost;
                    if(passive?.Kind=="Onos"&&currentAttack==2){chargeSnapshots[shotIndex]=chargeDamageStacks;if(build.Conditions?.ChargeStackReset=="OnRelease")chargeDamageStacks=0;}
                    var contacts=(result.Damage.Values.Sum()<=0&&result.Modifiers!.GetValueOrDefault("CarrierProjectile")==0)||(build.Conditions?.MissedShots??[]).Contains(shotIndex)?0:currentCadence.Beam?Math.Min(1,pellets):pellets;var beamScale=currentCadence.Beam?pellets*shot.BeamScale:1;
                    if(passive?.Kind=="Onos"&&currentAttack==1)beamScale=pellets*(1+1.5*Math.Clamp((time-chargeStarted)/(chargeEnds-chargeStarted),0,1));
                    if(RangedVileDischarge.Applies(weapon)&&currentAttack==1){if(dischargeStored<=0)throw new ArgumentException("방출할 저장 피해가 없습니다.");dischargeSnapshots[shotIndex]=dischargeStored;Log("dischargeRelease","바일 디스차지 · 저장 피해 방출",dischargeStored);dischargeStored=0;}
                    var shotArcaneStacks=arcaneStacks;var usedAlternate=alternateRule?.Alternate.Contains(currentAttack)==true;if(usedAlternate){barrageSnapshots[shotIndex]=barrageReady;barrageReady=false;alternateShotCharges[shotIndex]=alternateCharges;alternateCharges=0;Log("alternateConsume","보조 공격 게이지 소비",alternateShotCharges[shotIndex]);}
                    if(currentAttack==1&&result.Modifiers!.GetValueOrDefault("BuckshotFalloff")>0){buckshotSnapshots[shotIndex]=buckshotReady;buckshotReady=false;}
                    if(manualGrenades&&currentAttack==0)grenadeRack.Launch(shotIndex,victim.Index,contacts);
                    Log("shot",$"발사 {shotIndex} · 탄창 {shot.MagazineIndex+1}",pellets);Log("ammo","예비 탄약",ammo.Reserve);
                    var flight=RangedAttackLinks.FlightTime(weapon,currentAttack,current,result);if(RangedDeployments.Get(weapon.Id) is {DirectAttack:<0})flight+=build.Conditions?.DetonationDelay??0;
                    if(weapon.Id==RangedKomorex.Id&&currentAttack==2)komorexLastTargets[shotIndex]=RangedPenetration.Plan(weapon,currentAttack,result,build.Conditions??new(),victim.Index).Select(h=>h.Target).DefaultIfEmpty(victim.Index).Last();
                    if(contacts>0&&!(RangedAzima.Applies(weapon)&&currentAttack==2)&&!(RangedTendrils.Applies(weapon)&&currentAttack==0)&&!(RangedInfestedOrbs.Applies(weapon)&&currentAttack==1)&&result.Modifiers!.GetValueOrDefault("TetherTargets")==0&&!(RangedQuanta.Applies(weapon)&&currentAttack==1))foreach(var hit in RangedPenetration.Plan(weapon,currentAttack,result,build.Conditions??new(),victim.Index))
                        penetrations.Enqueue((currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex,hit.Target),(time+flight+hit.AfterFirstHit,penetrationOrder++));
                    if(RangedCollisions.Get(weapon.Id,currentAttack) is not null||RangedFlatExplosions.Equipped(build))
                    {
                        if(build.Conditions?.FirstImpactSurface==true)collisions.Enqueue((new(0,victim.Index,true),currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex),(time+flight,collisionOrder++));
                        foreach(var collision in build.Conditions?.Collisions??[])
                            collisions.Enqueue((collision,currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex),(time+flight+collision.AfterImpact,collisionOrder++));
                    }
                    if(RangedQuanta.Applies(weapon)&&currentAttack==0&&build.Conditions?.ShootOwnCubes==true&&cubes.Count>0&&contacts>0)
                    {
                        var c=build.Conditions;var radius=c.CubeHitRadius??throw new CombatRuleUnavailableException("큐브의 명중 판정 크기는 위키에 없습니다. 경로 설정에서 충돌 반경을 지정하세요. 0은 중심을 정확히 조준한 경로입니다.");var aim=TargetPoint(victim);var length=Math.Sqrt(aim.X*aim.X+aim.Y*aim.Y);var range=result.Modifiers!.GetValueOrDefault("EffectiveRange",length);var ratio=length>0?Math.Min(1,range/length):0;
                        if(cubes.HitBeam(time,aim.X*ratio,aim.Y*ratio,radius) is {} cubeId)
                        {
                            if(c.CubeBeamContinues is null)throw new CombatRuleUnavailableException("큐브를 기폭한 같은 빔 틱이 뒤의 적까지 명중하는지는 위키에 없습니다. 빔 통과 시나리오를 선택하세요.");
                            Log("cubeBeamHit","본체 빔 · 큐브 경로 교차",cubeId);CubeDamage();if(!c.CubeBeamContinues.Value)contacts=0;
                        }
                    }
                    if(RangedInfestedOrbs.Applies(weapon)&&currentAttack==0&&build.Conditions?.InfestedShootThrough==true&&contacts>0)
                    {
                        var orbPoint=TargetPoint(victim);var speed=(RangedEngine.N(RangedEngine.Attacks(weapon)[0],"ShotSpeed")??100)*Math.Max(0,1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed"));
                        infestedShotPaths[shotIndex]=(time,orbPoint.X,orbPoint.Y,speed,contacts,victim.Index);
                        foreach(var crossing in infested.Crossings(time,orbPoint.X,orbPoint.Y,speed))orbCrossings.Enqueue((crossing.Orb,shotIndex,contacts,victim.Index),(crossing.Time,crossingOrder++));
                    }
                    if(RangedAzima.Applies(weapon)&&currentAttack==2){}
                    else if(RangedInfestedOrbs.Applies(weapon)&&currentAttack==1)
                    {
                        if(contacts>0)
                        {
                            var c=build.Conditions??new();var stop=c.InfestedStopSeconds??(build.Distance==0?0:throw new CombatRuleUnavailableException("감염 구체가 정지하는 시간은 원문에 없습니다. 정지까지의 시간을 지정하세요."));
                            var orbPoint=TargetPoint(victim);var length=Math.Sqrt(orbPoint.X*orbPoint.X+orbPoint.Y*orbPoint.Y);var distance=5*Math.Max(0,1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed"))*stop;
                            InfestedDamage(infested.Launch(time,stop,pellets,result.Multishot,shotIndex,victim.Index,c.InfestedFirstPulse,result.Modifiers!.GetValueOrDefault("BurstingMass")>0,length==0?distance:orbPoint.X/length*distance,length==0?0:orbPoint.Y/length*distance));
                        }
                    }
                    else if(RangedQuanta.Applies(weapon)&&currentAttack==1)
                    {
                        if(!(build.Conditions?.MissedShots??[]).Contains(shotIndex))cubes.Launch(time,shotIndex,RangedQuanta.Routes(current,result,pellets,victim.Index),result.Modifiers!.GetValueOrDefault("PunchThrough"),build.Conditions?.CubeLifetimeRule??"Unspecified");
                    }
                    else if(RangedStug.Launch(weapon,currentAttack))
                    {
                        if(pellets>0&&!(build.Conditions?.MissedShots??[]).Contains(shotIndex))blobArrivals.Enqueue((currentAttack==7?10:pellets,shotIndex,victim.Index,currentAttack==2),(time+flight,blobOrder++));
                    }
                    else if(simulorWeapon&&currentAttack==0)
                    {
                        if(pellets>0)SimulorDamage(simulor.Launch(time,flight,pellets,shotIndex,victim.Index));
                    }
                    else if(usedAlternate&&alternateRule!.LockTargets>1)
                    {
                        foreach(var locked in victims.Where(v=>v.State.Health>0).Take(alternateRule.LockTargets))
                            if(build.Distance<=80)impacts.Enqueue((currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex,locked.Index,false),(time+flight,eventOrder++));
                    }
                    else if(passive?.Kind=="Grimoire"&&currentAttack==2){LaunchOrb(current);}
                    else if(build.Conditions?.FirstImpactSurface==true&&(RangedCollisions.Get(weapon.Id,currentAttack) is not null||RangedFlatExplosions.Equipped(build))){}
                    else if(flight==0)Impact(currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex,victim.Index);
                    else impacts.Enqueue((currentAttack,contacts,beamScale,pellets,shotArcaneStacks,current.Conditions!.FinalShot,shotIndex,victim.Index,false),(time+flight,eventOrder++));
                    if(arcane?.Code=="Surge")arcaneStacks=0;
                    if(arcane?.Code=="Sharpshot")arcaneStacks=build.Conditions?.Headshot==true?1:0;
                    if(RangedQuanta.Applies(weapon)&&currentAttack==1&&manualCube){manualCube=false;currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,fire.Ammo);continue;}
                    if(RangedInfestedOrbs.Applies(weapon)&&currentAttack==1&&manualInfested){manualInfested=false;currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,fire.Ammo);continue;}
                    if(weapon.Id==RangedPullWeapons.Scyotid&&currentAttack==1&&manualSnare){manualSnare=false;currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,fire.Ammo);continue;}
                    if(RangedVileDischarge.Applies(weapon)&&currentAttack==1)
                    {
                        if(manualDischarge){manualDischarge=false;currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,dischargeAmmo);}else fire.Stop();continue;
                    }
                    if(usedAlternate)
                    {
                        if(!alternateRule!.NoAmmo)alternateNormalAmmo=fire.Ammo;
                        if(build.Conditions?.AutoAlternate==true||manualAlternate){manualAlternate=false;currentAttack=alternateRule.Primary[0];fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,alternateNormalAmmo);}else fire.Stop();
                        continue;
                    }
                    if(passive?.Kind=="Onos"&&currentAttack==2&&build.AttackIndex!=2)
                    {
                        chargeCycles++;chargeStarted=double.NaN;chargeEnds=double.PositiveInfinity;var remaining=fire.Ammo;Log("chargeRelease","오노스 완충 발사 · 남은 인카논 탄약",remaining);
                        currentAttack=remaining>0?1:0;var nextMode=RangedEngine.Calculate(Current());
                        if(remaining>0)fire.SwitchMode(nextMode.Cadence!,time+(build.Conditions?.ChargeRecoverySeconds??0),remaining);
                        else{fire.SwitchMode(nextMode.Cadence!,time+(nextMode.Reload??0),normalAmmo);Log("transform","인카논 소진 · 일반 모드 복귀");}
                        continue;
                    }
                    if(manualAzima){manualAzima=false;currentAttack=0;fire.SwitchMode(RangedEngine.Calculate(Current()).Cadence!,time+currentCadence.ShotDelay,fire.Ammo);continue;}
                    var after=RangedEngine.Calculate(Current());
                    if(RangedIncarnons.IsForm(weapon,currentAttack)&&fire.Ammo<=0)
                    {
                        currentAttack=0;var normal=RangedEngine.Calculate(Current());fire.Transform(normal.Cadence!,normal.Reload??0);if(!ammo.DirectReserve)fire.SetLoadedAmmo(Math.Min(normal.Cadence!.Magazine,normalAmmo+ammo.Spend(Math.Max(0,normal.Cadence!.Magazine-normalAmmo))));Log("transform","인카논 탄약 소진 · 기본형 복귀");
                    }
                    else if(build.AutoTransform&&incarnon is not null&&!RangedIncarnons.IsForm(weapon,currentAttack)&&incarnonGauge>=RangedEngine.N(RangedEngine.Attacks(weapon)[incarnon.Forms[0]],"IncarnonCharges"))
                    {
                        foreach(var (id,rule) in evolutionRules)if(rule.ResetOnReload)evolutionStates.Remove(id);
                        var animation=after.Reload??0;normalAmmo=ammo.DirectReserve?after.Cadence!.Magazine:fire.Ammo+ammo.Spend(Math.Max(0,after.Cadence!.Magazine-fire.Ammo));if(fire.Ammo<=1e-9)emptyReloadStacks++;firingStacks=0;currentAttack=incarnon.Forms[0];incarnonGauge=0;var form=RangedEngine.Calculate(Current());fire.Transform(form.Cadence!,animation);Log("transform","인카논 완충 · 변신");
                    }
                    else
                    {
                        var previousMagazine=fire.MagazineIndex;
                        fire.Advance(after.Cadence!,RangedAmmo.Efficiency(after.Modifiers!),spool is null?null:RangedSpool.Ammo(spool,spoolStart+fire.ShotIndex+1),deferReload:true);
                        if(RangedBurstAugments.IsFrenzy(weapon,currentAttack)&&frenzyShots>=currentCadence.Magazine&&fire.Ammo>0)
                        {
                            if(build.Conditions?.FrenzyBurstPause is not {} pause)throw new CombatRuleUnavailableException("탄약 효율이 적용된 와일드 프렌지의 탄창 길이 연사 이후 재입력 간격은 위키에 없습니다. 대기 시간을 설정하세요.");
                            if(frenzyKills>=2)Log("frenzyRefund","와일드 프렌지 · 예비 탄약 환급",ammo.Pickup(RangedBurstAugments.Refund(frenzySpent,initial.Modifiers!.GetValueOrDefault("FrenzyRefund"),build.Conditions.RefundRounding)));
                            frenzyKills=0;frenzyShots=0;frenzySpent=0;fire.ScheduleAt(time+Math.Max(pause,currentCadence.ShotDelay));
                        }
                        if(passive?.Kind=="Onos"&&currentAttack==1&&fire.Next>chargeEnds&&fire.Ammo>0)fire.ScheduleAt(chargeEnds);
                        if(fire.MagazineIndex!=previousMagazine)foreach(var (id,rule) in evolutionRules)if(rule.ResetOnReload)evolutionStates.Remove(id);
                    }
                }
                AdvanceEnergy(limit);time=limit;sums[point,11]+=dischargeStored;sums[point,10]+=playerHealth;sums[point,0]+=direct;sums[point,1]+=dot;sums[point,7]+=currentEnergy;sums[point,8]+=playerOverguard;sums[point,9]+=veloxHits*initial.Modifiers!.GetValueOrDefault("VeloxStrengthPerHit");
                foreach(var measured in victims){SelectVictim(measured);var statuses=Status();var armor=MeleeTargetModel.EffectiveArmor(target.Armor,new(Statuses:statuses,HeatArmorStrip:HeatStrip()));sums[point,2]+=target.Health;sums[point,3]+=target.Shields;sums[point,4]+=target.Overguard;sums[point,5]+=armor/victims.Length;sums[point,6]+=statuses.Count/(double)victims.Length;}
            }
            damageMoments.Add(direct+dot);dpsMoments.Add((direct+dot)/settings.Duration);progress?.Report(new("총기 교전 표본 계산",trial+1,settings.Trials));
        }
        Array.Sort(times);double? Q(double p){var value=times[(int)Math.Ceiling(p*settings.Trials)-1];return double.IsFinite(value)?value:null;}
        return new(Enumerable.Range(0,count).Select(i=>new RangedCombatPoint(settings.Duration*i/(count-1),sums[i,0]/settings.Trials,sums[i,1]/settings.Trials,sums[i,2]/settings.Trials,sums[i,3]/settings.Trials,sums[i,4]/settings.Trials,sums[i,5]/settings.Trials,sums[i,6]/settings.Trials,sums[i,7]/settings.Trials,sums[i,8]/settings.Trials,sums[i,9]/settings.Trials,sums[i,10]/settings.Trials,sums[i,11]/settings.Trials)).ToArray(),events.ToArray(),damageMoments.Estimate(),dpsMoments.Estimate(),new(Q(.1),Q(.5),Q(.9),times.Count(double.IsFinite)/(double)settings.Trials,settings.Trials,settings.Seed,settings.Duration),[..build.Conditions?.ApproximationNotes??[],..runtimeApproximations,build.Conditions?.MissedShots is {Length:>0}?$"대상 {build.Conditions?.GroupCount??1}명 · 지정 발사는 폭발 포함 빗나감 · 모두 처치한 뒤 누적 중지":$"대상 {build.Conditions?.GroupCount??1}명 · 발사체 전부 명중 · 살아 있는 다음 대상으로 조준 전환",build.Conditions?.ReplenishReserve==true?"예비 탄약 지속 보급 설정":"예비 탄약·탄창을 별도로 소비하며 탄약 소진 시 발사를 중지합니다.", $"추가 충돌 {build.Conditions?.Collisions?.Length??0}회: 입력한 경로·시각 사용, 멀티샷은 동일 경로 배치 가정", build.Conditions?.TargetsInLine==true?"장애물 없는 일렬 배치 · 몸통 두께와 관통 후 비행 시간은 지정한 경로 조건":"장애물 없는 동일 적의 횡대 배치 · 적 간격 고정 · 연쇄/촉수는 가까운 대상부터 선택", "프라이머의 자체 피해는 제외하고 시작 상태·남은 기본 지속 시간만 적용", "이벤트 목록은 첫 번째 표본, 그래프와 평균·표준오차는 전체 표본 기준"]);
    }
}
