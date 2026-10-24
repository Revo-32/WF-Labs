namespace TennoForge.Core;

public record RangedRivenComparisonResult(RangedCombatResult Baseline,RangedCombatResult Candidate,
    RangedResult BaselineStats,RangedResult CandidateStats,RangedEncounter Settings);

public static class RangedRivenComparison
{
    // Both candidates start from the same build and target, with the same horizon and random seed.
    public static RangedBuild Equip(RangedBuild build,RangedRivenRoll roll,int destination)
    {
        RangedRivens.Validate(roll);
        if(build.WeaponId!=roll.WeaponId)throw new ArgumentException("모딩 화면에서 리벤과 같은 무기를 선택하세요. 비교에는 그 무기의 모드·적·상황 설정을 사용합니다.");
        if(destination is <0 or >7)throw new ArgumentException("비교할 리벤 슬롯을 선택하세요.");
        var slots=build.Slots.ToArray();
        for(int i=0;i<slots.Length;i++)if(slots[i] is {} slot&&RangedRivens.IsRiven(RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId)))slots[i]=null;
        var weapon=RangedEngine.Resolve(build);var mod=RangedRivens.Mod(weapon);slots[destination]=new(mod.Id,roll.Rank);
        return build with{Slots=slots,Riven=roll};
    }
    public static RangedRivenComparisonResult Calculate(RangedBuild build,RangedRivenRoll baseline,RangedRivenRoll candidate,int destination,
        RangedEncounter settings,CancellationToken cancellation=default,IProgress<SimulationProgress>? progress=null)
    {
        var a=Equip(build,baseline,destination);var b=Equip(build,candidate,destination);
        var statsA=RangedEngine.Calculate(a);var statsB=RangedEngine.Calculate(b);
        cancellation.ThrowIfCancellationRequested();
        var first=RangedCombat.Simulate(a,settings,cancellation,progress);
        cancellation.ThrowIfCancellationRequested();
        var second=RangedCombat.Simulate(b,settings,cancellation,progress);
        return new(first,second,statsA,statsB,settings);
    }
}
