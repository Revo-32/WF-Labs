using System.Text.Json;
using TennoForge.Core;

static class RangedCoverageAudit
{
    public static void Combat(string path,bool approximate=false,bool extended=false)
    {
        // A deterministic path audit, not a statistical accuracy estimate or an exhaustive build proof.
        var rows=new List<object>();var failures=new List<string>();int executed=0,requiresSetup=0;
        var targets=new[]{"Grineer","Corpus"}.Select(f=>EnemyCatalog.Load().Enemies.First(e=>e.Faction==f&&e.SupportsEximus)).ToArray();
        foreach(var w in RangedEngine.Weapons.Where(w=>w.WeaponType!="Exalted Weapon"))
        {
            var attacks=RangedEngine.Attacks(w);
            for(int i=0;i<attacks.Length;i++)foreach(var target in targets)
            {
                var slots=new ModSlot?[8];
                string? augment=(w.EnglishName,i) switch
                {
                    ("Penta" or "Carmine Penta" or "Secura Penta",2)=>"Napalm Grenades",
                    ("Grakata" or "Prisma Grakata",1)=>"Wild Frenzy",
                    ("Ogris" or "Kuva Ogris",2)=>"Nightwatch Napalm",
                    ("Mutalist Quanta",3)=>"Bursting Mass",
                    ("Embolist",1 or 2)=>"Vile Discharge",
                    _=>null
                };
                if(augment is not null){var m=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==augment);slots[0]=new(m.Id,m.MaxRank);}
                if(extended)
                {
                    var common=RangedEngine.ModCatalog.Mods.FirstOrDefault(m=>m.EnglishName is "Split Chamber" or "Hell's Chamber" or "Barrel Diffusion"&&RangedEngine.Compatible(m,w));
                    if(common is not null)slots[1]=new(common.Id,common.MaxRank);
                }
                var build=new RangedBuild(w.Id,i,slots,Enumerable.Repeat("None",8).ToArray(),Enemy:new(target.Id,200,Eximus:true),Distance:extended?8:0,Conditions:new(AllowApproximations:approximate,GroupCount:extended?3:1,Headshot:extended));
                string status="executed";string? detail=null;double? damage=null;int? events=null;
                try
                {
                    var result=RangedCombat.Simulate(build,new(Duration:5,Trials:1,Seed:73519));
                    if(!double.IsFinite(result.Damage.Mean)||result.Damage.Mean<0||!double.IsFinite(result.Dps.Mean))throw new InvalidOperationException("피해가 유한한 비음수가 아닙니다.");
                    if(result.Points.Any(p=>!double.IsFinite(p.Time)||!double.IsFinite(p.Health)||!double.IsFinite(p.Armor)||!double.IsFinite(p.DirectDamage)||!double.IsFinite(p.StatusDamage)))throw new InvalidOperationException("시간별 결과에 비유한 수치가 있습니다.");
                    if(result.Events.Any(e=>!double.IsFinite(e.Time)||e.Time<0||e.Time>5+1e-8||!double.IsFinite(e.Value)))throw new InvalidOperationException("사건 시각·값 범위 오류");
                    damage=result.Damage.Mean;events=result.Events.Length;executed++;
                }
                catch(Exception e) when(e is ArgumentException or CombatRuleUnavailableException){status="requires-review";detail=e.Message;requiresSetup++;}
                catch(Exception e){status="failure";detail=e.ToString();failures.Add($"{w.EnglishName}/{i}/{target.Faction}: {detail}");}
                rows.Add(new{w.Id,w.EnglishName,attack=i,attackName=RangedEngine.S(attacks[i],"AttackName"),target=target.Id,augment,status,detail,damage,events,firingAttack=RangedAttackSelection.FiringAttack(w,i)});
            }
        }
        File.WriteAllText(path,JsonSerializer.Serialize(new{note="전체 공격을 5초·고정 시드 1표본으로 실행하는 경로 검사. 수치 정확도는 별도의 공식·사건 회귀 검사로 검증합니다. requires-review는 파생 공격/추가 입력/출처 공백을 구분해 검토해야 하며 통과 수에 포함하지 않습니다.",executed,requiresSetup,failures,rows},Catalog.Json));
        Console.WriteLine($"교전 경로 {rows.Count}: 실행 {executed}, 추가 검토 {requiresSetup}, 예외 {failures.Count}");Environment.ExitCode=failures.Count==0?0:1;
    }
    // An inventory of connected code paths, never a claim of mechanic completeness.
    public static void Write(string path)
    {
        var rows=new List<object>();
        foreach(var w in RangedEngine.Weapons.Where(w=>w.WeaponType!="Exalted Weapon"))
        {
            var attacks=RangedEngine.Attacks(w);
            for(int i=0;i<attacks.Length;i++)
            {
                var build=new RangedBuild(w.Id,i,new ModSlot?[8],Enumerable.Repeat("None",8).ToArray());
                string? error=null;string[] issues=[];double? damage=null;
                try{var result=RangedEngine.Calculate(build);damage=result.ExpectedShot;issues=result.TimingIssues;}
                catch(Exception e) when(e is ArgumentException or CombatRuleUnavailableException){error=e.Message;}
                rows.Add(new{w.Id,w.Name,w.EnglishName,w.Category,attack=i,attackName=RangedEngine.S(attacks[i],"AttackName"),source=w.Source,
                    fixedDamage=damage,fixedError=error,timingIssues=issues,
                    linkedChildren=RangedAttackLinks.Children(w.Id,i).Select(c=>c.Child).ToArray(),
                    chain=RangedChains.Get(w.Id,i),field=RangedFields.Get(w.Id),reloadGrenade=RangedReloadGrenades.Get(w.Id),
                    passive=RangedPassives.Get(w.Id)?.Kind,alternate=RangedAlternates.Get(w.Id),
                    collision=RangedCollisions.Get(w.Id,i),cluster=RangedClusters.Get(w.Id),incarnon=RangedIncarnons.Get(w.Id) is not null});
            }
        }
        File.WriteAllText(path,JsonSerializer.Serialize(new{note="고정 구성요소와 연결된 경로의 목록입니다. 모든 고유 동작의 구현 완료 증명이 아닙니다.",components=rows},Catalog.Json));
        Console.WriteLine($"공격 구성요소 {rows.Count}개 점검 목록 저장");
    }
}
