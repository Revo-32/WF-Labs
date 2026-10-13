using System.Text.Json;
using TennoForge.Core;

internal static class IncarnonBladeChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string id)=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,IncarnonBlades:[new(0,.1,Motion:id==IncarnonBladeFlight.Hate?"Neutral":"Aerial")]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("유령 칼날 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 유령 칼날 입력 허용: "+text);}
        check("유령 칼날:발사 동작·중복 번호·60m/s 검사·설정 저장",()=>{
            Reject(()=>IncarnonBladeFlight.Validate([new(0,.1,Travel:60)],IncarnonBladeFlight.Ack,1),"60m/s");
            Reject(()=>IncarnonBladeFlight.Validate([new(0,.1)],IncarnonBladeFlight.Hate,1),"정지/전진");
            Reject(()=>IncarnonBladeFlight.Validate([new(0,.1),new(0,.2)],IncarnonBladeFlight.Ack,1),"중복");
            var b=B(IncarnonBladeFlight.Hate);var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.IncarnonBlades!.SequenceEqual(b.Encounter!.IncarnonBlades!));
        });
        check("액크 도끼:컨오 직격 곱연산·공중2배·폭발CO/콤보/PP/KB제외",()=>{
            var b=B(IncarnonBladeFlight.Ack);Mods(b,"Pressure Point","Killing Blow","Condition Overload","Shocking Touch");
            b=b with {Scenario=b.Scenario with {Combo=12},Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5)),Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            var f=b.Encounter!.IncarnonBlades![0];
            Near(IncarnonBladeFlight.Calculate(catalog,b,f).Damage.Values.Sum(),149*2*2.5*2.6*1.9);
            var explosion=IncarnonBladeFlight.Calculate(catalog,b,f,true);Near(explosion.Damage.Values.Sum(),303*2*2.5*1.9);Yes(explosion.Damage.ContainsKey(DamageType.Radiation)&&!explosion.Damage.ContainsKey(DamageType.Electricity));
            Near(IncarnonBladeFlight.Calculate(catalog,b,f with {Motion="Slide"},true).Damage.Values.Sum(),303*2.5*1.9);
            Near(IncarnonBladeFlight.Calculate(catalog,b,f,true,5).Damage.Values.Sum(),303*2*2.5*1.9*.7);Near(IncarnonBladeFlight.Calculate(catalog,b,f,true,5.01).Damage.Values.Sum(),0);
        });
        check("헤이트 칼날:발사 스탠스 배율·범용 피해·직격만CO·폭발3m",()=>{
            var b=B(IncarnonBladeFlight.Hate);Mods(b,"Pressure Point","Killing Blow","Condition Overload");
            b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5)),Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};var f=b.Encounter!.IncarnonBlades![0];
            Near(IncarnonBladeFlight.Calculate(catalog,b,f,motionMultiplier:3).Damage.Values.Sum(),130*3*(1+1.5+1.6));
            Near(IncarnonBladeFlight.Calculate(catalog,b,f,true,motionMultiplier:3).Damage.Values.Sum(),130*3*2.5);
            Near(IncarnonBladeFlight.Calculate(catalog,b,f,true,3,motionMultiplier:3).Damage.Values.Sum(),130*3*2.5*.7);
            var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var first=stances.ForWeapon(w).Single(s=>s.IsDefault).Combos.Single(c=>c.Key=="Neutral").Hits.First(h=>h.Multiplier>0&&h.AttackType!="Slam");
            Near(IncarnonBladeFlight.MotionMultiplier(catalog,stances,b,f),first.Multiplier);
            Reject(()=>IncarnonBladeFlight.MotionMultiplier(catalog,stances,b,f with {LaunchHit=256}),"타수");
        });
        check("헤이트 전투:접촉 후0.4초 폭발·지정 대상별3m 반경·폭발CO 제외",()=>{
            var b=B(IncarnonBladeFlight.Hate);b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("가장자리",3,Defense:new(1e9,0)),new("밖",3.01,Defense:new(1e9,0))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.Events.Single(e=>e.Kind=="incarnonBladeImpact").Time,.1);Yes(r.Events.Count(e=>e.Kind=="incarnonBladeExplosion")==2);Near(r.Events.First(e=>e.Kind=="incarnonBladeExplosion").Time,.5);Yes(r.Enemies![1].DirectDamage>0&&r.Enemies[2].DirectDamage==0&&!r.HasSnapshotDps);
            Mods(b,"Condition Overload");var co=CombatTimeline.Calculate(catalog,b,stances,1);Near(co.Events.First(e=>e.Kind=="incarnonBladeExplosion").Amount,r.Events.First(e=>e.Kind=="incarnonBladeExplosion").Amount);
        });
        check("액크 도끼 전투:공중 직격·즉발 폭발·두번째 이펙트 제외·콤보 미획득",()=>{
            var b=B(IncarnonBladeFlight.Ack);var r=CombatTimeline.Calculate(catalog,b,stances,1);
            Yes(r.Events.Count(e=>e.Kind=="incarnonBladeImpact")==1&&r.Events.Count(e=>e.Kind=="incarnonBladeExplosion")==1);Near(r.Events.Single(e=>e.Kind=="incarnonBladeExplosion").Time,.1);Near(r.Points.Last().ComboPoints,0);
            b=b with {Encounter=b.Encounter! with {IncarnonBlades=[new(0,.1,Surface:true)]}};var surface=CombatTimeline.Calculate(catalog,b,stances,1);Yes(surface.Events.All(e=>e.Kind!="incarnonBladeImpact")&&surface.Events.Count(e=>e.Kind=="incarnonBladeExplosion")==1);
        });
        check("액크 도끼 전투:직격 처치 후 주변 폭발·일반 칼날 피해 미삽입",()=>{
            var b=B(IncarnonBladeFlight.Ack);b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("주변",5,Defense:new(1e9,0)),new("밖",5.01,Defense:new(1e9,0))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage,1);Yes(r.Enemies![1].DirectDamage>=303*2*.7&&r.Enemies[2].DirectDamage==0);
        });
        check("유령 칼날 전투:발사 뒤 변신 만료와 피해 버프 저장 불확실성 분리",()=>{
            var b=B(IncarnonBladeFlight.Hate);b=b with {Melee=b.Melee! with {IncarnonSecondsRemaining=.05}};var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Any(e=>e.Kind=="incarnonBladeExplosion"));
            b=b with {Melee=b.Melee with {Incarnon=false}};Reject(()=>CombatTimeline.Calculate(catalog,b,stances,1),"변신");
        });
        check("헤이트 전투:부착 중 숙주 사망의 폭발 위치를 임의 생성하지 않음",()=>{
            var b=B(IncarnonBladeFlight.Hate);b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("주변",1,Defense:new(1e9,0))]}};
            Reject(()=>CombatTimeline.Calculate(catalog,b,stances,1),"숙주가 사망");
        });
        check("액크 도끼 전투:원소4충전을 한 발사에서 공유하고 다음 발사에서 제거",()=>{
            var plain=B(IncarnonBladeFlight.Ack);var b=plain with {Melee=plain.Melee! with {Conditions=new(){["BlockHeat"]=2,["BlockCold"]=2}},Encounter=plain.Encounter! with {IncarnonBlades=[new(0,.1),new(1,1.1,Projectile:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);var baseline=CombatTimeline.Calculate(catalog,plain,stances,1);
            Yes(r.Events.Count(e=>e.Kind=="absorbedElementSpent")==1);Near(r.Events.Single(e=>e.Kind=="absorbedElementSpent").Amount,4);
            Near(r.Events.First(e=>e.Kind=="incarnonBladeImpact").Amount,baseline.Events.Single(e=>e.Kind=="incarnonBladeImpact").Amount*1.7);
        });
        check("유령 칼날 전투:비행 중 범용 피해 발동의 미기재 저장 시점을 숨기지 않음",()=>{
            var b=B(IncarnonBladeFlight.Ack);b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5)),Encounter=b.Encounter! with {Inputs=[new(.05,"WallLatch")]}};
            Reject(()=>CombatTimeline.Calculate(catalog,b,stances,1),"버프가 바뀌");
        });
    }
}
