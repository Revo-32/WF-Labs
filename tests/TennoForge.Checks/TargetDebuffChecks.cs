using TennoForge.Core;

internal static class TargetDebuffChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("대상 어빌리티 효과 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string? id=null){id??=c.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,2700)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));}
        check("대상 취약도:페트리파이·돔·매스·스플린터 별도 브라켓·위력 적용 예외",()=>{
            var setup=new MeleeSetup(Debuffs:[new("Petrify",Strength:10),new("Strangledome",Strength:10),new("MassVitrify",Strength:2),new("SplinterStorm",Strength:2)]);Near(TargetDebuffs.Incoming(setup),1.5*2*2*1.7);Near(TargetDebuffs.Incoming(setup,true),1.5*2*1.7);
            for(var rank=0;rank<=3;rank++){Near(TargetDebuffs.Incoming(new(Debuffs:[new("MassVitrify",rank,2)])),1+new[]{.2,.3,.4,.5}[rank]*2);Near(TargetDebuffs.Incoming(new(Debuffs:[new("SplinterStorm",rank,2)])),1+new[]{.15,.2,.25,.35}[rank]*2);}
        });
        check("대상 취약도:프레셔·CO·원소·상태 원천을 바꾸지 않고 받는 피해에 한 번 적용",()=>{
            var b=B();var mod=c.Mods.Single(m=>m.EnglishName=="Pressure Point");b.Slots[0]=new(mod.Id,mod.MaxRank);var plain=MeleeEngine.Calculate(c,b);var weakened=MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Debuffs=[new("Petrify")]}});Near(weakened.RawDps,plain.RawDps);Near(weakened.Melee!.ModdedBaseDamage,plain.Melee!.ModdedBaseDamage);Near(weakened.TargetDps,plain.TargetDps*1.5);Near(weakened.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,plain.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick);var equation=DpsEquation.From(weakened);Near(equation.Terms.Single(t=>t.Id=="vulnerability").Value,1.5);Near(equation.Evaluate(),weakened.TargetDps);
        });
        check("윕클로:인스네어×2는 어빌리티 원천 배수·스트랭글돔 취약도×2는 제외",()=>{
            var id=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Whipclaw).Id;var b=B(id);var plain=MeleeEngine.Calculate(c,b);var ensnared=MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Debuffs=[new("Ensnare"),new("Strangledome")]}});Near(ensnared.RawDps,plain.RawDps*2);Near(ensnared.Melee!.IncomingDamageMultiplier,1);Near(ensnared.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,plain.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick*2);var ordinary=B();Near(MeleeEngine.Calculate(c,ordinary with {Melee=ordinary.Melee! with {Debuffs=[new("Ensnare")]}}).RawDps,MeleeEngine.Calculate(c,ordinary).RawDps);
        });
        check("대상 효과:주변 적에게 자동 복사하지 않고 대상별 설정을 보존",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Debuffs=[new("Petrify")]}};var noBuff=CombatScene.ForEnemy(b,new("other",1));var other=CombatScene.ForEnemy(b,new("other",1,Debuffs:[new("SplinterStorm",Strength:2)]));Near(MeleeEngine.Calculate(c,noBuff).Melee!.IncomingDamageMultiplier,1);Near(MeleeEngine.Calculate(c,other).Melee!.IncomingDamageMultiplier,1.7);
        });
        check("대상 취약도 시간 경로:직접 피해·상태 피해 각각×1.5·상태에 이중 적용 없음",()=>{
            var b=B();var ordinary=CombatTimeline.Calculate(c,b,stances,8);var weakened=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Debuffs=[new("Petrify",Remaining:10)]}},stances,8);Yes(weakened.Unsupported.Length==0);Near(weakened.DirectDamage,ordinary.DirectDamage*1.5);Yes(ordinary.StatusDamage>0);Near(weakened.StatusDamage,ordinary.StatusDamage*1.5);
        });
        check("대상 취약도 만료:기존 베기의 다음 틱에도 현재 취약도를 적용·원천에 영구 고정하지 않음",()=>{
            var b=B(ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Clones).Id);b=b with {Scenario=b.Scenario with {BaseInterval=60}};var ordinary=CombatTimeline.Calculate(c,b,stances,1);var weakened=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Debuffs=[new("Petrify",Remaining:1.5)]}},stances,1);CombatPoint At(CombatTimelineResult r,double t)=>r.Points.First(p=>Math.Abs(p.Time-t)<1e-8);Near(At(weakened,1).StatusDamage,At(ordinary,1).StatusDamage*1.5);Near(At(weakened,2).StatusDamage-At(weakened,1).StatusDamage,At(ordinary,2).StatusDamage-At(ordinary,1).StatusDamage);
        });
        check("대상 약화:원문 잔여시간 경계·오버가드의 CC 면역과 비CC 효과 구분",()=>{
            Yes(TargetDebuffs.AtTime([new("Petrify",Remaining:1)],1).Length==0);Near(TargetDebuffs.Incoming(new(Overguard:100,Debuffs:[new("Petrify"),new("Strangledome"),new("MassVitrify"),new("SplinterStorm")])),1.35);
        });
        check("군중제어면역:석화/돔/유리화/인스네어효과제외·스플린터는유지",()=>{
            var m=new MeleeSetup(Conditions:new(){["ControlImmune"]=1},Debuffs:[new("Petrify"),new("Strangledome"),new("MassVitrify"),new("Ensnare"),new("SplinterStorm")]);Near(TargetDebuffs.Incoming(m),1.35);Near(TargetDebuffs.WhipclawMultiplier(m),1);Yes(TargetDebuffs.Dome(m) is null);
        });
        check("초기오버가드:막힌석화가오버가드파괴뒤자동활성화되지않음",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Overguard=1}};var plain=CombatTimeline.Calculate(c,b,stances,1);var blocked=CombatTimeline.Calculate(c,b with {Melee=b.Melee with {Debuffs=[new("Petrify")]}},stances,1);Yes(blocked.Unsupported.Length==0);Near(blocked.DirectDamage,plain.DirectDamage);Near(blocked.StatusDamage,plain.StatusDamage);
        });
    }
}
