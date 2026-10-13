using System.Text.Json;

namespace TennoForge.Core;

// Actual entry/exit in an existing acid pool. This is not a guessed ground
// trajectory or a launch animation; Created identifies the pool's age.
public record CaustacystPoolWindow(double Enter,double Exit,double Created=0,int Target=1,double? BlindDuration=null);
public record CaustacystPoolTick(double Time,int Window,int Target,bool Entry);
public record CaustacystWaveEvidence(Calculation Infobox,Calculation Article,double RemainingLifetime,string[] Differences);

public static class CaustacystMechanic
{
    public const string WeaponId="/Lotus/Weapons/Infested/Melee/InfEmbolistScythe/InfestedScythe";
    public const string CodaId="/Lotus/Weapons/Infested/InfestedLich/Melee/CodaCaustacyst/CodaCaustacyst";
    public const string Source="https://wiki.warframe.com/w/Caustacyst?oldid=2785124";
    public const string CodaSource="https://wiki.warframe.com/w/Coda_Caustacyst?oldid=2795803";
    public static bool Applies(string id)=>id is WeaponId or CodaId;
    public static CaustacystPoolTick[] Schedule(string weaponId,CaustacystPoolWindow[] windows,int targets)
    {
        if(!Applies(weaponId)&&windows.Length>0)throw new ArgumentException("카우스타시스트 계열의 웅덩이 설정입니다.");
        if(windows.Length>128)throw new ArgumentException("웅덩이 체류 구간은 최대128개입니다.");
        var ticks=new List<CaustacystPoolTick>();
        for(var i=0;i<windows.Length;i++)
        {
            var w=windows[i];MeleeEngine.Range(w.Created,0,300,"웅덩이 생성 시각");MeleeEngine.Range(w.Enter,w.Created,300,"웅덩이 진입 시각");MeleeEngine.Range(w.Exit,w.Enter+1e-6,300,"웅덩이 이탈 시각");MeleeEngine.Range(w.Target,1,targets,"웅덩이 대상 번호");
            if(w.BlindDuration is {} blind)MeleeEngine.Range(blind,1e-6,300,"실명 지속시간 가정");
            if(w.Exit>w.Created+4+1e-9)throw new CombatRuleUnavailableException("웅덩이 지속시간은 위키 본문8초/데이터 표4초가 상충합니다. 생성 후4초까지 양쪽에 공통인 구간만 확정할 수 있습니다. "+(weaponId==CodaId?CodaSource:Source));
            if(windows.Take(i).Any(p=>p.Target==w.Target&&Math.Max(p.Enter,w.Enter)<Math.Min(p.Exit,w.Exit)))throw new CombatRuleUnavailableException("같은 적에 여러 웅덩이가 겹칠 때 피해 틱 공유/독립 규칙은 위키 미기재입니다. 체류 구간이 겹치지 않는 시나리오가 필요합니다.");
            for(var n=0;w.Enter+n<w.Exit-1e-9;n++)ticks.Add(new(w.Enter+n,i,w.Target,n==0));
        }
        return ticks.OrderBy(t=>t.Time).ThenBy(t=>t.Window).ToArray();
    }
    public static Calculation Pool(Catalog catalog,Build build)
    {
        if(!Applies(build.WeaponId))throw new ArgumentException("카우스타시스트 계열이 필요합니다.");
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);var attack=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Corrosive Pool");
        var policy=MeleeEngine.AttackPolicy(build.WeaponId,"Corrosive Pool") with {Tennokai=false,FollowThrough=false,HitCombo=false,EvolutionDamage=false,EvolutionStats=false};
        return MeleeEngine.CalculateAttack(catalog,Input(build,"Corrosive Pool"),component:new(attack,policy,NoEnergyChannel:true,FixedStatusChance:true));
    }
    public static CaustacystWaveEvidence Wave(Catalog catalog,Build build,double elapsed,int priorHits=0)
    {
        if(!Applies(build.WeaponId))throw new ArgumentException("카우스타시스트 계열이 필요합니다.");
        MeleeEngine.Range(elapsed,0,300,"파동 비행 시간");MeleeEngine.Range(priorHits,0,10000,"앞서 관통한 대상 수");
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);var original=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Corrosive Wave");
        Calculation Variant(bool article)
        {
            var raw=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(original.GetRawText())!;
            raw.Remove("Falloff");raw.Remove("Range");
            if(article)raw["StatusChance"]=JsonSerializer.SerializeToElement(.37);
            var policy=MeleeEngine.AttackPolicy(build.WeaponId,"Corrosive Wave") with {FollowThrough=false};
            // The article explicitly subtracts 20 percentage points per prior
            // enemy, shortening life by .5s. Never multiply by blade's .6.
            var factor=Math.Max(0,1-elapsed/(article?2.5:35d/15)-.2*priorHits);
            return MeleeEngine.CalculateAttack(catalog,Input(build,"Corrosive Wave"),component:new(JsonSerializer.SerializeToElement(raw),policy,DamageMultiplier:factor));
        }
        var differences=new List<string>{"파동 폭 도입부5m/본문3m, 최대거리 도입부30m/본문37.5m/데이터 표35m가 상충합니다. 두 결과는 출처 분기이며 표본 오차가 아닙니다."};
        if(build.WeaponId==CodaId)differences.Add("코다 파동 상확은 본문37%/데이터 표41%가 상충합니다.");
        return new(Variant(false),Variant(true),Math.Max(0,2.5-elapsed-.5*priorHits),differences.ToArray());
    }
    private static Build Input(Build b,string attack)=>b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=1},Melee=b.Melee! with {Attack=attack,Tennokai=false,Finisher=null,TargetIndex=1,Distance=0}};
    public static string PayloadSignature(Catalog catalog,Build b)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Statuses=[],Debuffs=[],Faction="Neutral",Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Pool(catalog,neutral);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
