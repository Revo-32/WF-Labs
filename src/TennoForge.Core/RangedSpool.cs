using System.Text.Json;
namespace TennoForge.Core;

/// <summary>Held Kohm-family shots: native pellet count, status per pellet, and fractional ammunition.</summary>
public static class RangedSpool
{
    public static RangedTimingRule? Get(string id)=>RangedTimingCatalog.Data.Weapons.SingleOrDefault(r=>r.Id==id&&r.PelletSpoolMaximum>0);
    public static double Ammo(RangedTimingRule rule,int fired)=>Math.Max(1,Math.Min(rule.PelletSpoolMaximum,fired+1)/rule.PelletAmmoDivisor);
    public static JsonElement Attack(ReferenceWeapon weapon,JsonElement attack,int? fired)
    {
        if(fired is null||Get(weapon.Id) is not {} rule)return attack;
        if(fired<0)throw new ArgumentException("연속 발사 횟수 오류");
        var attacks=RangedEngine.Attacks(weapon);
        var pellets=Math.Min(rule.PelletSpoolMaximum,fired.Value+1);
        var values=attack.EnumerateObject().ToDictionary(p=>p.Name,p=>(object)p.Value);
        values["Multishot"]=pellets;
        values["StatusChance"]=RangedEngine.N(attacks[0],"StatusChance")!.Value/pellets;
        values["AmmoCost"]=Ammo(rule,fired.Value);
        values["FireRate"]=RangedEngine.N(attacks[1],"FireRate")!.Value*Math.Min(1,rule.SpoolStart!.Value+rule.SpoolStep!.Value*fired.Value);
        return JsonSerializer.SerializeToElement(values);
    }
}
