namespace TennoForge.Core;

public record WarcrySetup(int Rank=3,double Strength=1,double Duration=1,bool Helminth=false,
    int? EternalWarRank=null,bool SelfCast=true,bool Active=true,double? Remaining=null,
    double BaseArmor=855,bool HysteriaActive=false);

public static class WarcryRules
{
    public const string Source="https://wiki.warframe.com/w/Warcry?oldid=2816217";
    public const string ReworkSource="https://www.warframe.com/en/patch-notes/pc/39-0-0";
    public const string EternalSource="https://wiki.warframe.com/w/Eternal_War?oldid=2729817";
    public static void Validate(WarcrySetup s)
    {
        MeleeEngine.Range(s.Rank,0,3,"워크라이 랭크");MeleeEngine.Range(s.Strength,0,20,"워크라이 시전자 위력");
        MeleeEngine.Range(s.Duration,.01,20,"워크라이 시전자 지속");MeleeEngine.Range(s.BaseArmor,0,1e7,"워크라이 수신자의 기본 방어력");
        if(s.EternalWarRank is {} rank)MeleeEngine.Range(rank,0,3,"이터널 워 랭크");
        if(s.Remaining is {} left)MeleeEngine.Range(left,0,BaseDuration(s)*(s.EternalWarRank is null?1:2),"워크라이 시작 잔여 시간");
        if(s.SelfCast&&s.Helminth&&s.HysteriaActive)throw new ArgumentException("헬민스 워크라이 시전자는 히스테리아를 사용할 수 없습니다.");
    }
    public static double BaseDuration(WarcrySetup s)=>new[]{10d,14,17,20}[s.Rank]*s.Duration;
    public static bool Active(WarcrySetup? s)=>s is {Active:true}&&(s.Remaining??BaseDuration(s))>0;
    public static double Speed(WarcrySetup s)=>Active(s)?(s.Helminth?new[]{.09,.12,.15,.3}:new[]{.15,.2,.25,.5})[s.Rank]*s.Strength:0;
    public static double Armor(WarcrySetup? s)
    {
        if(s is null)return 0;
        Validate(s);
        return Active(s)?s.BaseArmor*new[]{.25,.35,.45,.5}[s.Rank]*s.Strength*(s.HysteriaActive?3:1):0;
    }
}

public sealed class WarcryState
{
    private readonly WarcrySetup setup;
    private double end;
    private bool received;
    public WarcryState(WarcrySetup s)
    {
        WarcryRules.Validate(s);setup=s;received=s.Active;
        end=s.Active?s.Remaining??WarcryRules.BaseDuration(s):0;
    }
    public double Remaining(double time)=>received?Math.Max(0,end-time):0;
    public double NextExpiry(double time)=>end>time+1e-9?end:double.PositiveInfinity;
    public WarcrySetup At(double time)=>setup with {Active=Remaining(time)>0,Remaining=Remaining(time)};
    public bool Cast(double time)
    {
        // Update 39 explicitly permits recasting while active, refreshing the timer.
        received=true;end=time+WarcryRules.BaseDuration(setup);return true;
    }
    public void Lose(bool casterDispel=false)
    {received=false;if(setup.SelfCast||casterDispel)end=0;}
    public bool Reenter(double time)
    {if(setup.SelfCast||end<=time)return false;received=true;return true;}
    public double Kill(double time,bool casterKill,bool disc=false,bool hystericalLeap=false)
    {
        if(!casterKill||hystericalLeap||end<=time||setup.EternalWarRank is not {} rank)return 0;
        // The Zenistar article explicitly excludes maintaining an ally's Warcry via disc kills.
        if(disc&&!setup.SelfCast)return 0;
        var previous=end;end=Math.Min(time+2*WarcryRules.BaseDuration(setup),end+new[]{1.25,1.5,1.75,2}[rank]*setup.Duration);
        return end-previous;
    }
}
