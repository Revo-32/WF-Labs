namespace TennoForge.Core;

public static class ExaltedGeometry
{
    public static double? Radius(ExaltedDefinition d,MeleeSetup setup,IReadOnlyDictionary<string,double> effects)
    {
        if(d.Family==ExaltedFamily.Whipclaw)return Math.Max(0,Math.Min(10,new[]{3d,4,4,5}[setup.AbilityRank]*setup.AbilityRange)+effects.GetValueOrDefault("Range"));
        if(d.Family!=ExaltedFamily.Landslide)return null;
        var sequence=setup.Conditions?.GetValueOrDefault("AbilityComboSequence",1)??1;
        if(sequence is <1 or >3||sequence!=Math.Floor(sequence))throw new ArgumentException("랜드슬라이드의 연속 시전 순서는1~3입니다.");
        if(sequence>1&&setup.AbilityRank<3)throw new CombatRuleUnavailableException("랜드슬라이드 랭크0~2의 두 번째·세 번째 타격 반경은 위키에 ?로 표기되어 있습니다. 랭크3의4/6/8m와 첫 타격3/3.2/3.4/4m만 확정됩니다. "+d.Source);
        return (sequence==1?new[]{3d,3.2,3.4,4}[setup.AbilityRank]:sequence==2?6:8)*setup.AbilityRange;
    }
    public static double? Reach(ExaltedDefinition d,MeleeSetup setup)=>d.Family switch
    {
        ExaltedFamily.Whipclaw=>new[]{5d,7,8,10}[setup.AbilityRank]*setup.AbilityRange,
        ExaltedFamily.Landslide=>new[]{6d,8,10,12}[setup.AbilityRank]*(setup.AbilityAugment?.ModId==ExaltedAugments.Rubble&&setup.Conditions?.GetValueOrDefault("RubbleHeap")>0?1+.25*(setup.AbilityAugment.Rank+1):1),
        ExaltedFamily.Lash=>new[]{7d,8,10,12}[setup.AbilityRank]*setup.AbilityRange,
        ExaltedFamily.Clones=>50*setup.AbilityRange,_=>null
    };
}
