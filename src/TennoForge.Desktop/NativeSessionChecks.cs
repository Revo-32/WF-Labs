using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal static class NativeSessionChecks
{
    internal static async Task Invoke(UIElement element)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(element)!.GetPattern(PatternInterface.Invoke)).Invoke();
        await element.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
    }
    internal static string Json<T>(T value)=>JsonSerializer.Serialize(value,Catalog.Json);
    internal static void Yes(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private delegate bool EnumCallback(nint hwnd,nint data);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumCallback callback,nint data);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint hwnd,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint hwnd,System.Text.StringBuilder text,int length);
    [DllImport("user32.dll")]private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(nint hwnd);
    internal static async Task Dialog(UIElement button,string? path,bool cancel=false)
    {
        var control=Task.Run(async()=>
        {
            nint dialog=0;var timer=Stopwatch.StartNew();
            while(dialog==0&&timer.Elapsed<TimeSpan.FromSeconds(25))
            {
                EnumWindows((h,_)=>{GetWindowThreadProcessId(h,out var pid);if(pid==Environment.ProcessId&&IsWindowVisible(h)){var name=new System.Text.StringBuilder(256);GetClassName(h,name,256);if(name.ToString()=="#32770")dialog=h;}return true;},0);
                if(dialog==0)await Task.Delay(100);
            }
            Yes(dialog!=0,"실제 파일 선택 창이 열리지 않음");
            MonitorWorkArea.SetWindowPos(dialog,0,-25000,-25000,0,0,0x15);
            await Task.Delay(200);var root=AutomationElement.FromHandle(dialog);
            if(!cancel)
            {
                var edits=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>().ToArray();
                var edit=edits.FirstOrDefault(e=>e.Current.AutomationId=="1148")??edits.FirstOrDefault(e=>e.Current.Name.Contains("파일 이름")||e.Current.Name.Contains("File name"))??throw new InvalidOperationException("파일 이름 입력 없음: "+string.Join(",",edits.Select(e=>e.Current.AutomationId+":"+e.Current.Name)));
                ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).SetValue(path!);
            }
            var action=root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.AutomationIdProperty,cancel?"2":"1")))??throw new InvalidOperationException("파일 창 확인·취소 버튼 없음");
            ((InvokePattern)action.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            timer.Restart();while(IsWindow(dialog)&&timer.Elapsed.TotalSeconds<25)await Task.Delay(100);
            Yes(!IsWindow(dialog),"파일 창이 닫히지 않았습니다.");
        });
        // Invoke queues WPF's real button handler; ShowDialog pumps messages while
        // the independent UIA client interacts with the native shell dialog.
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)).Invoke();
        await control;await button.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
    }
    private delegate bool MonitorCallback(nint monitor,nint dc,nint rect,nint data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
    internal static async Task<object> Monitors(MainWindow window,string dir)
    {
        var handles=new List<nint>();EnumDisplayMonitors(0,0,(h,_,_,_)=>{handles.Add(h);return true;},0);
        var hwnd=new WindowInteropHelper(window).Handle;var records=new List<object>();var dpis=new HashSet<uint>();int changes=0;
        window.DpiChanged+=(_,_)=>changes++;
        // Hide the HWND without layering it: the actual native DPI and WPF text
        // rendering mode remain identical, and the user's desktop is undisturbed.
        ShowWindow(hwnd,0);
        foreach(var monitor in handles.Concat(handles.Take(1)))
        {
            var info=MonitorWorkArea.Read(monitor);var a=info.Work;
            MonitorWorkArea.SetWindowPos(hwnd,0,a.Left+12,a.Top+12,Math.Min(1840,a.Right-a.Left),Math.Min(960,a.Bottom-a.Top),0x14);
            await Task.Delay(300);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();
            var dpi=MonitorWorkArea.GetDpiForWindow(hwnd);dpis.Add(dpi);var scale=VisualTreeHelper.GetDpi(window);
            Yes(MonitorWorkArea.MonitorFromWindow(hwnd,2)==monitor,"실제 모니터 이동 실패");
            Yes(Math.Abs(scale.DpiScaleX-dpi/96d)<.01,"HWND와 WPF DPI 불일치");
            MonitorWorkArea.GetWindowRect(hwnd,out var rect);
            Yes(rect.Left>=a.Left-1&&rect.Right<=a.Right+1&&rect.Top>=a.Top-1&&rect.Bottom<=a.Bottom+1,"작업 영역 밖으로 창 잘림");
            var report=window.CheckNativeMonitorLayout();
            var image=(FrameworkElement)window.Content;App.Render(image,Path.Combine(dir,$"monitor-{records.Count}-{dpi}.png"),(int)window.ActualWidth,(int)window.ActualHeight,scale.DpiScaleX);
            records.Add(new{info.Device,dpi,scale=scale.DpiScaleX,width=window.ActualWidth,height=window.ActualHeight,bounds=new{rect.Left,rect.Top,rect.Right,rect.Bottom},report});
        }
        MonitorWorkArea.SetWindowPos(hwnd,0,-30000,-30000,1760,920,0x14);
        return new{passed=true,monitors=handles.Count,actualDpiChanges=changes,distinctDpi=dpis,records,note=dpis.Count>1?"실제 모니터 간 서로 다른 DPI 전환 확인":"연결된 실제 모니터는 모두 같은 배율입니다. 실제 이종 배율 전환은 검증할 수 없으며 시각 트리 배율 검사는 별도로 수행합니다."};
    }
    [DllImport("user32.dll")]private static extern bool ShowWindow(nint hwnd,int command);
}

