using System.Text.Json;
using TennoForge.Core;

internal static class DestrezaFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(params DestrezaContact[] contacts)=>new(MeleeMechanicScope.DestrezaPrimeId,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(MeleeMechanicScope.DestrezaPrimeId,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,DestrezaContacts:contacts));
        void Mod(Build b,string name){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[Array.FindIndex(b.Slots,s=>s is null)]=new(m.Id,m.MaxRank);}
        void Yes(bool value){if(!value)throw new Exception("유령 레이피어 시간 계산 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Reject(Action f){try{f();}catch(ArgumentException){return;}throw new Exception("잘못된 레이피어 경로를 허용");}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);return r;}
        bool Tier(double damage,double basis)=>Math.Abs(damage-basis)<1e-6||Math.Abs(damage-3*basis)<1e-6;
        check("레이피어:5개 실제명중·강제2종·두 번째부터 CO2배계수",()=>{
            var b=B(Enumerable.Range(1,5).Select(i=>new DestrezaContact(0,i*.1,Rapier:i)).ToArray());Mod(b,"Condition Overload");
            var r=Run(b);var hits=r.Events.Where(e=>e.Kind=="rapierImpact").ToArray();Yes(hits.Length==5&&r.Events.Count(e=>e.Kind=="rapierLaunch")==1);
            Yes(Tier(hits[0].Amount,765*2));foreach(var h in hits.Skip(1))Yes(Tier(h.Amount,765*(2+2*.8*2)));
            Near(r.Points.Last().StatusTypes,2);Yes(r.StatusDamage>0&&r.Notes.Contains(DestrezaFlight.Partial));Yes(DpsEquation.FromTimeline(r).Context!.Contains("일부"));
        });
        check("레이피어:명중 순간 준비 동작·기존 발사체에 현재 콤보 적용",()=>{
            var b=B(new(0,.2,Rapier:1),new(0,.3,Rapier:2),new(0,.4,Rapier:3));b=b with {Encounter=b.Encounter! with {Inputs=[new(.25,"HeavyAnimationStart"),new(.35,"HeavyAnimationEnd")]}};
            var hits=Run(b).Events.Where(e=>e.Kind=="rapierImpact").ToArray();Yes(Tier(hits[0].Amount,765*2));Yes(Tier(hits[1].Amount,765*2*12));Yes(Tier(hits[2].Amount,765*2));
        });
        check("레이피어:무한 신체 관통·각 대상 방어층/상태·2초 수명",()=>{
            var b=B(new(0,.1,Target:1),new(0,.2,Target:2));b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("second",2,Defense:new(1e9,2700))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="rapierImpact")==2);Yes(r.Enemies is {Length:2});
            Reject(()=>DestrezaFlight.Validate([new(0,2.01)],1));Reject(()=>DestrezaFlight.Validate([new(0,.1,Rapier:6)],1));Reject(()=>DestrezaFlight.Validate([new(0,.1),new(0,.2)],1));Reject(()=>DestrezaFlight.Validate([new(0,.1),new(.1,.2,Rapier:2)],1));
        });
        check("레이피어:직접 처치의 진화 관통/준비 버프·강제베기의 리렌틀리스 콤보",()=>{
            var b=B(new(0,.1,Target:1),new(0,.2,Target:2));b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("survivor",1,Defense:new(1e9,0))]}};
            var r=Run(b);Near(r.Points.Last().IncarnonPunctureBonus,.1);Yes(r.Events.Count(e=>e.Kind=="rapierImpact")==2);
            b=B(new DestrezaContact(0,.1));Mod(b,"Relentless Combination");b=b with {Scenario=b.Scenario with {Combo=1}};
            r=Run(b);Yes(r.Points.Last().ComboPoints>0);
        });
        check("레이피어:상확 가정은 표시·저장하고 무작위 상태에 연결",()=>{
            var b=B(Enumerable.Range(1,5).Select(i=>new DestrezaContact(0,i*.1,Rapier:i,AssumedStatusChance:1)).ToArray());
            var copy=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(copy.Encounter!.DestrezaContacts!.SequenceEqual(b.Encounter!.DestrezaContacts!));
            var r=Run(copy);Yes(r.Notes.Contains(DestrezaFlight.Assumed)&&!r.Notes.Contains(DestrezaFlight.Partial));Yes(r.Points.Last().StatusTypes>=3);
            var c=DestrezaFlight.Calculate(catalog,b,new(0,.1,AssumedStatusChance:1));Near(c.StatusChance,1);
        });
        check("레이피어:미기재 상확 의존 치명효과는 0으로 확정하지 않음",()=>{
            var b=B(new DestrezaContact(0,.1));var d=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Doughty");b=b with {Arcanes=new(Melee:new(d.Id,5))};
            Reject(()=>DestrezaFlight.Calculate(catalog,b,new(0,.1)));Yes(DestrezaFlight.Calculate(catalog,b,new(0,.1,AssumedStatusChance:1)).CriticalMultiplier>3);
        });
        check("레이피어:로어·이클립스·인플루언스 제외·검과 자동중복 금지",()=>{
            var b=B(new DestrezaContact(0,.1));var c=DestrezaFlight.Calculate(catalog,b,new(0,.1));var boosted=DestrezaFlight.Calculate(catalog,b with {Melee=b.Melee! with {Buffs=new(.5,2)}},new(0,.1));Near(boosted.ExpectedHit,c.ExpectedHit*4.5);
            var influence=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Influence");Mod(b,"Shocking Touch");b=b with {Arcanes=new(Melee:new(influence.Id,5)),Encounter=b.Encounter! with {DestrezaContacts=[new(0,.1,AssumedStatusChance:10)],NearbyEnemies=[new("other",1,Defense:new(1e9,0))]}};
            Yes(!Run(b).Events.Any(e=>e.Kind=="influence"));Reject(()=>Run(b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=false}}));
        });
    }
}
