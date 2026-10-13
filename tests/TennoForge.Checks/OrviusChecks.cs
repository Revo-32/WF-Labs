using System.Text.Json;
using TennoForge.Core;

internal static class OrviusChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=OrviusHoverState.WeaponId;
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,OrviusHovers:[new(0,.1,Detonate:2,Returned:2.2,TickTimes:[.2,1.2,2.2],UseInfobox:true)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("오르비우스 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 오르비우스 입력 허용: "+text);}
        CombatTimelineResult Run(Build b,int trials=1){var r=CombatTimeline.Calculate(catalog,b,stances,trials);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("오르비우스 상태:처음 오버가드 판정 유지·3초·수동기폭 후 틱취소·회수",()=>{
            var s=new OrviusHoverState();var h=new OrviusHover(0,.1,Detonate:2,Returned:2.2,TickTimes:[.2,1.2,2.2],UseInfobox:true);s.Launch(h);s.Attach(1,true);Yes(!s.Suspends(1,.2)&&!s.Suspends(1,1.9));Yes(s.Tick(1,1.2)&&!s.Tick(1,2));Yes(s.InFlight(2.1)&&!s.InFlight(2.2));
            Reject(()=>s.Launch(h with {Throw=2,Launch=2.1}),"회수");s.Launch(h with {Throw=2,Launch=2.3,Attach=2.4,Detonate=null,Returned=6});s.Attach(2,false);Yes(s.Suspends(2,5.39)&&!s.Suspends(2,5.4));
        });
        check("오르비우스 표:매달기75냉기CO가산·수동폭발586/콤보/KB·4m무감쇠",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload","Killing Blow");b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            var c=OrviusHoverState.Calculate(catalog,b);Near(c.Damage[DamageType.Cold],75*3);Near(c.CriticalChance,.2);Near(c.CriticalMultiplier,2.2);Near(c.StatusChance,.6);
            var blast=OrviusHoverState.Calculate(catalog,b,true,4);Near(blast.Damage[DamageType.Blast],586*3.4*12);Yes(blast.Melee!.ForcedProcs.Contains("Cold"));Near(OrviusHoverState.Calculate(catalog,b,true,4.01).Damage.Values.Sum(),0);
        });
        check("오르비우스 전투:매달기2틱 뒤 수동기폭·남은틱취소·다중대상·무시야폭발",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("edge",4,Defense:new(1e9,0)),new("outside",4.01,Defense:new(1e9,0))],BlockedSight=[new(1,2)]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="orviusHover")==2&&r.Events.Count(e=>e.Kind=="orviusExplosion")==2&&r.Events.Count(e=>e.Kind=="heavyCombo")==1);Yes(r.Enemies![1].DirectDamage>=586&&r.Enemies[2].DirectDamage==0);Yes(r.Events.Any(e=>e.Kind=="orviusReturn"));
        });
        check("오르비우스 전투:매달기 틱에 즉사해도 원래 위치에 남아 주변 기폭",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("near",1,Defense:new(1e9,0))]}};
            var r=Run(b);Near(r.DirectDamage,1);Yes(r.Events.Count(e=>e.Kind=="orviusHover")==1&&r.Events.Count(e=>e.Kind=="orviusExplosion")==1&&r.Enemies![1].DirectDamage>=586);
        });
        check("오르비우스 전투:초기OG 파괴 후에도 매달기 재개 없음·다음투척만 허용",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Overguard=1},Encounter=b.Encounter! with {OrviusHovers=[b.Encounter.OrviusHovers![0],new(2.3,2.4,Throw:2,Detonate:4,Returned:4.5,TickTimes:[2.5,3.5,4.5],UseInfobox:true)]}};
            var r=Run(b);var holds=r.Events.Where(e=>e.Kind=="orviusHold").ToArray();Yes(holds.Length==2);Near(holds[0].Amount,0);Near(holds[1].Amount,1);Yes(r.Points.Last().Overguard==0);
        });
        check("오르비우스 출처:표 선택 없는 계산·자동폭발 미기재 피해·비행 중 칼날 거부",()=>{
            var b=B();Reject(()=>Run(b with {Encounter=b.Encounter! with {OrviusHovers=[b.Encounter.OrviusHovers![0] with {UseInfobox=false}]}}),"상충");
            Reject(()=>Run(b with {Encounter=b.Encounter! with {OrviusHovers=[b.Encounter.OrviusHovers![0] with {Detonate=null,Returned=3.5}]}}),"자동 폭발");
            Reject(()=>Run(b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=false}}),"비행 중");
            var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.OrviusHovers![0].TickTimes!.SequenceEqual(b.Encounter!.OrviusHovers![0].TickTimes!));
        });
    }
}
