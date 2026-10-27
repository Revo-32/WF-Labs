using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class RivenLatencyChecks
{
    internal static async Task Run(string directory)
    {
        var image=RelicOcr.Load(Path.Combine(directory,"input.png"));var prepare=Stopwatch.StartNew();await RivenOcr.PrepareAsync();prepare.Stop();
        var probe=RivenVisualProbe.Analyze(image);var warm=await RivenOcr.ReadAutomaticAsync(image,probe.Regions,"Auto",8,default);
        if(warm.Cards.Length!=2||warm.Cards.Any(c=>!c.Corroborated||!c.Report.Valid))throw new InvalidOperationException("실제 두 카드 교차 확인 실패");
        var expected=new[]{new[]{218.3,110.1,177.7,65.7},new[]{56.6,110.1,99.3,116.4}};
        for(int card=0;card<2;card++)if(!warm.Cards[card].Report.Options.Select(o=>o.Value).SequenceEqual(expected[card])||warm.Cards[card].Report.Options[^1].Negative!=true)throw new InvalidOperationException("첨부한 순환 화면의 정답 수치·부호와 불일치");
        var runs=new List<object>();var modes=new[]{true,false,false,true,true,false};
        for(int i=0;i<modes.Length;i++)
        {
            Environment.SetEnvironmentVariable("WF_LABS_RIVEN_SERIAL",modes[i]?"1":null);
            var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var timer=Stopwatch.StartNew();probe=RivenVisualProbe.Analyze(image);double probeMs=timer.Elapsed.TotalMilliseconds;
            var result=await RivenOcr.ReadAutomaticAsync(image,probe.Regions,"Auto",8,default);timer.Stop();
            if(result.Cards.Length!=2||result.Cards.Where((c,index)=>!c.Corroborated||!RivenOcr.Same(c.Report,warm.Cards[index].Report)).Any())throw new InvalidOperationException("순차·병렬 OCR 결과 불일치");
            File.WriteAllText(Path.Combine(directory,$"recognition-{i}.json"),JsonSerializer.Serialize(result,Catalog.Json));
            runs.Add(new{iteration=i,serial=modes[i],probeMs,ocrSeconds=result.Seconds,totalSeconds=timer.Elapsed.TotalSeconds,cpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,workingSetMiB=process.WorkingSet64/1048576d,cards=result.Cards.Length,verified=true});
        }
        Environment.SetEnvironmentVariable("WF_LABS_RIVEN_SERIAL",null);
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{passed=true,preparationSeconds=prepare.Elapsed.TotalSeconds,firstReadSeconds=warm.Seconds,runs},Catalog.Json));
    }
}
