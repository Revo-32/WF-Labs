using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class OcrBenchmarkChecks
{
    private sealed record ExpectedOption(string Key,double Value,bool Negative=false);
    private sealed record Fixture(string Name,string Image,string Domain,ExpectedOption[]? Options=null,string[]? Slugs=null,bool Full=false,bool Live=false,int? MinimumAutomatic=null,string ReadKind="Melee",string? ExpectedKind=null,ExpectedOption[][]? CardOptions=null,bool Diagnostics=false,bool TranscriptionOnly=false);
    internal static async Task Run(string manifest,string directory)
    {
        var fixtures=JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(manifest),Catalog.Json)!;
        Directory.CreateDirectory(directory);var reports=new List<object>();int passed=0,exactCount=0,totalWrongAutomatic=0;
        foreach(var fixture in fixtures)
        {
            using var diagnostic=fixture.Diagnostics&&fixture.Domain=="riven"?new RivenImages(Path.Combine(directory,string.Concat(fixture.Name.Where(c=>char.IsLetterOrDigit(c)||c=='-').Take(96)))):null;
            var image=RelicOcr.Load(Path.GetFullPath(fixture.Image));var timer=Stopwatch.StartNew();bool exact;bool transcriptionExact=false,automaticExact=false;object details;int correctAutomatic=0,wrongAutomatic=0;
            if(fixture.Domain=="riven")
            {
                var probe=RivenVisualProbe.Analyze(image);
                if(fixture.Options is {Length:0})
                {
                    var anchors=RivenFooterAnchor.Find(image);
                    exact=probe.Regions.Length==0&&anchors.Length==0;details=new{probe.Regions,anchors};
                }
                else
                {
                    var result=fixture.Full?await RivenOcr.ReadAsync(image,fixture.ReadKind,8,default):await RivenOcr.ReadAutomaticAsync(image,probe.Regions,fixture.ReadKind,8,default);
                    var expectedCards=fixture.CardOptions??new[]{fixture.Options??[]};
                    bool Match(RivenOcrCard card,int index)=>index<expectedCards.Length&&(fixture.ExpectedKind is null||card.Report.Kind==fixture.ExpectedKind)&&card.Report.Options.Select(o=>new ExpectedOption(o.Key,o.Value,o.Negative)).SequenceEqual(expectedCards[index]);
                    transcriptionExact=result.Cards.Length==expectedCards.Length&&result.Cards.Select((card,index)=>Match(card,index)).All(ok=>ok);
                    automaticExact=transcriptionExact&&result.Cards.All(c=>c.Corroborated&&c.Report.Valid);
                    // Historical cards can have a different disposition. Test
                    // their exact transcription separately; do not label it as
                    // a currently valid or automatically confirmed game roll.
                    exact=fixture.TranscriptionOnly?transcriptionExact:automaticExact;
                    for(int index=0;index<result.Cards.Length;index++)
                        if(result.Cards[index].Corroborated&&result.Cards[index].Report.Valid){if(Match(result.Cards[index],index))correctAutomatic++;else wrongAutomatic++;}
                    details=result;
                }
            }
            else
            {
                var trace=fixture.Diagnostics?Path.Combine(directory,string.Concat(fixture.Name.Select(c=>char.IsLetterOrDigit(c)?c:'_').Take(96))):null;
                if(trace is not null)Directory.CreateDirectory(trace);
                var result=await RelicOcr.RecognizeAsync(image,RelicCatalog.Bundled(),default,diagnosticsDirectory:trace,live:fixture.Live);
                var actual=result.Rewards.Select(r=>r.Item is {} item?item.Tradable?item.Slug:item.Id:"?").ToArray();
                var expected=fixture.Slugs??[];exact=actual.SequenceEqual(expected);details=result;
                for(int i=0;i<actual.Length;i++)if(actual[i]!="?"){if(i<expected.Length&&actual[i]==expected[i])correctAutomatic++;else wrongAutomatic++;}
            }
            bool contractPassed=exact||fixture.MinimumAutomatic is {} minimum&&fixture.Domain=="relic"&&wrongAutomatic==0&&correctAutomatic>=minimum;
            if(contractPassed)passed++;if(exact)exactCount++;totalWrongAutomatic+=wrongAutomatic;
            reports.Add(new{fixture.Name,fixture.Domain,fixture.Image,fixture.TranscriptionOnly,contractPassed,exact,transcriptionExact,automaticExact,correctAutomatic,wrongAutomatic,seconds=timer.Elapsed.TotalSeconds,details});
            File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{model=OcrModels.Korean,passed,exact=exactCount,wrongAutomatic=totalWrongAutomatic,total=fixtures.Length,results=reports},Catalog.Json));
        }
        if(passed!=fixtures.Length||totalWrongAutomatic!=0)throw new InvalidOperationException($"OCR 회귀 검사 실패: {passed}/{fixtures.Length} 통과 · 잘못된 자동 확정 {totalWrongAutomatic}개. results.json을 확인하세요.");
    }
    private sealed class RivenImages:IDisposable
    {
        private readonly Action<BitmapSource,string>? previous=RivenOcr.DiagnosticImage;
        internal RivenImages(string directory)
        {
            Directory.CreateDirectory(directory);int index=0;
            RivenOcr.DiagnosticImage=(bitmap,name)=>
            {
                var safe=string.Concat(name.Select(c=>char.IsLetterOrDigit(c)||c=='-'?c:'_'));
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(directory,$"{Interlocked.Increment(ref index):000}-{safe}.png"));encoder.Save(stream);
            };
        }
        public void Dispose()=>RivenOcr.DiagnosticImage=previous;
    }
}
