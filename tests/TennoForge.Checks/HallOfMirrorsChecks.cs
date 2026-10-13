using TennoForge.Core;

internal static class HallOfMirrorsChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();
        void Yes(bool b){if(!b)throw new Exception("홀 오브 미러즈 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(HallOfMirrorsSetup? s=null){var w=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime");return new(w.Id,new ModSlot?[8],new(Combo:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(ExternalEquipment:new(HallOfMirrors:s??new())));}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        ArcaneSlot A(string name)=>new(ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==name).Id,5,true);
        check("미라지 분신:랭크별1~4개·5/10/15/20%·피해와치확상확각각배율",()=>{
            foreach(var rank in Enumerable.Range(0,4))
            {
                var b=B(new(rank,2));Mods(b,"Pressure Point","True Steel","Melee Prowess");var owner=MeleeEngine.Calculate(catalog,b);var scale=new[]{.05,.1,.15,.2}[rank]*2;
                for(var clone=1;clone<=rank+1;clone++){var c=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,clone));Near(c.Damage.Values.Sum(),owner.Damage.Values.Sum()*scale);Near(c.CriticalChance,owner.CriticalChance*scale);Near(c.StatusChance,owner.StatusChance*scale);Near(c.CriticalMultiplier,owner.CriticalMultiplier);}
            }
        });
        check("미라지 콤보제외:12배·초기콤보·블러드러시/위핑운즈·강공격배수복사안함",()=>{
            var b=B() with {Scenario=new(Combo:12,Enemy:new(1e9,0))};Mods(b,"Blood Rush","Weeping Wounds","Corrupt Charge");
            var clone=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,1));var plain=MeleeEngine.Calculate(catalog,B());Near(clone.CriticalChance,plain.CriticalChance*.2);Near(clone.StatusChance,plain.StatusChance*.2);Near(clone.Melee!.EffectiveCombo,1);Yes(!clone.Melee.Policy!.HitCombo);
            var heavy=b with {Melee=b.Melee! with {Attack="Heavy Attack"}};Near(MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(heavy,1)).Melee!.Brackets!.ComboMultiplier,1);
        });
        check("미라지 갈바나이즈드/일반아케인제외:기본효과도제외·원본빌드보존",()=>{
            var b=B();Mods(b,"Galvanized Steel","Galvanized Elementalist");b=b with {Arcanes=new(Warframe1:A("Arcane Fury"),Warframe2:A("Arcane Avenger")),Melee=b.Melee! with {Conditions=new(){["GalvanizedStacks"]=4}}};
            var c=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,1));var plain=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(B(),1));Near(c.ExpectedHit,plain.ExpectedHit);Near(c.StatusChance,plain.StatusChance);Yes(b.Slots[0] is not null&&b.Arcanes.Warframe1 is not null);
        });
        check("미라지 리탈리에이션:분신자신실드입력·미입력은플레이어값대입금지",()=>{
            var b=B(new(CloneShields:400)) with {Arcanes=new(Melee:A("Melee Retaliation"),Shields:2000)};var c=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,1));var plain=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(B(),1));Near(c.ExpectedHit/plain.ExpectedHit,1.6);
            try{MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b with {Melee=b.Melee! with {ExternalEquipment=new(HallOfMirrors:new())}},1));throw new Exception("분신 실드 누락 허용");}catch(CombatRuleUnavailableException){}
        });
        check("미라지 다우티:분신상확으로보너스계산·피해비율은배율전후동일",()=>{
            var b=B();Mods(b,"Auger Strike","Melee Prowess");b=b with {Arcanes=new(Melee:A("Melee Doughty"))};var c=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,1));var plain=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b with {Arcanes=null},1));Near(c.CriticalMultiplier-plain.CriticalMultiplier,ArcaneEngine.DoughtyBonus(c.Damage,c.StatusChance,1));
        });
        check("멀레볼런스:랭크별10처치상한·별도피해배수·위력/치확상확재증폭없음",()=>{
            for(var rank=0;rank<4;rank++)
            {var b=B(new(Strength:2,MalevolenceRank:rank,Kills:10));var c=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(b,1));var plain=MeleeEngine.Calculate(catalog,HallOfMirrorsRules.Copy(B(new(Strength:2)),1));Near(c.ExpectedHit/plain.ExpectedHit,1+(rank+2)*.1);Near(c.CriticalChance,plain.CriticalChance);Near(c.StatusChance,plain.StatusChance);}
        });
        check("미라지 실제명중:4분신강공격·강제베기·플레이어콤보소모/획득없음",()=>{
            var b=B() with {Scenario=new(Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,HologramContacts:Enumerable.Range(1,4).Select(n=>new HologramContact(n*.1,n,Attack:"Heavy Attack")).ToArray())};Mods(b,"Relentless Combination");
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),4);Yes(r.StatusDamage>0);Near(r.Points.Single(p=>p.Time==4).Combo,12);Near(r.Points.Last().Combo,1);Near(r.Points.Last().HologramCount!.Value,4);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
        });
        check("미라지 처치귀속:이터널워/버서커/근접처치증폭없음·멀레볼런스만증가",()=>{
            var b=B(new(MalevolenceRank:3));b=b with {Scenario=new(Enemy:new(1,0)),Melee=b.Melee! with {ExternalEquipment=b.Melee.ExternalEquipment! with {Warcry=new(EternalWarRank:3)}},Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,HologramContacts:[new(.1)])};Mods(b,"Berserker Fury");
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Points.Last().WarcryRemaining!.Value,15);Near(r.Points.Last().HallMalevolence,.05);Yes(!r.Events.Any(e=>e.Kind is "berserker" or "eternalWar"));
        });
        check("미라지 만료/개별무효화:그분신후속명중제외·다른분신유지",()=>{
            var b=B(new(Remaining:2)) with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,Inputs:[new(.5,"MirrorNullified",1)],HologramContacts:[new(.1,1),new(1,1),new(1,2),new(2.1,2)])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),2);Near(r.Points.Last().HologramCount!.Value,0);
        });
        check("미라지 고유단검:콰서스분신투사체·플레이어12배콤보미소모",()=>{
            var b=B();var id=WeaponStanceMechanic.Quassus;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[new(0,0,Hologram:1,Contacts:[new(.1)])])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),1);Near(r.Points.Single(p=>p.Time==4).Combo,12);Near(r.Points.Last().Combo,1);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
        });
        check("미라지 아케인출처충돌:미선택거부·본문분기에서분신발동제외",()=>{
            var b=B() with {Arcanes=new(Melee:A("Melee Influence")),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,HologramContacts:[new(.1)])};
            try{CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);throw new Exception("분신 아케인 충돌 숨김");}catch(CombatRuleUnavailableException){}
            var branch=b with {Melee=b.Melee! with {ExternalEquipment=new(HallOfMirrors:new(ArcaneTriggers:"Article"))}};var r=CombatTimeline.Calculate(catalog,branch,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0&&!r.Events.Any(e=>e.Kind=="influenceStart"));
        });
        check("미라지 제니스타:플레이어와4분신동시배치·분신10초수명·소유자별칼날변환",()=>{
            var mirrors=new HallOfMirrorsState(new());var discs=new ZenistarDiscState(mirrors);
            discs.Launch(new(0,0,Disc:1),12);for(var n=1;n<=4;n++){var d=discs.Launch(new(0,0,Disc:n+1,Hologram:n),12);Near(d.Combo,1);Near(d.EarliestEnd,10);}
            Yes(discs.Deployed(9)&&discs.Deployed(9,4));Yes(discs.Deployed(10)&&!discs.Deployed(10,4));
            mirrors.Remove(1);Yes(!discs.Deployed(1,1)&&discs.Deployed(1,2));
        });
        check("미라지 제니스타:능력종료원반소멸·재시전후이전원반부활금지",()=>{
            var mirrors=new HallOfMirrorsState(new());var discs=new ZenistarDiscState(mirrors);discs.Launch(new(0,0,Hologram:1),12);
            mirrors.End();Yes(!discs.Active(1,1));Yes(mirrors.Cast(2));Yes(!discs.Active(1,2));discs.Launch(new(2,2,Disc:2,Hologram:1),12);Yes(discs.Active(2,3));
        });
        check("미라지 제니스타:실제원반사건·한번만플레이어콤보소모·분신종료후오라없음",()=>{
            var b=B(new(Remaining:1));var id=ZenistarDiscState.WeaponId;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ZenistarDiscs:Enumerable.Range(0,5).Select(n=>new ZenistarDeployment(0,.1,Disc:n+1,Hologram:n,AuraTimes:[.5,1.5])).ToArray())};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="zenistarLaunch"),5);Near(r.Events.Count(e=>e.Kind=="heavyCombo"),1);Near(r.Events.Count(e=>e.Kind=="zenistarAura"),6);Near(r.Events.Count(e=>e.Kind=="hologramHit"),8);Yes(r.Events.Where(e=>e.Kind=="hologramHit").All(e=>e.Time<1));
        });
        check("미라지 제니스타:분신원반만으로플레이어칼날피해/콤보변경금지",()=>{
            var b=B();var id=ZenistarDiscState.WeaponId;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,BaseInterval:60,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ZenistarDiscs:[new(.1,.2,Hologram:1,AuraTimes:[.5])])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Points.First().SnapshotDps,r.Points.Single(p=>p.Time==4).SnapshotDps);Near(r.Points.Single(p=>p.Time==4).Combo,12);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
        });
        check("미라지 빈퀴부스:지연폭발/막기사격3배·플레이어정밀충전귀속분리",()=>{
            var b=B();var id=VinquibusState.WeaponId;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,WeaponActions:[new(0,0,Sequence:2,Hologram:1,Contacts:[new(.1)])])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),2);Near(r.Events.Single(e=>e.Kind=="vinquibusBlast").Time,.8);Yes(!r.Events.Any(e=>e.Kind=="vinquibusPrecision"||e.Kind=="combo"));
        });
        check("미라지 글레이브:4분신동시투척·각자반사/관통·플레이어콤보제외",()=>{
            var b=B();var id=catalog.Weapons.Single(w=>w.EnglishName=="Glaive Prime").Id;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,GlaiveFlights:Enumerable.Range(1,4).Select(n=>new GlaiveFlight(0,1,Throw:n,Hologram:n,Contacts:[new(.1*n)])).ToArray())};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="glaiveContact"),4);Near(r.Events.Count(e=>e.Kind=="hologramHit"),4);Yes(r.StatusDamage>0);Near(r.Points.Single(p=>p.Time==4).Combo,12);Yes(!r.Events.Any(e=>e.Kind=="combo"));
            try{GlaiveFlightState.Validate([new(0,1,Hologram:1,Detonation:new(.5))],1,id);throw new Exception("분신글레이브기폭허용");}catch(ArgumentException){}
        });
        check("미라지 볼래틸:반사폭발도분신배율·콤보획득없음",()=>{
            var b=B();var id=catalog.Weapons.Single(w=>w.EnglishName=="Glaive Prime").Id;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,GlaiveFlights:[new(0,1,Hologram:1,Contacts:[new(.2)])])};Mods(b,"Volatile Rebound");
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),2);Near(r.Events.Count(e=>e.Kind=="glaiveBounce"),1);Yes(!r.Events.Any(e=>e.Kind=="combo"));
        });
        check("미라지 울프슬레지:분신4망치+본체·전진폭발·무콤보기폭/귀환강타",()=>{
            var b=B();var id=WolfSledgeFlight.WeaponId;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Scenario=new(Combo:12,Enemy:new(1e9,0)),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,GlaiveFlights:[new(0,1,Hologram:1,Detonation:new(.5),Contacts:[new(.2),new(.7,Kind:GlaiveContactKind.Return)])])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),4);Near(r.Events.Count(e=>e.Kind=="wolfBounce"),1);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"||e.Kind=="combo"));Near(r.Points.Single(p=>p.Time==4).Combo,12);
        });
        check("미라지 방패:동시투척·노출시간·회수폭발·플레이어충전미소모",()=>{
            var b=B();var id=ShieldThrowMechanic.Sigma;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ShieldFlights:[new(0,1,Impact:.1,Throw:1,Hologram:1,FinisherWindowSeconds:.2,Detonation:new(.5)),new(0,1,Impact:.1,Throw:2,Hologram:2,FinisherWindowSeconds:.2)])};
            var r=CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));Near(r.Events.Count(e=>e.Kind=="hologramHit"),3);Near(r.Events.Count(e=>e.Kind=="shieldExplosion"),1);Yes(!r.Events.Any(e=>e.Kind=="combo"||e.Kind=="blockCharge"));
        });
        check("미라지 투사체:능력재시전으로이전분신탄환의미기재수명우회금지",()=>{
            var b=B();var id=WeaponStanceMechanic.Quassus;b=b with {WeaponId=id,Capacity=CapacityCatalog.Load().Default(id,null),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,Inputs:[new(.1,"MirrorsEnd"),new(.2,"MirrorsCast")],WeaponActions:[new(0,0,Hologram:1,Contacts:[new(.3)])])};
            try{CombatTimeline.Calculate(catalog,b,StanceCatalog.Load(),1);throw new Exception("이전분신탄환복원허용");}catch(CombatRuleUnavailableException e){Yes(e.Message.Contains("분신 소멸"));}
        });
        check("미라지 상태:각분신널리파이어·전멸/만료후재시전·증폭중첩초기화",()=>{
            var s=new HallOfMirrorsState(new(MalevolenceRank:3));Yes(!s.Cast(1));for(var i=0;i<12;i++)s.Kill(1);Near(s.Kills,10);s.Remove(1);Yes(!s.Active(1,2)&&s.Active(2,2));s.Remove(2);s.Remove(3);s.Remove(4);Yes(!s.Any(2));Near(s.Kills,0);Yes(s.Cast(3));Yes(s.Active(1,27.9)&&!s.Any(28));Near(s.At(28).Kills,0);Yes(s.Cast(28));
        });
    }
}
