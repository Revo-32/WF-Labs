using TennoForge.Core;

internal static class WeaponComponentChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("공격 성분 회귀 조건 불일치");}
        void Near(double actual,double expected){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception($"{actual} != {expected}");}
        Build B(string name="Vitrica",params string[] names)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];
            for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,slots,new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        }
        check("근접 기본 공속: 표시 반올림을 X/60으로 복원·공속 버프는 반올림하지 않음",()=>{
            Near(MeleeAttackSpeed.FromWiki(.833),50d/60);Near(MeleeAttackSpeed.FromWiki(1.08),65d/60);Near(MeleeAttackSpeed.FromWiki(1.167),70d/60);
            var b=B("Nikana Prime","Fury");Near(MeleeEngine.Calculate(catalog,b).AttackSpeed,65d/60*1.3);
            var neutral=StanceEngine.Calculate(catalog,B("Nikana Prime") with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice")},stances);Near(neutral.Duration!.Value,2.4);
        });
        check("고유 기믹 자료실: 원본 검토 목록을 보존하며 현재 지원·다음 단계·출처 공백을 구분",()=>{
            var rules=MeleeRuleCatalog.Load();var lecta=rules.Weapons.Single(w=>w.Name=="Secura Lecta");var coverage=MeleeMechanicCoverage.For(lecta);
            Yes(lecta.PendingMechanics!.Length>0&&coverage.Implemented.Single().Contains("50%")&&coverage.Remaining.Length==0&&coverage.SourceIssues.Length>0);
            var zen=MeleeMechanicCoverage.For(rules.Weapons.Single(w=>w.Name=="Zenistar"));Yes(zen.Implemented.Length>0&&zen.Remaining.Length==0&&zen.Deferred?.Any(s=>s.Contains("시리우스")&&s.Contains("내비게이터"))==true&&zen.SourceIssues.Any(s=>s.Contains("충돌")));
            var sancti=MeleeMechanicCoverage.For(rules.Weapons.Single(w=>w.Name=="Sancti Magistar"));Yes(sancti.Implemented.Length>0&&sancti.SourceIssues.Length>0);
        });
        check("제니스타: 컨오1040%를 오라에 적용하지 않음, 접촉 원문 충돌은 명시",()=>{
            var b=B("Zenistar","Condition Overload");b=b with {Scenario=b.Scenario with {ExistingStatusTypes=4},Melee=new(Attack:"Disc Aura")};
            Near(MeleeEngine.Calculate(catalog,b).Damage.Values.Sum(),50);Near(MeleeEngine.Calculate(catalog,b with {Melee=new(Attack:"Disc Explosion")}).Damage.Values.Sum(),350);
            try{MeleeEngine.Calculate(catalog,b with {Melee=new(Attack:"Disc Impact")});throw new Exception("서로 다른 CO 원문을 확정값으로 사용");}catch(CombatRuleUnavailableException){}
            Near(MeleeEngine.Calculate(catalog,b with {Scenario=b.Scenario with {ExistingStatusTypes=0},Melee=new(Attack:"Disc Impact")}).Damage.Values.Sum(),75);
        });
        check("제니스타 원반: 강공격 피해·콤보·힐링 리턴 제외, 일반 칼날은 별도",()=>{
            var b=B("Zenistar","Killing Blow","Healing Return");b=b with {Scenario=new(Combo:12,ExistingStatusTypes:4,Enemy:new(1e9,0)),Melee=new(Attack:"Disc Aura",TargetIndex:4)};
            var c=MeleeEngine.Calculate(catalog,b);Near(c.Damage.Values.Sum(),50);Near(c.Melee!.Healing,0);Near(c.Melee.Brackets!.FollowThroughMultiplier,1);Yes(!MeleeEngine.AttackPolicy(b.WeaponId,"Disc Aura").HitCombo);
            var blade=MeleeEngine.Calculate(catalog,b with {Melee=new(Attack:"Attacks While Disc Deployed",Conditions:new(){["Invisible"]=1})});Near(blade.Damage.Values.Sum(),130);Near(blade.Melee!.Healing,44);Yes(blade.Melee.RevealsOnHit);
        });
        check("제니스타 원반 반경: 리치·공중 모투스 적용, 방사형 냉기·천공 취약 제외",()=>{
            var b=B("Zenistar","Primed Reach","Motus Impact");b=b with {Melee=new(Attack:"Disc Aura",Distance:9,Conditions:new(){["Airborne"]=1},Statuses:new(){[DamageType.Puncture]=5,[DamageType.Cold]=10})};
            var c=MeleeEngine.Calculate(catalog,b);Near(c.Melee!.Range,9);Near(c.Damage.Values.Sum(),50);Near(c.CriticalChance,.1);Near(c.CriticalMultiplier,2);
            var miss=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=9.01}});Yes(!miss.Melee!.HitConnects);Near(miss.ExpectedHit,0);
        });
        check("마무리 공속: 무기 기본값 제외·+50% 상한·시큐라 렉타 선천 보너스",()=>{
            StanceCalculation Fin(Build b)
            {
                var s=stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));
                return StanceEngine.Calculate(catalog,b with {Stance=new(s.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,s.IsDefault?null:s.Id),Melee=new(ComboDurationOverride:3)},stances);
            }
            var bare=Fin(B("Lecta"));Near(bare.Stats.AttackSpeed,1);Near(bare.Duration!.Value,3);
            var fury=Fin(B("Lecta","Fury"));Near(fury.Stats.AttackSpeed,1.3);Near(fury.Duration!.Value,3/1.3);
            var quick=Fin(B("Lecta","Primed Fury"));Near(quick.Stats.AttackSpeed,1.5);Near(quick.Duration!.Value,2);
            var secura=Fin(B("Secura Lecta"));Near(secura.Stats.AttackSpeed,1.5);Near(secura.Duration!.Value,2);Yes(secura.Hits.All(h=>h.Calculation!.AttackSpeed==1.5));
            Near(MeleeEngine.Calculate(catalog,B("Secura Lecta")).AttackSpeed,1.25);
        });
        check("히루도 고정 상태 흡혈: 듀플리케이트 재추첨·은신 접촉 대기도 기대값에 반영",()=>{
            var b=B("Hirudo");var id=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Duplicate").Id;b=b with {Arcanes=new(Melee:new(id,5))};Near(MeleeEngine.Calculate(catalog,b).Melee!.Healing,130*.05*(.3*3+.3*.3*3));
            b=b with {Melee=new(Conditions:new(){["Invisible"]=1})};Near(MeleeEngine.Calculate(catalog,b).Melee!.Healing,130*.05*(.3*10+.3*.3*3));
        });
        check("히루도2종: 치명타마다 체력5%·최대5중첩, 오버가드에도 피해5% 흡혈",()=>{
            foreach(var name in new[]{"Hirudo","Coda Hirudo"})
            {
                var b=B(name,"Blood Rush");b=b with {Scenario=new(Combo:12,Enemy:new(1e9,2700)),Melee=new(Overguard:1e8)};
                var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.CriticalHealing>0);Near(t.Points.Last().InvigoratedStacks,5);
                Near(t.CriticalHealing!.Value,t.Events.Where(e=>e.Kind=="hit").Sum(e=>e.Amount)*.05);Yes(t.Events.Where(e=>e.Kind=="invigorated").All(e=>e.Amount<=.25));
                Near(MeleeEngine.Calculate(catalog,b).Melee!.Effects["MovementSpeed"],.05);
            }
        });
        check("히루도: 처치한 치명타도 흡혈·인비거레이티드 발동",()=>{
            var b=B("Hirudo","Blood Rush");b=b with {Scenario=new(Combo:12,Enemy:new(1,0))};var t=CombatTimeline.Calculate(catalog,b,stances,1);
            Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="hit")==1&&t.Events.Any(e=>e.Kind=="kill")&&t.CriticalHealing>0);Near(t.Points.Last().InvigoratedStacks,1);
        });
        check("히루도: 은신 피해를 치명타로 오인하지 않음",()=>{
            var b=B("Hirudo");b=b with {Scenario=new(BaseInterval:60,Enemy:new(1e9,0)),Melee=new(Conditions:new(){["Invisible"]=1})};var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);
            var hit=t.Events.Single(e=>e.Kind=="hit");var isCritical=hit.Amount/130>=10;Near(t.Points.Last().InvigoratedStacks,isCritical?1:0);Near(t.CriticalHealing!.Value,isCritical?hit.Amount*.05:0);
        });
        check("히루도: 마지막 치명타 후15초가 지나면 모든 체력 중첩 만료",()=>{
            var b=B("Hirudo","Blood Rush");b=b with {Scenario=new(Combo:12,Enemy:new(1e9,0)),Stance=new("Sparring","Heavy",stances.Revision),Melee=new(ComboDurationOverride:20),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,1,1,1,1])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Points.Any(p=>p.InvigoratedStacks>0));Near(t.Points.Last().InvigoratedStacks,0);
        });
        check("프로톤 스냅: 위키 갈라틴 예제 20%×(1+90%+40%)+50%=96%",()=>{
            var b=B("Galatine","Melee Prowess","Proton Snap","Weeping Wounds");b=b with {Scenario=b.Scenario with {Combo=2},Melee=new(Conditions:new(){["ProtonSnap"]=1})};Near(MeleeEngine.Calculate(catalog,b).StatusChance,.96);
        });
        check("프로톤 스냅: 스탠스 상태 배율2 이후에50% 가산",()=>{
            var b=B("Nikana Prime","Proton Snap","Weeping Wounds");b=b with {Scenario=b.Scenario with {Combo=2},Melee=new(Conditions:new(){["ProtonSnap"]=1}),Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Blind Justice"),Stance=new("Blind Justice","Forward",stances.Revision)};
            var c=StanceEngine.Calculate(catalog,b,stances);var hit=c.Hits.First(h=>h.SourceHit!.StatusMultiplier==2);Near(hit.Calculation!.StatusChance,.28*1.4*2+.5);
            Yes(hit.Calculation.Melee!.Contributions!.Any(c=>c.Effect=="PostStanceStatus"&&c.Bracket.Contains("스탠스")));
        });
        check("프로톤 스냅 시간 전이: 2초 유지 완료 시점부터 랭크0은5초, 만료·재발동",()=>{
            var b=B("Nikana Prime","Proton Snap");b.Slots[0]=b.Slots[0]! with {Rank=0};b=b with {Scenario=b.Scenario with {BaseInterval=60},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"WallLatchTwoSeconds"),new(7,"WallLatchTwoSeconds")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var buffs=t.Events.Where(e=>e.Kind=="buff").ToArray();Yes(buffs.Length==2&&buffs[0].Time==1&&buffs[1].Time==7&&buffs.All(e=>e.Description.Contains("5초")));
            var baseline=t.Points.Single(p=>p.Time==.5).SnapshotDps;Near(t.Points.Single(p=>p.Time==1).SnapshotDps,baseline*1.25);Near(t.Points.Single(p=>p.Time==6).SnapshotDps,baseline);Near(t.Points.Single(p=>p.Time==7).SnapshotDps,baseline*1.25);
        });
        check("비트리카: 유리화 원래 대상 상확100%, 주변 대상33% 분리",()=>{
            var b=B();var e=VitricaMechanic.Calculate(catalog,b,false);Near(e.GlassedTarget.StatusChance,1);Near(e.NearbyTarget.StatusChance,.33);
            Near(e.GlassedTarget.ExpectedHit,999*(1+.23*1.3));Near(e.NearbyTarget.ExpectedHit,e.GlassedTarget.ExpectedHit);
            var selected=MeleeEngine.Calculate(catalog,b with {Melee=new(Attack:"Glass Explosion")});Near(selected.StatusChance,1);Near(selected.ExpectedHit,e.GlassedTarget.ExpectedHit);
        });
        check("비트리카: 원래 대상만 물리·원소 모드, 주변 대상만 일반 상태 확률 모드",()=>{
            var b=B("Vitrica","Pressure Point","Melee Prowess","Buzz Kill","Fever Strike");var e=VitricaMechanic.Calculate(catalog,b,false);
            Near(e.GlassedTarget.StatusChance,1);Near(e.NearbyTarget.StatusChance,.33*1.9);
            Near(e.GlassedTarget.Damage[DamageType.Slash],399.6*2.2*2.2);Near(e.GlassedTarget.Damage[DamageType.Toxin],999*2.2*.9);
            Near(e.NearbyTarget.Damage.Values.Sum(),999*2.2);Yes(!e.NearbyTarget.Damage.ContainsKey(DamageType.Toxin));
        });
        check("비트리카: 고정 추가 상확은 원래 대상에도 적용, 사거리 모드는 폭발 반경 미적용",()=>{
            var b=B("Vitrica","Proton Snap","Primed Reach");b=b with {Melee=new(Conditions:new(){["ProtonSnap"]=1})};var e=VitricaMechanic.Calculate(catalog,b,false,3);
            Near(e.GlassedTarget.StatusChance,1.5);Near(e.NearbyTarget.StatusChance,.83);Near(e.Radius,6);
            Near(e.NearbyTarget.Melee!.Brackets!.RangeFalloff,.55);Near(e.GlassedTarget.Melee!.Brackets!.RangeFalloff,1);
        });
        check("비트리카: 강 슬램 파동의 콤보와 강공격 치확이 유리 폭발에 적용",()=>{
            var b=B("Vitrica","True Steel","Killing Blow","Condition Overload");b=b with {Scenario=new(Combo:5,Enemy:new(1e9,0)),Melee=new(Statuses:new(){[DamageType.Viral]=1,[DamageType.Corrosive]=1})};
            var e=VitricaMechanic.Calculate(catalog,b,true);Near(e.GlassedTarget.CriticalChance,.23*(1+2*1.2));Near(e.GlassedTarget.Damage.Values.Sum(),999*2.2*5);
            Near(e.NearbyTarget.Damage.Values.Sum(),999*2.2*5);Near(e.GlassedTarget.Melee!.Brackets!.AdditiveConditionOverload,0);
        });
        check("폭발 반경 밖: 비트리카 주변 피해0, 원래 대상의 독립 성분은 그대로",()=>{
            var e=VitricaMechanic.Calculate(catalog,B(),false,6.001);Near(e.NearbyTarget.ExpectedHit,0);Yes(!e.NearbyTarget.Melee!.HitConnects&&e.GlassedTarget.ExpectedHit>0);
            Near(DpsEquation.From(e.NearbyTarget).Evaluate(),0);Near(VitricaMechanic.Calculate(catalog,B(),false,6).NearbyTarget.Melee!.Brackets!.RangeFalloff,.1);
        });
        check("범위 밖 슬램은 강제 충격·콤보·회복·명중 버프를 만들지 않음",()=>{
            var b=B("Nikana Prime","Healing Return");b=b with {Melee=new(Attack:"Slam Attack",Distance:100),Scenario=new(BaseInterval:1,Enemy:new(1e9,0))};
            var c=MeleeEngine.Calculate(catalog,b);Near(c.ExpectedHit,0);Near(c.Melee!.Healing,0);Yes(c.Melee.ForcedProcs.Length==0&&!c.Melee.HitConnects);
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Any(e=>e.Kind=="miss")&&!t.Events.Any(e=>e.Kind is "proc" or "hit" or "buff"));Near(t.Points.Last().ComboPoints,0);Near(t.DirectDamage+t.StatusDamage,0);
        });
    }
}
