using TennoForge.Core;

internal static class WhipclawDomeChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("윕클로 돔 전파 불일치");}
        var id=ExaltedRules.All.First(d=>d.Family==ExaltedFamily.Whipclaw).Id;
        Build B(params CombatEnemy[] neighbors)=>new(id,new ModSlot?[8],new(Enemy:new(1e8,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Debuffs:[new("Strangledome")]),Encounter:new(EncounterMode.FirstHit,NearbyEnemies:neighbors));
        CombatEnemy Target(string key,double x,int group=1,double? og=null)=>new(key,x,Overguard:og,Debuffs:[new("Strangledome",Group:group)]);
        check("윕클로 돔:직접 반경 밖 동일 돔에50%·다른 돔/미포획/오버가드 제외",()=>{
            var b=B(Target("same",7),Target("different",8,2),new("free",9),Target("guard",10,og:1000));var r=CombatTimeline.Calculate(c,b,stances,32);Yes(r.Unsupported.Length==0);
            var hits=r.Events.Where(e=>e.Kind=="whipDistribution").ToArray();Yes(hits.Length==1);var basis=MeleeEngine.Calculate(c,b).Damage.Values.Sum();Yes(Math.Abs(hits[0].Amount-basis*.5)<1e-8||Math.Abs(hits[0].Amount-basis)<1e-8);
            Yes(r.Enemies!.Single(e=>e.Id=="same").DirectDamage>0);foreach(var key in new[]{"different","free","guard"})Yes(r.Enemies!.Single(e=>e.Id==key).DirectDamage==0);
        });
        check("윕클로 돔:같은 시전의 직접 명중 대상에는 절반 피해를 중복 추가하지 않음",()=>{
            var r=CombatTimeline.Calculate(c,B(Target("direct",2),Target("remote",7)),stances,1);Yes(r.Events.Count(e=>e.Kind=="hit")==2);Yes(r.Events.Count(e=>e.Kind=="whipDistribution")==1);
        });
        check("윕클로 돔:전파된 대상은 어큐뮬레이팅의 직접3명 명중 조건에 포함하지 않음",()=>{
            var b=B(Target("far1",7),Target("far2",8));b=b with {Melee=b.Melee! with {AbilityAugment=new(ExaltedAugments.Accumulating,3)}};
            var r=CombatTimeline.Calculate(c,b,stances,1);Yes(r.Unsupported.Length==0);Yes(r.Events.Count(e=>e.Kind=="whipDistribution")==2);Yes(r.Events.All(e=>e.Kind!="whipclawAugment"));
        });
        check("윕클로 돔:전파는 적별 방어층·취약도·치명타·상태를 개별 계산",()=>{
            var r=CombatTimeline.Calculate(c,B(Target("armor",7) with {Defense=new(1e8,2700)},Target("plain",8)),stances,128);Yes(r.Unsupported.Length==0);var armor=r.Enemies!.Single(e=>e.Id=="armor");var plain=r.Enemies!.Single(e=>e.Id=="plain");Yes(armor.DirectDamage<plain.DirectDamage*.15);Yes(armor.StatusTypes>0&&plain.StatusTypes>0);
            var expired=CombatTimeline.Calculate(c,B(Target("expired",7) with {Debuffs=[new("Strangledome",Remaining:0)]}),stances,1);Yes(expired.Enemies!.Single(e=>e.Id=="expired").DirectDamage==0);
        });
    }
}
