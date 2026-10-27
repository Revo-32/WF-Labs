using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

// Explicit, bounded, offline profiling on the validation desktop. The sampler
// never captures a screen, watches another process, changes focus or trims RAM.
internal sealed class ResourceSampler : IAsyncDisposable
{
    internal sealed record Sample(double Milliseconds,string Stage,double CpuMilliseconds,long WorkingSetBytes,long PrivateBytes,long ManagedBytes,long AllocatedBytes,int Gen0,int Gen1,int Gen2);
    private readonly CancellationTokenSource stop=new();
    private readonly ConcurrentQueue<Sample> samples=new();
    private readonly Task sampling;
    private int disposed;
    private string stage="startup";
    internal string Stage {set=>Volatile.Write(ref stage,value);}
    internal ResourceSampler()
    {
        sampling=Task.Run(async()=>
        {
            using var process=Process.GetCurrentProcess();var clock=Stopwatch.StartNew();
            while(!stop.IsCancellationRequested)
            {
                process.Refresh();
                samples.Enqueue(new(clock.Elapsed.TotalMilliseconds,Volatile.Read(ref stage),process.TotalProcessorTime.TotalMilliseconds,
                    process.WorkingSet64,process.PrivateMemorySize64,GC.GetTotalMemory(false),GC.GetTotalAllocatedBytes(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)));
                try{await Task.Delay(200,stop.Token);}catch(OperationCanceledException){break;}
            }
        });
    }
    internal Sample[] Samples=>samples.ToArray();
    public async ValueTask DisposeAsync(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();await sampling;stop.Dispose();}
}

public sealed partial class MainWindow
{
    internal async Task<object> CheckResourceUsage(string dir)
    {
        var runs=new List<object>();var outcomes=new List<object>();
        await using var monitor=new ResourceSampler();
        async Task Measure(string stage,Func<Task> action)
        {
            monitor.Stage=stage;using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(true);
            var timer=Stopwatch.StartNew();await action();timer.Stop();process.Refresh();
            var samples=monitor.Samples.Where(s=>s.Stage==stage).ToArray();
            runs.Add(new{stage,seconds=timer.Elapsed.TotalSeconds,cpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,
                cpuMachinePercent=(process.TotalProcessorTime-cpu).TotalMilliseconds/timer.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,
                allocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,samples=samples.Length,
                peakWorkingSetBytes=samples.Select(s=>s.WorkingSetBytes).DefaultIfEmpty(process.WorkingSet64).Max(),
                peakPrivateBytes=samples.Select(s=>s.PrivateBytes).DefaultIfEmpty(process.PrivateMemorySize64).Max(),
                endWorkingSetBytes=process.WorkingSet64,endPrivateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false)});
            File.WriteAllText(Path.Combine(dir,"phases.json"),JsonSerializer.Serialize(runs,Catalog.Json));
        }
        EnsureSchedulePage();ShowModding();
        // Preserve the same instrumentation source for the frozen RC2 baseline,
        // which predates the visibility seam used by isolated-render UI tests.
        void DashboardVisible(bool value)=>typeof(SchedulePage).GetProperty("DashboardVisibilityOverride",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(schedulePage,value);
        DashboardVisible(false);
        var clockTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        clockTimer.Tick+=(_,_)=>schedulePage!.Tick();clockTimer.Start();
        try
        {
            await Measure("startup-settling",()=>Task.Delay(8000));
            await Measure("idle-modding-hidden-schedule",()=>Task.Delay(10_000));
            DashboardVisible(true);ShowSchedules();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Measure("idle-schedule-visible",()=>Task.Delay(10_000));
            ShowModding();DashboardVisible(false);
            var catalog=RelicCatalog.Bundled();
            var source=Path.Combine(Environment.CurrentDirectory,"artifacts","relic-styanax-20261027","row-3440.png");
            if(!File.Exists(source))source=Path.Combine(Environment.CurrentDirectory,"tests","fixtures","relic","styanax-three-rewards.png");
            var realImage=RelicOcr.Load(source);
            for(int i=0;i<3;i++)await Measure("relic-three-"+i,async()=>
            {
                var updates=new List<RelicRecognition>();var result=await RelicOcr.RecognizeAsync(realImage,catalog,default,live:true,onProgress:updates.Add);
                string[] expected=["yareli_prime_neuroptics_blueprint","styanax_prime_chassis_blueprint","paris_prime_string"];
                if(!result.Rewards.Select(r=>r.Item?.Slug).SequenceEqual(expected)||updates.SelectMany(u=>u.Rewards).Any(r=>r.Confident&&!expected.Contains(r.Item?.Slug)))
                    throw new InvalidOperationException("자원 측정 중 성유물 정답·중간 표시 불일치");
                outcomes.Add(new{stage="relic-three-"+i,result.Timing,actual=result.Rewards.Select(r=>r.Item?.Slug)});
            });
            var fixtureRoot=Path.Combine(Environment.CurrentDirectory,"tests","fixtures","riven");
            foreach(var name in new[]{"innodem-real.png","reroll-pair.png","reroll-pair.png"})
            {
                var image=RelicOcr.Load(Path.Combine(fixtureRoot,name));string stage="riven-"+outcomes.Count;
                await Measure(stage,async()=>
                {
                    var probe=RivenVisualProbe.Analyze(image);var result=await RivenOcr.ReadAutomaticAsync(image,probe.Regions,"Auto",8,default);
                    int expected=name=="innodem-real.png"?1:2;
                    if(result.Cards.Length!=expected||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid))throw new InvalidOperationException("자원 측정 중 리벤 교차 검증 실패");
                    outcomes.Add(new{stage,name,result.Seconds,cards=result.Cards.Select(c=>new{c.Report.Kind,c.Corroborated,c.Report.Options})});
                });
            }
            await Measure("idle-after-ocr-models",()=>Task.Delay(10_000));
        }
        finally{clockTimer.Stop();}
        await monitor.DisposeAsync();
        File.WriteAllText(Path.Combine(dir,"samples.json"),JsonSerializer.Serialize(monitor.Samples,Catalog.Json));
        var csv=new StringBuilder("milliseconds,stage,cpu_milliseconds,working_set_bytes,private_bytes,managed_bytes,allocated_bytes,gen0,gen1,gen2\n");
        foreach(var s in monitor.Samples)csv.AppendLine(FormattableString.Invariant($"{s.Milliseconds:F3},{s.Stage},{s.CpuMilliseconds:F3},{s.WorkingSetBytes},{s.PrivateBytes},{s.ManagedBytes},{s.AllocatedBytes},{s.Gen0},{s.Gen1},{s.Gen2}"));
        File.WriteAllText(Path.Combine(dir,"samples.csv"),csv.ToString());
        return new{passed=true,logicalProcessors=Environment.ProcessorCount,samplingIntervalMilliseconds=200,runs,outcomes,
            inputs="저장된 실제 글자·카드 자료. 오프라인 일정 데이터. 숨김/표시 일정 Tick을 1초 간격으로 구동.",
            limitations="실제 게임 캡처·GPU 프레임 영향·온라인 시세 요청을 포함하지 않음. 강제 GC·작업 집합 비우기 없음.",gameInput=false,screenCapture=false};
    }
}
