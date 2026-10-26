using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class OcrBenchmarkChecks
{
    private sealed record ExpectedOption(string Key,double Value,bool Negative=false);
    private sealed record Fixture(string Name,string Image,string Domain,ExpectedOption[]? Options=null,string[]? Slugs=null,bool Full=false,bool Live=false,int? MinimumAutomatic=null,string ReadKind="Melee",string? ExpectedKind=null,ExpectedOption[][]? CardOptions=null);
    internal static async Task Run(string manifest,string directory)
    {
        var fixtures=JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(manifest),Catalog.Json)!;
        Directory.CreateDirectory(directory);var reports=new List<object>();int passed=0,exactCount=0,totalWrongAutomatic=0;
        foreach(var fixture in fixtures)
        {
            var image=RelicOcr.Load(Path.GetFullPath(fixture.Image));var timer=Stopwatch.StartNew();bool exact;object details;int correctAutomatic=0,wrongAutomatic=0;
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
                    bool Match(RivenOcrCard card,int index)=>index<expectedCards.Length&&card.Corroborated&&card.Report.Valid&&(fixture.ExpectedKind is null||card.Report.Kind==fixture.ExpectedKind)&&card.Report.Options.Select(o=>new ExpectedOption(o.Key,o.Value,o.Negative)).SequenceEqual(expectedCards[index]);
                    exact=result.Cards.Length==expectedCards.Length&&result.Cards.Select((card,index)=>Match(card,index)).All(ok=>ok);
                    for(int index=0;index<result.Cards.Length;index++)
                        if(result.Cards[index].Corroborated&&result.Cards[index].Report.Valid){if(Match(result.Cards[index],index))correctAutomatic++;else wrongAutomatic++;}
                    details=result;
                }
            }
            else
            {
                var result=await RelicOcr.RecognizeAsync(image,RelicCatalog.Bundled(),default,live:fixture.Live);
                var actual=result.Rewards.Select(r=>r.Item is {} item?item.Tradable?item.Slug:item.Id:"?").ToArray();
                var expected=fixture.Slugs??[];exact=actual.SequenceEqual(expected);details=result;
                for(int i=0;i<actual.Length;i++)if(actual[i]!="?"){if(i<expected.Length&&actual[i]==expected[i])correctAutomatic++;else wrongAutomatic++;}
            }
            bool contractPassed=exact||fixture.MinimumAutomatic is {} minimum&&fixture.Domain=="relic"&&wrongAutomatic==0&&correctAutomatic>=minimum;
            if(contractPassed)passed++;if(exact)exactCount++;totalWrongAutomatic+=wrongAutomatic;
            reports.Add(new{fixture.Name,fixture.Domain,fixture.Image,contractPassed,exact,correctAutomatic,wrongAutomatic,seconds=timer.Elapsed.TotalSeconds,details});
            File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{model=OcrModels.Korean,passed,exact=exactCount,wrongAutomatic=totalWrongAutomatic,total=fixtures.Length,results=reports},Catalog.Json));
        }
    }
}
