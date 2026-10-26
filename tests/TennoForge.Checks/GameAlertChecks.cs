using System.Text.Json;
using TennoForge.Core;

static class GameAlertChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool ok,string why){if(!ok)throw new Exception(why);}
        var now=DateTimeOffset.Parse("2026-10-26T00:00:00Z");const string item="/Lotus/Types/Gameplay/Events/RoadToTauEvent/RoadToTauEventResource";
        string Api(string id="a")=>JsonSerializer.Serialize(new{alerts=new[]{new{id,activation=now.AddMinutes(-1),expiry=now.AddHours(1),mission=new{nodeKey="Saxis (Eris)",typeKey="Extermination",reward=new{credits=17000,countedItems=new[]{new{uniqueName=item,count=50}}}}}}});
        var baseSnapshot=WorldScheduleChecks.Snapshot(now);var table=ArbitrationSchedule.Bundled(ScheduleCatalog.Load());
        WorldAlertMission Parse(){using var doc=JsonDocument.Parse(Api());return GameAlerts.FromApi(doc.RootElement).Single();}
        check("얼럿 API: 내부 ID·한국어 위치·월육·크레딧·활성 시간",()=>
        {var a=Parse();Yes(a.Id=="a"&&a.Mission=="섬멸"&&a.Node=="에리스 · Saxis"&&a.Reward.Contains("월육 ×50")&&a.Reward.Contains("17,000 크레딧")&&a.Active(now),"API 정규화");});
        check("얼럿 공식 자료: MissionInfo와 보상 내부 ID 연결",()=>
        {
            object Date(DateTimeOffset t)=>new Dictionary<string,object>{{"$date",new Dictionary<string,string>{{"$numberLong",t.ToUnixTimeMilliseconds().ToString()}}}};
            var reward=new{credits=17000,countedItems=new[]{new{ItemType=item,ItemCount=50}}};var mission=new{location="SolNode171",missionType="MT_EXTERMINATION",missionReward=reward};
            var alert=new{_id=new Dictionary<string,string>{{"$oid","official"}},Activation=Date(now),Expiry=Date(now.AddHours(1)),MissionInfo=mission};
            using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{Alerts=new[]{alert}}));var a=GameAlerts.FromOfficial(doc.RootElement).Single();Yes(a.Id=="official"&&a.Node=="에리스 · Saxis"&&a.Reward.Contains("월육 ×50"),"공식 자료 정규화");
        });
        check("얼럿 알림: 기본 끔·보상 이름 필터·ID 중복 방지",()=>
        {var a=Parse();var data=new WorldScheduleData(baseSnapshot with{Alerts=[a],AlertsUtc=now},table,false,null,now);var seen=new HashSet<string>();Yes(GameAlerts.Due(data,new(),now,seen).Length==0,"기본 끔");var p=new SchedulePreferences{NotifyGameAlerts=true,AlertRewardFilter="월육"};var due=GameAlerts.Due(data,p,now,seen);Yes(due.Length==1&&due[0].Id=="alert:a","보상 필터 일치");seen.Add(due[0].Id);Yes(GameAlerts.Due(data,p,now,seen).Length==0,"중복 방지");Yes(GameAlerts.Due(data,p with{AlertRewardFilter="니타인"},now,new HashSet<string>()).Length==0,"미일치 필터");});
        check("얼럿 알림: 캐시·오래된 자료·미시작·만료 이벤트 차단",()=>
        {var a=Parse();var p=new SchedulePreferences{NotifyGameAlerts=true};var data=new WorldScheduleData(baseSnapshot with{Alerts=[a],AlertsUtc=now},table,false,null,now);Yes(GameAlerts.Due(data with{Cached=true},p,now,new HashSet<string>()).Length==0,"캐시 차단");Yes(GameAlerts.Due(data with{Snapshot=data.Snapshot! with{AlertsUtc=now.AddMinutes(-4)}},p,now,new HashSet<string>()).Length==0,"오래된 자료");foreach(var x in new[]{a with{StartUtc=now.AddSeconds(1)},a with{EndUtc=now}})Yes(GameAlerts.Due(data with{Snapshot=data.Snapshot! with{Alerts=[x]}},p,now,new HashSet<string>()).Length==0,"활성 경계");});
        check("얼럿 알림: 성유물 ID와 분리·새 얼럿은 다시 알림",()=>
        {var a=Parse();var data=new WorldScheduleData(baseSnapshot with{Alerts=[a,a with{Id="new"}],AlertsUtc=now},table,false,null,now);var due=GameAlerts.Due(data,new(){NotifyGameAlerts=true},now,new HashSet<string>{"fissure:a","alert:new"});Yes(due.Length==1&&due[0].Id=="alert:a","알림 namespace 분리");});
        check("얼럿 설정: 표시·알림·보상 필터 JSON 저장 복원",()=>
        {var p=new SchedulePreferences{ShowGameAlerts=false,NotifyGameAlerts=true,AlertRewardFilter="월육"};var q=JsonSerializer.Deserialize<SchedulePreferences>(JsonSerializer.Serialize(p,Catalog.Json),Catalog.Json)!;Yes(!q.ShowGameAlerts&&q.NotifyGameAlerts&&q.AlertRewardFilter=="월육","설정 복원");});
        check("얼럿 없는 응답: 빈 배열은 정상·없는 필드는 신선함을 보장하지 않음",()=>
        {using var doc=JsonDocument.Parse("{\"alerts\":[]}");Yes(GameAlerts.FromApi(doc.RootElement).Length==0&&!GameAlerts.Fresh(baseSnapshot with{AlertsUtc=null},now),"빈 자료/누락 구분");});
    }
}
