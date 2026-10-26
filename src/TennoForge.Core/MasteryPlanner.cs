namespace TennoForge.Core;

public sealed record PlannerPrice(double Platinum,DateTimeOffset FetchedUtc,bool Stale);
public sealed record CraftRequirement(string Id,long Needed,long Owned,long Missing,double? BuyPlatinum,bool CreditBlueprint=false);
public sealed record MasteryCraftWork(string ResultId,int Seconds,int[] Dependencies);
public sealed record MasteryPlan(InventoryItem Item,int EarnedRank,int RemainingPoints,bool Owned,bool InFoundry,bool RankLocked,
    CraftRequirement[] Requirements,long Credits,int BuildSeconds,double? BuyPlatinum,double OpportunityPlatinum,bool PricesStale,string[] Warnings)
{
    public int AvailablePoints {get;init;}
    public int AdditionalForma {get;init;}
    public int AcquisitionOrder {get;init;}=2;
    public string AcquisitionLabel=>Owned?AvailablePoints>0?"보유 장비 랭작":"포르마 후 추가 랭크 해금":InFoundry?"주조소 제작 중":InventoryAcquisition.OrderLabel(AcquisitionOrder);
    public long? DirectPurchaseCredits {get;init;}
    public long BlueprintPurchaseCredits {get;init;}
    public double CreditsPer1000Mastery=>Credits*1000d/Math.Max(1,AvailablePoints);
    public int MissingAcquisitions=>Requirements.Where(r=>r.Missing>0&&!r.CreditBlueprint&&!(DirectPurchaseCredits.HasValue&&r.Id==Item.Id)).Select(r=>r.Id).Distinct().Count();
    public MasteryCraftWork[] CraftWork {get;init;}=[];
    public bool Ready=>!Owned&&!InFoundry&&!RankLocked&&Requirements.All(x=>x.Missing==0)&&Warnings.All(x=>!x.StartsWith("크레딧 부족")&&!x.StartsWith("보유 목록")&&!x.StartsWith("계정 마스터리"));
    public int Priority=>Owned?(AvailablePoints>0?0:7):InFoundry?1:RankLocked?6:2+AcquisitionOrder;
    public double? PlatinumPer1000Mastery=>BuyPlatinum is {} p&&AvailablePoints>0?p/AvailablePoints*1000:null;
}
public static class MasteryPlanner
{
    public static MasteryPlan[] Recommend(InventoryCatalog catalog,InventorySnapshot inventory,IReadOnlyDictionary<string,PlannerPrice>? prices=null)
    {
        prices??=new Dictionary<string,PlannerPrice>();
        if(!inventory.MasteryComplete)return []; // Missing history is unknown, never "unmastered".
        var items=catalog.Items.ToDictionary(x=>x.Id);var recipes=catalog.Recipes.GroupBy(x=>x.ResultId).ToDictionary(x=>x.Key,x=>x.First());var blueprintIds=catalog.Recipes.Select(r=>r.BlueprintId).ToHashSet();
        var plans=new List<MasteryPlan>();
        foreach(var item in catalog.Items.Where(x=>x.Masterable&&x.MaxRank>0))
        {
            int earned=inventory.MasteryRanks.GetValueOrDefault(item.Id);if(earned>=item.MaxRank)continue;
            bool owned=inventory.Count(item.Id)>0;var required=new List<CraftRequirement>();var warnings=new List<string>();var stock=new Dictionary<string,long>(inventory.Quantities);
            long credits=0,blueprintCredits=0;double opportunity=0;bool stale=false;var active=new HashSet<string>();var work=new List<MasteryCraftWork>();var available=new Dictionary<string,int>();
            var produced=new Dictionary<string,List<(long Count,int Job,int Time)>>();
            bool queued=recipes.TryGetValue(item.Id,out var recipe)&&inventory.Crafting.Any(j=>j.BlueprintId==recipe.BlueprintId);
            if(!inventory.InventoryComplete)warnings.Add("보유 목록 일부만 확인됨 · 제작 준비 여부 확인 필요");
            if(item.GildRequired)warnings.Add("도금 후 획득한 랭크만 마스터리에 반영 · 같은 핵심 부품은 중복 제외");
            if(item.MaxRank>30)warnings.Add("30 이후에는 포르마 1개당 최대 랭크 +2 · 40까지 최대 5개 필요");
            if(owned)
            {
                var best=inventory.Equipment.Where(e=>e.Id==item.Id).OrderByDescending(e=>e.Rank).FirstOrDefault();
                if(best is not null&&best.Rank<earned)warnings.Add($"현재 랭크 {best.Rank} · 과거 최고 {earned}까지는 추가 마스터리 없음");
                if(item.GildRequired&&best?.Gilded!=true)warnings.Add("현재 도금 여부 확인 필요");
            }
            (int Time,int? Job) Take(string id,long needed,bool reusable=false)
            {
                long have=stock.GetValueOrDefault(id),take=Math.Min(have,needed);if(!reusable)stock[id]=have-take;
                if(take>0&&prices.TryGetValue(id,out var quote)){opportunity+=take*quote.Platinum;stale|=quote.Stale;}
                if(take>0&&items.GetValueOrDefault(id)?.Masterable==true&&inventory.MasteryRanks.GetValueOrDefault(id)<items[id].MaxRank)warnings.Add(items[id].Name+" 소모 전 랭작 완료 권장");
                long missing=needed-take;
                int producedTime=0;int? producedJob=null;
                if(produced.TryGetValue(id,out var lots))for(int i=0;i<lots.Count&&missing>0;i++)
                {
                    var lot=lots[i];long used=Math.Min(missing,lot.Count);if(used==0)continue;missing-=used;lots[i]=lot with{Count=lot.Count-used};
                    if(lot.Time>=producedTime){producedTime=lot.Time;producedJob=lot.Job;}
                }
                // Output from a planned multi-item batch is not current inventory.
                // Keep its completion dependency and only reserve real stock.
                if(missing==0){if(take>0)required.Add(new(id,take,take,0,0));return (producedTime,producedJob);}
                // Internal resource recipes are not an instruction to buy an
                // unobtainable resource blueprint. Farm the missing material
                // unless the player actually owns its reusable blueprint.
                bool resource=items.GetValueOrDefault(id) is {Masterable:false,Category:"Resources" or "Misc"};
                if(recipes.TryGetValue(id,out var r)&&(!resource||stock.GetValueOrDefault(r.BlueprintId)>0)&&active.Add(id))
                {
                    long batches=(missing+r.Quantity-1)/r.Quantity;credits=checked(credits+r.Credits*batches);
                    if(take>0)required.Add(new(id,take,take,0,0));
                    Blueprint(r,batches);var children=r.Ingredients.Select(ingredient=>Take(ingredient.Id,checked(ingredient.Count*batches))).ToArray();
                    active.Remove(id);
                    int duration=checked((int)(r.Seconds*batches)),end=checked(Math.Max(available.GetValueOrDefault(id),children.Select(c=>c.Time).DefaultIfEmpty(0).Max())+duration);available[id]=end;
                    int index=work.Count;work.Add(new(id,duration,children.Where(c=>c.Job.HasValue).Select(c=>c.Job!.Value).Concat(producedJob.HasValue?[producedJob.Value]:[]).Distinct().ToArray()));
                    long excess=batches*r.Quantity-missing;if(excess>0){if(!produced.TryGetValue(id,out lots))produced[id]=lots=[];lots.Add((excess,index,end));}return (end,index);
                }
                // Store blueprint credit costs are not Platinum prices. Unpriced
                // resources remain explicit missing requirements, not free items.
                var credit=blueprintIds.Contains(id)?items.GetValueOrDefault(id)?.BlueprintCredits:null;
                if(credit is >0){credits=checked(credits+credit.Value*missing);blueprintCredits=checked(blueprintCredits+credit.Value*missing);required.Add(new(id,needed,take,missing,0,true));warnings.Add("설계도 구매처·퀘스트·클랜 연구·평판 조건 확인 필요");return (0,null);}
                double? buy=prices.TryGetValue(id,out var p)?p.Platinum*missing:null;if(p is not null)stale|=p.Stale;
                required.Add(new(id,take+missing,take,missing,buy));return (producedTime,producedJob);
            }
            void Blueprint(InventoryRecipe r,long batches)
            {
                long need=r.ConsumeBlueprint?batches:1;long have=stock.GetValueOrDefault(r.BlueprintId),take=Math.Min(have,need);
                if(r.ConsumeBlueprint)stock[r.BlueprintId]=have-take;
                long missing=need-take;var cost=items.GetValueOrDefault(r.BlueprintId)?.BlueprintCredits;
                if(missing>0&&cost is >0){credits=checked(credits+missing*cost.Value);blueprintCredits=checked(blueprintCredits+missing*cost.Value);required.Add(new(r.BlueprintId,need,take,missing,0,true));warnings.Add("설계도 구매처·퀘스트·클랜 연구·평판 조건 확인 필요");}
                else{double? price=missing==0?0:prices.TryGetValue(r.BlueprintId,out var p)?p.Platinum*missing:null;required.Add(new(r.BlueprintId,need,take,missing,price));if(prices.TryGetValue(r.BlueprintId,out var q)){opportunity+=take*q.Platinum;stale|=q.Stale;}}
            }
            var direct=InventoryAcquisition.DirectCredits(item.Id);
            int seconds=0;
            if(!owned&&!queued)
            {
                if(direct is not null){credits=direct.Costs[0].Amount;required.Add(new(item.Id,1,0,1,0));warnings.Add("완성품 크레딧 구매 · 상점에서 구입 후 바로 랭작");}
                else seconds=Take(item.Id,1).Time;
            }
            if(credits>inventory.Credits)warnings.Add($"크레딧 부족 · {credits-inventory.Credits:N0} 필요");
            if(!owned&&!queued&&recipe is null&&direct is null)warnings.Add("직접 획득·조립 장비 · 아래 획득처에서 조건 확인");
            // Acquisition MR is not a levelling lock on a weapon already owned
            // through a bypass (e.g. a claimed Lich weapon).
            bool rankLocked=!owned&&!queued&&inventory.MasteryRank is {} mr&&mr<item.MasteryRequired;
            if(!owned&&!queued&&inventory.MasteryRank is null)warnings.Add("계정 마스터리 랭크 미확인 · 제작 제한 확인 필요");
            double? total=required.All(x=>x.Missing==0||x.BuyPlatinum.HasValue)?required.Sum(x=>x.BuyPlatinum??0):null;
            if(!inventory.InventoryComplete&&!owned&&!queued)total=null;
            int forma=inventory.Equipment.Where(e=>e.Id==item.Id).Select(e=>e.Forma).DefaultIfEmpty(0).Max();
            int cap=Math.Min(item.MaxRank,30+2*forma);
            int acquisitionOrder=InventoryAcquisition.AcquisitionOrder(item.Id);
            if(acquisitionOrder==1&&total is >0)acquisitionOrder=2; // all missing parts have a confirmed purchase quote
            plans.Add(new(item,earned,(item.MaxRank-earned)*item.PointsPerRank,owned,queued,rankLocked,required.ToArray(),credits,seconds,total,opportunity,stale,warnings.Distinct().ToArray())
            {AvailablePoints=Math.Max(0,cap-earned)*item.PointsPerRank,AdditionalForma=Math.Max(0,(item.MaxRank-30)/2-forma),AcquisitionOrder=acquisitionOrder,DirectPurchaseCredits=direct?.Costs[0].Amount,BlueprintPurchaseCredits=blueprintCredits,CraftWork=work.ToArray()});
        }
        // Time is a final tie-breaker. Independent Foundry jobs can run together;
        // summing their hours would penalize parallel crafting incorrectly.
        return plans.OrderBy(p=>p.Priority).ThenByDescending(p=>p.Ready).ThenBy(p=>p.AcquisitionOrder==2?p.PlatinumPer1000Mastery??double.MaxValue:0).ThenBy(p=>p.MissingAcquisitions).ThenBy(p=>p.CreditsPer1000Mastery).ThenBy(p=>p.BuildSeconds/(double)Math.Max(1,p.AvailablePoints)).ThenBy(p=>p.Item.Name,StringComparer.Ordinal).ToArray();
    }
}

