using TennoForge.Core;

internal static class WarfanSlamChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id,WeaponActionInput? input=null)=>new(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:input is null?[]:[input]));
        void Yes(bool v){if(!v)throw new Exception("워팬 슬램 검증 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("잘못된 슬램 경로 허용");}
        check("워팬6종:모든 장착 스탠스에서 슬램2타·강슬램 둘째는일반피해",()=>{
            var fans=catalog.Weapons.Where(w=>WarfanSlam.Applies(w.Id)).ToArray();Yes(fans.Length==6);
            foreach(var w in fans)foreach(var s in stances.ForWeapon(w))foreach(var heavy in new[]{false,true})
            {
                var b=B(w.Id) with {Stance=new(s.Id,heavy?"Heavy Slam":"Slam",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.Id)};
                var c=StanceEngine.Calculate(catalog,b,stances);Yes(c.Hits.Length==2&&c.Combo.Duration is null);
                Near(c.Hits[1].Damage.Values.Sum(),w.Damage.Values.Sum()*2);
                Near(c.Hits[0].Damage.Values.Sum(),w.Damage.Values.Sum()*(heavy?3*12:2));
                Yes(c.Hits[1].Calculation!.Melee!.ForcedProcs.Contains("Impact"));
            }
        });
        check("워팬 강슬램:콤보·강공격피해·치확은첫타만·CO는두광역타제외",()=>{
            var b=B(WarfanSlam.Gunsen);Mods(b,"Pressure Point","Killing Blow","True Steel","Condition Overload");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            var input=new WeaponActionInput(0,0,Mode:"HeavySlam");
            var a=WeaponActionFlight.Calculate(catalog,b,input,new(.1,SlamHit:1));var z=WeaponActionFlight.Calculate(catalog,b,input,new(.2,SlamHit:2));
            Near(a.Damage.Values.Sum(),480*(1+1.2+1.2)*12);Near(z.Damage.Values.Sum(),320*(1+1.2));
            Near(a.CriticalChance,.16*(1+1.2*2));Near(z.CriticalChance,.16*(1+1.2));
            Yes(a.Melee!.ForcedProcs.Contains("Lifted")&&z.Melee!.ForcedProcs.Contains("Impact"));
        });
        check("워팬 강슬램:6m/5m 별도 반경·입력당 한번 콤보소모·명중시간 보존",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.1,SlamHit:1),new(.4,SlamHit:2)],Mode:"HeavySlam");var b=B(WarfanSlam.Gunsen,v);
            b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("첫타만",5.5,Defense:new(1e9,0)),new("범위밖",6.1,Defense:new(1e9,0))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1);Yes(r.Events.Count(e=>e.Kind=="warfanSlam"&&e.Time==.1)==2&&r.Events.Count(e=>e.Kind=="warfanSlam"&&e.Time==.4)==1);Near(r.Enemies![2].DirectDamage,0);
            Near(r.Events.Single(e=>e.Kind=="weaponActionInput").Amount,12);Yes(!r.HasSnapshotDps);
        });
        check("워팬 일반슬램:콤보미소모·각타격 방어층전이·상태시간계산",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.1,SlamHit:1),new(.4,SlamHit:2)],Mode:"Slam");var b=B(WarfanSlam.Gunsen,v) with {Melee=new(Overguard:1)};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="warfanSlam")==2&&!r.Events.Any(e=>e.Kind=="heavyCombo"));Yes(r.Events.Any(e=>e.Kind=="overguardBreak"||e.Description.Contains("오버가드 파괴"))||r.Enemies![0].Overguard==0);
            Yes(r.Events.Any(e=>e.Kind=="proc"&&e.Description.Contains("충격")));
        });
        check("워팬:세번째/중복/역순타격 거부·모드와타격번호 저장복원",()=>{
            var v=new WeaponActionInput(0,0,Contacts:[new(.1,SlamHit:1),new(.4,SlamHit:2)],Mode:"HeavySlam");var b=B(WarfanSlam.Vericres,v);
            foreach(var cs in new WeaponActionContact[][]{[new(.1),new(.2)],[new(.2,SlamHit:1),new(.1,SlamHit:2)],[new(.1,SlamHit:3)]})Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[v with {Contacts=cs}],1));
            Reject(()=>WeaponActionFlight.Schedule(WeaponStanceMechanic.Syam,[v],1));
            var restored=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;
            Yes(restored.Encounter!.WeaponActions![0].Mode=="HeavySlam"&&restored.Encounter.WeaponActions[0].Contacts![1].SlamHit==2);
        });
    }
}
