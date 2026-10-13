using TennoForge.Core;

internal static class SharedMeleeInputChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("공동 근접 입력 판정 실패");}
        Build B(string id)=>new(id,new ModSlot?[8],new(Combo:6,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("텐노카이:다른공격사이에뒤늦게명중한같은입력은중복세지않음",()=>{
            var s=new TennokaiState(0,4,3);Yes(!s.Contact(0,1,0,0,false,true,.1));Yes(!s.Contact(.1,2,0,0,false,true,.1));Yes(!s.Contact(.2,1,0,1,false,true,.1));Yes(!s.Contact(.3,2,1,0,false,true,.1));Yes(s.Progress==2);
            Yes(!s.Contact(.4,3,0,0,false,true,.1));Yes(s.Contact(.5,4,0,0,false,true,.1));Yes(s.Generated==1);
        });
        check("버디락:입력4회각2대상칼날명중·디서플린기회는1회",()=>{
            var b=B(WeaponStanceMechanic.Verdilac);var m=catalog.Mods.Single(m=>m.EnglishName=="Discipline's Merit");
            b=b with {Melee=b.Melee! with {Exilus=new(m.Id,m.MaxRank)},Encounter=b.Encounter! with {ScriptedAttacksOnly=true,NearbyEnemies=[new("second",1,Defense:new(1e9,0))],WeaponActions=Enumerable.Range(0,4).Select(i=>new WeaponActionInput(i*.5,i*.5,Input:i+1,Contacts:[new(i*.5+.1,Blade:true),new(i*.5+.2,Target:2,Blade:true)])).ToArray()}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="weaponBladeImpact")==8);Yes(r.Events.Count(e=>e.Kind=="tennokaiOpportunity")==1);
        });
        check("코룸:애니모시티비대상범위성분후칼날명중도입력당1회적립",()=>{
            var b=B(WeaponStanceMechanic.Korumm);var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Animosity");
            b=b with {Arcanes=new(Melee:new(arcane.Id,5,true)),Encounter=b.Encounter! with {ScriptedAttacksOnly=true,NearbyEnemies=[new("blast",2,Defense:new(1e9,0))],WeaponActions=[new(0,0,Contacts:[new(.1,Target:2,Explosion:true),new(.2,Blade:true)])]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="animosity")==1);Yes(r.Events.Single(e=>e.Kind=="animosity").Time==.2);
        });
        check("사이베어:허공강공격도소모전6배로인카논발동·기본버프병행",()=>{
            var b=B(catalog.Weapons.Single(w=>w.EnglishName=="Sibear").Id);b=b with {Scenario=b.Scenario with {BaseInterval=10},Encounter=b.Encounter! with {Inputs=[new(.2,"HeavyAttackMiss")]}};
            var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="incarnonStart"&&e.Time==.2));Yes(r.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("사이베어")));Yes(r.Points.Last().Combo==1);
        });
    }
}
