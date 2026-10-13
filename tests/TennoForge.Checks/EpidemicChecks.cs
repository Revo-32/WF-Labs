using TennoForge.Core;

internal static class EpidemicChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var original=Catalog.LoadMelee();var id=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");
        var catalog=original with {Weapons=original.Weapons.Select(w=>w.Id==id?w with {Damage=new(){[DamageType.Impact]=100},CriticalChance=0,CriticalMultiplier=2,StatusChance=0}:w).ToArray()};
        ArcaneSlot Arc(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).MaxRank);
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Exodia:Arc("Exodia Epidemic")),Encounter:new(EncounterMode.TenSeconds,Epidemic:[new(0),new(1,EpidemicPhase.Explosion)],ScriptedAttacksOnly:true));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("에피데믹 검증 불일치");}
        check("에피데믹:10 폭발 파동·무기30% 바이러스·서로 다른 치명/상태 기본값",()=>{
            var c=catalog with {Weapons=catalog.Weapons.Select(w=>w.Id==id?w with {CriticalChance=.25,StatusChance=.3}:w).ToArray()};var r=ExodiaEpidemicMechanic.Calculate(c,B());
            Near(r.Wave.Damage[DamageType.Blast],10);Near(r.Wave.CriticalChance,0);Near(r.Wave.CriticalMultiplier,1);Near(r.Wave.StatusChance,0);
            Near(r.Explosion.Damage[DamageType.Viral],30);Near(r.Explosion.CriticalChance,.25);Near(r.Explosion.CriticalMultiplier,2);Near(r.Explosion.StatusChance,.3);Near(r.SuspensionSeconds,4);
            Yes(r.Notes.Any(n=>n.Contains("약30%")));
        });
        check("에피데믹:파동CO×범용·폭발피해+범용·원소/콤보/슬램/강공격/위력 제외",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload","Shocking Touch","Killing Blow","Seismic Wave");b=b with {Arcanes=new(Warframe1:Arc("Arcane Arachne")),Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Puncture]=1},AbilityStrength=10},Scenario=b.Scenario with {Combo=12,MotionMultiplier=9}};
            var r=ExodiaEpidemicMechanic.Calculate(catalog,b);Near(r.Wave.Damage.Values.Sum(),65);Near(r.Explosion.Damage.Values.Sum(),111);Yes(r.Wave.Damage.Count==1&&r.Explosion.Damage.Count==1);
            Near(r.Wave.Melee!.Brackets!.ConditionMultiplier,2.6);Near(r.Explosion.Melee!.Brackets!.ConditionMultiplier,1);
        });
        check("에피데믹:기본0 파동에도 프로톤 고정상확·어벤저 고정치확·크레푸스큘러 고정치피",()=>{
            var b=B();Mods(b,"Proton Snap","True Steel","Organ Shatter");b=b with {Arcanes=new(Warframe1:Arc("Arcane Avenger"),Warframe2:Arc("Arcane Crepuscular")),Melee=b.Melee! with {Conditions=new(){["ProtonSnap"]=1,["Invisible"]=1}}};var r=ExodiaEpidemicMechanic.Calculate(catalog,b);
            Near(r.Wave.StatusChance,.5);Near(r.Wave.CriticalChance,.45);Near(r.Wave.CriticalMultiplier,4);Near(r.Wave.Damage.Values.Sum(),10);Yes(!r.Wave.Damage.ContainsKey(DamageType.Toxin));
        });
        check("에피데믹 전투:명중 시각·대상별 방어·부유와컨오/어플릭션즈 분리",()=>{
            var b=B();b=b with {Arcanes=new(Melee:Arc("Melee Afflictions")),Encounter=b.Encounter! with {NearbyEnemies=[new CombatEnemy("옆 대상",1,0,0,Defense:new(1e9,2700))],Epidemic=[new(0),new(1,EpidemicPhase.Explosion,2)]}};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0);Near(r.DirectDamage,10);Near(r.Enemies![1].DirectDamage,3);Near(r.Points.Last().ComboPoints,0);Near(r.Points.Last().StatusTypes,0);Yes(r.Events.Count(e=>e.Kind=="epidemicSuspension")==1&&!r.Events.Any(e=>e.Kind=="affliction"));
        });
        check("에피데믹 폭발:듀플리케이트 별도타격·콤보/애니모시티 중첩 없음",()=>{
            var c=catalog with {Weapons=catalog.Weapons.Select(w=>w.Id==id?w with {CriticalChance=1}:w).ToArray()};var b=B();b=b with {Arcanes=new(Melee:Arc("Melee Duplicate")),Encounter=b.Encounter! with {Epidemic=[new(0,EpidemicPhase.Explosion)]}};
            var r=CombatTimeline.Calculate(c,b,StanceCatalog.Load(),1);Near(r.DirectDamage,120);Near(r.Points.Last().ComboPoints,0);Yes(r.Events.Count(e=>e.Kind=="epidemicImpact")==1);
            b=b with {Arcanes=new(Melee:Arc("Melee Animosity"))};r=CombatTimeline.Calculate(c,b,StanceCatalog.Load(),1);Near(r.Points.Last().AnimosityBonus??0,0);
        });
        check("에피데믹:저장복원·잘못된시각/대상 거부·다른엑소디아와혼용 거부",()=>{
            var b=B();var json=System.Text.Json.JsonSerializer.Serialize(b,Catalog.Json);var loaded=System.Text.Json.JsonSerializer.Deserialize<Build>(json,Catalog.Json)!;Yes(loaded.Encounter!.Epidemic!.SequenceEqual(b.Encounter!.Epidemic!));
            void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("잘못된 명중 허용");}
            Reject(()=>ExodiaEpidemicMechanic.Validate(new(double.NaN),1));Reject(()=>ExodiaEpidemicMechanic.Validate(new(0,Target:2),1));Reject(()=>ExodiaEpidemicMechanic.Calculate(catalog,b with {Melee=b.Melee! with {Exodia=Arc("Exodia Contagion")}}));
        });
    }
}
