using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal sealed record InventoryReadResult(InventorySnapshot? Snapshot,string Message,long BytesRead,double Seconds,int Regions=0,int Candidates=0,bool CompletePass=false,InventoryCandidateInfo? Diagnostic=null,bool Deferred=false);
internal sealed class InventoryMemoryReader : IDisposable
{
    private const int ReadStep=1024*1024,Overlap=2*1024*1024;
    private readonly object stateSync=new();
    private bool reading;
    private volatile bool disposed;
    private int processId,regionIndex;
    private long processStart;
    private ulong regionOffset,cached,queryCursor;
    private int cachedSize;
    private DateTimeOffset cacheTime;
    private bool searchStarted,enumerationComplete;
    private List<(ulong Start,ulong Length)> regions=[];

    internal Task<InventoryReadResult> ReadAsync(InventoryCatalog catalog,CancellationToken token,bool automatic=false,Func<bool>? yieldRequested=null)=>RunAsync(catalog,token,false,automatic,yieldRequested);
    internal Task<InventoryReadResult> ReadSelfForCheck(InventoryCatalog catalog,CancellationToken token,bool automatic=false,Func<bool>? yieldRequested=null)=>RunAsync(catalog,token,true,automatic,yieldRequested);
    internal void SeedSelfRangeForCheck(nint address,int length)
    {
        if(address==0||length<=0)throw new ArgumentOutOfRangeException(nameof(length));
        lock(stateSync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);if(reading)throw new InvalidOperationException("인벤토리 확인이 진행 중입니다.");
            using var self=Process.GetCurrentProcess();Reset();processId=self.Id;processStart=self.StartTime.ToUniversalTime().Ticks;
            regions.Add(((ulong)address,(ulong)length));searchStarted=enumerationComplete=true;
        }
    }
    private async Task<InventoryReadResult> RunAsync(InventoryCatalog catalog,CancellationToken token,bool selfCheck,bool automatic,Func<bool>? yieldRequested)
    {
        lock(stateSync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(reading)return new(null,"기존 인벤토리 확인이 끝나면 이어서 확인합니다.",0,0,Deferred:true);
            reading=true;
        }
        try
        {
            return await Task.Run(()=>
            {
                var thread=Thread.CurrentThread;var old=thread.Priority;
                try{thread.Priority=ThreadPriority.BelowNormal;return Read(catalog,token,selfCheck,automatic,yieldRequested);}
                finally{thread.Priority=old;}
            },token).ConfigureAwait(false);
        }
        finally
        {
            lock(stateSync){reading=false;if(disposed||token.IsCancellationRequested)Reset();}
        }
    }
    private void Reset()
    {processId=0;processStart=0;regions.Clear();regionIndex=0;regionOffset=0;cached=0;cachedSize=0;cacheTime=default;queryCursor=0x10000;searchStarted=false;enumerationComplete=false;}
    private void BeginSearch()
    {regions.Clear();regionIndex=0;regionOffset=0;queryCursor=0x10000;searchStarted=true;enumerationComplete=false;}
    private void CheckStop(CancellationToken token,Func<bool>? yieldRequested)
    {
        token.ThrowIfCancellationRequested();
        if(disposed)throw new OperationCanceledException("인벤토리 확인을 종료했습니다.",token);
        if(yieldRequested?.Invoke()==true)throw new ScanYieldException();
    }
    private sealed class ScanYieldException : Exception {}
    private sealed class ScanProcessGoneException : Exception {}
    private sealed class ScanBudgetException : Exception {}
    private static Process? GameProcess()
    {
        foreach(var name in new[]{"Warframe.x64","Warframe"})
        {
            Process? selected=null;
            foreach(var candidate in Process.GetProcessesByName(name))
            {if(selected is null)selected=candidate;else candidate.Dispose();}
            if(selected is not null)return selected;
        }
        return null;
    }
    private InventoryReadResult Read(InventoryCatalog catalog,CancellationToken token,bool selfCheck,bool automatic,Func<bool>? yieldRequested)
    {
        var clock=Stopwatch.StartNew();long read=0;int visited=0,candidates=0;InventoryCandidateInfo? diagnostic=null;
        var budget=TimeSpan.FromMilliseconds(automatic?250:6000);long byteBudget=automatic?128L*1024*1024:8L*1024*1024*1024;
        InventoryReadResult Deferred(string message)=>new(null,message,read,clock.Elapsed.TotalSeconds,visited,candidates,false,diagnostic,true);
        try
        {
            CheckStop(token,yieldRequested);
            using var game=selfCheck?Process.GetCurrentProcess():GameProcess();
            if(game is null){Reset();return new(null,"워프레임 실행을 기다리고 있습니다. 마지막 저장 목록은 유지합니다.",0,clock.Elapsed.TotalSeconds);}
            long started=game.StartTime.ToUniversalTime().Ticks;
            if(game.Id!=processId||started!=processStart){Reset();processId=game.Id;processStart=started;}
            // Query + VM_READ only. No writes, injected modules or account API.
            using var handle=OpenProcess(0x0410,false,processId);
            if(handle.IsInvalid)return new(null,"게임 데이터 읽기 권한을 얻지 못했습니다. 저장 목록은 유지합니다.",0,clock.Elapsed.TotalSeconds);
            void CheckReadStop(int bytes=0)
            {
                CheckStop(token,yieldRequested);if(game.HasExited)throw new ScanProcessGoneException();
                if(bytes>0&&read>byteBudget-bytes)throw new ScanBudgetException();
            }
            bool Stable(ulong address,ReadOnlySpan<byte> expected)
            {
                var again=new byte[Math.Min(ReadStep,expected.Length)];
                try
                {
                    for(int offset=0;offset<expected.Length;offset+=again.Length)
                    {
                        int length=Math.Min(again.Length,expected.Length-offset);CheckReadStop(length);
                        bool ok=ReadProcessMemory(handle,(nint)(address+(ulong)offset),again,(nuint)length,out var got);read+=(long)got;
                        if(!ok||got!=(nuint)length||!expected.Slice(offset,length).SequenceEqual(again.AsSpan(0,length)))return false;
                    }
                    return true;
                }
                finally{Array.Clear(again);}
            }
            int ReadRange(ulong origin,byte[] target,int length,bool skipHoles=false)
            {
                var portion=new byte[Math.Min(ReadStep,length)];int offset=0;
                try
                {
                    while(offset<length)
                    {
                        CheckReadStop();ulong address=origin+(ulong)offset;int size=Math.Min(portion.Length,length-offset);
                        if(skipHoles)
                        {
                            if(VirtualQueryEx(handle,(nint)address,out var part,(nuint)Marshal.SizeOf<Region>())==0)break;
                            ulong end=(ulong)part.BaseAddress+(ulong)part.RegionSize;if(end<=address)break;
                            int remaining=(int)Math.Min((ulong)(length-offset),end-address);
                            if(part.State!=0x1000||(part.Protect&0x100)!=0||(part.Protect&0xff) is not (2 or 4 or 8)){offset+=remaining;continue;}
                            size=Math.Min(size,remaining);
                        }
                        CheckReadStop(size);ReadProcessMemory(handle,(nint)address,portion,(nuint)size,out var got);int count=(int)got;read+=count;
                        if(count>0)portion.AsSpan(0,count).CopyTo(target.AsSpan(offset));
                        if(count<size&&!skipHoles)return offset+count;
                        offset+=size;
                    }
                    return offset;
                }
                finally{Array.Clear(portion);}
            }
            if(cached!=0&&DateTimeOffset.UtcNow-cacheTime<TimeSpan.FromSeconds(10))
            {
                var bytes=new byte[cachedSize];
                try
                {
                    if(ReadRange(cached,bytes,bytes.Length)==bytes.Length)
                    {
                        CheckReadStop();var hit=InventoryJsonLocator.Find(bytes,catalog);
                        if(hit is {} found&&Stable(cached,bytes))return new(found.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds);
                    }
                }
                finally{Array.Clear(bytes);}
                cached=0;
            }
            else if(cached!=0){cached=0;BeginSearch();}
            if(!searchStarted||enumerationComplete&&regionIndex>=regions.Count)BeginSearch();
            if(!enumerationComplete)
            {
                while(clock.Elapsed<budget&&read<byteBudget)
                {
                    CheckReadStop();
                    if(VirtualQueryEx(handle,(nint)queryCursor,out var info,(nuint)Marshal.SizeOf<Region>())==0){enumerationComplete=true;break;}
                    ulong start=(ulong)info.BaseAddress,length=(ulong)info.RegionSize,end=start+length;
                    if(end<=queryCursor){enumerationComplete=true;break;}
                    uint access=info.Protect&0xff;
                    if(info.State==0x1000&&info.Type==0x20000&&(info.Protect&0x100)==0&&access is 2 or 4 or 8)regions.Add((start,length));
                    queryCursor=end;
                }
                if(!enumerationComplete)return Deferred($"인벤토리 메모리 영역 확인 중 · {regions.Count:N0}개 · 다음 시도에서 이어서 확인합니다.");
                // Preserve the complete searchable range; only scheduling is sliced.
                regions=regions.OrderBy(r=>r.Length is >=65536 and <=16*1024*1024?0:r.Length>16*1024*1024?1:2).ThenByDescending(r=>r.Length).ToList();
            }
            int chunk=automatic?ReadStep:4*ReadStep;var buffer=new byte[chunk+Overlap];var block=new byte[chunk];int carry=0;ulong previous=0;
            try
            {
                while(regionIndex<regions.Count&&clock.Elapsed<budget&&read<byteBudget)
                {
                    CheckReadStop();var r=regions[regionIndex];ulong from=r.Start+regionOffset;
                    if(from!=previous)
                    {
                        carry=0;
                        // No account bytes survive a slice. Re-read the contiguous
                        // prefix to restore overlap without rewinding the progress
                        // cursor, even if a slice processed only one small chunk.
                        int prefix=(int)Math.Min((ulong)Overlap,regionOffset);
                        if(prefix>0&&ReadRange(from-(ulong)prefix,buffer,prefix)==prefix)carry=prefix;
                    }
                    int length=(int)Math.Min((ulong)chunk,r.Length-regionOffset);CheckReadStop(length);
                    ReadProcessMemory(handle,(nint)from,block,(nuint)length,out var got);int n=(int)got;read+=n;visited++;
                    if(n>0)
                    {
                        block.AsSpan(0,n).CopyTo(buffer.AsSpan(carry));int total=carry+n;var span=buffer.AsSpan(0,total);
                        if(InventoryJsonLocator.FindField(span,"XPInfo"u8)>=0)
                        {
                            CheckReadStop();candidates++;var hit=InventoryJsonLocator.Find(span,catalog);diagnostic=InventoryJsonLocator.InspectCandidate(span,catalog);
                            if(hit is {} found&&Stable(from-(ulong)carry+(ulong)found.Start,span.Slice(found.Start,found.Length)))
                            {cached=from-(ulong)carry+(ulong)found.Start;cachedSize=found.Length;cacheTime=DateTimeOffset.UtcNow;return new(found.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
                            if(diagnostic.State=="root-missing")
                            {
                                ulong origin=r.Start>(ulong)Overlap?r.Start-(ulong)Overlap:0x10000UL;int capacity=(int)Math.Min(12UL*(ulong)ReadStep,r.Start+r.Length+(ulong)Overlap-origin);var window=new byte[capacity];
                                try
                                {
                                    ReadRange(origin,window,capacity,true);CheckReadStop();hit=InventoryJsonLocator.Find(window,catalog);diagnostic=InventoryJsonLocator.InspectCandidate(window,catalog);
                                    if(hit is {} whole&&Stable(origin+(ulong)whole.Start,window.AsSpan(whole.Start,whole.Length)))
                                    {cached=origin+(ulong)whole.Start;cachedSize=whole.Length;cacheTime=DateTimeOffset.UtcNow;return new(whole.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
                                }
                                finally{Array.Clear(window);}
                            }
                            if(r.Length>(ulong)chunk&&r.Length<=32UL*(ulong)ReadStep)
                            {
                                var full=new byte[(int)r.Length];
                                try
                                {
                                    int fullGot=ReadRange(r.Start,full,full.Length);CheckReadStop();hit=InventoryJsonLocator.Find(full.AsSpan(0,fullGot),catalog);
                                    if(hit is {} whole&&Stable(r.Start+(ulong)whole.Start,full.AsSpan(whole.Start,whole.Length)))
                                    {cached=r.Start+(ulong)whole.Start;cachedSize=whole.Length;cacheTime=DateTimeOffset.UtcNow;return new(whole.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
                                }
                                finally{Array.Clear(full);}
                            }
                        }
                        carry=n==length?Math.Min(Overlap,total):0;if(carry>0)buffer.AsSpan(total-carry,carry).CopyTo(buffer);
                    }
                    else carry=0;
                    regionOffset+=(ulong)length;previous=from+(ulong)length;
                    if(regionOffset>=r.Length){regionIndex++;regionOffset=0;}
                }
                bool complete=regionIndex>=regions.Count;
                string message=complete?"현재 게임 메모리에 완전한 인벤토리 응답이 없습니다. 보유 목록은 확인되지 않았습니다. 마지막 저장 목록은 유지합니다.":$"인벤토리 데이터 탐색 중 · 영역 {regionIndex:N0}/{regions.Count:N0} · 다음 시도에서 이어서 확인합니다.";
                return new(null,message,read,clock.Elapsed.TotalSeconds,visited,candidates,complete,diagnostic,!complete);
            }
            finally{Array.Clear(buffer);Array.Clear(block);}
        }
        catch(ScanYieldException){return Deferred("화면 인식에 우선권을 양보했습니다. 다음 동기화에서 이어서 확인합니다.");}
        catch(ScanBudgetException){return Deferred("인벤토리 확인 작업을 나누어 처리합니다. 다음 동기화에서 이어서 확인합니다.");}
        catch(ScanProcessGoneException){Reset();return new(null,"워프레임이 종료됐습니다. 마지막 저장 목록은 유지합니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
    }
    public void Dispose()
    {
        lock(stateSync){if(disposed)return;disposed=true;if(!reading)Reset();}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Region{public nint BaseAddress,AllocationBase;public uint AllocationProtect;public nuint RegionSize;public uint State,Protect,Type;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern nuint VirtualQueryEx(SafeProcessHandle process,nint address,out Region buffer,nuint length);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ReadProcessMemory(SafeProcessHandle process,nint address,[Out]byte[] buffer,nuint length,out nuint read);
}
