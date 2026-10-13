using System.Text.Json;
using System.Text.Json.Nodes;

namespace TennoForge.Core;

public record VitricaExplosion(Calculation GlassedTarget,Calculation NearbyTarget,double Radius,bool Heavy,string[] Notes);

/// <summary>Vitrica rev.2789905: source target and nearby targets are different components.</summary>
public static class VitricaMechanic
{
    public const string Source="https://wiki.warframe.com/w/Vitrica?oldid=2789905";
    internal static JsonElement OriginRaw(JsonElement raw)
    {
        var node=JsonNode.Parse(raw.GetRawText())!;
        node["StatusChance"]=1d;node["Range"]=0d;node.AsObject().Remove("Falloff");
        return JsonSerializer.SerializeToElement(node);
    }
    public static VitricaExplosion Calculate(Catalog catalog,Build build,bool heavy,double nearbyDistance=0,bool useInitialComboFloor=true)
    {
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);
        if(profile.Name!="Vitrica"||build.Melee is null)throw new ArgumentException("비트리카 빌드가 필요합니다.");
        MeleeEngine.Range(nearbyDistance,0,1000,"유리 폭발 중심에서 주변 대상까지 거리");
        var raw=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Glass Explosion");
        Calculation Part(bool origin)
        {
            var attack=origin?"Vitrica Glass Origin":"Vitrica Glass Radial";
            var policy=new AttackRule(attack,Co:0,Heavy:heavy,Combo:heavy,Tennokai:false,EvolutionDamage:false,EvolutionStats:false,
                StatusMods:!origin,Elements:origin,Ips:origin,Code:origin?"VitricaGlass":"VitricaGlassRadial");
            var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=attack,Tennokai=false,Distance=origin?0:nearbyDistance,TargetIndex=1}};
            return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(origin?OriginRaw(raw):raw,policy));
        }
        return new(Part(true),Part(false),6,heavy,[
            "에임 글라이드 중 첫 공중 공격의 투사체는 피해 없이 대상을 7초간 유리화합니다.",
            "유리화 대상에 슬램·강 슬램·스탠스 슬램이 닿으면 폭발합니다. 원래 대상과 주변 대상의 피해는 합산하지 않습니다.",
            "원래 대상: 기본 상태 확률 100%, 일반 상태 확률 모드 제외·고정 추가 확률 허용, 물리/원소 모드 적용.",
            "주변 대상: 기본 상태 확률 33%에 모드 적용, 물리/원소 피해 모드 제외, 반경 6m·선형 90% 감쇠·사거리 모드 제외.",
            "강 슬램 파동으로 깨뜨리면 강공격 치확 보너스와 콤보 배수가 적용됩니다. 출처에 없는 비행/착지 시각은 생성하지 않습니다.",Source]);
    }
}
