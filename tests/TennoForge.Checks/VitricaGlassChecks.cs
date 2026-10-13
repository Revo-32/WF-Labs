using System.Text.Json;
using TennoForge.Core;

internal static class VitricaGlassChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=VitricaGlassState.WeaponId;
        Build B()=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,VitricaGlasses:[new(0)],VitricaSlams:[new(.5)]));
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("비트리카 유리화 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 유리화 입력 허용: "+text);}
        check("비트리카 상태:7초 경계·재유리화·면역·피해 해제·한 번만 파괴",()=>{
            var g=new VitricaGlassState();Yes(g.Apply(0,0)&&g.Active(0,6.999)&&!g.Active(0,7));Yes(g.Expire(7).SequenceEqual([0]));Yes(!g.Apply(1,8,true));Yes(g.Apply(0,8)&&g.Apply(0,9));Near(g.NextExpiry,16);Yes(g.Remove(0)&&!g.Remove(0)&&!g.Active(0,10));
        });
        check("비트리카 입력:동일 슬램의 시각·타깃 중복 검사·저장 복원",()=>{
            Reject(()=>VitricaGlassState.Validate([],[new(0),new(1,Target:2)],2),"같은 시각");Reject(()=>VitricaGlassState.Validate([],[new(0),new(0)],1),"한 번");
            var b=B();var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.VitricaGlasses!.SequenceEqual(b.Encounter!.VitricaGlasses!)&&saved.Encounter.VitricaSlams!.SequenceEqual(b.Encounter.VitricaSlams!));
        });
        check("비트리카 전투:유리화 자체 무피해·중심5m·오버가드 면역·7초 자동 해제",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {Mode=EncounterMode.TenSeconds,VitricaSlams=[],NearbyEnemies=[new("5m",5,Defense:new(1e9,0)),new("밖",5.01,Defense:new(1e9,0)),new("오버가드",1,Defense:new(1e9,0),Overguard:100)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.DirectDamage+r.StatusDamage,0);Yes(r.Events.Count(e=>e.Kind=="vitricaGlass")==2&&r.Events.Count(e=>e.Kind=="vitricaGlassImmune")==1&&r.Events.Count(e=>e.Kind=="vitricaGlassExpired")==2);Near(r.Events.First(e=>e.Kind=="vitricaGlassExpired").Time,7);
        });
        check("비트리카 전투:원래 유리 대상과6m 주변 성분·일반 피해로 다른 유리 해제",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("주변 유리",5,Defense:new(1e9,0)),new("6m",6,Defense:new(1e9,0)),new("밖",6.01,Defense:new(1e9,0))],VitricaSlams=[new(.5),new(1,Target:2,Slam:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="vitricaShatter")==1&&r.Events.Count(e=>e.Kind=="vitricaExplosion")==3&&r.Events.Any(e=>e.Kind=="vitricaGlassRemoved"));Near(r.Enemies![3].DirectDamage,0);Yes(r.Enemies[2].DirectDamage>=99.9&&!r.HasSnapshotDps);
        });
        check("비트리카 전투:원래 대상 강제100% 상확·모딩된 주변 상확과 분리",()=>{
            var b=B();var m=catalog.Mods.Single(m=>m.EnglishName=="Melee Prowess");b.Slots[0]=new(m.Id,m.MaxRank);
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="proc")==1);Near(r.Events.Single(e=>e.Kind=="vitricaShatter").Time,.5);
        });
        check("비트리카 전투:직접·지속 피해로 유리 제거 후 슬램에 폭발 미발생",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {BaseInterval=1},Encounter=b.Encounter! with {ScriptedAttacksOnly=false,VitricaGlasses=[new(.1)],VitricaSlams=[new(1.5)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Any(e=>e.Kind=="vitricaGlassRemoved")&&r.Events.All(e=>e.Kind!="vitricaShatter"));
        });
        check("비트리카 전투:한 강 슬램의 여러 유리 폭발은 콤보를 한 번 공유·소모",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Combo=12},Encounter=b.Encounter! with {NearbyEnemies=[new("동시 대상",4,Defense:new(1e9,0))],VitricaSlams=[new(.5,Heavy:true),new(.5,Target:2,Heavy:true)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1&&r.Events.Count(e=>e.Kind=="vitricaShatter")==2&&r.Events.Count(e=>e.Kind=="vitricaExplosion")==4);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,220);Near(r.Points.Last().ComboPoints,0);Yes(r.DirectDamage>=999*12);
        });
    }
}
