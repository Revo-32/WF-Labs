using TennoForge.Core;

static class DashboardChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool ok,string why){if(!ok)throw new Exception(why);}
        var c=InventoryCatalog.Load();
        check("마스터리 대시보드: 이력 미연동을 미숙련0%로 표시하지 않음",()=>
        {var p=MasteryOverview.From(c,InventorySnapshot.Empty);Yes(p.Total==809&&p.Completed is null&&p.Remaining is null&&p.Owned is null&&p.Groups.All(g=>g.Completed is null),"미확인 상태");});
        check("마스터리 대시보드: 판매한 장비·40랭크·종류별 합계",()=>
        {
            var frame=c.Items.Single(i=>i.EnglishName=="Rhino");var gun=c.Items.Single(i=>i.EnglishName=="Braton Prime");var forty=c.Items.First(i=>i.MaxRank==40);
            var s=InventorySnapshot.Empty with{InventoryComplete=true,MasteryComplete=true,Quantities=new(){{frame.Id,2}},MasteryRanks=new(){{frame.Id,10},{gun.Id,30},{forty.Id,30}}};var p=MasteryOverview.From(c,s);
            Yes(p.Completed==1&&p.InProgress==2&&p.Owned==1&&p.Remaining==808,"중복 보유·판매·40랭크 분리");Yes(p.Groups.Sum(g=>g.Total)==p.Total&&p.Groups.Sum(g=>g.Completed)==p.Completed&&p.Groups.Sum(g=>g.InProgress)==p.InProgress,"분류 합계");
        });
        var relic=c.Relics.First(r=>r.Refinement==3&&!r.Family.Contains("/T5"));
        check("성유물 추천: 가치 기준 이상 확률은 4인 중 최고 보상 하나 기준",()=>
        {
            var selected=relic.Rewards.First(r=>r.Rarity=="RARE");var prices=relic.Rewards.ToDictionary(r=>r.Id,r=>new PlannerPrice(r.Id==selected.Id?10:1,DateTimeOffset.UtcNow,false));
            var p=RelicValueChance.Evaluate(c,relic,prices,10,4);Yes(p.Exact&&Math.Abs(p.Probability-.3439)<1e-10,"희귀보상 4인34.39% · 네 보상 확률을 더하면 안 됨");
        });
        check("성유물 추천: 미조회 호가는0P가 아닌 확률 하한",()=>
        {
            var rare=relic.Rewards.First(r=>r.Rarity=="RARE");var prices=new Dictionary<string,PlannerPrice>{{rare.Id,new(10,DateTimeOffset.UtcNow,false)}};var p=RelicValueChance.Evaluate(c,relic,prices,10,1);
            Yes(!p.Exact&&Math.Abs(p.Probability-.1)<1e-10&&p.KnownRewards<p.TotalRewards,"부분 시세 하한");
            bool rejected=false;try{RelicValueChance.Evaluate(c,relic,prices,0,4);}catch(ArgumentOutOfRangeException){rejected=true;}Yes(rejected,"0 이하의 가치 기준");
        });
    }
}
