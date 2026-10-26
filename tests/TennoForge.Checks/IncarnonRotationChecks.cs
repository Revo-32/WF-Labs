using System.Text.Json;
using TennoForge.Core;

internal static class IncarnonRotationChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool value){if(!value)throw new Exception("인카논 순환 조건 불일치");}
        var table=IncarnonRotations.Table;
        var monday=DateTimeOffset.Parse("2026-10-26T00:00:00Z");
        string Api(object choices)=>JsonSerializer.Serialize(new{duviriCycle=new{activation=monday.AddHours(2),expiry=monday.AddHours(4),choices}});
        IncarnonRotationAnchor? Read(string json){using var doc=JsonDocument.Parse(json);return IncarnonRotations.FromApi(doc.RootElement,monday);}
        object OfficialRow(DateTimeOffset start,int week)=>new{
            Activation=new Dictionary<string,object>{{"$date",new Dictionary<string,string>{{"$numberLong",start.ToUnixTimeMilliseconds().ToString()}}}},
            Expiry=new Dictionary<string,object>{{"$date",new Dictionary<string,string>{{"$numberLong",start.AddDays(7).ToUnixTimeMilliseconds().ToString()}}}},
            CategoryChoices=new[]{new{Category="EXC_HARD",Choices=table.Weeks[week-1].Weapons.Select(w=>w.EnglishName).ToArray()}}};
        check("인카논: 위키9주·45개 중복 없는 기본 무기",()=>Yes(table.Weeks.Length==9&&table.Weeks.All(w=>w.Weapons.Length==5)&&table.Weeks.SelectMany(w=>w.Weapons).Select(w=>w.Id).Distinct().Count()==45));
        check("인카논: 모든 무기 ID·한국어 이름·분류를 인벤토리 원본과 연결",()=>{
            var inventory=InventoryCatalog.Load();foreach(var weapon in table.Weeks.SelectMany(w=>w.Weapons))
            {var item=inventory.Items.Single(i=>i.Id==weapon.Id);Yes(item.Masterable&&item.Name==weapon.Name&&item.Category==weapon.Category&&weapon.Name.Any(c=>c>='가'&&c<='힣'));}
        });
        check("인카논: 45개 모두 실제 모딩 내부 ID 연결",()=>{
            var ids=Catalog.Load().Weapons.Select(w=>w.Id).Concat(ReferenceCatalog.Load().Weapons.Select(w=>w.InternalId)).ToHashSet();Yes(table.Weeks.SelectMany(w=>w.Weapons).All(w=>ids.Contains(w.Id)));
        });
        check("인카논: 월요일 UTC0시·한국9시 경계",()=>{Yes(IncarnonRotations.WeekStart(monday.AddTicks(-1))==monday.AddDays(-7));Yes(IncarnonRotations.WeekStart(monday)==monday);Yes(IncarnonRotations.WeekStart(monday.ToOffset(TimeSpan.FromHours(9)))==monday);});
        check("인카논: 공식10월19일8주차 기준·26일9주차·다음1주차",()=>{Yes(IncarnonRotations.Current(monday.AddDays(-7)).Week==8);Yes(IncarnonRotations.Current(monday).Week==9);Yes(IncarnonRotations.Current(monday.AddDays(7)).Week==1);});
        check("인카논: 9주 반복·기준 이전 음수 주차",()=>{foreach(int n in new[]{-18,-9,0,9,18})Yes(IncarnonRotations.Current(monday.AddDays(7*n)).Week==9);});
        check("인카논: 예상 일정은 확정으로 표시하지 않음",()=>{var c=IncarnonRotations.Current(monday);Yes(!c.Confirmed&&c.EndUtc==monday.AddDays(7));Yes(IncarnonRotations.Upcoming(monday,8).All(w=>!w.Confirmed));});
        check("인카논: 다음8주 연속·중복 없는9주 전체 구성",()=>{var all=new[]{IncarnonRotations.Current(monday)}.Concat(IncarnonRotations.Upcoming(monday,8)).ToArray();Yes(all.Select(w=>w.Week).Distinct().Count()==9);Yes(all.Zip(all.Skip(1)).All(p=>p.First.EndUtc==p.Second.StartUtc));});
        check("인카논: API 나선2시간과 주간 교체시간을 구분",()=>{
            var a=Read(Api(new[]{new{category="hard",choices=table.Weeks[8].Weapons.Select(w=>w.EnglishName).ToArray()}}))!;Yes(a.StartUtc==monday&&a.EndUtc==monday.AddDays(7));Yes(IncarnonRotations.Current(monday,a).Confirmed);
        });
        check("인카논: 공식 CategoryKey·순서·공백 변형 허용",()=>{
            var a=Read(Api(new[]{new{categoryKey="EXC_HARD",choices=table.Weeks[5].Weapons.Select(w=>w.EnglishName.Replace(" & ","")).Reverse().ToArray()}}))!;Yes(IncarnonRotations.Current(monday,a).Week==6);
        });
        check("인카논: 라이브 구성 우선·다음 주도 라이브 기준",()=>{
            var a=new IncarnonRotationAnchor(monday,monday.AddDays(7),table.Weeks[2].Weapons.Select(w=>w.EnglishName).ToArray(),monday,"검수");Yes(IncarnonRotations.Current(monday,a).Week==3&&IncarnonRotations.Upcoming(monday,1,a)[0].Week==4);
        });
        check("인카논: 오래된 자료·미래 시각은 확정 금지",()=>{
            var a=new IncarnonRotationAnchor(monday,monday.AddDays(7),table.Weeks[8].Weapons.Select(w=>w.EnglishName).ToArray(),monday,"검수");Yes(!IncarnonRotations.Current(monday.AddMinutes(10),a).Confirmed);Yes(!IncarnonRotations.Current(monday,a with{SourceUtc=monday.AddMinutes(3)}).Confirmed);
        });
        check("인카논: 지난 라이브 주간은 반복하지 않고 현재 예상으로 전환",()=>{
            var a=new IncarnonRotationAnchor(monday.AddDays(-7),monday,table.Weeks[7].Weapons.Select(w=>w.EnglishName).ToArray(),monday,"검수");var c=IncarnonRotations.Current(monday,a);Yes(c.Week==9&&!c.Confirmed);
        });
        check("인카논: 미등록 새 구성은 현재만 표시·임의 다음 주 금지",()=>{
            var a=new IncarnonRotationAnchor(monday,monday.AddDays(7),new[]{"Braton","Lato","Skana","Paris","UnknownWeapon"},monday,"검수");var c=IncarnonRotations.Current(monday,a);Yes(c.Week==0&&c.Weapons.Length==5&&c.Weapons[^1].Id==""&&IncarnonRotations.Upcoming(monday,8,a).Length==0);
        });
        check("인카논: 잘못된 API·개수·중복·값 형식 무시",()=>{
            foreach(var json in new[]{"null","[]","{\"duviriCycle\":null}","{\"duviriCycle\":{\"choices\":[null,3,{\"categoryKey\":null}]}}",Api(new[]{new{category="hard",choices=new[]{"A","A","B","C","D"}}}),Api(new[]{new{category="hard",choices=new object[]{"A","B","C","D","E",3}}})})Yes(Read(json) is null);
        });
        check("인카논: 공식 주간 선택지 파싱·기록 여러개 중 현행 우선",()=>{
            using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{EndlessXpSchedule=new[]{OfficialRow(monday.AddDays(7),1),OfficialRow(monday.AddDays(-7),8),OfficialRow(monday,9)}}));var a=IncarnonRotations.FromOfficial(doc.RootElement,monday)!;Yes(a.StartUtc==monday&&IncarnonRotations.Current(monday,a).Week==9);
        });
        check("인카논: 공식 누락·잘못된 날짜 객체 무시",()=>{
            foreach(var json in new[]{"null","{\"EndlessXpSchedule\":[null,3,{\"Activation\":null},{\"Activation\":{\"$date\":{\"$numberLong\":3}}}]}"}){using var doc=JsonDocument.Parse(json);Yes(IncarnonRotations.FromOfficial(doc.RootElement,monday) is null);}
        });
        check("인카논: 기존 월드 상태 캐시 역호환·새 앵커 직렬화",()=>{
            var previous=WorldScheduleChecks.Snapshot(monday);Yes(previous.Incarnon is null);var next=previous with{Incarnon=new(monday,monday.AddDays(7),table.Weeks[8].Weapons.Select(w=>w.EnglishName).ToArray(),monday,"검수")};var restored=JsonSerializer.Deserialize<WorldScheduleSnapshot>(JsonSerializer.Serialize(next,Catalog.Json),Catalog.Json)!;Yes(restored.Incarnon!.Choices.SequenceEqual(next.Incarnon.Choices));
        });
    }
}
