using System.Text.Json;

namespace TennoForge.Core;

/// <summary>Per-attack evaluation with explicit, saved conditions and source-backed rules.</summary>
public static class MeleeEngine
{
    public const string RulesVersion="melee-conditional-0.7.0";
    private static readonly DamageType[] Basic=[DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin];
    private static readonly Lazy<MeleeRuleCatalog> Rules=new(MeleeRuleCatalog.Load);
    public static AttackRule AttackPolicy(string weaponId,string attack)
    {
        var profile=Rules.Value.Weapon(weaponId);
        if(ExaltedRules.WavePolicy(weaponId,attack) is {} wave)return wave;
        if(ShieldThrowMechanic.Policy(profile,attack) is {} shield)return shield;
        if(WeaponStanceMechanic.Policy(profile,attack) is {} special)return special;
        return profile.Attacks.SingleOrDefault(a=>a.Key==attack)
            ??(attack=="Heavy Attack"?MeleeClassRules.HeavyPolicy(profile):null)
            ??throw new ArgumentException("공격별 적용 규칙이 없습니다.");
    }
    public static string[] Attacks(string id)
    {
        var profile=Rules.Value.Weapon(id);var raw=profile.Raw;
        var names=raw.GetProperty("Attacks").EnumerateArray().Select(x=>x.Text("AttackName")).ToList();
        if(MeleeClassRules.HeavyDamage(profile)>0)names.Insert(1,"Heavy Attack");
        if(raw.Number("SlideAttack")>0&&ExaltedRules.Find(id) is not {Pseudo:true} and not {Family:ExaltedFamily.Diwata})names.Insert(1,"Slide Attack");
        return names.Concat(ExaltedRules.AdditionalAttacks(id)).Distinct().ToArray();
    }
    internal static void Range(double value,double min,double max,string label)
    {if(!double.IsFinite(value)||value<min||value>max)throw new ArgumentException($"{label}: {min}~{max} 범위가 필요합니다.");}
    public static Calculation Calculate(Catalog catalog,Build build)=>CalculateAttack(catalog,build);
    internal static Calculation CalculateAttack(Catalog catalog,Build build,StanceHit? stanceHit=null,double coFactor=1,bool groundFinisher=false,bool slideAttack=false,bool useInitialComboFloor=true,MeleeAttackComponent? component=null,double sourceMultiplier=1)
    {
        if(build.SchemaVersion!=5||build.Melee is null)throw new ArgumentException("확장 근접 계산에는 버전 5의 조건이 필요합니다.");
        if(build.Arcanes?.PlayerArmor is {} playerArmor)
        {
            Range(playerArmor,0,1e7,"워프레임 방어력");
        }
        build=HallOfMirrorsRules.Prepare(catalog,build);
        if(build.Melee?.ExternalEquipment?.AtlasRubble is {} rubbleSetup){AtlasRubbleState.Validate(rubbleSetup);build=build with {Melee=build.Melee with {Conditions=new(build.Melee.Conditions??[]){["RubbleHeap"]=rubbleSetup.Armor>1400?1:0}}};}
        build=WarframeCombatArcanes.ApplyConditions(build);
        if(build.Melee!.RulesVersion!=RulesVersion)throw new ArgumentException("저장된 근접 계산 규칙 버전이 다릅니다. 해당 원본 규칙으로 재검토해야 합니다.");
        if(build.Stance is not null)throw new ArgumentException("스탠스 계산은 StanceEngine을 사용해야 합니다.");
        if(build.DataVersion!=catalog.Version)throw new ArgumentException("빌드 데이터 버전 불일치");
        if(build.Slots is null||build.Slots.Length!=8)throw new ArgumentException("모드 슬롯은 8개여야 합니다.");
        if(build.Capacity is null)throw new ArgumentException("수용량 설정이 없습니다.");
        CapacityEngine.Calculate(catalog,build);
        var weapon=catalog.Weapons.SingleOrDefault(w=>w.Id==build.WeaponId)??throw new ArgumentException("계산할 근접 무기가 없습니다.");
        var profile=Rules.Value.Weapon(weapon.Id);var raw=profile.Raw;var setup=build.Melee;var scenario=build.Scenario??throw new ArgumentException("계산 조건 누락");
        var exalted=ExaltedRules.Find(weapon.Id);if(exalted is not null)ExaltedRules.Validate(build,exalted);
        else if(setup.AbilityAugment is not null)throw new ArgumentException("어빌리티 개조 모드는 해당 익절티드 무기에서만 설정할 수 있습니다.");
        var enemy=scenario.Enemy??new Target();
        var enemyProfile=Rules.Value.EnemyProfiles.SingleOrDefault(p=>p.Id==setup.Faction)??throw new ArgumentException("대상 팩션 식별자 오류");
        Range(scenario.Combo,1,weapon.MaxCombo,"콤보 배수");Range(scenario.ExistingStatusTypes,0,18,"상태 종류");
        Range(scenario.MotionMultiplier,.01,100,"타격 배율");Range(scenario.BaseInterval,.05,60,"반복 간격");
        Range(enemy.Health,1,1e12,"체력");Range(enemy.Armor,0,2700,"방어력");Range(setup.Distance,0,1000,"거리");Range(setup.TargetIndex,1,100,"타격 순서");
        Range(setup.Shields,0,1e12,"실드");Range(setup.Overguard,0,1e12,"오버가드");Range(setup.AbilityStrength,0,100,"어빌리티 위력");
        var conditions=setup.Conditions??[];
        if(setup.Finisher is not (null or "Ground" or "Front" or "Back" or "Stealth" or "Universal"))throw new ArgumentException("마무리 종류 오류");
        var finisher=groundFinisher||setup.Finisher is not null;
        var contextFinisher=setup.Finisher is "Front" or "Back" or "Stealth" or "Universal";
        foreach(var (key,value) in conditions)Range(value,key=="UniversalStatusDuration"?-100:0,key=="BlockedDamage"?1e12:100000,key);
        double C(string key)=>setup.Overguard>0&&key is "Lifted" or "Knockdown"?0:conditions.GetValueOrDefault(key);
        var statuses=setup.Statuses??[];
        foreach(var (type,count) in statuses){if(!Enum.IsDefined(type)||!DamageTypeRules.HasStatus(type))throw new ArgumentException("상태 유형 오류");Range(count,0,type is DamageType.Puncture or DamageType.Impact?5:type is DamageType.Void?1:type is DamageType.Heat or DamageType.Slash or DamageType.Toxin or DamageType.Electricity?10000:10,"대상 상태 중첩");}
        int St(DamageType type)=>statuses.GetValueOrDefault(type);
        var frozen=(St(DamageType.Cold)>=10||build.Arcanes?.Frozen==true)&&setup.Overguard==0;
        var statusTypes=TargetStatusRules.Count(build);
        var attack=component?.Raw??ExaltedRules.WaveRaw(profile,setup.Attack)??WeaponStanceMechanic.Raw(profile,setup.Attack,stanceHit)??raw.GetProperty("Attacks").EnumerateArray().FirstOrDefault(a=>a.Text("AttackName")==setup.Attack);
        var policy=component?.Policy??AttackPolicy(weapon.Id,setup.Attack);
        if(policy.Code=="VitricaGlass")attack=VitricaMechanic.OriginRaw(attack);
        if(policy.Incarnon&&!setup.Incarnon)throw new ArgumentException("인카논 변신 중에만 사용할 수 있는 공격입니다.");
        var normal=raw.GetProperty("Attacks").EnumerateArray().First(a=>a.Text("AttackName")==ExaltedRules.DefaultAttack(weapon.Id));
        var synthetic=setup.Attack is "Heavy Attack" or "Slide Attack";
        if(attack.ValueKind==JsonValueKind.Undefined&&!synthetic)throw new ArgumentException("무기 공격 종류가 일치하지 않습니다.");
        if(synthetic)attack=policy.Code=="GunbladeHeavy"?raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Ranged Attack"):normal;
        var heavy=policy.Heavy;
        var heavyBonuses=heavy&&(!policy.HeavyAnimationOnly||C("HeavyAnimation")>0);
        var slam=setup.Attack.Contains("Slam",StringComparison.Ordinal);var aoe=attack.Text("ShotType")=="AoE"||slam||policy.Code=="ZenistarAura"||exalted?.Family is ExaltedFamily.Landslide or ExaltedFamily.Whipclaw;
        if(setup.Tennokai&&!heavy)throw new ArgumentException("텐노카이는 강공격에 적용합니다.");
        var effects=new Dictionary<string,double>();var elements=new List<(DamageType Type,double Value)>();var notes=new List<string>();
        notes.AddRange(policy.Notes??[]);
        if(build.WeaponId==SanctiMagistarState.WeaponId)notes.Add("생티 마기스타: 무기를 든 동안 상태 저항20%. 충격·보이드·넘어짐·띄우기·래그돌·움찔·기절 제외, 마이크로웨이브 포함. 영구 흡혈과15m 공유는 교전 회복 결과에 표시합니다. "+SanctiMagistarState.Source);
        if(component is null)notes.AddRange(MeleeMechanicScope.AdditionalComponents(profile,setup));
        if(policy.Code=="VitricaGlass")notes.Add("유리화된 원래 대상의 폭발: 기본 상확 100%, 고정 추가 상확 허용. 주변 대상은 33%·물리/원소 모드 제외인 별도 성분입니다. "+VitricaMechanic.Source);
        if(component is null&&!(profile.Name=="Tak & Lug"&&stanceHit is not null)&&MeleeMechanicScope.CompoundApplies(profile,setup,finisher))
            foreach(var pending in profile.PendingMechanics??[])notes.Add("미반영: "+pending+". 선택한 단일 공격 성분과 구분되며 전체 기믹 DPS·TTK를 뜻하지 않습니다.");
        var buffs=setup.Buffs??new();buffs.Validate();
        if(buffs.Eclipse>0&&setup.ContagionBuffs?.Eclipse>0)throw new ArgumentException("이클립스는 공통 버프와 기존 컨테이전 전용 버프 중 한 곳에만 입력하세요.");
        var eclipse=setup.Hologram is null?buffs.Eclipse:0;
        if(buffs.Active)notes.Add("외부 버프: 실제 받은 증가율을 교전 구간 내 유지합니다. 로어는 팩션 피해와 가산·상태 피해에 두 번, 이클립스는 가산 컨오를 제외한 피해에 곱합니다. "+WeaponBuffs.RoarSource+" "+WeaponBuffs.EclipseSource);
        if(setup.Hologram is not null&&buffs.Eclipse>0)notes.Add("분신에는 자신의 이클립스가 적용되지 않습니다. 토탈 이클립스 공유 시뮬레이션은 다음 단계입니다.");
        var contributions=new List<EffectContribution>(buffs.Contributions().Where(c=>c.Effect!="Eclipse"||eclipse>0));
        void Add(string stat,double amount)=>effects[stat]=effects.GetValueOrDefault(stat)+amount;
        void AddEffects(Dictionary<string,double>? values,double multiplier)
        {
            if(values is null)return;
            foreach(var (key,value) in values)
                if(Enum.TryParse<DamageType>(key,out var type)&&!Basic.Contains(type)){elements.Add((type,value*multiplier));}
                else if(Enum.TryParse<DamageType>(key,out type)&&Basic.Contains(type))elements.Add((type,value*multiplier));
                else Add(key,value*multiplier);
        }
        foreach(var external in MeleeExternalRules.Effects(raw.Text("Class"),setup.ExternalEquipment)){Add(external.Effect,external.Amount);contributions.Add(external);}
        if(MetronomeRules.Opera(setup.ExternalEquipment?.Metronome)>0&&weapon.Id==WeaponStanceMechanic.QuassusPrime)notes.Add("미검증:콰서스 프라임 오페라 적용은 일반판의 명시 규칙과 같은 고유 공격 구조에서 추론했습니다. 프라임 본문에는 버프명이 재기재되어 있지 않습니다.");
        if(effects.GetValueOrDefault("NikanaLifeSteal")>0)notes.Add("아말감 다이큐:니카나 직접 피해/샤암 파동 흡혈 · 상태 틱 제외 · "+MeleeExternalRules.DaikyuSource);
        var allSlots=build.Slots.Where(s=>s is not null).Cast<ModSlot>().Concat(setup.Exilus is {} ex?[ex]:[]).ToArray();
        var ids=allSlots.Select(s=>s.ModId).ToHashSet();if(ids.Count!=allSlots.Length)throw new ArgumentException("같은 모드를 중복 장착할 수 없습니다.");
        var selected=allSlots.Select(s=>(Slot:s,Rule:Rules.Value.Mods.SingleOrDefault(m=>m.Id==s.ModId)??throw new ArgumentException("근접 모드 규칙이 없습니다."))).ToArray();
        var sacrificial=selected.Count(x=>x.Rule.SetEffect=="Sacrificial")==2?1.25:1;
        var gladiator=selected.Count(x=>x.Rule.SetEffect=="Gladiator")+Math.Clamp(C("ExternalGladiator"),0,3);
        var rivenElementPosition=0;
        foreach(var (slot,rule) in selected)
        {
            if(rule.Effects?.GetValueOrDefault("Riven")>0)rivenElementPosition=elements.Count;
            Range(slot.Rank,0,rule.MaxRank,rule.Name+" 랭크");
            if(rule.Unsupported.Length>0)throw new ArgumentException(rule.Name+": "+string.Join(" ",rule.Unsupported));
            if(rule.Incompatible.Any(id=>id!=rule.Id&&ids.Contains(id)))throw new ArgumentException(rule.Name+": 함께 장착할 수 없는 모드입니다.");
            if(rule.CompatibleWeapons.Length>0&&!rule.CompatibleWeapons.Contains(weapon.Id)&&!(exalted?.Family==ExaltedFamily.Clones&&rule.Id=="/Lotus/Upgrades/Mods/Melee/WeaponMeleeStealthLethalMod"))throw new ArgumentException(rule.Name+": 선택한 무기에 장착할 수 없는 전용 모드입니다.");
            var scale=(slot.Rank+1d)/(rule.MaxRank+1d)*(rule.SetEffect=="Sacrificial"?sacrificial:1);
            void Explain(Dictionary<string,double>? values,double multiplier)
            {if(values is not null)foreach(var (key,amount) in values)contributions.Add(new(rule.Id,catalog.Mods.Single(m=>m.Id==rule.Id).Name,slot.Rank,key,EffectBrackets.Name(key),amount*multiplier,rule.Source?.Url??""));}
            if(rule.RankEffects is {} ranks)AddEffects(ranks[slot.Rank],1);else AddEffects(rule.Effects,scale);
            Explain(rule.RankEffects is {} rankEffects?rankEffects[slot.Rank]:rule.Effects,rule.RankEffects is null?scale:1);
            if(rule.Element is {} element&&rule.ElementBonus!=0)elements.Add((element,rule.ElementBonus*scale));
            if(rule.Element is {} elementKey)Explain(new(){[elementKey.ToString()]=rule.ElementBonus},scale);
            if(rule.Conditional is not null)
            {
                var count=rule.Condition=="Lifted"?C("Lifted"):C(rule.Condition??"");Range(count,0,rule.Cap,rule.Name+" 중첩");
                var conditional=rule.RankConditional is {} rankConditional?rankConditional[slot.Rank]:rule.Conditional;
                var conditionalScale=rule.RankConditional is null?scale*count:count;
                AddEffects(conditional,conditionalScale);
                Explain(conditional,conditionalScale);
            }
            if(rule.Utility is {} utility)notes.Add(utility);
            if(rule.SetEffect is "Carnis" or "Jugulus" or "Saxum")notes.Add(rule.SetEffect+" 세트의 회피·처치 후 범위 효과는 단일 타격 수치와 별도입니다.");
        }
        double V(string key)=>effects.GetValueOrDefault(key);
        // Only weapon MOD bonuses are halved for contextual finishers.
        // Innate elements, evolutions, weapon passives and arcanes enter later.
        if(contextFinisher)
        {
            for(var i=0;i<elements.Count;i++)elements[i]=(elements[i].Type,elements[i].Value*.5);
            foreach(var key in new[]{"ImpactDamage","PunctureDamage","SlashDamage","AdaptiveElement"})if(effects.ContainsKey(key))effects[key]*=.5;
            for(var i=0;i<contributions.Count;i++)if(Enum.TryParse<DamageType>(contributions[i].Effect,out _)||contributions[i].Effect is "ImpactDamage" or "PunctureDamage" or "SlashDamage" or "AdaptiveElement")
                contributions[i]=contributions[i] with {Amount=contributions[i].Amount*.5,Bracket=contributions[i].Bracket+" · 마무리 50% 적용"};
        }
        var incarnon=IncarnonEngine.Apply(build,Add,notes);
        if(incarnon?.Code=="Okina"&&V("BaseDamage")!=0)
        {
            if(policy.Code is not ("OkinaDagger" or "OkinaExplosion")){Add("BaseDamage",-V("BaseDamage"));Add("CoExcludedBase",-V("CoExcludedBase"));}
            notes.Add("오키나 진화II의 기본 피해는 위키 버그 기록에 따라 일반 칼날에는 적용하지 않고 유령 단검에 적용합니다. "+OkinaDaggerState.Source);
        }
        var ceramicProjectile=incarnon?.Code=="CeramicDagger"&&policy.Code=="CeramicProjectile";
        if(ceramicProjectile&&aoe)
        {
            var excluded=incarnon!.Perks.Where(p=>(setup.Evolutions??[]).Contains(p.Id)&&p.Condition=="FirstQuickMelee").Sum(p=>p.Conditional.GetValueOrDefault("BaseCriticalDamage")*C("FirstQuickMelee"));
            Add("BaseCriticalDamage",-excluded);
            if(excluded>0)notes.Add("레드 라이트 핸드는 유령 단검의 폭발에 적용되지 않습니다. https://wiki.warframe.com/w/Ceramic_Dagger_Incarnon_Genesis?oldid=2773637");
        }
        if(V("ForcedPunctureStacks")>0&&setup.Overguard==0)
        {
            // Ruvox Permanent Perforation: the target carries five Puncture
            // stacks while impaled. This is a target-state change before CO,
            // critical vulnerability and other status-dependent effects.
            statuses=new(statuses){[DamageType.Puncture]=Math.Max(St(DamageType.Puncture),(int)V("ForcedPunctureStacks"))};
            setup=setup with {Statuses=statuses};build=build with {Melee=setup};statusTypes=TargetStatusRules.Count(build);
        }
        if(build.Riven is {} riven)
        {
            RivenValues.Validate(riven);
            if(selected.Count(x=>x.Rule.Effects?.GetValueOrDefault("Riven")>0)!=1||!ids.Contains(RivenValues.ModId(weapon.Id)))throw new ArgumentException("무기에 맞는 리벤을 하나만 장착하세요.");
            if(riven.WeaponId!=weapon.Id||V("Riven")==0)throw new ArgumentException("리벤의 대상 무기와 장착 슬롯이 일치해야 합니다.");
            if(riven.Rank is <0 or >8||riven.Traits.Length is <2 or >4||riven.Traits.Count(x=>x.Negative)>1||riven.Traits.Select(x=>x.Stat).Distinct().Count()!=riven.Traits.Length)throw new ArgumentException("리벤 옵션 구성 오류");
            var rivenElements=new List<(DamageType Type,double Value)>();
            foreach(var trait in riven.Traits)
            {
                Range(Math.Abs(trait.Value),0,10000,"리벤 표시 수치");
                if(selected.Single(x=>x.Rule.Effects?.GetValueOrDefault("Riven")>0).Slot.Rank!=riven.Rank)throw new ArgumentException("리벤 슬롯 랭크와 입력 랭크가 다릅니다.");
                var value=Math.Abs(trait.Value)*(trait.Negative?-1:1);
                var flat=trait.Stat is RivenStat.Range or RivenStat.ComboDuration or RivenStat.InitialCombo;
                var amount=value/(flat?1:100);var key=trait.Stat==RivenStat.ParryAngle?"BlockAnglePercent":trait.Stat.ToString();
                if(contextFinisher&&(Enum.TryParse<DamageType>(key,out _)||key is "ImpactDamage" or "PunctureDamage" or "SlashDamage"))amount*=.5;
                if(Enum.TryParse<DamageType>(key,out var element))rivenElements.Add((element,amount));else Add(key,amount);
                contributions.Add(new(RivenValues.ModId(weapon.Id),"리벤 · "+RivenEngine.Label(trait.Stat),riven.Rank,key,EffectBrackets.Name(key),amount,RivenValues.Source));
            }
            // Wiki Riven Mods: elemental traits apply bottom-up, at this mod's slot position.
            rivenElements.Reverse();elements.InsertRange(rivenElementPosition,rivenElements);
        }
        else if(V("Riven")>0)throw new ArgumentException("장착한 리벤의 실제 표시 옵션을 입력하세요.");
        if(component?.NoWeaponStatusDuration==true){effects["StatusDuration"]=0;effects["SlashDuration"]=0;}
        // Lavos passive rev.2793587 / Prismatic Gem rev.2815760 explicitly
        // place this bonus additively with status-duration mods. Careen uses
        // the same external bonus, but excludes weapon-duration mods.
        Range(C("UniversalStatusDuration"),-100,1000,"외부 상태 지속시간 보너스 (%)");
        Add("StatusDuration",C("UniversalStatusDuration")/100);
        foreach(var passive in profile.Passives)
        {
            var count=passive.Key=="Always"?1:C(passive.Key);Range(count,0,passive.Maximum,passive.Label);
            if(passive.Attack.Length==0||setup.Attack.Contains(passive.Attack,StringComparison.Ordinal))AddEffects(passive.Effects,count);
        }
        if(profile.Name=="Arca Titron"&&C("SlamCapacitor")>0&&slam)
            notes.Add("슬램 캐패시터: 아르카 티트론 무기 본문의 명시적 규칙에 따라 프레셔 포인트와 가산, 사이즈믹 웨이브와 곱연산. 일반 Melee 문서의 슬램 피해 목록만으로 같은 가산 묶음으로 합치지 않습니다. "+profile.Source?.Url);
        if(profile.Code=="AckBrunt")
        {_=new AckBruntElementState(conditions);foreach(var type in Basic){var count=C("Block"+type);if(count>0)elements.Add((type,count*.175));}}
        if(profile.Code=="Amanata")
        {
            var roll=C("Roll");Range(roll,0,6,"아마나타 주사위");
            if(roll!=Math.Floor(roll))throw new ArgumentException("주사위 눈은 정수입니다.");
            if(roll is 1 or 6)Add("FlatCritical",.4);if(roll is 2 or 6)Add("FlatStatus",.3);
            if(roll is 3 or 6)Add("StatusFinalMultiplier",.3);if(roll is 4 or 6)Add("Range",1);if(roll is 5 or 6)Add("AttackSpeed",.2);
        }
        if(profile.Code=="Galariak")Add("FlatCritical",.1*statuses.Count(p=>p.Value>0&&p.Key!=DamageType.Blast));
        if(build.WeaponId==VinquibusState.WeaponId)
        {
            Range(C("VinquibusPrecision"),0,16,"빈퀴부스 정밀 충전");
            Range(C("VinquibusFerocity"),0,16,"빈퀴부스 맹렬 충전");if(C("VinquibusFerocity")!=Math.Truncate(C("VinquibusFerocity")))throw new ArgumentException("맹렬 충전은 정수입니다.");
            if(C("VinquibusPrecision")!=Math.Truncate(C("VinquibusPrecision")))throw new ArgumentException("정밀 충전은 정수입니다.");
            if(C("VinquibusPrecision")>0){Add("Damage",2.5);notes.Add("미검증: 빈퀴부스 정밀+250%는 기본 피해 가산으로 가정한 수치입니다. 위키·공식 설명에 브라켓이 명시되지 않아 확정된 가산 규칙과 구분합니다. "+VinquibusState.Source);}
            notes.Add("빈퀴부스 정밀: 충전이 남은 공격에 근접 피해 +250%. 충전 수는 피해 배수가 아니라 강화할 공격 횟수입니다. "+VinquibusState.Source);
        }
        if(profile.Code=="Paracesis")Add("VsSentient",.06*Math.Max(0,build.Capacity.WeaponRank-30));
        MeleeArcanes.Apply(build,heavy,aoe,frozen,statusTypes,effects,elements,notes);
        if(setup.Tennokai&&policy.Tennokai&&V("TennokaiDamage")>0)
        {
            Add("Damage",V("TennokaiDamage"));
            notes.Add("마스터즈 엣지·트루스 플레임의 텐노카이 피해는 프레셔 포인트와 가산입니다. 범용 피해만 받는 공격 성분에 근접 피해를 우회 적용하지 않습니다. https://wiki.warframe.com/w/Master's_Edge?oldid=2651938 · https://wiki.warframe.com/w/Truth's_Flame?oldid=2812278");
        }
        if(component?.NoDuplicate==true)effects["Duplicate"]=0;
        if(component?.NoEnergyChannel==true)effects["EnergyChannel"]=0;
        if(component is {FlatCriticalBonus:not 0})Add("FlatCritical",component.FlatCriticalBonus);
        if(exalted?.Family==ExaltedFamily.Blade){Add("Damage",.1);Add("AttackSpeed",.1);notes.Add("엑스칼리버 고유 검술: 피해·공속 각각+10%, 일반 모드와 가산. https://wiki.warframe.com/w/Excalibur/Abilities/Passive?oldid=2703993");}
        if(exalted?.Family==ExaltedFamily.Talons)
        {Range(C("ValkyrRage"),0,300,"발키르 분노");Add("Damage",Math.Floor(C("ValkyrRage"))/100);notes.Add("발키르 분노: 프레셔 포인트·컨디션 오버로드와 가산. "+ValkyrRageState.Source);}
        if(exalted is not null)ExaltedAugments.Apply(setup,Add,notes);
        var innate=JsonSerializer.Deserialize<Dictionary<DamageType,double>>(attack.GetProperty("Damage").GetRawText(),Catalog.Json)??throw new ArgumentException("공격 피해 자료 오류");
        var baseTotal=innate.Values.Sum();
        if(synthetic)
        {
            var newTotal=setup.Attack=="Heavy Attack"?MeleeClassRules.HeavyDamage(profile):raw.Number("SlideAttack");
            if(policy.Code=="GunbladeHeavy")newTotal/=attack.Number("Multishot",1);
            if(newTotal<=0)throw new ArgumentException("해당 공격의 피해 자료가 없습니다.");
            foreach(var k in innate.Keys.ToArray())innate[k]*=newTotal/baseTotal;baseTotal=newTotal;
        }
        if(stanceHit is {ComponentFraction:not 1})
        {
            Range(stanceHit.ComponentFraction,.0001,1,"복합 공격의 타격별 기본 피해 분율");
            foreach(var type in innate.Keys.ToArray())innate[type]*=stanceHit.ComponentFraction;
            baseTotal=innate.Values.Sum();
        }
        if(exalted is not null)
        {
            var strength=setup.AbilityStrength;
            if(setup.AbilityAugment?.ModId==ExaltedAugments.Accumulating)strength+=C("AccumulatingWhipclaw")/100;
            if(setup.AbilityAugment?.ModId==ExaltedAugments.Rubble&&C("RubbleHeap")>0)strength+=.25*(setup.AbilityAugment.Rank+1);
            var scale=exalted.RankDamage[setup.AbilityRank]/exalted.RankDamage[3]*strength;
            foreach(var type in innate.Keys.ToArray())innate[type]*=scale;baseTotal=innate.Values.Sum();
            if(baseTotal<=0)throw new ArgumentException("익절티드 계산에는 양수인 유효 어빌리티 위력이 필요합니다.");
            notes.Add($"어빌리티 랭크 {setup.AbilityRank} · 위력 {strength:P0} · 무기 랭크는 수용량만 결정. "+exalted.Source);
        }
        if(setup.AbilityAugment?.ModId==ExaltedAugments.Chromatic&&!slam)
        {
            var physical=innate.Where(p=>p.Key is DamageType.Impact or DamageType.Puncture or DamageType.Slash).Sum(p=>p.Value);
            foreach(var type in new[]{DamageType.Impact,DamageType.Puncture,DamageType.Slash})innate.Remove(type);
            innate[setup.ChromaticElement!.Value]=innate.GetValueOrDefault(setup.ChromaticElement.Value)+physical;
        }
        // Evolution base damage is flat. The Wiki explicitly excludes heavy-attack base scaling.
        if(V("BaseDamage")!=0&&policy.EvolutionDamage)
        {
            var split=policy.Code is "OkinaDagger" or "OkinaExplosion"?attack:normal;
            var normalTotal=split.GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble());
            foreach(var p in split.GetProperty("Damage").EnumerateObject())
                if(Enum.TryParse<DamageType>(p.Name,out var type))innate[type]=innate.GetValueOrDefault(type)+V("BaseDamage")*p.Value.GetDouble()/normalTotal;
            baseTotal=innate.Values.Sum();
        }
        if(ceramicProjectile)
        {
            var shieldBonus=incarnon!.Perks.Where(p=>(setup.Evolutions??[]).Contains(p.Id)&&p.Condition=="ShieldBreak").Sum(p=>p.Conditional.GetValueOrDefault("BaseDamage")*C("ShieldBreak"));
            if(shieldBonus>0)
            {
                foreach(var type in innate.Keys.ToArray())innate[type]*=(baseTotal+shieldBonus)/baseTotal;
                baseTotal=innate.Values.Sum();
                notes.Add("세라믹 단검 투사체/폭발은 진화II의 상시+100/+120을 제외하지만 실드 파괴의+80 기본 피해는 받습니다. https://wiki.warframe.com/w/Ceramic_Dagger_Incarnon_Genesis?oldid=2773637");
            }
        }
        if(V("ImpactToPuncture")>0)
        {var convert=innate.GetValueOrDefault(DamageType.Impact)*V("ImpactToPuncture");innate[DamageType.Impact]=innate.GetValueOrDefault(DamageType.Impact)-convert;innate[DamageType.Puncture]=innate.GetValueOrDefault(DamageType.Puncture)+convert;}
        var adversaryImpact=0d;var ordinaryImpactBonus=V("ImpactDamage");
        if(setup.BonusElement is {} bonus)
        {
            if(!raw.Flag("IsLichWeapon")||bonus is not (DamageType.Impact or DamageType.Heat or DamageType.Cold or DamageType.Electricity or DamageType.Toxin or DamageType.Magnetic or DamageType.Radiation))throw new ArgumentException("이 무기에 사용할 수 없는 추가 원소입니다.");
            Range(setup.Bonus,.25,.6,"추가 원소 비율");if(bonus==DamageType.Impact)adversaryImpact=baseTotal*setup.Bonus;innate[bonus]=innate.GetValueOrDefault(bonus)+baseTotal*setup.Bonus;baseTotal=innate.Values.Sum();
        }
        else if(setup.Bonus!=0)throw new ArgumentException("추가 원소 종류를 선택하세요.");
        if(profile.Code=="Motovore")
        {
            foreach(var k in new[]{"ImpactDamage","PunctureDamage","SlashDamage"})effects[k]=V(k)*6;
            var dominant=new[]{DamageType.Impact,DamageType.Puncture,DamageType.Slash}.OrderByDescending(t=>normal.GetProperty("Damage").Number(t.ToString())*(1+V(t+"Damage"))).First();
            if(dominant==DamageType.Impact)Add("AttackSpeed",.4);if(dominant==DamageType.Slash)Add("StatusChance",1);if(dominant==DamageType.Puncture)Add("Range",1.5);
        }
        var finalCombo=useInitialComboFloor?Math.Max(scenario.Combo,Math.Min(weapon.MaxCombo,Math.Floor(V("InitialCombo")/(profile.Code=="DexNikana"?11:20))+1)):scenario.Combo;
        if(setup.Hologram is not null)finalCombo=1;
        var effectiveCombo=policy.ComboStats?Math.Min(12,finalCombo):1;
        if(!policy.ComboStats)notes.Add("시그마 & 옥탄티스 투척: 콤보 배수와 콤보 기반 치명/상태 모드 제외. 일반 치명/상태 모드·막기 충전은 유지합니다. "+ShieldThrowMechanic.SigmaSource);
        if(exalted?.Family==ExaltedFamily.Staff)Add("Range",.5*Math.Min(11,finalCombo-1));
        if(exalted?.Family==ExaltedFamily.Clones){Add("SlashStatusDamage",.25);Add("SlashDuration",.5);}
        if(policy.CoSourceConflict is {} conflict&&V("ConditionOverload")>0&&statusTypes>0)throw new CombatRuleUnavailableException(conflict);
        var innodemProjectile=policy.Code=="InnodemProjectile";
        if(policy.Code=="ExaltedWave"&&exalted?.Family==ExaltedFamily.Blade&&C("BladeWaveFalloffModel")>0)notes.Add("미검증: "+ExaltedWaveFlight.Conditional+" · "+(C("BladeWaveFalloffModel")==1?"40%/초·30m/s·75m":"0~70m 선형·관통당15m 수명 감소")+" · "+exalted.Source);
        var coCoefficient=innodemProjectile?conditions.GetValueOrDefault("InnodemCoMultiplier",policy.Co):policy.Co;
        var location=innodemProjectile?conditions.GetValueOrDefault("InnodemLocationMultiplier",1):1;
        if(innodemProjectile)
        {
            Range(location,0,100,"인노뎀 위치 배율 가정");
            if(coCoefficient is not (1 or 2))throw new ArgumentException("인노뎀 컨오 보정은 원문 두 분기인1 또는2를 지정하세요.");
            notes.Add("미검증: 인노뎀은 위키에 위치에 따른 불규칙 피해와 컨오1/2배 두 분기가 기재되어 있지만 위치/분기 선택 규칙은 없습니다. 현재 위치 배율 "+location+"·컨오 "+coCoefficient+"는 명시한 시나리오 또는 참조 조건입니다. 컨오는 스탠스·위치·거리 감쇠를 받지 않습니다. https://wiki.warframe.com/w/Innodem?oldid=2816908");
        }
        var co=coCoefficient*V("ConditionOverload")*statusTypes*coFactor;
        var faction=1+buffs.Roar+(policy.Faction?V("Vs"+enemyProfile.Faction):0);
        var eclipseTwice=policy.Code=="Contagion"||raw.Text("Class")=="Glaive"&&setup.Attack.EndsWith("Explosion",StringComparison.Ordinal);
        var usesFollowThrough=policy.FollowThrough&&!slam&&!slideAttack&&setup.Attack!="Slide Attack"&&policy.Code is not ("VitricaGlass" or "VitricaGlassRadial");
        if(usesFollowThrough&&setup.TargetIndex>1&&!raw.TryGetProperty("FollowThrough",out _))throw new ArgumentException("이 조합의 연속 관통 피해 계수가 원문에 없어 두 번째 이후 대상 계산을 지원하지 않습니다.");
        var follow=Math.Clamp(raw.Number("FollowThrough",1)+V("FollowThrough"),0,1);
        var brackets=new DamageBrackets(baseTotal,policy.DamageSource=="None"?0:policy.DamageSource=="Combined"?V("Damage")+V("UniversalDamage"):V(policy.DamageSource=="Universal"?"UniversalDamage":"Damage"),
            heavyBonuses&&!ceramicProjectile&&(!policy.HeavyDamageAnimationOnly||C("HeavyAnimation")>0)?V("HeavyDamage"):0,
            policy.CoMultiplicative?0:co,policy.EvolutionDamage?V("CoExcludedBase"):0,policy.CoMultiplicative?1+co:1,
            scenario.MotionMultiplier*location*(policy.Code=="SpectralAxe"&&C("Airborne")>0?2:1),policy.Combo||exalted?.Pseudo==true?finalCombo:1,
            slam?1+V("SlamDamage"):1,1,
            usesFollowThrough?Math.Pow(follow,setup.TargetIndex-1):1,faction,policy.Code=="KorummTacticalSlam"?1:Math.Max(0,1+V("FinalDamage")),policy.CoCombo,
            PostBaseDamage:heavyBonuses?C("BlockedDamage")*V("StoredDamageFraction"):0,
            WeaponDamageMultiplier:policy.Code=="Contagion"?1+V("UniversalDamage"):raw.Text("Class")=="Glaive"&&setup.Attack.Contains("Throw")?1+V("ThrowWeaponDamage")+V("UniversalDamage"):1,
            ConditionUsesMotion:!innodemProjectile,EclipseMultiplier:Math.Pow(1+eclipse,eclipseTwice?2:1));
        if(component is not null)brackets=brackets with {
            AdditiveDamage=brackets.AdditiveDamage+component.MeleeDamageBonus,
            WeaponDamageMultiplier=brackets.WeaponDamageMultiplier+component.WeaponDamageBonus,
            FinalMultiplier=brackets.FinalMultiplier*component.DamageMultiplier*component.SeparateDamageMultiplier};
        var hologram=setup.Hologram is not null?setup.ExternalEquipment!.HallOfMirrors:null;
        if(hologram is not null)
        {
            brackets=brackets with {FinalMultiplier=brackets.FinalMultiplier*HallOfMirrorsRules.Scale(hologram)*HallOfMirrorsRules.Malevolence(hologram)};
            notes.Add("홀 오브 미러즈:분신1개의 피해·무기 치명/상태 확률 배율·콤보 제외·갈바나이즈드/일반 아케인 제외. 멀레볼런스는 별도 피해 배수이며 위력에 영향받지 않습니다. "+HallOfMirrorsRules.Source);
            policy=policy with {HitCombo=false,Combo=false,ComboStats=false,Tennokai=false};
        }
        if(sourceMultiplier!=1){Range(sourceMultiplier,0,1,"전파 피해 비율");brackets=brackets with {FinalMultiplier=brackets.FinalMultiplier*sourceMultiplier};}
        TargetDebuffs.Validate(setup.Debuffs);
        if(exalted?.Family==ExaltedFamily.Whipclaw&&TargetDebuffs.WhipclawMultiplier(setup)>1)
        {brackets=brackets with {FinalMultiplier=brackets.FinalMultiplier*2};notes.Add("인스네어:윕클로의 어빌리티 피해×2·위력/프레셔와 별도. 상태 피해의 원천에 반영합니다. https://wiki.warframe.com/w/Ensnare?oldid=2816106");}
        var incoming=TargetDebuffs.Incoming(setup,exalted?.Family==ExaltedFamily.Whipclaw);
        if(setup.Debuffs?.Length>0)notes.Add("대상 취약도:무기 기본 피해/프레셔/컨오와 별도·받는 피해에 한 번 적용·상태 피해에 중복 적용하지 않음. 어빌리티 자체가 주는 피해는 근접 피해에 합산하지 않습니다. "+TargetDebuffs.Source);
        var moddedBase=brackets.ModdedBaseDamage;
        var statusOnlyElectric=policy.Code=="KorummTacticalSlam"?elements.Where(e=>e.Type==DamageType.Electricity).Sum(e=>e.Value):0;
        if(!policy.Elements)elements.Clear();
        var damage=innate.Where(p=>!Basic.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value/baseTotal*moddedBase*(1+(policy.Ips&&p.Key is DamageType.Impact or DamageType.Puncture or DamageType.Slash?V(p.Key+"Damage"):0)));
        if(profile.Code=="Motovore"&&adversaryImpact>0&&policy.Ips)damage[DamageType.Impact]-=adversaryImpact/baseTotal*moddedBase*ordinaryImpactBonus*5;
        if(stanceHit is not null)
            foreach(var key in damage.Keys.ToArray())damage[key]*=key switch {DamageType.Impact=>stanceHit.Impact,DamageType.Puncture=>stanceHit.Puncture,DamageType.Slash=>stanceHit.Slash,_=>1};
        foreach(var key in damage.Keys.Where(t=>t is DamageType.Impact or DamageType.Puncture or DamageType.Slash).ToArray())damage[key]*=policy.PhysicalMultiplier;
        var elementalBonuses=elements.GroupBy(e=>e.Type).ToDictionary(g=>g.Key,g=>g.Sum(e=>e.Value));
        if(policy.Code=="KorummTacticalSlam")elementalBonuses[DamageType.Electricity]=statusOnlyElectric;
        foreach(var type in Basic)if(innate.GetValueOrDefault(type)>0)elements.Add((type,innate[type]/baseTotal));
        // An absent (zero-valued) element must not reserve a combination slot.
        // Residual/Ack & Brunt conditions may contain zero entries for other types.
        var ordered=elements.Where(e=>e.Value!=0&&Basic.Contains(e.Type)).Select(e=>e.Type).Distinct().ToArray();
        var amounts=elements.GroupBy(e=>e.Type).ToDictionary(g=>g.Key,g=>g.Sum(e=>e.Value));
        for(var i=0;i<ordered.Length;i+=2)
        {
            var type=ordered[i];var amount=amounts[type];
            if(i+1<ordered.Length){amount+=amounts[ordered[i+1]];type=DamageEngine.Combine(type,ordered[i+1]);}
            damage[type]=damage.GetValueOrDefault(type)+moddedBase*Math.Max(0,amount);
        }
        foreach(var (type,value) in amounts.Where(p=>!Basic.Contains(p.Key)))damage[type]=damage.GetValueOrDefault(type)+moddedBase*Math.Max(0,value);
        if(setup.AbilityAugment?.ModId==ExaltedAugments.Reactive&&damage.Remove(DamageType.Impact,out var adaptedImpact))
        {var adapted=ExaltedAugments.AdaptiveType(setup.Faction);damage[adapted]=damage.GetValueOrDefault(adapted)+adaptedImpact;}
        if(V("Exposure")>0&&component?.NoSupplementalElements!=true&&policy.Code!="KorummTacticalSlam")damage[DamageType.Corrosive]=damage.GetValueOrDefault(DamageType.Corrosive)+moddedBase*V("Exposure");
        if(V("SeparateHeat")>0&&component?.NoSupplementalElements!=true&&policy.Code!="KorummTacticalSlam")
        {damage[DamageType.Heat]=damage.GetValueOrDefault(DamageType.Heat)+moddedBase*V("SeparateHeat");elementalBonuses[DamageType.Heat]=elementalBonuses.GetValueOrDefault(DamageType.Heat)+V("SeparateHeat");}
        if(V("AdaptiveElement")>0&&!slam&&component?.NoSupplementalElements!=true)
        {
            var adaptive=setup.Overguard>0?DamageType.Corrosive:setup.Faction switch {"Infested"=>DamageType.Heat,"Anarchs"=>DamageType.Electricity,"Narmer"=>DamageType.Toxin,"Grineer" or "KuvaGrineer" or "Zariman" or "Scaldra"=>DamageType.Corrosive,"Corpus" or "CorpusAmalgam" or "Techrot"=>DamageType.Magnetic,"Orokin"=>DamageType.Viral,"Sentient" or "TheMurmur"=>DamageType.Radiation,"InfestedDeimos"=>DamageType.Gas,_=>throw new ArgumentException("센티언트 인시전에는 대상 팩션 선택이 필요합니다.")};
            damage[adaptive]=damage.GetValueOrDefault(adaptive)+moddedBase*V("AdaptiveElement");
            elementalBonuses[adaptive]=elementalBonuses.GetValueOrDefault(adaptive)+V("AdaptiveElement");
        }
        if(finisher)
        {
            damage=new(){[DamageType.Finisher]=damage.Values.Sum()*(1+V("FinisherDamage"))};
            notes.Add(FinisherCatalog.Label(setup.Finisher??"Ground")+": 모딩 피해를 마무리 피해로 전환 · 방어력 무시 · 무작위 상태 발생 없음");
        }
        if(exalted?.Family==ExaltedFamily.Clones)foreach(var type in damage.Keys.ToArray())damage[type]*=1+V("FinisherDamage");
        var sc=(attack.Number("StatusChance",weapon.StatusChance)+(policy.EvolutionStats?V("BaseStatus"):0))*(1+(policy.StatusMods?V("StatusChance")+V("WeepingWounds")*(effectiveCombo-1)+(setup.Tennokai&&policy.Tennokai?V("TennokaiStatus"):0):0))+V("FlatStatus");
        if(policy.Code=="KorummTacticalSlam"){sc=.5;notes.Add("코룸 특수 슬램: 직접 피해는 슬램 피해·팩션만, 치확0%·상확50%·10m 무감쇠. 전기 모드는 상태 틱에만 적용합니다. "+WeaponStanceMechanic.Source(weapon.Id));}
        if(stanceHit is not null)sc*=stanceHit.StatusMultiplier;
        sc+=V("PostStanceStatus");
        if(component?.FixedStatusChance==true||policy.Code=="CaustacystPool")sc=attack.Number("StatusChance");
        sc*=1+.1*St(DamageType.Tau);
        var statusCritical=component?.UnknownStatusUpperBound==true&&V("StatusToBaseCritical")>0?.35:Math.Min(.35,sc*V("StatusToBaseCritical"));
        var criticalModMultiplier=GlaiveCriticalRules.ModMultiplier(weapon.Id,setup,heavyBonuses);
        if(criticalModMultiplier!=(heavyBonuses?2:1))notes.Add("글레이브 위키의 지상/공중 투척 치확 버그 적용: 치확 모드 배수만 변경하며 강공격 피해·콤보와 구분합니다. "+GlaiveCriticalRules.Source);
        var cc=(attack.Number("CritChance",weapon.CriticalChance)+(policy.EvolutionStats?V("BaseCritical")+statusCritical:0))*(1+V("CriticalChance")*criticalModMultiplier+V("PassiveCriticalChance")+V("ArcaneCriticalChance")+(V("BloodRush")+.1*gladiator)*(effectiveCombo-1)+(setup.Attack=="Slide Attack"||slideAttack?V("SlideCriticalChance"):0))+V("FlatCritical");
        if(hologram is not null){cc*=HallOfMirrorsRules.Scale(hologram);sc*=HallOfMirrorsRules.Scale(hologram);}
        // Puncture and Cold vulnerability are explicitly excluded from radial attacks by the Wiki.
        if(!aoe)cc+=.05*St(DamageType.Puncture);
        var cdBase=V("SetBaseCriticalDamage")>0?V("SetBaseCriticalDamage"):attack.Number("CritMultiplier",weapon.CriticalMultiplier);
        var cd=(cdBase+V("BaseCriticalDamage")+(setup.Incarnon?V("IncarnonCriticalDamage"):0))*(1+V("CriticalDamage")+(setup.Tennokai&&policy.Tennokai?V("TennokaiCriticalDamage"):0))+V("FlatCriticalDamage");
        if(component?.WeaponDoughty==true&&V("Doughty")>0)
        {
            var reference=CalculateAttack(catalog,build with {Melee=setup with {Attack="Normal Attack",Finisher=null,Tennokai=false},Scenario=scenario with {MotionMultiplier=1}},useInitialComboFloor:useInitialComboFloor);
            cd+=ArcaneEngine.DoughtyBonus(reference.Damage,reference.StatusChance,V("Doughty"));
            notes.Add("이 성분의 다우티는 일반 근접의 피해 비율·상태 확률을 참조합니다.");
        }
        else cd+=component?.UnknownStatusUpperBound==true&&V("Doughty")>0&&damage.GetValueOrDefault(DamageType.Puncture)>0?50:ArcaneEngine.DoughtyBonus(damage,sc,V("Doughty"));
        // Inability to generate random procs does not remove the modded
        // weapon stat from effects which read status chance (Doughty, etc.).
        if(finisher||component?.NoRandomStatus==true)sc=0;
        var coldStacks=setup.Overguard>0?Math.Min(4,St(DamageType.Cold)):St(DamageType.Cold);
        if(!aoe)cd+=frozen?1:coldStacks>0?.1+.05*(coldStacks-1):0;
        if(finisher)
        {
            // The attack converts damage/proc behavior, not the weapon's
            // modded critical stats. In particular Doughty must still read
            // its weapon's Puncture ratio and status chance, not 100% Finisher.
            var weaponStats=CalculateAttack(catalog,build with {Melee=setup with {Finisher=null},Scenario=scenario with {MotionMultiplier=1}},useInitialComboFloor:useInitialComboFloor);
            cc=weaponStats.CriticalChance;cd=weaponStats.CriticalMultiplier;
        }
        cc=Math.Max(0,cc);sc=Math.Max(0,sc);cd=Math.Max(1,cd);
        if(!policy.CriticalMods){cc=attack.Number("CritChance")*(hologram is null?1:HallOfMirrorsRules.Scale(hologram));cd=attack.Number("CritMultiplier",1);}
        if(component?.FlatCriticalOnly==true)
        {
            cc=Math.Max(0,attack.Number("CritChance")+V("FlatCritical"))*(hologram is null?1:HallOfMirrorsRules.Scale(hologram));
            cd=Math.Max(1,attack.Number("CritMultiplier",1)+V("FlatCriticalDamage"));
        }
        var sourceSpeed=normal.Number("FireRate",weapon.AttackSpeed);
        var baseSpeed=MeleeAttackSpeed.FromWiki(sourceSpeed);
        var speed=baseSpeed*Math.Max(.05,1+V("AttackSpeed"))*(1+V("SpeedMultiplier"));
        if(exalted is {AttackSpeed:false})speed=1; // Casting speed is separate from melee attack speed.
        if(exalted?.Family==ExaltedFamily.Clones)speed=Math.Min(1.5,speed);
        if(exalted?.Family==ExaltedFamily.Clones&&C("ClonesCastSpeed")>0){Range(C("ClonesCastSpeed"),.05,1.5,"블레이드 스톰 시전 공속");speed=C("ClonesCastSpeed");}
        if(finisher)
        {
            speed=Math.Min(1.5,Math.Max(.05,1+V("AttackSpeed")+V("FinisherSpeed"))*(1+V("SpeedMultiplier")));
            notes.Add("마무리 동작은 무기 기본 공속을 사용하지 않으며 공속 보너스 +50%에서 애니메이션 길이 2/3로 제한됩니다. https://wiki.warframe.com/w/Finisher?oldid=2812039");
        }
        var windup=slam?0:raw.Number("WindUp",MeleeClassRules.Find(raw.Text("Class"))?.Windup??0)/Math.Max(.05,1+V("Windup"));
        if(weapon.Id==WolfSledgeFlight.WeaponId&&setup.Attack=="Throw")
        {
            windup=attack.Number("ChargeTime")/Math.Max(.05,build.Capacity?.StanceEquipped==true?speed:1+V("Windup"));
            notes.Add("울프 슬레지: 스탠스 장착 시 투척 충전은 공격 속도, 미장착 시 준비 속도로 단축됩니다. 최초200ms 홀드·짧은 발사 동작·왕복은 별도이며 충전 시간만으로 전체 주기를 만들지 않습니다. "+WolfSledgeFlight.Source);
        }
        if(setup.Tennokai)notes.Add("텐노카이 준비 시간은 일반 준비 속도 모드의 영향을 받지 않습니다. 준비 수치는 일반 강공격 기준입니다.");
        var interval=scenario.BaseInterval/speed;
        var area=MeleeAreaRules.Resolve(attack,policy,raw.Text("Class"),setup,effects,weapon.Id);
        var falloff=area.Falloff;
        if(component is null&&policy.Code=="CaustacystWave")
        {
            falloff=Math.Max(0,1-setup.Distance/35-.2*(setup.TargetIndex-1));
            notes.Add("카우스타시스트 파동: 이 고정 수치는 데이터 표35m 감쇠 기준입니다. 본문37.5m 감쇠 및 코다 상확37/41%와 상충하며, 별도 성분 표에 두 출처 결과를 표시합니다. 앞 대상마다20% 차감하고 칼날 관통0.6을 중복하지 않습니다. "+CaustacystMechanic.Source);
        }
        if(policy.Code=="ExaltedWave")falloff=ExaltedRules.WaveFalloff(exalted!,setup);
        if(policy.RangeProfile=="Stropha")
        {
            var waveDistance=setup.Distance/Math.Max(.00001,1+V("ProjectileSpeed"));
            falloff=waveDistance<=11?1:waveDistance<=15?.5:waveDistance<=17?.5-(waveDistance-15)/2*(.5-10d/700):0;
        }
        if(policy.Code=="AgendusDisk")
        {
            var model=C("AgendusFalloffModel");var diskDistance=setup.Distance/Math.Max(.00001,1+V("ProjectileSpeed"));
            if(diskDistance>10&&!area.Outside&&model is not (1 or 2))throw new CombatRuleUnavailableException("테넷 아젠더스 거리 감쇠는 본문 최저5%/데이터6.95%가 상충합니다. 고유 공격에서 출처 분기를 선택하세요. "+WeaponStanceMechanic.Source(weapon.Id));
            falloff=1-Math.Clamp((diskDistance-10)/10,0,1)*(model==2?.9305:.95);
            if(diskDistance>10&&!area.Outside)notes.Add("미검증: 테넷 아젠더스 거리 감쇠 출처 분기 · "+(model==2?"데이터6.95%":"본문5%"));
        }
        var effectRange=area.Radius;
        var outsideRange=area.Outside;
        if(exalted is not null)
        {
            if(ExaltedGeometry.Radius(exalted,setup,effects) is {} radius){effectRange=radius;outsideRange=setup.Distance>radius;}
            if(ExaltedGeometry.Reach(exalted,setup) is {} reach)
            {notes.Add($"어빌리티 시전/표식 거리 {reach:0.###}m"+(aoe?$" · 명중 지점 주위 반경 {effectRange:0.###}m":"")+" · "+exalted.Source);if(exalted.Family==ExaltedFamily.Lash)outsideRange=setup.Distance>reach;}
            if(policy.Code=="ExaltedWave"&&falloff<=0)outsideRange=true;
            if(C("ExaltedInactive")>0)outsideRange=true;
        }
        if(weapon.Id==ShieldThrowMechanic.Sigma&&setup.Attack=="Shield Throw")
        {
            var shieldSpeed=Math.Max(0,1+V("ProjectileSpeed"));var low=24*shieldSpeed;var high=25*shieldSpeed;
            if(setup.Distance>low&&setup.Distance<=high)throw new CombatRuleUnavailableException("시그마 & 옥탄티스 투척 사거리가 본문25m/무기 데이터24m로 상충합니다. 공통 범위 또는 범위 밖에서는 계산합니다. "+ShieldThrowMechanic.SigmaSource);
            outsideRange=setup.Distance>high;notes.Add("투척 사거리: 본문25m/데이터24m 불일치 · 근접 사거리 모드 제외 · 투사체 속도 보너스 적용");
        }
        if(policy.Code=="NepheriFireball")
        {
            var fireballSpeed=Math.Max(0,1+V("ProjectileSpeed"));
            if(setup.Distance>19*fireballSpeed&&setup.Distance<=20*fireballSpeed)throw new CombatRuleUnavailableException("네페리 화염구 사거리는 본문19m/무기 데이터20m가 상충합니다. 공통 범위 안팎만 확정 계산합니다. "+WeaponStanceMechanic.Source(weapon.Id));
            notes.Add("네페리 화염구:19/20m 출처 불일치 · 10m 안 강제 충격 · 콤보 획득/유지 없음 · 공속은 투사체 속도를 바꾸지 않습니다.");
        }
        if(outsideRange)falloff=0;
        if(component is null&&policy.Code=="CaustacystWave")
        {brackets=brackets with {FinalMultiplier=brackets.FinalMultiplier*falloff};falloff=1;}
        var oldBase=moddedBase;brackets=brackets with {RangeFalloff=falloff};moddedBase=brackets.ModdedBaseDamage;
        if(outsideRange)moddedBase=0;
        foreach(var key in damage.Keys.ToArray())damage[key]*=oldBase>0?moddedBase/oldBase:0;
        var armor=MeleeTargetModel.EffectiveArmor(enemy.Armor,setup);
        var dr=DamageEngine.EnemyArmorReduction(armor);var viral=St(DamageType.Viral)>0?2+.25*(St(DamageType.Viral)-1):1;
        var magnetic=St(DamageType.Magnetic)>0?2+.25*(St(DamageType.Magnetic)-1):1;
        var targetDamage=damage.ToDictionary(p=>p.Key,p=>p.Value*MeleeTargetModel.Multiplier(p.Key,enemy.Armor,setup)*incoming);
        var stealth=policy.Stealth?MeleeStealth.Bonus(build):0;
        var meanCritical=MeleeStealth.Mean(cc,cd,stealth);
        var revealsOnHit=!contextFinisher&&setup.Attack is "Normal Attack" or "Heavy Attack" or "Slide Attack" or "Attacks While Disc Deployed"&&stanceHit?.AttackType!="Ranged"&&policy.Code!="GunbladeHeavy";
        var reveal= revealsOnHit&&MeleeStealth.Invisible(setup)&&!MeleeStealth.Blinded(setup);
        var crit=meanCritical+ArcaneEngine.YellowChance(cc)*V("Duplicate")*(meanCritical-(reveal?stealth:0));
        var extra=outsideRange||policy.Code=="KorummTacticalSlam"?0:EnergyChannelState.Amount(C("EnergySpent"),V("EnergyChannel"),elementalBonuses);
        var projectiles=attack.Number("Multishot",1);
        var opera=MetronomeRules.Opera(setup.ExternalEquipment?.Metronome);
        if(component?.SingleProjectile!=true&&opera>0&&MetronomeRules.OperaApplies(weapon.Id,setup.Attack))
        {
            projectiles*=1+opera;
            contributions.Add(new("external:opera","오페라",setup.ExternalEquipment!.Metronome!.Rank,"Multishot","투사체 수",opera,MetronomeRules.Source));
            notes.Add($"오페라:발사당 기대 {projectiles:0.###}개 · 정수부 확정+소수부 확률로1개 추가·각 투사체 치명/상태 독립. "+MetronomeRules.MultishotSource);
            if(WeaponStanceMechanic.IsQuassus(weapon.Id))notes.Add("콰서스 오페라:일반판 문서의 단검 멀티샷 규칙을 같은 고유 공격을 사용하는 프라임에도 적용합니다. 여러 단검의 간헐적 상태 합침은 독립 명중 조건에서 비교합니다.");
        }
        else if(opera>0&&component?.SingleProjectile!=true&&setup.Attack is not ("Normal Attack" or "Slide Attack" or "Slam Attack" or "Heavy Slam Attack")&&!MetronomeRules.OperaApplies(weapon.Id,setup.Attack))
            notes.Add("미반영:오페라 멀티샷의 이 고유 공격 적용 여부는 출처에서 확인되지 않았습니다. 현재 확인된 콰서스 단검/리디머 프라임 사격에 적용합니다.");
        if(C("ProjectilesHit")>0){Range(C("ProjectilesHit"),1,Math.Ceiling(projectiles),"명중 투사체 수");if(C("ProjectilesHit")!=Math.Floor(C("ProjectilesHit")))throw new ArgumentException("명중 투사체 수는 정수여야 합니다.");projectiles=(int)C("ProjectilesHit");}
        // A stored Energy Channel charge is consumed by one hit, not by every pellet.
        var expected=damage.Values.Sum()*crit*projectiles+extra;var targetHit=targetDamage.Values.Sum()*crit*projectiles+(extra>0?extra*EnergyChannelState.TargetMultiplier(setup,dr,incoming):0);
        if(extra>0)notes.Add("에너지 채널:실제로 소모한 에너지100까지 저장·적에게 맞은 한 타격에만 소비·치명 이후 합산·원소 모드만 증폭. 시간별 교전에서는 입력한 어빌리티 비용으로 재충전합니다. "+EnergyChannelState.Source);
        var duration=6*Math.Max(0,1+V("StatusDuration"));
        var statusDamage=new List<StatusDamage>();
        var forced=attack.Strings("ForcedProcs");
        if(ShieldThrowMechanic.IsCobra(weapon.Id)&&setup.Attack is "Normal Attack" or "Heavy Attack" or "Slide Attack")forced=forced.Append("Stagger").Distinct().ToArray();
        if(weapon.Id=="/Lotus/Weapons/Tenno/Melee/Glaives/LightGlaive/LightGlaiveWeapon"&&setup.Attack=="Throw")
        {forced=forced.Append("Impact").Distinct().ToArray();notes.Add("일반 투척의 강제 충격은 무기 본문의 명시 규칙으로 보완합니다. "+GlaiveFlightState.ReturnSource);}
        if(synthetic&&heavy&&stanceHit is null&&raw.Text("Class") is "Claws" or "Nikana" or "Rapier" or "Scythe" or "Two-Handed Nikana" or "Whip")forced=forced.Append("Slash").ToArray();
        if(stanceHit is not null)forced=forced.Concat(stanceHit.ForcedProcs.Select(ProcName)).Distinct().ToArray();
        if(policy.Code=="OkinaDagger")forced=forced.Where(p=>p!="Cold").Concat(Enumerable.Repeat("Cold",10)).ToArray();
        if(exalted?.Family==ExaltedFamily.Clones)forced=forced.Append("Slash").Distinct().ToArray();
        if(exalted?.Family==ExaltedFamily.Landslide&&C("AbilityComboSequence")==3)forced=forced.Append("Knockdown").Distinct().ToArray();
        if(V("ForcedSlash")>0)forced=forced.Append("Slash").Distinct().ToArray();
        if(policy.Code=="AgendusDisk"&&setup.Distance<=20)forced=forced.Append("Impact").Distinct().ToArray();
        if(policy.Code=="NepheriFireball")forced=setup.Distance<=10?["Impact"]:[];
        if(outsideRange){forced=[];notes.Add("선택한 거리는 이 공격 성분의 범위 밖입니다. 피해·상태·명중 콤보를 적용하지 않습니다.");}
        foreach(var type in new[]{DamageType.Slash,DamageType.Heat,DamageType.Electricity,DamageType.Toxin,DamageType.Gas,DamageType.Blast})
        {
            var chance=damage.GetValueOrDefault(type)/Math.Max(1e-12,damage.Values.Sum())*sc+(forced.Contains(type.ToString())?1:0);
            if(type==DamageType.Slash&&V("ImpactToSlash")>0)chance+=(damage.GetValueOrDefault(DamageType.Impact)/Math.Max(1e-12,damage.Values.Sum())*sc+(forced.Contains("Impact")?1:0))*V("ImpactToSlash");
            var tick=moddedBase*faction*(1+V("StatusDamage")+(type==DamageType.Slash?V("SlashStatusDamage"):0))*(1+V("StatusFinalMultiplier"))*(1+V("StatusVulnerability"))*(type==DamageType.Slash?.35:type==DamageType.Blast?.3:.5);
            if(type!=DamageType.Slash)tick*=1+elementalBonuses.GetValueOrDefault(type);
            if(exalted?.Family==ExaltedFamily.Clones&&type==DamageType.Slash)tick*=1+V("FinisherDamage");
            tick*=meanCritical;
            if(stanceHit is not null&&forced.Contains(type.ToString()))tick*=stanceHit.ProcDamageMultiplier;
            var procDuration=type==DamageType.Slash?6*Math.Max(0,1+V("StatusDuration")+V("SlashDuration")):type==DamageType.Blast?duration/4:duration;
            var ticks=type==DamageType.Blast?1:type is DamageType.Electricity or DamageType.Gas?(int)Math.Ceiling(procDuration-1e-9):(int)Math.Floor(procDuration+1e-9);
            statusDamage.Add(new(type,chance,tick,procDuration,Math.Max(0,ticks),type==DamageType.Slash));
        }
        if(V("HealingPerStatus")>0)notes.Add("시작 상태 회복 수치는 기존 상태만 사용합니다. 같은 명중에서 부여된 상태·산탄별 변화·처치가 반영된 힐링 리턴은 교전 결과에 별도 표시합니다. "+HealingReturnRules.Source);
        notes.Add("선택한 발동 상태·중첩을 고정한 1회 공격 계산입니다. 발동 대기 시간과 유지율은 결과와 구분합니다.");
        if(profile.Name=="Rakta Dark Dagger")notes.Add("실드 회복과 리탈리에이션 변화는 시간별 전투에서 처리합니다. 워프레임 최대 실드가 필요하며, 바이러스/자성 비피해 상태의 추가 회복량은 원문 수식이 미기재입니다.");
        notes.Add("상태 피해 표는 치명타 기대값을 포함한 단일 상태 1중첩의 피해입니다. 상태 확률을 실제 중첩으로 반올림하지 않습니다.");
        notes.Add("반복 간격은 사용자 설정입니다. 준비 시간은 별도 표시하며, 공격 후 회복 시간이나 투사체 왕복 시간을 임의로 만들지 않습니다.");
        var triggered=effects.Where(p=>MeleeMechanicScope.EffectCanTrigger(p.Key,weapon.Id,setup)).ToDictionary();
        notes.AddRange(MeleeEffectCoverage.Notes(triggered));
        var dormant=effects.Where(p=>!MeleeMechanicScope.EffectCanTrigger(p.Key,weapon.Id,setup)).ToDictionary();
        notes.AddRange(MeleeEffectCoverage.Notes(dormant).Select(n=>n.Replace("미반영:","선택한 동작에서 미발동:")));
        var trace=brackets.Trace(profile.Source?.Url??weapon.Source).ToList();
        if(exalted is not null)trace.Insert(0,new("어빌리티 기본 피해",$"랭크 {setup.AbilityRank} 기본 {exalted.RankDamage[setup.AbilityRank]} × 위력 {setup.AbilityStrength:P0}",baseTotal,exalted.Source));
        trace.Add(new("공격 동작 속도",finisher?"무기 기본 공속 제외 · 보너스 +50% 상한":$"기본 {sourceSpeed:G8} → {baseSpeed:G12} (X/60) × 가산 공속 × 별도 공속",speed,finisher?FinisherCatalog.Source.Source.Url:MeleeAttackSpeed.Source));
        trace.Add(new("최종 치명타 확률","(기본 확률 + 진화 기본 치확) × 가산 보너스 + 고정 치확",cc,RulesVersion));
        if(stealth>0){trace.Add(new("은신·치명타 가산","1 + 은신 보너스 + 치명타 확률 × (치명 배수 − 1)",meanCritical,MeleeStealth.Source));notes.Add("은신과 치명타 보너스는 가산입니다. 발생한 상태 피해도 해당 타격의 은신·치명타 배수를 보존합니다. "+MeleeStealth.DotSource);}
        trace.Add(new("방어력","부식·화염 감소를 적용한 방어력",armor,RulesVersion));
        var details=new MeleeDetails(setup.Attack,moddedBase,faction,1+V("StatusDamage"),elementalBonuses,targetDamage,statusDamage.ToArray(),raw.Number("MeleeRange")+V("Range")+(setup.Tennokai?V("TennokaiRange"):0)+(C("Airborne")>0?V("AerialRange"):0)+(setup.Attack=="Slide Attack"?V("SlideRange"):0),follow,(raw.Number("ComboDur",5)+V("ComboDuration"))*(1+V("ComboDurationPercent")),V("InitialCombo"),1+V("ComboChance"),Math.Clamp(V("HeavyEfficiency")+(slam&&heavy?V("HeavySlamEfficiency"):0),0,.9),windup,V("Tennokai")>0?.15*(1+V("TennokaiChance")):0,2+V("TennokaiWindow"),V("HealingPerStatus")*statusTypes+(heavy?V("HeavyLifeSteal")*targetHit:0),extra,forced,effects,setup,projectiles);
        if(V("InfiniteCombo")>0)details=details with {ComboDuration=double.PositiveInfinity};
        var duplicateActivation=ArcaneEngine.YellowChance(cc)*V("Duplicate");
        var criticalHealingMean=MeleeStealth.CriticalOnlyMean(cc,cd,stealth)+duplicateActivation*MeleeStealth.CriticalOnlyMean(cc,cd,reveal?0:stealth);
        trace.Add(new("대상 피해 취약도","(1 + 가산 취약 그룹) × 독립 취약 효과 · 상태 피해 중복 증폭 없음",incoming,TargetDebuffs.Source));
        var healing=(policy.HealingReturn?V("HealingPerStatus")*statusTypes*projectiles*(1+duplicateActivation):0)+(V("LifeSteal")+V("NikanaLifeSteal")+(heavy?V("HeavyLifeSteal"):0))*targetHit+V("CriticalLifeSteal")*targetDamage.Values.Sum()*criticalHealingMean*projectiles;
        return new(damage,cc,cd,sc,speed,interval,expected,expected/interval,targetHit/interval,dr,trace.ToArray(),notes.ToArray(),V("Duplicate"),details with {IncomingDamageMultiplier=incoming,Brackets=brackets,EffectiveCombo=finalCombo,Contributions=contributions.ToArray(),Healing=outsideRange?0:healing,StealthBonus=stealth,RevealsOnHit=revealsOnHit,StealthReadyBonus=policy.Stealth?MeleeStealth.Bonus(build,true):0,HitConnects=!outsideRange,Range=policy.RadiusMods?effectRange:details.Range,ForcedProcDamageMultiplier=stanceHit?.ProcDamageMultiplier??1,Policy=policy,AreaRadius=aoe&&effectRange>0?effectRange:null});
    }
    internal static string ProcName(string name)=>name.Replace("unresolved:","") switch {"Knockback"=>"Impact","Weakened"=>"Puncture","Bleed"=>"Slash","Freeze"=>"Cold","Ignite"=>"Heat","Poison"=>"Toxin",var other=>other};
}
