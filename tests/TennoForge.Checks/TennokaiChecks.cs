using TennoForge.Core;

internal static class TennokaiChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name,string exilus,int rank)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var m=catalog.Mods.Single(m=>m.EnglishName==exilus);var pp=catalog.Mods.Single(m=>m.EnglishName=="Pressure Point");
            return new(w.Id,[new(pp.Id,pp.MaxRank),null,null,null,null,null,null,null],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:"Heavy Attack",Tennokai:true,Exilus:new(m.Id,rank)));
        }
        check("마스터즈 엣지:프레셔120%+텐노카이60%는2.8배·별도1.6배 아님",()=>{
            var b=B("Nikana Prime","Master's Edge",5);var c=MeleeEngine.Calculate(catalog,b);Near(c.Melee!.Brackets!.AdditiveDamage,1.8);Near(c.Melee.Brackets.TennokaiMultiplier,1);
            var off=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Tennokai=false}});Near(c.ExpectedHit/off.ExpectedHit,2.8/2.2);Near(DpsEquation.From(c).Evaluate(),c.TargetDps);
            var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");b.Slots[1]=new(co.Id,co.MaxRank);b=b with {Scenario=b.Scenario with {ExistingStatusTypes=2}};var both=MeleeEngine.Calculate(catalog,b);Near(both.Melee!.Brackets!.AdditiveDamage,1.8);Near(both.Melee.Brackets.AdditiveConditionOverload,1.6);
        });
        check("트루스 플레임:랭크0/1/2/3의30/40/60/120%·성공 전 보너스 없음",()=>{
            var values=new[]{.3,.4,.6,1.2};for(var rank=0;rank<=3;rank++)
            {
                var b=B("Nikana Prime","Truth's Flame",rank);Near(MeleeEngine.Calculate(catalog,b).Melee!.Brackets!.AdditiveDamage,1.2);
                b=b with {Melee=b.Melee! with {Conditions=new(){["TruthFlame"]=1}}};var c=MeleeEngine.Calculate(catalog,b);Near(c.Melee!.Brackets!.AdditiveDamage,1.2+values[rank]);Near(c.Melee.Brackets.TennokaiMultiplier,1);
                Near(c.Melee.Contributions!.Single(x=>x.Effect=="TennokaiDamage").Amount,values[rank]);
            }
        });
        check("디시플린즈 메리트:랭크별7/6/5/4입력·일반 선형 효과로 축소하지 않음",()=>{
            for(var rank=0;rank<=3;rank++){var c=MeleeEngine.Calculate(catalog,B("Nikana Prime","Discipline's Merit",rank));Near(c.Melee!.Effects["TennokaiEvery"],7-rank);}
        });
        check("범용 피해 전용 성분:텐노카이 피해를 근접 피해 브라켓 밖으로 우회하지 않음",()=>{
            var b=B("Ceramic Dagger","Master's Edge",5);var attack=MeleeEngine.Attacks(b.WeaponId).First(a=>a.StartsWith("Spectral")&&!a.Contains("Explosion"));
            b=b with {Melee=b.Melee! with {Incarnon=true,Attack=attack}};var withMod=MeleeEngine.Calculate(catalog,b);var withoutMod=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Exilus=null}});Near(withMod.ExpectedHit,withoutMod.ExpectedHit);
        });
        void Yes(bool v){if(!v)throw new Exception("텐노카이 전이 불일치");}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("오퍼튜니티즈 리치:랭크별 총2.8/3.2/3.6/4초·기본2초 중복 없음",()=>{
            for(var rank=0;rank<4;rank++){var c=MeleeEngine.Calculate(catalog,B("Nikana Prime","Opportunity's Reach",rank));Near(c.Melee!.TennokaiWindow,2.8+.4*rank);}
        });
        check("텐노카이 입력 계수:검 다중타격/다중적 1회·건블레이드 연사와 적은 별도·산탄은 중복 제외",()=>{
            var sword=new TennokaiState(.15,4,2);for(var hit=0;hit<12;hit++)sword.Contact(hit*.01,0,hit,hit%2,false,true,0);Near(sword.Progress,1);Near(sword.Generated,0);
            for(var input=1;input<=3;input++)sword.Contact(input,input,0,0,false,true,0);Near(sword.Generated,1);
            var gun=new TennokaiState(.15,4,2);for(var pellet=0;pellet<10;pellet++)gun.Contact(0,0,0,0,true,true,0);Near(gun.Progress,1);
            gun.Contact(.1,0,0,1,true,true,0);gun.Contact(.2,0,1,0,true,true,0);Yes(gun.Contact(.2,0,1,1,true,true,0));Near(gun.Generated,1);
            var profile=MeleeRuleCatalog.Load().Weapon(catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id);Yes(!TennokaiState.Generates(profile,"Heavy Attack")&&!TennokaiState.Generates(profile,"Slam Attack"));
        });
        check("텐노카이 기본확률·2초 경계·열린 기회 갱신 미기재를 확정값으로 치환하지 않음",()=>{
            var state=new TennokaiState(.15,0,2);Yes(!state.Contact(0,0,0,0,false,true,.15));Yes(state.Contact(1,1,0,0,false,true,.149));Near(state.RemainingMinimum(1),2);Yes(!state.Begin(3));
            state.Contact(4,2,0,0,false,true,0);state.Contact(5,3,0,0,false,true,0);Near(state.EndMinimum,6);Near(state.EndMaximum,7);
            try{state.Begin(6.5);throw new Exception("불확실한 유지 시간 사용을 허용함");}catch(CombatRuleUnavailableException){}
        });
        check("트루스 연쇄:첫 사용 피해보너스 없음·처치 후4초 반환·다음 사용만 강화·실패 총600",()=>{
            var s=new TennokaiState(.15,0,2,3);s.StartingOpportunity(2);Yes(s.Begin(0)&&!s.ActiveBonus);Yes(s.Kill(.2,true));Near(s.EndMinimum,4.2);Near(s.Finish(.4),0);
            Yes(s.Begin(.5)&&s.ActiveBonus);Near(s.Finish(1),600);Near(s.Failures,1);Near(s.Refunds,1);Yes(!s.Begin(1.1));
        });
        check("트루스 저주:랭크별150/210/300/600·면역과 즉시 취소·효율 무시 콤보 초기화",()=>{
            for(var rank=0;rank<4;rank++){var s=new TennokaiState(.15,0,2,rank);s.StartingOpportunity(2);s.Begin(0);Near(s.Finish(.1),new[]{150,210,300,600}[rank]);}
            var immune=new TennokaiState(.15,0,2,3);immune.StartingOpportunity(2);immune.Begin(0);Near(immune.Finish(.1,statusImmune:true),0);Near(immune.Failures,1);
            var cancel=new TennokaiState(.15,0,2,3);cancel.StartingOpportunity(2);cancel.Begin(0);Near(cancel.Finish(0,cancel:true),0);Near(cancel.Failures,0);
            var combo=new MeleeComboCounter(220,12,20,12,5);Near(combo.Reset(0),220);Near(combo.Points,0);combo.Advance(1);Near(combo.Points,40);
        });
        check("시간 교전:처음 텐노카이 1회만 무료·반복 강공격을 무한 무료로 계산하지 않음",()=>{
            var b=B("Nikana Prime","Master's Edge",5);b=b with {Scenario=b.Scenario with {Combo=4,BaseInterval=1},Encounter=new(EncounterMode.FiveSeconds)};var r=Run(b);var spent=r.Events.Where(e=>e.Kind=="heavyCombo").ToArray();Near(spent[0].Amount,0);Yes(spent[1].Amount>0);Near(r.Points.Last().TennokaiUses,1);
        });
        check("시간 교전:디시플린4입력→명시한 준비시각에 사용→다음 강공격 콤보 보존",()=>{
            var b=B("Nikana Prime","Discipline's Merit",3);b=b with {Scenario=b.Scenario with {BaseInterval=.216,Combo=4},Melee=b.Melee! with {Attack="Normal Attack",Tennokai=false},Encounter=new(EncounterMode.FiveSeconds,AttackOrder:["Normal Attack","Normal Attack","Normal Attack","Normal Attack","Heavy Attack"],Inputs:[new(.7,"TennokaiHeavyInput")])};
            var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="tennokaiOpportunity"&&Math.Abs(e.Time-3*MeleeEngine.Calculate(catalog,b).HitInterval)<1e-8));var heavy=r.Events.Where(e=>e.Kind=="heavyCombo").ToArray();Near(heavy[0].Amount,0);Yes(heavy[1].Amount>0);Near(r.Points.Last().TennokaiUses,1);
        });
        check("시간 교전:트루스 실제 처치 반환·다음 실패 콤보초기화·자기 피해를 적 DPS에 더하지 않음",()=>{
            var b=B("Nikana Prime","Truth's Flame",3);b=b with {Scenario=b.Scenario with {BaseInterval=2,Combo=4,Enemy=new(1,0)},Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",1,Defense:new(1e9,0))],TargetOrder:[0,1],Inputs:[new(.2,"TennokaiFinish"),new(.3,"TennokaiHeavyInput"),new(2,"TennokaiFinish")])};
            var r=Run(b);Near(r.Points.Last().TennokaiRefunds,1);Near(r.Points.Last().TennokaiFailures,1);Near(r.Points.Last().TennokaiRawCurse,600);Near(r.Points.Single(p=>p.Time==2).ComboPoints,0);
            var immune=Run(b with {Melee=b.Melee! with {Conditions=new(){["StatusImmune"]=1}}});Near(immune.Points.Last().TennokaiFailures,1);Near(immune.Points.Last().TennokaiRawCurse,0);Near(immune.Enemies![1].DirectDamage,r.Enemies![1].DirectDamage);
        });
    }
}
