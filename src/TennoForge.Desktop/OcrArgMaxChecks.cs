using System.Diagnostics;
using System.Numerics;

namespace TennoForge.Desktop;

internal static class OcrArgMaxChecks
{
    internal static object Run()
    {
        var random=new Random(20261030);int checks=0;
        void Check(float[] values)
        {
            int scalar=OcrArgMax.Scalar(values),simd=OcrArgMax.Find(values);
            if(scalar!=simd||BitConverter.SingleToInt32Bits(values[scalar])!=BitConverter.SingleToInt32Bits(values[simd]))
                throw new InvalidOperationException("OCR 최댓값 선택의 기존 결과와 SIMD 결과가 다릅니다.");
            checks++;
        }
        foreach(int length in new[]{1,2,7,8,9,15,16,17,31,11947})
        {
            Check(new float[length]);
            var values=Enumerable.Range(0,length).Select(_=>(float)(random.NextDouble()*2-1)).ToArray();Check(values);
            for(int i=0;i<Math.Min(40,length);i++)
            {
                var sample=(float[])values.Clone();sample[i]=float.NaN;Check(sample);
                sample[i]=float.PositiveInfinity;sample[^1]=float.PositiveInfinity;Check(sample);
                sample[i]=0;sample[0]=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));Check(sample);
            }
        }
        const int rows=96,classes=11947;var matrix=new float[rows][];
        for(int r=0;r<rows;r++)
        {matrix[r]=Enumerable.Range(0,classes).Select(_=>(float)random.NextDouble()*.001f).ToArray();matrix[r][r%3==0?0:random.Next(classes)]=.99f;Check(matrix[r]);}
        long Scan(bool vector)
        {
            long sum=0;for(int repeat=0;repeat<12;repeat++)foreach(var row in matrix)sum+=vector?OcrArgMax.Find(row):OcrArgMax.Scalar(row);return sum;
        }
        Scan(false);Scan(true);var runs=new List<object>();
        foreach(bool vector in new[]{false,true,true,false})
        {var clock=Stopwatch.StartNew();long checksum=Scan(vector);clock.Stop();runs.Add(new{vector,milliseconds=clock.Elapsed.TotalMilliseconds,checksum});}
        return new{passed=true,checks,vectorAvailable=Vector.IsHardwareAccelerated,vectorWidth=Vector<float>.Count,runs};
    }
}
