using TennoForge.Core;

internal static class EncounterChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var weapon=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");
        void Yes(bool x){if(!x)throw new Exception("교전 검사 조건 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(EncounterMode mode,params string[] names)
        {
            var slots=new ModSlot?[8];for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(weapon.Id,slots,new(Enemy:new(10000,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(weapon.Id,null) with {StanceEquipped=false},Melee:new(),Encounter:new(mode));
        }
        EncounterResult C(Build b,int n=128)=>EncounterEngine.CalculateOrdered(catalog,b,stances,n);
        check("교전 첫 타격: 저장된 프라이머 상태를 자동 합산하지 않음",()=>{
            var b=B(EncounterMode.FirstHit,"Pressure Point","Condition Overload");b=b with {Scenario=b.Scenario with {ExistingStatusTypes=5},Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=10}}};
            var c=C(b);Near(c.Damage.Low,198*2.2*(1+.28*1.4));Near(c.Damage.High,c.Damage.Low);Near(c.EndStatusTypes.High,0);
        });
        check("프라이머: 3종 CO는 PP와 가산, 같은 속성 10중첩도 1종",()=>{
            var b=B(EncounterMode.Primer,"Pressure Point","Condition Overload");b=b with {Melee=new(Statuses:new(){[DamageType.Slash]=10,[DamageType.Impact]=1,[DamageType.Radiation]=1})};
            var c=C(b);Near(c.Damage.Low,198*4.6*(1+.28*1.4));Near(c.EndStatusTypes.High,3);
        });
        check("교전 자기 예열: 첫 타격에 자신이 건 컨오가 소급되지 않음",()=>{
            var b=B(EncounterMode.FiveSeconds,"Condition Overload");var c=C(b);Yes(c.Unsupported.Length==0);Near(c.Samples[0].Damage.Low,198*(1+.28*1.4));
            Yes(c.Samples[^1].StatusTypes.Low>0);Yes(c.Damage.Low>C(b with {Slots=new ModSlot?[8]}).Damage.Low);
        });
        check("교전 5초·10초: 누적 피해를 구간 길이로 나눈 평균",()=>{
            var b=B(EncounterMode.FiveSeconds,"Condition Overload","Fever Strike","North Wind");var five=C(b);var ten=C(b with {Encounter=new(EncounterMode.TenSeconds)});
            Yes(five.Unsupported.Length==0&&ten.Unsupported.Length==0);Near(five.AverageDps!.Low,five.Damage.Low/5);Near(ten.AverageDps!.Low,ten.Damage.Low/10);Yes(ten.Damage.Low>five.Damage.Low);
        });
        check("교전 상태 만료: 오래 기다려도 모든 상태가 영구 유지되지 않음",()=>{
            var b=B(EncounterMode.TenSeconds,"Condition Overload");b=b with {Scenario=b.Scenario with {BaseInterval=20}};var c=C(b);Near(c.EndStatusTypes.High,0);Yes(c.Samples.Length==1);
        });
        check("화염 방깎: 0.5초마다 15·30·40·50%, 지속시간 배율 반영",()=>{
            foreach(var (time,value) in new[]{(0d,0d),(.49,0d),(.5,.15),(1,.3),(1.5,.4),(2,.5)})Near(EncounterEngine.HeatStrip(time),value);
            Near(EncounterEngine.HeatStrip(2,2),.3);Near(EncounterEngine.HeatStrip(4,2),.5);
        });
        check("교전 방어력 감소: 충격 상태 발동 없이 타격당 기본 방어력 6 감소",()=>{
            var b=B(EncounterMode.FiveSeconds,"Shattering Impact");b=b with {Scenario=b.Scenario with {Enemy=new(10000,1000)},Encounter=new(EncounterMode.FiveSeconds,100)};var c=C(b,16);
            Yes(c.Unsupported.Length==0);Near(c.Samples[0].Armor.High,1000);Near(c.Samples[1].Armor.High,940);Near(c.Samples[2].Armor.High,880);
        });
        check("교전 지원 범위: 방깎 입력 누락·미구현 무기 버프를 0으로 계산하지 않음",()=>{
            Yes(C(B(EncounterMode.FiveSeconds,"Shattering Impact"),1).Unsupported.Any(s=>s.Contains("기본 방어력")));
            var lesion=catalog.Weapons.Single(w=>w.EnglishName=="Lesion");var b=B(EncounterMode.FiveSeconds);b=b with {WeaponId=lesion.Id,Capacity=CapacityCatalog.Load().Default(lesion.Id,null)};
            Yes(C(b,1).Unsupported.Any(s=>s.Contains("고유 버프")));
        });
        check("교전 위키 타격 시각 누락: 균등 간격을 만들어 넣지 않음",()=>{
            var s=stances.ForWeapon(weapon).Single(s=>s.Id=="Decisive Judgement");var combo=s.Combos.Single(c=>c.Key=="Forward");
            var b=B(EncounterMode.StanceCycle);b=b with {Stance=new(s.Id,combo.Key,stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,s.Id)};var c=C(b,16);
            if(c.Unsupported.Length>0)throw new Exception(string.Join(" · ",c.Unsupported));Yes(c.Samples.All(p=>p.Earliest==0&&p.Latest==c.Seconds));Near(c.AverageDps!.Low,c.Damage.Low/c.Seconds!.Value);
        });
        check("교전 반복 실행 재현성 · 취소",()=>{
            var b=B(EncounterMode.FiveSeconds,"Condition Overload");Near(C(b,32).Damage.Low,C(b,32).Damage.Low);
            using var token=new CancellationTokenSource();token.Cancel();try{EncounterEngine.CalculateOrdered(catalog,b,stances,128,token.Token);throw new Exception("취소 누락");}catch(OperationCanceledException){}
        });
        check("교전 콤보 누적: 20회 명중 뒤 다음 타격부터 BR에 반영",()=>{
            var b=B(EncounterMode.FiveSeconds,"Blood Rush") with {Scenario=new(BaseInterval:.2,Enemy:new(10000,0))};var c=C(b,32);
            Near(c.Samples[19].Combo,1);Near(c.Samples[20].Combo,2);Yes(c.Damage.Low>C(b with {Slots=new ModSlot?[8]},32).Damage.Low);
        });
        check("낮은 상태 확률: 한 번 명중했다고 IPS 3종을 모두 적용하지 않음",()=>{
            var b=B(EncounterMode.FiveSeconds) with {Scenario=new(BaseInterval:20,Enemy:new(10000,0))};var c=C(b,4096);
            Yes(Math.Abs(c.EndStatusTypes.High-.28)<.03);Yes(c.EndStatusTypes.High<1);
        });
        check("위키 Blind Justice 정지: 강제 충격 1타·베기 7타·넉다운 8타, 타격별 CO",()=>{
            var b=B(EncounterMode.StanceCycle,"Condition Overload");b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,"Blind Justice")};
            var stance=StanceEngine.Calculate(catalog,b,stances);var hits=stance.Hits.SelectMany(h=>Enumerable.Repeat(h,h.Count)).ToArray();
            Yes(hits.Length==8);Yes(hits[0].Calculation!.Melee!.ForcedProcs.Contains("Impact"));Yes(hits[6].Calculation!.Melee!.ForcedProcs.Contains("Slash"));Yes(hits[7].Calculation!.Melee!.ForcedProcs.Contains("Knockdown"));
            var factors=new[]{1d,1,.5,.5,.5,.5,.5,.25};for(var i=0;i<8;i++){Near(hits[i].CoFactor,factors[i]);Yes(hits[i].CoVerified);}
            var c=C(b,64);if(c.Unsupported.Length>0)throw new Exception(string.Join(" · ",c.Unsupported));
            Near(c.Samples[0].StatusTypes.Low,0);Yes(c.Samples[1].StatusTypes.Low>=1);Yes(c.Samples[7].StatusTypes.Low>=2);
            Near(c.Samples[0].Damage.Low,C(b with {Encounter=new(EncounterMode.FirstHit)},1).Damage.Low);
            Yes(c.Damage.Low>C(b with {Slots=new ModSlot?[8]},64).Damage.Low);
        });
        check("Blind Justice 프라이머: 첫 타격에 콤보 평균 58%를 곱하지 않음",()=>{
            var b=B(EncounterMode.Primer,"Condition Overload");b=b with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,"Blind Justice"),Melee=new(Statuses:new(){[DamageType.Slash]=1})};
            var c=C(b,1);Yes(c.Unsupported.Length==0);Near(c.Damage.Low,(9.9+9.9+178.2*2)*3*1.8*(1+.28*1.4));
        });
        check("위키 인용 타격별 CO: 자료 서명·개수·반복 타격 값 검증",()=>{
            var source=StanceHitCoCatalog.Load();Yes(source.Rules.Length>=140);Yes(source.Rules.Select(r=>(r.StanceId,r.Combo)).Distinct().Count()==source.Rules.Length);
            foreach(var r in source.Rules){var combo=stances.Stances.Single(s=>s.Id==r.StanceId).Combos.Single(c=>c.Key==r.Combo);Yes(r.Factors.Length==combo.Hits.Count(h=>h.Multiplier>0&&h.AttackType!="Slam"));Yes(r.Factors.All(f=>double.IsFinite(f)&&f>0));}
        });
        check("CO 숨은 상태: 넉다운·띄우기는 1종, 마이크로웨이브는 별도",()=>{
            var b=B(EncounterMode.Primer,"Condition Overload") with {Melee=new(Conditions:new(){["Knockdown"]=1,["Lifted"]=1,["Microwave"]=1})};Near(TargetStatusRules.Count(b),2);
            Near(TargetStatusRules.Count(b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=5}}}),3);
        });
        check("방어력 상한: 원래 레벨 배율이 없으면 범위, 완전 제거 시 0",()=>{
            var b=B(EncounterMode.FiveSeconds,"Shattering Impact") with {Scenario=new(Enemy:new(10000,2700)),Encounter=new(EncounterMode.FiveSeconds,12)};var c=C(b,1);
            Near(c.Samples[1].Armor.Low,1350);Near(c.Samples[1].Armor.High,2700);Near(c.Samples[2].Armor.Low,0);Near(c.Samples[2].Armor.High,0);
        });
        check("v0.6.0 저장 빌드: 슬롯·상태 유지, 규칙 버전 명시적 변환",()=>{
            var old=B(EncounterMode.Primer,"Condition Overload") with {Melee=new(Statuses:new(){[DamageType.Slash]=2},RulesVersion:"melee-conditional-0.6.0")};
            var upgraded=BuildMigration.ToMelee(catalog,old);Yes(upgraded.Melee!.RulesVersion==MeleeEngine.RulesVersion);Yes(upgraded.Slots.SequenceEqual(old.Slots));Yes(upgraded.Melee.Statuses![DamageType.Slash]==2);Yes(C(upgraded).Damage.Low>0);
        });
        check("교전 기본 방어력: 무한·음수 입력 거부",()=>{
            foreach(var a in new[]{double.NaN,double.PositiveInfinity,-1}){try{C(B(EncounterMode.FiveSeconds,"Shattering Impact") with {Encounter=new(EncounterMode.FiveSeconds,a)});throw new Exception("유한값 검사 누락");}catch(ArgumentException){}}
        });
        check("회귀: 강철의 길 엑시무스·5초·Blind Justice·컨오 계산",()=>{
            var b=B(EncounterMode.FiveSeconds,"Pressure Point","Blood Rush","Organ Shatter","Fury","Virulent Scourge","Vicious Frost","Weeping Wounds","Condition Overload");
            b=b with {Scenario=b.Scenario with {Enemy=new(2132240,2700)},Melee=b.Melee! with {Overguard=365709,Faction="Grineer"},Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,"Blind Justice")};
            var result=EncounterEngine.Calculate(catalog,b,stances,128);
            if(result.Unsupported.Length>0)throw new Exception(string.Join(" · ",result.Unsupported));
            Yes(result.AverageDps!.Low>0&&result.Samples.Length>8);
            Yes(result.Samples.All(s=>s.Health is not null&&s.Overguard is not null));
            Yes(result.Samples[^1].Damage.High<=365709+2132240);
        });
        check("스탠스 방어층: 오버가드 잔존 중 바이러스·방어력·팩션 약점 제외",()=>{
            var b=B(EncounterMode.StanceCycle,"Fever Strike","North Wind","Condition Overload") with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,"Blind Justice"),Melee=new(Overguard:1e9),Scenario=new(Enemy:new(10000,2700))};
            var result=C(b,64);Yes(result.Unsupported.Length==0);Near(result.Samples[^1].Health!.Low,10000);
            Near(result.Samples[^1].Overguard!.High+result.Damage.Low,1e9);
            var noArmor=C(b with {Scenario=b.Scenario with {Enemy=new(10000,0)}},64);Near(result.Damage.Low,noArmor.Damage.Low);
        });
        check("스탠스 방어층: 파괴 이후 체력으로 전이하고 전체 피해를 생존량에서 제한",()=>{
            var b=B(EncounterMode.StanceCycle,"Pressure Point") with {Stance=new("Blind Justice","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(weapon.Id,"Blind Justice"),Melee=new(Overguard:10),Scenario=new(Enemy:new(10,0))};
            var result=C(b,16);Yes(result.Unsupported.Length==0);Near(result.Damage.Low,20);Near(result.Damage.High,20);Near(result.Samples[^1].Health!.High,0);Near(result.Samples[^1].Overguard!.High,0);
        });
    }
}
