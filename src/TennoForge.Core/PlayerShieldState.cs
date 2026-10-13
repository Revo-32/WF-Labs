namespace TennoForge.Core;

/// <summary>Explicit player resources. Enemy shields are a different state.</summary>
public sealed class PlayerShieldState
{
    public double Maximum {get;}
    public double MaximumOvershields {get;}
    public double Shields {get;private set;}
    public double Overshields {get;private set;}
    private readonly bool rechargeEnabled;
    private readonly double rechargeRate,delayReduction,rechargeMultiplier;
    private double time,rechargeAt;
    private double aegisEnd,aegisBonus,magneticEnd;
    public bool AegisActive=>aegisEnd>time;
    public PlayerShieldState(double maximum,double maximumOvershields,double shields,double overshields,bool rechargeEnabled=true,double rechargeBonus=0,double delayReduction=0,double delayRemaining=0)
    {
        MeleeEngine.Range(maximum,0,1e6,"워프레임 최대 실드");MeleeEngine.Range(maximumOvershields,0,1e6,"오버실드 한도");
        MeleeEngine.Range(shields,0,maximum,"워프레임 시작 실드");MeleeEngine.Range(overshields,0,maximumOvershields,"시작 오버실드");
        if(overshields>0&&(shields!=maximum||maximum==0))throw new ArgumentException("오버실드는 기본 실드가 가득 찬 상태에서 설정하세요.");
        Maximum=maximum;MaximumOvershields=maximumOvershields;Shields=shields;Overshields=overshields;
        MeleeEngine.Range(rechargeBonus,-1,100,"실드 재생 속도 보너스");MeleeEngine.Range(delayReduction,0,.8,"실드 재생 대기 감소");MeleeEngine.Range(delayRemaining,0,300,"시작 시 실드 재생 대기");
        this.rechargeEnabled=rechargeEnabled;rechargeMultiplier=1+rechargeBonus;rechargeRate=(15+.05*maximum)*rechargeMultiplier;this.delayReduction=delayReduction;rechargeAt=delayRemaining;
    }
    public void Advance(double now)
    {
        // Event additions and chart fractions can spell the same instant as
        // 0.6000000000000001 and 0.6. Clamp only that rounding gap.
        if(now<time&&time-now<=1e-9)now=time;
        MeleeEngine.Range(now,time,300,"실드 사건 시각");
        var activeEnd=Math.Min(now,aegisEnd);
        var activeStart=Math.Max(time,magneticEnd);
        if(activeEnd>activeStart)Restore((15+(.05+aegisBonus)*Maximum)*rechargeMultiplier*(activeEnd-activeStart),false);
        var ordinaryStart=Math.Max(Math.Max(time,aegisEnd),Math.Max(magneticEnd,rechargeAt));
        if(rechargeEnabled&&now>ordinaryStart)Restore(rechargeRate*(now-ordinaryStart),false);
        time=now;
    }
    public void IncomingHit()=>rechargeAt=AegisActive?time:Math.Max(rechargeAt,time+(Shields+Overshields>0?1:4)*(1-delayReduction));
    public bool ActivateAegis(int rank)
    {
        if(rank is <0 or >5)throw new ArgumentException("이지스 랭크 범위 오류");
        if(Maximum==0||AegisActive)return false;
        aegisEnd=time+12;aegisBonus=.05*(rank+1);rechargeAt=time;return true;
    }
    public void Magnetic(double duration=6)
    {MeleeEngine.Range(duration,0,300,"플레이어 자성 지속시간");magneticEnd=time+duration;}
    public double Damage(double amount)
    {
        MeleeEngine.Range(amount,0,1e15,"실드 피격량");
        var extra=Math.Min(amount,Overshields);Overshields-=extra;amount-=extra;
        var ordinary=Math.Min(amount,Shields);Shields-=ordinary;
        if(extra+ordinary>0)IncomingHit();return extra+ordinary;
    }
    public void Break(){Shields=0;Overshields=0;IncomingHit();}
    public double Restore(double amount,bool allowOvershields,bool needsExistingShield=false)
    {
        MeleeEngine.Range(amount,0,1e15,"실드 회복량");
        if(Maximum==0||needsExistingShield&&Shields+Overshields<1)return 0;
        var ordinary=Math.Min(amount,Maximum-Shields);Shields+=ordinary;amount-=ordinary;
        var extra=allowOvershields?Math.Min(amount,MaximumOvershields-Overshields):0;Overshields+=extra;
        return ordinary+extra;
    }
}
