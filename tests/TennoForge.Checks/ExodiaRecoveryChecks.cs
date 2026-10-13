using TennoForge.Core;

internal static class ExodiaRecoveryChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool value){if(!value)throw new Exception("엑소디아 회복 사건 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string arcane)
        {
            var id=ZawCatalog.Id("Sepfahn","Peye","Jai");var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName==arcane);
            return new(id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Exodia:new(a.Id,3)),Encounter:new(EncounterMode.TenSeconds));
        }
        check("브레이브: 2·3중첩만4초 갱신, 3중첩 중 추가 처치에 갱신 없음",()=>{
            var s=new ExodiaBraveState(3);s.HeavyKill(0);s.HeavyKill(1);s.HeavyKill(2);s.HeavyKill(5);s.Advance(6);
            Near(s.Generated,5+10+4*15);Yes(s.Stacks==0);Near(s.Rate,0);s.HeavyKill(7);s.Advance(11);Near(s.Generated,95);
        });
        check("브레이브: 어빌리티1초 중단·채널링 종료 후 남은 시간만 재생",()=>{
            var s=new ExodiaBraveState(3);s.HeavyKill(0);s.Cast(1);s.Channel(2.5,true);s.Channel(3.5,false);s.Advance(5);Near(s.Generated,10);
            var rank0=new ExodiaBraveState(0,true);rank0.HeavyKill(0);rank0.Advance(8);Near(rank0.Generated,0);
        });
        check("브레이브 시간 경로: 다른 적 직접 강공격만 발동·시전 사건·회복량 저장",()=>{
            var b=B("Exodia Brave") with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"HeavyMeleeKill",12),new(1,"AbilityCast"),new(2,"MeleeKill"),new(2.5,"ChannelStart"),new(3.5,"ChannelStop"),new(5,"MeleeAssist")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Count(e=>e.Kind=="brave")==1);
            Near(t.Recovery!.Energy!.Mean,10);Near(t.Points.Single(p=>p.Time==1).BraveRate!.Value,0);Near(t.Points.Single(p=>p.Time==2).BraveRate!.Value,5);Near(t.Points.Last().BraveEnergy!.Value,10);
            var plain=CombatTimeline.Calculate(catalog,b with {Melee=b.Melee! with {Exodia=null}},stances,1);Near(t.DirectDamage,plain.DirectDamage);
        });
        check("브레이브: 실제 강공격 처치 발동, 대상 처치 뒤 회복량을 계속 누적하지 않음",()=>{
            var b=B("Exodia Brave") with {Scenario=new(Enemy:new(1,0)),Melee=B("Exodia Brave").Melee! with {Attack="Heavy Attack"}};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0&&t.Events.Any(e=>e.Kind=="brave"));Near(t.Recovery!.Energy!.Mean,0);
        });
        check("마이트: 시간 교전 시작은 미발동, 마무리·파라존만 발동하고8초 만료",()=>{
            var b=B("Exodia Might");var no=CombatTimeline.Calculate(catalog,b,stances,1);Near(no.Recovery!.ExodiaHealing!.Mean,0);
            var inputs=Enumerable.Repeat(new CombatInput(0,"ParazonKill"),40).Append(new CombatInput(9,"MeleeKill")).ToArray();
            var t=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.TenSeconds,Inputs:inputs)},stances,1);
            Yes(t.Unsupported.Length==0&&t.Recovery!.ExodiaHealing!.Mean>0&&t.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("마이트")));
            Yes(t.Events.Where(e=>e.Kind=="mightHealing").All(e=>e.Time<8));Near(t.Recovery!.HostFinisherHealing!.Mean,0);
        });
        check("마이트: 이미 생성된 지속 피해도 발동 후 흡혈·만료 후 중단",()=>{
            var b=B("Exodia Might");var names=new[]{"Melee Prowess","Weeping Wounds","Virulent Scourge","Volcanic Edge"};for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}
            var inputs=Enumerable.Repeat(new CombatInput(1,"FinisherKill"),40).ToArray();
            b=b with {Scenario=b.Scenario with {Combo=12},Encounter=new(EncounterMode.TenSeconds,Inputs:inputs)};
            var t=CombatTimeline.Calculate(catalog,b,stances,64);Yes(t.Unsupported.Length==0&&t.StatusDamage>0&&t.Recovery!.ExodiaHealing!.Mean>0);
            Yes(t.Events.Where(e=>e.Kind=="mightHealing").All(e=>e.Time>=1&&e.Time<9));
        });
        check("마이트: 지상 마무리 실제 처치·호스트 첫 회복을 클라이언트와 분리",()=>{
            var b=B("Exodia Might");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var s=stances.ForWeapon(w).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));
            b=b with {Scenario=new(Enemy:new(1,0)),Melee=b.Melee! with {ComboDurationOverride=2},Stance=new(s.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.IsDefault?null:s.Id)};
            var sc=StanceEngine.Calculate(catalog,b,stances);b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,sc.Hits.Sum(h=>h.Count)).ToArray())};
            var t=CombatTimeline.Calculate(catalog,b,stances,2048);if(t.Unsupported.Length>0||t.Recovery?.HostFinisherHealing?.Mean is not >0)throw new Exception(System.Text.Json.JsonSerializer.Serialize(new{t.Unsupported,t.Recovery,Events=t.Events.Take(8),s.Id,sc.Duration}));Near(t.Recovery!.ExodiaHealing!.Mean,0);
            Yes(t.Recovery!.HostFinisherHealing!.StandardError>0);Near(t.Ttk.KillFraction,1);
        });
        check("슬램 캐패시터: 다른 적 처치 입력도 충전·도움 제외",()=>{
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Arca Titron");var b=B("Exodia Brave") with {WeaponId=w.Id,Capacity=CapacityCatalog.Load().Default(w.Id,null),Melee=new(),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeKill"),new(2,"HeavyMeleeKill"),new(3,"MeleeAssist")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);var e=t.Events.Where(e=>e.Kind=="capacitor").ToArray();Yes(e.Length==2);Near(e.Last().Amount,2);
        });
        check("라이프 스트라이크: 방어 후 직접 강공격만20%·강제 베기 피해 제외",()=>{
            var w=catalog.Weapons.Single(w=>w.EnglishName=="Hate");var mod=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");var slots=new ModSlot?[8];slots[0]=new(mod.Id,mod.MaxRank);
            var b=new Build(w.Id,slots,new(BaseInterval:60,Combo:12,Enemy:new(1e9,2700)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:"Heavy Attack"),Encounter:new(EncounterMode.TenSeconds));
            var t=CombatTimeline.Calculate(catalog,b,stances,64);Yes(t.Unsupported.Length==0&&t.StatusDamage>0);Near(t.Recovery!.HeavyHealing!.Mean,t.DirectDamage*.2);
            var normal=CombatTimeline.Calculate(catalog,b with {Melee=new()},stances,64);Near(normal.Recovery!.HeavyHealing!.Mean,0);
        });
        check("라이프 스트라이크: 건블레이드 강공격 탄환과 글레이브 수동 폭발 포함",()=>{
            var m=catalog.Mods.Single(m=>m.EnglishName=="Life Strike");var slots=new ModSlot?[8];slots[0]=new(m.Id,m.MaxRank);
            foreach(var name in new[]{"Redeemer Prime","Glaive Prime"})
            {
                var w=catalog.Weapons.Single(w=>w.EnglishName==name);var attack=name=="Redeemer Prime"?"Heavy Attack":"Charged Throw Recall Explosion";
                var b=new Build(w.Id,slots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack),Encounter:new(EncounterMode.FiveSeconds));
                var t=CombatTimeline.Calculate(catalog,b,stances,64);Yes(t.Unsupported.Length==0&&t.DirectDamage>0);Near(t.Recovery!.HeavyHealing!.Mean,t.DirectDamage*.2);
            }
        });
    }
}
