using TennoForge.Core;

internal static class ComponentComboChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        check("별도 성분:소모한 초기 콤보를 즉시 복구해 블러드 러시를 증폭하지 않음",()=>{
            var id=OkinaDaggerState.PrimeId;var w=catalog.Weapons.Single(w=>w.Id==id);var b=new Build(id,new ModSlot?[8],new(Combo:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true));Mods(b,"Corrupt Charge","Blood Rush");
            var start=OkinaDaggerState.Calculate(catalog,b);var spent=OkinaDaggerState.Calculate(catalog,b,useInitialComboFloor:false);Near(start.CriticalChance,w.CriticalChance*1.4);Near(spent.CriticalChance,w.CriticalChance);Near(spent.Melee!.EffectiveCombo,1);
        });
        check("에피데믹 전투:강공격 직후 초기 콤보 회복 전 끝 폭발의 실제 치명 기대값",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var w=catalog.Weapons.Single(w=>w.Id==id);var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Epidemic");
            var b=new Build(id,new ModSlot?[8],new(Combo:2,BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Attack:"Heavy Attack",Exodia:new(exodia.Id,3)),Encounter:new(EncounterMode.FiveSeconds,NearbyEnemies:[new("blast",1,Defense:new(1e9,0))],Epidemic:[new(.1,EpidemicPhase.Explosion,2)]));Mods(b,"Corrupt Charge","Blood Rush","Organ Shatter");
            var r=CombatTimeline.Calculate(catalog,b,stances,2048);var expected=w.Damage.Values.Sum()*.3*(1+w.CriticalChance*(w.CriticalMultiplier*1.9-1));
            if(Math.Abs(r.Enemies![1].DirectDamage/expected-1)>.035)throw new Exception($"폭발 {r.Enemies[1].DirectDamage}, 실제1배 콤보 기대값 {expected}");
            Near(r.Points.First(p=>p.Time>=.1).Combo,1);
        });
        check("세라믹 투사체:킬링 블로우 제외·강공격 치확 배수와 콤보는 유지",()=>{
            var id=CeramicDaggerMechanic.WeaponId;var b=new Build(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true));Mods(b,"Killing Blow","True Steel");
            foreach(var burst in new[]{false,true}){var c=CeramicDaggerMechanic.Calculate(catalog,b,explosion:burst);Near(c.Damage.Values.Sum(),350*12);Near(c.CriticalChance,.1*(1+1.2*2));}
        });
        check("세라믹 레드 라이트 핸드:첫 단검만 치피+2·둘째/두 폭발 제외",()=>{
            var id=CeramicDaggerMechanic.WeaponId;var red=IncarnonEngine.ForWeapon(id)!.Perks.Single(p=>p.Condition=="FirstQuickMelee");var b=new Build(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true,Evolutions:[red.Id],Conditions:new(){["FirstQuickMelee"]=1}));
            Near(CeramicDaggerMechanic.Calculate(catalog,b).CriticalMultiplier,3.5);Near(CeramicDaggerMechanic.Calculate(catalog,b,2).CriticalMultiplier,1.5);Near(CeramicDaggerMechanic.Calculate(catalog,b,1,true).CriticalMultiplier,1.5);Near(CeramicDaggerMechanic.Calculate(catalog,b,2,true).CriticalMultiplier,1.5);
        });
    }
}
