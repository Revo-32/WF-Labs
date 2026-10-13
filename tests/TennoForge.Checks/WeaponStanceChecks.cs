using TennoForge.Core;

internal static class WeaponStanceChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id,string attack="Normal Attack")=>new(id,new ModSlot?[8],new(Combo:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Attack:attack));
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("무기 고유 동작 불일치");}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        StanceCalculation St(Build b,string key,string? sid=null){var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).First(s=>sid is null||s.Id==sid);return StanceEngine.Calculate(catalog,b with {Stance=new(s.Id,key,stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.Id)},stances);}
        check("무기 고유 동작:7개 무기 모든 장착 스탠스에서 동일 타격·성분",()=>{
            foreach(var id in new[]{WeaponStanceMechanic.Argo,WeaponStanceMechanic.Edun,WeaponStanceMechanic.Syam,WeaponStanceMechanic.Quassus,WeaponStanceMechanic.QuassusPrime,WeaponStanceMechanic.Verdilac,WeaponStanceMechanic.Korumm})
            {
                var key=id is WeaponStanceMechanic.Verdilac or WeaponStanceMechanic.Korumm?"Block":"Heavy";double? first=null;
                foreach(var s in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==id))){var c=St(B(id),key,s.Id);first??=c.ComboDamage.Values.Sum();Near(c.ComboDamage.Values.Sum(),first.Value);Yes(c.Combo.ResolutionNote!.Contains("고유"));}
            }
        });
        check("콰서스:76기초×2.5×12·5×6, 프라임18·9발·칼날 별도",()=>{
            foreach(var id in new[]{WeaponStanceMechanic.Quassus,WeaponStanceMechanic.QuassusPrime})
            {
                var c=St(B(id),"Heavy");var darts=c.Hits.Where(h=>h.SourceHit!.AttackType=="Ranged").ToArray();Yes(darts.Length==2);
                Near(darts[0].Damage.Values.Sum(),76*2.5);Near(darts[1].Damage.Values.Sum(),76*5);
                Near(darts[0].Count,id==WeaponStanceMechanic.Quassus?12:18);Near(darts[1].Count,id==WeaponStanceMechanic.Quassus?6:9);
                foreach(var h in darts){Near(h.Calculation!.Melee!.Projectiles,1);Yes(h.Calculation.Melee.ForcedProcs.Contains("Slash"));}
                var blade=catalog.Weapons.Single(w=>w.Id==id).Damage.Values.Sum();Near(c.ComboDamage.Values.Sum(),blade*10+76*(id==WeaponStanceMechanic.Quassus?60:90));
            }
        });
        check("아르고·샤암·에둔:별도 피해600/500/1500/480·반올림 스탠스 배율 역산 제외",()=>{
            Near(St(B(WeaponStanceMechanic.Argo),"Heavy").ComboDamage.Values.Sum(),600);
            var syam=St(B(WeaponStanceMechanic.Syam),"Heavy");Near(syam.Hits[0].Damage[DamageType.Heat],500);Near(syam.Hits[1].Damage[DamageType.Heat],1500);
            Yes(syam.Hits.All(h=>!h.Calculation!.Melee!.ForcedProcs.Contains("Slash")));
            Near(St(B(WeaponStanceMechanic.Edun),"Heavy").ComboDamage.Values.Sum(),480);
        });
        check("투사체 CO:콰서스 .4/.2 가산·샤암/버디락 별도 곱 브라켓",()=>{
            var b=B(WeaponStanceMechanic.Quassus);Mods(b,"Pressure Point","Condition Overload");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            var c=St(b,"Heavy");var d=c.Hits.Where(h=>h.SourceHit!.AttackType=="Ranged").ToArray();Near(d[0].Damage.Values.Sum(),190*(1+1.2+.8*.4));Near(d[1].Damage.Values.Sum(),380*(1+1.2+.8*.2));
            foreach(var id in new[]{WeaponStanceMechanic.Syam,WeaponStanceMechanic.Verdilac})
            {var t=B(id,id==WeaponStanceMechanic.Syam?"First Shockwave":"Energy Wave");Mods(t,"Pressure Point","Condition Overload");t=t with {Melee=t.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};Near(MeleeEngine.Calculate(catalog,t).Damage.Values.Sum(),(id==WeaponStanceMechanic.Syam?500:213)*2.2*1.8);}
        });
        check("에둔·샤암:슬라이딩 강공격은 일반 폴암/니카나 칼날과 강제 상태",()=>{
            foreach(var id in new[]{WeaponStanceMechanic.Edun,WeaponStanceMechanic.Syam})
            {
                var c=St(B(id),"Heavy Sliding");Yes(c.Hits.All(h=>h.Input!.Melee!.Attack=="Heavy Attack"&&h.SourceHit!.AttackType!="Ranged"));
                Near(c.ComboDamage.Values.Sum(),catalog.Weapons.Single(w=>w.Id==id).Damage.Values.Sum()*(id==WeaponStanceMechanic.Syam?10:12));
                if(id==WeaponStanceMechanic.Syam)Yes(c.Hits.All(h=>h.Calculation!.Melee!.ForcedProcs.Contains("Slash")));
            }
        });
        check("콰서스 프라임:공식 Export ID 별칭으로 한국어 이름·기존 빌드 ID 유지",()=>{
            var w=catalog.Weapons.Single(w=>w.Id==WeaponStanceMechanic.QuassusPrime);Yes(w.Name=="콰서스 프라임"&&w.EnglishName=="Quassus Prime");
        });
        check("버디락:4/2/5배 투사체·명중 콤보 없음·35m·폭발2m와0%상확",()=>{
            var c=St(B(WeaponStanceMechanic.Verdilac),"Block");var waves=c.Hits.Where(h=>h.SourceHit!.AttackType=="Ranged").ToArray();Yes(waves.Length==3);
            for(var i=0;i<3;i++){Near(waves[i].Damage[DamageType.Toxin],213*new[]{4,2,5}[i]);Yes(!waves[i].Calculation!.Melee!.Policy!.HitCombo);}
            var b=B(WeaponStanceMechanic.Verdilac,"Energy Wave");Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=35.01}}).ExpectedHit,0);
            b=b with {Melee=b.Melee! with {Attack="Energy Wave Explosion",Distance=2}};var burst=MeleeEngine.Calculate(catalog,b);Near(burst.Damage[DamageType.Electricity],2);Near(burst.StatusChance,0);Near(burst.Melee!.AreaRadius!.Value,2);Yes(!burst.Melee.Policy!.HitCombo);
        });
        check("샤암:파동230m·직접 근접 사거리 모드와 칼날 관통 배율 제외",()=>{
            var b=B(WeaponStanceMechanic.Syam,"First Shockwave");Mods(b,"Primed Reach");var first=MeleeEngine.Calculate(catalog,b);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=230,TargetIndex=3}}).ExpectedHit,first.ExpectedHit);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=230.01}}).ExpectedHit,0);
        });
        check("코룸 특수 슬램:1040 전기×슬램×팩션만·전기 모드는 상태 피해에만",()=>{
            var b=B(WeaponStanceMechanic.Korumm,"Tactical Combo Slam Attack");Mods(b,"Pressure Point","Condition Overload","True Steel","Melee Prowess","Shocking Touch","Seismic Wave","Smite Grineer","Melee Elementalist");
            b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Faction="Grineer",Distance=10,Statuses=new(){[DamageType.Impact]=1}}};
            var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage[DamageType.Electricity],1040*3*1.3);Yes(c.Damage.Count==1);Near(c.CriticalChance,0);Near(c.CriticalMultiplier,1);Near(c.StatusChance,.5);
            Near(c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Electricity).DamagePerTick,1040*3*1.3*1.3*1.9*1.9*.5);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=10.01}}).ExpectedHit,0);
        });
    }
}
