using System.Text.Json;

namespace TennoForge.Core;

public record ExaltedWaveContact(double Launch,double Impact,double Distance,int Projectile=1,int Target=1,string Combo="Neutral",int Wave=1,int Cast=1,int FalloffModel=0,string? ImpactCombo=null,int ImpactGroup=1);

public static class ExaltedWaveFlight
{
    private static readonly Lazy<StanceCatalog> MotionStances=new(StanceCatalog.Load);
    public const string Conditional="익절티드 블레이드 감쇠 원문을 선택한 조건부 결과";
    public const string MotionSource="https://wiki.warframe.com/w/Exalted_Blade_%28Stance%29?oldid=2658083";
    public static bool Applies(string id)=>ExaltedRules.Find(id)?.Family is ExaltedFamily.Blade or ExaltedFamily.Wind;
    public static StanceHitDamage Resolve(Catalog catalog,StanceCatalog stances,Build b,ExaltedWaveContact f)
    {
        var definition=ExaltedRules.Find(b.WeaponId);if(definition?.Family is not (ExaltedFamily.Blade or ExaltedFamily.Wind))throw new ArgumentException("익절티드 블레이드 또는 데저트 윈드가 필요합니다.");
        var input=b with {Stance=new(definition.Stance!,f.Combo,stances.Revision),Melee=b.Melee! with {Distance=0,TargetIndex=1,Finisher=null}};
        var resolved=StanceEngine.Calculate(catalog,input,stances).Hits.Where(h=>h.Input!.Melee!.Attack is "Exalted Wave" or "Exalted Heavy Wave").SelectMany(h=>Enumerable.Repeat(h,h.Count)).ToArray();
        if(f.Wave<1||f.Wave>resolved.Length)throw new ArgumentException("해당 스탠스 동작의 파동 번호가 범위를 벗어납니다.");return resolved[f.Wave-1];
    }
    public static void Validate(Catalog catalog,StanceCatalog stances,Build b,ExaltedWaveContact[] contacts,int targets)
    {
        if(contacts.Length==0)return;if(!Applies(b.WeaponId))throw new ArgumentException("익절티드 파동 무기가 필요합니다.");
        if(contacts.Length>512)throw new ArgumentException("익절티드 파동 접촉은 최대512회입니다.");var blade=ExaltedRules.Find(b.WeaponId)!.Family==ExaltedFamily.Blade;
        foreach(var f in contacts)
        {
            MeleeEngine.Range(f.Launch,0,300,"파동 발사");MeleeEngine.Range(f.Impact,f.Launch,300,"파동 명중");MeleeEngine.Range(f.Distance,0,1000,"누적 비행 거리");MeleeEngine.Range(f.Projectile,1,10000,"파동 번호");MeleeEngine.Range(f.Target,1,targets,"명중 대상");MeleeEngine.Range(f.Cast,1,10000,"공격 입력 번호");MeleeEngine.Range(f.FalloffModel,0,2,"원문 감쇠 선택");
            var source=Resolve(catalog,stances,b,f);_=ImpactMotion(catalog,stances,b,source,f);
            if(blade&&Math.Abs(f.Impact-f.Launch-f.Distance/30)>1e-8)throw new ArgumentException("익절티드 블레이드 파동은30m/s입니다. 접촉 시각은 발사+누적 거리/30과 같아야 합니다.");
            if(blade&&f.Distance>0&&f.Distance<75&&f.FalloffModel==0)throw new CombatRuleUnavailableException("블레이드 파동의75m 시간식/70m 거리식이 원문에서 충돌합니다. 사용할 조건부 원문 분기를 선택하세요.");
        }
        foreach(var group in contacts.GroupBy(f=>f.Projectile))
        {
            var first=group.First();if(group.Any(f=>f.Launch!=first.Launch||f.Cast!=first.Cast||f.Combo!=first.Combo||f.Wave!=first.Wave||f.FalloffModel!=first.FalloffModel))throw new ArgumentException("같은 파동은 발사·공격 입력·동작·스탠스 파동 번호·감쇠 원문을 공유합니다.");
            var ordered=group.OrderBy(f=>f.Impact).ToArray();if(ordered.Zip(ordered.Skip(1)).Any(p=>p.Second.Distance<p.First.Distance))throw new ArgumentException("파동의 누적 비행 거리는 감소하지 않습니다.");
        }
        foreach(var cast in contacts.GroupBy(f=>f.Cast))
        {
            var first=cast.First();var source=Resolve(catalog,stances,b,first);
            if(cast.Any(f=>f.Launch!=first.Launch||f.Combo!=first.Combo||Resolve(catalog,stances,b,f).Group!=source.Group))throw new ArgumentException("같은 공격 입력의 파동은 발사 시각과 스탠스 입력 묶음을 공유해야 합니다.");
            var unique=cast.GroupBy(f=>f.Projectile).Select(g=>g.First()).ToArray();if(unique.Select(f=>f.Wave).Distinct().Count()!=unique.Length)throw new ArgumentException("스탠스의 같은 파동을 다른 번호로 복제할 수 없습니다.");
        }
    }
    public static Calculation Calculate(Catalog catalog,Build b,StanceHitDamage source,ExaltedWaveContact f,int targetIndex=1,bool useInitialComboFloor=true)
    {
        var c=new Dictionary<string,double>(b.Melee!.Conditions??[]){["BladeWaveFalloffModel"]=f.FalloffModel};
        var motion=ImpactMotion(catalog,MotionStances.Value,b,source,f);
        var input=b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=motion},Melee=b.Melee with {Attack=source.Input!.Melee!.Attack,Distance=f.Distance,TargetIndex=targetIndex,Finisher=null,Conditions=c}};
        return MeleeEngine.CalculateAttack(catalog,input,source.SourceHit,source.CoFactor,useInitialComboFloor:useInitialComboFloor);
    }
    public static double ImpactMotion(Catalog catalog,StanceCatalog stances,Build b,StanceHitDamage source,ExaltedWaveContact f)
    {
        // The stance article explicitly describes light waves reading the
        // animation at impact, including 100% when no attack is in progress.
        // Do not extend that pre-heavy-wave report to newer heavy cross waves.
        if(ExaltedRules.Find(b.WeaponId)?.Family!=ExaltedFamily.Blade||source.Input!.Melee!.Attack=="Exalted Heavy Wave")
        {
            if(f.ImpactCombo is not null)throw new ArgumentException("명중 순간 동작 배율은 블레이드 일반 파동의 규칙입니다.");
            return source.Input!.Scenario.MotionMultiplier;
        }
        if(f.ImpactCombo is null)return 1;
        var stance=stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)).Single(s=>s.Id==ExaltedRules.Find(b.WeaponId)!.Stance);
        var hits=stance.Combos.SingleOrDefault(c=>c.Key==f.ImpactCombo)?.Hits.Where(h=>h.Group==f.ImpactGroup&&h.AttackType is "" or "360" or "Sweep" or "Thrust" or "Direct").ToArray()??[];
        if(hits.Length==0||hits.Select(h=>h.Multiplier).Distinct().Count()!=1)throw new ArgumentException("명중 순간 진행 중인 스탠스 입력 묶음을 선택하세요.");
        return hits[0].Multiplier;
    }
    internal static Build WithResources(Build current,Build launched,bool heavy)
    {
        if(!heavy)return current;var c=new Dictionary<string,double>(current.Melee!.Conditions??[]);
        foreach(var key in new[]{"AnimosityStacks","TruthFlame"})c[key]=launched.Melee!.Conditions?.GetValueOrDefault(key)??0;
        return current with {Scenario=current.Scenario with {Combo=launched.Scenario.Combo},Melee=current.Melee with {Tennokai=launched.Melee!.Tennokai,Conditions=c}};
    }
    internal static string Signature(Catalog catalog,Build b,StanceHitDamage source,ExaltedWaveContact f,int targetIndex)
    {
        var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=b.Melee! with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(b.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,source,f,targetIndex,false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
