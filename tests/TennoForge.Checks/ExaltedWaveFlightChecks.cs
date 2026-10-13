using TennoForge.Core;

internal static class ExaltedWaveFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(ExaltedFamily family){var id=ExaltedRules.All.First(d=>d.Family==family).Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ExaltedWaves:[new(0,.1,family==ExaltedFamily.Blade?3:6,FalloffModel:family==ExaltedFamily.Blade?1:0)]));}
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("익절티드 파동 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 파동 입력 허용: "+text);}
        check("익절티드 파동:실제 스탠스 파동·입력 묶음·30m/s와 미기재 속도 구분",()=>{
            var blade=B(ExaltedFamily.Blade);Reject(()=>ExaltedWaveFlight.Validate(catalog,stances,blade,[new(0,.1,6,FalloffModel:1)],1),"30m/s");
            Reject(()=>ExaltedWaveFlight.Validate(catalog,stances,blade,[new(0,.1,3)],1),"충돌");
            Reject(()=>ExaltedWaveFlight.Validate(catalog,stances,blade,[new(0,0,0),new(0,0,0,Projectile:2)],1),"복제");
            var wind=B(ExaltedFamily.Wind);ExaltedWaveFlight.Validate(catalog,stances,wind,wind.Encounter!.ExaltedWaves!,1);
        });
        check("블레이드 파동:시간식·거리식 분리·관통20%/15m·강 파동 예외",()=>{
            var b=B(ExaltedFamily.Blade);var f=new ExaltedWaveContact(0,1,30,FalloffModel:1);var source=ExaltedWaveFlight.Resolve(catalog,stances,b,f);var zero=ExaltedWaveFlight.Calculate(catalog,b,source,f with {Distance=0}).Damage.Values.Sum();
            Near(ExaltedWaveFlight.Calculate(catalog,b,source,f).Damage.Values.Sum(),zero*.6);Near(ExaltedWaveFlight.Calculate(catalog,b,source,f with {FalloffModel=2}).Damage.Values.Sum(),zero*(1-30d/70));Near(ExaltedWaveFlight.Calculate(catalog,b,source,f,2).Damage.Values.Sum(),zero*.4);
            Near(ExaltedWaveFlight.Calculate(catalog,b,source,f with {Distance=60},2).Damage.Values.Sum(),0);
            var heavy=f with {Combo="Heavy"};var hs=ExaltedWaveFlight.Resolve(catalog,stances,b,heavy);var h1=ExaltedWaveFlight.Calculate(catalog,b,hs,heavy);var h2=ExaltedWaveFlight.Calculate(catalog,b,hs,heavy,2);Near(h1.ExpectedHit,h2.ExpectedHit);
        });
        check("데저트 윈드 파동:CO 제외·무한 관통·20m 경계·명중 콤보",()=>{
            var b=B(ExaltedFamily.Wind);var f=b.Encounter!.ExaltedWaves![0];var source=ExaltedWaveFlight.Resolve(catalog,stances,b,f);var baseline=ExaltedWaveFlight.Calculate(catalog,b,source,f);var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");b.Slots[0]=new(co.Id,co.MaxRank);b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            Near(ExaltedWaveFlight.Calculate(catalog,b,source,f,3).Damage.Values.Sum(),baseline.Damage.Values.Sum());Yes(ExaltedWaveFlight.Calculate(catalog,b,source,f with {Distance=20}).ExpectedHit>0);Near(ExaltedWaveFlight.Calculate(catalog,b,source,f with {Distance=20.01}).ExpectedHit,0);
            b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("다음",4,Defense:new(1e9,0))],ExaltedWaves=[new(0,.1,6),new(0,.2,10,Target:2)]}};var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="exaltedWaveLaunch")==1&&r.Events.Count(e=>e.Kind=="exaltedWaveImpact")==2);Near(r.Points.Last().ComboPoints,2);
        });
        check("데저트 윈드 파동:시야 차단 대상은 피해·상태·콤보 없이 통과",()=>{
            var b=B(ExaltedFamily.Wind);b=b with {Encounter=b.Encounter! with {BlockedSight=[new(-1,0)],NearbyEnemies=[new("보이는 대상",4,Defense:new(1e9,0))],ExaltedWaves=[new(0,.1,6),new(0,.2,10,Target:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage+r.StatusDamage,0);Yes(r.Enemies![1].DirectDamage>0&&r.Events.Any(e=>e.Kind=="exaltedWaveSight"));Near(r.Points.Last().ComboPoints,1);
        });
        check("익절티드 파동:직격 사망 후 관통·파동 명중으로 텐노카이4회 조건",()=>{
            var b=B(ExaltedFamily.Wind);var merit=catalog.Mods.Single(m=>m.EnglishName=="Discipline's Merit");b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Exilus=new(merit.Id,merit.MaxRank)},Encounter=b.Encounter! with {NearbyEnemies=[new("2",3,Defense:new(1e9,0)),new("3",6,Defense:new(1e9,0)),new("4",9,Defense:new(1e9,0))],ExaltedWaves=Enumerable.Range(1,4).Select(i=>new ExaltedWaveContact(0,i*.1,i*3,Target:i)).ToArray()}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="exaltedWaveImpact")==4&&r.Events.Any(e=>e.Kind=="tennokaiOpportunity"));Near(r.Points.Last().ComboPoints,4);
        });
        check("블레이드 교차 파동:실제2파동·공격 입력 한 번 소모·피해 콤보 제외",()=>{
            var b=B(ExaltedFamily.Blade);b=b with {Scenario=b.Scenario with {Combo=12},Encounter=b.Encounter! with {ExaltedWaves=[new(0,.1,3,Combo:"Heavy",FalloffModel:1),new(0,.1,3,Projectile:2,Combo:"Heavy",Wave:2,FalloffModel:1)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="exaltedWaveImpact")==2&&r.Events.Count(e=>e.Kind=="heavyCombo")==1);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,220);Near(r.Points.Last().ComboPoints,0);
            var f=b.Encounter.ExaltedWaves[0];var source=ExaltedWaveFlight.Resolve(catalog,stances,b,f);Near(ExaltedWaveFlight.Calculate(catalog,b,source,f).ExpectedHit,ExaltedWaveFlight.Calculate(catalog,b with {Scenario=b.Scenario with {Combo=1}},source,f).ExpectedHit);
        });
    }
}
