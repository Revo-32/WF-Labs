using TennoForge.Core;

internal static class DualIchorCloudChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var id=DualIchorCloudMechanic.WeaponId;var def=IncarnonEngine.ForWeapon(id)!;var stances=StanceCatalog.Load();
        Build B()=>new(id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Incarnon:true),Encounter:new(EncounterMode.FiveSeconds,NearbyEnemies:[new("second",1,Defense:new(1e9,0))],IchorClouds:[new(.1,Target:2)]));
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        ArcaneSlot Arc(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).MaxRank);
        void Near(double a,double b){if(Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("독 구름 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){Yes(e.Message.Contains(text));return;}throw new Exception("잘못된 구름 명중 허용");}
        check("독 구름:350/진화II440·변신피해100%·CO/콤보/치명/상확 모드 제외",()=>{
            var b=B();var plain=DualIchorCloudMechanic.Calculate(catalog,b);Near(plain.Damage[DamageType.Toxin],700);Near(plain.StatusChance,.5);Near(plain.CriticalChance,0);
            Mods(b,"Condition Overload","True Steel","Organ Shatter","Proton Snap","Melee Prowess");b=b with {Arcanes=new(Warframe1:Arc("Arcane Avenger")),Scenario=b.Scenario with {Combo=12},Melee=b.Melee! with {Conditions=new(){["ProtonSnap"]=1},Statuses=new(){[DamageType.Impact]=5},Evolutions=[def.Perks.First(p=>p.Stage==2).Id,def.Perks.Single(p=>p.Name=="Absolute Valor").Id]}};
            var r=DualIchorCloudMechanic.Calculate(catalog,b);Near(r.CriticalChance,0);Near(r.CriticalMultiplier,1);Near(r.StatusChance,.5);Near(r.Damage[DamageType.Toxin],1760); // Proton elemental damage still applies.
        });
        check("독 구름:원소 조합·프레셔 가산·팩션 직접/상태 이중 적용·강제독성 없음",()=>{
            var b=B();Mods(b,"Pressure Point","Molten Impact");var r=DualIchorCloudMechanic.Calculate(catalog,b);Near(r.Damage[DamageType.Gas],350*3.2*1.9);Yes(!r.Damage.ContainsKey(DamageType.Toxin)&&r.Melee!.ForcedProcs.Length==0);
            var smite=catalog.Mods.Single(m=>m.EnglishName=="Smite Grineer");b.Slots[2]=new(smite.Id,smite.MaxRank);b=b with {Melee=b.Melee! with {Faction="Grineer"}};var f=DualIchorCloudMechanic.Calculate(catalog,b);Near(f.Damage.Values.Sum()/r.Damage.Values.Sum(),1.3);Near(f.Melee!.StatusDamage.Single(p=>p.Type==DamageType.Gas).DamagePerTick/r.Melee!.StatusDamage.Single(p=>p.Type==DamageType.Gas).DamagePerTick,1.69);
        });
        check("독 구름:사거리4m·지속시간7초와 상태 지속 모드 분리·범용 피해 제외",()=>{
            var b=B();Mods(b,"Primed Reach","Lasting Sting");var r=DualIchorCloudMechanic.Calculate(catalog,b);Near(r.Melee!.AreaRadius!.Value,4);Near(r.Damage.Values.Sum(),700);Near(r.Melee.StatusDamage.Single(p=>p.Type==DamageType.Toxin).Duration,12.6);Near(DualIchorCloudMechanic.Lifetime,7);
            b=b with {Arcanes=new(Warframe1:Arc("Arcane Arachne"))};Near(DualIchorCloudMechanic.Calculate(catalog,b).Damage.Values.Sum(),700);Near(DualIchorCloudMechanic.Calculate(catalog,b,4.01).Damage.Values.Sum(),0);
        });
        check("독 구름 전투:처치 후 생성·지정명중700·일반 콤보 미획득·수명/미생성/거리 검증",()=>{
            var b=B();var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.Enemies![1].DirectDamage,700);Yes(r.Events.Count(e=>e.Kind=="ichorCloudCreate")==1&&r.Events.Count(e=>e.Kind=="ichorCloudHit")==1);Near(r.Points.First(p=>p.Time>=.1).ComboPoints,1);
            Reject(()=>CombatTimeline.Calculate(catalog,b with {Encounter=b.Encounter! with {Mode=EncounterMode.TenSeconds,IchorClouds=[new(7,Target:2)]}},stances,1),"수명");
            Reject(()=>CombatTimeline.Calculate(catalog,b with {Encounter=b.Encounter! with {IchorClouds=[new(.1,Cloud:2,Target:2)]}},stances,1),"생성");
            Reject(()=>CombatTimeline.Calculate(catalog,b with {Encounter=b.Encounter! with {NearbyEnemies=[new("far",4.01)]}},stances,1),"4m");
        });
        check("독 구름 전투:구름 직접 처치의 연쇄 생성·별도 번호·인플루언스 미발동",()=>{
            var b=B();Mods(b,"North Wind","Fever Strike","Shocking Touch");b=b with {Arcanes=new(Melee:Arc("Melee Influence")),Encounter=b.Encounter! with {NearbyEnemies=[new("second",1,Defense:new(1,0)),new("third",2,Defense:new(1e9,0))],IchorClouds=[new(.1,Target:2),..Enumerable.Range(1,40).Select(i=>new IchorCloudContact(.1+i*.1,Cloud:2,Target:3))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Events.Count(e=>e.Kind=="ichorCloudCreate")==2);Yes(r.Enemies![2].DirectDamage>0);Yes(r.Events.Any(e=>e.Kind=="proc"&&e.Description.Contains("전기")));Yes(!r.Events.Any(e=>e.Kind.StartsWith("influence")));
        });
        check("독 구름:기존 독성에만 알케미스트5콤보·첫 접촉 새 상태 소급 제외",()=>{
            var b=B();b=b with {Melee=b.Melee! with {Evolutions=[def.Perks.Single(p=>p.Effects.ContainsKey("ComboOnToxin")).Id]},Encounter=b.Encounter! with {IchorClouds=[new(.1,Target:2),new(.2,Target:2)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,512);Near(r.Points.First(p=>p.Time>=.1).ComboPoints,1);
            var expected=1+5*.5;Yes(Math.Abs(r.Points.First(p=>p.Time>=.2).ComboPoints-expected)<.4);
        });
        check("독 구름:같은 시각 실제 처치 이후 생성·연쇄 명중 순서 보존",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("second",1,Defense:new(1,0)),new("third",2,Defense:new(1e9,0))],IchorClouds=[new(0,Target:2),new(0,Cloud:2,Target:3)]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Near(r.Enemies![2].DirectDamage,700);Yes(r.Events.Count(e=>e.Kind=="ichorCloudCreate")==2);Yes(r.Events.Where(e=>e.Kind=="ichorCloudHit").All(e=>e.Time==0));
        });
        check("독 구름:구름의 상태 피해 처치도 후속 구름 생성",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("second",1,Defense:new(800,0)),new("outside",20,Defense:new(1e9,0))]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,128);Yes(r.Enemies![1].StatusDamage>0);Yes(r.Enemies[1].Ttk.KillFraction>.3&&r.Enemies[1].Ttk.KillFraction<.7);
            // The deterministic retained path must keep the actual source of its kill.
            if(r.Events.Any(e=>e.Kind=="tick"&&e.Description.Contains("대상2")))Yes(r.Events.Any(e=>e.Kind=="ichorCloudCreate"&&e.Time>1));
        });
        check("독 구름:원문 없는 틱을 초당1회로 만들거나 인접 적 피해를 누락하지 않음",()=>{
            var b=B();var r=EncounterEngine.Calculate(catalog,b with {Encounter=b.Encounter! with {IchorClouds=null}},stances,1);Yes(r.Unsupported.Any(n=>n.Contains("틱 간격"))&&r.AverageDps is null);
            b=b with {Encounter=b.Encounter! with {NearbyEnemies=[new("outside",4.01)],IchorClouds=null}};var safe=CombatTimeline.Calculate(catalog,b,stances,1);Yes(safe.Unsupported.Length==0&&safe.Events.Any(e=>e.Kind=="ichorCloudCreate"));
        });
    }
}
