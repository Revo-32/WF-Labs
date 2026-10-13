using System.Text.Json;
using TennoForge.Core;

internal static class InnodemFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=InnodemFlight.WeaponId;
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,InnodemContacts:[new(0,.1,6,LocationMultiplier:1,CoMultiplier:2)]));
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("인노뎀 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 인노뎀 입력 허용: "+text);}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        check("인노뎀:위치/컨오 분기 미기재를 명시하고60m/s·21m·관통 입력 검증",()=>{
            Reject(()=>InnodemFlight.Validate([new(0,.1,6)],1),"선택 규칙");Reject(()=>InnodemFlight.Validate([new(0,.1,7,LocationMultiplier:1,CoMultiplier:2)],1),"60m/s");
            Reject(()=>InnodemFlight.Validate([new(0,22d/60,22,LocationMultiplier:1,CoMultiplier:2)],1),"비행 거리");Reject(()=>InnodemFlight.Validate([new(0,.1,6,LocationMultiplier:1,CoMultiplier:2),new(0,.2,12,LocationMultiplier:1,CoMultiplier:2)],1),"두 번");
            var b=B();var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.InnodemContacts!.SequenceEqual(b.Encounter!.InnodemContacts!));
        });
        check("인노뎀 공식:스탠스·위치·거리 감쇠는PP 분기만·CO는 별도 가산",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload","Fever Strike");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            var c=new InnodemContact(0,20d/60,20,MotionMultiplier:3,LocationMultiplier:1.5,CoMultiplier:2);var r=InnodemFlight.Calculate(catalog,b,c);
            Near(r.Damage.Values.Sum(),360*(2.2*3*1.5*.2778+2*.8*2)*1.9);Near(r.Melee!.Brackets!.BeforeFaction,360*(2.2*3*1.5*.2778+2*.8*2));Near(DpsEquation.From(r).Evaluate(),r.TargetDps);
            var single=InnodemFlight.Calculate(catalog,b,c with {CoMultiplier=1});Near(r.Damage.Values.Sum()-single.Damage.Values.Sum(),360*.8*2*1.9);
            Yes(r.Limitations.Any(n=>n.Contains("위치")&&n.Contains("컨오")));
        });
        check("인노뎀 공식:고유 방사능·강제 충격·21m 끝과 사거리 밖 구분",()=>{
            var b=B();var c=b.Encounter!.InnodemContacts![0] with {Distance=21};var r=InnodemFlight.Calculate(catalog,b,c);Near(r.Damage[DamageType.Radiation],360*.2778);Yes(r.Melee!.ForcedProcs.Contains("Impact"));Near(InnodemFlight.Calculate(catalog,b,c with {Distance=21.01}).Damage.Values.Sum(),0);
        });
        check("인노뎀 전투:한 발사체 무한 관통·각 명중 시각·칼날 관통감쇠와 콤보 제외",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("다음",6,Defense:new(1e9,0))],InnodemContacts=[new(0,.1,6,LocationMultiplier:1,CoMultiplier:2),new(0,.2,12,Target:2,LocationMultiplier:1,CoMultiplier:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="innodemLaunch")==1&&r.Events.Count(e=>e.Kind=="innodemImpact")==2);Near(r.Events.Last(e=>e.Kind=="innodemImpact").Time,.2);Near(r.Points.Last().ComboPoints,0);Yes(r.Enemies![1].DirectDamage>=360&&r.Notes.Any(n=>n.StartsWith(InnodemFlight.Conditional)));Near(DpsEquation.FromTimeline(r).Evaluate(),(r.DirectDamage+r.StatusDamage)/r.Seconds);
        });
        check("인노뎀 전투:이미 발사한 파동은 변신 만료 후 도달·직격 사망 후 관통",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {IncarnonSecondsRemaining=.05},Encounter=b.Encounter! with {NearbyEnemies=[new("관통",6,Defense:new(1e9,0))],InnodemContacts=[new(0,.1,6,LocationMultiplier:1,CoMultiplier:2),new(0,.2,12,Target:2,LocationMultiplier:1,CoMultiplier:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage,1);Yes(r.Enemies![1].DirectDamage>=360);
        });
        check("인노뎀 전투:아라크네 범용 피해를 이 파동의 근접 피해로 합치지 않음",()=>{
            var b=B();b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5)),Encounter=b.Encounter! with {Inputs=[new(.05,"WallLatch")]}};
            // Arachne is universal and does not enter this melee-damage policy.
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Any(e=>e.Kind=="innodemImpact"));
        });
    }
}
