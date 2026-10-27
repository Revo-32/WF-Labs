using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class InventoryPage
{
    private MasteryAcquisitionGroup? acquisitionGroup;
    private Action? ShowMasteryView;
    internal readonly Dictionary<MasteryAcquisitionGroup,RadioButton> AcquisitionTabs=new();
    internal readonly ComboBox CreditSource=new(){ItemsSource=new[]{"상점·도장 전체","상점","도장"},SelectedIndex=0,MinWidth=130,Margin=new(8,0,0,0),Visibility=Visibility.Collapsed};
    internal readonly TextBlock AcquisitionHint=Text("",13,"MutedBrush");
    internal Action? RelicPlanningRequested;
    internal readonly Grid RelicPlanningView=new(){Margin=new(8,12,8,0)};
    internal readonly TextBlock PlanningInventorySummary=Text("",13,"MutedBrush");
    private static string AcquisitionTitle(MasteryAcquisitionGroup group)=>group switch
    {MasteryAcquisitionGroup.Owned=>"제작 중·보유 중",MasteryAcquisitionGroup.Credits=>"크레딧 설계도",MasteryAcquisitionGroup.Farming=>"파밍 필요",MasteryAcquisitionGroup.Platinum=>"상점 플래티넘",MasteryAcquisitionGroup.Trade=>"유저 간 거래",_=>"기타"};
    private void BuildAcquisitionTabs(DockPanel title,Grid filters)
    {
        var tabs=(Panel)MasteryTab.Parent;int index=1;
        foreach(var group in Enum.GetValues<MasteryAcquisitionGroup>())
        {
            var tab=new RadioButton{Content=AcquisitionTitle(group),GroupName=MasteryTab.GroupName,Style=MasteryTab.Style,Margin=new(0,0,16,0)};
            AutomationProperties.SetName(tab,AcquisitionTitle(group));AcquisitionTabs[group]=tab;tabs.Children.Insert(index++,tab);
            tab.Checked+=(_,_)=>{acquisitionGroup=group;MasteryList.SelectedIndex=-1;ShowMasteryView?.Invoke();Ownership.SelectedIndex=0;Refresh();};
        }
        filters.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Grid.SetColumn(CreditSource,3);filters.Children.Add(CreditSource);
        AutomationProperties.SetName(CreditSource,"설계도 구매처");CreditSource.SelectionChanged+=(_,_)=>Refresh();
        MasteryArea.Children.Remove(filters);var filterBlock=new StackPanel();filterBlock.Children.Add(AcquisitionHint);filterBlock.Children.Add(filters);Grid.SetRow(filterBlock,2);MasteryArea.Children.Add(filterBlock);
        AcquisitionHint.Margin=new(0,0,0,12);
    }
    private bool MatchesAcquisition(MasteryPlan plan)
    {
        if(acquisitionGroup is null)return true;var info=acquisition.For(plan);
        if(acquisitionGroup==MasteryAcquisitionGroup.Platinum)return !plan.Owned&&!plan.InFoundry&&info.CanBuyPlatinum;
        if(acquisitionGroup==MasteryAcquisitionGroup.Trade)return !plan.Owned&&!plan.InFoundry&&info.CanTrade;
        return info.Group==acquisitionGroup&&(acquisitionGroup!=MasteryAcquisitionGroup.Credits||CreditSource.SelectedIndex==0||CreditSource.SelectedIndex==1&&info.Shop||CreditSource.SelectedIndex==2&&info.Dojo);
    }
    private void RefreshAcquisitionCounts()
    {
        if(AcquisitionTabs.Count==0)return;
        PlanningInventorySummary.Text=Snapshot.CapturedUtc==DateTimeOffset.MinValue?"보유 성유물을 동기화하면 추천을 확인할 수 있습니다.":$"보유 목록 · {Snapshot.CapturedUtc.LocalDateTime:MM-dd HH:mm} 동기화"+(Snapshot.InventoryComplete?"":" · 일부 항목만 확인됨");
        foreach(var (group,tab) in AcquisitionTabs)
        {
            int count=plans.Count(p=>Matches(p.Item,p.Item.Name)&&(group==MasteryAcquisitionGroup.Platinum?!p.Owned&&!p.InFoundry&&acquisition.For(p).CanBuyPlatinum:group==MasteryAcquisitionGroup.Trade?!p.Owned&&!p.InFoundry&&acquisition.For(p).CanTrade:acquisition.For(p).Group==group));
            tab.Content=$"{AcquisitionTitle(group)} {count}";
        }
        CreditSource.Visibility=acquisitionGroup==MasteryAcquisitionGroup.Credits?Visibility.Visible:Visibility.Collapsed;
        AcquisitionHint.Text=acquisitionGroup switch
        {
            MasteryAcquisitionGroup.Owned=>"이미 보유하거나 주조소에서 제작 중인 장비 · 남은 마스터리를 먼저 확인하세요.",
            MasteryAcquisitionGroup.Credits=>"상점·도장에서 설계도를 마련할 수 있는 장비입니다. 제작 재료·클랜 연구 조건은 별도로 확인합니다. 완성품 크레딧 구매도 포함합니다.",
            MasteryAcquisitionGroup.Farming=>"섀시·시스템·뉴로옵틱스 등 부품 또는 설계도의 파밍·퀘스트·교환이 필요한 장비입니다.",
            MasteryAcquisitionGroup.Platinum=>"추천순 · 획득 마스터리 1,000점당 상점 가격이 낮은 순서입니다. MR 제한 장비는 뒤에 표시합니다.",
            MasteryAcquisitionGroup.Trade=>"추천순 · 남은 마스터리 1,000점당 추가 거래비가 낮은 순서입니다. 시세 미확인·MR 제한은 뒤로, 제작시간은 동률일 때만 반영합니다.",
            MasteryAcquisitionGroup.Other=>"특수 획득·조립·현재 획득 제한 등 추가 조건을 확인할 장비입니다.",
            _=>"마스터리 진행도를 확인하고, 획득 방식별 탭에서 다음 장비를 골라보세요."
        };
    }
    private string PurchaseRecommendationLabel(MasteryPlan plan,int index)
    {
        if(acquisitionGroup is not (MasteryAcquisitionGroup.Trade or MasteryAcquisitionGroup.Platinum))return PurchaseRouteLabel(plan);
        return MasteryPurchaseRecommendations.Per1000(plan,acquisitionGroup) is {} ratio
            ?$"추천 {index+1} · 1,000점당 {ratio:0.##} P"+(plan.PricesStale&&acquisitionGroup==MasteryAcquisitionGroup.Trade?" · 오래된 시세 포함":"")
            :plan.AvailablePoints<=0?"추가 랭크 해금 후 추천":"시세 확인 후 추천";
    }
    private string PurchaseRouteLabel(MasteryPlan plan)=>acquisitionGroup switch
    {
        MasteryAcquisitionGroup.Platinum=>"게임 내 상점 · 플래티넘 구매 가능",
        MasteryAcquisitionGroup.Trade=>InventoryMarket.Find(plan.Item.Id) is not null?"유저 거래 · 장비":"유저 거래 · 설계도·부품",
        _=>acquisition.For(plan).Label
    };
    private string? PurchaseRoutePrice(MasteryPlan plan)=>acquisitionGroup switch
    {
        MasteryAcquisitionGroup.Platinum=>MasteryPurchaseRecommendations.Cost(plan,acquisitionGroup) is {} cost?$"상점 {cost:N0} P":"상점 가격 확인 필요",
        MasteryAcquisitionGroup.Trade=>MasteryPurchaseRecommendations.Cost(plan,acquisitionGroup) is {} cost?$"거래 {cost:0.#} P":"거래 시세 확인",
        _=>null
    };
    private void AddPurchaseRouteSummary(MasteryPlan plan)
    {
        if(acquisitionGroup is not (MasteryAcquisitionGroup.Platinum or MasteryAcquisitionGroup.Trade))return;
        MasteryDetail.Children.Add(Text(PurchaseRouteLabel(plan),16,"AccentBrush"));
        MasteryDetail.Children.Add(Text(PurchaseRoutePrice(plan)!,20,"AccentBrush"));
        if(MasteryPurchaseRecommendations.Per1000(plan,acquisitionGroup) is {} ratio)MasteryDetail.Children.Add(Text($"추천 기준 · 마스터리 1,000점당 {ratio:0.##} P · 현재 획득 가능 +{plan.AvailablePoints:N0}점",14));
        MasteryDetail.Children.Add(Text(acquisitionGroup==MasteryAcquisitionGroup.Platinum?"상점 표준가 · 할인·개인 쿠폰 별도":"거래 가능한 품목의 판매 호가 · 부품 구매 후 추가 제작·파밍이 필요할 수 있습니다.",13,"MutedBrush"));
        var alternate=Text("제작·다른 획득 경로 비교",14);alternate.Margin=new(0,16,0,4);MasteryDetail.Children.Add(alternate);
    }
    private void BuildRelicPlanningView()
    {
        RelicPlanningView.RowDefinitions.Add(new(){Height=GridLength.Auto});RelicPlanningView.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var header=new DockPanel{Margin=new(0,0,0,16)};
        var sync=ActionButton("보유 목록 동기화",async()=>{PlanningInventorySummary.Text="보유 목록 확인 중…";await SyncAsync();PlanningInventorySummary.Text=Status.Text;});sync.Margin=new(12,0,0,0);DockPanel.SetDock(sync,Dock.Right);header.Children.Add(sync);header.Children.Add(PlanningInventorySummary);Grid.SetColumnSpan(header,2);RelicPlanningView.Children.Add(header);
        if(persist)RelicPlanningView.Loaded+=async(_,_)=>await LoadPricesAsync();
        RelicPlanningView.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});RelicPlanningView.ColumnDefinitions.Add(new(){Width=new(1.25,GridUnitType.Star)});
        RelicArea.Margin=new(0,0,24,0);RelicArea.Children.Remove(RelicDetails);((ScrollViewer)RelicDetails.Content).Content=null;RelicDetails.Content=null;
        RelicArea.RowDefinitions[4].Height=new(0);Grid.SetRow(RelicArea,1);RelicPlanningView.Children.Add(RelicArea);
        var detail=new Border{Child=Scroll(RelicDetail),Padding=new(24,0,0,0),BorderThickness=new(1,0,0,0)};detail.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Grid.SetColumn(detail,1);Grid.SetRow(detail,1);RelicPlanningView.Children.Add(detail);
        // The right-hand detail is always visible here; expanding a hidden old
        // rail expander must not close the user's recommendation controls.
    }
}
