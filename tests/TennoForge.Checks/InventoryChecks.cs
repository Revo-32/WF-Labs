using System.Text;
using System.Text.Json;
using TennoForge.Core;

static class InventoryChecks
{
    internal static string Fixture(InventoryCatalog c)
    {
        var braton=c.Items.Single(i=>i.EnglishName=="Braton Prime");var rhino=c.Items.Single(i=>i.EnglishName=="Rhino");var recipe=c.Recipes.Single(r=>r.ResultId==braton.Id);var relic=c.Relics.First(r=>r.Refinement==0&&r.Rewards.Any(s=>s.Id==recipe.BlueprintId));
        return JsonSerializer.Serialize(new{PlayerLevel=20,RegularCredits=1000000,Suits=new[]{new{ItemType=rhino.Id,XP=100000}},LongGuns=new[]{new{ItemType=braton.Id,XP=0,Polarized=1}},Pistols=Array.Empty<object>(),Melee=Array.Empty<object>(),Recipes=new[]{new{ItemType=recipe.BlueprintId,ItemCount=1}},MiscItems=recipe.Ingredients.Select(i=>new{ItemType=i.Id,ItemCount=i.Count}).Append(new{ItemType=relic.Id,ItemCount=8L}),XPInfo=new[]{new{ItemType=braton.Id,XP=450000},new{ItemType=rhino.Id,XP=100000}},PendingRecipes=Array.Empty<object>(),Nonce="MUST-NOT-PERSIST",AccountId="MUST-NOT-PERSIST"});
    }
    internal static void Run(Action<string,Action> check)
    {
        AcquisitionChecks.Run(check);
        AcquisitionGroupingChecks.Run(check);
        DashboardChecks.Run(check);
        void Yes(bool b,string message){if(!b)throw new Exception(message);}
        var c=InventoryCatalog.Load();var json=Fixture(c);var snapshot=InventoryImport.Parse(json,c);var braton=c.Items.Single(i=>i.EnglishName=="Braton Prime");
        check("인벤토리: 공식 한국어·제작식·6보상 성유물 데이터 무결성",()=>
        {
            Yes(c.Items.Count(i=>i.Masterable)>700&&c.Recipes.Length>1400&&c.Relics.Length==3200,"카탈로그 누락");
            var ids=c.Items.Select(i=>i.Id).ToHashSet();Yes(ids.Count==c.Items.Length,"중복 내부 ID");Yes(c.Relics.All(r=>r.Rewards.All(s=>ids.Contains(s.Id))),"보상 ID 미연결");
            Yes(c.Items.Where(i=>i.Masterable).All(i=>(i.Name.Any(ch=>ch>='가'&&ch<='힣')||i.Name is "AX-52" or "Cyte-09")&&i.PointsPerRank is 100 or 200&&i.MaxRank is 30 or 40),"한국어/랭크 규칙 누락");
        });
        check("인벤토리: 포르마 현재 랭크0과 과거 최고30 분리",()=>{Yes(snapshot.InventoryComplete&&snapshot.MasteryComplete,"완전성");Yes(snapshot.MasteryRanks[braton.Id]==30&&snapshot.Equipment.Single(e=>e.Id==braton.Id).Rank==0,"랭크 분리");Yes(!MasteryPlanner.Recommend(c,snapshot).Any(p=>p.Item.Id==braton.Id),"마스터한 무기 재추천");});
        check("인벤토리: 계정 인증·원문 필드가 내보내기에 남지 않음",()=>{var saved=JsonSerializer.Serialize(snapshot,Catalog.Json);Yes(!saved.Contains("MUST-NOT-PERSIST")&&!saved.Contains("Nonce")&&!saved.Contains("AccountId"),"원문 유출");});
        check("인벤토리: GEP 이벤트 문자열/객체 래퍼 동등",()=>
        {var envelope=JsonSerializer.Serialize(new{info=new{match_info=new{inventory=json}}});var a=InventoryImport.Parse(envelope,c,"GEP");Yes(a.Count(braton.Id)==snapshot.Count(braton.Id)&&a.MasteryRanks.Count==snapshot.MasteryRanks.Count,"GEP");});
        check("인벤토리: 덮어쓴 JSON 여는 헤더·공백 포함 필드 복구",()=>
        {var damaged=Encoding.UTF8.GetBytes("binary-noise\0"+json[1..].Replace("\"XPInfo\":","\"XPInfo\" \n : ")+"\0tail");var hit=InventoryJsonLocator.Find(damaged,c);Yes(hit is not null&&hit.Value.Snapshot.MasteryRank==20&&hit.Value.Snapshot.InventoryComplete&&hit.Value.Snapshot.Count(braton.Id)==snapshot.Count(braton.Id)&&hit.Value.Snapshot.Source.Contains("복구"),"손상된 앞부분 복구");});
        check("인벤토리: 미완성 배열·빠진 종료 괄호·분리된 응답 합치기 거부",()=>
        {
            Yes(InventoryJsonLocator.Find(Encoding.UTF8.GetBytes(json[1..^1]),c) is null,"종료 괄호를 만들어 수락하면 안 됨");
            var node=System.Text.Json.Nodes.JsonNode.Parse(json)!;node.AsObject().Remove("Recipes");Yes(InventoryJsonLocator.Find(Encoding.UTF8.GetBytes(node.ToJsonString()),c) is null,"빠진 필수 섹션");
            var a=System.Text.Json.Nodes.JsonNode.Parse(json)!;a.AsObject().Remove("XPInfo");var b=System.Text.Json.Nodes.JsonNode.Parse(json)!;b.AsObject().Remove("Suits");Yes(InventoryJsonLocator.Find(Encoding.UTF8.GetBytes(a.ToJsonString()+"\0"+b.ToJsonString()),c) is null,"서로 다른 조각의 품목·이력 혼합 금지");
        });
        check("인벤토리: 필드명 상수와 실제 JSON 속성 구분",()=>
        {Yes(InventoryJsonLocator.FindField("noise XPInfo \"XPInfo\" token"u8,"XPInfo"u8)<0,"필드명 문자열 오탐");Yes(InventoryJsonLocator.FindField("{\"XPInfo\" \t:[]}"u8,"XPInfo"u8)==1,"JSON 공백 필드");});
        check("인벤토리: 부분 갱신에서 기존 보유 섹션 소실 방지",()=>
        {var next=snapshot with{Sections=snapshot.Sections.Where(s=>s!="MiscItems").ToArray()};Yes(InventoryImport.MissingUpdateSections(snapshot,next).Contains("MiscItems"),"보유 목록이 일부 빠진 갱신");Yes(InventoryImport.MissingUpdateSections(InventorySnapshot.Empty,snapshot).Length==0&&InventoryImport.MissingUpdateSections(snapshot,snapshot).Length==0,"정상 초기/전체 갱신");});
        check("인벤토리: 마스터리 이력 없는 부분 가져오기 추천 금지",()=>{var p=InventoryImport.Parse("{\"MiscItems\":[]}",c);Yes(!p.MasteryComplete&&!p.InventoryComplete&&MasteryPlanner.Recommend(c,p).Length==0,"이력 미확인을 미획득으로 취급");});
        check("인벤토리: 음수 수량/손상 스키마 거부",()=>
        {foreach(var bad in new[]{"{\"MiscItems\":[{\"ItemType\":\"/Lotus/X\",\"ItemCount\":-1}]}","{\"schemaVersion\":99}"}){bool rejected=false;try{InventoryImport.Parse(bad,c);}catch(Exception e)when(e is InvalidDataException or JsonException){rejected=true;}Yes(rejected,"잘못된 목록 수락");}});
        check("인벤토리: 완전한 JSON 위치·중괄호가 든 문자열·잘린 응답 거부",()=>
        {
            var content=json.Replace("MUST-NOT-PERSIST","text { } \\\"text\\\"");var bytes=Encoding.UTF8.GetBytes("binary-noise\0"+content+"\0tail");
            var found=InventoryJsonLocator.Find(bytes,c);Yes(found is not null&&found.Value.Start==13&&found.Value.Snapshot.MasteryComplete,"객체 위치");
            Yes(InventoryJsonLocator.Find(bytes.AsSpan(0,bytes.Length/2),c) is null,"부분 응답 수락");
        });
        check("인벤토리: 원자적 저장·재시작 복원",()=>
        {
            var folder=Path.Combine(Path.GetTempPath(),"WFLabs-inventory-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var path=Path.Combine(folder,"snapshot.json");InventoryImport.Save(path,snapshot);var restored=InventoryImport.Parse(File.ReadAllText(path),c);Yes(restored.Count(braton.Id)==1&&restored.MasteryRanks[braton.Id]==30,"저장 복원");File.Delete(path);Directory.Delete(folder);
        });
        check("마스터리: 40랭크·200점 비무기 규칙",()=>{Yes(c.Items.Where(i=>i.MaxRank==40).Any()&&c.Items.Where(i=>i.Category=="Warframes").All(i=>i.PointsPerRank==200),"마스터리 배수");var mech=c.Items.Single(i=>i.EnglishName=="Voidrig");Yes(mech.MaxRank*mech.PointsPerRank==8000,"네크라메크 총8000");});
        check("마스터리: 모아·하운드 헤드의 권총 분류를 마스터리 배수로 사용하지 않음",()=>{var pets=c.Items.Where(i=>i.Masterable&&i.Category=="Pets").ToArray();Yes(pets.Length>20&&pets.All(i=>i.PointsPerRank==200),"동반자 200점");Yes(pets.Where(i=>i.EnglishName.Contains("Moa")||i.EnglishName.Contains("Hound")||i.EnglishName.Contains("Vulpaphyla")||i.EnglishName.Contains("Predasite")).All(i=>i.GildRequired),"도금 필요");});
        check("마스터리: 제작 준비·부품 구매·재료 누락·MR제한",()=>
        {
            var s=snapshot with{Quantities=new(snapshot.Quantities),MasteryRanks=new(snapshot.MasteryRanks)};s.Quantities.Remove(braton.Id);s.MasteryRanks.Remove(braton.Id);
            var plan=MasteryPlanner.Recommend(c,s).Single(p=>p.Item.Id==braton.Id);Yes(plan.Ready&&plan.Credits==15000&&plan.RemainingPoints==3000,"제작 준비");
            var recipe=c.Recipes.Single(r=>r.ResultId==braton.Id);string part=recipe.Ingredients.First(i=>i.Id.Contains("Barrel")).Id;s.Quantities.Remove(part);
            var price=new Dictionary<string,PlannerPrice>{{part,new(4,DateTimeOffset.UtcNow,false)}};plan=MasteryPlanner.Recommend(c,s,price).Single(p=>p.Item.Id==braton.Id);Yes(plan.BuyPlatinum==4&&!plan.Ready,"구매 총비용");
            s.Quantities.Remove(recipe.Ingredients.First(i=>i.Id.Contains("OrokinCell")).Id);plan=MasteryPlanner.Recommend(c,s,price).Single(p=>p.Item.Id==braton.Id);Yes(plan.BuyPlatinum is null,"거래불가 재료를 무료 처리");
            plan=MasteryPlanner.Recommend(c,s with{MasteryRank=0}).Single(p=>p.Item.Id==braton.Id);Yes(plan.RankLocked,"MR 제한");
        });
        check("마스터리: 크레딧 설계도도 구입 전에는 보유·제작 준비로 취급하지 않음",()=>
        {
            var weapon=c.Items.First(i=>i.Masterable&&i.BlueprintCredits>0&&c.Recipes.Any(r=>r.ResultId==i.Id));var recipe=c.Recipes.First(r=>r.ResultId==weapon.Id);
            var stock=recipe.Ingredients.ToDictionary(i=>i.Id,i=>i.Count);var s=snapshot with{Quantities=stock,Equipment=[],MasteryRanks=new()};
            var plan=MasteryPlanner.Recommend(c,s).Single(p=>p.Item.Id==weapon.Id);
            Yes(!plan.Ready&&plan.Requirements.Any(r=>r.CreditBlueprint&&r.Missing>0)&&plan.Warnings.Any(w=>w.Contains("구매처")),"설계도 획득 제한");
        });
        check("마스터리: 40랭크 잠재 점수와 현재 열린 랭크의 점수 분리",()=>
        {
            var weapon=c.Items.First(i=>i.Masterable&&i.MaxRank==40&&i.PointsPerRank==100);var s=snapshot with{Quantities=new(){{weapon.Id,1}},Equipment=[new(weapon.Id,30,3,null,[])],MasteryRanks=new(){{weapon.Id,30}}};
            var p=MasteryPlanner.Recommend(c,s).Single(p=>p.Item.Id==weapon.Id);Yes(p.RemainingPoints==1000&&p.AvailablePoints==600&&p.AdditionalForma==2,"40랭크 비용·해금");
            p=MasteryPlanner.Recommend(c,s with{MasteryRank=0}).Single(p=>p.Item.Id==weapon.Id);Yes(!p.RankLocked,"이미 보유한 무기에 획득 MR을 랭작 제한으로 적용");
        });
        check("성유물: 등급별 확률합·4인 빛작 희귀34.39%",()=>
        {
            for(int q=0;q<4;q++){double sum=3*InventoryRelicPlanner.Probability("COMMON",q)+2*InventoryRelicPlanner.Probability("UNCOMMON",q)+InventoryRelicPlanner.Probability("RARE",q);Yes(Math.Abs(sum-1)<1e-10,"확률합");}
            Yes(Math.Abs((1-Math.Pow(.9,4))-.3439)<1e-10,"희귀 확률");
            Yes(Math.Abs(InventoryRelicPlanner.ExpectedBest(new[]{(0d,.9),(100d,.1)},4)-34.39)<1e-9,"선택 보상 기댓값을 네 보상 합으로 취급");
        });
        check("성유물: 정련 비용·시세 누락·잔영 부족 구분",()=>
        {
            var r=c.Relics.First(r=>r.Refinement==1&&!r.Family.Contains("/T5"));var s=snapshot with{Quantities=new(){{r.Id,2}}};var target=r.Rewards.Single(x=>x.Rarity=="RARE").Id;
            var plans=InventoryRelicPlanner.Recommend(c,s,new Dictionary<string,PlannerPrice>(),new Dictionary<string,int>(),new HashSet<string>{target},4,50);
            var radiant=plans.Single(p=>p.Relic.Refinement==3);Yes(radiant.TraceCost==100&&!radiant.Affordable&&radiant.ExpectedPlatinum is null&&Math.Abs(radiant.TargetChance-.3439)<1e-10,"정련/누락");Yes(Math.Abs(radiant.ChanceWithinOwned-(1-Math.Pow(.9,8)))<1e-10,"보유 확률");
        });
        check("성유물: 포르마 거래불가 0P와 거래 부품 시세 미확인 구분",()=>
        {
            var items=c.Items.ToDictionary(i=>i.Id);var relic=c.Relics.First(r=>r.Refinement==0&&!r.Family.Contains("/T5")&&r.Rewards.Any(s=>items[s.Id].Tradable==false));
            var prices=relic.Rewards.Where(s=>items[s.Id].Tradable).DistinctBy(s=>s.Id).ToDictionary(s=>s.Id,_=>new PlannerPrice(10,DateTimeOffset.UtcNow,false));var s=snapshot with{Quantities=new(){{relic.Id,1}}};
            var p=InventoryRelicPlanner.Recommend(c,s,prices,new Dictionary<string,int>()).Single(p=>p.Relic.Refinement==0);Yes(p.ExpectedPlatinum.HasValue,"거래불가 보상을 시세 누락으로 취급");prices.Remove(prices.Keys.First());p=InventoryRelicPlanner.Recommend(c,s,prices,new Dictionary<string,int>()).Single(p=>p.Relic.Refinement==0);Yes(p.ExpectedPlatinum is null,"누락된 거래 가격을 0P 취급");
        });
    }
}

