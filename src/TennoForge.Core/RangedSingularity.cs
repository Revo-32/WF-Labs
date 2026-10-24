using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record RangedSingularityPulse(double AfterSpawn,DamageType Type,double Output,double Radius);
public static class RangedSingularity
{
    public static bool Equipped(RangedBuild b)=>b.Slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey("SpontaneousChance"));
    public static JsonElement Attack(RangedBuild b,JsonElement attack)
    {
        if(!Equipped(b))return attack;
        if(b.AttackIndex is 1 or 3)throw new ArgumentException("스폰테이니어스 싱귤래리티는 합체·기존 특이점을 없앱니다. 구체 발사를 선택하세요.");
        var n=JsonNode.Parse(attack.GetRawText())!;n["Trigger"]="Semi-Auto";if(b.AttackIndex==0)n["CarrierProjectile"]=1;return JsonSerializer.SerializeToElement(n);
    }
    public static void Validate(RangedConditions c)
    {
        if(c.SingularityAllyPosition is {} position&&(!double.IsFinite(position)||Math.Abs(position)>10000)||!double.IsFinite(c.SingularityAllySpeed)||Math.Abs(c.SingularityAllySpeed)>1000||c.SingularityLimit is {} limit&&(limit<1||limit>1000))throw new ArgumentException("특이점 아군 경로·활성 한도 오류");
        if(c.SingularityDamageBracket is not ("Unspecified" or "Additive" or "Separate"))throw new ArgumentException("특이점 피해 증가 브라켓 시나리오 오류");
        if(c.SingularityPullRadius is {} radius&&(!double.IsFinite(radius)||radius<0||radius>100)||c.SingularityPulses is {} pulses&&(pulses.Length>1000||pulses.Any(p=>!double.IsFinite(p.AfterSpawn)||p.AfterSpawn<0||p.AfterSpawn>=5||!Enum.IsDefined(p.Type)||!double.IsFinite(p.Output)||p.Output<0||!double.IsFinite(p.Radius)||p.Radius<0||p.Radius>100)))throw new ArgumentException("특이점 피해 사건은 5초 이내, 피해·반경은 유효한 양수 또는 0이어야 합니다.");
    }
}
