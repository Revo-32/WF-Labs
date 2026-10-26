using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class ScheduleMarqueeChecks
{
    internal static async Task Run(string directory)
    {
        var checks=new List<string>();var scans=new List<object>();void Yes(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        var preferences=new SchedulePreferences{Width=400,Height=570,BackgroundOpacity=.55,TextShadow=true};
        var missions=new[]{"옴니아 · 보이드 홍수","메소 · 파괴공작","레퀴엠 · 이동 방어","액시 · 생존","리스 · 섬멸"};
        var longData=new ScheduleOverlayData([new("nextS","다음 S티어 중재","감청 · 목성 / Callisto","00:42:10")],Enumerable.Range(0,12).Select(i=>new ScheduleOverlayEntry("fissure-"+i,missions[i%missions.Length],"자리만 · 투불 광장","00:"+(5+i).ToString("00")+":18","강철의 길",i==0)).ToArray(),true,"",""){FissureFilter="강철의 길"};
        var window=new ScheduleOverlay(preferences,p=>preferences=p);var bounds=new Int32Rect(20,20,2200,1300);
        async Task Settle(){window.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);window.UpdateLayout();await Task.Delay(80);}
        void Save(string name)
        {
            var root=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(directory,name+".png"));encoder.Save(stream);
        }
        try
        {
            window.Update("08:25:29",longData,preferences);window.PresentAt(bounds);await Settle();var view=LayoutDiagnostics.Tree(window).OfType<ScheduleOverlayView>().Single();var ticker=view.Marquee;var root=(FrameworkElement)window.Content;
            Yes(ticker.Running&&ticker.ContentHeight>ticker.ViewportHeight,"표시 영역을 넘는 본문만 자동 스크롤 시작");
            Yes(!LayoutDiagnostics.Tree(view).OfType<ScrollBar>().Any(b=>b.IsVisible),"오버레이 스크롤바 표시 없음");
            Yes(Math.Abs(ticker.Offset)<.5,"첫 화면을 읽는 동안 시작 위치 유지");var clockPosition=view.ClockForCheck.TranslatePoint(new(),root);var bodyPosition=view.PrimaryContent.TranslatePoint(new(),root);Save("01-start");
            ticker.PauseForCheck();await Settle();ticker.SeekForCheck(VerticalMarquee.ReadingPause+2);await Settle();Yes(Math.Abs(ticker.Offset-24)<1,$"본문은 초당12 DIP의 일정한 속도로 위로 이동 · offset={ticker.Offset}, height={ticker.ContentHeight}, viewport={ticker.ViewportHeight}, revision={ticker.Revision}");Yes(view.ClockForCheck.TranslatePoint(new(),root)==clockPosition,"현재 시각 위치 고정");Yes(Math.Abs(view.PrimaryContent.TranslatePoint(new(),root).Y-bodyPosition.Y+24)<1,"일정과 균열을 포함한 본문 전체 이동");
            var revision=ticker.Revision;var offset=ticker.Offset;for(int i=0;i<5;i++){window.Update("08:25:3"+i,longData with{Fissures=longData.Fissures.Select(f=>f with{Value="00:04:"+(20-i)}).ToArray()},preferences);await Settle();}
            Yes(ticker.Revision==revision&&Math.Abs(ticker.Offset-offset)<1,"매초 시각·남은 시간 갱신에도 애니메이션 재시작 없음");
            ticker.ResumeForCheck();await Task.Delay(650);await Settle();Yes(ticker.Offset>offset+4&&ticker.Offset<offset+18,"실시간 애니메이션도 천천히 진행");ticker.PauseForCheck();Save("02-moving");
            ticker.SeekForCheck(ticker.BottomTime+.3);await Settle();double bottom=ticker.ContentHeight-ticker.ViewportHeight;Yes(Math.Abs(ticker.Offset-bottom)<1,"마지막 항목이 들어온 위치에서 정지");ticker.SeekForCheck(ticker.BottomTime+2);await Settle();Yes(Math.Abs(ticker.Offset-bottom)<1,"마지막 항목을 읽는 정지 시간 제공");Save("03-bottom");
            ticker.SeekForCheck(ticker.LoopTime-.01);await Settle();Yes(Math.Abs(ticker.DuplicateForCheck.TranslatePoint(new(),root).Y-bodyPosition.Y)<1,"끝 다음의 반복 사본이 첫 위치로 이어짐");Save("04-before-loop");
            ticker.SeekForCheck(ticker.LoopTime+.01);await Settle();Yes(ticker.Offset<1,"한 바퀴 뒤 빈 화면 없이 시작 위치로 순환");Save("05-after-loop");
            foreach(var (width,height,textScale) in new[]{(340,470,1d),(400,570,1.5),(760,620,1d),(760,620,1.5)})
            {
                window.Width=width;window.Height=height;preferences=preferences with{TextScale=textScale};window.Update("08:25:29",longData,preferences);await Settle();
                var issues=LayoutDiagnostics.Inspect(root);scans.Add(new{width,height,textScale,issues});File.WriteAllText(Path.Combine(directory,"layout.json"),JsonSerializer.Serialize(scans,Catalog.Json));
                Yes(issues.Length==0,$"{width}×{height} / 글자{textScale} · 본문 잘림·겹침 검사");Yes(ticker.Running,$"{width} / 글자{textScale} · 크기 변경 후 자동 순환 재계산");Yes(!LayoutDiagnostics.Tree(view).OfType<ScrollBar>().Any(b=>b.IsVisible),"크기·글자 배율 변경 후에도 스크롤바 없음");Save($"size-{width}-{textScale}");
            }
            preferences=preferences with{TextScale=1};var shortData=longData with{Timers=longData.Timers,Fissures=longData.Fissures.Take(1).ToArray()};window.Update("08:25:29",shortData,preferences);await Settle();Yes(!ticker.Running&&!ticker.HasAnimation&&ticker.Offset==0,"짧은 목록으로 바뀌면 애니메이션 제거·정지");Save("06-short");
            window.Update("08:25:29",longData,preferences);await Settle();window.BeginEdit(bounds);await Settle();Yes(window.Editing&&!ticker.HasAnimation,"위치·크기 편집 중에는 흐름 정지");
            var done=LayoutDiagnostics.Tree(window).OfType<Button>().Single(b=>Equals(b.Content,"완료"));((IInvokeProvider)new ButtonAutomationPeer(done).GetPattern(PatternInterface.Invoke)).Invoke();await Settle();Yes(!window.Editing&&!window.IsVisible&&!ticker.HasAnimation,"편집 완료 실제 UIA 클릭 · 숨김 상태의 애니메이션 제거");
            window.PresentAt(bounds);await Settle();Yes(ticker.Running,"다시 표시하면 자동 순환 재개");var hwnd=new WindowInteropHelper(window).Handle;Yes(HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor.A==0,"애니메이션·편집 후에도 native 바탕 투명 유지");Yes((RelicOverlay.GetStyle(hwnd,-20).ToInt64()&0x08000020L)==0x08000020L,"움직이는 본문도 비활성·클릭 통과 유지");
            window.Conceal();await Settle();Yes(!ticker.HasAnimation&&ticker.Offset==0,"게임 밖·오버레이 숨김 상태에서 렌더링 애니메이션 정지");
            File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=Array.Empty<string>(),checks,scans},Catalog.Json));
        }
        finally{window.Close();}
    }
}
