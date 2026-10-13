using TennoForge.Core;

internal static class CaustacystChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id=CaustacystMechanic.WeaponId)=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,CaustacystPools:[new(0,4,BlindDuration:1.1)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("카우스타시스트 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){Yes(e.Message.Contains(text));return;}throw new Exception("잘못된 웅덩이 입력 허용");}
        check("카우스타시스트:파동은 범용 피해·웅덩이는 일반 피해, 컨오/상확 예외",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload","Proton Snap");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1},Conditions=new(){["ProtonSnap"]=1}},Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5))};
            var wave=CaustacystMechanic.Wave(catalog,b,0);var pool=CaustacystMechanic.Pool(catalog,b);
            Near(pool.Damage.Values.Sum(),98*2.2*2);Near(pool.StatusChance,.5);Near(pool.CriticalChance,0);
            Near(wave.Article.Damage.Values.Sum(),225*(1+1.5+.8)*2);Near(wave.Article.StatusChance,.87);
        });
        check("카우스타시스트:15m/s·관통당20%와0.5초 감소·칼날0.6 관통계수 중복 제외",()=>{
            var b=B() with {Melee=new(TargetIndex:4)};var r=CaustacystMechanic.Wave(catalog,b,1,1);
            Near(r.Article.Damage.Values.Sum(),90);Near(r.Infobox.Damage.Values.Sum(),225*(1-15d/35-.2));Near(r.RemainingLifetime,1);
            Near(CaustacystMechanic.Wave(catalog,b,2,1).Article.Damage.Values.Sum(),0);
        });
        check("코다:파동 상확37/41 출처 분기 보존·웅덩이145/100%와 리치 원소",()=>{
            var b=B(CaustacystMechanic.CodaId);var wave=CaustacystMechanic.Wave(catalog,b,0);Near(wave.Article.StatusChance,.37);Near(wave.Infobox.StatusChance,.41);
            var pool=CaustacystMechanic.Pool(catalog,b);Near(pool.Damage.Values.Sum(),145);Near(pool.StatusChance,1);Yes(wave.Differences.Any(n=>n.Contains("37%")));
        });
        check("웅덩이 시간:진입 즉시·1초 간격·이탈 시각 제외·중간 진입",()=>{
            var ticks=CaustacystMechanic.Schedule(CaustacystMechanic.WeaponId,[new(0,4)],1);Yes(ticks.Select(t=>t.Time).SequenceEqual(new[]{0d,1,2,3}));Yes(ticks.Count(t=>t.Entry)==1);
            var later=CaustacystMechanic.Schedule(CaustacystMechanic.WeaponId,[new(1.1,3.5)],1);Yes(later.Length==3);Near(later[2].Time,3.1);
            Reject(()=>CaustacystMechanic.Schedule(CaustacystMechanic.WeaponId,[new(0,5)],1),"상충");
            Reject(()=>CaustacystMechanic.Schedule(CaustacystMechanic.WeaponId,[new(0,2),new(1,3)],1),"겹칠");
        });
        check("웅덩이 전투:첫타 이후 실명 은신·고정1초 공속 제외·콤보/강공격 중복 제외",()=>{
            var b=B();Mods(b,"Fury","Killing Blow","Blood Rush");var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage,98*25);Near(r.StatusDamage,0);Yes(r.Events.Count(e=>e.Kind=="caustPoolHit")==4);Near(r.Points.Last().ComboPoints,0);
        });
        check("웅덩이 전투:미기재 실명 시간 거부·오버가드가 차단하는 실명 제외",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {CaustacystPools=[new(0,4)]}};
            Reject(()=>CombatTimeline.Calculate(catalog,b,stances,1),"실명 지속시간");
            var r=CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Overguard=1e9}},stances,1);Near(r.DirectDamage,98*4);Yes(!r.Events.Any(e=>e.Kind=="caustBlind"));
        });
        check("웅덩이 전투:대상별 체류·직접 명중 누적·실명 만료",()=>{
            var b=B(CaustacystMechanic.CodaId);b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("other",1,Defense:new(1e9,0))],CaustacystPools=[new(0,2,BlindDuration:.5),new(1,3,Target:2,BlindDuration:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage,145*2);Near(r.Enemies![1].DirectDamage,145*9);Yes(r.Events.Count(e=>e.Kind=="caustPoolHit")==4);
        });
        check("카우스타시스트:고정 파동과 성분의 감쇠 일치·슬램은 파동 출처 문제와 분리",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Attack="Corrosive Wave",Distance=15,TargetIndex=2},Encounter=null};
            var result=MeleeEngine.Calculate(catalog,b);Near(result.Damage.Values.Sum(),CaustacystMechanic.Wave(catalog,b,1,1).Infobox.Damage.Values.Sum());
            Mods(b,"Condition Overload");b=b with {Melee=b.Melee with {Statuses=new(){[DamageType.Slash]=1}}};Near(MeleeEngine.Calculate(catalog,b).Damage.Values.Sum(),CaustacystMechanic.Wave(catalog,b,1,1).Infobox.Damage.Values.Sum());
            var profile=MeleeRuleCatalog.Load().Weapon(b.WeaponId);Yes(!MeleeMechanicScope.CompoundApplies(profile,b.Melee with {Attack="Heavy Slam Attack"}));
            var slam=b with {Melee=b.Melee with {Attack="Heavy Slam Attack",Distance=0,TargetIndex=1},Encounter=new(EncounterMode.FiveSeconds)};Yes(CombatTimeline.Calculate(catalog,slam,stances,1).Unsupported.Length==0);
        });
    }
}
