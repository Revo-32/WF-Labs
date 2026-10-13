using System.Text.Json;
using TennoForge.Core;

internal static class AckSharedAttackChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B()=>new(IncarnonBladeFlight.Ack,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(IncarnonBladeFlight.Ack,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,IncarnonBlades:[new(.1,.2,BladeImpact:0)]));
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("액크 & 브런트 공동 입력 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 공동 입력 허용: "+text);}
        check("액크 공동 입력:칼날은 PP·인카논 피해 적용·투사체는 제외",()=>{
            var b=B();var f=b.Encounter!.IncarnonBlades![0];var pp=catalog.Mods.Single(m=>m.EnglishName=="Pressure Point");
            var blade=IncarnonBladeFlight.PhysicalContact(catalog,stances,b,f);var dart=IncarnonBladeFlight.Calculate(catalog,b,f);
            b.Slots[0]=new(pp.Id,pp.MaxRank);var modded=IncarnonBladeFlight.PhysicalContact(catalog,stances,b,f);
            Near(modded.Calculation!.Damage.Values.Sum()/blade.Calculation!.Damage.Values.Sum(),3.2/2);
            Near(IncarnonBladeFlight.Calculate(catalog,b,f).Damage.Values.Sum(),dart.Damage.Values.Sum());
            Near(blade.Input!.Scenario.MotionMultiplier,2);Near(IncarnonBladeFlight.PhysicalContact(catalog,stances,b,f with {Motion="Slide"}).Input!.Scenario.MotionMultiplier,1);
        });
        check("액크 공동 입력:칼날→발사→직격/폭발·충전 한 번·칼날만 콤보",()=>{
            var plain=B();var b=plain with {Melee=plain.Melee! with {Conditions=new(){["BlockHeat"]=4}}};var r=Run(b);var baseline=Run(plain);
            Near(r.Events.Single(e=>e.Kind=="incarnonPhysicalHit").Amount,baseline.Events.Single(e=>e.Kind=="incarnonPhysicalHit").Amount*1.7);
            Yes(r.Events.Count(e=>e.Kind=="absorbedElementSpent")==1);Near(r.Events.Single(e=>e.Kind=="absorbedElementSpent").Amount,4);
            Yes(r.Events.Count(e=>e.Kind=="incarnonBladeLaunch")==1&&r.Events.Count(e=>e.Kind=="incarnonBladeImpact")==1&&r.Events.Count(e=>e.Kind=="incarnonBladeExplosion")==1);Near(r.Points.Single(p=>p.Time==.5).ComboPoints,2);Near(r.Points.Last().ComboPoints,0); // 200% stance hit; 5-second expiry.
        });
        check("액크 공동 입력:비행 중 새 원소는 다음 입력에 남음·대상별 칼날/투사체",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Conditions=new(){["BlockHeat"]=4}},Encounter=b.Encounter! with {NearbyEnemies=[new("투사체 대상",5,Defense:new(1e9,0))],IncarnonBlades=[new(.1,.4,Target:2,BladeImpact:0),new(1.1,1.4,Projectile:2,Target:2,BladeImpact:1)],Inputs=[new(.2,"BlockedElement",2,DamageType.Cold)]}};
            var r=Run(b);var spent=r.Events.Where(e=>e.Kind=="absorbedElementSpent").ToArray();Yes(spent.Length==2);Near(spent[0].Time,0);Near(spent[0].Amount,4);Near(spent[1].Time,1);Near(spent[1].Amount,2);Near(r.Points.Last().ComboPoints,6);Yes(r.Enemies!.All(e=>e.DirectDamage>0)); // Two 200% contacts + two blocked attacks.
        });
        check("액크 공동 입력:투사체 먼저·칼날 나중도 지원·명중 순서/중복 반복 검사·저장",()=>{
            var b=B();var f=new IncarnonBladeContact(0,.1,BladeImpact:.2,LaunchHit:3);b=b with {Encounter=b.Encounter! with {IncarnonBlades=[f]}};var r=Run(b);Near(r.Events.Single(e=>e.Kind=="incarnonPhysicalHit").Time,.2);Near(r.Points.Last().ComboPoints,2);
            var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.IncarnonBlades!.Single()==f);
            Reject(()=>Run(b with {Encounter=b.Encounter with {ScriptedAttacksOnly=false}}),"자동 반복");
            Reject(()=>IncarnonBladeFlight.Validate([f with {BladeImpact=.1}],b.WeaponId,1),"서로 다른 시각");
            Reject(()=>IncarnonBladeFlight.PhysicalContact(catalog,stances,b,f with {LaunchHit=4}),"타수");
        });
    }
}
