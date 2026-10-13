using TennoForge.Core;

internal static class AtlasRubbleChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool b){if(!b)throw new Exception("러블 검증 실패");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(bool landslide=false)
        {var id=landslide?ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Landslide).Id:catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;return new(id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(ExternalEquipment:new(AtlasRubble:new(1450))),Encounter:new(EncounterMode.FiveSeconds));}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("러블:1500상한·초당5·방어력습득만2초유예·1400초과경계",()=>{
            var r=new AtlasRubbleState(new(1400));Yes(!r.Heap);r.Pickup(new(0,75));Near(r.Armor,1475);r.Advance(2);Near(r.Armor,1475);r.Advance(3);Near(r.Armor,1470);r.Pickup(new(3,75));Near(r.Armor,1500);r.Advance(25);Near(r.Armor,1400);Yes(!r.Heap);
        });
        check("러블:1체력부족도전량회복분기·넘침전환없음·유예갱신없음",()=>{
            var r=new AtlasRubbleState(new(1000));Near(r.Pickup(new(1,75,1)),1);Near(r.Armor,995);r.Advance(2);Near(r.Armor,990);Near(r.Healing,1);
        });
        check("러블:무효화영역감소2배·이탈복원·낙하는보존·호스트이전초기화",()=>{
            var r=new AtlasRubbleState(new(1500));r.SetNullified(1,true);r.Advance(2);Near(r.Armor,1485);r.SetNullified(3,false);r.Advance(4);Near(r.Armor,1470);r.HostMigration(5);Near(r.Armor,0);
        });
        check("러블:대상별50/랜드슬라이드75·30초소멸·중복습득거부",()=>{
            var r=new AtlasRubbleState(new());r.Drop(0,1,false);r.Drop(0,2,true);r.Pickup(new(1,Target:1));r.Pickup(new(1,Target:2));Near(r.Armor,125);
            try{r.Pickup(new(2,Target:1));throw new Exception("중복습득허용");}catch(CombatRuleUnavailableException){}
            r.Drop(0,3,false);try{r.Pickup(new(30,Target:3));throw new Exception("소멸후습득허용");}catch(ArgumentException){}
        });
        check("러블힙:실제러블로수동토글대체·능력위력가산브라켓·감소에따른전환",()=>{
            var b=B(true);b=b with {Melee=b.Melee! with {AbilityStrength=2,AbilityAugment=new(ExaltedAugments.Rubble,3),ExternalEquipment=new(AtlasRubble:new(1410))}};
            var full=MeleeEngine.Calculate(catalog,b);var inactive=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee with {ExternalEquipment=new(AtlasRubble:new(1400)),Conditions=new(){["RubbleHeap"]=1}}});Near(full.ExpectedHit/inactive.ExpectedHit,1.5);
            var r=Run(b);Near(r.Points.Single(p=>p.Time==2).RubbleArmor!.Value,1400);Near(r.Points.Last().RubbleArmor!.Value,1385);
        });
        check("러블실시간방어력:아케인조건용방어력에한번가산·습득/회복로그",()=>{
            var b=B();b=b with {Arcanes=new(PlayerArmor:500),Encounter=b.Encounter! with {RubblePickups=[new(1,50),new(2,75,1)],Inputs=[new(3,"RubbleNullifierEnter"),new(4,"RubbleNullifierExit")]}};
            var r=Run(b);Near(r.Points.Last().RubbleArmor!.Value,1480);Near(r.Points.Last().PlayerArmor!.Value,1980);Near(r.Points.Last().RubbleHealing,1);Near(r.Events.Count(e=>e.Kind=="rubblePickup"),2);
        });
        check("석화러블:랜드슬라이드처치75·생성과습득분리·적전멸후에도습득",()=>{
            var b=B(true);b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Debuffs=[new("Petrify")],ExternalEquipment=new(AtlasRubble:new())},Encounter=b.Encounter! with {RubblePickups=[new(.5,Target:1)]}};
            var r=Run(b);Near(r.Events.Single(e=>e.Kind=="rubbleDrop").Amount,75);Near(r.Events.Single(e=>e.Kind=="rubblePickup").Time,.5);Near(r.Points.Last().RubbleDrops,1);Near(r.Points.Last().RubbleArmor!.Value,62.5);
            var none=Run(b with {Encounter=b.Encounter with {RubblePickups=[]}});Near(none.Points.Last().RubbleArmor!.Value,0);
        });
        check("석화러블:아군석화/군중제어면역/초기오버가드에서생성하지않음",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Debuffs=[new("Petrify",Allied:true)]}};Near(Run(b).Points.Last().RubbleDrops,0);
            b=b with {Melee=b.Melee with {Debuffs=[new("Petrify")],Conditions=new(){["ControlImmune"]=1}}};Near(Run(b).Points.Last().RubbleDrops,0);
            b=b with {Melee=b.Melee with {Conditions=[],Overguard=1}};Near(Run(b).Points.Last().RubbleDrops,0);
        });
    }
}
