namespace TennoForge.Core;

public static class WarframeStatusArcanes
{
    public const string Truculence="/Lotus/Upgrades/CosmeticEnhancers/Defensive/RadialViralAttackOnOverguardGain";
    public const string Fallout="/Lotus/Upgrades/CosmeticEnhancers/Utility/AbilityRadiationProcsCreateUniversalOrbsOnKill";
    public const string Eruption="/Lotus/Upgrades/CosmeticEnhancers/Utility/RadialKnockdownOnEnergyPickup";
    public const string TruculenceSource="https://wiki.warframe.com/w/Arcane_Truculence?oldid=2702306";
    public const string FalloutSource="https://wiki.warframe.com/w/Arcane_Universal_Fallout?oldid=2809434";
    public const string EruptionSource="https://wiki.warframe.com/w/Arcane_Eruption?oldid=2700554";
    public static ArcaneSlot? Find(Build b,string id)=>new[]{b.Arcanes?.Warframe1,b.Arcanes?.Warframe2}.SingleOrDefault(s=>s?.Id==id);
    public static double EruptionChance(int rank){MeleeEngine.Range(rank,0,5,"이럽션 랭크");return Math.Min(1,.17*(rank+1));}
    public static double EruptionRadius(int rank){MeleeEngine.Range(rank,0,5,"이럽션 랭크");return 5*(rank+1);}
}

public sealed class TruculenceState
{
    public double Progress {get;private set;}
    public long Waves {get;private set;}
    public TruculenceState(double progress=0){MeleeEngine.Range(progress,0,2999.999999,"트루큘런스 시작 획득량");Progress=progress;}
    public long Gain(double overguard)
    {
        MeleeEngine.Range(overguard,0,1e12,"실제로 획득한 오버가드");
        var total=Progress+overguard;var waves=(long)Math.Floor(total/3000);Progress=total-waves*3000;Waves+=waves;return waves;
    }
}

/// <summary>Each target keeps permanent ability-Radiation credit, independently
/// of the live status cap/expiry. A drop is distinct from a pickup.</summary>
public sealed class UniversalFalloutState
{
    private readonly int rank;
    public UniversalFalloutState(int rank){MeleeEngine.Range(rank,0,5,"유니버설 폴아웃 랭크");this.rank=rank;}
    public int AbilityRadiationProcs {get;private set;}
    public double DropChance=>Math.Min(.6,(rank+1)*.01*AbilityRadiationProcs);
    public void Apply(int count=1)
    {
        MeleeEngine.Range(rank,0,5,"유니버설 폴아웃 랭크");
        if(count<0)throw new ArgumentException("어빌리티 방사능 횟수 오류");AbilityRadiationProcs=(int)Math.Min(60,(long)AbilityRadiationProcs+count);
    }
    public bool Drops(double draw){MeleeEngine.Range(draw,0,1,"오브 추첨값");return draw<DropChance;}
}
