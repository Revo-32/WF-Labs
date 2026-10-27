using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckRivenCapture(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var recognitions=new List<object>();
        void Yes(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);}
        var manualWindow=new RivenCaptureWindow(new(Automatic:true));
        try
        {
            Yes(!manualWindow.CancelButton.IsEnabled&&manualWindow.LoadButton.IsEnabled&&manualWindow.PasteButton.IsEnabled,"리벤 대기 상태에서는 수동 인식 취소 비활성");
            Yes(manualWindow.Status.Text.Contains("자동 인식 켜짐"),"리벤 인식 창 초기 안내가 실제 자동 인식 설정과 일치");
            var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            manualWindow.ClipboardImage=()=>BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[4],4);
            manualWindow.ReadImage=async(_,_,_,token)=>{started.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}finally{canceled.TrySetResult();}throw new InvalidOperationException("완료되지 않는 검수용 판독");};
            static void InvokeManual(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            InvokeManual(manualWindow.PasteButton);await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Yes(manualWindow.CancelButton.IsEnabled&&!manualWindow.LoadButton.IsEnabled&&!manualWindow.PasteButton.IsEnabled,"실제 클립보드 읽기 버튼에서 수동 인식 진행·중복 시작 차단");
            InvokeManual(manualWindow.CancelButton);await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));await System.Windows.Threading.Dispatcher.Yield();
            Yes(!manualWindow.CancelButton.IsEnabled&&manualWindow.LoadButton.IsEnabled&&manualWindow.PasteButton.IsEnabled&&manualWindow.Status.Text.Contains("취소했습니다"),"실제 수동 취소 버튼에서 취소 전달·입력 복구");
            Yes(manualWindow.Automatic.IsChecked==true,"수동 파일·클립보드 취소가 자동 인식 설정을 끄지 않음");
            render((FrameworkElement)manualWindow.Content,"manual-canceled",1100,780,1);
        }
        finally{manualWindow.Close();}
        var prepared=RivenOcr.PrepareAsync();
        Yes(Enumerable.Range(0,20).All(_=>ReferenceEquals(prepared,RivenOcr.PrepareAsync())),"모델 준비: 반복 감지 20회에도 백그라운드 작업은 한 번만 생성");await prepared;
        var legacyPreferences=JsonSerializer.Deserialize<RivenCapturePreferences>("{\"Kind\":\"Melee\",\"Rank\":8,\"Automatic\":true}")!;
        Yes(legacyPreferences.DetectKind&&legacyPreferences.ReadKind=="Auto","이전 근접 설정 파일도 자동 종류 판별이 기본값");
        await CheckDisplayedCapture(Yes);
        await CheckRivenRefresh(directory,Yes,render);
        static void Click(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        var left=RivenScan.Parse("+195.7% 근접 피해\n+106.9% 상태이상 확률","Melee",8);
        var damage=RivenScan.Parse("+195.7% 근접 피해","Melee",8);var status=RivenScan.Parse("+106.9% 상태이상 확률","Melee",8);
        Yes(RivenOcr.CorroboratedBy(left,[left,damage,status]),"분리 판독: 완전한 한 판독과 각 옵션의 다른 판독이 모두 일치");
        Yes(!RivenOcr.CorroboratedBy(left,[left,damage]),"상확이 한 번만 읽히면 자동 확정 금지");
        Yes(!RivenOcr.CorroboratedBy(left,[left,left]),"동일 판독 객체 재사용은 추가 근거로 세지 않음");
        var conflict=RivenScan.Parse("+194.7% 근접 피해","Melee",8);var conflictAgain=RivenScan.Parse("+194.7% 근접 피해","Melee",8);
        Yes(!RivenOcr.CorroboratedBy(left,[left,damage,status,conflict,conflictAgain]),"반복된 다른 숫자가 있으면 다수 선택으로 자동 확정하지 않음");
        Yes(!RivenOcr.CorroboratedBy(left,[left,damage,RivenScan.Parse("-106.9% 상태이상 확률","Melee",8)]),"부호가 다르면 숫자가 같아도 교차 확인 실패");
        Yes(!RivenOcr.CorroboratedBy(left,[left,damage,RivenScan.Parse("+106.9 상태이상 확률","Melee",8)]),"단위 없는 판독으로 정상 수치를 보충하지 않음");
        var impossible=RivenScan.Parse("+195.79% 근접 피해","Melee",8);var impossibleAgain=RivenScan.Parse("+195.79% 근접 피해","Melee",8);
        Yes(RivenOcr.CorroboratedBy(left,[left,damage,status,impossible,impossibleAgain]),"표시할 수 없는 두 자리 소수는 근거·유효한 숫자 충돌로 세지 않음");
        var extra=RivenScan.Parse("+76.9% 강 공격 효율","Melee",8);
        Yes(!RivenOcr.CorroboratedBy(left,[left,damage,status,extra]),"별도 판독에서 추가 옵션을 읽으면 일부 옵션만 있는 후보를 완전한 카드로 확정하지 않음");
        var right=RivenScan.Parse("+185.3% 근접 피해\n+101.3% 화염\n+101.3% 냉기\n-81.0% 치명타 피해","Melee",8);
        Yes(left.Valid&&right.Valid,"검수용 실제 범위 수치 유효");
        RivenOcrResult? example=null;
        RivenOcrResult? actual=null;
        var liveFrames=Path.Combine(Environment.CurrentDirectory,"artifacts","riven-auto-fix-2026-10-25","live-ready");
        if(File.Exists(Path.Combine(liveFrames,"preview-15.png")))
        {
            var replay=Path.Combine(directory,"animated-replay");Directory.CreateDirectory(replay);
            Yes(await RivenProbeLiveCheck.Replay(liveFrames,replay)==1,"실전 배경 애니메이션 16프레임: 첫 인식 성공·같은 옵션 추가 OCR 없음");
        }
        var realFixture=Path.Combine(Environment.CurrentDirectory,"tests","fixtures","riven");
        var actualPath=Path.Combine(realFixture,"innodem-real.png");
        if(!File.Exists(actualPath))throw new InvalidOperationException("필수 실제 리벤 검수 자료가 없습니다.");
        using(var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(realFixture,"provenance.json"))))
            foreach(var entry in provenance.RootElement.EnumerateArray())
            {
                var file=Path.Combine(realFixture,entry.GetProperty("image").GetString()!);
                using var stream=File.OpenRead(file);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                Yes(hash.Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"실제 OCR fixture SHA-256 · "+entry.GetProperty("image").GetString());
            }
        if(File.Exists(actualPath))
        {
            var source=RelicOcr.Load(actualPath);var probe=RivenVisualProbe.Analyze(source);
            Yes(probe.Regions.Length is >=1 and <=2,"실제 인노뎀 화면: OCR 후보 영역 감지");
            actual=await RivenOcr.ReadAsync(RivenVisualProbe.Crop(source,probe.Regions),"Melee",8,default);
            File.WriteAllText(Path.Combine(directory,"actual-recognition.json"),JsonSerializer.Serialize(actual,Catalog.Json));
            Yes(actual.Cards.Length==1&&actual.Cards[0].Corroborated&&actual.Cards[0].Report.Options.Select(o=>o.Value).SequenceEqual(new[]{118.8,108.1,106.4,100d}),"실제 인노뎀 화면: 네 수치와 음옵 교차 확인");
            Yes(actual.Cards[0].Report.Options[3].Key=="SlideCriticalChance"&&actual.Cards[0].Report.Options[3].Negative,"문장 뒤 -100% 슬라이드 치명타 정확히 연결");
            foreach(var dimensions in new[]{(1920,1080),(3440,1440),(3840,2160)})
            {
                var scene=new DrawingVisual();using(var dc=scene.RenderOpen()){dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,dimensions.Item1,dimensions.Item2));dc.DrawImage(source,new Rect((dimensions.Item1-source.PixelWidth)/2d,(dimensions.Item2-source.PixelHeight)/2d,source.PixelWidth,source.PixelHeight));}
                var frame=new RenderTargetBitmap(dimensions.Item1,dimensions.Item2,96,96,PixelFormats.Pbgra32);frame.Render(scene);frame.Freeze();
                var detected=RivenVisualProbe.Analyze(frame);File.WriteAllText(Path.Combine(directory,$"probe-{dimensions.Item1}.json"),JsonSerializer.Serialize(detected.Regions,Catalog.Json));Save(frame,Path.Combine(directory,$"real-card-{dimensions.Item1}.png"));Yes(detected.Regions.Length is >=1 and <=2,$"실제 카드 원본 크기 / {dimensions}: OCR 후보 영역 감지");
                var read=await RivenOcr.ReadAutomaticAsync(frame,detected.Regions,"Auto",8,default);
                File.WriteAllText(Path.Combine(directory,$"recognition-{dimensions.Item1}.json"),JsonSerializer.Serialize(read,Catalog.Json));
                Yes(read.Cards.Length==1&&read.Cards[0].Corroborated&&read.Cards[0].Report.Options.SequenceEqual(actual.Cards[0].Report.Options),$"실제 카드 원본 크기 / {dimensions}: 네 옵션 정확히 인식");
            }
            var small=new TransformedBitmap(source,new ScaleTransform(640d/source.PixelWidth,640d/source.PixelWidth));var durations=new List<double>();
            for(int i=0;i<12;i++){var watchTime=System.Diagnostics.Stopwatch.StartNew();_=RivenVisualProbe.Analyze(small);if(i>1)durations.Add(watchTime.Elapsed.TotalMilliseconds);}
            File.WriteAllText(Path.Combine(directory,"probe-timing.json"),JsonSerializer.Serialize(new{width=small.PixelWidth,height=small.PixelHeight,meanMilliseconds=durations.Average(),maximumMilliseconds=durations.Max(),includesNativeScreenCapture=false},Catalog.Json));
            var watch=new RivenWatchState();Yes(watch.Observe(true,probe.Signature,0)==RivenWatchAction.None&&watch.Observe(true,probe.Signature,750)==RivenWatchAction.Read,"실제 화면: 단축키·로그 없이 자동 인식 시작");
            Yes(Enumerable.Range(0,160).All(n=>watch.Observe(true,probe.Signature,1500+n*750)==RivenWatchAction.None),"동일 리벤 화면 120초 재생: 추가 OCR 0회");
            var leftPanel=RivenOverlay.BuildSide(actual,false);render(leftPanel,"actual-overlay-left",360,560,1);
            var leftLabels=Descendants(leftPanel).OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Yes(!leftLabels.Any(t=>t.Contains("옵션 변경 후"))&&leftLabels.Any(t=>t.Contains("품질 87.0%")),"좌측 수율 전용 · 음옵 품질 숫자와 등급 방향 일치");
            var rightPanel=RivenOverlay.BuildSide(actual,true);render(rightPanel,"actual-overlay-right",360,650,1);render(rightPanel,"actual-overlay-right-150",360,650,1.5);
            var rightLabels=Descendants(rightPanel).OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Yes(rightLabels.Any(t=>t.Contains("근접 피해 필요"))&&rightLabels.Any(t=>t.Contains("상태이상 피해"))&&rightLabels.Contains("S–SSS"),"우측 실제 카드: 상태 확률 + 부족한 근접 피해 → 상태 피해 · 조건부 S–SSS");
            Yes(RivenScan.Opportunities(actual.Cards[0].Report).All(o=>rightLabels.Any(t=>t.Contains(o.ResultName))),"우측 실제 카드: 현재 옵션으로 파생되는 접합 가능성 모두 표시");
            var badges=new StackPanel{Orientation=Orientation.Horizontal};foreach(var score in new[]{.1,.3,.5,.7,.85,.94,.99})badges.Children.Add(RivenGradePresentation.Badge(score));render(badges,"grade-palette",540,60,1);
        }
        var pairFrame=RelicOcr.Load(Path.Combine(realFixture,"reroll-pair.png"));var pairProbe=RivenVisualProbe.Analyze(pairFrame);
        var realPair=await RivenOcr.ReadAutomaticAsync(pairFrame,pairProbe.Regions,"Auto",8,default);
        Yes(realPair.Cards.Length==2&&realPair.Cards.All(c=>c.Corroborated&&c.Report.Valid),"실제 순환 전후: 두 카드 모두 교차 확인");
        var expectedPair=new[]{new[]{218.3,110.1,177.7,65.7},new[]{56.6,110.1,99.3,116.4}};
        for(int index=0;index<2;index++)Yes(realPair.Cards[index].Report.Options.Select(o=>o.Value).SequenceEqual(expectedPair[index])&&realPair.Cards[index].Report.Options[^1].Negative,"실제 순환 전후: 수치·음옵·좌우 카드 유지 · "+index);
        File.WriteAllText(Path.Combine(directory,"actual-pair-recognition.json"),JsonSerializer.Serialize(realPair,Catalog.Json));
        var lockedSource=RelicOcr.Load(Path.Combine(realFixture,"innodem-two-locks-real.png"));
        using(var lockedProvenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(realFixture,"innodem-two-locks-provenance.json"))))
            Yes(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(realFixture,"innodem-two-locks-real.png")))).Equals(lockedProvenance.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"두 잠금 실제 카드의 원본 crop 해시 고정");
        var lockedRead=await RivenOcr.ReadAutomaticAsync(lockedSource,RivenVisualProbe.Analyze(lockedSource).Regions,"Auto",8,default);
        Yes(lockedRead.Cards is [var lockedCard]&&lockedCard.Corroborated&&lockedCard.Report.Valid&&lockedCard.Report.Kind=="Melee"&&lockedCard.Report.Options.Select(o=>o.Key).SequenceEqual(new[]{"Electricity","StatusDamage","HeavyEfficiency","SlashDamage"})&&lockedCard.Report.Options.Select(o=>o.Value).SequenceEqual(new[]{110.6,110.1,76.9,117.5})&&lockedCard.Report.Options.Select(o=>o.Negative).SequenceEqual(new[]{false,false,false,true}),"두 줄 제목·흰 잠금 두 옵션: 전기 포함 네 옵션·수치·부호·종류를 정확히 교차 확인");
        File.WriteAllText(Path.Combine(directory,"actual-two-locks-recognition.json"),JsonSerializer.Serialize(lockedRead,Catalog.Json));
        BitmapSource EditFirst(bool label,bool pair=false,bool outside=false)
        {
            int width=pair?lockedSource.PixelWidth*2+32:lockedSource.PixelWidth;var visual=new DrawingVisual();
            using(var drawing=visual.RenderOpen())
            {
                drawing.DrawImage(lockedSource,new Rect(0,0,lockedSource.PixelWidth,lockedSource.PixelHeight));
                if(pair)drawing.DrawImage(lockedSource,new Rect(lockedSource.PixelWidth+32,0,lockedSource.PixelWidth,lockedSource.PixelHeight));
                var area=outside?new Rect(2,2,20,20):label?new Rect(350,378,66,35):new Rect(145,378,140,35);
                drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(16,16,23)),null,area);
                if(!outside)drawing.DrawText(new FormattedText(label?"냉기":"+105.6%",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),27,Brushes.White,1),area.TopLeft);
            }
            var image=new RenderTargetBitmap(width,lockedSource.PixelHeight,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();return image;
        }
        var lockedBounds=lockedRead.Cards[0].Bounds;
        bool DetectFirstChange(BitmapSource before,BitmapSource after,Rect[] regions)
        {
            // The production probe receives a landscape game preview. A tight
            // portrait crop intentionally resembles the inventory-wide guard.
            int originX=(1920-before.PixelWidth)/2,originY=180;
            BitmapSource Screen(BitmapSource card)
            {
                var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.Black,null,new Rect(0,0,1920,1080));drawing.DrawImage(card,new Rect(originX,originY,card.PixelWidth,card.PixelHeight));}
                var frame=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);frame.Render(visual);frame.Freeze();return frame;
            }
            var mapped=regions.Select(r=>new Rect((originX+r.X*before.PixelWidth)/1920d,(originY+r.Y*before.PixelHeight)/1080d,r.Width*before.PixelWidth/1920d,r.Height*before.PixelHeight/1080d)).ToArray();
            var old=RivenVisualProbe.Analyze(Screen(before),mapped);var next=RivenVisualProbe.Analyze(Screen(after),mapped);
            File.WriteAllText(Path.Combine(directory,"first-white-change-metrics.json"),JsonSerializer.Serialize(new{before.PixelWidth,before.PixelHeight,regions=mapped,difference=RivenWatchState.Difference(old.Signature,next.Signature),aligned=RivenWatchState.AlignedDifference(old.Signature,next.Signature,640),oldInk=old.Signature.Count(p=>p!=0),newInk=next.Signature.Count(p=>p!=0)},Catalog.Json));
            Save(after,Path.Combine(directory,"first-white-changed.png"));
            var watch=new RivenWatchState(640);watch.Prime(old.Signature,0);
            return Enumerable.Range(0,5).Any(i=>watch.Observe(true,next.Signature,3000+i*750,true)==RivenWatchAction.Read);
        }
        Yes(DetectFirstChange(lockedSource,EditFirst(false),[lockedBounds]),"첫 흰색 옵션의 숫자만 변경해도 재인식");
        Yes(DetectFirstChange(lockedSource,EditFirst(true),[lockedBounds]),"첫 흰색 옵션의 이름만 변경해도 재인식");
        var beforePair=new DrawingVisual();using(var drawing=beforePair.RenderOpen()){drawing.DrawImage(lockedSource,new Rect(0,0,lockedSource.PixelWidth,lockedSource.PixelHeight));drawing.DrawImage(lockedSource,new Rect(lockedSource.PixelWidth+32,0,lockedSource.PixelWidth,lockedSource.PixelHeight));}
        var pairImage=new RenderTargetBitmap(lockedSource.PixelWidth*2+32,lockedSource.PixelHeight,96,96,PixelFormats.Pbgra32);pairImage.Render(beforePair);pairImage.Freeze();
        double fixturePairWidth=pairImage.PixelWidth;var pairBounds=new[]{new Rect(lockedBounds.X*lockedSource.PixelWidth/fixturePairWidth,lockedBounds.Y,lockedBounds.Width*lockedSource.PixelWidth/fixturePairWidth,lockedBounds.Height),new Rect((lockedSource.PixelWidth+32+lockedBounds.X*lockedSource.PixelWidth)/fixturePairWidth,lockedBounds.Y,lockedBounds.Width*lockedSource.PixelWidth/fixturePairWidth,lockedBounds.Height)};
        Yes(DetectFirstChange(pairImage,EditFirst(false,true),pairBounds),"두 카드에서 왼쪽 첫 흰색 숫자만 바뀌어도 재인식");
        Yes(!DetectFirstChange(lockedSource,EditFirst(false,outside:true),[lockedBounds]),"옵션 밖의 커서·장식 변화는 재인식하지 않음");
        var incompleteText="+110.070\n+110.1% 상태 이상 피해\n+76.9% 강 공격 효율\n-117.5% 베기";
        var incompleteReport=RivenKindDetection.Resolve(incompleteText,8,"인노뎀 Vexi-plagaus");
        var incompleteResult=new RivenOcrResult([new(incompleteText,incompleteReport,false,new Rect(0,0,1,1))],0,"옵션 구성 확인 필요");
        var incompletePanel=RivenOverlay.BuildSide(incompleteResult,false);render(incompletePanel,"incomplete-card-no-grade",360,560,1);
        var incompleteLabels=Descendants(incompletePanel).OfType<TextBlock>().Select(t=>t.Text).ToArray();
        Yes(incompleteLabels.Contains("읽힌 옵션 3개 · 수율 계산 대기")&&incompleteLabels.Contains("+110.1%")&&incompleteLabels.Contains("+76.9%")&&incompleteLabels.Contains("−117.5%"),"부분 인식 오버레이는 읽힌 세 수치를 보존하고 수율 평가를 보류");
        Yes(!incompleteLabels.Any(t=>System.Text.RegularExpressions.Regex.IsMatch(t,@"품질\s+\d")||t.Contains("범위 밖")),"구성 미확정 수치에 잘못된 등급·연쇄 범위 오류를 붙이지 않음");
        foreach(var scale in new[]{1d,1.5d})
        {
            var source=Fixture(left,right,scale);var file=Path.Combine(directory,$"synthetic-riven-{scale:0.0}.png");Save(source,file);
            var result=await RivenOcr.ReadAsync(source,"Melee",8,default);recognitions.Add(new{file,result});
            File.WriteAllText(Path.Combine(directory,"ocr.json"),JsonSerializer.Serialize(recognitions,Catalog.Json));
            Yes(result.Cards.Length==2,$"한국어 시험 카드 {scale}배: 두 카드 분리");
            Yes(result.Cards[0].Report.Options.SequenceEqual(left.Options)&&result.Cards[1].Report.Options.SequenceEqual(right.Options),$"한국어 시험 카드 {scale}배: 모든 수치·부호·옵션 일치");
            Yes(result.Cards.All(c=>c.Corroborated),$"한국어 시험 카드 {scale}배: 두 전처리 교차 확인");example=result;
            var probe=RivenVisualProbe.Analyze(source);Yes(probe.Regions.Length==2,$"한국어 시험 카드 {scale}배: 좌우 카드 시각 감지");
        }
        var blank=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);var empty=await RivenOcr.ReadAsync(blank,"Melee",8,default);Yes(empty.Cards.Length==0,"리벤 없는 화면에서 수치 생성하지 않음");
        Yes(RivenFooterAnchor.Find(blank).Length==0,"빈 화면은 MR 기준점이 아님");
        Yes(RivenVisualProbe.Analyze(blank).Regions.Length==0,"리벤 없는 화면은 OCR 시작 후보가 아님");
        var reward=Path.Combine(Environment.CurrentDirectory,"artifacts","relic-wrapped-2026-10-25","reward.png");
        if(File.Exists(reward))Yes(RivenVisualProbe.Analyze(RelicOcr.Load(reward)).Regions.Length==0,"실제 성유물 보상 화면에서 리벤 OCR 시작하지 않음");
        OpenRivenClass("Melee");await Idle();
        Click(RivenRecognitionBar.SettingsButton);await Idle();
        Yes(rivenCaptureWindow is {IsVisible:true},"상단 화면 인식 버튼으로 실제 창 열림");
        var window=rivenCaptureWindow!;
        Yes(window.Kind.SelectedIndex==0&&window.Preferences.ReadKind=="Auto","작업실 종류와 무관하게 화면 인식은 자동 판별 기본");
        var detectedRifle=RivenKindDetection.Resolve("+121% 멀티샷\n+208.4% 치명타 확률\n+53.3% 반동",8);
        window.SetResult(new([new(RivenScan.Editable(detectedRifle),detectedRifle,true,new Rect(0,0,1,1))],.5,"시험 카드"));await Idle();
        Yes(Descendants(window).OfType<TextBlock>().Any(t=>t.Text.Contains("소총 · 자동 판별")),"멀티샷·반동 화면은 종류 선택 없이 소총으로 표시");
        render((FrameworkElement)window.Content,"riven-kind-automatic",1096,806,1);
        var ambiguous=RivenKindDetection.Resolve("+106.9% 화염\n+106.9% 냉기",8);
        window.SetResult(new([new("+106.9% 화염\n+106.9% 냉기",ambiguous,false,new Rect(0,0,1,1))],.5,"시험 카드"));await Idle();
        Yes(!Descendants(window).OfType<Button>().Single(b=>Equals(b.Content,"리벤 분석에 가져오기")).IsEnabled,"여러 종류의 공통 수치는 확인 없이 가져오기 금지");
        window.Kind.SelectedIndex=1;await Idle();
        Yes(window.LastResult!.Cards[0].Report.Valid,"애매한 카드만 종류 직접 선택으로 범위 확인");
        window.SetResult(example!);await Idle();
        window.MinWidth=1140;window.MinHeight=890;window.Width=1140;window.Height=890;await Idle();
        render((FrameworkElement)window.Content,"riven-capture",1096,806,1);
        window.MinWidth=800;window.MinHeight=640;window.Width=800;window.Height=640;await Idle();
        render((FrameworkElement)window.Content,"riven-capture-minimum-150",756,556,1.5);
        Yes(window.LastResult!.Cards.All(c=>c.Report.Valid),"분석 창 수율·접합 결과 표시");
        var expander=Descendants(window).OfType<Expander>().First();((IExpandCollapseProvider)new ExpanderAutomationPeer(expander).GetPattern(PatternInterface.ExpandCollapse)).Expand();await Idle();
        var firstCard=window.LastResult.Cards[0];var raw=Descendants(window).OfType<TextBox>().First(t=>t.AcceptsReturn);raw.Text="+9999% 근접 피해\n+106.9% 상태이상 확률";
        Click(Descendants(window).OfType<Button>().First(b=>Equals(b.Content,"수정 적용")));await Idle();
        Yes(!window.LastResult.Cards[0].Report.Valid&&window.LastResult.Cards[0].Report.Recipes.Length==0,"잘못 수정한 수치의 접합 예측 차단");
        raw=Descendants(window).OfType<TextBox>().First(t=>t.AcceptsReturn);raw.Text=firstCard.Text;Click(Descendants(window).OfType<Button>().First(b=>Equals(b.Content,"수정 적용")));await Idle();
        Yes(window.LastResult.Cards[0].Report.Valid&&window.LastResult.Cards[0].Corroborated,"수치 교정 적용 후 분석 회복");
        window.Rank.SelectedItem=0;await Idle();Yes(!window.LastResult.Cards[0].Report.Valid,"랭크 변경 즉시 범위 재검증");window.Rank.SelectedItem=8;await Idle();
        Click(Descendants(window).OfType<Button>().First(b=>Equals(b.Content,"리벤 분석에 가져오기")));await Idle();
        Yes(rivenCaptureWindow is null&&ReadRivenWorkbench().Traits.SequenceEqual(RivenScan.Melee(left).Traits),"교정된 옵션이 기존 근접 작업실로 실제 이동");
        foreach(var kind in new[]{"Rifle","Shotgun","Pistol"})
        {
            var trial=RivenScan.Parse("+1% 피해\n+1% 화염",kind,8);
            var options=trial.Options.Select((o,i)=>o with{Value=Math.Round((trial.Values[i].Range.Minimum+trial.Values[i].Range.Maximum)/2,1)}).ToArray();
            var report=RivenScan.Analyze(options,kind,8);Yes(report.Valid,kind+" 가져오기 시험 수치 유효");
            ShowRivenCapture(kind);await Idle();var dialog=rivenCaptureWindow!;
            dialog.SetResult(new([new(RivenScan.Editable(report),report,true,new Rect(0,0,1,1))],0,"시험 카드"));await Idle();
            Click(Descendants(dialog).OfType<Button>().First(b=>Equals(b.Content,"리벤 분석에 가져오기")));await Idle();
            Yes(rivenCaptureWindow is null&&page.Content is RangedRivenWorkbench workbench&&workbench.RivenClass==kind,kind+" OCR 결과가 해당 총기 리벤 작업실로 이동");
        }
        foreach(var side in new[]{false,true})foreach(var scale in new[]{1d,1.5d})
        {
            var panel=RivenOverlay.BuildSide(example!,side);render(panel,$"side-{side}-{scale}",side?340:720,740,scale);
            var gameBounds=new Int32Rect(0,0,1920,1080);var rect=RivenOverlay.Placement(gameBounds,new DpiScale(scale,scale),side,comparison:true);
            render(RivenOverlay.BuildSide(example!,side),$"side-fhd-{side}-{scale}",(int)(rect.Width/scale),(int)(rect.Height/scale),scale);
            Yes(rect.Y>0&&rect.Y+rect.Height<1080*.85&& (side?rect.X>1920*.7:rect.X+rect.Width<1920*.46),$"좌우 오버레이 {side}/{scale}: 비교 폭 확장·하단 쿠바 영역 비움");
        }
        var comparisonLabels=Descendants(RivenOverlay.BuildSide(example!,false)).OfType<TextBlock>().Select(t=>t.Text).ToArray();
        Yes(comparisonLabels.Contains("왼쪽 옵션")&&comparisonLabels.Contains("오른쪽 옵션")&&comparisonLabels.Contains("상태이상 확률")&&comparisonLabels.Contains("—"),"비교 표: 양쪽 카드·공통 옵션 정렬·없는 옵션 구분");
        var spliceLabels=Descendants(RivenOverlay.BuildSide(example!,true)).OfType<TextBlock>().Select(t=>t.Text).ToArray();
        Yes(spliceLabels.Contains("왼쪽 옵션")&&spliceLabels.Contains("오른쪽 옵션")&&spliceLabels.Count(t=>t.StartsWith("지금 접합 가능"))==2,"오른쪽 접합 창: 두 카드의 접합 결과 각각 표시");
        RivenScanReport Stress(string[] keys)
        {
            var stub=RivenScan.Analyze(keys.Select((key,i)=>new RivenScanOption(key,RivenScan.Label("Melee",key),0,i==3,RivenScan.Unit("Melee",key))).ToArray(),"Melee",8);
            return RivenScan.Analyze(stub.Values.Select(v=>v.Option with{Value=Math.Round((v.Range.Minimum+v.Range.Maximum)/2,1)}).ToArray(),"Melee",8);
        }
        var stressA=Stress(["Damage","StatusChance","Heat","CriticalDamage"]);var stressB=Stress(["AttackSpeed","Range","Toxin","ComboDuration"]);
        Yes(stressA.Valid&&stressB.Valid,"최대 비교 행 시험: 겹치지 않는 유효한 양음옵 여덟 개");
        var stressResult=new RivenOcrResult([new(RivenScan.Editable(stressA),stressA,true,new()),new(RivenScan.Editable(stressB),stressB,true,new())],0,"시험 카드");
        var stressPanel=RivenOverlay.BuildSide(stressResult,false);render(stressPanel,"comparison-eight-stats",720,710,1);
        stressPanel.Measure(new Size(720,710));stressPanel.Arrange(new Rect(0,0,720,710));
        var stressView=Descendants(stressPanel).OfType<Viewbox>().First();
        Yes(stressView.Child.DesiredSize.Height<=710/.9,"여덟 옵션 비교: 보조 정보를 한 행에 배치해 과도한 글자 축소 방지");
        foreach(var screen in new[]{(1920,1080),(3440,1440),(3840,2160)})foreach(var scale in new[]{1d,1.5d})
        {
            var single=RivenOverlay.Placement(new(0,0,screen.Item1,screen.Item2),new(scale,scale),false);
            var pair=RivenOverlay.Placement(new(0,0,screen.Item1,screen.Item2),new(scale,scale),false,true);
            var rightRect=RivenOverlay.Placement(new(0,0,screen.Item1,screen.Item2),new(scale,scale),true,true);
            Yes(pair.Width>=single.Width*1.8&&pair.X+pair.Width<rightRect.X&&pair.Y+pair.Height<screen.Item2*.85,$"{screen}/{scale}: 두 카드 왼쪽 폭 확대·창 간 겹침·하단 선택 영역 검사");
        }
        var overlay=new RivenOverlay(new WindowInteropHelper(this).Handle,example!);overlay.Show();await Idle();
        var persistentHandle=new WindowInteropHelper(overlay).Handle;
        overlay.UpdateResult(example! with{Cards=[example!.Cards[0]]});var singleWidth=RelicCapture.ClientBounds(persistentHandle).Width;
        overlay.UpdateResult(example);var pairWidth=RelicCapture.ClientBounds(persistentHandle).Width;
        Yes(pairWidth>singleWidth*1.7&&new WindowInteropHelper(overlay).Handle==persistentHandle,"한 카드→두 카드: 동일 창에서 비교 폭 확장");
        overlay.UpdateResult(example with{Cards=[example.Cards[0]]});
        Yes(RelicCapture.ClientBounds(persistentHandle).Width==singleWidth&&new WindowInteropHelper(overlay).Handle==persistentHandle,"두 카드→한 카드: 동일 창에서 원래 폭 복귀");
        var styles=RelicOverlay.GetStyle(new WindowInteropHelper(overlay).Handle,-20).ToInt64();Yes((styles&RelicOverlay.PassThroughStyle)==RelicOverlay.PassThroughStyle&&!overlay.ShowActivated&&!overlay.IsHitTestVisible,"오버레이 비활성·클릭 통과 네이티브 스타일");
        OverlayPositionerChecks.Run(overlay);Yes(true,"정지 위치 120회 갱신 생략·이동 시 다른 최상위 창의 순서·포커스 유지");overlay.Close();
        Yes(IsRivenCaptureSignal("12 Script [Info]: RivenEvolution.lua: Missing icon data!")&&!IsRivenCaptureSignal("ProjectionRewardChoice.lua: Got rewards"),"리벤 로그와 성유물 로그 트리거 분리");
        var log=Path.Combine(directory,"riven-test.log");File.WriteAllText(log,"Script [Info]: RivenEvolution.lua: old\n");int events=0;
        using(var detector=new RelicAutoDetector(_=>{},_=>{},log,false,observed:line=>{if(IsRivenCaptureSignal(line))Interlocked.Increment(ref events);}))
        {
            await Task.Delay(700);Yes(events==0,"자동 감지 시작 시 과거 로그 재실행 없음");File.AppendAllText(log,"Script [Info]: RivenEvolution.lua: new\n");
            for(int i=0;i<20&&events==0;i++)await Task.Delay(150);Yes(events==1,"새 리벤 로그에서 자동 감지 신호 발생");
        }
        return new{passed=checks.Count,checks,synthetic=true,actualGameRivenScreenshotVerified=actual is not null,liveGameRerollVerified=false};
    }
    private static BitmapSource Fixture(RivenScanReport left,RivenScanReport right,double scale)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(22,24,31)),null,new Rect(0,0,1920,1080));
            int index=0;foreach(var report in new[]{left,right})
            {
                double x=260+index++*820;dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(44,31,59)),new Pen(Brushes.MediumPurple,2),new Rect(x,120,570,720));
                var lines=RivenScan.Editable(report).Split('\n');double y=360;
                foreach(var line in lines)
                {
                    var text=new FormattedText(line,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),26,new SolidColorBrush(Color.FromRgb(215,181,240)),1);
                    dc.DrawText(text,new Point(x+(570-text.Width)/2,y));y+=58;
                }
            }
        }
        var bitmap=new RenderTargetBitmap((int)(1920*scale),(int)(1080*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
    }
    private static void Save(BitmapSource image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);}
}
