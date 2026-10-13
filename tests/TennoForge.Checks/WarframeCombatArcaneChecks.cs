using TennoForge.Core;

internal static class WarframeCombatArcaneChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("워프레임 아케인 전투 조건 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        ArcaneSlot A(string name,int rank=5)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,rank);
        Build B(string name="Nikana Prime")
        {
            var id=catalog.Weapons.Single(w=>w.EnglishName==name).Id;
            return new(id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds));
        }
        CombatTimelineResult Run(Build b,int samples=1){var r=CombatTimeline.Calculate(catalog,b,stances,samples);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("워프레임 방어력 아케인: 랭크별150/110/200 가산·포티피케이션과 중복 배가 없음",()=>{
            for(var rank=0;rank<=5;rank++)foreach(var (name,perRank) in new[]{("Arcane Guardian",150),("Arcane Reaper",110),("Arcane Tanker",200),("Arcane Ultimatum",200)})
                Near(WarframeCombatArcanes.ArmorBonus(new(Warframe1:A(name,rank))),perRank*(rank+1));
            Near(WarframeCombatArcanes.ArmorBonus(new(Warframe1:A("Arcane Ultimatum"),Warframe2:A("Arcane Guardian")),2),3300);
        });
        check("가디언 정적 계산: 실제 방어력450초과로 진화 피해를 갱신",()=>{
            var b=B("Sibear");var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Condition=="Armor450");b=b with {Melee=b.Melee! with {Evolutions=[perk.Id]},Arcanes=new(Warframe1:A("Arcane Guardian"),PlayerArmor:300)};
            var active=MeleeEngine.Calculate(catalog,b);var inactive=MeleeEngine.Calculate(catalog,b with {Arcanes=b.Arcanes! with {Warframe1=A("Arcane Guardian") with {Active=false}}});
            Near(active.Melee!.Setup.Conditions!["Armor450"],1);Near(inactive.Melee!.Setup.Conditions!["Armor450"],0);Near(active.Damage.Values.Sum()-inactive.Damage.Values.Sum(),40);
        });
        check("탱커 전투: 아크윙 총기 장착 뒤1200방어력·강공격 효율과 실제 콤보 소모",()=>{
            var b=B("Ack & Brunt");var perk=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Condition=="Armor450");
            b=b with {Scenario=b.Scenario with {Combo=12},Melee=new(Attack:"Heavy Attack",Evolutions:[perk.Id]),Arcanes=new(Warframe1:A("Arcane Tanker"),PlayerArmor:300),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"EquipArchgun")])};
            var r=Run(b);Near(r.Points[0].PlayerArmor!.Value,1500);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,66);Near(r.Points[0].ComboPoints,154);
        });
        check("얼티메이텀 전투: 지상과 파라존 별도 가산·동일 유형 재발동은 갱신",()=>{
            var b=B() with {Arcanes=new(Warframe1:A("Arcane Ultimatum"),PlayerArmor:300),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"GroundFinisherKill"),new(1,"ParazonKill"),new(2,"GroundFinisherKill")])};
            var r=Run(b);Near(r.Points[0].PlayerArmor!.Value,1500);Near(r.Points.Single(p=>p.Time==1).PlayerArmor!.Value,2700);Near(r.Points.Last().PlayerArmor!.Value,2700);
        });
        check("리퍼 전투: 근접 처치 발동·도움 제외·660방어력·24체력/초 적분",()=>{
            var b=B() with {Arcanes=new(Warframe1:A("Arcane Reaper"),PlayerArmor:300),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeAssist"),new(2,"MeleeKill")])};
            var r=Run(b);Near(r.Points.Single(p=>p.Time==1).PlayerArmor!.Value,300);Near(r.Points.Single(p=>p.Time==2).PlayerArmor!.Value,960);Near(r.Recovery!.HealthRegeneration!.Mean,192);
        });
        check("트리커리: 정적 은신 브라켓·전투 중 발동 이후 첫 접촉과3초 대기",()=>{
            var b=B() with {Arcanes=new(Warframe1:A("Arcane Trickery"))};var plain=MeleeEngine.Calculate(catalog,b with {Arcanes=null});var hidden=MeleeEngine.Calculate(catalog,b);
            Near(hidden.Melee!.StealthBonus,7);Near(hidden.ExpectedHit-plain.ExpectedHit,plain.Damage.Values.Sum()*7);
            b=b with {Scenario=b.Scenario with {BaseInterval=1},Encounter=new(EncounterMode.TenSeconds,Inputs:Enumerable.Range(0,100).Select(_=>new CombatInput(0,"ParazonKill")).ToArray())};
            var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("트리커리")));var hits=r.Events.Where(e=>e.Kind=="hit").ToArray();Yes(hits[0].Amount>7*plain.Damage.Values.Sum());Yes(hits[1].Amount<3*plain.Damage.Values.Sum());
        });
        check("가디언:15% 피격 발동·활성 중 추가 피해로20초 갱신 금지",()=>{
            var actions=Enumerable.Range(0,100).Select(_=>new CombatInput(0,"IncomingHit")).Append(new(8,"IncomingHit")).ToArray();
            var r=Run(B() with {Arcanes=new(Warframe1:A("Arcane Guardian"),PlayerArmor:300),Encounter=new(EncounterMode.TenSeconds,Inputs:actions)});
            Near(r.Points[0].PlayerArmor!.Value,1200);Yes(r.Events.Count(e=>e.Kind=="buff"&&e.Description.Contains("가디언"))==1);
        });
        check("탱커: 위키 내부 IsRefreshable=false와 재장착 시 남은 시간 보존",()=>{
            Yes(ReferenceCatalog.Load().Arcanes.Single(a=>a.Id==A("Arcane Tanker").Id).Raw.GetProperty("IsRefreshable").GetBoolean()==false);
            var r=Run(B() with {Arcanes=new(Warframe1:A("Arcane Tanker"),PlayerArmor:300),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"EquipArchgun"),new(8,"EquipArchgun")])});
            Yes(r.Events.Count(e=>e.Kind=="buff"&&e.Description.Contains("탱커"))==1);
        });
        check("이지스: 위키 재생 수식·자성 중단·활성 중 갱신 없음·종료 경계 적분",()=>{
            var s=new PlayerShieldState(3560,1200,0,0,true,.9);Yes(s.ActivateAegis(5));s.Advance(1);Near(s.Shields,(15+.35*3560)*1.9);
            s.Damage(1e4);s.Magnetic();s.Advance(7);Near(s.Shields,0);Yes(!s.ActivateAegis(0));s.Advance(8);Near(s.Shields,(15+.35*3560)*1.9);Near(s.Overshields,0);
            var z=new PlayerShieldState(10000,1200,0,0,true);z.ActivateAegis(0);z.Advance(11.9);z.Damage(10000);z.Advance(13);Near(z.Shields,(15+.1*10000)*.1+(15+.05*10000));
        });
        check("이지스 전투: 실드 피해만 추첨·미발동 시작·리탈리에이션 후속 피해 증가",()=>{
            var actions=Enumerable.Range(0,300).Select(_=>new CombatInput(0,"PlayerShieldDamage",1)).ToArray();
            var b=B() with {Arcanes=new(Melee:A("Melee Retaliation"),Warframe1:A("Arcane Aegis"),Shields:500,MaximumShields:1000,ShieldRechargeEnabled:false),Encounter=new(EncounterMode.TenSeconds,Inputs:actions)};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="aegis")==1);Near(r.Points[0].PlayerShields!.Value,200);Near(r.Points.Last().PlayerShields!.Value,1000);Yes(r.Points.Last().SnapshotDps>r.Points[0].SnapshotDps);
            var generic=Run(b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"IncomingHit")])});Yes(!generic.Events.Any(e=>e.Kind=="aegis"));Near(generic.Points.Last().PlayerShields!.Value,500);
        });
        check("배리어 전투: 랭크5 실드 복구·6초 재사용·오버실드 생성 없음",()=>{
            var actions=Enumerable.Range(0,100).Select(_=>new CombatInput(0,"PlayerShieldDamage",1)).Concat(Enumerable.Range(0,100).Select(_=>new CombatInput(6,"PlayerShieldDamage",1))).ToArray();
            var r=Run(B() with {Arcanes=new(Warframe1:A("Arcane Barrier"),Shields:900,MaximumShields:1000,ShieldRechargeEnabled:false),Encounter=new(EncounterMode.TenSeconds,Inputs:actions)});
            Yes(r.Events.Count(e=>e.Kind=="barrier")==2);Yes(r.Points.Last().PlayerShields>900);Near(r.Points.Last().PlayerOvershields!.Value,0);
        });
    }
}
