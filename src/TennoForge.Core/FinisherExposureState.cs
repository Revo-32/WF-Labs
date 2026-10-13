namespace TennoForge.Core;

/// <summary>Known exposure lifetime and unresolved wake/animation transitions remain distinct.</summary>
public sealed class FinisherExposureState
{
    public const string SleepSource="https://wiki.warframe.com/w/Sleep?oldid=2816991";
    public double End {get;private set;}=double.NegativeInfinity;
    private double applied=double.NegativeInfinity;
    private bool unknownLifetime,unknownWake,sleep;
    public bool Apply(double time,double? seconds,bool sleeping,double overguard,bool immune=false)
    {
        if(overguard>0||immune)return false;
        if(seconds is {} span)MeleeEngine.Range(span,.001,300,"마무리 노출 시간");
        applied=time;End=seconds is {} duration?time+duration:double.PositiveInfinity;
        unknownLifetime=seconds is null;unknownWake=false;sleep=sleeping;return true;
    }
    public bool Active(double time)=>time<End&&!unknownWake&&(!unknownLifetime||time==applied);
    public void EnsureKnown(double time)
    {
        if(time>=End)return;
        if(unknownWake)throw new CombatRuleUnavailableException("코브라 & 크레인 수면 중 피해를 받은 뒤 조기 기상 조건이 위키에 없습니다. 이후 타격의 은신 보너스를 임의 확정하지 않습니다. "+SleepSource);
        if(unknownLifetime&&time>applied)throw new CombatRuleUnavailableException("시그마 & 옥탄티스 마무리 노출의 지속시간이 위키에 없습니다. 후속 피해를 계산하려면 방패 항목에서 지속시간을 사용자 조건으로 지정하세요. "+ShieldThrowMechanic.SigmaSource);
    }
    public bool Damage(double time)
    {
        if(!sleep||time>=End||unknownWake)return false;
        unknownWake=true;return true;
    }
    public bool Status(DamageType type,double time)
    {
        if(sleep||time>=End||type is not (DamageType.Impact or DamageType.Electricity))return false;
        End=time;unknownLifetime=false;return true;
    }
    public void Clear(){End=double.NegativeInfinity;unknownWake=false;unknownLifetime=false;}
}
