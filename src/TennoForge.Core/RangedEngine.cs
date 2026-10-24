using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedMod(string Id,string Name,string EnglishName,string Category,int MaxRank,int BaseDrain,string Polarity,string[] Incompatible,Dictionary<string,double> Effects,string[] Limitations,string Description,string Source,bool Exilus=false,string[]? CompatibilityTags=null,string[]? WeaponIds=null,RangedEvolutionTrigger[]? Triggers=null,Dictionary<string,double[]>? RankEffects=null,string[]? IncompatibleWeaponIds=null);
public record RangedModCatalog(string SourceSha256,string Rules,RangedMod[] Mods)
{
    public static RangedModCatalog Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-mods.json")!;return JsonSerializer.Deserialize<RangedModCatalog>(s,Catalog.Json)!;}
}
public record RangedBonus(DamageType Type,double Fraction);
public record RangedBuild(string WeaponId,int AttackIndex,ModSlot?[] Slots,string[] Polarities,bool Catalyst=true,string Arcane="None",int ArcaneStacks=0,double Distance=0,int SchemaVersion=1,EnemySelection? Enemy=null,RangedConditions? Conditions=null,int ArcaneRank=5,RangedBonus? Progenitor=null,int EncounterMode=3,ModSlot? Exilus=null,string ExilusPolarity="None",string[]? Evolutions=null,int? IncarnonAmmo=null,bool AutoTransform=false,RangedRivenRoll? Riven=null,KitgunAssembly? Kitgun=null);
public record RangedResult(Dictionary<DamageType,double> Damage,double CriticalChance,double CriticalMultiplier,double StatusChance,double Multishot,double ExpectedShot,double? FireRate,double? Reload,double? Magazine,double? BurstDps,double? SustainedDps,int Drain,int Capacity,string[] TimingIssues,string[] Scope,RangedCadence? Cadence=null,Dictionary<string,double>? Modifiers=null,double BaseDamage=0,double BaseFactor=1,double FactionFactor=1,double CoFactor=1,Dictionary<DamageType,double>? ArsenalDamage=null,double StatusBaseFactor=1);

