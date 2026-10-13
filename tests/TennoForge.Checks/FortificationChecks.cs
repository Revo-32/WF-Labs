using TennoForge.Core;

internal static class FortificationChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool x){if(!x)throw new Exception("포티피케이션 전이 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Fortification");
        Build B(string name="Sibear")
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var perk=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition=="Armor450");
            return new(w.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Evolutions:[perk.Id]),Arcanes:new(Melee:new(arcane.Id,5),PlayerArmor:300),Encounter:new(EncounterMode.TenSeconds));
        }
        check("포티피케이션: 10초 독립 만료·255중첩·상한 이후 갱신 근거 없으면 거부",()=>{
            var s=new MeleeFortificationState(5);s.Kill(0);s.Kill(2);s.Advance(10);Near(s.Bonus,210);s.Advance(12);Near(s.Bonus,0);
            for(var i=0;i<255;i++)s.Kill(12);Near(s.Bonus,53550);var rejected=false;try{s.Kill(13);}catch(CombatRuleUnavailableException){rejected=true;}Yes(rejected);
        });
        check("방어력 진화: 정확히450은 미발동·450초과 발동·저장 수동 토글보다 수치 우선",()=>{
            var b=B();var low=MeleeEngine.Calculate(catalog,b with {Arcanes=b.Arcanes! with {PlayerArmor=450},Melee=b.Melee! with {Conditions=new(){["Armor450"]=1}}});
            var high=MeleeEngine.Calculate(catalog,b with {Arcanes=b.Arcanes! with {PlayerArmor=450.01}});
            Near(high.Damage.Values.Sum()-low.Damage.Values.Sum(),40);Near(low.Melee!.Setup.Conditions!["Armor450"],0);Near(high.Melee!.Setup.Conditions!["Armor450"],1);
            var copy=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Near(copy.Arcanes!.PlayerArmor!.Value,300);
        });
        check("포티피케이션: 처치/도움 구분·진화 피해 활성·10초 만료 시 원복",()=>{
            var b=B() with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"MeleeKill"),new(1,"MeleeAssist")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="fortification")==1);
            Near(t.Points[0].PlayerArmor!.Value,510);Near(t.Points.Last().PlayerArmor!.Value,300);Near(t.Points.Last().FortificationArmor,0);
            var low=MeleeEngine.Calculate(catalog,b);var high=CombatTimeline.Calculate(catalog,b with {Arcanes=b.Arcanes! with {Melee=null,PlayerArmor=510}},stances,1);
            Near(t.Points[0].SnapshotDps,high.Points[0].SnapshotDps);Near(t.Points.Last().SnapshotDps,low.TargetDps);
        });
        check("포티피케이션: 워프레임 방어력 미입력 시 진화 전이를 임의 결정하지 않음",()=>{
            var b=B();var t=CombatTimeline.Calculate(catalog,b with {Arcanes=b.Arcanes! with {PlayerArmor=null}},stances,1);Yes(t.Unsupported.Any(s=>s.Contains("워프레임 방어력")));
        });
        check("액크 & 브런트: 처치로 방어력450초과→강공격 효율70%→콤보 실제 소모",()=>{
            var b=B("Ack & Brunt");b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Attack="Heavy Attack"},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"MeleeKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);Near(t.Events.Single(e=>e.Kind=="heavyCombo").Amount,66);Near(t.Points[0].ComboPoints,154);
        });
    }
}
