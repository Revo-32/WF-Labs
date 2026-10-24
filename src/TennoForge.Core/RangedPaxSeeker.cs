using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record RangedSeekerPath(int Target,double? Flight,bool Miss=false);
public static class RangedPaxSeeker
{
    public static bool Is(JsonElement attack)=>RangedEngine.N(attack,"PaxSeekerProjectile")==1;
    public static void Append(KitgunDefinition d,KitgunAssembly parts,JsonArray attacks)
    {
        if(parts.Arcane!="PaxSeeker")return;
        var a=attacks[0]!.DeepClone();a["AttackIndex"]=attacks.Count+1;a["AttackName"]="Pax Seeker · homing projectile";a["PaxSeekerProjectile"]=1;a["Damage"]=new JsonObject{["Slash"]=.5,["Puncture"]=.5};a["ForcedProcs"]=new JsonArray("Impact");a["ShotType"]="Projectile";a["Trigger"]="Semi-Auto";a["Multishot"]=1;a["AmmoCost"]=1;
        foreach(var key in new[]{"Range","Falloff","ChargeTime","PunchThrough","ShotSpeed","UnsupportedReason","CarrierProjectile"})a.AsObject().Remove(key);
        attacks.Add(a);
    }
    public static double Basis(KitgunDefinition d,KitgunAssembly parts,double damageBonus,double multishot,bool approximate=false)
    {
        if(!approximate&&d.Chamber is not ("Gaze" or "Rattleguts" or "Tombfinger" or "Catchmoon"))throw new CombatRuleUnavailableException("이 챔버의 팍스 시커 피해식은 위키에 없습니다. 확인된 4개 챔버의 식을 대신 적용하지 않습니다.");
        var damage=d.Stats.GetProperty("Damage");double Sum(JsonElement packet)=>packet.EnumerateObject().Sum(p=>p.Value.GetDouble());
        var barrel=Sum(damage.GetProperty("Base"));var grip=Sum(damage.GetProperty(parts.Grip))-barrel;
        var basis=d.Chamber is "Gaze" or "Rattleguts" or "Sporelacer" or "Vermisplicer"?barrel+grip:barrel;
        return Math.Max(0,(d.Chamber=="Catchmoon"?1:2)*basis*Math.Max(0,1+damageBonus)*multishot+grip);
    }
    public static void Validate(RangedConditions c)
    {
        if(c.PaxSeekerRoll is {} roll&&(roll<0||roll>10000)||c.PaxSeekerPaths is {} paths&&(paths.Length>4||paths.Any(p=>p.Target<0||p.Target>=c.GroupCount||p.Flight is {} f&&(!double.IsFinite(f)||f<0||f>300))))throw new ArgumentException("팍스 시커 탄환 수·경로·비행 시간 오류");
    }
}
