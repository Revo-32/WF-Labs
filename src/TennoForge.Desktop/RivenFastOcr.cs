using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;
using Tesseract;
using Rect=System.Windows.Rect;

namespace TennoForge.Desktop;

internal static partial class RivenOcr
{
    // Only bounded cards enter this path. No screen-wide DL polling. A different
    // trained Tesseract observation must corroborate every literal option.
    private static RivenOcrResult? TryFastCard(byte[] pixels,int w,int h,string kind,int rank,CancellationToken token,bool focused)
    {
        if(!RivenLineRecognizer.Available||w>1000||h>1500||w<100||h<80)return null;
        using var scope=RivenPerformance.Measure("fast-card");var clock=Stopwatch.StartNew();
        var gray=FastMask(pixels,w,h);
        var inverse=new byte[pixels.Length];
        for(int i=0;i<pixels.Length;i++)inverse[i]=i%4==3?(byte)255:(byte)(255-pixels[i]);
        var original=BitmapSource.Create(w,h,300,300,PixelFormats.Bgra32,null,inverse,w*4);
        int footer=FastFooter(pixels,w,h);
        // An MR-anchored crop may already contain only the option text. Its
        // top rows are actual stats, not the artwork of a complete card.
        int startRow=focused||w>h*.95?0:(int)(h*.37);
        var rows=FastSegments(gray,w,h,startRow).Where(b=>b.Y<footer).ToList();if(rows.Count is <2 or >22)return null;
        var readings=new List<Line>();var lineImage=BitmapSource.Create(w,h,300,300,PixelFormats.Gray8,null,gray,w);
        var lineCrops=new List<BitmapSource>();var originalCrops=new List<BitmapSource>();var detailed=new List<RivenLineRecognizer.Reading>();
        foreach(var box in rows)
        {
            token.ThrowIfCancellationRequested();
            var crop=new CroppedBitmap(lineImage,box);
            var originalCrop=new CroppedBitmap(original,box);var read=RivenLineRecognizer.ReadDetailed(originalCrop);
            WriteDiagnostic(crop,$"fast-row-{box.Y}");RivenPerformance.Event("fast-row",new{read.Text,read.Confidence,box.X,box.Y,box.Width,box.Height});
            if(Regex.IsMatch(read.Text,@"\bMR\s*\d",RegexOptions.IgnoreCase))break;
            readings.Add(new(read.Text,new Rect(box.X,box.Y,box.Width,box.Height)));lineCrops.Add(crop);originalCrops.Add(originalCrop);detailed.Add(read);
        }
        var labels=RivenKindDetection.Kinds.SelectMany(RivenScan.AcceptedLabels).Select(s=>s.Replace(" ","")).Distinct().ToArray();
        bool TraitHint(string text)
        {string compact=Regex.Replace(text,@"\s","");return labels.Contains(compact)||labels.Any(compact.Contains)&&Regex.IsMatch(compact,@"\d|에대한피해");}
        int first=readings.FindIndex(r=>TraitHint(r.Text)||Regex.IsMatch(r.Text,@"\d+(?:[.,]\d+)?\s*%"));
        if(first<0)return null;
        int last=readings.FindLastIndex(r=>TraitHint(r.Text)||Regex.IsMatch(r.Text,@"\d+(?:[.,]\d+)?\s*(?:%|m|초)|[xX×]\s*\d+[.,]\d+"));
        while(last+1<readings.Count)
        {
            string continuation=Regex.Replace(readings[last+1].Text,@"\s","");
            if(continuation.Length==0||!(labels.Any(l=>l.Contains(continuation))||Regex.IsMatch(continuation,@"^공격시2배\)?$")))break;
            last++;
        }
        if(last<first)return null;
        readings.RemoveRange(last+1,readings.Count-last-1);lineCrops.RemoveRange(last+1,lineCrops.Count-last-1);
        originalCrops.RemoveRange(last+1,originalCrops.Count-last-1);detailed.RemoveRange(last+1,detailed.Count-last-1);
        string title=string.Join("\n",readings.Take(first).Select(r=>r.Text));
        // A general Korean reader sees a title only once. A domain model is
        // deliberately not allowed to invent a weapon from plausible stats.
        if(kind=="Auto"&&RivenKindDetection.WeaponKind(title) is null&&KoreanLineRecognizer.Available)
        {
            var titleReadings=new List<string>();
            for(int i=Math.Max(0,first-3);i<first;i++)
            {token.ThrowIfCancellationRequested();var header=KoreanLineRecognizer.Read(lineCrops[i]);if(header.Confidence>=.75)titleReadings.Add(header.Text);}
            var observed=string.Join("\n",titleReadings);
            if(RivenKindDetection.WeaponKind(observed) is not null)title=observed;
        }
        var options=readings.Skip(first).ToArray();
        if(options.Length==0)return null;
        string learned=string.Join("\n",options.Select(r=>r.Text));
        RivenScanReport Parse(string text)=>kind=="Auto"?RivenKindDetection.Resolve(text,rank,title):RivenScan.Parse(RivenScan.NormalizeOcrLabels(text,kind),kind,rank);
        var report=Parse(learned);
        var textBounds=options.Select((line,i)=>
        {
            var glyphs=detailed[first+i].Glyphs;
            if(glyphs.Length==0)return line.Box;
            double left=glyphs.Min(g=>g.Left),right=glyphs.Max(g=>g.Right);
            return new Rect(line.Box.X+left,line.Box.Y,Math.Max(1,right-left),line.Box.Height);
        }).Aggregate(Rect.Union);
        bool KindKnown(RivenScanReport candidate)=>kind!="Auto"||RivenKindDetection.WeaponKind(title) is not null||candidate.Valid&&
            (candidate.KindCandidates.Length==1||RivenKindDetection.Resolve(RivenScan.Editable(candidate),rank).KindCandidates is [var unique]&&unique==candidate.Kind);
        if(CompleteTranscription(report)&&KindKnown(report))
        {
            // Cross-check the text using a second color representation. This
            // is consistency checking, not an independent engine vote.
            var maskReadings=lineCrops.Skip(first).Select(c=>RivenLineRecognizer.ReadDetailed(c)).ToArray();
            var maskText=string.Join("\n",maskReadings.Select(r=>r.Text));var maskReport=Parse(maskText);
            var numericEvidence=new List<string>();
            static string Korean(string value)=>Regex.Replace(value,@"[^가-힣]","");
            bool sameLabels=CompleteTranscription(maskReport)&&maskReport.Options.SequenceEqual(report.Options)||Korean(maskText)==Korean(learned);
            var labelGlyphs=maskReadings.SelectMany(r=>r.Glyphs).Where(g=>Regex.IsMatch(g.Text,@"[가-힣]")).ToArray();
            bool agreement=sameLabels&&labelGlyphs.Length>0&&labelGlyphs.Average(g=>g.Confidence)>=.90&&
                detailed.Skip(first).Where(r=>r.Text.Length>0).All(r=>r.Confidence>=.90);
            if(agreement)
            {
                for(int i=first;i<detailed.Count;i++)
                    if(!VerifyLineNumbers(originalCrops[i],lineCrops[i],detailed[i],token,numericEvidence)){agreement=false;break;}
                if(agreement)
                {
                    var area=textBounds;
                    var numericCard=new RivenOcrCard(RivenScan.Editable(report),report,report.Valid,new Rect(area.X/w,area.Y/h,area.Width/w,area.Height/h),string.Join("\n",numericEvidence))
                    {TitleText=title,TranscriptionVerified=true};
                    return new([numericCard],clock.Elapsed.TotalSeconds,report.Valid?"옵션·숫자 교차 확인 완료":"원문 인식 완료 · 현재 수치 범위 확인 필요",title+"\nFAST LINE:\n"+learned+"\nMASK:\n"+maskText+"\nNUMERIC VERIFY:\n"+string.Join("\n",numericEvidence));
                }
            }
            RivenPerformance.Event("numeric-unresolved",new{learned,maskText,evidence=numericEvidence.ToArray()});
        }
        var bounds=textBounds;bounds.Inflate(3,3);bounds.Intersect(new Rect(0,0,w,h));
        var block=new CroppedBitmap(BitmapSource.Create(w,h,300,300,PixelFormats.Gray8,null,gray,w),
            new Int32Rect((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height));
        string Observe(BitmapSource source,PageSegMode mode)
        {
            token.ThrowIfCancellationRequested();using var pix=OcrPixels.Create(source);using var page=Engine().Process(pix,mode);return page.GetText();
        }
        string second=Observe(block,PageSegMode.SingleBlock);var corroborating=Parse(second);
        string third="";var verified=LiteralConsensus([report,corroborating],report.Kind,rank);
        // A raw-color observation preserves faint strokes that a color mask
        // can erase. Resizes of Tesseract still count as one engine family.
        string rawSecond="";
        if(verified is null)
        {
            rawSecond=Observe(new CroppedBitmap(original,new Int32Rect((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height)),PageSegMode.SingleBlock);
            verified=LiteralConsensus([report,Parse(rawSecond)],report.Kind,rank);
        }
        if(verified is null&&KoreanLineRecognizer.Available)
        {
            var lines=new List<string>();
            for(int i=first;i<lineCrops.Count;i++)
            {
                token.ThrowIfCancellationRequested();var read=KoreanLineRecognizer.Read(lineCrops[i]);
                if(Regex.IsMatch(read.Text,@"\bMR\s*\d",RegexOptions.IgnoreCase))break;
                lines.Add(read.Text);
            }
            third=string.Join("\n",lines);
            // Fine-tuned/base PP-OCR share a backbone; never count them as two
            // independent votes. Both alternatives still need Tesseract.
            verified=LiteralConsensus([Parse(third),corroborating],report.Kind,rank)??LiteralConsensus([Parse(third),Parse(rawSecond)],report.Kind,rank);
        }
        // A base-model fallback must not discard a trait already present in
        // the domain model's full reading merely to get a shorter agreement.
        if(verified is null||report.Options.Any(o=>!verified.Options.Any(v=>v.Key==o.Key)))
        {RivenPerformance.Event("fast-rejected",new{stage="verify",learned,second,rawSecond,third,title,report.Kind,report.Issues});return null;}
        if(!KindKnown(verified))return null;
        report=verified with{DetectedKind=kind=="Auto",KindCandidates=[verified.Kind],KindBasis=kind!="Auto"?"선택한 종류":RivenKindDetection.WeaponKind(title) is not null?"무기 이름":"옵션·수치 범위"};
        WriteDiagnostic(block,"fast-verified-block");
        var card=new RivenOcrCard(RivenScan.Editable(report),report,report.Valid,
            new Rect(textBounds.X/w,textBounds.Y/h,textBounds.Width/w,textBounds.Height/h),second+"\n"+third){TitleText=title,TranscriptionVerified=true};
        return new([card],clock.Elapsed.TotalSeconds,report.Valid?"옵션·수치 교차 확인 완료":"원문 인식 완료 · 현재 수치 범위 확인 필요",title+"\nFAST LINE:\n"+learned+"\nFAST VERIFY:\n"+second+"\nFAST KOREAN:\n"+third);
    }

    internal static bool CompleteTranscription(RivenScanReport report)=>report.TranscriptionIssues.Length==0&&report.Options.Length is >=2 and <=4&&
        report.Options.Select(o=>o.Key).Distinct().Count()==report.Options.Length&&report.Options.All(DisplayedPrecision);

    private static bool VerifyLineNumbers(BitmapSource original,BitmapSource mask,RivenLineRecognizer.Reading reading,CancellationToken token,List<string> evidence)
    {
        static string Canonical(string s)=>Regex.Replace(s,@"\s","").Replace('×','x').Replace('X','x').Replace('−','-').Replace('–','-').Replace(',','.');
        var matches=Regex.Matches(reading.Text,@"(?:[+−–－-]\s*\d+(?:[.,]\d+)?\s*%?|[xX×]\s*\d+(?:[.,]\d+)?)");
        foreach(Match match in matches)
        {
            int offset=0;var glyphs=new List<RivenLineRecognizer.Glyph>();
            foreach(var g in reading.Glyphs){if(offset<match.Index+match.Length&&offset+g.Text.Length>match.Index)glyphs.Add(g);offset+=g.Text.Length;}
            if(glyphs.Count==0)return false;
            int left=Math.Max(0,(int)Math.Floor(glyphs.Min(g=>g.Left))-2),right=Math.Min(original.PixelWidth,(int)Math.Ceiling(glyphs.Max(g=>g.Right))+2);
            bool found=false,numberSeen=false,unitSeen=!match.Value.EndsWith('%');
            string expected=Canonical(match.Value),expectedNumber=expected.TrimEnd('%');
            foreach(var engine in new[]{Engine(latin:true),Engine()})
            {
                try
                {
                    engine.SetVariable("tessedit_char_whitelist","0123456789+-.,%xX");
                    foreach(var source in new[]{original,mask})
                    {
                        var crop=new CroppedBitmap(source,new Int32Rect(left,0,Math.Max(1,right-left),source.PixelHeight));
                        var enlarged=new TransformedBitmap(crop,new ScaleTransform(Math.Max(1,48d/crop.PixelHeight),Math.Max(1,48d/crop.PixelHeight)));
                        WriteDiagnostic(enlarged,"numeric-"+Canonical(match.Value));
                        foreach(var mode in new[]{PageSegMode.SingleLine,PageSegMode.RawLine})
                        {
                            token.ThrowIfCancellationRequested();using var pix=OcrPixels.Create(enlarged);using var page=engine.Process(pix,mode);var observed=page.GetText();
                            evidence.Add(Canonical(match.Value)+" = "+observed.Trim());
                            string actual=Canonical(observed);
                            if(actual.TrimEnd('%')==expectedNumber)numberSeen=true;
                            // Sign/value and the printed percent glyph can be
                            // confirmed by separate observations of this same
                            // fixed numeric crop. Neither is guessed from the
                            // stat's allowed range or from a different row.
                            if(actual.EndsWith('%')&&actual.TrimEnd('%').TrimStart('+','-')==expectedNumber.TrimStart('+','-'))unitSeen=true;
                            if(numberSeen&&unitSeen){found=true;break;}
                        }
                        if(found)break;
                    }
                }
                finally{engine.SetVariable("tessedit_char_whitelist","");}
                if(found)break;
            }
            if(!found)return false;
        }
        return true;
    }

    // One vote per independent engine, not per threshold/resize of that engine.
    // Values, signs, units and order are copied literally from actual readings.
    internal static RivenScanReport? LiteralConsensus(IReadOnlyList<RivenScanReport> evidence,string kind,int rank)
    {
        var seen=new HashSet<RivenScanReport>(ReferenceEqualityComparer.Instance);
        var readings=evidence.Where(r=>r.Kind==kind&&seen.Add(r)).ToArray();
        var agreed=readings.SelectMany(r=>r.Options).Where(DisplayedPrecision).Distinct()
            .Where(o=>readings.Count(r=>r.Options.Contains(o))>=2).ToArray();
        if(agreed.Length is <2 or >4||agreed.Select(o=>o.Key).Distinct().Count()!=agreed.Length)return null;
        if(readings.Any(r=>r.Options.Length>agreed.Length))return null;
        var order=readings.Where(r=>r.Options.Length==agreed.Length&&r.Options.Select(o=>o.Key).Distinct().Count()==agreed.Length&&
            r.Options.All(o=>agreed.Any(a=>a.Key==o.Key))).OrderBy(r=>r.TranscriptionIssues.Length).FirstOrDefault();
        if(order is null||order.TranscriptionIssues.Length>0)return null;
        var options=order.Options.Select(o=>agreed.Single(a=>a.Key==o.Key)).ToArray();
        return RivenScan.Analyze(options,kind,rank);
    }

    private static int FastFooter(byte[] pixels,int w,int h)
    {
        for(int y=(int)(h*.62);y<h*.93;y++)
        {
            int count=0,columns=0;
            for(int x=(int)(w*.25);x<w*.75;x++)
            {
                int p=(y*w+x)*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];columns++;
                int above=pixels[(Math.Max(0,y-3)*w+x)*4+2],below=pixels[(Math.Min(h-1,y+3)*w+x)*4+2];
                if(r>16&&r-g>4&&b-r>2&&r>(above+below)/2d+8)count++;
            }
            if(count>columns*.70)return y;
        }
        return h;
    }

    private static byte[] FastMask(byte[] pixels,int w,int h)
    {
        var reds=new int[256];var chromas=new int[511];int samples=0;
        for(int y=(int)(h*.52);y<(int)(h*.83);y++)for(int x=(int)(w*.15);x<(int)(w*.85);x++)
        {int p=(y*w+x)*4;reds[pixels[p+2]]++;chromas[pixels[p]-pixels[p+1]+255]++;samples++;}
        static int Percentile(int[] histogram,int count)
        {int sum=0;for(int i=0;i<histogram.Length;i++){sum+=histogram[i];if(sum>=Math.Max(1,(int)Math.Ceiling(count*.93)))return i;}return histogram.Length-1;}
        double redFloor=Math.Max(28,Percentile(reds,samples)*.55),chromaFloor=Math.Max(8,Math.Max(20,Percentile(chromas,samples)-255)*.25);
        var gray=new byte[w*h];
        for(int i=0;i<gray.Length;i++)
        {
            int p=i*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];
            int hi=Math.Max(r,Math.Max(g,b)),lo=Math.Min(r,Math.Min(g,b));
            bool neutral=hi>60&&hi-lo<22,purple=r>redFloor&&r-g>3&&b-r>3&&b-g>chromaFloor;
            int contrast=neutral?(hi-35)*4:((r-g)+(b-r)-6)*8;
            gray[i]=neutral||purple?(byte)(255-Math.Clamp(contrast,0,255)):(byte)255;
        }
        return gray;
    }

