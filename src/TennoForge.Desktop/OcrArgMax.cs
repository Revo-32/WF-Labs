using System.Numerics;
using System.Runtime.CompilerServices;

namespace TennoForge.Desktop;

// Preserve the original strict comparison, first tie, signed-zero and NaN
// behavior. Vector blocks that cannot improve the current best need no scalar
// scan. This changes no logits, model weights, confidence or decoding rule.
internal static class OcrArgMax
{
    private static readonly bool ScalarOnly=Environment.GetEnvironmentVariable("WF_LABS_OCR_SCALAR")=="1";
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static int Find(ReadOnlySpan<float> values)
    {
        if(ScalarOnly||!Vector.IsHardwareAccelerated||values.Length<Vector<float>.Count*2)return Scalar(values);
        int best=0,i=1,width=Vector<float>.Count;float score=values[0];
        for(;i<=values.Length-width;i+=width)
        {
            if(!Vector.GreaterThanAny(new Vector<float>(values.Slice(i,width)),new Vector<float>(score)))continue;
            for(int c=i;c<i+width;c++)if(values[c]>score){best=c;score=values[c];}
        }
        for(;i<values.Length;i++)if(values[i]>score){best=i;score=values[i];}
        return best;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static int Scalar(ReadOnlySpan<float> values)
    {
        int best=0;float score=values[0];
        for(int c=1;c<values.Length;c++)if(values[c]>score){best=c;score=values[c];}
        return best;
    }
}
