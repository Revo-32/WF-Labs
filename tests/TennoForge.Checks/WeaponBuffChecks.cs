using System.Text.Json;
using TennoForge.Core;

internal static class WeaponBuffChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string name="Nikana Prime",string attack="Normal Attack")
        {var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e12,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack));}
        Build Buff(Build b,double roar=0,double eclipse=0)=>b with {Melee=b.Melee! with {Buffs=new(roar,eclipse)}};
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        Calculation C(Build b)=>MeleeEngine.Calculate(catalog,b);
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("외부 버프 계산 규칙 불일치");}
        void Reject(Action f){try{f();}catch(ArgumentException){return;}throw new Exception("잘못된 버프를 허용했습니다.");}
        check("로어:위키 예제·팩션모드와 가산·모든 팩션에 적용",()=>{
            foreach(var faction in new[]{"Neutral","Grineer","Corpus","Infested","Orokin"})
            {var b=B();b=b with {Melee=b.Melee! with {Faction=faction}};Near(C(Buff(b,.5)).ExpectedHit,C(b).ExpectedHit*1.5);}
            var smite=B();Mods(smite,"Smite Grineer");smite=smite with {Melee=smite.Melee! with {Faction="Grineer"}};
            var c=C(Buff(smite,.5));Near(c.Melee!.FactionMultiplier,1.8);Near(c.ExpectedHit,198*1.8*(1+.28*1.4));
            Near(c.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,198*.35*1.8*1.8*(1+.28*1.4));
        });
        check("이클립스:가산 컨오 제외·프레셔와 강공격 브라켓 이후",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload");b=b with {Scenario=b.Scenario with {ExistingStatusTypes=3}};
            var c=C(Buff(b,eclipse:2));Near(c.Melee!.Brackets!.ModdedBaseDamage,198*((1+1.2)*3+.8*3));
            Near(c.ExpectedHit,198*9*(1+.28*1.4));Near(c.Melee.Brackets.AdditiveStage,198*9);Near(c.Melee.Brackets.FinalMultiplier,1);
            Near(DpsEquation.From(c).Evaluate(),c.TargetDps);Yes(DpsEquation.From(c).Terms.Any(t=>t.Id=="eclipse"));
            b=b with {Melee=b.Melee! with {Attack="Heavy Attack"},Scenario=b.Scenario with {Combo=4}};Mods(b,"Pressure Point","Condition Overload","Killing Blow");
            var h=C(Buff(b,eclipse:2));var br=h.Melee!.Brackets!;
            Near(br.BeforeFaction,br.BaseDamage*((1+1.2+1.2)*3+2.4)*4);
        });
        check("로어·이클립스:교차항·상태 피해는 팩션제곱 이클립스1회",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload","Smite Grineer");
            b=b with {Scenario=b.Scenario with {ExistingStatusTypes=3},Melee=b.Melee! with {Faction="Grineer"}};
            var c=C(Buff(b,.5,2));Near(c.ExpectedHit,198*9*1.8*1.392);
            Near(c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,198*9*.35*1.8*1.8*1.392);
            Near(DpsEquation.From(c).Evaluate(),c.TargetDps);
            Yes(c.Melee.Contributions!.Any(p=>p.SourceId=="buff:roar"&&p.SourceUrl==WeaponBuffs.RoarSource));
        });
        check("이클립스:글레이브 직격1회·수동/볼래틸 폭발2회·슬램1회",()=>{
            foreach(var w in catalog.Weapons.Where(w=>w.WeaponType=="Glaive"))
            foreach(var attack in MeleeRuleCatalog.Load().Weapon(w.Id).Attacks.Where(a=>a.Key.Contains("Throw")||a.Key=="Slam Attack"))
            {
                var b=B(w.EnglishName,attack.Key);
                if(attack.Key.Contains("Bounce"))Mods(b,"Volatile Quick Return");
                var plain=C(b);var c=C(Buff(b,eclipse:2));Near(c.ExpectedHit,plain.ExpectedHit*(attack.Key.EndsWith("Explosion")?9:3));
                Near(c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick,plain.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).DamagePerTick*(attack.Key.EndsWith("Explosion")?9:3));
            }
        });
        check("이클립스:컨테이전 세 성분2회·곱연산 컨오·검에는1회",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var w=catalog.Weapons.Single(w=>w.Id==id);var b=B(w.EnglishName);
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
            Mods(b,"Condition Overload");b=b with {Scenario=b.Scenario with {ExistingStatusTypes=3},Melee=b.Melee! with {Exodia=new(a.Id,3)}};
            var plain=ExodiaContagionMechanic.Calculate(catalog,b,0);var boost=ExodiaContagionMechanic.Calculate(catalog,Buff(b,.5,2),0);
            Near(boost.Contact.ExpectedDamage,plain.Contact.ExpectedDamage*9*1.5);Near(boost.EmbeddedTick.ExpectedDamage,plain.EmbeddedTick.ExpectedDamage*9*1.5);Near(boost.Explosion.ExpectedDamage,plain.Explosion.ExpectedDamage*9*1.5);
            var legacy=ExodiaContagionMechanic.Calculate(catalog,b with {Melee=b.Melee with {ContagionBuffs=new(Eclipse:2)}},0);
            Near(legacy.Contact.ExpectedDamage,ExodiaContagionMechanic.Calculate(catalog,Buff(b,eclipse:2),0).Contact.ExpectedDamage);
            var sword=C(Buff(b,eclipse:2));Near(sword.Melee!.Brackets!.EclipseMultiplier,3);
            Reject(()=>C(Buff(b with {Melee=b.Melee with {ContagionBuffs=new(Eclipse:2)}},eclipse:2)));
        });
        check("외부 버프:실제 교전 직접/강제베기 틱·5초/10초 유지",()=>{
            foreach(var mode in new[]{EncounterMode.FiveSeconds,EncounterMode.TenSeconds})
            {
                var b=B(attack:"Heavy Attack");b=b with {Encounter=new(mode)};
                var plain=CombatTimeline.Calculate(catalog,b,stances,32);var r=CombatTimeline.Calculate(catalog,Buff(b,.5,2),stances,32);
                Yes(r.Unsupported.Length==0&&r.StatusDamage>0);Near(r.DirectDamage,plain.DirectDamage*4.5);Near(r.StatusDamage,plain.StatusDamage*6.75);Near(DpsEquation.FromTimeline(r).Evaluate(),(r.DirectDamage+r.StatusDamage)/r.Seconds);
            }
        });
        check("외부 버프:중첩 스탠스의 타격별 브라켓과 합산 수식",()=>{
            var b=B();Mods(b,"Pressure Point","Condition Overload");b=Buff(b,.5,2) with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")};
            var s=StanceEngine.Calculate(catalog,b,stances);Near(DpsEquation.From(s.Stats,s).Evaluate(),s.TargetDps!.Value);
            Yes(s.Hits.All(h=>h.Calculation!.Melee!.Brackets!.EclipseMultiplier==3&&h.Calculation.Melee.FactionMultiplier==1.5));
        });
        check("외부 버프:저장복원·기존빌드 호환·잘못된 값 거부",()=>{
            var b=Buff(B(),.753,1.375);var copy=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;
            Yes(copy.Melee!.Buffs==b.Melee!.Buffs);Near(C(copy).ExpectedHit,C(b).ExpectedHit);Near(C(Buff(B())).ExpectedHit,C(B()).ExpectedHit);
            foreach(var value in new[]{-1d,double.NaN,double.PositiveInfinity}){Reject(()=>C(Buff(B(),value)));Reject(()=>C(Buff(B(),eclipse:value)));}
        });
    }
}
