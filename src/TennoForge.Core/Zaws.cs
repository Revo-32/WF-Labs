using System.Reflection;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TennoForge.Core;

public record ZawType(string Type,string Polarity,double Range,double Slide);
public record ZawStrike(string Id,ZawType One,ZawType Two,Dictionary<DamageType,double> Damage,double Speed,double CriticalChance,double CriticalMultiplier,double StatusChance,double Disposition,double TwoHandedFactor);
public record ZawGrip(string Id,bool TwoHanded,double Damage,double Speed);
public record ZawLink(string Id,double Speed,double CriticalChance,double StatusChance,double Damage);
public record ZawParts(string Version,WikiSource Source,ZawStrike[] Strikes,ZawGrip[] Grips,ZawLink[] Links);

public static class ZawCatalog
{
    private static readonly Lazy<ZawParts> Data=new(()=>
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.zaw-parts.json")!;
        return JsonSerializer.Deserialize<ZawParts>(s,Catalog.Json)!;
    });
    private static readonly ConcurrentDictionary<string,(Weapon Weapon,MeleeWeaponRule Rule,WeaponCapacityDefault Capacity)> Cache=new();
    public static ZawParts Parts=>Data.Value;
    public static bool IsZaw(string id)=>id.StartsWith("zaw:",StringComparison.Ordinal);
    public static string Id(string strike,string grip,string link)=>"zaw:"+string.Join('|',strike,grip,link);
    public static IEnumerable<Weapon> All()=>from s in Parts.Strikes from g in Parts.Grips from l in Parts.Links select Weapon(Id(s.Id,g.Id,l.Id));
    public static Weapon Weapon(string id)=>Get(id).Weapon;
    public static MeleeWeaponRule Rule(string id)=>Get(id).Rule;
    public static WeaponCapacityDefault Capacity(string id)=>Get(id).Capacity;
    private static (Weapon Weapon,MeleeWeaponRule Rule,WeaponCapacityDefault Capacity) Get(string id)=>Cache.GetOrAdd(id,key=>
    {
        var names=key[4..].Split('|');if(names.Length!=3)throw new ArgumentException("조우 부품 식별자 오류");
        var s=Parts.Strikes.Single(x=>x.Id==names[0]);var g=Parts.Grips.Single(x=>x.Id==names[1]);var l=Parts.Links.Single(x=>x.Id==names[2]);
        var type=g.TwoHanded?s.Two:s.One;var factor=g.TwoHanded?s.TwoHandedFactor:1;
        var total=(s.Damage.Values.Sum()+g.Damage+l.Damage)*factor;
        var plague=s.Id is "Plague Kripath" or "Plague Keewar";
        // Module:Modular distributes changes equally among all four plague damage types.
        var damage=s.Damage.Where(x=>x.Value>0).ToDictionary(p=>p.Key,p=>plague?p.Value+(total-s.Damage.Values.Sum())/4:p.Value/s.Damage.Values.Sum()*total);
        var speed=s.Speed+g.Speed+l.Speed;var cc=s.CriticalChance+l.CriticalChance;var sc=s.StatusChance+l.StatusChance;
        var localized=ReferenceCatalog.Load().Weapons.Single(x=>x.Id=="wiki:modular/"+s.Id).Name;
        var name=$"{localized} · {g.Id} · {l.Id}";
        var weapon=new Weapon(key,name,$"{s.Id} / {g.Id} / {l.Id}",damage,cc,s.CriticalMultiplier,sc,speed,type.Type,Source:Parts.Source.Url,InnateNotes:["조우 부품 합산 · 랭크 30 완성 수치"]);
        // Part-derived values stay separate from class rules. The calculation layer
        // resolves heavy attacks from the pinned class article, never another weapon.
        var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["Name"]=name,["Class"]=type.Type,["Disposition"]=s.Disposition,["Family"]=s.Id,["MeleeRange"]=type.Range,["ComboDur"]=5,["SlideAttack"]=total*type.Slide,["Attacks"]=new[]{new Dictionary<string,object>{["AttackName"]="Normal Attack",["Damage"]=damage,["CritChance"]=cc,["CritMultiplier"]=s.CriticalMultiplier,["StatusChance"]=sc,["FireRate"]=speed}}},Catalog.Json);
        var rule=new MeleeWeaponRule(key,name,Parts.Source,"조우 부품 조합",raw,[],"Zaw",[new("Normal Attack"),new("Slide Attack")]);
        var polarity=type.Polarity switch{"Madurai"=>"AP_ATTACK","Naramon"=>"AP_TACTIC","Zenurik"=>"AP_POWER",_=>"None"};
        return(weapon,rule,new(Enumerable.Repeat("None",8).ToArray(),polarity));
    });
}
