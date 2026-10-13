using TennoForge.Core;

internal static class IncarnonTimerChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool x){if(!x)throw new Exception("인카논 시간 발동 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string name,string condition)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var p=IncarnonEngine.ForWeapon(w.Id)!.Perks.Single(p=>p.Condition==condition);
            return new(w.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Evolutions:[p.Id],Conditions:new(){[condition]=1}),Encounter:new(EncounterMode.TenSeconds));
        }
        check("나미 솔로: 수동 영구 발동 제거·무기 전환4초·재전환으로 갱신",()=>{
            var b=B("Nami Solo","OnEquip");var no=CombatTimeline.Calculate(catalog,b,stances,1);
            var t=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"EquipMelee"),new(4,"EquipMelee")])},stances,1);Yes(t.Unsupported.Length==0);
            double D(CombatTimelineResult r,double time)=>r.Points.Single(p=>p.Time==time).SnapshotDps;
            Near(D(t,0),D(no,0));Near(D(t,1),D(no,1)*1.3);Near(D(t,7),D(no,7)*1.3);Near(D(t,8),D(no,8));
            Yes(t.Events.Count(e=>e.Kind=="incarnonPerk")==2);
        });
        check("나미 솔로: 마무리 처치 체력40/초·10초·비치명 마무리와 파라존 제외",()=>{
            var b=B("Nami Solo","OnEquip");var p=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("FinisherHealing"));
            b=b with {Melee=new(Evolutions:[p.Id]),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"FinisherHit"),new(1,"ParazonKill"),new(2,"GroundFinisherKill")])};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);Near(r.Recovery!.IncarnonHealing!.Mean,320);Yes(r.Events.Count(e=>e.Kind=="finisherRegen")==1);
        });
        check("중첩되지 않는 체력 재생: 같은 출처 갱신·다른 출처 합산·만료 경계 적분",()=>{
            var r=new TimedHealthRegeneration();r.Activate(0,"Nami",40,10);r.Activate(5,"Nami",40,10);r.Activate(5,"Ichor",33,9);r.Advance(20);
            Near(r.Generated("Nami"),600);Near(r.Generated("Ichor"),297);
        });
        check("듀얼 이코르: 실제 독성3중첩 대상 처치 뒤 다음 대상 교전에서33체력/초",()=>{
            var b=B("Dual Ichor","NeutralFinalHit");var p=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("ToxinKillHealing"));
            b=b with {Scenario=new(BaseInterval:.1,Combo:12,Enemy:new(5000,0)),Melee=new(Evolutions:[p.Id]),Encounter=new(EncounterMode.TenSeconds,NearbyEnemies:[new("other",10,Defense:new(1e9,0))],TargetOrder:[0,1])};
            var mod=catalog.Mods.Single(m=>m.EnglishName=="Weeping Wounds");b.Slots[0]=new(mod.Id,mod.MaxRank);
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);var start=r.Events.First(e=>e.Kind=="toxinKillRegen").Time;Near(r.Recovery!.IncarnonHealing!.Mean,33*Math.Min(9,10-start));
        });
        check("듀얼 이코르: 정지 콤보 마지막 동작의 명중부터 공속33초·수동 영구 버프 제외",()=>{
            var b=B("Dual Ichor","NeutralFinalHit");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).Single(s=>s.Id=="Crossing Snakes");
            b=b with {Stance=new(s.Id,"Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.Id)};
            var sc=StanceEngine.Calculate(catalog,CombatTimeline.InitialState(b),stances);var n=sc.Hits.Sum(h=>h.Count);var times=Enumerable.Range(0,n).Select(i=>i*.1).ToArray();
            b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:times)};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);var first=r.Events.First(e=>e.Kind=="incarnonPerk");
            var startIndex=sc.Hits.TakeWhile(h=>h.SourceHit!.Group!=sc.Hits.Last().SourceHit!.Group).Sum(h=>h.Count);Near(first.Time,times[startIndex]/sc.Stats.AttackSpeed);Yes(r.Points.Last().SnapshotDps>r.Points.First().SnapshotDps);
        });
        check("루복스 중첩 진화: 처치만 공속15%·3상한·도움 제외·기존 수동 조건 제거",()=>{
            var b=B("Ruvox","RuvoxKills") with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeKill"),new(2,"MeleeAssist"),new(3,"MeleeKill"),new(4,"MeleeKill"),new(5,"MeleeKill")])};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);var baseDps=r.Points[0].SnapshotDps;
            Near(r.Points.Single(p=>p.Time==1).SnapshotDps,baseDps*1.15);Near(r.Points.Single(p=>p.Time==2).SnapshotDps,baseDps*1.15);Near(r.Points.Last().SnapshotDps,baseDps*1.45);
        });
        check("인카논 중첩 만료: 한 번은 정확히 만료·반복 발동의 미기재 갱신 정책 구분",()=>{
            var b=B("Thalys","ThalysStatus");var s=new IncarnonStackTimers(b);s.Trigger("Status",0);Near(s.Count("ThalysStatus",7.999),1);Near(s.Count("ThalysStatus",8),0);
            s.Trigger("Status",9);s.Trigger("Status",10);Near(s.Count("ThalysStatus",16),2);try{s.Count("ThalysStatus",17);}catch(CombatRuleUnavailableException){return;}throw new Exception("알 수 없는 중첩 만료를 가정함");
        });
        check("인노뎀: 공중 근접 처치만 피해 중첩·지상/도움 제외·실제 후속 타격",()=>{
            var b=B("Innodem","AirborneKills");b=b with {Melee=b.Melee! with {Conditions=new(){["Airborne"]=1,["AirborneKills"]=3}},Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(1,"MeleeKill"),new(2,"MeleeAssist")])};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.Points.Last().SnapshotDps,r.Points[0].SnapshotDps*1.5);
            var grounded=CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=[]}},stances,1);Near(grounded.Points.Last().SnapshotDps,grounded.Points[0].SnapshotDps);
        });
        check("루복스:8m 주변 적 꿰뚫기·오버가드 제외·5명 조건 자동 판정",()=>{
            var b=B("Ruvox","ImpaledFive");var initial=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("InitialCombo"));b=b with {Melee=b.Melee! with {Attack="Heavy Slam Attack",Incarnon=true,Evolutions=b.Melee.Evolutions!.Append(initial.Id).ToArray()},Scenario=new(BaseInterval:2,Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("a",1),new("b",2),new("c",3),new("d",8),new("outside",8.001),new("guarded",2,Overguard:1e9)],AttackOrder:["Heavy Slam Attack","Heavy Attack"])};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);Yes(r.Events.Count(e=>e.Kind=="impale"&&e.Time==0)==5);
            Near(r.Events.Where(e=>e.Kind=="heavyCombo").Skip(1).First().Amount,12);
        });
        check("인노뎀: 죽이지 않은 마무리도40초 강공격 효율·실제 콤보 소모 반영",()=>{
            var b=B("Innodem","FinisherBuff");b=b with {Melee=b.Melee! with {Attack="Heavy Attack"},Scenario=b.Scenario with {Combo=12},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"FinisherHit")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);Near(t.Events.First(e=>e.Kind=="heavyCombo").Amount,132);
            Yes(t.Events.Any(e=>e.Kind=="incarnonPerk"&&e.Description.Contains("40초"))&&!t.Events.Any(e=>e.Kind=="externalKill"));
        });
        check("루복스: 마무리 명중 이후 콤보 확률+30%·단일 비치명적 사건",()=>{
            var b=B("Ruvox","FinisherBuff") with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"FinisherHit")])};var t=CombatTimeline.Calculate(catalog,b,stances,2048);
            Yes(t.Unsupported.Length==0&&Math.Abs(t.Points[0].ComboPoints-1.3)<.05&&t.Events.Count(e=>e.Kind=="incarnonPerk")==1);
        });
        check("프레이도스: 일반 처치는 제외·슬라이드 처치 후30초 효율50%",()=>{
            var b=B("Praedos","SlideKill");b=b with {Melee=b.Melee! with {Attack="Heavy Attack"},Scenario=b.Scenario with {Combo=12}};
            var no=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"MeleeKill")])},stances,1);
            var yes=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"SlideMeleeKill")])},stances,1);
            Yes(yes.Unsupported.Length==0&&no.Unsupported.Length==0);Near(no.Events.First(e=>e.Kind=="heavyCombo").Amount,220);Near(yes.Events.First(e=>e.Kind=="heavyCombo").Amount,110);
        });
        check("데스트레자: 실제 직접 강공격 처치의8초 준비 속도·일반 처치 제외",()=>{
            var b=B("Destreza","HeavyKill");var no=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"MeleeKill")])},stances,1);Yes(!no.Events.Any(e=>e.Kind=="incarnonPerk"));
            b=b with {Scenario=new(Enemy:new(1,0)),Melee=b.Melee! with {Attack="Heavy Attack"}};var t=CombatTimeline.Calculate(catalog,b,stances,1);
            Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="incarnonPerk"&&e.Description.Contains("8초"))==1);
        });
        check("인노뎀: 실제 비치명적 지상 마무리가 발동하며 처치를 요구하지 않음",()=>{
            var b=B("Innodem","FinisherBuff");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));
            b=b with {Melee=b.Melee! with {ComboDurationOverride=2},Stance=new(s.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.IsDefault?null:s.Id)};
            var sc=StanceEngine.Calculate(catalog,b,stances);b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,sc.Hits.Sum(h=>h.Count)).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Any(e=>e.Kind=="incarnonPerk")&&!t.Events.Any(e=>e.Kind=="kill"));
        });
        check("데스트레자: 원문에 있는 동반 투사체를 누락한 강공격 DPS를 전체 결과로 내지 않음",()=>{
            var b=B("Destreza","HeavyKill");b=b with {Melee=b.Melee! with {Incarnon=true,Attack="Heavy Attack"}};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Any(s=>s.Contains("유령 레이피어")));
            var blade=MeleeEngine.Calculate(catalog,b);Yes(blade.ExpectedHit>0&&blade.Limitations.Any(s=>s.Contains("검 타격")));
            var ordinary=CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Attack="Normal Attack"}},stances,1);Yes(ordinary.Unsupported.Length==0);
        });
        check("피어싱 스테처: 관통마다6초 독립·5중첩 이후 가장 오래된 것만 교체",()=>{
            var s=new PiercingStatureState();for(var n=0;n<5;n++)s.Proc(n);s.Proc(5);s.Advance(6);Yes(s.Stacks==5);s.Advance(7);Yes(s.Stacks==4);s.Advance(10);Yes(s.Stacks==1);s.Advance(11);Yes(s.Stacks==0);
        });
        check("오벡스: 마무리·파라존 같은 영구 카운터·일반 처치 제외·16상한",()=>{
            var b=B("Prisma Obex","FinisherStacks");b=b with {Melee=b.Melee! with {Conditions=new(){["FinisherStacks"]=14}},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"FinisherKill"),new(2,"ParazonKill"),new(3,"MeleeKill"),new(4,"FinisherHit"),new(5,"FinisherKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);Near(t.Points[0].FinisherParkourBonus!.Value,.35);Near(t.Points.Single(p=>p.Time==1).FinisherParkourBonus!.Value,.375);Near(t.Points.Last().FinisherParkourBonus!.Value,.4);
            var e=t.Events.Where(e=>e.Kind=="rapidConclusion").ToArray();Yes(e.Length==3);Near(e[0].Amount,15);Near(e[1].Amount,16);Near(e[2].Amount,16);
        });
        check("피어싱 스테처: 현재 타격에는 소급하지 않고 후속 상확·디사이시브 치확을 갱신",()=>{
            var b=B("Destreza Prime","PunctureStacks");var definition=IncarnonEngine.ForWeapon(b.WeaponId)!;var decisive=definition.Perks.Single(p=>p.Effects.ContainsKey("StatusToBaseCritical"));
            var names=new[]{"Melee Prowess","Weeping Wounds","Body Count"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}
            b=b with {Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Evolutions=b.Melee!.Evolutions!.Append(decisive.Id).ToArray()}};
            var t=CombatTimeline.Calculate(catalog,b,stances,2048);Yes(t.Unsupported.Length==0&&t.Points.Single(p=>p.Time==.5).SnapshotDps>t.Points.Single(p=>p.Time==6.1).SnapshotDps);
            var manual=b with {Melee=b.Melee! with {Conditions=new(){["PunctureStacks"]=0}}};var control=CombatTimeline.Calculate(catalog,manual,stances,2048);Near(t.DirectDamage,control.DirectDamage);
            Yes(t.Events.Where(e=>e.Kind=="piercingStature").All(e=>e.Time==0));
        });
    }
}
