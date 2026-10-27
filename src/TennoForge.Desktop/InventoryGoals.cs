using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class InventoryPage
{
    internal FarmingWorkspace Farming {get;}
    internal readonly Grid GoalsPane=new();
    internal readonly ListBox GoalList=CreateList();
    internal readonly StackPanel GoalDetail=new();
    internal readonly RewardSearchBox GoalSearch=new(){MinHeight=36};
    internal RadioButton GoalsTab=null!,MasteryTab=null!;
    internal Button AddGoalButton=null!;
    internal Action? GoalSchedulesRequested;
    private readonly StackPanel purchaseView=new();
    private CancellationTokenSource? purchaseCancellation;
    private bool changingPlanning,renderingGoals;
    private bool showGoalPurchases;
    private string? selectedGoalId;
    private void BuildGoals(DockPanel title)
    {
        var tabs=new WrapPanel();
        MasteryTab=new(){Content="전체 요약",GroupName="EquipmentView"+GetHashCode(),IsChecked=true,Style=(Style)Application.Current.Resources["NavigationItem"],Margin=new(0,0,16,0)};
        GoalsTab=new(){Content="내 목표",GroupName=MasteryTab.GroupName,Style=MasteryTab.Style};tabs.Children.Add(MasteryTab);tabs.Children.Add(GoalsTab);title.Children.Add(tabs);
        GoalsPane.Visibility=Visibility.Collapsed;GoalsPane.RowDefinitions.Add(new(){Height=GridLength.Auto});GoalsPane.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        Grid.SetRow(GoalsPane,1);Grid.SetRowSpan(GoalsPane,3);MasteryArea.Children.Add(GoalsPane);
        var add=new Grid{Margin=new(0,0,0,16)};add.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});add.ColumnDefinitions.Add(new(){Width=GridLength.Auto});add.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        GoalSearch.ItemsSource=CatalogData.Items.Where(i=>i.Masterable).OrderBy(i=>i.Name,StringComparer.Ordinal).Select(i=>new RelicItem(i.Id,"",i.Name,i.EnglishName,0)).ToArray();
        AutomationProperties.SetName(GoalSearch,"목표 장비 검색");add.Children.Add(GoalSearch);
        AddGoalButton=ActionButton("목표 추가",()=>{if(GoalSearch.SelectedItem is RelicItem i)AddFarmingGoal(i.Id);});AddGoalButton.IsEnabled=false;AddGoalButton.Margin=new(8,0,0,0);GoalSearch.SelectionChanged+=(_,_)=>AddGoalButton.IsEnabled=GoalSearch.SelectedItem is RelicItem;Grid.SetColumn(AddGoalButton,1);add.Children.Add(AddGoalButton);
        var all=ActionButton("전체 구매·예약",()=>{selectedGoalId=null;GoalList.SelectedIndex=-1;ShowGoal();RefreshRelics();});all.Margin=new(8,0,0,0);Grid.SetColumn(all,2);add.Children.Add(all);GoalsPane.Children.Add(add);
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new(.30,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new(.70,GridUnitType.Star)});
        GoalList.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Margin='0,0,12,0'><TextBlock Text='{Binding Name}' FontSize='16' TextWrapping='Wrap'/><TextBlock Text='{Binding State}' Foreground='{DynamicResource MutedBrush}' FontSize='13' Margin='0,6,0,0' TextWrapping='Wrap'/><TextBlock Text='{Binding Value}' Foreground='{DynamicResource AccentBrush}' FontSize='14' Margin='0,6,0,0'/></StackPanel></DataTemplate>");
        body.Children.Add(GoalList);var detail=new Border{Child=Scroll(GoalDetail),Padding=new(20,0,0,0),Margin=new(12,0,0,0),BorderThickness=new(1,0,0,0)};detail.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Grid.SetColumn(detail,1);body.Children.Add(detail);Grid.SetRow(body,1);GoalsPane.Children.Add(body);
        GoalList.SelectionChanged+=(_,_)=>{if(renderingGoals)return;selectedGoalId=(GoalList.SelectedItem as InventoryRow)?.Id;ShowGoal();RefreshRelics();};
        void Change(bool show)
        {
            foreach(var child in MasteryArea.Children.OfType<FrameworkElement>().Where(c=>Grid.GetRow(c)>0&&!ReferenceEquals(c,GoalsPane)))child.Visibility=show?Visibility.Collapsed:Visibility.Visible;
            GoalsPane.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(show)RefreshGoals();
            MasteryCount.Visibility=show?Visibility.Collapsed:Visibility.Visible;
            if(!show)Progress.Visibility=acquisitionGroup is null?Visibility.Visible:Visibility.Collapsed;
        }
        GoalsTab.Checked+=(_,_)=>Change(true);MasteryTab.Checked+=(_,_)=>{acquisitionGroup=null;MasteryList.SelectedIndex=-1;Change(false);Refresh();};
        ShowMasteryView=()=>Change(false);
    }
    internal void AddFarmingGoal(string id)
    {
        try{Farming.Add(id);selectedGoalId=id;goal=null;GoalsTab.IsChecked=true;RefreshGoals();RefreshRelics();Status.Text=CatalogData.Name(id)+" · 목표에 등록했습니다. 재료 예약·성유물·미션 알림에 함께 적용됩니다.";}
        catch(Exception e)when(e is ArgumentException or IOException or UnauthorizedAccessException){Status.Text="목표를 저장하지 못했습니다. "+e.Message;}
    }
    private void FarmingChanged()
    {if(changingPlanning||disposed)return;Snapshot=Farming.Inventory;plansDirty=true;Refresh();}
    private void EditGoal(Action action)
    {
        try{action();}catch(Exception e)when(e is IOException or ArgumentException or UnauthorizedAccessException){Status.Text="목표 변경 확인 필요 · "+e.Message;}
    }
    private void RefreshGoals()
    {
        GoalsTab.Content=$"내 목표 {Farming.Document.Goals.Length}";if(GoalsPane.Visibility!=Visibility.Visible)return;
        renderingGoals=true;
        try
        {
            GoalList.ItemsSource=Farming.Document.Goals.Select((g,index)=>
            {
                var p=Farming.Plan.Goals.FirstOrDefault(p=>p.Goal.ItemId==g.ItemId);
                string state=!g.Enabled?"일시 정지":p?.Secured==true?"확보 · 목표 재고 보호":p?.Waiting==true?"제작 중·선행 제작":p?.MaterialsReady==true?"제작 준비":$"부족 {p?.Requirements.Count(r=>r.Missing>0)??0}종";
                return new InventoryRow(g.ItemId,$"{index+1}. {CatalogData.Name(g.ItemId)}",state,$"{p?.Owned??0:N0} / {g.Quantity:N0}개",g);
            }).ToArray();
            GoalList.SelectedItem=GoalList.Items.Cast<InventoryRow>().FirstOrDefault(r=>r.Id==selectedGoalId);
        }
        finally{renderingGoals=false;}
        ShowGoal();
    }
    private void ShowGoal()
    {
        if(renderingGoals)return;purchaseCancellation?.Cancel();GoalDetail.Children.Clear();
        var definition=Farming.Document.Goals.FirstOrDefault(g=>g.ItemId==selectedGoalId);
        if(definition is null)
        {
            Detail(GoalDetail,"전체 목표 · 구매와 예약",Farming.Document.Goals.Length==0?"장비를 검색해 추가하거나 마스터리 목록에서 목표로 등록하세요.":"목록의 순서대로 보유 재료를 배분합니다. 예약은 게임 재고를 변경하지 않습니다.");
            if(Farming.Notice.Length>0)GoalDetail.Children.Add(Text(Farming.Notice,14,"ErrorBrush"));
        }
        else
        {
            Detail(GoalDetail,CatalogData.Name(definition.ItemId),definition.Enabled?"목표 수량만큼 재료를 예약합니다.":"일시 정지 · 재료 예약과 알림에서 제외됩니다.");
            var actions=new WrapPanel{Margin=new(0,12,0,12)};
            var quantity=new ComboBox{ItemsSource=Enumerable.Range(1,99).ToArray(),SelectedItem=definition.Quantity,MinWidth=68,MinHeight=36,Margin=new(0,0,8,8)};AutomationProperties.SetName(quantity,"목표 보유 수량");quantity.SelectionChanged+=(_,_)=>{if(!renderingGoals&&quantity.SelectedItem is int n)EditGoal(()=>Farming.Edit(definition with{Quantity=n}));};actions.Children.Add(new TextBlock{Text="목표 수량",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,8,8)});actions.Children.Add(quantity);
            actions.Children.Add(ActionButton(definition.Enabled?"일시 정지":"다시 시작",()=>EditGoal(()=>Farming.Edit(definition with{Enabled=!definition.Enabled}))));
            actions.Children.Add(ActionButton("위로",()=>EditGoal(()=>Farming.Move(definition.ItemId,-1))));actions.Children.Add(ActionButton("아래로",()=>EditGoal(()=>Farming.Move(definition.ItemId,1))));
            actions.Children.Add(ActionButton("목표 삭제",()=>EditGoal(()=>Farming.Remove(definition.ItemId))));GoalDetail.Children.Add(actions);
            quantity.ToolTip="만들고 싶은 추가 개수가 아닌, 최종 보유 수량입니다. 이미 확보한 장비도 다른 목표에서 소모하지 않도록 보호합니다.";
            foreach(var b in actions.Children.OfType<Button>()){b.Padding=new(10,6,10,6);b.Margin=new(0,0,8,8);}
            var notify=new CheckBox{Content="목표 성유물 미션 알림",ToolTip="보유 성유물로 부족한 목표 부품을 얻을 수 있을 때만 알립니다.",IsChecked=definition.Notify,Margin=new(0,0,0,12)};AutomationProperties.SetName(notify,"목표 성유물 미션 알림");void Notify()=>EditGoal(()=>Farming.Edit(definition with{Notify=notify.IsChecked==true}));notify.Checked+=(_,_)=>Notify();notify.Unchecked+=(_,_)=>Notify();GoalDetail.Children.Add(notify);
            var shortcuts=new WrapPanel{Margin=new(0,0,0,12)};
            shortcuts.Children.Add(ActionButton("추천 성유물 보기",()=>{RefreshRelics();RelicPlanningRequested?.Invoke();}));
            if(GoalSchedulesRequested is not null)shortcuts.Children.Add(ActionButton("관련 일정 보기",()=>GoalSchedulesRequested()));
            if(items[definition.ItemId].Category is "Primary" or "Secondary" or "Melee")shortcuts.Children.Add(ActionButton("이 장비 모딩하기",()=>ModRequested?.Invoke(definition.ItemId)));
            foreach(var b in shortcuts.Children.OfType<Button>())b.Margin=new(0,0,8,8);GoalDetail.Children.Add(shortcuts);
            if(Farming.Plan.Goals.FirstOrDefault(g=>g.Goal.ItemId==definition.ItemId) is {} plan)
            {
                GoalDetail.Children.Add(Text($"크레딧 {plan.ReservedCredits:N0} / {plan.Credits:N0} 예약",15,"AccentBrush"));
                foreach(var warning in plan.Warnings)GoalDetail.Children.Add(Text(warning,13,"AccentBrush"));
                var sections=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,20,0,16)};
                var materials=new StackPanel();
                foreach(var (title,shopping) in new[]{("재료·예약",false),("구매 비교",true)})
                {
                    var tab=new RadioButton{Content=title,GroupName="GoalSections"+GetHashCode(),Style=(Style)Application.Current.Resources["NavigationItem"],Margin=new(0,0,16,0),IsChecked=shopping==showGoalPurchases};AutomationProperties.SetName(tab,"목표 "+title);
                    tab.Checked+=(_,_)=>{showGoalPurchases=shopping;materials.Visibility=shopping?Visibility.Collapsed:Visibility.Visible;purchaseView.Visibility=shopping?Visibility.Visible:Visibility.Collapsed;};sections.Children.Add(tab);
                }
                GoalDetail.Children.Add(sections);materials.Visibility=showGoalPurchases?Visibility.Collapsed:Visibility.Visible;purchaseView.Visibility=showGoalPurchases?Visibility.Visible:Visibility.Collapsed;
                materials.Children.Add(UiHierarchy.Heading("필요 재료 · 예약 / 필요"));
                foreach(var r in plan.Requirements.OrderByDescending(r=>r.Missing>0).ThenByDescending(r=>r.ConsumesUnmastered))
                {
                    string state=r.Crafting>0?$" · 제작 중 {r.Crafting:N0}":r.Planned>0?$" · 계획한 선행 제작 {r.Planned:N0}":r.Missing>0?$" · 부족 {r.Missing:N0}":" · 확보";
                    materials.Children.Add(Text($"{CatalogData.Name(r.ItemId)}\n{r.Reserved:N0} / {r.Needed:N0}"+state+(r.Reusable?" · 재사용 설계도":"")+(r.CreditPurchase?" · 크레딧 구매":""),14));
                    if(r.ConsumesUnmastered)materials.Children.Add(Text("소모 전 숙련 완료 확인 필요",13,"AccentBrush"));
                    if(r.Missing>0)materials.Children.Add(new Expander{Header="획득처·구매 조건",Content=AcquisitionView(r.ItemId),Margin=new(0,4,0,12)});
                }
                GoalDetail.Children.Add(materials);
            }
        }
        if(definition is null||!definition.Enabled)purchaseView.Visibility=Visibility.Visible;
        if(purchaseView.Parent is Panel previous)previous.Children.Remove(purchaseView);GoalDetail.Children.Add(purchaseView);
        _=CompareGoalPurchasesAsync(definition?.ItemId);
    }
    internal Task RefreshGoalPurchasesForCheck()=>CompareGoalPurchasesAsync(selectedGoalId);
    internal Task RefreshGoalPricesForCheck()=>RefreshGoalPricesAsync(selectedGoalId);
    private Dictionary<string,long> GoalDemands(string? id)
    {
        var selected=Farming.Plan.Goals.Where(g=>id is null||g.Goal.ItemId==id).ToArray();
        return selected.SelectMany(g=>g.Requirements).Where(r=>r.Missing>0&&!r.CreditPurchase).GroupBy(r=>r.ItemId).ToDictionary(g=>g.Key,g=>g.Any(r=>r.Reusable)?g.Max(r=>r.Missing):g.Sum(r=>r.Missing));
    }
    private async Task CompareGoalPurchasesAsync(string? id)
    {
        purchaseCancellation?.Cancel();using var pending=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);purchaseCancellation=pending;
        purchaseView.Children.Clear();purchaseView.Children.Add(UiHierarchy.Heading("거래 부품 · 세트와 낱개 비교"));
        if(!Farming.Plan.InventoryConfirmed){purchaseView.Children.Add(Text("보유 목록이 일부이거나 연동 전입니다. 구매 수량과 판매 여유는 목록 확인 후 계산합니다.",14,"MutedBrush"));purchaseCancellation=null;return;}
        var demands=GoalDemands(id);var quotes=new Dictionary<string,PlannerPrice>(prices);
        var state=Text("구매 조합 계산 중…",14,"MutedBrush");purchaseView.Children.Add(state);
        var cancel=ActionButton("계산 취소",()=>pending.Cancel());purchaseView.Children.Add(cancel);
        try
        {
            var comparison=await Task.Run(()=>FarmingPurchases.Compare(demands,quotes,FarmingMarket.Document.Bundles,FarmingMarket.Part,pending.Token),pending.Token);
            if(disposed||pending.IsCancellationRequested||!ReferenceEquals(purchaseCancellation,pending))return;
            purchaseView.Children.Remove(cancel);state.Text=comparison.BestCost is {} total?$"확인 호가 기준 최소 {total:0.##} P" :"구매 합계 확인 필요 · 부품 또는 세트 시세가 없습니다.";state.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
            purchaseView.Children.Add(Text(comparison.PartsCost is {} parts?$"부족 낱개만 구매 {parts:0.##} P"+(comparison.Savings is >0 and var saved?$" · 조합 구매로 {saved:0.##} P 절약":""):"낱개 합계 미확인 · 시세가 없는 품목을 무료로 계산하지 않습니다.",13,"MutedBrush"));
            foreach(var line in comparison.Lines)purchaseView.Children.Add(Text($"{line.Item.Name} ×{line.Quantity} · {line.Cost:0.##} P"+(line.Bundle?" · 세트":""),14));
            foreach(var extra in comparison.ExtraItems)purchaseView.Children.Add(Text($"추가로 남음 · {CatalogData.Name(extra.Id)} ×{extra.Count}",13,"MutedBrush"));
            if(comparison.Stale)purchaseView.Children.Add(Text("오래된 호가 포함 · 구매 전 갱신하세요.",13,"AccentBrush"));
            var refresh=ActionButton("부품·세트 시세 갱신",()=>_=RefreshGoalPricesAsync(id));purchaseView.Children.Add(refresh);
            purchaseView.Children.Add(Text("확인된 등록 호가·세트 구성 기준입니다. 재판매 수익은 비용에서 빼지 않으며, 크레딧·비거래 재료는 별도로 확보합니다.",12,"MutedBrush"));
            var free=new Expander{Header="목표 예약 외 거래 부품",Margin=new(0,12,0,0)};free.Expanded+=(_,_)=>free.Content??=FreeTradeParts();purchaseView.Children.Add(free);
        }
        catch(OperationCanceledException){if(!disposed&&ReferenceEquals(purchaseCancellation,pending)){state.Text="계산을 취소했습니다.";purchaseView.Children.Remove(cancel);}}
        catch(Exception e)when(e is ArgumentException or InvalidOperationException or IOException){state.Text="구매 비교 확인 필요 · "+e.Message;purchaseView.Children.Remove(cancel);}
        finally{if(ReferenceEquals(purchaseCancellation,pending))purchaseCancellation=null;}
    }
    private FrameworkElement FreeTradeParts()
    {
        var p=new StackPanel();var available=Snapshot.Quantities.Where(q=>Farming.Plan.Free(Snapshot,q.Key)>0&&FarmingMarket.Part(q.Key) is not null&&
            (!items.TryGetValue(q.Key,out var item)||!item.Masterable||Snapshot.MasteryComplete&&Snapshot.MasteryRanks.GetValueOrDefault(q.Key)>=item.MaxRank)).OrderBy(q=>CatalogData.Name(q.Key)).ToArray();
        foreach(var q in available)
        {
            long reserved=Farming.Plan.Reserved.GetValueOrDefault(q.Key);p.Children.Add(Text($"{CatalogData.Name(q.Key)} · 예약 외 {Farming.Plan.Free(Snapshot,q.Key):N0}개"+(reserved>0?$" · 목표 예약 {reserved:N0}개":""),13));
        }
        if(available.Length==0)p.Children.Add(Text("목표 예약 외 거래 부품이 없습니다.",13,"MutedBrush"));
        p.Children.Add(Text("등록한 목표에 예약되지 않은 수량입니다. 미숙련 장비는 제외합니다. 다른 제작 용도까지 자동으로 판매 대상으로 확정하지 않습니다.",12,"MutedBrush"));return p;
    }
    private async Task RefreshGoalPricesAsync(string? id)
    {
        if(quoteCancellation is not null)return;
        using var pending=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);quoteCancellation=pending;
        var demands=GoalDemands(id);var targets=demands.Keys.Select(FarmingMarket.Part).Where(i=>i is not null).Cast<RelicItem>()
            .Concat(FarmingMarket.Document.Bundles.Where(b=>b.Parts.Any(p=>demands.ContainsKey(p.GameId))).Select(b=>b.Item)).DistinctBy(i=>i.Id).ToArray();
        var preferences=RelicPreferences.Read();int complete=0;
        try
        {
            foreach(var item in targets)
            {
                Status.Text=$"목표 부품·세트 호가 확인 {complete+1}/{targets.Length}";
                var quote=QuoteOverride is {} query?await query(item,pending.Token):await market.QuoteAsync(item,preferences.Presence==1?MarketPresence.Online:MarketPresence.InGame,preferences.Crossplay,true,pending.Token);
                if(disposed)return;
                var keys=FarmingMarket.GameIds(item.Id).Append(item.Id).Distinct();
                foreach(var key in keys)if(quote.SellMin is >0 and var value)prices[key]=new((double)value,quote.FetchedUtc,quote.Stale);else if(quote.Error is null)prices.Remove(key);
                complete++;
            }
            Status.Text=$"목표 시세 {complete}개 확인 · {DateTime.Now:HH:mm:ss}";plansDirty=true;Refresh();
        }
        catch(OperationCanceledException){Status.Text="호가 확인을 취소했습니다. 확인한 가격은 유지합니다.";}
        catch(Exception e)when(e is IOException or InvalidOperationException or System.Net.Http.HttpRequestException){Status.Text="호가 확인 실패 · 저장 시세 유지 · "+e.Message;}
        finally{quoteCancellation=null;}
    }
}
