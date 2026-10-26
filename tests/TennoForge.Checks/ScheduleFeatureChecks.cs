using System.Text.Json;
using TennoForge.Core;

static class ScheduleFeatureChecks
{
    internal static void Run(Action<string,Action> check)
    {
        GameAlertChecks.Run(check);
        void Yes(bool b,string why){if(!b)throw new Exception(why);}
        var now=DateTimeOffset.Parse("2026-10-26T00:00:00Z");var c=ScheduleCatalog.Load();var table=ArbitrationSchedule.Bundled(c);
        var f=new FissureMission("a","자리만 · Tuvul Commons","MT_VOID_CASCADE",6,true,false,now.AddMinutes(-1),now.AddHours(1));
        var snapshot=WorldScheduleChecks.Snapshot(now) with{Fissures=[f],FissuresUtc=now};var data=new WorldScheduleData(snapshot,table,false,null,now);
        check("중재 다중 선택: 감청을 제외하면 S티어 목록과 다음 S에서 제외",()=>
        {var filter=new ArbitrationFilter("S",ExcludedMissions:["감청"]);Yes(c.Nodes.Where(n=>n.Mission=="감청").All(n=>!filter.Matches(n)),"감청 제외");var next=table.NextS(c,now,filter);Yes(next is not null&&c.Nodes.Single(n=>n.Id==next.NodeId).Mission!="감청","다음 S 연동");});
        check("중재 다중 선택: 전체 해제·전체 선택·팩션 교집합",()=>
        {Yes(table.NextS(c,now,new("S",ExcludedMissions:c.Nodes.Select(n=>n.Mission).Distinct().ToArray())) is null,"전체 해제");Yes(table.NextS(c,now,new("S",ExcludedMissions:[]))==table.NextS(c,now),"전체 선택");var filter=new ArbitrationFilter("S",Faction:"코퍼스",ExcludedMissions:["감청"]);Yes(c.Nodes.Where(filter.Matches).All(n=>n.Mission!="감청"&&n.Faction.Contains("코퍼스")),"팩션 교집합");});
        check("성유물 한국어: 보이드 범람·보이드 홍수 공식 문자열 분리",()=>Yes(FissureParser.MissionLabel("MT_VOID_CASCADE")=="보이드 범람"&&FissureParser.MissionLabel("MT_CORRUPTION")=="보이드 홍수","한국어 매핑"));
        check("성유물 API: 강길·폭풍·등급·내부 ID·시각 파싱",()=>
        {using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{fissures=new[]{new{id="test",node="Tuvul Commons (Zariman)",missionTypeKey="Void Cascade",tierNum=6,isHard=true,isStorm=false,activation=now,expiry=now.AddHours(1)}}}));var x=FissureParser.FromApi(doc.RootElement).Single();Yes(x.Id=="test"&&x.SteelPath&&!x.Storm&&x.MissionCode=="MT_VOID_CASCADE"&&x.Mission=="보이드 범람"&&x.TierLabel=="옴니아"&&x.Node.Contains("자리만"),"API 파싱");});
        check("성유물 공식 원문: Hard·Modifier·노드와 한국어 연결",()=>
        {object Date(DateTimeOffset t)=>new Dictionary<string,object>{{"$date",new Dictionary<string,string>{{"$numberLong",t.ToUnixTimeMilliseconds().ToString()}}}};var r=new{ActiveMissions=new[]{new{_id=new Dictionary<string,string>{{"$oid","official"}},Node="SolNode135",MissionType="MT_SABOTAGE",Hard=true,Modifier="VoidT2",Activation=Date(now),Expiry=Date(now.AddHours(1))}}};using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));var x=FissureParser.FromOfficial(doc.RootElement).Single();Yes(x.Id=="official"&&x.SteelPath&&x.Tier==2&&x.Mission==FissureParser.MissionLabel("MT_SABOTAGE")&&!x.Node.StartsWith("SolNode"),"공식 파싱");});
        check("성유물 알림: 강길 범람 조건만 일치",()=>
        {var rule=new FissureAlertRule("r",["MT_VOID_CASCADE"]);Yes(rule.Matches(f)&&!rule.Matches(f with{SteelPath=false})&&!rule.Matches(f with{MissionCode="MT_CORRUPTION"})&&!rule.Matches(f with{Storm=true}),"경로와 미션 AND");Yes(!(rule with{Tier=1}).Matches(f),"등급 필터");});
        check("성유물 알림: 같은 균열 중복 제거·새 ID 재알림",()=>
        {var rule=new FissureAlertRule("r",["MT_VOID_CASCADE"]);var delivered=new HashSet<string>();var first=FissureAlerts.Due(data,[rule,rule with{Id="r2"}],now,delivered);Yes(first.Length==1,"중복 조건");delivered.Add(first[0].Id);Yes(FissureAlerts.Due(data,[rule],now,delivered).Length==0,"중복 알림");Yes(FissureAlerts.Due(data with{Snapshot=snapshot with{Fissures=[f with{Id="new"}]}},[rule],now,delivered).Length==1,"새 균열");});
        check("성유물 알림: 오래된 자료·캐시·만료·시작 전은 알리지 않음",()=>
        {var r=new FissureAlertRule("r",[]);var none=new HashSet<string>();Yes(FissureAlerts.Due(data with{Cached=true},[r],now,none).Length==0,"오프라인");Yes(FissureAlerts.Due(data with{Snapshot=snapshot with{FissuresUtc=now.AddMinutes(-4)}},[r],now,none).Length==0,"오래된 API");foreach(var x in new[]{f with{EndUtc=now},f with{StartUtc=now.AddSeconds(1)}})Yes(FissureAlerts.Due(data with{Snapshot=snapshot with{Fissures=[x]}},[r],now,none).Length==0,"활성 시간 경계");});
        check("성유물 알림: 주기와 균열의 자료 신선도 독립",()=>
        {var r=new FissureAlertRule("r",[]);Yes(FissureAlerts.Due(data with{Snapshot=snapshot with{Timestamp=now.AddDays(-1),FissuresUtc=now}},[r],now,new HashSet<string>()).Length==1,"공식 보완 균열");});
        check("성유물 갱신: 주기가 유효해도 균열 자료 3분 지연이면 공식 자료로 보완",()=>
        {
            var at=DateTimeOffset.UtcNow;var folder=Path.Combine(Path.GetTempPath(),"wf-fissure-test-"+Guid.NewGuid().ToString("N"));
            object Date(DateTimeOffset t)=>new Dictionary<string,object>{{"$date",new Dictionary<string,string>{{"$numberLong",t.ToUnixTimeMilliseconds().ToString()}}}};
            var official=JsonSerializer.Serialize(new{Time=at.ToUnixTimeSeconds(),SyndicateMissions=new[]{new{Tag="CetusSyndicate",Expiry=Date(at.AddMinutes(120))}},ActiveMissions=new[]{new{_id=new Dictionary<string,string>{{"$oid","fresh-official"}},Node="SolNode232",MissionType="MT_VOID_CASCADE",Hard=true,Modifier="VoidT6",Activation=Date(at.AddMinutes(-1)),Expiry=Date(at.AddHours(1))}}});
            using var client=new WorldScheduleClient(folder,new FixtureHandler(request=>request.RequestUri!.Host.Contains("github")?string.Join('\n',table.Slots.Select(s=>$"{s.StartUtc.ToUnixTimeSeconds()},{s.NodeId}")):request.RequestUri.Host=="api.warframestat.us"?WorldScheduleChecks.Fixture(at.AddMinutes(-4)):official));
            try{var value=client.RefreshAsync(true,default).GetAwaiter().GetResult();Yes(value.Snapshot!.FissuresSource=="공식 월드 상태"&&FissureAlerts.Fresh(value.Snapshot,at)&&value.Snapshot.Fissures.Single().Id=="fresh-official","공식 균열 복구");}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        });
        check("일정 설정: 위치·크기·무효값 범위 보정과 JSON 왕복",()=>
        {var p=(new SchedulePreferences{X=-1,Y=2,Width=5000,Height=-5,ExcludedMissions=["감청"],FissureRules=[new("r",["MT_VOID_CASCADE"])]}).Sanitize();Yes(p.X==0&&p.Y==1&&p.Width==1100&&p.Height==260,"화면 밖 보정");var restored=JsonSerializer.Deserialize<SchedulePreferences>(JsonSerializer.Serialize(p,Catalog.Json),Catalog.Json)!;Yes(restored.ExcludedMissions.SequenceEqual(p.ExcludedMissions)&&restored.FissureRules[0].Matches(f),"재시작 설정");});
        check("오버레이 설정: 배경·그림자·글자·음량의 NaN·무한대·범위 밖 보정",()=>
        {var p=(new SchedulePreferences{BackgroundOpacity=double.NaN,SoundVolume=double.PositiveInfinity,ShadowStrength=-1,TextScale=9,FissureView=99}).Sanitize();Yes(p.BackgroundOpacity==.82&&p.SoundVolume==.65&&p.ShadowStrength==0&&p.TextScale==1.3&&p.FissureView==4,"설정 보정");});
        check("오버레이 설정: 이전 설정 파일과 새 표시·사운드 설정 호환",()=>
        {var old=JsonSerializer.Deserialize<SchedulePreferences>("{\"width\":360,\"height\":420,\"overlayItems\":[\"nextS\"]}",Catalog.Json)!.Sanitize();Yes(old.Width==360&&old.ShowCurrentFissures&&old.TextShadow&&old.BackgroundOpacity==.82,"이전 파일 호환");var p=old with{BackgroundOpacity=.35,TextShadow=false,TextScale=1.15,SoundVolume=.4,FissureView=1};var r=JsonSerializer.Deserialize<SchedulePreferences>(JsonSerializer.Serialize(p,Catalog.Json),Catalog.Json)!;Yes(r.BackgroundOpacity==.35&&!r.TextShadow&&r.TextScale==1.15&&r.SoundVolume==.4&&r.FissureView==1,"표시 설정 복원");});
    }
    private sealed class FixtureHandler(Func<HttpRequestMessage,string> respond):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(respond(request))});}
}
