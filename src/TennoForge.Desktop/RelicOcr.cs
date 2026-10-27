using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;
using Tesseract;

namespace TennoForge.Desktop;

internal sealed record RelicTiming(double TotalMilliseconds,double OcrMilliseconds,double MatchingMilliseconds,int Reads);
internal sealed record RelicRecognition(RecognizedReward[] Rewards,RewardText[] Lines,string Notice,RelicTiming? Timing=null)
{
    public string Model{get;init;}="kor";
}
internal static class RelicOcr
{
    private static readonly SemaphoreSlim gate=new(1,1);
    internal static BitmapSource Load(string path)
    {
        using var stream=File.OpenRead(path);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad);
        var image=decoder.Frames[0];Validate(image);image.Freeze();return image;
    }
    internal static void Validate(BitmapSource image)
    {
        if(image.PixelWidth<80||image.PixelHeight<30||image.PixelWidth>12000||image.PixelHeight>8000||(long)image.PixelWidth*image.PixelHeight>40_000_000)
            throw new ArgumentException("80×30 이상, 4천만 픽셀 이하의 보상 화면을 선택하세요.");
    }
    internal static async Task<RelicRecognition> RecognizeAsync(BitmapSource image,RelicCatalog catalog,CancellationToken token,string? diagnosticsDirectory=null,Action<RelicItem[]>? onCandidates=null,bool live=false,Action<RelicRecognition>? onProgress=null)
    {
        using var activity=OcrActivity.Enter();token.ThrowIfCancellationRequested();
        Validate(image);
        // BitmapFrame may retain a dispatcher-bound decoder even after Freeze.
        // Cross the worker boundary with pixels, never with a WIC decoder graph.
        var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var width=converted.PixelWidth;var height=converted.PixelHeight;
        var pixels=new byte[width*height*4];converted.CopyPixels(pixels,width*4,0);await gate.WaitAsync(token);
        try{return await Task.Run(()=>{var detached=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);detached.Freeze();return Recognize(detached,catalog,token,diagnosticsDirectory,onCandidates,live,onProgress) with{Model=OcrModels.ForRelics(width,height)};},token);}
        finally{gate.Release();}
    }
    private static RelicRecognition Recognize(BitmapSource source,RelicCatalog catalog,CancellationToken token,string? diagnosticsDirectory,Action<RelicItem[]>? onCandidates,bool live,Action<RelicRecognition>? onProgress)
    {
        var total=Stopwatch.StartNew();double ocrMilliseconds=0,matchingMilliseconds=0;int reads=0;
        var matching=new RelicCatalog.MatchSession();
        RecognizedReward[] Match(RewardText[] lines,Func<RecognizedReward,bool>? corroborated=null){var watch=Stopwatch.StartNew();var result=catalog.Recognize(lines,matching,corroborated);matchingMilliseconds+=watch.Elapsed.TotalMilliseconds;return result;}
        RelicTiming Timing()=>new(total.Elapsed.TotalMilliseconds,ocrMilliseconds,matchingMilliseconds,reads);
        var scale=Math.Min(3,Math.Min(3840d/source.PixelWidth,Math.Max(1,2880d/source.PixelWidth)));
        var scaled=new TransformedBitmap(source,new ScaleTransform(scale,scale));
        var image=new FormatConvertedBitmap(scaled,PixelFormats.Bgra32,null,0);image.Freeze();
        TesseractEngine? mixed=null;bool prefetched=false;
        var koreanLanguage=OcrModels.ForRelics(source.PixelWidth,source.PixelHeight);
        TesseractEngine Mixed(){if(mixed is null){mixed=new(OcrModels.DirectoryPath,koreanLanguage+"+eng",EngineMode.LstmOnly);mixed.SetVariable("user_defined_dpi","300");mixed.SetVariable("preserve_interword_spaces","1");}return mixed;}
        try
        {
            using var korean=new TesseractEngine(OcrModels.DirectoryPath,koreanLanguage,EngineMode.LstmOnly);
            korean.SetVariable("user_defined_dpi","300");korean.SetVariable("preserve_interword_spaces","1");
            var passes=new List<(RewardText[] Lines,RecognizedReward[] Rewards)>();
            var fullPasses=new List<(RewardText[] Lines,RecognizedReward[] Rewards)>();
            var evidence=new List<RewardEvidence>();
            RelicRecognition? cachedSnapshot=null;int snapshotPasses=-1,snapshotEvidence=-1;
            string published="";
            var languageLines=new Dictionary<string,List<RewardText>>{{"mixed",[]},{"ko",[]}};
            // Reward names occupy the central region. A narrow first pass avoids processing
            // the whole scene; only four independently corroborated labels allow an early exit.
            // Other layouts and uncertain names continue through the original exhaustive pipeline.
            if(source.PixelWidth>=1920 && source.PixelHeight>=1080)
            {
                var precisionImage=image;
                var fastScale=Math.Min(1,Math.Max(1920d/source.PixelWidth,1080d/source.PixelHeight));
                image=new FormatConvertedBitmap(new TransformedBitmap(source,new ScaleTransform(fastScale,fastScale)),PixelFormats.Bgra32,null,0);image.Freeze();
                // Compact reward layouts place the first line of wrapped names
                // above 40% of the screen. Read that complete row before windows
                // which may bisect the first line. Keep the existing independent
                // evidence and four-slot acceptance requirements unchanged.
                int upperY=(int)(image.PixelHeight*.372),upperEnd=(int)(image.PixelHeight*.427);
                int upperX=(int)(image.PixelWidth*.20),upperRight=(int)(image.PixelWidth*.80);
                var upper=new CroppedBitmap(image,new(upperX,upperY,upperRight-upperX,upperEnd-upperY));
                Read(BrightText(upper),PageSegMode.SparseText,"fast-upper-bright",upperY,upperX,koreanOnly:true);
                if(!live||passes[^1].Rewards.Length>0)
                {
                    Read(TintText(upper),PageSegMode.SparseText,"fast-upper-tint",upperY,upperX,koreanOnly:true);
                    Read(InvertedValue(upper),PageSegMode.SparseText,"fast-upper-inverted",upperY,upperX,koreanOnly:true);
                }
                var compact=TryComplete();if(compact is not null)return compact;
                var compactRow=Snapshot().Rewards;
                bool preferNativeRow=compactRow.Length is >=2 and <=3&&compactRow.Any(r=>!r.Confident);
                var locatedNames=compactRow.Where(r=>r.Candidates.FirstOrDefault()?.Similarity>=.82).ToArray();
                if(locatedNames.Length<2)
                {
                    // Cropped captures and large UI scales move the name row.
                    // Search above the usual band only when it yielded no row.
                    int y=(int)(image.PixelHeight*.22),end=(int)(image.PixelHeight*.37);
                    var search=new CroppedBitmap(image,new(0,y,image.PixelWidth,end-y));
                    Read(BrightText(search),PageSegMode.SparseText,"locate-bright",y,koreanOnly:true);
                    locatedNames=Snapshot().Rewards.Where(r=>r.Candidates.FirstOrDefault()?.Similarity>=.82).ToArray();
                }
                if(locatedNames.Length<2)
                {
                    // A locator band can end between a name and Blueprint. Such
                    // fragments may not rank highly yet, but two aligned Prime
                    // labels still locate the row; they are never confirmations.
                    var primeMarkers=passes.SelectMany(p=>p.Lines).Where(l=>l.Height<.05&&l.Width<.28&&
                        RelicCatalog.NormalizeOcr(l.Text).Contains("프라임")).ToArray();
                    var markerRows=primeMarkers.Select(anchor=>
                    {
                        var row=new List<RewardText>();
                        foreach(var marker in primeMarkers.Where(l=>Math.Abs(l.Y-anchor.Y)<.045).OrderByDescending(l=>l.Confidence))
                            if(!row.Any(l=>Math.Abs(l.X+l.Width/2-marker.X-marker.Width/2)<.055))row.Add(marker);
                        return row;
                    }).OrderByDescending(row=>row.Count).FirstOrDefault();
                    if(markerRows is {Count:>=2})locatedNames=markerRows.Select(l=>new RecognizedReward(l.Text,l,[],false)).ToArray();
                }
                // Small Hangul loses distinguishing strokes in the fast downsample.
                // Keep the whole located name row (including a following Blueprint
                // line), but recover those pixels from the source before enlarging.
                // Upscaling an already binarized/downsampled crop cannot recover them.
                RelicRecognition? ReadNativeRow()
                {
                    if(locatedNames.Length<2)return null;
                    var fastImage=image;
                    var glyphHeights=passes.SelectMany(p=>p.Lines).Where(l=>RelicCatalog.NormalizeOcr(l.Text).Contains("프라임")&&l.Height<.045)
                        .Select(l=>l.Height).DefaultIfEmpty(.02).Order().ToArray();
                    double glyphHeight=glyphHeights[glyphHeights.Length/2];
                    double firstLine=locatedNames.Min(r=>r.Bounds.Y);
                    double nativeTop=Math.Max(0,firstLine-.003);
                    double nativeBottom=Math.Min(1,Math.Max(locatedNames.Max(r=>r.Bounds.Y+r.Bounds.Height),firstLine+glyphHeight*2.3)+.003);
                    image=new FormatConvertedBitmap(new TransformedBitmap(source,new ScaleTransform(2,2)),PixelFormats.Bgra32,null,0);image.Freeze();
                    int y=(int)(nativeTop*image.PixelHeight),end=(int)Math.Ceiling(nativeBottom*image.PixelHeight);
                    int left=(int)(image.PixelWidth*.08),right=(int)(image.PixelWidth*.92);
                    var row=new CroppedBitmap(image,new(left,y,right-left,end-y));
                    int start=passes.Count;
                    Read(BrightText(row),PageSegMode.SparseText,"native-row-bright",y,left,koreanOnly:true);
                    Read(GoldText(row),PageSegMode.SparseText,"native-row-gold",y,left,koreanOnly:true);
                    var complete=TryComplete();
                    var nativeReads=passes.Skip(start).ToArray();
                    var nativeAnchors=Snapshot().Rewards;
                    if(complete is null)
                    foreach(var anchor in nativeAnchors.Where(r=>!r.Confident&&r.Candidates.Length>0))
                    {
                        // A row read can split the component off the weapon prefix.
                        // Include nearby glyph fragments assigned to this card, or
                        // a tight prefix crop could falsely read a main Blueprint.
                        var fragments=nativeReads.SelectMany(p=>p.Lines).Where(l=>l.Confidence>=40&&l.Text.Any(c=>c is >= '\uac00' and <= '\ud7a3')&&
                            ReferenceEquals(nativeAnchors.MinBy(a=>Math.Abs(l.X+l.Width/2-a.Bounds.X-a.Bounds.Width/2)),anchor)).ToArray();
                        double glyphLeft=Math.Min(anchor.Bounds.X,fragments.Select(l=>l.X).DefaultIfEmpty(anchor.Bounds.X).Min());
                        double glyphRight=Math.Max(anchor.Bounds.X+anchor.Bounds.Width,fragments.Select(l=>l.X+l.Width).DefaultIfEmpty(anchor.Bounds.X+anchor.Bounds.Width).Max());
                        int labelLeft=Math.Max(left,(int)(glyphLeft*image.PixelWidth)-24);
                        int labelRight=Math.Min(right,(int)Math.Ceiling(glyphRight*image.PixelWidth)+24);
                        int nativeGlyphTop=Math.Max(y,(int)(anchor.Bounds.Y*image.PixelHeight)-6);
                        int labelEnd=Math.Min(end,(int)Math.Ceiling((anchor.Bounds.Y+Math.Max(anchor.Bounds.Height,glyphHeight*2.3))*image.PixelHeight)+6);
                        if(labelRight<=labelLeft||labelEnd<=nativeGlyphTop)continue;
                        var label=new CroppedBitmap(image,new(labelLeft,nativeGlyphTop,labelRight-labelLeft,labelEnd-nativeGlyphTop));
                        Read(CleanLabelTop(BrightText(label)),PageSegMode.SingleBlock,$"refine-native-bright-{labelLeft}",nativeGlyphTop,labelLeft,koreanOnly:true);
                        Read(CleanLabelTop(GoldText(label)),PageSegMode.SingleBlock,$"refine-native-gold-{labelLeft}",nativeGlyphTop,labelLeft,koreanOnly:true);
                        complete=TryComplete();if(complete is not null)break;
                    }
                    // A shorter row must be independently read in full, never
                    // inferred from just the labels we managed to recognize.
                    var rewards=Snapshot().Rewards;
                    bool extraText=nativeReads.SelectMany(p=>p.Lines).Any(l=>l.Confidence>=55&&l.Text.Count(c=>c is >= '\uac00' and <= '\ud7a3')>=4&&
                        !rewards.Any(r=>Math.Abs(l.X+l.Width/2-r.Bounds.X-r.Bounds.Width/2)<Math.Max(.035,r.Bounds.Width*.55)));
                    if(complete is null&&rewards.Length is >=1 and <=3&&rewards.All(r=>r.Confident)&&
                        rewards.Length==passes.Max(p=>p.Rewards.Length)&&nativeReads.Length==2&&!extraText&&
                        rewards.All(r=>Corroborated(r)&&evidence.Any(e=>e.Reward.Candidates.FirstOrDefault()?.Item.Id==r.Item!.Id&&e.Reward.Candidates[0].Similarity==1&&e.Reward.Bounds.Confidence>=65))&&
                        nativeReads.All(p=>p.Rewards.Length==rewards.Length&&p.Rewards.Zip(rewards).All(pair=>
                            Math.Abs(pair.First.Bounds.X+pair.First.Bounds.Width/2-pair.Second.Bounds.X-pair.Second.Bounds.Width/2)<.035)))
                        complete=new(rewards,nativeReads.SelectMany(p=>p.Lines).ToArray(),$"보상 {rewards.Length}개 자동 인식 완료",Timing());
                    image=fastImage;
                    return complete;
                }
                bool expandedRowRead=false;
                RelicRecognition? ReadExpandedRow(RecognizedReward[] located)
                {
                    if(located.Length<2)return null;
                    expandedRowRead=true;
                    // Locate the bottom baseline, then include the preceding line.
                    // Refining only the already detected glyph box cannot recover a
                    // weapon name cut off above a wrapped component such as Blade.
                    double baseline=located.Select(r=>r.Bounds.Y+r.Bounds.Height).Order().ElementAt(located.Length/2);
                    double lineHeight=located.Select(r=>r.Bounds.Height).Order().ElementAt(located.Length/2);
                    int y=(int)(image.PixelHeight*Math.Max(0,baseline-Math.Clamp(lineHeight*3,.055,.10)));
                    int end=(int)(image.PixelHeight*Math.Min(1,baseline+.008));
                    var row=new CroppedBitmap(image,new(0,y,image.PixelWidth,end-y));
                    Read(BrightText(row),PageSegMode.SparseText,"wrapped-row-bright",y,koreanOnly:true);
                    Read(TintText(row),PageSegMode.SparseText,"wrapped-row-tint",y,koreanOnly:true);
                    Read(InvertedValue(row),PageSegMode.SparseText,"wrapped-row-inverted",y,koreanOnly:true);
                    return TryComplete();
                }
                // A located but imperfect compact row benefits from source
                // glyphs first; searching for a displaced/wrapped row retains
                // the cheaper broad-row route that already handles four cards.
                if(preferNativeRow){compact=ReadNativeRow();if(compact is not null)return compact;}
                compact=ReadExpandedRow(locatedNames);if(compact is not null)return compact;
                if(!preferNativeRow){compact=ReadNativeRow();if(compact is not null)return compact;}
                // Refine detected names before broad scenery windows. The live
                // path spends its budget on uncertain labels, not the full scene.
                var initial=Snapshot();
                foreach(var anchor in initial.Rewards.Where(r=>!r.Confident&&r.Candidates.FirstOrDefault()?.Similarity>=.82))
                {
                    int x=Math.Max(0,(int)(anchor.Bounds.X*image.PixelWidth)-6),y=Math.Max(0,(int)(anchor.Bounds.Y*image.PixelHeight)-6);
                    int right=Math.Min(image.PixelWidth,(int)((anchor.Bounds.X+anchor.Bounds.Width)*image.PixelWidth)+6),end=Math.Min(image.PixelHeight,(int)((anchor.Bounds.Y+anchor.Bounds.Height)*image.PixelHeight)+6);
                    if(right<=x||end<=y)continue;
                    var label=new CroppedBitmap(image,new(x,y,right-x,end-y));
                    Read(BrightText(label),PageSegMode.SingleBlock,$"refine-early-bright-{x}",y,x,koreanOnly:true);
                    Read(TintText(label),PageSegMode.SingleBlock,$"refine-early-tint-{x}",y,x,koreanOnly:true);
                    compact=TryComplete();if(compact is not null)return compact;
                    Read(InvertedValue(label),PageSegMode.SingleBlock,$"refine-early-inverted-{x}",y,x,koreanOnly:true);
                    compact=TryComplete();if(compact is not null)return compact;
                }
                // Small overlapping name windows keep scenery from dominating text
                // segmentation. These are only search windows, never assumed slots:
                // all four names still need corroboration and other layouts fall back.
                int labelTop=(int)(image.PixelHeight*.40),labelBottom=(int)(image.PixelHeight*.57);
                bool completeRowLocated=initial.Rewards.Length==4&&initial.Rewards.All(r=>r.Candidates.FirstOrDefault()?.Similarity>=.82);
                for(int slot=0;slot<4&&!completeRowLocated;slot++)
                {
                    int left=(int)(image.PixelWidth*(.08+.20*slot)),right=(int)(image.PixelWidth*(.32+.20*slot));
                    var label=new CroppedBitmap(image,new(left,labelTop,right-left,labelBottom-labelTop));
                    Read(LocalContrastText(label),PageSegMode.SparseText,$"fast-card-contrast-{slot}",labelTop,left,koreanOnly:true);
                    Read(InvertedValue(label),PageSegMode.SparseText,$"fast-card-inverted-{slot}",labelTop,left,koreanOnly:true);
                }
                var bounded=TryComplete();if(bounded is not null)return bounded;
                if(!expandedRowRead)
                {
                    bounded=ReadExpandedRow(Snapshot().Rewards.Where(r=>r.Candidates.FirstOrDefault()?.Similarity>=.82).ToArray());
                    if(bounded is not null)return bounded;
                }
                // Use the detected glyph bounds to remove scenery around each name.
                // A single accurate inverted read may locate a label even when the
                // contrast read joined it with a neighboring illustration or UI line.
                var anchors=Snapshot().Rewards;
                if(anchors.Length>=2)
                for(int i=0;i<anchors.Length;i++)
                {
                    var anchor=anchors[i];if(anchor.Confident||anchor.Candidates.FirstOrDefault()?.Similarity<.82||anchor.Candidates.Length==0)continue;
                    int margin=Math.Max(6,(int)(image.PixelHeight*.004));
                    int x=Math.Max(0,(int)(anchor.Bounds.X*image.PixelWidth)-margin),y=Math.Max(0,(int)(anchor.Bounds.Y*image.PixelHeight)-margin);
                    int right=Math.Min(image.PixelWidth,(int)((anchor.Bounds.X+anchor.Bounds.Width)*image.PixelWidth)+margin),glyphBottom=Math.Min(image.PixelHeight,(int)((anchor.Bounds.Y+anchor.Bounds.Height)*image.PixelHeight)+margin);
                    if(right<=x||glyphBottom<=y)continue;
                    var name=new CroppedBitmap(image,new(x,y,right-x,glyphBottom-y));
                    Read(InvertedValue(name),PageSegMode.SingleBlock,$"refine-fast-inverted-{i}",y,x,koreanOnly:true);
                    Read(LocalContrastText(name),PageSegMode.SingleBlock,$"refine-fast-contrast-{i}",y,x,koreanOnly:true);
                    bounded=TryComplete();if(bounded is not null)return bounded;
                    if(anchor.Bounds.Height<.045)
                    {
                        // A single isolated label must not be segmented as several
                        // paragraph lines (e.g. the syllables of 듀얼).
                        Read(BrightText(name),PageSegMode.SingleLine,$"refine-line-bright-{i}",y,x,koreanOnly:true);
                        Read(InvertedValue(name),PageSegMode.SingleLine,$"refine-line-inverted-{i}",y,x,koreanOnly:true);
                        bounded=TryComplete();if(bounded is not null)return bounded;
                    }
                }
                bounded=TryComplete();if(bounded is not null)return bounded;
                int top=(int)(image.PixelHeight*.38),bottom=(int)(image.PixelHeight*.72);
                var located=anchors.Where(a=>a.Candidates.FirstOrDefault()?.Similarity>=.82).ToArray();
                if(located.Length>=2)
                {
                    // Use the consensus baseline, so one scenery-contaminated box
                    // cannot expand a small two-line name row into the whole scene.
                    double row=located.Select(a=>a.Bounds.Y).Order().ElementAt(located.Length/2);
                    double h=located.Select(a=>a.Bounds.Height).Order().ElementAt(located.Length/2);
                    top=(int)(image.PixelHeight*Math.Max(0,row-.008));
                    bottom=(int)(image.PixelHeight*Math.Min(1,row+Math.Clamp(h*1.2,.06,.09)+.012));
                }
                var strip=new CroppedBitmap(image,new(0,top,image.PixelWidth,bottom-top));
                var bandStart=passes.Count;
                Read(LocalContrastText(strip),PageSegMode.SparseText,"fast-contrast",top,koreanOnly:true);
                Read(InvertedValue(strip),PageSegMode.SparseText,"fast-inverted",top,koreanOnly:true);
                var fast=TryComplete();if(fast is not null)return fast;
                Read(BrightText(strip),PageSegMode.SparseText,"fast-bright",top,koreanOnly:true);
                fast=TryComplete();if(fast is not null)return fast;
                // One-to-three rewards need all three whole-row variants to agree.
                // Unexplained readable Hangul in the same name row blocks this exit,
                // so a shorter subset cannot silently replace a visible unread card.
                var rowReads=passes.Skip(bandStart).ToArray();
                var certified=rowReads.Select(p=>RelicCatalog.ConfirmCorroborated(p.Rewards,evidence)).ToArray();
                var rowRewards=certified.FirstOrDefault()??[];
                if(rowRewards.Length is >=1 and <=3&&certified.All(rr=>rr.Length==rowRewards.Length&&rr.All(r=>r.Confident)&&rr.Select(r=>r.Item!.Id).SequenceEqual(rowRewards.Select(r=>r.Item?.Id))))
                {
                    double first=rowRewards.Min(r=>r.Bounds.Y),last=rowRewards.Max(r=>r.Bounds.Y+r.Bounds.Height);
                    bool unexplained=rowReads.SelectMany(p=>p.Lines).Any(l=>l.Confidence>=55&&l.Text.Count(c=>c is >= '\uac00' and <= '\ud7a3')>=4&&
                        l.Y+l.Height/2>=first-.005&&l.Y+l.Height/2<=last+.005&&!rowRewards.Any(r=>
                            Math.Abs(l.X+l.Width/2-r.Bounds.X-r.Bounds.Width/2)<Math.Max(.035,r.Bounds.Width*.55)));
                    if(!unexplained)return new(rowRewards,rowReads.SelectMany(p=>p.Lines).ToArray(),$"보상 {rowRewards.Length}개 자동 인식 완료",Timing());
                }
                image=precisionImage;
            }
            // Automatic overlays must never enter the minute-long archival
            // fallback. Preserve verified cards; uncertain cards stay editable.
            if(live)return Snapshot();
            foreach(var variant in new[]{"original","bright","tinted","contrast"})
            {
                var input=variant switch{"bright"=>BrightText(image),"tinted"=>TintText(image),"contrast"=>LocalContrastText(image),_=>image};
                var firstFull=passes.Count;Read(input,PageSegMode.SparseText,variant);fullPasses.AddRange(passes.Skip(firstFull));
                var exact=passes.Where(p=>p.Rewards.Length==4&&p.Rewards.All(r=>r.Confident)).ToArray();
                if(exact.Length>=2&&exact.Any(a=>exact.Count(b=>a.Rewards.Select(r=>r.Item!.Id).SequenceEqual(b.Rewards.Select(r=>r.Item!.Id)))>=2))
                {
                    var agreed=exact.First(a=>exact.Count(b=>a.Rewards.Select(r=>r.Item!.Id).SequenceEqual(b.Rewards.Select(r=>r.Item!.Id)))>=2);
                    return new(agreed.Rewards,agreed.Lines,"보상 4개 자동 인식 완료",Timing());
                }
                if(source.PixelWidth>=1920&&source.PixelHeight>=1080&&fullPasses.SelectMany(p=>p.Lines).Any(l=>RelicCatalog.Normalize(l.Text).Contains("보상")))
                {
                    // Never accept a shorter subset when another pass saw more slots.
                    // Full-screen corroborated readings, with a visible reward heading, are
                    // required for the one-to-three reward early exit.
                    int count=passes.Max(p=>p.Rewards.Length);
                    var partial=fullPasses.Select(p=>(p.Lines,Rewards:RelicCatalog.ConfirmCorroborated(p.Rewards,evidence)))
                        .Where(p=>p.Rewards.Length==count&&count is >=1 and <=3&&p.Rewards.All(r=>r.Confident)).ToArray();
                    foreach(var candidate in partial)
                        if(partial.Count(p=>p.Rewards.Select(r=>r.Item!.Id).SequenceEqual(candidate.Rewards.Select(r=>r.Item!.Id))&&p.Rewards.Zip(candidate.Rewards).All(pair=>Math.Abs(pair.First.Bounds.X-pair.Second.Bounds.X)<.06&&Math.Abs(pair.First.Bounds.Y-pair.Second.Bounds.Y)<.065))>=3)
                            return new(candidate.Rewards,candidate.Lines,$"보상 {count}개 자동 인식 완료",Timing());
                }
            }
            // Small imported screenshots can lose a faint syllable in a binary
            // mask. Read only unresolved, precisely located glyphs from the
            // original pixels. No fuzzy-name
            // exception or lower confirmation threshold is introduced here.
            RelicRecognition? RefineSmallLabels(RecognizedReward[] anchors)
            {
                if(source.PixelWidth>=1920&&source.PixelHeight>=1080)return null;
                if(diagnosticsDirectory is not null)File.WriteAllText(Path.Combine(diagnosticsDirectory,"small-source-anchors.json"),System.Text.Json.JsonSerializer.Serialize(anchors,Catalog.Json));
                if(anchors.Length>=2&&anchors.Count(a=>RelicCatalog.NormalizeOcr(a.RawText).Contains("프라임"))>=2)
                foreach(var anchor in anchors.Where(a=>!a.Confident&&a.Candidates.FirstOrDefault()?.Similarity>=.68))
                {
                    var archive=image;
                    try
                    {
                        image=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
                        int left=Math.Max(0,(int)(anchor.Bounds.X*image.PixelWidth)-4),top=Math.Max(0,(int)(anchor.Bounds.Y*image.PixelHeight)-2);
                        int right=Math.Min(image.PixelWidth,(int)Math.Ceiling((anchor.Bounds.X+anchor.Bounds.Width)*image.PixelWidth)+4),bottom=Math.Min(image.PixelHeight,(int)Math.Ceiling((anchor.Bounds.Y+anchor.Bounds.Height)*image.PixelHeight)+2);
                        if(right<=left||bottom<=top)continue;
                        var label=new CroppedBitmap(image,new(left,top,right-left,bottom-top));
                        var mode=label.PixelHeight<=22?PageSegMode.SingleLine:PageSegMode.SingleBlock;
                        // Keep native tiny-line geometry. The large white border
                        // used for enlarged refinements can suppress six-pixel
                        // glyphs inside a dark imported screenshot.
                        Read(new FormatConvertedBitmap(label,PixelFormats.Gray8,null,0),mode,$"small-original-{left}",top,left,koreanOnly:true);
                        image=new FormatConvertedBitmap(new TransformedBitmap(source,new ScaleTransform(2,2)),PixelFormats.Bgra32,null,0);
                        var enlarged=new FormatConvertedBitmap(new TransformedBitmap(label,new ScaleTransform(2,2)),PixelFormats.Bgra32,null,0);
                        Read(InvertedValue(enlarged),mode,$"small-inverted-{left}",top*2,left*2,koreanOnly:true);
                        var complete=TryComplete();if(complete is not null)return complete;
                    }
                    finally{image=archive;}
                }
                return null;
            }
            // Small reward labels sit under detailed item art. Re-read bounded text bands after locating
            // a Prime label; this prevents icons from joining the label and changing its baseline.
            var markers=passes.SelectMany(p=>p.Lines).Where(x=>x.Height<.13&&x.Text.Length<60).Where(x=>RelicCatalog.Normalize(x.Text).Contains("프라임")||RelicCatalog.Normalize(x.Text).Contains("PRIME")).ToArray();
            var bands=new List<(double Top,double Bottom)>();
            foreach(var marker in markers.OrderBy(x=>x.Height))
            {
                var center=marker.Y+marker.Height/2;var h=Math.Max(10d/image.PixelHeight,marker.Height);
                if(bands.Any(b=>center>=b.Top&&center<=b.Bottom))continue;
                bands.Add((Math.Max(0,marker.Y-h*1.1),Math.Min(1,marker.Y+marker.Height+h*.2)));
                if(bands.Count==2)break;
            }
            for(int i=0;i<bands.Count;i++)
            {
                int y=(int)(bands[i].Top*image.PixelHeight),height=Math.Max(1,(int)((bands[i].Bottom-bands[i].Top)*image.PixelHeight));
                var band=new CroppedBitmap(image,new(0,y,image.PixelWidth,Math.Min(height,image.PixelHeight-y)));
                Read(InvertedValue(band),PageSegMode.SingleBlock,"band-inverted-"+i,y);
                Read(LocalContrastText(band),PageSegMode.SingleBlock,"band-contrast-"+i,y);
                // Overlapping bounded windows isolate labels even when theme borders disappear.
                // Coordinates stay in the original image; overlapping hypotheses are deduplicated later.
                for(int step=0;step<=8;step++)
                {
                    int left=(int)(band.PixelWidth*step*.09),right=Math.Min(band.PixelWidth,left+(int)(band.PixelWidth*.28));
                    var column=new CroppedBitmap(band,new(left,0,right-left,band.PixelHeight));
                    Read(InvertedValue(column),PageSegMode.SingleBlock,$"column-{i}-{step}",y,left);
                    Read(LocalContrastText(column),PageSegMode.SingleBlock,$"column-contrast-{i}-{step}",y,left);
                    Read(TintText(column),PageSegMode.SingleBlock,$"column-tint-{i}-{step}",y,left);
                }
            }
            foreach(var entries in languageLines.Values)
            {
                var lines=entries.DistinctBy(x=>(RelicCatalog.Normalize(x.Text),Math.Round(x.X,2),Math.Round(x.Y,2))).ToArray();
                passes.Add((lines,Match(lines)));
            }
            var combined=passes.SelectMany(p=>p.Lines).DistinctBy(x=>(RelicCatalog.Normalize(x.Text),Math.Round(x.X,2),Math.Round(x.Y,2))).ToArray();passes.Add((combined,Match(combined)));
            RelicRecognition? TryComplete()
            {
                var snapshot=Snapshot();var lines=snapshot.Lines;var rewards=snapshot.Rewards;
                if(!prefetched&&rewards.Length>0&&rewards.All(r=>r.Confident)){prefetched=true;onCandidates?.Invoke(rewards.Select(r=>r.Item!).ToArray());}
                if(rewards.Length!=4||rewards.Any(r=>!r.Confident))return null;
                // Two preprocessing families must each corroborate every label. Do not turn
                // a single plausible fragment or a duplicate language read into agreement.
                if(rewards.Any(r=>evidence.Where(e=>e.Reward.Candidates.FirstOrDefault()?.Item.Id==r.Item!.Id &&
                    e.Reward.Candidates[0].Similarity>=.9 && e.Reward.Bounds.Confidence>=40 &&
                    Math.Abs(e.Reward.Bounds.X-r.Bounds.X)<.06&&Math.Abs(e.Reward.Bounds.Y-r.Bounds.Y)<.065)
                    .Select(e=>e.Family).Distinct().Count()<2))return null;
                return new(rewards,lines,"보상 4개 자동 인식 완료",Timing());
            }
            RelicRecognition Snapshot()
            {
                // Progress publication and early-exit checks often inspect the
                // same evidence. Reuse that immutable ranking until a read adds
                // a pass/evidence; refresh elapsed time, never the decision rules.
                if(cachedSnapshot is not null&&snapshotPasses==passes.Count&&snapshotEvidence==evidence.Count)return cachedSnapshot with{Timing=Timing()};
                var lines=passes.SelectMany(p=>p.Lines).DistinctBy(x=>(RelicCatalog.Normalize(x.Text),Math.Round(x.X,2),Math.Round(x.Y,2))).ToArray();
                // A prefix from one pass joined to Blueprint from another may
                // spell a different valid item. Prefer the complete label that
                // independent full-label reads actually corroborated at this slot.
                var rewards=RelicCatalog.ConfirmCorroborated(Match(lines,Corroborated),evidence);
                rewards=rewards.Select(r=>r.Confident&&!Corroborated(r)?r with{Confident=false}:r).ToArray();
                int confirmed=rewards.Count(r=>r.Confident);
                snapshotPasses=passes.Count;snapshotEvidence=evidence.Count;
                return cachedSnapshot=new(rewards,lines,confirmed==rewards.Length&&confirmed>0?$"보상 {confirmed}개 인식":$"보상 {confirmed}개 확인 · 나머지는 이름 확인 필요",Timing());
            }
            bool Corroborated(RecognizedReward r)=>r.Candidates.Length>0&&evidence.Where(e=>e.Reward.Candidates.FirstOrDefault()?.Item.Id==r.Candidates[0].Item.Id&&
                e.Reward.Candidates[0].Similarity>=.9&&e.Reward.Bounds.Confidence>=40&&Math.Abs(e.Reward.Bounds.X-r.Bounds.X)<.06&&Math.Abs(e.Reward.Bounds.Y-r.Bounds.Y)<.065)
                .Select(e=>e.Family).Distinct().Count()>=2;
            void Read(BitmapSource input,PageSegMode mode,string name,int offsetY=0,int offsetX=0,bool koreanOnly=false)
            {
                token.ThrowIfCancellationRequested();
                // Cooperative budget: an in-flight native OCR read finishes,
                // but no further read starts after six seconds.
                if(live&&total.Elapsed.TotalSeconds>=6)return;
                const int padding=12;
                var prepared=name.StartsWith("refine-")?PadText(input,padding):input;
                var border=name.StartsWith("refine-")?padding:0;
                var encoded=diagnosticsDirectory is null?null:OcrPixels.Png(prepared);
                using var pix=OcrPixels.Create(prepared);
                foreach(var (processor,language) in koreanOnly?new[]{(korean,"ko")}:new[]{(Mixed(),"mixed"),(korean,"ko")})
                {
                token.ThrowIfCancellationRequested();var passName=name+"-"+language;
                if(diagnosticsDirectory is not null)File.WriteAllBytes(Path.Combine(diagnosticsDirectory,passName+".png"),encoded!);
                var readWatch=Stopwatch.StartNew();reads++;
                using var page=processor.Process(pix,mode);
                using var iterator=page.GetIterator();var words=new List<RewardText>();iterator.Begin();
                var level=name.StartsWith("refine-native-")||name.StartsWith("small-")?PageIteratorLevel.TextLine:PageIteratorLevel.Word;
                do
                {
                    var text=iterator.GetText(level)?.Trim();
                    if(string.IsNullOrWhiteSpace(text)||!iterator.TryGetBoundingBox(level,out var box))continue;
                    words.Add(new(text,(double)(box.X1-border+offsetX)/image.PixelWidth,(double)(box.Y1-border+offsetY)/image.PixelHeight,(double)box.Width/image.PixelWidth,(double)box.Height/image.PixelHeight,iterator.GetConfidence(level)));
                }while(iterator.Next(level));
                ocrMilliseconds+=readWatch.Elapsed.TotalMilliseconds;
                var lines=level==PageIteratorLevel.TextLine?words.ToArray():GroupWords(words,(double)image.PixelWidth/image.PixelHeight);languageLines[language].AddRange(lines);var rewards=Match(lines);passes.Add((lines,rewards));
                if(!prefetched&&source.PixelWidth>=1920&&source.PixelHeight>=1080&&rewards.Length==4&&rewards.All(r=>r.Confident&&r.Candidates[0].Similarity==1))
                {prefetched=true;onCandidates?.Invoke(rewards.Select(r=>r.Item!).ToArray());}
                var family=name.Contains("contrast")?"contrast":name.Contains("gold")?"gold":name.Contains("tint")?"tint":name.Contains("bright")?"bright":name.Contains("original")?"original":"inverted";
                evidence.AddRange(rewards.Select(x=>new RewardEvidence(family,x)));
                if(onProgress is not null)
                {
                    var snapshot=Snapshot();var key=string.Join("|",snapshot.Rewards.Select(r=>$"{r.Bounds.X:0.00}:{r.Item?.Id}"));
                    if(snapshot.Rewards.Any(r=>r.Confident)&&key!=published){published=key;onProgress(snapshot);}
                }
                if(diagnosticsDirectory is not null)File.WriteAllText(Path.Combine(diagnosticsDirectory,passName+".json"),System.Text.Json.JsonSerializer.Serialize(lines,Catalog.Json));
                }
            }
            token.ThrowIfCancellationRequested();
            var best=passes.OrderByDescending(p=>p.Rewards.Sum(x=>(x.Confident?2:0)+Math.Pow(x.Candidates.FirstOrDefault()?.Similarity??0,4))).First();
            RecognizedReward[] Fuse()=>RelicCatalog.ConfirmCorroborated(best.Rewards.Select(anchor=>passes.SelectMany(p=>p.Rewards).Where(x=>x.Candidates.Length>0&&
                Math.Abs(x.Bounds.X+x.Bounds.Width/2-anchor.Bounds.X-anchor.Bounds.Width/2)<Math.Max(.035,Math.Min(x.Bounds.Width,anchor.Bounds.Width)*.4)&&
                Math.Abs(x.Bounds.Y-anchor.Bounds.Y)<.065)
                .OrderByDescending(x=>x.Candidates[0].Similarity).ThenByDescending(x=>x.Confident).ThenByDescending(x=>x.Bounds.Confidence).FirstOrDefault()??anchor),evidence);
            var fused=Fuse();
            // Refine unresolved cards using the actual neighboring label centers. This isolates
            // wrapped names from adjoining cards without assuming a fixed UI scale or four rewards.
            if(fused.Length>=2&&bands.Count>0)
            {
                var centers=fused.Select(x=>x.Bounds.X+x.Bounds.Width/2).ToArray();
                for(int i=0;i<fused.Length;i++)
                {
                    if(fused[i].Confident)continue;
                    double left=i==0?Math.Max(0,centers[0]-(centers[1]-centers[0])/2):(centers[i-1]+centers[i])/2;
                    double right=i==fused.Length-1?Math.Min(1,centers[i]+(centers[i]-centers[i-1])/2):(centers[i]+centers[i+1])/2;
                    var band=bands.OrderBy(x=>Math.Abs((x.Top+x.Bottom)/2-fused[i].Bounds.Y-fused[i].Bounds.Height/2)).First();
                    int x=(int)(left*image.PixelWidth),y=(int)(band.Top*image.PixelHeight);
                    int w=Math.Max(1,(int)((right-left)*image.PixelWidth)),h=Math.Max(1,(int)((band.Bottom-band.Top)*image.PixelHeight));
                    var crop=new CroppedBitmap(image,new(x,y,Math.Min(w,image.PixelWidth-x),Math.Min(h,image.PixelHeight-y)));
                    Read(InvertedValue(crop),PageSegMode.SingleBlock,$"refine-inverted-{i}",y,x);
                    Read(LocalContrastText(crop),PageSegMode.SingleBlock,$"refine-contrast-{i}",y,x);
                    Read(TintText(crop),PageSegMode.SingleBlock,$"refine-tint-{i}",y,x);
                    var rows=TextRows(LocalContrastText(crop)).ToArray();
                    if(rows.Length is >=1 and <=3)
                    foreach(var family in new[]{"inverted","contrast"})
                    {
                        var first=passes.Count;
                        foreach(var (top,bottom) in rows)
                        {
                            var row=new CroppedBitmap(crop,new(0,top,crop.PixelWidth,bottom-top));
                            Read(family=="inverted"?InvertedValue(row):LocalContrastText(row),PageSegMode.SingleLine,$"refine-{family}-{i}-row-{top}",y+top,x);
                        }
                        var lines=passes.Skip(first).SelectMany(p=>p.Lines).ToArray();var rewards=Match(lines);
                        passes.Add((lines,rewards));evidence.AddRange(rewards.Select(r=>new RewardEvidence(family,r)));
                    }
                }
            }
            var small=RefineSmallLabels(Fuse());if(small is not null)return small;
            best=(best.Lines,Fuse());
            // A combat HUD can produce two-syllable fragments resembling Requiem names.
            // With no complete label or confident reward anywhere, leave the screen unrecognized.
            if(best.Rewards.All(r=>!r.Confident&&RelicCatalog.NormalizeOcr(r.RawText).Length<4))best=(best.Lines,[]);
            if(diagnosticsDirectory is not null)File.WriteAllText(Path.Combine(diagnosticsDirectory,"evidence.json"),System.Text.Json.JsonSerializer.Serialize(evidence,Catalog.Json));
            var pending=best.Rewards.Count(x=>!x.Confident);
            var notice=best.Rewards.Length==0?"보상 이름을 찾지 못했습니다. 이미지에서 보상 이름 줄을 드래그하거나 아래에서 직접 선택하세요.":
                pending>0?$"보상 {best.Rewards.Length}개 · 이름 확인 {pending}개 · 확인이 끝난 보상은 시세를 자동 조회합니다.":$"보상 {best.Rewards.Length}개 자동 인식 완료";
            if(source.PixelWidth<800&&best.Rewards.Length>0)notice+=" 작은 이미지여서 글자가 뭉개질 수 있습니다.";
            return new(best.Rewards,best.Lines,notice,Timing());
        }
        catch(Exception e) when(e is DllNotFoundException or TesseractException or TypeInitializationException)
        {throw new InvalidOperationException("한국어 인식 엔진을 시작하지 못했습니다. 앱의 OCR 파일과 Microsoft Visual C++ 2015–2022 x64 런타임을 확인하세요. 직접 검색은 계속 사용할 수 있습니다.",e);}
        finally{mixed?.Dispose();}
    }
    private static BitmapSource PadText(BitmapSource source,int padding)
    {
        var gray=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);int w=gray.PixelWidth,h=gray.PixelHeight,stride=w+padding*2;
        var pixels=new byte[w*h];gray.CopyPixels(pixels,w,0);var output=Enumerable.Repeat((byte)255,stride*(h+padding*2)).ToArray();
        for(int y=0;y<h;y++)Array.Copy(pixels,y*w,output,(y+padding)*stride+padding,w);
        var image=BitmapSource.Create(stride,h+padding*2,300,300,PixelFormats.Gray8,null,output,stride);image.Freeze();return image;
    }
    private static IEnumerable<(int Top,int Bottom)> TextRows(BitmapSource source)
    {
        int w=source.PixelWidth,h=source.PixelHeight;var pixels=new byte[w*h];source.CopyPixels(pixels,w,0);
        int start=-1;
        for(int y=0;y<=h;y++)
        {
            int count=0;if(y<h)for(int x=8;x<w-8;x++)if(pixels[y*w+x]<128)count++;
            var ink=y<h&&count>Math.Max(3,(w-16)*.035);
            if(ink&&start<0)start=y;
            if(!ink&&start>=0){if(y-start>=6)yield return(start,y);start=-1;}
        }
    }
    private static BitmapSource BrightText(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight;var data=new byte[width*height*4];source.CopyPixels(data,width*4,0);var gray=new byte[width*height];
        for(int i=0;i<gray.Length;i++)
        {
            var b=data[i*4];var g=data[i*4+1];var r=data[i*4+2];
            // Game rarity text ranges from white/silver to saturated gold/bronze.
            bool text=(r>145&&g>130&&b>120)||(r>155&&g>100&&r>b*1.15);
            gray[i]=text?(byte)0:(byte)255;
        }
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    private static BitmapSource TintText(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight;var data=new byte[width*height*4];source.CopyPixels(data,width*4,0);var gray=new byte[width*height];
        for(int i=0;i<gray.Length;i++)
        {
            int b=data[i*4],g=data[i*4+1],r=data[i*4+2];
            int maximum=Math.Max(r,Math.Max(g,b)),minimum=Math.Min(r,Math.Min(g,b));
            // UI themes can render reward labels as dim gold or red, below the white-text threshold.
            // Preserve saturated glyphs regardless of hue; separate from the white/silver pass.
            bool tinted=maximum>=60&&maximum-minimum>=25&&maximum-minimum>maximum*.22;
            gray[i]=tinted?(byte)0:(byte)255;
        }
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    private static BitmapSource GoldText(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight;var data=new byte[width*height*4];source.CopyPixels(data,width*4,0);var gray=new byte[width*height];
        for(int i=0;i<gray.Length;i++)
        {
            int b=data[i*4],g=data[i*4+1],r=data[i*4+2];
            // Gold UI strokes over gray scenery: preserve their edge intensity
            // while removing neutral highlights and the blue illustration/border.
            // Other themes retain the existing bright/tint/contrast fallbacks.
            gray[i]=r>=110&&r>=g*.95&&g>=b*1.1?(byte)(255-Math.Clamp((r-b-15)*4,0,255)):(byte)255;
        }
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    private static BitmapSource CleanLabelTop(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight;var gray=new byte[width*height];source.CopyPixels(gray,width,0);
        var ink=new int[height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(gray[y*width+x]<128)ink[y]++;
        double threshold=Math.Max(width*.08,ink.Max()*.25);
        int first=Array.FindIndex(ink,n=>n>=threshold);
        // Isolated tips of the item illustration can touch the first glyph and
        // make Tesseract combine two lines. Remove only sparse leading pixels;
        // keep the coordinate system and a margin above the dense text row.
        if(first>0&&first<height*.4)Array.Fill(gray,(byte)255,0,Math.Max(0,first-6)*width);
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    private static BitmapSource LocalContrastText(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight,stride=width+1;var data=new byte[width*height*4];source.CopyPixels(data,width*4,0);
        var values=new byte[width*height];var integral=new long[(width+1)*(height+1)];
        for(int y=0;y<height;y++)
        {
            long row=0;for(int x=0;x<width;x++){int i=y*width+x;values[i]=Math.Max(data[i*4],Math.Max(data[i*4+1],data[i*4+2]));row+=values[i];integral[(y+1)*stride+x+1]=integral[y*stride+x+1]+row;}
        }
        var gray=new byte[values.Length];const int radius=15;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int left=Math.Max(0,x-radius),right=Math.Min(width,x+radius+1),top=Math.Max(0,y-radius),bottom=Math.Min(height,y+radius+1),i=y*width+x;
            double mean=(double)(integral[bottom*stride+right]-integral[top*stride+right]-integral[bottom*stride+left]+integral[top*stride+left])/((right-left)*(bottom-top));
            gray[i]=values[i]>mean+12&&values[i]>=55?(byte)0:(byte)255;
        }
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    private static BitmapSource InvertedValue(BitmapSource source)
    {
        int width=source.PixelWidth,height=source.PixelHeight;var data=new byte[width*height*4];source.CopyPixels(data,width*4,0);var gray=new byte[width*height];
        for(int i=0;i<gray.Length;i++)gray[i]=(byte)(255-Math.Max(data[i*4],Math.Max(data[i*4+1],data[i*4+2])));
        var result=BitmapSource.Create(width,height,300,300,PixelFormats.Gray8,null,gray,width);result.Freeze();return result;
    }
    internal static RewardText[] GroupWords(IEnumerable<RewardText> words,double aspectRatio=1)
    {
        var rows=new List<List<RewardText>>();
        foreach(var word in words.OrderBy(x=>x.Y))
        {
            var row=rows.FirstOrDefault(r=>Math.Abs(r.Average(x=>x.Y+x.Height/2)-word.Y-word.Height/2)<Math.Max(word.Height*.65,.006));
            if(row is null)rows.Add([word]);else row.Add(word);
        }
        var output=new List<RewardText>();
        foreach(var row in rows)
        {
            // Punctuation and a partially detected Hangul glyph can have a tiny
            // box. Use the row's typical glyph height so that box cannot split
            // the weapon name from Prime/component into additional reward cards.
            double glyphHeight=row.Select(w=>w.Height).Order().ElementAt(row.Count/2);
            var group=new List<RewardText>();
            foreach(var word in row.OrderBy(x=>x.X))
            {
                // X and Y are normalized against different dimensions. A fixed
                // screen-width gap joins adjacent cards on ultrawide monitors.
                // Compare the gap to a glyph's physical height instead.
                if(group.Count>0&&word.X-group[^1].X-group[^1].Width>
                    Math.Clamp(Math.Max(glyphHeight,Math.Min(word.Height,group[^1].Height))*.85/aspectRatio,.006,.018)){Flush();group.Clear();}
                group.Add(word);
            }
            Flush();
            void Flush(){if(group.Count==0)return;double x=group.Min(w=>w.X),y=group.Min(w=>w.Y);output.Add(new(string.Join(" ",group.Select(w=>w.Text)),x,y,group.Max(w=>w.X+w.Width)-x,group.Max(w=>w.Y+w.Height)-y,group.Average(w=>w.Confidence)));}
        }
        return output.ToArray();
    }
}
