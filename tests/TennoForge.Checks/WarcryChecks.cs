using System.Text.Json;
using TennoForge.Core;

internal static class WarcryChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool b){if(!b)throw new Exception("워크라이 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name="Skana",WarcrySetup? setup=null){var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(ExternalEquipment:new(Warcry:setup??new())),Arcanes:new(PlayerArmor:1710),Encounter:new(EncounterMode.FiveSeconds));}
        CombatTimelineResult Run(Build b,int n=1){var r=CombatTimeline.Calculate(catalog,b,stances,n);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("워크라이:현행20초·전랭크·퓨리와가산·헬민스30%·시전자위력",()=>{
            foreach(var r in Enumerable.Range(0,4))
            {
                var s=new WarcrySetup(Rank:r,Strength:1.3);Near(WarcryRules.Speed(s),new[]{.15,.2,.25,.5}[r]*1.3);Near(WarcryRules.Speed(s with {Helminth=true}),new[]{.09,.12,.15,.3}[r]*1.3);Near(WarcryRules.BaseDuration(s),new[]{10d,14,17,20}[r]);
            }
            var b=B(setup:new(Strength:1.3));var fury=catalog.Mods.Single(m=>m.EnglishName=="Fury");b.Slots[0]=new(fury.Id,fury.MaxRank);
            var c=MeleeEngine.Calculate(catalog,b);var plain=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {ExternalEquipment=null}});Near(c.AttackSpeed/plain.AttackSpeed,1.95/1.3);
        });
        check("워크라이 방어력:855기본값·모드방어력에재곱하지않음·히스테리아3배·반복계산안정",()=>{
            var b=B(setup:new(Strength:1.3));Near(1710+WarcryRules.Armor(b.Melee!.ExternalEquipment!.Warcry),855*2.65);
            Near(WarcryRules.Armor(new(Strength:1.3,HysteriaActive:true)),855*.65*3);
            var threshold=B(setup:new(BaseArmor:200)) with {Arcanes=new(PlayerArmor:400)};
            Near(WarframeCombatArcanes.ApplyConditions(threshold).Melee!.Conditions!["Armor450"],1);
            Near(threshold.Arcanes.PlayerArmor!.Value,400);Near(MeleeEngine.Calculate(catalog,threshold).ExpectedHit,MeleeEngine.Calculate(catalog,threshold).ExpectedHit);
        });
        check("이터널워:처치당랭크별지속·잔여2배상한·만료뒤부활불가·도약제외",()=>{
            foreach(var rank in Enumerable.Range(0,4))
            {
                var s=new WarcryState(new(Duration:1.5,EternalWarRank:rank));Near(s.Kill(2,true),new[]{1.25,1.5,1.75,2}[rank]*1.5);
                for(var i=0;i<100;i++)s.Kill(2,true);Near(s.Remaining(2),60);Near(s.Kill(2,true),0);Near(s.Kill(70,true),0);
            }
            var leap=new WarcryState(new(EternalWarRank:3));Near(leap.Kill(0,true,hystericalLeap:true),0);Near(leap.Kill(0,false),0);
        });
        check("워크라이 수신:자기시전만료·아군널리파이어와재진입·시전자해제·원반예외",()=>{
            var own=new WarcryState(new(EternalWarRank:3));Yes(own.Cast(1));Near(own.Remaining(1),20);own.Lose();Yes(!own.Reenter(2));Yes(own.Cast(2));Near(own.Remaining(2),20);
            var ally=new WarcryState(new(EternalWarRank:3,SelfCast:false));Near(ally.Kill(1,true,disc:true),0);Near(ally.Kill(1,true),2);
            ally.Lose();Near(ally.Remaining(2),0);Yes(ally.Reenter(3));Near(ally.Remaining(3),19);ally.Lose(true);Yes(!ally.Reenter(4));
        });
        check("워크라이 시간계산:만료공속·방어력·캐시변화·재시전20초",()=>{
            var b=B(setup:new(Remaining:1));var r=Run(b);var active=r.Points.Single(p=>p.Time==0);var ended=r.Points.First(p=>p.Time>=1);
            Near(active.PlayerArmor!.Value,2137.5);Near(ended.PlayerArmor!.Value,1710);Near(active.SnapshotDps/ended.SnapshotDps,1.5);Near(ended.WarcryRemaining!.Value,0);
            var recast=Run(b with {Encounter=b.Encounter! with {Inputs=[new(2,"WarcryCast")]}});Near(recast.Points.Last().WarcryRemaining!.Value,17);Yes(recast.Events.Any(e=>e.Kind=="warcryCast"));
        });
        check("이터널워 실제처치:제니스타원반·자신시전만·처치이벤트한번",()=>{
            foreach(var self in new[]{true,false})
            {
                var b=B("Zenistar",new(EternalWarRank:3,SelfCast:self));b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {ScriptedAttacksOnly=true,ZenistarDiscs=[new(0,.1,AuraTimes:[1])]}};
                var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="eternalWar")== (self?1:0));Near(r.Points.Last().WarcryRemaining!.Value,self?17:15);
            }
        });
        check("이터널워 외부행동:도움제외·아군시전자처치·무효화후재진입",()=>{
            var own=B(setup:new(EternalWarRank:3));own=own with {Encounter=own.Encounter! with {Inputs=[new(.1,"MeleeAssist"),new(.2,"MeleeKill"),new(.3,"FinisherKill")]}};
            var r=Run(own);Near(r.Points.Last().WarcryRemaining!.Value,19);Yes(r.Events.Count(e=>e.Kind=="eternalWar")==2);
            var ally=B(setup:new(EternalWarRank:3,SelfCast:false));ally=ally with {Encounter=ally.Encounter! with {Inputs=[new(.1,"MeleeKill"),new(1,"AbilityNullified"),new(2,"WarcryCasterMeleeKill"),new(3,"WarcryReenter")]}};
            var a=Run(ally);Near(a.Points.Single(p=>p.Time==1).WarcryRemaining!.Value,0);Near(a.Points.Last().WarcryRemaining!.Value,17);
        });
        check("워크라이 공식개편:유지중재시전갱신·히스테리아해제즉시방어력변경",()=>{
            var b=B(setup:new(Remaining:18));b=b with {Encounter=b.Encounter! with {Inputs=[new(2,"WarcryCast")]}};Near(Run(b).Points.Last().WarcryRemaining!.Value,17);
            var id="/Lotus/Powersuits/Berserker/BerserkerMelee";var talons=B(setup:new(HysteriaActive:true));talons=talons with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Encounter=talons.Encounter! with {Inputs=[new(1,"ExaltedDeactivate")]}};
            var r=Run(talons);Near(r.Points[0].WarcryArmor,855*.5*3);Near(r.Points.Single(p=>p.Time==1).WarcryArmor,855*.5);Near(r.Points.Last().PlayerArmor!.Value,1710+855*.5);
        });
        check("워크라이 저장:원본모델왕복·이전빌드기본null·과도잔여거부",()=>{
            var b=B(setup:new(EternalWarRank:1,Helminth:true,Strength:1.7,Duration:.5,BaseArmor:370));var restored=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b))!;Yes(restored.Melee!.ExternalEquipment==b.Melee!.ExternalEquipment);Yes(new MeleeExternalEquipment().Warcry is null);
            try{WarcryRules.Validate(new(Remaining:21));throw new Exception("과도한 잔여 허용");}catch(ArgumentException){}
        });
    }
}
