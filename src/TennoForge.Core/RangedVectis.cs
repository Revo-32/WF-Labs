using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public static class RangedVectis
{
    public static bool Applies(ReferenceWeapon w)=>RangedEngine.S(w.Raw,"Family")=="Vectis";
    public static JsonElement Normalize(ReferenceWeapon w,JsonElement attack,int index)
    {
        if(!Applies(w)||index==0)return attack;
        var n=JsonNode.Parse(attack.GetRawText())!;
        n["Damage"]=new JsonObject{["Cold"]=index==3?25:150};
        if(index==2)n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=6.7,["Reduction"]=.7};
        // Notes call the embed radius approximate; do not promote 0.5m to an exact source value.
        if(index==3){n["Range"]=.5;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=.5,["Reduction"]=0};}
        return JsonSerializer.SerializeToElement(n);
    }
    public static double[] BodyTicks(RangedConditions c)
    {
        if(c.VectisBodyTickOne is not {} first||c.VectisBodyTickTwo is not {} second)throw new CombatRuleUnavailableException("벡티스 인카논의 몸통 부착은 즉시 1틱과 2.5초 안에 추가 2틱입니다. 추가 틱의 정확한 시점은 위키에 없으므로 교전 조건에서 지정하세요.");
        if(!double.IsFinite(first)||!double.IsFinite(second)||first<=0||second<=first||second>2.5)throw new ArgumentException("부착 후 두 틱은 0 < 첫 틱 < 둘째 틱 ≤ 2.5초여야 합니다.");
        return [first,second];
    }
}
