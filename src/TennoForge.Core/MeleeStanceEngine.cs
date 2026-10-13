namespace TennoForge.Core;

internal static class MeleeStanceEngine
{
    internal static StanceCalculation Calculate(Catalog catalog,Build build,StanceCatalog source)
    {
        var selection=build.Stance??throw new ArgumentException("스탠스 선택 누락");
        if(selection.Revision!=source.Revision)throw new ArgumentException("스탠스 원본 개정 불일치");
        var weapon=catalog.Weapons.Single(w=>w.Id==build.WeaponId);var setup=build.Melee??throw new ArgumentException("확장 계산 조건 누락");
        var stance=source.ForWeapon(weapon).SingleOrDefault(s=>s.Id==selection.Id)??throw new ArgumentException("무기와 스탠스가 호환되지 않습니다.");
        var combo=stance.Combos.SingleOrDefault(c=>c.Key==selection.ComboKey)??throw new ArgumentException("스탠스 동작 식별자 오류");
        if(combo.FinisherKind is not (null or "Ground")&&combo.Unsupported.Length>0)throw new CombatRuleUnavailableException(combo.InputLabel+": "+string.Join(" / ",combo.Unsupported));
        var action=combo.ActionKind;
        if(ShieldThrowMechanic.IsCobra(weapon.Id)&&action=="Aerial")throw new CombatRuleUnavailableException("코브라 & 크레인 공중 방패의100% 타격·넉다운은 원문에 있지만 별도 투사체의 치명/상태 확률 항목은 없습니다. 일반 공중 베기3타로 대체하지 않습니다. "+ShieldThrowMechanic.CobraSource);
        var duration=combo.Duration??setup.ComboDurationOverride;
        if(duration is {} knownDuration)MeleeEngine.Range(knownDuration,.01,120,"동작 전체 시간");
        CapacityEngine.Calculate(catalog,build);
        var rules=MeleeRuleCatalog.Load();var profile=rules.Weapon(weapon.Id);var raw=profile.Raw;
        var uniqueId=UniqueWeaponStances.SourceId(weapon.Id,combo.Key,source.Stances);
        var weaponSpecial=WeaponStanceMechanic.IsSpecial(weapon.Id,combo.Key)||uniqueId is not null||weapon.Id==KeratinosMechanic.WeaponId&&combo.Key=="Heavy Article";
        var coStance=uniqueId??(weaponSpecial?(weapon.Id==WeaponStanceMechanic.QuassusPrime?"Quassus":profile.Name):stance.Id);
        var coRule=combo.AttackOverride is null?rules.StanceCo.SingleOrDefault(r=>r.StanceId==coStance&&r.Combo==combo.Key):null;
        var coFactor=coRule?.Factor??1;
        // The weapon-specific CO table separates blade contact (ordinary
        // additive) and its light/heavy projectiles. Applying an old aggregate
        // stance coefficient again would count the projectile anomaly twice.
        var splitExalted=ExaltedRules.Find(weapon.Id)?.Family is ExaltedFamily.Blade or ExaltedFamily.Wind;
        if(splitExalted)coFactor=1;
        var hitCo=combo.AttackOverride is null?StanceHitCoCatalog.Load().Find(coStance,combo.Key):null;
        var directHits=combo.Hits.Where(h=>h.Multiplier>0&&(h.AttackType!="Slam"||combo.Key=="Slam")).ToArray();
        if(hitCo is not null&&hitCo.Factors.Length!=directHits.Length)throw new ArgumentException("타격별 CO 자료와 스탠스 타격 개수가 다릅니다.");
        var notes=new List<string>();
        if(combo.ResolutionNote is {} resolution)notes.Add(resolution);
        var coActive=build.Slots.Any(s=>s is not null&&rules.Mods.Single(m=>m.Id==s.ModId).Effects?.ContainsKey("ConditionOverload")==true)
            &&TargetStatusRules.Count(build)>0;
        if(coActive&&hitCo is null&&coRule is not null&&coFactor!=1)notes.Add($"위키 CO 표의 콤보 평균 보정 {coFactor:P0}을 사용한 근사값. 이 동작의 타격별 CO 분포는 아직 연결되지 않아 TTK 확률 분포는 제공하지 않습니다.");
        if(hitCo is not null)notes.Add("위키 인용 원본의 타격별 CO 배율 적용 · 강제 프록 순서와 별도로 처리합니다.");
        if(combo.Duration is null)notes.Add(duration is null?"위키에 동작 시간이 없습니다. 타격별 피해·강제 상태는 계산하며 DPS·TTK는 표시하지 않습니다.":$"동작 전체 시간 {duration:0.###}초는 사용자 가정입니다.");
        var hits=new List<StanceHitDamage>();var total=new Dictionary<DamageType,double>();
        var rawExpected=0d;var targetExpected=0d;
        var energyChannel=new EnergyChannelState(setup.Conditions?.GetValueOrDefault("EnergySpent")??0);
        var hitOrdinal=0;
        foreach(var hit in combo.Hits)
        {
            if(hit.Multiplier==0)continue;
            if(hit.Count<1||!double.IsFinite(hit.Multiplier)||hit.Multiplier<0)throw new ArgumentException("스탠스 타격 수치 오류");
            // Wiki stance legend: radial slam entries do not hit the directly struck target.
            if(hit.AttackType=="Slam"&&combo.Key!="Slam")
            {notes.Add("스탠스 표의 방사형 슬램은 직접 타격 대상에게 중복 합산하지 않습니다.");continue;}
            var attack=hit.AttackOverride??combo.AttackOverride??"Normal Attack";var multiplier=hit.Multiplier;
            if(hit.AttackOverride is not null||combo.AttackOverride is not null){/* Weapon component already has its own base damage. */}
            else if(action=="Heavy")
            {
                attack="Heavy Attack";
                var normalTotal=weapon.Damage.Values.Sum();
                var heavyTotal=MeleeClassRules.HeavyDamage(profile);
                if(heavyTotal<=0)throw new ArgumentException("강공격 기본 피해 자료가 없습니다.");
                multiplier/=heavyTotal/normalTotal;
            }
            else if(combo.Key=="Slam")
            {attack="Slam Attack";multiplier/=2;}
            else if(combo.Key=="Slide")
            {
                if(raw.Number("SlideAttack")>0){attack="Slide Attack";multiplier/=raw.Number("SlideAttack")/weapon.Damage.Values.Sum();}
                else notes.Add("개별 무기 슬라이드 피해 항목이 비어 있어 위키 스탠스의 슬라이드 배율을 기본 피해에 직접 적용했습니다.");
            }
            if(hit.AttackType=="Ranged"&&hit.AttackOverride is null)
            {
                if(ExaltedRules.Find(weapon.Id)?.Family is ExaltedFamily.Blade or ExaltedFamily.Wind)
                {attack=action=="Heavy"?"Exalted Heavy Wave":"Exalted Wave";multiplier=hit.Multiplier/(action=="Heavy"?5:1);}
                else
                {
                var options=profile.Attacks.Where(a=>a.Key is not ("Normal Attack" or "Slide Attack" or "Heavy Attack" or "Slam Attack" or "Heavy Slam Attack")&&!a.Key.Contains("Explosion")&&!a.Key.Contains("Pool")&&!a.Key.Contains("AoE")).ToArray();
                if(raw.Text("Class")=="Gunblade")
                {attack=combo.Key=="Heavy"?"Heavy Attack":"Ranged Attack";multiplier=combo.Key=="Heavy"?hit.Multiplier/4:hit.Multiplier;}
                else
                {
                    var option=options.Length==1?options[0]:options.ElementAtOrDefault(Math.Min(hit.Group-1,options.Length-1));
                    if(option is null)throw new ArgumentException("이 스탠스의 투사체 연결 자료가 없습니다.");
                    attack=option.Key;
                    var attackRaw=raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==attack);
                    var actual=attackRaw.GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble())*attackRaw.Number("Multishot",1);
                    multiplier=hit.Multiplier*weapon.Damage.Values.Sum()/actual;
                }
                }
            }
            var conditions=new Dictionary<string,double>(setup.Conditions??[]);
            if(combo.Key=="Aerial")conditions["Airborne"]=1;
            if(combo.Key=="Finisher")conditions["ProneFinisher"]=1;
            var hitBuild=build with {Stance=null,Melee=setup with {Attack=attack,Tennokai=setup.Tennokai&&action=="Heavy",Conditions=conditions,Finisher=combo.FinisherKind},Scenario=build.Scenario with {MotionMultiplier=multiplier,BaseInterval=1}};
            var perHitCo=hitCo?.Factors[hitOrdinal++]??(weaponSpecial?1:coFactor);
            // A wave's explicit weapon CO rule supersedes the aggregate stance correction.
            if(splitExalted||hit.AttackOverride is not null||MeleeEngine.AttackPolicy(weapon.Id,attack).Code=="ExaltedWave")perHitCo=1;
            var c=MeleeEngine.CalculateAttack(catalog,hitBuild,hit,perHitCo,combo.Key=="Finisher",combo.Key=="Slide");
            c=energyChannel.Apply(c);
            void Add(int count,Calculation current)
            {
                hits.Add(new(hit.Group,count,current.Damage,current,hitBuild,hit,perHitCo,splitExalted||hit.AttackOverride is not null||weaponSpecial||combo.AttackOverride is not null||hitCo is not null||coRule?.Factor==1||combo.Key=="Slam"));
                rawExpected+=current.ExpectedHit*count;targetExpected+=current.TargetDps*current.HitInterval*count;
            }
            if(c.Melee!.ExtraDamage>0)
            {Add(1,c);energyChannel.Consume();if(hit.Count>1)Add(hit.Count-1,energyChannel.Apply(c));}
            else Add(hit.Count,c);
            foreach(var (type,value) in c.Damage)total[type]=total.GetValueOrDefault(type)+value*hit.Count*(c.Melee?.Projectiles??1);
        }
        if(hits.Count==0)throw new ArgumentException("이 동작에는 직접 대상 타격이 없습니다.");
        var stats=combo.AttackOverride is not null?hits[0].Calculation!:MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Melee=setup with {Attack="Normal Attack",Tennokai=false,Finisher=combo.FinisherKind},Scenario=build.Scenario with {MotionMultiplier=1,BaseInterval=1}},groundFinisher:combo.Key=="Finisher");
        if(!splitExalted&&!weaponSpecial&&combo.AttackOverride is null&&coRule is null&&hitCo is null&&coActive)notes.Add("위키 CO 표에 이 동작의 보정값이 없습니다. 일반 가산 규칙을 사용한 값이며 해당 조합의 별도 검증은 되지 않았습니다.");
        if(hits.Any(h=>h.Calculation!.Melee!.ExtraDamage>0))notes.Add("에너지 채널의 시작 저장량은 콤보의 첫 명중에만 적용했습니다. 재충전 없이 반복되는 지속 DPS와 구분합니다.");
        var calculatedDuration=duration/stats.AttackSpeed;
        stats=stats with {Limitations=stats.Limitations.Concat(notes.Distinct()).ToArray()};
        return new(stats,stance,combo,hits.ToArray(),total,calculatedDuration,rawExpected,rawExpected/calculatedDuration,targetExpected/calculatedDuration);
    }
}
