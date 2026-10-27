using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckFarming(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var scans=new List<object>();void Yes(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        static void Select(RadioButton b)=>((ISelectionItemProvider)new RadioButtonAutomationPeer(b).GetPattern(PatternInterface.SelectionItem)).Select();
        async Task Choose(ComboBox box,int index)
        {
            var peer=new ComboBoxAutomationPeer(box);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);expand.Expand();await Dispatcher.Yield();box.UpdateLayout();
            var rows=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();((ISelectionItemProvider)rows[index].GetPattern(PatternInterface.SelectionItem)).Select();expand.Collapse();await Dispatcher.Yield();
        }
        ShowInventory();var p=inventoryPage!;var c=p.CatalogData;
        var braton=c.Items.Single(i=>i.EnglishName=="Braton Prime");var burston=c.Items.Single(i=>i.EnglishName=="Burston Prime");var recipe=c.Recipes.Single(r=>r.ResultId==braton.Id);
        var barrel=recipe.Ingredients.First(i=>c.Name(i.Id).Contains("배럴"));var relic=c.Relics.First(r=>r.Refinement==0&&r.Rewards.Any(s=>s.Id==barrel.Id));
        var stock=recipe.Ingredients.ToDictionary(i=>i.Id,i=>i.Count);stock[recipe.BlueprintId]=1;stock.Remove(barrel.Id);stock[relic.Id]=5;
        var inventory=InventorySnapshot.Empty with{CapturedUtc=DateTimeOffset.UtcNow,Credits=1000000,InventoryComplete=true,MasteryComplete=true,MasteryRank=30,Quantities=stock};p.UseSnapshot(inventory,false);
        Select(p.GoalsTab);await Dispatcher.Yield();render(this,"goals-empty",1760,920,1);
        Yes(p.GoalsPane.IsVisible&&p.Farming.Document.Goals.Length==0,"실제 목표 탭 선택 · 빈 상태와 검색 진입");
        p.GoalSearch.SelectedItem=p.GoalSearch.Items.Cast<RelicItem>().Single(i=>i.Id==braton.Id);Click(p.AddGoalButton);await p.RefreshGoalPurchasesForCheck();
        Yes(p.Farming.Document.Goals.Single().ItemId==braton.Id,"실제 목표 추가 버튼 · 안정 ID 저장");Yes(p.Farming.Plan.Missing[barrel.Id]==1,"목표 등록 즉시 부족 배럴 연결");
        File.WriteAllText(Path.Combine(directory,"goal-link-debug.json"),JsonSerializer.Serialize(new{barrel,relic,header=p.RelicGoal.Text,rows=p.RelicList.Items.Count,missing=p.Farming.Plan.Missing,owned=p.Snapshot.Quantities},Catalog.Json));
        Yes(p.RelicList.Items.Count>0&&p.RelicGoal.Text.Contains(braton.Name)&&p.RelicList.Items.Cast<InventoryRow>().All(r=>((RelicPlan)r.Model).TargetChance>0),"목표 등록 → 우측 보유 성유물·정련 추천 자동 연결");
        var quantity=LayoutDiagnostics.Tree(p.GoalDetail).OfType<ComboBox>().Single(b=>AutomationProperties.GetName(b)=="목표 보유 수량");await Choose(quantity,1);await p.RefreshGoalPurchasesForCheck();
        Yes(p.Farming.Document.Goals.Single().Quantity==2&&p.Farming.Plan.Missing[barrel.Id]==2,"실제 수량 드롭다운 → 예약·부족 수량 재계산");
        quantity=LayoutDiagnostics.Tree(p.GoalDetail).OfType<ComboBox>().Single(b=>AutomationProperties.GetName(b)=="목표 보유 수량");await Choose(quantity,0);await p.RefreshGoalPurchasesForCheck();
        Select(LayoutDiagnostics.Tree(p.GoalDetail).OfType<RadioButton>().Single(b=>AutomationProperties.GetName(b)=="목표 구매 비교"));await p.RefreshGoalPurchasesForCheck();
        decimal partPrice=10;int quoteCalls=0;p.QuoteOverride=async(item,token)=>{quoteCalls++;await Task.Delay(20,token);decimal value=item.Slug.EndsWith("_set",StringComparison.Ordinal)?8:partPrice;return new MarketQuote(item.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,true,value,value,value,5,4);};
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"부품·세트 시세 갱신")));await p.RefreshGoalPurchasesForCheck();
        string[] Texts()=>LayoutDiagnostics.Tree(p.GoalDetail).OfType<TextBlock>().Select(t=>t.Text).ToArray();
        File.WriteAllText(Path.Combine(directory,"goal-purchase-debug.json"),JsonSerializer.Serialize(new{quoteCalls,texts=Texts()},Catalog.Json));
        Yes(quoteCalls>=2&&Texts().Any(t=>t.Contains("최소 8 P"))&&Texts().Any(t=>t.Contains("추가로 남음")),"실제 시세 갱신 → 부족 낱개보다 싼 세트·여분 표시");
        partPrice=5;await p.RefreshGoalPricesForCheck();await p.RefreshGoalPurchasesForCheck();Yes(Texts().Any(t=>t.Contains("최소 5 P")),"시세 변경 후 낱개가 유리한 경로로 자동 갱신");
        render(this,"goal-purchase-comparison",1760,920,1);
        Select(LayoutDiagnostics.Tree(p.GoalDetail).OfType<RadioButton>().Single(b=>AutomationProperties.GetName(b)=="목표 재료·예약"));await Dispatcher.Yield();
        p.GoalSearch.SelectedItem=p.GoalSearch.Items.Cast<RelicItem>().Single(i=>i.Id==burston.Id);Click(p.AddGoalButton);await p.RefreshGoalPurchasesForCheck();
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"위로")));await p.RefreshGoalPurchasesForCheck();Yes(p.Farming.Document.Goals[0].ItemId==burston.Id,"실제 위로 버튼 → 목표 예약 우선순위 변경");
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"아래로")));await p.RefreshGoalPurchasesForCheck();Yes(p.Farming.Document.Goals[1].ItemId==burston.Id,"실제 아래로 버튼 → 순서 복원");
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"목표 삭제")));p.AddFarmingGoal(braton.Id);await p.RefreshGoalPurchasesForCheck();Yes(p.Farming.Document.Goals.Length==1,"실제 삭제 후 남은 목표와 예약 유지");
        EnsureRelicPage();var marketPart=InventoryMarket.Find(barrel.Id)!;relicPage!.FarmingHint=Farming.Reward;
        var card=relicPage.Cards[0];card.SetRecognition(new("fixture",new("fixture",0,0,1,1,1),[new(marketPart,1)],true));relicPage.UpdateHighlights();
        Yes(card.FarmingLabel.IsVisible==false&&card.FarmingLabel.Text.Contains("1개 필요"),"비활성 성유물 페이지도 목표 힌트 갱신 · 창 강제 전환 없음");
        using(var output=File.Create(Path.Combine(directory,"goal-reward-hint.json")))JsonSerializer.Serialize(output,new{marketPart.Name,hint=Farming.Reward(marketPart)},Catalog.Json);
        var overlay=new RelicOverlay(IntPtr.Zero,relicPage);var content=overlay.PreviewForCheck();render(content,"goal-reward-overlay",1440,208,1);var overlayIssues=LayoutDiagnostics.Inspect(content);scans.Add(new{name="overlay",issues=overlayIssues});Yes(overlayIssues.Length==0,"목표 보상 오버레이 실제 콘텐츠 · 글자 잘림·겹침 없음");overlay.Close();
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"일시 정지")));await p.RefreshGoalPurchasesForCheck();relicPage.UpdateHighlights();Yes(Farming.Plan.Goals.Length==0&&!card.FarmingLabel.Text.Contains("필요"),"실제 일시 정지 → 예약 해제·보상 강조 해제");
        Click(LayoutDiagnostics.Tree(p.GoalDetail).OfType<Button>().Single(b=>Equals(b.Content,"다시 시작")));await p.RefreshGoalPurchasesForCheck();Yes(Farming.Plan.Missing[barrel.Id]==1,"실제 다시 시작 → 예약·목표 복원");
        foreach(var light in new[]{false,true})foreach(var size in new[]{(1760,920,1d),(1280,720,1.5)})
        {
            SetDarkTheme(!light);ShowInventory();p.GoalsTab.IsChecked=true;await p.RefreshGoalPurchasesForCheck();render(this,$"goals-{light}-{size.Item1}",size.Item1,size.Item2,size.Item3);
            var issues=LayoutDiagnostics.Inspect((FrameworkElement)Content);scans.Add(new{name="goals",light,width=size.Item1,height=size.Item2,scale=size.Item3,issues});Yes(issues.Length==0,$"목표 UI {light} {size.Item1} · 잘림·겹침 없음");
            Yes(Math.Abs(((FrameworkElement)Content).ActualWidth-size.Item1)<2,"요청한 실제 뷰포트 너비 적용 · 큰 창 이미지의 가장자리 잘라내기 아님");
            Yes(p.GoalList.ActualHeight>180&&!p.RelicList.IsVisible,"작은 화면에서도 목표 목록 공간 유지 · 성유물은 전용 탭");
        }
        SetDarkTheme(true);
        var testFolder=Path.Combine(directory,"persistence");Directory.CreateDirectory(testFolder);var path=Path.Combine(testFolder,"goals.json");
        var saved=new FarmingWorkspace(false,c,path);saved.Add(braton.Id);saved.Edit(new(braton.Id,2));var restored=new FarmingWorkspace(false,c,path);Yes(restored.Document.Goals.Single().Quantity==2,"목표 파일 실제 저장 → 새 세션 복원");
        File.WriteAllText(path,"broken");var recovered=new FarmingWorkspace(false,c,path);Yes(recovered.Document.Goals.Length==1&&recovered.Notice.Contains("백업"),"손상된 목표 원본을 보존하고 정상 백업 읽기");recovered.Edit(new(braton.Id,3));Yes(Directory.GetFiles(testFolder,"goals.json.invalid-*").Length==1&&new FarmingWorkspace(false,c,path).Document.Goals.Single().Quantity==3,"손상 원본 별도 보존 후 정상 목표 저장");
        EnsureSchedulePage();checks.AddRange(await schedulePage!.CheckFarmingUi(directory,render));
        File.WriteAllText(Path.Combine(directory,"layout-scans.json"),JsonSerializer.Serialize(scans,Catalog.Json));
        return new{passed=true,checks=checks.Count,details=checks};
    }
}

