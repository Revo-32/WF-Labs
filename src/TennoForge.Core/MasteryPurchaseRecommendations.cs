namespace TennoForge.Core;

public static class MasteryPurchaseRecommendations
{
    public static double? Cost(MasteryPlan plan,MasteryAcquisitionGroup? group)
    {
        if(group==MasteryAcquisitionGroup.Trade)return Valid(plan.BuyPlatinum);
        if(group!=MasteryAcquisitionGroup.Platinum)return null;
        var entry=InventoryAcquisition.Find(plan.Item.Id);
        return Valid(entry?.BuiltPlatinum??(double?)entry?.Routes.Where(r=>r.Kind=="market").SelectMany(r=>r.Costs).FirstOrDefault(c=>c.Currency=="플래티넘"&&c.Amount>0)?.Amount);
    }
    private static double? Valid(double? value)=>value is {} v&&double.IsFinite(v)&&v>=0?v:null;
    public static double? Per1000(MasteryPlan plan,MasteryAcquisitionGroup? group)=>plan.AvailablePoints>0&&Cost(plan,group) is {} cost?cost*1000/plan.AvailablePoints:null;
    public static IEnumerable<MasteryPlan> Order(IEnumerable<MasteryPlan> plans,MasteryAcquisitionGroup? group)
    {
        // Preserve the established owned/credit/farming order outside purchase
        // views. Never let the crafting acquisition bucket override purchase cost.
        if(group is not (MasteryAcquisitionGroup.Trade or MasteryAcquisitionGroup.Platinum))return plans;
        return plans.OrderBy(p=>p.AvailablePoints<=0).ThenBy(p=>p.RankLocked)
            .ThenBy(p=>Per1000(p,group)??double.PositiveInfinity)
            .ThenBy(p=>Cost(p,group)??double.PositiveInfinity)
            .ThenBy(p=>p.PricesStale).ThenBy(p=>p.MissingAcquisitions)
            .ThenBy(p=>p.CreditsPer1000Mastery)
            // Foundry jobs run concurrently: time only breaks otherwise equal choices.
            .ThenBy(p=>p.BuildSeconds/(double)Math.Max(1,p.AvailablePoints))
            .ThenBy(p=>p.Item.Name,StringComparer.Ordinal).ThenBy(p=>p.Item.Id,StringComparer.Ordinal);
    }
}
