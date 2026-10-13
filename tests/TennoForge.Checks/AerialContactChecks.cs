using TennoForge.Core;

internal static class AerialContactChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B()
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Innodem");var stance=stances.ForWeapon(w).Single(s=>s.IsDefault);var perk=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition=="AirborneKills");
            var b=new Build(w.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1,0)),catalog.Version,5,Stance:new(stance.Id,"Aerial",stances.Revision),Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Evolutions:[perk.Id],ComboDurationOverride:2),Encounter:new(EncounterMode.FiveSeconds));
            var hits=StanceEngine.Calculate(catalog,b,stances).Hits.Sum(h=>h.Count);
            return b with {Encounter=b.Encounter! with {HitTimes=Enumerable.Range(0,hits).Select(i=>i*.1).ToArray()}};
        }
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        void Yes(bool v){if(!v)throw new Exception("공중 접촉 조건 전달 불일치");}
        check("공중 스탠스:별도 공중 토글 없이 실제 처치에 인노뎀 스우핑 런지 발동",()=>{
            var b=B();Yes(b.Melee!.Conditions is null);var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="incarnonStack"&&e.Description.Contains("스우핑 런지")&&e.Amount==1));
        });
        check("공중 스탠스:접촉의 공중 조건을 이후 외부 처치에 영구 유출하지 않음",()=>{
            var b=B();var r=Run(b with {Encounter=b.Encounter! with {Inputs=[new(1,"MeleeKill")]}});Yes(r.Events.Count(e=>e.Kind=="incarnonStack"&&e.Description.Contains("스우핑 런지"))==1);
            var grounded=b with {Stance=null,Encounter=new(EncounterMode.FiveSeconds)};Yes(!Run(grounded).Events.Any(e=>e.Kind=="incarnonStack"&&e.Description.Contains("스우핑 런지")));
        });
    }
}
