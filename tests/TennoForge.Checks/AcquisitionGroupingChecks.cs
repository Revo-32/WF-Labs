using TennoForge.Core;

static class AcquisitionGroupingChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=InventoryCatalog.Load();var groups=new MasteryAcquisitionGroups(catalog);
        InventoryItem Item(string name)=>catalog.Items.Single(i=>i.EnglishName==name);
        foreach(var name in new[]{"Rhino","Excalibur","Braton Prime","Equinox","Oberon"})check("획득 탭: "+name+" · 부품 파밍을 본체 크레딧 구매로 오분류하지 않음",()=>
        {if(groups.For(Item(name).Id).Group!=MasteryAcquisitionGroup.Farming)throw new Exception(name+" 파밍 분류 실패");});
        foreach(var name in new[]{"Banshee","Volt","Ignis"})check("획득 탭: "+name+" · 도장 설계도",()=>
        {var result=groups.For(Item(name).Id);if(result.Group!=MasteryAcquisitionGroup.Credits||!result.Dojo)throw new Exception(name+" 도장 분류 실패");});
        foreach(var name in new[]{"Paris","Braton"})check("획득 탭: "+name+" · 일반 자원 부족은 별도 파밍 장비로 오분류하지 않음",()=>
        {var result=groups.For(Item(name).Id);if(result.Group!=MasteryAcquisitionGroup.Credits||!result.Shop)throw new Exception(name+" 상점 분류 실패");});
        check("획득 탭: 플래티넘 바로 구매는 파밍 경로를 숨기지 않음",()=>
        {var r=groups.For(Item("Rhino").Id);if(!r.CanBuyPlatinum||r.Group!=MasteryAcquisitionGroup.Farming)throw new Exception("프리미엄 우선 분류");});
        foreach(var name in new[]{"Rhino","Soma","Volt"})check("획득 탭: "+name+" · 게임 상점 구매를 유저 거래와 분리",()=>
        {var r=groups.For(Item(name).Id);if(!r.CanBuyPlatinum||r.CanTrade)throw new Exception("상점/거래 혼합");});
        foreach(var name in new[]{"Rhino Prime","Braton Prime","Prisma Skana"})check("획득 탭: "+name+" · 거래 가능한 부품·설계도·장비 ID",()=>
        {var r=groups.For(Item(name).Id);if(!r.CanTrade||r.CanBuyPlatinum)throw new Exception("거래 경로 누락/상점 오분류");});
        check("획득 탭: 거래 시세 유무로 게임 상점 구매 여부가 바뀌지 않음",()=>
        {
            var p=new MasteryPlan(Item("Braton Prime"),0,3000,false,false,false,[],0,0,50,0,false,[]);
            if(groups.For(p).CanBuyPlatinum||!groups.For(p).CanTrade||groups.For(p with{BuyPlatinum=null}).CanBuyPlatinum)throw new Exception("시세 의존 분류");
        });
        MasteryPlan Plan(string name,double? cost,int points=3000)=>new(Item(name),0,points,false,false,false,[],1000,3600,cost,0,false,[]){AvailablePoints=points};
        void OrderIs(MasteryPlan[] input,MasteryPlan[] expected,MasteryAcquisitionGroup group=MasteryAcquisitionGroup.Trade)
        {if(!MasteryPurchaseRecommendations.Order(input,group).SequenceEqual(expected))throw new Exception("추천 순서 불일치");}
        check("구매 추천: 기존 제작 버킷과 관계없이 13·20·36 P 순서",()=>
        {var a=Plan("Velox",13) with{AcquisitionOrder=2};var b=Plan("Corvas",20) with{AcquisitionOrder=1};var c=Plan("Onorix",36) with{AcquisitionOrder=0};OrderIs([c,b,a],[a,b,c]);});
        check("구매 추천: 총액이 아닌 실제 추가 마스터리당 지출",()=>
        {var a=Plan("Rhino Prime",20,6000);var b=Plan("Braton Prime",13);OrderIs([b,a],[a,b]);});
        check("구매 추천: 시세 미확인·부분 합계는 0 P가 아님",()=>
        {var a=Plan("Braton Prime",null);var b=Plan("Prisma Skana",30);OrderIs([a,b],[b,a]);});
        check("구매 추천: MR 제한·추가 랭크 해금은 즉시 랭작 뒤",()=>
        {var a=Plan("Rhino Prime",1) with{RankLocked=true};var b=Plan("Braton Prime",20);var c=Plan("Prisma Skana",0,0);OrderIs([c,a,b],[b,a,c]);});
        check("구매 추천: 추가 거래비 0은 확인된 0일 때만 최상단",()=>
        {var a=Plan("Braton Prime",0);var b=Plan("Prisma Skana",null);OrderIs([b,a],[a,b]);});
        check("구매 추천: 병렬 제작 대기시간은 저렴한 구매를 밀어내지 않음",()=>
        {var a=Plan("Braton Prime",13) with{BuildSeconds=259200};var b=Plan("Prisma Skana",20) with{BuildSeconds=0};OrderIs([b,a],[a,b]);});
        check("구매 추천: 상점 표준가와 거래 시세를 혼합하지 않음",()=>
        {var a=Plan("Volt",9999,6000);var b=Plan("Soma",1);OrderIs([b,a],[a,b],MasteryAcquisitionGroup.Platinum);});
        check("구매 추천: 가격 갱신 이후 재정렬",()=>
        {var a=Plan("Braton Prime",40);var b=Plan("Prisma Skana",20);OrderIs([a,b],[b,a]);a=a with{BuyPlatinum=10};OrderIs([b,a],[a,b]);});
        check("획득 탭: 보유·제작 중 우선",()=>
        {
            var item=Item("Rhino");var p=new MasteryPlan(item,0,6000,true,false,false,[],0,0,null,0,false,[]);
            if(groups.For(p).Group!=MasteryAcquisitionGroup.Owned||groups.For(p with{Owned=false,InFoundry=true}).Group!=MasteryAcquisitionGroup.Owned)throw new Exception("소유 상태 우선 분류 실패");
        });
    }
}
