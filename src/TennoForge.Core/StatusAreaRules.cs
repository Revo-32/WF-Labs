namespace TennoForge.Core;

public static class StatusAreaRules
{
    public const string GasSource="https://wiki.warframe.com/w/Damage/Gas_Damage?oldid=2813897";
    public static SourceInterval GasRadius(int stacks)
    {
        if(stacks is <1 or >10)throw new ArgumentException("가스 중첩은1~10입니다.");
        // The text says 3 m initially, +0.3 m per subsequent proc, AND 6 m
        // at ten stacks. Those endpoints do not determine a consistent
        // intermediate formula. Do not silently invent 5.7 m as the cap.
        return stacks switch{1=>new(3,3),10=>new(6,6),_=>new(3+.3*(stacks-1),3+.3*stacks)};
    }
    public static bool GasReaches(int stacks,double distance)
    {
        var r=GasRadius(stacks);
        if(distance>r.Minimum+1e-9&&distance<=r.Maximum+1e-9)throw new CombatRuleUnavailableException("가스 반경: 원문 초기3m·후속중첩+0.3m·10중첩6m의 중간값이 산술적으로 일치하지 않습니다. 이 배치는 그 불확실 구간에 있습니다.");
        return distance<=r.Minimum+1e-9;
    }
}
