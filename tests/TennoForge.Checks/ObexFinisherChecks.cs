using TennoForge.Core;

internal static class ObexFinisherChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("오벡스 범위 마무리 불일치");}
        void Near(double x,double y){if(!double.IsFinite(x)||Math.Abs(x-y)>1e-8*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        Build B(string kind="Finisher")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Prisma Obex");var stance=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key==kind));
            return new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,2700)),catalog.Version,5,Stance:new(stance.Id,kind,stances.Revision),Capacity:CapacityCatalog.Load().Default(w.Id,stance.IsDefault?null:stance.Id),Melee:new(Incarnon:true));
        }
        check("오벡스: 지상 마무리의 각 접촉이12m 파동·감쇠 없음·원 대상 중복 금지",()=>{
            var b=B();var direct=StanceEngine.Calculate(catalog,b,stances);var center=ObexFinisherMechanic.Calculate(catalog,b,stances,0);var edge=ObexFinisherMechanic.Calculate(catalog,b,stances,12);var miss=ObexFinisherMechanic.Calculate(catalog,b,stances,12.001);
            Yes(center.Waves.Length==direct.Combo.HitCount&&center.Waves.Length>1);Near(center.RawDamage,direct.ExpectedComboDamage);Near(center.TargetDamage,direct.TargetDamage);Near(edge.RawDamage,center.RawDamage);Near(miss.RawDamage,0);Near(StanceEngine.Calculate(catalog,b,stances).TargetDamage,direct.TargetDamage);
        });
        check("오벡스: 마무리 피해 모드와450초과 진화는 가산·같은 조건의 주변 적에 적용",()=>{
            var b=B("Finisher Front");var baseline=ObexFinisherMechanic.Calculate(catalog,b,stances,5);var mod=catalog.Mods.Single(m=>m.EnglishName=="Finishing Touch");b.Slots[0]=new(mod.Id,mod.MaxRank);
            var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Condition=="Armor450");b=b with {Melee=b.Melee! with {Evolutions=[perk.Id]},Arcanes=new(PlayerArmor:451)};
            var withPerkBase=ObexFinisherMechanic.Calculate(catalog,b with {Slots=new ModSlot?[8],Arcanes=new(PlayerArmor:450)},stances,5);var combined=ObexFinisherMechanic.Calculate(catalog,b,stances,5);
            Near(combined.RawDamage,withPerkBase.RawDamage*(1+.6+.8));Yes(baseline.RawDamage>0);
        });
        check("오벡스: 잠정적인 방사형 은신 효과를 확정 피해에 섞지 않음",()=>{
            var b=B("Finisher Front");var plain=ObexFinisherMechanic.Calculate(catalog,b,stances,3);var hidden=ObexFinisherMechanic.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Invisible"]=1}}},stances,3);
            Near(hidden.RawDamage,plain.RawDamage);Yes(hidden.Waves.All(w=>w.StealthRawDamage>w.RawDamage));
        });
        check("오벡스: 첫 마무리 접촉의 처치에도 한 파동·중단된 후속 파동은 생성하지 않음",()=>{
            var b=B();b=b with {Melee=b.Melee! with {ComboDurationOverride=2},Scenario=new(Enemy:new(1,0))};var sc=StanceEngine.Calculate(catalog,b,stances);
            b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,sc.Combo.HitCount).ToArray())};var t=CombatTimeline.Calculate(catalog,b,stances,64);
            Yes(t.Unsupported.Length==0&&t.SecondaryBursts?.Length==1);Near(t.SecondaryBursts![0].ExpectedActivations,1);Near(t.DirectDamage,1);Yes(t.SecondaryBursts[0].ExpectedCenterDamage>1);
        });
        check("오벡스 실제 주변 파동:12m 경계·각 대상 처치·크레센도 영구 콤보",()=>{
            var b=B();var arcane=new ArcaneSlot(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Crescendo").Id,5);
            b=b with {Scenario=new(Enemy:new(1,0)),Arcanes=new(Melee:arcane),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("edge",12),new("outside",12.001)])};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);Near(r.Enemies![0].DirectDamage,1);Near(r.Enemies[1].DirectDamage,1);Near(r.Enemies[2].DirectDamage,0);
            Yes(r.Events.Count(e=>e.Kind=="crescendo")==2);Near(r.Enemies[0].Ttk.Median!.Value,0);Near(r.Enemies[1].Ttk.Median!.Value,0);
        });
    }
}
