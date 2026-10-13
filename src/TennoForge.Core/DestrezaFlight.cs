namespace TennoForge.Core;

/// <summary>One explicitly timed contact from one of the five launched rapiers.
/// Missing SC stays missing; an optional value is a disclosed scenario assumption.</summary>
public record DestrezaContact(double Launch,double Impact,int Volley=1,int Rapier=1,int Target=1,double? AssumedStatusChance=null);

public static class DestrezaFlight
{
    public const string Partial="미반영:데스트레자 투사체의 기본 상확이 미기재여서 무작위 상태를 제외한 직접 피해·강제 충격/베기만 계산합니다. 실제 전체 DPS나 하한값이 아닙니다.";
    public const string Assumed="미검증:데스트레자 투사체 기본 상확을 사용자가 지정한 조건부 계산입니다. 위키 확인값이 아닙니다.";
    public static void Validate(DestrezaContact[] contacts,int targets)
    {
        if(contacts.Length>512)throw new ArgumentException("유령 레이피어 명중은 최대512회입니다.");
        foreach(var c in contacts)
        {
            MeleeEngine.Range(c.Launch,0,300,"레이피어 발사 시각");MeleeEngine.Range(c.Impact,c.Launch,c.Launch+2,"레이피어 명중 시각 · 수명2초");
            MeleeEngine.Range(c.Volley,1,10000,"발사 묶음 번호");MeleeEngine.Range(c.Rapier,1,5,"레이피어 번호");MeleeEngine.Range(c.Target,1,targets,"레이피어 대상");
            if(c.AssumedStatusChance is {} sc)MeleeEngine.Range(sc,0,10,"레이피어 기본 상확 가정");
        }
        if(contacts.Select(c=>c.AssumedStatusChance).Distinct().Count()>1)throw new ArgumentException("같은 무기의 레이피어 기본 상확 가정은 같아야 합니다.");
        foreach(var g in contacts.GroupBy(c=>c.Volley))
        {
            if(g.Select(c=>c.Launch).Distinct().Count()!=1)throw new ArgumentException("한 번에 생성되는5개 레이피어는 같은 발사 시각을 사용합니다.");
            if(g.GroupBy(c=>(c.Rapier,c.Target)).Any(x=>x.Count()>1))throw new ArgumentException("같은 레이피어가 같은 대상을 두 번 관통하지 않습니다.");
        }
    }
    public static Calculation Calculate(Catalog catalog,Build b,DestrezaContact contact)
    {
        var windup=b.Melee?.Conditions?.GetValueOrDefault("HeavyAnimation")>0;
        var c=DestrezaMechanic.Evaluate(catalog,b,windup,assumedStatusChance:contact.AssumedStatusChance,useInitialComboFloor:false);
        if(contact.AssumedStatusChance is null)
        {
            var upper=DestrezaMechanic.Evaluate(catalog,b,windup,upper:true,useInitialComboFloor:false);
            if(c.CriticalChance!=upper.CriticalChance||c.CriticalMultiplier!=upper.CriticalMultiplier)
                throw new CombatRuleUnavailableException("데스트레자 기본 상확이 미기재여서 현재 상확 의존 치명 보너스를 확정할 수 없습니다. 성분 표의 근거 범위를 확인하거나 기본 상확 가정을 명시하세요.");
        }
        return c with {Limitations=c.Limitations.Append(contact.AssumedStatusChance is null?Partial:Assumed).ToArray()};
    }
    internal static string Signature(Catalog catalog,Build b,DestrezaContact contact)
    {
        var conditions=(b.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();
        // These specific current-state effects are documented for the rapiers.
        foreach(var key in new[]{"HeavyAnimation","IncarnonHeavyKills","HeavyKill","PunctureStacks"})conditions[key]=0;
        var input=b with {Scenario=b.Scenario with {Combo=1,Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=b.Melee with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=conditions}};
        var c=DestrezaMechanic.Evaluate(catalog,input,false,assumedStatusChance:contact.AssumedStatusChance,useInitialComboFloor:false);
        return System.Text.Json.JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