    private static List<Int32Rect> FastSegments(byte[] gray,int w,int h,int startRow)
    {
        var seen=new bool[gray.Length];var heights=new List<int>();var points=new List<int>();
        for(int start=0;start<gray.Length;start++)
        {
            if(gray[start]>=200||seen[start])continue;
            points.Clear();points.Add(start);seen[start]=true;int l=w,r=0,t=h,b=0;
            for(int n=0;n<points.Count;n++)
            {
                int p=points[n],x=p%w,y=p/w;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int q=yy*w+xx;if(gray[q]<200&&!seen[q]){seen[q]=true;points.Add(q);}}
            }
            int width=r-l+1,height=b-t+1;
            if(height>=5&&height<h*.12&&width>=2&&width<w*.15&&(t+b)/2d>h*.4&&(t+b)/2d<h*.86&&points.Count>=height*1.5)heights.Add(height);
        }
        heights.Sort();double glyph=heights.Count==0?Math.Max(8,h*.04):heights[Math.Min(heights.Count-1,(int)(heights.Count*.65))];
        // Do not remove a component that could contain text. Compression can
        // join a genuine glyph to a frame fragment; center projection locates
        // rows without deleting those strokes.
        var projection=new int[h];for(int y=startRow;y<h;y++)for(int x=(int)(w*.25);x<w*.75;x++)if(gray[y*w+x]<200)projection[y]++;
        var spans=new List<(int Top,int Bottom)>();int top=-1;
        for(int y=0;y<=h;y++)
        {
            bool ink=y<h&&projection[y]>=Math.Max(5,glyph*.38);
            if(ink&&top<0)top=y;
            if(!ink&&top>=0)
            {
                int height=y-top;
                if(height>=Math.Max(3,(int)(glyph*.35)))
                {
                    if(height<=glyph*1.65)spans.Add((top,y));
                    else if(height<=glyph*3.4)
                    {
                        int middle=(top+y)/2,radius=(int)(glyph*.25),split=middle;
                        for(int j=Math.Max(top+3,middle-radius);j<Math.Min(y-3,middle+radius);j++)if(projection[j]<projection[split])split=j;
                        spans.Add((top,split));spans.Add((split+1,y));
                    }
                }
                top=-1;
            }
        }
        var result=new List<Int32Rect>();
        foreach(var row in spans)
        {
            int y0=Math.Max(0,row.Top-2),y1=Math.Min(h,row.Bottom+2);var groups=new List<(int L,int R)>();int first=-1,last=-1;
            for(int x=0;x<w;x++)
            {
                bool ink=false;for(int y=y0;y<y1;y++)if(gray[y*w+x]<200){ink=true;break;}
                if(!ink)continue;
                if(first>=0&&x-last>Math.Max(5,(int)(glyph*.5))){groups.Add((first,last+1));first=-1;}
                if(first<0)first=x;last=x;
            }
            if(first>=0)groups.Add((first,last+1));
            groups=groups.Where(g=>!(g.L<w*.1&&g.R-g.L<glyph*.8||g.R>w*.9&&g.R-g.L<glyph*.8)).ToList();
            if(groups.Count==0)continue;
            int l=Math.Max(0,groups[0].L-2),r=Math.Min(w,groups[^1].R+2);
            if(r-l>=glyph*.7)result.Add(new(l,y0,r-l,y1-y0));
        }
        return result;
    }
}
