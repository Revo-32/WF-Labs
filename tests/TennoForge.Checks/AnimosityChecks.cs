using TennoForge.Core;

internal static class AnimosityChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Animosity");
        void Yes(bool value){if(!value)throw new Exception("애니모시티 사건 불일치");}
        void Near(double actual,double expected){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{actual} != {expected}");}
        Build B(string weapon,string attack="Normal Attack",double stacks=0)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==weapon);
            return new(w.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,
                Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack,Conditions:new(){["AnimosityStacks"]=stacks}),
                Arcanes:new(Melee:new(arcane.Id,5)),Encounter:new(EncounterMode.FiveSeconds));
        }
        CombatTimelineResult Run(Build b)
        {
            var t=CombatTimeline.Calculate(catalog,b,stances,1);
            if(t.Unsupported.Length>0)throw new Exception(string.Join("; ",t.Unsupported));
            return t;
        }
        check("애니모시티: 일반 명중 입력당1중첩·10상한·일반 슬램 제외",()=>{
            var normal=Run(B("Nikana Prime",stacks:2));Near(normal.Points.Last().AnimosityBonus!.Value,3*.42);
            var max=Run(B("Nikana Prime",stacks:10));Near(max.Points.Last().AnimosityBonus!.Value,4.2);
            var slam=Run(B("Nikana Prime","Slam Attack",2));Near(slam.Points.Last().AnimosityBonus!.Value,2*.42);
            Yes(!slam.Events.Any(e=>e.Kind=="animosity"));
        });
        check("애니모시티: 강 슬램 빗나감은 중첩 유지·콤보 소모와 분리",()=>{
            var b=B("Nikana Prime","Heavy Slam Attack",10);b=b with {Melee=b.Melee! with {Distance=100},Scenario=b.Scenario with {Combo=12}};
            var t=Run(b);Near(t.DirectDamage,0);Near(t.Points.Last().AnimosityBonus!.Value,4.2);
            Near(t.Events.Single(e=>e.Kind=="heavyCombo").Amount,220);Yes(t.Events.Any(e=>e.Kind=="miss"));
            var hit=Run(b with {Melee=b.Melee! with {Distance=0}});Near(hit.Points.Last().AnimosityBonus!.Value,0);
        });
        check("애니모시티: 여러 타격인 스탠스도 입력 그룹마다1중첩",()=>{
            var b=B("Nikana Prime");b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")};
            var s=StanceEngine.Calculate(catalog,b,stances);var count=s.Hits.Sum(h=>h.Count);var groups=s.Hits.Select(h=>h.Group).Distinct().Count();
            Yes(count>groups);b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,count).ToArray())};
            var t=Run(b);Near(t.Points.Last().AnimosityBonus!.Value,Math.Min(10,groups)*.42);Yes(t.Events.Count(e=>e.Kind=="animosity")==groups);
        });
        check("애니모시티: 지상 글레이브 투척·자동 폭발만 예외 치확, 강공격 전용 효과와 분리",()=>{
            var steel=catalog.Mods.Single(m=>m.EnglishName=="True Steel");var life=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");
            foreach(var attack in new[]{"Throw","Charged Throw","Throw Bounce Explosion","Charged Throw Bounce Explosion"})
            {
                var b=B("Glaive Prime",attack,10);b.Slots[0]=new(steel.Id,steel.MaxRank);b.Slots[1]=new(life.Id,life.MaxRank);
                var plain=MeleeEngine.Calculate(catalog,b with {Slots=new ModSlot?[8],Arcanes=null});var c=MeleeEngine.Calculate(catalog,b);
                // Glaive weapon-type Wiki: standing volatile explosion doubles
                // crit MODS, independently of Animosity's +420% arcane bracket.
                Near(c.CriticalChance,plain.CriticalChance*(1+(attack.Contains("Bounce")?2.4:1.2)+4.2));
                var airborne=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["AnimosityStacks"]=10,["Airborne"]=1}}});
                Near(airborne.CriticalChance,plain.CriticalChance*2.2);
                var t=Run(b);Near(t.Points.Last().AnimosityBonus!.Value,4.2);Near(t.Recovery!.HeavyHealing!.Mean,0);
                Yes(!t.Events.Any(e=>e.Kind is "animosity" or "heavyCombo"));
            }
        });
        check("애니모시티: 글레이브 수동 회수 폭발에는 강공격 치확·명중 소비 적용",()=>{
            var b=B("Glaive Prime","Charged Throw Recall Explosion",10);var steel=catalog.Mods.Single(m=>m.EnglishName=="True Steel");b.Slots[0]=new(steel.Id,steel.MaxRank);
            Near(MeleeEngine.Calculate(catalog,b).CriticalChance,.26*(1+2.4+4.2));
            var hit=Run(b);Near(hit.Points.Last().AnimosityBonus!.Value,0);Yes(hit.Events.Single(e=>e.Kind=="animosity").Amount==10);
            var miss=Run(b with {Melee=b.Melee! with {Distance=100}});Near(miss.Points.Last().AnimosityBonus!.Value,4.2);
        });
        check("애니모시티: 한 입력의 첫 타격에서 처치해도 중첩·콤보 소비",()=>{
            var b=B("Ceramic Dagger",stacks:10);b=b with {Stance=new("Dagger","Heavy",stances.Revision),Melee=b.Melee! with {ComboDurationOverride=2},Scenario=new(Combo:4,Enemy:new(1,0)),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,.1,.5,.6])};
            var t=Run(b);Near(t.Points.Last().AnimosityBonus!.Value,0);Near(t.Events.Single(e=>e.Kind=="heavyCombo").Amount,60);Near(t.Ttk.KillFraction,1);
        });
        check("애니모시티: 정수가 아닌 시작 중첩을 조용히 버림 처리하지 않음",()=>{
            try{MeleeEngine.Calculate(catalog,B("Nikana Prime",stacks:1.5));}catch(ArgumentException){return;}
            throw new Exception("1.5중첩이 허용됨");
        });
    }
}
