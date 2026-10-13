using System.Text.Json;

namespace TennoForge.Core;

public record InnodemContact(double Launch,double Impact,double Distance,int Projectile=1,int Target=1,double MotionMultiplier=1,double? LocationMultiplier=null,int? CoMultiplier=null);

public static class InnodemFlight
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Zariman/Melee/Dagger/ZarimanDaggerWeapon";
    public const string Source="https://wiki.warframe.com/w/Innodem?oldid=2816908";
    public const string Conditional="인노뎀 위치/컨오 분기를 지정한 조건부 결과";
    public static void Validate(InnodemContact[] contacts,int targets)
    {
        if(contacts.Length>512)throw new ArgumentException("인노뎀 접촉은 최대512회입니다.");
        foreach(var c in contacts)
        {
            MeleeEngine.Range(c.Launch,0,300,"인노뎀 발사");MeleeEngine.Range(c.Impact,c.Launch,300,"인노뎀 접촉");MeleeEngine.Range(c.Distance,0,21,"인노뎀 비행 거리");MeleeEngine.Range(c.Projectile,1,10000,"투사체 번호");MeleeEngine.Range(c.Target,1,targets,"대상 번호");MeleeEngine.Range(c.MotionMultiplier,0,100,"발사 타격 배율");
            if(c.LocationMultiplier is not {} location||c.CoMultiplier is not (1 or 2))throw new CombatRuleUnavailableException("인노뎀의 위치 배율·컨오1/2 분기는 위키 선택 규칙이 없습니다. 조건부 시나리오에 두 값을 명시하세요.");
            MeleeEngine.Range(location,0,100,"위치 배율 가정");
            if(Math.Abs(c.Impact-c.Launch-c.Distance/60)>1e-8)throw new ArgumentException("인노뎀 직선 비행은60m/s입니다. 명중 시각은 발사+거리/60과 같아야 합니다.");
        }
        foreach(var group in contacts.GroupBy(c=>c.Projectile))
        {
            var first=group.First();
            if(group.Any(c=>c.Launch!=first.Launch||c.MotionMultiplier!=first.MotionMultiplier))throw new ArgumentException("같은 인노뎀 투사체의 발사 시각·동작 배율은 같아야 합니다.");
            if(group.Select(c=>c.Target).Distinct().Count()!=group.Count())throw new ArgumentException("직선 비행에서 같은 대상을 두 번 관통하지 않습니다. 굴절/재진입 경로는 위키 미기재입니다.");
        }
    }
    public static Calculation Calculate(Catalog catalog,Build b,InnodemContact contact,bool useInitialComboFloor=true)
    {
        if(b.WeaponId!=WeaponId||b.Melee?.Incarnon!=true)throw new ArgumentException("인노뎀 변신·조준 활공 투사체가 필요합니다.");
        var conditions=new Dictionary<string,double>(b.Melee.Conditions??[]){["InnodemLocationMultiplier"]=contact.LocationMultiplier??1,["InnodemCoMultiplier"]=contact.CoMultiplier??2};
        var raw=MeleeRuleCatalog.Load().Weapon(WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Dagger Projectile");
        var policy=MeleeEngine.AttackPolicy(WeaponId,"Dagger Projectile") with {FollowThrough=false,HitCombo=false,Tennokai=false};
        return MeleeEngine.CalculateAttack(catalog,b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=contact.MotionMultiplier},Melee=b.Melee with {Attack="Dagger Projectile",Finisher=null,Distance=contact.Distance,TargetIndex=1,Tennokai=false,Conditions=conditions}},useInitialComboFloor:useInitialComboFloor,component:new(raw,policy));
    }
    internal static string Signature(Catalog catalog,Build b,InnodemContact f)
    {
        var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=b.Melee! with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(b.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,f,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
