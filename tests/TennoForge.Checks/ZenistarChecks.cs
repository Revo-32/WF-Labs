using System.Text.Json;
using TennoForge.Core;

internal static class ZenistarChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();var id=ZenistarDiscState.WeaponId;
        Build B()=>new(id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ZenistarDiscs:[new(0,.1,Retrieve:2,AuraTimes:[.5,1,1.5,2,3],WrathfulCriticalPercent:200)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b,double tolerance=1e-5){if(Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool value){if(!value)throw new Exception("제니스타 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 제니스타 입력 허용: "+text);}
        CombatTimelineResult Run(Build b,int trials=1){var r=CombatTimeline.Calculate(catalog,b,stances,trials);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("제니스타 수명:콤보당10초·100초 회수 버그·배치 원점 불확실 범위",()=>{
            var s=new ZenistarDiscState();var d=s.Launch(new(0,.5),12);Near(d.EarliestEnd,100);Near(d.LatestEnd,100.5);Yes(s.Active(1,99.9));Reject(()=>s.Active(1,100.1),"시점");Yes(!s.Active(1,100.5));
            var next=s.Launch(new(101,101.5,Disc:2,Retrieve:105),2);Near(next.EarliestEnd,105);Yes(s.Deployed(104)&&!s.Deployed(105));Reject(()=>s.Aura(2,101.2),"배치 전");
            Reject(()=>ZenistarDiscState.Validate([new(0,1,AuraTimes:[2,2])],1),"중복");
        });
        check("제니스타 오라:50Heat·상확모드·PP적용·KB/CO/콤보/힐링리턴 제외",()=>{
            var b=B();Mods(b,"Pressure Point","Killing Blow","Condition Overload","Melee Prowess","Healing Return");b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1}}};
            var c=ZenistarDiscState.Calculate(catalog,b);Near(c.Damage[DamageType.Heat],50*2.2);Near(c.StatusChance,.95);Near(c.CriticalChance,.1);Near(c.CriticalMultiplier,2);Yes(!c.Melee!.Policy!.HealingReturn&&!c.Melee.Policy.HitCombo);Near(c.Melee.ExtraDamage,0);
            Near(ZenistarDiscState.Calculate(catalog,b,"Disc Explosion").Damage[DamageType.Heat],350*2.2);
        });
        check("제니스타 래스풀:최종치확 가산·저장 보너스는 오라에만 적용",()=>{
            var b=B();Mods(b,"True Steel");var aura=ZenistarDiscState.Calculate(catalog,b,wrathfulCriticalPercent:200);Near(aura.CriticalChance,.1*2.2+2);Near(aura.ExpectedHit,50*(1+2.22));
            Near(ZenistarDiscState.Calculate(catalog,b,"Disc Explosion",wrathfulCriticalPercent:200).CriticalChance,.1*2.2);
        });
        check("제니스타 범위:프라임리치·공중모터스·스프링 중첩의 실시간 반경",()=>{
            var b=B();Mods(b,"Primed Reach","Motus Impact","Spring-Loaded Blade");var normal=ZenistarDiscState.Calculate(catalog,b,distance:9);Near(normal.Melee!.AreaRadius!.Value,7);Near(normal.Damage.Values.Sum(),0);
            var active=b with {Melee=b.Melee! with {Conditions=new(){["Airborne"]=1,["SpringStacks"]=2}}};var air=ZenistarDiscState.Calculate(catalog,active,distance:9);Yes(air.Melee!.AreaRadius>9&&air.Damage.Values.Sum()>0);
        });
        check("제니스타 전투:발사 콤보1회·배치폭발·지정오라만·회수 이후 제외",()=>{
            var b=B();var r=Run(b,512);Yes(r.Events.Count(e=>e.Kind=="zenistarLaunch")==1&&r.Events.Count(e=>e.Kind=="heavyCombo")==1&&r.Events.Count(e=>e.Kind=="zenistarAura")==3);Near(r.Points.First(p=>p.Time>=.2).ComboPoints,0);Near(r.DirectDamage,350*1.1+3*50*3.1,30);Yes(!r.HasSnapshotDps);
            var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.ZenistarDiscs![0].AuraTimes!.SequenceEqual(b.Encounter!.ZenistarDiscs![0].AuraTimes!));
        });
        check("제니스타 전투:지정하지 않은 오라 자동생성 없음·접촉 없는 폭발 범위",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {ZenistarDiscs=[new(0,.1)],NearbyEnemies=[new("edge",4,Defense:new(1e9,0)),new("outside",4.01,Defense:new(1e9,0))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="zenistarExplosion")==2&&!r.Events.Any(e=>e.Kind=="zenistarAura"||e.Kind=="zenistarImpact"));Near(r.Enemies![2].DirectDamage,0);Yes(r.Enemies[1].DirectDamage>0);
        });
        check("제니스타 칼날:배치 중130IPS·회수 후298HeatIPS·상태 전환에 따른 DPS",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {Combo=1,BaseInterval=60},Encounter=b.Encounter! with {ScriptedAttacksOnly=false,ZenistarDiscs=[new(.1,.2,Retrieve:2)]}};
            var r=Run(b);var before=r.Points.Last(p=>p.Time<.1);var deployed=r.Points.First(p=>p.Time>=.3);var after=r.Points.First(p=>p.Time>=2.1);Near(deployed.SnapshotDps/before.SnapshotDps,130d/298,.025);Near(after.SnapshotDps,before.SnapshotDps,.025);
        });
        check("제니스타 원반 전용:선택한 강공격 애니메이션 시간을 임의로 요구하지 않음",()=>{
            var b=B();var dream=catalog.Mods.Single(m=>m.EnglishName=="Dreamer's Wrath");b=b with {Melee=b.Melee! with {Attack="Heavy Attack",Tennokai=true,Exilus=new(dream.Id,dream.MaxRank)}};
            var r=Run(b);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,0);Near(r.Points.First(p=>p.Time>=.2).ComboPoints,220);
        });
        check("제니스타 회수 흡혈:시작 포함·완료 제외·오라 직접 피해의20%·상태 피해 제외",()=>{
            var b=B();Mods(b,"Life Strike");b=b with {Encounter=b.Encounter! with {ZenistarDiscs=[b.Encounter.ZenistarDiscs![0] with {RetrieveStarted=1}]}};
            var r=Run(b);var aura=r.Events.Where(e=>e.Kind=="zenistarAura"&&e.Time>=1&&e.Time<2).ToArray();
            Yes(aura.Length==2&&r.StatusDamage>0);Near(r.Recovery!.HeavyHealing!.Mean,aura.Sum(e=>e.Amount)*.2);
            Yes(r.Events.Where(e=>e.Kind=="heavyHealing").All(e=>e.Time>=1&&e.Time<2));
            var plain=Run(b with {Slots=new ModSlot?[8]});Near(r.DirectDamage,plain.DirectDamage);Near(r.StatusDamage,plain.StatusDamage);
            Near(r.Events.Where(e=>e.Kind=="heavyCombo").Sum(e=>e.Amount),220);
        });
        check("제니스타 회수 흡혈:흡혈이 오라에 강공격 피해·콤보·힐링리턴을 추가하지 않음",()=>{
            var b=B();Mods(b,"Life Strike","Killing Blow","Condition Overload","Healing Return");
            b=b with {Encounter=b.Encounter! with {ZenistarDiscs=[b.Encounter.ZenistarDiscs![0] with {RetrieveStarted=.5}]}};
            var r=Run(b);var plain=Run(b with {Slots=new ModSlot?[8]});Near(r.DirectDamage,plain.DirectDamage);
            Yes(r.Events.Where(e=>e.Kind=="healingReturn").All(e=>e.Time<.5));Near(r.Recovery!.HeavyHealing!.Mean,r.Events.Where(e=>e.Kind=="zenistarAura").Sum(e=>e.Amount)*.2);
            Near(r.Points.Last().ComboPoints,0);
        });
        check("제니스타 회수 흡혈:미지정 회수 애니메이션은 임의 생성하지 않고 영구 배치도 흡혈 없음",()=>{
            var b=B();Mods(b,"Life Strike");Reject(()=>Run(b),"시작 시각");
            b=b with {Encounter=b.Encounter! with {ZenistarDiscs=[new(0,.1,AuraTimes:[.5,1,1.5])]}};
            Near(Run(b).Recovery!.HeavyHealing!.Mean,0);
        });
        check("제니스타 회수 흡혈:오버가드·실드·체력 방어를 적용한 실제 피해 기준",()=>{
            var b=B();Mods(b,"Life Strike");b=b with {Scenario=b.Scenario with {Enemy=new(1e9,2700)},Melee=b.Melee! with {Shields=100,Overguard=100},Encounter=b.Encounter! with {ZenistarDiscs=[new(0,.1,Retrieve:2,RetrieveStarted:.5,AuraTimes:[.5,1,1.5])]}};
            var r=Run(b);Near(r.Recovery!.HeavyHealing!.Mean,.2*r.Events.Where(e=>e.Kind=="zenistarAura").Sum(e=>e.Amount));
            Yes(r.Recovery.HeavyHealing.Mean>0&&r.DirectDamage<400);
        });
        check("제니스타 회수:동작 시작·완료 입력 검증과 빌드 저장 복원",()=>{
            Reject(()=>ZenistarDiscState.Validate([new(0,.1,RetrieveStarted:1)],1),"완료 시각");
            Reject(()=>ZenistarDiscState.Validate([new(0,.1,Retrieve:1,RetrieveStarted:1)],1),"뒤여야");
            var b=B();b=b with {Encounter=b.Encounter! with {ZenistarDiscs=[b.Encounter.ZenistarDiscs![0] with {RetrieveStarted=1}]}};
            var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;
            Near(saved.Encounter!.ZenistarDiscs![0].RetrieveStarted!.Value,1);
        });
    }
}
