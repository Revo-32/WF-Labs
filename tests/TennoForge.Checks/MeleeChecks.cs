using TennoForge.Core;

internal static class MeleeChecks
{
    internal static void Run(Catalog catalog,Action<string,Action> check)
    {
        void Near(double? actual,double? expected){if(actual is null||expected is null||!double.IsFinite(actual.Value)||!double.IsFinite(expected.Value)||Math.Abs(actual.Value-expected.Value)>1e-8*Math.Max(1,Math.Abs(expected.Value)))throw new Exception($"{actual} != {expected}");}
        void Yes(bool value){if(!value)throw new Exception("조건 불일치");}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("잘못된 입력 허용");}
        var rules=MeleeRuleCatalog.Load();var capacity=CapacityCatalog.Load();
        Build B(string name,params string[] mods)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];
            for(var i=0;i<mods.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==mods[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,slots,new(Enemy:new(10000,0)),catalog.Version,5,Capacity:capacity.Default(w.Id,null) with {StanceEquipped=false},Melee:new());
        }
        Calculation Calc(Build b)=>DamageEngine.Calculate(catalog,b);
        Build Conditions(Build b,params (string Key,double Value)[] values)=>b with {Melee=b.Melee! with {Conditions=values.ToDictionary(p=>p.Key,p=>p.Value)}};
        Build Evo(Build b,int stage,int choice,bool form=false)
        {var i=IncarnonEngine.ForWeapon(b.WeaponId)!;return b with {Melee=b.Melee! with {Incarnon=form,Evolutions=[i.Perks.Where(p=>p.Stage==stage).ElementAt(choice-1).Id]}};}
        ArcaneSlot A(string name,int rank=5)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,rank);
        check("확장 규칙: 일반 공격과 기존 엔진 대조",()=>{
            foreach(var w in catalog.Weapons.Where(w=>ExaltedRules.Find(w.Id) is null))
            {
                var b=B(w.EnglishName);var modern=Calc(b);var old=DamageEngine.Calculate(catalog,b with {SchemaVersion=4,Melee=null});
                if(rules.Weapon(w.Id).Passives.Any(p=>p.Key=="Always")||rules.Weapon(w.Id).Code=="Motovore")continue;
                Near(modern.ExpectedHit,old.ExpectedHit);
            }
        });
        check("피해 브라켓: PP+CO는 가산, 팩션은 별도 곱",()=>{
            var b=B("Nikana Prime","Pressure Point","Condition Overload","Smite Grineer") with {Scenario=new(ExistingStatusTypes:3,Enemy:new(10000,0))};
            b=b with {Melee=b.Melee! with {Faction="Grineer"}};var c=Calc(b);
            Near(c.Melee!.Brackets!.AdditiveStage,198*(1+1.2+2.4));Near(c.Damage.Values.Sum(),198*4.6*1.3);
        });
        check("피해 브라켓: 강공격 피해는 기본 가산, 콤보는 별도 곱",()=>{
            var b=B("Nikana Prime","Pressure Point","Killing Blow") with {Scenario=new(Combo:5,Enemy:new(10000,0)),Melee=new(Attack:"Heavy Attack")};
            var c=Calc(b);Near(c.Damage.Values.Sum(),990*(1+1.2+1.2)*5);
        });
        check("피해 브라켓: 슬램은 CO 제외, 슬램 모드는 별도 곱",()=>{
            var b=B("Nikana Prime","Pressure Point","Seismic Wave","Condition Overload") with {Scenario=new(ExistingStatusTypes:5,Enemy:new(10000,0)),Melee=new(Attack:"Slam Attack")};
            Near(Calc(b).Damage.Values.Sum(),396*2.2*3);
        });
        check("아르카 티트론: 무기 본문의 명시적 브라켓 — PP와 가산, 사이즈믹과 곱",()=>{
            var b=Conditions(B("Arca Titron","Pressure Point","Seismic Wave"),("SlamCapacitor",5));
            b=b with {Melee=b.Melee! with {Attack="Slam Attack"}};
            var c=Calc(b);Near(c.Damage.Values.Sum(),720*(1+1.2+12.5)*(1+2));
            var plain=Calc(b with {Melee=b.Melee! with {Attack="Normal Attack"}});Near(plain.Damage.Values.Sum(),360*2.2);
        });
        check("슬램·슬라이드는 두 번째 이후 대상에도 연속 관통 감쇠 없음",()=>{
            foreach(var attack in new[]{"Slam Attack","Heavy Slam Attack","Slide Attack"})
            {
                var b=B("Nikana Prime") with {Melee=new(Attack:attack)};
                Near(Calc(b).ExpectedHit,Calc(b with {Melee=b.Melee! with {TargetIndex=4}}).ExpectedHit);
            }
            var normal=B("Nikana Prime");Near(Calc(normal with {Melee=new(TargetIndex:3)}).ExpectedHit,Calc(normal).ExpectedHit*.7*.7);
        });
        check("피해 브라켓: 그리거리 원반은 범용 피해와 CO 곱연산",()=>{
            var b=B("Tenet Grigori","Pressure Point","Condition Overload") with {Scenario=new(ExistingStatusTypes:2,Enemy:new(10000,0)),Melee=new(Attack:"Energy Disk"),Arcanes=new(Warframe1:A("Arcane Arachne"))};
            var c=Calc(b);var baseDamage=rules.Weapon(b.WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.GetProperty("AttackName").GetString()=="Energy Disk").GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble());
            Near(c.Damage.Values.Sum(),baseDamage*2.5*2.6);Near(c.Melee!.Brackets!.AdditiveDamage,1.5);
        });
        check("상태 피해: 팩션 이중 적용·물리 모드 분리",()=>{
            var b=B("Nikana Prime","Pressure Point","Smite Grineer","Jagged Edge","Melee Elementalist") with {Melee=new(Faction:"Grineer")};
            var c=Calc(b);var slash=c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash);
            Near(slash.DamagePerTick,198*2.2*1.3*1.3*1.9*.35*(1+.28*1.4));
        });
        check("치확 브라켓: 애니모시티는 강공격 2배 대상 아님",()=>{
            var b=Conditions(B("Nikana Prime","True Steel"),("AnimosityStacks",10)) with {Arcanes=new(Melee:A("Melee Animosity"))};
            b=b with {Melee=b.Melee! with {Attack="Heavy Attack"}};
            Near(Calc(b).CriticalChance,.28*(1+1.2*2+4.2));
        });
        check("인카논: 기본 치확과 모드 치확의 단계 분리",()=>{
            var b=Evo(B("Ceramic Dagger","True Steel"),4,3);Near(Calc(b).CriticalChance,(.1+.3)*2.2);
        });
        check("인카논: 추가 기본 피해에 강공격 고유 배율 중복 금지",()=>{
            var b=Evo(B("Ceramic Dagger"),2,1) with {Scenario=new(Combo:3,Enemy:new(10000,0))};b=b with {Melee=b.Melee! with {Attack="Heavy Attack"}};
            Near(Calc(b).Damage.Values.Sum(),(280+100)*3);
        });
        check("인카논: 세라믹 CO에서 진화 기본 피해 제외",()=>{
            var b=Evo(B("Ceramic Dagger","Condition Overload"),2,1) with {Scenario=new(ExistingStatusTypes:1,Enemy:new(10000,0))};Near(Calc(b).Damage.Values.Sum(),140*1.8+100);
        });
        check("인카논: 진화 단계 중복·다른 무기 ID 거부",()=>{
            var b=B("Ceramic Dagger");var p=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks;
            Reject(()=>Calc(b with {Melee=new(Evolutions:p.Where(p=>p.Stage==2).Select(p=>p.Id).ToArray())}));
            Reject(()=>Calc(B("Nikana Prime") with {Melee=new(Incarnon:true)}));
        });
        check("변신하지 않은 인카논 투사체 거부",()=>Reject(()=>Calc(B("Hate") with {Melee=new(Attack:"Spectral Blade")})));
        check("건블레이드: 강공격 산탄 수 중복 배율 금지",()=>{
            var c=Calc(B("Redeemer Prime") with {Melee=new(Attack:"Heavy Attack")});Near(c.ExpectedHit,3200*(1+.24*1.2));Yes(c.Melee!.Projectiles==10);Near(c.Damage[DamageType.Blast],320);
        });
        check("무기 패시브: 리션 공속은 별도 곱연산",()=>{
            var b=Conditions(B("Lesion","Fury"),("Frenzy",1));var c=Calc(b);var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);Near(c.AttackSpeed,w.AttackSpeed*1.3*1.15);Near(c.StatusChance,w.StatusChance+.25);
        });
        check("부식·화염 방깎, 바이러스는 체력에만 적용",()=>{
            var b=B("Nikana Prime") with {Scenario=new(Enemy:new(10000,2700)),Melee=new(Statuses:new(){[DamageType.Corrosive]=10,[DamageType.Heat]=1,[DamageType.Viral]=10})};
            var c=Calc(b);Near(c.ArmorReduction,DamageEngine.EnemyArmorReduction(270));Near(c.TargetDps,c.RawDps*(1-c.ArmorReduction)*4.25);
            var shield=Calc(b with {Melee=b.Melee! with {Shields=10000}});Near(shield.TargetDps,shield.RawDps);
        });
        check("오버가드 대상 냉기 중첩 상한·동결 배수 제외",()=>{
            var b=B("Nikana Prime") with {Melee=new(Statuses:new(){[DamageType.Cold]=10},Overguard:1000),Arcanes=new(Melee:A("Melee Careen"))};
            Near(Calc(b).CriticalMultiplier,2.4+.25);Near(Calc(b).Melee!.Brackets!.FinalMultiplier,1);
        });
        check("모든 인카논 진화 기본 계산 유한값",()=>{
            foreach(var i in rules.Incarnons)foreach(var p in i.Perks)
            {
                var w=catalog.Weapons.Single(w=>w.Id==i.WeaponId);var b=B(w.EnglishName) with {Melee=new(Incarnon:true,Evolutions:[p.Id])};
                var c=Calc(b);Yes(double.IsFinite(c.ExpectedHit)&&c.ExpectedHit>0);
            }
        });
        check("DPS 표시식: 모든 일반 근접 무기의 실제 엔진 결과와 일치",()=>{
            foreach(var w in catalog.Weapons.Where(w=>ExaltedRules.Find(w.Id) is null)){var c=Calc(B(w.EnglishName));var e=DpsEquation.From(c);Near(e.Evaluate(),c.TargetDps);Near(e.Result,c.TargetDps);}
        });
        check("DPS 표시식:고정 추가량은 확인된 중립 대상에서 일치·미기재 방어층은 거부",()=>{
            var b=Conditions(B("Nikana Prime","Pressure Point","True Steel","Fever Strike","Energy Channel"),("EnergySpent",100));
            var c=Calc(b);var e=DpsEquation.From(c);Near(e.Evaluate(),c.TargetDps);Yes(e.Terms.Any(t=>t.Id=="extra"));
            foreach(var setup in new[]{b.Melee! with {Shields=1000},b.Melee! with {Overguard=1000,Statuses=new(){[DamageType.Magnetic]=10}}})Reject(()=>Calc(b with {Melee=setup}));
            Reject(()=>Calc(b with {Scenario=b.Scenario with {Enemy=new(10000,2700)}}));
        });
        check("DPS 호버: 모드별 실제 영향 단계 구분",()=>{
            var baseline=DpsEquation.From(Calc(B("Nikana Prime")));
            foreach(var pair in new[]{("Pressure Point","damage"),("True Steel","critical"),("Fury","time"),("Fever Strike","types")})
            {var changed=DpsEquation.From(Calc(B("Nikana Prime",pair.Item1))).ChangedTerms(baseline);Yes(changed.SequenceEqual(new[]{pair.Item2}));}
            Yes(DpsEquation.From(Calc(B("Nikana Prime","Melee Elementalist"))).ChangedTerms(baseline).Length==0);
        });
        check("DPS 표시식: 스탠스 타격 합산·단계별 비율의 정확성",()=>{
            var stances=StanceCatalog.Load();var b=B("Nikana Prime","Pressure Point","Jagged Edge","Fever Strike");
            foreach(var key in new[]{"Neutral","Forward","Forward Block","Block"})
            {
                var build=b with {Stance=new("Blind Justice",key,stances.Revision),Capacity=capacity.Default(b.WeaponId,"Blind Justice")};
                var result=StanceEngine.Calculate(catalog,build,stances);var e=DpsEquation.From(result.Stats,result);Near(e.Evaluate(),result.TargetDps);Near(e.Result,result.TargetDps);
            }
        });
        check("에너지 채널: 위키 개별 Stats의 3랭크와 비선형 후보 분리",()=>{
            var b=Conditions(B("Nikana Prime","Energy Channel"),("EnergySpent",100));Near(Calc(b).Melee!.ExtraDamage,200);Yes(catalog.Mods.Single(m=>m.EnglishName=="Energy Channel").MaxRank==3);
        });
        check("어벤징 트루스: 피해 모드 다음에 저장 피해를 더함",()=>{
            var b=Conditions(B("Silva & Aegis Prime","Pressure Point","Avenging Truth"),("BlockedDamage",1000)) with {Melee=new(Attack:"Heavy Attack",Conditions:new(){["BlockedDamage"]=1000})};
            var c=Calc(b);Near(c.Melee!.Brackets!.PostBaseDamage,500);Near(c.Melee.Brackets.ModdedBaseDamage,(c.Melee.Brackets.BaseDamage*2.2+500));
            var slot=(ModSlot?[])b.Slots.Clone();slot[1]=slot[1]! with {Rank=0};Near(Calc(b with {Slots=slot}).Melee!.Brackets!.PostBaseDamage,200);
        });
        check("덱스 니카나: 11배 상한·11콤보 단위",()=>{
            var b=B("Dex Nikana","Corrupt Charge");Near(Calc(b).Melee!.EffectiveCombo,3);Reject(()=>Calc(b with {Scenario=new(Combo:12)}));
        });
        check("팩션 약점과 피해 모드 브라켓은 서로 다른 단계",()=>{
            var b=B("Nikana Prime","Smite Grineer") with {Melee=new(Faction:"Grineer")};var c=Calc(b);
            Near(c.Melee!.FactionMultiplier,1.3);Near(c.Melee.TargetDamage[DamageType.Impact],c.Damage[DamageType.Impact]*1.5);Near(c.Melee.TargetDamage[DamageType.Slash],c.Damage[DamageType.Slash]);
            var kuva=Calc(b with {Melee=b.Melee with {Faction="KuvaGrineer"}});Near(kuva.Melee!.FactionMultiplier,1.3);
            var narmer=Calc(b with {Melee=b.Melee with {Faction="Narmer"}});Near(narmer.Melee!.FactionMultiplier,1);
            var guard=Calc(b with {Melee=b.Melee with {Overguard=10000}});Near(guard.Melee!.TargetDamage[DamageType.Impact],guard.Damage[DamageType.Impact]);
        });
        check("팩션 표: 데이모스·속삭임 바이러스 저항과 테크병 냉기 저항",()=>{
            var p=rules.EnemyProfiles;Near(p.Single(p=>p.Id=="InfestedDeimos").Multiplier(DamageType.Viral),.5);Near(p.Single(p=>p.Id=="TheMurmur").Multiplier(DamageType.Viral),.5);Near(p.Single(p=>p.Id=="Techrot").Multiplier(DamageType.Cold),.5);Near(p.Single(p=>p.Id=="Corpus").Multiplier(DamageType.Puncture),1.5);
        });
        check("파라세시스: 30 이후 랭크당 센티언트 팩션 피해",()=>{
            var b=B("Paracesis") with {Melee=new(Faction:"Sentient")};Near(Calc(b).Melee!.FactionMultiplier,1);b=b with {Capacity=b.Capacity! with {WeaponRank=40}};Near(Calc(b).Melee!.FactionMultiplier,1.6);
        });
        check("상태 틱: 전기·가스 종료 시점의 무피해 틱 제외",()=>{
            var c=Calc(B("Nikana Prime","Molten Impact","Fever Strike","Shocking Touch"));
            foreach(var s in c.Melee!.StatusDamage.Where(s=>s.Type!=DamageType.Blast))Near(s.Ticks,6);
            var lasting=Calc(B("Nikana Prime","Lasting Sting"));
            Near(lasting.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Electricity).Ticks,13);
            Near(lasting.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).Ticks,12);
        });
        check("슬레이트라: 베기만 100% 지속, 모드와 가산",()=>{
            var c=Calc(B("Slaytra","Lasting Sting"));Near(c.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Slash).Duration,18.6);Near(c.Melee.StatusDamage.Single(s=>s.Type==DamageType.Heat).Duration,12.6);
        });
        check("톤카트: 오버히트 화염은 다른 원소와 조합되지 않음",()=>{
            var c=Calc(Conditions(B("Tonkkatt","Fever Strike","North Wind"),("Overheat",30)));Near(c.Damage[DamageType.Heat],c.Melee!.ModdedBaseDamage*1.2);Near(c.Damage[DamageType.Viral],c.Melee.ModdedBaseDamage*1.8);Yes(!c.Damage.ContainsKey(DamageType.Gas));
        });
        check("테코 프라임: 시그니처는 기본 상태 확률 브라켓",()=>{
            var c=Calc(Conditions(B("Tekko Prime","Melee Prowess"),("Signature",1)));Near(c.StatusChance,(.26+.06)*1.9);
        });
        check("바스틸락: 9산탄·물리 25%·회복은 명중 산탄별",()=>{
            var b=B("Vastilok","Healing Return") with {Scenario=new(ExistingStatusTypes:2),Melee=new(Attack:"Ranged Attack")};var c=Calc(b);Near(c.Damage.Values.Sum(),69*1.25);Near(c.ExpectedHit,69*1.25*9*(1+.19*.7));Near(c.Melee!.Healing,11*2*9);
            var heavy=Calc(b with {Melee=new(Attack:"Heavy Attack")});Near(heavy.Damage.Values.Sum()*heavy.Melee!.Projectiles,2484*1.25);
        });
        check("조리스: 콤보 지속 시간 무한, 지속 감소 모드 무효",()=>Yes(double.IsPositiveInfinity(Calc(B("Xoris","Corrupt Charge")).Melee!.ComboDuration)));
        check("니카나 강공격: 무작위 상태와 강제 베기를 별도 합산",()=>{
            var c=Calc(B("Nikana Prime") with {Melee=new(Attack:"Heavy Attack")});Yes(c.Melee!.ForcedProcs.Contains("Slash"));Near(c.Melee.StatusDamage.Single(s=>s.Type==DamageType.Slash).Chance,1+.28*.9);
        });
        check("코버트 리썰리티: 단검 외 장착 거부",()=>{Reject(()=>Calc(B("Nikana Prime","Covert Lethality")));Yes(Calc(B("Ceramic Dagger","Covert Lethality")).Melee!.InitialCombo==16);});
        check("상태 중첩 입력: 충격 5·보이드 1 상한·마무리 상태 거부",()=>{
            foreach(var type in new[]{DamageType.Impact,DamageType.Void,DamageType.True})Reject(()=>Calc(B("Nikana Prime") with {Melee=new(Statuses:new(){[type]=6})}));
        });
        check("베나토 프라임: 타우 유발 상태에서 방사능 제외",()=>{
            var c=Calc(B("Venato Prime","Molten Impact","Shocking Touch"));var effect=MeleeSecondaryEngine.Describe(c).Single(x=>x.Name.Contains("타우"));Near(effect.ExtraStacks!.Value,c.StatusChance*(1-c.Damage[DamageType.Radiation]/c.Damage.Values.Sum()));
        });
        check("에너지 채널:산탄 전체 중1회·반복 TTK도 시작 충전만 소비",()=>{
            var b=Conditions(B("Vastilok","Energy Channel"),("EnergySpent",100));b=b with {Melee=b.Melee! with {Attack="Ranged Attack"}};var c=Calc(b);Near(c.ExpectedHit,c.Damage.Values.Sum()*(1+c.CriticalChance*(c.CriticalMultiplier-1))*9+200);Yes(DamageEngine.Simulate(c,10000,16).KillFraction>0);Yes(DpsEquation.From(c).Limitations!.Length>0);
        });
        check("지원 범위: 시간에 반영하는 효과와 미연결 효과를 별도 표시",()=>{
            var c=Calc(B("Nikana Prime","Shattering Impact"));Yes(c.Limitations.Any(x=>x.StartsWith("전투 사건:")&&x.Contains("섀터링")));Yes(!c.Limitations.Any(x=>x.StartsWith("미반영:")&&x.Contains("섀터링")));
            var unresolved=MeleeEffectCoverage.Notes(new Dictionary<string,double>{{"StaticDischarge",1}});Yes(unresolved.Single().StartsWith("미반영:"));
        });
        check("차트 계산: 실드·약점·바이러스·부식까지 DPS와 일치",()=>{
            foreach(var setup in new[]{new MeleeSetup(Faction:"Grineer",Statuses:new(){[DamageType.Viral]=10,[DamageType.Corrosive]=10}),new MeleeSetup(Faction:"Corpus",Shields:1000),new MeleeSetup(Overguard:1000)})
            {
                var b=B("Nikana Prime","Fever Strike") with {Melee=setup,Scenario=new(Enemy:new(10000,675))};var c=Calc(b);Near(MeleeTargetModel.ExpectedHitAtArmor(c,675),c.TargetDps*c.HitInterval);
                foreach(var a in new[]{0d,2700d})Near(MeleeTargetModel.ExpectedHitAtArmor(c,a),Calc(b with {Scenario=b.Scenario with {Enemy=new(10000,a)}}).TargetDps*c.HitInterval);
            }
        });
        check("빌드 규칙 버전 고정·구형 슬롯/아케인 보존 변환",()=>{
            var old=B("Nikana Prime","Pressure Point") with {SchemaVersion=4,Melee=null,Arcanes=new(Warframe1:A("Arcane Fury"))};var upgraded=BuildMigration.ToMelee(catalog,old);
            Yes(upgraded.SchemaVersion==5&&upgraded.Slots.SequenceEqual(old.Slots)&&upgraded.Arcanes==old.Arcanes&&upgraded.Capacity==old.Capacity);Near(Calc(upgraded).Melee!.Brackets!.AdditiveDamage,3);
            Reject(()=>Calc(upgraded with {Melee=upgraded.Melee! with {RulesVersion="unknown"}}));
        });
        check("고유 기믹 미완료 무기는 지원 범위에 드러나며 연결된 효과와 구분",()=>{
            var tatsu=B("Tatsu") with {Melee=new(Attack:"Slide Attack")};Yes(DpsEquation.From(Calc(tatsu)).Limitations!.Any(s=>s.Contains("소울 스웜")));
            Yes(!DpsEquation.From(Calc(B("Harmony"))).Limitations!.Any(s=>s.Contains("미반영: 강공격의 기존 상태 피해 소모")));
        });
        check("워 슬라이드: 누락 개별 통계 대신 스탠스 2배·슬라이드 치확 적용",()=>{
            var b=B("War","Maiming Strike");var source=StanceCatalog.Load();var c=StanceEngine.Calculate(catalog,b with {Stance=new("Heavy Blade","Slide",source.Revision)},source);
            Near(c.ComboDamage.Values.Sum(),250*2);Near(c.Hits[0].Calculation!.CriticalChance,.26*2.5);
        });
    }
}
