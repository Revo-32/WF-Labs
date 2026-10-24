using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record RangedFieldRule(string WeaponId,int[] Parents,int Child,double SpawnDelay,double Duration,double Interval,int Explosion,bool Single,bool FireRateScales,bool UnmoddedCriticalStatus,WikiSource Source,bool RepeatAfterReload=false,bool BlockedByDefenses=false,bool RequireDuration=false);
public static class RangedFields
{
    public static readonly RangedFieldRule[] Rules=Load();
    private static RangedFieldRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-fields.json")!;return JsonSerializer.Deserialize<RangedFieldRule[]>(s,Catalog.Json)!;}
    public static RangedFieldRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        var r=Get(weapon.Id);if(r is null)return attack;
        var node=JsonNode.Parse(attack.GetRawText())!;
        if(index==r.Child){node["ShotType"]="AoE";node["Trigger"]="Semi-Auto";if(r.BlockedByDefenses){node["CritChance"]=0;node["StatusChance"]=.5;}}
        if(r.Explosion>=0&&r.Parents.Contains(index))node["Trigger"]="Semi-Auto";
        return JsonSerializer.SerializeToElement(node);
    }
    public static double End(RangedFieldRule rule,RangedConditions c)=>Math.Min(rule.Duration,c.FieldEndSeconds??(rule.RequireDuration?throw new CombatRuleUnavailableException("팍스 구름 지속 시간은 본문 4초와 모듈 5초가 다릅니다. 장판 설정에서 계산 기준을 선택하세요."):rule.Duration));
    public static double[] Pulses(RangedFieldRule rule,RangedConditions c,double fireBonus)
    {
        var interval=rule.Interval/(rule.FireRateScales?Math.Max(.05,1+fireBonus):1);
        if(!double.IsFinite(c.FieldFirstPulse)||c.FieldFirstPulse<0||c.FieldFirstPulse>1||c.FieldEndSeconds is {} end&&(!double.IsFinite(end)||end<=0||end>rule.Duration))throw new ArgumentException("장판 첫 틱 위치(0–1) 또는 유지 시간이 범위를 벗어납니다.");
        var first=interval*c.FieldFirstPulse;var duration=End(rule,c);var times=new List<double>();
        for(double t=first;t<duration-1e-9||c.FieldFirstPulse==1&&t<=duration+1e-9;t+=interval)times.Add(rule.SpawnDelay+t);
        return times.ToArray();
    }
}
