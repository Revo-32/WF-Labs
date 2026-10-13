namespace TennoForge.Core;

/// <summary>Armor and invisibility are combat inputs, not harmless utility labels.</summary>
public static class WarframeCombatArcanes
{
    public static bool ArmorCode(string code)=>code is "Guardian" or "Reaper" or "Ultimatum" or "Tanker";
    public static double ArmorBonus(ArcaneSetup? setup,int ultimatumStacks=1)
    {
        if(ultimatumStacks is <0 or >8)throw new ArgumentException("얼티메이텀의 별도 마무리 유형 중첩 범위 오류");
        return new[]{setup?.Warframe1,setup?.Warframe2}.Where(a=>a?.Active==true).Cast<ArcaneSlot>().Sum(a=>{
            var code=(MeleeRuleCatalog.Load().Arcanes.SingleOrDefault(r=>r.Id==a.Id)??throw new ArgumentException("아케인 식별자 오류")).Code;
            return (a.Rank+1)*(code switch{"Guardian"=>150,"Reaper"=>110,"Tanker"=>200,"Ultimatum"=>200*ultimatumStacks,_=>0});
        });
    }
    public static Build ApplyConditions(Build build)
    {
        var conditions=new Dictionary<string,double>(build.Melee!.Conditions??[]);
        var stacks=conditions.TryGetValue("UltimatumStacks",out var count)?count:1;
        if(stacks!=Math.Truncate(stacks))throw new ArgumentException("얼티메이텀의 유형 중첩은 정수입니다.");
        var bonus=ArmorBonus(build.Arcanes,(int)stacks)+WarcryRules.Armor(build.Melee.ExternalEquipment?.Warcry)+(build.Melee.ExternalEquipment?.AtlasRubble?.Armor??0);
        if(build.Arcanes?.PlayerArmor is {} armor)conditions["Armor450"]=armor+bonus>450?1:0;
        else if(bonus>450)conditions["Armor450"]=1;
        if(new[]{build.Arcanes?.Warframe1,build.Arcanes?.Warframe2}.Any(a=>a?.Active==true&&(MeleeRuleCatalog.Load().Arcanes.SingleOrDefault(r=>r.Id==a.Id)??throw new ArgumentException("아케인 식별자 오류")).Code=="Trickery"))conditions["Invisible"]=1;
        return build with {Melee=build.Melee with {Conditions=conditions}};
    }
}
