using TennoForge.Core;

internal static class WarframeStatusArcaneChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("워프레임 상태 아케인 규칙 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(){var id=c.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Arcanes:new(Warframe1:new(WarframeStatusArcanes.Truculence,0)),Encounter:new(EncounterMode.FiveSeconds,Inputs:[new(0,"PlayerOverguardGain",3000)]));}
        check("트루큘런스: 실제 획득 누적3000·잉여 보존·한 번에 여러 파동",()=>{
            var state=new TruculenceState(2900);Yes(state.Gain(99)==0);Near(state.Progress,2999);Yes(state.Gain(1)==1);Near(state.Progress,0);Yes(state.Gain(9500)==3);Near(state.Progress,500);Near(state.Waves,4);
        });
        check("트루큘런스: 플레이어 중심 랭크0 5m·시야·정확히10 바이러스",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("edge",5),new("outside",5.01),new("blocked",1)],BlockedSight=[new(-1,3)]}};var result=CombatTimeline.Calculate(c,b,stances,4);Yes(result.Unsupported.Length==0);
            var waves=result.Events.Where(e=>e.Kind=="truculenceHit").ToArray();Yes(waves.Length==2&&waves.Any(e=>e.Description.StartsWith("대상1"))&&waves.Any(e=>e.Description.StartsWith("대상2")));Yes(waves.All(e=>e.Amount==10));
            b=b with {Encounter=b.Encounter! with {BlockedSight=[new(-1,0),new(-1,1),new(-1,3)]}};result=CombatTimeline.Calculate(c,b,stances,4);Yes(!result.Events.Any(e=>e.Kind=="truculenceHit"));
        });
        check("트루큘런스: 파동을 직접 피해로 더하지 않으며 첫타에 바이러스10중첩 적용",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {Mode=EncounterMode.FirstHit}};var result=CombatTimeline.Calculate(c,b,stances,1);var ordinary=CombatTimeline.Calculate(c,b with {Arcanes=null,Encounter=b.Encounter! with {Inputs=[]}},stances,1);Near(result.DirectDamage,ordinary.DirectDamage*4.25);Near(result.StatusDamage,0);
            var large=CombatTimeline.Calculate(c,b with {Encounter=b.Encounter! with {Inputs=[new(0,"PlayerOverguardGain",9500)]}},stances,1);Near(large.DirectDamage,result.DirectDamage);Near(large.Events.Single(e=>e.Kind=="truculenceWave").Amount,3);
        });
        check("유니버설 폴아웃 상태: 모든 랭크60%상한·누적 방사능·오브는 습득과 별개",()=>{
            var low=new UniversalFalloutState(0);low.Apply(10);Near(low.DropChance,.1);low.Apply(1000);Near(low.DropChance,.6);Yes(low.Drops(.599)&&!low.Drops(.6));var high=new UniversalFalloutState(5);high.Apply(10);Near(high.DropChance,.6);
        });
        check("유니버설 폴아웃: 상태 만료 후에도 영구60%·오브 생성과 회복 구분",()=>{
            var b=B();b=b with {Scenario=new(Enemy:new(1,0)),Arcanes=new(Warframe1:new(WarframeStatusArcanes.Fallout,0)),Melee=new(Conditions:new(){["UniversalStatusDuration"]=-100}),Encounter=new(EncounterMode.FirstHit,Inputs:[new(0,"AbilityRadiationInfliction",60)])};var result=CombatTimeline.Calculate(c,b,stances,256);Yes(result.Unsupported.Length==0);var orbs=result.Recovery!.DroppedUniversalOrbs!;Yes(orbs.Mean>.45&&orbs.Mean<.75);Yes(result.Recovery.Energy is null&&result.Recovery.HysteriaHealing is null);Yes(result.Events.Any(e=>e.Kind=="falloutDrop"&&e.Description.Contains("60%")));
        });
        check("유니버설 폴아웃: 일반 무기의 방사능은 어빌리티로 집계하지 않음",()=>{
            var b=B();b=b with {Arcanes=new(Warframe1:new(WarframeStatusArcanes.Fallout,5)),Scenario=new(Combo:12,Enemy:new(1000,0)),Encounter=new(EncounterMode.FiveSeconds)};
            foreach(var (name,slot) in new[]{("Volcanic Edge",0),("Voltaic Strike",1),("Weeping Wounds",2)}){var mod=c.Mods.Single(m=>m.EnglishName==name);b.Slots[slot]=new(mod.Id,mod.MaxRank);}var result=CombatTimeline.Calculate(c,b,stances,128);Yes(result.Unsupported.Length==0&&result.Ttk.KillFraction>0);Near(result.Recovery!.DroppedUniversalOrbs!.Mean,0);
        });
        check("유니버설 폴아웃: 윕클로의 실제 방사능 명중과 처치에 연결",()=>{
            var b=B();var id=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Whipclaw).Id;
            b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Arcanes=new(Warframe1:new(WarframeStatusArcanes.Fallout,5)),Scenario=new(Combo:12,Enemy:new(10000,0)),Melee=new(ComboDurationOverride:1),Encounter=new(EncounterMode.FiveSeconds)};
            foreach(var (name,slot) in new[]{("Volcanic Edge",0),("Voltaic Strike",1),("Weeping Wounds",2)}){var mod=c.Mods.Single(m=>m.EnglishName==name);b.Slots[slot]=new(mod.Id,mod.MaxRank);}
            var result=CombatTimeline.Calculate(c,b,stances,256);Yes(result.Unsupported.Length==0&&result.Ttk.KillFraction>0);Yes(result.Recovery!.DroppedUniversalOrbs!.Mean>0);Yes(result.Events.Any(e=>e.Kind=="falloutDrop"));
        });
    }
}
