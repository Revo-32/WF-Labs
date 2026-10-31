namespace TennoForge.Core;

public sealed record MasteryMaterial(string Id,string Name,string EnglishName,long Required,long Owned,long InFoundry,long Missing,string[] UsedBy)
{
    public long ProtectedStock {get;init;}
    public int PreparationPriority {get;init;}
    public string PreparationGroup {get;init;}="";
    public string PreparationReason {get;init;}="";
    public string FoundryText=>string.Join("\n",new[]{InFoundry>0?$"제작 중 {InFoundry:N0}":"",ProtectedStock>0?$"장비 보존 {ProtectedStock:N0}":""}.Where(s=>s.Length>0));
}
public sealed record MasteryMaterialPlan(int RemainingCraftable,int AlreadyAvailable,int CraftJobs,long Credits,long BlueprintCredits,
    MasteryMaterial[] Materials,MasteryMaterial[] Blueprints,string[] Warnings,bool MasteryKnown,bool InventoryKnown)
{
    public long TotalCredits=>checked(Credits+BlueprintCredits);
}

public static class MasteryMaterials
{
    // This is a forecast for making every unfinished, fixed-recipe item once.
    // A target weapon consumed by another recipe can be levelled first; it is
    // not counted as an extra permanent copy. Resource crafting stays at the
    // finished-resource level (Fieldron, Forma...), rather than hiding those
    // quantities inside their own crafting ingredients.
    public static MasteryMaterialPlan Calculate(InventoryCatalog catalog,InventorySnapshot inventory,bool consumeOwnedEquipment=false)
    {
        if(!inventory.MasteryComplete)return new(0,0,0,0,0,[],[],["마스터리 이력을 동기화하면 남은 제작 장비의 재료를 계산합니다."],false,inventory.InventoryComplete);
        var items=catalog.Items.ToDictionary(i=>i.Id);var warnings=new List<string>();
        var recipes=catalog.Recipes.Where(r=>r.Quantity>0&&r.Ingredients.All(i=>i.Count>0)).GroupBy(r=>r.ResultId).ToDictionary(g=>g.Key,g=>g.First());
        var byBlueprint=catalog.Recipes.Where(r=>r.Quantity>0).GroupBy(r=>r.BlueprintId).ToDictionary(g=>g.Key,g=>g.First());
        var targets=catalog.Items.Where(i=>i.Masterable&&i.MaxRank>0&&inventory.MasteryRanks.GetValueOrDefault(i.Id)<i.MaxRank&&recipes.ContainsKey(i.Id)).ToArray();
        var targetIds=targets.Select(i=>i.Id).ToHashSet();
        var incoming=new Dictionary<string,long>();
        foreach(var job in inventory.Crafting)if(byBlueprint.TryGetValue(job.BlueprintId,out var r))incoming[r.ResultId]=checked(incoming.GetValueOrDefault(r.ResultId)+r.Quantity);
        bool Owned(string id)=>inventory.Count(id)>0||inventory.Equipment.Any(e=>e.Id==id);
        bool Available(string id)=>Owned(id)||incoming.GetValueOrDefault(id)>0;
        bool Expand(string id)=>recipes.ContainsKey(id)&&(!items.TryGetValue(id,out var item)||!MaterialPreparation.IsFinishedResource(item));
        var graph=new Dictionary<string,string[]>();var pending=new Queue<string>(targetIds);
        while(pending.TryDequeue(out var id))
        {
            if(graph.ContainsKey(id))continue;
            var children=Expand(id)?recipes[id].Ingredients.Select(i=>i.Id).Distinct().ToArray():[];graph[id]=children;
            foreach(var child in children)pending.Enqueue(child);
        }
        var parents=graph.Keys.ToDictionary(id=>id,_=>0);
        foreach(var children in graph.Values)foreach(var child in children)parents[child]++;
        var ready=new Queue<string>(parents.Where(p=>p.Value==0).Select(p=>p.Key));var demand=new Dictionary<string,long>();
        var owners=graph.Keys.ToDictionary(id=>id,id=>targetIds.Contains(id)?new HashSet<string>{id}:new HashSet<string>());
        var materials=new Dictionary<string,long>();var blueprints=new Dictionary<string,long>();
        long credits=0;int jobs=0,visited=0;
        while(ready.TryDequeue(out var id))
        {
            visited++;long need=demand.GetValueOrDefault(id),stock=inventory.Count(id),queued=incoming.GetValueOrDefault(id);
            bool equipment=items.GetValueOrDefault(id)?.Masterable==true;
            if(equipment)stock=Math.Max(stock,inventory.Equipment.LongCount(e=>e.Id==id));
            if(equipment&&!consumeOwnedEquipment){stock=0;queued=0;}
            long missing=Math.Max(0,need-stock-queued);
            if(targetIds.Contains(id)&&!Available(id))missing=Math.Max(1,missing);
            if(Expand(id)&&missing>0)
            {
                var recipe=recipes[id];long batches=checked((missing+recipe.Quantity-1)/recipe.Quantity);
                credits=checked(credits+recipe.Credits*batches);jobs=checked(jobs+(int)batches);
                blueprints[recipe.BlueprintId]=recipe.ConsumeBlueprint?checked(blueprints.GetValueOrDefault(recipe.BlueprintId)+batches):Math.Max(1,blueprints.GetValueOrDefault(recipe.BlueprintId));
                foreach(var ingredient in recipe.Ingredients)
                    demand[ingredient.Id]=checked(demand.GetValueOrDefault(ingredient.Id)+ingredient.Count*batches);
            }
            else if(!Expand(id)&&need>0)materials[id]=need;
            foreach(var child in graph[id])
            {owners[child].UnionWith(owners[id]);if(--parents[child]==0)ready.Enqueue(child);}
        }
        if(visited!=graph.Count)warnings.Add("순환 제작식이 있는 일부 항목은 합산하지 못했습니다: "+string.Join(" · ",parents.Where(p=>p.Value>0).Select(p=>catalog.Name(p.Key)).Distinct().Take(12)));
        // Max-rank expansion is a levelling expense, separate from a recipe's
        // own Forma ingredient. It is needed only once per mastery target.
        var forma=catalog.Items.FirstOrDefault(i=>i.EnglishName=="Forma");
        if(forma is not null)
        {
            foreach(var item in targets.Where(i=>i.MaxRank>30))
            {
                int used=inventory.Equipment.Where(e=>e.Id==item.Id).Select(e=>e.Forma).DefaultIfEmpty(0).Max();
                int extra=Math.Max(0,(item.MaxRank-30+1)/2-used);
                if(extra==0)continue;materials[forma.Id]=checked(materials.GetValueOrDefault(forma.Id)+extra);
                if(!owners.TryGetValue(forma.Id,out var set))owners[forma.Id]=set=[];set.Add(item.Id);
            }
        }
        MasteryMaterial Row(KeyValuePair<string,long> p)
        {
            var item=items.GetValueOrDefault(p.Key);long owned=inventory.Count(p.Key),queued=incoming.GetValueOrDefault(p.Key);
            if(item?.Masterable==true)owned=Math.Max(owned,inventory.Equipment.LongCount(e=>e.Id==p.Key));
            long protectedStock=item?.Masterable==true&&!consumeOwnedEquipment?checked(owned+queued):0;
            var preparation=MaterialPreparation.For(item,recipes.ContainsKey(p.Key));
            return new(p.Key,item?.Name??"이름 확인 필요",item?.EnglishName??"",p.Value,owned,queued,Math.Max(0,p.Value-owned-queued+protectedStock),
                owners.GetValueOrDefault(p.Key)?.Select(catalog.Name).Distinct().OrderBy(n=>n,StringComparer.Ordinal).ToArray()??[])
                {ProtectedStock=protectedStock,PreparationPriority=preparation.Priority,PreparationGroup=preparation.Group,PreparationReason=preparation.Reason};
        }
        var rows=MaterialPreparation.Sort(materials.Select(Row));
        var bp=blueprints.Select(Row).OrderByDescending(r=>r.Missing>0).ThenBy(r=>r.Name,StringComparer.Ordinal).ToArray();
        long blueprintCredits=0;
        foreach(var row in bp)
        {
            var result=byBlueprint.TryGetValue(row.Id,out var recipe)?items.GetValueOrDefault(recipe.ResultId):null;
            if((items.GetValueOrDefault(row.Id)?.BlueprintCredits??result?.BlueprintCredits) is {} cost)blueprintCredits=checked(blueprintCredits+cost*row.Missing);
        }
        if(!inventory.InventoryComplete)warnings.Add("보유 목록이 일부만 확인되어 부족분은 잠정 수량입니다.");
        int alternatives=catalog.Recipes.GroupBy(r=>r.ResultId).Count(g=>graph.ContainsKey(g.Key)&&g.Count()>1);
        if(alternatives>0)warnings.Add($"대체 제작식 {alternatives}개 항목은 자료의 기본 제작식을 적용했습니다.");
        if(targets.Any(i=>i.MaxRank>30))warnings.Add("최대 랭크 40 장비의 추가 랭작 포르마를 포함합니다.");
        return new(targets.Length,targets.Count(i=>Available(i.Id)),jobs,credits,blueprintCredits,rows,bp,warnings.ToArray(),true,inventory.InventoryComplete);
    }
}
