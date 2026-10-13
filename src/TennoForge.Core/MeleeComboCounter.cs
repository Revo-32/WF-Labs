namespace TennoForge.Core;

/// <summary>
/// Combo points are a timed resource, separate from the displayed multiplier.
/// Wiki: Melee Combo revision 2817078; initial combo regenerates at 40 points/s.
/// </summary>
public sealed class MeleeComboCounter
{
    private double clock;
    private double expires;
    private double initial;
    private readonly double duration;
    private readonly int threshold;
    private readonly int maximum;
    public bool Paused {get;private set;}
    public double Points { get; private set; }
    public int Multiplier => Math.Min(maximum,1+(int)(Points/threshold));
    public double Capacity => (maximum-1)*threshold;

    public MeleeComboCounter(double initial,int startMultiplier,int threshold,int maximum,double duration)
    {
        if(!double.IsFinite(initial)||initial<0||threshold<=0||maximum<1||startMultiplier<1||startMultiplier>maximum||double.IsNaN(duration))
            throw new ArgumentException("콤보 초기 조건 오류");
        this.threshold=threshold;this.maximum=maximum;this.duration=duration;
        this.initial=Math.Min(initial,Capacity);
        Points=Math.Min(Capacity,Math.Max(this.initial,(startMultiplier-1)*threshold));
        expires=duration>0?duration:double.PositiveInfinity;
    }

    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("콤보 시각은 순서대로 진행해야 합니다.");
        // The event queue treats timestamps within 1 ns as simultaneous.
        time=Math.Max(clock,time);
        if(Paused&&double.IsFinite(expires))expires+=time-clock;
        if(time>=expires)
        {
            // Expiry clears earned combo above the floor. It must not refill a
            // spent initial combo instantly while its 40 points/s regeneration
            // is still running (e.g. 220 initial takes 5.5 seconds).
            Points=Points>=initial?initial:Math.Min(initial,Points+(time-clock)*40);
            expires=double.PositiveInfinity;
        }
        else if(Points<initial)Points=Math.Min(initial,Points+(time-clock)*40);
        clock=time;
    }

    public void SetInitial(double time,double value)
    {
        if(!double.IsFinite(value)||value<0)throw new ArgumentException("초기 콤보 수치 오류");
        Advance(time);
        var old=initial;initial=Math.Min(value,Capacity);
        // After the earned combo timer has already expired, points held only
        // by an old minimum must follow a lower minimum. Earned active combo
        // is retained until its own expiry; an increase regenerates at 40/s.
        if(double.IsPositiveInfinity(expires)&&initial<old&&Points<=old)Points=Math.Min(Points,initial);
    }

    public void SetPaused(double time,bool paused)
    {
        Advance(time);Paused=paused;
    }

    public double Gain(double time,double points)
    {
        if(!double.IsFinite(points)||points<0)throw new ArgumentException("콤보 획득량 오류");
        Advance(time);
        if(duration<=0||points==0)return 0;
        var before=Points;Points=Math.Min(Capacity,Points+points);
        expires=time+duration;
        return Points-before;
    }

    public double SpendHeavy(double time,double efficiency,bool tennokai)
    {
        if(!double.IsFinite(efficiency)||efficiency is <0 or >.9)throw new ArgumentException("강공격 효율 범위 오류");
        Advance(time);
        var before=Points;
        if(!tennokai)Points*=efficiency;
        expires=duration>0?time+duration:double.PositiveInfinity;
        return before-Points;
    }

    public double Reset(double time)
    {
        Advance(time);var lost=Points;Points=0;expires=duration>0?time+duration:double.PositiveInfinity;return lost;
    }
}
