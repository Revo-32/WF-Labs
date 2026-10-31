using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TennoForge.Desktop;

// Domain fine-tuning of the Korean PP-OCRv5 CTC classifier. The pretrained
// backbone is preserved; Python/CUDA are never runtime dependencies.
internal static class RivenLineRecognizer
{
    internal record Glyph(string Text,double Confidence,double Left,double Right);
    internal record Reading(string Text,double Confidence,Glyph[] Glyphs);
    private static readonly object Sync=new();
    private static InferenceSession? session;
    private static string[] characters=[];
    internal static bool Available=>Environment.GetEnvironmentVariable("WF_LABS_RIVEN_LINE")!="0"&&File.Exists(Path.Combine(OcrModels.DirectoryPath,"riven-line.onnx"));
    internal static (string Text,double Confidence) Read(BitmapSource source)
    {var reading=ReadDetailed(source);return (reading.Text,reading.Confidence);}
    internal static Reading ReadDetailed(BitmapSource source)
    {
        try{return ReadCore(source);}
        catch(Exception ex)when(ex is OnnxRuntimeException or DllNotFoundException or JsonException or KeyNotFoundException)
        {throw new InvalidOperationException("추가 학습 리벤 OCR 모델을 실행하지 못했습니다. 앱 파일을 확인하세요.",ex);}
    }
    private static Reading ReadCore(BitmapSource source)
    {
        if(!Available)return new("",0,[]);
        lock(Sync)
        {
            using var scope=RivenPerformance.Measure("trained-riven-line",new{source.PixelWidth,source.PixelHeight});
            if(session is null)
            {
                using var options=new SessionOptions{IntraOpNumThreads=2,InterOpNumThreads=1,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
                options.AddSessionConfigEntry("session.intra_op.allow_spinning","0");
                session=new InferenceSession(Path.Combine(OcrModels.DirectoryPath,"riven-line.onnx"),options);
                characters=JsonSerializer.Deserialize<string[]>(session.ModelMetadata.CustomMetadataMap["characters"])??throw new InvalidOperationException("리벤 OCR 문자 목록이 없습니다.");
                if(session.OutputMetadata.First().Value.Dimensions[^1]!=characters.Length+1)
                    throw new InvalidOperationException("리벤 OCR 모델과 문자 목록이 일치하지 않습니다.");
                if(session.ModelMetadata.CustomMetadataMap["architecture"]!="PP-OCRv5 Korean CTC classifier fine-tune")
                    throw new InvalidOperationException("리벤 OCR 모델 형식이 올바르지 않습니다.");
                AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{lock(Sync)session?.Dispose();};
            }
            const int height=48;
            int resizedWidth=Math.Clamp((int)Math.Ceiling(height*source.PixelWidth/(double)source.PixelHeight),1,3200),width=Math.Max(320,resizedWidth);
            var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
            int sw=bitmap.PixelWidth,sh=bitmap.PixelHeight;var pixels=new byte[sw*sh*4];bitmap.CopyPixels(pixels,sw*4,0);
            var input=new DenseTensor<float>(new[]{1,3,height,width});var buffer=input.Buffer.Span;
            for(int y=0;y<height;y++)for(int x=0;x<resizedWidth;x++)for(int c=0;c<3;c++)
            {
                // Explicit half-pixel bilinear sampling matches the training
                // input. WPF's bitmap scaler can use a different interpolation
                // path, a material difference on 6–9 pixel web-image glyphs.
                double sx=Math.Clamp((x+.5)*sw/resizedWidth-.5,0,sw-1),sy=Math.Clamp((y+.5)*sh/height-.5,0,sh-1);
                int x0=(int)sx,y0=(int)sy,x1=Math.Min(sw-1,x0+1),y1=Math.Min(sh-1,y0+1);double fx=sx-x0,fy=sy-y0;
                double top=pixels[(y0*sw+x0)*4+c]*(1-fx)+pixels[(y0*sw+x1)*4+c]*fx;
                double bottom=pixels[(y1*sw+x0)*4+c]*(1-fx)+pixels[(y1*sw+x1)*4+c]*fx;
                buffer[(c*height+y)*width+x]=(float)((top*(1-fy)+bottom*fy)/127.5-1);
            }
            using var outputs=session.Run(new[]{NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),input)});
            var output=outputs.First().AsTensor<float>();ReadOnlySpan<float> logits=output is DenseTensor<float> dense?dense.Buffer.Span:output.ToArray();
            int classes=output.Dimensions[2],last=-1;double confidence=0;var text=new System.Text.StringBuilder();int count=0;
            var glyphs=new List<Glyph>();double cell=width/(double)output.Dimensions[1]*source.PixelWidth/resizedWidth;
            for(int t=0;t<output.Dimensions[1];t++)
            {
                int offset=t*classes,best=OcrArgMax.Find(logits.Slice(offset,classes));float score=logits[offset+best];
                if(best!=0&&best!=last)
                {
                    text.Append(characters[best-1]);confidence+=score;count++;
                    glyphs.Add(new(characters[best-1],score,Math.Max(0,(t-.75)*cell),Math.Min(source.PixelWidth,(t+1.75)*cell)));
                }
                else if(best!=0&&best==last&&glyphs.Count>0)glyphs[^1]=glyphs[^1] with{Right=Math.Min(source.PixelWidth,(t+1.75)*cell)};
                last=best;
            }
            return new(text.ToString(),count==0?0:confidence/count,glyphs.ToArray());
        }
    }
}
