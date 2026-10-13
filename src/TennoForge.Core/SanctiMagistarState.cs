namespace TennoForge.Core;

public sealed class SanctiMagistarState
{
    public const string WeaponId="/Lotus/Weapons/Syndicates/NewLoka/Melee/NLMagistar";
    public const string Source="https://wiki.warframe.com/w/Sancti_Magistar?oldid=2817302";
    public double Minimum {get;private set;}
    public double Maximum {get;private set;}
    public SanctiMagistarState(double previousCombo=0)
    {
        MeleeEngine.Range(previousCombo,0,12,"생티 마기스타 이전 강공격 콤보");
        if(previousCombo!=Math.Truncate(previousCombo))throw new ArgumentException("이전 강공격 콤보는 0~12의 정수입니다. 0은 미발동입니다.");
        Minimum=Maximum=previousCombo*.05;
    }
    public void Heavy(double combo)
    {
        MeleeEngine.Range(combo,1,12,"생티 마기스타 강공격 콤보");
        var rate=combo*.05;
        if(Maximum==0)Minimum=Maximum=rate;
        else {Minimum=Math.Min(Minimum,rate);Maximum=Math.Max(Maximum,rate);}
    }
    public void Death()=>Minimum=Maximum=0;
}
