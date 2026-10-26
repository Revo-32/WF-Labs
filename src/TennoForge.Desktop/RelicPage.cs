using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class RelicPage : Grid,IDisposable
{
    internal readonly RelicMarketClient Market;
    internal RelicCatalog Catalog{get;private set;}=RelicCatalog.Bundled();
    internal readonly List<RewardCard> Cards=[];
    internal readonly UniformGrid CardGrid=new(){Columns=4};
    internal readonly ComboBox Presence=new(){ItemsSource=new[]{"게임 중인 거래자","온라인 + 게임 중"},SelectedIndex=0,MinWidth=205};
    internal readonly CheckBox Crossplay=new(){Content="크로스플레이 포함",IsChecked=true,VerticalAlignment=VerticalAlignment.Center,Margin=new(18,0,18,0)};
    internal readonly CheckBox Shortcut=new(){Content="Ctrl + Shift + R",VerticalAlignment=VerticalAlignment.Center,Margin=new(16,0,0,0)};
    internal readonly CheckBox Automatic=new(){Content="성유물 보상 자동 인식",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,16,0)};
    internal readonly TextBlock AutoStatus=Text("자동 인식을 켜면 보상 화면에서 시세가 자동으로 표시됩니다.",14,"MutedBrush");
    internal readonly TextBlock PriceStoreStatus=Text("가격은 조회 시점의 판매 호가입니다.",14,"MutedBrush");
    internal readonly CheckBox Overlay=new(){Content="수동 캡처 오버레이",VerticalAlignment=VerticalAlignment.Center,Margin=new(16,0,0,0),ToolTip="게임 화면 읽기·단축키로 캡처할 때 게임 위에 표시합니다. 자동 인식은 항상 오버레이로 표시합니다. 창 모드 / 테두리 없는 창 · 클릭은 게임으로 통과"};
    internal event Action? ResultsChanged;
    internal double LastElapsedSeconds{get;private set;}
    internal double FirstPricedSeconds{get;private set;}
    internal string TimingLabel{get;private set;}="인식·시세";
    internal readonly TextBlock Status=Text("보상 화면을 읽거나, 아래 검색창에서 품목을 직접 고르세요.",15);
    private readonly TextBlock catalogStatus=Text("",14,"MutedBrush");
    internal readonly Button ReadButton,LoadButton,PasteButton,RefreshButton,CancelButton,AddButton,ReturnButton;
    private readonly Image preview=new(){Stretch=Stretch.Uniform};
    private readonly Grid previewHost=new(){Height=210,ClipToBounds=true};
    private readonly ScrollViewer viewScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0)};
    private readonly Canvas selectionLayer=new(){IsHitTestVisible=false};
    private readonly Rectangle selection=new(){StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(35,242,203,87)),Visibility=Visibility.Collapsed};
    private readonly Button cropButton,resetCrop;
    private readonly Expander imageExpander;
    private BitmapSource? source;
    private Int32Rect? crop;
    private Point? dragStart;
    private CancellationTokenSource? recognitionCancel;
    private CancellationTokenSource? priceRefresh;
    private bool? priceCrossplay;
    private bool suppress,disposed,catalogBusy;
    private long recognitionGeneration;
    internal Func<Task>? CaptureRequested;
    internal Action? GameReturnRequested;
    internal Action<bool>? HotkeyChanged;
    internal Action<bool>? AutomaticChanged;
    internal Func<BitmapSource?> ClipboardImage=()=>Clipboard.ContainsImage()?Clipboard.GetImage():null;
    internal Func<string?>? ChooseImageFile;
    internal RelicRecognition? LastRecognitionForCheck{get;private set;}
    internal bool Busy=>recognitionCancel is not null;
    internal FrameworkElement PreviewForCheck=>previewHost;
    internal Rect ImageBoundsForCheck=>ImageBounds();
    internal Int32Rect? CropForCheck=>crop;
    internal Button CropButtonForCheck=>cropButton;
    internal Button ZoomButton{get;}
    internal Window? PreviewWindowForCheck{get;private set;}
    internal RelicPage(RelicMarketClient? market=null)
    {
        Market=market??new RelicMarketClient(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","relic-market-cache"),snapshotMode:UiFonts.PersistChanges);
        RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var body=new StackPanel{Margin=new(4,10,4,16)};
        body.Children.Add(Text("성유물 보상",26));body.Children.Add(Text("플래티넘과 두캇을 비교해 보상을 고르세요.",15,"MutedBrush"));
        var actions=new WrapPanel{Margin=new(0,14,0,18)};
        ReadButton=Button("게임 화면 읽기",async()=>{if(CaptureRequested is not null)await CaptureRequested();});UiHierarchy.Primary(ReadButton);
        LoadButton=Button("이미지 불러오기",LoadImage);PasteButton=Button("붙여넣기",PasteImage);
        CancelButton=Button("인식 취소",CancelRecognition);CancelButton.Visibility=Visibility.Collapsed;
        foreach(var button in new[]{ReadButton,LoadButton,PasteButton,CancelButton})actions.Children.Add(button);
        actions.Children.Add(Shortcut);Shortcut.Checked+=(_,_)=>HotkeyChanged?.Invoke(true);Shortcut.Unchecked+=(_,_)=>HotkeyChanged?.Invoke(false);
        ReturnButton=Button("게임으로 돌아가기",()=>GameReturnRequested?.Invoke());ReturnButton.Margin=new(22,0,0,0);actions.Children.Add(ReturnButton);
        body.Children.Add(actions);
        var preferences=RelicPreferences.Read();Presence.SelectedIndex=preferences.Presence;Crossplay.IsChecked=preferences.Crossplay;
        var automaticRow=new WrapPanel{Margin=new(0,0,0,12)};Automatic.IsChecked=preferences.Automatic;
        automaticRow.Children.Add(Automatic);automaticRow.Children.Add(AutoStatus);body.Children.Insert(2,automaticRow);
        Automatic.Checked+=(_,_)=>AutomaticChanged?.Invoke(true);Automatic.Unchecked+=(_,_)=>AutomaticChanged?.Invoke(false);
        Overlay.IsChecked=preferences.Overlay;actions.Children.Add(Overlay);
        Overlay.Checked+=(_,_)=> (RelicPreferences.Read() with{Overlay=true}).Save();Overlay.Unchecked+=(_,_)=> (RelicPreferences.Read() with{Overlay=false}).Save();
        var filter=new WrapPanel{Margin=new(0,0,0,14)};filter.Children.Add(Text("PC 마켓",15));Presence.Margin=new(18,0,0,0);filter.Children.Add(Presence);filter.Children.Add(Crossplay);
        RefreshButton=Button("시세 새로고침",()=>_=RefreshAsync(true));filter.Children.Add(RefreshButton);
        var updateItems=Button("품목 목록 갱신",()=>_=RefreshCatalogAsync(true));filter.Children.Add(updateItems);body.Children.Add(filter);
        body.Children.Add(PriceStoreStatus);
        if(Market.SnapshotMode){PriceStoreStatus.Text="저장 시세 · 12시간마다 백그라운드 갱신";RefreshButton.Content="현재 호가 확인";}
        void Changed(){if(!suppress){(RelicPreferences.Read() with{Presence=Presence.SelectedIndex,Crossplay=Crossplay.IsChecked==true}).Save();StartPriceRefresh();_=RefreshAsync(false);}}
        Presence.SelectionChanged+=(_,_)=>Changed();Crossplay.Checked+=(_,_)=>Changed();Crossplay.Unchecked+=(_,_)=>Changed();
        Status.Margin=new(0,0,0,16);AutomationProperties.SetLiveSetting(Status,AutomationLiveSetting.Polite);body.Children.Add(Status);
        body.Children.Add(CardGrid);
        AddButton=Button("+ 보상 추가",()=>AddCard());AddButton.HorizontalAlignment=HorizontalAlignment.Left;AddButton.Margin=new(0,10,0,16);body.Children.Add(AddButton);
        body.Children.Add(Text("판매 최저는 표시된 조회 시점의 개당 판매 호가입니다. 저장 시세는 현재 호가나 거래 완료 가격을 보장하지 않습니다.",14,"MutedBrush"));
        body.Children.Add(Text("높은 판매 호가와 높은 두캇은 각각 표시합니다. 이름 확인이 필요한 보상과 오래된 시세는 가격 강조에서 제외합니다.",14,"MutedBrush"));
        previewHost.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");previewHost.Children.Add(preview);previewHost.Children.Add(selectionLayer);selectionLayer.Children.Add(selection);selection.SetResourceReference(Shape.StrokeProperty,"AccentBrush");
        previewHost.MouseLeftButtonDown+=StartCrop;previewHost.MouseMove+=MoveCrop;previewHost.MouseLeftButtonUp+=EndCrop;
        var previewActions=new WrapPanel{Margin=new(0,10,0,0)};cropButton=Button("선택 영역 다시 읽기",async()=>{if(source is not null&&crop is {} rect)await ReadAsync(new CroppedBitmap(source,rect),false);});cropButton.IsEnabled=false;
        ZoomButton=Button("원본 크게 보기",OpenLargePreview);previewActions.Children.Add(ZoomButton);
        resetCrop=Button("전체 화면 다시 읽기",async()=>{if(source is not null)await ReadAsync(source,false);});resetCrop.IsEnabled=false;previewActions.Children.Add(cropButton);previewActions.Children.Add(resetCrop);
        imageExpander=new(){Header="원본 화면 · 드래그해서 보상 이름 영역 선택",Margin=new(0,14,0,14),IsExpanded=false,Content=new StackPanel{Children={previewHost,previewActions}}};body.Children.Add(imageExpander);
        previewHost.SizeChanged+=(_,_)=>UpdateCropOutline();
        var attribution=new WrapPanel();var link=Button("시세 출처: Warframe.market ↗",()=>OpenUrl("https://warframe.market"));attribution.Children.Add(link);body.Children.Add(attribution);body.Children.Add(catalogStatus);UpdateCatalogStatus();
        body.Children.Add(Text("화면 인식은 이 PC에서만 처리합니다. 화면 이미지는 마켓에 전송하거나 자동 저장하지 않습니다.",14,"MutedBrush"));
        body.Children.Add(Text("실시간 인식: FHD(1920×1080) 이상 · 창 모드 / 테두리 없는 창 · 보상 선택은 게임에서 직접 합니다.",14,"MutedBrush"));
        viewScroll.Content=body;Children.Add(viewScroll);
        for(int i=0;i<4;i++)AddCard();
        AllowDrop=true;DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Drop+=async(_,e)=>{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths&&paths.Length>0)await OpenFileAsync(paths[0]);};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&Busy){CancelRecognition();e.Handled=true;}else if(e.Key==Key.V&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBox){PasteImage();e.Handled=true;}};
    }
    internal void StartPriceRefresh()
    {
        if(!Market.SnapshotMode||disposed)return;
        bool crossplay=Crossplay.IsChecked==true;if(priceRefresh is not null&&priceCrossplay==crossplay)return;
        priceRefresh?.Cancel();var cancellation=new CancellationTokenSource();priceRefresh=cancellation;priceCrossplay=crossplay;
        _=RefreshPrices();
        async Task RefreshPrices()
        {
            try
            {
                await MarketPriceSnapshot.RunAsync(Market,crossplay,progress=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(disposed||!ReferenceEquals(priceRefresh,cancellation))return;
                    PriceStoreStatus.Text=progress.Running?$"저장 시세 · 12시간 갱신 · 준비 {progress.Complete}/{progress.Total}"+(progress.Failed>0?$" · 실패 {progress.Failed}":"")
                        :progress.Failed>0?"저장 시세 일부 갱신 실패 · 이전 가격 유지 · 5분 뒤 재시도":$"저장 시세 준비됨 · 프라임·성유물 {progress.Total}개 · 12시간 갱신";
                })),id=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(disposed||!ReferenceEquals(priceRefresh,cancellation)||Busy)return;
                    foreach(var card in Cards.Where(c=>c.Item?.Id==id))_=card.RefreshAsync(false);
                })),cancellation.Token);
            }
            catch(OperationCanceledException){}
            catch(Exception)when(disposed||cancellation.IsCancellationRequested){}
            finally{if(ReferenceEquals(priceRefresh,cancellation))priceRefresh=null;cancellation.Dispose();}
        }
    }
    internal static TextBlock Text(string value,double size=14,string brush="TextBrush")
    {
        var text=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,LineHeight=size*1.5,Margin=new(0,0,0,8),FontWeight=size>=20?FontWeights.SemiBold:FontWeights.Normal};
        text.SetResourceReference(TextBlock.ForegroundProperty,brush);Typography.SetNumeralAlignment(text,FontNumeralAlignment.Tabular);return text;
    }
    internal static Button Button(string label,Action action)
    {
        var button=new Button{Content=label,Padding=new(12,7,12,7),Margin=new(0,0,8,0),MinHeight=36};AutomationProperties.SetName(button,label);button.Click+=(_,_)=>action();return button;
    }
    internal static void OpenUrl(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){}}
    private void AddCard()
    {
        if(Cards.Count>=4)return;var card=new RewardCard(this);Cards.Add(card);CardGrid.Children.Add(card);Renumber();
    }
    internal void RemoveCard(RewardCard card){card.Dispose();Cards.Remove(card);CardGrid.Children.Remove(card);if(Cards.Count==0)AddCard();Renumber();UpdateHighlights();}
    private void Renumber(){for(int i=0;i<Cards.Count;i++)Cards[i].Number.Text=$"보상 {i+1}";AddButton.IsEnabled=Cards.Count<4;}
    internal async Task RefreshCatalogAsync(bool force)
    {
        if(catalogBusy||disposed)return;catalogBusy=true;
        try{var (catalog,message)=await Market.CatalogAsync(force,CancellationToken.None);if(disposed)return;Catalog=catalog;foreach(var card in Cards)card.SetCatalog();UpdateCatalogStatus();if(message is not null)Status.Text=message;}
        catch(Exception e){if(!disposed)Status.Text="품목 갱신 실패 · "+e.Message;}
        finally{catalogBusy=false;}
    }
    private void UpdateCatalogStatus()=>catalogStatus.Text=$"품목 {Catalog.Items.Length:N0}개 · 목록 기준 {Catalog.RetrievedUtc.LocalDateTime:yyyy-MM-dd} · PC / 선택한 접속 상태 기준";
    private async void LoadImage(){if(ChooseImageFile is not null){if(ChooseImageFile() is {} chosen)await OpenFileAsync(chosen);return;}var dialog=new OpenFileDialog{Title="한국어 성유물 보상 화면",Filter="화면 이미지|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(Window.GetWindow(this))==true)await OpenFileAsync(dialog.FileName);}
    internal async Task OpenFileAsync(string path){try{await ReadAsync(RelicOcr.Load(path),true);}catch(Exception e){Status.Text="이미지를 열지 못했습니다. "+e.Message;}}
    private async void PasteImage(){try{var image=ClipboardImage();if(image is null){Status.Text="클립보드에 이미지가 없습니다. 스크린샷을 복사하거나 파일을 불러오세요.";return;}await ReadAsync(image,true);}catch(Exception e){Status.Text="이미지를 붙여넣지 못했습니다. "+e.Message;}}
    internal async Task ReadAsync(BitmapSource image,bool replacePreview=true,Stopwatch? operation=null,bool live=false)
    {
        var elapsed=operation??Stopwatch.StartNew();LastElapsedSeconds=0;FirstPricedSeconds=0;TimingLabel=operation is null?"인식·시세":"캡처·인식·시세";
        Task? warming=null;var presence=(MarketPresence)Presence.SelectedIndex;var crossplay=Crossplay.IsChecked==true;
        var oldImage=source;var oldCrop=crop;var oldExpanded=imageExpander.IsExpanded;
        CancelRecognition();var id=++recognitionGeneration;var cts=new CancellationTokenSource();recognitionCancel=cts;
        foreach(var card in Cards)card.CancelQuote();
        SetBusy(true);Status.Text="보상 이름을 읽고 있습니다…";
        if(replacePreview){source=image;preview.Source=image;imageExpander.IsExpanded=true;crop=null;selection.Visibility=Visibility.Collapsed;cropButton.IsEnabled=false;resetCrop.IsEnabled=true;}
        try
        {
            var result=await RelicOcr.RecognizeAsync(image,Catalog,cts.Token,onCandidates:items=>
            {
                // Speculative read-only requests overlap the second OCR verification.
                // Nothing is committed to cards until the complete recognition returns.
                warming=Task.WhenAll(items.Where(x=>x.Tradable).DistinctBy(x=>x.Id).Select(item=>Market.QuoteAsync(item,presence,crossplay,false,cts.Token)));
            },live:live,onProgress:live&&Market.SnapshotMode?partial=>
            {
                // Serialize progressive UI commits with local cache reads. A late
                // callback from a canceled capture cannot replace a newer screen.
                Dispatcher.InvokeAsync(new Func<Task>(async()=>
                {
                    if(disposed||id!=recognitionGeneration||cts.IsCancellationRequested)return;
                    ApplyRecognition(partial);await RefreshAsync(false);
                    if(FirstPricedSeconds==0&&Cards.Any(c=>c.Quote?.SellMin is not null))FirstPricedSeconds=elapsed.Elapsed.TotalSeconds;
                })).Task.Unwrap().GetAwaiter().GetResult();
            }:null);cts.Token.ThrowIfCancellationRequested();if(disposed||id!=recognitionGeneration)return;
            ApplyRecognition(result);await RefreshAsync(false);
            if(FirstPricedSeconds==0&&Cards.Any(c=>c.Quote?.SellMin is not null))FirstPricedSeconds=elapsed.Elapsed.TotalSeconds;
            if(!disposed&&id==recognitionGeneration){LastElapsedSeconds=elapsed.Elapsed.TotalSeconds;Status.Text=result.Notice+$" · {TimingLabel} {LastElapsedSeconds:0.0}초";ResultsChanged?.Invoke();}
        }
        catch(OperationCanceledException){if(id==recognitionGeneration){RestorePreview();Status.Text="인식을 취소했습니다. 기존 보상은 유지됩니다.";}}
        catch(Exception e){if(id==recognitionGeneration){RestorePreview();Status.Text=e.Message;}}
        finally{cts.Cancel();if(warming is not null)try{await warming;}catch(Exception){/* Optional prefetch; normal card requests report errors. */}cts.Dispose();if(id==recognitionGeneration){recognitionCancel=null;SetBusy(false);if(Market.SnapshotMode&&!disposed)_=RefreshAsync(false);}}
        void RestorePreview(){if(!replacePreview)return;source=oldImage;preview.Source=oldImage;crop=oldCrop;imageExpander.IsExpanded=oldExpanded;selection.Visibility=Visibility.Collapsed;}
    }
    internal void ApplyRecognition(RelicRecognition result)
    {
        LastRecognitionForCheck=result;
        foreach(var card in Cards)card.Dispose();Cards.Clear();CardGrid.Children.Clear();
        suppress=true;
        foreach(var reward in result.Rewards){AddCard();Cards[^1].SetRecognition(reward);}
        if(Cards.Count==0)AddCard();suppress=false;Status.Text=result.Notice;Renumber();UpdateHighlights();viewScroll.ScrollToTop();
    }
    internal void CancelRecognition(){recognitionCancel?.Cancel();foreach(var card in Cards)card.CancelQuote();}
    private void SetBusy(bool value){CardGrid.IsEnabled=!value;CancelButton.Visibility=value?Visibility.Visible:Visibility.Collapsed;ReadButton.IsEnabled=!value;LoadButton.IsEnabled=!value;PasteButton.IsEnabled=!value;ZoomButton.IsEnabled=!value&&source is not null;cropButton.IsEnabled=!value&&crop is not null;resetCrop.IsEnabled=!value&&source is not null;}
    internal async Task RefreshAsync(bool force)
    {
        await Task.WhenAll(Cards.ToArray().Select(card=>card.RefreshAsync(force)));if(!disposed)UpdateHighlights();
    }
    internal bool Suppress=>suppress;
    internal void UpdateHighlights()
    {
        var priced=Cards.Where(c=>c.Quote is {SellMin:not null,Stale:false,Error:null}).ToArray();var max=priced.Select(c=>c.Quote!.SellMin).DefaultIfEmpty(null).Max();
        var ducats=Cards.Where(c=>c.Item is not null).Select(c=>c.Item!.Ducats).DefaultIfEmpty(0).Max();
        foreach(var card in Cards)card.SetBest(max is not null&&card.Quote?.SellMin==max&&priced.Contains(card),ducats>0&&card.Item?.Ducats==ducats);
        ResultsChanged?.Invoke();
    }
    private Rect ImageBounds()
    {
        if(source is null)return Rect.Empty;var scale=Math.Min(previewHost.ActualWidth/source.PixelWidth,previewHost.ActualHeight/source.PixelHeight);
        var w=source.PixelWidth*scale;var h=source.PixelHeight*scale;return new((previewHost.ActualWidth-w)/2,(previewHost.ActualHeight-h)/2,w,h);
    }
    private void OpenLargePreview()
    {
        if(source is null||Busy||PreviewWindowForCheck is not null)return;
        var original=(StackPanel)imageExpander.Content;original.Children.Remove(previewHost);
        var window=new Window{Title="보상 화면 · 영역 선택",Owner=Window.GetWindow(this),Width=Math.Min(1440,SystemParameters.WorkArea.Width-40),Height=Math.Min(900,SystemParameters.WorkArea.Height-40),MinWidth=640,MinHeight=480,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        window.SetResourceReference(Window.FontFamilyProperty,"AppFont");PreviewWindowForCheck=window;
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=new(64)});layout.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=Text("보상 이름이 있는 줄을 드래그하세요.",18);header.Margin=new(20,16,20,10);
        var close=Button("×",()=>window.Close());close.Margin=new(10,12,16,12);AutomationProperties.SetName(close,"원본 창 닫기");System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close,true);
        var caption=new DockPanel();DockPanel.SetDock(close,Dock.Right);caption.Children.Add(close);caption.Children.Add(header);layout.Children.Add(caption);
        previewHost.Height=double.NaN;previewHost.Margin=new(16);Grid.SetRow(previewHost,1);layout.Children.Add(previewHost);
        var actions=new WrapPanel{Margin=new(20,12,20,18)};var apply=Button("선택 영역 읽기",async()=>{window.Close();if(source is not null&&crop is {} area)await ReadAsync(new CroppedBitmap(source,area),false);});UiHierarchy.Primary(apply);apply.IsEnabled=crop is not null;
        previewHost.MouseLeftButtonUp+=UpdateApply;
        void UpdateApply(object sender,MouseButtonEventArgs e)=>apply.IsEnabled=crop is not null;
        actions.Children.Add(apply);actions.Children.Add(Button("닫기",()=>window.Close()));Grid.SetRow(actions,2);layout.Children.Add(actions);
        WindowFrame.Attach(window,layout);window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
        window.Closed+=(_,_)=>{previewHost.MouseLeftButtonUp-=UpdateApply;layout.Children.Remove(previewHost);previewHost.Height=210;previewHost.Margin=new(0);Grid.SetRow(previewHost,0);original.Children.Insert(0,previewHost);PreviewWindowForCheck=null;};
        window.ShowDialog();
    }
    private void UpdateCropOutline()
    {
        if(dragStart is not null||source is null||crop is not {} area)return;var b=ImageBounds();if(b.IsEmpty)return;
        selection.Visibility=Visibility.Visible;Canvas.SetLeft(selection,b.X+area.X*b.Width/source.PixelWidth);Canvas.SetTop(selection,b.Y+area.Y*b.Height/source.PixelHeight);selection.Width=area.Width*b.Width/source.PixelWidth;selection.Height=area.Height*b.Height/source.PixelHeight;
    }
    private void StartCrop(object sender,MouseButtonEventArgs e)
    {if(source is null||Busy)return;var p=e.GetPosition(previewHost);if(!ImageBounds().Contains(p))return;dragStart=p;previewHost.CaptureMouse();selection.Visibility=Visibility.Visible;selection.Width=selection.Height=0;Canvas.SetLeft(selection,p.X);Canvas.SetTop(selection,p.Y);e.Handled=true;}
    private void MoveCrop(object sender,MouseEventArgs e)
    {if(dragStart is not {} start)return;var bounds=ImageBounds();var p=e.GetPosition(previewHost);p=new(Math.Clamp(p.X,bounds.Left,bounds.Right),Math.Clamp(p.Y,bounds.Top,bounds.Bottom));Canvas.SetLeft(selection,Math.Min(start.X,p.X));Canvas.SetTop(selection,Math.Min(start.Y,p.Y));selection.Width=Math.Abs(p.X-start.X);selection.Height=Math.Abs(p.Y-start.Y);}
    private void EndCrop(object sender,MouseButtonEventArgs e)
    {
        if(dragStart is null||source is null)return;MoveCrop(sender,e);dragStart=null;previewHost.ReleaseMouseCapture();var b=ImageBounds();
        int x=(int)((Canvas.GetLeft(selection)-b.X)/b.Width*source.PixelWidth),y=(int)((Canvas.GetTop(selection)-b.Y)/b.Height*source.PixelHeight);
        int w=(int)(selection.Width/b.Width*source.PixelWidth),h=(int)(selection.Height/b.Height*source.PixelHeight);
        crop=w>=80&&h>=30?new Int32Rect(x,y,Math.Min(w,source.PixelWidth-x),Math.Min(h,source.PixelHeight-y)):null;cropButton.IsEnabled=crop is not null;
    }
    public void Dispose(){disposed=true;priceRefresh?.Cancel();recognitionGeneration++;CancelRecognition();foreach(var card in Cards)card.Dispose();Market.Dispose();}
}

