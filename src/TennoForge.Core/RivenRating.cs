namespace TennoForge.Core;

// WF Labs presentation bands, not game rarity or a DPS/build score.
public static class RivenRating
{
    public static string Tier(double quality)
    {
        if(!double.IsFinite(quality)||quality<0||quality>1)throw new ArgumentOutOfRangeException(nameof(quality));
        return quality switch{>=.97=>"SSS",>=.90=>"SS",>=.80=>"S",>=.60=>"A",>=.40=>"B",>=.20=>"C",_=>"F"};
    }
    public static double Quality(double position,bool negative=false)=>negative?1-position:position;
    public static double InheritedPosition(double multiplier)=>Math.Clamp((multiplier-.9)/.2,0,1);
}

public record RivenSpliceOpportunity(string HeldKey,string HeldName,bool HeldNegative,string MissingKey,string MissingName,string ResultKey,string ResultName,double MinimumQuality,double MaximumQuality);
