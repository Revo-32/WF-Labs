using TennoForge.Core;

internal static class TatsuChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool v){if(!v)throw new Exception("소울 스웜 사건 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B()=>new(TatsuSoulState.PrimeId,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(TatsuSoulState.PrimeId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        CombatTimelineResult Run(Build b,int n=1){var r=CombatTimeline.Calculate(catalog,b,stances,n);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("타츠 소울:5/9 상한·처치마다120초 전체 갱신·정확한 만료",()=>{
            var s=new TatsuSoulState();for(var i=0;i<8;i++)s.Kill(i);Yes(s.Charges==5);Near(s.End,127);
            s.Advance(126.99);Yes(s.Charges==5);s.Advance(127);Yes(s.Charges==0);
            var revenant=new TatsuSoulState(true);for(var i=0;i<20;i++)revenant.Kill(i);Yes(revenant.Charges==9);
            Yes(new TatsuSoulState(charges:5,remaining:0).Charges==0);
        });
        check("타츠 소울:전량 동시 발사·저장 만료와 비행 탄 분리·30m/20m초 경계",()=>{
            var s=new TatsuSoulState(charges:5);Yes(s.Release(10)==5&&s.Charges==0&&s.InFlight==5);
            s.Kill(11);s.Advance(131);Yes(s.Charges==0&&s.InFlight==5);s.Impact(131,30);Yes(s.InFlight==4);
            var fast=new TatsuSoulState(charges:1);fast.Release(0);
            try{fast.Impact(1.499,30);throw new Exception("비행 시간 검증 누락");}catch(ArgumentException){}
            fast.Impact(1.5,30);Yes(fast.InFlight==0);
            try{fast.Impact(2,0);throw new Exception("없는 투사체 허용");}catch(ArgumentException){}
        });
        check("타츠:실제 무기 처치만 충전·도움/파라존 제외·발사3발 대상2에 분배",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=new(Conditions:new(){["SoulCharges"]=2}),Encounter=new(EncounterMode.FiveSeconds,
                Inputs:[new(.1,"MeleeAssist"),new(.2,"ParazonKill"),new(1,"SoulSwarmSlideInput"),new(1.1,"SoulSwarmImpact",2),new(1.2,"SoulSwarmImpact",2),new(1.3,"SoulSwarmImpact",2)],
                NearbyEnemies:[new("other",1,Defense:new(1e9,0))],TargetOrder:[0,1])};
            var r=Run(b);Near(r.Events.Single(e=>e.Kind=="soulCharge").Amount,3);Near(r.Events.Single(e=>e.Kind=="soulRelease").Amount,3);
            Yes(r.Events.Count(e=>e.Kind=="soulImpact")==3&&r.Enemies![1].DirectDamage>0);Near(r.Points.Last().SoulCharges!.Value,0);Near(r.Points.Last().SoulsInFlight!.Value,0);
        });
        check("타츠 소울:첫 검 타격 이후 남은 저장 시간 소멸·다른 적 처치로 갱신",()=>{
            var b=B() with {Melee=new(Conditions:new(){["SoulCharges"]=4,["SoulRemaining"]=2})};
            var expired=Run(b);Near(expired.Points.Last().SoulCharges!.Value,0);
            var refreshed=Run(b with {Encounter=b.Encounter! with {Inputs=[new(1,"MeleeKill")]}});Near(refreshed.Points.Last().SoulCharges!.Value,5);
        });
        check("타츠 소울:범용 피해 브라켓·프레셔 제외·원소 모드 포함",()=>{
            var b=B() with {Melee=new(Attack:"Soul Swarm Projectile")};var baseline=MeleeEngine.Calculate(catalog,b);
            var pressure=catalog.Mods.Single(m=>m.EnglishName=="Pressure Point");b.Slots[0]=new(pressure.Id,pressure.MaxRank);
            Near(MeleeEngine.Calculate(catalog,b).ExpectedHit,baseline.ExpectedHit);
            var shock=catalog.Mods.Single(m=>m.EnglishName=="Shocking Touch");b.Slots[1]=new(shock.Id,shock.MaxRank);
            Near(MeleeEngine.Calculate(catalog,b).ExpectedHit,baseline.ExpectedHit*1.9);
        });
        check("타츠 소울:명중 대상/발사 순서 검증·다른 무기 행동 거부",()=>{
            void Reject(Build b){try{Run(b);}catch(ArgumentException){return;}throw new Exception("잘못된 소울 시나리오 허용");}
            var b=B();Reject(b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(1,"SoulSwarmImpact")])});
            Reject(b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(1,"SoulSwarmImpact",2)])});
            Reject(b with {Melee=new(Conditions:new(){["SoulCharges"]=6})});
            Reject(b with {Melee=new(Conditions:new(){["SoulCharges"]=1}),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"SoulSwarmSlideInput"),new(1,"SoulSwarmImpact",2)],NearbyEnemies:[new("out",30.001)])});
        });
    }
}
