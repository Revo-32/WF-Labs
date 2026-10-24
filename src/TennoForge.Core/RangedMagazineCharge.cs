namespace TennoForge.Core;
/// <summary>Angstrum charges one rocket per base charge interval. The last salvo uses the remaining magazine.</summary>
public static class RangedMagazineCharge
{
    public static bool Applies(ReferenceWeapon w,int attack)=>RangedEngine.S(w.Raw,"Family")=="Angstrum"&&attack is 0 or 1;
    public static double MagazineCycle(ReferenceWeapon w,RangedBuild b,RangedResult result)
    {
        var cadence=result.Cadence!;if(!Applies(w,b.AttackIndex))return cadence.MagazineCycle();
        var c=b.Conditions??new();var configured=Rounds(w,c,result.Modifiers!);var perRocket=cadence.Charge/configured;
        double time=0;int remaining=cadence.Magazine;
        while(remaining>0){var rounds=Math.Min(remaining,c.ChargedRounds==0?cadence.Magazine:c.ChargedRounds);time+=perRocket*rounds+cadence.ShotDelay;remaining-=rounds;}
        return time+cadence.Reload;
    }
    public static int Rounds(ReferenceWeapon w,RangedConditions c,Dictionary<string,double> effects)
    {
        if(c.ChargedRounds is <0 or >10000||c.LoadedAmmo is {} supplied&&(!double.IsFinite(supplied)||supplied<=0||supplied>10000))throw new ArgumentException("충전 로켓 수와 장전 탄약을 확인하세요.");
        var capacity=Math.Max(1,(int)Math.Round(((RangedEngine.N(w.Raw,"Magazine")??1)+effects.GetValueOrDefault("BaseMagazine"))*(1+effects.GetValueOrDefault("Magazine")),MidpointRounding.AwayFromZero));
        var loaded=c.LoadedAmmo is {} amount?Math.Min(capacity,(int)Math.Floor(amount)):capacity;
        if(loaded<1)throw new ArgumentException("충전에 사용할 탄약이 없습니다.");
        return Math.Min(loaded,c.ChargedRounds==0?capacity:c.ChargedRounds);
    }
}
