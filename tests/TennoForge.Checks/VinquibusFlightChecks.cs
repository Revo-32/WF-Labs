using TennoForge.Core;

internal static class VinquibusFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("빈퀴부스 비행 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("무효 경로를 허용함");}
        Build B(params WeaponActionContact[] contacts)=>new(VinquibusState.WeaponId,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(VinquibusState.WeaponId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[new(0,.1,Contacts:contacts)]));
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("빈퀴부스 비행:폭발은접촉아닌발사+0.8초·자동폭발중복없음",()=>{
            var r=Run(B(new WeaponActionContact(.4,Travel:20)));Yes(r.Events.Count(e=>e.Kind=="vinquibusImpact")==1);Yes(r.Events.Count(e=>e.Kind=="vinquibusBlast")==1);Near(r.Events.Single(e=>e.Kind=="vinquibusBlast").Time,.9);Yes(!r.Events.Any(e=>e.Kind=="gunshotFuse"));
        });
        check("빈퀴부스 비행:처치한첫적관통후두번째적중심폭발·충전1회",()=>{
            var b=B(new WeaponActionContact(.3),new(.5,Target:2,Travel:10));b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Conditions=new(){["VinquibusPrecision"]=1}},Encounter=b.Encounter! with {NearbyEnemies=[new("second",10,Defense:new(1e9,0)),new("edge",14,Defense:new(1e9,0)),new("outside",14.01,Defense:new(1e9,0))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="vinquibusImpact")==2);Yes(r.Events.Count(e=>e.Kind=="vinquibusPrecision")==1);Near(r.Points.Last().VinquibusPrecision!.Value,0);Yes(r.Events.Any(e=>e.Kind=="vinquibusPierce"));
            var edge=r.Enemies!.Single(e=>e.Id=="edge").DirectDamage;Yes(Math.Abs(edge-980)<1e-6||Math.Abs(edge-3136)<1e-6);Near(r.Enemies!.Single(e=>e.Id=="outside").DirectDamage,0);
        });
        check("빈퀴부스 비행:생존적관통거부·표면충돌후추가접촉거부",()=>{
            var b=B(new WeaponActionContact(.3),new(.5,Target:2,Travel:10));b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("second",10,Defense:new(1e9,0))]}};Reject(()=>Run(b));
            Reject(()=>WeaponActionFlight.Schedule(b.WeaponId,[new(0,.1,Contacts:[new(.2,Surface:true),new(.3,Target:2)])],2));
        });
        check("빈퀴부스 비행:직격없는표면폭발도유효·추가직격생성없음",()=>{
            var b=B(new WeaponActionContact(.4,Surface:true));var r=Run(b);Yes(!r.Events.Any(e=>e.Kind=="vinquibusImpact"));Yes(r.Events.Count(e=>e.Kind=="vinquibusBlast")==1);Yes(r.Events.Count(e=>e.Kind=="vinquibusPrecision")==1);
        });
        check("빈퀴부스 비행:막기정지3배·방어층과컨오별도계산",()=>{
            var b=B(new WeaponActionContact(.4));var v=b.Encounter!.WeaponActions![0];var baseHit=WeaponActionFlight.Calculate(catalog,b,v);var stance=WeaponActionFlight.Calculate(catalog,b,v with {Sequence=2});Near(stance.ExpectedHit,baseHit.ExpectedHit*3);
            var blast=WeaponActionFlight.Calculate(catalog,b,v,explosion:true,distance:4);Near(blast.Damage.Values.Sum(),280);Near(blast.TargetDps/blast.RawDps,1);
            b=b with {Melee=b.Melee! with {Shields=100,Overguard=100}};var r=Run(b);Near(r.Points.Last().Shields,0);Near(r.Points.Last().Overguard,0);Yes(r.StatusDamage>0);
        });
        check("빈퀴부스 비행:타이머뒤접촉·3대상·미완성처치관통차단",()=>{
            Reject(()=>WeaponActionFlight.Schedule(VinquibusState.WeaponId,[new(0,.1,Contacts:[new(1)])],1));
            Reject(()=>WeaponActionFlight.Schedule(VinquibusState.WeaponId,[new(0,.1,Contacts:[new(.2),new(.3,Target:2),new(.4,Target:3)])],3));
            var b=B(new WeaponActionContact(.3));b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("alive",1,Defense:new(1e9,0))]}};var r=EncounterEngine.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Any(s=>s.Contains("후속")));
        });
    }
}
