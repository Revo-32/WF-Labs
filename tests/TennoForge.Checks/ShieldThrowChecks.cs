using TennoForge.Core;

internal static class ShieldThrowChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id=ShieldThrowMechanic.Sigma,string attack="Shield Throw")=>new(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Attack:attack));
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("방패 투척 규칙 불일치");}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        check("시그마 방패:모든 장착 스탠스의 공중 동작이116 단일 투척",()=>{
            var w=catalog.Weapons.Single(w=>w.Id==ShieldThrowMechanic.Sigma);
            foreach(var s in stances.ForWeapon(w))
            {
                var b=B();b=b with {Stance=new(s.Id,"Aerial",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.Id)};
                var c=StanceEngine.Calculate(catalog,b,stances);Yes(c.Hits.Length==1&&c.Hits[0].Count==1);Near(c.ComboDamage.Values.Sum(),116);
                Near(c.Stats.CriticalChance,.28);Near(c.Stats.CriticalMultiplier,2.2);Near(c.Stats.StatusChance,.16);Yes(c.Hits[0].Calculation!.Melee!.ForcedProcs.Contains("Stun"));
                Yes(c.Duration is null);
            }
        });
        check("시그마 방패:블러드러시·위핑·글래디에이터 콤보 통계 제외·일반 모드 유지",()=>{
            var b=B();Mods(b,"Blood Rush","Weeping Wounds","Gladiator Might","True Steel","Melee Prowess");
            var c=MeleeEngine.Calculate(catalog,b);Near(c.CriticalChance,.28*2.2);Near(c.StatusChance,.16*1.9);Near(c.CriticalMultiplier,2.2*1.6);
            var first=MeleeEngine.Calculate(catalog,b with {Scenario=b.Scenario with {Combo=1}});Near(c.ExpectedHit,first.ExpectedHit);
            var blade=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Attack="Normal Attack"}});Yes(blade.CriticalChance>c.CriticalChance&&blade.StatusChance>c.StatusChance);
        });
        check("시그마 방패:직격116과 폭발50의 별도기초·범위4m 선형 전감쇠",()=>{
            var b=B();Mods(b,"Pressure Point","Killing Blow","Primed Reach");var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage.Values.Sum(),116*2.2);
            b=b with {Melee=b.Melee! with {Attack="Shield Throw Explosion",Distance=2}};
            var burst=MeleeEngine.Calculate(catalog,b);Near(burst.Damage[DamageType.Blast],50*2.2*.5);Near(burst.Melee!.AreaRadius!.Value,4);Yes(!burst.Melee.Policy!.Heavy&&!burst.Melee.Policy.Combo);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee with {Distance=4.01}}).ExpectedHit,0);
        });
        check("시그마 방패:24/25m 원문불일치 경계만 거부·근접 리치로 연장하지 않음",()=>{
            var b=B();Mods(b,"Primed Reach");b=b with {Melee=b.Melee! with {Distance=24}};Yes(MeleeEngine.Calculate(catalog,b).ExpectedHit>0);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee with {Distance=25.01}}).ExpectedHit,0);
            try{MeleeEngine.Calculate(catalog,b with {Melee=b.Melee with {Distance=24.5}});}catch(CombatRuleUnavailableException e){Yes(e.Message.Contains("24m"));return;}throw new Exception("출처 불일치를 확정함");
        });
        check("코브라 방패:일반·프라임·모드 스탠스 모두 고유1타·미기재 투사체를 공중3타로 대체하지 않음",()=>{
            foreach(var id in new[]{ShieldThrowMechanic.Cobra,ShieldThrowMechanic.CobraPrime})foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==id)))
            {
                var row=s.Combos.Single(c=>c.Key=="Aerial");Yes(row.HitCount==1&&row.Hits.Single().AttackType=="Ranged");
                var b=B(id,"Normal Attack");b=b with {Stance=new(s.Id,"Aerial",stances.Revision),Capacity=CapacityCatalog.Load().Default(id,s.Id)};
                var rejected=false;try{StanceEngine.Calculate(catalog,b,stances);}catch(CombatRuleUnavailableException e){rejected=e.Message.Contains("치명/상태");}Yes(rejected);
                Yes(MeleeEngine.Calculate(catalog,b with {Stance=null}).ExpectedHit>0);
            }
        });
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        Build Flight()=>B() with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ShieldFlights:[new(0,1,.2,Travel:12,Detonation:new(.4),FinisherWindowSeconds:.5)])};
        check("시그마 방패 비행:직격1회·기폭과 중심/가장자리/범위밖 방어층 분리",()=>{
            var b=Flight();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("half",2,Defense:new(1e9,0)),new("out",4.01,Defense:new(1e9,0))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="shieldImpact")==1&&r.Events.Count(e=>e.Kind=="shieldExplosion")==2&&r.Events.Count(e=>e.Kind=="shieldReturn")==1);
            Yes(r.Enemies![1].DirectDamage>0);Near(r.Enemies[2].DirectDamage,0);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
            Near(r.DirectDamage,r.Events.Where(e=>e.Kind is "shieldImpact" or "shieldExplosion"&&e.Description.Contains("대상1")).Sum(e=>e.Amount));Yes(!r.HasSnapshotDps);
        });
        check("시그마 방패 비행:회수 폭발은 자동 생성되지 않으며 빗나간 투척도 기폭 가능",()=>{
            var b=Flight();b=b with {Encounter=b.Encounter! with {ShieldFlights=[new(0,1,.2)]}};var direct=Run(b);Yes(!direct.Events.Any(e=>e.Kind=="shieldExplosion"));
            b=b with {Encounter=b.Encounter with {ShieldFlights=[new(0,1,Detonation:new(.4))]}};var blast=Run(b);Yes(!blast.Events.Any(e=>e.Kind=="shieldImpact")&&blast.Events.Count(e=>e.Kind=="shieldExplosion")==1&&blast.DirectDamage>0);
        });
        check("시그마 방패 비행:다음 입력의 막기 충전은 발사1회와 기폭 입력에서 별도 소비",()=>{
            var b=Flight();b=b with {Melee=b.Melee! with {Conditions=new(){["BlockCharges"]=4}},Encounter=b.Encounter! with {Inputs=[new(.1,"BlockedHit",2)]}};
            var r=Run(b);var spent=r.Events.Where(e=>e.Kind=="blockCharge"&&e.Description.Contains("소모")).ToArray();
            Yes(spent.Length==2);Near(spent[0].Amount,4);Near(spent[0].Time,0);Near(spent[1].Amount,2);Near(spent[1].Time,.4);
            var saved=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.ShieldFlights!.Single()==b.Encounter.ShieldFlights!.Single());
        });
        check("시그마 방패 비행:60m/s 최단 비행·중첩 투척·회수 후 명중을 거부",()=>{
            void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("잘못된 방패 경로 허용");}
            Reject(()=>Run(Flight() with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ShieldFlights:[new(0,1,.1,Travel:12)])}));
            Reject(()=>ShieldFlightRules.Validate([new(0,1,1)],ShieldThrowMechanic.Sigma,1));
            Reject(()=>ShieldFlightRules.Validate([new(0,1),new(.5,2,Throw:2)],ShieldThrowMechanic.Sigma,1));
            Reject(()=>ShieldFlightRules.Validate([new(0,1,.5,Detonation:new(.4))],ShieldThrowMechanic.Sigma,1));
        });
    }
}