// Attack Damage in the fixed Wiki module is PER PROJECTILE, not the arsenal sum.
public static class RangedEngine
{
    public static readonly RangedModCatalog ModCatalog=RangedModCatalog.Load();
    public static ReferenceWeapon[] Weapons=>ReferenceCatalog.Load().Weapons.Where(w=>w.Category is "primary" or "secondary").Concat(RangedKitguns.Data.Weapons.Select(d=>RangedKitguns.Assemble(d))).ToArray();
    public static ReferenceWeapon Resolve(RangedBuild b)=>RangedKitguns.Get(b.WeaponId) is {} modular?RangedKitguns.Assemble(modular,b.Kitgun):ReferenceCatalog.Load().Weapons.SingleOrDefault(w=>w.Id==b.WeaponId&&w.Category is "primary" or "secondary")??throw new ArgumentException("무기 식별자가 없습니다.");
    public static JsonElement[] Attacks(ReferenceWeapon w)=>RangedKomorex.Append(w,RangedAzima.Append(w,RangedNapalm.Append(w,RangedInfestedOrbs.Append(w,RangedVileDischarge.Append(w,RangedStug.Append(w,RangedBurstAugments.Append(w,RangedAugmentFields.Append(w,RangedReturningBlades.Append(w,w.Raw.TryGetProperty("Attacks",out var a)&&a.ValueKind==JsonValueKind.Array?a.EnumerateArray().Select((attack,index)=>RangedZymos.Normalize(w,RangedPullWeapons.Normalize(w,RangedPassives.Normalize(w,RangedReloadGrenades.Normalize(w,RangedIncarnons.Normalize(w,RangedFields.Normalize(w,RangedDeployments.Normalize(w,RangedVectis.Normalize(w,RangedQuanta.Normalize(w,RangedAttackOverrides.Normalize(w,attack,index),index),index),index),index),index),index),index),index),index)).ToArray():[])))))))));
    public static double? N(JsonElement j,string key)=>j.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out var n)&&double.IsFinite(n)?n:null;
    public static string S(JsonElement j,string key)=>j.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
    public static string ModCategory(ReferenceWeapon w)=>w.Category=="secondary"?"Pistol":w.WeaponType=="Shotgun"?"Shotgun":"Rifle";
    public static bool Compatible(RangedMod m,ReferenceWeapon w)
    {
        if(m.IncompatibleWeaponIds?.Contains(w.Id)==true)return false;
        if(m.WeaponIds is {Length:>0})return m.WeaponIds.Contains(w.Id);
        if(m.Category!=ModCategory(w)&&m.Category!=(w.Category=="primary"?"Primary":"Secondary"))return false;
        var attacks=Attacks(w);
        return (m.CompatibilityTags??[]).All(tag=>tag switch
        {
            "BEAM"=>S(w.Raw,"Trigger")=="Held"||attacks.Length>0&&S(attacks[0],"Trigger")=="Held",
            "PROJECTILE"=>attacks.Any(a=>S(a,"ShotType")=="Projectile"),
            "SEMI_AUTO"=>S(w.Raw,"Trigger") is "Semi" or "Semi-Auto",
            _=>false
        });
    }
    public static double Quantize(double value,double baseDamage)=>Math.Round(value/baseDamage*32,MidpointRounding.AwayFromZero)*baseDamage/32;
    public static double QuantizeCritical(double value)=>(float)(MathF.Floor(Math.Clamp((float)value,0,32)*(4095f/32)+.5f)*(32f/4095));
    public static double TargetShot(RangedResult r,EnemySelection enemy,RangedConditions? conditions=null)
    {
        var stats=EnemyScaling.Calculate(enemy);var definition=EnemyCatalog.Load().Get(enemy.EnemyId);
        var setup=new MeleeSetup(Faction:definition.Faction,Shields:stats.Shields,Overguard:stats.Overguard,Statuses:conditions?.Statuses);
        return r.Damage.Sum(p=>p.Value*MeleeTargetModel.Multiplier(p.Key,stats.Armor,setup))*(r.Multishot+Math.Max(0,r.Multishot-(r.Modifiers?.GetValueOrDefault("NativeMultishot")??r.Multishot))*((r.Modifiers?.GetValueOrDefault("ExtraMultishotDamage")??0)+Math.Max(0,(r.Modifiers?.GetValueOrDefault("ExtraProjectileDamageRatio")??0)-1)))*(CriticalExpectation(r.CriticalChance,r.CriticalMultiplier,r.Modifiers?.GetValueOrDefault("CriticalTierUpgradeChance")??0)+Math.Max(0,1-r.CriticalChance)*(r.Modifiers?.GetValueOrDefault("NonCriticalAttritionChance")??0)*(r.Modifiers?.GetValueOrDefault("NonCriticalAttritionBonus")??0))*(stats.Overguard>0?Math.Max(1,r.Modifiers?.GetValueOrDefault("OverguardMultiplier")??1):1);
    }
    public static double CriticalExpectation(double chance,double multiplier,double upgradeChance=0)
    {
        double Tier(double tier)=>Math.Max(0,1+tier*(multiplier-1));
        double Value(double tier)=>tier==0?1:(1-upgradeChance)*Tier(tier)+upgradeChance*Tier(tier+1);
        var low=Math.Floor(chance);var fraction=chance-low;
        return (1-fraction)*Value(low)+fraction*Value(low+1);
    }
    public static RangedResult Calculate(RangedBuild b,bool extraProjectile=false,bool irradiated=false)
    {
        b=RangedApproximations.Resolve(b);
        if(irradiated)b=b with{Conditions=(b.Conditions??new()) with{Headshot=false,DisableConditionOverload=true}};
        if(b.EncounterMode is <0 or >4)throw new ArgumentException("교전 모드 범위 오류");
        if(b.SchemaVersion!=1||b.Slots is null||b.Polarities is null||b.Slots.Length!=8||b.Polarities.Length!=8)throw new ArgumentException("총기 빌드 형식이 다릅니다.");
        if(!double.IsFinite(b.Distance)||b.Distance<0)throw new ArgumentException("거리는 0 이상의 유한한 값이어야 합니다.");
        var w=Resolve(b);
        if(RangedBurstAugments.IsFrenzy(w,b.AttackIndex))b=b with{Conditions=(b.Conditions??new()) with{Aiming=false}};
        if(RangedZymos.Applies(w)&&b.AttackIndex>=3)b=b with{Conditions=(b.Conditions??new()) with{InfestedOrbs=b.Conditions?.ZymosSporeOrbs,SimulorOrbs=null}};
        if(RangedInfestedOrbs.Applies(w)&&b.AttackIndex>0)b=b with{Conditions=(b.Conditions??new()) with{InfestedOrbs=null,SimulorOrbs=null}};
        var condition=b.Conditions??new();
        foreach(var link in RangedAttackLinks.Children(w.Id,b.AttackIndex))_=RangedAttackLinks.Delay(link,condition);
        if(!double.IsFinite(condition.CantareBuffSeconds)||condition.CantareBuffSeconds<0||condition.CantareBuffSeconds>(condition.JadeEnsemble?60:30))throw new ArgumentException("칸타레 합창의 시작 잔여 시간 범위 오류");
        if(condition.ReturningBladeHits is not null)_=RangedReturningBlades.Path(condition);
        if(condition.RefundRounding is not ("Unspecified" or "Down" or "Up")||condition.FrenzyBurstPause is {} pause&&(!double.IsFinite(pause)||pause<0||pause>300))throw new ArgumentException("점사 대기 또는 탄약 환급 조건 오류");
        if(!double.IsFinite(condition.InfiniteAmmoSeconds)||condition.InfiniteAmmoSeconds<0||condition.InfiniteAmmoSeconds>300)throw new ArgumentException("탄약 무한의 남은 시간은 0–300초입니다.");
        RangedGrenadeEvents.Validate(condition.GrenadeEvents,condition.GroupCount);RangedOrbInteractions.Validate(condition.OrbInteractions);RangedAzima.Validate(condition.Azima);RangedResidual.Validate(condition.Residual);RangedNullifiers.Validate(condition.Nullifier);RangedCannonade.Validate(condition.Cannonade);RangedOrdinarySimulor.Validate(condition.OrdinarySimulor);RangedSingularity.Validate(condition);RangedTendrils.Validate(condition);RangedPaxSeeker.Validate(condition);RangedAdhesive.Validate(condition);RangedSyndicates.Validate(condition);RangedPenetration.Validate(condition);RangedOrbPickups.Validate(b,w);RangedOrbBuffs.Validate(condition);RangedVoltageTraps.Validate(condition);RangedZymos.Validate(condition);
        if(!double.IsFinite(condition.HemaCriticalBonus)||condition.HemaCriticalBonus is <0 or >3.6||condition.HemaDrainPhase is {} phase&&(!double.IsFinite(phase)||phase is <0 or >1)||condition.HemaPartialRounding is not ("Unspecified" or "Down" or "Up"))throw new ArgumentException("헤마 체력 재장전 조건 오류");
        if(!double.IsFinite(condition.AffinityRange)||condition.AffinityRange<0||condition.AffinityRange>1000)throw new ArgumentException("공유 경험치 범위 오류");
        if(condition.VeloxHits is <0 or >100)throw new ArgumentException("벨록스 컨클루전 시작 명중 횟수는 0–100입니다.");
        if(condition.DodgeShotsRemaining is <0 or >2)throw new ArgumentException("회피 강화 사격은 0~2발입니다.");
        if(!double.IsFinite(condition.CriticalMutationBonus)||condition.CriticalMutationBonus is <0 or >3)throw new ArgumentException("크리티컬 뮤테이션 버프는 0~300% 범위입니다.");
        if(condition.ChargeDamageStacks is <0 or >50||condition.ChargeStackReset is not ("Unspecified" or "OnRelease" or "Persistent")||condition.DoubleTapStacks is <0 or >20||!double.IsFinite(condition.DoubleTapAge)||condition.DoubleTapAge<0||!double.IsFinite(condition.AlternateCharge)||condition.AlternateCharge is <0 or >1||!double.IsFinite(condition.OrbFirstPulseDelay)||condition.OrbFirstPulseDelay is <0 or >1||condition.GroupCount is <1 or >32||!double.IsFinite(condition.GroupSpacing)||condition.GroupSpacing<0||condition.ExternalSynthMods is <0 or >4||condition.HolsterRounding is not ("Unspecified" or "Down" or "Up" or "Accumulate")||condition.EnemiesWithinSixMeters<0||!double.IsFinite(condition.TargetHealthFraction)||condition.TargetHealthFraction is <0 or >1||condition.PreviousMagazineHits is <0 or >15||condition.StatusHealingProgress is <0 or >2||condition.PlayerMaxHealth<=0||condition.PlayerHealth>condition.PlayerMaxHealth||condition.FiringStacks<0||condition.EmptyReloadStacks<0||condition.AttritionStacks is <0 or >3||condition.MomentumStacks is <0 or >99||condition.EvolutionHeadshotStacks is <0 or >3||condition.ExternalVigilanteMods is <0 or >6||condition.WeaponBuffStacks<0||condition.SniperComboHits<0||condition.HiddenStatusCount<0||condition.Kills<0||condition.HeadshotKills<0||condition.Statuses?.Any(p=>!Enum.IsDefined(p.Key)||p.Value<0||p.Value>100000)==true||new[]{condition.KillExplosionDelay??0,condition.DuplexReleaseDelay,condition.DetonationDelay,condition.PlayerHealth,condition.PlayerMaxHealth,condition.PostureRecoverySeconds??0,condition.NearestOtherEnemyDistance,condition.SprintSpeed,condition.Roar,condition.Eclipse,condition.RadialDistance,condition.Elapsed,condition.HeadshotAge,condition.ReloadAge,condition.AbilityAge,condition.WeakpointMultiplier,condition.ArcaneAge??0,condition.PlayerArmor,condition.CurrentEnergy,condition.MaxEnergy,condition.StoredSurgeBonus,condition.RoundsReloaded,condition.WeaponBuffAge,condition.JumpAge,condition.EvolutionHeadshotAge,condition.AttritionAge,condition.LingeringSeconds,condition.PunchThroughBuffSeconds}.Any(v=>!double.IsFinite(v)||v<0))throw new ArgumentException("전투 조건 범위 오류");
        if(condition.MissedShots?.Any(n=>n<1||n>100000)==true||condition.MissedShots?.Distinct().Count()!=condition.MissedShots?.Length||!double.IsFinite(condition.PlayerMaxShields)||!double.IsFinite(condition.PlayerShieldDeficit)||condition.PlayerMaxShields<0||condition.PlayerShieldDeficit<0||condition.PlayerShieldDeficit>condition.PlayerMaxShields)throw new ArgumentException("빗나갈 발사 번호 또는 플레이어 실드 입력 오류");
        if(condition.ExternalEvents?.Any(e=>!double.IsFinite(e.Time)||e.Time<0||e.Time>300||!double.IsFinite(e.Value)||e.Value<=0||e.Kind is not ("PrimaryFire" or "ArrowPickup" or "WeaponAffinity" or "AllyKill" or "Reload" or "Dodge" or "BlockedShot" or "AlternateFire" or "Holster" or "Equip" or "EquipFromPrimary" or "PlayerShieldBreak" or "PlayerOverguardBreak" or "AbilityCast" or "EnergySpent" or "EnergyRestore" or "AmmoPickup" or "PrimaryAmmoDrop" or "SecondaryAmmoDrop" or "UniversalAmmoDrop"))==true)throw new ArgumentException("외부 발동 사건의 시간·종류·수치 오류");
        if(w.WeaponType=="Exalted Weapon")throw new ArgumentException("익절티드는 별도 구현 범위입니다.");
        RangedCollisions.Validate(b,false);RangedClusters.Validate(b);var attacks=Attacks(w);if(b.AttackIndex<0||b.AttackIndex>=attacks.Length)throw new ArgumentException("공격을 선택하세요.");
        var a=RangedSimulor.Attack(w,RangedSpool.Attack(w,attacks[b.AttackIndex],condition.SpoolShots),b.AttackIndex,condition);
        a=RangedOrdinarySimulor.Attack(w,a,b.AttackIndex,condition);
        a=RangedInfestedOrbs.Attack(w,a,b.AttackIndex,condition);a=RangedStug.Attack(w,a,b.AttackIndex,condition);a=RangedPentaAugments.Attack(w,b,a);a=RangedNapalm.Attack(w,b,a);a=RangedSingularity.Attack(b,a);
        if(RangedStug.Applies(w)&&b.AttackIndex==7&&(condition.StugAdapterInstalled||b.AutoTransform||b.Evolutions is {Length:>0}))throw new ArgumentException("인카논 어댑터가 설치된 스터그는 충전 보조 사격을 사용할 수 없습니다.");
        a=RangedApproximations.Attack(b,a);
        if(S(a,"UnsupportedReason") is {Length:>0} unsupported)throw new CombatRuleUnavailableException(unsupported);
        if(a.TryGetProperty("$duplicateFields",out _)||!a.TryGetProperty("Damage",out var rawDamage)||rawDamage.ValueKind!=JsonValueKind.Object)throw new ArgumentException("공격 피해 원문의 누락·중복 필드 확인이 필요합니다.");
        // Pinned Module:Weapons defines Multishot = 1 when the attack omits the field.
        double Required(string k)=>N(a,k)??(k=="Multishot"?1:throw new ArgumentException("위키 공격 수치 없음: "+k));
        var damage=new Dictionary<DamageType,double>();
        foreach(var p in rawDamage.EnumerateObject())
        {if(!Enum.TryParse<DamageType>(p.Name,out var t)||p.Value.ValueKind!=JsonValueKind.Number||!p.Value.TryGetDouble(out var v)||!double.IsFinite(v)||v<0)throw new ArgumentException("해석되지 않은 피해 유형·수치");damage[t]=v;}
        var baseDamage=damage.Values.Sum();if(baseDamage<=0&&N(a,"CarrierProjectile")!=1&&!((RangedSimulor.Applies(w)||w.Id=="wiki:primary/Simulor")&&b.AttackIndex==0))throw new ArgumentException("공격 피해가 없습니다.");
        if(b.Progenitor is {} bonus)
        {
            if(!w.Raw.TryGetProperty("IsLichWeapon",out var lich)||lich.ValueKind!=JsonValueKind.True)throw new ArgumentException("추가 원소는 쿠바·테넷·코다 무기에만 적용합니다.");
            if(!double.IsFinite(bonus.Fraction)||bonus.Fraction<.25||bonus.Fraction>.6||bonus.Type is not (DamageType.Impact or DamageType.Heat or DamageType.Cold or DamageType.Electricity or DamageType.Toxin or DamageType.Magnetic or DamageType.Radiation))throw new ArgumentException("추가 원소는 게임에 표시된 25~60%와 허용된 속성을 선택하세요.");
            damage[bonus.Type]=damage.GetValueOrDefault(bonus.Type)+baseDamage*bonus.Fraction;
            baseDamage*=1+bonus.Fraction;
        }
        var nativeBaseDamage=baseDamage;
        var evolution=new Dictionary<string,double>();RangedIncarnons.Apply(b,w,evolution);RangedEvolutionEvents.Apply(b,evolution,RangedIncarnons.IsForm(w,b.AttackIndex),irradiated);
        if(evolution.GetValueOrDefault("BaseDamage") is {} baseIncrease&&baseIncrease!=0)
        {
            var newBase=baseDamage+baseIncrease;if(newBase<=0)throw new ArgumentException("진화 후 기본 피해가 0 이하입니다.");
            foreach(var type in damage.Keys.ToArray())damage[type]*=newBase/baseDamage;baseDamage=newBase;
        }
        var sums=new Dictionary<string,double>(evolution);var elements=new List<(DamageType Type,double Value)>();var selected=new List<RangedMod>();var drain=0;
        for(int i=0;i<9;i++)
        {
            var slotPolarity=i==8?b.ExilusPolarity:b.Polarities[i];
            if(slotPolarity is not ("None" or "Madurai" or "Vazarin" or "Naramon" or "Zenurik"))throw new ArgumentException("지원하지 않는 슬롯 극성");
            if((i==8?b.Exilus:b.Slots[i]) is not {} slot)continue;
            var m=ModCatalog.Mods.SingleOrDefault(x=>x.Id==slot.ModId)??throw new ArgumentException("총기 모드 식별자 없음");
            if(RangedRivens.IsRiven(m))m=RangedRivens.Resolve(m,b.Riven,b.WeaponId,slot.Rank);
            if(i==8&&!m.Exilus)throw new ArgumentException("엑실러스 전용 슬롯에 넣을 수 없는 모드입니다.");
            if(!Compatible(m,w)||m.Limitations.Length>0)throw new ArgumentException(m.Name+": "+(m.Limitations.FirstOrDefault()??"호환되지 않는 무기군"));
            if(slot.Rank<0||slot.Rank>m.MaxRank)throw new ArgumentException("모드 랭크 범위 오류");
            if(selected.Any(x=>x.Id==m.Id||x.Incompatible.Contains(m.EnglishName)||m.Incompatible.Contains(x.EnglishName)))throw new ArgumentException("중복·동시 장착 불가 모드: "+m.Name);
            selected.Add(m);var cost=m.BaseDrain+slot.Rank;drain+=slotPolarity=="None"||m.Polarity=="None"?cost:slotPolarity==m.Polarity?(int)Math.Ceiling(cost/2d):(int)Math.Ceiling(cost*1.25);
            foreach(var (stat,max) in m.Effects)
            {var value=m.RankEffects?.TryGetValue(stat,out var rankValues)==true?rankValues[slot.Rank]:stat.EndsWith("Limit")||(stat.EndsWith("Duration")&&stat is not ("StatusDuration" or "SniperComboDuration"))||stat.StartsWith("Lock")||stat is "VigilanteSetChance" or "DistantThreshold" or "IsolationRadius" or "ExtraElectricBaseChance"?max:max*(slot.Rank+1)/(m.MaxRank+1);if(irradiated&&stat is "Heat" or "Cold" or "Electricity" or "Toxin" or "Radiation" or "Magnetic" or "Viral" or "Corrosive" or "Blast" or "Gas")value*=.67;if(stat is "Heat" or "Cold" or "Electricity" or "Toxin")elements.Add((Enum.Parse<DamageType>(stat),value*baseDamage));sums[stat]=sums.GetValueOrDefault(stat)+value;}
            double E(string key)=>m.Effects.GetValueOrDefault(key)*(slot.Rank+1)/(m.MaxRank+1);
            void Add(string key,double value)=>sums[key]=sums.GetValueOrDefault(key)+value;
            var kills=Math.Max(0,Math.Min(condition.Kills,m.Effects.GetValueOrDefault("KillStackLimit",1))-(m.Effects.GetValueOrDefault("KillDuration")>0?Math.Floor(condition.Elapsed/m.Effects["KillDuration"]):0));
            if(m.Effects.ContainsKey("MagazineHitBonus")){var value=(.05+.02*slot.Rank)*condition.PreviousMagazineHits;Add("Multishot",value);Add("StatusChance",value);}
            if(condition.Sliding){Add("Recoil",E("SlidingRecoil"));Add("Accuracy",E("SlidingAccuracy"));}
            Add("Multishot",E("KillMultishot")*kills);Add("ProjectileSpeed",E("KillProjectileSpeed")*kills);Add("BeamRangePercent",E("KillBeamRangePercent")*kills);
            Add("Damage",Math.Min(3.6,Math.Floor(condition.PlayerHealth/75)*E("HealthStepDamage")));Add("StatusChance",Math.Min(3.6,Math.Floor(condition.PlayerHealth/75)*E("HealthStepStatusChance")));
            if(condition.Airborne||condition.AimGlide)Add("AmmoEfficiency",E("AirborneAmmoEfficiency"));
            if(condition.WallLatch){Add("CriticalChance",E("LatchCriticalChance"));Add("StatusChance",E("LatchStatusChance"));}
            if(condition.RecentJump&&condition.JumpAge<m.Effects.GetValueOrDefault("JumpDuration")){Add("CriticalChance",E("JumpCriticalChance"));Add("StatusChance",E("JumpStatusChance"));}
            Add("ActiveCO",E("ConditionOverload")*kills);
            Add("ReloadSpeed",E("KillReloadSpeed")*Math.Min(1,kills));
            if(condition.Aiming){Add("FinalDamage",E("AimFinalDamage"));Add("Accuracy",E("AimAccuracy"));Add("ProjectileSpeed",E("AimProjectileSpeed"));Add("FireRate",E("AimFireRate"));Add("Damage",E("AimDamage"));Add("CriticalDamage",E("KillCriticalDamage")*Math.Min(1,kills));if(condition.RecentHeadshot&&condition.HeadshotAge<m.Effects.GetValueOrDefault("HeadshotDuration"))Add("CriticalChance",E("HeadshotCriticalChance"));Add("CriticalChance",E("HeadshotKillCriticalChance")*(condition.Elapsed<m.Effects.GetValueOrDefault("HeadshotDuration")?Math.Min(condition.HeadshotKills,m.Effects.GetValueOrDefault("HeadshotKillStackLimit")):0));if(S(w.Raw,"ReloadStyle")!="Regenerate"&&condition.RecentReload&&condition.ReloadAge<m.Effects.GetValueOrDefault("ReloadDuration"))Add("FireRate",E("ReloadFireRate"));if(condition.RecentAbility&&condition.AbilityAge<m.Effects.GetValueOrDefault("AbilityDuration"))Add("StatusChance",E("AbilityStatusChance"));}
            if(condition.Invisible){Add("Damage",E("InvisibleDamage"));Add("CriticalDamage",E("InvisibleCriticalDamage"));}
        }
        if(RangedPassives.Get(w.Id)?.Kind=="Ocucor"){sums["CriticalChance"]=sums.GetValueOrDefault("CriticalChance")+sums.GetValueOrDefault("TendrilCriticalChance")*Math.Min(4,condition.WeaponBuffStacks);sums["StatusChance"]=sums.GetValueOrDefault("StatusChance")+sums.GetValueOrDefault("TendrilStatusChance")*Math.Min(4,condition.WeaponBuffStacks);}
        
        if(condition.AimGlide)sums["ReloadSpeed"]=sums.GetValueOrDefault("ReloadSpeed")+sums.GetValueOrDefault("GlideReloadSpeed");
        if(condition.Airborne)sums["FireRate"]=sums.GetValueOrDefault("FireRate")+sums.GetValueOrDefault("AirborneFireRate");
        if(condition.Aiming)sums["Accuracy"]=sums.GetValueOrDefault("Accuracy")+evolution.GetValueOrDefault("AimAccuracy");
        var distanceBonus=b.Distance>V("DistantThreshold")?1+V("DistantFinalDamage"):1;
        if(condition.NearestOtherEnemyDistance>=V("IsolationRadius")&&b.Distance>=V("IsolationRadius"))sums["Damage"]=V("Damage")+V("IsolatedDamage");
        var bodyshotFactor=condition.Headshot?1:1+V("BodyshotDamage");
        RangedArcanes.Apply(b,w,sums);
        RangedKitguns.Apply(b,sums);
        if(RangedReturningBlades.Get(w.Id) is {} returning){sums["InfiniteAmmo"]=1;if(condition.CantareBuffSeconds>0)sums["ReloadSpeed"]=sums.GetValueOrDefault("ReloadSpeed")+(condition.JadeEnsemble?returning.EnsembleBonus:returning.ReloadBonus);}
        if(RangedVectis.Applies(w)&&b.AttackIndex>0)sums["PunchThrough"]=0;
        if(RangedRecall.Get(w.Id) is not null){sums["InfiniteAmmo"]=1;sums["PunchThrough"]=0;}
        RangedPassives.Apply(b,w,sums);
        RangedKomorex.Apply(b,sums);
        if(V("CriticalMutationStep")>0){sums["CriticalChance"]=V("CriticalChance")+condition.CriticalMutationBonus;sums["CriticalDamage"]=V("CriticalDamage")+condition.CriticalMutationBonus;}
        if(RangedPassives.Get(w.Id)?.Kind=="Onos"&&b.AttackIndex is 2 or 3){sums["CriticalChance"]=V("CriticalChance")+V("ChargeBlastCritical")*condition.ChargeDamageStacks;sums["CriticalDamage"]=V("CriticalDamage")+V("ChargeBlastCritical")*condition.ChargeDamageStacks;}
        if(RangedFields.Get(w.Id) is {Single:true,Explosion:<0} spear&&spear.Parents.Contains(b.AttackIndex))sums["InfiniteMagazine"]=1;
        sums["HolsterReload"]=sums.GetValueOrDefault("HolsterReload")+.05*Math.Min(4,condition.ExternalSynthMods+selected.Count(m=>m.EnglishName=="Synth Charge"));
        if(V("SpontaneousDamage")>0)
        {
            if(condition.SingularityDamageBracket=="Unspecified")throw new CombatRuleUnavailableException("스폰테이니어스의 +90%가 기본 피해 가산인지 별도 배율인지 원문에 없습니다. 피해 브라켓 시나리오를 선택하세요.");
            if(condition.SingularityDamageBracket=="Additive")sums["Damage"]=V("Damage")+V("SpontaneousDamage");else sums["SpontaneousFinal"]=V("SpontaneousDamage");
        }
        if(RangedPaxSeeker.Is(a))
        {
            var definition=RangedKitguns.Get(w.Id)!;var parts=b.Kitgun??RangedKitguns.Default(definition);
            var multi=Math.Max(0,1+(V("LockMultishot")>0?0:V("Multishot")));sums["PaxSourceMultishot"]=multi;
            var basis=RangedPaxSeeker.Basis(definition,parts,V("Damage"),condition.PaxSeekerRoll??multi,condition.AllowApproximations);
            for(int i=0;i<elements.Count;i++)elements[i]=(elements[i].Type,elements[i].Value*basis/baseDamage);
            damage=new(){[DamageType.Slash]=basis*.5,[DamageType.Puncture]=basis*.5};baseDamage=basis;nativeBaseDamage=basis;
            sums["Damage"]=0;sums["Multishot"]=0;sums["PunchThrough"]=0;sums["CriticalChance"]=2*V("CriticalChance");sums["CriticalDamage"]=2*V("CriticalDamage");
        }
        if(sums.GetValueOrDefault("PassiveToxin")>0)elements.Add((DamageType.Toxin,baseDamage*sums["PassiveToxin"]));
        double V(string k)=>sums.GetValueOrDefault(k);
        foreach(var t in new[]{DamageType.Impact,DamageType.Puncture,DamageType.Slash})if(damage.ContainsKey(t))damage[t]*=Math.Max(0,1+V(t.ToString()));
        foreach(var t in new[]{DamageType.Radiation,DamageType.Magnetic,DamageType.Viral,DamageType.Corrosive,DamageType.Blast,DamageType.Gas})damage[t]=damage.GetValueOrDefault(t)+baseDamage*V(t.ToString());
        foreach(var t in new[]{DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin})if(damage.Remove(t,out var innate))elements.Add((t,innate));
        if(RangedZymos.Applies(w)&&b.AttackIndex==4)sums["Damage"]=0;
        var ordered=elements.Select(e=>e.Type).Distinct().Select(t=>(Type:t,Value:elements.Where(e=>e.Type==t).Sum(e=>e.Value))).ToArray();
        for(int i=0;i<ordered.Length;i+=2){var t=i+1<ordered.Length?DamageEngine.Combine(ordered[i].Type,ordered[i+1].Type):ordered[i].Type;damage[t]=damage.GetValueOrDefault(t)+ordered[i].Value+(i+1<ordered.Length?ordered[i+1].Value:0);}
        RangedOrbBuffs.AddElement(w,b.AttackIndex,condition,baseDamage,damage,sums);
        var fieldRule=RangedFields.Get(w.Id);
        if(RangedReloadGrenades.Get(w.Id) is not null&&b.AttackIndex>0)sums["Multishot"]=0;
        if(fieldRule is {Single:true}&&b.AttackIndex>=fieldRule.Parents[0])sums["Multishot"]=0;
        if(RangedStug.Applies(w)&&!RangedStug.Launch(w,b.AttackIndex))sums["Multishot"]=0;
        if(RangedZymos.Applies(w)&&b.AttackIndex>=3)sums["Multishot"]=0;
        if((RangedSimulor.Applies(w)||RangedOrdinarySimulor.Applies(w))&&b.AttackIndex>0)sums["Multishot"]=0;
        if(RangedSimulor.Applies(w)&&b.AttackIndex==3)sums["BlastRadius"]=0;
        if(RangedPenetration.Locked(w,b.AttackIndex))sums["PunchThrough"]=0;
        var radial=S(a,"ShotType")=="AoE";
        var sc=(Required("StatusChance")+V("BaseStatusChance"))*Math.Max(0,1+V("StatusChance"))+V("FlatStatusChance");
        if(condition.FluxOscillation is {} oscillation&&(!double.IsFinite(oscillation)||oscillation is <0 or >1))throw new ArgumentException("플럭스 변동항 위치는 0~100%입니다.");
        if(condition.HigasaForcedStatus is {} higasaStatus&&higasaStatus>DamageType.Corrosive)throw new ArgumentException("히가사 강제 상태 시나리오는 일반 피해 속성을 선택하세요.");
        if(V("OscillatingStatusChance")>0){sc*=1+V("OscillatingStatusChance")*(condition.FluxOscillation??0);if(w.EnglishName=="Tenet Flux Rifle"&&condition.TenetFluxDoubleBase==true)sc*=2;}
        if(S(w.Raw,"Family")=="Sporothrix"&&b.AttackIndex==1)sc+=Required("StatusChance")*V("VolatileStatus");
        var derivedCritical=Math.Min(V("CriticalFromStatusCap"),sc*V("CriticalFromStatus"));
        if(RangedFields.Get(w.Id) is {UnmoddedCriticalStatus:true} fixedField&&b.AttackIndex==fixedField.Child)sc=Required("StatusChance");
        if(RangedHema.Applies(w)&&V("HealthReloadCriticalStep")>0&&condition.HemaHost){sums["CriticalChance"]=V("CriticalChance")+condition.HemaCriticalBonus;sums["CriticalDamage"]=V("CriticalDamage")+condition.HemaCriticalBonus;}
        if(V("SentientBarrageCritical")>0){if(b.AttackIndex==2)throw new ArgumentException("센티언트 배라지 장착 시 보조 공격 폭발은 발생하지 않습니다.");if(b.AttackIndex==1&&condition.SentientBarrageReady){sums["CriticalChance"]=V("CriticalChance")+V("SentientBarrageCritical");sums["CriticalDamage"]=V("CriticalDamage")+V("SentientBarrageCritical");}}
        var conversionCritical=Math.Max(0,(Required("CritChance")+V("BaseCriticalChance")+derivedCritical)*Math.Max(0,1+V("CriticalChance"))+V("FlatCriticalChance"));
        var cc=(Required("CritChance")+V("BaseCriticalChance")+derivedCritical)*Math.Max(0,1+V("CriticalChance")+(condition.Headshot&&!radial?V("WeakpointCriticalChance"):0));var cd=QuantizeCritical(Required("CritMultiplier")+V("BaseCriticalDamage"))*(1+V("CriticalDamage"));
        if(RangedZymos.Applies(w)&&b.AttackIndex>=3){cc+=V("FlatCriticalChance")*Math.Max(0,1+V("CriticalChance"));cd*=Math.Max(0,1+V("CriticalDamage"));}
        cc=Math.Max(0,cc+V("FlatCriticalChance"));cd+=V("FlatCriticalDamage");
        if(RangedFields.Get(w.Id) is {UnmoddedCriticalStatus:true} fixedCrit&&b.AttackIndex==fixedCrit.Child){cc=Required("CritChance");cd=QuantizeCritical(Required("CritMultiplier"));}
        if(RangedReturningBlades.Get(w.Id) is {} returnCrit&&b.AttackIndex==1)cc=returnCrit.CriticalChance;
        if(!radial){cc+=.05*Math.Min(w.Id=="wiki:primary/Evensong"?7:5,condition.Statuses?.GetValueOrDefault(DamageType.Puncture)??0);var cold=condition.Statuses?.GetValueOrDefault(DamageType.Cold)??0;cd+=cold>=10?1:cold>0?.05+.05*cold:0;}
        if(V("NoBodyCritical")>0&&(!condition.Headshot||radial))cc=0;
        sc+=Math.Min(V("StatusFromCriticalCap"),conversionCritical*V("StatusFromCritical"))*Math.Max(0,1+V("StatusChance"));
        if(cc<(V("LowCriticalThreshold")>0?V("LowCriticalThreshold"):.5)&&V("LowCriticalBaseDamage")>0)cd+=(QuantizeCritical(Required("CritMultiplier")+V("BaseCriticalDamage")+V("LowCriticalBaseDamage"))-QuantizeCritical(Required("CritMultiplier")+V("BaseCriticalDamage")))*Math.Max(0,1+V("CriticalDamage"));
        cd*=Math.Max(1,V("CriticalDamageMultiplier"));
        if(condition.Headshot&&!radial&&condition.DodgeShotsRemaining>0)cc*=1+V("DodgeHeadshotCriticalMultiplier");
        if(RangedSimulor.Applies(w)&&b.AttackIndex==3){cc=0;cd=1;sc=0;sums["CriticalTierUpgradeChance"]=0;sums["NonCriticalAttritionChance"]=0;}
        if(RangedZymos.Applies(w)&&b.AttackIndex==3)sc=0;
        var chargedRounds=RangedMagazineCharge.Applies(w,b.AttackIndex)?RangedMagazineCharge.Rounds(w,condition,sums):1;
        var nativeMultishot=(Required("Multishot")+V("BaseMultishot"))*chargedRounds;
        var ms=nativeMultishot*Math.Max(0,1+(V("LockMultishot")>0?0:V("Multishot")*(1+V("FormMultishotBonus"))));if(cc<0||!double.IsFinite(cd)||sc<0||ms<0)throw new ArgumentException("지원 범위를 벗어난 공격 수치");
        if(w.Id=="wiki:primary/Phantasma"&&b.AttackIndex==0)ms=nativeMultishot+5*(V("LockMultishot")>0?0:V("Multishot"));
        if(RangedKitguns.Get(w.Id) is {Chamber:"Gaze",Category:"primary"}&&b.AttackIndex==1)ms=1;
        if(RangedDeployments.Get(w.Id) is {AllowBatchOverflow:false} deploymentLimit)ms=Math.Min(ms,deploymentLimit.Limit);
        // Conversion replaces a share of the completed elemental distribution, not a bonus to native IPS.
        var converted=damage.Values.Sum();var conversion=V("ConvertImpact")+V("ConvertPuncture")+V("ConvertSlash");
        if(conversion>1)throw new ArgumentException("피해 전환 합계가 100%를 넘습니다.");
        foreach(var t in damage.Keys.ToArray())damage[t]*=1-conversion;
        foreach(var t in new[]{DamageType.Impact,DamageType.Puncture,DamageType.Slash})damage[t]=damage.GetValueOrDefault(t)+converted*V("Convert"+t);
        var arsenal=new Dictionary<DamageType,double>(damage);
        if(baseDamage>0)foreach(var t in damage.Keys.ToArray())damage[t]=Quantize(damage[t],baseDamage);
        var falloff=1d;
        var beam=RangedEngine.S(a,"Trigger")=="Held"||RangedEngine.S(a,"Trigger")==""&&RangedEngine.S(w.Raw,"Trigger")=="Held";
        var radiusMods=!(RangedReloadGrenades.Get(w.Id) is not null&&b.AttackIndex>0)&&RangedCompression.Rules.FirstOrDefault(r=>r.WeaponId==w.Id&&r.AttackIndex==b.AttackIndex)?.RangeMod!=false;
        var radialScale=(radiusMods?1+V("BlastRadius"):1)*sums.GetValueOrDefault("CompressionRadius",1);
        var projectileScale=1+V("ProjectileSpeed");
        var rangeScale=radial?radialScale:beam?1+V("BeamRangePercent"):S(a,"ShotType")=="Projectile"?projectileScale:1;
        var extraRadius=S(w.Raw,"Family")=="Sporothrix"&&b.AttackIndex==1?V("VolatileRadius"):0;
        var maxRange=(N(a,"Range")??double.PositiveInfinity)*rangeScale+(beam?V("BeamRange"):0)+extraRadius;
        if(double.IsFinite(maxRange))sums["EffectiveRange"]=maxRange;

        if(a.TryGetProperty("Falloff",out var f))
        {var start=N(f,"StartRange");var end=N(f,"EndRange");var reduction=N(f,"Reduction");if(start is null||end is null||reduction is null)throw new ArgumentException("피해 감쇠 원문 불완전");var d=S(a,"ShotType")=="AoE"?condition.RadialDistance:b.Distance;var distanceScale=radial?radialScale:RangedReloadGrenades.Get(w.Id) is not null&&b.AttackIndex==0?projectileScale:beam||RangedPassives.Get(w.Id)?.FalloffSpeedScaling==false?1:projectileScale;if(b.AttackIndex==1&&condition.BuckshotReady)distanceScale*=1+V("BuckshotFalloff");if(distanceScale<=0)throw new ArgumentException("거리 배율이 0 이하입니다.");start*=distanceScale;end=end*distanceScale+extraRadius;falloff=1-reduction.Value*(end>start?Math.Clamp((d-start.Value)/(end.Value-start.Value),0,1):d>end?1:0);}
        if(w.Id==RangedPullWeapons.Proboscis&&b.AttackIndex==1){maxRange=9;falloff=1;}
        var connects=!(maxRange>0&&(radial?condition.RadialDistance:b.Distance)>maxRange);
        var lowHealthBase=V("LowHealthChargeExclusion")>0&&!RangedIncarnons.IsForm(w,b.AttackIndex)?attacks[0].GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble()):nativeBaseDamage;
        var lowHealthBonus=condition.TargetHealthFraction<.5&&baseDamage>0?V("LowHealthNativeDamage")*lowHealthBase/baseDamage:0;
        var baseFactor=Math.Max(0,1+V("Damage")+lowHealthBonus);
        if(extraProjectile&&baseDamage>0)baseFactor+=V("ExtraMultishotFlatDamage")/baseDamage;
        var faction=b.Enemy is {} target?EnemyCatalog.Load().Get(target.EnemyId).Faction:"Neutral";
        var factionBonus=faction switch{"KuvaGrineer"=>"Grineer","CorpusAmalgam"=>"Corpus","InfestedDeimos"=>"Infested","TheMurmur"=>"Murmur",_=>faction};
        var factionFactor=1+V("Faction"+factionBonus)+condition.Roar;
        var co=RangedPaxSeeker.Is(a)?new RangedCoRule(w.Id,b.AttackIndex,"Multiplicative",1):RangedCoCatalog.Get(w,b.AttackIndex);
        if(RangedFields.Get(w.Id) is {Child:2,SpawnDelay:>0} cloud&&b.AttackIndex==cloud.Child&&condition.SourceAttack==0&&condition.AttachedTarget)co=new(w.Id,b.AttackIndex,"Multiplicative",1);
        var coBonus=condition.DisableConditionOverload?0:((V("ActiveCO")+V("InnateCO"))*((condition.Statuses?.Count(p=>p.Value>0)??0)+condition.HiddenStatusCount)+V("Shiver")*(condition.Statuses?.GetValueOrDefault(DamageType.Cold)??0))*co.Ratio;
        // CO is excluded from final-damage multipliers/falloff in the additive branch.
        var finalFactor=V("OrbFinalMultiplier")>0?V("OrbFinalMultiplier"):1;
        finalFactor*=(1+V("SpontaneousFinal"))*distanceBonus*bodyshotFactor*(1+V("CompressionMultiplier"))*(1+V("SniperMultiplier"))*falloff*(1+condition.Eclipse)*(1+V("FinalDamage"))*(1+V("SurgeMultiplier"))*(condition.FinalShot&&V("FullMagazineDamage")==0&&N(w.Raw,"Magazine")>=6?1+V("FinalShotDamage"):1);
        if(co.Mode=="Additive")coBonus*=baseDamage>0?nativeBaseDamage/baseDamage:0;
        if(co.MultishotIndependent)coBonus=ms>0?coBonus/ms:0;
        if(co.ExtraProjectileRatio!=1)coBonus*=Math.Min(1,Required("Multishot")/Math.Max(ms,double.Epsilon))+(1-Math.Min(1,Required("Multishot")/Math.Max(ms,double.Epsilon)))*co.ExtraProjectileRatio;
        if(condition.TargetHealthFraction<.5){if(RangedIncarnons.IsForm(w,b.AttackIndex))coBonus+=V("LowHealthCOBracket");else baseFactor+=V("LowHealthCOBracket");}
        var excludedLowHealthCO=V("LowHealthIgnoresCO")>0?lowHealthBonus*coBonus:0;
        var factor=connects?(co.Mode=="Multiplicative"?(baseFactor*(1+coBonus)-excludedLowHealthCO)*finalFactor:baseFactor*finalFactor+(co.Mode=="Additive"?coBonus:0))*factionFactor:0;
        var statusFactor=(1+V("SpontaneousFinal"))*(V("OrbFinalMultiplier")>0?V("OrbFinalMultiplier"):1)*distanceBonus*bodyshotFactor*(1+V("CompressionMultiplier"))*(1+V("SniperMultiplier"))*(co.Mode=="Multiplicative"?baseFactor*(1+coBonus)-excludedLowHealthCO:baseFactor+(co.Mode=="Additive"?coBonus:0))*(1+condition.Eclipse)*(1+V("FinalDamage"))*(1+V("SurgeMultiplier"));
        var altRule=RangedAlternates.Get(w.Id);var altFactor=altRule is {DamagePerCharge:true}&&altRule.Alternate.Contains(b.AttackIndex)?RangedAlternates.Charges(altRule,condition.AlternateCharge):1;
        var firstMagazineFactor=altFactor*(1+V("DoubleTapDamage")*(condition.DoubleTapAge<2?condition.DoubleTapStacks:0))*(condition.FirstMagazineShot?1+V("FirstMagazineDamage"):1)*(V("FullMagazineDamage")>0?Math.Max(1,Math.Round(((N(w.Raw,"Magazine")??1)+V("BaseMagazine"))*(1+V("Magazine")),MidpointRounding.AwayFromZero)):1);
        if(RangedPhantasma.Applies(w)&&b.AttackIndex>0)firstMagazineFactor*=RangedPhantasma.Scale(w,condition,sums);
        foreach(var t in damage.Keys.ToArray())damage[t]*=factor*firstMagazineFactor;statusFactor*=firstMagazineFactor;
        foreach(var t in arsenal.Keys.ToArray())arsenal[t]*=baseFactor;
        if(condition.Headshot&&!radial&&!(RangedPaxSeeker.Is(a)&&RangedKitguns.Get(w.Id)?.Chamber=="Catchmoon")){var nativeHeadshot=(RangedPassives.Get(w.Id)?.NoHeadshotBonus.Contains(b.AttackIndex)==true||RangedKitguns.Get(w.Id)?.Chamber=="Catchmoon")?1:condition.WeakpointMultiplier;var weakpoint=(nativeHeadshot+1.5*V("WeakpointDamage"))*(1+V("HeadshotMultiplier")+Math.Max(0,N(a,"ExtraHeadshotDmg")??0));foreach(var t in damage.Keys.ToArray())damage[t]*=weakpoint;statusFactor*=weakpoint;if(condition.HeadCritical&&nativeHeadshot>1)cd*=2;}
        if(RangedZymos.Applies(w)&&b.AttackIndex==2&&condition.Headshot&&condition.AttachedTarget&&condition.ZymosHeadExplosionHitsHead){foreach(var t in damage.Keys.ToArray())damage[t]*=2;statusFactor*=2;cd*=2;}
        if(RangedVectis.Applies(w)&&b.AttackIndex==2&&condition.Headshot&&condition.AttachedTarget&&condition.VectisHeadshotAoeBonus){var vectisBonus=1+V("HeadshotMultiplier");foreach(var t in damage.Keys.ToArray())damage[t]*=vectisBonus;statusFactor*=vectisBonus;}
        var promote=Math.Min(.3,V("VigilanteSetChance")+.05*condition.ExternalVigilanteMods);
        sums["CriticalTierUpgradeChance"]=Math.Min(.3,V("VigilanteSetChance")+.05*condition.ExternalVigilanteMods);
        var damageMultishot=ms+Math.Max(0,ms-nativeMultishot)*V("ExtraMultishotDamage");
        sums["NativeMultishot"]=nativeMultishot;
        var shot=damage.Values.Sum()*damageMultishot*(CriticalExpectation(cc,cd,promote)+Math.Max(0,1-cc)*V("NonCriticalAttritionChance")*V("NonCriticalAttritionBonus"));
        if(!extraProjectile&&V("ExtraMultishotFlatDamage")>0&&ms>nativeMultishot)
        {
            var extra=Calculate(b,true,irradiated);
            shot+=(extra.Damage.Values.Sum()-damage.Values.Sum())*(ms-nativeMultishot)*(CriticalExpectation(cc,cd,promote)+Math.Max(0,1-cc)*V("NonCriticalAttritionChance")*V("NonCriticalAttritionBonus"));
            sums["ExtraProjectileDamageRatio"]=damage.Values.Sum()>0?extra.Damage.Values.Sum()/damage.Values.Sum():1;
        }
        var cadence=RangedTiming.Calculate(w,a,V("LockFireRate")>0?0:(1+V("FireRate"))*(1+V("FinalFireRate"))-1,V("ReloadSpeed"),V("Magazine"),V("BaseMagazine"),condition.DuplexReleaseDelay);
        cadence=cadence with{Magazine=cadence.Magazine+(int)Math.Round(V("FlatMagazine"),MidpointRounding.AwayFromZero),Reload=cadence.Reload*(1-V("ReloadTimeReduction"))};
        if(RangedPassives.Get(w.Id)?.Kind=="Grimoire")cadence=cadence with{Magazine=1,AmmoCost=1,NoReload=true,Reload=0,ShotDelay=b.AttackIndex==2?45:cadence.ShotDelay};
        if(altRule is {NoAmmo:true}&&altRule.Alternate.Contains(b.AttackIndex))cadence=cadence with{Magazine=1,AmmoCost=1,NoReload=true,Reload=0};
        if(V("FullMagazineDamage")>0&&b.AttackIndex==1)cadence=cadence with{AmmoCost=cadence.Magazine,NoShotDelay=true};
        if(condition.SpoolShots is not null&&RangedSpool.Get(w.Id) is not null)cadence=cadence with{SpoolStart=null,SpoolStep=null};
        if(RangedPhantasma.Applies(w)&&b.AttackIndex==1){var rounds=RangedPhantasma.Rounds(w,condition,sums);cadence=cadence with{Charge=rounds/11d,AmmoCost=rounds};}
        var issues=cadence.Issues.ToList();
        if(RangedClusters.Get(w.Id) is {} clusterComponent&&b.AttackIndex==clusterComponent.Contact)issues.Add("자탄은 독립 발사하지 않습니다. 본탄 발사를 선택하면 자탄 직격과 폭발을 함께 계산합니다.");
        if(w.Id=="wiki:secondary/Aksomati Prime"&&condition.AllowApproximations){issues.Remove("발사 가속 시작 속도·증가율 원문 확인 필요");cadence=cadence with{SpoolStart=.25,SpoolStep=.15};}
        if(RangedQuanta.Applies(w)&&b.AttackIndex>=2)issues.Add("큐브 파생 폭발은 큐브 발사를 선택하면 직격·외부 기폭·만료·연쇄와 함께 계산합니다.");
        if(V("OscillatingStatusChance")>0&&condition.FluxOscillation is null)issues.Add("플럭스 변동항의 발생 분포는 위키에 없습니다. 전용 모드 설정에서 고정 위치 시나리오를 선택하세요.");
        if(V("OscillatingStatusChance")>0&&w.EnglishName=="Tenet Flux Rifle"&&condition.TenetFluxDoubleBase is null)issues.Add("테넷 플럭스 본문의 기본 상확 2배 설명과 모드의 변동 곱연산이 중복인지 불명확합니다. 전용 모드 설정에서 계산 기준을 선택하세요.");
        if(V("HigasaForcedStatus")>0&&b.AttackIndex==1&&condition.HigasaForcedStatus is null)issues.Add("히가사 서레이션의 무작위 강제 상태 종류·가중치는 위키에 없습니다. 전용 모드 설정에서 특정 결과의 시나리오를 선택하세요.");
        if(RangedPassives.Get(w.Id)?.Kind=="Onos"&&b.AttackIndex>=1)
        {
            if(condition.ChargeCycleSeconds is {} chargeSeconds)
            {
                if(!double.IsFinite(chargeSeconds)||chargeSeconds<=0||chargeSeconds>300||!double.IsFinite(condition.ChargeRecoverySeconds)||condition.ChargeRecoverySeconds<0||condition.ChargeRecoverySeconds>300)throw new ArgumentException("충전/후속 대기 시간 범위를 확인하세요.");
                if(b.AttackIndex==2){issues.Remove("충전 시간 원문 확인 필요");cadence=cadence with{Charge=chargeSeconds,ShotDelay=condition.ChargeRecoverySeconds,NoShotDelay=true};}
            }
        }
        if(RangedPassives.Get(w.Id)?.Kind!="Onos"&&cadence.Trigger.Contains("Charge")&&issues.Contains("충전 시간 원문 확인 필요")&&condition.ChargeCycleSeconds is {} explicitCharge)
        {
            if(!double.IsFinite(explicitCharge)||explicitCharge<=0||explicitCharge>300||!double.IsFinite(condition.ChargeRecoverySeconds)||condition.ChargeRecoverySeconds<0||condition.ChargeRecoverySeconds>300)throw new ArgumentException("충전·후속 대기 시간은 유효한 0~300초 값이어야 합니다.");
            issues.Remove("충전 시간 원문 확인 필요");cadence=cadence with{Charge=explicitCharge,ShotDelay=condition.ChargeRecoverySeconds,NoShotDelay=true};
        }
        if(V("LockFireRate")>0&&cadence.Trigger!="Semi-Auto")throw new ArgumentException("캐논네이드는 반자동 공격에만 장착할 수 있습니다.");
        var incarnonCharges=N(a,"IncarnonCharges");
        if(incarnonCharges is {} charges)
        {
            if(b.IncarnonAmmo is {} supplied&&(supplied<1||supplied>charges))throw new ArgumentException("인카논 시작 탄약이 충전 한도를 벗어납니다.");
            cadence=cadence with{Magazine=b.IncarnonAmmo??(int)charges,Reload=0};
            if(RangedIncarnons.Get(w.Id) is {SpoolStep:<0} reverseSpool)cadence=cadence with{SpoolStart=1,SpoolStep=reverseSpool.SpoolStep,SpoolFloor=reverseSpool.SpoolFloor};
            sums["AmmoEfficiency"]=0;sums["InfiniteMagazine"]=0;
        }
        if(!(w.Id==RangedPullWeapons.Scyotid&&b.AttackIndex==1)&&S(a,"ShotType") is not ("Hit-Scan" or "Hitscan" or "Projectile")&&RangedDeployments.Get(w.Id)?.FireModes.Contains(b.AttackIndex)!=true&&RangedAirbursts.Get(w.Id,b.AttackIndex) is null)issues.Add("파생 공격 단독으로는 발사 주기를 계산하지 않습니다. 원래 발사 모드를 선택하고 교전 분석에서 후속 공격을 함께 계산하세요.");
        if(RangedDeployments.Get(w.Id) is {} deployment&&deployment.FireModes.Contains(b.AttackIndex)&&!(RangedPentaAugments.Applies(w)&&(condition.ManualGrenades||V("PentaNapalm")>0)))
        {
            var speed=N(a,"ShotSpeed")??0;if(speed<=0)issues.Add("투척 속도 원문 확인 필요");
            else cadence=cadence with{ShotDelay=Math.Max(cadence.ShotDelay,b.Distance/(speed*(1+V("ProjectileSpeed")))+condition.DetonationDelay)};
        }
        if(RangedPassives.Get(w.Id)?.Kind=="Cedo"&&b.AttackIndex==1)
        {
            if(condition.GlaiveReturnSeconds is {} returnTime)cadence=cadence with{ShotDelay=Math.Max(cadence.ShotDelay,returnTime)};
        }
        var field=RangedFields.Get(w.Id);
        if(field is not null)
        {
            if(field.RequireDuration&&condition.FieldEndSeconds is null)issues.Add("팍스 구름 지속 시간: 본문 4초 / 모듈 5초 중 계산 기준을 선택하세요.");else _=RangedFields.Pulses(field,condition,V("FireRate"));
            if(field.RepeatAfterReload&&field.Parents.Contains(b.AttackIndex))cadence=cadence with{ShotDelay=cadence.Reload,Charge=0};
            else if(field.Single&&field.Parents.Contains(b.AttackIndex))cadence=cadence with{ShotDelay=Math.Max(cadence.ShotDelay,field.SpawnDelay+RangedFields.End(field,condition)+(b.Distance==0?0:b.Distance/((N(a,"ShotSpeed")??1)*projectileScale)))};
        }
        if(RangedReloadGrenades.Get(w.Id) is not null)cadence=cadence with{Reload=RangedReloadGrenades.Reload(V("ReloadSpeed"),b.AttackIndex==0||b.AttackIndex>=3)};
        if(RangedAirbursts.Get(w.Id,b.AttackIndex) is {} airburst)cadence=cadence with{ShotDelay=Math.Max(cadence.ShotDelay,RangedAirbursts.Fuse(airburst,condition))};
        if(RangedReturningBlades.Get(w.Id) is not null&&b.AttackIndex==1)issues.Add("복귀 칼날은 일반 사격의 재장전 사건에서 계산합니다.");
        if(RangedMagazineCharge.Applies(w,b.AttackIndex))cadence=cadence with{Charge=cadence.Charge*chargedRounds,AmmoCost=chargedRounds};
        if(RangedBurstAugments.IsFrenzy(w,b.AttackIndex))
        {
            if(V("FrenzyRate")<=0)throw new ArgumentException("와일드 프렌지를 장착해야 이 사격을 사용할 수 있습니다.");
            var fixedRate=V("FrenzyRate");cadence=cadence with{Rate=fixedRate,ShotDelay=1/fixedRate,BurstDelay=1/fixedRate};
        }
        if(RangedSimulor.Applies(w)&&b.AttackIndex>0)issues.Add("구체 파생 피해는 구체 발사를 선택하면 교전 사건으로 함께 계산합니다.");
        var rate=cadence.Rate;var reload=cadence.Reload;var magazine=cadence.Magazine;
        if(condition.InfiniteAmmoSeconds>0&&incarnonCharges is null)sums["InfiniteMagazine"]=1;
        RangedBalance4413.Apply(sums);
        var shots=Math.Floor(magazine/cadence.AmmoCost);var cycle=cadence.MagazineCycle();
        var fireCycle=cadence.BurstCount*cadence.Charge+cadence.Delay(0)+(cadence.BurstCount-1)*cadence.BurstDelay;
        double? burst=issues.Count==0&&fireCycle>0?shot*cadence.BurstCount/fireCycle:null;
        double? sustained=incarnonCharges is null&&issues.Count==0&&cycle>0?shot*shots/cycle:null;
        if((cadence.Beam||RangedAmmo.Efficiency(sums)>0)&&issues.Count==0&&incarnonCharges is null)
        {
            var horizon=60d;var firing=new RangedFireState(cadence);double accumulated=0;
            while(firing.Next<horizon){var sample=firing.Consume(cadence,RangedAmmo.Efficiency(sums));accumulated+=shot*sample.BeamScale;firing.Advance(cadence,RangedAmmo.Efficiency(sums));}
            sustained=accumulated/horizon;
        }
        string[] scope=["선택한 공격 구성요소 · 모든 탄환 명중. 전투 시 피해는 위키의 1/32 양자화를 적용합니다.","발사 시간은 점사 내부 간격·충전·발사 가속·재장전을 각각 계산합니다. 빔·탄약 효율 적용 시 지속 DPS는 60초간 피해 상승·탄약 소비·재장전을 포함합니다.","현재 조건의 처치·약점·아케인 중첩은 고정 상태 수치입니다. 교전에서는 발동·만료 사건으로 갱신합니다.","무기 전체의 파생 공격과 상태 피해·적 방어층 변화는 교전 분석 결과에서 구분합니다."];
        scope=[..scope,..condition.ApproximationNotes??[]];
        if(RangedPhantasma.Applies(w)&&b.AttackIndex>0){sustained=null;scope=[..scope,$"충전 {RangedPhantasma.Rounds(w,condition,sums)}발 · 초당 11발 · 본탄당 유도 자탄 5개 · 피해 기준 {condition.PhantasmaReferenceAmmo??2:0.###}발"]; }
        if(RangedAzima.Applies(w)&&b.AttackIndex==2){sustained=null;burst=null;scope=[..scope,"아지마 투척: 탄창 전체를 설치합니다. 4방향 회전·실제 명중·만료 폭발은 교전에서 계산하며 미기재 시간과 범위는 명시한 시나리오입니다."];}
        if(RangedResidual.Is(b.Kitgun?.Arcane))scope=[..scope,"레지듀얼: 확인된 생성 확률·지속·전체 2개 제한을 적용합니다. 미기재 피해 배율·공격 시점·처치 귀속은 지정한 시나리오입니다."];
        if(RangedMagazineCharge.Applies(w,b.AttackIndex)&&chargedRounds>1){sustained=null;scope=[..scope,$"충전 로켓 {chargedRounds}개 · 탄약 {chargedRounds}발 소비. 탄창 잔량은 마지막 충전에 반영합니다. 모든 로켓이 같은 대상에 명중하는 배치입니다."];}
        if(RangedAirbursts.Get(w.Id,b.AttackIndex) is {} airburstScope)scope=[..scope,$"발사 후 {RangedAirbursts.Fuse(airburstScope,condition):0.###}초에 공중 기폭합니다. 기폭 전 적·지형 충돌이 없는 경로이며, 폭발 중심과 대상 거리는 별도로 설정합니다."];
        if(RangedDeployments.Get(w.Id) is not null)scope=[..scope,"수동 기폭: 한 번 투척하고 대상 도착 후 설정한 시간에 기폭한 뒤 다음 투척·재장전을 진행합니다. 여러 발을 미리 설치하는 패턴은 이 결과와 다릅니다."];
        if(cadence.Trigger=="Duplex")scope=[..scope,$"듀플렉스: 누른 뒤 {condition.DuplexReleaseDelay:N2}초 후 뗍니다. 다음 누름은 위키의 연사 간격과 이전 뗌 시각을 모두 따릅니다."];
        if(altRule is not null&&altRule.Alternate.Contains(b.AttackIndex)){sustained=null;scope=[..scope,$"보조 공격 게이지 {RangedAlternates.Charges(altRule,condition.AlternateCharge)} / {altRule.Capacity} · 최소 {altRule.Required} 필요 · 사용 시 전부 소비"];}
        if(RangedPassives.Get(w.Id)?.Kind=="Grimoire"&&b.AttackIndex==2){sustained=null;scope=[..scope,"방전 1회 수치입니다. 구체는 6초간 초당 방전 후 폭발하며, 재사용 게이지·이동·연쇄는 교전 분석에서 계산합니다."];}
        if(RangedCollisions.Get(w.Id,b.AttackIndex) is {} collisionRule)scope=[..scope,$"추가 충돌 {condition.Collisions?.Length??0}회 · 지정한 시각/대상 경로만 계산합니다. 지형 자동 추적이나 최대 도탄 횟수의 전부 명중을 가정하지 않습니다."];
        if(RangedPassives.Get(w.Id)?.Kind=="Cedo"&&b.AttackIndex==1){sustained=null;scope=[..scope,"글레이브 폭발: 피해 가중 상태 1개 + 기본 원소 무작위 상태 1개. 회수 시간은 위키에서 약 5초로만 기재되어 반복 사용 시 설정한 시간을 사용합니다."];}
        if(RangedClusters.Get(w.Id) is {} clusters&&b.AttackIndex==clusters.Parent)scope=[..scope,$"주탄 1개마다 자탄 {clusters.Count}개 생성 · 교전은 지정한 착탄 경로만 사용합니다. 경로에서 제외한 자탄은 적 무리에 맞지 않는 배치입니다."];
        if(RangedReloadGrenades.Get(w.Id) is not null&&b.AttackIndex>0){sustained=null;burst=null;scope=[..scope,"재장전 유탄 한 발의 구성요소입니다. 빔 사격의 재장전 사건에서 유탄을 발사합니다."];}
        if(field is not null&&field.Parents.Contains(b.AttackIndex)){sustained=null;scope=[..scope,$"지속 장판: 첫 틱은 생성 후 주기의 {condition.FieldFirstPulse:P0} 지점이라는 설정을 사용합니다. 위키는 첫 틱 위상을 명시하지 않습니다."];}
        if(RangedPassives.Get(w.Id)?.Kind=="Onos"&&b.AttackIndex>=1){sustained=null;scope=[..scope,"오노스 충전 빔 + 완충 발사: 충전 시간과 다음 충전까지의 대기는 설정한 시간 가정입니다. 모드 적용 후 실제 시간으로 입력합니다. 완충 공격을 단독 선택하면 충전 중 피해를 포함하지 않습니다."];}
        if(cadence.Trigger.Contains("Charge")&&condition.ChargeCycleSeconds is not null&&N(a,"ChargeTime") is null)scope=[..scope,"충전·다음 충전 대기 시간은 원문 미기재로 입력한 모드 적용 후 시나리오 시간입니다. 공격 피해·부착 후 폭발 지연은 위키 수치를 사용합니다."];
        if(V("AdhesiveChance")>0)scope=[..scope,"애드히시브 블래스트: 탄환마다 부착 확률을 계산합니다. 일반 폭발은 부착 후 4초, 자르는 3초, 톤코어는 발사 후 약 7초 기준입니다. 펜타는 수동 기폭이며 부착 후 후속 도탄을 멈춥니다. 테더는 기존 지형 부착을 유지합니다. 추가 피해·DoT는 적용하지 않습니다."];
        if(V("VoltageTrapDuration")>0)scope=[..scope,"볼티지 시퀀스: 비행 적의 무기 처치에서만 함정이 낙하합니다. 반경 4m, 초당 60틱, 고정 6–7 Electric·상확 100%이며 모드로 피해를 증가시키지 않습니다. 낙하 시간·최대 대상 수·피해 범위 내 값·첫 틱 위상은 지정한 시나리오입니다. 함정·시퀀스 피해 처치는 새 함정을 만들지 않습니다."];
        if(RangedZymos.Applies(w))scope=[..scope,"자이모스: 몸통 직격의 즉시 폭발과 약점 직격의 1.1초 후 폭발·포자 6개를 분리합니다. 포자 직격 상확 0%, 7m 초과 강제 Impact, 포자 폭발은 기본 피해 모드를 제외하며 원소·팩션은 적용합니다. 포자 치피 모드·고정 치확은 이중 적용합니다. 굴착 피해·포자 경로 및 폭발 버그는 명시된 시나리오입니다."];
        if(RangedHema.Applies(w))scope=[..scope,"헤마는 예비 탄약 대신 체력을 소비합니다. 교전의 재장전 소비 시점은 지정한 진행률을 사용합니다. 네크로패직 비거는 20 체력 단위의 치확·치피 가산, 최대 +360%이며 다음 재장전에서 새 소비량으로 갱신합니다.",condition.HemaHost?"호스트 시나리오 · 네크로패직 비거 효과 적용":"클라이언트 버그 시나리오 · 네크로패직 비거의 치확·치피 증가 미적용"];
        if(V("ExternalDaggerArmorStrip")>0)scope=[..scope,"아말감 아르고나크: 총기 관통 거리만 이 페이지의 공격에 적용합니다. 단검 방어력 제거는 근접 페이지의 외부 주무기 설정으로 계산합니다. 조준 중 적 표시 범위 100m는 적 AI·가림 시각화 범위 밖입니다."];
        if(RangedQuanta.Applies(w)&&b.AttackIndex>=1)scope=[..scope,"콴타 큐브 · 직격/자연 만료 150 Blast·0.5m, 플레이어 피해로 기폭 600 Blast·6m. 주변 큐브는 연쇄로 강화 기폭합니다. 멀티샷 및 관통 후 이동은 지정 경로를 사용하며 미지의 충돌 물리를 추측하지 않습니다."];
        if(V("DisarmChance")>0)scope=[..scope,"디스아밍 퓨리티 · 보조 사격과 폭발 상태의 무장 해제 확률, 오버가드 차단 및 퓨리티를 연결합니다. 무장 해제는 전투 기록에 표시하며 적의 공격 AI는 시뮬레이션하지 않습니다. 가능 여부가 확인되지 않은 적은 별도 기록합니다."];
        if(V("BuckshotFalloff")>0)scope=[..scope,"오버프레셔드 라운즈 · 일반 사격 처치는 다음 벅샷의 감쇠 거리를 강화하며 벅샷 발사 때 소비합니다. 모든 펠릿 명중 배치이며 원문에 수치가 없는 산탄 퍼짐 감소로 추가 명중률을 만들지 않습니다. 탄창 회복의 소수 처리·예비 탄약 사용은 선택한 기준을 따릅니다."];
        if(V("OscillatingStatusChance")>0)scope=[..scope,$"플럭스 오버드라이브 · 가산 상확 후 별도 ×(1 + {V("OscillatingStatusChance"):0.###} × 변동항 위치). 현재는 {(condition.FluxOscillation is {} fluxPhase?fluxPhase.ToString("P0"):"미지정")} 고정 시나리오이며 무작위 평균·시간 변화의 기대값이 아닙니다."];
        if(V("HigasaForcedStatus")>0)scope=[..scope,"히가사 서레이션 · 기본 피해 가산은 모든 공격에 적용합니다. 보조 공격의 강제 상태는 선택한 결과의 시나리오이며 무작위 분포를 추정하지 않습니다."];
        if(V("ExternalNikanaLifeSteal")>0)scope=[..scope,"아말감 다이큐: 약점 배율과 예약 화살 회수를 적용합니다. 니카나 직접 흡혈은 근접 페이지의 외부 주무기 설정에서 계산하며 총기 흡혈로 적용하지 않습니다."];
        if(V("ExternalShieldBlockCombo")>0)scope=[..scope,"아말감 자블락: 탄창 증가와 잔탄에 따른 투척 피해에 적용합니다. 검방패 막기 콤보 증가는 총기 공격의 콤보가 아니므로 이 교전의 수치에 합산하지 않습니다."];
        if(V("PetrifyHits")>0)scope=[..scope,"메타모픽 매거진: 대상별 20회 명중에 석화 5초. 오버가드가 남아 있으면 차단합니다. 아틀라스 페트리파이의 피해 증폭·추가 CO 상태는 적용하지 않으며 적 공격 AI는 이 계산의 범위가 아닙니다."];
        if(V("FinisherStunRange")>0)scope=[..scope,$"디지잉 라운즈: 피해가 발생할 때 {V("FinisherStunRange"):0.#}m 안의 오버가드가 없는 적을 기절시키고 마무리 공격을 허용합니다. 총기 교전 결과에는 별도의 근접 마무리 공격 피해를 더하지 않습니다."];
        if(V("DocumentedInactive")>0)scope=[..scope,"선택한 진화는 위키에서 현재 작동하지 않는 것으로 명시되어 효과를 적용하지 않습니다."];
        if(V("ThunderboltChance")>0||V("ConcealedExplosionChance")>0)scope=[..scope,"고정 폭발 모드 · 250 Blast, 반경 10m와 반경 모드, 팩션 브라켓만 적용합니다. 교전 계산에만 확률 폭발을 포함합니다. 컨실드는 직접 명중 시 폭발하지 않는 위키 버그와 폭발 중심 시야를 반영합니다. 썬더볼트 직격 처리 기준은 별도 시나리오이며 소멸한 화살의 후속 사건은 취소합니다."];
        if(V("HealthOrbShotBonus")>0||V("EnergyOrbShotBonus")>0)scope=[..scope,"오브 사격 · 예약한 발사·공격이 실제로 오브에 닿을 때 회복합니다. 투사체 1개는 오브에서 멈추며 남은 멀티샷만 기존 대상 경로를 계속합니다. 광역 공격은 탄환을 소모하지 않습니다. 히트스캔의 통과 여부는 위키에 무기별 수치가 없어 별도 기준을 지정합니다. 워프레임의 오브 관련 어빌리티·모드 발동은 포함하지 않습니다."];
        if((condition.InfestedOrbs??[]).Length>0)scope=[..scope,$"뮤탈리스트 콴타 통과 구체: 구체당 최종 피해 ×0.66·고정 치확 +25%, 치피 모드 +25%는 한 번. 전기는 각 구체의 1/2/3중첩에 따라 100/366/500%를 합산합니다. 현재 최종 피해 계수 {V("OrbFinalMultiplier"):0.####}. 보조 히트스캔·빔의 전기는 결합하지 않습니다."];
        if(RangedOrdinarySimulor.Applies(w)&&!RangedSingularity.Equipped(b))scope=[..scope,"일반 시뮬러: 독립 멀티샷 구체는 발사점에서 합체합니다. 합체마다 20초 수명 갱신, 4중첩에서 특이점, 활성 구체 4개·특이점 3개 제한. 미합체 수명·중첩별 출력·틱은 명시한 시나리오입니다. 미합체 폭발은 모듈의 200 Magnetic·5m이며 본문의 4m 설명과 차이가 있습니다."];
        if(condition.SimulorOrbs is {Length:>0})scope=[..scope,"시뮬러 통과 구체: 1/2/3/4중첩마다 100/300/400/500% 별도 전기. 다른 원소와 결합하지 않습니다. 구체당 최종 ×0.66·고정 치확 +25%p, 치피 모드 +25%는 감염 구체와 함께 한 번 적용합니다."];
        if(V("NeutralizingChance")>0)scope=[..scope,"뉴트럴라이징 저스티스: 미터의 일반·충전 칼날마다 생성기 영구 파괴 확률. 파괴한 칼날 외의 멀티샷은 통과하며, 실패하면 모두 튕깁니다. 테라 실드 오스프리·아이솔레이터 벌사에는 이 파괴 효과가 없습니다."];
        if(condition.Nullifier is {Kind:not "None"})scope=[..scope,"널리파이어 막: 프레임별 비치명 피해 합산 후 100~400 제한, 자성 포함 시 3배. 현재 반경의 6% × 제한 피해/100만큼 축소합니다. 붕괴 경계·재생은 지정 시나리오이며 7,200 HP를 확정값으로 사용하지 않습니다."];
        if(V("CannonadeChance")>0)scope=[..scope,"구형 캐노네이드: 현재 획득 불가, 기존 보유 모드. 명중별 최대 30%·250 Blast·10m 거리 감쇠. 감쇠 곡선과 피해 브라켓은 지정한 시나리오이며 고정 비치명·추가 상태 없음으로 계산합니다."];
        if(V("SpontaneousChance")>0)scope=[..scope,"스폰테이니어스 싱귤래리티: +90% 피해는 선택한 브라켓 시나리오, 명중 즉시 기폭, 18%로 5초 특이점 생성. 특이점이 생성된 구체는 폭발하지 않으며 도탄·합체는 제거됩니다. 특이점의 미기재 피해·끌어오기 반경과 멀티샷 방식은 별도 시나리오입니다. 기본 활성 구체 4개 제한을 유지하는 시나리오를 사용합니다."];
        if(RangedTendrils.Applies(w))scope=[..scope,"버미스플라이서: 표적 획득 중에도 탄약을 소비합니다. 획득 지연과 촉수 배분은 지정한 시나리오입니다. 주무기는 직접 관통에 맞은 적을 제외하고 가장 먼 유효 대상에서만 70% 촉수 3개가 분기합니다. 보조는 5개 촉수를 각각 계산하며 자동 표적 촉수는 약점을 조준하지 않습니다."];
        var result=new RangedResult(damage,cc,cd,sc,ms,shot,rate,reload,magazine,burst,sustained,drain,(int)(N(w.Raw,"MaxRank")??30)*(b.Catalyst?2:1),issues.ToArray(),scope,cadence,sums,baseDamage,baseFactor,factionFactor,co.Mode=="None"?1:1+coBonus,arsenal,statusFactor);
        if(N(a,"CarrierProjectile")==1){sums["CarrierProjectile"]=1;result=result with{BurstDps=null,SustainedDps=null,Scope=[..result.Scope,"직격 피해 0인 운반체입니다. 충돌 폭발·자탄은 교전 결과에 따로 합산합니다."]};}
        if(RangedPaxSeeker.Is(a))result=result with{BurstDps=null,SustainedDps=null,TimingIssues=["팍스 시커는 약점 처치로 생성합니다. 기본 공격의 교전 결과에서 계산하세요."],Scope=[..result.Scope,"팍스 시커: 50% 베기·50% 관통과 원소 모드, 강제 Impact. 그립 보너스는 기본 피해·멀티샷 브라켓 뒤에 더합니다. 치확·치피 모드 보너스는 두 번, CO는 최종 곱연산입니다. 멀티샷은 피해의 정수 확률 배수이며 유도탄 개수는 아케인 랭크 + 1입니다."]};
        var ammo=new RangedAmmo(w,result,condition.ReserveAmmo,condition.ReplenishReserve);sums["MaximumReserveAmmo"]=ammo.Maximum;
        if(RangedVectis.Applies(w)&&b.AttackIndex>0)result=result with{Scope=[..result.Scope,"벡티스 인카논 본문 우선: 직접 150, 부착 25, 약점 폭발 150×2. 부착 반경 약 0.5m는 근삿값이며, 원문 모듈의 일반판 5 피해/0.15m 및 폭발 감쇠 0%와 상충합니다.",condition.VectisHeadshotAoeBonus?"위키의 간헐적인 약점 폭발 보너스 적용 시나리오 · 최초 부착 대상만 적용":"위키의 간헐적인 약점 폭발 보너스는 적용하지 않은 시나리오"]};
        if(RangedStug.Launch(w,b.AttackIndex))result=result with{Damage=[],ExpectedShot=0,BurstDps=null,SustainedDps=null,Scope=[..result.Scope,"스터그 구체 발사: 직격 자체 피해 없이 부착 주기 피해·중첩·폭발을 교전에서 계산합니다. 같은 지점 중첩은 최초 부착 시 1회와 이후 초당 1회 피해를 주며 추가 중첩은 주기를 초기화하지 않는 원문 해석을 사용합니다."]};
        if(RangedOrdinarySimulor.Applies(w)&&b.AttackIndex==0&&!RangedSingularity.Equipped(b))result=result with{BurstDps=null,SustainedDps=null};
        if(RangedSimulor.Applies(w)&&b.AttackIndex==0&&!RangedSingularity.Equipped(b))result=result with{BurstDps=null,SustainedDps=null,Scope=[..result.Scope,"구체 발사 자체는 피해가 없습니다. 멀티샷은 구체 값이며, 합체·주기 피해·만료와 수동 기폭은 교전 분석에서 계산합니다."]};
        return RangedNapalm.Calculate(b,w,RangedInfestedOrbs.Calculate(b,w,RangedPentaAugments.Calculate(b,w,RangedPullWeapons.Calculate(b,w,RangedVileDischarge.Calculate(b,w,RangedAugmentFields.Calculate(b,w,result))))));
    }
}
