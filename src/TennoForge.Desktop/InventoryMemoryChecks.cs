using System.Runtime.InteropServices;
using System.Text.Json;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<string[]> CheckInventoryMemorySlices(InventoryCatalog catalog)
    {
        var checks=new List<string>();void Yes(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);}
        const int step=1024*1024,capacity=3*step;
        var rhino=catalog.Items.Single(i=>i.EnglishName=="Rhino");
        var raw=JsonSerializer.SerializeToUtf8Bytes(new{PlayerLevel=20,RegularCredits=777001,Suits=new[]{new{ItemType=rhino.Id,XP=100000}},LongGuns=Array.Empty<object>(),Pistols=Array.Empty<object>(),Melee=Array.Empty<object>(),Recipes=Array.Empty<object>(),MiscItems=Array.Empty<object>(),XPInfo=new[]{new{ItemType=rhino.Id,XP=100000}}});
        var bytes=new byte[capacity];raw.CopyTo(bytes.AsSpan(step-raw.Length/2));
        var pointer=Marshal.AllocHGlobal(capacity);
        try
        {
            Marshal.Copy(bytes,0,pointer,capacity);
            using var reader=new InventoryMemoryReader();reader.SeedSelfRangeForCheck(pointer,capacity);
            int observations=0;
            var first=await reader.ReadSelfForCheck(catalog,default,automatic:true,yieldRequested:()=>++observations>=4);
            Yes(first is {Snapshot:null,Deferred:true,CompletePass:false,BytesRead:step},"인벤토리 자동 slice: 한 청크 후 OCR 우선 양보·완료로 오표시하지 않음");
            var resumed=await reader.ReadSelfForCheck(catalog,default,automatic:true);
            Yes(resumed.Snapshot is {Credits:777001} s&&s.MasteryRanks.GetValueOrDefault(rhino.Id)==10&&!resumed.Deferred,"인벤토리 자동 slice: 원시 버퍼를 버린 뒤 경계 JSON을 재판독해 정확히 복구");
            var cached=await reader.ReadSelfForCheck(catalog,default,automatic:true);
            Yes(cached.Snapshot?.Credits==777001&&cached.BytesRead<step,"인벤토리 자동 slice: 찾은 위치의 두 번 일치 검증 유지");
            var paused=await reader.ReadSelfForCheck(catalog,default,automatic:true,yieldRequested:()=>true);
            Yes(paused is {Snapshot:null,Deferred:true,BytesRead:0},"인벤토리 OCR 우선권이 이미 있으면 프로세스 탐색·메모리 읽기 생략");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();bool observed=false;
                try{await reader.ReadSelfForCheck(catalog,cancelled.Token,automatic:true);}catch(OperationCanceledException){observed=true;}
                Yes(observed,"인벤토리 slice 취소 전파·후속 작업 잠금 해제");
            }
            reader.SeedSelfRangeForCheck(pointer,capacity);
            var again=await reader.ReadSelfForCheck(catalog,default,automatic:true);
            Yes(again.Snapshot?.Credits==777001,"인벤토리 취소 뒤 새 작업 정상 재시작");
            reader.Dispose();bool disposed=false;
            try{await reader.ReadSelfForCheck(catalog,default);}catch(ObjectDisposedException){disposed=true;}
            Yes(disposed,"인벤토리 Reader 종료 후 새 작업 생성 차단");
        }
        finally{Array.Clear(bytes);Array.Clear(raw);Marshal.Copy(bytes,0,pointer,capacity);Marshal.FreeHGlobal(pointer);}
        return checks.ToArray();
    }
}
