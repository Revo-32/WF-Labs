using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedAttackLink(string WeaponId,int Parent,int Child,double Delay,double MinimumDistance,WikiSource Source,bool ManualDetonation=false,double TickInterval=0,int TickCount=1,bool SingleTickOnPierce=false,double? MaximumDelay=null);
public static class RangedAttackLinks
{
    public static readonly RangedAttackLink[] Rules=Load();
    private static RangedAttackLink[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-links.json")!;return JsonSerializer.Deserialize<RangedAttackLink[]>(s,Catalog.Json)!;}
    public static RangedAttackLink[] Children(string id,int parent)=>id==RangedVileDischarge.WeaponId&&parent==1?[new(id,1,2,0,0,RangedVileDischarge.Source)]:Rules.Where(r=>r.WeaponId==id&&r.Parent==parent).ToArray();
    public static double Delay(RangedAttackLink rule,RangedConditions? conditions=null,RangedBuild? build=null)
    {
        if(build is not null&&rule.Parent==0&&RangedEngine.S(RangedEngine.Resolve(build).Raw,"Family")=="Sporothrix"&&build.Slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.GetValueOrDefault("VolatileVariant")>0))return 0;
        if(rule.MaximumDelay is not {} maximum)return rule.Delay;
        var delay=conditions?.BubbleDelay??rule.Delay;
        if(!double.IsFinite(delay)||delay<rule.Delay||delay>maximum)throw new ArgumentException($"물방울 폭발 지연은 {rule.Delay}~{maximum}초 범위입니다.");
        return delay;
    }
    public static double LastDelay(string id,int parent,double detonationDelay=0,RangedConditions? conditions=null,RangedBuild? build=null)=>Children(id,parent).Select(c=>Delay(c,conditions,build)+(c.TickCount-1)*c.TickInterval+(c.ManualDetonation?(build is not null&&(RangedPentaAugments.Has(build,"TetherTargets")||RangedPentaAugments.Has(build,"PentaNapalm"))?RangedPentaAugments.Delay(build):detonationDelay):0)+LastDelay(id,c.Child,detonationDelay,conditions,build)).DefaultIfEmpty(0).Max();
    public static double FlightTime(ReferenceWeapon weapon,int attack,RangedBuild build,RangedResult result)
    {
        if(RangedPentaAugments.Applies(weapon)&&attack==0&&result.Modifiers!.GetValueOrDefault("TetherTargets")>0){if(build.Distance==0)return 0;var travel=build.Conditions?.TetherFlightSeconds??throw new CombatRuleUnavailableException("테더 유탄 감속량은 원문에 없습니다. 비행 시간을 지정하세요.");if(!double.IsFinite(travel)||travel<0||travel>300)throw new ArgumentException("테더 유탄 비행 시간 오류");return travel;}
        if(weapon.Id==RangedPullWeapons.Scyotid&&attack==1){if(build.Distance==0)return 0;var value=build.Conditions?.SnareFlightSeconds??throw new CombatRuleUnavailableException("사이오티드 올가미의 비행 속도는 원문에 없습니다. 도착 시간을 지정하세요.");if(!double.IsFinite(value)||value<0||value>300)throw new ArgumentException("올가미 비행 시간 오류");return value;}
        if(RangedVileDischarge.Applies(weapon)&&attack==1){if(build.Distance==0)return 0;var flight=build.Conditions?.DischargeFlightSeconds??throw new CombatRuleUnavailableException("바일 디스차지 주머니의 투사체 속도가 원문에 없습니다. 목표까지 비행 시간을 지정하세요.");if(!double.IsFinite(flight)||flight<0||flight>300)throw new ArgumentException("바일 디스차지 비행 시간 오류");return flight;}
        if((RangedSimulor.Applies(weapon)||RangedOrdinarySimulor.Applies(weapon))&&attack==0)return RangedSimulor.Arrival(build);
        if(RangedAirbursts.Get(weapon.Id,attack) is {} airburst)return RangedAirbursts.Fuse(airburst,build.Conditions??new());
        if(RangedReloadGrenades.Get(weapon.Id) is not null&&attack is 1 or 3)
        {
            if(build.Distance==0)return 0;
            if(build.Conditions?.GrenadeFlightSeconds is not {} seconds)throw new CombatRuleUnavailableException("카타볼리스트 유탄의 비행 속도는 원문에 없습니다. 목표까지 비행 시간을 설정하세요.");
            if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentException("유탄 비행 시간은 0 이상의 유한한 값이어야 합니다.");
            return seconds;
        }
        var a=RangedEngine.Attacks(weapon)[attack];
        if(RangedEngine.S(a,"ShotType")!="Projectile"&&RangedDeployments.Get(weapon.Id) is null||build.Distance==0)return 0;
        var speed=RangedEngine.N(a,"ShotSpeed");
        if(speed is null or <=0){var supplied=build.Conditions?.ProjectileFlightSeconds??throw new CombatRuleUnavailableException("이 투사체의 비행 속도는 원문에 없습니다. 목표까지의 비행 시간 시나리오를 지정하세요.");if(!double.IsFinite(supplied)||supplied<0||supplied>300)throw new ArgumentException("투사체 비행 시간 오류");return supplied;}
        var multiplier=1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed");
        if(multiplier<=0)throw new ArgumentException("투사체 속도 합계가 0 이하입니다.");
        return build.Distance/(speed.Value*multiplier);
    }
}
