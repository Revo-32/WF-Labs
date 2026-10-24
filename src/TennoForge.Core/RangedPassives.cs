using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedZoom(string Name,string Kind,double Value);
public record RangedPassiveRule(string WeaponId,string Kind,RangedZoom[] Zooms,int[] NoHeadshotBonus,int ComboMinimum,double ComboDuration,WikiSource? Source,double SprintReload,bool FalloffSpeedScaling=true,double HolsterReload=0);
public static class RangedPassives
{
    public static readonly RangedPassiveRule[] Rules=Load();
    private static RangedPassiveRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-passives.json")!;return JsonSerializer.Deserialize<RangedPassiveRule[]>(s,Catalog.Json)!;}
    public static RangedPassiveRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        if(weapon.Id=="wiki:primary/Evensong")
        {
            var evensong=System.Text.Json.Nodes.JsonNode.Parse(attack.GetRawText())!;evensong["ForcedProcs"]=new System.Text.Json.Nodes.JsonArray(Enumerable.Repeat("Puncture",7).Select(s=>(System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(s)).ToArray());return JsonSerializer.SerializeToElement(evensong);
        }
        if(RangedAlternates.Get(weapon.Id) is {Capacity:5,LockTargets:1} && index==2)
        {
            var grenade=System.Text.Json.Nodes.JsonNode.Parse(attack.GetRawText())!;grenade["ShotType"]="Projectile";
            return JsonSerializer.SerializeToElement(grenade);
        }
        if(Get(weapon.Id)?.Kind!="Grimoire"||index!=2)return attack;
        var node=System.Text.Json.Nodes.JsonNode.Parse(attack.GetRawText())!;node["Damage"]!["Electricity"]=280;node["ShotType"]="Hit-Scan";
        return JsonSerializer.SerializeToElement(node);
    }
    public static double ComboMultiplier(double hits,int minimum)=>minimum<=0||hits<minimum?1:1.5+.5*Math.Floor(Math.Log(hits/minimum,3)+1e-12);
    public static void Apply(RangedBuild build,ReferenceWeapon weapon,Dictionary<string,double> effects)
    {
        var c=build.Conditions??new();var r=Get(weapon.Id);if(r is null)return;
        void Add(string key,double value)=>effects[key]=effects.GetValueOrDefault(key)+value;
        Add("HolsterReload",r.HolsterReload);
        var radial=RangedEngine.S(RangedEngine.Attacks(weapon)[build.AttackIndex],"ShotType")=="AoE";
        if(c.Aiming&&r.Zooms.Length>0)
        {
            if(c.ZoomIndex<0||c.ZoomIndex>=r.Zooms.Length)throw new ArgumentException("조준 배율을 선택하세요.");
            var z=r.Zooms[c.ZoomIndex];Add(z.Kind,z.Value);
        }
        if(c.Aiming&&r.ComboMinimum>0)Add("SniperMultiplier",ComboMultiplier(c.SniperComboHits,r.ComboMinimum)-1);
        var n=c.WeaponBuffStacks;
        switch(r.Kind)
        {
            case "Alternox":if(c.SignatureOwner&&build.AttackIndex==0)Add("Multishot",.2);break;
            case "Scisco":n=Math.Max(0,Math.Min(5,n)-(int)Math.Floor(c.WeaponBuffAge/2));Add("FlatCriticalChance",.04*n);Add("FlatStatusChance",.04*n);break;
            case "Perigale":if(c.SignatureOwner)Add("BaseAmmoAdd",24);if(n>0&&c.WeaponBuffAge<4)Add("InfiniteMagazine",1);break;
            case "Magnus":if(n>0&&c.WeaponBuffAge<4)Add("InfiniteMagazine",1);break;
            case "Athodai":if(n>0&&c.WeaponBuffAge<8){if(build.AttackIndex!=0)throw new ArgumentException("오버드라이브가 활성화된 동안 아소다이 보조 공격을 사용할 수 없습니다.");Add("InfiniteMagazine",1);Add("FireRate",1);Add("Recoil",-1);}break;
            case "Grimoire":Add("InfiniteAmmo",1);break;
            case "MagazineDump":if(build.AttackIndex is 1 or 2)Add("FullMagazineDamage",1);break;
            case "Cedo":if(!radial)Add("InnateCO",.6);if(build.AttackIndex==1)Add("InfiniteMagazine",1);break;
            case "AX52":if(!c.Aiming)Add("AmmoEfficiency",.75);else if(c.Headshot)Add("CriticalChance",4);break;
            case "Velox":Add("AmmoEfficiency",.2);if(c.SignatureOwner)Add("AmmoEfficiency",.2);break;
            case "Knell":
                n=Math.Max(0,Math.Min(3,n)-(int)Math.Floor(c.WeaponBuffAge/2));
                if(n>0){Add("InfiniteMagazine",1);Add("FlatCriticalDamage",.5*n);Add("FlatStatusChance",.2*n);}break;
            case "Toxocyst":if(n>0&&c.WeaponBuffAge<3){Add("InfiniteMagazine",1);Add("PassiveToxin",1);Add("Toxin",1);Add("FinalFireRate",1.5);Add("Recoil",-.5);}break;
            case "Spirex":if(c.WeaponBuffAge<4)Add("ReloadSpeed",.25*Math.Min(2,n));break;
            // Wiki describes percentage reductions of reload TIME for the signature sprint passive.
            case "SprintReload":if(c.Sprinting)Add("ReloadTimeReduction",r.SprintReload*(c.SignatureOwner?2:1));break;
        }
    }
}
