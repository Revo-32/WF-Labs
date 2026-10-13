using TennoForge.Core;

internal static class ExaltedChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("익절티드 규칙 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(ExaltedFamily family){var w=ExaltedRules.All.First(w=>w.Family==family);return new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:ExaltedRules.DefaultAttack(w.Id)));}
        ModSlot M(string name,int rank)=>new(c.Mods.Single(m=>m.EnglishName==name).Id,rank);
        check("익절티드21종: 내부 식별자와 어빌리티별 기본 공격 연결",()=>{
            Yes(ExaltedRules.All.Count()==21);foreach(var d in ExaltedRules.All){Yes(c.Weapons.Count(w=>w.Id==d.Id)==1);var b=B(d.Family) with {WeaponId=d.Id,Capacity=CapacityCatalog.Load().Default(d.Id,null)};Yes(MeleeEngine.Calculate(c,b).ExpectedHit>0);}
        });
        check("익절티드21종: 고유 수용량·어빌리티·피해 브라켓 표시식 대조",()=>{
            foreach(var d in ExaltedRules.All){var b=B(d.Family) with {WeaponId=d.Id,Capacity=CapacityCatalog.Load().Default(d.Id,null)};b.Slots[0]=M("Pressure Point",5);b.Slots[1]=M("Molten Impact",5);b=b with {Melee=b.Melee! with {AbilityRank=2,AbilityStrength=1.75},Scenario=b.Scenario with {Combo=7,Enemy=new(1e9,2700)}};var calc=MeleeEngine.Calculate(c,b);Near(DpsEquation.From(calc).Evaluate(),calc.TargetDps);}
        });
        check("발키르 분노: 프레셔와 가산·5초 유예·지수/선형 경계·죽음 방지",()=>{
            var b=B(ExaltedFamily.Talons);b.Slots[0]=M("Pressure Point",5);b=b with {Melee=b.Melee! with {Conditions=new(){["ValkyrRage"]=150}}};Near(MeleeEngine.Calculate(c,b).Damage.Values.Sum(),250*(1+1.2+1.5));
            var state=new ValkyrRageState(300);state.Advance(5);Near(state.Raw,300);state.Advance(5+Math.Log(2)/.016);Near(state.Raw,150);
            state.Advance(5+Math.Log(16)/.016);Near(state.Raw,18.75);state.Advance(6+Math.Log(16)/.016);Near(state.Raw,18.45);Yes(!state.PreventDeath(180));state.Gain(180,150);Yes(state.PreventDeath(180));Near(state.Raw,0);
        });
        check("발키르: 실제 명중 분노·히스테리아 랭크/위력 회복·치명상 사건",()=>{
            var b=B(ExaltedFamily.Talons) with {Melee=new(AbilityRank:2,AbilityStrength:2),Encounter=new(EncounterMode.FiveSeconds)};var r=CombatTimeline.Calculate(c,b,stances,4);
            Yes(r.Unsupported.Length==0);Near(r.Recovery!.HysteriaHealing!.Mean,8*90*2);Near(r.Points.Last().ValkyrRage!.Value,24);
            b=b with {Melee=b.Melee! with {Conditions=new(){["ValkyrRage"]=150}},Encounter=b.Encounter! with {Inputs=[new(.5,"ValkyrFatalHit")]}};r=CombatTimeline.Calculate(c,b,stances,4);Near(r.Points.Last().ValkyrRage!.Value,21);Yes(r.Events.Any(e=>e.Kind=="valkyrDeath"&&e.Description.Contains("전부 소모")));
        });
        check("어빌리티 콤보: 재시전20점·클론 접촉3점·시전 공속 고정",()=>{
            var state=new AbilityComboCastState(5);Yes(!state.Cast(0));Yes(state.Cast(4.9));Yes(!state.Cast(9.9));Near(state.Sequence,1);
            var b=B(ExaltedFamily.Clones) with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"ExaltedAbilityCast"),new(1,"ExaltedAbilityCast")])};var r=CombatTimeline.Calculate(c,b,stances,4);Yes(r.Unsupported.Length==0);Near(r.Points.Last().ComboPoints,35);
            b.Slots[0]=M("Berserker Fury",5);b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"MeleeKill")]}};r=CombatTimeline.Calculate(c,b,stances,4);Near(r.Points.Last().ComboPoints,15);
            b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"MeleeKill"),new(2,"ExaltedAbilityCast")]}};r=CombatTimeline.Calculate(c,b,stances,4);Yes(r.Points.Last().ComboPoints>15);
        });
        check("익절티드: 랭크0~3·위력·프레셔는 별도 배수·무기랭크는 피해에 무관",()=>{
            foreach(var family in Enum.GetValues<ExaltedFamily>())for(var rank=0;rank<=3;rank++)
            {var b=B(family);b.Slots[0]=M("Pressure Point",5);b=b with {Melee=b.Melee! with {AbilityRank=rank,AbilityStrength=2}};var d=ExaltedRules.Find(b.WeaponId)!;Near(MeleeEngine.Calculate(c,b).Damage.Values.Sum(),d.RankDamage[rank]*2*(family==ExaltedFamily.Blade?2.3:2.2));Near(MeleeEngine.Calculate(c,b with {Capacity=b.Capacity! with {WeaponRank=0}}).Damage.Values.Sum(),MeleeEngine.Calculate(c,b).Damage.Values.Sum());}
        });
        check("익절티드: 스탠스10용량·변경 금지·내장 카탈리스트",()=>{
            foreach(var d in ExaltedRules.All.Where(d=>!d.Pseudo)){var b=B(d.Family);Near(CapacityEngine.Calculate(c,b).Total,70);Yes(d.Stance is null||stances.ForWeapon(c.Weapons.Single(w=>w.Id==b.WeaponId)).Single().Id==d.Stance);try{CapacityEngine.Calculate(c,b with {Capacity=b.Capacity! with {Catalyst=false}});throw new Exception("카탈리스트 해제 허용");}catch(ArgumentException){}}
        });
        check("간접 익절티드: 가짜 강공격·슬라이드·엑실러스 차단, 블레이드스톰은 공속1.5상한",()=>{
            foreach(var d in ExaltedRules.All.Where(d=>d.Pseudo)){Yes(!MeleeEngine.Attacks(d.Id).Any(a=>a.Contains("Heavy")||a.Contains("Slide")));}
            var b=B(ExaltedFamily.Clones);b.Slots[0]=M("Primed Fury",10);Near(MeleeEngine.Calculate(c,b).AttackSpeed,1.5);
            var whip=B(ExaltedFamily.Whipclaw);whip.Slots[0]=M("Spoiled Strike",3);Near(MeleeEngine.Calculate(c,whip).AttackSpeed,1);
        });
        check("아이언 스태프: 콤보마다0.5m·12배5.5m·일반 사거리 모드와 가산",()=>{
            var b=B(ExaltedFamily.Staff);Near(MeleeEngine.Calculate(c,b).Melee!.Range,3.5);b=b with {Scenario=b.Scenario with {Combo=12}};Near(MeleeEngine.Calculate(c,b).Melee!.Range,9);b.Slots[0]=M("Primed Reach",10);Near(MeleeEngine.Calculate(c,b).Melee!.Range,12);
        });
        check("익절티드 파동: 라이트CO2가산·강파동CO별도곱·콤보피해 제외",()=>{
            var b=B(ExaltedFamily.Blade);b.Slots[0]=M("Pressure Point",5);b.Slots[1]=M("Condition Overload",5);b=b with {Melee=b.Melee! with {Attack="Exalted Wave",Statuses=new(){[DamageType.Viral]=1}},Scenario=b.Scenario with {Combo=12}};
            Near(MeleeEngine.Calculate(c,b).Damage.Values.Sum(),250*(1+.1+1.2+1.6));Near(MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Attack="Exalted Heavy Wave"}}).Damage.Values.Sum(),1250*(1+.1+1.2)*1.8);
            var wind=B(ExaltedFamily.Wind);Near(MeleeEngine.Calculate(c,wind with {Melee=new(Attack:"Exalted Wave",Distance:21)}).ExpectedHit,0);
        });
        check("섀도우 클론: 원소는 마무리로 변환하지 않고 강제베기에 원소 모드 제외",()=>{
            var b=B(ExaltedFamily.Clones);b.Slots[0]=M("Molten Impact",5);b.Slots[1]=M("Finishing Touch",5);var calc=MeleeEngine.Calculate(c,b);Near(calc.Damage[DamageType.Finisher],1500*1.6);Near(calc.Damage[DamageType.Heat],1500*.9*1.6);Near(calc.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,1500*1.6*.35*1.25*(1+.05*.2));
        });
        check("애쉬: 베기 피해25%·지속50%는 해당 모드와 각각 가산",()=>{
            var b=B(ExaltedFamily.Clones);b.Slots[0]=M("Melee Elementalist",5);b.Slots[1]=M("Lasting Sting",10);var calc=MeleeEngine.Calculate(c,b);var slash=calc.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash);Near(slash.DamagePerTick,1500*.35*(1+.9+.25)*(1+.05*.2));Near(slash.Duration,6*(1+1.1+.5));
        });
        check("핫 샷: 익절티드 무기의 실제 화염은 어빌리티 화염으로 활성화",()=>{
            var b=B(ExaltedFamily.Staff);b.Slots[0]=M("Molten Impact",5);b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Hot Shot").Id,5)),Encounter=new(EncounterMode.FiveSeconds),Scenario=b.Scenario with {BaseInterval=.1}};var r=CombatTimeline.Calculate(c,b,stances,4);Yes(r.Unsupported.Length==0&&r.Points.Last().HotShotStacks>0);
        });
        check("크로마틱 블레이드: 비선형 랭크·위력상확 가산·물리 전환 뒤 원소 조합",()=>{
            var b=B(ExaltedFamily.Blade);b.Slots[0]=M("North Wind",5);b.Slots[1]=M("Drifting Contact",3);b.Slots[2]=M("Jagged Edge",5);
            b=b with {Melee=b.Melee! with {AbilityStrength=1.3,AbilityAugment=new(ExaltedAugments.Chromatic,3),ChromaticElement=DamageType.Heat}};var calc=MeleeEngine.Calculate(c,b);
            Near(calc.StatusChance,.15*(1+3*1.3+.4));Near(calc.Damage[DamageType.Blast],250*1.3*1.1*1.9);Yes(!calc.Damage.ContainsKey(DamageType.Slash));
            for(var rank=0;rank<4;rank++)Near(MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Chromatic,rank)}}).StatusChance,.15*(1+new[]{1d,2,2.5,3}[rank]*1.3+.4));
        });
        check("리액티브 스톰: 개조 후 충격만 전환·충격 모드 적용·기본 원소와 결합 금지",()=>{
            var b=B(ExaltedFamily.Wind);b.Slots[0]=M("Heavy Trauma",5);b.Slots[1]=M("North Wind",5);b.Slots[2]=M("Drifting Contact",3);
            b=b with {Melee=b.Melee! with {AbilityStrength=1.3,Faction="Infested",AbilityAugment=new(ExaltedAugments.Reactive,3)}};var calc=MeleeEngine.Calculate(c,b);
            Near(calc.Damage[DamageType.Heat],250*1.3*1.9);Near(calc.Damage[DamageType.Cold],250*1.3*.9);Yes(!calc.Damage.ContainsKey(DamageType.Blast));Near(calc.StatusChance,.15*(1+2.5*1.3+.4));
        });
        check("인레이지드: 프레셔/치확 가산·위력 미적용·강공격에 개조 치확 두 배 없음",()=>{
            var b=B(ExaltedFamily.Talons);b.Slots[0]=M("Pressure Point",5);b.Slots[1]=M("True Steel",5);b=b with {Melee=b.Melee! with {AbilityStrength=2,AbilityAugment=new(ExaltedAugments.Enraged,3)}};
            var calc=MeleeEngine.Calculate(c,b);Near(calc.Damage.Values.Sum(),250*2*(1+1.2+3.5));Near(calc.CriticalChance,.5*(1+1.2+3.5));Near(MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Attack="Heavy Attack"}}).CriticalChance,.5*(1+2.4+3.5));
            b=b with {Melee=b.Melee! with {AbilityDuration=.1},Encounter=new(EncounterMode.FiveSeconds)};var result=CombatTimeline.Calculate(c,b,stances,4);Yes(result.Unsupported.Length==0);Near(result.Recovery!.HysteriaHealing!.Mean,3*100*2);
        });
        check("개조 누적 보너스: 획득 갱신·상한·유예 후 초당 감소·지속 역수",()=>{
            var p=new DelayedDecayBonus(150,.5,5,149);p.Gain(0,30);Near(p.Value,150);p.Advance(7);Near(p.Value,149);p.Gain(7,15);p.Advance(12);Near(p.Value,150);p.Advance(14);Near(p.Value,149);
            var w=new DelayedDecayBonus(350,2.5,10,350);w.Advance(24);Near(w.Value,315);w.Reset(24);Near(w.Value,0);
        });
        check("랜드슬라이드·윕클로: 반복 입력별 재시전20콤보·랭크별 반경",()=>{
            var b=B(ExaltedFamily.Landslide) with {Encounter=new(EncounterMode.FiveSeconds)};var result=CombatTimeline.Calculate(c,b,stances,4);Yes(result.Unsupported.Length==0);Near(result.Points.Last().ComboPoints,85);
            for(var n=1;n<=3;n++){var calc=MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {AbilityRange=2,Conditions=new(){["AbilityComboSequence"]=n}}});Near(calc.Melee!.AreaRadius!.Value,(2+2*n)*2);}
            var whip=B(ExaltedFamily.Whipclaw);whip.Slots[0]=M("Primed Reach",10);whip=whip with {Melee=whip.Melee! with {AbilityRange=3}};Near(MeleeEngine.Calculate(c,whip).Melee!.AreaRadius!.Value,13);Yes(!MeleeEngine.Calculate(c,whip with {Melee=whip.Melee! with {Distance=13.1}}).Melee!.HitConnects);
        });
        check("윕클로 개조:3명 실제 방사 명중·현재 시전에는 소급하지 않음·한 번씩 적립",()=>{
            var b=B(ExaltedFamily.Whipclaw) with {Melee=new(AbilityAugment:new(ExaltedAugments.Accumulating,3)),Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",1),new("third",2),new("fourth",3)])};var result=CombatTimeline.Calculate(c,b,stances,4);
            Yes(result.Unsupported.Length==0);var events=result.Events.Where(e=>e.Kind=="whipclawAugment").ToArray();Yes(events.Length==5);Near(events[0].Amount,35);Near(events[^1].Amount,175);
            var one=CombatTimeline.Calculate(c,b with {Encounter=b.Encounter! with {Mode=EncounterMode.FirstHit}},stances,4);var plain=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {AbilityAugment=null},Encounter=b.Encounter! with {Mode=EncounterMode.FirstHit}},stances,4);Near(one.DirectDamage,plain.DirectDamage);
        });
        check("라이징 스톰: 클론 기본3+위력 적용 보너스를 추가콤보확률에 함께 적용",()=>{
            var b=B(ExaltedFamily.Clones);b.Slots[0]=M("True Punishment",3);b=b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Rising,3),AbilityStrength=2},Encounter=new(EncounterMode.FiveSeconds)};
            Near(MeleeEngine.Calculate(c,b).Melee!.ComboDuration,7.5);var result=CombatTimeline.Calculate(c,b,stances,4);Yes(result.Unsupported.Length==0);Near(result.Points.Last().ComboPoints,110);
        });
        check("러블 힙: 랭크 보너스는 위력과 가산·발동 여부·사거리만 증가",()=>{
            var b=B(ExaltedFamily.Landslide) with {Melee=new(AbilityStrength:2,AbilityAugment:new(ExaltedAugments.Rubble,3),Conditions:new(){["RubbleHeap"]=1})};Near(MeleeEngine.Calculate(c,b).Damage.Values.Sum(),350*3);Near(ExaltedGeometry.Reach(ExaltedRules.Find(b.WeaponId)!,b.Melee!)!.Value,24);Near(MeleeEngine.Calculate(c,b).Melee!.AreaRadius!.Value,4);
        });
        check("익절티드 블레이드: 강공격 입력마다 파동2개·검만 콤보·파동은 별도 CO",()=>{
            var b=B(ExaltedFamily.Blade);b=b with {Stance=new("Exalted Blade","Heavy",stances.Revision),Scenario=b.Scenario with {Combo=12}};var calc=StanceEngine.Calculate(c,b,stances);Near(calc.ComboDamage.Values.Sum(),250*1.1*(10*12+20));Yes(calc.Hits.Count(h=>h.Calculation!.Melee!.Attack=="Exalted Heavy Wave")==2&&calc.Hits.Where(h=>h.Calculation!.Melee!.Attack=="Exalted Heavy Wave").All(h=>h.Count==2));
            b.Slots[0]=M("Condition Overload",5);b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=1}}};calc=StanceEngine.Calculate(c,b,stances);Near(calc.ComboDamage.Values.Sum(),250*(1+.1+.8)*10*12+250*1.1*20*1.8);Yes(calc.Hits.All(h=>h.CoVerified));
        });
        check("익절티드 블레이드 슬라이드: 검+파동 연결·범위 밖은 콤보/회복도 미발동",()=>{
            var b=B(ExaltedFamily.Blade) with {Stance=new("Exalted Blade","Slide",stances.Revision)};var calc=StanceEngine.Calculate(c,b,stances);Near(calc.ComboDamage.Values.Sum(),250*1.1*4);Yes(calc.Hits.Length==2);
            var wind=B(ExaltedFamily.Wind) with {Melee=new(Attack:"Exalted Wave",Distance:21),Encounter=new(EncounterMode.FiveSeconds)};var result=CombatTimeline.Calculate(c,wind,stances,4);Near(result.DirectDamage,0);Near(result.Points.Last().ComboPoints,0);
        });
        check("데저트 윈드: 접촉CO100%·파동0%를 분리하면 정지 집계60%와 일치",()=>{
            var b=B(ExaltedFamily.Wind);b.Slots[0]=M("Condition Overload",5);b=b with {Stance=new("Serene Storm","Neutral",stances.Revision),Melee=new(Statuses:new(){[DamageType.Viral]=1})};var calc=StanceEngine.Calculate(c,b,stances);Near(calc.ComboDamage.Values.Sum(),250*(6*1.8+4));Yes(calc.Hits.All(h=>h.CoVerified));
            var wave=MeleeEngine.Calculate(c,b with {Stance=null,Melee=b.Melee! with {Attack="Exalted Wave"}});Near(wave.Damage.Values.Sum(),250);
        });
        check("스탠스 평균 타격 피해: 직접 대상에서 제외한 방사 타격은 분모에도 제외",()=>{
            var b=B(ExaltedFamily.Talons) with {Stance=new("Hysteria","Heavy",stances.Revision)};var result=StanceEngine.Calculate(c,b,stances);Yes(result.Hits.Sum(h=>h.Count)==5&&result.Combo.HitCount==6);Near(result.ExpectedHit,250*3*1.5);
        });
    }
}
