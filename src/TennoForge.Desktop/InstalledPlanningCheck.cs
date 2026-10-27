using System.IO;
using System.Text.Json;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class InstalledPlanningCheck
{
    internal static void Prepare(string reportFolder)
    {
        var folder=Path.Combine(reportFolder,"qa-profile");Directory.CreateDirectory(folder);
        var catalog=InventoryCatalog.Load();var item=catalog.Items.Single(i=>i.EnglishName=="Braton Prime");
        var recipe=catalog.Recipes.Single(r=>r.ResultId==item.Id);
        var snapshot=InventorySnapshot.Empty with{MasteryRank=30,MasteryComplete=true,InventoryComplete=true,Credits=1000000,
            Quantities=recipe.Ingredients.GroupBy(i=>i.Id).ToDictionary(g=>g.Key,g=>g.Sum(i=>i.Count))};
        var farming=new FarmingWorkspace(false,catalog,Path.Combine(folder,"goals.json"));farming.SetInventory(snapshot);farming.Add(item.Id);
        var preferences=new SchedulePreferences{OverlayEnabled=true,OverlayItems=["nextS","cetusCycle"],BackgroundOpacity=.37,TextShadow=true,X=.2,Y=.3,Width=560,Height=480,FissureView=1};
        File.WriteAllText(Path.Combine(folder,"inventory.json"),JsonSerializer.Serialize(snapshot,Catalog.Json));
        File.WriteAllText(Path.Combine(folder,"schedules.json"),JsonSerializer.Serialize(preferences,Catalog.Json));
        File.WriteAllText(Path.Combine(folder,"expected.json"),JsonSerializer.Serialize(new{farming.Document,farming.Plan.Reserved,farming.Plan.ReservedCredits,Preferences=preferences},Catalog.Json));
    }
    internal static bool Verify(string reportFolder)
    {
        var folder=Path.Combine(reportFolder,"qa-profile");if(!Directory.Exists(folder))return false; // Older public versions predate planning.
        var catalog=InventoryCatalog.Load();var snapshot=InventoryImport.Parse(File.ReadAllText(Path.Combine(folder,"inventory.json")),catalog);
        var farming=new FarmingWorkspace(false,catalog,Path.Combine(folder,"goals.json"));farming.SetInventory(snapshot);
        var preferences=JsonSerializer.Deserialize<SchedulePreferences>(File.ReadAllText(Path.Combine(folder,"schedules.json")),Catalog.Json)!.Sanitize();
        string actual=JsonSerializer.Serialize(new{farming.Document,farming.Plan.Reserved,farming.Plan.ReservedCredits,Preferences=preferences},Catalog.Json);
        if(actual!=File.ReadAllText(Path.Combine(folder,"expected.json")))throw new InvalidOperationException("업데이트 후 목표·예약·오버레이 설정 복구 불일치");
        return true;
    }
}
