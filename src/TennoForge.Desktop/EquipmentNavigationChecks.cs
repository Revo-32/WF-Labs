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
    internal async Task<object> CheckEquipmentNavigation(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var scans=new List<object>();
        void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        static void Select(RadioButton button)=>((ISelectionItemProvider)new RadioButtonAutomationPeer(button).GetPattern(PatternInterface.SelectionItem)).Select();
        static void Click(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        ShowInventory();var p=inventoryPage!;var c=p.CatalogData;
        InventoryItem Item(string name)=>c.Items.Single(i=>i.EnglishName==name);
        var rhino=Item("Rhino");var volt=Item("Volt");var prime=Item("Braton Prime");var recipe=c.Recipes.First(r=>r.ResultId==prime.Id);
        var relic=c.Relics.First(r=>r.Refinement==0&&r.Rewards.Any(s=>s.Id==recipe.BlueprintId));
        var snapshot=InventorySnapshot.Empty with{CapturedUtc=DateTimeOffset.UtcNow,Source="검수용 목록",MasteryRank=30,Credits=1000000,InventoryComplete=true,MasteryComplete=true,
            Quantities=new(){{rhino.Id,1},{relic.Id,5}},MasteryRanks=new(){{rhino.Id,10},{Item("Lato").Id,30}},Equipment=[new(rhino.Id,10,0,false,[])],Crafting=[new(c.Recipes.First(r=>r.ResultId==volt.Id).BlueprintId,DateTimeOffset.UtcNow.AddHours(12))]};
        p.UseSnapshot(snapshot,false);await Dispatcher.Yield();
        Yes(p.MasteryList.IsVisible&&!p.RelicList.IsVisible&&p.Progress.IsVisible,"장비 전체 요약 · 성유물 추천을 표시하지 않음");
        foreach(var (group,tab) in p.AcquisitionTabs)
        {
            Select(tab);await Dispatcher.Yield();Yes(tab.IsChecked==true&&p.MasteryList.Items.Count>0,"획득 탭 실제 선택 · "+group);
            Yes(!p.Progress.IsVisible&&p.GoalsPane.Visibility==Visibility.Collapsed,"획득 목록에서는 요약·목표 편집을 접어 작업 공간 확보 · "+group);
        }
        Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Platinum]);await Dispatcher.Yield();
        Yes(p.MasteryList.Items.Cast<InventoryRow>().Any(r=>r.Id==Item("Soma").Id)&&p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.Id!=prime.Id),"상점 플래티넘 · 일반 상점 장비 포함, 프라임 거래 제외");
        Yes(p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.Value.Contains("상점")&&!r.Value.Contains("Cr")),"상점 탭 목록은 제작 크레딧 대신 상점 플래티넘 표시");
        Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Trade]);await Dispatcher.Yield();
        Yes(p.MasteryList.Items.Cast<InventoryRow>().Any(r=>r.Id==prime.Id)&&p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.Id!=Item("Soma").Id),"유저 간 거래 · 프라임 부품 포함, 일반 상점 장비 제외");
        Yes(p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.Value.Contains("거래")&&!r.Value.Contains("Cr")),"거래 탭 목록은 거래 시세 또는 시세 확인 상태 표시");
        foreach(var group in new[]{MasteryAcquisitionGroup.Platinum,MasteryAcquisitionGroup.Trade})
        {
            Select(p.AcquisitionTabs[group]);await Dispatcher.Yield();
            var visible=p.MasteryList.Items.Cast<InventoryRow>().Select(r=>(MasteryPlan)r.Model).Where(m=>!m.RankLocked&&m.AvailablePoints>0).ToArray();
            var costs=visible.Select(m=>MasteryPurchaseRecommendations.Per1000(m,group)??double.PositiveInfinity).ToArray();
            Yes(costs.SequenceEqual(costs.OrderBy(x=>x)),"실제 목록 · 마스터리당 구매비 오름차순 · "+group);
            Yes(p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.State.Contains("추천")||((MasteryPlan)r.Model).RankLocked),"추천 순서·추천 보류 이유 표시 · "+group);
        }
        Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Owned]);await Dispatcher.Yield();
        Yes(p.MasteryList.Items.Cast<InventoryRow>().Select(r=>r.Id).ToHashSet().SetEquals([rhino.Id,volt.Id]),"제작 중·보유 중 · 두 상태 모두 포함하고 숙련 완료 제외");
        Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Credits]);await Dispatcher.Yield();
        var peer=new ComboBoxAutomationPeer(p.CreditSource);((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Dispatcher.Yield();
        var rows=peer.GetChildren().Where(r=>r.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();((ISelectionItemProvider)rows[2].GetPattern(PatternInterface.SelectionItem)).Select();((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)).Collapse();await Dispatcher.Yield();
        Yes(p.MasteryList.Items.Cast<InventoryRow>().Any(r=>r.Id==Item("Banshee").Id)&&p.MasteryList.Items.Cast<InventoryRow>().All(r=>!new[]{Item("Paris").Id,Item("Braton").Id}.Contains(r.Id)),"크레딧 탭 · 실제 도장 구매처 선택");
        Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Farming]);await Dispatcher.Yield();
        p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().Single(r=>r.Id==prime.Id);p.UpdateLayout();
        Click(p.MasteryDetail.Children.OfType<Button>().Single(b=>Equals(b.Content,"필요한 부품의 성유물 찾기")));await Dispatcher.Yield();
        Yes(RelicPlanningTab!.IsChecked==true&&p.RelicList.IsVisible&&!p.MasteryList.IsVisible,"장비에서 성유물 찾기 → 성유물 추천 하위 탭 이동");
        Yes(p.RelicGoal.Text.Contains(prime.Name)&&p.RelicList.Items.Count>0&&p.RelicDetail.IsVisible,"선택 목표·보유 성유물·보상 상세 전달");
        Select(RelicRewardsTab!);await Dispatcher.Yield();Yes(relicPage!.IsVisible&&!p.RelicList.IsVisible,"성유물 보상 인식·시세 탭 실제 전환");
        Select(RelicPlanningTab!);await Dispatcher.Yield();Yes(relicPage!.RecognitionBar.IsVisible,"성유물 추천에서도 자동인식 스위치 유지");Yes(p.RelicGoal.Text.Contains(prime.Name)&&p.RelicList.Items.Count>0,"추천 탭 복귀 · 선택 목표 유지");
        ShowInventory();Select(p.MasteryTab);await Dispatcher.Yield();Yes(p.Progress.IsVisible&&p.MasteryList.IsVisible,"장비 전체 요약 복귀 · 진행 그래프 복구");
        p.AddFarmingGoal(prime.Id);await Dispatcher.Yield();
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"추천 성유물 보기")));await Dispatcher.Yield();Yes(RelicPlanningTab.IsChecked==true&&p.RelicGoal.Text.Contains(prime.Name),"내 목표 → 추천 성유물 보기 실제 연결");
        foreach(bool light in new[]{false,true})foreach(var size in new[]{(1760,920,1d),(1280,720,1.5)})
        {
            SetDarkTheme(!light);
            foreach(string view in new[]{"overview","credits","platinum","trade","farming","goals","relics"})
            {
                ShowInventory();
                if(view=="overview")Select(p.MasteryTab);
                if(view=="credits")Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Credits]);
                if(view=="platinum")Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Platinum]);
                if(view=="trade")Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Trade]);
                if(view=="farming")Select(p.AcquisitionTabs[MasteryAcquisitionGroup.Farming]);
                if(view=="goals")Select(p.GoalsTab);
                if(view=="relics")ShowRelicPlanning();
                await Dispatcher.Yield();render(this,$"{view}-{light}-{size.Item1}",size.Item1,size.Item2,size.Item3);
                if(view is "platinum" or "trade")
                {
                    double Gap(RadioButton tab)
                    {
                        tab.ApplyTemplate();var line=(FrameworkElement)tab.Template.FindName("Indicator",tab);
                        var presenter=LayoutDiagnostics.Tree(tab).OfType<ContentPresenter>().First();
                        return line.TranslatePoint(new Point(0,0),tab).Y-presenter.TranslatePoint(new Point(0,presenter.ActualHeight),tab).Y;
                    }
                    var active=p.AcquisitionTabs[view=="platinum"?MasteryAcquisitionGroup.Platinum:MasteryAcquisitionGroup.Trade];
                    double topGap=Gap(navigation[5]),subGap=Gap(active);
                    Yes(subGap>=8&&Math.Abs(topGap-subGap)<2,$"상·하위 탭 밑줄 여백 일치 {view} · {light} · {size.Item1} ({topGap:0.0}/{subGap:0.0})");
                }
                var issues=LayoutDiagnostics.Inspect((FrameworkElement)Content);scans.Add(new{view,light,width=size.Item1,issues});Yes(issues.Length==0,$"{view} · {light} · {size.Item1} 글자·요소 경계 검사");
                Yes(Math.Abs(((FrameworkElement)Content).ActualWidth-size.Item1)<2,"실제 뷰포트 너비 확인");
                if(view=="relics")Yes(p.RelicList.ActualHeight>150&&p.RelicDetail.IsVisible,"성유물 추천 목록·상세 공간 확보");
            }
        }
        static void Toggle(CheckBox box)=>((IToggleProvider)new CheckBoxAutomationPeer(box).GetPattern(PatternInterface.Toggle)).Toggle();
        ShowRelicRewards();relicPage!.Automatic.IsChecked=false;Toggle(relicPage.Automatic);await Dispatcher.Yield();
        Yes(relicPage.RecognitionBar.State.Contains("켜짐"),"성유물 자동인식 UIA 켜기 · 상태 즉시 갱신");
        relicPage.AutoStatus.Text="보상 이름을 읽고 있습니다…";await Dispatcher.Yield();Yes(relicPage.RecognitionBar.State=="화면 읽는 중","성유물 실제 읽기 상태 강조");
        Toggle(relicPage.Automatic);await Dispatcher.Yield();Yes(relicPage.RecognitionBar.State.Contains("꺼짐"),"성유물 자동인식 UIA 끄기");
        ShowRivens();RivenAutomatic.IsChecked=false;Toggle(RivenAutomatic);await Dispatcher.Yield();
        Yes(rivenCapturePreferences.Automatic&&RivenRecognitionBar.State.Contains("켜짐")&&rivenCaptureWindow is null,"리벤 분석 화면에서 바로 켜기 · 팝업 불필요");
        ShowRivenCapture("Melee");Toggle(RivenAutomatic);await Dispatcher.Yield();Yes(!rivenCapturePreferences.Automatic&&rivenCaptureWindow!.Automatic.IsChecked==false,"메인 끄기 → 열린 오버레이 설정에도 반영");
        Toggle(rivenCaptureWindow!.Automatic);await Dispatcher.Yield();Yes(RivenAutomatic.IsChecked==true,"오버레이 설정 켜기 → 메인 스위치 동기화");rivenCaptureWindow.Close();
        foreach(bool light in new[]{false,true})foreach(var size in new[]{(1760,920,1d),(1280,720,1.5)})foreach(string view in new[]{"relic-status","riven-status"})
        {
            SetDarkTheme(!light);if(view=="relic-status"){ShowRelicRewards();relicPage.Automatic.IsChecked=true;relicPage.AutoStatus.Text="자동 인식 대기 · 워프레임 보상 화면을 기다립니다.";}
            else{ShowRivens();SetRivenCaptureStatus("리벤 옵션을 읽고 있습니다…");}
            await Dispatcher.Yield();render(this,$"{view}-{light}-{size.Item1}",size.Item1,size.Item2,size.Item3);
            var issues=LayoutDiagnostics.Inspect((FrameworkElement)Content);scans.Add(new{view,light,width=size.Item1,issues});Yes(issues.Length==0,$"자동인식 상태 {view} · {light} · {size.Item1} 경계 검사");
        }
        SetRivenCaptureStatus("인식 내용 확인 필요");await Dispatcher.Yield();Yes(RivenRecognitionBar.State.Contains("확인 필요"),"읽기 완료와 인식 확인 필요 구분");
        var actual=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","inventory","snapshot.json");
        if(File.Exists(actual))
        {
            ShowInventory();p.UseSnapshot(InventoryImport.Parse(File.ReadAllText(actual),c),false);await p.LoadPricesAsync();SetDarkTheme(true);
            foreach(var group in new[]{MasteryAcquisitionGroup.Trade,MasteryAcquisitionGroup.Platinum})
            {
                Select(p.AcquisitionTabs[group]);await Dispatcher.Yield();p.MasteryList.SelectedIndex=0;p.MasteryList.ScrollIntoView(p.MasteryList.SelectedItem);
                var ranked=p.MasteryList.Items.Cast<InventoryRow>().Select(r=>(MasteryPlan)r.Model).Where(m=>!m.RankLocked&&m.AvailablePoints>0).ToArray();
                var ratios=ranked.Select(m=>MasteryPurchaseRecommendations.Per1000(m,group)??double.PositiveInfinity).ToArray();
                Yes(ratios.SequenceEqual(ratios.OrderBy(x=>x)),"실제 저장 목록·시세 · 마스터리당 구매비 정렬 · "+group);
                render(this,"actual-"+group,1760,920,1);Yes(LayoutDiagnostics.Inspect((FrameworkElement)Content).Length==0,"실제 추천 이유·가격 잘림 없음 · "+group);
                File.WriteAllText(Path.Combine(directory,"actual-order-"+group+".json"),JsonSerializer.Serialize(ranked.Take(20).Select(m=>new{m.Item.Name,m.AvailablePoints,cost=MasteryPurchaseRecommendations.Cost(m,group),per1000=MasteryPurchaseRecommendations.Per1000(m,group),m.RankLocked,m.PricesStale}),Catalog.Json));
            }
        }
        File.WriteAllText(Path.Combine(directory,"layout-scans.json"),JsonSerializer.Serialize(scans,Catalog.Json));
        return new{passed=true,count=checks.Count,checks};
    }
}
