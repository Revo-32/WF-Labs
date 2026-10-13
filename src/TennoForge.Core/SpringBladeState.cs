namespace TennoForge.Core;

/// <summary>Two independently expiring range stacks, not a permanent toggle.</summary>
public sealed class SpringBladeState
{
    public const string Source="https://wiki.warframe.com/w/Spring-Loaded_Blade?oldid=2782020";
    private readonly List<double> ends=[];
    private readonly double duration;
    private double uncertainFrom=double.PositiveInfinity;
    public SpringBladeState(int rank)
    {if(rank is <0 or >5)throw new ArgumentException("스프링 로디드 블레이드 랭크 오류");duration=4*(rank+1);}
    public double NextExpiry=>Math.Min(uncertainFrom,ends.DefaultIfEmpty(double.PositiveInfinity).Min());
    public int Count(double time)
    {
        if(time>=uncertainFrom-1e-9)throw new CombatRuleUnavailableException("스프링 로디드 블레이드의 2중첩 독립 만료는 확인되지만, 최대 중첩에서 추가 발동 시 어느 타이머를 갱신하는지는 위키 미기재입니다. "+Source);
        ends.RemoveAll(e=>e<=time+1e-9);return ends.Count;
    }
    public void Proc(double time)
    {
        _=Count(time);
        if(ends.Count<2)ends.Add(time+duration);
        else if(time+duration>ends.Min()+1e-9)uncertainFrom=Math.Min(uncertainFrom,ends.Min());
    }
}
