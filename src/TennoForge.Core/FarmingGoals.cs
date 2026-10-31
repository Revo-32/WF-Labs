using System.Text.Json;

namespace TennoForge.Core;

public sealed record FarmingGoal(string ItemId,int Quantity=1,bool Enabled=true,bool Notify=true);
public sealed record FarmingGoalsDocument(int SchemaVersion,FarmingGoal[] Goals)
{
    public static FarmingGoalsDocument Empty=>new(1,[]);
    public void Validate(InventoryCatalog catalog)
    {
        if(SchemaVersion!=1||Goals is null||Goals.Length>50)throw new ArgumentException("목표 파일 형식을 확인하세요. 목표는 최대 50개입니다.");
        var allowed=catalog.Items.Select(i=>i.Id).ToHashSet(StringComparer.Ordinal);
        if(Goals.Any(g=>g is null||!allowed.Contains(g.ItemId)||g.Quantity is <1 or >99)||Goals.Select(g=>g.ItemId).Distinct().Count()!=Goals.Length)
            throw new ArgumentException("목표 품목·수량을 확인하세요. 같은 장비는 한 목표에서 수량을 조정합니다.");
    }
}
public sealed record FarmingRequirement(string ItemId,long Needed,long Reserved,long Missing,long Crafting=0,long Planned=0,bool Reusable=false,bool CreditPurchase=false,bool ConsumesUnmastered=false);
public sealed record FarmingGoalPlan(FarmingGoal Goal,long Owned,long Crafting,FarmingRequirement[] Requirements,long Credits,long ReservedCredits,string[] Warnings)
{
    public bool Secured=>Owned>=Goal.Quantity;
    public bool Waiting=>Crafting>0||Requirements.Any(r=>r.Crafting>0||r.Planned>0);
    public bool MaterialsReady=>Requirements.All(r=>r.Missing==0)&&ReservedCredits==Credits&&!Waiting;
}
public sealed record FarmingPlan(FarmingGoalPlan[] Goals,Dictionary<string,long> Reserved,Dictionary<string,long> Missing,long Credits,long ReservedCredits,bool InventoryConfirmed)
{
    public IReadOnlySet<string> MissingItems=>Missing.Where(x=>x.Value>0).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
    public long Free(InventorySnapshot inventory,string id)=>Math.Max(0,inventory.Count(id)-Reserved.GetValueOrDefault(id));
}

