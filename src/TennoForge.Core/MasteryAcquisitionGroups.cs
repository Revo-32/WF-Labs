namespace TennoForge.Core;

public enum MasteryAcquisitionGroup { Owned, Credits, Farming, Platinum, Trade, Other }
public sealed record MasteryAcquisitionInfo(MasteryAcquisitionGroup Group,string Label,bool Shop,bool Dojo,bool CanBuyPlatinum,bool CanTrade);

// Group by the equipment/blueprint acquisition route, not by ordinary resource
// shortages or the existence of an optional premium shortcut.
public sealed class MasteryAcquisitionGroups(InventoryCatalog catalog)
{
    private readonly Dictionary<string,InventoryItem> items=catalog.Items.ToDictionary(i=>i.Id);
    private readonly Dictionary<string,InventoryRecipe> recipes=catalog.Recipes.GroupBy(r=>r.ResultId).ToDictionary(g=>g.Key,g=>g.First());
    private readonly Dictionary<string,MasteryAcquisitionInfo> cache=new();
    public MasteryAcquisitionInfo For(MasteryPlan plan)
    {
        var info=For(plan.Item.Id);
        return info with{Group=plan.Owned||plan.InFoundry?MasteryAcquisitionGroup.Owned:info.Group,
            Label=plan.Owned?"보유 · 랭작 가능":plan.InFoundry?"주조소 제작 중":info.Label};
    }
    public MasteryAcquisitionInfo For(string id)
    {
        if(cache.TryGetValue(id,out var saved))return saved;
        bool shop=false,dojo=false,farm=false,unknown=false;
        var visited=new HashSet<string>();
        void Visit(string target,bool root=false)
        {
            if(!visited.Add(target))return;
            var item=items.GetValueOrDefault(target);
            if(!root&&item is {Masterable:false,Category:"Resources" or "Misc"}&&!recipes.ContainsKey(target))return;
            var entry=InventoryAcquisition.Find(target);
            var routes=entry?.Routes??[];
            if(routes.Length==0&&recipes.TryGetValue(target,out var sourceRecipe))routes=InventoryAcquisition.Find(sourceRecipe.BlueprintId)?.Routes??[];
            bool creditShop=routes.Any(r=>r.Kind=="market"&&r.Costs.Any(c=>c.Currency=="크레딧"&&c.Amount>0));
            bool creditDojo=routes.Any(r=>r.Kind=="dojo"&&(r.Costs.Length==0||r.Costs.Any(c=>c.Currency=="크레딧"&&c.Amount>0)));
            shop|=creditShop;dojo|=creditDojo;
            if(!creditShop&&!creditDojo)
            {
                if(routes.Any(r=>r.Kind is "drop" or "relic" or "quest" or "vendor" or "adversary" or "event" or "race")||!root&&item?.Category=="Components"&&routes.Any(r=>r.Kind=="other"))farm=true;
                else if(routes.Length==0||routes.All(r=>r.Kind is "other" or "unavailable" or "internal" or "bundled" or "companion"))unknown=true;
            }
            if(recipes.TryGetValue(target,out var recipe))
            {
                // Blueprint entries sometimes carry the precise route only on
                // their result item. Inspect the blueprint when that route is absent.
                if(!creditShop&&!creditDojo&&!farm&&InventoryAcquisition.Find(recipe.BlueprintId) is {Routes.Length:>0})Visit(recipe.BlueprintId);
                foreach(var part in recipe.Ingredients)
                {
                    var component=items.GetValueOrDefault(part.Id);
                    if(component is {Masterable:false,Category:"Resources" or "Misc"})continue;
                    if(recipes.ContainsKey(part.Id)||component is {Category:"Components" or "Warframe Parts" or "Weapon Parts"}||catalog.Relics.Any(r=>r.Rewards.Any(s=>s.Id==part.Id)))Visit(part.Id);
                }
            }
        }
        Visit(id,true);
        var rootEntry=InventoryAcquisition.Find(id);
        bool platinum=rootEntry?.BuiltPlatinum is >0||rootEntry?.Routes.Any(r=>r.Kind=="market"&&r.Costs.Any(c=>c.Currency=="플래티넘"&&c.Amount>0))==true;
        // The market catalog provides explicit game-ID links. Prices and resource
        // costs do not prove that the finished equipment is sold in the game shop.
        var tradeVisited=new HashSet<string>();
        bool TradeRoute(string target)
        {
            if(!tradeVisited.Add(target))return false;
            if(InventoryMarket.Find(target) is not null)return true;
            if(!recipes.TryGetValue(target,out var recipe))return false;
            if(InventoryMarket.Find(recipe.BlueprintId) is not null)return true;
            return recipe.Ingredients.Any(part=>items.TryGetValue(part.Id,out var component)&&component.Category is not ("Resources" or "Misc")&&TradeRoute(part.Id));
        }
        bool trade=TradeRoute(id);
        var group=farm?MasteryAcquisitionGroup.Farming:shop||dojo?MasteryAcquisitionGroup.Credits:platinum&&!unknown?MasteryAcquisitionGroup.Platinum:trade?MasteryAcquisitionGroup.Trade:MasteryAcquisitionGroup.Other;
        string label=group switch{MasteryAcquisitionGroup.Farming=>"설계도·부품 파밍"+(shop?" · 본체 설계도 상점":dojo?" · 도장 설계도 포함":""),MasteryAcquisitionGroup.Credits=>shop&&dojo?"상점·도장 설계도":dojo?"도장 · 연구 후 설계도 복제":InventoryAcquisition.DirectCredits(id) is not null?"상점 · 완성품 크레딧 구매":"상점 · 설계도 크레딧 구매",MasteryAcquisitionGroup.Platinum=>"상점 · 플래티넘 구매",MasteryAcquisitionGroup.Trade=>"유저 거래 · 장비·설계도·부품",_=>"특수 획득·조건 확인"};
        return cache[id]=new(group,label,shop,dojo,platinum,trade);
    }
}
