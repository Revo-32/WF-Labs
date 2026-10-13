using System.Text.Json;
using TennoForge.Core;

internal static class CeramicChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=CeramicDaggerMechanic.WeaponId;
        Build B()=>new(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,CeramicDaggers:[new(0,.1),new(0,.2,Dagger:2)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("세라믹 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 세라믹 입력 허용: "+text);}
        check("세라믹:한 발사당2단검·중복/시각/60m/s 조건 검증·설정 저장",()=>{
            Reject(()=>CeramicDaggerMechanic.Validate([new(0,.1),new(0,.2)],1),"두 번");Reject(()=>CeramicDaggerMechanic.Validate([new(0,.1),new(.1,.2,Dagger:2)],1),"발사 시각");Reject(()=>CeramicDaggerMechanic.Validate([new(0,.1,Travel:60)],1),"60m/s");
            var b=B();var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.CeramicDaggers!.SequenceEqual(b.Encounter!.CeramicDaggers!));
        });
        check("세라믹:컨오 직격만 곱연산·일반피해와KB제외·폭발4m30%감쇠",()=>{
            var b=B();Mods(b,"Pressure Point","Killing Blow","Condition Overload","Fever Strike");b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne").Id,5)),Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            Near(CeramicDaggerMechanic.Calculate(catalog,b).Damage.Values.Sum(),350*12*2.5*2.6*1.9);Near(CeramicDaggerMechanic.Calculate(catalog,b,explosion:true).Damage.Values.Sum(),350*12*2.5*1.9);Near(CeramicDaggerMechanic.Calculate(catalog,b,explosion:true,distance:4).Damage.Values.Sum(),350*12*2.5*1.9*.7);Near(CeramicDaggerMechanic.Calculate(catalog,b,explosion:true,distance:4.01).Damage.Values.Sum(),0);
        });
        check("세라믹 전투:두 발사체 모두 시작12배·강공격 콤보 단1회 소모",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {CeramicDaggers=[new(0,.1,Surface:true),new(0,.2,Dagger:2,Surface:true)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,512);Near(r.DirectDamage,2*350*12*1.05,200);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1&&r.Events.Count(e=>e.Kind=="ceramicExplosion")==2);Yes(!r.HasSnapshotDps);Near(r.Points.First(p=>p.Time>=.2).ComboPoints,0);
        });
        check("세라믹 전투:첫 폭발의 강제 충격은 두번째 직격CO에만 반영",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {CeramicDaggers=[new(0,.1,Surface:true),new(0,.2,Dagger:2)]}};
            var plain=CombatTimeline.Calculate(catalog,b,stances,1);Mods(b,"Condition Overload");var co=CombatTimeline.Calculate(catalog,b,stances,1);
            Yes(co.Events.Single(e=>e.Kind=="ceramicImpact").Amount>=1.8*plain.Events.Single(e=>e.Kind=="ceramicImpact").Amount-1e-4);
            var p=plain.Events.Where(e=>e.Kind=="ceramicExplosion").ToArray();var c=co.Events.Where(e=>e.Kind=="ceramicExplosion").ToArray();for(var i=0;i<2;i++)Near(p[i].Amount,c[i].Amount);
        });
        check("세라믹 전투:직격 즉사 뒤 주변 폭발·반경 경계·표면 충돌은 직격 없음",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=b.Encounter! with {NearbyEnemies=[new("edge",4,Defense:new(1e9,0)),new("outside",4.01,Defense:new(1e9,0))],CeramicDaggers=[new(0,.1)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage,1);Yes(r.Enemies![1].DirectDamage>=350*12*.7&&r.Enemies[2].DirectDamage==0);Yes(r.Events.Count(e=>e.Kind=="ceramicExplosion")==1);
        });
        check("세라믹 전투:텐노카이 공유 콤보 유지·일반 타격/중복 강공격과 구분",()=>{
            var b=B();var dream=catalog.Mods.Single(m=>m.EnglishName=="Dreamer's Wrath");b=b with {Melee=b.Melee! with {Attack="Heavy Attack",Tennokai=true,Exilus=new(dream.Id,dream.MaxRank)}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,0);Near(r.Points.First(p=>p.Time>=.2).ComboPoints,220);
            Reject(()=>CombatTimeline.Calculate(catalog,b with {Melee=b.Melee with {Attack="Heavy Attack"},Encounter=b.Encounter! with {ScriptedAttacksOnly=false}},stances,1),"이중 입력");
        });
        check("세라믹 전투:발사 후 피해 버프 변경을 임의의 snapshot 규칙으로 대체하지 않음",()=>{
            var b=B();var breach=IncarnonEngine.ForWeapon(id)!.Perks.Single(p=>p.Condition=="ShieldBreak");
            b=b with {Arcanes=new(MaximumShields:100,Shields:100),Melee=b.Melee! with {Evolutions=[breach.Id]},Encounter=b.Encounter! with {Inputs=[new(.05,"PlayerShieldDamage",100)]}};
            Reject(()=>CombatTimeline.Calculate(catalog,b,stances,1),"버프가 변경");
        });
    }
}
