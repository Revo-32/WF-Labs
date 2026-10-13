using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using WFLabs.Client;

namespace TennoForge.Desktop;
internal static class ClientIntegration
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static void Configure(ClientPreferences settings)
    {
        var helper=Path.Combine(AppContext.BaseDirectory,"WFLabs.Startup.exe");
        if((settings.StartWithWindows||settings.StartWithWarframe)&&!File.Exists(helper))throw new IOException("시작 도우미가 없습니다. 설치형 배포본을 사용하세요.");
        using var key=Registry.CurrentUser.CreateSubKey(RunKey);
        var previous=key.GetValue("WF Labs");
        try
        {
            if(settings.StartWithWindows||settings.StartWithWarframe)key.SetValue("WF Labs",$"\"{helper}\" --login");else key.DeleteValue("WF Labs",false);
            settings.Save();
        }
        catch{if(previous is null)key.DeleteValue("WF Labs",false);else key.SetValue("WF Labs",previous);throw;}
        StopWatcher();
        if(settings.StartWithWarframe)Process.Start(new ProcessStartInfo(helper,"--monitor"){UseShellExecute=false,CreateNoWindow=true});
    }
    internal static void RestoreStartup()
    {var s=ClientPreferences.Load();if(OwnsStartup()&&(s.StartWithWindows||s.StartWithWarframe))Configure(s);}
    internal static void StopOwnedWatcher(){if(OwnsStartup())StopWatcher();}
    private static bool OwnsStartup()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue("WF Labs") as string,$"\"{Path.Combine(AppContext.BaseDirectory,"WFLabs.Startup.exe")}\" --login",StringComparison.OrdinalIgnoreCase);
    }
    internal static void RemoveStartup()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunKey,true);
        var command=$"\"{Path.Combine(AppContext.BaseDirectory,"WFLabs.Startup.exe")}\" --login";
        if(string.Equals(key?.GetValue("WF Labs") as string,command,StringComparison.OrdinalIgnoreCase)){StopWatcher();key!.DeleteValue("WF Labs",false);}
    }
    internal static void StopWatcher()
    {
        try
        {
            var identity=WindowsIdentity.GetCurrent().User!.Value;
            using var signal=EventWaitHandle.OpenExisting(@"Local\WFLabs.Startup.Stop."+identity);signal.Set();
            try
            {
                using var mutex=Mutex.OpenExisting(@"Local\WFLabs.Startup."+identity);
                var held=false;
                try{held=mutex.WaitOne(TimeSpan.FromSeconds(5));}catch(AbandonedMutexException){held=true;}
                finally{if(held)mutex.ReleaseMutex();}
                if(!held)throw new IOException("시작 도우미를 종료하지 못했습니다. 잠시 후 다시 시도하세요.");
            }catch(WaitHandleCannotBeOpenedException){}
        }
        catch(WaitHandleCannotBeOpenedException){}
    }
}
