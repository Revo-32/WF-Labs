using TennoForge.Core;

internal static class KillBuffChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("처치 효과 조건 불일치");}
        void Near(double actual,double expected){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{actual} != {expected}");}
        Build B(string name,params string[] names)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,slots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds));
        }
        check("페넌트 처치 버프: 콤보별2~46초·각 중첩 독립 만료",()=>{
            var buffs=new MeleeKillBuffs(true,false);buffs.Kill(0,true,1);buffs.Kill(1,true,3);
            buffs.Advance(2);Yes(buffs.PennantStacks==1);buffs.Advance(11);Yes(buffs.PennantStacks==0);
            buffs.Kill(11,true,12);buffs.Advance(56.9);Yes(buffs.PennantStacks==1);buffs.Advance(57);Yes(buffs.PennantStacks==0);
            buffs.Kill(57,false,12);Yes(buffs.PennantStacks==0);
        });
        check("버서커 퓨리: 갱신10초·한 중첩씩 감소, 연속 처치에도2중첩 상한",()=>{
            var buffs=new MeleeKillBuffs(false,true);buffs.Kill(0,false,1);buffs.Kill(1,true,12);buffs.Kill(2,false,1);
            buffs.Advance(11.9);Yes(buffs.BerserkerStacks==2);buffs.Advance(12);Yes(buffs.BerserkerStacks==1);
            buffs.Kill(15,false,1);buffs.Advance(24.9);Yes(buffs.BerserkerStacks==2);buffs.Advance(25);Yes(buffs.BerserkerStacks==1);buffs.Advance(35);Yes(buffs.BerserkerStacks==0);
        });
        check("갈바나이즈드: 상태 처치 포함·4중첩 상한·20초마다 한 중첩 만료",()=>{
            var buffs=new MeleeKillBuffs(false,false,true);for(var n=0;n<5;n++)buffs.Kill(n,false,1);
            buffs.Advance(24);Yes(buffs.GalvanizedStacks==3);buffs.Advance(44);Yes(buffs.GalvanizedStacks==2);buffs.Advance(64);Yes(buffs.GalvanizedStacks==1);buffs.Advance(84);Yes(buffs.GalvanizedStacks==0);
        });
        check("갈바나이즈드 리플렉스: 반올림 전 초기 콤보 1.85/랭크·최대81.4",()=>{
            var b=B("Nikana Prime","Galvanized Reflex");b=b with {Melee=new(Conditions:new(){["GalvanizedStacks"]=4})};
            for(var rank=0;rank<=10;rank++){b.Slots[0]=b.Slots[0]! with {Rank=rank};Near(MeleeEngine.Calculate(catalog,b).Melee!.InitialCombo,1.85*(rank+1)*4);}
        });
        check("초기 콤보220: 연속기5초 만료가5.5초 재생을 즉시 완료시키지 않음",()=>{
            var combo=new MeleeComboCounter(220,12,20,12,5);combo.SpendHeavy(0,0,false);combo.Advance(5);Near(combo.Points,200);combo.Advance(5.5);Near(combo.Points,220);
        });
        check("전투 중 초기 콤보 변경: 초당40 회복·활성 명중 콤보는 만료까지 보존",()=>{
            var combo=new MeleeComboCounter(0,1,20,12,5);combo.SetInitial(1,20.35);Near(combo.Points,0);combo.Advance(1.25);Near(combo.Points,10);combo.Advance(2);Near(combo.Points,20.35);
            combo.Gain(2,20);combo.SetInitial(3,0);Near(combo.Points,40.35);combo.Advance(7);Near(combo.Points,0);
        });
        check("리플렉스·스틸 처치 경로: 다른 적4처치 후 콤보81.4까지 점진 회복",()=>{
            var b=B("Nikana Prime","Galvanized Reflex","Galvanized Steel");b=b with {Encounter=new(EncounterMode.TenSeconds,Inputs:Enumerable.Repeat(new CombatInput(1,"MeleeKill"),4).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="galvanized")==4);
            Near(t.Points.Single(p=>p.Time==1).ComboPoints,1);Near(t.Points.Single(p=>p.Time==1.5).ComboPoints,21);Near(t.Points.Single(p=>p.Time==3.1).ComboPoints,81.4);
            var before=t.Points.Single(p=>p.Time==.5).SnapshotDps;Yes(t.Points.Single(p=>p.Time==1).SnapshotDps>before);
        });
        check("페넌트 시간 경로: 외부 강공격 처치마다 화력 증가·독립 만료",()=>{
            var b=B("Pennant");b=b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"HeavyMeleeKill",1),new(1,"HeavyMeleeKill",2)])};
            var baseline=MeleeEngine.Calculate(catalog,b).TargetDps;var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);
            double D(double time)=>t.Points.Single(p=>Math.Abs(p.Time-time)<1e-9).SnapshotDps;
            Near(D(0),baseline*1.5);Near(D(1),baseline*2);Near(D(2),baseline*1.5);Near(D(7),baseline);
            Yes(t.Events.Count(e=>e.Kind=="pennant")==2);Near(t.Points.Single(p=>p.Time==1).ComboPoints,1);Near(t.Points.Last().ComboPoints,0);
        });
        check("페넌트 직접 강공격 처치: 콤보 소모 전12배를46초에 반영",()=>{
            var b=B("Pennant") with {Scenario=new(Combo:12,Enemy:new(1,0)),Melee=new(Attack:"Heavy Attack")};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Single(e=>e.Kind=="pennant").Description.Contains("46초"));
        });
        check("공속 전이: 이미 진행 중인 공격의 남은 동작을 보존·버프 만료도 반영",()=>{
            var b=B("Pennant") with {Scenario=new(BaseInterval:2*55d/60,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"HeavyMeleeKill",1)])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var hits=t.Events.Where(e=>e.Kind=="hit").ToArray();
            Near(hits[0].Time,0);Near(hits[1].Time,1+1/1.5);Near(hits[2].Time,3);Near(hits[3].Time,5);
        });
        check("공속 전이: 교전 밖에 예약된 타격도 이후 버프로 구간 안에 진입",()=>{
            var b=B("Pennant") with {Scenario=new(BaseInterval:11*55d/60,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"HeavyMeleeKill",12)])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var hits=t.Events.Where(e=>e.Kind=="hit").ToArray();Yes(hits.Length==2);Near(hits[1].Time,1+10/1.5);
        });
        check("버서커 시간 경로: 기존 수동 중첩을 영구 버프로 남기지 않음·도움은 발동 제외",()=>{
            var b=B("Nikana Prime","Berserker Fury");b=b with {Melee=new(Conditions:new(){["BerserkerStacks"]=2}),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeAssist"),new(2,"MeleeKill"),new(3,"HeavyMeleeKill",12)])};
            var baseline=MeleeEngine.Calculate(catalog,b with {Melee=new()}).TargetDps;var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);
            Near(t.Points.Single(p=>p.Time==1).SnapshotDps,baseline);Near(t.Points.Single(p=>p.Time==2).SnapshotDps,baseline*1.35);Near(t.Points.Single(p=>p.Time==3).SnapshotDps,baseline*1.7);
        });
        check("도르클레이브: 범위 밖 공격에는 시작 벤데타 충전 유지",()=>{
            var b=B("Dorrclave") with {Melee=new(Attack:"Slam Attack",Distance:100,Conditions:new(){["Vendetta"]=1})};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Any(e=>e.Kind=="miss")&&!t.Events.Any(e=>e.Kind=="vendetta"&&e.Description.Contains("남은")));
        });
        check("같은 시각의 모든 외부 사건을 첫 타격 전에 적용·도르클레이브 도움10회",()=>{
            var b=B("Dorrclave");b=b with {Encounter=new(EncounterMode.TenSeconds,Inputs:Enumerable.Repeat(new CombatInput(0,"MeleeAssist"),10).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);
            var activation=Array.FindIndex(t.Events,e=>e.Kind=="vendetta"&&e.Amount==10);var hit=Array.FindIndex(t.Events,e=>e.Kind=="hit");Yes(activation>=0&&activation<hit&&t.Events.Any(e=>e.Kind=="vendetta"&&e.Amount==9));
        });
        check("도르클레이브: 열 번째 처치를 만든 타격에 새10회 충전을 소급 소모하지 않음",()=>{
            var b=B("Dorrclave") with {Scenario=new(Enemy:new(1,0)),Encounter=new(EncounterMode.FiveSeconds,Inputs:Enumerable.Repeat(new CombatInput(0,"MeleeAssist"),9).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Any(e=>e.Kind=="vendetta"&&e.Amount==10)&&!t.Events.Any(e=>e.Kind=="vendetta"&&e.Amount==9));
        });
        check("크레센도: 기존 중첩+이 무기 마무리+파라존, 일반 근접/주무기 처치 제외",()=>{
            var id=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Crescendo").Id;
            var b=B("Nikana Prime","Galvanized Steel") with {Arcanes=new(Melee:new(id,5),FinisherKills:2),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"FinisherKill"),new(2,"ParazonKill"),new(3,"PrimaryKill"),new(4,"MeleeKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var e=t.Events.Where(e=>e.Kind=="crescendo").ToArray();Yes(e.Length==2);Near(e[0].Amount,18);Near(e[1].Amount,24);Near(t.Points.Last().ComboPoints,24);
            Yes(t.Events.Count(e=>e.Kind=="galvanized")==2);
        });
        check("크레센도: 실제 지상 마무리 처치 사건으로 한 번만 초기 콤보 증가",()=>{
            var b=B("Nikana Prime");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));
            var id=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Crescendo").Id;
            b=b with {Scenario=new(Enemy:new(1,0)),Stance=new(s.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.IsDefault?null:s.Id),Arcanes=new(Melee:new(id,5)),Melee=new(ComboDurationOverride:2)};
            var c=StanceEngine.Calculate(catalog,b,stances);b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,c.Hits.Sum(h=>h.Count)).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="crescendo")==1);Near(t.Events.Single(e=>e.Kind=="crescendo").Amount,6);
        });
        check("세라믹 대거 건 앤 블레이드: 주무기만 집계·시작99중첩 보존·100상한",()=>{
            var b=B("Ceramic Dagger");var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Condition=="PrimaryKills");
            b=b with {Melee=new(Evolutions:[perk.Id],Conditions:new(){["PrimaryKills"]=99}),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeKill"),new(2,"PrimaryKill"),new(3,"PrimaryKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var e=t.Events.Where(e=>e.Kind=="gunAndBlade").ToArray();Yes(e.Length==2&&e.All(e=>e.Amount==100));Near(t.Points.Last().ComboPoints,100);
        });
    }
}
