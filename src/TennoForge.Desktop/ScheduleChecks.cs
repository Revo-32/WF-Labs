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
    internal async Task<object> CheckSchedules(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var scans=new List<object>();void Yes(bool b,string s){if(!b)throw new InvalidOperationException(s);checks.Add(s);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        static void Toggle(System.Windows.Controls.Primitives.ToggleButton b)=>((IToggleProvider)new ToggleButtonAutomationPeer(b).GetPattern(PatternInterface.Toggle)).Toggle();
        static void Select(RadioButton b)=>((ISelectionItemProvider)new RadioButtonAutomationPeer(b).GetPattern(PatternInterface.SelectionItem)).Select();
        async Task Choose(ComboBox box,int index)
        {var peer=new ComboBoxAutomationPeer(box);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);expand.Expand();await Dispatcher.Yield();box.UpdateLayout();var items=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();((ISelectionItemProvider)items[index].GetPattern(PatternInterface.SelectionItem)).Select();expand.Collapse();await Dispatcher.Yield();}
        Select(navigation[6]);await Dispatcher.Yield();var p=schedulePage!;render(this,"schedule-empty",1760,920,1);Yes(navigation[6].IsChecked==true&&p.NextCountdown.Text!="—","일정 탭 실제 선택 · 내장 다음 S티어 즉시 표시");
        var now=DateTimeOffset.Parse("2026-10-26T00:00:00Z");p.Clock=()=>now;
        var snapshot=new WorldScheduleSnapshot(now,[new("cetusCycle","day",now.AddMinutes(20)),new("vallisCycle","cold",now.AddMinutes(10)),new("cambionCycle","fass",now.AddMinutes(20)),new("zarimanCycle","grineer",now.AddMinutes(90)),new("duviriCycle","envy",now.AddMinutes(60))],null,null,null,null,null,null,"");
        var data=new WorldScheduleData(snapshot,p.Data.Arbitrations,false,null,now);p.UseData(data);Yes(p.NextCountdown.Text=="03:00:00"&&p.NextNode.Text.Contains("Callisto"),"알려진 예측 03UTC Callisto · 3시간 카운트다운");now=now.AddSeconds(1);p.Tick();Yes(p.NextCountdown.Text=="02:59:59","로컬 초 단위 카운트다운");
        var savedNow=now;now=DateTimeOffset.Parse("2026-10-26T03:00:00Z");p.Tick();Yes(p.Ongoing.Visibility==Visibility.Visible&&p.Ongoing.Text.Contains("Callisto")&&p.NextNode.Text.Contains("10-27")&&p.NextCountdown.Text=="1일 11:00:00","시작 경계에서 진행 중 S티어와 다음 S티어 분리 · 같은 노드가 다시 나오는 일정");render(this,"schedule-ongoing-s",1760,920,1.5);File.WriteAllText(Path.Combine(directory,"ongoing-layout.json"),JsonSerializer.Serialize(LayoutDiagnostics.Inspect(p),Catalog.Json));Yes(LayoutDiagnostics.Inspect(p).Length==0,"진행 중 S티어 추가 행 · 최소 창 150% 잘림 없음");now=savedNow;p.UseData(data);
        await Choose(p.Tier,2);await Choose(p.Horizon,0);Yes(p.ArbitrationList.Items.Count==25,"드롭다운 실제 펼치기·모든 등급 24시간 적용");await Choose(p.Tier,0);Yes(p.ArbitrationList.Items.Count==1,"S티어 필터 실제 적용");
        await Choose(p.Horizon,2);Yes(p.ArbitrationList.Items.Count>1,"7일 S티어 목록 확대");var item=(ListBoxItem)p.ArbitrationList.Items[^1];p.ArbitrationList.ScrollIntoView(item);p.UpdateLayout();((ISelectionItemProvider)new ListBoxItemAutomationPeer(item,new ListBoxAutomationPeer(p.ArbitrationList)).GetPattern(PatternInterface.SelectionItem)).Select();Yes(p.ArbitrationDetail.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("시작")),"중재 선택 시 시각·노드 상세 표시");
        p.Mission.SetExcluded(p.Mission.Choices.Keys.Where(n=>n!="감청"));await Choose(p.Faction,p.Faction.Items.Cast<string>().ToList().IndexOf("코퍼스"));
        Yes(p.ArbitrationList.Items.Count>0&&p.ArbitrationList.Items.Cast<ListBoxItem>().All(i=>{var n=p.CatalogData.Nodes.Single(n=>n.Id==((ArbitrationSlot)i.Tag).NodeId);return n.Mission=="감청"&&n.Faction=="코퍼스";}),"미션 유형·팩션을 실제 드롭다운으로 동시 필터");
        p.Mission.SetExcluded(p.Mission.Choices.Keys.Where(n=>n!="거울상 방어"));Yes(p.NextNode.Text.Contains("Tyana Pass"),"그리니어·코퍼스 혼합 노드를 코퍼스 필터에 포함 · 다음 S도 연동");
        p.Mission.SetExcluded(p.Mission.Choices.Keys.Where(n=>n!="생존"));Yes(p.ArbitrationList.Items.Count==0&&p.NextCountdown.Text=="일정 없음","필터 결과 없음과 다음 S 없음 표시");Click(p.ResetFilters);await Dispatcher.Yield();Yes(p.Mission.Excluded.Length==0&&p.Faction.SelectedIndex==0&&p.ArbitrationList.Items.Count>0,"실제 초기화 버튼으로 필터 복원");
        Yes(p.ArbitrationList.IsVisible&&!p.WeaponList.IsVisible&&p.NextCountdown.IsVisible,"종합 일정에서 중재·다음 S 표시 · 상점은 무기 순환으로 분리");Yes(!LayoutDiagnostics.Tree(p).OfType<CheckBox>().Any(c=>c.IsVisible),"일정 메인 화면의 체크박스 제거 · 필터 기능 유지");
        checks.AddRange(await CheckWeaponRotations(directory,render));p.UseData(data);Select(p.PageTabs["무기 순환"]);await Dispatcher.Yield();
        await Choose(p.Shop,1);Yes(p.WeaponList.Items.Count==5,"테넷 상점 5개");await Choose(p.Shop,2);Yes(p.WeaponList.Items.Count==14,"코다 A·B 전체 14개");
        p.WeaponExpanded.IsExpanded=true;var wish=p.WeaponDetail.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Single();var chosen=(ScheduledWeapon)((ListBoxItem)p.WeaponList.SelectedItem).Tag;Toggle(wish);await Dispatcher.Yield();Yes(p.Wishes.Contains(chosen.Id),"실제 찜 버튼 반영");Toggle(p.WishOnly);await Dispatcher.Yield();Yes(p.WeaponList.Items.Count==1,"찜한 무기만 필터");Toggle(p.WishOnly);await Choose(p.Shop,0);
        int requested=0;p.RefreshOverride=(_,_)=>{requested++;return Task.FromResult(data with{Cached=true,Notice="검수용 연결 실패 · 마지막 자료 유지"});};Click(p.RefreshButton);await Dispatcher.Yield();Yes(requested==1&&p.Data.Cached&&p.RefreshButton.IsEnabled,"실제 새로고침 · 캐시 보존·버튼 재활성화");
        var preferences=Path.Combine(directory,"preferences-test");using(var one=new SchedulePage(false,preferences)){one.SetWish(chosen.Id,true);}using(var two=new SchedulePage(false,preferences)){Yes(two.Wishes.Contains(chosen.Id),"찜 목록 파일 저장·새 인스턴스 복원");}
        var sourcePanel=LayoutDiagnostics.Tree(p).OfType<Expander>().Single(e=>Equals(e.Header,"자료 기준·갱신 방식"));
        foreach(bool dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})foreach(var section in new[]{"대시보드","중재 상세","무기 상점 상세","자료 기준"})
        {
            SetDarkTheme(dark);Select(p.PageTabs[section=="무기 상점 상세"?"무기 순환":"종합 일정"]);p.ArbitrationExpanded.IsExpanded=section=="중재 상세";p.WeaponExpanded.IsExpanded=section=="무기 상점 상세";sourcePanel.IsExpanded=section=="자료 기준";await Dispatcher.Yield();string name=$"schedule-{section}-{(dark?"dark":"light")}-{scale:0.##}";render(this,name,1760,920,scale);
            var issues=LayoutDiagnostics.Inspect(p);if(issues.Length>0)File.WriteAllText(Path.Combine(directory,"layout-failure.json"),JsonSerializer.Serialize(issues,Catalog.Json));scans.Add(new{name,issues});Yes(issues.Length==0,name+" · 글자 잘림·요소 겹침 없음" );
            foreach(var t in LayoutDiagnostics.Tree(p).OfType<TextBlock>().Where(t=>t.IsVisible&&t.FontSize==20&&new[]{"낮","추움","파스","그리니어","질투"}.Contains(t.Text)))
            {
                static double L(System.Windows.Media.Color color){static double C(byte b){double x=b/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*C(color.R)+.7152*C(color.G)+.0722*C(color.B);}
                double fg=L(((System.Windows.Media.SolidColorBrush)t.Foreground).Color),bg=L((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark?"#101112":"#F4F4F2"));Yes((Math.Max(fg,bg)+.05)/(Math.Min(fg,bg)+.05)>=3,name+" · 상태 색상 대비 "+t.Text);
            }
            foreach(var scroll in LayoutDiagnostics.Tree(p).OfType<ScrollViewer>().Where(s=>s.IsVisible&&s.ViewportHeight>0&&s.ScrollableHeight>1).ToArray())
            {scroll.ScrollToEnd();await Dispatcher.Yield(DispatcherPriority.ContextIdle);scroll.UpdateLayout();Yes(Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<1,name+" · 스크롤 끝 도달");scroll.ScrollToTop();}
            foreach(var b in LayoutDiagnostics.Tree(p).OfType<Button>().Where(b=>b.IsVisible&&b.ActualWidth>0)){var box=b.TransformToAncestor(p).TransformBounds(new Rect(b.RenderSize));Yes(box.Left>=-1&&box.Right<=p.ActualWidth+1,name+" · 버튼 가로 경계 "+b.Content);}
        }
        sourcePanel.IsExpanded=false;SetDarkTheme(true);Select(p.PageTabs["무기 순환"]);p.WeaponExpanded.IsExpanded=true;var mod=p.WeaponDetail.Children.OfType<Button>().Single(b=>Equals(b.Content,"이 무기 모딩하기"));Click(mod);await Dispatcher.Yield();Yes(navigation[0].IsChecked==true,"상점 무기에서 모딩으로 실제 이동");
        ShowSchedules();Select(p.PageTabs["종합 일정"]);var mixed=snapshot with{Timestamp=now.AddMinutes(-20),Cycles=snapshot.Cycles.Select(c=>c with{SourceUtc=c.Id is "cetusCycle" or "cambionCycle"?now:now.AddMinutes(-20),Source=c.Id is "cetusCycle" or "cambionCycle"?"공식 월드 상태":"warframestat.us"}).ToArray()};p.UseData(data with{Snapshot=mixed});render(this,"schedule-mixed-sources",1760,920,1);var texts=LayoutDiagnostics.Tree(p).OfType<TextBlock>().Where(t=>t.IsVisible).ToArray();Yes(texts.Count(t=>t.Text.EndsWith("공식 자료"))==2&&texts.Count(t=>t.Text.EndsWith("저장 자료 예상"))==3,"혼합 출처 · 공식 보완 2지역과 오래된 3지역을 개별 표시");p.UseData(data);
        ShowSchedules();var workspace=CaptureUpdateWorkspace();ShowModding();ApplyUpdateWorkspace(workspace);Yes(navigation[6].IsChecked==true&&page.Content==p,"업데이트 작업 복원 · 일정 탭 유지");CheckHeaderLayout();render(this,"schedule-final",1920,1000,1);
        checks.AddRange(await p.CheckFeatures(directory,render));
        return new{passed=checks.Count,failed=Array.Empty<string>(),checks,scans};
    }
}
