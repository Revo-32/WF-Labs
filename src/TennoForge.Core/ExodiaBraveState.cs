namespace TennoForge.Core;

/// <summary>Integrates Brave's regeneration; casting pauses generation, not the buff clock.</summary>
public sealed class ExodiaBraveState
{
    public const string Source="https://wiki.warframe.com/w/Exodia_Brave?oldid=2722089";
    private readonly double perStack;
    private double clock,end,pauseEnd;
    private bool channeling;
    public int Stacks {get;private set;}
    public double Generated {get;private set;}
    public double Rate=>channeling||clock<pauseEnd||clock>=end?0:perStack*Stacks;
    public ExodiaBraveState(int rank,bool channeling=false)
    {
        if(rank is <0 or >3)throw new ArgumentException("브레이브 랭크는 0~3입니다.");
        perStack=1.25*(rank+1);this.channeling=channeling;
    }
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("에너지 재생 시각은 순서대로 진행해야 합니다.");
        time=Math.Max(time,clock);
        if(!channeling)Generated+=Math.Max(0,Math.Min(time,end)-Math.Max(clock,pauseEnd))*perStack*Stacks;
        clock=time;if(clock>=end-1e-9)Stacks=0;
    }
    public void HeavyKill(double time)
    {
        Advance(time);
        // The pinned Wiki explicitly records no timer refresh at the cap.
        if(Stacks<3){Stacks++;end=time+4;}
    }
    public void Cast(double time){Advance(time);pauseEnd=Math.Max(pauseEnd,time+1);}
    public void Channel(double time,bool active){Advance(time);channeling=active;}
}
