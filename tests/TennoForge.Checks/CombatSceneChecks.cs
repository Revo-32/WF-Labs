using System.Text.Json;
using TennoForge.Core;

internal static class CombatSceneChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("다중 대상 검사 실패");}
        Build B(string name="Xoris"){var id=catalog.Weapons.Single(w=>w.EnglishName==name).Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));}
        CombatTimelineResult Run(Build b,int n=64){var r=CombatTimeline.Calculate(catalog,b,stances,n);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        ArcaneSlot A(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,5);
        check("인플루언스 발동: 전기20%·랭크별 반경/수명·발동 중 갱신 없음·즉사/간접 제외",()=>{
            var s=new MeleeInfluenceState(5);Near(s.Radius,20);Near(s.Duration,18);
            Yes(!s.TryTrigger(0,DamageType.Heat,true,true,()=>0));Yes(!s.TryTrigger(0,DamageType.Electricity,false,true,()=>0));Yes(!s.TryTrigger(0,DamageType.Electricity,true,false,()=>0));
            Yes(!s.TryTrigger(0,DamageType.Electricity,true,true,()=>.2));Yes(s.TryTrigger(0,DamageType.Electricity,true,true,()=>.199));
            Yes(!s.TryTrigger(17,DamageType.Electricity,true,true,()=>throw new Exception("지속 중 재추첨")));Near(s.End,18);
            Yes(s.TryTrigger(18,DamageType.Electricity,true,true,()=>0));Near(s.End,36);
            Yes(Enum.GetValues<DamageType>().Count(MeleeInfluenceState.Spreads)==10);
        });
        check("다중 대상: 저장 복원·입력별 교대·개별 방어층·피해 합산과 대상1 구분",()=>{
            var b=B("Skana");b=b with {Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("armored",5,Defense:new(1e9,2700),Overguard:1e8)],TargetOrder:[0,1])};
            b=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;
            var r=Run(b);Yes(r.Enemies!.Length==2&&r.Enemies[1].DirectDamage>0);Near(r.DirectDamage,r.Enemies[0].DirectDamage);
            Near(r.Enemies[1].Health,1e9);Near(r.Enemies[1].Overguard,1e8-r.Enemies[1].DirectDamage-r.Enemies[1].StatusDamage);
            Yes(r.Enemies[0].Overguard==0&&r.Enemies[0].Health<1e9);
        });
        check("다중 대상: 죽은 적 재공격 제외·다른 적으로 교대·처치 시간은 각각 보존",()=>{
            var b=B("Skana");b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("living",5)],TargetOrder:[0,1])};
            // A second target with the inherited 1 HP dies at the next input.
            var r=Run(b,1);Near(r.Enemies![0].DirectDamage,1);Near(r.Enemies[1].DirectDamage,1);Near(r.Enemies[0].Ttk.Median!.Value,0);Yes(r.Enemies[1].Ttk.Median>0);
        });
        check("인플루언스: 주변 적 전파·20m 경계·원래 대상 중복 없음·상태 틱 독립",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Attack="Charged Throw Recall Explosion"},Arcanes=new(Melee:A("Melee Influence")),Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("edge",20),new("outside",-20.001)])};
            var r=Run(b,128);var alone=Run(b with {Encounter=b.Encounter! with {NearbyEnemies=[]}},128);
            Near(r.DirectDamage,alone.DirectDamage);Near(r.StatusDamage,alone.StatusDamage);
            Yes(r.Enemies![1].DirectDamage>0&&r.Enemies[1].StatusDamage>0);Near(r.Enemies[2].DirectDamage,0);Near(r.Enemies[2].StatusDamage,0);
        });
        check("인플루언스: 한 타 즉사에는 전파·발동 없음",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Attack="Charged Throw Recall Explosion"},Arcanes=new(Melee:A("Melee Influence")),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("survivor",15,Defense:new(1e9,0))])};
            var r=Run(b);Near(r.Enemies![1].DirectDamage,0);Near(r.Enemies[1].StatusDamage,0);Near(r.Enemies[1].StatusTypes,0);
        });
        check("다중 대상: 잘못된 순서·중복 ID·비정상 좌표 입력 거부",()=>{
            void Reject(Build b){try{Run(b,1);}catch(ArgumentException){return;}throw new Exception("잘못된 장면을 허용함");}
            var b=B();Reject(b with {Encounter=new(TargetOrder:[1])});Reject(b with {Encounter=new(NearbyEnemies:[new("a",1),new("a",2)])});Reject(b with {Encounter=new(NearbyEnemies:[new("a",double.NaN)])});
        });
        check("벌컨 블리츠: 원래 적 최대 체력·선형 반경·시야·받는 적 방어력",()=>{
            var b=B("Jat Kittag");var mod=catalog.Mods.Single(m=>m.EnglishName=="Vulcan Blitz");b.Slots[0]=new(mod.Id,mod.MaxRank);
            b=b with {Scenario=new(Enemy:new(100,0)),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("half",3,Defense:new(1e9,0)),new("wall",-3,Defense:new(1e9,0)),new("edge",6,Defense:new(1e9,0)),new("armor",0,3,Defense:new(1e9,2700))],BlockedSight:[new(0,2)])};
            var r=Run(b,1);Near(r.Enemies![1].DirectDamage,180);Near(r.Enemies[2].DirectDamage,0);Near(r.Enemies[3].DirectDamage,0);Near(r.Enemies[4].DirectDamage,18);
            Near(r.DirectDamage,100);Yes(r.Events.Count(e=>e.Kind=="deathModExplosion")==3);
        });
        check("엔트로피 디토네이션: 지상 마무리 처치만·1000+20%체력·10m·시야",()=>{
            var b=B("Obex");var mod=catalog.Mods.Single(m=>m.EnglishName=="Entropy Detonation");b.Slots[0]=new(mod.Id,mod.MaxRank);var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var stance=stances.ForWeapon(w).First();
            b=b with {Scenario=new(Enemy:new(100,0)),Stance=new(stance.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,stance.IsDefault?null:stance.Id),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("edge",10,Defense:new(1e9,0)),new("blocked",-1,Defense:new(1e9,0))],BlockedSight:[new(0,2)])};
            var r=Run(b,1);Near(r.Enemies![1].DirectDamage,1020);Near(r.Enemies[2].DirectDamage,0);Near(r.Enemies[1].StatusTypes,0);
            var normal=Run(b with {Stance=null,Scenario=new(Enemy:new(1,0))},1);Near(normal.Enemies![1].DirectDamage,0);
        });
        check("시야 설정: 대칭·저장 복원·없는 대상 쌍 거부",()=>{
            var b=B() with {Encounter=new(NearbyEnemies:[new("x",1)],BlockedSight:[new(0,1)])};b=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;
            Yes(!CombatScene.HasSight(b.Encounter,1,0));Yes(CombatScene.HasSight(b.Encounter,0,0));
            try{Run(b with {Encounter=b.Encounter! with {BlockedSight=[new(0,2)]}},1);}catch(ArgumentException){return;}throw new Exception("없는 시야 대상을 허용함");
        });
        check("엑소디아 포스: 플레이어 중심6m·대상 중심과 구분·자체 상태 없음",()=>{
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var b=B() with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,Enemy:new(1e9,0)),Melee=new(Distance:2,Exodia:new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Force").Id,3)),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("center",0,-2),new("edge",0,4)])};
            var mod=catalog.Mods.Single(m=>m.EnglishName=="Weeping Wounds");b.Slots[0]=new(mod.Id,mod.MaxRank);
            var r=Run(b,256);Yes(r.Enemies![1].DirectDamage>0);Near(r.Enemies[1].StatusDamage,0);Near(r.Enemies[1].StatusTypes,0);Near(r.Enemies[2].DirectDamage,0);
        });
        check("볼러타일 퀵 리턴: 폭발 반경+3m·일반 사거리 모드 제외·경계 밖 미명중",()=>{
            var b=B("Glaive Prime");b=b with {Melee=new(Attack:"Charged Throw Recall Explosion",Distance:7.8)};var mod=catalog.Mods.Single(m=>m.EnglishName=="Volatile Quick Return");b.Slots[0]=new(mod.Id,mod.MaxRank);
            var c=MeleeEngine.Calculate(catalog,b);Near(c.Melee!.AreaRadius!.Value,7.8);Yes(c.Melee.HitConnects);
            var reach=catalog.Mods.Single(m=>m.EnglishName=="Reach");b.Slots[1]=new(reach.Id,reach.MaxRank);Near(MeleeEngine.Calculate(catalog,b).Melee!.AreaRadius!.Value,7.8);
            Yes(!MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=7.801}}).Melee!.HitConnects);
        });
        check("인카논 슬램 반경: 시즈믹 슬램100%·감쇠 좌표 확대·피해 모드와 독립",()=>{
            var b=B("Magistar");var p=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("SlamRadiusPercent"));b=b with {Melee=new(Attack:"Slam Attack")};
            var plain=MeleeEngine.Calculate(catalog,b);var radius=plain.Melee!.AreaRadius!.Value;
            var expanded=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Evolutions=[p.Id],Distance=radius}});
            var half=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=radius/2}});
            Near(expanded.Melee!.AreaRadius!.Value,2*radius);Near(expanded.ExpectedHit,half.ExpectedHit);
        });
        check("일반 슬램 다중 대상: 각 거리 감쇠·반경 경계·같은 입력의 주변 명중",()=>{
            var b=B("Nikana Prime");b=b with {Melee=new(Attack:"Slam Attack"),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("half",3),new("edge",6),new("outside",6.001)])};
            var r=Run(b,2048);Near(r.Enemies![3].DirectDamage,0);Yes(r.Enemies[1].DirectDamage>0&&r.Enemies[2].DirectDamage>0);
            var plain=MeleeEngine.Calculate(catalog,b);Yes(Math.Abs(r.Enemies[1].DirectDamage-plain.ExpectedHit*.75)<4*r.Enemies[1].Damage.StandardError);Yes(Math.Abs(r.Enemies[2].DirectDamage-plain.ExpectedHit*.5)<4*r.Enemies[2].Damage.StandardError);
        });
        check("범위 강공격: 주변 적마다 콤보를 재소모하지 않음",()=>{
            var b=B("Glaive Prime");b=b with {Scenario=new(Combo:12,Enemy:new(1e9,0)),Melee=new(Attack:"Charged Throw Recall Explosion"),Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("b",1),new("c",2)])};
            var r=Run(b,1);Yes(r.Enemies!.All(e=>e.DirectDamage>0));Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,220);
        });
        check("전기 상태:3m 범위 틱·받은 적에 상태 복제 없음·3m 밖 제외",()=>{
            var b=B("Prova");var ww=catalog.Mods.Single(m=>m.EnglishName=="Weeping Wounds");b.Slots[0]=new(ww.Id,ww.MaxRank);b=b with {Scenario=b.Scenario with {Combo=12},Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("edge",3),new("outside",-3.001)])};
            var r=Run(b,128);Yes(r.StatusDamage>0);Near(r.Enemies![1].StatusDamage,r.StatusDamage);Near(r.Enemies[1].DirectDamage,0);Near(r.Enemies[1].StatusTypes,0);Near(r.Enemies[2].StatusDamage,0);
        });
        check("폭발 상태: 처음 즉사한 적의 새 폭발도5m 범위·300%·원래 대상에 중복 없음",()=>{
            var b=B("Glaive Prime");b=b with {Scenario=b.Scenario with {Enemy=new(1,0)},Melee=b.Melee! with {Attack="Charged Throw Recall Explosion"},Encounter=new(EncounterMode.FirstHit,NearbyEnemies:[new("edge",5,Defense:new(1e9,0)),new("outside",-5.001,Defense:new(1e9,0))])};
            var expected=MeleeEngine.Calculate(catalog,b);var blast=expected.Melee!.StatusDamage.Single(s=>s.Type==DamageType.Blast);
            var r=Run(b,2048);Near(r.DirectDamage,1);Near(r.StatusDamage,0);Near(r.Enemies![2].StatusDamage,0);
            var mean=10*blast.DamagePerTick*blast.Chance;Yes(Math.Abs(r.Enemies[1].StatusDamage-mean)<4*r.Enemies[1].Damage.StandardError);Yes(r.Enemies[1].StatusDamage>0);
        });
        check("가스 반경: 원문3m·10중첩6m·중간 산술 충돌은 확정값으로 숨기지 않음",()=>{
            Near(StatusAreaRules.GasRadius(1).Minimum,3);Near(StatusAreaRules.GasRadius(10).Minimum,6);Yes(StatusAreaRules.GasReaches(5,4));Yes(!StatusAreaRules.GasReaches(5,5));
            try{StatusAreaRules.GasReaches(5,4.3);}catch(CombatRuleUnavailableException){return;}throw new Exception("가스 경계의 출처 불확실성을 놓침");
        });
        check("가스 실제 사후 피해: 숙주 처치 후에도 주변 적에 남은 틱·상태 복제 없음",()=>{
            var b=B("Dual Ichor");var names=new[]{"Molten Impact","Volcanic Edge","Weeping Wounds","Melee Prowess","Drifting Contact","Proton Snap"};
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}
            b=b with {Scenario=new(Combo:12,Enemy:new(1400,0)),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"WallLatchTwoSeconds")],NearbyEnemies:[new("near",3,Defense:new(1e9,0))])};
            var r=Run(b,1);var death=r.Enemies![0].Ttk.Median;Yes(death is >0 and <10);Yes(r.Events.Any(e=>e.Time>death&&e.Description.Contains("가스 범위 피해 · 대상1에서")));Yes(r.Enemies[1].StatusDamage>0);Near(r.Enemies[1].StatusTypes,0);
        });
    }
}
