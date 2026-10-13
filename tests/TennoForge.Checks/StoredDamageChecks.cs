using TennoForge.Core;

internal static class StoredDamageChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("저장 피해 사건 불일치");}
        Build B(params string[] mods)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Silva & Aegis Prime");var slots=new ModSlot?[8];
            for(var i=0;i<mods.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==mods[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        }
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("어벤징 트루스:위키 예제 (1590×2.2+3000)×1.9×4=49384.8",()=>{
            var b=B("Pressure Point","Shocking Touch","Avenging Truth");b=b with {Scenario=b.Scenario with {Combo=4},Melee=new(Attack:"Heavy Attack",Conditions:new(){["BlockedDamage"]=6000})};
            var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage.Values.Sum(),49384.8);Near(c.Melee!.Brackets!.PostBaseDamage,3000);
        });
        check("어벤징 트루스:막기 실제 피해 흡수·일반 공격에 유지·다음 강공격에1회 사용",()=>{
            var b=B("Avenging Truth");b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(.2,"BlockedDamage",6000)],AttackOrder:["Normal Attack","Heavy Attack","Heavy Attack"])};
            var r=Run(b);var uses=r.Events.Where(e=>e.Kind=="storedDamageUse").ToArray();Yes(uses.Length>=2);Near(uses[0].Amount,3000);Yes(uses.Skip(1).All(e=>e.Amount==0));
            Near(r.Events.Single(e=>e.Kind=="storedDamage").Amount,3000);Yes(r.Events.Any(e=>e.Kind=="blockCharge"&&e.Time==.2));
        });
        check("어벤징 트루스:일반 공격을 거친 초기 저장량과 이후 재충전을 분리",()=>{
            var b=B("Avenging Truth");b=b with {Melee=new(Conditions:new(){["BlockedDamage"]=2000}),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(2.1,"BlockedDamage",4000)],AttackOrder:["Normal Attack","Heavy Attack","Heavy Attack"])};
            var r=Run(b);var uses=r.Events.Where(e=>e.Kind=="storedDamageUse").ToArray();Near(uses[0].Amount,1000);Near(uses[1].Amount,2000);
        });
        check("어벤징 트루스:100000은 게임 저장 상한이 아님·모드 랭크별20~50%",()=>{
            var b=B("Avenging Truth");b=b with {Melee=new(Attack:"Heavy Attack",Conditions:new(){["BlockedDamage"]=200000})};
            for(var rank=0;rank<=3;rank++){b.Slots[0]=b.Slots[0]! with {Rank=rank};Near(MeleeEngine.Calculate(catalog,b).Melee!.Brackets!.PostBaseDamage,200000*(.2+.1*rank));}
        });
    }
}
