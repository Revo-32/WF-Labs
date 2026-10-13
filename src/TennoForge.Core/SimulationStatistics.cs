namespace TennoForge.Core;

public record SimulationProgress(string Phase,int Completed,int Total);
public record SampleEstimate(int Samples,double Mean,double? StandardDeviation,double? StandardError)
{
    // Normal approximation for the sample mean, not a damage/TTK percentile and
    // not an error bound for unverified rules or missing attack timestamps.
    public double? Approximate95HalfWidth=>1.959963984540054*StandardError;
}
public record EncounterPrecision(SampleEstimate FirstScenario,SampleEstimate? SecondScenario=null);
public sealed class SampleMoments
{
    private int count;
    private double mean,m2;
    public void Add(double value)
    {
        if(!double.IsFinite(value))throw new ArgumentException("표본 값은 유한해야 합니다.");
        count++;
        var difference=value-mean;mean+=difference/count;m2+=difference*(value-mean);
    }
    public SampleEstimate Estimate()
    {
        var sd=count>1?Math.Sqrt(Math.Max(0,m2)/(count-1)):(double?)null;
        return new(count,mean,sd,sd/Math.Sqrt(count));
    }
}
