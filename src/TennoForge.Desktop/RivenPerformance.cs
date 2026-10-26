using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TennoForge.Desktop;

// Explicit, bounded live timing only. Does not capture images or send input.
internal static class RivenPerformance
{
    private static readonly string? PathName=Environment.GetEnvironmentVariable("WF_LABS_RIVEN_TRACE");
    private static readonly AsyncLocal<string?> Session=new();
    private static readonly object Sync=new();
    private static readonly Stopwatch Lifetime=Stopwatch.StartNew();
    private static int entries;
    internal static IDisposable Measure(string stage,object? data=null)=>PathName is null?Empty.Instance:new Scope(stage,data);
    internal static T Time<T>(string stage,Func<T> action,object? data=null){using var scope=Measure(stage,data);return action();}
    internal static void Event(string stage,object? data=null,double? milliseconds=null)
    {
        if(PathName is null||Lifetime.Elapsed>TimeSpan.FromMinutes(30)||Interlocked.Increment(ref entries)>4000)return;
        try{lock(Sync)File.AppendAllText(PathName,JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,session=Session.Value,stage,milliseconds,data})+Environment.NewLine);}
        catch(IOException){}catch(UnauthorizedAccessException){}
    }
    private sealed class Scope:IDisposable
    {
        private readonly string stage;private readonly object? data;private readonly bool root;private readonly Stopwatch clock=Stopwatch.StartNew();
        internal Scope(string stage,object? data){this.stage=stage;this.data=data;root=Session.Value is null;if(root)Session.Value=Guid.NewGuid().ToString("N")[..12];Event(stage+".begin",data);}
        public void Dispose(){Event(stage,data,clock.Elapsed.TotalMilliseconds);if(root)Session.Value=null;}
    }
    private sealed class Empty:IDisposable{internal static readonly Empty Instance=new();public void Dispose(){}}
}
