using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckInteractions(Action<FrameworkElement,string,int,int> render)
    {
        var checks=new List<string>();
        foreach(var dark in new[]{true,false})
        foreach(var kind in new[]{"primary","secondary"})
        {
            SetDarkTheme(dark);ShowRanged(kind);UpdateLayout();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            checks.AddRange(await rangedPages[kind].CheckInteractions((element,name,width,height)=>render(element,$"{kind}-{dark}-{name}",width,height)));
            render((FrameworkElement)Content,$"{kind}-{dark}",1760,920);
        }
        return new{passed=true,method="WPF UI Automation Invoke / ExpandCollapse / SelectionItem, 실제 HWND 팝업과 상태·계산 결과 대조",checks};
    }
}

internal sealed partial class RangedPage
{
    internal async Task<string[]> CheckInteractions(Action<FrameworkElement,string,int,int> render)
    {
        var checks=new List<string>();var saved=Capture();
        void Yes(bool value,string name){if(!value)throw new Exception(category+": "+name);checks.Add(category+": "+name);}
        async Task Settle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
        async Task Invoke(UIElement control)
        {
            var peer=UIElementAutomationPeer.CreatePeerForElement(control)??throw new Exception("Automation peer 없음");
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();await Settle();
        }
        async Task Select(ComboBox combo,int index)
        {
            var peer=UIElementAutomationPeer.CreatePeerForElement(combo)!;
            ((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            var candidates=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();
            if(candidates.Length<=index)throw new Exception($"선택 항목 Automation 누락: {candidates.Length} / {index}");
            ((ISelectionItemProvider)candidates[index].GetPattern(PatternInterface.SelectionItem)).Select();
            combo.IsDropDownOpen=false;await Settle();
        }
        ComboBox Editor(string name)=>LayoutDiagnostics.Tree(slotEditor).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)==name);
        try
        {
            active=0;Array.Clear(slots);Array.Fill(polarities,"None");Refresh();await Settle();
            Yes(!LayoutDiagnostics.Tree(this).OfType<Button>().Any(b=>b.Content as string=="랭크 · 극성"),"8개 반복 편집 버튼 제거");
            Yes(!LayoutDiagnostics.Tree(slotEditor).OfType<Button>().Single(b=>b.Content as string=="해제").IsEnabled,"빈 슬롯 해제 비활성");
            var mod=RangedEngine.ModCatalog.Mods.First(m=>m.EnglishName==(category=="primary"?"Serration":"Hornet Strike"));
            var baseline=RangedEngine.Calculate(Capture()).ExpectedShot;
            Button LibraryMod()=>library.Children.OfType<Button>().Single(b=>b.Tag is ValueTuple<RangedMod,int> d&&d.Item1.Id==mod.Id);
            await Invoke(LibraryMod());
            Yes(slots[0]?.ModId==mod.Id&&slots[0]?.Rank==mod.MaxRank,"보관함 Invoke → 선택 슬롯 장착");
            var max=RangedEngine.Calculate(Capture()).ExpectedShot;Yes(max>baseline,"장착 직후 직접 피해 증가");
            Rect CardContentRect()
            {
                var card=SlotCard(0);var face=(ModCardFace)card.Content;
                return face.TransformToAncestor(card).TransformBounds(new Rect(face.RenderSize));
            }
            var selectedRect=CardContentRect();await Invoke(SlotCard(1));var idleRect=CardContentRect();await Invoke(SlotCard(0));
            Yes(selectedRect==idleRect&&CardContentRect()==selectedRect,"선택 전환에도 카드 내용 좌표·크기 고정");
            await Select(Editor("선택한 모드 랭크"),0);
            Yes(slots[0]?.Rank==0&&RangedEngine.Calculate(Capture()).ExpectedShot<max,"랭크 선택 → 저장 상태·계산 변경");
            await Select(Editor("선택한 슬롯 극성"),1);
            Yes(polarities[0]=="Madurai","극성 선택 → 슬롯 상태 변경");
            var expected=(int)Math.Ceiling(mod.BaseDrain/2d);
            Yes(RangedEngine.Calculate(Capture()).Drain==expected,"일치 극성의 실제 수용량 차감");
            Yes(LayoutDiagnostics.Tree(SlotCard(0)).OfType<TextBlock>().Any(t=>t.Text==expected.ToString()),"카드 코스트에 일치 극성 반값 반영");
            var menu=SlotCard(0).ContextMenu!;menu.PlacementTarget=SlotCard(0);menu.IsOpen=true;await Settle();
            var rank=menu.Items.OfType<MenuItem>().Single(i=>i.Header as string=="모드 랭크");rank.ApplyTemplate();
            render(menu,"context-menu",(int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight));
            var arrows=menu.Items.OfType<MenuItem>().Where(i=>i.HasItems).Select(i=>(FrameworkElement)i.Template.FindName("Arrow",i)).ToArray();
            Yes(arrows.Select(a=>a.TranslatePoint(new(),menu).X).Max()-arrows.Select(a=>a.TranslatePoint(new(),menu).X).Min()<1,"하위 메뉴 화살표 우측 정렬");
            Yes(rank.Template.FindName("Arrow",rank) is FrameworkElement {Visibility:Visibility.Visible},"하위 메뉴 화살표 표시");
            ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(rank)!.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            var popup=rank.Template.FindName("PART_Popup",rank) as Popup;
            Yes(popup is {IsOpen:true}&&PresentationSource.FromVisual(popup.Child) is not null,"실제 하위 메뉴 HWND 생성");
            var child=(MenuItem)rank.Items[mod.MaxRank];
            Yes(child.IsVisible&&child.ActualHeight>=32,"최대 랭크 메뉴가 보이고 조작 가능");
            Yes(child.Template.FindName("MenuHeader",child) is FrameworkElement {ActualWidth:>8},"하위 메뉴 글자 영역 확보");
            render((FrameworkElement)popup!.Child,"rank-menu",(int)Math.Ceiling(((FrameworkElement)popup.Child).ActualWidth),(int)Math.Ceiling(((FrameworkElement)popup.Child).ActualHeight));
            await Invoke(child);Yes(slots[0]?.Rank==mod.MaxRank,"하위 메뉴 Invoke → 랭크 적용");
            Yes(!menu.IsOpen,"메뉴 항목 실행 후 닫힘");
            menu=SlotCard(0).ContextMenu!;menu.PlacementTarget=SlotCard(0);menu.IsOpen=true;await Settle();
            var polarity=menu.Items.OfType<MenuItem>().Single(i=>i.Header as string=="포르마 · 슬롯 극성");
            ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(polarity)!.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            await Invoke((MenuItem)polarity.Items[2]);Yes(polarities[0]=="Vazarin","극성 하위 메뉴 Invoke → 적용");
            expected=(int)Math.Ceiling((mod.BaseDrain+mod.MaxRank)*1.25);
            Yes(RangedEngine.Calculate(Capture()).Drain==expected,"불일치 극성의 실제 수용량 차감");
            Yes(LayoutDiagnostics.Tree(SlotCard(0)).OfType<TextBlock>().Any(t=>t.Text==expected.ToString()),"불일치 극성 코스트 올림 반영");
            await Invoke(SlotCard(1));await Invoke(LibraryMod());
            Yes(slots[0] is null&&slots[1]?.ModId==mod.Id,"장착된 모드 클릭 → 중복 없이 선택 슬롯으로 이동");
            var state=Capture();Restore(state);await Settle();Yes(Capture().Slots[1]==state.Slots[1]&&Capture().Polarities.SequenceEqual(state.Polarities),"랭크·극성 저장 복원");
            await Invoke(LayoutDiagnostics.Tree(slotEditor).OfType<Button>().Single(b=>b.Content as string=="해제"));
            Yes(slots.All(s=>s is null)&&Math.Abs(RangedEngine.Calculate(Capture()).ExpectedShot-baseline)<1e-9,"해제 → 슬롯 비움·원래 피해 복원");
            slots[0]=new(mod.Id,mod.MaxRank);active=0;Refresh();await Settle();
            Check();await Settle();Yes(true,"기존 장착·복원·드래그 이동·해제 회귀");
            active=0;RefreshSlots();await Settle();render((FrameworkElement)Window.GetWindow(this).Content,"equipped",1760,920);
            Yes(LayoutDiagnostics.Inspect(this).Length==0,"편집 행·모드 보관함 잘림 없음");
            if(exilusMod.Items.Count>1)
            {
                await Select(exilusMod,1);Yes(Capture().Exilus is not null&&exilusRank.IsEnabled,"엑실러스 선택 → 실제 장착·랭크 활성");
                await Select(exilusRank,0);Yes(Capture().Exilus?.Rank==0,"엑실러스 랭크 변경");
                await Select(exilusPolarity,1);Yes(Capture().ExilusPolarity=="Madurai","엑실러스 극성 변경");
                await Select(exilusMod,0);Yes(Capture().Exilus is null&&!exilusRank.IsEnabled,"엑실러스 해제 → 랭크 비활성");
            }
            var merciless=RangedArcanes.Rules.Single(a=>a.Category==category&&a.Code=="Merciless");
            await Select(arcane,arcane.Items.Cast<ArcaneChoice>().Select((a,i)=>(a,i)).Single(x=>x.a.Id==merciless.Id).i);
            await Select(stacks,merciless.Cap);await Select(arcaneRank,5);var fullArcane=RangedEngine.Calculate(Capture()).ExpectedShot;
            Yes(Capture().Arcane==merciless.Id&&Capture().ArcaneStacks==merciless.Cap&&arcaneRank.IsEnabled&&stacks.IsEnabled,"아케인 장착·시작 중첩 적용");
            await Select(arcaneRank,0);Yes(RangedEngine.Calculate(Capture()).ExpectedShot<fullArcane,"아케인 랭크 변경 → 실제 피해 갱신");
            await Select(arcane,0);Yes(!arcaneRank.IsEnabled&&!stacks.IsEnabled,"아케인 해제 → 랭크·중첩 비활성");
            var capacityBefore=RangedEngine.Calculate(Capture()).Capacity;
            ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(catalyst)!.GetPattern(PatternInterface.Toggle)).Toggle();await Settle();
            Yes(RangedEngine.Calculate(Capture()).Capacity!=capacityBefore,"카탈리스트 스위치 → 실제 수용량 변경");
            await Select(sort,1);var firstOrder=library.Children.OfType<Button>().Select(b=>((ValueTuple<RangedMod,int>)b.Tag).Item1.Id).ToArray();
            var direction=LayoutDiagnostics.Tree(this).OfType<Button>().Single(b=>b.Content as string is "↑" or "↓");var oldArrow=direction.Content;
            await Invoke(direction);var reversed=library.Children.OfType<Button>().Select(b=>((ValueTuple<RangedMod,int>)b.Tag).Item1.Id).ToArray();
            Yes(!Equals(oldArrow,direction.Content)&&firstOrder.Reverse().SequenceEqual(reversed),"정렬 방향 버튼 → 아이콘·실제 목록 동시 반전");
            await Invoke(direction);await Select(sort,0);
        }
        finally{Restore(saved);await Settle();}
        return checks.ToArray();
    }
}
