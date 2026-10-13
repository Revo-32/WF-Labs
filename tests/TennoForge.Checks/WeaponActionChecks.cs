using TennoForge.Core;

internal static class WeaponActionChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id,params WeaponActionInput[] inputs)=>new(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:inputs));
        void Yes(bool v){if(!v)throw new Exception("고유 공격 시간 경로 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        void Reject(Action a,string text){try{a();}catch(Exception e) when(e is ArgumentException or CombatRuleUnavailableException){Yes(e.Message.Contains(text));return;}throw new Exception("잘못된 입력을 허용했습니다: "+text);}
        check("고유 투사체:무기별 비행 속도·상한·반사/단검 개수를 검사",()=>{
            foreach(var id in new[]{WeaponStanceMechanic.Argo,WeaponStanceMechanic.Edun,WeaponStanceMechanic.Quassus})
            {var v=new WeaponActionInput(0,0,Contacts:[new(.01,Travel:10)],ExplosionModel:"Module");var b=B(id,v);Reject(()=>Run(b),"최단 비행");}
            Reject(()=>WeaponActionFlight.Schedule(WeaponStanceMechanic.Quassus,[new(0,0,Contacts:[new(.2,Projectile:13)])],1),"투사체 번호");
            Reject(()=>WeaponActionFlight.Schedule(WeaponStanceMechanic.Argo,[new(0,0,Contacts:[new(.1),new(.2,Target:2),new(.3,Target:3)])],3),"반사");
            Reject(()=>WeaponActionFlight.Schedule(WeaponStanceMechanic.Edun,[new(0,0,Contacts:[new(.1)],ExplosionModel:"Unresolved")],1),"상충");
        });
        check("아르고:발사1회·직격과 반사·각3m폭발·공유12배 콤보 한 번 소비",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.2,Travel:15),new(.4,Target:2,Travel:20)]);var b=B(WeaponStanceMechanic.Argo,v);
            b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("반사",1,Defense:new(1e9,0)),new("범위밖",5,Defense:new(1e9,0))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="weaponActionInput")==1&&r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==2&&r.Events.Count(e=>e.Kind=="weaponProjectileExplosion")==4);
            Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1);Near(r.Events.Single(e=>e.Kind=="weaponActionInput").Amount,12);Near(r.Enemies![2].DirectDamage,0);
            var direct=WeaponActionFlight.Calculate(catalog,b,v);var radial=WeaponActionFlight.Calculate(catalog,b,v,explosion:true,distance:3);Near(direct.Damage.Values.Sum(),200*3*12);Near(radial.Damage.Values.Sum(),100*3*12*.6);
        });
        check("에둔:본문1초/5m와 데이터0.8초/4m 분기·폭발에서CO제외",()=>{
            foreach(var model in new[]{"Article","Module"})
            {
                var v=new WeaponActionInput(0,0,Contacts:[new(.2,Travel:10)],ExplosionModel:model);var b=B(WeaponStanceMechanic.Edun,v);Mods(b,"Condition Overload");
                b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("4.5m",4.5,Defense:new(1e9,0))]}};var r=Run(b);var bursts=r.Events.Where(e=>e.Kind=="weaponProjectileExplosion").ToArray();Yes(bursts.Length==(model=="Article"?2:1));Near(bursts[0].Time,model=="Article"?1.2:1);
                Yes(r.Notes.Any(n=>n.StartsWith("미검증:")));
                var primed=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};Near(WeaponActionFlight.Calculate(catalog,primed,v,explosion:true).Damage.Values.Sum(),400*12);
            }
        });
        check("에둔·샤암:발사 후 강공격 동작 시작/종료로 명중 순간 보너스를 판정",()=>{
            foreach(var id in new[]{WeaponStanceMechanic.Edun,WeaponStanceMechanic.Syam})
            {
                var v=new WeaponActionInput(0,0,Contacts:[new(.4)],ExplosionModel:"Module");var b=B(id,v);Mods(b,"Killing Blow","True Steel");
                var ordinary=Run(b);var active=Run(b with {Encounter=b.Encounter! with {Inputs=[new(.1,"HeavyAnimationStart"),new(.5,"HeavyAnimationEnd")]}});
                Yes(active.Events.Single(e=>e.Kind=="weaponProjectileImpact").Amount>ordinary.Events.Single(e=>e.Kind=="weaponProjectileImpact").Amount);
                Yes(active.Events.Count(e=>e.Kind=="heavyCombo")==1);
            }
        });
        check("콰서스:칼날과 단검이같은입력자원·다중단검병합불확실성표시",()=>{
            var v=new WeaponActionInput(0,.1,Contacts:[new(.01,Blade:true),new(.02,Blade:true,BladeHit:2),new(.4,Projectile:1),new(.4,Projectile:2)],IndependentDaggers:true);
            var b=B(WeaponStanceMechanic.Quassus,v);var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1&&r.Events.Count(e=>e.Kind=="weaponBladeImpact")==2&&r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==2);
            Yes(r.Notes.Any(n=>n.Contains("독립 타격/상태")));Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[v with {IndependentDaggers=false}],1),"합쳐지는");
            var saved=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.WeaponActions![0].Contacts!.SequenceEqual(v.Contacts!));
        });
        check("버디락:칼날+파동+2전기소멸폭발·프로젝트콤보미증가·수명초과거부",()=>{
            var v=new WeaponActionInput(0,.1,Sequence:2,Contacts:[new(.01,Blade:true),new(.5,Travel:10),new(1.4,Explosion:true)]);var b=B(WeaponStanceMechanic.Verdilac,v);var r=Run(b);
            Yes(r.Events.Count(e=>e.Kind=="weaponBladeImpact")==1&&r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==1&&r.Events.Count(e=>e.Kind=="weaponProjectileExplosion")==1);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
            var invalid=v with {Contacts=[new(1.41,Travel:10)]};Reject(()=>Run(B(b.WeaponId,invalid)),"수명");
        });
        check("코룸:찌르기전기·뽑기베기·10m충격파는직격대상에중복합산하지않음",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.1,Blade:true),new(.3,Blade:true,BladeHit:2),new(.35,Explosion:true)]);var b=B(WeaponStanceMechanic.Korumm,v);
            b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("가장자리",10,Defense:new(1e9,0)),new("범위밖",10.01,Defense:new(1e9,0))]}};var r=Run(b);
            Yes(r.Events.Count(e=>e.Kind=="weaponBladeImpact")==2&&r.Events.Count(e=>e.Kind=="weaponProjectileExplosion")==1);Near(r.Enemies![1].DirectDamage,1040);Near(r.Enemies[2].DirectDamage,0);
            Near(r.Enemies[0].DirectDamage,r.Events.Where(e=>e.Kind=="weaponBladeImpact").Sum(e=>e.Amount));Yes(r.Events.Any(e=>e.Kind=="forcedProc"&&e.Description.Contains("Electricity"))||r.StatusDamage>0);
        });
        check("고유 강공격:애니모시티 중첩을 한 입력 전체에 공유하고다음입력에는소모",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.1,Blade:true),new(.3)]);var b=B(WeaponStanceMechanic.Quassus,v);
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Animosity");b=b with {Arcanes=new(Melee:new(a.Id,5,true)),Melee=b.Melee! with {Conditions=new(){["AnimosityStacks"]=10}}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="animosity")==1);Near(r.Events.Single(e=>e.Kind=="animosity").Amount,10);
        });
    }
}
