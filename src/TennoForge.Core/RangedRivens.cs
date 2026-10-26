using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedRivenAttribute(string Key,string Name,Dictionary<string,double> Bases,bool PositiveOnly,bool Spliced,string Unit);
public record RangedRivenRecipe(string First,string Second,string Result);
public record RangedRivenTable(string Version,WikiSource Source,RangedRivenAttribute[] Attributes,RangedRivenRecipe[] Recipes);
public record RangedRivenTrait(string Stat,double Value,bool Negative=false,bool Spliced=false);
public record RangedRivenRoll(string WeaponId,int Rank,RangedRivenTrait[] Traits,string Version,int? BaseDrain=null,string? Polarity=null);
public record RangedSplicePreview(int First,int Second,string Result,EncounterRange Value,EncounterRange InheritedRoll,bool ReplacementNegative);

public static class RangedRivens
{
    public static readonly RangedRivenTable Table=Load();
    private static RangedRivenTable Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-rivens.json")!;
        return JsonSerializer.Deserialize<RangedRivenTable>(stream,Catalog.Json)! with{Version=RivenBalance.Version};
    }
    public static bool IsRiven(RangedMod mod)=>mod.EnglishName is "Rifle Riven Mod" or "Shotgun Riven Mod" or "Pistol Riven Mod";
    public static RangedMod Mod(ReferenceWeapon weapon)=>RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==RangedEngine.ModCategory(weapon)+" Riven Mod");
    public static RangedRivenAttribute Attribute(string key)=>Table.Attributes.SingleOrDefault(a=>a.Key==key)??throw new ArgumentException("지원하지 않는 총기 리벤 옵션입니다.");
    public static RivenRange Range(RangedRivenRoll roll,RangedRivenTrait trait)
    {
        var weapon=RangedEngine.Weapons.SingleOrDefault(w=>w.Id==roll.WeaponId)??throw new ArgumentException("총기 리벤의 무기를 선택하세요.");
        var disposition=RivenBalance.Multiplier;
        var positives=roll.Traits.Count(t=>!t.Negative);var negatives=roll.Traits.Count(t=>t.Negative);
        var weight=(positives,negatives,trait.Negative) switch{(2,0,false)=>.99,(2,1,false)=>1.2375,(2,1,true)=>.495,(3,0,false)=>.75,(3,1,false)=>.9375,(3,1,true)=>.75,_=>throw new ArgumentException("리벤은 양옵 2~3개와 음옵 최대 1개입니다.")};
        var basis=Attribute(trait.Stat).Bases[RangedEngine.ModCategory(weapon)];
        var center=basis*disposition*weight*(roll.Rank+1)/9;
        return new(center*.9,center*1.1,disposition,basis,weight);
    }
    public static void Validate(RangedRivenRoll roll,bool values=true,bool requireMetadata=true)
    {
        if(roll is not null&&roll.Version!=Table.Version)throw new ArgumentException(RivenBalance.LegacyMessage);
        if(roll is null||roll.Traits is null||roll.Traits.Any(t=>t is null)||roll.Version!=Table.Version||roll.Rank is <0 or >8)throw new ArgumentException("총기 리벤 자료 버전·랭크·옵션을 확인하세요.");
        if((requireMetadata||roll.BaseDrain is not null)&&roll.BaseDrain is not >=0 or >30||(requireMetadata||roll.Polarity is not null)&&roll.Polarity is not ("Madurai" or "Vazarin" or "Naramon"))throw new ArgumentException("리벤의 0랭크 코스트와 극성을 입력하세요.");
        if(roll.Traits.Count(t=>!t.Negative) is <2 or >3||roll.Traits.Count(t=>t.Negative)>1||roll.Traits.Count(t=>t.Spliced)>1||roll.Traits.Select(t=>t.Stat).Distinct().Count()!=roll.Traits.Length)throw new ArgumentException("리벤은 중복 없이 양옵 2~3개, 음옵 최대 1개, 접합 최대 1개입니다.");
        foreach(var trait in roll.Traits)
        {
            var attribute=Attribute(trait.Stat);
            if(!double.IsFinite(trait.Value)||trait.Value<0||trait.Negative&&attribute.PositiveOnly||trait.Spliced!=attribute.Spliced)throw new ArgumentException(attribute.Name+": 옵션 부호·접합 여부·표시값을 확인하세요.");
            var range=Range(roll,trait);if(values)RivenEngine.Grade(trait.Value,range.Minimum,range.Maximum);
        }
    }
    public static RangedMod Resolve(RangedMod mod,RangedRivenRoll? roll,string weaponId,int slotRank)
    {
        if(roll is null)throw new ArgumentException("장착할 총기 리벤의 옵션을 입력하세요.");
        Validate(roll);
        if(roll.WeaponId!=weaponId||roll.Rank!=slotRank)throw new ArgumentException("리벤 무기·표시 수치의 랭크가 장착 슬롯과 다릅니다.");
        // In-game elemental precedence inside a Riven is bottom to top.
        var effects=roll.Traits.Reverse().ToDictionary(t=>t.Stat,t=>t.Value*(t.Negative?-1:1)*(t.Stat=="Recoil"?-1:1)/(t.Stat=="PunchThrough"?1:100)*9/(roll.Rank+1));
        return mod with{MaxRank=8,BaseDrain=roll.BaseDrain!.Value,Polarity=roll.Polarity!,Effects=effects,Limitations=[],Description=string.Join("\n",roll.Traits.Select(t=>$"{Attribute(t.Stat).Name} {(t.Negative?"−":"+")}{t.Value:0.###}{Attribute(t.Stat).Unit}"))};
    }
    public static RangedSplicePreview[] Predict(RangedRivenRoll roll,IReadOnlyList<int>? displayedDecimals=null)
    {
        Validate(roll,values:displayedDecimals is null,requireMetadata:false);
        if(displayedDecimals is not null)
        {
            if(displayedDecimals.Count!=roll.Traits.Length)throw new ArgumentException("표시 정밀도 개수가 옵션과 다릅니다.");
            for(int i=0;i<roll.Traits.Length;i++){var r=Range(roll,roll.Traits[i]);RivenEngine.Grade(roll.Traits[i].Value,r.Minimum,r.Maximum,displayedDecimals[i]);}
        }
        if(roll.Traits.Any(t=>t.Spliced))return [];
        var previews=new List<RangedSplicePreview>();
        for(int i=0;i<roll.Traits.Length;i++)for(int j=i+1;j<roll.Traits.Length;j++)foreach(var recipe in Table.Recipes)
        {
            var first=roll.Traits[i];var second=roll.Traits[j];
            if(!((first.Stat==recipe.First&&second.Stat==recipe.Second)||(first.Stat==recipe.Second&&second.Stat==recipe.First)))continue;
            EncounterRange Hidden(RangedRivenTrait trait,int index)
            {var center=Range(roll,trait).Minimum/.9;var half=.5*Math.Pow(10,-(displayedDecimals?[index]??1));return new(Math.Max(.9,(trait.Value-half)/center),Math.Min(1.1,(trait.Value+half)/center));}
            var a=Hidden(first,i);var b=Hidden(second,j);var inherited=new EncounterRange(Math.Max(a.Low,b.Low),Math.Max(a.High,b.High));
            var basis=Range(roll,new(recipe.Result,0,Spliced:true)).Minimum/.9;
            previews.Add(new(i,j,recipe.Result,new(basis*inherited.Low,basis*inherited.High),inherited,first.Negative||second.Negative));
        }
        return previews.ToArray();
    }
}
