using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TennoForge.Desktop;

// Separate native window: no game hooks, injected DLLs, or simulated game inputs.
internal sealed class RelicOverlay : Window
{
    private const int ExtendedStyle=-20;
    internal const long PassThroughStyle=0x08000000L|0x00000020L|0x00000080L; // NOACTIVATE | TRANSPARENT | TOOLWINDOW
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern IntPtr GetStyle(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetStyle(IntPtr hwnd,int index,IntPtr value);
    private readonly OverlayPositioner positioner=new();
    private readonly TextBlock status=RelicPage.Text("보상 이름을 읽고 있습니다…",14);
    private readonly UniformGrid cards=new(){Columns=4};
    private readonly DispatcherTimer lifetime=new(){Interval=TimeSpan.FromSeconds(15)};
    private readonly DispatcherTimer visibility=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private readonly IntPtr game;
    private readonly RelicPage page;
    private bool closed;
    internal bool Dismissed=>closed;
    internal FrameworkElement PreviewForCheck(){Update();return (FrameworkElement)Content;}
    internal RelicOverlay(IntPtr game,RelicPage page)
    {
        this.game=game;this.page=page;
        Title="WF Labs · 성유물 비교";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowActivated=false;ShowInTaskbar=false;Topmost=true;
        Focusable=false;IsHitTestVisible=false;UseLayoutRounding=true;Width=960;Height=208;Left=-30000;Top=-30000;
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
        var body=new StackPanel();
        status.Margin=new(10,6,10,4);body.Children.Add(status);body.Children.Add(cards);
        body.Children.Add(new TextBlock{Text="판매 최저 호가 · 클릭은 게임으로 통과 · "+(page.Shortcut.IsChecked==true?"Ctrl + Shift + R: 닫기":"15초 뒤 닫힘")+" · 수정은 WF Labs에서",FontSize=12,Foreground=Brushes.LightGray,Margin=new(10,5,10,8)});
        Content=new Border{Background=new SolidColorBrush(Color.FromArgb(244,15,17,19)),BorderBrush=new SolidColorBrush(Color.FromRgb(96,87,54)),BorderThickness=new(1),CornerRadius=new(6),Child=body};
        SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(this).Handle;SetStyle(hwnd,ExtendedStyle,new(GetStyle(hwnd,ExtendedStyle).ToInt64()|PassThroughStyle));};
        page.ResultsChanged+=Update;
        lifetime.Tick+=(_,_)=>Close();visibility.Tick+=(_,_)=>{if(RelicCapture.GetForegroundWindow()!=game||RelicCapture.IsMinimized(game)){Close();return;}try{Position();}catch(InvalidOperationException){Close();}};
        Closed+=(_,_)=>{closed=true;page.ResultsChanged-=Update;lifetime.Stop();visibility.Stop();};
    }
    internal void Present()
    {
        if(closed||RelicCapture.GetForegroundWindow()!=game)return;
        Show();Position();lifetime.Start();visibility.Start();
    }
    private void Position()
    {
        if(closed)return;var bounds=RelicCapture.ClientBounds(game);
        // Physical pixels avoid incorrect placement when the game moves between DPI scales.
        var dpi=VisualTreeHelper.GetDpi(this);
        int logicalHeight=page.Cards.Any(c=>page.FarmingHint?.Invoke(c.Item) is not null)?208:184;
        int width=Math.Min(bounds.Width-32,1440),height=Math.Min((int)Math.Ceiling(logicalHeight*dpi.DpiScaleY),bounds.Height-48);
        positioner.Apply(new WindowInteropHelper(this).Handle,new Int32Rect(bounds.X+(bounds.Width-width)/2,bounds.Y+bounds.Height-height-24,width,height));
    }
    private void Update()
    {
        if(closed)return;cards.Children.Clear();
        status.Text=page.LastElapsedSeconds>0?$"WF Labs · {page.TimingLabel} {page.LastElapsedSeconds:0.0}초":$"WF Labs · {page.Cards.Count(c=>c.Item is not null)}개 확인 · 나머지 확인 중…";
        if(page.LastRecognitionForCheck?.Rewards.Length==0){status.Text="보상 이름을 찾지 못했습니다 · WF Labs에서 원본 영역을 선택해 주세요.";return;}
        cards.Columns=Math.Max(1,page.Cards.Count);
        foreach(var card in page.Cards)
        {
            var panel=new StackPanel{Margin=new(10,3,10,2)};
            var name=new TextBlock{Text=card.Item is null?"이름 확인 필요":card.Item.Name,FontSize=14,LineHeight=20,Height=40,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Brushes.White};panel.Children.Add(name);
            var value=card.Item is null?"—":card.Item.Tradable?(card.Quote?.SellMin?.ToString("0.##")??"—")+" P   ·   "+card.Item.Ducats+" 두캇":"거래 불가";
            panel.Children.Add(new TextBlock{Text=value,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(242,203,87)),Margin=new(0,4,0,4)});
            if(page.FarmingHint?.Invoke(card.Item) is {} hint)panel.Children.Add(new TextBlock{Text=hint.Text,FontSize=13,LineHeight=17,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=hint.Highlight?new SolidColorBrush(Color.FromRgb(242,203,87)):Brushes.LightGray,Margin=new(0,0,0,3)});
            string detail=card.Item is null?"앱에서 이름 수정":card.Item.Tradable&&card.Quote is null?"시세 조회 중…":card.Quote is {Stale:true}?"오래된 시세 · 현재가 아님":card.Quote?.Error is not null?"연결 실패 · 두캇으로 비교":card.Quote is {SellOrders:0}?"판매 주문 없음":card.Best.Text;
            if(card.Quote is {Snapshot:true,Error:not null,Stale:false})detail="저장 시세 준비 중 · 두캇으로 비교";
            if(card.Quote is {Error:null} q)detail+=(detail.Length>0?" · ":"")+q.FetchedUtc.LocalDateTime.ToString("MM-dd HH:mm")+(q.Snapshot?" 저장 시세":q.Cached?" 캐시":"");
            panel.Children.Add(new TextBlock{Text=detail,FontSize=12,LineHeight=17,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});
            cards.Children.Add(new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(55,57,59)),BorderThickness=new(0,0,1,0),Child=panel});
        }
    }
}