public sealed record MasteryBatch(MasteryPlan[] Plans,int MasteryPoints,long Credits,int ParallelBuildSeconds);
public static class MasteryBatchPlanner
{
    // A practical feasible sequence using the same recommendation order, not a
    // claim of a globally optimal knapsack or a prediction of farming/levelling.
    public static MasteryBatch Recommend(InventoryCatalog catalog,InventorySnapshot snapshot,IReadOnlyDictionary<string,PlannerPrice>? prices=null,int limit=12)
    {
        if(limit is <1 or >50)throw new ArgumentOutOfRangeException(nameof(limit));
        if(!snapshot.InventoryComplete||!snapshot.MasteryComplete)return new([],0,0,0);
        var stock=new Dictionary<string,long>(snapshot.Quantities);long credits=snapshot.Credits;var chosen=new List<MasteryPlan>();var selected=new HashSet<string>();
        var reusable=catalog.Recipes.Where(r=>!r.ConsumeBlueprint).Select(r=>r.BlueprintId).ToHashSet();
        for(int index=0;index<limit;index++)
        {
            var next=snapshot with{Quantities=new(stock),Credits=credits};
            var plan=MasteryPlanner.Recommend(catalog,next,prices).FirstOrDefault(p=>!p.Owned&&!p.InFoundry&&!p.RankLocked&&!selected.Contains(p.Item.Id)&&p.Credits<=credits&&p.MissingAcquisitions==0&&p.AcquisitionOrder<3&&!p.Warnings.Any(w=>w.Contains("소모 전 랭작")));
            if(plan is null)break;
            foreach(var requirement in plan.Requirements)
            {
                if(reusable.Contains(requirement.Id))
                {if(requirement.CreditBlueprint&&requirement.Missing>0)stock[requirement.Id]=Math.Max(1,stock.GetValueOrDefault(requirement.Id));continue;}
                stock[requirement.Id]=Math.Max(0,stock.GetValueOrDefault(requirement.Id)-requirement.Owned);
            }
            credits-=plan.Credits;selected.Add(plan.Item.Id);chosen.Add(plan);
        }
        var available=new Dictionary<string,int>();int last=0;
        foreach(var plan in chosen)
        {
            var completion=new int[plan.CraftWork.Length];
            for(int i=0;i<completion.Length;i++)
            {
                var job=plan.CraftWork[i];int start=Math.Max(available.GetValueOrDefault(job.ResultId),job.Dependencies.Select(d=>completion[d]).DefaultIfEmpty(0).Max());
                completion[i]=checked(start+job.Seconds);available[job.ResultId]=completion[i];last=Math.Max(last,completion[i]);
            }
        }
        return new(chosen.ToArray(),chosen.Sum(p=>p.AvailablePoints),snapshot.Credits-credits,last);
    }
}

