using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private async Task<string[]> CheckWeaponRotations(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var scans=new List<object>();var p=schedulePage!;var prior=p.Data;var clock=p.Clock;var route=p.ModRequested;
        void Yes(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
        static void Select(RadioButton b)=>((ISelectionItemProvider)new RadioButtonAutomationPeer(b).GetPattern(PatternInterface.SelectionItem)).Select();
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        Button[] Choices(Panel panel)=>LayoutDiagnostics.Tree(panel).OfType<Button>().ToArray();
        var now=DateTimeOffset.Parse("2026-10-25T23:59:59Z");p.Clock=()=>now;
        try
        {
            p.UseData(prior with{Snapshot=prior.Snapshot! with{Incarnon=null},Cached=true});
            Select(p.PageTabs["무기 순환"]);await Dispatcher.Yield();render(this,"rotations-before-reset",1760,920,1);
            Yes(p.WeaponList.IsVisible&&!p.ArbitrationList.IsVisible&&p.IncarnonClock.IsVisible,"무기 순환 실제 UIA 선택 · 상점과 인카논 표시");
            Yes(p.IncarnonWeek.Text.Contains("8주차")&&p.IncarnonClock.Text=="00:00:01","주간 교체 직전8주차·1초 표시");
            now=now.AddSeconds(1);p.Tick();await Dispatcher.Yield();p.UpdateLayout();
            Yes(p.IncarnonWeek.Text.Contains("9주차")&&Choices(p.IncarnonCurrent).Length==5&&Choices(p.IncarnonNext).Length==5,"교체 경계에서 이번 주5개·다음 주5개 자동 전환");
            Yes(LayoutDiagnostics.Tree(p.IncarnonCurrent).OfType<TextBlock>().Any(t=>t.Text=="벡티스")&&LayoutDiagnostics.Tree(p.IncarnonNext).OfType<TextBlock>().Any(t=>t.Text=="브래튼"),"현재9주·다음1주 한국어 무기 이름");
            Yes(p.IncarnonSource.Text.Contains("예상"),"저장 순환표는 예상임을 명시");
            string? selected=null;p.ModRequested=id=>selected=id;Click(Choices(p.IncarnonCurrent)[0]);await Dispatcher.Yield();
            Yes(selected==IncarnonRotations.Table.Weeks[8].Weapons[0].Id,"인카논 선택 버튼 실제 UIA Invoke · 정확한 내부 무기 ID 전달");p.ModRequested=route;
            var live=new IncarnonRotationAnchor(now,now.AddDays(7),IncarnonRotations.Table.Weeks[8].Weapons.Select(w=>w.EnglishName).ToArray(),now,"공식 월드 상태");
            p.UseData(prior with{Snapshot=prior.Snapshot! with{Incarnon=live},Cached=false});
            Yes(p.IncarnonSource.Text.StartsWith("공식 월드 상태 확인"),"온라인 공식 선택지 확인·예상과 구분");
            var retained=Choices(p.IncarnonCurrent)[0];now=now.AddSeconds(1);p.Tick();Yes(ReferenceEquals(retained,Choices(p.IncarnonCurrent)[0]),"초 단위 타이머에서 선택 버튼 재생성·초점 소실 없음");
            int shop=p.Shop.SelectedIndex;Select(p.PageTabs["종합 일정"]);await Dispatcher.Yield();Yes(p.ArbitrationList.IsVisible&&!p.WeaponList.IsVisible,"종합 일정 복귀·불필요한 상점 요소 숨김");
            Select(p.PageTabs["무기 순환"]);await Dispatcher.Yield();Yes(p.Shop.SelectedIndex==shop&&ReferenceEquals(retained,Choices(p.IncarnonCurrent)[0]),"탭 왕복 시 상점 필터·무기 버튼 유지");
            var calendar=LayoutDiagnostics.Tree(p).OfType<Expander>().Single(e=>Equals(e.Header,"전체 9주 순환표"));
            var expander=(IExpandCollapseProvider)new ExpanderAutomationPeer(calendar).GetPattern(PatternInterface.ExpandCollapse);expander.Expand();await Dispatcher.Yield();Yes(calendar.IsExpanded&&p.IncarnonCalendar.Children.Count==9,"전체9주 표 실제 UIA 펼치기");expander.Collapse();
            foreach(bool dark in new[]{true,false})foreach(var (width,height,scale) in new[]{(1760,920,1d),(1536,820,1.25),(1280,688,1.5)})
            {
                SetDarkTheme(dark);Select(p.PageTabs["무기 순환"]);p.WeaponExpanded.IsExpanded=false;
                string name=$"rotations-{(dark?"dark":"light")}-{width}";render(this,name,width,height,scale);
                Yes(Math.Abs(((FrameworkElement)Content).ActualWidth-width)<2&&Math.Abs(((FrameworkElement)Content).ActualHeight-height)<2,name+" · 실제 창 측정 크기 일치");
                var issues=LayoutDiagnostics.Inspect(p);scans.Add(new{name,issues});File.WriteAllText(Path.Combine(directory,"rotation-layout-scans.json"),JsonSerializer.Serialize(scans,Catalog.Json));Yes(issues.Length==0,name+" · 잘림·겹침 없음");
                foreach(var b in Choices(p.IncarnonCurrent).Concat(Choices(p.IncarnonNext)))
                {var bounds=b.TransformToAncestor(p).TransformBounds(new Rect(b.RenderSize));Yes(bounds.Left>=0&&bounds.Right<=p.ActualWidth+1,name+" · 무기 버튼 가로 경계");}
                Yes(p.WeaponList.ActualHeight>=100&&p.IncarnonCurrent.IsVisible,name+" · 상점 목록과 이번 주 구성 접근 가능");
                p.WeaponExpanded.IsExpanded=true;await Dispatcher.Yield();render(this,name+"-details",width,height,scale);Yes(p.WeaponList.ActualHeight>=100&&LayoutDiagnostics.Inspect(p).Length==0,name+" · 무기 상세 펼침에서도 목록·글자 접근 가능");p.WeaponExpanded.IsExpanded=false;
                calendar.IsExpanded=true;await Dispatcher.Yield();render(this,name+"-calendar",width,height,scale);Yes(LayoutDiagnostics.Inspect(p).Length==0,name+" · 전체 순환표 펼침 레이아웃");calendar.IsExpanded=false;
                Select(p.PageTabs["종합 일정"]);await Dispatcher.Yield();render(this,name+"-overview",width,height,scale);Yes(LayoutDiagnostics.Inspect(p).Length==0,name+" · 종합 일정 레이아웃");
            }
            SetDarkTheme(true);Select(p.PageTabs["무기 순환"]);
            p.UseData(prior with{Snapshot=prior.Snapshot! with{Incarnon=live with{Choices=new[]{"Braton","Lato","Skana","Paris","UnknownWeapon"}}},Cached=false});await Dispatcher.Yield();p.UpdateLayout();
            Yes(p.IncarnonWeek.Text.Contains("새 구성")&&Choices(p.IncarnonCurrent).Count(b=>!b.IsEnabled)==1&&Choices(p.IncarnonNext).Length==0,"등록되지 않은 새 순환 · 모르는 무기만 비활성화·임의 예측 금지");
        }
        finally{p.Clock=clock;p.ModRequested=route;p.UseData(prior);Select(p.PageTabs["종합 일정"]);SetDarkTheme(true);}
        return checks.ToArray();
    }
}
