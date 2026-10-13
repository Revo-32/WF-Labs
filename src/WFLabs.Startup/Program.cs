using System.Diagnostics;
using System.Security.Principal;
using WFLabs.Client;

internal static class Program
{
    private static readonly int Session=CurrentSession();
    private static int CurrentSession(){using var p=Process.GetCurrentProcess();return p.SessionId;}
    private static void Main(string[] args)
    {
        var identity=WindowsIdentity.GetCurrent().User!.Value;
        using var singleton=new Mutex(true,@"Local\WFLabs.Startup."+identity,out var owner);if(!owner)return;
        using var stop=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\WFLabs.Startup.Stop."+identity);
        var policy=new LaunchPolicy();var loginEvent=args.Contains("--login");
        // Bounded process-name checks only. No game handles, memory reads or screen capture.
        do
        {
            if(stop.WaitOne(0))return;
            var settings=ClientPreferences.Load();
            var running=settings.StartWithWarframe&&(Running("Warframe.x64")||Running("Warframe"));
            if(policy.ShouldLaunch(settings.StartWithWindows,settings.StartWithWarframe,loginEvent,running))Launch();
            loginEvent=false;if(!settings.StartWithWarframe)return;
        }while(!stop.WaitOne(TimeSpan.FromSeconds(3)));
    }
    private static bool Running(string name)
    {
        var found=false;foreach(var p in Process.GetProcessesByName(name)){using(p)try{if(p.SessionId==Session)found=true;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}return found;
    }
    private static void Launch()
    {
        if(Running("WFLabs"))return;
        var file=Path.Combine(AppContext.BaseDirectory,"WFLabs.exe");
        if(File.Exists(file))try{Process.Start(new ProcessStartInfo(file){UseShellExecute=true,WorkingDirectory=AppContext.BaseDirectory});}catch(System.ComponentModel.Win32Exception){}
    }
}
