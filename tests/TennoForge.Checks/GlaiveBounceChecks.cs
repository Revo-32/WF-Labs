using TennoForge.Core;

internal static class GlaiveBounceChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var rules=MeleeRuleCatalog.Load();
        Build B(string name="Glaive",string mod="Volatile Quick Return",int rank=3)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var m=catalog.Mods.Single(m=>m.EnglishName==mod);var slots=new ModSlot?[8];slots[0]=new(m.Id,rank);
            return new(w.Id,slots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:"Charged Throw"),Encounter:new(EncounterMode.FiveSeconds));
        }
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("글레이브 반사 폭발 불일치");}
        check("볼래틸:12종 글레이브의 충전/일반 폭발·콤보/컨오/강공격/관통감쇠 제외",()=>{
            foreach(var w in rules.Weapons.Where(w=>w.Raw.GetProperty("Class").GetString()=="Glaive"))foreach(var attack in new[]{"Throw","Charged Throw"})
            {
                var b=B(w.Name);var pp=catalog.Mods.Single(m=>m.EnglishName=="Pressure Point");var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");var kb=catalog.Mods.Single(m=>m.EnglishName=="Killing Blow");b.Slots[1]=new(pp.Id,pp.MaxRank);b.Slots[2]=new(co.Id,co.MaxRank);b.Slots[3]=new(kb.Id,kb.MaxRank);
                var baseHit=GlaiveBounceMechanic.Calculate(catalog,b,attack);var changed=GlaiveBounceMechanic.Calculate(catalog,b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {TargetIndex=3,Statuses=new(){[DamageType.Impact]=1,[DamageType.Viral]=1}}},attack);
                Near(baseHit.Damage.Values.Sum(),changed.Damage.Values.Sum());Near(changed.Melee!.Brackets!.AdditiveConditionOverload,0);Near(changed.Melee.Brackets.HeavyAttackDamage,0);Near(changed.Melee.Brackets.ComboMultiplier,1);Yes(!changed.Melee.Policy!.HitCombo);
            }
        });
        check("볼래틸:랭크0~3 발동25/50/75/100%·VQR 반경 가산·단일 폭발 강제 상태 구분",()=>{
            for(var rank=0;rank<4;rank++)Near(GlaiveBounceMechanic.Chance(B(rank:rank)),(rank+1)*.25);
            var b=B("Glaive Prime");var quick=GlaiveBounceMechanic.Calculate(catalog,b,"Throw");var rebound=GlaiveBounceMechanic.Calculate(catalog,B("Glaive Prime","Volatile Rebound"),"Throw");
            Near(quick.Melee!.AreaRadius!.Value-rebound.Melee!.AreaRadius!.Value,3);Yes(quick.Melee.ForcedProcs.SequenceEqual(new[]{"Impact"})&&rebound.Melee.ForcedProcs.Length==0);
            Yes(GlaiveBounceMechanic.Calculate(catalog,b,"Charged Throw").Melee!.ForcedProcs.Length==0);
        });
        check("볼래틸 전투:직격 후 폭발·접촉1회 콤보·폭발은 콤보 미획득/미소모",()=>{
            var b=B();var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);Yes(r.Events.Count(e=>e.Kind=="hit")==2);Yes(r.Events.Count(e=>e.Kind=="glaiveBounce")==1);Near(r.Points.First(p=>p.Time>0).ComboPoints,1);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
            var direct=r.Events.Where(e=>e.Kind=="hit").Sum(e=>e.Amount);Near(r.DirectDamage,direct);
        });
        check("볼래틸 전투:직격즉사 후 주변 폭발·벽무시·확장 반경과 대상별 방어",()=>{
            var b=B();var radius=GlaiveBounceMechanic.Calculate(catalog,b,"Charged Throw").Melee!.AreaRadius!.Value;
            b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("inside",radius,Defense:new(1e9,2700)),new("outside",radius+.01,Defense:new(1e9,0))],BlockedSight=[new(0,1)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);Near(r.Enemies![0].Health,0);Yes(r.Enemies[1].DirectDamage>0);Near(r.Enemies[2].DirectDamage,0);Yes(r.Events.Count(e=>e.Kind=="glaiveBounce")==1);
        });
        check("볼래틸 전투:랭크0 표본 발동률·확률표본 오차 안의25%",()=>{
            var r=CombatTimeline.Calculate(catalog,B(rank:0),stances,2048);
            // Each first-hit direct damage has the same distribution; compare
            // means with and without explosion through the per-trial samples.
            var b=B(rank:0);var reference=GlaiveBounceMechanic.Calculate(catalog,b,"Charged Throw");var direct=MeleeEngine.Calculate(catalog,b);
            var expected=direct.ExpectedHit+.25*reference.ExpectedHit;
            Yes(Math.Abs(r.DirectDamage-expected)<expected*.05);
        });
        check("볼래틸:일반 검 타격에는 폭발 없음·수동 기폭에 재귀 폭발 없음",()=>{
            foreach(var attack in new[]{"Normal Attack","Charged Throw Recall Explosion"})
            {var b=B();b=b with {Melee=b.Melee! with {Attack=attack}};var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0&&!r.Events.Any(e=>e.Kind=="glaiveBounce"));}
        });
    }
}
