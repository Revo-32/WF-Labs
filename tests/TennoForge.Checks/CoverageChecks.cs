using System.Text.Json;
using TennoForge.Core;

internal static class CoverageChecks
{
    internal static void Run(Catalog catalog,Action<string,Action> check)
    {
        void Yes(bool value){if(!value)throw new Exception("조건 불일치");}
        void Near(double? a,double? b){if(a is null||b is null||!double.IsFinite(a.Value)||!double.IsFinite(b.Value)||Math.Abs(a.Value-b.Value)>1e-7)throw new Exception($"{a} != {b}");}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("잘못된 입력을 거부하지 않음");}
        var source=ReferenceCatalog.Load();var stances=StanceCatalog.Load();var capacity=CapacityCatalog.Load();var weapon=catalog.Weapons[0];
        ArcaneSlot A(string name,int rank=5,bool active=true)=>new(source.Arcanes.Single(a=>a.EnglishName==name).Id,rank,active);
        Mod M(string name)=>catalog.Mods.Single(m=>m.EnglishName==name);
        Build B(ArcaneSetup? arcanes=null,params string[] mods)=>new(weapon.Id,mods.Select(n=>{var m=M(n);return (ModSlot?)new ModSlot(m.Id,m.MaxRank);}).Concat(Enumerable.Repeat<ModSlot?>(null,8-mods.Length)).ToArray(),new(Enemy:new(1000,0)),catalog.Version,4,Capacity:capacity.Default(weapon.Id,null),Arcanes:arcanes);
        Calculation C(Build b)=>DamageEngine.Calculate(catalog,b);
        check("위키 8분할 원본의 전체 목록 개수",()=>{Yes(source.Weapons.Length==911&&source.Arcanes.Length==166&&source.Mods.Length==1550);Yes(source.Weapons.Select(w=>w.Id).Distinct().Count()==911);Yes(source.Weapons.Select(w=>w.Category).Distinct().Count()==8);Yes(source.Sources.Length==11);});
        check("자료 등록과 계산 지원 상태 구분",()=>{Yes(source.Weapons.Count(w=>w.Calculable)==229);Yes(source.Weapons.Where(w=>!w.Calculable).All(w=>w.Limitations.Length>0));Yes(source.Weapons.Where(w=>w.Calculable).All(w=>catalog.Weapons.Any(x=>x.Id==w.InternalId)));});
        check("전체 기본 근접 수치 보존",()=>{foreach(var w in catalog.Weapons){var c=C(B() with{WeaponId=w.Id,Capacity=capacity.Default(w.Id,null)});Near(c.Damage.Values.Sum(),w.Damage.Values.Sum());Yes(c.ExpectedHit>0&&double.IsFinite(c.RawDps));}});
        check("미지원 일반 모드는 계산에서 조용히 누락되지 않음",()=>{foreach(var m in catalog.Mods.Where(m=>m.CalculationIssues is {Length:>0})){var b=B();b.Slots[0]=new(m.Id,m.MaxRank);Reject(()=>C(b));}});
        check("모든 지원 모드의 0/최대 랭크 수치 유효성",()=>{foreach(var m in catalog.Mods.Where(m=>m.CalculationIssues is not {Length:>0}))foreach(var rank in new[]{0,m.MaxRank}){var b=B();b.Slots[0]=new(m.Id,rank);var c=C(b);Yes(c.ExpectedHit>0&&c.AttackSpeed>0&&double.IsFinite(c.RawDps));}});
        check("물리 모드가 원소 피해를 증폭하지 않음",()=>{var c=C(B(null,"Auger Strike","Fever Strike"));Near(c.Damage[DamageType.Puncture],9.9*2.2);Near(c.Damage[DamageType.Toxin],198*.9);});
        check("무기 내장 단일 원소가 모드 원소와 결합",()=>{var w=catalog.Weapons.First(w=>w.Damage.Count==1&&w.Damage.ContainsKey(DamageType.Heat));var c=C(B(null,"North Wind") with{WeaponId=w.Id,Capacity=capacity.Default(w.Id,null)});Near(c.Damage[DamageType.Blast],w.Damage.Values.Sum()*1.9);Yes(!c.Damage.ContainsKey(DamageType.Heat)&&!c.Damage.ContainsKey(DamageType.Cold));});
        check("물리 모드와 스탠스 물리 보정은 곱연산",()=>{var b=B(null,"Buzz Kill") with{Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=capacity.Default(weapon.Id,"Blind Justice")};var c=StanceEngine.Calculate(catalog,b,stances);Near(c.ComboDamage[DamageType.Slash],178.2*2.2*24);});
        check("퓨리 아케인과 피해 모드는 가산",()=>Near(C(B(new(Warframe1:A("Arcane Fury")),"Pressure Point")).Damage.Values.Sum(),198*(1+1.2+1.8)));
        check("스트라이크 아케인 공속은 퓨리 모드와 가산",()=>Near(C(B(new(Warframe1:A("Arcane Strike")),"Fury")).AttackSpeed,weapon.AttackSpeed*1.9));
        check("어벤저는 최종 치명타 확률에 45%p 가산",()=>Near(C(B(new(Warframe1:A("Arcane Avenger")),"True Steel")).CriticalChance,.28*2.2+.45));
        check("블레이드 차저 0랭크는 피해 50%",()=>Near(C(B(new(Warframe1:A("Arcane Blade Charger",0)))).Damage.Values.Sum(),198*1.5));
        check("미발동 아케인은 스탯 보너스 없음",()=>Near(C(B(new(Warframe1:A("Arcane Fury",5,false)))).ExpectedHit,C(B()).ExpectedHit));
        check("익스포저 랭크/중첩/240% 상한과 원소 독립",()=>{var c=C(B(new(Melee:A("Melee Exposure"),ExposureStacks:4),"Fever Strike","North Wind"));Near(c.Damage[DamageType.Corrosive],198*2.4);Near(c.Damage[DamageType.Viral],198*1.8);Near(C(B(new(Melee:A("Melee Exposure",0),ExposureStacks:24))).Damage[DamageType.Corrosive],198*2.4);});
        check("리탈리에이션 실드/오버실드 200단위 계단",()=>{var c=C(B(new(Melee:A("Melee Retaliation"),Shields:700,Overshields:300)));Near(c.Damage.Values.Sum(),198*(1+.3*3+.15));Near(C(B(new(Melee:A("Melee Retaliation"),Shields:199,Overshields:199))).Damage.Values.Sum(),198);Near(C(B(new(Melee:A("Melee Retaliation"),Shields:100000))).Damage.Values.Sum(),198*5.2);});
        check("도우티 위키 데스트레자 예제 +9.7x",()=>{var w=catalog.Weapons.Single(w=>w.EnglishName=="Destreza Prime");var b=B(null,"Auger Strike","Drifting Contact","Weeping Wounds") with{WeaponId=w.Id,Capacity=capacity.Default(w.Id,null),Scenario=new(Combo:12)};Near(C(b with{Arcanes=new(Melee:A("Melee Doughty"))}).CriticalMultiplier-C(b).CriticalMultiplier,9.7);});
        check("도우티 +50x 상한",()=>Near(ArcaneEngine.DoughtyBonus(new(){{DamageType.Puncture,100}},100,1),50));
        check("듀플리케이트의 노란 치명타 확률",()=>{Near(ArcaneEngine.YellowChance(0),0);Near(ArcaneEngine.YellowChance(.5),.5);Near(ArcaneEngine.YellowChance(1),1);Near(ArcaneEngine.YellowChance(1.5),.5);Near(ArcaneEngine.YellowChance(2),0);});
        check("듀플리케이트 기대 피해는 무조건 2배가 아님",()=>{Near(C(B(new(Melee:A("Melee Duplicate")))).ExpectedHit,275.616*1.28);Near(C(B(new(Melee:A("Melee Duplicate",0)))).ExpectedHit,275.616*1.07);});
        check("듀플리케이트 실제 TTK 추가 타격",()=>{var c=C(B()) with{CriticalChance=1,CriticalMultiplier=2,DuplicateChance=1};var t=DamageEngine.Simulate(c,198*3.5);Near(t.Median!.Value,c.HitInterval);var ordinary=DamageEngine.Simulate(c with{DuplicateChance=0},198*3.5);Near(ordinary.Median!.Value,c.HitInterval*2);});
        check("듀플리케이트 스탠스 TTK 추가 타격",()=>{var b=B() with{Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=capacity.Default(weapon.Id,"Blind Justice")};var c=StanceEngine.Calculate(catalog,b,stances);c=c with{Stats=c.Stats with{CriticalChance=1,CriticalMultiplier=2,DuplicateChance=1}};Near(StanceEngine.Simulate(c,4653*4).Median.Latest!.Value,c.Duration);Near(StanceEngine.Simulate(c with{Stats=c.Stats with{DuplicateChance=0}},4653*4).Median.Latest!.Value,c.Duration*2);});
        check("동결은 최종 치피 +1.0, 캐린은 피해 곱연산",()=>{var frozen=C(B(new(Frozen:true)));Near(frozen.CriticalMultiplier,3.4);Near(C(B(new(Melee:A("Melee Careen"),Frozen:true))).ExpectedHit,frozen.ExpectedHit*2.5);Near(C(B(new(Melee:A("Melee Careen")))).ExpectedHit,C(B()).ExpectedHit);});
        check("동결 상태의 CO는 최소 1종",()=>Near(C(B(new(Frozen:true),"Condition Overload")).Damage.Values.Sum(),198*1.8));
        check("크레센도 초기 콤보와 BR",()=>{var c=C(B(new(Melee:A("Melee Crescendo"),FinisherKills:10),"Blood Rush"));Near(c.CriticalChance,.28*(1+.4*3));});
        check("아케인 슬롯·중복·랭크·유한 조건 검사",()=>{Reject(()=>C(B(new(Melee:A("Arcane Fury")))));Reject(()=>C(B(new(Warframe1:A("Arcane Fury"),Warframe2:A("Arcane Fury")))));Reject(()=>C(B(new(Melee:A("Melee Duplicate",6)))));Reject(()=>C(B(new(Shields:double.NaN,Melee:A("Melee Retaliation")))));Reject(()=>C(B(new(Melee:A("Melee Influence")))));});
        check("버전4 아케인 저장 복원과 구버전 혼합 거부",()=>{var b=B(new(Melee:A("Melee Exposure"),Warframe1:A("Arcane Strike"),ExposureStacks:3));var restored=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Near(C(restored).RawDps,C(b).RawDps);Reject(()=>C(b with{SchemaVersion=3}));Reject(()=>C(b with{Capacity=null}));});
        check("40랭크 무기 수용량",()=>{var w=catalog.Weapons.First(w=>w.MaxRank==40);var setup=capacity.Default(w.Id,null) with{WeaponRank=40};Near(CapacityEngine.Calculate(catalog,B() with{WeaponId=w.Id,Capacity=setup}).Total,80);Reject(()=>CapacityEngine.Calculate(catalog,B() with{WeaponId=w.Id,Capacity=setup with{WeaponRank=41}}));});
        check("이전 빌드의 명시적 데이터 버전 변환",()=>{var b=B() with{SchemaVersion=3,DataVersion=BuildMigration.LegacyVersion};var upgraded=BuildMigration.Upgrade(catalog,b);Yes(upgraded.DataVersion==catalog.Version);Yes(upgraded.Slots.SequenceEqual(b.Slots));Near(C(upgraded).ExpectedHit,C(B()).ExpectedHit);Reject(()=>BuildMigration.Upgrade(catalog,b with{DataVersion="unknown"}));});
        check("일반 검에 엑절티드 블레이드 스탠스 장착 금지",()=>{var w=catalog.Weapons.First(w=>w.WeaponType=="Sword");Yes(stances.ForWeapon(w).All(s=>!s.AbilityOnly));});
        check("벤카 프라임 13콤보와 BR/WW 12배 상한",()=>{var w=catalog.Weapons.Single(w=>w.EnglishName=="Venka Prime");var b=B(null,"Blood Rush","Weeping Wounds") with{WeaponId=w.Id,Capacity=capacity.Default(w.Id,null),Scenario=new(Combo:13)};var c=C(b);Near(c.CriticalChance,w.CriticalChance*5.4);Near(c.StatusChance,w.StatusChance*5.4);Reject(()=>C(b with{Scenario=new(Combo:14)}));});
        check("리치 위키 개별 Stats 표의 랭크 3 / 코스트 7",()=>{var m=M("Reach");Yes(m.MaxRank==3);Near(CapacityEngine.SlotCost(m,3,"None"),7);});
        check("모든 계산 무기의 위키 원문 수치와 정규화 일치",()=>
        {
            foreach(var w in catalog.Weapons)
            {
                var raw=source.Weapons.Single(x=>x.Calculable&&x.InternalId==w.Id).Raw;
                var a=raw.GetProperty("Attacks").EnumerateArray().Single(x=>x.GetProperty("AttackName").GetString()=="Normal Attack");
                Near(w.CriticalChance,a.GetProperty("CritChance").GetDouble());Near(w.CriticalMultiplier,a.GetProperty("CritMultiplier").GetDouble());Near(w.StatusChance,a.GetProperty("StatusChance").GetDouble());Near(w.AttackSpeed,a.GetProperty("FireRate").GetDouble());
                foreach(var p in a.GetProperty("Damage").EnumerateObject())Near(w.Damage[Enum.Parse<DamageType>(p.Name)],p.Value.GetDouble());
                Yes(w.WeaponType==raw.GetProperty("Class").GetString());
            }
        });
        check("1248개 동작의 원문 타격 배열·물리 보정·시간 전수 대조",()=>
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"full-source-records.json")));
            var rawStances=document.RootElement.GetProperty("tables").GetProperty("Module:Stances/data");var count=0;
            foreach(var s in stances.Stances)foreach(var c in s.Combos)
            {
                var raw=rawStances.GetProperty(s.Id);if(!raw.TryGetProperty(c.Key,out var combo))combo=rawStances.GetProperty(s.WeaponType).GetProperty(c.Key);
                if(combo.TryGetProperty("Duration",out var duration))Near(c.Duration!.Value,duration.GetDouble());else Yes(c.Duration is null);
                var i=0;var group=0;
                foreach(var attack in combo.GetProperty("Attacks").EnumerateArray())
                {
                    group++;var damage=attack.GetProperty("Dmg").EnumerateArray().ToArray();var hits=attack.GetProperty("Hits").EnumerateArray().ToArray();
                    for(var j=0;j<damage.Length;j++)
                    {
                        var h=c.Hits[i++];Near(h.Multiplier,damage[j].GetDouble()/100);Near(h.Count,hits[j].GetDouble());Near(h.Group,group);
                        double Modifier(string key)=>attack.TryGetProperty(key,out var values)&&values.ValueKind==JsonValueKind.Array&&values.GetArrayLength()>j?values[j].GetDouble():1;
                        Near(h.Impact,Modifier("ImpactMultiplier"));Near(h.Puncture,Modifier("PunctureMultiplier"));Near(h.Slash,Modifier("SlashMultiplier"));Near(h.StatusMultiplier,Modifier("StatusMultiplier"));
                    }
                }
                Yes(i==c.Hits.Length);count++;
            }
            Yes(count==1248);
        });
        check("아케인 내부 식별자·랭크·출처 유일성",()=>{Yes(source.Arcanes.Select(a=>a.Id).Distinct().Count()==166);Yes(source.Arcanes.All(a=>a.MaxRank>=0&&a.Source.StartsWith("https://wiki.warframe.com/w/")));});
    }
}
