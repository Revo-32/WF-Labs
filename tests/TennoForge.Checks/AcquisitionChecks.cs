using TennoForge.Core;

static class AcquisitionChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool ok,string why){if(!ok)throw new Exception(why);}
        var c=InventoryCatalog.Load();var data=InventoryAcquisition.Document;
        InventoryItem Item(string en)=>c.Items.First(i=>i.EnglishName==en&&i.Masterable);
        AcquisitionEntry Entry(string en)=>InventoryAcquisition.Find(Item(en).Id)!;
        AcquisitionEntry Blueprint(string en)=>InventoryAcquisition.Find(c.Recipes.Single(r=>r.ResultId==Item(en).Id).BlueprintId)!;
        check("획득처: 809개 마스터리 장비 연결·고정 개정과 원문 해시",()=>
        {
            Yes(c.Items.Count(i=>i.Masterable)==809&&c.Items.Where(i=>i.Masterable).All(i=>InventoryAcquisition.Find(i.Id) is {Routes.Length:>0}),"장비 획득처 누락");
            Yes(data.Sources.Length>=800&&data.Sources.All(s=>s.Revision>0&&s.Sha256.Length==64&&s.Url.Contains("oldid=")),"원본 추적");
            Yes(data.Entries.Select(e=>e.Id).Distinct().Count()==data.Entries.Length&&data.Entries.All(e=>e.Routes.All(r=>r.Source.StartsWith("https://")&&r.Costs.All(v=>v.Amount>0)&&r.Chance is null or >=0 and <=100)),"가격·확률·출처 무결성");
        });
        check("획득처: 라이노 본체 상점35,000·부품은 자칼 Fossa",()=>
        {
            Yes(Blueprint("Rhino").Routes.Any(r=>r.Kind=="market"&&r.Costs.Any(p=>p.Currency=="크레딧"&&p.Amount==35000)),"본체 가격");
            var part=InventoryAcquisition.Find("/Lotus/Types/Recipes/WarframeRecipes/RhinoHelmetBlueprint")!;
            Yes(!part.Routes.Any(r=>r.Kind=="market")&&part.Routes.Any(r=>r.Detail.Contains("자칼")&&r.Detail.Contains("Fossa")),"부품을 상점 판매로 오인");
        });
        check("획득처: 클랜 연구실과 복제비 · 다크 스플릿 소드15,000",()=>
        {Yes(Blueprint("Hema").Routes.Any(r=>r.Location.Contains("바이오 연구소")),"바이오 연구소");Yes(Blueprint("Dark Split-Sword").BlueprintCredits==15000&&Entry("Dark Split-Sword").BuiltPlatinum==225,"설계도·완성품 비용 분리");});
        check("획득처: Cyte09 본체50,000평판·부품20,000평판",()=>
        {
            var main=Blueprint("Cyte-09");Yes(main.Routes.Any(r=>r.Costs.Any(p=>p.Currency=="평판"&&p.Amount==50000)&&r.Condition.Contains("4")),"본체 평판");
            var recipe=c.Recipes.Single(r=>r.ResultId==Item("Cyte-09").Id);var part=c.Recipes.First(r=>recipe.Ingredients.Any(i=>i.Id==r.ResultId)&&c.Items.Single(i=>i.Id==r.ResultId).Category=="Components");
            Yes(InventoryAcquisition.Find(part.BlueprintId)!.Routes.Any(r=>r.Costs.Any(p=>p.Currency=="평판"&&p.Amount==20000)&&r.Condition.Contains("2")),"부품 평판");
        });
        check("획득처: 단테270모세혈관·네크라메크 평판은 크레딧이 아님",()=>
        {
            Yes(Blueprint("Dante").Routes.Any(r=>r.Costs.Any(p=>p.Amount==270&&p.Currency.Contains("모세혈관"))),"단테 교환");
            Yes(Blueprint("Bonewidow").BlueprintCredits is null&&Blueprint("Bonewidow").Routes.Any(r=>r.Costs.Any(v=>v.Currency=="평판"&&v.Amount==10000)),"본위도우 교환 재화");
            Yes(Blueprint("Voidrig").BlueprintCredits is null&&Blueprint("Voidrig").Routes.Any(r=>r.Kind=="quest")&&Blueprint("Voidrig").Routes.Any(r=>r.Costs.Any(v=>v.Amount==5000&&v.Currency=="평판")),"보이드리그 무료 퀘스트·평판 대체");
        });
        check("획득처: 나이트웨이브·바로의 복합 가격과 판매 회차",()=>
        {
            Yes(Blueprint("Dark Sword").Routes.Any(r=>r.Costs.Any(v=>v.Currency=="나이트웨이브 크레드"&&v.Amount==50)),"크레드");
            Yes(Entry("Prisma Grakata").Routes.Any(r=>r.Costs.Any(v=>v.Currency=="크레딧"&&v.Amount==100000)&&r.Costs.Any(v=>v.Currency=="두캇"&&v.Amount==610)&&r.Condition.Contains("방문")),"바로 복합 비용");
            Yes(InventoryAcquisition.AcquisitionOrder(Item("Prisma Grakata").Id)!=0,"두캇도 필요한 무기를 크레딧만 구매로 추천");
        });
        check("획득처: 데트론 상점 판매 금지·쿠브로의 인큐베이터 전제 분리",()=>
        {Yes(!Entry("Detron").Routes.Any(r=>r.Kind=="market"),"판매하지 않는다는 문장을 판매 근거로 사용");Yes(Entry("Chesa Kubrow").Routes.All(r=>r.Kind=="companion"),"인큐베이터 부품 상점을 쿠브로 완성품 판매로 오인");});
        check("구매 시세: 프라임 외 프리즈마·신디케이트도 gameRef로 연결",()=>
        {
            Yes(InventoryMarket.Document.Entries.Length>1500&&InventoryMarket.Document.SourceSha256.Length==64,"전체 거래 메타데이터");
            foreach(var name in new[]{"Prisma Grakata","Rakta Cernos"})
            {var item=Item(name);var market=InventoryMarket.Find(item.Id);Yes(market is not null&&market.Id==item.MarketId&&item.Tradable&&!market.Slug.EndsWith("_set"),name+" 거래 ID 연결");}
        });
        check("구매 시세: 프라임 세트와 제작된 완성 무기를 혼동하지 않음",()=>
        {
            Yes(InventoryMarket.Find(Item("Braton Prime").Id) is null&&Item("Braton Prime").MarketId is null,"거래 불가 완성품에 세트 호가 적용");
            Yes(InventoryMarket.Document.Entries.Select(e=>e.GameId).Distinct().Count()==InventoryMarket.Document.Entries.Length&&!InventoryMarket.Items.Any(i=>i.Slug.EndsWith("_set")),"세트·중복 gameRef");
        });
        var s=new InventorySnapshot(1,DateTimeOffset.UtcNow,"시험",40,10_000_000,new(),new(),[],[],true,true,[]);
        check("추천: 라토·브래튼은 완성품 크레딧 구매·제작식/설계도 요구 없음",()=>
        {
            foreach(var pair in new[]{("Lato",10000L),("Braton",25000L)})
            {var p=MasteryPlanner.Recommend(c,s).Single(p=>p.Item.Id==Item(pair.Item1).Id);Yes(p.DirectPurchaseCredits==pair.Item2&&p.Credits==pair.Item2&&p.BuildSeconds==0&&p.BlueprintPurchaseCredits==0&&p.Requirements.Length==1&&p.Requirements[0].Id==p.Item.Id,"직접 구매를 제작으로 오인");}
        });
        check("추천: 크레딧→미션→플래티넘 순서와 보유·제작중 우선",()=>
        {
            var rows=MasteryPlanner.Recommend(c,s);Yes(rows.All(p=>p.Priority>=2)&&rows.Select(p=>p.Priority).SequenceEqual(rows.Select(p=>p.Priority).Order()),"획득 경로 정렬");
            var owned=Item("Detron");var next=s with{Quantities=new(){{owned.Id,1}},Equipment=[new(owned.Id,0,0,null,[])]};rows=MasteryPlanner.Recommend(c,next);Yes(rows[0].Item.Id==owned.Id&&rows[0].AcquisitionLabel.Contains("보유"),"보유 장비 먼저");
            var locked=MasteryPlanner.Recommend(c,s with{MasteryRank=0});Yes(!locked.TakeWhile(p=>p.Priority<6).Any(p=>p.RankLocked),"MR 제한 후보 정렬");
        });
        check("추천: 부족한 뉴로드는 자원 수집 · 미보유 자원 설계도 구매 강제 안 함",()=>
        {
            var rhino=Item("Rhino");var plan=MasteryPlanner.Recommend(c,s).Single(p=>p.Item.Id==rhino.Id);
            var neurodes=c.Items.First(i=>i.EnglishName=="Neurodes");Yes(plan.Requirements.Any(r=>r.Id==neurodes.Id&&r.Missing>0),"뉴로드 자원 누락");
            Yes(!plan.Requirements.Any(r=>r.Id.EndsWith("NeurodeBlueprint")||r.Id.Contains("ControlModuleResourceBlueprint")),"내부 자원 제작식으로 과잉 전개");
            Yes(plan.BlueprintPurchaseCredits==35000&&plan.Credits>plan.BlueprintPurchaseCredits,"설계도 비용·제작비 분리");
            Yes(InventoryAcquisition.Find(neurodes.Id)!.Routes.Any(r=>r.Kind=="resource"&&r.Location.Contains("지구")&&r.Location.Contains("데이모스")),"재료 수집 장소 미연결");
        });
        check("동시 제작: 공통 재료·크레딧 중복 사용 금지와 병렬 최대 시간",()=>
        {
            InventoryItem Gear(string id)=>new(id,id,id,"Melee",true,100,30,0,false,false,null,null);
            var a=Gear("/a");var b=Gear("/b");var mat=new InventoryItem("/mat","재료","Material","Misc",false,0,0,0,false,false,null,null);
            var custom=new InventoryCatalog(1,DateTimeOffset.UtcNow,[a,b,mat],[new("/abp",a.Id,100,60,1,true,[new(mat.Id,5)]),new("/bbp",b.Id,200,90,1,true,[new(mat.Id,5)])],[]);
            var sample=s with{Quantities=new(){{"/abp",1},{"/bbp",1},{"/mat",5}}};var batch=MasteryBatchPlanner.Recommend(custom,sample);
            Yes(batch.Plans.Length==1&&batch.Credits==100&&batch.MasteryPoints==3000,"같은 재료를 두 번 사용");
            batch=MasteryBatchPlanner.Recommend(custom,sample with{Quantities=new(){{"/abp",1},{"/bbp",1},{"/mat",10}}});
            Yes(batch.Plans.Length==2&&batch.Credits==300&&batch.ParallelBuildSeconds==90,"독립 제작시간을 합산");
            batch=MasteryBatchPlanner.Recommend(custom,sample with{Credits=50});Yes(batch.Plans.Length==0,"크레딧을 초과하는 배치");
            batch=MasteryBatchPlanner.Recommend(custom,sample with{InventoryComplete=false});Yes(batch.Plans.Length==0,"부분 목록으로 제작 가능 배치 확정");
        });
        check("추천: 제작 대기시간보다 추가 재화 효율을 우선",()=>
        {
            InventoryItem Gear(string id)=>new(id,id,id,"Melee",true,100,30,0,false,false,null,null);
            var custom=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Gear("/cheap"),Gear("/fast")],[new("/cheapbp","/cheap",100,86400,1,true,[]),new("/fastbp","/fast",1000,60,1,true,[])],[]);
            var rows=MasteryPlanner.Recommend(custom,s with{Quantities=new(){{"/cheapbp",1},{"/fastbp",1}}});
            Yes(rows[0].Item.Id=="/cheap"&&rows[0].BuildSeconds>rows[1].BuildSeconds,"빠른 제작만으로 비싼 장비를 먼저 추천");
        });
        check("추천: 추가 포르마 없이는 점수가 없는 보유 장비를 뒤로 배치",()=>
        {
            var weapon=c.Items.First(i=>i.Masterable&&i.MaxRank==40);var sample=s with{Quantities=new(){{weapon.Id,1}},Equipment=[new(weapon.Id,30,0,null,[])],MasteryRanks=new(){{weapon.Id,30}}};
            var rows=MasteryPlanner.Recommend(c,sample);var owned=rows.Single(p=>p.Item.Id==weapon.Id);Yes(owned.AvailablePoints==0&&owned.Priority>rows.First().Priority,"포르마 지출 없이 점수를 얻을 수 없는 후보를 최우선 추천");
        });
        check("동시 제작: 공통 중간 부품은 순차 제작하고 다른 장비는 병렬 처리",()=>
        {
            InventoryItem Gear(string id,bool masterable)=>new(id,id,id,masterable?"Melee":"Components",masterable,masterable?100:0,masterable?30:0,0,false,false,null,null);
            var custom=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Gear("/a",true),Gear("/b",true),Gear("/part",false)],[new("/abp","/a",100,60,1,true,[new("/part",1)]),new("/bbp","/b",200,90,1,true,[new("/part",1)]),new("/partbp","/part",0,30,1,true,[])],[]);
            var sample=s with{Quantities=new(){{"/abp",1},{"/bbp",1},{"/partbp",2}}};var batch=MasteryBatchPlanner.Recommend(custom,sample);
            Yes(batch.Plans.Length==2&&batch.ParallelBuildSeconds==150,"공통 부품30+30초 뒤 두번째 장비90초 · 첫 장비와 병렬");
        });
        check("동시 제작: 보유 중간 부품을 일부만 쓸 때도 실제 재고 예약",()=>
        {
            InventoryItem Gear(string id,bool masterable)=>new(id,id,id,masterable?"Melee":"Components",masterable,masterable?100:0,masterable?30:0,0,false,false,null,null);
            var custom=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Gear("/a",true),Gear("/b",true),Gear("/part",false)],[new("/abp","/a",100,60,1,true,[new("/part",2)]),new("/bbp","/b",200,90,1,true,[new("/part",2)]),new("/partbp","/part",0,30,1,true,[])],[]);
            var sample=s with{Quantities=new(){{"/abp",1},{"/bbp",1},{"/partbp",1},{"/part",1}}};var batch=MasteryBatchPlanner.Recommend(custom,sample);
            Yes(batch.Plans.Length==1&&batch.Plans[0].Requirements.Any(r=>r.Id=="/part"&&r.Owned==1),"보유 부품 일부와 새 제작을 섞을 때 보유량 미예약");
        });
        check("제작 시간: 묶음 제작의 남은 생산품도 완료 시점을 기다림",()=>
        {
            InventoryItem Gear(string id,bool masterable)=>new(id,id,id,masterable?"Melee":"Components",masterable,masterable?100:0,masterable?30:0,0,false,false,null,null);
            var custom=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Gear("/goal",true),Gear("/slow",false),Gear("/fast",false),Gear("/part",false)],[new("/goalbp","/goal",0,0,1,true,[new("/fast",1),new("/slow",1)]),new("/fastbp","/fast",0,1,1,true,[new("/part",1)]),new("/slowbp","/slow",0,100,1,true,[new("/part",1)]),new("/partbp","/part",0,10,2,true,[])],[]);
            var sample=s with{Quantities=new(){{"/goalbp",1},{"/fastbp",1},{"/slowbp",1},{"/partbp",1}}};var plan=MasteryPlanner.Recommend(custom,sample).Single();
            Yes(plan.BuildSeconds==110&&!plan.Requirements.Any(r=>r.Id=="/part"&&r.Owned>0),"묶음 생산품을 즉시 보유한 것처럼 사용");
        });
    }
}
