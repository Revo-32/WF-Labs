namespace TennoForge.Desktop;

// A small atomic signal lets background inventory work yield at chunk boundaries
// without querying a WPF property or depending on which capture window started OCR.
internal static class OcrActivity
{
    private static int active;
    internal static bool IsBusy=>Volatile.Read(ref active)>0;
    internal static IDisposable Enter(){Interlocked.Increment(ref active);return new Lease();}
    private sealed class Lease:IDisposable
    {
        private int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)Interlocked.Decrement(ref active);}
    }
}
