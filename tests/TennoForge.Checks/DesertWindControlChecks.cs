using TennoForge.Core;

internal static class DesertWindControlChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        var id=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Wind).Id;
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ExaltedWaves:[new(0,.1,6)]));
        void Yes(bool value){if(!value)throw new Exception("서린 스톰 무장 해제 불일치");}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,8);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        check("데저트 윈드:명시된 군중 제어만 무장 해제·오버가드·다른 무기 제외",()=>{
            foreach(var d in ExaltedRules.All.Where(d=>d.Family==ExaltedFamily.Wind))
            {
                foreach(var proc in new[]{"Knockdown","Lifted","Ragdoll","Stagger"}){Yes(DesertWindControl.Applies(d.Id,[proc],0));Yes(!DesertWindControl.Applies(d.Id,[proc],1));}
                Yes(DesertWindControl.Applies(d.Id,[],0,true));Yes(!DesertWindControl.Applies(d.Id,["Slash"],0));
            }
            Yes(!DesertWindControl.Applies(ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Blade).Id,["Stagger"],0));
        });
        check("데저트 윈드:일반 파동 강제 비틀거림·영구 무장 해제 한 번·CO 추가 없음",()=>{
            var b=B();var f=b.Encounter!.ExaltedWaves![0];var source=ExaltedWaveFlight.Resolve(catalog,stances,b,f);var c=ExaltedWaveFlight.Calculate(catalog,b,source,f);
            Yes(c.Melee!.ForcedProcs.Contains("Stagger"));
            b=b with {Encounter=b.Encounter with {ExaltedWaves=[f,new(.2,.3,6,Projectile:2,Cast:2)]}};
            var r=Run(b);Yes(r.Enemies![0].DisarmFraction==1&&r.Events.Count(e=>e.Kind=="disarm")==1);
            Yes(TargetStatusRules.Count(b with {Melee=b.Melee! with {Conditions=new(){["Disarmed"]=1,["Stagger"]=1}}})==0);
            Yes(r.Enemies[0].StatusTypes<=1); // Only the weapon's Impact status is possible.
        });
        check("데저트 윈드:적별 무장 해제·가려진 적·오버가드 유지/파괴 구분",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("가려짐",4,Defense:new(1e9,0)),new("보호됨",6,Defense:new(1e9,0),Overguard:1e9),new("보호 파괴",8,Defense:new(1e9,0),Overguard:1)],BlockedSight=[new(-1,1)],ExaltedWaves=Enumerable.Range(1,4).Select(i=>new ExaltedWaveContact(0,.1*i,2*i,Target:i)).ToArray()}};
            var r=Run(b);Yes(r.Enemies!.Select(e=>e.DisarmFraction).SequenceEqual(new double?[]{1,0,0,1}));Yes(r.Enemies![1].DirectDamage==0);
        });
        check("데저트 윈드:즉사에는 무장 해제 발동 없음·20m 밖 미적용",()=>{
            var b=B();Yes(Run(b with {Scenario=b.Scenario with {Enemy=new(1,0)}}).Enemies![0].DisarmFraction==0);
            Yes(Run(b with {Encounter=b.Encounter! with {ExaltedWaves=[new(0,.5,20.01)]}}).Enemies![0].DisarmFraction==0);
        });
    }
}