public static class FarmingPlanner
{
    // Reservations are a projection of the latest inventory. Never subtract them
    // from the real snapshot or persist a second copy of item quantities.
    public static FarmingPlan Evaluate(InventoryCatalog catalog,InventorySnapshot inventory,FarmingGoalsDocument document)
    {
        document.Validate(catalog);
        var items=catalog.Items.ToDictionary(i=>i.Id,StringComparer.Ordinal);
        var recipes=catalog.Recipes.GroupBy(r=>r.ResultId).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        var byBlueprint=catalog.Recipes.GroupBy(r=>r.BlueprintId).ToDictionary(g=>g.Key,g=>g.First());
        var queued=new Dictionary<string,long>();
        foreach(var job in inventory.Crafting)if(byBlueprint.TryGetValue(job.BlueprintId,out var recipe))queued[recipe.ResultId]=checked(queued.GetValueOrDefault(recipe.ResultId)+recipe.Quantity);
        var stock=new Dictionary<string,long>(inventory.Quantities,StringComparer.Ordinal);
        var reserved=new Dictionary<string,long>(StringComparer.Ordinal);
        var reusableOwned=new HashSet<string>(StringComparer.Ordinal);var reusableRequested=new HashSet<string>(StringComparer.Ordinal);
        var planned=new Dictionary<string,long>(StringComparer.Ordinal);
        var result=new List<FarmingGoalPlan>();long creditsLeft=inventory.Credits;
        foreach(var goal in document.Goals.Where(g=>g.Enabled))
        {
            var needs=new Dictionary<string,FarmingRequirement>(StringComparer.Ordinal);var warnings=new HashSet<string>(StringComparer.Ordinal);var active=new HashSet<string>(StringComparer.Ordinal);long credits=0;
            if(!inventory.InventoryComplete)warnings.Add("보유 목록 미확인·일부 확인 · 부족 수량과 판매 여유는 확정하지 않습니다.");
            void Add(FarmingRequirement r)
            {
                if(needs.TryGetValue(r.ItemId,out var old))
                    r=r.Reusable?r with{Needed=Math.Max(old.Needed,r.Needed),Reserved=Math.Max(old.Reserved,r.Reserved),Missing=Math.Max(old.Missing,r.Missing),Crafting=Math.Max(old.Crafting,r.Crafting),Planned=Math.Max(old.Planned,r.Planned)}
                        :r with{Needed=checked(old.Needed+r.Needed),Reserved=checked(old.Reserved+r.Reserved),Missing=checked(old.Missing+r.Missing),Crafting=checked(old.Crafting+r.Crafting),Planned=checked(old.Planned+r.Planned),ConsumesUnmastered=old.ConsumesUnmastered||r.ConsumesUnmastered};
                needs[r.ItemId]=r;
            }
            long TakeStock(string id,long quantity)
            {
                long used=Math.Min(stock.GetValueOrDefault(id),quantity);stock[id]=stock.GetValueOrDefault(id)-used;
                reserved[id]=checked(reserved.GetValueOrDefault(id)+used);return used;
            }
            void Blueprint(InventoryRecipe recipe,long batches)
            {
                string id=recipe.BlueprintId;long quantity=recipe.ConsumeBlueprint?batches:1;
                long used=!recipe.ConsumeBlueprint&&reusableOwned.Contains(id)?1:TakeStock(id,quantity);
                if(!recipe.ConsumeBlueprint&&used>0)reusableOwned.Add(id);
                long missing=quantity-used;var price=items.GetValueOrDefault(id)?.BlueprintCredits;
                if(missing>0&&price is >0&&(recipe.ConsumeBlueprint||reusableRequested.Add(id)))credits=checked(credits+price.Value*missing);
                Add(new(id,quantity,used,missing,Reusable:!recipe.ConsumeBlueprint,CreditPurchase:price is >0));
            }
            (long Owned,long Crafting) Need(string id,long quantity,bool root=false)
            {
                if(quantity<=0)return (0,0);
                long used=TakeStock(id,quantity),left=quantity-used;
                bool consumes=!root&&used>0&&items.GetValueOrDefault(id) is {Masterable:true} item&&(!inventory.MasteryComplete||inventory.MasteryRanks.GetValueOrDefault(id)<item.MaxRank);
                if(consumes)warnings.Add(catalog.Name(id)+" 소모 전 숙련 완료 여부를 확인하세요.");
                long pending=Math.Min(left,queued.GetValueOrDefault(id));queued[id]=queued.GetValueOrDefault(id)-pending;left-=pending;
                long shared=Math.Min(left,planned.GetValueOrDefault(id));planned[id]=planned.GetValueOrDefault(id)-shared;left-=shared;
                if(used+pending+shared>0)Add(new(id,used+pending+shared,used,0,pending,shared,ConsumesUnmastered:consumes));
                if(left==0)return(used,pending);
                var direct=InventoryAcquisition.DirectCredits(id);
                if(direct is not null)
                {credits=checked(credits+left*direct.Costs[0].Amount);Add(new(id,left,0,left,CreditPurchase:true));return(used,pending);}
                bool resource=items.GetValueOrDefault(id) is {Masterable:false,Category:"Resources" or "Misc"};
                if(recipes.TryGetValue(id,out var recipe)&&(!resource||stock.GetValueOrDefault(recipe.BlueprintId)>0||reusableOwned.Contains(recipe.BlueprintId))&&active.Add(id))
                {
                    if(recipe.Quantity<=0||recipe.Credits<0||recipe.Ingredients.Any(i=>i.Count<=0))throw new ArgumentException("제작식 수량·비용을 확인하세요.");
                    long batches=checked((left+recipe.Quantity-1)/recipe.Quantity);credits=checked(credits+recipe.Credits*batches);Blueprint(recipe,batches);
                    foreach(var ingredient in recipe.Ingredients)Need(ingredient.Id,checked(ingredient.Count*batches));
                    active.Remove(id);long excess=checked(batches*recipe.Quantity-left);if(excess>0)planned[id]=checked(planned.GetValueOrDefault(id)+excess);
                }
                else {Add(new(id,left,0,left));if(active.Contains(id))warnings.Add(catalog.Name(id)+" 제작 경로 순환 · 획득처 확인 필요");}
                return(used,pending);
            }
            var target=Need(goal.ItemId,goal.Quantity,true);long creditReservation=Math.Min(creditsLeft,credits);creditsLeft-=creditReservation;
            if(creditReservation<credits)warnings.Add($"크레딧 {credits-creditReservation:N0} 부족 · 앞선 목표 예약 우선");
            result.Add(new(goal,target.Owned,target.Crafting,needs.Values.ToArray(),credits,creditReservation,warnings.ToArray()));
        }
        var missing=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var group in result.SelectMany(g=>g.Requirements).Where(r=>r.Missing>0).GroupBy(r=>r.ItemId))
            missing[group.Key]=group.Any(r=>r.Reusable)?group.Max(r=>r.Missing):group.Sum(r=>r.Missing);
        return new(result.ToArray(),reserved,missing,result.Sum(g=>g.Credits),inventory.Credits-creditsLeft,inventory.InventoryComplete);
    }
}

public sealed record FarmingRewardHint(string Text,string Detail,long? Needed,bool Highlight);
public static class FarmingRewards
{
    public static FarmingRewardHint? Hint(InventoryCatalog catalog,InventorySnapshot inventory,FarmingPlan plan,string gameId)
    {
        var goals=plan.Goals.Where(g=>g.Requirements.Any(r=>r.ItemId==gameId&&r.Missing>0)).ToArray();
        if(goals.Length>0)
        {
            string names=string.Join(" · ",goals.Select(g=>catalog.Name(g.Goal.ItemId)).Distinct());
            long needed=plan.Missing.GetValueOrDefault(gameId);
            return new(plan.InventoryConfirmed?$"목표 부품 · {needed:N0}개 필요":"목표 관련 · 보유 확인 필요",names,plan.InventoryConfirmed?needed:null,true);
        }
        long reserved=plan.Reserved.GetValueOrDefault(gameId),have=inventory.Count(gameId);
        if(reserved>0)return new($"목표 예약 {reserved:N0}개",$"현재 보유 {have:N0}개 · 예약 외 {plan.Free(inventory,gameId):N0}개",0,false);
        if(have>0)return new($"보유 {have:N0}개","선택 보상을 자동으로 재고에 추가하지 않습니다.",0,false);
        return null;
    }
}
