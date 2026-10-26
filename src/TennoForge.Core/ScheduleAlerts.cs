namespace TennoForge.Core;

public sealed record ScheduleAlertRules(bool NextS=false,bool PlainsNight=false,bool TenetRotation=false,bool CodaWishlist=false,int LeadMinutes=5,string[]? WishWeaponIds=null)
{
    public bool Any=>NextS||PlainsNight||TenetRotation||CodaWishlist;
}
public sealed record ScheduleAlert(string Id,string Title,string Detail,DateTimeOffset EventUtc);
public static class ScheduleAlerts
{
    public static ScheduleAlert[] Due(ScheduleCatalog catalog,WorldScheduleData data,ScheduleAlertRules rules,DateTimeOffset now,ISet<string> delivered)
    {
        if(rules.LeadMinutes is <0 or >60)throw new ArgumentOutOfRangeException(nameof(rules));
        var result=new List<ScheduleAlert>();
        void Add(string key,string title,string detail,DateTimeOffset eventTime)
        {
            var id=key+":"+eventTime.ToUnixTimeSeconds();if(delivered.Contains(id)||eventTime<now||eventTime-now>TimeSpan.FromMinutes(rules.LeadMinutes))return;
            result.Add(new(id,title,detail,eventTime));
        }
        if(rules.NextS&&now-data.Arbitrations.RetrievedUtc<TimeSpan.FromDays(7)&&data.Arbitrations.RetrievedUtc-now<TimeSpan.FromMinutes(2)&&!data.ArbitrationConflict(now))
        {
            var s=data.Arbitrations.NextS(catalog,now.AddTicks(-1));
            if(s is not null){var n=catalog.Nodes.Single(x=>x.Id==s.NodeId);Add("arbitration-s","S티어 중재가 곧 시작됩니다",$"{n.Planet} · {n.Name} · {n.Mission}\n일정표 예측 · {WorldScheduleEngine.KoreaTime(s.StartUtc)}",s.StartUtc);}
        }
        if(rules.PlainsNight&&data.Fresh(now)&&data.Snapshot!.Cycles.FirstOrDefault(c=>c.Id=="cetusCycle") is {} anchor)
        {var next=WorldScheduleEngine.NextState(anchor,"night",now);if(next is not null)Add("plains-night","아이돌론 평원의 밤이 곧 시작됩니다",WorldScheduleEngine.KoreaTime(next.StartUtc),next.StartUtc);}
        if(rules.TenetRotation)
        {var shop=WorldScheduleEngine.ShopAt(catalog,"tenet",now);Add("tenet","테넷 무기 보너스 교체가 곧 시작됩니다",WorldScheduleEngine.KoreaTime(shop.EndUtc)+" · 위키 주기 예상",shop.EndUtc);}
        if(rules.CodaWishlist)
        {
            var shop=WorldScheduleEngine.ShopAt(catalog,"coda",now);var upcoming=catalog.Offers.Where(w=>w.Shop=="coda"&&w.Batch!=shop.Batch&&rules.WishWeaponIds?.Contains(w.Id)==true).ToArray();
            if(upcoming.Length>0)Add("coda-wishlist","찜한 코다 무기의 판매 회차가 곧 시작됩니다",string.Join(" · ",upcoming.Select(w=>w.Name))+"\n위키 주기 예상 · "+WorldScheduleEngine.KoreaTime(shop.EndUtc),shop.EndUtc);
        }
        return result.ToArray();
    }
}
