namespace TennoForge.Core;

public record HallOfMirrorsSetup(int Rank=3,double Strength=1,double Duration=1,bool Active=true,double? Remaining=null,
    int? MalevolenceRank=null,int Kills=0,double? CloneShields=null,double CloneOvershields=0,string ArcaneTriggers="Unresolved");
public record HologramCopy(int Number=1);
public record HologramContact(double Time,int Number=1,int Target=1,string Attack="Normal Attack",int StanceHit=0,double Distance=0,int TargetIndex=1);
public static class HallOfMirrorsRules
{
    public const string Source="https://wiki.warframe.com/w/Hall_of_Mirrors?oldid=2816130";
    public const string AugmentSource="https://wiki.warframe.com/w/Hall_of_Malevolence?oldid=2659191";
    public static double Duration(HallOfMirrorsSetup s)=>new[]{10d,15,20,25}[s.Rank]*s.Duration;
    public static double Scale(HallOfMirrorsSetup s)=>new[]{.05,.1,.15,.2}[s.Rank]*s.Strength;
    public static double Malevolence(HallOfMirrorsSetup s)=>s.MalevolenceRank is {} rank?1+(rank+2)*.01*Math.Min(10,s.Kills):1;
    public static void Validate(HallOfMirrorsSetup s)
    {
        MeleeEngine.Range(s.Rank,0,3,"홀 오브 미러즈 랭크");MeleeEngine.Range(s.Strength,0,20,"미라지 위력");MeleeEngine.Range(s.Duration,.01,20,"미라지 지속");
        MeleeEngine.Range(s.Kills,0,10,"홀 오브 멀레볼런스 중첩");if(s.MalevolenceRank is {} r)MeleeEngine.Range(r,0,3,"홀 오브 멀레볼런스 랭크");
        if(s.Remaining is {} left)MeleeEngine.Range(left,0,Duration(s),"분신 잔여 시간");if(s.CloneShields is {} shield)MeleeEngine.Range(shield,0,1e6,"분신 실드");MeleeEngine.Range(s.CloneOvershields,0,1e6,"분신 오버실드");
        if(s.ArcaneTriggers is not ("Unresolved" or "Article"))throw new ArgumentException("분신 아케인 발동 출처 분기 오류");
    }
    public static Build Prepare(Catalog catalog,Build build)
    {
        if(build.Melee?.Hologram is not {} copy)return build;
        var s=build.Melee.ExternalEquipment?.HallOfMirrors??throw new ArgumentException("분신 공격에는 홀 오브 미러즈 설정이 필요합니다.");Validate(s);MeleeEngine.Range(copy.Number,1,s.Rank+1,"분신 번호");
        if(!s.Active||(s.Remaining??Duration(s))<=0)throw new ArgumentException("활성 상태의 분신이 필요합니다.");
        var rules=MeleeRuleCatalog.Load();
        ArcaneSlot? CloneArcane(ArcaneSlot? slot)
        {
            if(slot is null)return null;var code=rules.Arcanes.Single(a=>a.Id==slot.Id).Code;
            if(code is not ("Doughty" or "Retaliation"))return null;
            if(slot.Active&&code=="Retaliation"&&s.CloneShields is null)throw new CombatRuleUnavailableException("분신 리탈리에이션은 분신 자신의 실드를 사용합니다. 분신 실드 수치를 지정하세요. 플레이어의 실드를 복사하지 않습니다. "+Source);
            return slot;
        }
        var arcane=build.Arcanes??new();
        // Galvanized base stats and conditional stacks are both excluded by the article.
        var galv=new HashSet<string>(StringComparer.Ordinal){"/Lotus/Upgrades/Mods/Melee/Expert/WeaponMeleeStatusChanceSPMod","/Lotus/Upgrades/Mods/Melee/Expert/WeaponHeavyAttackEfficiencySPMod","/Lotus/Upgrades/Mods/Melee/Expert/WeaponCritChanceSPMod"};
        var conditions=new Dictionary<string,double>(build.Melee.Conditions??[]);
        if(build.WeaponId==VinquibusState.WeaponId&&conditions.GetValueOrDefault("VinquibusPrecision")>0)throw new CombatRuleUnavailableException("빈퀴부스 정밀 충전의 분신 공유·소모 규칙은 위키 미기재입니다. 충전 없는 분신 사격·지연 폭발은 계산합니다. "+VinquibusState.Source);
        foreach(var k in new[]{"HotShotStacks","DemulcentHeat","DemulcentCold","DemulcentElectricity","DemulcentToxin","AnimosityStacks","GalvanizedStacks","SpringStacks"})conditions.Remove(k);
        return build with {Slots=build.Slots.Select(m=>m is not null&&galv.Contains(m.ModId)?null:m).ToArray(),Scenario=build.Scenario with {Combo=1},
            Arcanes=arcane with {Melee=CloneArcane(arcane.Melee),Warframe1=null,Warframe2=null,Operator=null,Shields=s.CloneShields??0,Overshields=s.CloneOvershields,PlayerArmor=null,ExposureStacks=0,FinisherKills=0},
            Melee=build.Melee with {Tennokai=false,Exodia=null,Conditions=conditions}};
    }
    public static Build Copy(Build owner,int clone)=>owner with {Melee=owner.Melee! with {Hologram=new(clone)}};
    public static void ValidateContacts(Build build,int targets)
    {
        var contacts=build.Encounter?.HologramContacts??[];var inputs=(build.Encounter?.WeaponActions??[]).Where(a=>a.Hologram>0).ToArray();var discs=(build.Encounter?.ZenistarDiscs??[]).Where(d=>d.Hologram>0).ToArray();
        var throwActors=(build.Encounter?.GlaiveFlights??[]).Select(f=>f.Hologram).Concat((build.Encounter?.ShieldFlights??[]).Select(f=>f.Hologram)).Where(n=>n>0).ToArray();
        if(contacts.Length==0&&inputs.Length==0&&discs.Length==0&&throwActors.Length==0)return;
        var s=build.Melee?.ExternalEquipment?.HallOfMirrors??throw new ArgumentException("분신 명중에는 홀 오브 미러즈 설정이 필요합니다.");Validate(s);
        if(ExaltedRules.Find(build.WeaponId) is not null)throw new ArgumentException("홀 오브 미러즈는 미라지 자신의 일반 근접무기를 복제합니다.");
        if(contacts.Length>512)throw new ArgumentException("분신 명중은 최대512개입니다.");
        foreach(var c in contacts){MeleeEngine.Range(c.Time,0,300,"분신 명중 시각");MeleeEngine.Range(c.Number,1,s.Rank+1,"분신 번호");MeleeEngine.Range(c.Target,1,targets,"분신 대상");MeleeEngine.Range(c.StanceHit,0,512,"분신 스탠스 타격");MeleeEngine.Range(c.Distance,0,1000,"분신-대상 거리");MeleeEngine.Range(c.TargetIndex,1,1000,"분신 관통 대상 순서");}
        foreach(var c in inputs)MeleeEngine.Range(c.Hologram,1,s.Rank+1,"고유 공격 분신 번호");
        foreach(var d in discs)MeleeEngine.Range(d.Hologram,1,s.Rank+1,"원반 분신 번호");
        foreach(var n in throwActors)MeleeEngine.Range(n,1,s.Rank+1,"투척 분신 번호");
        var rules=MeleeRuleCatalog.Load();var triggers=new[]{build.Arcanes?.Melee,build.Arcanes?.Warframe1,build.Arcanes?.Warframe2}.Where(a=>a is not null).Any(a=>rules.Arcanes.Single(r=>r.Id==a!.Id).Code is not ("Doughty" or "Retaliation"));
        if(triggers&&s.ArcaneTriggers!="Article")throw new CombatRuleUnavailableException("분신 아케인:본문은 미발동, 버그 항목은 인플루언스 등 발동으로 서로 상충합니다. 본문 기준을 선택한 시나리오에서 분신의 추가 아케인 발동을 제외할 수 있습니다. "+Source);
    }
}

