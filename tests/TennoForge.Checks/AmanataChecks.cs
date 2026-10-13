using TennoForge.Core;

internal static class AmanataChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("아마나타 규칙 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(){var id=c.Weapons.Single(w=>w.EnglishName=="Amanata").Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Conditions:new(){["Roll"]=4}),Encounter:new(EncounterMode.FirstHit));}
        void Pressure(Build b){var m=c.Mods.Single(m=>m.EnglishName=="Amanata Pressure");b.Slots[0]=new(m.Id,m.MaxRank);}
        check("아마나타:30회 실제 명중 뒤 축복 교체·공격 실패와 듀플리케이트 제외",()=>{
            var state=new AmanataState(1,29);Yes(!state.Contact(0,false,false,false,()=>6));Yes(state.Roll==1&&state.Contacts==29);Yes(!state.Contact(0,false,true,true,()=>6));Yes(state.Contact(1,false,true,false,()=>5));Yes(state.Roll==5&&state.Contacts==0&&state.Rerolls==1);
        });
        check("아마나타:슬램은 헛쳐도1회·같은 범위 공격의 여러 적은 중복하지 않음",()=>{
            var state=new AmanataState(4,29);Yes(state.Contact(0,true,false,false,()=>6));Yes(!state.Contact(0,true,true,false,()=>1));Yes(!state.Contact(0,true,true,false,()=>1));Yes(state.Contacts==0&&state.Roll==6);Yes(!state.Contact(1,true,false,false,()=>1));Yes(state.Contacts==1);
        });
        check("아마나타 프레셔:8×경계·강공격의 첫 대상·입력마다 초기화·13속성",()=>{
            var state=new AmanataState(4);Yes(!state.Pressure(0,false,12,true));Yes(!state.Pressure(0,true,7,true));Yes(!state.Pressure(0,true,8,false));Yes(state.Pressure(0,true,8,true));Yes(!state.Pressure(0,true,12,true));Yes(state.Pressure(1,true,8,true));Yes(AmanataState.PressureTypes.Length==13&&!AmanataState.PressureTypes.Contains(DamageType.Void)&&!AmanataState.PressureTypes.Contains(DamageType.Tau));
        });
        check("아마나타 축복:치확·상확 최종 가산·상태 피해 별도 곱·사거리·공속",()=>{
            var b=B();Calculation Calc(int roll)=>MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Conditions=new(){["Roll"]=roll}}});var naked=Calc(0);var all=Calc(6);Near(all.CriticalChance,naked.CriticalChance+.4);Near(all.StatusChance,naked.StatusChance+.3);Near(all.Melee!.Range,naked.Melee!.Range+1);Near(all.AttackSpeed,naked.AttackSpeed*1.2);
            var status=Calc(3);Near(status.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,naked.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick*1.3);
        });
        check("아마나타 시간 경로:29누적에서 첫 명중 재추첨·가중치 근거 구분",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Conditions=new(){["Roll"]=4,["AmanataHits"]=29}}};var result=CombatTimeline.Calculate(c,b,stances,8);Yes(result.Unsupported.Length==0);Yes(result.Events.Single(e=>e.Kind=="amanataRoll") is {Time:0,Amount:>=1 and <=6});Yes(result.Notes.Any(n=>n.Contains("균등 확률을 가정")));
        });
        check("아마나타 프레셔 시간 경로:8×강공격 추가 상태·7×및 일반 공격 제외",()=>{
            var b=B();Pressure(b);b=b with {Scenario=b.Scenario with {Combo=8},Melee=b.Melee! with {Attack="Heavy Attack"}};var result=CombatTimeline.Calculate(c,b,stances,16);Yes(result.Unsupported.Length==0);Yes(result.Events.Count(e=>e.Kind=="amanataPressure")==1);var low=CombatTimeline.Calculate(c,b with {Scenario=b.Scenario with {Combo=7}},stances,1);Yes(!low.Events.Any(e=>e.Kind=="amanataPressure"));var normal=CombatTimeline.Calculate(c,b with {Melee=b.Melee! with {Attack="Normal Attack"}},stances,1);Yes(!normal.Events.Any(e=>e.Kind=="amanataPressure"));
        });
        check("아마나타 프레셔:강 슬램이 여러 적에 닿아도 첫 명중 대상에게만 추가 상태",()=>{
            var b=B();Pressure(b);b=b with {Scenario=b.Scenario with {Combo=8},Melee=b.Melee! with {Attack="Heavy Slam Attack"},Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("near",1)])};var result=CombatTimeline.Calculate(c,b,stances,16);Yes(result.Unsupported.Length==0);Yes(result.Events.Count(e=>e.Kind=="amanataPressure")==1);Yes(result.Events.Single(e=>e.Kind=="amanataPressure").Amount==1);
        });
    }
}
