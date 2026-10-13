namespace TennoForge.Core;

/// <summary>Ability recasts are input events, distinct from later damage contacts.</summary>
public sealed class AbilityComboCastState(double duration)
{
    public const string Source="https://www.warframe.com/en/patch-notes/pc/38-5-0";
    private double? previous;
    public int Sequence {get;private set;}
    public bool Cast(double time)
    {
        if(!double.IsFinite(time)||time<0||previous is {} p&&time<p)throw new ArgumentException("어빌리티 시전 시각 오류");
        var continued=previous is {} last&&time-last<duration;
        Sequence=continued?Sequence%3+1:1;previous=time;return continued;
    }
}

/// <summary>Wiki rev.2775801: exact exponential then linear Rage decay.</summary>
public sealed class ValkyrRageState
{
    public const string Source="https://wiki.warframe.com/w/Valkyr/Abilities/Passive?oldid=2775801";
    private double clock,lastGain;
    private double amount;
    public double Raw=>amount;
    public int DamagePercent=>(int)Math.Floor(amount+1e-9);
    public ValkyrRageState(double start=0,double graceRemaining=5)
    {
        MeleeEngine.Range(start,0,300,"발키르 분노");MeleeEngine.Range(graceRemaining,0,5,"분노 감소 유예 시간");
        amount=start;lastGain=graceRemaining-5;
    }
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("분노 시각 오류");
        var elapsed=Math.Max(0,time-Math.Max(clock,lastGain+5));
        // At exp(-lambda*n)=1/16, 300/16=18.75 points remain.
        if(amount>18.75&&elapsed>0)
        {var exponential=Math.Min(elapsed,Math.Log(amount/18.75)/.016);amount*=Math.Exp(-.016*exponential);elapsed-=exponential;}
        amount=Math.Max(0,amount-.3*elapsed);clock=Math.Max(clock,time);
    }
    public void Gain(double time,double value){MeleeEngine.Range(value,0,300,"발키르 분노 획득량");Advance(time);amount=Math.Min(300,amount+value);lastGain=time;}
    public bool PreventDeath(double time){Advance(time);if(DamagePercent<150)return false;amount=0;return true;}
}