internal sealed class RewardCard : Border,IDisposable
{
    private readonly RelicPage owner;
    internal readonly RewardSearchBox Search=new();
    internal readonly TextBlock Number=RelicPage.Text("",14,"MutedBrush"),ItemName=RelicPage.Text("보상을 선택하세요",21),State=RelicPage.Text("직접 검색하거나 화면을 읽어주세요.",14,"MutedBrush"),Price=RelicPage.Text("—",34),Ducats=RelicPage.Text("—",28),Detail=RelicPage.Text("",14,"MutedBrush"),Best=RelicPage.Text("",14,"AccentBrush");
    private string? candidateName;
    private readonly TextBlock fetched=RelicPage.Text("",14,"MutedBrush");
    private readonly TextBlock raw=RelicPage.Text("",14,"MutedBrush");
    internal readonly Button Correct,MarketLink,Remove;
    internal RelicItem? Item=>Search.SelectedItem as RelicItem;
    internal MarketQuote? Quote{get;private set;}
    private CancellationTokenSource? request;private bool setting,disposed;private long generation;
    internal RewardCard(RelicPage owner)
    {
        this.owner=owner;Padding=new(18);Margin=new(0,0,12,0);BorderThickness=new(1);CornerRadius=new(5);
        SetResourceReference(BackgroundProperty,"SurfaceBrush");SetResourceReference(BorderBrushProperty,"StrokeBrush");
        var content=new StackPanel();var heading=new DockPanel();Remove=RelicPage.Button("×",()=>owner.RemoveCard(this));Remove.ToolTip="보상 제거";Remove.Padding=new(8,0,8,0);Remove.MinHeight=26;Remove.Margin=new(0);DockPanel.SetDock(Remove,Dock.Right);heading.Children.Add(Remove);heading.Children.Add(Number);content.Children.Add(heading);
        ItemName.MinHeight=60;ItemName.Margin=new(0,8,0,8);content.Children.Add(ItemName);
        Search.ItemsSource=owner.Catalog.Items;AutomationProperties.SetName(Search,"보상 이름 검색");Search.Margin=new(0,0,0,10);
        Correct=RelicPage.Button("보상 선택",()=>Search.OpenSuggestions());
        Search.SelectionChanged+=(_,_)=>{if(setting)return;candidateName=null;Quote=null;Render();State.Text="직접 선택한 품목";Correct.Content="이름 수정";raw.Text="";owner.UpdateHighlights();if(!owner.Suppress)_=RefreshAsync(false);};
        Correct.HorizontalAlignment=HorizontalAlignment.Right;State.MinHeight=40;State.Margin=new(0,0,8,0);
        var metrics=new Grid();metrics.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});metrics.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var p=new StackPanel{Children={RelicPage.Text("판매 최저 · 플래티넘",14,"MutedBrush"),Price}};var d=new StackPanel{Children={RelicPage.Text("두캇",14,"MutedBrush"),Ducats}};Grid.SetColumn(d,1);metrics.Children.Add(p);metrics.Children.Add(d);content.Children.Add(metrics);
        Best.MinHeight=24;content.Children.Add(Best);
        content.Children.Add(Search);var correction=new DockPanel{Margin=new(0,0,0,8)};DockPanel.SetDock(Correct,Dock.Right);correction.Children.Add(Correct);correction.Children.Add(State);content.Children.Add(correction);
        fetched.MinHeight=42;content.Children.Add(fetched);
        MarketLink=RelicPage.Button("마켓에서 보기 ↗",()=>{if(Item is {Tradable:true} item)RelicPage.OpenUrl(item.MarketUrl);});MarketLink.HorizontalAlignment=HorizontalAlignment.Left;
        var original=new Expander{Header="인식한 원문",Content=raw,Margin=new(0,14,0,0)};content.Children.Add(new Expander{Header="시세 상세 · 인식 원문",Content=new StackPanel{Children={Detail,MarketLink,original}},Margin=new(0,6,0,0)});Child=content;Render();
    }
    internal void SetCatalog(){var item=Item;setting=true;Search.ItemsSource=owner.Catalog.Items;Search.SelectedItem=owner.Catalog.Items.FirstOrDefault(x=>x.Id==item?.Id);setting=false;Quote=null;Render();if(Item is not null)_=RefreshAsync(false);}
    internal void SetRecognition(RecognizedReward reward)
    {
        CancelQuote();Quote=null;
        candidateName=reward.Item is null?(reward.Candidates.FirstOrDefault() is {Similarity:>=.82} candidate?candidate.Item.Name:"이름 확인 필요"):null;setting=true;Search.SelectedItem=reward.Item;setting=false;raw.Text=reward.RawText;Render();
        if(reward.Item is null){Search.Suggest(reward.RawText);ItemName.Text=candidateName??"이름 확인 필요";State.Text="이름 확인 필요 · 후보를 선택하세요";State.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");Correct.Content="후보 확인";}
        else{State.Text="자동 인식 완료";State.ToolTip=reward.Confirmation;Correct.Content="이름 수정";}
    }
    internal void Select(RelicItem item){Search.SelectedItem=item;}
    internal void CancelQuote(){var previous=request;request=null;previous?.Cancel();}
    internal async Task RefreshAsync(bool force)
    {
        var item=Item;CancelQuote();var id=++generation;if(item is null||!item.Tradable){Quote=null;Render();return;}
        var cts=new CancellationTokenSource();request=cts;Quote=null;Render();fetched.Text="시세 조회 중…";owner.UpdateHighlights();
        try{var quote=await owner.Market.QuoteAsync(item,(MarketPresence)owner.Presence.SelectedIndex,owner.Crossplay.IsChecked==true,force,cts.Token);if(disposed||id!=generation)return;Quote=quote;Render();owner.UpdateHighlights();}
        catch(OperationCanceledException){if(id==generation&&!disposed){fetched.Text="조회 취소됨";Detail.Text="조회 취소됨 · 시세 새로고침으로 다시 조회하세요.";}}
        catch(Exception e){if(id==generation&&!disposed){fetched.Text="시세 조회 실패 · 새로고침으로 재시도";Detail.Text="시세를 불러오지 못했습니다. "+e.Message;}}
        finally{cts.Dispose();if(id==generation)request=null;}
    }
    private void Render()
    {
        var item=Item;Correct.Content=item is not null?"이름 수정":candidateName is not null?"후보 확인":"보상 선택";ItemName.Text=item?.Name??candidateName??"보상을 선택하세요";Ducats.Text=item is null?"—":item.Ducats.ToString("N0");Price.Text=Quote?.SellMin?.ToString("0.##")??"—";
        MarketLink.IsEnabled=item?.Tradable==true;Detail.Text="";fetched.Text="";State.SetResourceReference(TextBlock.ForegroundProperty,item is null&&candidateName is not null?"AccentBrush":"MutedBrush");
        if(item is {Tradable:false}){Price.Text="거래 불가";Price.FontSize=24;Detail.Text="플레이어 간 거래 불가\n두캇 교환 불가";}
        else Price.FontSize=34;
        if(Quote is {} q)
        {
            fetched.Text=q.Snapshot?(q.Error??$"{(q.Stale?"갱신 지연 · ":"")}저장 시세 · {q.FetchedUtc.LocalDateTime:MM-dd HH:mm}")
                :q.Error is not null?(q.Stale?"오래된 시세 · ":"시세 조회 실패 · ")+q.FetchedUtc.LocalDateTime.ToString("MM-dd HH:mm:ss"):$"판매 {q.SellOrders}명 · {q.FetchedUtc.LocalDateTime:HH:mm:ss}"+(q.Cached?" · 캐시":"");
            fetched.SetResourceReference(TextBlock.ForegroundProperty,q.Error is null?"MutedBrush":"ErrorBrush");
            var buy=q.BuyMax?.ToString("0.##")??"—";var median=q.LowFiveMedian?.ToString("0.##")??"—";
            Detail.Text=$"구매 최고 {buy} P · {q.BuyOrders}명\n하위 5명 판매 중앙값 {median} P\n판매 {q.SellOrders}명 · {q.FetchedUtc.LocalDateTime:MM-dd HH:mm:ss}"+(q.Cached?" · 캐시":"")+(q.Error is not null?"\n"+(q.Stale?"오래된 시세 · ":"")+q.Error:q.SellOrders==0?"\n조건에 맞는 판매 주문 없음":"");
            if(q.Snapshot)Detail.Text+="\n12시간 저장 시세 · 구매 최고·판매 최저는 저장 당시 호가";
            if(item?.MarketRank is {} rank)Detail.Text+=$"\n레퀴엠 · 마켓 랭크 {rank} 주문 기준";
            Detail.SetResourceReference(TextBlock.ForegroundProperty,q.Error is not null?"ErrorBrush":"MutedBrush");
        }
        else Detail.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        if(item is null)Best.Text="";
    }
    internal void SetBest(bool price,bool ducats)
    {Best.Text=string.Join(" · ",new[]{price?"판매 호가 높음":null,ducats?"두캇 높음":null}.Where(x=>x is not null));Price.SetResourceReference(TextBlock.ForegroundProperty,price?"AccentBrush":"TextBrush");Ducats.SetResourceReference(TextBlock.ForegroundProperty,ducats?"AccentBrush":"TextBrush");}
    public void Dispose(){disposed=true;generation++;CancelQuote();}
}
