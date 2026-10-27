using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;
using Tesseract;
using Rect=System.Windows.Rect;

namespace TennoForge.Desktop;

internal record RivenOcrCard(string Text,RivenScanReport Report,bool Corroborated,Rect Bounds,string VerificationText="",bool Corrected=false)
{
    public string TitleText {get;init;}="";
}
internal record RivenOcrResult(RivenOcrCard[] Cards,double Seconds,string Notice,string LocatorText="")
{
    public Rect? SourceCrop{get;init;}
    public double InputGain {get;init;}=1;
}
internal static class RivenOcr
{
    internal static Action<BitmapSource,string>? DiagnosticImage;
    private static readonly object DiagnosticSync=new();
    private static void WriteDiagnostic(BitmapSource bitmap,string name)
    {
        if(DiagnosticImage is null)return;
        // A comparison runs two readers. Keep diagnostic file writes serialized
        // and distinguish equal-sized crops from the left and right cards.
        lock(DiagnosticSync)DiagnosticImage?.Invoke(bitmap,$"worker-{Array.IndexOf(Workers,Current)}-{name}");
    }
    private sealed class Worker
    {
        internal readonly SemaphoreSlim Gate=new(1,1);
        internal TesseractEngine? Primary,Latin,Mixed;
    }
    // At most two native OCR contexts: a before/after pair can be read in
    // parallel without sharing a non-thread-safe Tesseract engine.
    private static readonly Worker[] Workers=[new(),new()];
    private static readonly AsyncLocal<Worker?> Context=new();
    private static Worker Current=>Context.Value??Workers[0];
    private static SemaphoreSlim Gate=>Current.Gate;
    private static readonly object PreparationSync=new();
    private static Task? preparation;
    internal static Task PrepareAsync()
    {
        lock(PreparationSync)return preparation??=Task.Run(async()=>
        {
            using var activity=OcrActivity.Enter();
            await Gate.WaitAsync();
            try
            {
                using var scope=RivenPerformance.Measure("model-preparation");_=Engine();
                if(KoreanLineRecognizer.Available)
                {
                    var blank=BitmapSource.Create(320,48,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)255,320*48).ToArray(),320);blank.Freeze();
                    _=KoreanLineRecognizer.Read(blank);
                }
            }
            catch(Exception ex)when(ex is IOException or InvalidOperationException or TesseractException)
            {RivenPerformance.Event("preparation-failed",new{ex.Message});}
            finally{Gate.Release();}
        });
    }
    // Each context serializes its own native use. Weights stay warm across
    // cards and fallbacks; the second context is loaded only for comparison.
    static RivenOcr()
    {
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>
        {
            foreach(var worker in Workers)
            {
                if(!worker.Gate.Wait(0))continue;
                try{worker.Primary?.Dispose();worker.Latin?.Dispose();worker.Mixed?.Dispose();worker.Primary=worker.Latin=worker.Mixed=null;}finally{worker.Gate.Release();}
            }
        };
    }
    private static TesseractEngine Engine(bool latin=false,bool mixed=false)
    {
        var worker=Current;ref var cached=ref (latin?ref worker.Latin:ref (mixed?ref worker.Mixed:ref worker.Primary));
        if(cached is not null)return cached;
        string language=latin?"eng":mixed?OcrModels.Mixed:OcrModels.Korean;
        cached=RivenPerformance.Time("model-init",()=>new TesseractEngine(OcrModels.DirectoryPath,language,EngineMode.LstmOnly),new{language});
        cached.SetVariable("user_defined_dpi","300");cached.SetVariable("preserve_interword_spaces","1");
        return cached;
    }
    private record Line(string Text,Rect Box);
    internal static async Task<RivenOcrResult> ReadAutomaticAsync(BitmapSource source,Rect[] regions,string kind,int rank,CancellationToken token)
    {
        using var activity=OcrActivity.Enter();token.ThrowIfCancellationRequested();
        using var automaticScope=RivenPerformance.Measure("automatic",new{source.PixelWidth,source.PixelHeight,regions=regions.Length});
        var timer=Stopwatch.StartNew();
        var anchored=Environment.GetEnvironmentVariable("WF_LABS_RIVEN_ANCHOR")=="0"?[]:await RivenFooterAnchor.FindAsync(source,token);
        if(anchored.Length==2)return (await ReadSeparatedAsync(source,anchored,kind,rank,token)) with{Seconds=timer.Elapsed.TotalSeconds};
        if(anchored.Length>0)regions=anchored;
        RivenPerformance.Event("anchor-result",new{count=anchored.Length});
        var exactBox=anchored.Length>0?anchored.Aggregate(Rect.Union):RivenVisualProbe.CropBounds(regions);
        // Large cards can render long labels beyond the footer edge. Small
        // cards benefit from keeping animated decoration outside the crop.
        bool preferPadding=anchored.Length>0&&exactBox.Width>.14;
        var box=preferPadding?RivenVisualProbe.CropBounds(anchored):exactBox;
        if(regions.Length==0||box.Width>.55||anchored.Length==0&&source.PixelWidth<=1920)
            return (await ReadAsync(source,kind,rank,token,splitCards:false)) with{SourceCrop=new Rect(0,0,1,1),Seconds=timer.Elapsed.TotalSeconds};
        BitmapSource Crop(Rect area)
        {
            int x=(int)(area.X*source.PixelWidth),y=(int)(area.Y*source.PixelHeight);
            return new CroppedBitmap(source,new Int32Rect(x,y,Math.Max(1,Math.Min(source.PixelWidth-x,(int)Math.Ceiling(area.Width*source.PixelWidth))),Math.Max(1,Math.Min(source.PixelHeight-y,(int)Math.Ceiling(area.Height*source.PixelHeight)))));
        }
        var result=(await ReadAsync(Crop(box),kind,rank,token,focused:true)) with{SourceCrop=box};
        if(anchored.Length>0&&(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid)))
        {
            var alternateBox=preferPadding?exactBox:RivenVisualProbe.CropBounds(anchored);
            var alternate=(await ReadAsync(Crop(alternateBox),kind,rank,token,focused:true)) with{SourceCrop=alternateBox};
            if(alternate.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length)>=result.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length))result=alternate;
        }
        if(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid))
        {
            var full=await ReadAsync(source,kind,rank,token,splitCards:false);
            int Score(RivenOcrResult r)=>r.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length);
            if(Score(full)>=Score(result))result=full with{SourceCrop=new Rect(0,0,1,1)};
        }
        return result with{Seconds=timer.Elapsed.TotalSeconds};
    }
    private static async Task<RivenOcrResult> ReadSeparatedAsync(BitmapSource source,Rect[] anchors,string kind,int rank,CancellationToken token)
    {
        using var scope=RivenPerformance.Measure("separated-cards");var clock=Stopwatch.StartNew();
        var ordered=anchors.OrderBy(a=>a.X).ToArray();var cards=new RivenOcrCard[2];var diagnostics=new string[2];
        double middle=(ordered[0].X+ordered[0].Width/2+ordered[1].X+ordered[1].Width/2)/2;
        int Score(RivenOcrResult result)=>result.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length);
        async Task ReadCard(int index)
        {
            Context.Value=Workers[index];
            token.ThrowIfCancellationRequested();var anchor=ordered[index];bool padding=anchor.Width>.14;
            Rect Area(bool padded)
            {
                var area=padded?RivenVisualProbe.CropBounds([anchor]):anchor;
                area.Intersect(index==0?new Rect(0,0,middle,1):new Rect(middle,0,1-middle,1));return area;
            }
            (BitmapSource Image,Rect Bounds) Crop(Rect area)
            {
                int x=(int)(area.X*source.PixelWidth),y=(int)(area.Y*source.PixelHeight);
                int w=Math.Max(1,Math.Min(source.PixelWidth-x,(int)Math.Ceiling(area.Width*source.PixelWidth))),h=Math.Max(1,Math.Min(source.PixelHeight-y,(int)Math.Ceiling(area.Height*source.PixelHeight)));
                return (new CroppedBitmap(source,new Int32Rect(x,y,w,h)),new Rect(x/(double)source.PixelWidth,y/(double)source.PixelHeight,w/(double)source.PixelWidth,h/(double)source.PixelHeight));
            }
            var crop=Crop(Area(padding));var result=await ReadAsync(crop.Image,kind,rank,token,focused:true);
            if(result.Cards.Length!=1||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid))
            {
                var alternateCrop=Crop(Area(!padding));var alternate=await ReadAsync(alternateCrop.Image,kind,rank,token,focused:true);
                if(Score(alternate)>Score(result)){result=alternate;crop=alternateCrop;}
            }
            if(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid))
            {
                var thorough=await ReadAsync(crop.Image,kind,rank,token,splitCards:false);
                if(Score(thorough)>Score(result))result=thorough;
            }
            var card=result.Cards.OrderByDescending(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length).FirstOrDefault();
            if(card is null)
            {
                var report=kind=="Auto"?RivenKindDetection.Resolve("",rank):RivenScan.Parse("",kind,rank);
                cards[index]=new("",report,false,anchor);
            }
            else cards[index]=card with{Bounds=new Rect(crop.Bounds.X+card.Bounds.X*crop.Bounds.Width,crop.Bounds.Y+card.Bounds.Y*crop.Bounds.Height,card.Bounds.Width*crop.Bounds.Width,card.Bounds.Height*crop.Bounds.Height)};
            diagnostics[index]=$"CARD {index} GAIN {result.InputGain:0.###}:\n"+result.LocatorText;
        }
        if(Environment.GetEnvironmentVariable("WF_LABS_RIVEN_SERIAL")=="1"){await ReadCard(0);await ReadCard(1);}
        else await Task.WhenAll(ReadCard(0),ReadCard(1));
        return new(cards.ToArray(),clock.Elapsed.TotalSeconds,cards.All(c=>c.Corroborated)?"두 카드의 옵션을 확인했습니다.":"일부 카드의 인식 내용을 확인하세요.",string.Join("\n",diagnostics)){SourceCrop=new Rect(0,0,1,1)};
    }
    internal static async Task<RivenOcrResult> ReadAsync(BitmapSource source,string kind,int rank,CancellationToken token,bool focused=false,bool splitCards=true)
    {
        using var activity=OcrActivity.Enter();token.ThrowIfCancellationRequested();
        using var readScope=RivenPerformance.Measure("read",new{source.PixelWidth,source.PixelHeight});
        RelicOcr.Validate(source);
        if(!focused&&splitCards&&source.PixelWidth>source.PixelHeight*1.35)
        {
            var anchors=await RivenFooterAnchor.FindAsync(source,token);
            if(anchors.Length==2)return await ReadSeparatedAsync(source,anchors,kind,rank,token);
        }
        var bgra=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var w=bgra.PixelWidth;var h=bgra.PixelHeight;
        var pixels=new byte[w*h*4];bgra.CopyPixels(pixels,w*4,0);
        await Gate.WaitAsync(token);
        try{return await Task.Run(()=>
        {
            double gain=1;
            if(focused||w<h*1.3){var normalized=RivenTone.Normalize(pixels,w,h);pixels=normalized.Pixels;gain=normalized.Gain;RivenPerformance.Event("card-tone",new{gain});}
            // Locked/spliced traits are white. Include them in the first pass,
            // rather than exhausting purple-only variants before discovering one.
            var elapsed=Stopwatch.StartNew();var result=Read(pixels,w,h,kind,rank,token,includeNeutral:true);
            if(w<320&&(focused||w<h*1.3)&&(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid)))
            {
                // Imported web crops and small focused cards can have too few
                // pixels for the initial line locator to find any row. Keep the
                // original first; only an unresolved compact card gets one
                // bounded interpolation pass. No characters are synthesized.
                using var resizeScope=RivenPerformance.Measure("compact-card-locator",new{w,h});
                double resize=Math.Min(3,384d/w);
                var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);
                var enlargedBitmap=new FormatConvertedBitmap(new TransformedBitmap(bitmap,new ScaleTransform(resize,resize)),PixelFormats.Bgra32,null,0);
                int ew=enlargedBitmap.PixelWidth,eh=enlargedBitmap.PixelHeight;var enlargedPixels=new byte[ew*eh*4];enlargedBitmap.CopyPixels(enlargedPixels,ew*4,0);
                try
                {
                    var compact=Read(enlargedPixels,ew,eh,kind,rank,token,includeNeutral:true);
                    int Score(RivenOcrResult r)=>r.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length);
                    if(Score(compact)>Score(result))result=compact;
                }
                finally{Array.Clear(enlargedPixels);}
            }
            if(!focused&&(result.Cards.Length==0||result.Cards.Any(c=>!c.Corroborated||!c.Report.Valid)))
            {
                // Some imported/tightly cropped images lose a line boundary in
                // the Korean segmenter. One cached mixed-language pass remains
                // a fallback; it never replaces a corroborated Korean result.
                var alternate=Read(pixels,w,h,kind,rank,token,mixed:true);
                int Score(RivenOcrResult r)=>r.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length);
                if(Score(alternate)>Score(result))result=alternate;
            }
            foreach(bool enlarged in focused?Array.Empty<bool>():new[]{true,false})
            {
                if(result.Cards.Length>0&&result.Cards.All(c=>c.Corroborated&&c.Report.Valid))break;
                var locked=Read(pixels,w,h,kind,rank,token,true,enlarged);
                int Score(RivenOcrResult r)=>r.Cards.Sum(c=>(c.Corroborated?100:0)+(c.Report.Valid?10:0)+c.Report.Options.Length);
                if(Score(locked)>=Score(result))result=locked;
            }
            return result with{Seconds=elapsed.Elapsed.TotalSeconds,InputGain=gain};
        },token);}finally{Gate.Release();}
    }
    private static RivenOcrResult Read(byte[] pixels,int width,int height,string kind,int rank,CancellationToken token,bool includeNeutral=false,bool enlarged=false,bool mixed=false)
    {
        using var variantScope=RivenPerformance.Measure("variant",new{includeNeutral,enlarged,width,height});
        var clock=Stopwatch.StartNew();var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();
        var scale=Math.Min(1,Math.Min(2560d/width,1080d/height));
        BitmapSource input=scale<1?new TransformedBitmap(image,new ScaleTransform(scale,scale)):image;
        var engine=Engine(mixed:mixed);
        var initial=ReadLines(Prepare(input,false,true,includeNeutral),PageSegMode.SparseText);
        if(initial.Count(l=>Regex.IsMatch(l.Text,@"\d"))<2)initial=ReadLines(Prepare(input,true,true,includeNeutral),PageSegMode.SparseText);
        var labelTokens=(kind=="Auto"?RivenKindDetection.Kinds.SelectMany(RivenScan.AcceptedLabels):RivenScan.AcceptedLabels(kind)).SelectMany(label=>Regex.Matches(label,@"[가-힣]{2,}").Select(m=>m.Value)).Append("확률").Append("슬라이드").Distinct().ToArray();
        bool LabelHint(string text)=>labelTokens.Any(t=>text.Replace(" ","").Contains(t));
        // Purple animation and card cost can also resemble signed numbers. A locator
        // must have a stat label on the same/adjacent text line before it defines a crop.
        var signed=initial.Where(l=>Regex.IsMatch(l.Text,@"\d")&&(LabelHint(l.Text)||
            Regex.IsMatch(l.Text,@"(?:[+−–－-]\s*\d+(?:[.,]\s*\d+)?\s*(?:%|ｍ|m|초)(?![A-Za-z])|[xX×]\s*\d+[.,]\d+)")&&initial.Any(n=>!ReferenceEquals(n,l)&&LabelHint(n.Text)&&Math.Abs(n.Box.Y-l.Box.Y)<Math.Max(l.Box.Height,n.Box.Height)*2&&Rect.Intersect(new Rect(l.Box.X,0,l.Box.Width,1),new Rect(n.Box.X,0,n.Box.Width,1)).Width>0))).ToArray();
        var groups=new List<List<Line>>();
        foreach(var line in signed.OrderBy(l=>l.Box.X+l.Box.Width/2))
        {
            double center=line.Box.X+line.Box.Width/2;
            var group=groups.LastOrDefault();
            if(group is null||Math.Abs(group.Average(l=>l.Box.X+l.Box.Width/2)-center)>input.PixelWidth*.16){group=[];groups.Add(group);}
            group.Add(line);
        }
        var cards=new List<RivenOcrCard>();var diagnostics=new List<string>();List<Line>? purpleLocator=null;
        foreach(var group in groups.Where(g=>g.Count>=2&&!g.Any(l=>l.Text.Replace(" ","").Contains("품질")||l.Text.Replace(" ","").Contains("WFLabs",StringComparison.OrdinalIgnoreCase))).Take(2))
        {
            token.ThrowIfCancellationRequested();
            var groupBox=group.Select(l=>l.Box).Aggregate(Rect.Union);
            var title=string.Join("\n",initial.Where(l=>l.Box.Top<=groupBox.Top&&l.Box.Bottom>groupBox.Top-groupBox.Height&&Math.Abs(l.Box.X+l.Box.Width/2-groupBox.X-groupBox.Width/2)<Math.Max(groupBox.Width,input.PixelWidth*.08)).Select(l=>l.Text));
            if(kind=="Auto"&&RivenKindDetection.WeaponKind(title) is null&&KoreanLineRecognizer.Available)
            {
                // Read each title line independently, so a wrapped Latin suffix
                // cannot corrupt the Korean weapon prefix. No full-screen DL pass.
                var titleLines=initial.Where(l=>l.Box.Top<groupBox.Top&&l.Box.Bottom>groupBox.Top-Math.Max(groupBox.Height,group.Average(g=>g.Box.Height)*5)&&
                    Math.Abs(l.Box.X+l.Box.Width/2-groupBox.X-groupBox.Width/2)<Math.Max(groupBox.Width,input.PixelWidth*.08)&&!Regex.IsMatch(l.Text,@"^\s*[+−–－]?\d")).OrderByDescending(l=>l.Box.Y).Take(3);
                foreach(var line in titleLines)
                {
                    var area=line.Box;area.Inflate(4,3);area.Intersect(new Rect(0,0,input.PixelWidth,input.PixelHeight));
                    var slice=new CroppedBitmap(input,new Int32Rect((int)area.X,(int)area.Y,(int)area.Width,(int)area.Height));
                    var recognized=KoreanLineRecognizer.Read(Prepare(slice,false,true,includeNeutral));
                    diagnostics.Add("TITLE LINE: "+recognized.Text);
                    if(recognized.Confidence>=.75&&RivenKindDetection.WeaponKind(recognized.Text) is not null){title=recognized.Text;break;}
                }
            }
            RivenScanReport Parse(string value,string? forcedKind=null)=>forcedKind is not null?RivenScan.Parse(RivenScan.NormalizeOcrLabels(value,forcedKind),forcedKind,rank):kind=="Auto"?RivenKindDetection.Resolve(value,rank,title):RivenScan.Parse(RivenScan.NormalizeOcrLabels(value,kind),kind,rank);
            string NormalizeText(string value)=>kind=="Auto"?value:RivenScan.NormalizeOcrLabels(value,kind);
            var text=NormalizeText(string.Join("\n",group.OrderBy(l=>l.Box.Y).Select(l=>l.Text)));var report=Parse(text);
            var initialText=text;var initialReport=report;
            var box=group.Select(l=>l.Box).Aggregate(Rect.Union);
            string? alternateLocatorText=null;RivenScanReport? alternateLocatorReport=null;
            if(includeNeutral&&(!report.Valid||report.Options.Any(o=>!DisplayedPrecision(o))))
            {
                // White frame/art can obscure the first wrapped stat in the
                // coarse locator. Use a purple-only observation to recover its
                // real text bounds, not a guessed number or a larger photo ROI.
                purpleLocator??=ReadLines(Prepare(input,false,true,false),PageSegMode.SparseText);
                double lineHeight=group.Average(l=>l.Box.Height);
                var nearby=purpleLocator.Where(l=>l.Box.Top>=groupBox.Top-lineHeight*3&&l.Box.Top<=groupBox.Bottom+lineHeight*2&&
                    Math.Abs(l.Box.X+l.Box.Width/2-groupBox.X-groupBox.Width/2)<Math.Max(groupBox.Width*.65,lineHeight*2)&&
                    (LabelHint(l.Text)||Regex.IsMatch(l.Text,@"[+−–－-]\s*\d+(?:[.,]\d+)?\s*%|[xX×]\s*\d+[.,]\d+"))).OrderBy(l=>l.Box.Y).ToArray();
                if(nearby.Length>0)
                {
                    foreach(var line in nearby)box=Rect.Union(box,line.Box);
                    alternateLocatorText=NormalizeText(string.Join("\n",nearby.Select(l=>l.Text)));alternateLocatorReport=Parse(alternateLocatorText);
                    if(alternateLocatorReport.Valid){text=alternateLocatorText;report=alternateLocatorReport;}
                }
            }
            // A faction's multiplier can be wrapped below its label and missed
            // by the sparse locator. Keep that label and the following value row.
            if(includeNeutral)foreach(var line in initial.Where(l=>LabelHint(l.Text)&&l.Box.Top>=box.Top&&l.Box.Top<=box.Bottom+Math.Max(20,l.Box.Height*2)&&Math.Abs(l.Box.X+l.Box.Width/2-box.X-box.Width/2)<box.Width*.7).OrderBy(l=>l.Box.Y))
            {box=Rect.Union(box,line.Box);if(new[]{"그리니어","코퍼스","인페스티드","오로킨","테크병","스칼드라"}.Any(line.Text.Contains)&&!Regex.IsMatch(line.Text,@"\d"))box=Rect.Union(box,new Rect(line.Box.X,line.Box.Y,line.Box.Width,line.Box.Height*2.6));}
            // A locator can see only the lower portion of the first white
            // line. Keep one glyph height above it instead of clipping the
            // next line and retrying the entire card at several scales.
            var trackingBox=box;trackingBox.Inflate(18,14);trackingBox.Intersect(new Rect(0,0,input.PixelWidth,input.PixelHeight));
            double topPadding=Math.Max(14,group.Max(l=>l.Box.Height)*1.5);
            box=new Rect(box.X-18,box.Y-topPadding,box.Width+36,box.Height+topPadding+14);
            box.Intersect(new Rect(0,0,input.PixelWidth,input.PixelHeight));
            var cropArea=new Int32Rect((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height);
            BitmapSource crop=new CroppedBitmap(input,cropArea);
            // Native and enlarged crops are separate, bounded fallback passes.
            // They cover different UI scales without discarding the older path.
            double factor=enlarged?Math.Clamp(42d/group.Average(l=>l.Box.Height),1,2):1;
            if(factor>1.05)crop=new TransformedBitmap(crop,new ScaleTransform(factor,factor));
            // OCR needs generous padding to recover clipped white glyphs. The
            // change probe instead follows only the actual, confirmed option
            // rows; animated artwork must never become part of its signature.
            var geometry=new List<Line[]>();
            bool InsideCrop(Line line)=>box.Contains(new Point(line.Box.X+line.Box.Width/2,line.Box.Y+line.Box.Height/2));
            geometry.Add(initial.Where(InsideCrop).ToArray());
            if(purpleLocator is not null)geometry.Add(purpleLocator.Where(InsideCrop).ToArray());
            Line[] InputLines(IEnumerable<Line> lines)=>lines.Select(line=>line with{Box=new Rect(
                cropArea.X+line.Box.X*cropArea.Width/crop.PixelWidth,cropArea.Y+line.Box.Y*cropArea.Height/crop.PixelHeight,
                line.Box.Width*cropArea.Width/crop.PixelWidth,line.Box.Height*cropArea.Height/crop.PixelHeight)}).ToArray();
            WriteDiagnostic(crop,$"card-crop-{width}-{height}-{includeNeutral}-{cards.Count}");
            var second=ReadLines(Prepare(crop,true,true,includeNeutral),PageSegMode.SingleBlock);
            geometry.Add(InputLines(second));
            var secondText=NormalizeText(string.Join("\n",second.Select(l=>l.Text)));var secondReport=Parse(secondText);
            bool confirmed=report.Valid&&secondReport.Valid&&Same(report,secondReport);
            var evidence=new List<(string Text,RivenScanReport Report)>{(initialText,initialReport),(secondText,secondReport)};
            if(alternateLocatorReport is not null)evidence.Add((alternateLocatorText!,alternateLocatorReport));
            bool ConfirmEvidence()
            {
                foreach(var candidate in evidence.Where(e=>e.Report.Valid).OrderByDescending(e=>e.Report.Options.Length))
                    if(CorroboratedBy(candidate.Report,evidence.Select(e=>kind=="Auto"?Parse(e.Text,candidate.Report.Kind):e.Report))){text=candidate.Text;report=candidate.Report;return true;}
                return false;
            }
            // Keep a usable refinement for correction, but never silently call one pass verified.
            if(secondReport.Valid&&!report.Valid||secondReport.Options.Length>report.Options.Length){text=secondText;report=secondReport;}
            if(!confirmed)confirmed=ConfirmEvidence();
            if(!confirmed)
            {
                var third=ReadLines(Prepare(crop,false,true,includeNeutral),PageSegMode.SingleBlock);
                geometry.Add(InputLines(third));
                var thirdText=NormalizeText(string.Join("\n",third.Select(l=>l.Text)));
                var thirdReport=Parse(thirdText);
                evidence.Add((thirdText,thirdReport));
                confirmed=secondReport.Valid&&thirdReport.Valid&&Same(secondReport,thirdReport);
                if(confirmed){text=secondText;report=secondReport;}
                else if(thirdReport.Options.Length>report.Options.Length){text=thirdText;report=thirdReport;}
                if(!confirmed)confirmed=ConfirmEvidence();
                if(!confirmed)
                {
                    foreach(bool binary in new[]{false,true})
                    {
                        var rows=ReadRows(crop,binary);
                        geometry.Add(InputLines(rows.KoreanLines));geometry.Add(InputLines(rows.LearnedLines));
                        foreach(var rawRows in new[]{rows.Korean,rows.Learned}.Where(s=>s.Length>0))
                        {
                            diagnostics.Add($"ROW {binary}:\n"+rawRows);var rowText=NormalizeText(rawRows);var rowReport=Parse(rowText);
                            evidence.Add((rowText,rowReport));
                            if(rowReport.Options.Length>report.Options.Length||rowReport.Valid&&!report.Valid){text=rowText;report=rowReport;}
                            if(ConfirmEvidence()){confirmed=true;break;}
                        }
                        if(confirmed)break;
                    }
                }
            }
            if(report.Options.Length==0)continue;
            if(confirmed&&TryOptionBounds(geometry,report,out var optionBounds))
            {
                optionBounds.Inflate(2,2);optionBounds.Intersect(new Rect(0,0,input.PixelWidth,input.PixelHeight));
                trackingBox=optionBounds;
            }
            cards.Add(new(text,report,confirmed,new Rect(trackingBox.X/input.PixelWidth,trackingBox.Y/input.PixelHeight,trackingBox.Width/input.PixelWidth,trackingBox.Height/input.PixelHeight),secondText){TitleText=title});
        }
        return new(cards.ToArray(),clock.Elapsed.TotalSeconds,cards.Count==0?"리벤 옵션을 찾지 못했습니다. 카드가 크게 보이도록 연 뒤 다시 읽거나 스크린샷을 불러오세요.":"무기 종류 자동 판별 · 랭크는 설정값 기준입니다. 범위 위치는 획득 확률이 아닙니다.",string.Join("\n",initial.Select(l=>l.Text).Concat(diagnostics)));

        List<Line> ReadLines(BitmapSource prepared,PageSegMode mode,TesseractEngine? processor=null)
        {
            using var lineScope=RivenPerformance.Measure("tesseract",new{mode=mode.ToString(),prepared.PixelWidth,prepared.PixelHeight,language=processor is null?(mixed?"mixed":"korean"):ReferenceEquals(processor,Current.Primary)?"korean":"digits"});
            token.ThrowIfCancellationRequested();using var pix=OcrPixels.Create(prepared);using var page=(processor??engine).Process(pix,mode);using var it=page.GetIterator();it.Begin();var lines=new List<Line>();
            do
            {
                var text=it.GetText(PageIteratorLevel.TextLine)?.Trim();
                if(!string.IsNullOrWhiteSpace(text)&&it.TryGetBoundingBox(PageIteratorLevel.TextLine,out var box))lines.Add(new(text,new Rect(box.X1,box.Y1,box.Width,box.Height)));
            }while(it.Next(PageIteratorLevel.TextLine));
            return lines;
        }
        (string Korean,string Learned,Line[] KoreanLines,Line[] LearnedLines) ReadRows(BitmapSource source,bool binary)
        {
            using var rowScope=RivenPerformance.Measure("rows",new{binary});
            var mask=RemoveEdgeFrame(Prepare(source,true,true,includeNeutral));
            var clean=RemoveEdgeFrame(Prepare(source,binary,true,includeNeutral));
            int w=mask.PixelWidth,h=mask.PixelHeight;var bits=new byte[w*h];mask.CopyPixels(bits,w,0);
            var rows=new List<(int Top,int Bottom)>();int top=-1,last=-1;
            for(int y=0;y<h;y++)
            {
                // Thin animated frame edges must not join the last multiplier
                // row to the MR/reroll footer beneath it.
                // Detect line gaps in the middle of the text block. The full
                // width is still OCR'd, including leading signs and long labels.
                int count=0;for(int x=(int)(w*.2);x<(int)(w*.8);x++)if(bits[y*w+x]<128)count++;
                if(count>=4){if(top<0)top=y;last=y;}
                else if(top>=0&&y-last>3){if(last-top>=5)rows.Add((Math.Max(0,top-3),Math.Min(h,last+4)));top=-1;}
            }
            if(top>=0&&last-top>=5)rows.Add((Math.Max(0,top-3),Math.Min(h,last+4)));
            var texts=new List<string>();var learnedTexts=new List<string>();var koreanLines=new List<Line>();var learnedLines=new List<Line>();foreach(var row in rows.Take(16))
            {
                // Do not shave a fixed percentage off the row: long options
                // start near the crop edge and can lose their sign/first digit.
                int inset=0;
                int left=inset,right=w-inset;var groups=new List<(int Left,int Right)>();int first=-1,lastInk=-1;
                int gap=Math.Max(14,(int)((row.Bottom-row.Top)*.8));
                for(int x=left;x<right;x++)
                {
                    bool ink=false;for(int y=row.Top;y<row.Bottom;y++)if(bits[y*w+x]<128){ink=true;break;}
                    if(!ink)continue;
                    if(first>=0&&x-lastInk>gap){groups.Add((first,lastInk+1));first=-1;}
                    if(first<0)first=x;lastInk=x;
                }
                if(first>=0)groups.Add((first,lastInk+1));
                if(groups.Count>1)
                {
                    var firstGroup=groups[0];var lastGroup=groups[^1];int thin=(int)((row.Bottom-row.Top)*.8);
                    if(firstGroup.Left<w*.05&&firstGroup.Right-firstGroup.Left<thin&&groups[1].Left-firstGroup.Right>gap)left=Math.Max(left,groups[1].Left-3);
                    if(lastGroup.Right>w*.95&&lastGroup.Right-lastGroup.Left<thin&&lastGroup.Left-groups[^2].Right>gap)right=Math.Min(right,groups[^2].Right+3);
                }
                // Keep the ink bounds, not the full-width OCR strip. The latter
                // can include a moving frame despite recognizing correct text.
                int glyphLeft=right,glyphRight=left,glyphTop=row.Bottom,glyphBottom=row.Top;
                for(int y=row.Top;y<row.Bottom;y++)for(int x=left;x<right;x++)if(bits[y*w+x]<128)
                {glyphLeft=Math.Min(glyphLeft,x);glyphRight=Math.Max(glyphRight,x+1);glyphTop=Math.Min(glyphTop,y);glyphBottom=Math.Max(glyphBottom,y+1);}
                var inkBounds=glyphRight>glyphLeft&&glyphBottom>glyphTop?new Rect(glyphLeft,glyphTop,glyphRight-glyphLeft,glyphBottom-glyphTop):Rect.Empty;
                var koBounds=inkBounds;var learnedBounds=inkBounds;
                var slice=new CroppedBitmap(clean,new Int32Rect(left,row.Top,right-left,row.Bottom-row.Top));
                (string Text,double Confidence) learned=("",0);
                if(KoreanLineRecognizer.Available)
                {
                    learned=KoreanLineRecognizer.Read(slice);diagnostics.Add($"LEARNED {binary}: {learned.Text} ({learned.Confidence:0.000})");
                }
                double factor=Math.Clamp(42d/(row.Bottom-row.Top),1,2);
                var prepared=new FormatConvertedBitmap(new TransformedBitmap(slice,new ScaleTransform(factor,factor)),PixelFormats.Gray8,null,0);
                int rw=prepared.PixelWidth,rh=prepared.PixelHeight,pad=16;var raw=new byte[rw*rh];prepared.CopyPixels(raw,rw,0);
                var padded=Enumerable.Repeat((byte)255,(rw+2*pad)*(rh+2*pad)).ToArray();
                for(int y=0;y<rh;y++)Buffer.BlockCopy(raw,y*rw,padded,(y+pad)*(rw+2*pad)+pad,rw);
                var bitmap=BitmapSource.Create(rw+2*pad,rh+2*pad,300,300,PixelFormats.Gray8,null,padded,rw+2*pad);
                WriteDiagnostic(bitmap,$"row-{width}-{height}-{includeNeutral}-{binary}-{row.Top}");
                var ko=string.Join(" ",ReadLines(bitmap,PageSegMode.SingleLine,Engine()).Select(l=>l.Text));
                bool Parsed(string value)=>(kind=="Auto"?RivenKindDetection.Kinds:new[]{kind}).Any(k=>RivenScan.Parse(RivenScan.NormalizeOcrLabels(value,k),k,rank).Options is [var option]&&DisplayedPrecision(option));
                string english="";
                if(LabelHint(ko)&&!Parsed(ko))
                {
                    english=string.Join(" ",ReadLines(bitmap,PageSegMode.SingleLine,Engine(true)).Select(l=>l.Text));
                    var latinNumbers=Regex.Matches(english,@"[+−–－-]\s*\d+(?:[.,]\d)?\s*%");
                    var label=Regex.Replace(ko,@"[^가-힣\s]","");
                    if(latinNumbers.Count==1){var candidate=latinNumbers[0].Value+" "+label;if(Parsed(candidate))ko=candidate;}
                }
                // A wrapped purple title can overlap the vertical extent of a
                // white locked trait. Projection then produces two text lines,
                // which a single-line reader cannot decode. Isolate neutral
                // glyphs only for an unresolved row, trim their actual ink, and
                // require independent engines to agree on its complete value.
                if(!Parsed(ko)&&KoreanLineRecognizer.Available&&NeutralLine(new CroppedBitmap(source,new Int32Rect(left,row.Top,right-left,row.Bottom-row.Top)),binary,out var neutralBounds) is {} neutral)
                {
                    var neutralKo=string.Join(" ",ReadLines(neutral,PageSegMode.SingleLine,Engine()).Select(l=>l.Text));
                    var neutralLearned=KoreanLineRecognizer.Read(neutral);
                    bool Agree(string a,string b)=>(kind=="Auto"?RivenKindDetection.Kinds:new[]{kind}).Any(k=>
                    {
                        var first=RivenScan.Parse(RivenScan.NormalizeOcrLabels(a,k),k,rank).Options;
                        var second=RivenScan.Parse(RivenScan.NormalizeOcrLabels(b,k),k,rank).Options;
                        return first is [var x]&&second is [var y]&&x==y&&DisplayedPrecision(x);
                    });
                    diagnostics.Add($"NEUTRAL {binary}: KO={neutralKo} / LEARNED={neutralLearned.Text} ({neutralLearned.Confidence:0.000})");
                    WriteDiagnostic(neutral,$"neutral-row-{width}-{height}-{binary}-{row.Top}");
                    static string[] Numbers(string value)=>Regex.Matches(value,@"[+−–－-]\s*\d+(?:[.,]\d)?\s*(?:%|ｍ|m(?![A-Za-z])|초(?![가-힣]))?").Select(m=>Regex.Replace(m.Value,@"\s","").Replace(',','.').Replace('−','-').Replace('–','-').Replace('－','-')).ToArray();
                    var readNumbers=Numbers(neutralKo);var learnedNumbers=Numbers(neutralLearned.Text);
                    bool numericAgreement=readNumbers.Length==1&&readNumbers.SequenceEqual(learnedNumbers);
                    if(!numericAgreement&&neutralLearned.Confidence>=.75&&Parsed(neutralLearned.Text))
                    {
                        var neutralLatin=string.Join(" ",ReadLines(neutral,PageSegMode.SingleLine,Engine(true)).Select(l=>l.Text));
                        diagnostics.Add($"NEUTRAL NUMBERS {binary}: "+neutralLatin);
                        var latinRead=Numbers(neutralLatin);numericAgreement=latinRead.Length==1&&latinRead.SequenceEqual(learnedNumbers);
                    }
                    if(neutralLearned.Confidence>=.75&&Parsed(neutralLearned.Text)&&numericAgreement)
                    {
                        // Keep the two readings independent. A different Korean
                        // label in Tesseract is never silently replaced; only its
                        // separately read number supports the learned reading.
                        // The complete card still needs the second image pass.
                        learned=neutralLearned;
                        learnedBounds=new Rect(left+neutralBounds.X,row.Top+neutralBounds.Y,neutralBounds.Width,neutralBounds.Height);
                        if(Agree(neutralKo,neutralLearned.Text)){ko=neutralKo;koBounds=learnedBounds;}
                    }
                }
                bool latinFactor=false;
                if(!Regex.IsMatch(learned.Text,@"[가-힣]")&&Regex.IsMatch(ko+learned.Text,@"[xX×]")&&!Regex.IsMatch(learned.Text,@"^[xX×]\s*\d+(?:[.,]\d{1,2})?\s*$"))
                {
                    if(english.Length==0)english=string.Join(" ",ReadLines(bitmap,PageSegMode.SingleLine,Engine(true)).Select(l=>l.Text));
                    if(Regex.IsMatch(english.Trim(),@"^[xX×]\s*\d+(?:[.,]\d{1,2})?\s*$")){learned=(english.Trim(),0);latinFactor=true;diagnostics.Add("LATIN FACTOR: "+english);}
                }
                if(learned.Confidence>=.75||latinFactor){learnedTexts.Add(learned.Text);if(!learnedBounds.IsEmpty)learnedLines.Add(new(learned.Text,learnedBounds));}
                diagnostics.Add($"LINE {binary}: KO={ko} / ENG={english}");
                if(LabelHint(ko)||Regex.IsMatch(ko,@"(?:[+−–－-]\s*\d+(?:[.,]\d+)?\s*(?:%|ｍ|m|초)|[xX×]\s*\d+[.,]\d+)")){texts.Add(ko);if(!koBounds.IsEmpty)koreanLines.Add(new(ko,koBounds));}
            }
            return (string.Join("\n",texts),string.Join("\n",learnedTexts),koreanLines.ToArray(),learnedLines.ToArray());
        }
    }
    private static bool TryOptionBounds(IEnumerable<Line[]> readings,RivenScanReport report,out Rect bounds)
    {
        using var trackingScope=RivenPerformance.Measure("tracking-geometry");
        bounds=Rect.Empty;
        var matched=new Rect[report.Options.Length];Array.Fill(matched,Rect.Empty);
        var parsed=new Dictionary<string,RivenScanOption?>(StringComparer.Ordinal);
        foreach(var reading in readings)
        {
            var lines=reading.Where(l=>!l.Box.IsEmpty).OrderBy(l=>l.Box.Y).ToArray();
            for(int i=0;i<lines.Length;i++)for(int count=1;count<=3&&i+count<=lines.Length;count++)
            {
                var segment=lines.AsSpan(i,count);var area=segment[0].Box;bool adjacent=true;
                for(int j=1;j<segment.Length;j++)
                {
                    var before=segment[j-1].Box;var next=segment[j].Box;
                    if(next.Top-before.Bottom>Math.Max(before.Height,next.Height)*2||Math.Min(before.Right,next.Right)<=Math.Max(before.Left,next.Left)){adjacent=false;break;}
                    area=Rect.Union(area,next);
                }
                if(!adjacent)break;
                var raw=string.Join("\n",segment.ToArray().Select(l=>l.Text));
                if(!parsed.TryGetValue(raw,out var option))
                {
                    var normalized=RivenScan.NormalizeOcrLabels(raw,report.Kind);
                    // A title plus a valid option is two logical lines, not one
                    // option's geometry. Wrapped labels/faction multipliers may
                    // use up to three physical rows, as in the input parser.
                    option=RivenScan.LogicalLines(normalized,report.Kind).Length==1&&RivenScan.Parse(normalized,report.Kind,report.Rank).Options is [var single]?single:null;
                    parsed.Add(raw,option);
                }
                if(option is null)continue;
                int index=Array.FindIndex(report.Options,o=>o.Key==option.Key&&o.Value==option.Value&&o.Negative==option.Negative&&o.Unit==option.Unit);
                // A sparse iterator can attach the title or frame to an option
                // line. Prefer the smallest complete matching observation;
                // unioning every reader's rectangle reintroduces that noise.
                if(index>=0&&(matched[index].IsEmpty||area.Width*area.Height<matched[index].Width*matched[index].Height))matched[index]=area;
            }
        }
        if(matched.Length==0||matched.Any(b=>b.IsEmpty))return false;
        bounds=matched.Aggregate(Rect.Union);return true;
    }
    internal static bool Same(RivenScanReport a,RivenScanReport b)=>a.Kind==b.Kind&&a.Rank==b.Rank&&a.Options.SequenceEqual(b.Options)&&a.Issues.Length==0&&b.Issues.Length==0&&a.Options.All(o=>Math.Abs(o.Value-Math.Round(o.Value,1))<1e-8);
    internal static bool CorroboratedBy(RivenScanReport candidate,IEnumerable<RivenScanReport> readings)
    {
        var evidence=readings.Distinct<RivenScanReport>(ReferenceEqualityComparer.Instance).ToArray();
        if(!candidate.Valid||evidence.Any(r=>r.Kind!=candidate.Kind||r.Rank!=candidate.Rank))return false;
        var keys=candidate.Options.Select(o=>o.Key).ToHashSet();
        // A card is not complete if any independent reading still contains a
        // different, precisely displayed trait. Do not silently accept a valid
        // subset merely because its numeric ranges happen to overlap.
        if(evidence.SelectMany(r=>r.Options).Any(o=>DisplayedPrecision(o)&&!keys.Contains(o.Key)))return false;
        return candidate.Options.All(o=>DisplayedPrecision(o)&&evidence.Count(e=>e.Options.Contains(o))>=2&&
            !evidence.SelectMany(e=>e.Options.Where(x=>x.Key==o.Key&&DisplayedPrecision(x))).Distinct().Any(other=>other!=o&&evidence.Count(e=>e.Options.Contains(other))>=2));
    }
    private static bool DisplayedPrecision(RivenScanOption option)=>Math.Abs(option.Value-Math.Round(option.Value,1))<1e-8;
    private static BitmapSource? NeutralLine(BitmapSource source,bool binary,out Rect inkBounds)
    {
        inkBounds=Rect.Empty;
        var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        int w=converted.PixelWidth,h=converted.PixelHeight;var pixels=new byte[w*h*4];converted.CopyPixels(pixels,w*4,0);
        var gray=Enumerable.Repeat((byte)255,w*h).ToArray();int left=w,right=-1,top=h,bottom=-1,count=0;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int p=(y*w+x)*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2],value=Math.Max(b,Math.Max(g,r));
            if(value<=135||value-Math.Min(b,Math.Min(g,r))>=35)continue;
            gray[y*w+x]=binary?(byte)0:(byte)(255-value);count++;
            left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
        }
        if(count<20||bottom-top<5||right-left<12)return null;
        inkBounds=new Rect(left,top,right-left+1,bottom-top+1);
        var image=BitmapSource.Create(w,h,300,300,PixelFormats.Gray8,null,gray,w);
        int x0=Math.Max(0,left-3),y0=Math.Max(0,top-3);
        var crop=new CroppedBitmap(image,new Int32Rect(x0,y0,Math.Min(w,right+4)-x0,Math.Min(h,bottom+4)-y0));
        return crop;
    }
    private static BitmapSource RemoveEdgeFrame(BitmapSource source)
    {
        int w=source.PixelWidth,h=source.PixelHeight;var pixels=new byte[w*h];source.CopyPixels(pixels,w,0);
        var seen=new bool[pixels.Length];var component=new List<int>();
        for(int start=0;start<pixels.Length;start++)
        {
            if(seen[start]||pixels[start]>=200)continue;
            component.Clear();component.Add(start);seen[start]=true;int left=w,right=0,top=h,bottom=0;
            for(int n=0;n<component.Count;n++)
            {
                int p=component[n],x=p%w,y=p/w;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int q=yy*w+xx;if(!seen[q]&&pixels[q]<200){seen[q]=true;component.Add(q);}}
            }
            // Only edge-attached decoration spanning several text rows or most
            // of the card width. Keep interior glyphs, signs and the lock icon.
            if((left<=1||right>=w-2)&&(bottom-top>Math.Max(18,h*.25)||right-left>w*.65))
                foreach(int p in component)pixels[p]=255;
        }
        var result=BitmapSource.Create(w,h,300,300,PixelFormats.Gray8,null,pixels,w);result.Freeze();return result;
    }
    private static BitmapSource Prepare(BitmapSource source,bool threshold,bool purple=false,bool includeNeutral=false)
    {
        using var prepareScope=RivenPerformance.Measure("prepare",new{source.PixelWidth,source.PixelHeight,threshold,purple,includeNeutral});
        var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=converted.PixelWidth,h=converted.PixelHeight;
        var pixels=new byte[w*h*4];converted.CopyPixels(pixels,w*4,0);var gray=new byte[w*h];
        for(int i=0;i<gray.Length;i++)
        {
            int p=i*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];int value=Math.Max(b,Math.Max(g,r));
            if(purple){bool neutral=includeNeutral&&value>135&&value-Math.Min(b,Math.Min(g,r))<35;bool ink=r>75&&r-g>12&&b-r>8||neutral;gray[i]=!ink?(byte)255:threshold?(byte)0:(byte)(255-Math.Min(255,neutral?value:r+2*(r-g)));}
            else gray[i]=threshold?(byte)(value>=135?0:255):(byte)(255-value);
        }
        var result=BitmapSource.Create(w,h,300,300,PixelFormats.Gray8,null,gray,w);result.Freeze();return result;
    }
}
