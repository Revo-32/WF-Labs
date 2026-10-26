using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using TennoForge.Core;

namespace TennoForge.Desktop;

// Event-driven debug output, with incremental file tailing as a fallback.
// No game memory reads, input injection, screen polling, or uploaded log contents.
internal sealed class RelicAutoDetector : IDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly RelicRewardSignals signals=new();
    private readonly Action<RelicRewardSignal> notify;
    private readonly Action<string> status;
    private readonly Action<string>? observed;
    private readonly string path;
    private readonly string debugPrefix;
    private readonly Func<uint,bool> acceptProcess;
    private readonly Task fileTask,debugTask;
    private readonly object sync=new();
    private bool disposed;
    internal RelicAutoDetector(Action<RelicRewardSignal> notify,Action<string> status,string? logPath=null,bool debugOutput=true,string debugPrefix="DBWIN",Func<uint,bool>? acceptProcess=null,Action<string>? observed=null)
    {
        this.notify=notify;this.status=status;this.observed=observed;
        this.debugPrefix=debugPrefix;this.acceptProcess=acceptProcess??IsWarframe;
        path=logPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Warframe","EE.log");
        // Snapshot before starting: old rewards must not trigger on enable/restart.
        long offset=0;DateTime stamp=default;
        try{var info=new FileInfo(path);if(info.Exists){offset=info.Length;stamp=info.CreationTimeUtc;}}catch(IOException){}catch(UnauthorizedAccessException){}
        fileTask=Task.Run(()=>Tail(offset,stamp));
        debugTask=debugOutput?Task.Factory.StartNew(DebugOutput,TaskCreationOptions.LongRunning):Task.CompletedTask;
    }
    private static bool IsWarframe(uint pid)
    {
        try{using var process=Process.GetProcessById((int)pid);return process.ProcessName is "Warframe.x64" or "Warframe";}
        catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}catch(System.ComponentModel.Win32Exception){return false;}
    }
    private void Feed(string line)
    {
        lock(sync)
        {
            if(disposed)return;
            observed?.Invoke(line);
            var signal=signals.Accept(line,Environment.TickCount64);
            if(signal!=RelicRewardSignal.None)notify(signal);
        }
    }
    private async Task Tail(long offset,DateTime stamp)
    {
        string pending="";
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    var info=new FileInfo(path);
                    if(info.Exists)
                    {
                        if(info.Length<offset||info.CreationTimeUtc!=stamp){offset=0;pending="";stamp=info.CreationTimeUtc;}
                        if(info.Length>offset)
                        {
                            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                            // Bound work even if the game logs a large diagnostic dump.
                            if(input.Length-offset>262144){offset=input.Length-262144;pending="";}
                            input.Position=offset;using var reader=new StreamReader(input,Encoding.UTF8,true,4096);
                            var text=pending+await reader.ReadToEndAsync(stop.Token);offset=input.Position;
                            int end=text.LastIndexOf('\n');
                            if(end>=0){foreach(var line in text[..end].Split('\n'))Feed(line.TrimEnd('\r'));pending=text[(end+1)..];}
                            else pending=text;
                            if(pending.Length>8192)pending="";
                        }
                    }
                }
                catch(Exception e)when(e is IOException or UnauthorizedAccessException){ /* Retry on rotation/temporary lock. */ }
                await Task.Delay(500,stop.Token);
            }
        }
        catch(OperationCanceledException){}
    }
    private void DebugOutput()
    {
        try
        {
            // This Windows debug channel has one consumer. Never steal it from
            // AlecaFrame, WFInfo, DebugView, or another running WF Labs instance.
            using var ready=new EventWaitHandle(false,EventResetMode.AutoReset,debugPrefix+"_BUFFER_READY",out bool owns);
            if(!owns){status("자동 인식 대기 · 로그 파일 감지 사용 (다른 앱이 실시간 로그 사용 중)");return;}
            using var data=new EventWaitHandle(false,EventResetMode.AutoReset,debugPrefix+"_DATA_READY");
            using var memory=MemoryMappedFile.CreateOrOpen(debugPrefix+"_BUFFER",4096,MemoryMappedFileAccess.ReadWrite);
            using var view=memory.CreateViewAccessor(0,4096,MemoryMappedFileAccess.Read);
            status("자동 인식 대기 · 보상 화면에서 자동 표시");
            var bytes=new byte[4092];WaitHandle[] waits=[stop.Token.WaitHandle,data];
            while(!stop.IsCancellationRequested)
            {
                ready.Set();if(WaitHandle.WaitAny(waits)!=1)break;
                uint pid=view.ReadUInt32(0);view.ReadArray(4,bytes,0,bytes.Length);
                int end=Array.IndexOf(bytes,(byte)0);var line=Encoding.UTF8.GetString(bytes,0,end<0?bytes.Length:end);
                // Filter before looking up the process; irrelevant debug traffic
                // never triggers an expensive scan of all running processes.
                if(RelicRewardSignals.Classify(line)==RelicRewardSignal.None)continue;
                if(acceptProcess(pid))Feed(line);
            }
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {status("자동 인식 대기 · 로그 파일 감지 사용 (실시간 로그 연결 불가)");}
    }
    public void Dispose()
    {
        lock(sync){if(disposed)return;disposed=true;stop.Cancel();}
        _=Task.WhenAll(fileTask,debugTask).ContinueWith(_=>stop.Dispose(),TaskScheduler.Default);
    }
}
