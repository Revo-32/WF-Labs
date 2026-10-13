using TennoForge.Core;

internal static class WolfSledgeFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B()=>new(WolfSledgeFlight.WeaponId,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(WolfSledgeFlight.WeaponId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,GlaiveFlights:[new(0,1,Contacts:[new(.1),new(.4,Kind:GlaiveContactKind.Return)],Detonation:new(.3))]));
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("울프 슬레지 비행 규칙 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 투척 입력 허용: "+text);}
        check("울프 슬레지:직격777·전진 폭발777/40%감쇠·귀환 강타777의 다른 브라켓",()=>{
            var b=B();foreach(var (name,index) in new[]{("Pressure Point",0),("Killing Blow",1),("True Steel",2),("Shocking Touch",3)}){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[index]=new(m.Id,m.MaxRank);}
            b=b with {Scenario=b.Scenario with {Combo=12}};
            var thrown=GlaiveFlightState.Calculate(catalog,b,"Throw");var bounce=WolfSledgeFlight.Bounce(catalog,b);var returned=WolfSledgeFlight.HeavyReturn(catalog,b);
            Near(thrown.Damage.Values.Sum(),777*2.2*1.9);Near(bounce.Damage.Values.Sum(),777*2.2*1.9);Near(WolfSledgeFlight.Bounce(catalog,b,5).Damage.Values.Sum(),bounce.Damage.Values.Sum()*.6);
            Near(returned.Damage.Values.Sum(),777*3.4*12*1.9);Near(returned.CriticalChance,.19*3.4);Near(bounce.CriticalChance,.19*3.4);Near(thrown.CriticalChance,.19*2.2);
            Yes(returned.Melee!.Policy!.Heavy&&returned.Melee.ForcedProcs.Contains("Impact")&&!returned.Melee.ForcedProcs.Contains("Ragdoll"));
        });
        check("울프 슬레지:전진2접촉의 각 폭발·귀환은 폭발 없이 콤보 획득",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("두 번째",2,Defense:new(1e9,0))],GlaiveFlights=[new(0,1,Contacts:[new(.1),new(.2,2),new(.4,Kind:GlaiveContactKind.Return)])]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="glaiveContact")==3&&r.Events.Count(e=>e.Kind=="wolfBounce")==4);Yes(r.Events.Where(e=>e.Kind=="wolfBounce").All(e=>e.Time is .1 or .2));Near(r.Points.Single(p=>p.Time==1).ComboPoints,3);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
        });
        check("울프 슬레지:스탠스 장착 시 공속·미장착 시 준비 속도에만 충전 시간 단축",()=>{
            var b=B();var fury=catalog.Mods.Single(m=>m.EnglishName=="Fury");var killing=catalog.Mods.Single(m=>m.EnglishName=="Killing Blow");b.Slots[0]=new(fury.Id,fury.MaxRank);b.Slots[1]=new(killing.Id,killing.MaxRank);
            Near(GlaiveFlightState.Calculate(catalog,b,"Throw").Melee!.Windup,.75/1.6);
            b=b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Crushing Ruin")};var c=GlaiveFlightState.Calculate(catalog,b,"Throw");Near(c.Melee!.Windup,.75/c.AttackSpeed);
        });
        check("울프 슬레지:지형 충돌 폭발·즉시 귀환·원래 대상 직격을 생성하지 않음",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {GlaiveFlights=[new(0,1,Contacts:[new(.1,Surface:new(0,0,0)),new(.2,Kind:GlaiveContactKind.Return)])]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="wolfBounce")==1&&r.Events.Count(e=>e.Kind=="glaiveContact")==1);Near(r.Points.Single(p=>p.Time==1).ComboPoints,1);
            Reject(()=>Run(b with {Encounter=b.Encounter with {GlaiveFlights=[new(0,1,Contacts:[new(.1,Surface:new()),new(.2)])]}}),"지형 접촉");
        });
        check("울프 슬레지:수동 기폭 한 번 소모·귀환 강타와 라이프 스트라이크·동반 폭발 제외",()=>{
            var b=B();var life=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");b.Slots[0]=new(life.Id,life.MaxRank);
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1&&r.Events.Count(e=>e.Kind=="glaiveDetonation")==1&&r.Events.Count(e=>e.Kind=="wolfBounce")==1);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,1);Near(r.Points.Single(p=>p.Time==1).ComboPoints,0);Yes(r.Recovery?.HeavyHealing?.Mean>0);
        });
        check("울프 슬레지:마구스 충전은 투척과 수동 기폭 각1회·다중 접촉 중복 소비 제외",()=>{
            var b=B();b=b with {Arcanes=new(Operator:new(MagusAggressState.Id,5)),Encounter=b.Encounter! with {Inputs=[new(0,"WarframeMeleeTransfer")]}};
            var r=Run(b);Yes(r.Events.Where(e=>e.Kind=="magusCharge").Select(e=>e.Amount).SequenceEqual(new[]{3d,2}));Near(r.Points.Last().MagusAggressCharges!.Value,2);
        });
        check("울프 슬레지:지상 전용·40m와후속20m·기폭 후 다른 콤보의 미기재 저장 구분",()=>{
            var b=B();Reject(()=>Run(b with {Encounter=b.Encounter! with {GlaiveFlights=[new(0,1,Airborne:true,Contacts:[new(.1)])]}}),"지상");
            Reject(()=>Run(b with {Encounter=b.Encounter! with {GlaiveFlights=[new(0,1,FirstLegDistance:40.01,Contacts:[new(.1)])]}}),"40m");
            Reject(()=>Run(b with {Encounter=b.Encounter! with {NearbyEnemies=[new("먼 대상",20.01)],GlaiveFlights=[new(0,1,Contacts:[new(.1),new(.2,2)])]}}),"20m");
            Reject(()=>Run(b with {Scenario=b.Scenario with {Combo=12}}),"저장 콤보");
            Yes(GlaiveFlightState.Attack(new(0,1,Charged:true),weaponId:WolfSledgeFlight.WeaponId)=="Throw");
        });
    }
}
