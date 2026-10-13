using TennoForge.Core;

internal static class ExaltedLifetimeChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool value){if(!value)throw new Exception("익절티드 수명 규칙 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        check("인레이지드:최대 지속 종료 후 유지시간만큼 대기·정확한 재사용 경계",()=>{
            var state=new ExaltedLifetime(15);Yes(state.Active);Yes(state.Advance(15));Near(state.ReadyAt,30);Yes(!state.Activate(29.999));Yes(state.Activate(30));Near(state.End,45);
        });
        check("인레이지드:5초만 사용하면 해제 후5초 대기·시작 전 경과도 포함",()=>{
            var state=new ExaltedLifetime(15);Yes(state.Deactivate(5));Near(state.ReadyAt,10);Yes(!state.Activate(9));Yes(state.Activate(10));var existing=new ExaltedLifetime(15,4);existing.Deactivate(1);Near(existing.ReadyAt,6);
        });
        check("인레이지드:전이 중 종료는 복귀 시 대기 시작·이미 시작한 대기는 계속 흐름",()=>{
            var state=new ExaltedLifetime(15);state.Transfer(14,true);Yes(!state.CanAttack);state.Advance(15);Yes(state.WaitingForReturn);state.Transfer(20,false);Near(state.ReadyAt,35);Yes(!state.Activate(34.999));Yes(state.Activate(35));
            var running=new ExaltedLifetime(15);running.Deactivate(5);running.Transfer(6,true);running.Transfer(9,false);Near(running.ReadyAt,10);Yes(running.Activate(10));
        });
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(ExaltedFamily family){var id=ExaltedRules.All.First(d=>d.Family==family).Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));}
        check("인레이지드 실제 교전:조기 해제·대기 중 실패·재시전 후에만 명중 재개",()=>{
            var b=B(ExaltedFamily.Talons);b=b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Enraged,3)},Encounter=b.Encounter! with {Inputs=[new(1,"ExaltedDeactivate"),new(1.5,"ExaltedActivate"),new(2,"ExaltedActivate")]}};var result=CombatTimeline.Calculate(c,b,stances,8);Yes(result.Unsupported.Length==0);Yes(result.Events.Any(e=>e.Kind=="exaltedActivation"&&e.Time==1.5&&e.Amount==0));Yes(result.Events.Any(e=>e.Kind=="exaltedActivation"&&e.Time==2&&e.Amount==1));Yes(!result.Events.Any(e=>e.Kind=="hit"&&e.Time>=1&&e.Time<2));Yes(result.Events.Any(e=>e.Kind=="hit"&&e.Time>=2));
        });
        check("프라이멀 레이지:해제 시 초기화·꺼진 동안 다른 무기 처치로 재충전하지 않음",()=>{
            var b=B(ExaltedFamily.Staff);b=b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Primal,3),Conditions=new(){["PrimalRage"]=150}},Encounter=b.Encounter! with {Inputs=[new(0,"ExaltedDeactivate"),new(.2,"PrimaryKill"),new(.4,"ExaltedActivate")]}};
            var result=CombatTimeline.Calculate(c,b,stances,8);var without=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Conditions=[]},Encounter=b.Encounter! with {Inputs=[new(0,"ExaltedDeactivate"),new(.4,"ExaltedActivate")]}},stances,8);Yes(result.Unsupported.Length==0);Near(result.DirectDamage,without.DirectDamage);
        });
        check("어큐뮬레이팅 윕클로:무효화·낙사로 초기화·다음 타격 위력에서 제거",()=>{
            foreach(var action in new[]{"AbilityNullified","FallOutOfBounds"})
            {
                var b=B(ExaltedFamily.Whipclaw);b=b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Accumulating,3),Conditions=new(){["AccumulatingWhipclaw"]=350}},Encounter=new(EncounterMode.FirstHit,Inputs:[new(0,action)])};var result=CombatTimeline.Calculate(c,b,stances,8);var plain=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Conditions=[]},Encounter=new(EncounterMode.FirstHit)},stances,8);Yes(result.Unsupported.Length==0);Near(result.DirectDamage,plain.DirectDamage);
            }
        });
    }
}
