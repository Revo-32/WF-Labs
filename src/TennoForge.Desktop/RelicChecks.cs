using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckRelics(string dir,Action<FrameworkElement,string,int,int,double> render,bool online=false)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        var catalog=RelicCatalog.Bundled();
        var glyphGroups=RelicOcr.GroupWords([new("니카나",.20,.43,.04,.024,90),new("프",.25,.438,.008,.008,90),new("라임",.267,.43,.025,.024,90),new("블레이드",.30,.43,.055,.024,90),new("브래튼",.60,.43,.045,.024,90)],16d/9);
        Yes(glyphGroups.Length==2&&RelicCatalog.NormalizeOcr(glyphGroups[0].Text)=="니카나프라임블레이드","작은 글자 상자 때문에 같은 보상 이름이 여러 카드로 분리되지 않음");
        var testItems=new[]{"nikana_prime_blade","braton_prime_blueprint","ash_prime_chassis_blueprint",""}.Select(slug=>slug==""?catalog.Items.Single(x=>x.Id=="local:forma-blueprint"):catalog.Items.Single(x=>x.Slug==slug)).ToArray();
        if(online)
        {
            using var live=new RelicMarketClient(Path.Combine(dir,"online-cache"));var (fresh,warning)=await live.CatalogAsync(true,default);
            Yes(warning is null&&fresh.Items.Length>500,"실제 한국어 v2 목록 조회");
            var quotes=new List<MarketQuote>();foreach(var item in testItems.Where(x=>x.Tradable))quotes.Add(await live.QuoteAsync(item,MarketPresence.InGame,true,true,default));
            Yes(quotes.All(x=>x.Error is null),"실제 PC 크로스플레이 게임 중 호가 조회");
            return new{passed=true,checks,quotes,catalogCount=fresh.Items.Length,source="https://api.warframe.market/v2/",imagesUploaded=false};
        }
        var fake=new RelicFixtureHandler(catalog);var market=new RelicMarketClient(Path.Combine(dir,"market-cache"),fake);
        using var view=new RelicPage(market);SelectPage(4);page.Content=view;
        render(this,"empty",1760,920,1);Yes(CheckHeaderLayout() is not null,"성유물 탭·상단 간격");
        var returned=0;view.GameReturnRequested=()=>returned++;Invoke(view.ReturnButton);await Idle();Yes(returned==1,"게임으로 돌아가기 실제 버튼 동작 연결");
        var fixtures=new List<object>();var captured=new List<RelicRecognition>();
        foreach(var (width,height,count,wrap) in new[]{(1280,720,1,false),(1920,1080,2,false),(2560,1440,3,true),(3440,1440,4,true)})
        {
            var expected=testItems.Take(count).ToArray();var image=RelicFixture(expected,width,height,wrap);
            SaveFixture(image,Path.Combine(dir,$"fixture-{width}-{count}.png"));
            var result=await RelicOcr.RecognizeAsync(image,catalog,default);captured.Add(result);
            var actual=result.Rewards.Select(x=>x.Candidates.FirstOrDefault()?.Item.Id).ToArray();
            fixtures.Add(new{width,height,count,wrap,synthetic=true,expected=expected.Select(x=>x.Id),actual,allCorrect=actual.SequenceEqual(expected.Select(x=>x.Id)),rewards=result.Rewards.Select(x=>new{x.RawText,x.Confident,candidates=x.Candidates.Take(3)}),lines=result.Lines});
            File.WriteAllText(Path.Combine(dir,"ocr-fixtures.json"),JsonSerializer.Serialize(fixtures,Catalog.Json));
            Yes(actual.SequenceEqual(expected.Select(x=>x.Id)),$"실제 한국어 OCR 엔진 {width}×{height} / {count}개 / 줄바꿈 {wrap}");
        }
        var asymmetric=new[]{"orthos_prime_blade","dual_zoren_prime_handle","sarofang_prime_handle","cedo_prime_barrel"}.Select(s=>catalog.Items.Single(x=>x.Slug==s)).ToArray();
        foreach(var (width,height) in new[]{(1920,1080),(3440,1440),(3840,2160)})
        {
            var input=RelicFixture(asymmetric,width,height,false,firstWrappedAbove:true);SaveFixture(input,Path.Combine(dir,$"wrapped-first-{width}.png"));
            var timer=System.Diagnostics.Stopwatch.StartNew();var result=await RelicOcr.RecognizeAsync(input,catalog,default,live:true);
            Yes(result.Rewards.Select(r=>r.Item?.Id).SequenceEqual(asymmetric.Select(x=>x.Id)),$"실시간 첫 카드만 윗줄 줄바꿈 {width}×{height}: 4개 확정·순서 일치");
            Yes(timer.Elapsed.TotalSeconds<8,$"실시간 비대칭 줄바꿈 {width}×{height}: 8초 이내");
        }
        view.ApplyRecognition(captured[^1]);await view.RefreshAsync(false);render(this,"recognized",1760,920,1);
        Yes(view.Cards.Count==4,"보상 4개 좌우 순서");
        // Test uncertainty explicitly rather than depending on OCR happening to fail.
        var ambiguous=catalog.Recognize([new("니카나 프라임 블래이드",.1,.4,.2,.03,88)])[0];
        view.Cards[0].SetRecognition(ambiguous);await view.Cards[0].RefreshAsync(false);
        Yes(view.Cards[0].Item is null&&view.Cards[0].Price.Text=="—","오인식 후보를 확정 호가로 보여주지 않음");
        view.Cards[0].Search.CancelSearch();Yes(view.Cards[0].Search.QueryInput.Text==ambiguous.RawText&&view.Cards[0].Search.SelectedItem is null,"초점 이탈·검색 취소 후 미확정 OCR 이름 보존");
        Invoke(view.Cards[0].Correct);await Idle();var search=view.Cards[0].Search;search.QueryInput.Text="니카나 프라임 블레이드";
        await Idle();Yes(search.IsDropDownOpen&&search.SelectedItem is null,"검색 입력만으로 품목 확정하지 않음");
        RelicKey(search.QueryInput,System.Windows.Input.Key.Down);Yes(search.SelectedItem is null&&search.Suggestions.SelectedIndex==0,"방향키 후보 강조와 적용 구분");
        render((FrameworkElement)search.SearchPopup.Child,"correction-popup",390,360,1);
        RelicKey(search.QueryInput,System.Windows.Input.Key.Enter);await view.Cards[0].RefreshAsync(false);
        Yes(view.Cards[0].Item?.Id==testItems[0].Id&&view.Cards[0].Quote?.SellMin==20,"Enter 선택·정정 품목 가격 반영");
        Invoke(view.Cards[0].Correct);search.QueryInput.Text="없는보상";RelicKey(search.QueryInput,System.Windows.Input.Key.Escape);
        Yes(view.Cards[0].Item?.Id==testItems[0].Id&&!search.IsDropDownOpen,"Escape로 미확정 입력 취소·기존 품목 유지");
        view.Presence.SelectedIndex=1;await view.RefreshAsync(false);Yes(view.Cards[0].Quote?.SellMin==15,"온라인 필터 변경 후 가격 재조회");
        fake.Fail=true;await view.RefreshAsync(true);Yes(view.Cards[0].Quote is {Stale:true}&&view.Cards[0].Best.Text is not "판매 호가 높음","오프라인 이전 조회 시각 보존·가격 강조 제외");render(this,"offline",1760,920,1);
        fake.Fail=false;view.Presence.SelectedIndex=0;await view.RefreshAsync(true);
        // Slow old item response must not overwrite the newly committed item.
        fake.Delay=160;view.Cards[0].Select(testItems[1]);var old=view.Cards[0].RefreshAsync(true);view.Cards[0].Select(testItems[0]);await view.Cards[0].RefreshAsync(true);await old;fake.Delay=0;
        Yes(view.Cards[0].Quote?.ItemId==testItems[0].Id,"품목 변경 뒤 지연 응답 덮어쓰기 차단");
        Invoke(view.Cards[^1].Remove);await Idle();Yes(view.Cards.Count==3,"보상 제거 실제 버튼");Invoke(view.AddButton);await Idle();Yes(view.Cards.Count==4,"보상 추가 실제 버튼");view.Cards[^1].Select(testItems[^1]);
        Yes(view.Cards[^1].Price.Text=="거래 불가"&&!view.Cards[^1].MarketLink.IsEnabled,"포르마 거래 불가·마켓 버튼 비활성");
        view.ClipboardImage=()=>null;Invoke(view.PasteButton);await Idle();Yes(view.Status.Text.Contains("클립보드에 이미지가 없습니다"),"이미지 없는 클립보드 안내");
        view.ChooseImageFile=()=>Path.Combine(dir,"fixture-1920-2.png");Invoke(view.LoadButton);await WaitRecognition();render(this,"loaded-file",1760,920,1);Yes(view.Cards.Count==2,"이미지 불러오기 버튼 → OCR → 시세: "+view.Status.Text+" / "+view.Cards.Count);
        view.ClipboardImage=()=>RelicFixture(testItems,1920,1080,false);Invoke(view.PasteButton);await WaitRecognition();Yes(view.Cards.Count==4,"붙여넣기 버튼 → OCR → 비교 · 실제 클립보드 변경 없음");
        var previousIds=view.Cards.Select(x=>x.Item?.Id).ToArray();var pending=view.ReadAsync(RelicFixture(testItems.Take(1).ToArray(),1920,1080,false));Invoke(view.CancelButton);await Idle();await pending;
        Yes(view.Cards.Select(x=>x.Item?.Id).SequenceEqual(previousIds)&&!view.Busy,"인식 취소 버튼 · 기존 결과 유지");
        var whole=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(150);
        await view.ReadAsync(RelicFixture(testItems,1920,1080,false),operation:whole);
        Yes(view.TimingLabel=="캡처·인식·시세"&&view.LastElapsedSeconds>=view.LastRecognitionForCheck!.Timing!.TotalMilliseconds/1000+.1,"캡처 이전부터 시작한 전체 작업 시간·라벨 반영");
        async Task WaitRecognition(){await Idle();var started=DateTime.UtcNow;while(view.Busy){if(DateTime.UtcNow-started>TimeSpan.FromSeconds(45))throw new Exception("인식 작업 미종료");await Task.Delay(40);}await Idle();}
        // Expand the same controls users open; inspect the scrollable content too.
        foreach(var expander in LayoutDiagnostics.Tree(view).OfType<Expander>().ToArray())expander.IsExpanded=true;
        render(this,"details-expanded",1760,920,1);Yes(LayoutDiagnostics.Inspect(view).Length==0,"시세 상세·원문·이미지 펼침 배치");
        var scroll=LayoutDiagnostics.Tree(view).OfType<ScrollViewer>().First();scroll.ScrollToEnd();await Idle();render(this,"details-bottom",1760,920,1);
        Yes(Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<1,"원본 화면까지 스크롤 도달");
        var native=await CheckRelicNative(view,dir);checks.Add("Windows 입력·캡처: "+JsonSerializer.Serialize(native));
        foreach(var expander in LayoutDiagnostics.Tree(view).OfType<Expander>().ToArray())expander.IsExpanded=false;scroll.ScrollToTop();
        var allIssues=new Dictionary<string,LayoutDiagnostics.Issue[]>();
        foreach(var font in UiFonts.Choices)foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})
        {
            UiFonts.Apply(font.Id);SetDarkTheme(dark);var key=$"{font.Id}-{dark}-{scale}";render(this,key,1760,920,scale);
            var issues=LayoutDiagnostics.Inspect(view);allIssues[key]=issues;
            File.WriteAllText(Path.Combine(dir,"layout.json"),JsonSerializer.Serialize(allIssues,Catalog.Json));Yes(issues.Length==0,key+" 글꼴·배율·테마 배치");
            Yes(view.Cards.All(c=>c.ActualWidth>300),key+" 보상별 최소 읽기 폭");
        }
        UiFonts.Apply("jamsil");SetDarkTheme(true);await Task.Delay(200);render(this,"final",1920,1000,1);
        using(var hotkey=new RelicHotkey(this,()=>{})){Yes(hotkey.Enable(true),"전역 단축키 등록");using var conflict=new WindowHolder();using var competing=new RelicHotkey(conflict.Window,()=>{});Yes(!competing.Enable(true),"단축키 충돌 확인");hotkey.Enable(false);Yes(competing.Enable(true),"단축키 해제 후 재등록");}
        return new{passed=true,checks=checks.Count,details=checks,ocrFixtures=fixtures,scope="합성 한국어 보상 이미지의 실제 Tesseract 인식과 실제 WPF 컨트롤·HTTP fixture. 실제 게임 영상의 정답률이나 독점 전체화면 캡처를 의미하지 않음."};
    }
    private sealed class WindowHolder:IDisposable{internal Window Window=new(){ShowActivated=false,ShowInTaskbar=false,Width=100,Height=100,Left=-30000,Top=-30000};public void Dispose()=>Window.Close();}
    private static void Invoke(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    private static void RelicKey(UIElement input,Key key)=>input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static async Task Idle()=>await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
    internal static BitmapSource RelicFixture(RelicItem[] items,int width,int height,bool wrap,bool firstWrappedAbove=false)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24,29,34)),null,new Rect(0,0,width,height));
            for(int i=0;i<items.Length;i++)
            {
                double center=(i+1d)/(items.Length+1)*width;var text=items[i].Name;
                if(wrap||firstWrappedAbove&&i==0){var index=text.LastIndexOf(' ');if(index>0)text=text[..index]+"\n"+text[(index+1)..];}
                var formatted=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),Math.Max(24,height/1080d*28),i%2==0?Brushes.White:Brushes.Gold,1){TextAlignment=TextAlignment.Center};
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(43,48,50)),null,new Rect(center-width*.06,height*.25,width*.12,height*.13));
                dc.DrawText(formatted,new Point(center,height*(firstWrappedAbove?(i==0?.365:.40):.43)));
            }
            dc.DrawText(new FormattedText("성유물 보상 선택",CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),height*.03,Brushes.White,1),new Point(width*.04,height*.08));
        }
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();return image;
    }
    private static void SaveFixture(BitmapSource image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
    private async Task<object> CheckRelicNative(RelicPage view,string dir)
    {
        if(Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")=="1")return new{executed=false,reason="사용자 비방해 요청: 실제 입력·전경 전환·화면 캡처 검사 제외"};
        var oldLeft=Left;var oldTop=Top;var wasTopmost=Topmost;var previous=GetForegroundWindow();GetCursorPos(out var point);var down=false;
        try
        {
            Left=20;Top=20;Topmost=true;Activate();await Task.Delay(200);
            var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if(GetForegroundWindow()!=hwnd)
            {
                // Use an actual click on our own topmost caption; no input is sent to the game.
                var caption=PointToScreen(new Point(600,30));SetCursorPos((int)caption.X,(int)caption.Y);
                var click=new[]{new InputPacket{Mouse=new(){Flags=2}},new InputPacket{Mouse=new(){Flags=4}}};SendInput(2,click,System.Runtime.InteropServices.Marshal.SizeOf<InputPacket>());await Task.Delay(150);
            }
            if(GetForegroundWindow()!=hwnd)return new{executed=false,reason="Windows가 검사 창의 전경 활성화를 허용하지 않음"};
            Invoke(view.ZoomButton);await Idle();var zoom=view.PreviewWindowForCheck??throw new Exception("원본 확대 창 버튼 실패");await Task.Delay(150);
            if(LayoutDiagnostics.Inspect((FrameworkElement)zoom.Content).Length>0)throw new Exception("원본 확대 창 겹침·잘림");
            SaveFixture(RelicCapture.Capture(new System.Windows.Interop.WindowInteropHelper(zoom).Handle),Path.Combine(dir,"large-original.png"));
            var bounds=view.ImageBoundsForCheck;var target=view.PreviewForCheck;
            async Task Move(double x,double y){var p=target.PointToScreen(new Point(x,y));SetCursorPos((int)p.X,(int)p.Y);await Task.Delay(60);}
            void MouseButton(bool held){var packets=new[]{new InputPacket{Mouse=new(){Flags=held?2u:4u}}};if(SendInput(1,packets,System.Runtime.InteropServices.Marshal.SizeOf<InputPacket>())!=1)throw new Exception("영역 선택 입력 실패");down=held;}
            await Move(bounds.X+bounds.Width*.04,bounds.Y+bounds.Height*.38);MouseButton(true);await Move(bounds.X+bounds.Width*.96,bounds.Y+bounds.Height*.56);MouseButton(false);await Idle();
            if(view.CropForCheck is null||!view.CropButtonForCheck.IsEnabled)throw new Exception("실제 드래그 영역 선택 실패");
            var crop=view.CropForCheck;zoom.Close();await Idle();Invoke(view.CropButtonForCheck);await Idle();var started=DateTime.UtcNow;while(view.Busy){if(DateTime.UtcNow-started>TimeSpan.FromSeconds(45))throw new Exception("영역 인식 작업 미종료");await Task.Delay(50);}
            if(view.Cards.Count!=4)throw new Exception("선택 영역 OCR 보상 수 오류");
            var capture=RelicCapture.Capture(hwnd);SaveFixture(capture,Path.Combine(dir,"native-capture.png"));
            if(capture.PixelWidth<1000||capture.PixelHeight<500)throw new Exception("네이티브 클라이언트 영역 캡처 크기 오류");
            return new{executed=true,passed=true,crop,width=capture.PixelWidth,height=capture.PixelHeight,scope="실제 OS 마우스로 이미지 영역 선택·재인식, 전경 WPF 창 픽셀 캡처. 게임 독점 전체화면과 구분."};
        }
        finally{if(down){var packet=new[]{new InputPacket{Mouse=new(){Flags=4}}};SendInput(1,packet,System.Runtime.InteropServices.Marshal.SizeOf<InputPacket>());}view.PreviewWindowForCheck?.Close();Mouse.Capture(null);Topmost=wasTopmost;Left=oldLeft;Top=oldTop;SetCursorPos(point.X,point.Y);if(previous!=IntPtr.Zero)SetForegroundWindow(previous);}
    }
    internal static async Task<object> CheckGameCapture(string dir)
    {
        var previous=RelicCapture.GetForegroundWindow();var game=RelicCapture.GameWindow();var minimized=RelicCapture.IsMinimized(game);
        try
        {
            if(minimized)RelicCapture.ShowWindow(game,9);RelicCapture.SetForegroundWindow(game);await Task.Delay(500);
            var image=RelicCapture.Capture(game);SaveFixture(image,Path.Combine(dir,"game-capture.png"));
            var result=await RelicOcr.RecognizeAsync(image,RelicCatalog.Bundled(),default);
            return new{passed=true,width=image.PixelWidth,height=image.PixelHeight,gameWindow=game.ToInt64(),rewardCandidates=result.Rewards.Select(x=>new{x.RawText,x.Confident,candidates=x.Candidates.Take(2)}),scope="실행 중인 실제 게임 창의 요청 시점 캡처·OCR. 게임 조작 없음. 해당 화면이 보상 선택 화면인지는 별도 시각 확인."};
        }
        finally{if(minimized)RelicCapture.ShowWindow(game,6);if(previous!=IntPtr.Zero)RelicCapture.SetForegroundWindow(previous);}
    }
}

internal sealed class RelicFixtureHandler(RelicCatalog catalog):HttpMessageHandler
{
    internal bool Fail;internal int Delay;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(Delay>0)await Task.Delay(Delay,token);if(Fail)throw new HttpRequestException("연결 없음 · 검수용 응답");
        var slug=request.RequestUri!.Segments[^1];var id=catalog.Items.Single(x=>x.Slug==slug).Id;
        var data=new[]{new{itemId=id,type="sell",platinum=20,quantity=2,perTrade=1,visible=true,user=new{id="seller-a",status="ingame",platform="pc",crossplay=true}},new{itemId=id,type="sell",platinum=15,quantity=1,perTrade=1,visible=true,user=new{id="seller-b",status="online",platform="pc",crossplay=true}},new{itemId=id,type="buy",platinum=12,quantity=1,perTrade=1,visible=true,user=new{id="buyer-a",status="ingame",platform="pc",crossplay=true}}};
        return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{data}),Encoding.UTF8,"application/json")};
    }
}
