using System.Windows.Threading;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private static async Task CheckInventoryContinuation(InventoryPage view,Action<bool,string> yes)
    {
        var originalAutomatic=view.Automatic.IsChecked;
        var originalRead=view.ReadOverride;var originalCanRead=view.CanReadGame;var originalStatus=view.Status.Text;
        try
        {
            int reads=0;var resumed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            view.Automatic.IsChecked=true;view.CanReadGame=()=>true;
            view.ReadOverride=_=>
            {
                reads++;
                if(reads==1)return Task.FromResult(new InventoryReadResult(null,"자동 확인 일부 완료",1024,.001,Deferred:true));
                resumed.TrySetResult();return Task.FromResult(new InventoryReadResult(null,"전체 영역 확인 완료",2048,.001,CompletePass:true));
            };
            await view.SyncAsync(true);
            yes(reads==1&&view.AutomaticContinuationScheduled,"자동 확인 slice 뒤 단일 3초 이어읽기 예약");
            view.CanReadGame=()=>false;
            await Task.Delay(3150);await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.ContextIdle);
            yes(reads==1&&view.AutomaticContinuationScheduled,"OCR 진행 중에는 인벤토리 재판독 없이 다음 3초로 양보");
            view.CanReadGame=()=>true;
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(6));await System.Windows.Threading.Dispatcher.Yield();
            yes(reads==2&&!view.AutomaticContinuationScheduled,"OCR 종료 뒤 자동 이어읽기 · 전체 영역 완료 시 예약 중단");

            view.ReadOverride=_=>Task.FromResult(new InventoryReadResult(null,"자동 확인 일부 완료",1024,.001,Deferred:true));
            await view.SyncAsync(true);view.Automatic.IsChecked=false;
            yes(!view.AutomaticContinuationScheduled,"자동 동기화 끄기에서 이어읽기 예약 중단");

            view.Automatic.IsChecked=true;await view.SyncAsync(true);
            var snapshot=view.Snapshot;
            view.ReadOverride=_=>Task.FromResult(new InventoryReadResult(snapshot,"목록 확인 완료",1024,.001));
            await view.SyncAsync();
            yes(!view.AutomaticContinuationScheduled,"명시적 수동 동기화 성공에서 이전 자동 예약 중단");
            view.ReadOverride=_=>Task.FromResult(new InventoryReadResult(null,"수동 확인 일부 완료",1024,.001,Deferred:true));
            await view.SyncAsync();yes(!view.AutomaticContinuationScheduled,"수동 요청은 자동 반복 작업을 새로 만들지 않음");
        }
        finally
        {
            view.Automatic.IsChecked=false;view.ReadOverride=originalRead;view.CanReadGame=originalCanRead;
            view.Automatic.IsChecked=originalAutomatic;view.Status.Text=originalStatus;
        }
        var disposedView=new InventoryPage(false);
        try
        {
            disposedView.Automatic.IsChecked=true;
            disposedView.ReadOverride=_=>Task.FromResult(new InventoryReadResult(null,"자동 확인 일부 완료",1024,.001,Deferred:true));
            await disposedView.SyncAsync(true);yes(disposedView.AutomaticContinuationScheduled,"수명 검수용 자동 이어읽기 예약 생성");
            disposedView.Dispose();yes(!disposedView.AutomaticContinuationScheduled,"페이지 Dispose에서 이어읽기 timer와 reader 수명 종료");
        }
        finally{disposedView.Dispose();}
    }
}
