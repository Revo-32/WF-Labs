using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;

public record RangedBodyPenetration(string WeaponId,int[] Attacks,WikiSource Source);
public record RangedPenetrationHit(int Target,double AfterFirstHit);
public static class RangedPenetration
{
    public static readonly RangedBodyPenetration[] Rules=Load();
    static RangedBodyPenetration[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-penetration.json")!;return JsonSerializer.Deserialize<RangedBodyPenetration[]>(s,Catalog.Json)!;}
    public static bool InfiniteBody(ReferenceWeapon weapon,int attack)=>Rules.Any(r=>r.WeaponId==weapon.Id&&r.Attacks.Contains(attack))||RangedKitguns.Get(weapon.Id)?.Chamber=="Catchmoon"&&attack==0;
    public static double PlayerDistance(RangedConditions c,double firstDistance,int target)=>c.TargetsInLine?firstDistance+target*c.GroupSpacing:Math.Sqrt(firstDistance*firstDistance+Math.Pow(target*c.GroupSpacing,2));
    public static void Validate(RangedConditions c)
    {
        if(c.TargetBodyThickness is {} thickness&&(!double.IsFinite(thickness)||thickness<=0)||c.PenetrationGapSeconds is {} gap&&(!double.IsFinite(gap)||gap<0||gap>300))throw new ArgumentException("관통 경로의 두께·시간 오류");
        if(c.TargetsInLine&&c.TargetBodyThickness is {} width&&width>c.GroupSpacing)throw new ArgumentException("적 사이 간격이 지정한 몸통 두께보다 짧습니다.");
    }
    public static bool Locked(ReferenceWeapon weapon,int attack)
    {
        if(weapon.Id==RangedKomorex.Id)return false;
        if(RangedZymos.Applies(weapon))return true;
        if(RangedFields.Get(weapon.Id)?.RequireDuration==true||RangedStug.Applies(weapon)&&RangedStug.Launch(weapon,attack))return true;
        var raw=RangedEngine.Attacks(weapon)[attack];
        if(RangedEngine.S(raw,"ShotType")!="Projectile"||RangedEngine.S(weapon.Raw,"Family") is "Tysis" or "Sporothrix" or "Quanta")return false;
        // Wiki Punch Through: projectile AoE attacks have a locked geometry stat;
        // hitscan explosions and the explicit penetrating projectile exceptions are separate.
        return RangedAttackLinks.Children(weapon.Id,attack).Any()&&(RangedEngine.N(raw,"PunchThrough")??0)==0;
    }
    public static double Geometry(ReferenceWeapon weapon,int attack,RangedResult result)=>Locked(weapon,attack)?0:Math.Max(0,(RangedEngine.N(RangedEngine.Attacks(weapon)[attack],"PunchThrough")??0)+result.Modifiers!.GetValueOrDefault("PunchThrough"));
    public static RangedPenetrationHit[] Plan(ReferenceWeapon weapon,int attack,RangedResult result,RangedConditions c,int firstTarget)
    {
        Validate(c);if(!c.TargetsInLine||c.GroupCount<=firstTarget+1)return [];
        var raw=RangedEngine.Attacks(weapon)[attack];if(RangedEngine.S(raw,"ShotType")=="AoE")return [];
        var barrage=attack==1&&result.Modifiers!.GetValueOrDefault("SentientBarrageCritical")>0;var infinite=barrage||InfiniteBody(weapon,attack);var available=Geometry(weapon,attack,result);if(!infinite&&available<=0)return [];
        if(!barrage&&weapon.Id!=RangedKomorex.Id&&RangedAttackLinks.Children(weapon.Id,attack).Any())throw new CombatRuleUnavailableException("이 무기의 관통 후 추가 폭발·피해 사건 연결을 확인해야 합니다. 일반 관통으로 대체하지 않습니다.");
        var count=c.GroupCount-firstTarget-1;
        if(!infinite)
        {
            var thickness=c.TargetBodyThickness??throw new CombatRuleUnavailableException("관통할 몸통의 유효 두께를 설정하세요. 적·자세·조준 위치에 따라 달라 위키의 관통 거리만으로 명중 수를 정할 수 없습니다.");
            count=Math.Min(count,(int)Math.Min(32,Math.Floor(available/thickness+1e-9)));
        }
        if(count==0)return [];
        double delay=0;
        if(RangedEngine.S(raw,"ShotType")=="Projectile")
        {
            if(infinite){var speed=(RangedEngine.N(raw,"ShotSpeed")??0)*(1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed"));if(speed<=0)throw new CombatRuleUnavailableException("몸통 관통 투사체의 비행 속도 원문 수치를 확인해야 합니다.");delay=c.GroupSpacing/speed;}
            else delay=c.PenetrationGapSeconds??throw new CombatRuleUnavailableException("일반 투사체는 관통 후 감속하지만 고정 감속량은 위키에 없습니다. 다음 적까지의 관통 후 비행 시간을 지정하세요.");
        }
        return Enumerable.Range(1,count).Select(i=>new RangedPenetrationHit(firstTarget+i,i*delay)).ToArray();
    }
}
