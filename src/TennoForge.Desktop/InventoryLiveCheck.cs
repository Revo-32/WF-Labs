using System.IO;
using System.Text.Json;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal static class InventoryLiveCheck
{
    internal static async Task Run(string dir)
    {
        using var reader=new InventoryMemoryReader();var catalog=InventoryCatalog.Load();var reports=new List<object>();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for(int i=0;i<6;i++)
        {
            var result=await reader.ReadAsync(catalog,stop.Token);reports.Add(new{result.Message,result.BytesRead,result.Seconds,result.Regions,result.Candidates,result.CompletePass,result.Diagnostic,items=result.Snapshot?.Quantities.Count,mastery=result.Snapshot?.MasteryRanks.Count});
            if(result.Snapshot is {} snapshot){InventoryImport.Save(Path.Combine(dir,"snapshot.json"),snapshot);break;}
            if(result.BytesRead==0||result.CompletePass)break;await Task.Delay(150,stop.Token);
        }
        File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(reports,Catalog.Json));
    }
}
