using TennoForge.Core;

internal static class StealthChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("은신 회귀 조건 불일치");}
        void Near(double actual,double expected){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{actual} != {expected}");}
        Build B(string weapon="Nikana Prime",params string[] mods)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==weapon);var slots=new ModSlot?[8];
            for(var n=0;n<mods.Length;n++){var m=catalog.Mods.Single(m=>m.EnglishName==mods[n]);slots[n]=new(m.Id,m.MaxRank);}
            return new(w.Id,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null) with {StanceEquipped=false},Melee:new(Conditions:new(){["Invisible"]=1}),Encounter:new(EncounterMode.FiveSeconds));
        }
        Calculation C(Build b)=>MeleeEngine.Calculate(catalog,b);
        CombatTimelineResult Time(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" / ",r.Unsupported));return r;}
        check("은신 브라켓: PP와 치확 모드 이후 치명타+700% 가산, 표시식 일치",()=>{
            var b=B("Nikana Prime","Pressure Point","True Steel");var c=C(b);var expected=198*2.2*(1+7+.28*2.2*(2.4-1));
            Near(c.ExpectedHit,expected);Near(c.Melee!.StealthBonus,7);Near(DpsEquation.From(c).Evaluate(),c.TargetDps);Near(MeleeTargetModel.ExpectedHitAtArmor(c,0),expected);
            Yes(DpsEquation.From(c).Terms.Any(t=>t.Id=="critical"&&t.Label.Contains("은신")));Near(c.CriticalChance,.616);
        });
        check("은신 랭크: 0랭크+100%, 15랭크+400%, 30초과는+700% 상한",()=>{
            var b=B();Near(C(b with {Capacity=b.Capacity! with {WeaponRank=0}}).Melee!.StealthBonus,1);Near(C(b with {Capacity=b.Capacity! with {WeaponRank=15}}).Melee!.StealthBonus,4);
            var p=B("Paracesis");Near(C(p with {Capacity=p.Capacity! with {WeaponRank=40}}).Melee!.StealthBonus,7);
        });
        check("은신 상태 피해: 최초 타격의 치명·은신 가산 배수와 팩션 이중 적용",()=>{
            var b=B("Nikana Prime","Pressure Point","Smite Grineer","Melee Elementalist");b=b with {Melee=b.Melee! with {Faction="Grineer"}};
            var c=C(b);Near(c.Melee!.StatusDamage.Single(p=>p.Type==DamageType.Slash).DamagePerTick,198*2.2*1.3*1.3*.35*1.9*(8+.28*1.4));
        });
        check("에테르 대거: 30랭크 은신+900%, 저랭크 미기재를 임의 보간하지 않음",()=>{
            var b=B("Ether Daggers");Near(C(b).Melee!.StealthBonus,9);
            try{C(b with {Capacity=b.Capacity! with {WeaponRank=10}});throw new Exception("미기재 보간 허용");}catch(CombatRuleUnavailableException){}
        });
        check("은신 듀플리케이트: 원래 접촉 이후 복제 타격은 은신 대기 반영",()=>{
            var b=B();var id=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Duplicate").Id;b=b with {Arcanes=new(Melee:new(id,5))};var c=C(b);
            Near(c.ExpectedHit,198*((8+.28*1.4)+.28*(1+.28*1.4)));Near(DpsEquation.From(c).Evaluate(),c.TargetDps);
            var blind=C(b with {Melee=b.Melee! with {Conditions=new(){["Invisible"]=1,["Blinded"]=1}}});Near(blind.ExpectedHit,198*(8+.28*1.4)*1.28);
        });
        check("은신 시간 경로: 연속 접촉은 첫 타격만, 3초 쉬면 재적용",()=>{
            var b=B() with {Scenario=new(BaseInterval:65d/60,Enemy:new(1e9,0))};var r=Time(b);var hits=r.Events.Where(e=>e.Kind=="hit").ToArray();Yes(hits.Length==5&&hits[0].Amount/198>=8&&hits.Skip(1).All(e=>e.Amount/198<4));
            var slow=Time(b with {Scenario=b.Scenario with {BaseInterval=3.25}}).Events.Where(e=>e.Kind=="hit").ToArray();Yes(slow.Length==2&&slow.All(e=>e.Amount/198>=8));
        });
        check("실명과 원거리 근접: 접촉 대기 없음, 오버가드는 실명 효과 제외",()=>{
            var b=B() with {Scenario=new(BaseInterval:65d/60,Enemy:new(1e9,0)),Melee=new(Conditions:new(){["Blinded"]=1})};var hits=Time(b).Events.Where(e=>e.Kind=="hit").ToArray();Yes(hits.Length==5&&hits.All(e=>e.Amount/198>=8));
            Near(C(b with {Melee=b.Melee! with {Overguard=1000}}).Melee!.StealthBonus,0);
            var ranged=B("Stropha");ranged=ranged with {Melee=ranged.Melee! with {Attack="Ranged Attack"}};var r=Time(ranged);Yes(!C(ranged).Melee!.RevealsOnHit&&!r.Events.Any(e=>e.Kind=="stealthCooldown"));
        });
        check("은신 지속 피해 스냅샷: 접촉 후 대기가 시작해도 발생한 베기 틱은 유지",()=>{
            var b=B() with {Scenario=new(BaseInterval:60,Enemy:new(1e9,0)),Melee=new(Attack:"Heavy Attack",Conditions:new(){["Invisible"]=1})};var r=Time(b);
            var hit=r.Events.Single(e=>e.Kind=="hit");var proc=r.Events.First(e=>e.Kind=="proc"&&e.Description.StartsWith("베기"));Near(proc.Amount,hit.Amount*.35);
            Yes(r.Events.Any(e=>e.Kind=="stealthCooldown")&&r.Events.Any(e=>e.Kind=="tick"&&e.Description.StartsWith("베기")&&Math.Abs(e.Amount-proc.Amount)<1e-8));
        });
        check("은신 시작 대기: 첫 타격 보너스 없음, 원거리 공격 후 시간이 지나면 복구",()=>{
            var b=B("Stropha");b=b with {Melee=new(Attack:"Ranged Attack",Conditions:new(){["Invisible"]=1,["StealthCooldown"]=1}),Scenario=new(BaseInterval:1,Enemy:new(1e9,0))};
            Near(C(b).Melee!.StealthBonus,0);var hits=Time(b).Events.Where(e=>e.Kind=="hit").ToArray();Yes(hits.Length>1&&hits[0].Amount<C(b).Damage.Values.Sum()*4&&hits[^1].Amount>=C(b).Damage.Values.Sum()*8);
        });
    }
}
