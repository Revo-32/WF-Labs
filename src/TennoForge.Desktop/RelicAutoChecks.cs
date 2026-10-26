using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckRelicAuto(string dir,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        var path=Path.Combine(dir,"fixture-EE.log");File.WriteAllText(path,"1 Script [Info]: Got rewards\n");
        var events=new ConcurrentQueue<RelicRewardSignal>();
        var detector=new RelicAutoDetector(events.Enqueue,_=>{},path,debugOutput:false);
        try
        {
            await Task.Delay(650);Yes(events.IsEmpty,"기존 로그는 재생하지 않음");
            await File.AppendAllTextAsync(path,"2 Script [Info]: ProjectionsCountdown.lua: Relic timer closed\n2.1 Script [Info]: ProjectionRewardChoice.lua: Got rew");await Task.Delay(650);
            Yes(events.IsEmpty,"부분 기록된 줄은 다음 기록까지 보류");
            await File.AppendAllTextAsync(path,"ards\n3 Script [Info]: ProjectionRewardChoice.lua: Pause countdown done\n");await Task.Delay(900);
            Yes(events.SequenceEqual(new[]{RelicRewardSignal.Open}),"증분 기록을 감지하고 같은 보상 진입은 한 번만 알림");
            await File.AppendAllTextAsync(path,"4 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down\n5 Script [Info]: Got rewards\n");await Task.Delay(900);
            Yes(events.SequenceEqual(new[]{RelicRewardSignal.Open,RelicRewardSignal.Close,RelicRewardSignal.Open}),"종료와 다음 라운드 자동 감지");
            File.WriteAllText(path,"");await Task.Delay(650);
            await File.AppendAllTextAsync(path,"6 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down\n");await Task.Delay(900);
            Yes(events.Count==4&&events.Last()==RelicRewardSignal.Close,"로그 잘림·재시작 후 감시 유지");
            detector.Dispose();await File.AppendAllTextAsync(path,"7 Script [Info]: Got rewards\n");await Task.Delay(650);
            Yes(events.Count==4,"감지 끄기 뒤 추가 이벤트 없음");
        }
        finally{detector.Dispose();}
        // Private Windows objects exercise the transport without touching the
        // real DBWIN channel used by the game or other companion applications.
        var channel="WFLabsTest_"+Guid.NewGuid().ToString("N");var debugEvents=new ConcurrentQueue<RelicRewardSignal>();
        var readyNotice=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var debug=new RelicAutoDetector(debugEvents.Enqueue,_=>readyNotice.TrySetResult(),path,true,channel,pid=>pid==123))
        {
            await readyNotice.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var ready=EventWaitHandle.OpenExisting(channel+"_BUFFER_READY");using var data=EventWaitHandle.OpenExisting(channel+"_DATA_READY");
            using var memory=MemoryMappedFile.OpenExisting(channel+"_BUFFER");using var writer=memory.CreateViewAccessor();
            async Task Send(uint pid,string text)
            {
                Yes(await Task.Run(()=>ready.WaitOne(2000)),"비공개 실시간 로그 채널 준비");
                writer.Write(0,pid);var bytes=Encoding.UTF8.GetBytes(text+'\0');writer.WriteArray(4,bytes,0,bytes.Length);data.Set();await Task.Delay(100);
            }
            await Send(999,"10 Script [Info]: ProjectionRewardChoice.lua: Got rewards");Yes(debugEvents.IsEmpty,"다른 프로세스의 같은 메시지 무시");
            await Send(123,"10 Script [Info]: ProjectionRewardChoice.lua: Got rewards");Yes(debugEvents.Count==1,"실제 Lua 접두사가 포함된 실시간 로그 이벤트 수신");
            var fallback=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var second=new RelicAutoDetector(_=>{},m=>fallback.TrySetResult(m),path,true,channel,pid=>pid==123);
            Yes((await fallback.Task.WaitAsync(TimeSpan.FromSeconds(5))).Contains("다른 앱"),"이미 사용 중인 실시간 채널을 빼앗지 않음");
            await Send(123,"11 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down");Yes(debugEvents.Last()==RelicRewardSignal.Close,"채널 충돌 이후 기존 수신자 유지");
        }
        double idleCpuMilliseconds,idleWallMilliseconds;
        using(var idle=new RelicAutoDetector(_=>{},_=>{},path,debugOutput:false))
        {
            using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var clock=Stopwatch.StartNew();
            await Task.Delay(3000);idleCpuMilliseconds=(process.TotalProcessorTime-cpu).TotalMilliseconds;idleWallMilliseconds=clock.Elapsed.TotalMilliseconds;
        }
        Yes(JsonSerializer.Deserialize<RelicPreferences>("{\"Hotkey\":false}")?.Automatic==true,"이전 설정 파일의 자동 인식 기본값");
        using var view=new RelicPage(new RelicMarketClient(Path.Combine(dir,"cache"),new RelicFixtureHandler(RelicCatalog.Bundled()),snapshotMode:true));
        SelectPage(4);page.Content=view;view.AutoStatus.Text="자동 인식 대기 · 로그 파일 감지 사용 (다른 앱이 실시간 로그 사용 중)";
        var toggles=new List<bool>();view.AutomaticChanged=toggles.Add;
        var peer=new CheckBoxAutomationPeer(view.Automatic);var toggle=(IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
        toggle.Toggle();toggle.Toggle();Yes(toggles.SequenceEqual(new[]{false,true}),"자동 인식 체크박스의 실제 Automation Toggle 이벤트");
        render(this,"auto-minimum",1760,920,1);render(this,"auto-125",1760,920,1.25);render(this,"auto-150",1760,920,1.5);
        return new{passed=true,checks,idleCpuMilliseconds,idleWallMilliseconds,idleOneCorePercent=idleCpuMilliseconds/idleWallMilliseconds*100,nativeInput=false,screenCapture=false,debugChannelClaimed=false,scope="기록 재생·비공개 Windows 로그 채널·증분 감시·설정·실제 WPF 토글 검수. 대기 CPU는 격리 앱 전체이며 실전 게임 성능 측정이 아님."};
    }
}
