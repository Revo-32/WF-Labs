using System.Text.Json;
using TennoForge.Core;

internal static class ExaltedDetailChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(ExaltedFamily family=ExaltedFamily.Blade){var id=ExaltedRules.All.First(d=>d.Family==family).Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds));}
        void Yes(bool value){if(!value)throw new Exception("익절티드 세부 규칙 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        check("디와타:공식38.5.3 엑실러스 복원·막기/패리 허용·텐노카이 제외",()=>{
            foreach(var d in ExaltedRules.All.Where(d=>d.Family==ExaltedFamily.Diwata))
            {
                Yes(d.Exilus);var b=B(ExaltedFamily.Diwata) with {WeaponId=d.Id,Capacity=CapacityCatalog.Load().Default(d.Id,null)};
                foreach(var name in new[]{"Parry","Focused Defense"})
                {var m=catalog.Mods.Single(m=>m.EnglishName==name);var rule=MeleeRuleCatalog.Load().Mods.Single(r=>r.Id==m.Id);Yes(ExaltedRules.ModAllowed(d.Id,rule));var equipped=b with {Melee=b.Melee! with {Exilus=new(m.Id,m.MaxRank)}};Yes(MeleeEngine.Calculate(catalog,equipped).ExpectedHit>0);Yes(CapacityEngine.Calculate(catalog,equipped).ExilusCost>0);}
                var merit=MeleeRuleCatalog.Load().Mods.Single(m=>m.Name=="Discipline's Merit");Yes(!ExaltedRules.ModAllowed(d.Id,merit));
            }
        });
        check("블레이드 일반 파동:발사 동작과 명중 동작 분리·유휴100%·정지 첫타300%",()=>{
            var b=B();var f=new ExaltedWaveContact(0,.1,3,Combo:"Forward",FalloffModel:1);var source=ExaltedWaveFlight.Resolve(catalog,stances,b,f);
            var idle=ExaltedWaveFlight.Calculate(catalog,b,source,f);var neutral=ExaltedWaveFlight.Calculate(catalog,b,source,f with {ImpactCombo="Neutral",ImpactGroup=1});Near(neutral.Damage.Values.Sum(),idle.Damage.Values.Sum()*3);
            var forward=ExaltedWaveFlight.Calculate(catalog,b,source,f with {ImpactCombo="Forward",ImpactGroup=1});Near(forward.ExpectedHit,idle.ExpectedHit);
            var twice=ExaltedWaveFlight.Calculate(catalog,b,source,f with {ImpactCombo="Block",ImpactGroup=2});Near(twice.Damage.Values.Sum(),2*idle.Damage.Values.Sum());
            var copy=JsonSerializer.Deserialize<ExaltedWaveContact>(JsonSerializer.Serialize(f with {ImpactCombo="Block",ImpactGroup=2},Catalog.Json),Catalog.Json)!;Near(ExaltedWaveFlight.Calculate(catalog,b,source,copy).ExpectedHit,twice.ExpectedHit);
        });
        check("교차 파동:한 텐노카이 기회는 한 발사 입력만 보존·두 번째 공격은 콤보 소모",()=>{
            var b=B();var merit=catalog.Mods.Single(m=>m.EnglishName=="Discipline's Merit");
            b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Attack="Heavy Attack",Tennokai=true,Exilus=new(merit.Id,3)},Encounter=b.Encounter! with {ScriptedAttacksOnly=true,ExaltedWaves=[new(0,.1,3,Combo:"Heavy",FalloffModel:1),new(0,.1,3,Projectile:2,Combo:"Heavy",Wave:2,FalloffModel:1),new(.2,.3,3,Projectile:3,Combo:"Heavy",Cast:2,FalloffModel:1)]}};
            var r=Run(b);var spent=r.Events.Where(e=>e.Kind=="heavyCombo").ToArray();Yes(spent.Length==2);Near(spent[0].Amount,0);Near(spent[1].Amount,220);Near(r.Points.Last().TennokaiUses,1);
        });
        check("슬라이드 실명:5m와6초의 독립 거리/지속·시야/직전 인지·오버가드 면역",()=>{
            var m=new MeleeSetup(AbilityRange:2,AbilityDuration:.5);Near(ExaltedBlind.Radius(m),10);Near(ExaltedBlind.Duration(m),3);
            Yes(ExaltedBlind.Affects(10,0,true,false,m));Yes(!ExaltedBlind.Affects(10.001,0,true,false,m));Yes(!ExaltedBlind.Affects(1,1,true,true,m));Yes(!ExaltedBlind.Affects(1,0,false,false,m));Yes(ExaltedBlind.Affects(1,0,false,true,m));
            var b=B() with {Melee=m};b=b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=true,NearbyEnemies=[new("가려짐",3,Defense:new(1e9,0)),new("오버가드",4,Defense:new(1e9,0),Overguard:100),new("범위 밖",10.001,Defense:new(1e9,0))],BlockedSight=[new(-1,1)],Inputs=[new(0,"ExaltedSlideBlind",25)]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="bladeBlind")==1);Near(r.DirectDamage+r.StatusDamage,0);
            var input=new CombatInput(0,"ExaltedSlideBlind",25,RecentlyAwareTargets:[2]);var copy=JsonSerializer.Deserialize<CombatInput>(JsonSerializer.Serialize(input,Catalog.Json),Catalog.Json)!;Yes(copy.RecentlyAwareTargets!.SequenceEqual(new[]{2}));
            r=Run(b with {Encounter=b.Encounter! with {Inputs=[copy]}});Yes(r.Events.Count(e=>e.Kind=="bladeBlind")==2);
        });
        check("슬라이드 실명:지속 만료 후 은신 가산 제거·CO 상태 종류/피니셔로 잘못 추가하지 않음",()=>{
            var b=B() with {Melee=new(AbilityDuration:.25)};var normal=Run(b);
            b=b with {Encounter=b.Encounter! with {Inputs=[new(0,"ExaltedSlideBlind",25)]}};var r=Run(b);
            Yes(r.Points.Single(p=>p.Time==1).SnapshotDps>normal.Points.Single(p=>p.Time==1).SnapshotDps);
            Near(r.Points.Single(p=>p.Time==1.5).SnapshotDps,normal.Points.Single(p=>p.Time==1.5).SnapshotDps);
            var c=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Blinded"]=1}}});var m=c.Melee!;Near(m.StealthBonus,7);Near(TargetStatusRules.Count(b with {Melee=m.Setup}),0);Yes(m.Setup.Finisher is null);
        });
    }
}
