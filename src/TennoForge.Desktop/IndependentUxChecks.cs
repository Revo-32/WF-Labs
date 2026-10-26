using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckIndependentUx(string directory,Action<string,int,int,double> render)
    {
        var checks=new List<string>();var scans=new List<object>();
        void Yes(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        void Capture(string name,int width,int height,double scale)
        {
            render(name+"-"+height,width,height,scale);UpdateLayout();
            Yes(page.ActualWidth<=workspaceViewport.ActualWidth+1&&workspaceViewport.ScrollableWidth<1&&workspaceViewport.ScrollableHeight<1,name+" · 전체 작업 영역 가로·세로 스크롤 없음");
            Yes(System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight==root.RowDefinitions[0].Height.Value,name+" · 실제 제목줄 hit-test 높이와 UI 헤더 일치");
            var issues=LayoutDiagnostics.Inspect((FrameworkElement)Content);scans.Add(new{name,width,height,scale,issues});
            File.WriteAllText(Path.Combine(directory,"layout-scans.json"),JsonSerializer.Serialize(scans,Catalog.Json));
            Yes(issues.Length==0,name+" · 글자 잘림·요소 겹침 없음");
        }
        SetLightMode(true);ShowModding();Yes(capacityResult?.Fits==true,"신규 근접 기본 구성 수용량 충족");
        foreach(var (width,height,scale) in new[]{(1760,920,1d),(1536,820,1.25),(1280,720,1.5),(1280,688,1.5)})
        {
            ShowModding();Capture($"melee-{width}",width,height,scale);
            var geometry=board.MeasureLibrary((FrameworkElement)Content);
            scans.Add(new{name=$"library-melee-{width}",geometry});
            File.WriteAllText(Path.Combine(directory,"layout-scans.json"),JsonSerializer.Serialize(scans,Catalog.Json));
            Yes(geometry.FullyVisibleRows>=2,"근접 보관함 최소 두 줄 완전 표시 "+width);
            Yes(scenarioDrawer!.HandleForCheck.IsVisible,"근접 상황 설정 손잡이 표시 "+width);
            Click(scenarioDrawer.HandleForCheck);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Yes(scenarioDrawer.IsOpen,"상황 설정 실제 UIA 열기 "+width);Capture($"scenario-{width}",width,height,scale);Click(scenarioDrawer.CloseForCheck);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Yes(!scenarioDrawer.IsOpen,"상황 설정 실제 UIA 닫기 "+width);
            foreach(var category in new[]{"primary","secondary"}){ShowRanged(category);Capture(category+"-"+width,width,height,scale);Yes(rangedPages[category].LibraryRowsForCheck()>=2,category+" 보관함 최소 두 줄 완전 표시 "+width);}
            OpenRivenClass("Melee");Capture("riven-melee-"+width,width,height,scale);
            var apply=LayoutDiagnostics.Tree(rivenPage!).OfType<Button>().Single(b=>Equals(b.Content,"현재 근접 모딩에 가정 적용"));
            var bounds=apply.TransformToAncestor(rivenPage!).TransformBounds(new Rect(apply.RenderSize));Yes(bounds.Bottom<=rivenPage!.ActualHeight+1,"근접 리벤 적용 버튼 고정 footer "+width);
            OpenRivenClass("Rifle");Capture("riven-rifle-"+width,width,height,scale);
            ShowInventory();Capture("equipment-"+width,width,height,scale);
            inventoryPage!.RelicOptions.IsExpanded=true;inventoryPage.Traces.Text="-1";await Dispatcher.Yield();
            Yes(inventoryPage.RelicInputError.Visibility==Visibility.Visible&&inventoryPage.RelicEmpty.Text.Contains("입력 확인"),"잔영 오류를 입력 옆과 추천 영역에 표시 "+width);
            inventoryPage.Traces.Text="100";Yes(inventoryPage.RelicInputError.Visibility==Visibility.Collapsed,"잔영 입력 복구 "+width);inventoryPage.RelicOptions.IsExpanded=false;
            ShowRelics();Capture("relic-"+width,width,height,scale);var card=relicPage!.Cards[0];
            Yes(Equals(card.Correct.Content,"보상 선택"),"빈 성유물 카드 보상 선택 행동 "+width);card.Search.ApplyTemplate();
            Yes(card.Search.Template.FindName("Placeholder",card.Search) is TextBlock {Text:"보상 이름 검색",Visibility:Visibility.Visible},"검색 placeholder 표시 "+width);
            card.Search.QueryInput.Focus();await Dispatcher.Yield();Yes(((TextBlock)card.Search.Template.FindName("Placeholder",card.Search)).Visibility==Visibility.Collapsed,"검색 초점에서 placeholder·커서 겹침 없음 "+width);KeyboardClear();
            ShowSchedules();Capture("schedule-"+width,width,height,scale);
            if(height<720)
            {
                var scrolling=(IScrollProvider)new ScrollViewerAutomationPeer(schedulePage!.DashboardScroller).GetPattern(PatternInterface.Scroll);
                scrolling.Scroll(ScrollAmount.NoAmount,ScrollAmount.LargeIncrement);await Dispatcher.Yield();Yes(schedulePage.DashboardScroller.VerticalOffset>0,"작은 작업 영역에서도 실제 일정 스크롤로 미션 목록 접근");scrolling.Scroll(ScrollAmount.NoAmount,ScrollAmount.LargeDecrement);await Dispatcher.Yield();
            }
            Yes(!LayoutDiagnostics.Tree(schedulePage!).OfType<CheckBox>().Any(c=>c.IsVisible),"일정 메인에 오버레이 체크박스 없음 "+width);
            ShowCatalog();Capture("reference-"+width,width,height,scale);ShowRoadmap();Capture("roadmap-"+width,width,height,scale);
            Click(settingsButton);await Dispatcher.Yield();Yes(settingsPopup.IsOpen,"설정 버튼 실제 UIA 열기 "+width);settingsPopup.IsOpen=false;
        }
        var source=RangedEngine.Weapons.First(w=>w.EnglishName=="Braton");var roll=new RangedRivenRoll(source.Id,8,[new("Damage",0),new("Multishot",0)],RivenBalance.Version,10,"Madurai");
        roll=roll with{Traits=roll.Traits.Select(t=>t with{Value=Math.Round(RangedRivens.Range(roll,t).Minimum,1)}).ToArray()};
        var editor=new RangedRivenEditor(source,roll,_=>{},()=>{},inline:true);page.Content=editor;Capture("riven-cost",1760,920,1);
        var draft=editor.CaptureDraft();Yes(draft.DisplayedCost&&draft.Cost=="18"&&editor.ValidRoll?.BaseDrain==10,"총기 표시 코스트18 → 기본 코스트10 변환");
        await editor.CheckRankCostPreserved(Yes);
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(draft with{Cost="10"},Catalog.Json))!.AsObject();legacy.Remove("displayedCost");
        editor.RestoreDraft(JsonSerializer.Deserialize<RangedRivenDraft>(legacy.ToJsonString(),Catalog.Json)!);Yes(editor.CaptureDraft().Cost=="18"&&editor.ValidRoll?.BaseDrain==10,"기존 총기 리벤 JSON의 0랭크 코스트 자동복구 호환");
        editor.RestoreDraft(draft with{Cost="19"});Yes(editor.ValidRoll?.BaseDrain==11,"표시 코스트 변경 실제 수용량 모델 반영");editor.RestoreDraft(editor.CaptureDraft());Yes(editor.ValidRoll?.BaseDrain==11,"새 표시 코스트 작업 재복원 동등");
        ShowModding();var defaults=board.Slots;var mods=new[]{"pressure","bloodrush","shatter","fury","virulent","vicious","weeping","overload"};board.SetSlots(mods.Select(f=>{var m=catalog.Mods.First(x=>x.Family==f);return (ModSlot?)new ModSlot(m.Id,m.MaxRank);}).ToArray());Recalculate();
        Yes(capacityResult?.Fits==false&&planningResult.Text.Contains("실제 장착 불가"),"수용량 초과 시 계산 결과에도 계획용 표시");board.SetSlots(defaults);
        return new{passed=checks.Count,failed=Array.Empty<string>(),checks,scans};
    }
    private static void KeyboardClear()=>System.Windows.Input.Keyboard.ClearFocus();
}

internal sealed partial class RangedRivenEditor
{
    internal async Task CheckRankCostPreserved(Action<bool,string> yes)
    {
        var before=Read().BaseDrain;var original=rank.SelectedIndex;var text=cost.Text;
        async Task Choose(int index)
        {
            var peer=new ComboBoxAutomationPeer(rank);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);expand.Expand();await Dispatcher.Yield();rank.UpdateLayout();
            var items=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();((ISelectionItemProvider)items[index].GetPattern(PatternInterface.SelectionItem)).Select();expand.Collapse();await Dispatcher.Yield();
        }
        await Choose(7);yes(Read().BaseDrain==before&&cost.Text=="17","총기 리벤 실제 랭크8→7 · 표시17 / 기본10 보존");
        await Choose(original);yes(Read().BaseDrain==before&&cost.Text==text,"총기 리벤 실제 랭크7→8 · 표시18 복원");
    }
}
