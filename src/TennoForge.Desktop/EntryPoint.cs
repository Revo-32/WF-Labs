using System.Security.Principal;
using Velopack;

namespace TennoForge.Desktop;
internal static class EntryPoint
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false)
            .OnBeforeUpdateFastCallback(_=>ClientIntegration.StopOwnedWatcher())
            .OnAfterUpdateFastCallback(_=>ClientIntegration.RestoreStartup())
            .OnBeforeUninstallFastCallback(_=>ClientIntegration.RemoveStartup()).Run();
        var check=args.Any(a=>a=="--layout-probe"||a.EndsWith("smoke",StringComparison.Ordinal)||a.Contains("preview",StringComparison.Ordinal)||a.Contains("check",StringComparison.Ordinal));
        var identity=WindowsIdentity.GetCurrent().User!.Value;
        using var singleton=new Mutex(true,@"Local\WFLabs.App."+identity,out var owner);
        if(!owner&&!check){try{using var signal=EventWaitHandle.OpenExisting(@"Local\WFLabs.Show."+identity);signal.Set();}catch(WaitHandleCannotBeOpenedException){}return;}
        var app=new App();app.InitializeComponent();
        using var wake=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\WFLabs.Show."+identity);
        RegisteredWaitHandle? waiter=null;
        if(!check)waiter=ThreadPool.RegisterWaitForSingleObject(wake,(_,_)=>app.Dispatcher.BeginInvoke(()=>
        {if(app.MainWindow is {} window){window.Show();if(window.WindowState==System.Windows.WindowState.Minimized)window.WindowState=System.Windows.WindowState.Normal;window.Activate();}}),null,Timeout.Infinite,false);
        try{app.Run();}finally{waiter?.Unregister(null);}
    }
}