internal sealed partial class SchedulePage
{
    internal async Task<string[]> CheckFarmingUi(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        var goals=Farming!;var now=DateTimeOffset.UtcNow;Clock=()=>now;
        var mission=new FissureMission("goal-fixture","자리만 · 시험 노드","MT_VOID_CASCADE",6,true,false,now.AddMinutes(-1),now.AddHours(1));
        var snapshot=new WorldScheduleSnapshot(now,[],null,null,null,null,null,null,"fixture"){Fissures=[mission],FissuresUtc=now};
        int alerts=0;AlertSink=a=>alerts+=a.Length;UseData(new(snapshot,Data.Arbitrations,false,null,now));
        Yes(alerts==1,"목표 성유물 실시간 자료 → 알림 파이프라인 한 번 전달");TickFeatures();Yes(alerts==1,"동일 목표 미션 반복 알림 없음");
        Yes(OverlayData(now).Fissures.Single().Badge.Contains("목표"),"현재 균열 오버레이에 목표 표시 연결");
        ShowFarmingMissions();await Dispatcher.Yield(DispatcherPriority.ContextIdle);var root=(FrameworkElement)featureWindow!.Content;
        Yes(Preferences.FissureView==5&&featureWindow.IsVisible,"목표 미션 보기 → 실제 필터와 설정 창 열림");
        render(featureWindow,"goal-missions-display",1100,780,1);
        ((ISelectionItemProvider)new RadioButtonAutomationPeer(FeatureTabs["알림"]).GetPattern(PatternInterface.SelectionItem)).Select();await Dispatcher.Yield();
        var mode=LayoutDiagnostics.Tree(root).OfType<ComboBox>().Single(b=>AutomationProperties.GetName(b)=="목표 성유물 알림 경로");
        async Task Mode(int index){var peer=new ComboBoxAutomationPeer(mode);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);expand.Expand();await Dispatcher.Yield();var row=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ElementAt(index);((ISelectionItemProvider)row.GetPattern(PatternInterface.SelectionItem)).Select();expand.Collapse();await Dispatcher.Yield();}
        await Mode(2);
        Yes(Preferences.FarmingMode==2&&OverlayData(now).Fissures.Length==0,"목표 미션 경로 필터 · 일반만 선택 시 강길 제외");await Mode(1);
        var toggle=LayoutDiagnostics.Tree(root).OfType<CheckBox>().Single(b=>AutomationProperties.GetName(b)=="내 목표 성유물 미션 알림");((IToggleProvider)new CheckBoxAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();
        Yes(!Preferences.NotifyFarmingGoals,"목표 알림 실제 토글 · 설정 반영");((IToggleProvider)new CheckBoxAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();
        render(featureWindow,"goal-mission-notifications",1100,780,1);
        var layoutIssues=LayoutDiagnostics.Inspect(root);File.WriteAllText(Path.Combine(directory,"goal-notification-layout.json"),JsonSerializer.Serialize(layoutIssues,Catalog.Json));
        Yes(Math.Abs(featureWindow.ActualWidth-1100)<2&&Math.Abs(featureWindow.ActualHeight-780)<2,"목표 알림 설정의 실제 HWND와 검수 크기 일치");
        Yes(layoutIssues.Length==0,"목표 알림 설정 · 스크롤·글자·경계 검사");featureWindow.Close();
        return checks.ToArray();
    }
}
