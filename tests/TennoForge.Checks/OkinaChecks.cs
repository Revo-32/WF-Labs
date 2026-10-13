using TennoForge.Core;

internal static class OkinaChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id=OkinaDaggerState.PrimeId)=>new(id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,NearbyEnemies:[new("hit",3,Defense:new(1e9,0)),new("edge",7,Defense:new(1e9,0)),new("outside",7.01,Defense:new(1e9,0))],OkinaDaggers:[new(.1,1,Target:2,Travel:3)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("오키나 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){Yes(e.Message.Contains(text));return;}throw new Exception("잘못된 오키나 입력 허용");}
        check("오키나:생성 최대6·직접 단검 처치 제외·명중 후 슬롯 재사용",()=>{
            var s=new OkinaDaggerState();for(var i=0;i<6;i++)Near(s.Kill(0,false)!.Value,i+1);Yes(s.Kill(0,false) is null&&s.Kill(0,true) is null);s.Launch(new(.1,1),15);s.Impact(1);Near(s.Kill(1,false)!.Value,7);Near(s.Count,6);
            Reject(()=>s.Launch(new(.1,1,Dagger:2),15.01),"15m");Reject(()=>s.Launch(new(0,1,Dagger:2),0),"생성 이후");Reject(()=>s.Impact(2),"발사하지");Reject(()=>OkinaDaggerState.Validate(new(0,.1,Travel:18),1),"18m/s");
        });
        check("오키나:직격 컨오 곱·폭발 컨오 제외·프레셔/변신피해 제외·10/1 냉기",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Viral]=1}},Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5))};
            var hit=OkinaDaggerState.Calculate(catalog,b);var blast=OkinaDaggerState.Calculate(catalog,b,true);Near(hit.Damage.Values.Sum(),184*2.5*2.6);Near(blast.Damage.Values.Sum(),184*2.5);Yes(hit.Melee!.ForcedProcs.Count(p=>p=="Cold")==10&&blast.Melee!.ForcedProcs.Count(p=>p=="Cold")==1);
        });
        check("오키나 진화II:위키 버그의 칼날 제외·단검 성분 기본 피해와 진화IV",()=>{
            foreach(var id in new[]{OkinaDaggerState.WeaponId,OkinaDaggerState.PrimeId})
            {
                var b=B(id);var def=IncarnonEngine.ForWeapon(id)!;var baseBlade=MeleeEngine.Calculate(catalog,b).Damage.Values.Sum();var baseDagger=OkinaDaggerState.Calculate(catalog,b).Damage.Values.Sum();var evo=def.Perks.First(p=>p.Stage==2);b=b with {Melee=b.Melee! with {Evolutions=[evo.Id]}};
                Near(MeleeEngine.Calculate(catalog,b).Damage.Values.Sum(),baseBlade);Near(OkinaDaggerState.Calculate(catalog,b).Damage.Values.Sum(),baseDagger+evo.Effects["BaseDamage"]);Near(OkinaDaggerState.Calculate(catalog,b,true).Damage[DamageType.Cold],baseDagger+evo.Effects["BaseDamage"]);
                var cc=OkinaDaggerState.Calculate(catalog,b).CriticalChance;var fourth=def.Perks.First(p=>p.Stage==4&&p.Effects.ContainsKey("BaseCritical"));b=b with {Melee=b.Melee with {Evolutions=[evo.Id,fourth.Id]}};Near(OkinaDaggerState.Calculate(catalog,b).CriticalChance,cc+fourth.Effects["BaseCritical"]);
            }
        });
        check("오키나:범위4m 무감쇠·사거리 모드 제외·동결 대상 케어린 적용",()=>{
            var b=B();Mods(b,"Primed Reach");var center=OkinaDaggerState.Calculate(catalog,b,true);Near(OkinaDaggerState.Calculate(catalog,b,true,4).Damage.Values.Sum(),center.Damage.Values.Sum());Near(OkinaDaggerState.Calculate(catalog,b,true,4.01).Damage.Values.Sum(),0);Near(center.Melee!.AreaRadius!.Value,4);
            b=b with {Arcanes=new(Melee:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Careen").Id,5),Frozen:true)};Near(OkinaDaggerState.Calculate(catalog,b,true).Damage.Values.Sum()/center.Damage.Values.Sum(),2.5);
        });
        check("오키나 전투:처치→유도→직격 냉기10→폭발 대상별 냉기1·콤보 제외",()=>{
            var b=B();var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="okinaCreate")==1&&r.Events.Count(e=>e.Kind=="okinaImpact")==1&&r.Events.Count(e=>e.Kind=="okinaExplosion")==2);Yes(r.Enemies![1].DirectDamage>r.Enemies[2].DirectDamage&&r.Enemies[2].DirectDamage>0);Near(r.Enemies[3].DirectDamage,0);Near(r.Points.First(p=>p.Time>=1).ComboPoints,1);Yes(r.Events.Any(e=>e.Kind=="freeze"));
        });
        check("오키나 전투:단검 직접 즉사는 새 단검 제외·원 대상 즉사 후 주변 폭발",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("hit",3,Defense:new(1,0)),new("edge",7,Defense:new(1e9,0))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="okinaCreate")==1&&r.Events.Count(e=>e.Kind=="okinaExplosion")==1);Yes(r.Enemies![2].DirectDamage>0);
        });
        check("오키나 전투:상태 피해 처치는 새 단검 생성·생성 없는 입력 거부",()=>{
            var b=B();Mods(b,"Fever Strike","Weeping Wounds");b=b with {Scenario=b.Scenario with {Combo=12},Encounter=b.Encounter! with {NearbyEnemies=[new("hit",3,Defense:new(2200,0)),new("outside",20,Defense:new(1e9,0))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,64);Yes(r.Enemies![1].StatusDamage>0);
            Yes(r.Events.Any(e=>e.Kind=="okinaCreate"&&e.Time>1)&&r.Events.Any(e=>e.Kind=="tick"&&e.Time>1));
            Reject(()=>CombatTimeline.Calculate(catalog,B() with {Scenario=new(BaseInterval:60,Enemy:new(1e9,0))},stances,1),"아직 생성");
        });
        check("오키나 전투:직격 전기는 인플루언스 발동만·폭발 자성만 전파·강제 냉기 제외",()=>{
            var b=B();Mods(b,"Shocking Touch","Voltaic Strike","Melee Prowess","Weeping Wounds");
            var nearby=Enumerable.Range(1,5).Select(i=>new CombatEnemy("victim"+i,0,Defense:new(1,0))).Concat([new("hit",3,Defense:new(1e9,0)),new("observer",7.01,Defense:new(1e9,0))]).ToArray();
            b=b with {Arcanes=new(Melee:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Influence").Id,5)),Scenario=b.Scenario with {BaseInterval=.1,Combo=12},Encounter=b.Encounter! with {NearbyEnemies=nearby,TargetOrder=Enumerable.Range(0,6).ToArray(),OkinaDaggers=Enumerable.Range(1,6).Select(i=>new OkinaDaggerFlight(.6+i*.01,1+i*.1,i,7)).ToArray()}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Any(e=>e.Kind=="influenceStart"));Yes(r.Events.Any(e=>e.Kind=="influenceSpread"&&e.Description.Contains("자성")));Yes(!r.Events.Any(e=>e.Kind=="influenceSpread"&&(e.Description.Contains("냉기")||e.Description.Contains("전기"))));Yes(r.Enemies![7].DirectDamage>0);
        });
    }
}