public sealed partial class MainWindow
{
    internal object CheckNativeMonitorLayout()
    {
        var controls=headerControls.TransformToAncestor(this).TransformBounds(new Rect(headerControls.RenderSize));
        NativeSessionChecks.Yes(controls.Right<=ActualWidth+1,"창 제어 버튼 잘림");
        NativeSessionChecks.Yes(workspaceViewport.ViewportWidth>100&&workspaceViewport.ViewportHeight>100,"스크롤 작업 공간 없음");
        return new{horizontalScroll=workspaceViewport.ScrollableWidth,verticalScroll=workspaceViewport.ScrollableHeight,clearType=TextOptions.GetTextRenderingMode(this).ToString(),ideal=TextOptions.GetTextFormattingMode(this).ToString()};
    }
    internal async Task<object> CheckNativeFiles(string dir)
    {
        var checks=new List<string>();ShowModding();UpdateLayout();
        var original=NativeSessionChecks.Json(ReadBuild());
        var save=buildActions.Children.OfType<Button>().Single(b=>b.Content as string=="저장");
        var load=buildActions.Children.OfType<Button>().Single(b=>b.Content as string=="불러오기");
        var path=Path.Combine(dir,"근접 빌드.json");await NativeSessionChecks.Dialog(save,path);
        NativeSessionChecks.Yes(File.Exists(path),"근접 실제 저장 창 파일 생성 실패");checks.Add("근접: 실제 저장 창 · 한국어 경로");
        board.SetSlots(new ModSlot?[8],true);await NativeSessionChecks.Dialog(load,path);
        NativeSessionChecks.Yes(NativeSessionChecks.Json(ReadBuild())==original,"근접 실제 불러오기 불일치");checks.Add("근접: 실제 불러오기 · 빌드 일치");
        await NativeSessionChecks.Dialog(save,null,true);await NativeSessionChecks.Dialog(load,null,true);
        NativeSessionChecks.Yes(NativeSessionChecks.Json(ReadBuild())==original,"근접 취소 뒤 상태 변경");checks.Add("근접: 저장·불러오기 취소 보존");
        foreach(var (label,value) in new[]{("손상","{broken"),("필드 누락","{}"),("슬롯 오류",NativeSessionChecks.Json(ReadBuild() with{Slots=[]}))})
        {
            var bad=Path.Combine(dir,"근접 "+label+".json");File.WriteAllText(bad,value);await NativeSessionChecks.Dialog(load,bad);
            NativeSessionChecks.Yes(error.Text.StartsWith("불러오기 실패")&&NativeSessionChecks.Json(ReadBuild())==original,"근접 "+label+" 불러오기 보존 실패");checks.Add("근접: "+label+" 거부 · 기존 빌드 보존");
        }
        foreach(var kind in new[]{"primary","secondary"}){ShowRanged(kind);UpdateLayout();checks.AddRange(await rangedPages[kind].CheckNativeFiles(dir));}
        return new{passed=true,checks};
    }
    internal async Task<object> CheckWorkspaceProcess(string dir,bool restore)
    {
        var path=Path.Combine(dir,"workspace.json");EnableWorkspacePersistence(path);
        if(!restore)
        {
            comparisonBuild=ReadBuild();rivenValues[0].Text="입력 중";rivenAttempts.Text="17";ApplySampleSetting(32);
            foreach(var kind in new[]{"primary","secondary"}){ShowRanged(kind);UpdateLayout();await rangedPages[kind].PreparePersistentComparison();}
            ShowRanged("primary");
            await Task.Delay(1300);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            NativeSessionChecks.Yes(File.Exists(path),"앱 실행 중 변경 자동 저장 누락");
            var expected=CaptureUpdateWorkspace();File.WriteAllText(Path.Combine(dir,"expected.json"),NativeSessionChecks.Json(expected));
            // The normal Closing event, not a test-only serializer, must write it.
            Close();NativeSessionChecks.Yes(File.Exists(path),"정상 종료에서 자동 저장되지 않음");
            return new{passed=true,phase="정상 종료",process=Environment.ProcessId};
        }
        var actual=CaptureUpdateWorkspace();var expectedJson=File.ReadAllText(Path.Combine(dir,"expected.json"));
        NativeSessionChecks.Yes(NativeSessionChecks.Json(actual)==expectedJson,"별도 프로세스 재실행 상태 불일치");
        foreach(var ranged in rangedPages.Values)ranged.CheckPersistentComparison();
        var stale=new RangedPage("primary");stale.Restore(rangedPages["primary"].Capture());
        stale.CheckStaleComparison(rangedPages["primary"].CaptureSession());
        NativeSessionChecks.Yes(rivenValues[0].Text=="입력 중"&&comparisonBuild is not null,"근접·리벤 초안·비교 기준 복원 누락");
        // Verify automatic save and recovery without touching production files.
        var modified=actual with{ActivePage=3};AtomicJsonFile.Write(path,modified);AtomicJsonFile.Write(path,actual);
        NativeSessionChecks.Yes(AtomicJsonFile.Read<UpdateWorkspace>(path+".bak").ActivePage==3,"원자 저장 백업 누락");
        File.WriteAllText(path,"{broken");
        var recovered=new MainWindow();recovered.EnableWorkspacePersistence(path);
        NativeSessionChecks.Yes(recovered.navigation[3].IsChecked==true,"손상된 최신 저장본의 백업 복원 실패");recovered.Close();
        App.Render((FrameworkElement)Content,Path.Combine(dir,"restored-primary.png"),1760,920);
        return new{passed=true,phase="별도 프로세스 복원",process=Environment.ProcessId,checks=new[]{"근접·주무기·보조무기 빌드","근접·주무기·보조무기 비교 기준","주무기·보조무기 A/B 계산 결과","리벤 입력 중 텍스트","선택 페이지·표본 설정","원자 저장·백업","손상 파일 보존·백업 복원"}};
    }
}

