using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public static class RangedKomorex
{
    public const string Id="wiki:primary/Komorex";
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)
    {
        if(w.Id!=Id)return attacks;
        var radial=JsonNode.Parse(attacks[1].GetRawText())!;radial["ShotType"]="AoE";radial["Trigger"]="Semi-Auto";
        var zoom=JsonNode.Parse(attacks[0].GetRawText())!;zoom["AttackIndex"]=3;zoom["AttackName"]="3.5배 조준 사격";
        return [attacks[0],JsonSerializer.SerializeToElement(radial),JsonSerializer.SerializeToElement(zoom)];
    }
    public static void Apply(RangedBuild b,Dictionary<string,double> effects)
    {
        if(b.WeaponId!=Id)return;
        void Add(string key,double value)=>effects[key]=effects.GetValueOrDefault(key)+value;
        if(b.AttackIndex==0&&b.Conditions?.Aiming!=false)Add("PunchThrough",2);
        if(b.AttackIndex==2){Add("Impact",1);Add("Puncture",1);Add("Slash",1);Add("FireRate",-.75);}
        effects["AmmoMutation"]=1;
        if(b.AttackIndex==1)effects["BlastRadius"]=0;
    }
}
