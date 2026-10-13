namespace TennoForge.Core;

public sealed class MeleeInfluenceState
{
    public const string Source="https://wiki.warframe.com/w/Melee_Influence?oldid=2805640";
    public double End {get;private set;}=double.NegativeInfinity;
    public double Radius {get;}
    public double Duration {get;}
    public MeleeInfluenceState(int rank)
    {
        if(rank is <0 or >5)throw new ArgumentException("인플루언스 랭크는0~5입니다.");
        Radius=10+2*rank;Duration=3*(rank+1);
    }
    public bool Active(double time)=>time<End;
    public bool TryTrigger(double time,DamageType type,bool direct,bool survived,Func<double> random)
    {
        if(!direct||!survived||type!=DamageType.Electricity||Active(time)||random()>=.2)return false;
        End=time+Duration;return true;
    }
    public static bool Spreads(DamageType type)=>type is DamageType.Cold or DamageType.Electricity or DamageType.Heat or DamageType.Toxin or DamageType.Blast or DamageType.Corrosive or DamageType.Gas or DamageType.Magnetic or DamageType.Radiation or DamageType.Viral;
}
