using TennoForge.Core;

internal static class TimedModifierChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("시간 효과 전이 불일치");}
        void Near(double? a,double b){if(a is null||!double.IsFinite(a.Value)||Math.Abs(a.Value-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name,params (string Name,int Rank)[] mods)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];for(var i=0;i<mods.Length;i++)slots[i]=new(catalog.Mods.Single(m=>m.EnglishName==mods[i].Name).Id,mods[i].Rank);
            return new(w.Id,slots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds));
        }
        ArcaneSlot Hot()=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Hot Shot").Id,5);
        CombatTimelineResult Run(Build b,int trials=1){var r=CombatTimeline.Calculate(catalog,b,stances,trials);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("스프링 로디드:0/1초 발동은4/5초 독립 만료·최대2·미기재 재갱신은 최초 차이부터 분리",()=>{
            var s=new SpringBladeState(0);s.Proc(0);s.Proc(1);Near(s.Count(3.99),2);Near(s.Count(4),1);Near(s.Count(5),0);
            var uncertain=new SpringBladeState(0);uncertain.Proc(0);uncertain.Proc(1);uncertain.Proc(2);Near(uncertain.Count(3),2);
            try{uncertain.Count(4);throw new Exception("미기재 갱신 고정");}catch(CombatRuleUnavailableException){}
        });
        check("스프링 로디드:실제 강제 베기에 사거리 증가·4초 정확히 원복",()=>{
            var b=B("Nikana Prime",("Spring-Loaded Blade",0));b=b with {Melee=b.Melee! with {Attack="Heavy Attack"}};var baseRange=MeleeEngine.Calculate(catalog,b).Melee!.Range;var r=Run(b);
            Yes(r.Events.Any(e=>e.Kind=="springBlade"));Near(r.Points.Single(p=>p.Time==3.9).MeleeRange,baseRange+Math.Min(2,r.Events.Count(e=>e.Kind=="proc"&&e.Time==0))/6d);Near(r.Points.Single(p=>p.Time==4).MeleeRange,baseRange);
        });
        check("파워 스로우:명중만으로 적립 없음·버퍼 입력만1/2/3상한·새 투척에0",()=>{
            var b=B("Glaive Prime",("Power Throw",5));var attack=MeleeEngine.Attacks(b.WeaponId).First(a=>a.Contains("Throw")&&!a.Contains("Explosion"));
            b=b with {Melee=b.Melee! with {Attack=attack},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"GlaiveChainThrow"),new(2,"GlaiveChainThrow"),new(3,"GlaiveChainThrow"),new(4,"GlaiveChainThrow"),new(5,"GlaiveNewThrow")])};var r=Run(b);
            Near(r.Points[0].PowerThrowStacks,0);Near(r.Points.Single(p=>p.Time==1).PowerThrowStacks,1);Near(r.Points.Single(p=>p.Time==2).PowerThrowStacks,2);Near(r.Points.Single(p=>p.Time==4).PowerThrowStacks,3);Near(r.Points.Single(p=>p.Time==5).PowerThrowStacks,0);
            var control=Run(b with {Encounter=new(EncounterMode.TenSeconds)});Near(r.Points.Single(p=>p.Time==4).SnapshotDps,control.Points.Single(p=>p.Time==4).SnapshotDps*4);
        });
        check("핫 샷:외부 어빌리티 적 수·50상한·10초 전체 갱신·정확한 만료",()=>{
            var b=B("Nikana Prime");b=b with {Arcanes=new(Warframe1:Hot()),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"AbilityHeatInfliction",60)])};var r=Run(b);Near(r.Points[0].HotShotStacks,50);Near(r.Points.Single(p=>p.Time==9.9).HotShotStacks,50);Near(r.Points.Last().HotShotStacks,0);
            var renewed=Run(b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"AbilityHeatInfliction",30),new(1,"AbilityHeatInfliction",30)])});Near(renewed.Points[0].HotShotStacks,30);Near(renewed.Points.Last().HotShotStacks,50);
        });
        check("핫 샷:일반 무기의 실제 화염 부여는 어빌리티 화염으로 집계하지 않음",()=>{
            var regular=B("Nikana Prime",("Molten Impact",5));regular=regular with {Arcanes=new(Warframe1:Hot()),Scenario=regular.Scenario with {BaseInterval=.1}};var r=Run(regular);Yes(r.Events.Any(e=>e.Kind=="proc"&&e.Description.StartsWith("화염")));Near(r.Points.Last().HotShotStacks,0);
        });
        check("디멀센트:영역 실제 틱만 중첩·겹친 영역 각각·속성별15상한·이탈20초",()=>{
            var s=new ResidualElementState();s.Enter(DamageType.Heat,0);s.Tick(DamageType.Heat,.3,2);Near(s.Count(DamageType.Heat,.9),2);s.Tick(DamageType.Heat,1.3,20);Near(s.Count(DamageType.Heat,1.3),15);s.Leave(DamageType.Heat,2);Near(s.Count(DamageType.Heat,21.99),15);Near(s.Count(DamageType.Heat,22),0);
            var t=new ResidualElementState();t.Enter(DamageType.Heat,0);t.Enter(DamageType.Toxin,0);t.Tick(DamageType.Heat,0);t.Tick(DamageType.Toxin,0,3);Near(t.Count(DamageType.Heat,1),1);Near(t.Count(DamageType.Toxin,1),3);
            try{new ResidualElementState().Tick(DamageType.Cold,0);throw new Exception("영역 밖 틱 허용");}catch(ArgumentException){}
        });
        check("디멀센트:시작 중첩 잔여 시간·영역 재진입·원소 모드와 가산 및 재조합",()=>{
            var s=new ResidualElementState(new(){["ResidualCold"]=7,["ResidualColdRemaining"]=1});Near(s.Count(DamageType.Cold,.9),7);s.Enter(DamageType.Cold,.9);Near(s.Count(DamageType.Cold,2),7);s.Leave(DamageType.Cold,2);Near(s.Count(DamageType.Cold,22),0);
            var b=B("Nikana Prime",("North Wind",5));var dem=new ArcaneSlot(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Theorem Demulcent").Id,5);b=b with {Arcanes=new(Warframe1:dem),Melee=b.Melee! with {Conditions=new(){["ResidualToxin"]=15}}};var c=MeleeEngine.Calculate(catalog,b);var baseDamage=c.Melee!.ModdedBaseDamage;Near(c.Damage[DamageType.Viral],baseDamage*(.9+1.8));Yes(!c.Damage.ContainsKey(DamageType.Cold)&&!c.Damage.ContainsKey(DamageType.Toxin));
        });
        check("디멀센트 시간 경로:속성과 중첩을 사건에서 읽고 후속 DPS에 정확히 반영",()=>{
            var b=B("Nikana Prime",("North Wind",5));b=b with {Arcanes=new(Warframe1:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Theorem Demulcent").Id,5)),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(.1,"ResidualEnter",Element:DamageType.Toxin),new(.1,"ResidualTick",3,DamageType.Toxin),new(1,"ResidualLeave",Element:DamageType.Toxin)])};var r=Run(b);
            Near(r.Points.Single(p=>p.Time==.1).SnapshotDps/r.Points[0].SnapshotDps,2.26/1.9);Near(r.Points.Single(p=>p.Time==9.9).SnapshotDps/r.Points.Single(p=>p.Time==9.9).WithoutCoDps,1);Yes(r.Events.Count(e=>e.Kind=="demulcent")==3);
        });
    }
}
