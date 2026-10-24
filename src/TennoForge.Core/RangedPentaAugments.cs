using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public static class RangedPentaAugments
{
    public static bool Applies(ReferenceWeapon w)=>w.Id is "wiki:primary/Penta" or "wiki:primary/Secura Penta" or "wiki:primary/Carmine Penta";
    public static bool Has(RangedBuild b,string effect)=>b.Slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey(effect));
    public static JsonElement Attack(ReferenceWeapon w,RangedBuild b,JsonElement attack)
    {
        if(!Applies(w)||!Has(b,"TetherTargets")||b.AttackIndex!=1||w.Id!="wiki:primary/Secura Penta")return attack;
        var node=JsonNode.Parse(attack.GetRawText())!;node["Falloff"]!["Reduction"]=.5;return JsonSerializer.SerializeToElement(node);
    }
    public static double Delay(RangedBuild b)
    {
        if(Has(b,"PentaNapalm"))return 0;
        if(!Has(b,"TetherTargets"))return b.Conditions?.DetonationDelay??0;
        var c=b.Conditions??new();var lifetime=Math.Min(10,c.TetherRagdollRecovery??10);if(c.ManualGrenades)return lifetime;if(c.TetherAirburst)return 0;
        return Math.Min(lifetime,c.DetonationDelay);
    }
    public static RangedResult Calculate(RangedBuild b,ReferenceWeapon w,RangedResult r)
    {
        if(!Applies(w)||r.Modifiers!.GetValueOrDefault("TetherTargets")<=0)return r;
        var c=b.Conditions??new();if(c.ManualGrenades&&c.TetherAirburst)throw new CombatRuleUnavailableException("복수 유탄의 이동 중 기폭 위치는 아직 연결되지 않았습니다. 지형 부착 배치 또는 단발 공중 기폭을 선택하세요.");if(c.TetherRagdollRecovery is {} recovery&&(!double.IsFinite(recovery)||recovery<0||recovery>300)||c.PullSeconds is {} pull&&(!double.IsFinite(pull)||pull<=0||pull>300))throw new ArgumentException("테더 유탄 부착·이동 시간 오류");var scope=r.Scope.Concat(new[]{"테더 그레네이즈: 몸통을 무한 관통하며 직격 피해는 없습니다. 지형에 붙은 뒤 최대 10초에 자동 기폭하고 더 이른 수동 기폭을 허용합니다. 공중 기폭은 ×1.2이며 세큐라 폭발 감쇠는 50%입니다. 투척→기폭 순환 또는 활성 유탄을 모아 수동 기폭하는 패턴을 선택합니다. 원문에 없는 감속 비행 시간과 이동 경로는 입력한 시나리오입니다."}).ToArray();
        if(b.AttackIndex==0)
        {
            r.Modifiers!["CarrierProjectile"]=1;var travel=b.Distance==0?0:c.TetherFlightSeconds??throw new CombatRuleUnavailableException("테더 그레네이즈는 비행 속도를 감소시키지만 수치는 원문에 없습니다. 비행 시간을 지정하세요.");
            if(!double.IsFinite(travel)||travel<0||travel>300)throw new ArgumentException("테더 유탄 비행 시간 오류");
            return r with{Damage=[],ExpectedShot=0,BurstDps=null,SustainedDps=null,Cadence=r.Cadence! with{ShotDelay=c.ManualGrenades?r.Cadence!.ShotDelay:Math.Max(r.Cadence!.ShotDelay,travel+Delay(b))},Scope=scope};
        }
        var boost=c.TetherAirburst?1.2:1;
        return r with{Damage=r.Damage.ToDictionary(p=>p.Key,p=>p.Value*boost),ExpectedShot=r.ExpectedShot*boost,StatusBaseFactor=r.StatusBaseFactor*boost,BurstDps=null,SustainedDps=null,Scope=scope};
    }
}
