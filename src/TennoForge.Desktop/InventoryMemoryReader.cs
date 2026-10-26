using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal sealed record InventoryReadResult(InventorySnapshot? Snapshot,string Message,long BytesRead,double Seconds,int Regions=0,int Candidates=0,bool CompletePass=false,InventoryCandidateInfo? Diagnostic=null);
internal sealed class InventoryMemoryReader
{
    private int processId,regionIndex;private ulong regionOffset,cached;private int cachedSize;private DateTimeOffset cacheTime;
    private List<(ulong Start,ulong Length)> regions=[];
    internal async Task<InventoryReadResult> ReadAsync(InventoryCatalog catalog,CancellationToken token)=>await Task.Run(()=>
    {
        var thread=Thread.CurrentThread;var old=thread.Priority;try{thread.Priority=ThreadPriority.BelowNormal;return Read(catalog,token);}finally{thread.Priority=old;}
    },token);
    internal async Task<InventoryReadResult> ReadSelfForCheck(InventoryCatalog catalog,CancellationToken token)=>await Task.Run(()=>Read(catalog,token,true),token);
    private InventoryReadResult Read(InventoryCatalog catalog,CancellationToken token,bool selfCheck=false)
    {
        var clock=Stopwatch.StartNew();long read=0;int visited=0,candidates=0;InventoryCandidateInfo? diagnostic=null;
        using var game=selfCheck?Process.GetCurrentProcess():Process.GetProcessesByName("Warframe.x64").FirstOrDefault()??Process.GetProcessesByName("Warframe").FirstOrDefault();
        if(game is null)return new(null,"워프레임 실행을 기다리고 있습니다. 마지막 저장 목록은 유지합니다.",0,clock.Elapsed.TotalSeconds);
        if(game.Id!=processId){processId=game.Id;regions.Clear();regionIndex=0;regionOffset=0;cached=0;}
        // Query + VM_READ only. No writes, injected modules or account API.
        using var handle=OpenProcess(0x0410,false,processId);
        if(handle.IsInvalid)return new(null,"게임 데이터 읽기 권한을 얻지 못했습니다. 저장 목록은 유지합니다.",0,clock.Elapsed.TotalSeconds);
        bool Stable(ulong address,ReadOnlySpan<byte> expected)
        {
            var again=new byte[expected.Length];try{bool ok=ReadProcessMemory(handle,(nint)address,again,(nuint)again.Length,out var got);read+=(long)got;return ok&&got==(nuint)again.Length&&expected.SequenceEqual(again);}finally{Array.Clear(again);}
        }
        if(cached!=0&&DateTimeOffset.UtcNow-cacheTime<TimeSpan.FromSeconds(10))
        {
            var bytes=new byte[cachedSize];try{if(ReadProcessMemory(handle,(nint)cached,bytes,(nuint)bytes.Length,out var got)&&got==(nuint)bytes.Length)
            {read+=bytes.Length;var hit=InventoryJsonLocator.Find(bytes,catalog);if(hit is {} found&&Stable(cached,bytes))return new(found.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds);}}finally{Array.Clear(bytes);}cached=0;
        }
        else if(cached!=0){cached=0;regions.Clear();regionIndex=0;regionOffset=0;}
        if(regions.Count==0||regionIndex>=regions.Count)
        {
            regions.Clear();regionIndex=0;regionOffset=0;ulong cursor=0x10000;
            while(VirtualQueryEx(handle,(nint)cursor,out var info,(nuint)Marshal.SizeOf<Region>())>0)
            {
                token.ThrowIfCancellationRequested();ulong start=(ulong)info.BaseAddress,length=(ulong)info.RegionSize,end=start+length;if(end<=cursor)break;
                uint access=info.Protect&0xff;
                if(info.State==0x1000&&info.Type==0x20000&&(info.Protect&0x100)==0&&access is 2 or 4 or 8)regions.Add((start,length));cursor=end;
            }
            // Inventory JSON is data. Prefer plausible data allocations over code,
            // huge texture backing allocations and thousands of tiny pages.
            regions=regions.OrderBy(r=>r.Length is >=65536 and <=16*1024*1024?0:r.Length>16*1024*1024?1:2).ThenByDescending(r=>r.Length).ToList();
        }
        const int chunk=4*1024*1024,overlap=2*1024*1024;var buffer=new byte[chunk+overlap];var block=new byte[chunk];int carry=0;ulong previous=0;
        try
        {
            // Resume bounded passes; never sleep once per tiny allocation.
            while(regionIndex<regions.Count&&clock.Elapsed<TimeSpan.FromSeconds(6)&&read<8L*1024*1024*1024)
            {
                token.ThrowIfCancellationRequested();var r=regions[regionIndex];ulong from=r.Start+regionOffset;if(from!=previous)carry=0;int length=(int)Math.Min(chunk,r.Length-regionOffset);
                ReadProcessMemory(handle,(nint)from,block,(nuint)length,out var got);int n=(int)got;read+=n;visited++;
                if(n>0)
                {
                    block.AsSpan(0,n).CopyTo(buffer.AsSpan(carry));int total=carry+n;var span=buffer.AsSpan(0,total);
                    if(InventoryJsonLocator.FindField(span,"XPInfo"u8)>=0)
                    {
                        candidates++;var hit=InventoryJsonLocator.Find(span,catalog);diagnostic=InventoryJsonLocator.InspectCandidate(span,catalog);
                        if(hit is {} found&&Stable(from-(ulong)carry+(ulong)found.Start,span.Slice(found.Start,found.Length))){cached=from-(ulong)carry+(ulong)found.Start;cachedSize=found.Length;cacheTime=DateTimeOffset.UtcNow;return new(found.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
                        if(diagnostic.State=="root-missing")
                        {
                            ulong origin=r.Start>2*1024*1024?r.Start-2*1024*1024:0x10000;int capacity=(int)Math.Min(12UL*1024*1024,r.Start+r.Length+2*1024*1024-origin);var window=new byte[capacity];
                            try
                            {
                                int offset=0;while(offset<capacity)
                                {
                                    token.ThrowIfCancellationRequested();ulong address=origin+(ulong)offset;
                                    if(VirtualQueryEx(handle,(nint)address,out var part,(nuint)Marshal.SizeOf<Region>())==0)break;ulong end=(ulong)part.BaseAddress+(ulong)part.RegionSize;if(end<=address)break;int size=(int)Math.Min((ulong)(capacity-offset),end-address);
                                    if(part.State==0x1000&&(part.Protect&0x100)==0&&(part.Protect&0xff) is 2 or 4 or 8)
                                    {var portion=new byte[size];try{ReadProcessMemory(handle,(nint)address,portion,(nuint)size,out var count);read+=(long)count;portion.AsSpan(0,(int)count).CopyTo(window.AsSpan(offset));}finally{Array.Clear(portion);}}
                                    offset+=size;
                                }
                                hit=InventoryJsonLocator.Find(window,catalog);diagnostic=InventoryJsonLocator.InspectCandidate(window,catalog);
                                if(hit is {} whole&&Stable(origin+(ulong)whole.Start,window.AsSpan(whole.Start,whole.Length))){cached=origin+(ulong)whole.Start;cachedSize=whole.Length;cacheTime=DateTimeOffset.UtcNow;return new(whole.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}
                            }
                            finally{Array.Clear(window);}
                        }
                        // A candidate can straddle chunks. Re-read its bounded
                        // allocation, parse a complete object, then clear raw bytes.
                        if(r.Length>chunk&&r.Length<=32L*1024*1024)
                        {
                            var full=new byte[(int)r.Length];try{ReadProcessMemory(handle,(nint)r.Start,full,(nuint)full.Length,out var fullGot);read+=(long)fullGot;hit=InventoryJsonLocator.Find(full.AsSpan(0,(int)fullGot),catalog);
                                if(hit is {} whole&&Stable(r.Start+(ulong)whole.Start,full.AsSpan(whole.Start,whole.Length))){cached=r.Start+(ulong)whole.Start;cachedSize=whole.Length;cacheTime=DateTimeOffset.UtcNow;return new(whole.Snapshot,"게임 인벤토리와 마스터리 이력을 읽었습니다.",read,clock.Elapsed.TotalSeconds,visited,candidates);}}
                            finally{Array.Clear(full);}
                        }
                    }
                    carry=Math.Min(overlap,total);buffer.AsSpan(total-carry,carry).CopyTo(buffer);
                }
                else carry=0;
                regionOffset+=(ulong)length;previous=from+(ulong)length;if(regionOffset>=r.Length){regionIndex++;regionOffset=0;}
            }
            bool complete=regionIndex>=regions.Count;
            string message=complete?"현재 게임 메모리에 완전한 인벤토리 응답이 없습니다. 보유 목록은 확인되지 않았습니다. 마지막 저장 목록은 유지합니다.":$"인벤토리 데이터 탐색 중 · 영역 {regionIndex:N0}/{regions.Count:N0} · 다음 시도에서 이어서 확인합니다.";
            return new(null,message,read,clock.Elapsed.TotalSeconds,visited,candidates,complete,diagnostic);
        }
        finally{Array.Clear(buffer);Array.Clear(block);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Region{public nint BaseAddress,AllocationBase;public uint AllocationProtect;public nuint RegionSize;public uint State,Protect,Type;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern nuint VirtualQueryEx(SafeProcessHandle process,nint address,out Region buffer,nuint length);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ReadProcessMemory(SafeProcessHandle process,nint address,[Out]byte[] buffer,nuint length,out nuint read);
}
