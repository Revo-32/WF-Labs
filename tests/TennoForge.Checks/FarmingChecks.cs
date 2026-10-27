using System.Text.Json;
using TennoForge.Core;

static class FarmingChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool value,string message){if(!value)throw new Exception(message);}
        string A="/Lotus/Test/Alpha",B="/Lotus/Test/Beta",R="/Lotus/Test/Resource",BA="/Lotus/Test/BlueprintA",BB="/Lotus/Test/BlueprintB",RE="/Lotus/Test/T1Relic";
        InventoryItem Item(string id,bool master=false,int? credits=null)=>new(id,id.Split('/').Last(),id.Split('/').Last(),master?"Primary":"Resources",master,100,master?30:0,0,false,!master,credits,null);
        var catalog=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Item(A,true),Item(B,true),Item(R),Item(BA),Item(BB),Item(RE)],
            [new(BA,A,10,60,1,true,[new(R,2)]),new(BB,B,10,60,1,true,[new(R,2)])],
            [new(RE,RE,"시험 리스 성유물",0,[new(R,"RARE",1)])]);
        var inventory=InventorySnapshot.Empty with{CapturedUtc=DateTimeOffset.UtcNow,Credits=100,InventoryComplete=true,MasteryComplete=true,Quantities=new(){{BA,1},{BB,1},{R,3},{RE,1}}};
        var goals=new FarmingGoalsDocument(1,[new(A),new(B)]);
        FarmingPlan Plan(InventorySnapshot? inv=null,FarmingGoalsDocument? document=null)=>FarmingPlanner.Evaluate(catalog,inv??inventory,document??goals);
        check("목표 예약: 우선순위·재료 중복 방지",()=>
        {var p=Plan();Yes(p.Reserved[R]==3&&p.Missing[R]==1,"같은 재료 중복 예약");Yes(p.Goals[0].Requirements.Single(r=>r.ItemId==R).Reserved==2&&p.Goals[1].Requirements.Single(r=>r.ItemId==R).Reserved==1,"우선순위 배분");Yes(inventory.Count(R)==3,"실제 재고 변조");});
        check("목표 예약: 순서 변경 후 재계산",()=>{var p=Plan(document:goals with{Goals=[new(B),new(A)]});Yes(p.Goals[0].Goal.ItemId==B&&p.Goals[0].Requirements.Single(r=>r.ItemId==R).Missing==0,"순서 변경 반영");});
        check("목표 예약: 일시 정지·삭제 후 해제",()=>{var p=Plan(document:goals with{Goals=[new(A,Enabled:false),new(B)]});Yes(p.Goals.Length==1&&p.Reserved[R]==2&&p.Free(inventory,R)==1,"정지 목표 예약 유지됨");Yes(Plan(document:FarmingGoalsDocument.Empty).Reserved.Count==0,"삭제 후 예약 유지됨");});
        check("목표 예약: 목표 수량과 과잉 예약 방지",()=>{var p=Plan(document:new(1,[new(A,2)]));Yes(p.Missing[R]==1&&p.Missing[BA]==1,"복수 수량 확장");Yes(p.Reserved.All(x=>x.Value<=inventory.Count(x.Key)),"재고 초과 예약");});
        check("목표 예약: 확보한 완성 장비도 보호",()=>{var p=Plan(inventory with{Quantities=new(){{A,1},{BA,1},{BB,1},{R,3}}});Yes(p.Goals[0].Secured&&p.Reserved[A]==1&&p.Reserved[R]==2,"확보 상태 또는 다른 목표의 재료");});
        check("목표 예약: 주조소 재료를 다시 예약하지 않음",()=>{var p=Plan(inventory with{Crafting=[new(BA,DateTimeOffset.UtcNow.AddHours(1))]});Yes(p.Goals[0].Crafting==1&&p.Goals[0].Waiting&&p.Reserved[R]==2,"제작 중 재료 중복 예약");});
        check("목표 예약: 크레딧도 목표 순서대로 예약",()=>{var p=Plan(inventory with{Credits=15});Yes(p.Credits==20&&p.ReservedCredits==15&&p.Goals[0].ReservedCredits==10&&p.Goals[1].ReservedCredits==5,"크레딧 중복 예약");});
        check("목표 예약: 재사용 설계도는 한 번만 확보·구매",()=>
        {
            var c=catalog with{Items=catalog.Items.Select(i=>i.Id==BA?i with{BlueprintCredits=100}:i).ToArray(),Recipes=catalog.Recipes.Select(r=>r with{BlueprintId=BA,ConsumeBlueprint=false}).ToArray()};
            var owned=FarmingPlanner.Evaluate(c,inventory,goals);Yes(owned.Reserved[BA]==1&&owned.Goals.All(g=>g.Requirements.Single(r=>r.ItemId==BA).Missing==0),"재사용 설계도 중복 소모");
            var missing=FarmingPlanner.Evaluate(c,inventory with{Quantities=new(){{R,4}},Credits=500},goals);Yes(missing.Credits==120&&missing.Missing[BA]==1,"재사용 설계도 구매비 중복");
        });
        check("목표 예약: 미숙련 장비 소모 경고",()=>
        {var c=catalog with{Recipes=[catalog.Recipes[0],catalog.Recipes[1] with{Ingredients=[new(A,1)]}]};var p=FarmingPlanner.Evaluate(c,inventory with{Quantities=new(){{A,1},{BB,1}}},new(1,[new(B)]));Yes(p.Goals[0].Requirements.Any(r=>r.ConsumesUnmastered)&&p.Goals[0].Warnings.Any(w=>w.Contains("숙련")),"소모 전 숙련 경고 누락");});
        check("목표 예약: 부분 인벤토리에서 판매 여유 미확정",()=>Yes(!Plan(inventory with{InventoryComplete=false}).InventoryConfirmed,"부분 목록 완전 판정"));
        check("목표 형식: 중복·잘못된 수량·미등록 ID 거부",()=>
        {foreach(var d in new[]{new FarmingGoalsDocument(1,[new(A),new(A)]),new(1,[new(A,0)]),new(1,[new("unknown")])}){bool failed=false;try{d.Validate(catalog);}catch(ArgumentException){failed=true;}Yes(failed,"잘못된 목표 허용");}});
        check("목표 예약: 저장 형식 왕복",()=>{var decoded=JsonSerializer.Deserialize<FarmingGoalsDocument>(JsonSerializer.Serialize(goals,Catalog.Json),Catalog.Json)!;Yes(Plan(document:decoded).Reserved[R]==3,"목표 복원");});
        check("목표 보상: 부족 부품·수량·목표 연결",()=>{var h=FarmingRewards.Hint(catalog,inventory,Plan(),R);Yes(h is {Highlight:true,Needed:1}&&h.Detail.Contains("Beta"),"목표 힌트");});
        check("목표 보상: 이미 예약된 품목은 부족으로 표시하지 않음",()=>{var p=Plan(document:new(1,[new(A)]));var h=FarmingRewards.Hint(catalog,inventory,p,BA);Yes(h is {Highlight:false,Needed:0},"중복 파밍 강조");});
        check("목표 마스터리: 다른 목표의 예약·자기 예약 구분",()=>
        {var p=Plan(document:new(1,[new(A)]));var recommendations=MasteryPlanner.Recommend(catalog,inventory,reservations:p);Yes(recommendations.Single(x=>x.Item.Id==A).Requirements.Single(r=>r.Id==R).Missing==0,"자기 예약 사용 불가");Yes(recommendations.Single(x=>x.Item.Id==B).Requirements.Single(r=>r.Id==R).Missing==1,"다른 목표 예약 사용");});
        var now=DateTimeOffset.Parse("2026-10-26T00:00:00Z");
        var f=new FissureMission("goal-test","시험 노드","MT_CAPTURE",1,true,false,now.AddMinutes(-1),now.AddHours(1));
        var schedule=ScheduleCatalog.Load();var data=new WorldScheduleData(WorldScheduleChecks.Snapshot(now) with{Fissures=[f],FissuresUtc=now},ArbitrationSchedule.Bundled(schedule),false,null,now);
        check("목표 미션: 보유 성유물·부족 보상·등급 교집합",()=>
        {var match=FarmingMissions.Match(catalog,inventory,Plan(),[f,f with{Id="wrong",Tier=3}],now);Yes(match.Length==1&&match[0].GoalIds.Contains(B),"목표/등급 연결");Yes(FarmingMissions.Match(catalog,inventory with{Quantities=new()},Plan(),[f],now).Length==0,"없는 성유물 알림");});
        check("목표 미션: 옴니아는 1~4등급만 허용",()=>
        {Yes(FarmingMissions.Match(catalog,inventory,Plan(),[f with{Tier=6}],now).Length==1,"리스 옴니아 제외");var c=catalog with{Relics=[catalog.Relics[0] with{Family="/Lotus/Test/T5Relic"}]};Yes(FarmingMissions.Match(c,inventory,Plan(),[f with{Tier=6}],now).Length==0,"레퀴엠 옴니아 허용");});
        check("목표 알림: 오래된 자료·중복·미션 필터",()=>
        {var p=Plan();Yes(FarmingMissions.Due(catalog,inventory,p,data,now,new HashSet<string>()).Length==1,"신선한 목표 알림");Yes(FarmingMissions.Due(catalog,inventory,p,data with{Cached=true},now,new HashSet<string>()).Length==0,"캐시 알림");Yes(FarmingMissions.Due(catalog,inventory,p,data,now,new HashSet<string>{"fissure:"+f.Id}).Length==0,"수동 규칙과 중복");Yes(FarmingMissions.Due(catalog,inventory,p,data,now,new HashSet<string>(),_=>false).Length==0,"미션 필터 무시");});
        check("목표 알림: 알림 해제해도 보상·일정 연결 유지",()=>
        {var p=Plan(document:new(1,[new(A,Notify:false),new(B,Notify:false)]));Yes(FarmingMissions.Match(catalog,inventory,p,[f],now).Length==1,"알림 해제 시 정보 삭제");Yes(FarmingMissions.Due(catalog,inventory,p,data,now,new HashSet<string>()).Length==0,"해제한 알림 실행");});
        RelicItem Part(string id)=>new(id,id,id,id,15);
        PlannerPrice Price(double n,bool stale=false)=>new(n,now,stale);
        FarmingBundle Bundle(string id,long a=1,long b=1)=>new(A,Part(id),[new(A,a,Part(A)),new(B,b,Part(B))]);
        FarmingPurchaseComparison Buy(Dictionary<string,long> demand,Dictionary<string,PlannerPrice> quotes,params FarmingBundle[] bundles)=>FarmingPurchases.Compare(demand,quotes,bundles,Part);
        check("목표 구매: 세트와 낱개 현금 비용 비교",()=>{var p=Buy(new(){{A,1},{B,1}},new(){{A,Price(7)},{B,Price(7)},{"set",Price(10)}},Bundle("set"));Yes(p.PartsCost==14&&p.BestCost==10&&p.Savings==4&&p.Lines.Single().Bundle,"세트 비교");});
        check("목표 구매: 이미 보유한 부품 때문에 낱개가 유리",()=>{var p=Buy(new(){{B,1}},new(){{A,Price(7)},{B,Price(7)},{"set",Price(10)}},Bundle("set"));Yes(p.BestCost==7&&!p.Lines.Single().Bundle,"보유 부품 무시");});
        check("목표 구매: 여분 판매 수익을 빼지 않음",()=>{var p=Buy(new(){{B,1}},new(){{A,Price(50)},{B,Price(12)},{"set",Price(10)}},Bundle("set"));Yes(p.BestCost==10&&p.ExtraItems.Single()==new RecipeIngredient(A,1),"여분 수익 또는 수량 오류");});
        check("목표 구매: 한 세트에 같은 부품 여러 개",()=>{var p=Buy(new(){{A,3},{B,1}},new(){{A,Price(8)},{B,Price(3)},{"set",Price(17)}},Bundle("set",2));Yes(p.PartsCost==27&&p.BestCost==25,"세트 구성 수량");});
        check("목표 구매: 여러 세트의 겹치는 부품 최적 조합",()=>{string C="/Lotus/Test/C";var bc=new FarmingBundle(B,Part("bc"),[new(B,1,Part(B)),new(C,1,Part(C))]);var p=Buy(new(){{A,1},{B,1},{C,1}},new(){{A,Price(4)},{B,Price(4)},{C,Price(4)},{"set",Price(5)},{"bc",Price(5)}},Bundle("set"),bc);Yes(p.BestCost==9,"겹치는 세트 조합");});
        check("목표 구매: 호가 누락은 무료가 아님",()=>{var p=Buy(new(){{A,1},{B,1}},new(){{A,Price(7)}});Yes(p.BestCost is null&&p.PartsCost is null&&p.MissingPriceIds.Contains(B),"누락 호가 무료 처리");var set=Buy(new(){{A,1},{B,1}},new(){{"set",Price(10)}},Bundle("set"));Yes(set.BestCost==10&&set.PartsCost is null,"세트로 확보할 수 있는 누락 낱개 호가");});
        check("목표 구매: 선택한 경로의 오래된 가격 표시",()=>{var p=Buy(new(){{B,1}},new(){{B,Price(7,true)},{"set",Price(10)}},Bundle("set"));Yes(p.Stale,"가격 신선도");});
        check("목표 구매: 독립 완전 탐색과 무작위 50사례 대조",()=>
        {
            var random=new Random(261026);string C="/Lotus/Test/C";
            for(int n=0;n<50;n++)
            {
                int a=random.Next(1,4),b=random.Next(1,4),c=random.Next(1,4),pa=random.Next(3,16),pb=random.Next(3,16),pc=random.Next(3,16),ab=random.Next(2,26),bc=random.Next(2,26);
                decimal oracle=decimal.MaxValue;
                for(int x=0;x<=Math.Max(a,b);x++)for(int y=0;y<=Math.Max(b,c);y++)oracle=Math.Min(oracle,x*ab+y*bc+Math.Max(0,a-x)*pa+Math.Max(0,b-x-y)*pb+Math.Max(0,c-y)*pc);
                var p=Buy(new(){{A,a},{B,b},{C,c}},new(){{A,Price(pa)},{B,Price(pb)},{C,Price(pc)},{"ab",Price(ab)},{"bc",Price(bc)}},Bundle("ab"),new(B,Part("bc"),[new(B,1,Part(B)),new(C,1,Part(C))]));
                Yes(p.BestCost==oracle,$"독립 최적 비용 {n}: {p.BestCost}/{oracle}");
            }
        });
        check("목표 구매: 취소를 받아들임",()=>{using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{FarmingPurchases.Compare(new Dictionary<string,long>{{A,1}},new Dictionary<string,PlannerPrice>{{A,Price(1)}},[],Part,stop.Token);}catch(OperationCanceledException){cancelled=true;}Yes(cancelled,"취소 무시");});
        check("세트 원본: 실제 ID·구성 수량·불완전한 세트 구분",()=>
        {var c=InventoryCatalog.Load();var ids=c.Items.Select(i=>i.Id).ToHashSet();var d=FarmingMarket.Document;Yes(d.Bundles.Length>=200&&d.Bundles.All(b=>ids.Contains(b.ResultId)&&b.Parts.All(p=>ids.Contains(p.GameId)&&p.Count>0)),"원본 연결");Yes(d.Bundles.Single(b=>b.Item.Slug=="akstiletto_prime_set").Parts.Any(p=>p.Count==2),"쌍권총 중복 부품");Yes(d.Bundles.Single(b=>b.Item.Slug=="aklex_prime_set").Parts.Length==2,"아클렉스 세트에 렉스 두 자루를 임의 추가");});
    }
}
