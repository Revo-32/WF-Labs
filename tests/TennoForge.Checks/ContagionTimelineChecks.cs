using TennoForge.Core;

internal static class ContagionTimelineChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var original=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");
        var catalog=original with {Weapons=original.Weapons.Select(w=>w.Id==id?w with {Damage=new(){[DamageType.Impact]=100},CriticalChance=0,StatusChance=0}:w).ToArray()};
        var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Exodia:new(exodia.Id,3)),Encounter:new(EncounterMode.TenSeconds,Contagion:[new(0)],ScriptedAttacksOnly:true));
        CombatTimelineResult C(Build b,Catalog? c=null)=>CombatTimeline.Calculate(c??catalog,b,stances,1);
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("컨테이전 시간 전이 불일치");}
        void Reject(Action action,string text){try{action();}catch(ArgumentException e){Yes(e.Message.Contains(text));return;}throw new Exception("잘못된 조건을 허용");}
        check("컨테이전 비행:명중 후1~4초·5초 폭발·직격 즉사 계속 비행·번호/거리 검증",()=>{
            var state=new ContagionFlightState();var hit=state.Contact(new(.25,Travel:30),2);state.ResolveContact(hit,true);
            Near(state.NextTime,1.25);Yes(state.Due(1.249).Length==0);var ticks=state.Due(5.25);Yes(ticks.Length==5);Yes(ticks.Select(e=>e.Time).SequenceEqual(new[]{1.25,2.25,3.25,4.25,5.25}));Yes(ticks.Take(4).All(e=>e.Phase==ContagionPhase.Embedded)&&ticks[4].Phase==ContagionPhase.Explosion);
            Reject(()=>state.Contact(new(6),2),"이미 부착");var pass=new ContagionFlightState();pass.ResolveContact(pass.Contact(new(0,Travel:15),2),false);Yes(double.IsPositiveInfinity(pass.NextTime));
            Reject(()=>pass.Contact(new(1,Target:2,Travel:14),2),"누적 이동");var next=pass.Contact(new(1,Target:2,Travel:30),2);pass.ResolveContact(next,true);Near(pass.NextTime,2);
            Reject(()=>ContagionFlightState.Validate(new(double.NaN),2),"접촉 시각");Reject(()=>ContagionFlightState.Validate(new(0,Target:3),2),"대상 번호");
        });
        check("컨테이전 전투:직격200+부착100×4+폭발500·시점별 강제 충격·자동 검 타격 없음",()=>{
            var r=C(B());Yes(r.Unsupported.Length==0);Near(r.DirectDamage,1100);Near(r.StatusDamage,0);Near(r.Points.Last().ComboPoints,0);
            var phases=r.Events.Where(e=>e.Kind=="contagionPhase").ToArray();Yes(phases.Select(e=>e.Time).SequenceEqual(new[]{0d,1,2,3,4,5}));Yes(r.Events.Count(e=>e.Kind=="hit")==6);Yes(r.Events.Count(e=>e.Kind=="proc"&&e.Description.Contains("충격"))==5);
            var final=C(B() with {Encounter=B().Encounter! with {Mode=EncounterMode.FiveSeconds}});Near(final.DirectDamage,1100);
            var equation=DpsEquation.FromTimeline(r);Near(equation.Result,110);Near(equation.Evaluate(),110);Yes(equation.Context!.Contains("교전")&&!r.HasSnapshotDps&&r.Points.All(p=>p.SnapshotDps==0));
        });
        check("컨테이전 전투:오버가드300 소진 후 방어력90%·위치별 중심/외부 폭발",()=>{
            var b=B();var layered=C(b with {Scenario=b.Scenario with {Enemy=new(1e9,2700)},Melee=b.Melee! with {Overguard=300}});Near(layered.DirectDamage,380);Near(layered.Points.Last().Overguard,0);
            var surface=b with {Encounter=b.Encounter! with {Contagion=[new(0,Surface:true)],NearbyEnemies=[new("center",0,Defense:new(100000,0)),new("outside",8.01,Defense:new(100000,0))]}};
            var blast=C(surface);Near(blast.DirectDamage,500);Near(blast.Enemies!.Single(e=>e.Id=="center").DirectDamage,500);Near(blast.Enemies!.Single(e=>e.Id=="outside").DirectDamage,0);Yes(blast.Events.All(e=>e.Kind!="contagionAttached"));
            Reject(()=>C(surface with {Encounter=surface.Encounter! with {NearbyEnemies=[new("unknown",4,Defense:new(100000,0))]}}),"감쇠율");
        });
        check("컨테이전 전투:즉사 후 같은 투사체가 다른 적에 부착·처치 원래 대상 추가 피해 없음",()=>{
            var b=B();var r=C(b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("next",0,Defense:new(100000,0))],Contagion=[new(0,Travel:1),new(.5,Target:2,Travel:2)]}});
            Near(r.DirectDamage,1);Near(r.Enemies!.Single(e=>e.Id=="next").DirectDamage,1100);Yes(r.Events.Count(e=>e.Kind=="contagionContinue")==1&&r.Events.Count(e=>e.Kind=="contagionAttached")==1);
        });
        check("컨테이전 전투:듀플리케이트가 피해만 추가하고 부착/폭발 예약을 복제하지 않음",()=>{
            var critical=catalog with {Weapons=catalog.Weapons.Select(w=>w.Id==id?w with {CriticalChance=1,CriticalMultiplier=2}:w).ToArray()};
            var duplicate=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Duplicate");var r=C(B() with {Arcanes=new(Melee:new(duplicate.Id,5))},critical);
            Near(r.DirectDamage,4400);Yes(r.Events.Count(e=>e.Kind=="contagionPhase")==6&&r.Events.Count(e=>e.Kind=="contagionAttached")==1);
        });
        check("컨테이전 전투:부착 중 버프 변화의 미기재 저장 규칙을 일반 근접 DPS로 숨기지 않음",()=>{
            var b=B();var arachne=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne");b=b with {Arcanes=new(Warframe1:new(arachne.Id,5)),Encounter=b.Encounter! with {Inputs=[new(.5,"WallLatch")]}};
            Reject(()=>C(b),"저장/실시간");var r=EncounterEngine.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Any(s=>s.Contains("저장/실시간"))&&r.AverageDps is null);
        });
        check("컨테이전 전투:폭발 순간 강공격 동작·킬링 블로우·라이프 스트라이크·부착 제외",()=>{
            var b=B();var kb=catalog.Mods.Single(m=>m.EnglishName=="Killing Blow");var life=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");b.Slots[0]=new(kb.Id,kb.MaxRank);b.Slots[1]=new(life.Id,life.MaxRank);
            b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"HeavyAnimationStart"),new(1.5,"HeavyAnimationEnd"),new(4.5,"HeavyAnimationStart"),new(5.5,"HeavyAnimationEnd")]}};
            var result=C(b);Near(result.DirectDamage,200+400+500*2.2);Near(result.Recovery!.HeavyHealing!.Mean,500*2.2*.2);Near(result.Points.Last().ComboPoints,0);
        });
        check("컨테이전 폭발:방사형 실드 게이트·같은 순간 듀플리케이트·0.1초 이후 해제",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Shields=100},Encounter=b.Encounter! with {Contagion=[new(0,Surface:true),new(.05,2,Surface:true),new(.1,3,Surface:true)]}};
            var result=C(b);Near(result.DirectDamage,600);Near(result.Points.Last().Shields,0);Near(result.Points.Last().Health,1e9-500);
        });
    }
}
