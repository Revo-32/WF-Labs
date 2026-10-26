using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TennoForge.Desktop;

// Called only inside the serial Riven OCR gate, never on visual timer ticks.
// PP-OCRv5's Korean text-line model, CPU only, with its embedded character map.
internal static class KoreanLineRecognizer
{
    private static InferenceSession? session;
    private static readonly object Sync=new();
    private static string[] characters=[];
    internal static bool Available=>File.Exists(Path.Combine(OcrModels.DirectoryPath,"korean-line-v5.onnx"));
    internal static (string Text,double Confidence) Read(BitmapSource source)
    {
        lock(Sync)
        {
            try{return ReadCore(source);}
            catch(Exception ex)when(ex is OnnxRuntimeException or DllNotFoundException)
            {throw new InvalidOperationException("한국어 보조 인식 모델을 실행하지 못했습니다. 앱 파일을 확인하세요.",ex);}
        }
    }
    private static (string Text,double Confidence) ReadCore(BitmapSource source)
    {
        using var scope=RivenPerformance.Measure("korean-line",new{source.PixelWidth,source.PixelHeight});
        if(!Available)return ("",0);
        if(session is null)
        {
            using var options=new SessionOptions{IntraOpNumThreads=2,InterOpNumThreads=1,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
            options.AddSessionConfigEntry("session.intra_op.allow_spinning","0");
            session=new InferenceSession(Path.Combine(OcrModels.DirectoryPath,"korean-line-v5.onnx"),options);
            characters=new[]{""}.Concat(session.ModelMetadata.CustomMetadataMap["character"].TrimEnd('\r','\n').Split('\n')).Append(" ").ToArray();
            AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{lock(Sync)session?.Dispose();};
        }
        const int height=48;
        int resizedWidth=Math.Clamp((int)Math.Ceiling(height*source.PixelWidth/(double)source.PixelHeight),1,3200),width=Math.Max(320,resizedWidth);
        var resized=new TransformedBitmap(source,new ScaleTransform(resizedWidth/(double)source.PixelWidth,height/(double)source.PixelHeight));
        var bgra=new FormatConvertedBitmap(resized,PixelFormats.Bgra32,null,0);
        int stride=bgra.PixelWidth*4;var pixels=new byte[stride*bgra.PixelHeight];bgra.CopyPixels(pixels,stride,0);
        var tensor=new DenseTensor<float>(new[]{1,3,height,width});
        var input=tensor.Buffer.Span;
        for(int y=0;y<height;y++)for(int x=0;x<resizedWidth;x++)for(int c=0;c<3;c++)
            input[(c*height+y)*width+x]=pixels[Math.Min(y,bgra.PixelHeight-1)*stride+Math.Min(x,bgra.PixelWidth-1)*4+c]/127.5f-1;
        using var outputs=session.Run(new[]{NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),tensor)});
        var output=outputs.First().AsTensor<float>();var logits=output.ToArray();int classes=output.Dimensions[2];var tokens=new List<(string Text,float Score)>();int previous=-1;
        for(int t=0;t<output.Dimensions[1];t++)
        {
            int offset=t*classes,best=0;float score=logits[offset];
            for(int i=1;i<classes;i++)if(logits[offset+i]>score){best=i;score=logits[offset+i];}
            if(best!=0&&best!=previous)tokens.Add((characters[best],score));previous=best;
        }
        int first=0,last=tokens.Count;
        while(first<last&&tokens[first].Score<.65)first++;
        while(last>first&&tokens[last-1].Score<.65)last--;
        var retained=tokens.Skip(first).Take(last-first).ToArray();
        return (string.Concat(retained.Select(t=>t.Text)),retained.Length==0?0:retained.Average(t=>(double)t.Score));
    }
}