internal sealed partial class RangedPage
{
    internal async Task<string[]> CheckNativeFiles(string dir)
    {
        var checks=new List<string>();var fixture=Capture();var damageMod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==(category=="primary"?"Serration":"Hornet Strike"));
        fixture.Slots[0]=new(damageMod.Id,damageMod.MaxRank);fixture.Polarities[0]="Madurai";
        Restore(fixture with{Arcane="Merciless",ArcaneStacks=4,Distance=13,Enemy=fixture.Enemy! with{Level=123,Eximus=true,Difficulty=(EnemyDifficulty)1},EncounterMode=4});
        var original=Capture();
        var save=LayoutDiagnostics.Tree(this).OfType<Button>().Single(b=>b.Content as string=="저장");
        var load=LayoutDiagnostics.Tree(this).OfType<Button>().Single(b=>b.Content as string=="불러오기");
        var path=Path.Combine(dir,category+" 한국어 빌드.wfgun.json");
        await NativeSessionChecks.Dialog(save,path);NativeSessionChecks.Yes(File.Exists(path),category+" 저장 실패");checks.Add(category+": 실제 저장 창 · 한국어 경로");
        var changed=original with{Distance=original.Distance+2};Restore(changed);await NativeSessionChecks.Dialog(load,path);
        NativeSessionChecks.Yes(NativeSessionChecks.Json(Capture())==NativeSessionChecks.Json(original),category+" 실제 불러오기 불일치");checks.Add(category+": 실제 불러오기 · 모든 필드 일치");
        await NativeSessionChecks.Dialog(load,null,true);await NativeSessionChecks.Dialog(save,null,true);
        NativeSessionChecks.Yes(NativeSessionChecks.Json(Capture())==NativeSessionChecks.Json(original),category+" 취소 뒤 변경");checks.Add(category+": 저장·불러오기 취소 보존");
        foreach(var (name,content) in new[]{("손상","{broken"),("슬롯 오류",NativeSessionChecks.Json(original with{Slots=[]})),("다른 무기 구분",NativeSessionChecks.Json(original with{WeaponId=RangedEngine.Weapons.First(w=>w.Category!=category&&w.Category is "primary" or "secondary").Id}))})
        {
            var invalid=Path.Combine(dir,category+name+".wfgun.json");File.WriteAllText(invalid,content);await NativeSessionChecks.Dialog(load,invalid);
            NativeSessionChecks.Yes(message.Text.StartsWith("불러오기 실패")&&NativeSessionChecks.Json(Capture())==NativeSessionChecks.Json(original),category+name+" 잘못된 파일이 빌드를 변경함");checks.Add(category+": "+name+" 거부 · 기존 빌드 보존");
        }
        var flawed=RangedEngine.ModCatalog.Mods.Where(m=>m.EnglishName.StartsWith("Flawed")).ToArray();
        NativeSessionChecks.Yes(flawed.All(m=>Regex.IsMatch(m.Name,"[가-힣]")),"손상된 모드 한국어 이름 누락");
        NativeSessionChecks.Yes(RangedEngine.ModCatalog.Mods.Where(m=>!RangedRivens.IsRiven(m)).All(m=>Regex.IsMatch(KoreanPresentation.ModDescription(m,m.MaxRank),"[가-힣]")),"총기 모드 한국어 설명 누락");
        var untranslated=RangedEngine.Weapons.SelectMany(w=>RangedEngine.Attacks(w)).Select(a=>KoreanPresentation.Attack(RangedEngine.S(a,"AttackName"))).Where(t=>Regex.IsMatch(t,@"[A-Za-wyz]{2,}")).Distinct().ToArray();
        NativeSessionChecks.Yes(untranslated.Length==0,"공격 구성요소 번역 누락: "+string.Join(" / ",untranslated));
        checks.Add(category+": 총기 모드 한국어 이름·설명과 전체 공격 표시 검사");
        query.Text=category=="primary"?"Flawed Rifle Aptitude":"Flawed Sure Shot";RefreshLibrary();
        App.Render((FrameworkElement)Window.GetWindow(this).Content,Path.Combine(dir,category+"-localized-mod.png"),1760,920);
        query.Text="";return checks.ToArray();
    }
    internal async Task PreparePersistentComparison()
    {
        Restore(Capture() with{EncounterMode=1});
        async Task Run(){await NativeSessionChecks.Invoke(calculateButton);var t=Stopwatch.StartNew();while(simulation is not null&&t.Elapsed.TotalSeconds<60)await Task.Delay(40);NativeSessionChecks.Yes(lastCombat is not null,"비교 기준용 실제 계산 실패");}
        await Run();await NativeSessionChecks.Invoke(pinComparison);
        var b=Capture();var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.EnglishName==(category=="primary"?"Serration":"Hornet Strike"));b.Slots[0]=new(mod.Id,mod.MaxRank);Restore(b);await Run();
    }
    internal void CheckPersistentComparison()
    {
        NativeSessionChecks.Yes(comparisonBaseline is not null&&lastCombat is not null&&comparisonHost.Content is not null&&pinComparison.IsEnabled,"재실행 비교 결과 UI 누락");
        NativeSessionChecks.Yes(comparisonBaseline!.Result.Damage.Mean!=lastCombat!.Result.Damage.Mean,"재실행 기준/현재 결과가 섞임");
    }
    internal void CheckStaleComparison(SavedSession session)
    {
        RestoreSession(session with{Engine="old-engine"});
        NativeSessionChecks.Yes(comparisonBaseline is not null&&lastCombat is null&&!comparisonCacheValid,"엔진 변경 후 오래된 결과 표시");
        var resaved=CaptureSession();RestoreSession(resaved);
        NativeSessionChecks.Yes(!comparisonCacheValid&&lastCombat is null,"재저장 뒤 오래된 결과를 새 결과로 취급");
    }
    internal void CheckUpdatedComparison(SavedSession expected)
    {
        NativeSessionChecks.Yes(NativeSessionChecks.Json(comparisonBaseline)==NativeSessionChecks.Json(expected.Baseline),"업데이트 후 비교 기준 빌드·원래 결과 손실");
        if(expected.Engine==EngineIdentity.Value)
        {
            CheckPersistentComparison();
            NativeSessionChecks.Yes(NativeSessionChecks.Json(CaptureSession())==NativeSessionChecks.Json(expected),"동일 엔진 업데이트 후 비교 결과 불일치");
        }
        else
        {
            NativeSessionChecks.Yes(comparisonBaseline is not null&&lastCombat is null&&!comparisonCacheValid&&comparisonHost.Content is not null&&!pinComparison.IsEnabled,"업데이트로 엔진이 바뀐 뒤 이전 결과가 현재 계산으로 표시됨");
            NativeSessionChecks.Yes(CaptureSession().Engine=="invalidated","업데이트 뒤 이전 계산의 무효화 표식 누락");
        }
    }
}
