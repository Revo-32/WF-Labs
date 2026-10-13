using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record MeleeClassRule(string Name,double FirstHeavyMultiplier,double SecondHeavyMultiplier,double Windup,string FirstHeavyText,string SecondHeavyText);
public record MeleeClassSource(WikiSource Source,MeleeClassRule[] Classes);

public static class MeleeClassRules
{
    private static readonly Lazy<MeleeClassSource> Data=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.melee-classes.json")!;
        return JsonSerializer.Deserialize<MeleeClassSource>(stream,Catalog.Json)!;
    });
    public static MeleeClassSource Source=>Data.Value;
    public static MeleeClassRule? Find(string name)=>Source.Classes.SingleOrDefault(c=>c.Name==name);
    public static double HeavyDamage(MeleeWeaponRule weapon)
    {
        var raw=weapon.Raw;
        if(ExaltedRules.Find(weapon.Id) is {Pseudo:true} or {Family:ExaltedFamily.Diwata})return 0;
        if(raw.Number("HeavyAttack")>0)return raw.Number("HeavyAttack");
        // A glaive's heavy input detonates an in-flight projectile. Its melee
        // class table cannot stand in for the weapon's recall explosion.
        if(raw.Text("Class")=="Glaive")return 0;
        var rule=Find(raw.Text("Class"));
        if(rule is null)return 0;
        var normal=raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Normal Attack");
        return normal.GetProperty("Damage").EnumerateObject().Sum(p=>p.Value.GetDouble())*rule.FirstHeavyMultiplier;
    }
    public static AttackRule? HeavyPolicy(MeleeWeaponRule weapon)=>HeavyDamage(weapon)>0
        ?new("Heavy Attack",Heavy:true,Combo:true,Notes:["무기별 강공격 항목이 없어 위키 무기군의 강공격 배율 적용 · "+Source.Source.Url]):null;
}
