using TennoForge.Core;
using System.Text.Json;

internal static class CombatTimelineChecks
{
    private sealed class InlineProgress(Action<SimulationProgress> action):IProgress<SimulationProgress>
    { public void Report(SimulationProgress value)=>action(value); }
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("확장 전투 검증 실패");}
        void Near(double? a,double? b,double tolerance=1e-8){if(a is null||b is null||!double.IsFinite(a.Value)||!double.IsFinite(b.Value)||Math.Abs(a.Value-b.Value)>tolerance*Math.Max(1,Math.Abs(b.Value)))throw new Exception($"{a} != {b}");}
        Build B(string name="Nikana Prime",params string[] mods)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];
            for(var i=0;i<mods.Length;i++){var mod=catalog.Mods.Single(m=>m.EnglishName==mods[i]);slots[i]=new(mod.Id,mod.MaxRank);}
            return new(w.Id,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        }
        CombatTimelineResult C(Build b,int count=16){var result=CombatTimeline.Calculate(catalog,b,stances,count);if(result.Unsupported.Length>0)throw new Exception(string.Join(" / ",result.Unsupported));return result;}
        Build Ruvox(string perk,params string[] mods)
        {
            var b=B("Ruvox",mods);var p=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Name==perk);
            return b with {Melee=new(Incarnon:true,Evolutions:[p.Id])};
        }
        check("루복스 영구 천공: 꿰뚫기 중 관통5가 치확+25%와 컨오1종으로 반영",()=>{
            var b=Ruvox("Permanent Perforation","Condition Overload");var plain=MeleeEngine.Calculate(catalog,b);
            var impaled=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Impaled"]=1}}});
            Near(impaled.Melee!.Setup.Statuses![DamageType.Puncture],5);Near(impaled.CriticalChance-plain.CriticalChance,.25);Near(impaled.Damage.Values.Sum()/plain.Damage.Values.Sum(),1.8);
            var guard=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Impaled"]=1},Overguard=1000}});Near(guard.CriticalChance,plain.CriticalChance);Yes(!guard.Limitations.Any(s=>s.Contains("강제 관통 중첩")));
        });
        check("루복스 취약성 혈청: 상태 확률35%는 가산 고정치, 오버가드 대상 제외",()=>{
            var b=Ruvox("Vulnerability Serum","Melee Prowess");var normal=MeleeEngine.Calculate(catalog,b);
            var affected=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Impaled"]=1}}});Near(affected.StatusChance-normal.StatusChance,.35);
            var guard=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Impaled"]=1},Overguard=1000}});Near(guard.StatusChance,normal.StatusChance);
        });
        check("프라이머 첫 타격: 기존 꿰뚫림·상태·익스포저 발동을 시작 수치에 유지",()=>{
            var b=Ruvox("Permanent Perforation","Condition Overload");var exposure=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Exposure");
            b=b with {Melee=b.Melee! with {Conditions=new(){["Impaled"]=1},Statuses=new(){[DamageType.Radiation]=1}},Arcanes=new(Melee:new(exposure.Id,5),ExposureStacks:4),Encounter=new(EncounterMode.Primer)};
            Near(C(b,1).Points[0].StartDps,MeleeEngine.Calculate(catalog,b).TargetDps);
            b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Heat]=1}},Arcanes=b.Arcanes! with {Frozen=true},Scenario=b.Scenario with {Enemy=new(1e9,1000)}};
            Near(C(b,1).Points[0].StartDps,MeleeEngine.Calculate(catalog,b).TargetDps);
        });
        check("루복스 시간 전투: 소모 전 콤보로 가시 지속, 래스팅 스팅 지속 증가 제외",()=>{
            foreach(var combo in new[]{1,4,12})
            {
                var b=Ruvox("Permanent Perforation","Lasting Sting");b=b with {Melee=b.Melee! with {Attack="Heavy Slam Attack"},Scenario=new(Combo:combo,BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds)};
                var r=C(b,1);Near(r.Events.Single(e=>e.Kind=="impale").Amount,1.5+combo-1);Yes(r.Points[0].StatusTypes>=1);Yes(r.Points[^1].Combo==1);
                if(combo==1)Near(r.Points.Single(p=>p.Time==2).StatusTypes,0);
            }
        });
        check("루복스 가시 범위: 고정8m·변신/오버가드/기존래그돌 조건",()=>{
            var b=Ruvox("Permanent Perforation");b=b with {Melee=b.Melee! with {Attack="Heavy Slam Attack"},Scenario=new(Combo:4,BaseInterval:20,Enemy:new(1e9,0))};
            Yes(C(b with {Melee=b.Melee! with {Distance=8}},1).Events.Any(e=>e.Kind=="impale"));
            foreach(var melee in new[]{b.Melee! with {Distance=8.01},b.Melee! with {Incarnon=false},b.Melee! with {Overguard=1e9},b.Melee! with {Conditions=new(){["Ragdoll"]=1}}})Yes(!C(b with {Melee=melee},1).Events.Any(e=>e.Kind=="impale"));
        });
        check("워프레임 실드: 일반→오버실드 한도·기존1 실드 요건·실드 없는 프레임",()=>{
            var p=new PlayerShieldState(300,1200,1,0,false);Near(p.Restore(500,true,true),500);Near(p.Shields,300);Near(p.Overshields,201);
            Near(p.Restore(100000,true,true),999);Near(p.Overshields,1200);p.Break();Near(p.Restore(100,true,true),0);
            Near(p.Restore(90,false),90);Near(p.Restore(10000,false),210);Near(p.Overshields,0);
            var none=new PlayerShieldState(0,1200,0,0);Near(none.Restore(10000,true),0);
        });
        check("워프레임 실드: 연속시간 재생·전소4초·회복으로 대기 단축 금지",()=>{
            var p=new PlayerShieldState(1000,1200,100,0,rechargeBonus:.9,delayRemaining:1);
            p.Advance(.5);Near(p.Shields,100);p.Advance(3);Near(p.Shields,100+2*65*1.9);
            p.Break();p.Restore(300,false);p.Advance(7);Near(p.Shields,300);p.Advance(7.5);Near(p.Shields,300+.5*65*1.9);
            p.IncomingHit();p.Advance(8.5);Near(p.Shields,300+.5*65*1.9);p.Advance(9);Near(p.Shields,300+65*1.9);
            var fast=new PlayerShieldState(1000,1200,0,0,delayReduction:.8);fast.Break();fast.Advance(1);Near(fast.Shields,13);
            var rounded=new PlayerShieldState(1000,1200,0,0);rounded.Advance(.2+.4);rounded.Advance(.6);Near(rounded.Shields,39);
        });
        check("락타 다크 대거: 방사능 후 직접/상태 회복, 실드0에서는 시작 불가",()=>{
            var b=B("Rakta Dark Dagger","Weeping Wounds","Fever Strike") with {Scenario=new(Combo:12,BaseInterval:.3,Enemy:new(1e9,0)),Arcanes=new(Shields:1,MaximumShields:1000000,ShieldRechargeEnabled:false)};
            var r=C(b,1);var restores=r.Events.Where(e=>e.Kind=="raktaShield").ToArray();Yes(restores.Length>0);
            Near(r.Points[^1].PlayerShields,1+restores.Sum(e=>e.Amount));
            var firstRadiation=Array.FindIndex(r.Events,e=>e.Kind=="proc"&&e.Description.Contains("방사능"));var firstRestore=Array.FindIndex(r.Events,e=>e.Kind=="raktaShield");Yes(firstRadiation>=0&&firstRestore>firstRadiation);
            Yes(r.Events.Select((e,i)=>(e,i)).Any(x=>x.e.Kind=="tick"&&x.i>0&&r.Events[x.i-1].Kind=="raktaShield"&&Math.Abs(r.Events[x.i-1].Amount-x.e.Amount*.05)<1e-8));
            var zero=C(b with {Arcanes=b.Arcanes! with {Shields=0}},1);Near(zero.Points[^1].PlayerShields,0);Yes(!zero.Events.Any(e=>e.Kind=="raktaShield"));
        });
        check("락타 다크 대거: 회복 실드가 리탈리에이션 다음 타격 가산 브라켓으로 연결",()=>{
            var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Retaliation");
            var b=B("Rakta Dark Dagger","Weeping Wounds") with {Scenario=new(Combo:12,BaseInterval:.2,Enemy:new(1e9,0)),Arcanes=new(Melee:new(arcane.Id,5),Shields:199,MaximumShields:1000,ShieldRechargeEnabled:false)};
            var r=C(b,1);var plain=C(b with {Arcanes=b.Arcanes! with {Melee=null}},1);var last=r.Points[^1];Yes(last.PlayerShields>200);Yes(r.DirectDamage>plain.DirectDamage);
            var bracket=1+Math.Min(4.2,.3*(Math.Floor(last.PlayerShields!.Value/200)+.5*Math.Floor(last.PlayerOvershields!.Value/200)));
            Near(last.SnapshotDps/ plain.Points[^1].SnapshotDps,bracket);
            var unknown=CombatTimeline.Calculate(catalog,b with {Arcanes=b.Arcanes! with {MaximumShields=null}},stances,1);Yes(unknown.Unsupported.Any(s=>s.Contains("최대 실드")));
        });
        check("어시밀레이션: 실드 파괴 후 직접 강공격 처치에서 실제 최대실드30% 회복",()=>{
            var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Assimilation");
            var b=B() with {Melee=new(Attack:"Heavy Attack"),Scenario=new(Enemy:new(1,0)),Arcanes=new(Melee:new(arcane.Id,5),Shields:200,MaximumShields:1000,ShieldRechargeEnabled:false),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"PlayerShieldBreak")])};
            var r=C(b,1);Near(r.Points[^1].PlayerShields,300);Near(r.Points[^1].PlayerOvershields,0);Near(r.Events.Single(e=>e.Kind=="playerShieldRestore").Amount,300);
        });
        check("락타 다크 대거: 오버가드 피해도 회복, 어시밀레이션이 실드0을 먼저 복구",()=>{
            var b=B("Rakta Dark Dagger") with {Melee=new(Overguard:10000,Statuses:new(){[DamageType.Radiation]=1}),Arcanes=new(Shields:1,MaximumShields:1000,ShieldRechargeEnabled:false),Encounter=new(EncounterMode.Primer)};
            var og=C(b,1);Near(og.Points[0].PlayerShields,1+.05*og.Events.Single(e=>e.Kind=="hit").Amount);Near(og.Points[0].Health,1e9);Yes(og.Points[0].Overguard<10000);
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Assimilation");
            var killed=C(b with {Melee=b.Melee! with {Attack="Heavy Attack",Overguard=0},Scenario=new(Enemy:new(1,0)),Arcanes=new(Melee:new(a.Id,5),MaximumShields:1000,ShieldRechargeEnabled:false),Encounter=new(EncounterMode.Primer,Inputs:[new(0,"PlayerShieldBreak")])},1);
            Near(killed.Points[0].PlayerShields,300+.05*killed.Events.Single(e=>e.Kind=="hit").Amount);Yes(killed.Events.ToList().FindIndex(e=>e.Kind=="playerShieldRestore")<killed.Events.ToList().FindIndex(e=>e.Kind=="raktaShield"));
        });
        Build Contagion(params string[] mods)
        {
            var id=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
            return B("Nikana Prime",mods) with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Melee=new(Exodia:new(arcane.Id,3))};
        }
        check("컨테이전: 직격 IPS 각1/3·부착 폭발1배·폭발 바이러스5배·스탠스 별도",()=>{
            var b=Contagion();var damage=catalog.Weapons.Single(w=>w.Id==b.WeaponId).Damage.Values.Sum();var c=ExodiaContagionMechanic.Calculate(catalog,b,0);
            Near(c.Contact.Damage.Values.Sum(),damage*2);foreach(var t in new[]{DamageType.Impact,DamageType.Puncture,DamageType.Slash})Near(c.Contact.Damage[t],damage*2/3);
            Near(c.EmbeddedTick.Damage[DamageType.Blast],damage);Near(c.Explosion.Damage[DamageType.Viral],damage*5);Near(c.TravelMultiplier,1);
            Yes(c.EmbeddedTick.ForcedProcs.Contains("Impact")&&c.Explosion.ForcedProcs.Contains("Impact")&&!c.Explosion.ForcedProcs.Contains("Ragdoll"));
            var stance=ExodiaContagionMechanic.Calculate(catalog,b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")},0);
            Near(stance.Explosion.Damage[DamageType.Viral],damage*15);Near(stance.Contact.ExpectedDamage,c.Contact.ExpectedDamage);Near(stance.EmbeddedTick.ExpectedDamage,c.EmbeddedTick.ExpectedDamage);
        });
        check("컨테이전: 30m 문턱·전 랭크 콤보 배수와 강공격 배수 분리",()=>{
            var b=Contagion() with {Scenario=new(Combo:12,Enemy:new(1e9,0))};
            for(var rank=0;rank<=3;rank++)
            {
                var input=b with {Melee=b.Melee! with {Exodia=b.Melee.Exodia! with {Rank=rank}}};
                var below=ExodiaContagionMechanic.Calculate(catalog,input,29.999);var over=ExodiaContagionMechanic.Calculate(catalog,input,30);
                Near(below.TravelMultiplier,1);Near(over.TravelMultiplier,1+rank+11d/4);Near(over.Contact.ExpectedDamage/below.Contact.ExpectedDamage,1+rank+11d/4);Yes(over.Explosion.ForcedProcs.Contains("Ragdoll"));
                Near(over.Contact.Brackets.ComboMultiplier,1);
            }
        });
        check("컨테이전: 직격만 CO 곱연산, 아라크네와 PP는 별도 곱·임베드/폭발 CO 제외",()=>{
            var b=Contagion("Pressure Point","Condition Overload");var arachne=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne");
            b=b with {Arcanes=new(Warframe1:new(arachne.Id,5)),Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=1,[DamageType.Corrosive]=1,[DamageType.Toxin]=1}}};
            var c=ExodiaContagionMechanic.Calculate(catalog,b,0);var damage=catalog.Weapons.Single(w=>w.Id==b.WeaponId).Damage.Values.Sum();
            Near(c.Contact.Damage.Values.Sum(),damage*2*2.2*(1+.8*3)*2.5);Near(c.Contact.Brackets.AdditiveConditionOverload,0);Near(c.Contact.Brackets.ConditionMultiplier,3.4);
            Near(c.EmbeddedTick.Damage.Values.Sum(),damage*2.2*2.5);Near(c.Explosion.Damage.Values.Sum(),damage*5*2.2*2.5);
        });
        check("컨테이전: 명중 시 강공격 동작의 피해/치확·부착 틱 제외·콤보 배수 제외",()=>{
            var b=Contagion("Killing Blow","True Steel","Pressure Point");
            var normal=ExodiaContagionMechanic.Calculate(catalog,b,0);
            var heavy=ExodiaContagionMechanic.Calculate(catalog,b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Conditions=new(){["HeavyAnimation"]=1}}},0);
            var weapon=catalog.Weapons.Single(w=>w.Id==b.WeaponId);
            Near(normal.Contact.CriticalChance,weapon.CriticalChance*2.2);Near(heavy.Contact.CriticalChance,weapon.CriticalChance*3.4);
            Near(heavy.Explosion.CriticalChance,weapon.CriticalChance*3.4);Near(heavy.EmbeddedTick.CriticalChance,normal.EmbeddedTick.CriticalChance);
            Near(heavy.Contact.Damage.Values.Sum()/normal.Contact.Damage.Values.Sum(),3.4/2.2);
            Near(heavy.Explosion.Damage.Values.Sum()/normal.Explosion.Damage.Values.Sum(),3.4/2.2);
            Near(heavy.EmbeddedTick.ExpectedDamage,normal.EmbeddedTick.ExpectedDamage);Near(heavy.Contact.Brackets.ComboMultiplier,1);Near(heavy.Explosion.Brackets.ComboMultiplier,1);
        });
        check("컨테이전: 위키 스탠스 배율·미기재 스탠스 거부, 일반 근접 피해 불변",()=>{
            foreach(var (type,id,expected) in new[]{("Hammer",(string?)null,2d),("Heavy Blade",null,3d),("Staff","Flailing Branch",3d),("Machete","Sundering Weave",2d),("Nikana","Blind Justice",3d),("Sword","Iron Phoenix",3d)})Near(ExodiaContagionMechanic.ExplosionStanceMultiplier(type,id),expected);
            var rejected=false;try{ExodiaContagionMechanic.ExplosionStanceMultiplier("Nikana","없는 스탠스");}catch(CombatRuleUnavailableException){rejected=true;}Yes(rejected);
            var b=Contagion("Pressure Point");Near(MeleeEngine.Calculate(catalog,b).ExpectedHit,MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Exodia=null}}).ExpectedHit);
        });
        check("스피네렉스: 별도 폭발 기본 480 독성·강제 독성 1중첩·5초 표식",()=>{
            var e=SpinnerexMechanic.Calculate(catalog,B("Spinnerex"),3);
            Near(e.TotalDamage,480);Near(e.Damage[DamageType.Toxin],480);Near(e.Toxin.DamagePerTick,240);Near(e.Toxin.Chance,1);Near(e.Chance,.3);Near(e.Radius,10);
            Near(SpinnerexMechanic.Chance(15,0,4.999),1);Near(SpinnerexMechanic.Chance(15,0,5),0);Near(SpinnerexMechanic.Chance(5,double.NegativeInfinity,1),0);
            Near(SpinnerexMechanic.Chance(0,0,0),0);Near(SpinnerexMechanic.Chance(5,2,1),0);
        });
        check("스피네렉스: 피해·원소·팩션 브라켓 적용, 컨오·강공격·치명타 제외",()=>{
            var b=B("Spinnerex","Pressure Point","Fever Strike","Smite Grineer","Condition Overload","Blood Rush","Organ Shatter","Killing Blow","Melee Prowess");
            b=b with {Scenario=new(Combo:12,MotionMultiplier:4,Enemy:new(1e9,2700)),Melee=new(Attack:"Heavy Attack",Tennokai:true,Faction:"Grineer",TargetIndex:4,Statuses:new(){[DamageType.Viral]=10,[DamageType.Puncture]=5,[DamageType.Cold]=10,[DamageType.Toxin]=3})};
            var e=SpinnerexMechanic.Calculate(catalog,b,3);
            Near(e.TotalDamage,480*2.2*1.9*1.3);Near(e.Toxin.DamagePerTick,480*2.2*1.3*1.3*.5*1.9);Near(e.Toxin.Chance,1);
            var plain=SpinnerexMechanic.Calculate(catalog,b with {Scenario=b.Scenario with {Combo=1,MotionMultiplier=1},Melee=b.Melee with {Attack="Normal Attack",Tennokai=false,TargetIndex=1,Statuses=[]}},3);
            Near(e.TotalDamage,plain.TotalDamage);Near(e.Toxin.DamagePerTick,plain.Toxin.DamagePerTick);
        });
        check("스피네렉스: 복합 원소 순서는 공유 엔진과 일치하고 강제 독성은 유지",()=>{
            var b=B("Spinnerex","Fever Strike","Vicious Frost","Shocking Touch","Melee Elementalist");
            var e=SpinnerexMechanic.Calculate(catalog,b,10);var direct=MeleeEngine.Calculate(catalog,b);
            foreach(var (type,amount) in direct.Damage)Near(e.Damage.GetValueOrDefault(type),amount*480/168);
            Near(e.Damage.GetValueOrDefault(DamageType.Toxin),0);Near(e.Toxin.Chance,1);Near(e.Toxin.DamagePerTick,480*.5*1.9*1.9);
        });
        check("스피네렉스: 생존 적에게는 처치 폭발 없음, 첫 타 즉사에도 신규 독성 소급 없음",()=>{
            var alive=C(B("Spinnerex"),8);Yes(alive.DirectDamage>0);Near(alive.SecondaryBursts!.Single().ExpectedActivations,0);
            var killed=C(B("Spinnerex") with {Scenario=new(Enemy:new(1,0))},8);
            Near(killed.DirectDamage+killed.StatusDamage,1);Near(killed.SecondaryBursts!.Single().ExpectedActivations,0);
        });
        check("스피네렉스: 처치 직전 독성 중첩 확률은 주변 결과로만 집계",()=>{
            var b=B("Spinnerex","Weeping Wounds") with {Scenario=new(Combo:12,BaseInterval:2,Enemy:new(1000,0))};
            var r=C(b,128);var burst=r.SecondaryBursts!.Single();
            Yes(burst.ExpectedActivations>0&&burst.ExpectedActivations<=1);Near(burst.ExpectedCenterDamage,480*burst.ExpectedActivations);Near(burst.ExpectedToxinTick,240*burst.ExpectedActivations);
            Yes(r.Ttk.KillFraction>0);Yes(r.DirectDamage+r.StatusDamage<=1000+1e-8);
            Yes(r.Events.Any(e=>e.Kind=="secondaryOpportunity"));
        });
        check("표본 오차: 평균·표본 표준편차·표준오차 손계산 및 큰 값 안정성",()=>{
            foreach(var baseline in new[]{0d,1e12})
            {
                var moments=new SampleMoments();foreach(var value in new[]{1,2,3})moments.Add(baseline+value);
                var e=moments.Estimate();Near(e.Samples,3);Near(e.Mean,baseline+2);Near(e.StandardDeviation,1);Near(e.StandardError,1/Math.Sqrt(3));Near(e.Approximate95HalfWidth,1.959963984540054/Math.Sqrt(3));
            }
            var single=new SampleMoments();single.Add(5);Yes(single.Estimate().StandardError is null);
        });
        check("시간 전투 표본 오차: 직접+상태의 공동 표본에서 추정, 처치 상한 보존",()=>{
            var c=C(B(),32);Near(c.DamageEstimate!.Samples,32);Near(c.DamageEstimate.Mean,c.DirectDamage+c.StatusDamage);Yes(c.DamageEstimate.StandardError>0);
            var killed=C(B() with {Scenario=new(Enemy:new(1,0))},32);Near(killed.DamageEstimate!.Mean,1);Near(killed.DamageEstimate.StandardError,0);
        });
        check("실사용 기본 정밀도: 2048개 전투 표본을 완료하고 공동 피해 평균·표준오차 반환",()=>{
            var b=B("Nikana Prime","Condition Overload") with {Scenario=new(BaseInterval:2,Enemy:new(1e9,0))};
            var c=EncounterEngine.Calculate(catalog,b,stances);Yes(c.Unsupported.Length==0);Near(c.Trials,2048);Near(c.Timeline!.Trials,2048);
            Near(c.Damage.Low,c.Timeline.DirectDamage+c.Timeline.StatusDamage);Near(c.Precision!.FirstScenario.Mean,c.Damage.Low);Yes(c.Precision.FirstScenario.StandardError>0);
        });
        check("전투 계산: 저장 표본 수 사용·진행률 보고·작업 중 취소",()=>{
            var b=B() with {Encounter=new(EncounterMode.FiveSeconds,Samples:32)};
            var changes=new List<SimulationProgress>();var c=EncounterEngine.Calculate(catalog,b,stances,progress:new InlineProgress(changes.Add));
            Near(c.Trials,32);Near(c.Precision!.FirstScenario.Samples,32);Yes(changes.First().Completed==0&&changes.Last().Completed==32&&changes.All(p=>p.Total==32));
            using var cancel=new CancellationTokenSource();var stopped=false;
            try{EncounterEngine.Calculate(catalog,b,stances,cancellation:cancel.Token,progress:new InlineProgress(p=>{if(p.Completed==16)cancel.Cancel();}));}
            catch(OperationCanceledException){stopped=true;}
            Yes(stopped);
        });
        check("콤보: 강공격 160점의 40% 유지와 초기 콤보 초당 40 회복",()=>{
            var counter=new MeleeComboCounter(160,9,20,12,5);
            Near(counter.SpendHeavy(0,.4,false),96);Near(counter.Points,64);Near(counter.Multiplier,4);
            counter.Advance(1);Near(counter.Points,104);Near(counter.Multiplier,6);
            counter.Advance(2.4);Near(counter.Points,160);Near(counter.Multiplier,9);
            counter.Advance(4);Near(counter.Points,160);
        });
        check("콤보: 명중 없이 시간이 흐를 때 만료·초기값 복귀",()=>{
            var counter=new MeleeComboCounter(30,12,20,12,5);
            counter.Advance(4.99);Near(counter.Points,220);counter.Advance(5);Near(counter.Points,30);
            counter.Gain(6,20);Near(counter.Points,50);counter.Advance(11);Near(counter.Points,30);
        });
        check("콤보: 상한에서 명중해도 유지 시간 갱신, 무한 지속은 만료 없음",()=>{
            var counter=new MeleeComboCounter(0,12,20,12,5);
            Near(counter.Gain(4,100),0);counter.Advance(8);Near(counter.Points,220);counter.Advance(9);Near(counter.Points,0);
            var infinite=new MeleeComboCounter(0,12,20,12,double.PositiveInfinity);infinite.Advance(10000);Near(infinite.Points,220);
        });
        check("콤보: 텐노카이 비용 0 및 벤카·덱스 니카나 상한",()=>{
            var venka=new MeleeComboCounter(0,13,20,13,5);Near(venka.SpendHeavy(0,0,true),0);Near(venka.Points,240);
            var dex=new MeleeComboCounter(0,1,11,11,5);dex.Gain(0,999);Near(dex.Points,110);Near(dex.Multiplier,11);
            var disabled=new MeleeComboCounter(0,1,20,12,0);disabled.Gain(0,50);Near(disabled.Points,0);
        });
        check("강공격: 단검의 한 입력 안에서 두 타격에 같은 콤보, 입력마다 한 번 소모",()=>{
            var b=B("Ceramic Dagger") with {Stance=new("Dagger","Heavy",stances.Revision),Melee=new(ComboDurationOverride:2),Scenario=new(Combo:4,Enemy:new(1e9,0)),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,.1,.5,.6])};
            b=b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Dagger")};
            var c=C(b,1);var spends=c.Events.Where(e=>e.Kind=="heavyCombo").ToArray();Yes(spends.Length==2);
            Near(spends[0].Time,.1/catalog.Weapons.Single(w=>w.Id==b.WeaponId).AttackSpeed);
            Near(spends[0].Amount,60);Near(spends[1].Amount,0);
            Near(c.Points[0].ComboPoints,60);Yes(c.Events.Count(e=>e.Kind=="hit")==4);
        });
        check("강공격: 이름에 Heavy가 없는 글레이브 회수 폭발도 콤보 소모",()=>{
            var b=B("Glaive Prime") with {Melee=new(Attack:"Charged Throw Recall Explosion"),Scenario=new(Combo:4,BaseInterval:20,Enemy:new(1e9,0))};
            var c=C(b,1);Near(c.Events.Single(e=>e.Kind=="heavyCombo").Amount,60);Near(c.Points[0].ComboPoints,0);
            Yes(!c.Events.Any(e=>e.Kind=="combo"));
        });
        check("글레이브 스탠스: 일반·충전 회수 폭발을 분리하고 오래된 3타 배율을 합산하지 않음",()=>{
            var b=B("Glaive Prime","Condition Overload") with {Scenario=new(Combo:4,Enemy:new(1e9,0)),Melee=new(Statuses:new(){[DamageType.Viral]=1,[DamageType.Heat]=1})};
            b=b with {Stance=new("Glaive","Heavy Charged",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Glaive")};
            var charged=StanceEngine.Calculate(catalog,b,stances);var normal=StanceEngine.Calculate(catalog,b with {Stance=b.Stance! with {ComboKey="Heavy"}},stances);
            Near(charged.ComboDamage[DamageType.Blast],1184*4);Near(normal.ComboDamage[DamageType.Blast],592*4);
            Near(charged.Stats.CriticalChance,.26);Near(charged.Stats.CriticalMultiplier,2.4);Near(charged.Stats.StatusChance,.34);
            Yes(charged.Hits.Length==1&&charged.Hits[0].Count==1&&charged.Hits[0].CoVerified);
            Yes(charged.Hits[0].Calculation!.Melee!.ForcedProcs.Contains("Slash"));Yes(charged.Duration is null&&charged.TargetDps is null);
            Yes(charged.Combo.InputLabel.Contains("충전")&&normal.Combo.InputLabel.Contains("일반"));
            Yes(stances.Stances.Single(s=>s.Id=="Glaive").Combos.Single(c=>c.Key=="Heavy").HitCount==3);
            Near(DpsEquation.From(charged.Stats,charged).Evaluate(),charged.TargetDamage);
        });
        check("글레이브 12종: 모든 호환 스탠스의 두 회수 동작이 해당 무기의 원본 공격과 일치",()=>{
            var weapons=catalog.Weapons.Where(w=>w.WeaponType=="Glaive").ToArray();Yes(weapons.Length==12);
            foreach(var w in weapons)foreach(var st in stances.ForWeapon(w))foreach(var action in st.Combos.Where(c=>c.AttackOverride is not null))
            {
                var b=B(w.EnglishName) with {Stance=new(st.Id,action.Key,stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,st.Id)};
                var result=StanceEngine.Calculate(catalog,b,stances);
                var direct=MeleeEngine.Calculate(catalog,b with {Stance=null,Melee=new(Attack:action.AttackOverride!)});
                Near(result.ExpectedComboDamage,direct.ExpectedHit);Yes(result.Hits[0].Calculation!.Melee!.ForcedProcs.SequenceEqual(direct.Melee!.ForcedProcs));
            }
        });
        check("탁 앤 러그: 무기별 강공격 591×4 및 후속3회 강제 폭발, 공통 검방패 배율 제외",()=>{
            var b=B("Tak & Lug") with {Scenario=new(Combo:4,Enemy:new(1e9,0))};
            foreach(var st in stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)))
            {
                var full=b with {Stance=new(st.Id,"Heavy",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,st.Id)};
                var c=StanceEngine.Calculate(catalog,full,stances);Near(c.ComboDamage.Values.Sum(),591*4*4);
                Near(c.Hits.Sum(h=>h.Count),4);Near(c.Hits.Where(h=>h.Calculation!.Melee!.ForcedProcs.Contains("Blast")).Sum(h=>h.Count),3);
                Yes(c.Hits.All(h=>h.Group==1));Yes(c.Duration is null&&c.TargetDps is null);Near(DpsEquation.From(c.Stats,c).Evaluate(),c.TargetDamage);
                Near(DpsEquation.From(c.Stats,c).Terms.Single(t=>t.Id=="base").Value,2364);
                Yes(c.Combo.InputLabel.Contains("방패 투척"));
            }
        });
        check("탁 앤 러그: 시간 가정 시 4타·후속 폭발 순서, 한 입력에서 콤보 한 번 소모",()=>{
            var b=B("Tak & Lug") with {Stance=new("Sword and Shield","Heavy",stances.Revision),Melee=new(ComboDurationOverride:2),Scenario=new(Combo:4,Enemy:new(1e9,0)),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,.1,.2,.3])};
            b=b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Sword and Shield")};
            var c=C(b,1);Near(c.Events.Count(e=>e.Kind=="hit"),4);Near(c.Events.Count(e=>e.Kind=="proc"&&e.Description=="폭발 부여"),3);
            Near(c.Events.Single(e=>e.Kind=="heavyCombo").Amount,60);Near(c.Events.Single(e=>e.Kind=="heavyCombo").Time,.3);
            Yes(c.Events.Where(e=>e.Kind=="proc"&&e.Description=="폭발 부여").All(e=>e.Time>0));
        });
        check("복합 동작 지원 범위: 특수 슬램·투사체 미연결을 관계없는 일반 타격에 전파하지 않음",()=>{
            foreach(var name in new[]{"Azothane","Caustacyst","Coda Caustacyst","Ether Daggers","Orvius","Rumblejack","Sampotes","Tak & Lug","Tatsu","Tatsu Prime","Tenet Exec","Thalys","Vitrica","Zenistar"})
            {var result=C(B(name),1);Yes(result.DirectDamage>0);}
            var unresolved=CombatTimeline.Calculate(catalog,B("Sampotes") with {Melee=new(Attack:"Slam Attack")},stances,1);
            Yes(unresolved.Unsupported.Any(s=>s.Contains("충격파")));
        });
        check("전용 모드 발동 범위: 강공격 전용 모드를 넣어도 일반 공격은 정상 계산",()=>{
            foreach(var (name,mod) in new[]{("Prova","Static Discharge"),("Prova Vandal","Static Discharge"),("Amanata","Amanata Pressure")})
            {
                var b=B(name,mod) with {Melee=new(Conditions:new(){["Roll"]=2})};Yes(C(b,1).DirectDamage>0);
                var heavy=CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Attack="Heavy Attack"}},stances,1);Yes(name=="Amanata"?heavy.Unsupported.Length==0:heavy.Unsupported.Length>0);
            }
        });
        check("아마나타 프레셔: 랭크는 피해만 변경, 무작위 상태 기능 자체는 전 랭크에 존재",()=>{
            var b=B("Amanata","Amanata Pressure");
            for(var rank=0;rank<=5;rank++)
            {
                var slots=(ModSlot?[])b.Slots.Clone();slots[0]=slots[0]! with {Rank=rank};
                var c=MeleeEngine.Calculate(catalog,b with {Slots=slots});Near(c.Melee!.Effects["Damage"],.55*(rank+1));Near(c.Melee.Effects["AmanataRandomProc"],1);
            }
        });
        check("엑소디아 발동 범위: 컨테이전·에피데믹 장착만으로 일반 조우 타격을 막지 않음",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var source=ReferenceCatalog.Load();
            foreach(var name in new[]{"Exodia Contagion","Exodia Epidemic"})
            {
                var arcane=source.Arcanes.Single(a=>a.EnglishName==name);
                var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Melee=new(Exodia:new(arcane.Id,arcane.MaxRank))};
                Near(C(b,1).DirectDamage,C(b with {Melee=new()},1).DirectDamage);
                if(name=="Exodia Contagion")Yes(CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["Airborne"]=1}}},stances,1).Unsupported.Length>0);
            }
        });
        check("릴렌트리스 컴비네이션: 베기 부여와 지연된 각 틱의 콤보를 별도 반영",()=>{
            var b=B("Nikana Prime","Relentless Combination") with {Melee=new(Attack:"Heavy Attack"),Scenario=new(BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds)};
            var c=C(b,1);var procs=c.Events.Count(e=>e.Kind=="proc"&&e.Description=="베기 부여");var ticks=c.Events.Count(e=>e.Kind=="tick"&&e.Description=="베기 상태 피해");
            var gain=c.Events.Where(e=>e.Kind=="combo"&&e.Description.Contains("릴렌트리스")).ToArray();Near(gain.Length,procs+ticks);Yes(ticks>=6);
            Near(c.Points[0].ComboPoints,0);Near(c.Points.First(p=>p.Time==1).ComboPoints,procs);
            Near(c.Points[^1].ComboPoints,ticks); // 마지막 틱이 콤보 만료 시각을 다시 갱신한다.
        });
        check("릴렌트리스 컴비네이션: 추가 콤보 확률 100%는 각 발동을 두 점으로",()=>{
            var b=B("Nikana Prime","Relentless Combination","True Punishment") with {Melee=new(Attack:"Heavy Attack"),Scenario=new(BaseInterval:20,Enemy:new(1e9,0))};
            var c=C(b,1);var gain=c.Events.Where(e=>e.Kind=="combo"&&e.Description.Contains("릴렌트리스")).ToArray();Yes(gain.Length>0);Yes(gain.All(e=>e.Amount==2));
        });
        check("사이베어 진화: 첫 타격 자신의 냉기는 소급하지 않고 다음 명중부터 +10 콤보",()=>{
            var b=B("Sibear");var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Name=="Master’s Shatter");
            b=b with {Melee=new(Evolutions:[perk.Id]),Scenario=new(BaseInterval:.1,Enemy:new(1e9,0))};
            var c=C(b,1);var gain=c.Events.Where(e=>e.Kind=="combo"&&e.Description.StartsWith("명중")).ToArray();Near(gain[0].Amount,1);
            var cold=c.Events.First(e=>e.Kind=="proc"&&e.Description=="냉기 부여");
            Near(gain.First(e=>e.Time>cold.Time).Amount,11);
        });
        check("스카나 진화: 적 체력·실드가 손상되기 전만 +9, 오버가드 피해는 제외",()=>{
            foreach(var name in new[]{"Skana","Prisma Skana","Skana Prime"})
            {
                var b=B(name);var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Name=="Wartime Nerve");
                b=b with {Melee=new(Evolutions:[perk.Id]),Scenario=new(BaseInterval:.5,Enemy:new(1e9,0))};
                var normal=C(b,1).Events.Where(e=>e.Kind=="combo"&&e.Description.StartsWith("명중")).Take(2).ToArray();
                Near(normal[0].Amount,10);Near(normal[1].Amount,1);
                var guard=C(b with {Melee=b.Melee! with {Overguard=1e8}},1).Events.Where(e=>e.Kind=="combo"&&e.Description.StartsWith("명중")).Take(5).ToArray();
                Yes(guard.Length==5&&guard.All(e=>e.Amount==10));
                var shield=C(b with {Melee=b.Melee! with {Shields=1e8}},1).Events.Where(e=>e.Kind=="combo"&&e.Description.StartsWith("명중")).Take(2).ToArray();
                Near(shield[0].Amount,10);Near(shield[1].Amount,1);
            }
        });
        check("프레이도스 진화: 슬램 +4 콤보에 추가 콤보 확률 적용",()=>{
            var b=B("Praedos","True Punishment");var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Name=="Shockwave Synergy");
            b=b with {Melee=new(Attack:"Slam Attack",Evolutions:[perk.Id]),Scenario=new(BaseInterval:20,Enemy:new(1e9,0))};
            var c=C(b,1);Near(c.Events.Single(e=>e.Description=="슬램 범위 명중 · 진화 콤보").Amount,8);
            Near(c.Points[0].ComboPoints,10); // 일반 명중 2 + 진화 8
        });
        check("인노뎀: 변신 투사체 자체는 콤보를 쌓지 않음",()=>{
            var b=B("Innodem") with {Melee=new(Attack:"Dagger Projectile",Incarnon:true),Scenario=new(BaseInterval:.5,Enemy:new(1e9,0))};
            var c=C(b,1);Near(c.Points[^1].ComboPoints,0);Yes(!c.Events.Any(e=>e.Kind=="combo"));
        });
        check("전투 행동: 탄약 횟수·연속 지상 슬라이드 거리·막기 콤보를 구분",()=>{
            var b=B("Innodem","True Punishment","Guardian Derision");var inc=IncarnonEngine.ForWeapon(b.WeaponId)!;
            b=b with {Melee=new(Evolutions:inc.Perks.Where(p=>p.Name is "Armed Inspiration" or "Hawk Momentum").Select(p=>p.Id).ToArray()),Scenario=new(BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(.1,"AmmoPickup",3),new(.2,"GroundSlide",9),new(.3,"GroundSlide",9),new(.4,"GroundSlide",25),new(.5,"BlockedHit",2)])};
            var c=C(b,1);Near(c.Events.Single(e=>e.Description=="탄약 습득 · 진화 콤보").Amount,15);
            var slide=c.Events.Where(e=>e.Description=="연속 지상 슬라이드 · 진화 콤보").ToArray();Yes(slide.Length==1);Near(slide[0].Amount,10);
            var blocking=MeleeEngine.Calculate(catalog,b).Melee!;var blocks=c.Events.Where(e=>e.Description=="막기 · 콤보 획득").ToArray();Yes(blocks.Length==2);
            Yes(blocks.All(e=>e.Amount is 2 or 3));
            var mean=C(b,512).Points.Single(p=>p.Time==.5).ComboPoints;
            Yes(Math.Abs(mean-(27+2*(blocking.ComboChance+blocking.Effects["BlockingComboChance"])))<.2);
        });
        check("전투 행동: NaN 수량·소수 횟수·음수 거리는 거부",()=>{
            foreach(var input in new[]{new CombatInput(0,"AmmoPickup",double.NaN),new(0,"BlockedHit",1.5),new(0,"GroundSlide",-1),new(0,"AbilityCast",2)})
            {try{C(B() with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[input])});throw new Exception("잘못된 행동 허용");}catch(ArgumentException){}}
        });
        check("아르카 티트론: 반복 슬램은 처음에만 저장 충전을 쓰고 다음 슬램에 유지하지 않음",()=>{
            var b=B("Arca Titron") with {Melee=new(Attack:"Slam Attack",Conditions:new(){["SlamCapacitor"]=5}),Scenario=new(BaseInterval:2,Enemy:new(1e9,0))};
            var charged=C(b,1);var plain=C(b with {Melee=b.Melee! with {Conditions=[]}},1);
            Near(charged.Events.First(e=>e.Kind=="hit").Amount/plain.Events.First(e=>e.Kind=="hit").Amount,13.5);
            var spend=charged.Events.Where(e=>e.Kind=="capacitor"&&e.Description.Contains("소모")).ToArray();Yes(spend.Length>=2);Near(spend[0].Amount,5);Near(spend[1].Amount,0);
            Near(charged.Points[0].SnapshotDps,plain.Points[0].SnapshotDps);
        });
        check("아르카 티트론: 일반 공격 처치는 충전, 직접 슬램 처치는 충전하지 않음",()=>{
            var b=B("Arca Titron") with {Scenario=new(Enemy:new(1,0))};
            Near(C(b,1).Events.Single(e=>e.Kind=="capacitor"&&e.Description.Contains("처치")).Amount,1);
            Yes(!C(b with {Melee=new(Attack:"Slam Attack")},1).Events.Any(e=>e.Kind=="capacitor"&&e.Description.Contains("처치")));
        });
        check("검과 방패 5종: 막기 충전은 최대4, 다음 공격에 소모되고 막아서 다시 충전",()=>{
            foreach(var name in new[]{"Cobra & Crane","Cobra & Crane Prime","Sigma & Octantis","Silva & Aegis","Silva & Aegis Prime"})
            {
                var b=B(name) with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"BlockedHit",8),new(.1,"BlockedHit",2)])};
                var c=C(b,1);var charges=c.Events.Where(e=>e.Kind=="blockCharge").ToArray();
                Yes(charges.Length==4);Near(charges[0].Amount,4);Near(charges[1].Amount,4);Near(charges[2].Amount,2);Near(charges[3].Amount,2);
                Yes(charges[1].Description.Contains("소모")&&charges[3].Description.Contains("소모"));
            }
        });
        check("도르클레이브: 시작 벤데타는 10개 공격 입력 후 종료",()=>{
            var b=B("Dorrclave") with {Melee=new(Conditions:new(){["Vendetta"]=1}),Scenario=new(BaseInterval:.2,Enemy:new(1e9,0))};
            var c=C(b,1);var uses=c.Events.Where(e=>e.Kind=="vendetta"&&e.Description.Contains("남은 공격")).ToArray();
            Yes(uses.Length==10);Yes(uses.Select(e=>e.Amount).SequenceEqual(Enumerable.Range(0,10).Reverse().Select(n=>(double)n)));
            Yes(c.Events.Count(e=>e.Kind=="hit")>10);
        });
        check("타격 시각이 없는 경로에서 입력한 콤보 사건을 몰래 생략하지 않음",()=>{
            var b=B() with {Stance=new("Blind Justice","Neutral",stances.Revision),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"BlockedHit",30)])};
            b=b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")};
            var c=EncounterEngine.Calculate(catalog,b,stances,1);Yes(c.Unsupported.Any(n=>n.Contains("전투 중 행동")));
        });
        check("사건 전투: 실제 체력 소진 시 피해·공격·상태 틱 중단",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)}};var c=C(b);Near(c.DirectDamage,1);Near(c.StatusDamage,0);Near(c.Ttk.Median!.Value,0);Near(c.Ttk.KillFraction,1);Yes(c.Events.Count(e=>e.Kind=="hit")==1);Yes(c.Points.All(p=>p.Health==0));
        });
        check("사건 전투: 처치 못한 표본을 TTK 분위수에서 버리지 않음",()=>{
            var c=C(B());Yes(c.Ttk.P10 is null&&c.Ttk.Median is null&&c.Ttk.P90 is null);Near(c.Ttk.KillFraction,0);Yes(c.Points.Length==101);Near(c.Points[^1].Time,5);
        });
        check("사건 전투: 베기 틱 지연과 직접 피해·상태 피해 분리",()=>{
            var c=C(B());Yes(c.StatusDamage>0);Yes(c.Points.Where(p=>p.Time<1).All(p=>p.StatusDamage==0));Near(c.Points[^1].DirectDamage,c.DirectDamage);Near(c.Points[^1].StatusDamage,c.StatusDamage);
        });
        check("사건 전투: 컨오 비교선은 같은 상태에서 CO만 제외",()=>{
            var b=B("Nikana Prime","Condition Overload");var c=C(b,32);Yes(c.Points.Any(p=>p.SnapshotDps>p.WithoutCoDps));Yes(c.Points.All(p=>p.SnapshotDps>=p.WithoutCoDps));var no=C(B());Yes(no.Points.All(p=>Math.Abs(p.SnapshotDps-p.WithoutCoDps)<1e-8));
        });
        check("사건 전투: 0초 첫 타격에 자신의 컨오가 소급되지 않음",()=>{
            var plain=C(B(),1);var co=C(B("Nikana Prime","Condition Overload"),1);Near(plain.Events.First(e=>e.Kind=="hit").Amount,co.Events.First(e=>e.Kind=="hit").Amount);
        });
        check("사건 전투: 오버가드·실드·체력 전이와 피해 보존",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1000,0)},Melee=new(Shields:200,Overguard:300)};var c=C(b,32);Yes(c.Events.Any(e=>e.Description=="오버가드 파괴"));Yes(c.Events.Any(e=>e.Description=="실드 파괴"));foreach(var p in c.Points)Near(p.DirectDamage+p.StatusDamage+p.Health+p.Shields+p.Overguard,1500);
        });
        check("사건 전투: 사망 후 DPS 그래프는 0, 누적 피해는 유지",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)}};var c=C(b);Yes(c.Points.All(p=>p.SnapshotDps==0&&p.WithoutCoDps==0&&p.DirectDamage==1));
        });
        check("사건 전투: 위키 미기재 타격 시각을 균등 생성하지 않음",()=>{
            var b=B();b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")};var c=CombatTimeline.Calculate(catalog,b,stances,1);Yes(c.Unsupported.Any(s=>s.Contains("개별 타격 시각")));Yes(c.Points.Length==0);
        });
        check("사건 전투: 저장된 사용자 타격 시간의 순서·개수 검사",()=>{
            var b=B();b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice"),Encounter=new(EncounterMode.FiveSeconds,HitTimes:[0,1])};try{C(b);throw new Exception("잘못된 타수 허용");}catch(ArgumentException){}
        });
        check("사건 전투: 상태 만료 후 CO 수치 복귀",()=>{
            var b=B("Nikana Prime","Condition Overload");b=b with {Scenario=b.Scenario with {BaseInterval=20},Encounter=new(EncounterMode.TenSeconds)};var c=C(b,32);Near(c.Points[^1].StatusTypes,0);Near(c.Points[^1].SnapshotDps,c.Points[^1].WithoutCoDps);
        });
        check("사건 전투: 리션 상태 발생 이후 프렌지 발동",()=>{
            var c=C(B("Lesion"),32);Yes(c.Events.Any(e=>e.Description.StartsWith("리션 격노 발동")));Yes(c.Points.Any(p=>p.SnapshotDps>p.StartDps));
        });
        check("버닝 헤이트: 화염 틱으로 표식 생성, 기존 상태를 소급 증폭하지 않음",()=>{
            var b=B("Hate","Burning Hate","Weeping Wounds","Melee Prowess") with {Melee=new(Attack:"Spectral Blade Explosion",Incarnon:true),Scenario=new(Combo:12,BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds,ScriptedAttacksOnly:true,IncarnonBlades:[new(0,0,Motion:"Neutral",Surface:true)])};
            var slots=(ModSlot?[])b.Slots.Clone();slots[0]=null;
            var marked=C(b,1);var plain=C(b with {Slots=slots},1);
            Near(marked.Events.Single(e=>e.Kind=="vulnerability").Time,1.4);Near(marked.StatusDamage,plain.StatusDamage);
            Yes(marked.Events.Any(e=>e.Kind=="proc"&&e.Description=="화염 부여"));
        });
        check("버닝 헤이트: 표식을 건 타격의 새 상태는 제외하고 그 다음 타격부터 2.2배",()=>{
            var b=B("Hate","Burning Hate","Weeping Wounds","Melee Prowess") with {Melee=new(Attack:"Spectral Blade Explosion",Incarnon:true),Scenario=new(Combo:12,BaseInterval:.2,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,IncarnonBlades:Enumerable.Range(0,6).Select(i=>new IncarnonBladeContact(i*.2,i*.2,Projectile:i+1,Motion:"Neutral",Surface:true)).ToArray())};
            var slots=(ModSlot?[])b.Slots.Clone();slots[0]=null;var marked=C(b,1);var plain=C(b with {Slots=slots},1);
            var applied=marked.Events.Single(e=>e.Kind=="vulnerability").Time;Near(applied,.6);
            var mp=marked.Events.Where(e=>e.Kind=="proc"&&e.Description=="화염 부여").Take(6).ToArray();var pp=plain.Events.Where(e=>e.Kind=="proc"&&e.Description=="화염 부여").Take(6).ToArray();
            Yes(mp.Length==pp.Length&&mp.Any(e=>e.Time>applied));
            for(var i=0;i<mp.Length;i++){Near(mp[i].Time,pp[i].Time);Near(mp[i].Amount/pp[i].Amount,mp[i].Time<=applied+1e-9?1:2.2);}
        });
        check("버닝 헤이트: 상태 피해 브라켓은 엘리멘탈리스트와 곱, 직접 피해와 구분",()=>{
            var b=B("Hate","Burning Hate","Melee Elementalist") with {Melee=new(Conditions:new(){["BurningHateApplied"]=1})};
            var marked=MeleeEngine.Calculate(catalog,b);var ordinary=MeleeEngine.Calculate(catalog,b with {Melee=new()});
            Near(marked.ExpectedHit,ordinary.ExpectedHit);
            foreach(var p in marked.Melee!.StatusDamage)Near(p.DamagePerTick,ordinary.Melee!.StatusDamage.Single(x=>x.Type==p.Type).DamagePerTick*2.2);
        });
        check("사건 전투: 익스포저는 어빌리티 사건 이후 발동",()=>{
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Exposure");var b=B();b=b with {Arcanes=new(Melee:new(a.Id,5))};var inactive=C(b,16);var active=C(b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"AbilityCast")])},16);Yes(active.DirectDamage>inactive.DirectDamage);Yes(active.Events.Any(e=>e.Description.StartsWith("밀리 익스포저 발동")));
        });
        check("어시밀레이션: 내 실드 파괴 전 미발동, 파괴 이후 강공격 피해 가산",()=>{
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Assimilation");
            var b=B("Nikana Prime","Pressure Point") with {Melee=new(Attack:"Heavy Attack"),Arcanes=new(Melee:new(a.Id,5))};
            var idle=C(b,1);var active=C(b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"PlayerShieldBreak")])},1);
            Near(active.Events.First(e=>e.Kind=="hit").Amount/idle.Events.First(e=>e.Kind=="hit").Amount,3.7/2.2);
            Yes(active.Events.Any(e=>e.Description.Contains("어시밀레이션 발동 · 20초")));
            var regular=b with {Melee=new()};Near(C(regular,1).DirectDamage,C(regular with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"PlayerShieldBreak")])},1).DirectDamage);
        });
        check("어시밀레이션: 대상 실드 파괴로는 발동하지 않음",()=>{
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Assimilation");
            var b=B() with {Melee=new(Attack:"Heavy Attack",Shields:1),Arcanes=new(Melee:new(a.Id,5))};
            var c=C(b,1);Yes(c.Events.Any(e=>e.Description=="실드 파괴"));Yes(!c.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("어시밀레이션")));
        });
        check("밀리 커린: 구르기 냉기·동결·2초 재사용, 오버가드는 동결 차단",()=>{
            var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Careen");
            var b=B("Silva & Aegis") with {Arcanes=new(Melee:new(arcane.Id,5)),Scenario=new(BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"RollThroughTarget"),new(1,"RollThroughTarget"),new(2,"RollThroughTarget")])};
            var c=C(b,1);Near(c.Events.Count(e=>e.Kind=="careen"),2);Near(c.Events.Count(e=>e.Kind=="proc"&&e.Description=="냉기 부여"),10);Near(c.Events.Count(e=>e.Kind=="freeze"),1);
            var guard=C(b with {Melee=new(Overguard:1e8)},1);Yes(!guard.Events.Any(e=>e.Kind=="freeze"));
        });
        check("밀리 커린: 냉기 지속은 6초, 래스팅 스팅 제외·외부 범용 보너스만 적용",()=>{
            var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Careen");
            var b=B("Silva & Aegis","Lasting Sting") with {Arcanes=new(Melee:new(arcane.Id,0)),Scenario=new(BaseInterval:20,Enemy:new(1e9,0)),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"RollThroughTarget")])};
            var rolled=C(b,1);var plain=C(b with {Encounter=new(EncounterMode.TenSeconds)},1);
            Near(rolled.Points.First(p=>p.Time==7).SnapshotDps,plain.Points.First(p=>p.Time==7).SnapshotDps);
            var extended=C(b with {Melee=new(Conditions:new(){["UniversalStatusDuration"]=50})},1);
            Yes(extended.Points.First(p=>p.Time==7).SnapshotDps>rolled.Points.First(p=>p.Time==7).SnapshotDps);
            Near(extended.Points.First(p=>p.Time==9).SnapshotDps,plain.Points.First(p=>p.Time==9).SnapshotDps);
        });
        check("밀리 커린: 구르기 상태를 리션 무기 자체의 상태 발생으로 취급하지 않음",()=>{
            var arcane=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Careen");
            var b=B("Lesion") with {Arcanes=new(Melee:new(arcane.Id,0)),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"RollThroughTarget")])};
            var c=C(b,1);var beforeHit=c.Events.TakeWhile(e=>e.Kind!="hit");Yes(!beforeHit.Any(e=>e.Description.StartsWith("리션 격노")));
        });
        check("어시밀레이션: 직접 강공격 처치만 실드 회복, 상태 처치 제외",()=>{
            var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Assimilation");
            var b=B() with {Melee=new(Attack:"Heavy Attack"),Arcanes=new(Melee:new(a.Id,5)),Scenario=new(Enemy:new(1,0)),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"PlayerShieldBreak")])};
            var c=C(b,1);Near(c.Events.Single(e=>e.Kind=="shieldRestore").Amount,.3);
            var hit=c.Events.First(e=>e.Kind=="hit").Amount;
            var dot=C(b with {Scenario=new(BaseInterval:20,Enemy:new(hit+1,0))},1);Yes(dot.Events.Any(e=>e.Kind=="kill"));Yes(!dot.Events.Any(e=>e.Kind=="shieldRestore"));
        });
        check("사건 전투: 고정 시드 재현·취소·유한 입력",()=>{
            var b=B();Near(C(b,8).StatusDamage,C(b,8).StatusDamage);using var token=new CancellationTokenSource();token.Cancel();try{CombatTimeline.Calculate(catalog,b,stances,8,token.Token);throw new Exception("취소 미반영");}catch(OperationCanceledException){}try{C(b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(double.NaN,"AbilityCast")])});throw new Exception("NaN 허용");}catch(ArgumentException){}
        });
        check("조우: 셉판·피예·바르기트 II 자이 수치 손계산",()=>{
            var w=ZawCatalog.Weapon(ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai"));Near(w.Damage.Values.Sum(),218);Near(w.CriticalChance,.34);Near(w.StatusChance,.12);Near(w.AttackSpeed,1.083);Yes(w.WeaponType=="Nikana");Near(RivenValues.Disposition(w.Id),.7);
        });
        check("조우: 양손 도크람 피해 보정 0.93 및 분포 보존",()=>{
            var w=ZawCatalog.Weapon(ZawCatalog.Id("Dokrahm","Seekalla","Jai"));Near(w.Damage.Values.Sum(),(309-4-4)*.93);Near(w.Damage[DamageType.Slash]/w.Damage.Values.Sum(),.5);Yes(w.WeaponType=="Heavy Blade");
        });
        check("조우: 전체 1760개 조합의 식별자·수치·수용량",()=>{
            var all=ZawCatalog.All().ToArray();Yes(all.Length==1760&&all.Select(w=>w.Id).Distinct().Count()==1760);foreach(var w in all){Yes(w.AttackSpeed>0&&w.StatusChance>0&&w.CriticalChance>0);Yes(CapacityCatalog.Load().Default(w.Id,null).SlotPolarities.Length==8);}
        });
        check("조우: 실제 계산과 엑소디아 슬롯 허용",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null)};var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Triumph");var result=MeleeEngine.Calculate(catalog,b with {Melee=new(Exodia:new(a.Id,a.MaxRank))});Near(result.Melee!.ComboChance,1.5);
        });
        check("조우: 플레이그 크리패스 추가 피해는 네 속성에 균등 분배",()=>{
            var w=ZawCatalog.Weapon(ZawCatalog.Id("Plague Kripath","Peye","Jai"));Near(w.Damage[DamageType.Impact],28);Near(w.Damage[DamageType.Puncture],68);Near(w.Damage[DamageType.Slash],47);Near(w.Damage[DamageType.Viral],62);
        });
        check("조우: 위키 무기군 강공격 배율·준비 시간·콤보 브라켓",()=>{
            foreach(var (strike,grip,multiplier,windup) in new[]{("Sepfahn","Peye",5d,.5),("Balla","Peye",5d,.4),("Rabvee","Seekalla",6d,1.2),("Plague Kripath","Peye",4.5,.5)})
            {
                var id=ZawCatalog.Id(strike,grip,"Jai");var w=ZawCatalog.Weapon(id);
                var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Melee=new(Attack:"Heavy Attack"),Scenario=new(Combo:4,Enemy:new(1e9,0))};
                var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage.Values.Sum(),w.Damage.Values.Sum()*multiplier*4);Near(c.Melee!.Windup,windup);
                Yes(c.Limitations.Any(n=>n.Contains(MeleeClassRules.Source.Source.Url)));
            }
        });
        check("동작 시간 미기재: 조우 강공격 타격·강제 베기 보존, DPS·TTK 임의 생성 금지",()=>{
            var id=ZawCatalog.Id("Balla","Peye","Jai");var w=ZawCatalog.Weapon(id);
            var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,"Dagger"),Stance=new("Dagger","Heavy",stances.Revision)};
            var c=StanceEngine.Calculate(catalog,b,stances);
            Yes(c.Duration is null&&c.RawDps is null&&c.TargetDps is null);Yes(c.Hits.Length==4);
            Near(c.ComboDamage.Values.Sum(),w.Damage.Values.Sum()*10);
            Yes(c.Hits[0].Calculation!.Melee!.ForcedProcs.Contains("Slash"));Yes(!c.Hits[1].Calculation!.Melee!.ForcedProcs.Contains("Slash"));
            var result=EncounterEngine.Calculate(catalog,b,stances,1);Yes(result.DamageOnly&&result.AverageDps is null&&result.Samples.Length==0);Near(result.Damage.Low,c.TargetDamage);
            try{StanceEngine.Simulate(c,1000);throw new Exception("시간 미기재 동작의 TTK 생성");}catch(ArgumentException){}
        });
        check("누락된 무기별 강공격 값: 아마나타는 위키 폴암 6배 적용",()=>{
            var b=B("Amanata") with {Melee=new(Attack:"Heavy Attack")};Near(MeleeEngine.Calculate(catalog,b).Damage.Values.Sum(),126*6);
        });
        check("전투 그래프: 컨오 브라켓 역산과 모드 실제 해제 계산 일치",()=>{
            foreach(var name in new[]{"Nikana Prime","Redeemer Prime","Glaive Prime"})
            {
                var b=B(name,"Pressure Point","Condition Overload","Fever Strike","Organ Shatter");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=3,[DamageType.Corrosive]=5}},Scenario=b.Scenario with {Enemy=new(1e9,1000)}};
                foreach(var attack in MeleeEngine.Attacks(b.WeaponId).Where(a=>a is "Normal Attack" or "Heavy Attack"))
                {var x=b with {Melee=b.Melee! with {Attack=attack}};var full=MeleeEngine.Calculate(catalog,x);var slots=(ModSlot?[])x.Slots.Clone();slots[1]=null;Near(CombatTimeline.WithoutConditionOverloadDps(full),MeleeEngine.Calculate(catalog,x with {Slots=slots}).TargetDps);}
            }
        });
        check("전투 그래프: 스탠스 첫 타격 직전은 상태 고정 콤보 DPS와 같음",()=>{
            var b=B("Nikana Prime","Condition Overload");b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice"),Encounter=new(EncounterMode.StanceCycle)};
            var c=EncounterEngine.CalculateOrdered(catalog,b,stances,8);Yes(c.Unsupported.Length==0);Near(c.Samples[0].SnapshotDps!.Low,StanceEngine.Calculate(catalog,b,stances).TargetDps);Near(c.Samples[0].CoGain!.High,0);Yes(c.Samples.Skip(1).Any(s=>s.CoGain!.High>0));
        });
        check("사건 전투: 강공격 후 초기 콤보는 초당40 회복, 즉시 복원 아님",()=>{
            var b=B("Nikana Prime","Corrupt Charge");b=b with {Melee=new(Attack:"Heavy Attack"),Scenario=b.Scenario with {BaseInterval=20}};var c=C(b,1);Near(c.Points[0].Combo,1);Near(c.Points.First(p=>p.Time==.5).Combo,2);
        });
        check("사건 전투: 방어력 감소 후 200 하한을 다시 씌우지 않음",()=>{
            var b=B("Nikana Prime","Shattering Impact");b=b with {Scenario=b.Scenario with {BaseInterval=20,Enemy=new(1e9,240)},Encounter=new(EncounterMode.FiveSeconds,30)};Near(C(b,1).Points[0].Armor,192);
        });
        check("사건 전투: 하모니 강공격은 같은 타격의 베기까지 즉시 소모",()=>{
            var b=B("Harmony");b=b with {Melee=new(Attack:"Heavy Attack"),Scenario=b.Scenario with {BaseInterval=20}};var c=C(b,1);Yes(c.Events.Any(e=>e.Kind=="detonate"&&e.Time==0));Yes(c.Points[0].StatusDamage>0);Near(c.Points[0].StatusDamage,c.Points[^1].StatusDamage);
        });
        check("사건 전투: 엑소디아 포스는 무기218×200%, 거리3m에서 절반",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Force");var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Melee=new(Exodia:new(a.Id,3)),Encounter=new(EncounterMode.TenSeconds)};
            var c=C(b,1);var bursts=c.Events.Where(e=>e.Kind=="secondary").ToArray();Yes(bursts.Length>0);foreach(var burst in bursts)Near(burst.Amount,436);c=C(b with{Melee=b.Melee! with {Distance=3}},1);foreach(var burst in c.Events.Where(e=>e.Kind=="secondary"))Near(burst.Amount,218);
        });
        check("사건 전투: 5초 구간은 종료 경계에 새 공격을 추가하지 않음",()=>{
            var b=B();b=b with{Scenario=b.Scenario with {BaseInterval=65d/60}};var c=C(b,1);Yes(c.Events.Count(e=>e.Kind=="hit")==5);Yes(!c.Events.Any(e=>e.Kind=="hit"&&e.Time>=5));
        });
        RivenRoll R(params RivenTrait[] traits)=>new(B().WeaponId,8,traits,RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK");
        check("리벤: 무기 기질×양옵 가중치×0.9~1.1 손계산",()=>{
            var r=R(new(RivenStat.Damage,0),new(RivenStat.StatusChance,0));var range=RivenValues.Range(r,r.Traits[0]);Near(range.Minimum,164.7*.6*.99*.9);Near(range.Maximum,164.7*.6*.99*1.1);
        });
        check("리벤: 음옵 콤보 확률은 별도 기본값 104.85",()=>{
            var r=R(new(RivenStat.Damage,0),new(RivenStat.StatusChance,0),new(RivenStat.ComboChance,0,true));Near(RivenValues.Range(r,r.Traits[^1]).Minimum,104.85*.6*.495*.9);
        });
        check("리벤: 접합은 두 숨은 롤의 높은 값 계승·반올림 구간 보존",()=>{
            var r=R(new(RivenStat.Damage,101.8),new(RivenStat.StatusChance,52.4));var p=RivenValues.Predict(r).Single();Yes(p.Candidate.Result==RivenStat.StatusDamage);var inherited=101.8/(164.7*.6*.99);Near((p.Value.Low+p.Value.High)/2,90*.6*.99*inherited);Yes(p.Value.High>p.Value.Low);
        });
        check("리벤: 부적합 음옵·중복 접합·기질 버전 거부",()=>{
            foreach(var r in new[]{R(new(RivenStat.Damage,1,true),new(RivenStat.StatusChance,1),new(RivenStat.Range,1)),R(new(RivenStat.Viral,1,Spliced:true),new(RivenStat.Gas,1,Spliced:true)),R(new(RivenStat.Damage,1),new(RivenStat.StatusChance,1)) with {DispositionVersion="old"}}){try{RivenValues.Validate(r);throw new Exception("부적합 리벤 허용");}catch(ArgumentException){}}
        });
        check("리벤: 실제 모딩 브라켓 및 저장 복원",()=>{
            var r=R(new(RivenStat.Damage,97.8),new(RivenStat.StatusChance,53.5));var b=B();var m=catalog.Mods.Single(m=>m.Id==RivenValues.ModId(b.WeaponId));b=b with {Slots=[new(m.Id,8),null,null,null,null,null,null,null],Riven=r};var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage.Values.Sum(),198*1.978);Near(c.StatusChance,.28*1.535);var loaded=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Near(MeleeEngine.Calculate(catalog,loaded).ExpectedHit,c.ExpectedHit);
        });
        check("리벤: 슬롯 위치 및 옵션 아래부터 원소 결합",()=>{
            var r=R(new(RivenStat.Heat,53.5),new(RivenStat.Cold,53.5));var b=B("Nikana Prime","Fever Strike");var m=catalog.Mods.Single(m=>m.Id==RivenValues.ModId(b.WeaponId));b=b with {Slots=[b.Slots[0],new(m.Id,8),null,null,null,null,null,null],Riven=r};var c=MeleeEngine.Calculate(catalog,b);Yes(c.Damage.ContainsKey(DamageType.Viral)&&c.Damage.ContainsKey(DamageType.Heat)&&!c.Damage.ContainsKey(DamageType.Gas));
            b=b with {Slots=[new(m.Id,8),b.Slots[0],null,null,null,null,null,null]};c=MeleeEngine.Calculate(catalog,b);Yes(c.Damage.ContainsKey(DamageType.Blast)&&c.Damage.ContainsKey(DamageType.Toxin));
        });
        check("리벤: 실제 극성과 코스트를 포르마 슬롯 용량에 사용",()=>{
            var r=R(new(RivenStat.Damage,97.8),new(RivenStat.StatusChance,53.5));var b=B();var m=catalog.Mods.Single(m=>m.Id==RivenValues.ModId(b.WeaponId));b=b with {Slots=[new(m.Id,8),null,null,null,null,null,null,null],Riven=r,Capacity=b.Capacity! with {SlotPolarities=Enumerable.Repeat("AP_ATTACK",8).ToArray()}};Near(CapacityEngine.Calculate(catalog,b).Used,7);try{MeleeEngine.Calculate(catalog,b with{Riven=r with {BaseDrain=null}});throw new Exception("리벤 수용량 미입력 허용");}catch(ArgumentException){}
        });
    }
}
