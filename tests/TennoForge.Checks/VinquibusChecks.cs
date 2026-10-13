using TennoForge.Core;

internal static class VinquibusChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("빈퀴부스 전이 불일치");}
        void Near(double? a,double b){if(a is null||!double.IsFinite(a.Value)||Math.Abs(a.Value-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B()=>new(VinquibusState.WeaponId,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(VinquibusState.WeaponId,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("옴니 포르마:모든 일반 극성 절반·움브라 기본값·무극성 기본값",()=>{
            var baseMod=catalog.Mods.First();foreach(var p in CapacityEngine.ModPolarities){var m=baseMod with {BaseDrain=5,MaxRank=10,Polarity=p};Near(CapacityEngine.SlotCost(m,10,"Omni"),p is "None" or "AP_UMBRA"?15:8);}
            Yes(!CapacityEngine.ModPolarities.Contains("Omni"));
        });
        check("빈퀴부스:공식 내부ID·옴니 일반/스탠스 슬롯·해로잉 호환",()=>{
            var b=B();var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);Yes(w.WeaponType=="Bayonet"&&b.Capacity!.SlotPolarities[1]=="Omni");
            Yes(stances.ForWeapon(w).Any(s=>s.Id=="Harrowing Spire"));Near(CapacityEngine.Calculate(catalog,b with {Capacity=CapacityCatalog.Load().Default(w.Id,"Harrowing Spire")}).Total,70);
        });
        check("빈퀴부스:헤드샷2·16상한·다중 명중1입력 소모·빗나가면 유지",()=>{
            var s=new VinquibusState();s.Headshots(10);Near(s.Precision,16);s.FinishMeleeInput(1,false);Near(s.Precision,16);s.FinishMeleeInput(1,true);s.FinishMeleeInput(1,true);Near(s.Precision,15);s.FinishMeleeInput(2,true);Near(s.Precision,14);
            for(var n=0;n<5;n++)s.MeleeKill();Near(s.Ferocity,16);for(var n=0;n<16;n++)Yes(s.PrimaryShot());Yes(!s.PrimaryShot());Near(s.Ferocity,0);
        });
        check("빈퀴부스:정밀 충전 수는 공격 횟수·피해+250% 기본피해가산 가정의수치",()=>{
            var b=B();b.Slots[0]=new(catalog.Mods.Single(m=>m.EnglishName=="Pressure Point").Id,5);
            b=b with {Melee=new(Conditions:new(){["VinquibusPrecision"]=1})};Near(MeleeEngine.Calculate(catalog,b).Damage.Values.Sum(),260*4.7);
            Near(MeleeEngine.Calculate(catalog,b with {Melee=new(Conditions:new(){["VinquibusPrecision"]=16})}).Damage.Values.Sum(),260*4.7);
        });
        check("빈퀴부스 시간 경로:헤드샷 충전·처치4·실제 주무기 발사 소모",()=>{
            var b=B() with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"VinquibusHeadshot"),new(.1,"MeleeKill"),new(.2,"VinquibusPrimaryShot")])};var r=Run(b);
            Near(r.Points[0].VinquibusPrecision,1);Near(r.Points.Single(p=>p.Time==1).VinquibusPrecision,0);Near(r.Points.Single(p=>p.Time==.1).VinquibusFerocity,4);Near(r.Points.Single(p=>p.Time==.2).VinquibusFerocity,3);
        });
        check("빈퀴부스 스탠스:같은 입력2타에 마지막 충전을 공유·입력마다1회",()=>{
            var b=B() with {Stance=new("Harrowing Spire","Neutral",stances.Revision),Capacity=CapacityCatalog.Load().Default(VinquibusState.WeaponId,"Harrowing Spire"),Melee=new(Conditions:new(){["VinquibusPrecision"]=2}),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,.2,.4,.6])};var r=Run(b);
            var consumed=r.Events.Where(e=>e.Kind=="vinquibusPrecision").ToArray();Yes(consumed.Length==2);Near(consumed[0].Time,.2);Near(consumed[0].Amount,1);Near(consumed[1].Amount,0);
        });
        check("빈퀴부스 사격:0.8초 후 별도 폭발·정밀 마지막1충전 공유·별도 충전 소모 없음",()=>{
            var b=B();b=b with {Scenario=b.Scenario with {BaseInterval=60},Melee=new(Attack:"Gunshot",Conditions:new(){["VinquibusPrecision"]=1})};var r=Run(b);
            Near(VinquibusGunshot.Delay,.8);Yes(r.Events.Count(e=>e.Kind=="gunshotFuse")==1);Near(r.Events.Single(e=>e.Kind=="gunshotExplosion").Time,.8);
            var blast=r.Events.Single(e=>e.Kind=="hit"&&Math.Abs(e.Time-.8)<1e-8);Yes(Math.Abs(blast.Amount-400*3.5)<1e-8||Math.Abs(blast.Amount-400*3.5*3.2)<1e-8);
            Yes(r.Events.Count(e=>e.Kind=="vinquibusPrecision")==1);Near(r.Points.Last().VinquibusPrecision,0);
        });
        check("빈퀴부스 사격:생존 대상 중심의 지연 폭발·4m 가장자리70%·범위 밖 제외",()=>{
            var b=B();b=b with {Scenario=new(BaseInterval:60,Enemy:new(1e9,0)),Melee=new(Attack:"Gunshot"),Encounter=new(EncounterMode.FiveSeconds,NearbyEnemies:[new("edge",4,Defense:new(1000000,0)),new("outside",4.01,Defense:new(1000000,0))])};var r=Run(b);
            var edge=r.Enemies!.Single(e=>e.Id=="edge");Yes(Math.Abs(edge.DirectDamage-280)<1e-8||Math.Abs(edge.DirectDamage-896)<1e-8);Near(r.Enemies!.Single(e=>e.Id=="outside").DirectDamage,0);Yes(r.Events.Where(e=>e.Kind=="gunshotExplosion").All(e=>Math.Abs(e.Time-.8)<1e-9));
            var killed=EncounterEngine.Calculate(catalog,b with {Scenario=b.Scenario with {Enemy=new(1,0)}},stances,1);Yes(killed.Unsupported.Any(s=>s.Contains("관통")));
        });
        check("빈퀴부스 스탠스 사격:실제 접촉 시각+0.8초·동작3배·일반 근접 타격과 분리",()=>{
            var b=B() with {Stance=new("Harrowing Spire","Block",stances.Revision),Capacity=CapacityCatalog.Load().Default(VinquibusState.WeaponId,"Harrowing Spire"),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,1])};var r=Run(b);
            Near(r.Events.Single(e=>e.Kind=="gunshotExplosion").Time,.8);Yes(r.Events.Count(e=>e.Kind=="hit")==3);
            var hit=r.Events.Single(e=>e.Kind=="hit"&&Math.Abs(e.Time-.8)<1e-9);Yes(Math.Abs(hit.Amount-1200)<1e-8||Math.Abs(hit.Amount-3840)<1e-8);
        });
    }
}
