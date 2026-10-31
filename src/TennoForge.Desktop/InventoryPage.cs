using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed record InventoryRow(string Id,string Name,string State,string Value,object Model)
{public double? Meter {get;init;}}
internal sealed partial class InventoryPage : Grid,IDisposable
{
    internal InventoryCatalog CatalogData {get;}=InventoryCatalog.Load();
    internal InventorySnapshot Snapshot {get;private set;}=InventorySnapshot.Empty;
    private readonly Dictionary<string,InventoryItem> items;
    private readonly MasteryAcquisitionGroups acquisition;
    private readonly Dictionary<string,PlannerPrice> prices=new();
    private readonly Dictionary<string,int> ducats=new();
    private readonly string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","inventory");
    private readonly InventoryMemoryReader reader=new();
    private readonly RelicMarketClient market;
    internal readonly TextBlock Status=new OperationStatusText("게임에서 인벤토리와 마스터리 이력을 가져오세요.");
    private readonly TextBlock summary=Text("인벤토리 연동 전",15,"MutedBrush"),priceStatus=Text("공유 저장 시세 · 조회 중",14,"MutedBrush");
    internal readonly Button SyncButton,ImportButton,ExportButton;
    internal readonly CheckBox Automatic=new(){Content="자동 동기화",VerticalAlignment=VerticalAlignment.Center,Margin=new(12,0,16,0)};
    internal readonly TextBox Search=new(){MinWidth=240,Height=36};
    internal readonly ComboBox Category=new(){ItemsSource=new[]{"전체 종류","워프레임","주무기","보조무기","근접","동반자","아크윙","기타"},SelectedIndex=0,MinWidth=140,Margin=new(10,0,0,0)};
    internal readonly ComboBox Ownership=new(){ItemsSource=new[]{"전체","보유","랭작 미완료","마스터리 완료"},SelectedIndex=0,MinWidth=160,Margin=new(10,0,0,0)};
    internal readonly ComboBox Squad=new(){ItemsSource=new[]{"혼자 개봉","같은 성유물 · 2명","같은 성유물 · 3명","같은 성유물 · 4명"},SelectedIndex=0,MinWidth=205};
    internal readonly TextBox Traces=new(){Text="0",Width=110,Height=36,Margin=new(12,0,0,0)};
    internal readonly CheckBox OnlyAffordable=new(){Content="잔영으로 정련 가능",IsChecked=true,Margin=new(16,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    internal readonly ListBox MasteryList=CreateList(),RelicList=CreateList();
    internal readonly StackPanel MasteryDetail=new(),RelicDetail=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(30)};
    private readonly DispatcherTimer automaticContinuation=new(){Interval=TimeSpan.FromSeconds(3)};
    private readonly CancellationTokenSource lifetime=new();
    private MasteryPlan[] plans=[];private bool busy,disposed,pricesLoading,plansDirty=true;private string? goal;private DateTime inboxTime;private readonly bool persist;private int ticks;private HashSet<string> activeTargets=[];
    internal Func<string?>? ChooseImportFile {get;set;}
    internal Func<string?>? ChooseExportFile {get;set;}
    internal Func<CancellationToken,Task<InventoryReadResult>>? ReadOverride {get;set;}
    internal Action<string>? ModRequested;
    internal Func<bool> CanReadGame {get;set;}=()=>true;
    internal bool AutomaticContinuationScheduled=>automaticContinuation.IsEnabled;
    private static TextBlock Text(string s,double size=15,string brush="TextBrush")=>RivenPresentation.Text(s,size,brush);
    private static Button ActionButton(string s,Action action){var b=new Button{Content=s,Padding=new(14,7,14,7),Margin=new(0,0,8,0),MinHeight=36};b.Click+=(_,_)=>action();AutomationProperties.SetName(b,s);return b;}
    private static FrameworkElement Scroll(UIElement content)=>new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0)};
    internal InventoryPage(bool persist=true,FarmingWorkspace? farming=null)
    {
        this.persist=persist;
        Farming=farming??new FarmingWorkspace(persist,CatalogData);Snapshot=Farming.Inventory;
        items=CatalogData.Items.ToDictionary(x=>x.Id);acquisition=new(CatalogData);
        market=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","relic-market-cache"),snapshotMode:true);
        var marketItems=InventoryMarket.Items.ToDictionary(i=>i.Id);foreach(var item in CatalogData.Items.Where(i=>i.MarketId is not null))if(marketItems.TryGetValue(item.MarketId!,out var m))ducats[item.Id]=m.Ducats;
        SyncButton=ActionButton("게임에서 동기화",()=>_=SyncAsync());SyncButton.ToolTip="실행 중인 게임의 인벤토리 사본을 읽기 전용으로 확인합니다. 실패하면 마지막 저장 목록을 유지합니다.";UiHierarchy.Primary(SyncButton);
        ImportButton=ActionButton("목록 가져오기",Import);ExportButton=ActionButton("목록 내보내기",Export);
        RefreshQuoteButton=ActionButton("이 장비의 부품 시세 갱신",()=>{if(quoteCancellation is not null)quoteCancellation.Cancel();else _=RefreshSelectedPricesAsync();});
        BuildDashboard();
        automaticContinuation.Tick+=async(_,_)=>{automaticContinuation.Stop();if(!disposed&&Automatic.IsChecked==true)await SyncAsync(true);};
        Automatic.Unchecked+=(_,_)=>automaticContinuation.Stop();
        MasteryList.SelectionChanged+=(_,_)=>{if(!changingRows)ShowMastery();};RelicList.SelectionChanged+=(_,_)=>{if(!changingRows)ShowRelic();};
        Category.SelectionChanged+=(_,_)=>Refresh();Ownership.SelectionChanged+=(_,_)=>Refresh();Search.TextChanged+=(_,_)=>Refresh();
        Squad.SelectionChanged+=(_,_)=>RefreshRelics();Traces.TextChanged+=(_,_)=>RefreshRelics();OnlyAffordable.Checked+=(_,_)=>RefreshRelics();OnlyAffordable.Unchecked+=(_,_)=>RefreshRelics();
        if(persist)
        {
            try{var path=Path.Combine(folder,"snapshot.json");if(File.Exists(path))Snapshot=InventoryImport.Parse(File.ReadAllText(path),CatalogData);}
            catch(Exception e)when(e is IOException or JsonException or InvalidDataException){Status.Text="저장 목록을 읽지 못했습니다. 다시 동기화하거나 가져오세요.";}
            try{var preferences=Path.Combine(folder,"preferences.json");Automatic.IsChecked=!File.Exists(preferences)||JsonDocument.Parse(File.ReadAllText(preferences)).RootElement.GetProperty("automatic").GetBoolean();}catch{Automatic.IsChecked=true;}
            timer.Tick+=async(_,_)=>{await ReadInboxAsync();if(disposed)return;if(Automatic.IsChecked==true)await SyncAsync(true);if(++ticks%4==0)await LoadPricesAsync();};timer.Start();
            void SavePreference(){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"preferences.json"),JsonSerializer.Serialize(new{automatic=Automatic.IsChecked==true}));}
            Automatic.Checked+=(_,_)=>{SavePreference();_=SyncAsync(true);};Automatic.Unchecked+=(_,_)=>SavePreference();
            Loaded+=async(_,_)=>{await LoadPricesAsync();await ReadInboxAsync();if(Automatic.IsChecked==true)await SyncAsync(true);};
        }
        Farming.SetInventory(Snapshot);Farming.Changed+=FarmingChanged;Refresh();
    }
    private static ListBox CreateList()
    {
        var box=new ListBox{BorderThickness=new(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new(0)};
        box.SetResourceReference(Control.ForegroundProperty,"TextBrush");ScrollViewer.SetHorizontalScrollBarVisibility(box,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(box,ScrollBarVisibility.Auto);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,12,12,12)));style.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0,0,0,4)));style.Setters.Add(new Setter(Control.ForegroundProperty,new System.Windows.DynamicResourceExtension("TextBrush")));
        style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><Border Background='{TemplateBinding Background}' BorderBrush='{DynamicResource StrokeBrush}' BorderThickness='0,0,0,1' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Stretch'/></Border></ControlTemplate>")));
        var hover=new Trigger{Property=ListBoxItem.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,new System.Windows.DynamicResourceExtension("HoverBrush")));style.Triggers.Add(hover);
        var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,new System.Windows.DynamicResourceExtension("AccentSurfaceBrush")));selected.Setters.Add(new Setter(Control.ForegroundProperty,new System.Windows.DynamicResourceExtension("AccentBrush")));style.Triggers.Add(selected);box.ItemContainerStyle=style;
        box.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='135'/><ColumnDefinition Width='108'/></Grid.ColumnDefinitions><TextBlock Text='{Binding Name}' FontSize='16' TextWrapping='Wrap' Margin='0,0,16,0' VerticalAlignment='Center'/><TextBlock Grid.Column='1' Text='{Binding State}' Foreground='{DynamicResource MutedBrush}' TextWrapping='Wrap' Margin='0,0,12,0' VerticalAlignment='Center'/><TextBlock Grid.Column='2' Text='{Binding Value}' Foreground='{DynamicResource AccentBrush}' TextWrapping='Wrap' VerticalAlignment='Center'/></Grid></DataTemplate>");return box;
    }
    internal void UseSnapshot(InventorySnapshot snapshot,bool save)
    {
        InventoryImport.Validate(snapshot);Snapshot=snapshot;plansDirty=true;if(save&&persist)InventoryImport.Save(Path.Combine(folder,"snapshot.json"),snapshot);
        changingPlanning=true;try{Farming.SetInventory(snapshot);}finally{changingPlanning=false;}
        if(!Traces.IsKeyboardFocusWithin)Traces.Text=snapshot.Count("/Lotus/Types/Items/MiscItems/VoidTearDrop").ToString(System.Globalization.CultureInfo.InvariantCulture);Refresh();
    }
    internal async Task SyncAsync(bool automatic=false)
    {
        if(busy||disposed)return;
        automaticContinuation.Stop();
        if(!CanReadGame())
        {
            Status.Text=automatic?"화면 인식 우선 · 인벤토리 확인은 잠시 후 이어집니다.":"화면 인식이 진행 중입니다. 끝난 뒤 다시 동기화할 수 있습니다.";
            if(automatic)ScheduleAutomaticContinuation();return;
        }
        busy=true;SyncButton.IsEnabled=false;Status.Text="게임 인벤토리 확인 중…";
        try
        {
            var result=await (ReadOverride is {} read?read(lifetime.Token):reader.ReadAsync(CatalogData,lifetime.Token,automatic,()=>!CanReadGame()));if(disposed)return;
            if(result.Snapshot is {} snapshot)
            {
                var missing=InventoryImport.MissingUpdateSections(Snapshot,snapshot);
                if(missing.Length>0){Status.Text="이번 응답의 일부 항목이 누락돼 마지막 정상 목록을 유지합니다. 다음 동기화에서 다시 확인합니다.";return;}
                UseSnapshot(snapshot,true);
            }
            Status.Text=result.Message+$" · {result.Seconds:0.0}초";
            if(automatic&&result.Deferred&&result.Snapshot is null&&!result.CompletePass)ScheduleAutomaticContinuation();
        }
        catch(OperationCanceledException){}
        catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException){Status.Text="동기화 실패 · "+e.Message;}
        finally{busy=false;SyncButton.IsEnabled=true;}
    }
    private void ScheduleAutomaticContinuation()
    {
        if(disposed||Automatic.IsChecked!=true)return;
        // One stopped/restarted timer owns continuation; no accumulating Tasks
        // or immediate loops while OCR is using the CPU.
        automaticContinuation.Stop();automaticContinuation.Start();
    }
    private async Task ReadInboxAsync()
    {
        var path=Path.Combine(folder,"inventory-inbox.json");try{if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<=inboxTime)return;var stamp=File.GetLastWriteTimeUtc(path);var json=await File.ReadAllTextAsync(path,lifetime.Token);if(disposed)return;var snapshot=InventoryImport.Parse(json,CatalogData,"로컬 연동 파일");UseSnapshot(snapshot,true);inboxTime=stamp;Status.Text="연동 파일에서 새 목록을 가져왔습니다.";}catch(OperationCanceledException){}catch(Exception e)when(e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException){Status.Text="연동 파일 확인 필요 · 이전 목록 유지 · "+e.Message;}
    }
    private void Import()
    {
        var path=ChooseImportFile?.Invoke();if(ChooseImportFile is null){var dlg=new OpenFileDialog{Title="인벤토리 목록 가져오기",Filter="인벤토리 JSON|*.json",CheckFileExists=true};if(dlg.ShowDialog(Window.GetWindow(this))==true)path=dlg.FileName;}
        if(path is null)return;
        try{if(new FileInfo(path).Length>32*1024*1024)throw new InvalidDataException("파일이 너무 큽니다.");UseSnapshot(InventoryImport.Parse(File.ReadAllText(path),CatalogData),true);Status.Text="목록을 가져왔습니다. 게임 동기화 전까지 가져온 목록을 표시합니다.";}
        catch(Exception e)when(e is IOException or JsonException or InvalidDataException or InvalidOperationException){Status.Text=e is JsonException?"가져오기 실패 · JSON 형식이 올바르지 않습니다. 이전 목록을 유지합니다.":"가져오기 실패 · 이전 목록 유지 · "+e.Message;}
    }
    private void Export()
    {
        var path=ChooseExportFile?.Invoke();if(ChooseExportFile is null){var dlg=new SaveFileDialog{Title="인벤토리 목록 내보내기",Filter="인벤토리 JSON|*.json",FileName="WF-Labs-inventory.json"};if(dlg.ShowDialog(Window.GetWindow(this))!=true)return;path=dlg.FileName;}if(path is null)return;
        try{InventoryImport.Save(path,Snapshot);Status.Text="품목·수량·마스터리 이력만 내보냈습니다.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status.Text="저장 실패 · "+e.Message;}
    }
    internal async Task LoadPricesAsync()
    {
        if(pricesLoading||disposed)return;pricesLoading=true;
        try
        {
            market.ReloadSavedQuotes();
            prices.Clear();
            var preferences=RelicPreferences.Read();var presence=preferences.Presence==1?MarketPresence.Online:MarketPresence.InGame;
            var relics=InventoryMarket.Items.ToDictionary(x=>x.Id);int loaded=0;DateTimeOffset? oldest=null;
            foreach(var item in CatalogData.Items.Where(i=>i.MarketId is not null))
            {
                if(!relics.TryGetValue(item.MarketId!,out var marketItem))continue;
                ducats[item.Id]=marketItem.Ducats;var quote=await market.QuoteAsync(marketItem,presence,preferences.Crossplay,false,lifetime.Token);
                if(quote.SellMin is {} p&&p>0){prices[item.Id]=new((double)p,quote.FetchedUtc,quote.Stale);loaded++;if(oldest is null||quote.FetchedUtc<oldest)oldest=quote.FetchedUtc;}
            }
            foreach(var bundle in FarmingMarket.Document.Bundles)
            {
                var quote=await market.QuoteAsync(bundle.Item,presence,preferences.Crossplay,false,lifetime.Token);
                if(quote.SellMin is >0 and var p)prices[bundle.Item.Id]=new((double)p,quote.FetchedUtc,quote.Stale);
            }
            if(disposed)return;plansDirty=true;priceStatus.Text=$"공유 저장 시세 {loaded:N0}개 · PC"+(preferences.Crossplay?"·크로스플레이":"")+" 판매 최저 호가"+(oldest is {} d?$" · 가장 오래된 조회 {d.LocalDateTime:MM-dd HH:mm}":" · 성유물 탭에서 시세 준비 가능");Refresh();
        }
        catch(OperationCanceledException){}
        catch(Exception e)when(e is IOException or JsonException or InvalidOperationException){priceStatus.Text="저장 시세 확인 필요 · "+e.Message;}
        finally{pricesLoading=false;}
    }
    private string CategoryName(InventoryItem i)=>i.Category switch{"Warframes"=>"워프레임","Primary"=>"주무기","Secondary"=>"보조무기","Melee"=>"근접","Pets" or "Sentinels"=>"동반자","Archwing" or "Arch-Gun" or "Arch-Melee"=>"아크윙",_=>"기타"};
    private bool Matches(InventoryItem? item,string name)=> (Category.SelectedIndex==0||item is not null&&CategoryName(item)==Category.SelectedItem as string)&&
        (string.IsNullOrWhiteSpace(Search.Text)||RelicCatalog.Normalize(name).Contains(RelicCatalog.Normalize(Search.Text),StringComparison.Ordinal)||item?.EnglishName.Contains(Search.Text,StringComparison.OrdinalIgnoreCase)==true);
    private void Refresh()
    {
        if(!dashboardBuilt)return;
        var inventory=Snapshot;summary.Text=inventory.CapturedUtc==DateTimeOffset.MinValue?"연동 전 · 보유 수량과 미획득 이력은 아직 알 수 없습니다.":$"{inventory.Source} · 읽은 시각 {inventory.CapturedUtc.LocalDateTime:MM-dd HH:mm:ss} · MR {inventory.MasteryRank?.ToString()??"미확인"} · 크레딧 {inventory.Credits:N0} · 보유 {inventory.Quantities.Count:N0}종 · "+(inventory.InventoryComplete?"보유 목록 확인됨":"보유 목록 일부")+(inventory.MasteryComplete?"":" · 마스터리 이력 미확인");
        ExportButton.IsEnabled=inventory.CapturedUtc!=DateTimeOffset.MinValue;
        UiHierarchy.SetPrimary(SyncButton,inventory.CapturedUtc==DateTimeOffset.MinValue);
        if(plansDirty){plans=MasteryPlanner.Recommend(CatalogData,inventory,prices,Farming.Plan);plansDirty=false;}
        Progress.Update(MasteryOverview.From(CatalogData,Snapshot));
        RefreshAcquisitionCounts();
        var filtered=MasteryPurchaseRecommendations.Order(plans.Where(p=>Matches(p.Item,p.Item.Name)&&(Ownership.SelectedIndex!=1||p.Owned)&&MatchesAcquisition(p)),acquisitionGroup);
        var rows=Ownership.SelectedIndex==3?Enumerable.Empty<InventoryRow>():filtered.Select((p,index)=>new InventoryRow(p.Item.Id,p.Item.Name,
            p.Owned?p.AvailablePoints>0?"보유 · 랭작 가능":"포르마 후 추가 마스터리":p.InFoundry?"제작 중":p.RankLocked?$"MR {p.Item.MasteryRequired} 필요":PurchaseRecommendationLabel(p,index),
            $"+{p.AvailablePoints:N0}점"+(PurchaseRoutePrice(p) is {} purchase?"\n"+purchase:p.DirectPurchaseCredits is {} cc?$"\n{cc:N0} Cr":p.BuyPlatinum is >0 and var cost?$"\n{cost:0.#} P":p.Credits>0?$"\n{p.Credits:N0} Cr":""),p));
        if(acquisitionGroup is null&&Ownership.SelectedIndex is 2 or 3&&Snapshot.MasteryComplete)
            rows=rows.Concat(CatalogData.Items.Where(i=>i.Masterable&&Snapshot.MasteryRanks.GetValueOrDefault(i.Id)>=i.MaxRank&&Matches(i,i.Name)).OrderBy(i=>i.Name,StringComparer.Ordinal)
                .Select(i=>new InventoryRow(i.Id,i.Name,Snapshot.Count(i.Id)>0?"완료 · 보유":"완료 · 과거 이력",$"{i.MaxRank} / {i.MaxRank}",i)));
        Replace(MasteryList,rows);RefreshRelics();ShowMastery();RefreshGoals();RefreshMaterials();
        MasteryCount.Text=$"MR {inventory.MasteryRank?.ToString()??"—"} · 목록 {MasteryList.Items.Count:N0}개";

    }
    private bool changingRows;
    private void Replace(ListBox list,IEnumerable<InventoryRow> rows)
    {
        var id=(list.SelectedItem as InventoryRow)?.Id;var previous=list.Items.Cast<InventoryRow>().Select(r=>r.Id).ToArray();var data=rows.ToArray();
        static ScrollViewer? Viewer(DependencyObject parent)
        {if(parent is ScrollViewer viewer)return viewer;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)if(Viewer(VisualTreeHelper.GetChild(parent,i)) is {} found)return found;return null;}
        var scroll=Viewer(list);double offset=scroll?.VerticalOffset??0;bool sameOrder=previous.SequenceEqual(data.Select(r=>r.Id));changingRows=true;
        try
        {
            bool bestFirst=ReferenceEquals(list,RelicList)&&!sameOrder&&!RelicDetails.IsExpanded;
            list.ItemsSource=data;list.SelectedItem=bestFirst?data.FirstOrDefault():data.FirstOrDefault(x=>x.Id==id)??data.FirstOrDefault();list.UpdateLayout();
            if(sameOrder)Viewer(list)?.ScrollToVerticalOffset(offset);else if(list.SelectedItem is not null)list.ScrollIntoView(list.SelectedItem);
        }
        finally{changingRows=false;}
    }
    private void Detail(StackPanel panel,string title,params string[] lines){panel.Children.Clear();panel.Children.Add(Text(title,23));foreach(var line in lines)panel.Children.Add(Text(line,15,"MutedBrush"));}
    private void ShowMastery()
    {
        if(MasteryList.SelectedItem is InventoryRow{Model:InventoryItem mastered})
        {
            Detail(MasteryDetail,mastered.Name,$"마스터리 완료 · 최고 랭크 {mastered.MaxRank}/{mastered.MaxRank}",Snapshot.Count(mastered.Id)>0?"현재 보유 장비":"판매 여부와 관계없이 과거 숙련 이력에 포함됩니다.");
            MasteryDetail.Children.Add(ActionButton("제작·수집 목표에 추가",()=>AddFarmingGoal(mastered.Id)));
            if(mastered.Category is "Primary" or "Secondary" or "Melee")MasteryDetail.Children.Add(ActionButton("이 장비 모딩하기",()=>ModRequested?.Invoke(mastered.Id)));
            foreach(var action in MasteryDetail.Children.OfType<Button>())action.Margin=new(0,0,8,8);
            MasteryDetail.Children.Add(new Expander{Header="획득처·구매 조건",Content=AcquisitionView(mastered.Id),Margin=new(0,16,0,0)});return;
        }
        if(MasteryList.SelectedItem is not InventoryRow{Model:MasteryPlan p}){Detail(MasteryDetail,"마스터리 도우미",Snapshot.MasteryComplete?"검색 조건에 맞는 후보가 없습니다.":"동기화하면 판매한 장비의 이력까지 확인해 중복 랭작을 제외합니다.");return;}
        Detail(MasteryDetail,p.Item.Name,$"최고 랭크 {p.EarnedRank}/{p.Item.MaxRank} · 남은 마스터리 +{p.RemainingPoints:N0}");
        AddPurchaseRouteSummary(p);
        if(p.Owned)
        {
            MasteryDetail.Children.Add(Text(p.AvailablePoints>0?"보유 장비 · 추가 구매 없이 랭작":"현재 열린 랭크의 마스터리 완료",15,"AccentBrush"));
            foreach(var equipment in Snapshot.Equipment.Where(e=>e.Id==p.Item.Id))MasteryDetail.Children.Add(Text($"현재 랭크 {equipment.Rank} · 포르마 {equipment.Forma}"+(p.Item.GildRequired?equipment.Gilded==true?" · 도금됨":" · 도금 확인 필요":""),14,"MutedBrush"));
        }
        else
        {
            MasteryDetail.Children.Add(Text(acquisition.For(p).Label+(p.Ready?" · 제작 준비됨":""),15,"AccentBrush"));
            MasteryDetail.Children.Add(Text(p.InFoundry?"주조소에서 제작 중입니다.":p.DirectPurchaseCredits is {} cc?$"완성품 구매 {cc:N0} 크레딧 · 구매 후 바로 랭작":!CatalogData.Recipes.Any(r=>r.ResultId==p.Item.Id)?"직접 획득·조립 장비 · 아래 획득처에서 조건 확인":$"설계도 구매 {p.BlueprintPurchaseCredits:N0} + 제작 {p.Credits-p.BlueprintPurchaseCredits:N0} 크레딧\n재료 준비 후 {TimeSpan.FromSeconds(p.BuildSeconds):g}",14,"MutedBrush"));
            if(p.BuyPlatinum is >0 and var price)MasteryDetail.Children.Add(Text($"거래 부품 {price:0.#} P · 마스터리 1,000당 {p.PlatinumPer1000Mastery:0.##} P",15));
            if(p.OpportunityPlatinum>0)MasteryDetail.Children.Add(Text($"사용할 보유 부품 시세 {p.OpportunityPlatinum:0.#} P · 추가 구매비와 별도",13,"MutedBrush"));
        }
        if(p.AdditionalForma>0)MasteryDetail.Children.Insert(2,Text($"현재 열린 랭크까지 +{p.AvailablePoints:N0} · 최종 랭크까지 포르마 {p.AdditionalForma}개 추가 필요 · 포르마 비용 별도",15,"AccentBrush"));
        if(!p.Owned&&!p.InFoundry)MasteryDetail.Children.Add(Text(CatalogData.Recipes.Any(r=>r.ResultId==p.Item.Id)||p.DirectPurchaseCredits is not null?
            $"마스터리 1,000당 {p.CreditsPer1000Mastery:N0} 크레딧 · 추가 획득 {p.MissingAcquisitions}종":
            $"직접 획득·조립 · 추가 획득 {p.MissingAcquisitions}종",15,"AccentBrush"));
        var detailMessages=MasteryDetail.Children.OfType<TextBlock>().Select(t=>t.Text).ToHashSet(StringComparer.Ordinal);
        foreach(var w in p.Warnings)if(detailMessages.Add(w))MasteryDetail.Children.Add(Text(w,14,"AccentBrush"));
        if(p.PricesStale)MasteryDetail.Children.Add(Text("12시간 이상 지난 시세 포함",14,"AccentBrush"));
        var goalAction=ActionButton(Farming.Document.Goals.Any(g=>g.ItemId==p.Item.Id)?"내 목표 보기":"제작·수집 목표에 추가",()=>AddFarmingGoal(p.Item.Id));
        UiHierarchy.SetPrimary(goalAction,Snapshot.CapturedUtc!=DateTimeOffset.MinValue&&!p.Owned&&!p.InFoundry);MasteryDetail.Children.Add(goalAction);
        var relicAction=ActionButton("필요한 부품의 성유물 찾기",()=>{goal=p.Item.Id;selectedGoalId=null;RefreshRelics();RelicPlanningRequested?.Invoke();RelicList.Focus();});
        var targets=p.Requirements.Where(r=>r.Missing>0).Select(r=>r.Id).ToHashSet();relicAction.IsEnabled=CatalogData.Relics.Any(r=>r.Rewards.Any(s=>targets.Contains(s.Id)));if(relicAction.IsEnabled)MasteryDetail.Children.Add(relicAction);
        if(p.Item.Category is "Primary" or "Secondary" or "Melee")MasteryDetail.Children.Add(ActionButton("이 장비 모딩하기",()=>ModRequested?.Invoke(p.Item.Id)));
        if(RefreshQuoteButton.Parent is Panel old)old.Children.Remove(RefreshQuoteButton);
        RefreshQuoteButton.IsEnabled=quoteCancellation is not null||QuoteTargets(p).Length>0;RefreshQuoteButton.Visibility=RefreshQuoteButton.IsEnabled?Visibility.Visible:Visibility.Collapsed;MasteryDetail.Children.Add(RefreshQuoteButton);
        if(QuoteStatus.Parent is Panel statusParent)statusParent.Children.Remove(QuoteStatus);QuoteStatus.Visibility=RefreshQuoteButton.IsEnabled&&(quoteItemId is null||quoteItemId==p.Item.Id)?Visibility.Visible:Visibility.Collapsed;MasteryDetail.Children.Add(QuoteStatus);
        foreach(var action in MasteryDetail.Children.OfType<Button>())action.Margin=new(0,0,8,8);
        MasteryDetail.Children.Add(BatchView());
        if(p.Owned||p.InFoundry)MasteryDetail.Children.Add(new Expander{Header="설계도·장비 획득처",Content=AcquisitionView(p.Item.Id),Margin=new(0,12,0,0)});
        else{MasteryDetail.Children.Add(UiHierarchy.Heading("설계도·장비 획득처"));MasteryDetail.Children.Add(AcquisitionView(p.Item.Id));}
        if(p.Requirements.Length>0)MasteryDetail.Children.Add(UiHierarchy.Heading("필요 재료 · 보유 / 필요"));
        foreach(var r in p.Requirements)
        {
            var block=new StackPanel{Margin=new(0,0,0,16)};
            block.Children.Add(Text($"{CatalogData.Name(r.Id)}\n{r.Owned:N0} / {r.Needed:N0}"+(r.CreditBlueprint?" · 크레딧 설계도":r.Missing>0?$" · 부족 {r.Missing:N0}"+(r.BuyPlatinum is >0 and var price?$" · {price:0.#} P":""):" · 보유"),15));
            if(prices.TryGetValue(r.Id,out var q))block.Children.Add(Text($"판매 최저 등록 호가 · {q.Platinum:0.#} P/개 · {q.FetchedUtc.LocalDateTime:MM-dd HH:mm}"+(q.Stale?" · 오래된 시세":""),13,"MutedBrush"));
            if(r.Missing>0&&InventoryAcquisition.Find(r.Id) is {Routes.Length:>0})block.Children.Add(new Expander{Header="획득처·구매 조건",Content=AcquisitionView(r.Id),Margin=new(0,5,0,0)});
            MasteryDetail.Children.Add(block);
        }
        MasteryDetail.Children.Add(new Expander{Header="추천 정렬 기준",Content=Text("보유 장비 → 제작 중 → ① 크레딧 구매 → ② 미션·퀘스트·평판/이벤트 교환 → ③ 플래티넘 구매. 획득 MR이 부족한 장비는 뒤로 보냅니다. 같은 그룹에서는 제작 준비 여부, 마스터리 1,000당 플래티넘·크레딧, 부족한 항목 수를 우선합니다. 제작시간은 마지막 보조 기준이며 독립 장비의 제작시간을 합산하지 않습니다. 거래 부품의 호가가 모두 확인되면 플래티넘 구매 후보로 비교합니다. 추가 포르마 없이는 점수가 없는 보유 장비는 뒤로 보냅니다. 클랜·퀘스트·평판 해금 여부는 계정 데이터에서 확인되지 않으므로 획득 조건을 함께 표시합니다.",14,"MutedBrush"),Margin=new(0,8,0,0)});
    }
    private void RefreshRelics()
    {
        if(!dashboardBuilt)return;var target=plans.FirstOrDefault(p=>p.Item.Id==goal);
        var targets=target?.Requirements.Where(r=>r.Missing>0).Select(r=>r.Id).ToHashSet();
        if(target is null&&Farming.Plan.Goals.Length>0)targets=Farming.Plan.Goals.Where(g=>selectedGoalId is null||g.Goal.ItemId==selectedGoalId).SelectMany(g=>g.Requirements).Where(r=>r.Missing>0).Select(r=>r.ItemId).ToHashSet();activeTargets=targets??[];
        string goalTitle=target?.Item.Name??(Farming.Plan.Goals.Length>0?(selectedGoalId is {} selected?CatalogData.Name(selected):"내 목표 전체"):"보유 성유물");
        RelicGoal.Text=goalTitle+(targets is {Count:>0}?" · 부족 부품":"")+(Squad.SelectedIndex==0?" · 1인 개봉":$" · 동일 성유물 {Squad.SelectedIndex+1}명");ClearGoal.IsEnabled=goal is not null||selectedGoalId is not null;
        if(!long.TryParse(Traces.Text,out var traces)||traces<0)
        {
            RelicInputError.Text="보이드 잔영은 0 이상의 정수로 입력하세요.";RelicInputError.Visibility=Visibility.Visible;Traces.SetResourceReference(Control.BorderBrushProperty,"ErrorBrush");
            Detail(RelicDetail,"보이드 잔영 확인","0 이상의 정수를 입력하세요.");RelicList.ItemsSource=null;RelicEmpty.Text="입력 확인 필요 · 추천 조건의 보이드 잔영을 확인하세요.";RelicEmpty.Visibility=Visibility.Visible;return;
        }
        RelicInputError.Visibility=Visibility.Collapsed;Traces.SetResourceReference(Control.BorderBrushProperty,"StrokeBrush");
        var candidates=InventoryRelicPlanner.Recommend(CatalogData,Snapshot,prices,ducats,targets,Squad.SelectedIndex+1,traces)
            .Where(r=>OnlyAffordable.IsChecked!=true||r.Affordable).Where(r=>targets is not {Count:>0}||r.TargetChance>0)
            .Select(r=>(Plan:r,Value:RelicValueChance.Evaluate(items,r.Relic,prices,ValueThreshold,Squad.SelectedIndex+1))).ToArray();
        double Score((RelicPlan Plan,RelicValueChance Value) r)=>targets is {Count:>0}?r.Plan.TargetChance:RelicOrder.SelectedIndex switch{1=>r.Plan.ExpectedPlatinum??-1,2=>r.Plan.ExpectedDucats,_=>r.Value.Probability};
        var sorted=candidates.OrderByDescending(Score).ThenByDescending(r=>r.Value.Exact).ThenByDescending(r=>r.Plan.ExpectedPlatinum??-1).ThenBy(r=>r.Plan.TraceCost).ThenByDescending(r=>r.Plan.ExpectedDucats).ToArray();
        if(AllRefinements.IsChecked!=true)sorted=sorted.DistinctBy(r=>r.Plan.Relic.Family).ToArray();
        RelicCriteria.Text=targets is {Count:>0}?"목표 부품 획득 확률순":RelicOrder.SelectedIndex switch{1=>"플래티넘 기댓값순",2=>"두캇 기댓값순",_=>$"{ValueThreshold:0} P 이상 보상 확률순"};
        Replace(RelicList,sorted.Select(r=>new InventoryRow(r.Plan.Relic.Id+":"+r.Plan.SourceRefinement,r.Plan.Relic.Name,
            $"{InventoryRelicPlanner.Refinements[r.Plan.SourceRefinement]} → {InventoryRelicPlanner.Refinements[r.Plan.Relic.Refinement]} · 보유 {r.Plan.Owned}\n잔영 {r.Plan.TraceCost}"+(r.Plan.PricesStale?" · 오래된 시세":""),
            targets is {Count:>0}?$"목표 {r.Plan.TargetChance:P1}":RelicOrder.SelectedIndex==2?$"{r.Plan.ExpectedDucats:0.#} 두캇":r.Value.Probability==0&&!r.Value.Exact?"시세 확인 필요":$"{(r.Value.Exact?"":"최소 ")}{r.Value.Probability:P1} · "+(r.Plan.ExpectedPlatinum is {} p?$"기댓값 {p:0.##} P":"시세 일부"),r.Plan){Meter=targets is {Count:>0}?r.Plan.TargetChance:RelicOrder.SelectedIndex==2||r.Value.Probability==0&&!r.Value.Exact?null:r.Value.Probability}));
        RelicEmpty.Text=Snapshot.CapturedUtc==DateTimeOffset.MinValue?"보유 성유물을 동기화하면 추천이 표시됩니다.":"조건에 맞는 보유 성유물이 없습니다. 추천 조건을 변경해 보세요.";
        RelicEmpty.Visibility=RelicList.Items.Count==0?Visibility.Visible:Visibility.Collapsed;ShowRelic();
    }
    private void ShowRelic()
    {
        if(RelicList.SelectedItem is not InventoryRow{Model:RelicPlan r}){Detail(RelicDetail,"성유물 계획",goal is null?"보유 성유물을 동기화하면 개봉·정련 후보가 표시됩니다.":"목표 부품이 나오는 보유 성유물이 없거나 현재 잔영 조건을 만족하지 않습니다.","분대 비교는 전원이 같은 성유물·같은 정련도를 사용할 때입니다. 공개 분대의 보상은 미리 알 수 없습니다.");return;}
        Detail(RelicDetail,r.Relic.Name,InventoryRelicPlanner.Refinements[r.SourceRefinement]+" → "+InventoryRelicPlanner.Refinements[r.Relic.Refinement]+$" · 1개당 잔영 {r.TraceCost}",$"동일 성유물 {r.SquadSize}명 · 가장 높은 보상 하나를 선택하는 기댓값",r.ExpectedPlatinum is {} p?$"판매 호가 기댓값 {p:0.##} P":"플래티넘 기댓값 미확인 · 시세 누락 포함",$"두캇 기댓값 {r.ExpectedDucats:0.##}");
        if(activeTargets.Count>0){string within=r.ChanceWithinOwned>.999&&r.ChanceWithinOwned<1?">99.9%":r.ChanceWithinOwned.ToString("P2");RelicDetail.Children.Add(Text($"부족한 부품 중 하나 · 1회 확률 {r.TargetChance:P2} · 보유 수량 {r.Owned}회 중 한 번 이상 {within}",15));RelicDetail.Children.Add(Text("제작에 필요한 모든 부품을 모을 확률은 아닙니다. 보유 수량 확률은 각 회차에도 같은 분대를 유지하고 필요한 잔영을 확보한다는 조건입니다.",14,"MutedBrush"));}
        if(r.Relic.Family.Contains("/T5",StringComparison.Ordinal))RelicDetail.Children.Add(Text("기존 레퀴엠은 보유 정련도만 비교하며 추가 정련을 제안하지 않습니다.",14,"MutedBrush"));
        if(goal is {} id)RelicDetail.Children.Add(Text("제작 목표 · "+CatalogData.Name(id),16,"AccentBrush"));
        if(r.PricesStale)RelicDetail.Children.Add(Text("12시간 이상 지난 시세 포함",14,"AccentBrush"));
        var chance=RelicValueChance.Evaluate(items,r.Relic,prices,ValueThreshold,r.SquadSize);
        RelicDetail.Children.Add(Text($"{ValueThreshold:0} P 이상 보상 · {(chance.Exact?"":"최소 ")}{chance.Probability:P1} · 시세 확인 {chance.KnownRewards}/{chance.TotalRewards}",15,"AccentBrush"));
        if(!chance.Exact)RelicDetail.Children.Add(Text("누락된 호가를0P로 간주하지 않습니다. 확인된 보상만으로 계산한 확률 하한이며 시세가 채워지면 달라집니다.",13,"MutedBrush"));
        RelicDetail.Children.Add(UiHierarchy.Heading("보상 · 개인 개봉 확률"));
        foreach(var slot in r.Relic.Rewards)
        {
            double probability=InventoryRelicPlanner.Probability(slot.Rarity,r.Relic.Refinement);
            RelicDetail.Children.Add(Text($"{CatalogData.Name(slot.Id)}"+(slot.Count>1?$" ×{slot.Count}":"")+$"\n{probability:P2} · 보유 {Snapshot.Count(slot.Id)}"+(prices.TryGetValue(slot.Id,out var q)?$" · {q.Platinum:0.#} P":items.GetValueOrDefault(slot.Id)?.Tradable==false?" · 거래 불가":" · 시세 없음"),15));
            var bar=new Grid{Height=4,Margin=new(0,0,8,14)};bar.SetResourceReference(Panel.BackgroundProperty,"StrokeBrush");bar.ColumnDefinitions.Add(new(){Width=new(probability,GridUnitType.Star)});bar.ColumnDefinitions.Add(new(){Width=new(1-probability,GridUnitType.Star)});
            var fill=new Border{Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(slot.Rarity=="RARE"?"#EDCE60":slot.Rarity=="UNCOMMON"?"#A9BCC5":"#AD774F"))};bar.Children.Add(fill);RelicDetail.Children.Add(bar);
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();automaticContinuation.Stop();Farming.Changed-=FarmingChanged;purchaseCancellation?.Cancel();quoteCancellation?.Cancel();lifetime.Cancel();reader.Dispose();market.Dispose();lifetime.Dispose();}
}
