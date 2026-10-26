namespace TennoForge.Core;

public sealed record MasteryGroup(string Key,string Name,int Total,int? Completed,int? InProgress);
public sealed record MasteryOverview(int Total,int? Completed,int? InProgress,int? Unstarted,int? Owned,MasteryGroup[] Groups)
{
    public int? Remaining=>Completed is {} done?Total-done:null;
    public double? Completion=>Completed is {} done&&Total>0?done/(double)Total:null;
    public static string GroupKey(InventoryItem i)=>i.Category switch{"Warframes"=>"Warframes","Primary"=>"Primary","Secondary"=>"Secondary","Melee"=>"Melee","Pets" or "Sentinels"=>"Companions",_=>"Other"};
    public static MasteryOverview From(InventoryCatalog catalog,InventorySnapshot snapshot)
    {
        var items=catalog.Items.Where(i=>i.Masterable).ToArray();bool known=snapshot.MasteryComplete;
        int Done(IEnumerable<InventoryItem> rows)=>rows.Count(i=>snapshot.MasteryRanks.GetValueOrDefault(i.Id)>=i.MaxRank);
        int Progress(IEnumerable<InventoryItem> rows)=>rows.Count(i=>snapshot.MasteryRanks.GetValueOrDefault(i.Id)>0&&snapshot.MasteryRanks.GetValueOrDefault(i.Id)<i.MaxRank);
        var names=new[]{("Warframes","워프레임"),("Primary","주무기"),("Secondary","보조무기"),("Melee","근접"),("Companions","동반자"),("Other","기타 장비")};
        var groups=names.Select(n=>{var rows=items.Where(i=>GroupKey(i)==n.Item1).ToArray();return new MasteryGroup(n.Item1,n.Item2,rows.Length,known?Done(rows):null,known?Progress(rows):null);}).ToArray();
        return new(items.Length,known?Done(items):null,known?Progress(items):null,known?items.Length-Done(items)-Progress(items):null,snapshot.InventoryComplete?items.Count(i=>snapshot.Count(i.Id)>0):null,groups);
    }
}

public sealed record RelicValueChance(double Probability,int KnownRewards,int TotalRewards)
{
    public bool Exact=>KnownRewards==TotalRewards;
    public static RelicValueChance Evaluate(InventoryCatalog catalog,InventoryRelic relic,IReadOnlyDictionary<string,PlannerPrice> prices,double threshold,int squad)
        =>Evaluate(catalog.Items.ToDictionary(i=>i.Id),relic,prices,threshold,squad);
    public static RelicValueChance Evaluate(IReadOnlyDictionary<string,InventoryItem> items,InventoryRelic relic,IReadOnlyDictionary<string,PlannerPrice> prices,double threshold,int squad)
    {
        if(threshold<=0||!double.IsFinite(threshold)||squad is <1 or >4)throw new ArgumentOutOfRangeException();
        int known=0;double chance=0;
        foreach(var reward in relic.Rewards)
        {
            double? value=prices.TryGetValue(reward.Id,out var price)&&double.IsFinite(price.Platinum)&&price.Platinum>=0?price.Platinum:items.GetValueOrDefault(reward.Id)?.Tradable==false?0:null;
            if(value is null)continue;known++;if(value*reward.Count>=threshold)chance+=InventoryRelicPlanner.Probability(reward.Rarity,relic.Refinement);
        }
        return new(1-Math.Pow(1-Math.Clamp(chance,0,1),squad),known,relic.Rewards.Length);
    }
}
