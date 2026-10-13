namespace TennoForge.Core;

/// <summary>Destreza's independently timed status-chance stacks; oldest replaced at five.</summary>
public sealed class PiercingStatureState
{
    public const string Source="https://wiki.warframe.com/w/Destreza_Incarnon_Genesis?oldid=2816743";
    private readonly List<double> ends=[];
    private double clock;
    public int Stacks=>ends.Count;
    public double NextExpiry=>ends.DefaultIfEmpty(double.PositiveInfinity).Min();
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("피어싱 스테처 시각은 순서대로 진행해야 합니다.");
        clock=Math.Max(clock,time);ends.RemoveAll(e=>e<=clock+1e-9);
    }
    public void Proc(double time)
    {
        Advance(time);
        if(ends.Count==5)ends.Remove(ends.Min());
        ends.Add(time+6);
    }
}
