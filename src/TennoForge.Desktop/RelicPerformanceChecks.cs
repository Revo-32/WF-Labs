using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckRelicResolution(string dir)
    {
        var catalog=RelicCatalog.Bundled();var items=new[]{"sevagoth_prime_neuroptics_blueprint","protea_prime_neuroptics_blueprint","baza_prime_receiver","velox_prime_barrel"}.Select(s=>catalog.Items.Single(x=>x.Slug==s)).ToArray();
        var runs=new List<object>();
        foreach(var (font,label,color) in new[]{(18,"small",Color.FromRgb(230,230,230)),(22,"gold",Color.FromRgb(165,126,65))})
        {
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(24,28,34),Color.FromRgb(78,73,66),25),null,new(0,0,1920,1080));
                dc.DrawText(new FormattedText("성유물 보상 선택",CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),30,Brushes.White,1),new(70,80));
                for(int i=0;i<4;i++)
                {
                    string text=items[i].Name;var split=text.LastIndexOf(' ');text=text[..split]+"\n"+text[(split+1)..];
                    var name=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),font,new SolidColorBrush(color),1){TextAlignment=TextAlignment.Center};dc.DrawText(name,new(1920*(i+1d)/5,1080*.46));
                }
            }
            var master=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);master.Render(drawing);master.Freeze();
            for(int sample=0;sample<3;sample++)
            foreach(var width in sample%2==0?new[]{1024,1920}:new[]{1920,1024})
            {
                var image=FitImage(master,width,width==1024?720:1080);SaveFixture(image,Path.Combine(dir,$"{label}-{width}.png"));
                using var view=new RelicPage(new RelicMarketClient(Path.Combine(dir,$"live-{label}-{width}-{sample}")));SelectPage(4);page.Content=view;
                var timer=Stopwatch.StartNew();await view.ReadAsync(image);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);var result=view.LastRecognitionForCheck!;double seconds=timer.Elapsed.TotalSeconds;
                var actual=result.Rewards.Select(r=>r.Item?.Id).ToArray();var top=result.Rewards.Select(r=>r.Candidates.FirstOrDefault()?.Item.Id).ToArray();
                var wrongAutomatic=result.Rewards.Where((r,i)=>r.Item is not null&&(i>=items.Length||r.Item.Id!=items[i].Id)).Count();
                runs.Add(new{kind="synthetic-challenging-pair-live-market",label,font,width,height=image.PixelHeight,sample,seconds,timing=result.Timing,automatic=actual.Count(x=>x is not null),correctTop=top.Where((id,i)=>i<items.Length&&id==items[i].Id).Count(),wrongAutomatic,expected=items.Select(x=>x.Id),actual,top,notice=result.Notice,quotes=view.Cards.Select(c=>c.Quote)});
                File.WriteAllText(Path.Combine(dir,"timings.json"),JsonSerializer.Serialize(runs,Catalog.Json));
            }
        }
        return new{runs,synthetic=true,note="FHD 합성 원본을 비율 유지 축소. 실제 FHD 게임 보상 표본이 아니므로 일반적인 인식률로 해석하지 않음."};
    }
    internal async Task<object> CheckRelicPerformance(string dir,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        var catalog=RelicCatalog.Bundled();
        var items=new[]{"nikana_prime_blade","braton_prime_blueprint","ash_prime_chassis_blueprint",""}.Select(s=>s==""?catalog.Items.Single(x=>x.Id=="local:forma-blueprint"):catalog.Items.Single(x=>x.Slug==s)).ToArray();
        var runs=new List<object>();
        using var view=new RelicPage(new RelicMarketClient(Path.Combine(dir,"fixture-cache"),new RelicFixtureHandler(catalog)));SelectPage(4);page.Content=view;
        // Synthetic fixtures are rendered natively at each supported resolution.
        // They measure throughput and correctness, not real-world recognition rates.
        foreach(var count in new[]{4,1,2,3})
        {
            var expected=items.Take(count).ToArray();
            foreach(var width in new[]{1920,2560,3440,3840})
            {
                int height=width==3440?1440:width*9/16;var input=RelicFixture(expected,width,height,true);
                SaveFixture(input,Path.Combine(dir,$"comparison-{width}-{count}.png"));
                for(int sample=0;sample<2;sample++)
                {
                    var timer=Stopwatch.StartNew();await view.ReadAsync(input);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);var elapsed=timer.Elapsed.TotalSeconds;
                    var result=view.LastRecognitionForCheck!;var ids=view.Cards.Select(c=>c.Item?.Id).ToArray();
                    var correct=ids.SequenceEqual(expected.Select(x=>x.Id));
                    runs.Add(new{kind="synthetic-native-supported",width,height,count,sample,seconds=elapsed,timing=result.Timing,correct,automatic=ids.Count(x=>x is not null),expected=expected.Select(x=>x.Id),actual=ids,quotes=view.Cards.Select(c=>c.Quote),market="fixture with actual 400ms request-start gate"});
                    File.WriteAllText(Path.Combine(dir,"timings.json"),JsonSerializer.Serialize(runs,Catalog.Json));
                    Yes(correct,$"동일 보상 {width}×{height} / {count}개 / 반복 {sample+1} 정확·자동 확정");
                }
            }
        }
        // Real network, cold then warm cache; do not mix this variability into the
        // paired resolution comparison. Cache paths are isolated from the user's.
        foreach(var width in new[]{1920,3840})
        {
            using var live=new RelicPage(new RelicMarketClient(Path.Combine(dir,"live-cache-"+width)));page.Content=live;
            var input=RelicFixture(items,width,width*9/16,true);
            for(int sample=0;sample<2;sample++)
            {
                var timer=Stopwatch.StartNew();await live.ReadAsync(input);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);
                runs.Add(new{kind="synthetic-real-market",width,height=input.PixelHeight,count=4,sample,seconds=timer.Elapsed.TotalSeconds,timing=live.LastRecognitionForCheck!.Timing,quotes=live.Cards.Select(c=>c.Quote),market="https://api.warframe.market/v2/"});
                File.WriteAllText(Path.Combine(dir,"timings.json"),JsonSerializer.Serialize(runs,Catalog.Json));
                Yes(live.Cards.Where(c=>c.Item!.Tradable).All(c=>c.Quote is {Error:null}),$"{width} 실제 마켓 {(sample==0?"신규 조회":"캐시")} 완료");
            }
        }
        page.Content=view;render(this,"page",1760,920,1);
        Yes(view.LastElapsedSeconds>0&&view.Status.Text.Contains("초"),"인식·시세 실제 경과 시간 표시");
        RelicCapture.ValidateGameSize(1920,1080);Yes(true,"게임 최소 FHD 1920×1080 허용");
        foreach(var size in new[]{(640,360),(1024,720),(1919,1080),(1920,1079)}){bool rejected=false;try{RelicCapture.ValidateGameSize(size.Item1,size.Item2);}catch(InvalidOperationException){rejected=true;}Yes(rejected,$"FHD 미만 게임 크기 {size} 차단");}
        await CheckRelicOverlayNative(view,dir,checks,render);
        return new{passed=true,checks,runs,synthetic=true,realRewardScreenPairedComparison=false};
    }
    private static BitmapSource FitImage(BitmapSource input,int width,int height)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Black,null,new(0,0,width,height));var scale=Math.Min((double)width/input.PixelWidth,(double)height/input.PixelHeight);
            double w=input.PixelWidth*scale,h=input.PixelHeight*scale;RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
            dc.DrawImage(input,new((width-w)/2,(height-h)/2,w,h));
        }
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();return image;
    }
    private async Task CheckRelicOverlayNative(RelicPage view,string dir,List<string> checks,Action<FrameworkElement,string,int,int,double> render)
    {
        void Yes(bool v,string n){if(!v)throw new InvalidOperationException(n);checks.Add(n);}
        if(Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")=="1")
        {
            var isolated=new RelicOverlay(IntPtr.Zero,view);view.UpdateHighlights();
            try
            {
                foreach(var width in new[]{992,1440})
                {
                    render((FrameworkElement)isolated.Content,"overlay-headless-"+width,width,184,1);
                    Yes(LayoutDiagnostics.Inspect((FrameworkElement)isolated.Content).Length==0,$"격리 오버레이 {width}px 글자·경계 겹침 없음");
                }
            }
            finally{isolated.Close();}
            return;
        }
        var previous=RelicCapture.GetForegroundWindow();GetCursorPos(out var cursor);
        var host=new Window{Title="WF Labs 오버레이 검수용 창",Width=1024,Height=720,Left=25,Top=25,Topmost=true,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Background=Brushes.DarkSlateGray};
        RelicOverlay? overlay=null;
        try
        {
            host.Show();host.Activate();await Task.Delay(200);var handle=new WindowInteropHelper(host).Handle;
            if(RelicCapture.GetForegroundWindow()!=handle)
            {
                var p=host.PointToScreen(new Point(100,30));
                Yes(OverlayWindowAt(new((int)p.X,(int)p.Y))==handle,"검수 창의 안전한 활성화 위치 확인");
                SetCursorPos((int)p.X,(int)p.Y);
                var click=new[]{new InputPacket{Mouse=new(){Flags=2}},new InputPacket{Mouse=new(){Flags=4}}};SendInput(2,click,System.Runtime.InteropServices.Marshal.SizeOf<InputPacket>());await Task.Delay(200);
            }
            Yes(RelicCapture.GetForegroundWindow()==handle,"검수용 게임 역할 창 활성화");
            overlay=new RelicOverlay(handle,view);overlay.Present();view.UpdateHighlights();await Task.Delay(300);
            Yes(RelicCapture.GetForegroundWindow()==handle,"오버레이 표시·결과 갱신이 게임 역할 창의 초점을 빼앗지 않음");
            var overlayHandle=new WindowInteropHelper(overlay).Handle;
            Yes((RelicOverlay.GetStyle(overlayHandle,-20).ToInt64()&RelicOverlay.PassThroughStyle)==RelicOverlay.PassThroughStyle,"실제 HWND NOACTIVATE·TRANSPARENT·TOOLWINDOW 확인");
            var b=RelicCapture.ClientBounds(handle);var o=RelicCapture.ClientBounds(overlayHandle);
            Yes(o.X>=b.X&&o.Y>=b.Y&&o.X+o.Width<=b.X+b.Width&&o.Y+o.Height<=b.Y+b.Height,"1024×720 게임 창 내부 오버레이 경계");
            Yes(LayoutDiagnostics.Inspect((FrameworkElement)overlay.Content).Length==0,"오버레이 글자·경계 겹침 없음");
            render((FrameworkElement)overlay.Content,"overlay-1024",o.Width,o.Height,1);
            SaveFixture(RelicCapture.Capture(handle),Path.Combine(dir,"overlay-native-capture.png"));
            // WindowFromPoint verifies native hit testing, without sending a click to any application.
            Yes(OverlayWindowAt(new(o.X+o.Width/2,o.Y+o.Height/2))==handle,"오버레이 중앙 네이티브 히트 테스트가 아래 게임 역할 창으로 통과");
            overlay.Close();Yes(overlay.Dismissed,"오버레이 닫힘·구독 해제");
            overlay=new RelicOverlay(handle,view);overlay.Present();
            var other=new Window{Width=200,Height=100,Left=30,Top=30,Title="초점 전환 검수"};
            try{other.Show();other.Activate();await Task.Delay(500);Yes(overlay.Dismissed,"다른 앱으로 전환하면 오버레이 자동 닫힘");}finally{other.Close();}
        }
        finally{if(overlay is {Dismissed:false})overlay.Close();host.Close();SetCursorPos(cursor.X,cursor.Y);if(previous!=IntPtr.Zero)RelicCapture.SetForegroundWindow(previous);}
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]private readonly record struct OverlayPoint(int X,int Y);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="WindowFromPoint")]private static extern IntPtr OverlayWindowAt(OverlayPoint point);
    internal async Task<object> CheckRelicOverlay(string dir,Action<FrameworkElement,string,int,int,double> render)
    {
        var catalog=RelicCatalog.Bundled();using var view=new RelicPage(new RelicMarketClient(Path.Combine(dir,"cache"),new RelicFixtureHandler(catalog)));SelectPage(4);page.Content=view;
        var names=new[]{"sevagoth_prime_neuroptics_blueprint","protea_prime_neuroptics_blueprint","baza_prime_receiver","velox_prime_barrel"}.Select(s=>catalog.Items.Single(i=>i.Slug==s).Name).ToArray();
        view.ApplyRecognition(new(catalog.Recognize(names.Select((n,i)=>new RewardText(n,.04+.24*i,.4,.2,.03,99)).ToArray()),[],"검수용 합성 결과"));await view.RefreshAsync(false);
        var checks=new List<string>();await CheckRelicOverlayNative(view,dir,checks,render);render(this,"page",1760,920,1);return new{passed=true,checks,gameInputsSent=false,synthetic=true};
    }
}
