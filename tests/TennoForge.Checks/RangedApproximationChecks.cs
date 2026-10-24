using TennoForge.Core;

static class RangedApproximationChecks
{
    public static void Run(Action<string,Action> check)
    {
        void Yes(bool x){if(!x)throw new Exception("조건 불일치");}
        void Near(double x,double y){if(!double.IsFinite(x)||Math.Abs(x-y)>1e-7*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        var enemy=EnemyCatalog.Load().Enemies.First(e=>e.Faction=="Grineer"&&e.SupportsEximus);
        RangedBuild Build(string name,int attack=1,int rounds=0,params string[] mods)
        {
            var w=RangedEngine.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];
            for(int i=0;i<mods.Length;i++){var m=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==mods[i]);slots[i]=new(m.Id,m.MaxRank);}
            return new(w.Id,attack,slots,Enumerable.Repeat("None",8).ToArray(),Enemy:new(enemy.Id,9999,Eximus:true),Conditions:new(AllowApproximations:true,PhantasmaChargeAmmo:rounds));
        }
        // Expected values transcribed from the pinned current wiki descriptions, not the timing JSON.
        foreach(var (name,start,attack,ramp) in new[]{("Amprex",.30,0,.6),("Flux Rifle",.25,0,.6),("Glaxion Vandal",.30,0,.6),("Ignis",.35,0,.6),("Ignis Wraith",.35,0,.6),("Quanta",.30,0,.6),("Quanta Vandal",.30,0,.6),("Tenet Quanta",.30,0,.6),("Atomos",.35,0,.6),("Cycron",.30,0,.6),("Embolist",.45,0,.6),("Gammacor",.25,0,.6),("Nukor",.30,0,.6),("Spectra",.25,0,.6),("Spectra Vandal",.25,0,.6),("Synoid Gammacor",.30,0,.6),("Basmu",.20,2,.5)})
            check(name+" · 원문 빔 시작·상승 시간·교전 상태 일치",()=>
            {
                var cadence=RangedEngine.Calculate(Build(name,attack)).Cadence!;Yes(cadence.Beam);Near(cadence.BeamStart,start);Near(cadence.BeamRampSeconds,ramp);
                var state=new RangedFireState(cadence);Near(state.Consume(cadence,0).BeamScale,start);
                state.ScheduleAt(ramp/2);Near(state.Consume(cadence,0).BeamScale,(1+start)/2);
                state.ScheduleAt(ramp);Near(state.Consume(cadence,0).BeamScale,1);
                var expected=cadence.Shots(1).ToArray();state=new(cadence);
                foreach(var shot in expected){Near(state.Next,shot.Time);Near(state.Consume(cadence,0).BeamScale,shot.BeamScale);state.Advance(cadence,0);}
            });
        foreach(var name in new[]{"Phantasma","Phantasma Prime"})
        {
            check(name+" · 빔 15%→100%·탄약 0.5",()=>{var r=RangedEngine.Calculate(Build(name,0));Near(r.Cadence!.BeamStart,.15);Near(r.Cadence.AmmoCost,.5);var shots=r.Cadence.Shots(1).ToArray();Near(shots[0].BeamScale,.15);Near(shots.First(s=>s.Time>=.6).BeamScale,1);});
            check(name+" · 완충 11발/초·연사 모드 독립",()=>{var bare=RangedEngine.Calculate(Build(name));var fast=RangedEngine.Calculate(Build(name,1,0,"Shotgun Barrage"));Near(bare.Cadence!.Charge,1);Near(fast.Cadence!.Charge,1);Near(bare.Cadence.AmmoCost,11);});
            check(name+" · 충전 피해·상태 씨드 비례",()=>{var two=RangedEngine.Calculate(Build(name,1,2));var full=RangedEngine.Calculate(Build(name));Near(full.ExpectedShot/two.ExpectedShot,5.5);Near(full.StatusBaseFactor/two.StatusBaseFactor,5.5);Near(full.Multishot,two.Multishot);});
            check(name+" · 발사 뒤 재장전과 자탄 5개 연결",()=>{var b=Build(name);var r=RangedCombat.Simulate(b,new(3,1,FirstShot:true));Near(r.Events.Count(e=>e.Kind=="collision"),5);Near(r.Events.Count(e=>e.Kind=="impact"&&e.Detail=="Cluster Bombs Impact"),5);Near(r.Events.Count(e=>e.Kind=="explosion"&&e.Detail=="Cluster Bombs Explosion"),5);Near(r.Events.Single(e=>e.Kind=="shot").Time,1);Yes(r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.Contains("2발 충전")));});
            check(name+" · 비행 중 탄창 변화가 자탄 충전 피해를 변경하지 않음",()=>{var two=RangedCombat.Simulate(Build(name,1,2),new(3,1,FirstShot:true));var full=RangedCombat.Simulate(Build(name),new(3,1,FirstShot:true));Near(full.Damage.Mean/two.Damage.Mean,5.5);});
            check(name+" · 직접 지정한 자탄 빗나감 보존",()=>{var b=Build(name);b=b with{Conditions=b.Conditions! with{ClusterLandings=[]}};var r=RangedCombat.Simulate(b,new(3,1,FirstShot:true));Yes(!r.Events.Any(e=>e.Kind=="collision"));Yes(!r.Notes.Any(n=>n.Contains("0.5초 뒤")));});
            check(name+" · 잔탄 충전·무한 탄약 상한",()=>{var b=Build(name);var r=RangedEngine.Calculate(b with{Conditions=b.Conditions! with{LoadedAmmo=3,InfiniteAmmoSeconds=10}});Near(r.Cadence!.AmmoCost,3);Near(r.Cadence.Charge,3/11d);r=RangedEngine.Calculate(b with{Conditions=b.Conditions! with{InfiniteAmmoSeconds=10}});Near(r.Cadence!.AmmoCost,11);});
        }
        check("팬타즈마 · 헬스 체임버 6+5×1.2=12",()=>Near(RangedEngine.Calculate(Build("Phantasma",0,0,"Hell's Chamber")).Multishot,12));
        check("팬타즈마 · 탄창 모드로 충전 시간·피해 증가",()=>{var b=Build("Phantasma",1,0,"Ammo Stock");var r=RangedEngine.Calculate(b);Near(r.Cadence!.Charge,r.Cadence.Magazine/11d);Near(r.Cadence.AmmoCost,r.Cadence.Magazine);Yes(r.Cadence.Magazine>11);});
        check("근사 · 직접 입력 우선·해제 시 원본 조건 보존",()=>{var b=Build("Cedo");b=b with{Conditions=b.Conditions! with{GlaiveReturnSeconds=7}};Near(RangedApproximations.Resolve(b).Conditions!.GlaiveReturnSeconds!.Value,7);b=b with{Conditions=new(AllowApproximations:false)};Yes(RangedApproximations.Resolve(b).Conditions!.GlaiveReturnSeconds is null);});
        check("근사 · 반복 준비는 입력·근거를 중복하지 않음",()=>{var a=RangedApproximations.Resolve(Build("Phantasma"));var b=RangedApproximations.Resolve(a);Yes(ReferenceEquals(a,b));Yes(a.Conditions!.ApproximationNotes!.Distinct().Count()==a.Conditions.ApproximationNotes!.Length);});
        foreach(var (name,attack) in new[]{("Zarr",0),("Kuva Bramma",0),("Kulstar",0),("Cedo",1),("Onos",1),("Simulor",0),("Synoid Simulor",0),("Aksomati Prime",0),("Pox",0),("Sporelacer (Secondary)",0),("Tombfinger (Secondary)",0),("Vermisplicer (Primary)",0),("Vermisplicer (Secondary)",0),("Azima",2),("Cantare",0),("Proboscis Cernos",0),("Vectis Prime",1),("Stug",2),("Hema",0),("Catabolyst",0)})
            check(name+" · 기본 근사 교전 10초 경로",()=>{var r=RangedCombat.Simulate(Build(name,attack),new(10,1));Yes(double.IsFinite(r.Damage.Mean)&&r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.StartsWith("근사 · ")));Yes(r.Events.All(e=>e.Time>=0&&e.Time<=10));});
        foreach(var (name,mod,attack) in new[]{("Flux Rifle","Flux Overdrive",0),("Tenet Flux Rifle","Flux Overdrive",0),("Higasa","Higasa Serration",1),("Penta","Napalm Grenades",0),("Simulor","Spontaneous Singularity",0),("Phantasma","Galvanized Savvy",1)})
            check(name+" · "+mod+" 근사 교전",()=>{var b=Build(name,attack,0,mod);b=b with{Conditions=b.Conditions! with{Kills=2,GroupCount=3}};var r=RangedCombat.Simulate(b,new(10,1));Yes(double.IsFinite(r.Damage.Mean)&&r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.StartsWith("근사 · ")));});
        check("자이모스 · 근사 약점 굴착·포자 경로",()=>{var b=Build("Zymos",0) with{Enemy=new(enemy.Id,9999)};var r=RangedCombat.Simulate(b with{Conditions=b.Conditions! with{Headshot=true,GroupCount=3}},new(3,1,FirstShot:true));Yes(r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.Contains("굴착")));Yes(r.Events.Any(e=>e.Detail.Contains("Spore")));});
        foreach(var arcane in new[]{"ResidualBoils","ResidualMalodor","ResidualShock","ResidualViremia","PaxSeeker"})
            check("키트건 · "+arcane+" 근사 발동",()=>{var b=Build("Rattleguts (Secondary)",0);b=b with{Kitgun=new("Haymaker","Splat",arcane),Enemy=new(enemy.Id,10),Conditions=b.Conditions! with{Headshot=true,GroupCount=12}};var r=RangedCombat.Simulate(b,new(10,1));Yes(r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.StartsWith("근사 · ")));});
        check("톰핑거 근사 · 그립 총 기본 피해 보존",()=>{var b=Build("Tombfinger (Secondary)",0);var direct=RangedEngine.Calculate(b);var radial=RangedEngine.Calculate(b with{AttackIndex=1});Near(direct.BaseDamage+radial.BaseDamage,36);});
        check("스포어레이서 근사 · 자탄 합계가 본탄 폭발과 같음",()=>{var b=Build("Sporelacer (Secondary)",0);var main=RangedEngine.Calculate(b with{AttackIndex=1});var contact=RangedEngine.Calculate(b with{AttackIndex=2});var explosion=RangedEngine.Calculate(b with{AttackIndex=3});Near(3*(contact.BaseDamage+explosion.BaseDamage),main.BaseDamage);});
        check("팬타즈마 자탄 · 독립 발사 DPS와 본탄 교전 구분",()=>{var r=RangedEngine.Calculate(Build("Phantasma",3));Yes(r.BurstDps is null&&r.SustainedDps is null);Yes(r.TimingIssues.Any(n=>n.Contains("독립 발사")));});
        foreach(var (name,child,parent) in new[]{("Phantasma",3,1),("Phantasma Prime",4,1),("Tombfinger (Secondary)",1,0),("Sporelacer (Secondary)",3,0),("Basmu",1,0),("Basmu",2,2),("Cantare",1,0),("Catabolyst",3,0),("Quanta",3,1),("Scyotid",1,1)})
            check(name+$" · 구성요소 {child} → 발사 동작 {parent}",()=>Near(RangedAttackSelection.FiringAttack(RangedEngine.Resolve(Build(name,child)),child),parent));
        check("콴타 · 멀티샷 큐브 근사 경로·직접 경로 우선",()=>{var b=RangedApproximations.Resolve(Build("Quanta",1,0,"Split Chamber") with{Distance=8});var stats=RangedEngine.Calculate(b);var paths=RangedQuanta.Routes(b,stats,2,0);Near(paths.Length,2);Yes(paths.All(p=>p.Contacts!.Length==1));var r=RangedCombat.Simulate(b,new(5,1));Yes(r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.Contains("같은 직선")));b=b with{Conditions=b.Conditions! with{CubeRoutes=[new(0,5,Contacts:[]),new(0,-5,Contacts:[])]}};Yes(RangedQuanta.Routes(b,stats,2,0).All(p=>p.Contacts!.Length==0));});
        foreach(var (name,attack,mod) in new[]{("Penta",0,"Tether Grenades"),("Scyotid",1,"Barrel Diffusion"),("Torid",0,"Adhesive Blast")})
            check(name+" · 미기재 이동·신관 기본 근사",()=>{var b=Build(name,attack,0,mod) with{Distance=8};var r=RangedCombat.Simulate(b,new(10,1));Yes(double.IsFinite(r.Damage.Mean)&&r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.StartsWith("근사 · ")));});
        check("톰핑거 보조 · 직격 뒤 실제 폭발 사건",()=>{var r=RangedCombat.Simulate(Build("Tombfinger (Secondary)",0),new(1,1,FirstShot:true));Yes(r.Events.Any(e=>e.Kind=="impact"));Yes(r.Events.Any(e=>e.Kind=="explosion"));});
        check("팬타즈마 · 발사 후 탄창 한도 감소도 충전량 보존",()=>{var b=Build("Phantasma",3,2);var r=RangedEngine.Calculate(b);var snapshot=RangedEngine.Calculate(b with{Conditions=b.Conditions! with{PhantasmaChargeSnapshot=22}});Near(snapshot.ExpectedShot/r.ExpectedShot,11);});
        check("코모렉스 · 최대 줌 IPS 가산·원소 기준 피해 보존",()=>{var low=RangedEngine.Calculate(Build("Komorex",0,0,"Cryo Rounds"));var high=RangedEngine.Calculate(Build("Komorex",2,0,"Cryo Rounds"));Near(high.BaseDamage,97);Near(high.BaseFactor,low.BaseFactor);Near(high.Damage[DamageType.Cold],low.Damage[DamageType.Cold]);Near(high.Modifiers!["Impact"],1);Near(high.Modifiers["Puncture"],1);Near(high.Modifiers["Slash"],1);Near(high.FireRate!.Value,1.25);Near(high.StatusBaseFactor,low.StatusBaseFactor);});
        check("코모렉스 · 캐논네이드 연사 잠금·물리 모드와 가산",()=>{var b=Build("Komorex",2,0,"Semi-Rifle Cannonade","Fanged Fusillade");var r=RangedEngine.Calculate(b);Near(r.FireRate!.Value,5);Near(r.Modifiers!["Slash"],2.2);Yes(r.Modifiers["Damage"]>0);});
        check("코모렉스 · 직격 뒤 최대 줌 폭발·불확실 출력 명시",()=>{var b=Build("Komorex",2);var r=RangedCombat.Simulate(b,new(1,1,FirstShot:true));Yes(r.Events.Any(e=>e.Kind=="explosion"));Yes(r.Notes.Any(n=>n.Contains("106")));Near(RangedEngine.Calculate(b with{AttackIndex=1}).BaseDamage,106);});
        check("코모렉스 · 마지막 관통 대상에서만 폭발",()=>{var b=Build("Komorex",2,0,"Metal Auger") with{Distance=8};b=b with{Conditions=b.Conditions! with{TargetsInLine=true,GroupCount=3,GroupSpacing=5}};var r=RangedCombat.Simulate(b,new(3,1,FirstShot:true));var explosions=r.Events.Where(e=>e.Kind=="explosion").ToArray();if(explosions.Length!=1)throw new Exception(System.Text.Json.JsonSerializer.Serialize(r.Events.Where(e=>e.Kind is "explosion" or "impact" or "penetration")));Near(explosions[0].Target,2);});
        check("파라시스트 · 작살 충전·명중 직후부터4초간5틱",()=>{var b=Build("Paracyst",1);Near(RangedEngine.Calculate(b).Cadence!.Charge,.25);var r=RangedCombat.Simulate(b,new(5,1,FirstShot:true));var ticks=r.Events.Where(e=>e.Detail=="Infested Harpoon Contact").ToArray();Near(ticks.Length,5);Near(ticks.First().Time,.25);Near(ticks.Last().Time,4.25);});
        check("파라시스트 · 방어층 없는 원거리 적 끌어오기",()=>{var b=Build("Paracyst",1) with{Distance=40,Enemy=new(enemy.Id,9999)};var r=RangedCombat.Simulate(b,new(5,1,FirstShot:true));Yes(r.Events.Any(e=>e.Kind=="pull"));Yes(r.Notes.Any(n=>n.Contains("25m")));});
        check("가스 반경 · 상충하는 중간 중첩을 명시적 근사",()=>{var b=Build("Cyanex",0,0,"Barrel Diffusion") with{Distance=8};b=b with{Conditions=b.Conditions! with{GroupCount=3,Headshot=true}};var r=RangedCombat.Simulate(b,new(5,1));Yes(r.Damage.Mean>0);Yes(r.Notes.Any(n=>n.Contains("가스 중간")));});
        foreach(var rank in Enumerable.Range(0,4))
            check($"팍스 시커 {rank}랭크 · 유도탄 경로 수",()=>{var b=Build("Rattleguts (Secondary)",0) with{Kitgun=new("Haymaker","Splat","PaxSeeker",rank)};Near(RangedApproximations.Resolve(b).Conditions!.PaxSeekerPaths!.Length,rank+1);});
        check("널리파이어 · 근사 막 축소·타격 경로",()=>{var b=Build("Braton",0);b=b with{Conditions=b.Conditions! with{Nullifier=new(Kind:"Crewman")}};var r=RangedCombat.Simulate(b,new(10,1));Yes(r.Events.Any(e=>e.Kind=="nullifierShrink"));Yes(r.Notes.Any(n=>n.Contains("60fps")));});
        check("자이모스 · 오버가드 약점에서 포자 생성 금지",()=>{var b=Build("Zymos",0);var r=RangedCombat.Simulate(b with{Conditions=b.Conditions! with{Headshot=true}},new(3,1,FirstShot:true));Yes(r.Events.Any(e=>e.Kind=="explosion"));Yes(!r.Events.Any(e=>e.Kind=="zymosBurrow"));Yes(r.Notes.Any(n=>n.Contains("기절 면역")));});
        check("칸타레 · 30개 초과 시 FIFO 복귀 근사",()=>{var b=Build("Cantare",0,0,"Barrel Diffusion");b=b with{Conditions=b.Conditions! with{InfiniteAmmoSeconds=10}};var r=RangedCombat.Simulate(b,new(10,1));Yes(r.Notes.Any(n=>n.Contains("가장 오래된")));Yes(r.Events.Any(e=>e.Kind=="cantareChorale"));});
        check("화염 재점화 · 회복 중 방어 감소 연속성 근사",()=>{var b=Build("Ignis",0,0,"Rifle Aptitude");b=b with{Conditions=b.Conditions! with{Statuses=new(){[DamageType.Heat]=1},ExternalEvents=[new(.2,"Holster",7.8)]}};var r=RangedCombat.Simulate(b,new(10,1));Yes(r.Notes.Any(n=>n.Contains("화염 재점화")));Yes(r.Points.All(p=>double.IsFinite(p.Armor)&&p.Armor>=0));});
    }
}
