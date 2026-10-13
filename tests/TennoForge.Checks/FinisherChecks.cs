using TennoForge.Core;

internal static class FinisherChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("마무리 검증 불일치");}
        void Near(double actual,double expected){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{actual} != {expected}");}
        Build B(string name,string kind,params string[] names)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var s=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key==kind));var mods=new ModSlot?[8];
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);mods[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,mods,new(Enemy:new(1e9,2700)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,s.IsDefault?null:s.Id),Stance:new(s.Id,kind,stances.Revision),Melee:new(),Encounter:new(EncounterMode.StanceCycle));
        }
        StanceCalculation C(Build b)=>StanceEngine.Calculate(catalog,b,stances);
        check("마무리 원본: 30개 무기군/고유무기·119동작·원문 충돌4건을 분리",()=>{
            Yes(FinisherCatalog.Source.Source.Revision==2812039&&FinisherCatalog.Source.Rules.Length==119&&FinisherCatalog.Source.Rules.Count(r=>r.Issue.Length>0)==4);
            foreach(var r in FinisherCatalog.Source.Rules.Where(r=>r.Issue.Length==0))Yes(r.Hits.Length>0&&r.Hits.All(h=>h>0));
        });
        check("니카나 앞 마무리: 8배2타·자동 은신+700%·방어력 무시·DPS 생성 안 함",()=>{
            var c=C(B("Nikana Prime","Finisher Front"));Near(c.ExpectedComboDamage,198*16*(8+.28*1.4));Near(c.TargetDamage,c.ExpectedComboDamage);
            Yes(c.Hits.Length==2&&c.Hits.All(h=>h.Damage.Count==1&&h.Damage.ContainsKey(DamageType.Finisher)&&h.Calculation!.StatusChance==0));
            Yes(c.Duration is null&&c.TargetDps is null&&DpsEquation.From(c.Stats,c).DamageOnly);Near(DpsEquation.From(c.Stats,c).Evaluate(),c.TargetDamage);
        });
        check("앞 마무리 원소·물리 모드 50%: 피해·치명타·팩션은 원래 브라켓",()=>{
            var b=B("Nikana Prime","Finisher Front","Pressure Point","Fever Strike","Buzz Kill","Organ Shatter");var c=C(b);
            var sum=198*2.2*(1+.9/2+.9*1.2/2);Near(c.ComboDamage[DamageType.Finisher],sum*16);Near(c.ExpectedComboDamage,sum*16*(8+.28*(2.4*1.9-1)));
            var ground=C(b with {Stance=b.Stance! with {ComboKey="Finisher"}});Near(ground.ComboDamage[DamageType.Finisher],198*2.2*(1+.9+.9*1.2)*10);
        });
        check("마무리 공속: 앞·뒤·은신도 기본 공속 제외·시큐라 전용 보너스",()=>{
            var b=B("Secura Lecta","Finisher Front");var c=C(b with {Melee=new(ComboDurationOverride:3)});Near(c.Stats.AttackSpeed,1.5);Near(c.Duration!.Value,2);
            var n=B("Nikana Prime","Finisher Back","Fury");var q=C(n with {Melee=new(ComboDurationOverride:3)});Near(q.Stats.AttackSpeed,1.3);Near(q.Duration!.Value,3/1.3);
        });
        check("40랭크 무기: 일반 은신과 마무리의 랭크 정규화를 구분",()=>{
            var b=B("Paracesis","Finisher Front");var c=C(b with {Capacity=b.Capacity! with {WeaponRank=20}});Near(c.Stats.Melee!.StealthBonus,4);
            var normal=MeleeEngine.Calculate(catalog,b with {Stance=null,Capacity=b.Capacity! with {WeaponRank=20},Melee=new(Conditions:new(){["Invisible"]=1})});Near(normal.Melee!.StealthBonus,5);
        });
        check("밀리 다우티: 마무리의 상태 발생 금지·피해 변환이 무기 치명 배수를 지우지 않음",()=>{
            var b=B("Nikana Prime","Finisher Front","Melee Prowess","Fever Strike","Auger Strike");var id=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Doughty").Id;b=b with {Arcanes=new(Melee:new(id,5))};
            var c=C(b);var normal=MeleeEngine.Calculate(catalog,b with {Stance=null});Yes(normal.CriticalMultiplier>2.4);Near(c.Stats.CriticalMultiplier,normal.CriticalMultiplier);Near(c.Stats.StatusChance,0);
        });
        check("럼블잭: 0.4배 찌르기 강제 전기 후10배 마무리, 원소 결합해도 강제 전기 유지",()=>{
            var c=C(B("Rumblejack","Finisher Front","Fever Strike"));Yes(c.Hits.Length==2);Near(c.Hits[0].SourceHit!.Multiplier,.4);Near(c.Hits[1].SourceHit!.Multiplier,10);
            Yes(c.Hits[0].Calculation!.Melee!.ForcedProcs.Contains("Electricity")&&!c.Hits[1].Calculation!.Melee!.ForcedProcs.Contains("Electricity"));
            Yes(c.Hits.All(h=>h.Calculation!.StatusChance==0));
        });
        check("레이피어: 뒤 마무리8배3타·각 강제 베기의 피해 기반4배",()=>{
            var b=B("Destreza","Finisher Back");var c=C(b);Yes(c.Hits.Length==3&&c.Hits.All(h=>h.Calculation!.Melee!.ForcedProcs.Contains("Slash")));
            var weapon=catalog.Weapons.Single(w=>w.Id==b.WeaponId);
            foreach(var h in c.Hits)Near(h.Calculation!.Melee!.StatusDamage.Single(p=>p.Type==DamageType.Slash).DamagePerTick,weapon.Damage.Values.Sum()*4*.35*(8+weapon.CriticalChance*(weapon.CriticalMultiplier-1)));
        });
        check("마무리 표 충돌: 스파링 뒤·주먹 뒤를 임의 타수로 계산하지 않음",()=>{
            foreach(var name in new[]{"Hirudo","Ankyros"})
            {try{C(B(name,"Finisher Back"));throw new Exception("원문 타격 충돌을 허용");}catch(CombatRuleUnavailableException){}}
        });
        check("마무리 시간 경로: 재노출 없이 한 번만, 투명 상태 접촉 대기가 마무리 보너스를 지우지 않음",()=>{
            var b=B("Nikana Prime","Finisher Front");b=b with {Melee=new(ComboDurationOverride:2,Conditions:new(){["Invisible"]=1,["StealthCooldown"]=3}),Encounter=new(EncounterMode.TenSeconds,HitTimes:[0,1])};
            var c=C(b);Near(c.Stats.Melee!.StealthBonus,7);
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="hit")==2);Yes(!t.Events.Any(e=>e.Kind=="stealthCooldown"));
        });
        check("DT_FINISHER와 DT_HEALTH_DRAIN: 실드 차단·우회를 구분",()=>{
            var b=B("Nikana Prime","Finisher Front");var baseHit=MeleeEngine.Calculate(catalog,b with {Stance=null,Melee=new(Shields:1000)});
            Calculation Hit(DamageType type)=>baseHit with {Damage=new(){[type]=100},CriticalChance=0,HitInterval=1};
            Near(DamageEngine.Simulate(Hit(DamageType.Finisher),50,1,horizon:1.1).KillFraction,0);
            Near(DamageEngine.Simulate(Hit(DamageType.True),50,1,horizon:1.1).KillFraction,1);
            Near(MeleeTargetModel.Multiplier(DamageType.Finisher,2700,new()),1);Near(MeleeTargetModel.Multiplier(DamageType.True,2700,new(Shields:1000)),1);
            Yes(!DamageTypeRules.HasStatus(DamageType.Finisher)&&!DamageTypeRules.HasStatus(DamageType.True));
        });
        check("베기 상태 피해는 마무리 유형: 방어력 무시·실드는 우회하지 않음",()=>{
            var b=B("Nikana Prime","Finisher Front");b=b with {Stance=null,Scenario=new(BaseInterval:60,Enemy:new(1e9,2700)),Melee=new(Attack:"Heavy Attack",Shields:1e9),Encounter=new(EncounterMode.FiveSeconds)};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.StatusDamage>0);Near(t.Points.Last().Health,1e9);Yes(t.Points.Last().Shields<1e9);
        });
    }
}
