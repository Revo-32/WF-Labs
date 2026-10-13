using TennoForge.Core;

internal static class EruptionChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("이럽션 전투 사건 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(){var id=c.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Arcanes:new(Warframe1:new(WarframeStatusArcanes.Eruption,5)),Encounter:new(EncounterMode.FirstHit,Inputs:[new(0,"EnergyOrbPickup")]));}
        void Co(Build b){var m=c.Mods.Single(m=>m.EnglishName=="Condition Overload");b.Slots[0]=new(m.Id,m.MaxRank);}
        check("이럽션:랭크별17/34/51/68/85/100%와5~30m",()=>{
            for(var rank=0;rank<=5;rank++){Near(WarframeStatusArcanes.EruptionChance(rank),new[]{.17,.34,.51,.68,.85,1}[rank]);Near(WarframeStatusArcanes.EruptionRadius(rank),5*(rank+1));}
        });
        check("이럽션:플레이어 중심30m·벽 관통·오버가드 제외·추가 피해 없음",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("edge",30),new("outside",30.01),new("overguard",1,Overguard:100)],BlockedSight=[new(-1,1)]}};var result=CombatTimeline.Calculate(c,b,stances,1);Yes(result.Unsupported.Length==0);var hit=result.Events.Where(e=>e.Kind=="eruptionKnockdown").ToArray();Yes(hit.Length==2&&hit.Any(e=>e.Description.StartsWith("대상2")));var original=CombatTimeline.Calculate(c,b with {Encounter=b.Encounter! with {Inputs=[]}},stances,1);Near(result.DirectDamage,original.DirectDamage);
        });
        check("이럽션:첫 접촉은 실제 넉다운을 CO1종으로 적용·어플릭션즈 발동하지 않음",()=>{
            var b=B();Co(b);var result=CombatTimeline.Calculate(c,b,stances,1);var original=CombatTimeline.Calculate(c,b with {Encounter=b.Encounter! with {Inputs=[]}},stances,1);Near(result.DirectDamage,original.DirectDamage*1.8);Yes(!result.Events.Any(e=>e.Kind=="affliction"));
        });
        check("이럽션:시간 교전 회복시간 미기재를 임의로 채우지 않음·사용자 지정은 정확히 만료",()=>{
            var b=B();Co(b);b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"UniversalOrbPickup")])};var rejected=false;try{CombatTimeline.Calculate(c,b,stances,1);}catch(CombatRuleUnavailableException){rejected=true;}Yes(rejected);
            b=b with {Encounter=b.Encounter! with {Inputs=[new(0,"UniversalOrbPickup",Duration:1)]}};var result=CombatTimeline.Calculate(c,b,stances,1);Yes(result.Unsupported.Length==0);Yes(result.Events.Any(e=>e.Kind=="eruptionKnockdown"&&e.Description.Contains("사용자 지정1초")));Yes(result.Points.First().SnapshotDps>result.Points.Last().SnapshotDps);
        });
        check("이럽션:오브 생성만으로 발동하지 않으며 완전 동결된 적은 넉다운 제외",()=>{
            var b=B();var empty=CombatTimeline.Calculate(c,b with {Encounter=new(EncounterMode.FirstHit)},stances,1);Yes(!empty.Events.Any(e=>e.Kind=="eruptionKnockdown"));b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Cold]=10}},Encounter=b.Encounter! with {Mode=EncounterMode.Primer}};var frozen=CombatTimeline.Calculate(c,b,stances,1);Yes(!frozen.Events.Any(e=>e.Kind=="eruptionKnockdown"));
        });
    }
}