public sealed record RelicPlan(InventoryRelic Relic,long Owned,int SourceRefinement,int TraceCost,int SquadSize,double? ExpectedPlatinum,double ExpectedDucats,
    double TargetChance,double ChanceWithinOwned,int PriceCoverage,int RewardCount,bool PricesStale,bool Affordable);
public static class InventoryRelicPlanner
{
    public static readonly string[] Refinements=["온전한","우수한","완벽한","빛나는"];
    public static readonly int[] TraceCosts=[0,25,50,100];
    public static double Probability(string rarity,int refinement)=>rarity switch
    {
        "COMMON"=>new[]{.76/3,.70/3,.60/3,.50/3}[refinement],
        "UNCOMMON"=>new[]{.11,.13,.17,.20}[refinement],
        "RARE"=>new[]{.02,.04,.06,.10}[refinement],_=>throw new ArgumentException("일반 성유물 보상 등급이 아닙니다.")
    };
    public static double ExpectedBest(IEnumerable<(double Value,double Probability)> distribution,int squad)
    {
        if(squad is <1 or >4)throw new ArgumentOutOfRangeException(nameof(squad));
        double cdf=0,last=0,expected=0;
        foreach(var slot in distribution.GroupBy(x=>x.Value).OrderBy(g=>g.Key))
        {cdf+=slot.Sum(x=>x.Probability);var next=Math.Pow(Math.Min(1,cdf),squad);expected+=slot.Key*(next-last);last=next;}
        return expected;
    }
    public static RelicPlan[] Recommend(InventoryCatalog catalog,InventorySnapshot inventory,IReadOnlyDictionary<string,PlannerPrice> prices,
        IReadOnlyDictionary<string,int> ducats,IReadOnlySet<string>? targets=null,int squad=1,long traces=0)
    {
        if(squad is <1 or >4||traces<0)throw new ArgumentOutOfRangeException();
        var variants=catalog.Relics.GroupBy(r=>r.Family).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.Refinement).ToArray());var results=new List<RelicPlan>();var items=catalog.Items.ToDictionary(i=>i.Id);
        double? Value(string id)=>prices.TryGetValue(id,out var p)?p.Platinum:items.GetValueOrDefault(id)?.Tradable==false?0:null;
        foreach(var source in catalog.Relics.Where(r=>inventory.Count(r.Id)>0))
        foreach(var target in variants[source.Family].Where(r=>r.Refinement>=source.Refinement&&(!source.Family.Contains("/T5",StringComparison.Ordinal)||r.Refinement==source.Refinement)))
        {
            // Refining an already-refined relic pays the target's full trace cost.
            int cost=target.Refinement==source.Refinement?0:TraceCosts[target.Refinement];
            int coverage=target.Rewards.Count(s=>Value(s.Id).HasValue);bool stale=target.Rewards.Any(s=>prices.GetValueOrDefault(s.Id)?.Stale==true);
            double? platinum=coverage==target.Rewards.Length?ExpectedBest(target.Rewards.Select(s=>(Value(s.Id)!.Value*s.Count,Probability(s.Rarity,target.Refinement))),squad):null;
            double dc=ExpectedBest(target.Rewards.Select(s=>((double)ducats.GetValueOrDefault(s.Id)*s.Count,Probability(s.Rarity,target.Refinement))),squad);
            double p=target.Rewards.Where(s=>targets?.Contains(s.Id)==true).Sum(s=>Probability(s.Rarity,target.Refinement));p=1-Math.Pow(1-p,squad);
            var count=inventory.Count(source.Id);results.Add(new(target,count,source.Refinement,cost,squad,platinum,dc,p,1-Math.Pow(1-p,count),coverage,target.Rewards.Length,stale,cost<=traces));
        }
        return results.OrderByDescending(r=>targets is {Count:>0}?r.TargetChance:r.ExpectedPlatinum??-1).ThenBy(r=>r.TraceCost).ThenByDescending(r=>r.ExpectedDucats).ToArray();
    }
}
