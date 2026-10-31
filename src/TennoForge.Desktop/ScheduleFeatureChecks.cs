using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Media.Effects;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    internal async Task<string[]> CheckFeatures(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        static void Toggle(CheckBox b)=>((IToggleProvider)new CheckBoxAutomationPeer(b).GetPattern(PatternInterface.Toggle)).Toggle();
        var saved=Preferences;var savedData=Data;var savedClock=Clock;
        Click(Mission);await Dispatcher.Yield();Yes(Mission.Menu.IsOpen,"미션 유형 버튼 실제 Invoke → 체크박스 목록 열림");
        if(Mission.Choices["감청"].IsChecked!=true)Toggle(Mission.Choices["감청"]);Toggle(Mission.Choices["감청"]);await Dispatcher.Yield();
        Yes(ArbitrationList.Items.Cast<ListBoxItem>().All(i=>CatalogData.Nodes.Single(n=>n.Id==((ArbitrationSlot)i.Tag).NodeId).Mission!="감청"),"감청 실제 체크 해제 → S티어에서도 노출 안 됨");
        Yes(!NextNode.Text.Contains("감청"),"감청 제외 → 다음 S티어 타이머에도 적용");Mission.Menu.IsOpen=false;Mission.SetExcluded([]);
        Click(FeaturesButton);await Dispatcher.Yield(DispatcherPriority.ContextIdle);Yes(featureWindow?.IsVisible==true,"오버레이·성유물 알림 실제 버튼으로 설정 창 열림");
        var dialog=featureWindow!;var root=(FrameworkElement)dialog.Content;
        var picks=LayoutDiagnostics.Tree(root).OfType<Expander>().Single(e=>Equals(e.Header,"표시할 일정 선택"));((IExpandCollapseProvider)new ExpanderAutomationPeer(picks).GetPattern(PatternInterface.ExpandCollapse)).Expand();await Dispatcher.Yield();
        async Task Tab(string name){((ISelectionItemProvider)new RadioButtonAutomationPeer(FeatureTabs[name]).GetPattern(PatternInterface.SelectionItem)).Select();await Dispatcher.Yield(DispatcherPriority.ContextIdle);}
        Button Find(string name)=>LayoutDiagnostics.Tree(root).OfType<Button>().Single(b=>Equals(b.Content,name));
        await Tab("알림");Click(Find("이 조건으로 알림 추가"));await Dispatcher.Yield();Yes(Preferences.FissureRules.Any(r=>r.Missions.SequenceEqual(new[]{"MT_VOID_CASCADE"})&&r.Mode==1),"기본 강길 범람 조건 실제 추가");
        await Tab("표시");var fissurePin=LayoutDiagnostics.Tree(root).OfType<CheckBox>().Single(c=>Equals(c.Content,"알림 조건에 맞는 성유물 균열"));if(fissurePin.IsChecked!=true)Toggle(fissurePin);Yes(Preferences.OverlayItems.Contains("fissures"),"알림과 게임 오버레이 표시 조건 연결");
        ((IExpandCollapseProvider)new ExpanderAutomationPeer(picks).GetPattern(PatternInterface.ExpandCollapse)).Collapse();await Dispatcher.Yield();
        int alerts=0;AlertSink=a=>alerts+=a.Length;var now=DateTimeOffset.Parse("2026-10-26T00:00:00Z");Clock=()=>now;
        var f=new FissureMission("ui-check-1","자리만 · 투불 광장","MT_VOID_CASCADE",6,true,false,now.AddMinutes(-1),now.AddHours(1));
        var snapshot=Data.Snapshot! with{Timestamp=now,Fissures=[f,f with{Id="ignore-normal",SteelPath=false}],FissuresUtc=now};
        int dashboardBefore=DashboardUpdateCount;DashboardVisibilityOverride=false;
        UseData(Data with{Snapshot=snapshot,Cached=false});Yes(alerts==1,"숨긴 일정의 데이터 갱신 → 강길 범람만 1회 알림");
        Tick();Tick();Yes(DashboardUpdateCount==dashboardBefore&&alerts==1,"백그라운드에서는 메인 UI 재구성 없이 알림·중복 방지 유지");
        DashboardVisibilityOverride=true;Tick();Yes(DashboardUpdateCount==dashboardBefore+1,"일정을 다시 표시하면 최신 대시보드를 즉시 갱신");DashboardVisibilityOverride=null;
        Tick();Tick();Yes(alerts==1,"1초 타이머·재렌더링에서 중복 알림 없음");UseData(Data with{Cached=true});Yes(alerts==1,"연결 실패 캐시 자료에서 알림 안 함");UseData(Data with{Cached=false});Yes(alerts==1,"연결 복구 후 같은 균열 재알림 안 함");
        Yes(OverlayData(now).Fissures.Length==2&&OverlayData(now).Fissures.Count(f=>f.Highlight)==1,"현재 열린 균열을 모두 표시하고 알림 대상 구분");
        await Tab("알림");var notify=LayoutDiagnostics.Tree(root).OfType<CheckBox>().Single(c=>Equals(c.Content,"새 게임 얼럿 알림"));Toggle(notify);Yes(Preferences.NotifyGameAlerts,"새 게임 얼럿 알림 실제 켜기");
        var gameAlert=new WorldAlertMission("ui-alert-1","에리스 · Saxis","MT_EXTERMINATION","월육 ×50 · 17,000 크레딧",false,now.AddMinutes(-1),now.AddHours(1));
        var alertSnapshot=Data.Snapshot! with{Alerts=[gameAlert],AlertsUtc=now};UseData(Data with{Snapshot=alertSnapshot});Yes(alerts==2&&OverlayData(now).Timers.Any(t=>t.Kind=="alert"),"게임 얼럿 데이터 갱신 → 알림·오버레이 연결");Tick();Yes(alerts==2,"게임 얼럿 중복 알림 차단");
        var rewardFilter=LayoutDiagnostics.Tree(root).OfType<TextBox>().Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="얼럿 보상 필터");rewardFilter.Text="니타인";Yes(Preferences.AlertRewardFilter=="","입력 중인 보상 필터를 미리 확정하지 않음");Click(Find("적용"));await Dispatcher.Yield();UseData(Data with{Snapshot=alertSnapshot with{Alerts=[gameAlert with{Id="ui-alert-2"}]}});Yes(alerts==2,"보상 필터에 맞지 않는 얼럿은 알리지 않음");rewardFilter.Text="월육";Click(Find("적용"));await Dispatcher.Yield();Yes(alerts==3,"보상 필터 변경 → 일치한 얼럿 알림");Toggle(notify);UseData(Data with{Snapshot=alertSnapshot});
        foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})foreach(var size in new[]{(Width:1100,Height:780),(Width:960,Height:650)})foreach(var tab in new[]{"표시","알림","스타일"})
        {
            Theme.Apply(dark);await Tab(tab);dialog.MinWidth=960;dialog.MinHeight=650;dialog.Width=size.Width;dialog.Height=size.Height;await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);dialog.UpdateLayout();
            render(root,$"schedule-feature-dialog-{tab}-{dark}-{scale:0.##}-{size.Width}",size.Width,size.Height,scale);
            Yes(Math.Abs(root.ActualWidth-size.Width)<2&&Math.Abs(root.ActualHeight-size.Height)<2&&Math.Abs(dialog.ActualWidth-size.Width)<2&&Math.Abs(dialog.ActualHeight-size.Height)<2,"설정 창 실제 HWND와 렌더링 크기 일치");
            var issues=LayoutDiagnostics.Inspect(root);if(issues.Length>0)File.WriteAllText(Path.Combine(directory,"feature-layout-failure.json"),JsonSerializer.Serialize(issues,Catalog.Json));Yes(issues.Length==0,$"오버레이·알림 창 {dark} 배율 {scale} 글자·컨트롤 경계");
            foreach(var s in LayoutDiagnostics.Tree(root).OfType<ScrollViewer>().Where(s=>s.IsVisible&&s.ScrollableHeight>0).ToArray()){s.ScrollToEnd();await Dispatcher.Yield(DispatcherPriority.ContextIdle);s.UpdateLayout();Yes(Math.Abs(s.VerticalOffset-s.ScrollableHeight)<1,"설정 창 스크롤 끝 도달");s.ScrollToTop();}
        }
        Theme.Apply(true);await Tab("표시");dialog.Width=1100;dialog.Height=780;await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);dialog.UpdateLayout();render(root,"schedule-feature-dialog-final",1100,780,1);await Tab("알림");var sound=LayoutDiagnostics.Tree(root).OfType<CheckBox>().Single(c=>Equals(c.Content,"소리 알림"));Toggle(sound);Yes(!Preferences.Sound,"소리 알림 실제 토글 → 설정 반영");Toggle(sound);Click(Find("알림 테스트"));await Dispatcher.Yield();
        Click(Find("위치·크기 조정"));await Dispatcher.Yield();Yes(overlay?.Editing==true&&overlay.IsVisible,"위치·크기 조정 실제 버튼 → 편집 오버레이");
        var active=overlay!;Yes(active.ResizeMode==ResizeMode.CanResize&&System.Windows.Shell.WindowChrome.GetWindowChrome(active) is null,"편집 창 표준 이동·크기 조절 지원");
        var h=new WindowInteropHelper(active).Handle;var dpi=VisualTreeHelper.GetDpi(active);new OverlayPositioner().Apply(h,new((int)((active.Left+24)*dpi.DpiScaleX),(int)((active.Top+16)*dpi.DpiScaleY),(int)(430*dpi.DpiScaleX),(int)(470*dpi.DpiScaleY)));await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);active.UpdateLayout();
        var titlePoint=active.PointToScreen(new Point(12,16));var corner=active.PointToScreen(new Point(active.ActualWidth-2,active.ActualHeight-2));
        static IntPtr Packed(Point p)=>new(unchecked((int)(((int)p.Y&65535)<<16|((int)p.X&65535))));
        Yes(HitTestWindow(h,0x84,IntPtr.Zero,Packed(titlePoint)).ToInt64()==2,"실제 HWND 제목줄 HTCAPTION → 표준 창 이동");
        Yes(HitTestWindow(h,0x84,IntPtr.Zero,Packed(corner)).ToInt64()==17,"실제 HWND 모서리 HTBOTTOMRIGHT → 표준 창 크기 조절");
        Yes(!LayoutDiagnostics.Tree(active).OfType<TextBlock>().Any(t=>t.Text.Contains("여기를 끌어")),"하단 이동·크기 조절 막대 제거");
        var editSteel=LayoutDiagnostics.Tree(active).OfType<CheckBox>().Single(c=>Equals(c.Content,"강철의 길만"));Toggle(editSteel);Yes(Preferences.FissureView==1&&OverlayData(now).Fissures.Length==1,"편집 제목줄에서 강철의 길만 바로 선택");Toggle(editSteel);
        render((FrameworkElement)active.Content,"schedule-overlay-edit",430,470,1);
        var done=LayoutDiagnostics.Tree(active).OfType<Button>().Single(b=>Equals(b.Content,"완료"));Click(done);await Dispatcher.Yield();Yes(!active.Editing&&Math.Abs(Preferences.Width-430)<1&&Math.Abs(Preferences.Height-470)<1,"창 자체 완료 버튼 → 위치·크기 저장");
        var style=RelicOverlay.GetStyle(new WindowInteropHelper(active).Handle,-20).ToInt64();Yes((style&RelicOverlay.PassThroughStyle)==RelicOverlay.PassThroughStyle,"일정 오버레이 NOACTIVATE·TRANSPARENT·TOOLWINDOW 확인");
        var before=RelicCapture.GetForegroundWindow();active.Update("12:34:56",OverlayData(now),Preferences);active.PresentAt(new(100,100,1920,1080));await Dispatcher.Yield();Yes(RelicCapture.GetForegroundWindow()==before,"오버레이 표시·갱신 시 포커스 유지");
        render((FrameworkElement)active.Content,"schedule-game-overlay",430,470,1);Yes(LayoutDiagnostics.Inspect((FrameworkElement)active.Content).Length==0,"일정 게임 오버레이 가독성·줄 경계");
        Slider Slider(string name)=>LayoutDiagnostics.Tree(root).OfType<Slider>().Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)==name);
        static void Set(Slider slider,double value)=>((IRangeValueProvider)new SliderAutomationPeer(slider).GetPattern(PatternInterface.RangeValue)).SetValue(value);
        await Tab("스타일");Set(Slider("배경 투명도"),60);Set(Slider("그림자 진하기"),90);Set(Slider("글자 크기"),120);await Tab("알림");Set(Slider("알림 음량"),40);await Tab("스타일");
        Yes(Math.Abs(Preferences.BackgroundOpacity-.4)<.01&&Preferences.ShadowStrength==.9&&Preferences.TextScale==1.2&&Preferences.SoundVolume==.4,"실제 슬라이더로 배경·그림자·글자·음량 저장");
        active.Update("12:34:56",OverlayData(now),Preferences);Yes(((SolidColorBrush)((Border)active.Content).Background).Color.A==102&&active.Opacity==1,"배경만 반투명 · 글자와 창 전체 불투명도 유지");
        Yes(LayoutDiagnostics.Tree(active).OfType<TextBlock>().Any(t=>t.Effect is DropShadowEffect e&&e.Opacity==.9),"오버레이 글자 그림자 실시간 반영");
        var shadow=LayoutDiagnostics.Tree(root).OfType<CheckBox>().Single(c=>Equals(c.Content,"글자 그림자"));Toggle(shadow);active.Update("12:34:56",OverlayData(now),Preferences);Yes(LayoutDiagnostics.Tree(active).OfType<ScheduleOverlayView>().All(v=>!LayoutDiagnostics.Tree(v).OfType<TextBlock>().Any(t=>t.Effect is DropShadowEffect)),"그림자 끄기 반영");Toggle(shadow);
        Preferences=Preferences with{TextScale=1,BackgroundOpacity=.82};active.Update("12:34:56",OverlayData(now),Preferences);
        Yes(!LayoutDiagnostics.Tree(active).OfType<TextBlock>().Any(t=>t.Text=="WF Labs · 일정"),"불필요한 오버레이 브랜드 문구 제거");
        foreach(var size in new[]{(Width:340,Height:470),(Width:760,Height:620)})foreach(var scale in new[]{1d,1.25,1.5})
        {
            active.Width=size.Width;active.Height=size.Height;await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);active.UpdateLayout();render((FrameworkElement)active.Content,$"schedule-overlay-{size.Width}-{scale:0.##}",size.Width,size.Height,scale);
            var issues=LayoutDiagnostics.Inspect((FrameworkElement)active.Content);if(issues.Length>0)File.WriteAllText(Path.Combine(directory,"overlay-layout-failure.json"),JsonSerializer.Serialize(issues,Catalog.Json));Yes(issues.Length==0,"컴팩트·가로형 오버레이 배율 "+scale+" 글자 경계");
            var primary=LayoutDiagnostics.Tree(active).OfType<ScheduleOverlayView>().Single().PrimaryContent;Yes(LayoutDiagnostics.Tree(primary).OfType<TextBlock>().Count(t=>t.IsVisible&&(t.Text.StartsWith("일정")||t.Text.StartsWith("성유물 균열 ·")))==2,"일정과 현재 성유물 목록을 하나의 본문에 표시");
        }
        await Tab("표시");var steelPath=LayoutDiagnostics.Tree(root).OfType<RadioButton>().Single(r=>System.Windows.Automation.AutomationProperties.GetName(r)=="균열 경로 강철의 길만");((ISelectionItemProvider)new RadioButtonAutomationPeer(steelPath).GetPattern(PatternInterface.SelectionItem)).Select();await Dispatcher.Yield();Yes(OverlayData(now).Fissures.Length==1&&OverlayData(now).Fissures[0].Badge.Contains("강철의 길"),"강철의 길만 한 번 선택해 필터 적용");var allPaths=LayoutDiagnostics.Tree(root).OfType<RadioButton>().Single(r=>System.Windows.Automation.AutomationProperties.GetName(r)=="균열 경로 전체");((ISelectionItemProvider)new RadioButtonAutomationPeer(allPaths).GetPattern(PatternInterface.SelectionItem)).Select();await Dispatcher.Yield();
        var model=OverlayData(now);var many=model with{Timers=Enumerable.Range(0,9).Select(i=>new ScheduleOverlayEntry("timer-"+i,"지구 · 아이돌론 평원","밤 · 다음 낮까지","00:42:10")).ToArray(),Fissures=Enumerable.Range(0,29).Select(i=>new ScheduleOverlayEntry("fissure-"+i,"옴니아 · 보이드 범람","자리만 · 투불 광장","01:15:00",i%2==0?"강철의 길":"일반",i==0)).ToArray()};
        active.Width=480;active.Height=620;await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);active.Update("12:34:56",many,Preferences);render((FrameworkElement)active.Content,"schedule-overlay-many",480,620,1);
        var overlayView=LayoutDiagnostics.Tree(active).OfType<ScheduleOverlayView>().Single();var compactRows=LayoutDiagnostics.Tree(overlayView.PrimaryContent).OfType<Border>().Where(b=>Equals(b.Tag,"fissure")&&b.ActualHeight>0).ToArray();Yes(compactRows.Length==29&&compactRows.All(b=>b.ActualHeight<=55),"성유물 행 29개 모두 두 줄·55 DIP 이하");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Yes(overlayView.Marquee.Running,"많은 일정·29개 균열은 본문 전체 자동 순환");
        Yes(LayoutDiagnostics.Tree(active).OfType<ScrollViewer>().All(s=>s.VerticalScrollBarVisibility!=ScrollBarVisibility.Auto&&s.VerticalScrollBarVisibility!=ScrollBarVisibility.Visible),"오버레이 수동 스크롤바 제거");
        Yes(LayoutDiagnostics.Inspect((FrameworkElement)active.Content).Length==0,"많은 일정·균열도 본문 안에서 표시");
        var wave=ScheduleNotificationSound.Wave(1);var muted=ScheduleNotificationSound.Wave(0);Yes(System.Text.Encoding.ASCII.GetString(wave,0,4)=="RIFF"&&wave.Length>48000,"전용 알림 WAV 리소스 내장");Yes(muted.Skip(44).All(b=>b==0)&&wave.Skip(44).Any(b=>b!=0),"전용 음원 음량 0은 무음 · 원본 PCM 유효");
        active.Conceal();
        using(var popup=new ToastLease(new ScheduleToast("강철의 길 · 보이드 범람 열림","자리만 · 투불 광장 · 옴니아\n종료 10-26 10:00",new(100,100,1920,1080))))
        {popup.Window.Show();await Dispatcher.Yield();Yes(RelicCapture.GetForegroundWindow()==before,"실제 팝업 표시 시 포커스 유지");render((FrameworkElement)popup.Window.Content,"schedule-toast",420,150,1);}
        Click(Find("닫기"));await Dispatcher.Yield();Yes(featureWindow is null,"설정 창 실제 닫기");
        string prefsFolder=Path.Combine(directory,"feature-preferences");Directory.CreateDirectory(prefsFolder);File.WriteAllText(Path.Combine(prefsFolder,"preferences.json"),JsonSerializer.Serialize(Preferences with{ExcludedMissions=["감청"]},Catalog.Json));
        using(var one=new SchedulePage(false,prefsFolder)){one.Clock=()=>now;one.UseData(savedData with{Snapshot=snapshot,Cached=false});Yes(one.Mission.Excluded.Contains("감청")&&one.Preferences.Width==430&&one.Preferences.FissureRules.Any(r=>r.Mode==1),"재시작 시 제외 미션·알림 조건·오버레이 크기 복원");}
        using(var two=new SchedulePage(false,prefsFolder)){two.Clock=()=>now;two.UseData(savedData with{Snapshot=snapshot,Cached=false});Yes(two.CollectAlerts().Length==0,"재시작 후 이미 알려준 균열 중복 알림 방지");}
        AlertSink=null;Clock=savedClock;Preferences=saved;Mission.SetExcluded(saved.ExcludedMissions);UseData(savedData);return checks.ToArray();
    }
    private sealed class ToastLease(ScheduleToast window):IDisposable{internal ScheduleToast Window=>window;public void Dispose()=>window.Close();}
    [DllImport("user32.dll",EntryPoint="SendMessageW")]private static extern IntPtr HitTestWindow(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
}
