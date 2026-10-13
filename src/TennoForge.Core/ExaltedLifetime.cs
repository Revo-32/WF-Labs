namespace TennoForge.Core;

/// <summary>Enraged cooldown equals actual active time and starts after ending.
/// Transference delays the START of cooldown; it does not pause one already running.</summary>
public sealed class ExaltedLifetime
{
    public const string Source="https://wiki.warframe.com/w/Enraged?oldid=2729819";
    private readonly double? duration;
    private double started,clock,pendingCooldown;
    public bool Active {get;private set;}=true;
    public bool Transferred {get;private set;}
    public double End {get;private set;}
    public double ReadyAt {get;private set;}
    public bool WaitingForReturn=>pendingCooldown>0;
    public bool CanAttack=>Active&&!Transferred;
    public double NextTransition=>Active?End:!WaitingForReturn&&ReadyAt>clock+1e-9?ReadyAt:double.PositiveInfinity;
    public ExaltedLifetime(double? duration=null,double elapsed=0)
    {
        if(duration is {} d)MeleeEngine.Range(d,.15,1500,"인레이지드 지속시간");
        MeleeEngine.Range(elapsed,0,duration??0,"현재 소환 경과 시간");
        this.duration=duration;started=-elapsed;End=duration is {} total?total-elapsed:double.PositiveInfinity;
        Advance(0);
    }
    private void EndAt(double time)
    {
        var used=duration is null?0:Math.Clamp(time-started,0,duration.Value);Active=false;End=double.PositiveInfinity;
        if(Transferred&&used>0){pendingCooldown=used;ReadyAt=double.PositiveInfinity;}
        else{pendingCooldown=0;ReadyAt=time+used;}
    }
    public bool Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("익절티드 상태 시간 오류");
        var ended=Active&&time>=End-1e-9;if(ended)EndAt(End);clock=Math.Max(clock,time);return ended;
    }
    public bool Deactivate(double time){Advance(time);if(!Active)return false;EndAt(time);return true;}
    public bool Activate(double time)
    {
        Advance(time);if(Active||Transferred||WaitingForReturn||time<ReadyAt-1e-9)return false;
        Active=true;started=time;End=duration is {} d?time+d:double.PositiveInfinity;return true;
    }
    public void Transfer(double time,bool outside)
    {
        Advance(time);Transferred=outside;
        if(!outside&&WaitingForReturn){ReadyAt=time+pendingCooldown;pendingCooldown=0;}
    }
}
