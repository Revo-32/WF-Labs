using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal async Task<object> CheckInventory(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool b,string name){if(!b)throw new InvalidOperationException(name);checks.Add(name);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        async Task Choose(ComboBox box,int index)
        {
            var peer=new ComboBoxAutomationPeer(box);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);expand.Expand();await System.Windows.Threading.Dispatcher.Yield();box.UpdateLayout();
            var rows=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();((ISelectionItemProvider)rows[index].GetPattern(PatternInterface.SelectionItem)).Select();expand.Collapse();await System.Windows.Threading.Dispatcher.Yield();
        }
        ShowInventory();var p=inventoryPage!;render(this,"inventory-empty",1760,920,1);
        Yes(p.MasteryList.Items.Count==0&&!p.ExportButton.IsEnabled,"연동 전 미보유·미획득으로 추정하지 않음");
        var c=p.CatalogData;var braton=c.Items.Single(i=>i.EnglishName=="Braton Prime");var rhino=c.Items.Single(i=>i.EnglishName=="Rhino");var recipe=c.Recipes.Single(r=>r.ResultId==braton.Id);var relic=c.Relics.First(r=>r.Refinement==0&&r.Rewards.Any(s=>s.Id==recipe.BlueprintId));
        // Exercise the real Win32 query/read path against this isolated validation
        // process. No running game, unrelated process or user input is involved.
        var raw=JsonSerializer.Serialize(new{PlayerLevel=20,RegularCredits=234567,Suits=new[]{new{ItemType=rhino.Id,XP=100000}},LongGuns=Array.Empty<object>(),Pistols=Array.Empty<object>(),Melee=Array.Empty<object>(),Recipes=Array.Empty<object>(),MiscItems=new[]{new{ItemType=relic.Id,ItemCount=7}},XPInfo=new[]{new{ItemType=rhino.Id,XP=100000}}});
        var pointer=Marshal.StringToCoTaskMemUTF8(raw);
        try
        {
            var nativeReader=new InventoryMemoryReader();using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(24));InventoryReadResult? native=null;
            for(int pass=0;pass<3&&native?.Snapshot is null;pass++)native=await nativeReader.ReadSelfForCheck(c,limit.Token);
            Yes(native?.Snapshot is {Credits:234567} found&&found.Count(relic.Id)==7&&found.MasteryRanks[rhino.Id]==10,"실제 Win32 읽기 권한·영역 열거·완전한 인벤토리 추출: 격리 검수 프로세스");
            var again=await nativeReader.ReadSelfForCheck(c,limit.Token);Yes(again.Snapshot?.Credits==234567,"이미 찾은 JSON 위치의 빠른 재판독");
        }
        finally{Marshal.FreeCoTaskMem(pointer);GC.KeepAlive(raw);}
        var stock=recipe.Ingredients.ToDictionary(i=>i.Id,i=>i.Count);stock[recipe.BlueprintId]=1;stock.Remove(recipe.Ingredients.First(i=>i.Id.Contains("Barrel")).Id);stock[rhino.Id]=1;stock[relic.Id]=12;
        var snapshot=new InventorySnapshot(1,DateTimeOffset.UtcNow,"검수용 목록",20,1_000_000,stock,new(){{rhino.Id,10},{c.Items.Single(i=>i.EnglishName=="Lato").Id,30}},[new(rhino.Id,10,0,null,[])],[],true,true,InventoryImport.EquipmentSections.Concat(new[]{"MiscItems","Recipes","XPInfo"}).ToArray());
        string import=Path.Combine(directory,"import.json");InventoryImport.Save(import,snapshot);p.ChooseImportFile=()=>import;Click(p.ImportButton);await System.Windows.Threading.Dispatcher.Yield();
        Yes(p.Snapshot.Count(rhino.Id)==1&&p.MasteryList.Items.Count>100,"실제 가져오기 버튼으로 보유·추천 목록 갱신");
        var preserved=p.Snapshot;p.ReadOverride=_=>Task.FromResult(new InventoryReadResult(preserved with{Sections=preserved.Sections.Where(s=>s!="MiscItems").ToArray()},"부분 응답",1000,.1));Click(p.SyncButton);await System.Windows.Threading.Dispatcher.Yield();Yes(ReferenceEquals(preserved,p.Snapshot)&&p.Status.Text.Contains("누락"),"동기화 버튼 · 부분 응답으로 기존 보유 목록 삭제 방지");p.ReadOverride=null;
        p.Search.Text=rhino.Name;p.UpdateLayout();Yes(p.MasteryList.Items.Cast<InventoryRow>().Any(r=>r.Id==rhino.Id)&&p.MasteryList.Items.Cast<InventoryRow>().All(r=>r.Name.Contains(rhino.Name)),"한국어 장비 검색");
        p.Search.Text="";p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().Single(r=>r.Id==braton.Id);p.UpdateLayout();render(this,"mastery-plan",1760,920,1);
        Yes(Tree(p.MasteryDetail).OfType<TextBlock>().Any(t=>t.Text.Contains("성유물 개봉")),"마스터리 상세에 실제 설계도 획득처 표시");
        int quoteCalls=0;p.QuoteOverride=async(item,token)=>{quoteCalls++;await Task.Delay(20,token);return new(item.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,false,4,4,3,8,2);};
        Click(p.RefreshQuoteButton);await System.Windows.Threading.Dispatcher.Yield();for(int attempt=0;attempt<50&&p.RefreshQuoteButton.Content as string=="시세 조회 취소";attempt++)await Task.Delay(20);
        Yes(quoteCalls==1&&(p.MasteryList.SelectedItem as InventoryRow)?.Model is MasteryPlan{BuyPlatinum:4}&&p.QuoteStatus.Text.Contains("호가 확인"),$"실제 부품 시세 갱신 버튼 · 해당 장비 부품만 조회하고 구매 합계 재계산 (호출 {quoteCalls}, {p.QuoteStatus.Text})");
        var batch=p.MasteryDetail.Children.OfType<Expander>().Single(e=>Equals(e.Header,"현재 재료로 함께 준비할 장비"));
        ((IExpandCollapseProvider)new ExpanderAutomationPeer(batch).GetPattern(PatternInterface.ExpandCollapse)).Expand();await System.Windows.Threading.Dispatcher.Yield();
        Yes(Tree(batch).OfType<TextBlock>().Any(t=>t.Text.Contains("중복 사용하지")),"실제 함께 제작 후보 펼치기 · 공통 재료 중복 제외 안내");
        p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().First(r=>r.Id==rhino.Id);await System.Windows.Threading.Dispatcher.Yield();Yes(p.QuoteStatus.Visibility==Visibility.Collapsed,"다른 장비를 선택하면 이전 장비 시세 조회 문구 숨김");
        var sources=p.MasteryDetail.Children.OfType<Expander>().Single(e=>Equals(e.Header,"설계도·장비 획득처"));
        ((IExpandCollapseProvider)new ExpanderAutomationPeer(sources).GetPattern(PatternInterface.ExpandCollapse)).Expand();await System.Windows.Threading.Dispatcher.Yield();render(this,"owned-acquisition-expanded",1760,920,1);
        Yes(Tree(sources).OfType<TextBlock>().Any(t=>t.Text.Contains("35,000 크레딧")),"실제 획득처 펼치기 · 본체 설계도 고정 가격");
        var nonPrime=c.Items.Single(i=>i.EnglishName=="Prisma Grakata");p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().Single(r=>r.Id==nonPrime.Id);p.UpdateLayout();
        Yes(p.RefreshQuoteButton.IsEnabled,"프리즈마 완성 무기도 시세 조회 활성화");Click(p.RefreshQuoteButton);await System.Windows.Threading.Dispatcher.Yield();for(int attempt=0;attempt<50&&p.RefreshQuoteButton.Content as string=="시세 조회 취소";attempt++)await Task.Delay(20);
        Yes(quoteCalls==2&&(p.MasteryList.SelectedItem as InventoryRow)?.Model is MasteryPlan{BuyPlatinum:4,AcquisitionOrder:2},"실제 프라임 외 거래 호가 버튼 · 플래티넘/마스터리 비교 반영");
        p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().Single(r=>r.Id==braton.Id);p.UpdateLayout();
        var targetButton=p.MasteryDetail.Children.OfType<Button>().Single(b=>(string)b.Content=="필요한 부품의 성유물 찾기");Click(targetButton);await System.Windows.Threading.Dispatcher.Yield();
        Yes(p.MasteryList.IsVisible&&p.RelicList.IsVisible&&p.RelicGoal.Text.Contains(braton.Name),"성유물 찾기 버튼 · 탭 이동 없이 우측 목표 갱신");
        // The owned relic here contains its blueprint; choose a fixture where that
        // blueprint is missing too so this path has at least one actual candidate.
        stock.Remove(recipe.BlueprintId);p.UseSnapshot(snapshot with{Quantities=new(stock)},false);p.Squad.SelectedIndex=3;p.Traces.Text="100";p.UpdateLayout();
        Yes(p.RelicList.Items.Count>0,"필요 부품이 나오는 보유 성유물 후보 표시");
        var traceInput=(IValueProvider)new TextBoxAutomationPeer(p.Traces).GetPattern(PatternInterface.Value);traceInput.SetValue("-1");
        Yes(p.RelicInputError.Visibility==Visibility.Visible&&p.RelicEmpty.Visibility==Visibility.Visible&&p.RelicEmpty.Text.Contains("입력 확인"),"잘못된 잔영 실제 입력 · 숨은 상세창 대신 입력과 빈 목록에 오류 표시");traceInput.SetValue("100");Yes(p.RelicInputError.Visibility==Visibility.Collapsed&&p.RelicList.Items.Count>0,"잔영 실제 입력 복원 · 추천 목록 복구");
        ((IExpandCollapseProvider)new ExpanderAutomationPeer(p.RelicOptions).GetPattern(PatternInterface.ExpandCollapse)).Expand();await System.Windows.Threading.Dispatcher.Yield();
        await Choose(p.Threshold,3);Yes(Tree(p.RelicDetail).OfType<TextBlock>().Any(t=>t.Text.Contains("50 P 이상")),"실제 가치 기준 드롭다운 · 보상 확률 재계산");
        ((IToggleProvider)new CheckBoxAutomationPeer(p.AllRefinements).GetPattern(PatternInterface.Toggle)).Toggle();await System.Windows.Threading.Dispatcher.Yield();Yes(p.RelicList.Items.Count>1,"실제 정련도 비교 토글 · 상세 후보 보존");
        ((IToggleProvider)new CheckBoxAutomationPeer(p.AllRefinements).GetPattern(PatternInterface.Toggle)).Toggle();await System.Windows.Threading.Dispatcher.Yield();Yes(p.RelicList.Items.Count==1,"기본 성유물 추천은 같은 가족의 최적 정련도 하나로 압축");
        Click(p.ClearGoal);await System.Windows.Threading.Dispatcher.Yield();await Choose(p.RelicOrder,2);Yes(p.RelicCriteria.Text.Contains("두캇")&&p.RelicList.Items.Cast<InventoryRow>().All(r=>r.Value.Contains("두캇")),"실제 추천 정렬 드롭다운 · 두캇 모드 연결");await Choose(p.RelicOrder,0);await Choose(p.Threshold,1);
        ((IExpandCollapseProvider)new ExpanderAutomationPeer(p.RelicDetails).GetPattern(PatternInterface.ExpandCollapse)).Expand();await System.Windows.Threading.Dispatcher.Yield();Yes(!p.RelicOptions.IsExpanded,"추천 조건·상세가 동시에 목록 공간을 차지하지 않음");p.RelicDetails.IsExpanded=false;
        render(this,"relic-plan",1760,920,1);
        Yes(p.Progress.Model is {Completed:1,InProgress:1}&&p.Progress.Model.Total==c.Items.Count(i=>i.Masterable),"과거 판매한 장비도 전체 숙련 완료 그래프에 포함");
        var completedButton=Tree(p.Progress).OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="완료 장비 보기");Click(completedButton);await System.Windows.Threading.Dispatcher.Yield();
        Yes(p.Ownership.SelectedIndex==3&&p.MasteryList.Items.Count==1&&(p.MasteryList.Items[0] as InventoryRow)?.Name==c.Items.Single(i=>i.EnglishName=="Lato").Name,"완료 지표 실제 클릭 · 판매한 숙련 장비 목록 조회");
        p.Ownership.SelectedIndex=1;p.Category.SelectedIndex=1;p.UpdateLayout();Yes(p.MasteryList.Items.Count==1,"마스터리 보유·워프레임 필터");p.Ownership.SelectedIndex=0;p.Category.SelectedIndex=0;
        Yes(!Tree(p).OfType<RivenSections>().Any(),"인벤토리·성유물 하위 탭 제거");
        var saved=p.Snapshot;var forty=c.Items.First(i=>i.Masterable&&i.MaxRank==40);var quantities=new Dictionary<string,long>(saved.Quantities){{forty.Id,1}};var history=new Dictionary<string,int>(saved.MasteryRanks){{forty.Id,30}};
        p.UseSnapshot(saved with{Quantities=quantities,MasteryRanks=history,Equipment=saved.Equipment.Append(new OwnedEquipment(forty.Id,30,0,null,[])).ToArray()},false);
        var formaRow=p.MasteryList.Items.Cast<InventoryRow>().Single(r=>r.Id==forty.Id);p.MasteryList.SelectedItem=formaRow;await System.Windows.Threading.Dispatcher.Yield();
        Yes(formaRow.State.Contains("포르마")&&formaRow.Value.StartsWith("+0점")&&!Tree(p.MasteryDetail).OfType<TextBlock>().Any(t=>t.Text.Contains("추가 구매 없이")),"추가 포르마가 필요한 장비를 무료 랭작 가능으로 표시하지 않음");p.UseSnapshot(saved,false);
        p.MasteryList.ScrollIntoView(p.MasteryList.Items[^1]);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);var listScroll=Tree(p.MasteryList).OfType<ScrollViewer>().First();double before=listScroll.VerticalOffset;
        p.UseSnapshot(p.Snapshot,false);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);Yes(before>0&&Math.Abs(listScroll.VerticalOffset-before)<1,"동일 목록 동기화 시 사용자가 보던 스크롤 위치 유지");p.MasteryList.ScrollIntoView(p.MasteryList.SelectedItem);
        File.WriteAllText(import,"{invalid}");var previous=p.Snapshot;Click(p.ImportButton);await System.Windows.Threading.Dispatcher.Yield();Yes(ReferenceEquals(previous,p.Snapshot)&&p.Status.Text.Contains("실패"),"손상된 파일 가져오기 실패 시 기존 목록 유지");
        foreach(bool dark in new[]{true,false})foreach(var scenario in new[]{"기본","성유물 조건","성유물 상세","완료 장비","동기화 정보"})foreach(var scale in new[]{1d,1.25,1.5})
        {
            var metadata=Tree(p).OfType<Expander>().Single(e=>Equals(e.Header,"동기화 정보"));metadata.IsExpanded=scenario=="동기화 정보";
            SetDarkTheme(dark);p.Ownership.SelectedIndex=scenario=="완료 장비"?3:0;p.RelicOptions.IsExpanded=scenario=="성유물 조건";p.RelicDetails.IsExpanded=scenario=="성유물 상세";
            p.Progress.Update(MasteryOverview.From(c,p.Snapshot));p.UpdateLayout();render(this,$"dashboard-{scenario}-{(dark?"dark":"light")}-{scale:0.##}",1760,920,scale);
            double ratio=p.Dashboard.ColumnDefinitions[0].ActualWidth/p.Dashboard.ActualWidth;Yes(Math.Abs(ratio-.7)<.01,"장비 화면 70:30 열 비율 유지");
            foreach(var progress in Tree(p).OfType<MasteryProgressView>())
            {
                var circle=(FrameworkElement)progress.Children[0];var label=Tree(circle).OfType<TextBlock>().Single(t=>t.Text=="마스터리 완료");var bounds=label.TransformToAncestor(circle).TransformBounds(new Rect(label.RenderSize));
                Yes(label.TextAlignment==TextAlignment.Center&&Math.Abs(bounds.Left+bounds.Width/2-circle.ActualWidth/2)<.1,"마스터리 완료 라벨과 도넛의 수평 중심 일치");
            }
            Yes(p.MasteryList.ActualWidth>400&&p.MasteryList.ActualHeight>240&&p.RelicList.ActualWidth>380&&p.RelicList.ActualHeight>100,"마스터리·성유물 동시 작업 공간 확보");
            var issues=LayoutDiagnostics.Inspect(p);if(issues.Length>0)File.WriteAllText(Path.Combine(directory,"layout-failure.json"),JsonSerializer.Serialize(issues,Catalog.Json));Yes(issues.Length==0,$"{scenario} 배율 {scale}: 글자·컨트롤 잘림 없음");
            foreach(var scroll in Tree(p).OfType<ScrollViewer>().Where(s=>s.IsVisible&&s.ViewportHeight>0&&s.ScrollableHeight>1).ToArray())
            {scroll.ScrollToEnd();await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);scroll.UpdateLayout();Yes(Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<1,$"상세·목록 스크롤 끝 도달 · {scenario} {scale} ({scroll.VerticalOffset:0.##}/{scroll.ScrollableHeight:0.##})");Yes(LayoutDiagnostics.Inspect(p).Length==0,"스크롤 끝에서 글자 잘림 없음");scroll.ScrollToTop();}
            foreach(var button in Tree(p).OfType<Button>().Where(b=>b.IsVisible&&b.ActualWidth>0)){var bounds=button.TransformToAncestor(p).TransformBounds(new Rect(button.RenderSize));Yes(bounds.Left>=-.5&&bounds.Right<=p.ActualWidth+.5,"버튼 가로 경계");}
        }
        SetDarkTheme(true);Tree(p).OfType<Expander>().Single(e=>Equals(e.Header,"동기화 정보")).IsExpanded=false;p.Ownership.SelectedIndex=0;p.RelicOptions.IsExpanded=false;p.RelicDetails.IsExpanded=false;p.MasteryList.SelectedItem=p.MasteryList.Items.Cast<InventoryRow>().First(r=>r.Id==braton.Id);p.UpdateLayout();var mod=p.MasteryDetail.Children.OfType<Button>().Single(b=>(string)b.Content=="이 장비 모딩하기");Click(mod);await System.Windows.Threading.Dispatcher.Yield();Yes(navigation[0].IsChecked==true,"추천 장비에서 주무기 모딩으로 실제 이동");
        ShowInventory();var workspace=CaptureUpdateWorkspace();ShowModding();ApplyUpdateWorkspace(workspace);
        Yes(navigation[5].IsChecked==true&&page.Content==inventoryPage,"작업 상태 복원 시 내 장비 탭과 인벤토리 유지");
        var actual=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","inventory","snapshot.json");
        if(File.Exists(actual)){p.UseSnapshot(InventoryImport.Parse(File.ReadAllText(actual),c),false);await p.LoadPricesAsync();if(p.ClearGoal.IsEnabled)Click(p.ClearGoal);p.Squad.SelectedIndex=0;p.MasteryList.SelectedIndex=0;p.MasteryList.ScrollIntoView(p.MasteryList.SelectedItem);await System.Windows.Threading.Dispatcher.Yield();p.Status.Text="저장된 계정 목록과 시세로 표시합니다.";render(this,"real-dashboard",1760,920,1);Yes(LayoutDiagnostics.Inspect(p).Length==0,"실제 저장 계정 목록 · 통합 대시보드 잘림 없음");Yes(p.RelicList.Items.Count==0||p.RelicList.SelectedIndex==0,"시세·조건으로 추천 순위가 바뀌면 최상위 성유물부터 표시");}
        CheckHeaderLayout();return new{passed=checks.Count,failed=Array.Empty<string>(),checks};
    }
    private static IEnumerable<DependencyObject> Tree(DependencyObject parent)
    {yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Tree(VisualTreeHelper.GetChild(parent,i)))yield return child;}
}
