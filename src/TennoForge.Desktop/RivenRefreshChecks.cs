using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckRivenRefresh(string directory,Action<bool,string> yes,Action<FrameworkElement,string,int,int,double> render)
    {
        var dimPath=Path.Combine(Environment.CurrentDirectory,"artifacts","riven-real-reroll-2026-10-25","real-reroll.png");
        if(File.Exists(dimPath))
        {
            var image=RelicOcr.Load(dimPath);var dimProbe=RivenVisualProbe.Analyze(image);
            yes(dimProbe.Regions.Length==2,"실제 순환 화면: 어두운 이전 카드와 밝은 새 카드 모두 후보 감지");
            var r=await RivenOcr.ReadAutomaticAsync(image,dimProbe.Regions,"Auto",8,default);
            yes(r.Cards.Length==2&&r.Cards.All(c=>c.Corroborated&&c.Report.Valid),"실제 순환 화면: 카드별 밝기·잠금 옵션 포함 두 카드 교차 확인");
            var dimOld=r.Cards[0].Report.Options;var dimNext=r.Cards[1].Report.Options;
            yes(dimOld.Select(o=>o.Value).SequenceEqual(new[]{118.8,108.1,106.4,100d})&&dimNext.Select(o=>o.Value).SequenceEqual(new[]{107.1,108.1,61.5,38d}),"실제 순환 화면: 전후 8개 수치 정확·서로 섞이지 않음");
            var knownRight=new[]{dimProbe.Regions[1]};var s=RivenVisualProbe.Analyze(image,knownRight).Signature;int width=640;
            yes(s.Where((value,index)=>index%width<width*.7).Count(value=>value!=0)>200,"한 카드 추적 중 다른 열에 나타난 이전 카드도 변경 신호에 포함");
            render(RivenOverlay.BuildSide(r,false),"native-reroll-left",720,600,1);render(RivenOverlay.BuildSide(r,true),"native-reroll-right",360,600,1);
        }
        var fangPath=Path.Combine(Environment.CurrentDirectory,"artifacts","riven-labels-2026-10-25","fang.png");
        if(File.Exists(fangPath))
        {
            var fangImage=RelicOcr.Load(fangPath);var fangProbe=RivenVisualProbe.Analyze(fangImage);
            foreach(bool cropped in new[]{false,true})
            {
                var fang=cropped?await RivenOcr.ReadAutomaticAsync(fangImage,fangProbe.Regions,"Melee",8,default):await RivenOcr.ReadAsync(fangImage,"Melee",8,default);
                File.WriteAllText(Path.Combine(directory,$"fang-{cropped}.json"),JsonSerializer.Serialize(fang,Catalog.Json));
                yes(fang.Cards.Length==1&&fang.Cards[0].Corroborated&&fang.Cards[0].Report.Valid,$"실제 팽 {cropped}: 잠금 흰색·마무리 일격 음옵 교차 확인");
                var options=fang.Cards[0].Report.Options;
                yes(options.Select(o=>o.Value).SequenceEqual(new[]{141.8,140.7,76.1})&&options[2].Key=="FinisherDamage"&&options[2].Negative,$"실제 팽 {cropped}: 상피141.8·전기140.7·마무리 일격−76.1");
                if(cropped)render(RivenOverlay.BuildSide(fang,false),"fang-left",360,530,1);
            }
        }
        var hirudoPath=Path.Combine(Environment.CurrentDirectory,"artifacts","riven-range-2026-10-25","hirudo.png");
        if(File.Exists(hirudoPath))
        {
            var hirudo=await RivenOcr.ReadAsync(RelicOcr.Load(hirudoPath),"Melee",8,default);
            File.WriteAllText(Path.Combine(directory,"hirudo-recognition.json"),JsonSerializer.Serialize(hirudo,Catalog.Json));
            yes(hirudo.Cards.Length==1&&hirudo.Cards[0].Corroborated&&hirudo.Cards[0].Report.Valid,"실제 히루도: 단위 없는 사거리를 포함한 네 옵션 교차 확인");
            var options=hirudo.Cards[0].Report.Options;
            yes(options.Select(o=>o.Value).SequenceEqual(new[]{101.1,63.9,2,41d})&&options[2].Key=="Range"&&options[2].Unit=="m"&&options[3].Negative,"실제 히루도: 전기101.1·공속63.9·사거리2m·코퍼스×0.59");
            render(RivenOverlay.BuildSide(hirudo,false),"hirudo-left",360,584,1);
        }
        var fixtures=Path.Combine(Environment.CurrentDirectory,"artifacts","riven-refresh-2026-10-25");
        if(!File.Exists(Path.Combine(fixtures,"siam.png"))||!File.Exists(Path.Combine(fixtures,"inventory.png")))return;
        var siam=RelicOcr.Load(Path.Combine(fixtures,"siam.png"));var inventory=RelicOcr.Load(Path.Combine(fixtures,"inventory.png"));
        var probe=RivenVisualProbe.Analyze(siam);yes(probe.Regions.Length>0,"실제 시암 상세 화면: 후보 감지");
        var result=await RivenOcr.ReadAutomaticAsync(siam,probe.Regions,"Melee",8,default);
        File.WriteAllText(Path.Combine(directory,"siam-recognition.json"),JsonSerializer.Serialize(result,Catalog.Json));
        yes(result.Cards.Length==1&&result.Cards[0].Corroborated&&result.Cards[0].Report.Valid,"실제 시암: 잠긴 흰색 옵션·팩션 배율 교차 확인");
        var report=result.Cards[0].Report;
        yes(report.Options.Select(o=>o.Value).SequenceEqual(new[]{110.3,108.6,95.2,42d})&&report.Options[1].Key=="StatusDamage"&&report.Options[3].Negative,"실제 시암: 치피 110.3·접합 상피 108.6·전기 95.2·인페스티드 ×0.58");
        render(RivenOverlay.BuildSide(result,false),"siam-left",360,560,1);render(RivenOverlay.BuildSide(result,true),"siam-right",360,560,1);
        foreach(var width in new[]{1920,2560,3440})
        {
            var scaled=new TransformedBitmap(inventory,new ScaleTransform(width/(double)inventory.PixelWidth,width/(double)inventory.PixelWidth));
            yes(RivenVisualProbe.Analyze(scaled).Regions.Length==0,$"실제 모드 목록 {width}px: 상세 리벤 후보 제외");
        }
        // Equal-sized preview replay, so a changed buffer length cannot make the
        // transition pass accidentally. No game capture or desktop activation.
        static BitmapSource Frame(BitmapSource source)
        {
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(source,new Rect(0,0,640,360));
            var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
        }
        static Rect[] Mapped(RivenProbe p,RivenOcrResult r)
        {
            var crop=r.SourceCrop??RivenVisualProbe.CropBounds(p.Regions);return r.Cards.Select(c=>new Rect(crop.X+c.Bounds.X*crop.Width,crop.Y+c.Bounds.Y*crop.Height,c.Bounds.Width*crop.Width,c.Bounds.Height*crop.Height)).ToArray();
        }
        var old=RelicOcr.Load(Path.Combine(Environment.CurrentDirectory,"artifacts","riven-auto-2026-10-25","innodem-real.png"));
        var oldProbe=RivenVisualProbe.Analyze(old);var oldResult=await RivenOcr.ReadAsync(RivenVisualProbe.Crop(old,oldProbe.Regions),"Melee",8,default);
        var oldRegions=Mapped(oldProbe,oldResult);var oldFrame=Frame(old);var nextFrame=Frame(siam);var listFrame=Frame(inventory);
        var watch=new RivenWatchState(640);watch.Prime(RivenVisualProbe.Analyze(oldFrame,oldRegions).Signature,0);
        var next=RivenVisualProbe.Analyze(nextFrame,oldRegions);
        yes(watch.Observe(next.Regions.Length>0,next.Signature,750,true)==RivenWatchAction.None,"실제 카드 전환: 한 프레임 차이만으로 창을 닫지 않음");
        next=RivenVisualProbe.Analyze(nextFrame);
        watch.Observe(next.Regions.Length>0,next.Signature,1500,true);
        yes(watch.Observe(next.Regions.Length>0,next.Signature,2250,true)==RivenWatchAction.Read,"실제 카드 전환: 지속된 변경은 새 카드 재인식 요청");
        var nextRegions=Mapped(probe,result);var nextKnown=RivenVisualProbe.Analyze(nextFrame,nextRegions);watch.Prime(nextKnown.Signature,6000);
        yes(Enumerable.Range(0,20).All(i=>watch.Observe(true,nextKnown.Signature,6750+i*750,true)==RivenWatchAction.None),"변경 없는 새 카드 15초: 중복 OCR 없음");
        var list=RivenVisualProbe.Analyze(listFrame,nextRegions);
        yes(watch.Observe(list.Regions.Length>0,list.Signature,23000,true)==RivenWatchAction.None&&watch.Observe(list.Regions.Length>0,list.Signature,23750,true)==RivenWatchAction.Clear,"상세 카드 → 실제 모드 목록: 두 샘플 안에 닫기");
        yes(Enumerable.Range(0,20).All(i=>watch.Observe(false,list.Signature,24500+i*750,true)==RivenWatchAction.None),"모드 목록에 머무는 동안 오버레이 재표시·OCR 없음");
        var again=RivenVisualProbe.Analyze(oldFrame);
        yes(watch.Observe(true,again.Signature,41000,true)==RivenWatchAction.None&&watch.Observe(true,again.Signature,41750,true)==RivenWatchAction.Read,"목록에서 다른 리벤 재진입: 다시 자동 인식 요청");
        lastRivenCapture=oldResult;rivenTextRegions=oldRegions;
        rivenOverlay=new RivenOverlay(new WindowInteropHelper(this).Handle,oldResult);rivenOverlayRight=new RivenOverlay(new WindowInteropHelper(this).Handle,oldResult,right:true);
        rivenOverlay.Show();rivenOverlayRight.Show();
        var leftHandle=new WindowInteropHelper(rivenOverlay).Handle;var rightHandle=new WindowInteropHelper(rivenOverlayRight).Handle;
        for(int i=0;i<20;i++){rivenOverlay.SetPending(true);rivenOverlayRight.SetPending(true);rivenOverlay.UpdateResult(oldResult with{Seconds=i});rivenOverlayRight.UpdateResult(oldResult with{Seconds=i});}
        yes(rivenOverlay.ContentUpdates==1&&rivenOverlayRight.ContentUpdates==1&&!rivenOverlay.Pending&&rivenOverlay.IsVisible,"같은 리벤 20회 재확인: 창·내용 재생성 없음");
        rivenOverlay.SetPending(true);rivenOverlayRight.SetPending(true);rivenOverlay.UpdateResult(result);rivenOverlayRight.UpdateResult(result);
        yes(new WindowInteropHelper(rivenOverlay).Handle==leftHandle&&new WindowInteropHelper(rivenOverlayRight).Handle==rightHandle&&rivenOverlay.ContentUpdates==2&&rivenOverlayRight.ContentUpdates==2,"다른 리벤: 동일 HWND를 유지하며 내용만 교체");
        render((FrameworkElement)rivenOverlay.Content,"siam-persistent-window",360,584,1);
        InvalidateRivenCapture("카드 변경 감지 · 검수");
        yes(rivenOverlay.Dismissed&&rivenOverlayRight.Dismissed&&lastRivenCapture is null&&rivenTextRegions.Length==0,"실제 양쪽 창: 무효화 처리로 닫힘·이전 결과와 ROI 제거");
    }
}
