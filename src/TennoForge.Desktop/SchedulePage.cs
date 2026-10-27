using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage : Grid,IDisposable
{
    internal readonly ScheduleCatalog CatalogData=ScheduleCatalog.Load();
    internal WorldScheduleData Data {get;private set;}
    internal readonly Expander ArbitrationExpanded,WeaponExpanded;
    internal readonly Button RefreshButton;
    internal readonly ComboBox Tier=new(){ItemsSource=new[]{"S티어","S · A티어","모든 등급"},SelectedIndex=0,MinWidth=115};
    internal readonly ComboBox Horizon=new(){ItemsSource=new[]{"24시간","3일","7일"},SelectedIndex=1,MinWidth=100};
    internal readonly MissionChecklist Mission;
    internal readonly ComboBox Faction=new(){MinWidth=140};
    internal readonly ComboBox Shop=new(){ItemsSource=new[]{"전체 상점","테넷 · 에르고 글래스트","코다 · 엘레노어"},SelectedIndex=0,MinWidth=230};
    internal readonly ToggleButton WishOnly=new(){Content="찜한 무기만",MinHeight=34,Margin=new(20,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    internal readonly Button ResetFilters;
    internal readonly ListBox ArbitrationList=List(),WeaponList=List();
    internal readonly StackPanel ArbitrationDetail=new(),WeaponDetail=new();
    internal readonly TextBlock NextTitle=Text("다음 S티어 중재",17),NextCountdown=Text("—",36,"AccentBrush"),NextNode=Text("",21),Status=Text("",13,"MutedBrush");
    internal readonly TextBlock Ongoing=Text("",15,"AccentBrush");
    private readonly TextBlock currentArbitration=Text("",14),shopClocks=Text("",14),reportStatus=Text("원소·보너스는 회차별 위키 제보를 따로 확인합니다.",13,"MutedBrush");
    private readonly TextBlock emptyArbitration=Text("이 기간에 조건에 맞는 중재가 없습니다. 필터나 기간을 바꿔 보세요.",16,"MutedBrush");
    private readonly Dictionary<string,(TextBlock State,TextBlock Timer,TextBlock Next,ProgressBar Progress,TextBlock Source)> cycleViews=new();
    private readonly Dictionary<string,VendorReport> reports=new();
    internal HashSet<string> Wishes {get;private set;}=[];
    internal Action<string>? ModRequested;
    internal FarmingWorkspace? Farming {get;}
    internal Func<bool,CancellationToken,Task<WorldScheduleData>>? RefreshOverride {get;set;}
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly CancellationTokenSource lifetime=new();
    private readonly WorldScheduleClient? client;
    private readonly VendorReportClient? reportClient;
    private readonly bool persist,writePreferences;
    private bool busy,reportBusy,disposed;
    private bool dashboardDirty=true;
    private Window? dashboardHost;
    internal bool? DashboardVisibilityOverride {get;set;}
    internal int DashboardUpdateCount {get;private set;}
    private bool DashboardVisible=>DashboardVisibilityOverride??(!persist||IsLoaded&&IsVisible&&dashboardHost?.WindowState!=WindowState.Minimized);
    private long minute=-1;
    private DateTimeOffset nextRefresh,nextReport;
    private readonly string folder;
    internal Func<DateTimeOffset> Clock=()=>DateTimeOffset.UtcNow;
    private static TextBlock Text(string text,double size=15,string brush="TextBrush")=>RivenPresentation.Text(text,size,brush);
    private static Button Button(string label,Action action)
    {var b=new Button{Content=label,MinHeight=36,Padding=new(14,6,14,6),Margin=new(0,0,10,10)};b.Click+=(_,_)=>action();AutomationProperties.SetName(b,label);return b;}
    private static FrameworkElement Scroll(UIElement content)=>new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0)};
    private static ListBox List()
    {
        var box=new ListBox{Background=Brushes.Transparent,BorderThickness=new(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new(0)};
        ScrollViewer.SetHorizontalScrollBarVisibility(box,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(box,ScrollBarVisibility.Auto);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,10,12,10)));style.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0,0,0,4)));style.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("TextBrush")));
        var template=new ControlTemplate(typeof(ListBoxItem));var border=new FrameworkElementFactory(typeof(Border));border.Name="Row";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty));border.AppendChild(presenter);template.VisualTree=border;
        var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("AccentSurfaceBrush"),"Row"));template.Triggers.Add(selected);var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("HoverBrush"),"Row"));template.Triggers.Add(hover);style.Setters.Add(new Setter(Control.TemplateProperty,template));box.ItemContainerStyle=style;return box;
    }
    internal SchedulePage(bool persist=true,string? preferenceFolder=null,FarmingWorkspace? farming=null)
    {
        this.persist=persist;Farming=farming;writePreferences=persist||preferenceFolder is not null;folder=preferenceFolder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","schedules");
        Preferences=ReadPreferences();
        if(persist){client=new(folder);reportClient=new();Data=client.Current;LoadWishes();}
        else {Data=new(null,ArbitrationSchedule.Bundled(CatalogData),true,null,DateTimeOffset.MinValue);if(writePreferences)LoadWishes();}
        RefreshButton=Button("새로고침",()=>_=RefreshAsync(true));
        Mission=new(CatalogData.Nodes.Select(n=>n.Mission).Distinct(),Preferences.ExcludedMissions);
        ResetFilters=Button("초기화",()=>{Tier.SelectedIndex=0;Horizon.SelectedIndex=1;Mission.SetExcluded([]);Faction.SelectedIndex=0;});
        Faction.ItemsSource=new[]{"모든 팩션"}.Concat(CatalogData.Nodes.SelectMany(n=>n.Faction.Split('·',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(x=>x)).ToArray();Faction.SelectedIndex=0;
        (ArbitrationExpanded,WeaponExpanded)=BuildScheduleLayout();
        WishOnly.SetResourceReference(StyleProperty,"ScheduleFilterChip");
        void FilterChanged(){RebuildArbitrations();Tick();}
        Tier.SelectionChanged+=(_,_)=>FilterChanged();Horizon.SelectionChanged+=(_,_)=>RebuildArbitrations();Mission.Changed+=()=>{Preferences=Preferences with{ExcludedMissions=Mission.Excluded};SavePreferences();FilterChanged();};Faction.SelectionChanged+=(_,_)=>FilterChanged();Shop.SelectionChanged+=(_,_)=>RebuildWeapons();WishOnly.Checked+=(_,_)=>RebuildWeapons();WishOnly.Unchecked+=(_,_)=>RebuildWeapons();
        ArbitrationList.SelectionChanged+=(_,_)=>{ShowArbitration();if(ArbitrationList.IsKeyboardFocusWithin)ArbitrationExpanded.IsExpanded=true;};WeaponList.SelectionChanged+=(_,_)=>{ShowWeapon();if(WeaponList.IsKeyboardFocusWithin)WeaponExpanded.IsExpanded=true;};
        AutomationProperties.SetName(Tier,"중재 등급 필터");AutomationProperties.SetName(Horizon,"중재 조회 기간");AutomationProperties.SetName(Mission,"중재 미션 유형 필터");AutomationProperties.SetName(Faction,"중재 팩션 필터");AutomationProperties.SetName(Shop,"무기 상점 선택");
        if(persist)
        {
            Loaded+=async(_,_)=>{WatchDashboardWindow(Window.GetWindow(this));StartMonitoring();Tick();await RefreshAsync(false);};
            Unloaded+=(_,_)=>{WatchDashboardWindow(null);dashboardDirty=true;if(!BackgroundNeeded)timer.Stop();};
            IsVisibleChanged+=(_,_)=>{if(IsLoaded)Tick();};
            timer.Tick+=async(_,_)=>{Tick();if(Clock()>=nextRefresh)await RefreshAsync(false);};
        }
        UseData(Data);
    }
    internal event Action? DataChanged;
    internal void UseData(WorldScheduleData data){Data=data;dashboardDirty=true;Tick();DataChanged?.Invoke();}
    private void WatchDashboardWindow(Window? host)
    {
        if(ReferenceEquals(dashboardHost,host))return;
        if(dashboardHost is not null)dashboardHost.StateChanged-=DashboardStateChanged;
        dashboardHost=host;
        if(dashboardHost is not null)dashboardHost.StateChanged+=DashboardStateChanged;
    }
    private void DashboardStateChanged(object? sender,EventArgs e){if(IsLoaded&&IsVisible)Tick();}
    internal async Task RefreshAsync(bool force)
    {
        if(busy||disposed)return;busy=true;RefreshButton.IsEnabled=false;RefreshButton.Content="갱신 중…";
        try{if(RefreshOverride is {} test)UseData(await test(force,lifetime.Token));else if(client is not null)UseData(await client.RefreshAsync(force,lifetime.Token));}
        catch(OperationCanceledException){}finally{busy=false;nextRefresh=Clock().AddMinutes(1);if(!disposed){RefreshButton.IsEnabled=true;RefreshButton.Content="새로고침";}}
        if(!disposed&&reportClient is not null&&(force||Clock()>=nextReport))_=LoadReports();
    }
    private async Task LoadReports()
    {
        if(reportBusy||reportClient is null)return;reportBusy=true;nextReport=Clock().AddHours(1);reportStatus.Text="회차별 위키 제보 확인 중… · 일정은 바로 사용할 수 있습니다.";
        try{var result=await Task.WhenAll(new[]{"tenet","coda"}.Select(s=>reportClient.FetchAsync(CatalogData,s,Clock(),lifetime.Token)));if(disposed)return;foreach(var report in result)reports[report.Shop]=report;RebuildWeapons();reportStatus.Text="원소·보너스: 위키 제보 기준 · 상세에서 출처 확인";reportStatus.ToolTip="값이 바뀐 회차만 현회차 제보로 표시합니다. 무기를 선택하면 원문 개정과 제보 시각을 확인할 수 있습니다.";}
        catch(OperationCanceledException){}finally{reportBusy=false;}
    }
    internal void Tick()
    {
        if(disposed)return;
        // Keep due alerts, expiry checks and the game overlay alive while another
        // page is open, but do not rebuild an unloaded/minimized dashboard each
        // second. Loaded/visibility/restore refresh the deferred state at once.
        if(!DashboardVisible){dashboardDirty=true;TickFeatures();return;}
        DashboardUpdateCount++;
        var now=Clock();var next=Data.Arbitrations.NextS(CatalogData,now,CurrentFilter() with{Tier="S"});var current=Data.Arbitrations.Current(now);var node=current is null?null:CatalogData.Nodes.FirstOrDefault(n=>n.Id==current.NodeId);bool conflict=Data.ArbitrationConflict(now);
        NextTitle.Text=conflict?"다음 S티어 중재 · 일정 불일치 확인 필요":"다음 S티어 중재";NextCountdown.Text=next is null?"일정 없음":WorldScheduleEngine.Countdown(next.StartUtc,now);
        if(!conflict&&(Mission.Excluded.Length>0||Faction.SelectedIndex>0))NextTitle.Text="다음 S티어 중재 · 미션·팩션 필터 적용";
        var nextNode=next is null?null:CatalogData.Nodes.FirstOrDefault(n=>n.Id==next.NodeId);NextNode.Text=nextNode is null?"선택한 미션·팩션에 맞는 S티어 일정이 없습니다.":$"{nextNode.Planet} · {nextNode.Name} · {nextNode.Mission}   {WorldScheduleEngine.KoreaTime(next!.StartUtc)}";
        Status.Text=conflict?"실시간 노드와 예측표 불일치 · 현재 예측은 참고용":$"커뮤니티 S티어 · 예측 일정 · 한국 시간(KST) · {Data.Arbitrations.RetrievedUtc.ToOffset(TimeSpan.FromHours(9)):MM-dd HH:mm} 갱신";
        Status.ToolTip=Data.Notice??"등급: Arbitration Goons / browse.wf · 게임 공식 등급이 아닙니다.";
        currentArbitration.Text=node is null?"현재 시간의 중재 일정이 없습니다.":$"{(node.Tier=="S"?"지금 S티어 진행 중":"현재 중재")}  ·  {node.Planet} / {node.Name} / {node.Mission}  ·  {WorldScheduleEngine.Countdown(current!.EndUtc,now)} 남음  ·  예측표 기준";
        Ongoing.Visibility=node?.Tier=="S"&&!conflict&&CurrentFilter().Matches(node)?Visibility.Visible:Visibility.Collapsed;Ongoing.Text=node?.Tier=="S"?$"지금 S티어 · {node.Planet} / {node.Name} · 종료까지 {WorldScheduleEngine.Countdown(current!.EndUtc,now)} · 예측표 기준":"";
        foreach(var def in WorldScheduleEngine.Definitions)
        {
            var view=cycleViews[def.Id];var anchor=Data.Snapshot?.Cycles.FirstOrDefault(c=>c.Id==def.Id);var phase=anchor is null?null:WorldScheduleEngine.CycleAt(anchor,now);
            if(phase is null){view.State.Text="불러오기 전";view.Timer.Text="—";view.Next.Text=Data.Notice??"온라인 갱신이 필요합니다.";continue;}
            view.State.Text=phase.Label;var color=(Color)ColorConverter.ConvertFromString(def.Colors[phase.StateIndex]);if(!Theme.IsDark)color=Color.FromRgb((byte)(color.R*.5),(byte)(color.G*.5),(byte)(color.B*.5));view.State.Foreground=new SolidColorBrush(color);view.Timer.Text=WorldScheduleEngine.Countdown(phase.EndUtc,now);view.Progress.Value=phase.Progress(now);
            string nextLabel=def.Labels[(phase.StateIndex+1)%def.Labels.Length];view.Next.Text=$"다음 {nextLabel} · {TimeZoneInfo.ConvertTime(phase.EndUtc,WorldScheduleEngine.Korea):HH:mm}";view.Source.Text=Data.CycleFresh(anchor!,now)?anchor!.Source=="공식 월드 상태"?"공식 자료":"월드 상태":"저장 자료 예상";view.Source.SetResourceReference(TextBlock.ForegroundProperty,Data.CycleFresh(anchor!,now)?"MutedBrush":"AccentBrush");view.Source.ToolTip=view.Next.ToolTip=$"기준 자료: {WorldScheduleEngine.KoreaTime(anchor!.SourceUtc??Data.Snapshot!.Timestamp)} · {anchor.Source}\n{Data.Notice}";
        }
        var tenet=WorldScheduleEngine.ShopAt(CatalogData,"tenet",now);var coda=WorldScheduleEngine.ShopAt(CatalogData,"coda",now);TenetClock.Text=WorldScheduleEngine.Countdown(tenet.EndUtc,now);CodaClock.Text=WorldScheduleEngine.Countdown(coda.EndUtc,now);CodaPhase.Text=$"코다 {coda.Batch} → {(coda.Batch=="A"?"B":"A")} · 교체까지";shopClocks.Text=$"테넷 교체 {WorldScheduleEngine.Countdown(tenet.EndUtc,now)}   ·   코다 {coda.Batch} 회차 → {(coda.Batch=="A"?"B":"A")}  {WorldScheduleEngine.Countdown(coda.EndUtc,now)}   ·   위키 주기 예상";
        long m=now.ToUnixTimeSeconds()/3600;if(dashboardDirty||m!=minute){dashboardDirty=false;minute=m;RebuildArbitrations();RebuildWeapons();}
        RefreshIncarnon(now);TickFeatures();
    }
    private static ListBoxItem Row(object tag,string title,string subtitle)
    {var item=new ListBoxItem{Tag=tag,Content=RivenPresentation.Stack(Text(title,17),Text(subtitle,14,"MutedBrush"))};AutomationProperties.SetName(item,title+" · "+subtitle);return item;}
    internal void RebuildArbitrations()
    {
        var selected=(ArbitrationList.SelectedItem as ListBoxItem)?.Tag as ArbitrationSlot;ArbitrationList.Items.Clear();var now=Clock();int hours=Horizon.SelectedIndex switch{0=>24,2=>168,_=>72};
        foreach(var slot in Data.Arbitrations.Window(now,hours))
        {var n=CatalogData.Nodes.FirstOrDefault(n=>n.Id==slot.NodeId);if(n is null||!CurrentFilter().Matches(n))continue;ArbitrationList.Items.Add(Row(slot,$"{n.Tier}  ·  {n.Planet} / {n.Name}",$"{WorldScheduleEngine.KoreaTime(slot.StartUtc)}  ·  {n.Mission}  ·  {n.Faction}"));}
        ArbitrationList.SelectedItem=ArbitrationList.Items.Cast<ListBoxItem>().FirstOrDefault(i=>((ArbitrationSlot)i.Tag).StartUtc==selected?.StartUtc)??ArbitrationList.Items.Cast<ListBoxItem>().FirstOrDefault();ShowArbitration();
        emptyArbitration.Visibility=ArbitrationList.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    private ArbitrationFilter CurrentFilter()=>new(Tier.SelectedIndex switch{0=>"S",1=>"SA",_=>""},null,Faction.SelectedIndex>0?Faction.SelectedItem as string:null,Mission.Excluded);
    private void ShowArbitration()
    {
        ArbitrationDetail.Children.Clear();if((ArbitrationList.SelectedItem as ListBoxItem)?.Tag is not ArbitrationSlot slot){ArbitrationDetail.Children.Add(Text("이 기간에 해당하는 중재가 없습니다. 기간이나 등급을 바꿔 보세요.",17));return;}
        var node=CatalogData.Nodes.Single(n=>n.Id==slot.NodeId);ArbitrationDetail.Children.Add(Text($"{node.Tier}티어 · {node.Name}",24,"AccentBrush"));ArbitrationDetail.Children.Add(Text($"{node.Planet} · {node.Mission} · {node.Faction}",17));ArbitrationDetail.Children.Add(Text("시작  "+WorldScheduleEngine.KoreaTime(slot.StartUtc),19));ArbitrationDetail.Children.Add(Text("종료  "+WorldScheduleEngine.KoreaTime(slot.EndUtc),19));ArbitrationDetail.Children.Add(Text("Arbitration Goons의 커뮤니티 등급입니다. 수익·진행 효율에 대한 판단이며 게임 공식 등급이나 개인 빌드의 성공 보장은 아닙니다.",15,"MutedBrush"));ArbitrationDetail.Children.Add(Text($"출처: {Data.Arbitrations.Source}\n예측표 범위: {TimeZoneInfo.ConvertTime(Data.Arbitrations.Slots[0].StartUtc,WorldScheduleEngine.Korea):yyyy-MM-dd HH:mm} ~ {TimeZoneInfo.ConvertTime(Data.Arbitrations.Slots[^1].EndUtc,WorldScheduleEngine.Korea):yyyy-MM-dd HH:mm} KST",14,"MutedBrush"));ArbitrationDetail.Children.Add(Button("등급·일정 출처 열기",()=>Open("https://browse.wf/arbys")));
        if(Data.ArbitrationConflict(Clock()))ArbitrationDetail.Children.Add(Text("실시간 중재와 예측 일정이 일치하지 않습니다. 현재 예측은 참고용입니다.",15,"ErrorBrush"));
    }
    internal void RebuildWeapons()
    {
        var selected=(WeaponList.SelectedItem as ListBoxItem)?.Tag as ScheduledWeapon;WeaponList.Items.Clear();var now=Clock();
        foreach(var weapon in CatalogData.Offers.OrderBy(w=>WorldScheduleEngine.WeaponNextStart(w,WorldScheduleEngine.ShopAt(CatalogData,w.Shop,now))).ThenBy(w=>w.Shop).ThenBy(w=>w.Name))
        {if(Shop.SelectedIndex==1&&weapon.Shop!="tenet"||Shop.SelectedIndex==2&&weapon.Shop!="coda"||WishOnly.IsChecked==true&&!Wishes.Contains(weapon.Id))continue;var window=WorldScheduleEngine.ShopAt(CatalogData,weapon.Shop,now);bool offered=WorldScheduleEngine.WeaponNextStart(weapon,window)<=now;WeaponList.Items.Add(Row(weapon,(Wishes.Contains(weapon.Id)?"★  ":"")+weapon.Name,$"{(offered?"현재 판매 회차 예상":"다음 회차 · "+WorldScheduleEngine.KoreaTime(window.EndUtc))}  ·  {weapon.Price} {weapon.Currency}"));}
        WeaponList.SelectedItem=WeaponList.Items.Cast<ListBoxItem>().FirstOrDefault(i=>((ScheduledWeapon)i.Tag).Id==selected?.Id)??WeaponList.Items.Cast<ListBoxItem>().FirstOrDefault();ShowWeapon();
    }
    private void ShowWeapon()
    {
        WeaponDetail.Children.Clear();if((WeaponList.SelectedItem as ListBoxItem)?.Tag is not ScheduledWeapon weapon){WeaponDetail.Children.Add(Text("해당하는 무기가 없습니다. 상점이나 찜 필터를 바꿔 보세요.",17));return;}
        var now=Clock();var window=WorldScheduleEngine.ShopAt(CatalogData,weapon.Shop,now);bool offered=WorldScheduleEngine.WeaponNextStart(weapon,window)<=now;
        WeaponDetail.Children.Add(Text(weapon.Name,24));WeaponDetail.Children.Add(Text(window.Name+(weapon.Shop=="coda"?$" · {weapon.Batch} 회차":""),16,"MutedBrush"));WeaponDetail.Children.Add(Text($"{weapon.Price} {weapon.Currency} · 마스터리 {weapon.MasteryRequired}",19,"AccentBrush"));WeaponDetail.Children.Add(Text((offered?"현재 판매 회차 예상":"다음 판매 예상")+"\n"+WorldScheduleEngine.KoreaTime(offered?window.StartUtc:window.EndUtc)+" ~ "+WorldScheduleEngine.KoreaTime(offered?window.EndUtc:window.EndUtc.AddDays(4)),17));
        var wish=new ToggleButton{Content="이 무기 찜하기",IsChecked=Wishes.Contains(weapon.Id),HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,12,0,18)};wish.SetResourceReference(StyleProperty,"ScheduleFilterChip");AutomationProperties.SetName(wish,weapon.Name+" 찜하기");wish.Checked+=(_,_)=>SetWish(weapon.Id,true);wish.Unchecked+=(_,_)=>SetWish(weapon.Id,false);WeaponDetail.Children.Add(wish);
        var bonus=reports.GetValueOrDefault(weapon.Shop)?.Bonuses.FirstOrDefault(b=>b.WeaponId==weapon.Id);bool current=offered&&bonus?.ChangedThisRotation==true&&bonus.RevisionUtc>=window.StartUtc&&bonus.RevisionUtc<window.EndUtc;
        WeaponDetail.Children.Add(Text(current?$"{VendorReportParser.ElementKo(bonus!.Element)}  +{bonus.Percent:0.0}% · 현회차 위키 제보":"현회차 원소·보너스 제보 미확인",19,current?"AccentBrush":"MutedBrush"));WeaponDetail.Children.Add(Text("4일마다 교체 · 회차당 무기별 1회 구매\n일정은 위키 주기 기준 예상입니다. 다음 회차의 무작위 원소·보너스는 미리 알 수 없습니다.",15,"MutedBrush"));
        if(bonus is not null){var details=RivenPresentation.Stack(Text($"{(current?"현회차 제보":"이전 참고값 · 현회차 확정 아님")}\n{VendorReportParser.ElementKo(bonus.Element)} +{bonus.Percent:0.0}%\n개정 {bonus.Revision} · {WorldScheduleEngine.KoreaTime(bonus.RevisionUtc)}",15),Button("제보 원문 열기",()=>Open(bonus.Source)));WeaponDetail.Children.Add(new Expander{Header="원소·보너스 출처",Content=details,Margin=new(0,12,0,16)});}
        WeaponDetail.Children.Add(Button("이 무기 모딩하기",()=>ModRequested?.Invoke(weapon.Id)));WeaponDetail.Children.Add(Button("상점 규칙 원문 열기",()=>Open("https://wiki.warframe.com/w/"+(weapon.Shop=="tenet"?"Tenet_Weapons":"Coda_Weapons"))));
    }
    internal void SetWish(string id,bool enabled){if(enabled)Wishes.Add(id);else Wishes.Remove(id);SaveWishes();RebuildWeapons();}
    internal void LoadWishes(){try{var saved=JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(folder,"wishlist.json")));Wishes=(saved??[]).Where(id=>CatalogData.Offers.Any(w=>w.Id==id)).ToHashSet();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}}
    private void SaveWishes(){if(!writePreferences)return;try{Directory.CreateDirectory(folder);var path=Path.Combine(folder,"wishlist.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Wishes));File.Move(path+".tmp",path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){reportStatus.Text="찜 목록을 저장하지 못했습니다. 이번 실행에서는 유지됩니다.";}}
    private static FrameworkElement Sources()=>new Expander{Header="자료 기준·갱신 방식",Margin=new(0,20,0,12),Content=new ScrollViewer{MaxHeight=96,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=RivenPresentation.Stack(Text("월드 상태: warframestat.us / PC · 화면을 보는 동안 1분마다 갱신\n주기 API 지연 시 평원·캠비온은 공식 worldState.php로 보완 · 지역별 기준 시각은 마우스 오버로 확인\n중재 일정: browse.wf · 6시간마다 갱신 · 등급: Arbitration Goons\n무기 상점: WF Wiki 주기 기준 예상 · 원소·보너스 제보는 1시간마다 확인\n타이머는 로컬에서 계산합니다. 오프라인에서는 마지막 주기 자료로 예상합니다.\n지구 숲은 공식 Update 38.5에 따라 평원과 같은 낮 100분·밤 50분입니다.",14,"MutedBrush"),Button("공식 주기 변경 내용",()=>Open("https://www.warframe.com/en/patch-notes/pc/38-5-0")),Button("커뮤니티 일정·등급 출처",()=>Open("https://browse.wf/about")))}};
    private static void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException){}}
    public void Dispose(){disposed=true;timer.Stop();WatchDashboardWindow(null);DisposeFeatures();lifetime.Cancel();client?.Dispose();reportClient?.Dispose();lifetime.Dispose();}
}
