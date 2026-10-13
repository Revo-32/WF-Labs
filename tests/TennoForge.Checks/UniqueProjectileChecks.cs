using TennoForge.Core;

internal static class UniqueProjectileChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id)=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new());
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("고유 스탠스/투사체 검증 불일치");}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        StanceCalculation St(Build b,string sid){var key=WeaponStanceMechanic.Key(b.WeaponId);return StanceEngine.Calculate(catalog,b with {Stance=new(sid,key,stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,sid)},stances);}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        void Reject(Action a,string part){try{a();}catch(Exception e) when(e is ArgumentException or CombatRuleUnavailableException){Yes(e.Message.Contains(part));return;}throw new Exception("검증 누락: "+part);}
        check("고유 스탠스26연결:무기 고유 행만 덮어쓰고 공통 무기군 행은 유지",()=>{
            Yes(UniqueWeaponStances.Links.Count==26);
            foreach(var (id,sid) in UniqueWeaponStances.Links)
            {
                var w=catalog.Weapons.Single(w=>w.Id==id);var own=stances.Stances.Single(s=>s.Id==sid);
                foreach(var s in stances.ForWeapon(w))foreach(var row in own.Combos.Where(c=>c.InheritedFrom is null))
                {
                    var actual=s.Combos.Single(c=>c.Key==row.Key);
                    if(!WeaponStanceMechanic.Applies(id)&&!ShieldThrowMechanic.Applies(id))Yes(actual.Hits.SequenceEqual(row.Hits));
                }
            }
        });
        check("아룸 스피노사:70×2.5/5·18/9발·별도9%치확·6.16/12.3%상확",()=>{
            var id=WeaponStanceMechanic.Arum;var b=B(id);
            foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==id)))
            {
                var c=St(b,s.Id);var ranged=c.Hits.Where(h=>h.SourceHit!.AttackType=="Ranged").ToArray();Yes(ranged.Length==2);Near(ranged[0].Damage.Values.Sum(),175);Near(ranged[1].Damage.Values.Sum(),350);Near(ranged[0].Count,18);Near(ranged[1].Count,9);
                Near(ranged[0].Calculation!.CriticalChance,.09);Near(ranged[0].Calculation!.StatusChance,.0616);Near(ranged[1].Calculation!.StatusChance,.123);Near(ranged[0].Calculation!.Melee!.Projectiles,1);
            }
        });
        check("아룸:컨오1/5·1/10은 콤보 배수를 받지 않음·49m/s·1.3m관통",()=>{
            var b=B(WeaponStanceMechanic.Arum);Mods(b,"Pressure Point","Condition Overload");b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            var c=WeaponActionFlight.Calculate(catalog,b,new(0,0));Near(c.Damage.Values.Sum(),175*(2.2*12+.8*.2));
            var input=new WeaponActionInput(0,0,Contacts:[new(.2,Travel:9.8,PenetratedMeters:1.3)]);b=b with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[input])};Yes(Run(b).Events.Count(e=>e.Kind=="weaponProjectileImpact")==1);
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[input with {Contacts=[new(.2,PenetratedMeters:1.31)]}],1),"두께");
        });
        check("네페리:모든스탠스 정지4칼날+4화염구·131별도기초·콤보획득제외",()=>{
            var id=WeaponStanceMechanic.Nepheri;foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==id)))
            {var c=St(B(id),s.Id);var fire=c.Hits.Single(h=>h.SourceHit!.AttackType=="Ranged");Near(fire.Count,4);Near(fire.Damage[DamageType.Heat],131);Near(fire.Calculation!.CriticalChance,.21);Near(fire.Calculation.StatusChance,.33);Yes(!fire.Calculation.Melee!.Policy!.HitCombo);}
            var b=B(id) with {Melee=new(Attack:"Fireballs",Distance:10)};Yes(MeleeEngine.Calculate(catalog,b).Melee!.ForcedProcs.Contains("Impact"));Yes(!MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=10.01}}).Melee!.ForcedProcs.Contains("Impact"));
            Reject(()=>MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=19.5}}),"상충");Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=20.01}}).ExpectedHit,0);
        });
        check("네페리:화염구4발 각발사시각/0.2초수명·강제충격은섀터링임팩트를발동하지않음",()=>{
            var contacts=Enumerable.Range(0,4).Select(i=>new WeaponActionContact(i*.3+.1,Projectile:i+1,Travel:10,Launched:i*.3)).ToArray();var input=new WeaponActionInput(0,0,Sequence:2,Contacts:contacts);var b=B(WeaponStanceMechanic.Nepheri);Mods(b,"Shattering Impact");b=b with {Scenario=b.Scenario with {Enemy=new(1e9,2700)},Encounter=new(EncounterMode.FiveSeconds,BaseArmor:500,ScriptedAttacksOnly:true,WeaponActions:[input])};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==4&&!r.Events.Any(e=>e.Kind=="armor"));
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[input with {Contacts=contacts.Select(c=>c with {Launched=null}).ToArray()}],1),"연속 발사");
        });
        check("테넷 아젠더스:첫투사체2880·둘째칼날5배·본문/데이터감쇠분리",()=>{
            var id=WeaponStanceMechanic.Agendus;var b=B(id);foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==id)))
            {var c=St(b,s.Id);Near(c.Hits.Single(h=>h.SourceHit!.AttackType=="Ranged").Damage[DamageType.Electricity],2880);Yes(c.Hits.Any(h=>h.SourceHit!.AttackType!="Ranged"));}
            var body=WeaponActionFlight.Calculate(catalog,b,new(0,0,ExplosionModel:"Article"),distance:20);var table=WeaponActionFlight.Calculate(catalog,b,new(0,0,ExplosionModel:"Module"),distance:20);Near(body.Damage[DamageType.Electricity],2880*.05);Near(table.Damage[DamageType.Electricity],2880*.0695);
            Reject(()=>WeaponActionFlight.Calculate(catalog,b,new(0,0),distance:15),"상충");
        });
        check("테넷 그리고리:4080별도기초·범용피해와곱연산CO·칼날피해증가제외",()=>{
            var b=B(WeaponStanceMechanic.Grigori);Mods(b,"Pressure Point","Condition Overload");var arachne=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne");b=b with {Arcanes=new(Warframe1:new(arachne.Id,5,true)),Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)))
            {var c=St(b,s.Id);var disk=c.Hits.Single(h=>h.SourceHit!.AttackType=="Ranged");Near(disk.Damage.Values.Sum(),4080*2.5*1.8);}
        });
        check("그리고리:관통·표면반사후같은적재명중·반사자체는피해없음",()=>{
            var v=new WeaponActionInput(0,0,Sequence:2,Contacts:[new(.1,Travel:.1),new(.2,Travel:.2,Surface:true),new(.3,Travel:.3)],ProjectileExpires:1);
            var b=B(WeaponStanceMechanic.Grigori) with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[v])};var r=Run(b);
            Yes(r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==2&&r.Events.Count(e=>e.Kind=="grigoriRicochet")==1&&!r.Events.Any(e=>e.Kind=="weaponProjectileExplosion"));
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[v with {Contacts=[new(.1,Travel:.1),new(.3,Travel:.3)]}],1),"표면 반사");
        });
        check("테넷 그리고리:둘째강공격/슬라이딩발사·위키미기재수명은명시조건",()=>{
            var v=new WeaponActionInput(0,0,Sequence:2,Contacts:[new(.3,Travel:.3)],ProjectileExpires:1);var b=B(WeaponStanceMechanic.Grigori) with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[v])};var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="weaponProjectileImpact")==1&&r.Notes.Any(n=>n.Contains("수명 수식")));
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[v with {Sequence=1}],1),"발사되지");Yes(WeaponActionFlight.Schedule(b.WeaponId,[v with {Sequence=1,Sliding=true}],1).Length==2);
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[v with {ProjectileExpires=null}],1),"수식");
        });
    }
}
