using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public static class RangedBurstAugments
{
    public static bool IsFrenzy(ReferenceWeapon weapon,int attack)=>attack==1&&RangedEngine.S(weapon.Raw,"Family")=="Grakata";
    public static JsonElement[] Append(ReferenceWeapon weapon,JsonElement[] attacks)
    {
        if(RangedEngine.S(weapon.Raw,"Family")!="Grakata")return attacks;
        var attack=JsonNode.Parse(attacks[0].GetRawText())!;attack["AttackName"]="Wild Frenzy";attack["AttackIndex"]=2;attack["Trigger"]="Mag Burst";attack["FireRate"]=80;attack["BurstDelay"]=1d/80;
        return [..attacks,JsonSerializer.SerializeToElement(attack)];
    }
    public static double Refund(double spent,double fraction,string rounding)
    {
        var value=spent*fraction;
        if(Math.Abs(value-Math.Round(value))<1e-9)return Math.Round(value);
        return rounding switch{"Down"=>Math.Floor(value),"Up"=>Math.Ceiling(value),_=>throw new CombatRuleUnavailableException("와일드 프렌지의 소수 탄약 환급은 위키에 반올림 규칙이 없습니다. 버림/올림 시나리오를 선택하세요.")};
    }
}
