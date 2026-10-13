namespace TennoForge.Core;

public sealed class MeleeFortificationState
{
    public const string Source="https://wiki.warframe.com/w/Melee_Fortification?oldid=2795431";
    private readonly List<double> ends=[];
    private readonly double perKill;
    private double clock;
    public int Stacks=>ends.Count;
    public double Bonus=>perKill*Stacks;
    public double NextExpiry=>ends.DefaultIfEmpty(double.PositiveInfinity).Min();
    public MeleeFortificationState(int rank)
    {
        if(rank is <0 or >5)throw new ArgumentException("포티피케이션 랭크는 0~5입니다.");
        perKill=35*(rank+1);
    }
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("방어력 버프 시각은 순서대로 진행해야 합니다.");
        clock=Math.Max(clock,time);ends.RemoveAll(e=>e<=clock+1e-9);
    }
    public void Kill(double time)
    {
        Advance(time);
        if(Stacks==255)throw new CombatRuleUnavailableException("포티피케이션 255중첩 이후 추가 처치의 만료 갱신 규칙은 위키 미기재입니다.");
        ends.Add(time+10);
    }
}
