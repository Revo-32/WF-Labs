namespace TennoForge.Core;

/// <summary>One-second holster ticks. Fractional ammunition needs an explicit assumption,
/// because the pinned Reload/mod articles do not specify rounding or accumulation.</summary>
public sealed class RangedHolsterReload
{
    private double remainder;
    public double Rounds(double magazine,double rate,string rounding)
    {
        var amount=magazine*rate;
        if(!double.IsFinite(amount)||amount<0)throw new ArgumentException("수납 재장전 수치 오류");
        if(Math.Abs(amount-Math.Round(amount))<1e-9)return Math.Round(amount);
        return rounding switch
        {
            "Down"=>Math.Floor(amount),
            "Up"=>Math.Ceiling(amount),
            "Accumulate"=>Accumulate(amount),
            _=>throw new CombatRuleUnavailableException("수납 재장전의 초당 회복량이 소수 탄수입니다. 위키에는 반올림·누적 규칙이 없어 확정할 수 없습니다. 상황 설정에서 탄수 처리 가정을 선택할 수 있습니다.")
        };
    }
    private double Accumulate(double amount){var total=amount+remainder;var whole=Math.Floor(total+1e-9);remainder=total-whole;return whole;}
}
