using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record KitgunAssembly(string Grip,string Loader,string Arcane="None",int ArcaneRank=3);
public record KitgunDefinition(string WeaponId,string Chamber,string Category,ReferenceWeapon Template,JsonElement Stats,Dictionary<string,JsonElement> Grips,Dictionary<string,JsonElement> Loaders,WikiSource Source);
public record KitgunPartCatalog(string Version,WikiSource[] Sources,KitgunDefinition[] Weapons);
public static class RangedKitguns
{
    public static readonly KitgunPartCatalog Data=Load();
    private static KitgunPartCatalog Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.kitgun-parts.json")!;return JsonSerializer.Deserialize<KitgunPartCatalog>(stream,Catalog.Json)!;}
    public static KitgunDefinition? Get(string id)=>Data.Weapons.SingleOrDefault(w=>w.WeaponId==id);
    public static KitgunAssembly Default(KitgunDefinition d)=>new(d.Category=="primary"?"Brash":"Gibber","Splat");
    public static ReferenceWeapon Assemble(KitgunDefinition d,KitgunAssembly? parts=null)
    {
        parts??=Default(d);
        if(!d.Grips.ContainsKey(parts.Grip)||!d.Loaders.TryGetValue(parts.Loader,out var loader))throw new ArgumentException("키트건의 무기 구분·그립·로더 조합이 다릅니다.");
        double Number(JsonElement v,string key)=>v.GetProperty(key).GetDouble();
        if(parts.ArcaneRank is <0 or >3||parts.Arcane is not ("None" or "PaxCharge" or "PaxSoar" or "PaxBolt" or "PaxSeeker")&&!RangedResidual.Is(parts.Arcane))throw new ArgumentException("키트건 아케인 종류·랭크를 확인하세요.");
        var stats=d.Stats;var raw=JsonNode.Parse(d.Template.Raw.GetRawText())!.AsObject();
        var damage=stats.GetProperty("Damage").GetProperty(parts.Grip).EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetDouble());
        var rate=stats.GetProperty("FireRate").GetProperty(parts.Grip).GetDouble();
        raw["Magazine"]=stats.GetProperty("Magazine").GetProperty(loader.GetProperty("Magazine").GetString()!).GetDouble();raw["Reload"]=Number(loader,"Reload");
        foreach(var key in new[]{"AmmoMax","AmmoPickup","Disposition","Trigger","CompatibilityTags"})if(stats.TryGetProperty(key,out var value))raw[key]=JsonNode.Parse(value.GetRawText());
        raw["Family"]=d.Chamber;raw["KitgunGrip"]=parts.Grip;raw["KitgunLoader"]=parts.Loader;
        if(parts.Arcane=="PaxCharge")
        {
            raw["ReloadStyle"]="Regenerate";raw["AmmoType"]="None";raw["ReloadDelay"]=Number(loader,"Reload");
            raw["ReloadRate"]=(d.Chamber,d.Category) switch{("Catchmoon","primary") or ("Sporelacer","primary")=>5,("Sporelacer","secondary") or ("Vermisplicer","secondary")=>25,_=>50};
        }
        var attacks=raw["Attacks"]!.AsArray();
        for(int i=0;i<attacks.Count;i++)
        {
            var a=attacks[i]!;
            foreach(var key in new[]{"CritChance","CritMultiplier","StatusChance"})a[key]=Number(stats,key)+Number(loader,key);
            a["FireRate"]=rate;a["AttackIndex"]=i+1;
            a["Multishot"]=i==0?Number(stats,"Multishot"):1;
            if(i==0)
            {
                a["Trigger"]=stats.GetProperty("Trigger").GetString();
                foreach(var key in new[]{"AmmoCost","Range","ChargeTime","Falloff","ForcedProcs","PunchThrough"})
                    if(stats.TryGetProperty(key,out var value)){if(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(parts.Grip,out var byGrip))value=byGrip;a[key]=JsonNode.Parse(value.GetRawText());}
            }
            Dictionary<string,double> packet=[];
            switch(d.Chamber)
            {
                case "Catchmoon":case "Rattleguts":packet=new(damage);break;
                case "Gaze":packet=new(damage);if(i==1){a["ShotType"]="AoE";a["Range"]=3;}break;
                case "Vermisplicer":packet=damage.ToDictionary(p=>p.Key,p=>p.Value*(i==1?.7:1));break;
                case "Sporelacer":
                    if(i==0){packet["Impact"]=damage.GetValueOrDefault("Impact");if(packet["Impact"]==0)a["CarrierProjectile"]=1;}
                    else if(i==1)packet["Toxin"]=damage.GetValueOrDefault("Toxin");
                    else a["UnsupportedReason"]="스포어레이서 자탄의 그립별 피해 분할은 모듈에 0으로 비어 있어 정확한 값을 추가 확인해야 합니다.";
                    break;
                case "Tombfinger":
                    if(d.Category=="primary")
                    {
                        var quick=new Dictionary<string,double>{{"Brash",30},{"Palmaris",35},{"Shrewd",40},{"Steadyslam",79},{"Tremor",108}}[parts.Grip];
                        if(i==0){packet=new(damage);a["Trigger"]="Semi-Auto";a.AsObject().Remove("ChargeTime");}
                        else packet["Radiation"]=quick+(i==2?382:0);
                    }
                    else a["UnsupportedReason"]="톰핑거 보조무기의 방사능 직접/폭발 분할은 위키의 약 19.5% 서술과 91/89 예시가 불일치합니다. 정확한 그립별 분할 확인이 필요합니다.";
                    break;
            }
            if(packet.Values.Sum()<=0&&a["UnsupportedReason"] is null&&a["CarrierProjectile"] is null)a["UnsupportedReason"]="이 조합의 직접 피해는 0이며 투사체 운반 사건과 후속 폭발만으로 계산하는 경로를 연결해야 합니다.";
            a["Damage"]=JsonSerializer.SerializeToNode(packet);
        }
        if(d.Chamber=="Tombfinger"&&d.Category=="primary")
        {
            var charged=attacks[0]!.DeepClone();charged["AttackName"]="Charged Projectile";charged["AttackIndex"]=4;charged["Trigger"]="Charge";charged["ChargeTime"]=stats.GetProperty("ChargeTime").GetProperty(parts.Grip).GetDouble();
            attacks.Add(charged);attacks[2]!["AttackParentIndex"]=4;
        }
        RangedPaxSeeker.Append(d,parts,attacks);
        var template=d.Template;
        return template with{Category=d.Category,WeaponType=RangedEngine.S(d.Template.Raw,"Class"),Calculable=true,Limitations=[],Source=d.Source.Url,Raw=JsonSerializer.SerializeToElement(raw)};
    }
    public static void Apply(RangedBuild b,Dictionary<string,double> effects)
    {
        if(b.Kitgun is not {} parts)return;
        if(Get(b.WeaponId) is null)throw new ArgumentException("일반 무기에는 키트건 부품을 장착할 수 없습니다.");
        void Add(string key,double value)=>effects[key]=effects.GetValueOrDefault(key)+value;
        if(parts.Arcane=="PaxCharge")Add("ReloadSpeed",.125*(parts.ArcaneRank+1));
        if(parts.Arcane=="PaxSoar")
        {
            if(b.Conditions?.Airborne==true||b.Conditions?.AimGlide==true){Add("Accuracy",.125*(parts.ArcaneRank+1));Add("Recoil",-.125*(parts.ArcaneRank+1));}
            Add("AimGlideSeconds",1.25*(parts.ArcaneRank+1));Add("WallLatchSeconds",1.25*(parts.ArcaneRank+1));
        }
    }
    public static RangedTimingRule Timing(string id)=>new(id,Get(id)!.Source,null,null,null,null,.2,false,false,null,[]);
}
