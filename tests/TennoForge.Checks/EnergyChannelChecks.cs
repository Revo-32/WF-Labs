using TennoForge.Core;

internal static class EnergyChannelChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("에너지 채널 상태 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name="Nikana Prime",double energy=100){var id=c.Weapons.Single(w=>w.EnglishName==name).Id;var mod=c.Mods.Single(m=>m.EnglishName=="Energy Channel");var slots=new ModSlot?[8];slots[0]=new(mod.Id,mod.MaxRank);return new(id,slots,new(Enemy:new(1e8,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Conditions:new(){["EnergySpent"]=energy}),Encounter:new(EncounterMode.FiveSeconds));}
        check("에너지 채널:실제 비용100 상한·랭크별 변환·첫 명중 전량 소비·재충전",()=>{
            var state=new EnergyChannelState(25);state.Spend(10);Near(state.StoredEnergy,35);state.Spend(100);Near(state.StoredEnergy,100);
            for(var rank=0;rank<4;rank++)Near(EnergyChannelState.Amount(state.StoredEnergy,.5*(rank+1),new Dictionary<DamageType,double>()),50*(rank+1));
            Near(state.Consume(),100);Near(state.StoredEnergy,0);state.Spend(25);Near(state.StoredEnergy,25);
        });
        check("에너지 채널:치명·프레셔·CO에 곱하지 않고 원소 증폭만·CO 기여 비교 일치",()=>{
            var b=B();var names=new[]{"Pressure Point","True Steel","Fever Strike","Condition Overload"};for(var i=0;i<names.Length;i++){var mod=c.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i+1]=new(mod.Id,mod.MaxRank);}b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=1}}};var full=MeleeEngine.Calculate(c,b);Near(full.Melee!.ExtraDamage,380);var noCo=b with {Slots=b.Slots.ToArray()};noCo.Slots[4]=null;Near(CombatTimeline.WithoutConditionOverloadDps(full),MeleeEngine.Calculate(c,noCo).TargetDps);
        });
        check("에너지 채널 시간 경로:시작 저장량1회·후속 타격0·시전 비용으로 다시1회",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"AbilityEnergySpent",25)]}};var r=CombatTimeline.Calculate(c,b,stances,1);Yes(r.Unsupported.Length==0);var uses=r.Events.Where(e=>e.Kind=="energyChannelUse").ToArray();Yes(uses.Length==2);Near(uses[0].Amount,200);Near(uses[1].Amount,50);Yes(uses[1].Time>.5);
        });
        check("에너지 채널 산탄:바스틸락9산탄 중 단1발·표본 전체 직접 피해 차이200",()=>{
            var b=B("Vastilok");b=b with {Melee=b.Melee! with {Attack="Ranged Attack"},Encounter=new(EncounterMode.FirstHit)};var charged=CombatTimeline.Calculate(c,b,stances,16);var plain=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Conditions=[]}},stances,16);Yes(charged.Unsupported.Length==0);Near(charged.DirectDamage-plain.DirectDamage,200);Yes(charged.Events.Count(e=>e.Kind=="energyChannelUse")==1);
        });
        check("에너지 채널 반복 TTK:산탄·후속 공격·듀플리케이트에 충전을 재사용하지 않음",()=>{
            var baseCalc=MeleeEngine.Calculate(c,B());var fixture=baseCalc with {Damage=new(){[DamageType.Impact]=100},CriticalChance=0,HitInterval=1,Melee=baseCalc.Melee! with {Projectiles=1,ExtraDamage=200}};
            Near(DamageEngine.Simulate(fixture,450,1).Median!.Value,3);
            Near(DamageEngine.Simulate(fixture with {Melee=fixture.Melee! with {Projectiles=9}},1150,1).Median!.Value,2);
            Near(DamageEngine.Simulate(fixture with {CriticalChance=1,CriticalMultiplier=2,DuplicateChance=1},650,1).Median!.Value,2);
        });
        check("에너지 채널 빗나감:전량 유지·지형/허공에 소비한 것으로 처리하지 않음",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Attack="Heavy Slam Attack",Distance=1000},Encounter=new(EncounterMode.FirstHit)};var r=CombatTimeline.Calculate(c,b,stances,1);Yes(r.Unsupported.Length==0);Yes(r.Events.All(e=>e.Kind!="energyChannelUse"));Near(r.DirectDamage,0);
        });
        check("에너지 채널 스탠스:여러 타격·반복 타수에 시작 충전 한 번만 합산",()=>{
            var b=B();var stance=stances.ForWeapon(c.Weapons.Single(w=>w.Id==b.WeaponId)).First(s=>s.Id=="Blind Justice");b=b with {Stance=new(stance.Id,"Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,stance.Id)};
            var charged=StanceEngine.Calculate(c,b,stances);var plain=StanceEngine.Calculate(c,b with {Melee=b.Melee! with {Conditions=[]}},stances);Near(charged.ExpectedComboDamage-plain.ExpectedComboDamage,200);Near(charged.Hits.Sum(h=>h.Count*h.Calculation!.Melee!.ExtraDamage),200);Near(DpsEquation.From(charged.Stats,charged).Evaluate(),charged.TargetDps!.Value);
        });
    }
}
