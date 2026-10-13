namespace TennoForge.Core;

/// <summary>Integrates named, non-stacking healing sources without inventing the player's missing health.</summary>
public sealed class TimedHealthRegeneration
{
    private readonly Dictionary<string,(double Rate,double End)> sources=[];
    private readonly Dictionary<string,double> generated=[];
    private double time;
    public double Generated(string source)=>generated.GetValueOrDefault(source);
    public void Advance(double now)
    {
        MeleeEngine.Range(now,time-1e-9,300,"체력 재생 시각");now=Math.Max(now,time);
        foreach(var (key,value) in sources)generated[key]=Generated(key)+value.Rate*Math.Max(0,Math.Min(now,value.End)-time);
        time=now;
    }
    public void Activate(double now,string source,double rate,double duration)
    {
        MeleeEngine.Range(rate,0,1e6,"체력 재생량");MeleeEngine.Range(duration,0,300,"체력 재생 지속시간");
        Advance(now);sources[source]=(rate,now+duration);
    }
}
