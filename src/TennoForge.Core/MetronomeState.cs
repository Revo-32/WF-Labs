namespace TennoForge.Core;

public record MetronomeSetup(int Rank=3,double Strength=1,double Duration=1,bool Opera=false,bool Forte=false,double? OperaRemaining=null,double? ForteRemaining=null);
public static class MetronomeRules
{
    public const string Source="https://wiki.warframe.com/w/Metronome?oldid=2816159";
    public const string MultishotSource="https://wiki.warframe.com/w/Multishot?oldid=2815667";
    public const string RedeemerPrime="/Lotus/Weapons/Tenno/Melee/Gunblade/RedeemerPrime/RedeemerPrimeWep";
    public static void Validate(MetronomeSetup s)
    {
        MeleeEngine.Range(s.Rank,0,3,"메트로놈 랭크");MeleeEngine.Range(s.Strength,0,20,"메트로놈 시전자 위력");MeleeEngine.Range(s.Duration,.01,20,"메트로놈 시전자 지속");
        if(s.OperaRemaining is {} o)MeleeEngine.Range(o,0,Lifetime(s),"오페라 시작 잔여 시간");if(s.ForteRemaining is {} f)MeleeEngine.Range(f,0,Lifetime(s),"포르테 시작 잔여 시간");
    }
    public static double Lifetime(MetronomeSetup s)=>new[]{5d,8,12,15}[s.Rank]*s.Duration;
    public static double Opera(MetronomeSetup? s)=>s is {Opera:true}&&(s.OperaRemaining??Lifetime(s))>0?new[]{.12,.2,.25,.3}[s.Rank]*s.Strength:0;
    public static double Forte(MetronomeSetup? s)=>s is {Forte:true}&&(s.ForteRemaining??Lifetime(s))>0?new[]{.2,.25,.25,.3}[s.Rank]*s.Strength:0;
    public static bool OperaApplies(string weapon,string attack)=>WeaponStanceMechanic.IsQuassus(weapon)&&attack.Contains("Ethereal Daggers",StringComparison.Ordinal)||weapon==RedeemerPrime&&attack is "Ranged Attack" or "Heavy Attack";
    public static int Roll(double count,Func<double> random)=>count==Math.Floor(count)?(int)count:Sample(count,random());
    public static int Sample(double count,double roll)
    {MeleeEngine.Range(count,0,10000,"멀티샷 기대 투사체 수");MeleeEngine.Range(roll,0,1,"멀티샷 표본");var n=(int)Math.Floor(count);return n+(roll<count-n?1:0);}
}
public sealed class MetronomeState
{
    private readonly MetronomeSetup setup;
    private double operaEnd,forteEnd;
    public MetronomeState(MetronomeSetup s)
    {MetronomeRules.Validate(s);setup=s;operaEnd=s.Opera?s.OperaRemaining??MetronomeRules.Lifetime(s):0;forteEnd=s.Forte?s.ForteRemaining??MetronomeRules.Lifetime(s):0;}
    public void Receive(double time,bool opera){if(opera)operaEnd=time+MetronomeRules.Lifetime(setup);else forteEnd=time+MetronomeRules.Lifetime(setup);}
    public void Clear(){operaEnd=0;forteEnd=0;}
    public MetronomeSetup At(double time)=>setup with {Opera=operaEnd>time,Forte=forteEnd>time,OperaRemaining=Math.Max(0,operaEnd-time),ForteRemaining=Math.Max(0,forteEnd-time)};
    public double NextExpiry(double time)=>new[]{operaEnd,forteEnd}.Where(t=>t>time+1e-9).DefaultIfEmpty(double.PositiveInfinity).Min();
}
