using TennoForge.Core;

internal static class FirstContactChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("첫 접촉 실제 사건 불일치");}
        void Near(double x,double y){if(Math.Abs(x-y)>1e-8*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        Build B(string name,string attack,params string[] mods){var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];for(var i=0;i<mods.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==mods[i]);slots[i]=new(m.Id,m.MaxRank);}return new(w.Id,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack),Encounter:new(EncounterMode.FirstHit));}
        EncounterResult C(Build b,int trials=2048){var r=EncounterEngine.Calculate(catalog,b,stances,trials);Yes(r.Unsupported.Length==0&&r.Timeline is {Unsupported.Length:0,Seconds:0});return r;}
        check("첫 타격 실제 경로: 강제 상태 직후 힐링리턴·프라이머 지움·표본 오차",()=>{
            var b=B("Glaive Prime","Charged Throw Recall Explosion","Healing Return");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=10}},Scenario=b.Scenario with {ExistingStatusTypes=5}};
            var r=C(b);var healing=r.Timeline!.Recovery!.HealingReturn!;
            Yes(Math.Abs(healing.Mean-11*2.34)<4*healing.StandardError);Yes(r.EndStatusTypes.High>2&&r.EndStatusTypes.High<3);Yes(r.Precision is not null&&r.AverageDps is null&&r.Timeline.StatusDamage==0);
            var kill=C(b with {Scenario=new(Enemy:new(1,0))},64);Near(kill.Damage.High,1);Near(kill.Timeline!.Recovery!.HealingReturn!.Mean,0);
        });
        check("첫 타격 산탄: 후속 탄환은 앞 탄환 상태로CO상승·동일타 전체 고정값과 구분",()=>{
            var b=B("Redeemer Prime","Heavy Attack","Healing Return","Condition Overload");var r=C(b);var naked=EncounterEngine.CalculateOrdered(catalog,b,stances);
            Yes(r.Damage.Low>naked.Damage.High*1.3);var c=MeleeEngine.Calculate(catalog,b);Yes(r.Timeline!.Events.Count(e=>e.Kind=="hit")==c.Melee!.Projectiles);Yes(r.Timeline.Recovery!.HealingReturn!.Mean>11*c.Melee.Projectiles);
            var states=c.Damage.Keys.Concat(c.Melee.ForcedProcs.Where(p=>Enum.TryParse<DamageType>(p,out _)).Select(Enum.Parse<DamageType>)).Distinct().ToDictionary(t=>t,_=>1);
            var primer=C(b with {Melee=b.Melee! with {Statuses=states},Encounter=new(EncounterMode.Primer)},64);Near(primer.Timeline!.Recovery!.HealingReturn!.Mean,11*states.Count*c.Melee.Projectiles);
        });
        check("첫 마무리: 전체동작/개별타격 시각 없어도 선택한 첫 접촉만 계산",()=>{
            var b=B("Rumblejack","Normal Attack");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher Front"));
            b=b with {Stance=new(s.Id,"Finisher Front",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.IsDefault?null:s.Id)};
            var r=C(b,64);Yes(StanceEngine.Calculate(catalog,b,stances).Duration is null);Yes(r.Timeline!.Events.Count(e=>e.Kind=="hit")==1&&r.Damage.Low>0);
        });
    }
}
