using System.IO;
using TennoForge.Core;

namespace TennoForge.Desktop;

// One shared goal/inventory projection for equipment, reward OCR and schedules.
// No timer, process reads, capture or network calls are created by this store.
internal sealed class FarmingWorkspace
{
    internal InventoryCatalog Catalog {get;}
    internal InventorySnapshot Inventory {get;private set;}=InventorySnapshot.Empty;
    internal FarmingGoalsDocument Document {get;private set;}=FarmingGoalsDocument.Empty;
    internal FarmingPlan Plan {get;private set;}
    internal string Notice {get;private set;}="";
    internal event Action? Changed;
    private readonly string? path;
    internal FarmingWorkspace(bool persist,InventoryCatalog? catalog=null,string? storage=null)
    {
        Catalog=catalog??InventoryCatalog.Load();
        path=storage??(persist?Path.Combine(WFLabs.Client.ClientPreferences.Folder,"planning","goals.json"):null);
        if(path is not null&&File.Exists(path))
        {
            try{var saved=AtomicJsonFile.Read<FarmingGoalsDocument>(path,256*1024);saved.Validate(Catalog);Document=saved;}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
                Notice="목표 파일 확인 필요 · 원본을 보존했습니다.";
                try{var backup=AtomicJsonFile.Read<FarmingGoalsDocument>(path+".bak",256*1024);backup.Validate(Catalog);Document=backup;Notice="목표 백업을 복원했습니다. 원본 파일은 보존했습니다.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException){}
            }
        }
        if(persist)
        {
            try{var p=Path.Combine(WFLabs.Client.ClientPreferences.Folder,"inventory","snapshot.json");if(File.Exists(p))Inventory=InventoryImport.Parse(File.ReadAllText(p),Catalog);}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException){Notice="보유 목록 확인 필요 · 목표는 유지합니다.";}
        }
        Plan=FarmingPlanner.Evaluate(Catalog,Inventory,Document);
    }
    internal void SetInventory(InventorySnapshot snapshot)
    {
        InventoryImport.Validate(snapshot);var plan=FarmingPlanner.Evaluate(Catalog,snapshot,Document);Inventory=snapshot;Plan=plan;Changed?.Invoke();
    }
    internal void Set(FarmingGoalsDocument document)
    {
        document.Validate(Catalog);var plan=FarmingPlanner.Evaluate(Catalog,Inventory,document);
        if(path is not null)
        {
            // Do not replace the last good backup with a known corrupt original.
            if(File.Exists(path))try{var old=AtomicJsonFile.Read<FarmingGoalsDocument>(path,256*1024);old.Validate(Catalog);}catch(Exception e)when(e is IOException or ArgumentException or System.Text.Json.JsonException)
            {File.Copy(path,path+".invalid-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"),false);File.Delete(path);}
            AtomicJsonFile.Write(path,document);
        }
        Document=document;Plan=plan;Notice="";Changed?.Invoke();
    }
    internal void Add(string id)
    {
        if(Document.Goals.Any(g=>g.ItemId==id))return;
        Set(Document with{Goals=Document.Goals.Append(new FarmingGoal(id)).ToArray()});
    }
    internal void Edit(FarmingGoal goal)=>Set(Document with{Goals=Document.Goals.Select(g=>g.ItemId==goal.ItemId?goal:g).ToArray()});
    internal void Remove(string id)=>Set(Document with{Goals=Document.Goals.Where(g=>g.ItemId!=id).ToArray()});
    internal void Move(string id,int delta)
    {
        var rows=Document.Goals.ToList();int index=rows.FindIndex(g=>g.ItemId==id),destination=index+delta;
        if(index<0||destination<0||destination>=rows.Count)return;(rows[index],rows[destination])=(rows[destination],rows[index]);Set(Document with{Goals=rows.ToArray()});
    }
    internal FarmingRewardHint? Reward(RelicItem? item)
    {
        if(item is null)return null;
        return FarmingMarket.GameIds(item.Id).Select(id=>FarmingRewards.Hint(Catalog,Inventory,Plan,id)).Where(h=>h is not null).OrderByDescending(h=>h!.Highlight).FirstOrDefault();
    }
}
