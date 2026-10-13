using TennoForge.Core;

internal static class ThalysChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();
        void Near(double value,double expected){if(!double.IsFinite(value)||Math.Abs(value-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{value} != {expected}");}
        void Yes(bool value){if(!value)throw new Exception("탈리스 가시 검증 실패");}
        ArcaneSlot A(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,5);
        Build B(params string[] names)
        {
            var slots=new ModSlot?[8];for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(ThalysMechanic.WeaponId,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(ThalysMechanic.WeaponId,null),Melee:new(Incarnon:true));
        }
        string Perk(string effect)=>IncarnonEngine.ForWeapon(ThalysMechanic.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey(effect)).Id;
        check("탈리스 가시: 기본225·관통67.5/베기157.5·치확/상확0·치피1",()=>{
            var s=ThalysMechanic.Calculate(catalog,B());Near(s.Echo.Damage[DamageType.Puncture],67.5);Near(s.Echo.Damage[DamageType.Slash],157.5);Near(s.Echo.ExpectedHit,225);Near(s.Echo.CriticalChance,0);Near(s.Echo.CriticalMultiplier,1);Near(s.Echo.StatusChance,0);Near(s.Duration,15);Yes(s.Impale is null);
        });
        check("탈리스 가시: PP와 범용 피해·발동 공격 배율, 컨오/물리/원소/팩션 제외",()=>{
            var b=B("Pressure Point","Condition Overload","Buzz Kill","Fever Strike","Smite Grineer");b=b with {Scenario=new(Combo:12,ExistingStatusTypes:4,Enemy:new(1e9,0)),Melee=b.Melee! with {Faction="Grineer"},Arcanes=new(Warframe1:A("Arcane Arachne"))};
            var c=ThalysMechanic.Calculate(catalog,b,3).Echo;Near(c.Damage.Values.Sum(),225*(1+1.2+1.5)*3);Near(c.Melee!.FactionMultiplier,1);Near(c.Melee.Brackets!.AdditiveConditionOverload,0);Yes(c.Damage.Keys.Order().SequenceEqual(new[]{DamageType.Puncture,DamageType.Slash}.Order()));Near(c.Melee.Brackets.ComboMultiplier,1);
        });
        check("탈리스 가시: 고정 상확은 허용·일반 상확과 무기 상태 지속 모드 제외",()=>{
            var b=B("Proton Snap","Melee Prowess","Lasting Sting");b=b with {Melee=b.Melee! with {Conditions=new(){["ProtonSnap"]=1}}};var c=ThalysMechanic.Calculate(catalog,b).Echo;
            Near(c.StatusChance,.5);var slash=c.Melee!.StatusDamage.Single(p=>p.Type==DamageType.Slash);Near(slash.Chance,.5*.7);Near(slash.Duration,6);Yes(!c.Damage.ContainsKey(DamageType.Toxin));
        });
        check("탈리스 가시: 익스포저도 원소 성분에 누출되지 않음",()=>{
            var b=B() with {Arcanes=new(Melee:A("Melee Exposure"),ExposureStacks:4)};var c=ThalysMechanic.Calculate(catalog,b).Echo;Near(c.Damage.Values.Sum(),225);Yes(!c.Damage.ContainsKey(DamageType.Corrosive));
        });
        check("탈리스 가시 다우티: 가시0상확 대신 일반 무기의30% 관통·45.6% 상확 참조",()=>{
            var b=B("Melee Prowess","Organ Shatter","True Steel");b=b with {Arcanes=new(Melee:A("Melee Doughty"),Warframe1:A("Arcane Avenger"))};var c=ThalysMechanic.Calculate(catalog,b).Echo;
            Near(c.CriticalChance,.45);Near(c.CriticalMultiplier,1*1.9+1.4);Near(c.StatusChance,0);
            b=b with {Arcanes=new(Melee:A("Melee Duplicate"),Warframe1:A("Arcane Avenger"))};c=ThalysMechanic.Calculate(catalog,b).Echo;Near(c.DuplicateChance,0);Near(c.ExpectedHit,225*(1+.45*.9));
        });
        check("탈리스 폭발적 성장: 꿰뚫기450 기반에 컨오 적용·강공격 콤보 제외",()=>{
            var b=B("Pressure Point","Condition Overload","Killing Blow");b=b with {Melee=b.Melee! with {Evolutions=[Perk("ShardImpale")]},Scenario=new(Combo:12,ExistingStatusTypes:2,Enemy:new(1e9,0))};var s=ThalysMechanic.Calculate(catalog,b,3);
            Near(s.Echo.Damage.Values.Sum(),225*2.2*3);Near(s.Impale!.Damage.Values.Sum(),450*(1+1.2+1.6)*3);Near(s.Impale.Melee!.Brackets!.ComboMultiplier,1);Near(s.Impale.Melee.Brackets.HeavyAttackDamage,0);
        });
        check("탈리스 보이드 스플린터: 가시50%·30초·약점 치확을 가시에 더하지 않음",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Evolutions=[Perk("ShardDamageMultiplier")]}};var s=ThalysMechanic.Calculate(catalog,b);Near(s.Echo.ExpectedHit,112.5);Near(s.Duration,30);Near(s.Echo.CriticalChance,0);
        });
        check("탈리스 가시 수명: 새 숙주만 반응·기존 숙주 재타격으로15초 갱신 없음",()=>{
            var s=new ThalysShardState(3);Yes(s.Embed(0,0,0).Length==0);var events=s.Embed(1,1,1);Yes(events.Length==1&&events[0].Target==0);
            Yes(s.Embed(0,14,2).Length==0);Yes(!s.Embedded(0,15));Yes(s.Embed(2,15,3).Single().Target==1);
            var splinters=new ThalysShardState(1,voidSplinters:true);splinters.Embed(0,0,0);Yes(splinters.Embedded(0,29.99)&&!splinters.Embedded(0,30));
        });
        check("탈리스 성장: 스윙당1단계·새 가시도 같은 스윙 성장 가능·3단계 제거·5초 재부착 제한",()=>{
            var s=new ThalysShardState(6,explosiveGrowth:true);s.Embed(0,0,0);s.Embed(1,0,0);s.Embed(2,0,0);
            Near(s.Growth(0,0),1);Near(s.Growth(1,0),1);s.Embed(3,1,1);var events=s.Embed(4,2,2);Yes(events.Any(e=>e.Target==0&&e.Impale));
            Yes(events.Count(e=>e.Target==0)==2&&events.First(e=>e.Target==0).Impale==false);
            Yes(!s.Embedded(0,2));Near(s.ImpaleRemaining(0,2),5);Yes(s.Embed(0,6.99,3).Length==0);s.Embed(0,7,4);Yes(s.Embedded(0,7));
        });
        check("탈리스 연쇄: 숙주당 한 번·5m 연결로 전파·최초 숙주만 콤보 적용",()=>{
            var s=new ThalysShardState(4,chainShatter:true);for(var i=0;i<4;i++)s.Embed(i,0,i);
            double[] positions=[0,4,8,20];var events=s.Detonate(0,1,(a,b)=>Math.Abs(positions[a]-positions[b]));
            Yes(events.Length==3&&events.Count(e=>e.Root)==1&&events.All(e=>e.Detonation));Yes(s.Embedded(3,1)&&!s.Embedded(2,1));Yes(s.Detonate(0,1,(_,_)=>0).Length==0);
            var b=B("Pressure Point","Condition Overload","Shocking Touch","Primed Smite Grineer");b=b with {Scenario=b.Scenario with {Combo=12,ExistingStatusTypes=2},Melee=b.Melee! with {Faction="Grineer"}};
            var root=ThalysMechanic.Detonation(catalog,b,true);var chained=ThalysMechanic.Detonation(catalog,b,false);
            Near(root.Damage.Values.Sum(),600*(1+1.2+1.6)*1.9*12);Near(chained.Damage.Values.Sum(),root.Damage.Values.Sum()/12);Near(root.Melee!.FactionMultiplier,1);Near(root.StatusChance,.24);Near(root.DuplicateChance,0);
        });
        check("탈리스 실제 교전: 다른 대상의 가시225·무기 직접 타격과 별도·동일 대상 재타격은 미발동",()=>{
            var b=B();b=b with {Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",30),new("third",60)],TargetOrder:[0,1,2,0,1,2])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0);var hits=r.Events.Where(e=>e.Kind=="thalysShard").ToArray();
            Yes(hits.Length==3);foreach(var h in hits)Near(h.Amount,225);Yes(r.Enemies!.All(e=>e.DirectDamage>0));
            Near(r.DirectDamage,r.Enemies![0].DirectDamage);Yes(r.Events.Count(e=>e.Kind=="hit")>3);
        });
        check("탈리스 완전 성장:셋째 성장에도 일반225를 유지하고450 꿰뚫기를 추가",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Evolutions=[Perk("ShardImpale")]},Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",30),new("third",60),new("fourth",90)],TargetOrder:[0,1,2,3])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0);var source=r.Events.Where(e=>e.Kind=="thalysShard"&&e.Description.StartsWith("대상1 ·")).ToArray();Near(source.Sum(e=>e.Amount),225*3+450);Yes(source.Count(e=>e.Description.Contains("꿰뚫기"))==1);
        });
        check("탈리스 실제 연쇄: 일반3회→강공격·첫 숙주 콤보와 연쇄 무콤보·재부착",()=>{
            var b=B();b=b with {Scenario=new(Combo:6,Enemy:new(1e9,0)),Melee=b.Melee! with {Evolutions=[Perk("ShardDetonation")]},
                Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",4),new("third",8)],TargetOrder:[0,1,2,0],AttackOrder:["Normal Attack","Normal Attack","Normal Attack","Heavy Attack"])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0);
            var det=r.Events.Where(e=>e.Kind=="thalysShard"&&e.Description.Contains("폭발")).ToArray();Yes(det.Length==3);Near(det.Single(e=>e.Description.Contains("최초")).Amount,600*6);foreach(var e in det.Where(e=>e.Description.Contains("연쇄")))Near(e.Amount,600);
            Yes(r.Events.Any(e=>e.Kind=="heavyCombo"));Yes(r.Events.Count(e=>e.Kind=="thalysShard"&&!e.Description.Contains("폭발"))>3);
        });
    }
}