public sealed class HallOfMirrorsState
{
    private readonly HallOfMirrorsSetup setup;
    private readonly HashSet<int> removed=[];
    private double end;
    public int Kills {get;private set;}
    public int Generation {get;private set;}
    public HallOfMirrorsState(HallOfMirrorsSetup s)
    {HallOfMirrorsRules.Validate(s);setup=s;end=s.Active?s.Remaining??HallOfMirrorsRules.Duration(s):0;Kills=s.Active?s.Kills:0;}
    public bool Active(int clone,double time)=>time<end&&clone>=1&&clone<=setup.Rank+1&&!removed.Contains(clone);
    public bool Any(double time)=>time<end&&removed.Count<setup.Rank+1;
    public HallOfMirrorsSetup At(double time)=>setup with {Active=Any(time),Remaining=Math.Max(0,end-time),Kills=Any(time)?Kills:0};
    public double NextExpiry(double time)=>end>time+1e-9?end:double.PositiveInfinity;
    public void Kill(double time){if(Any(time)&&setup.MalevolenceRank is not null)Kills=Math.Min(10,Kills+1);}
    public void Remove(int clone){MeleeEngine.Range(clone,1,setup.Rank+1,"무효화된 분신");removed.Add(clone);if(removed.Count==setup.Rank+1){end=0;Kills=0;}}
    public void End(){end=0;removed.Clear();Kills=0;}
    public bool Cast(double time)
    {if(Any(time))return false;removed.Clear();Kills=0;Generation++;end=time+HallOfMirrorsRules.Duration(setup);return true;}
}
