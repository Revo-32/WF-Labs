using TennoForge.Core;

internal static class SpinnerexTimelineChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("스피네렉스 폭발 전이 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        Build B(params CombatEnemy[] nearby)=>new(SpinnerexMechanic.WeaponId,new ModSlot?[8],new(Enemy:new(1,0)),c.Version,5,
            Capacity:CapacityCatalog.Load().Default(SpinnerexMechanic.WeaponId,null),Melee:new(Statuses:new(){[DamageType.Toxin]=10}),Encounter:new(EncounterMode.Primer,NearbyEnemies:nearby));
        check("스피네렉스 폭발:중심의 대상별 방어력/실드/오버가드·비치명480·독성 강제 부여",()=>{
            var b=B(new("armor",0,Defense:new(100000,2700)),new("shields",0,Defense:new(100000,0),Shields:10000),new("guard",0,Defense:new(100000,2700),Overguard:10000));
            var r=CombatTimeline.Calculate(c,b,stances,1);Yes(r.Unsupported.Length==0);Near(r.DirectDamage,1);var armor=r.Enemies!.Single(e=>e.Id=="armor");Near(armor.DirectDamage,48);Near(armor.StatusTypes,1);
            var shields=r.Enemies!.Single(e=>e.Id=="shields");Near(shields.DirectDamage,480);Near(shields.Shields,10000);Near(shields.Health,99520);
            var guard=r.Enemies!.Single(e=>e.Id=="guard");Near(guard.Overguard,9520);Near(guard.Health,100000);Near(guard.StatusTypes,1);
        });
        check("스피네렉스 폭발:처치 원래 대상 중복 없음·10m 밖 제외·미기재 내부 감쇠는 숨기지 않음",()=>{
            var outside=CombatTimeline.Calculate(c,B(new CombatEnemy("far",10.01,Defense:new(100000,0))),stances,1);Yes(outside.Unsupported.Length==0);Near(outside.Enemies!.Single(e=>e.Id=="far").DirectDamage,0);
            try{CombatTimeline.Calculate(c,B(new CombatEnemy("falloff",5,Defense:new(100000,0))),stances,1);throw new Exception("미기재 감쇠를 확정값으로 계산");}catch(CombatRuleUnavailableException ex){Yes(ex.Message.Contains("감쇠"));}
        });
        check("스피네렉스 폭발:프라이머 독성0이면 폭발 없음·강제 독성만 발생·원소 모드 피해 별도",()=>{
            var b=B(new CombatEnemy("center",0,Defense:new(100000,0)));var cold=c.Mods.First(m=>m.EnglishName=="North Wind");b.Slots[0]=new(cold.Id,cold.MaxRank);
            var r=CombatTimeline.Calculate(c,b,stances,1);var target=r.Enemies!.Single(e=>e.Id=="center");Near(target.DirectDamage,480*1.9);Near(target.StatusTypes,1);
            var none=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Statuses=[]}},stances,1);Near(none.Enemies!.Single(e=>e.Id=="center").DirectDamage,0);
        });
    }
}
