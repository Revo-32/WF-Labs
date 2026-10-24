using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckRangedWorkflow(Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var originalSamples=SelectedSamples;
        foreach(var dark in new[]{true,false})foreach(var kind in new[]{"primary","secondary"})
        {
            SetDarkTheme(dark);ShowRanged(kind);UpdateLayout();
            ApplySampleSetting(512);
            if(rangedPages[kind].CombatSamples()!=512)throw new Exception("전역 표본 설정이 총기 페이지에 전달되지 않음");
            checks.Add(kind+": 전역 표본 설정 → 총기 계산 전달");ApplySampleSetting(originalSamples);
            checks.AddRange(await rangedPages[kind].CheckRangedWorkflow((name,width,height,scale)=>
            {
                Width=width;Height=height;UpdateLayout();render((FrameworkElement)Content,$"{kind}-{dark}-{name}-{width}x{height}-{scale}",width,height,scale);
                if(workspaceViewport.ScrollableWidth>1)throw new Exception("최소 창 가로 스크롤: "+workspaceViewport.ScrollableWidth);
            }));
        }
        return new{passed=true,method="WPF UIA Invoke·Value·SelectionItem, 검색 키 이벤트, 실제 비동기 256표본 계산, 최소 창/DPI/테마 렌더링",checks};
    }
}

internal sealed partial class RangedPage
{
    internal async Task<string[]> CheckRangedWorkflow(Action<string,int,int,double> render)
    {
        var saved=Capture();var oldBaseline=comparisonBaseline;var savedSamples=CombatSamples;CombatSamples=()=>256;var checks=new List<string>();
        void Yes(bool value,string name){if(!value)throw new Exception(category+": "+name);checks.Add(category+": "+name);}
        async Task Settle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        async Task Invoke(UIElement element){((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(element)!.GetPattern(PatternInterface.Invoke)).Invoke();await Settle();}
        async Task Value(TextBox box,string value){((IValueProvider)UIElementAutomationPeer.CreatePeerForElement(box)!.GetPattern(PatternInterface.Value)).SetValue(value);await Settle();}
        async Task Select(ComboBox box,int index)
        {
            var peer=UIElementAutomationPeer.CreatePeerForElement(box)!;
            ((IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            var choices=peer.GetChildren().Where(p=>p.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider).ToArray();
            ((ISelectionItemProvider)choices[index].GetPattern(PatternInterface.SelectionItem)).Select();box.IsDropDownOpen=false;await Settle();
        }
        async Task KeyPress(Key key)
        {
            var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(weapon.QueryInput),Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};
            weapon.QueryInput.RaiseEvent(args);await Settle();
        }
        async Task Calculate()
        {
            await Invoke(calculateButton);var timer=Stopwatch.StartNew();
            while(simulation is not null&&timer.Elapsed<TimeSpan.FromMinutes(3)){await Task.Delay(40);await Settle();}
            Yes(lastCombat is not null&&combatResultHost.Content is not null,"계산 버튼 → 최신 교전 결과 완료: "+message.Text);
            Yes(lastCombat!.Encounter.Trials==CombatSamples()&&lastCombat.Result.Dps.Samples==CombatSamples(),"선택한 표본 수로 실제 계산");
            Yes(pinComparison.IsEnabled&&calculateButton.IsEnabled&&cancelButton.Visibility==Visibility.Collapsed,"계산 완료 후 버튼 상태 복구");
        }
        async Task Equip(string english,int slot)
        {
            await Invoke(SlotCard(slot));await Value(query,english);
            var button=library.Children.OfType<Button>().Single(b=>b.Tag is ValueTuple<RangedMod,int> pair&&pair.Item1.EnglishName==english);
            await Invoke(button);await Value(query,"");Yes(slots[slot] is not null,"검색한 모드 장착: "+english);
        }
        async Task RenderStates(string phase)
        {
            foreach(var scale in new[]{1d,1.25,1.5})foreach(var size in new[]{(1760,920),(1920,1080)})
            {
                render(phase,size.Item1,size.Item2,scale);await Settle();
                var issues=LayoutDiagnostics.Inspect(this);
                Yes(issues.Length==0,$"{phase} {size} {scale}: 겹침·잘림 없음 "+string.Join("; ",issues.Take(3)));
                var second=(FrameworkElement)library.Children[library.Columns];
                var rect=second.TransformToAncestor(libraryScroller).TransformBounds(new Rect(second.RenderSize));
                Yes(rect.Bottom<=libraryScroller.ActualHeight+1,"최소 창 모드 보관함 두 줄 확보");
            }
        }
        try
        {
            comparisonBaseline=null;Restore(saved with{Slots=new ModSlot?[8],Arcane="None",Exilus=null,Conditions=new(AllowApproximations:true),EncounterMode=3});
            var beforeWeapon=Selected!.Id;var name=category=="primary"?"Braton Prime":"Lato Vandal";
            await Value(weapon.QueryInput,name);
            Yes(weapon.IsDropDownOpen&&Selected!.Id==beforeWeapon,"무기 검색 입력은 선택을 확정하지 않음");
            await KeyPress(Key.Enter);Yes(Selected!.Id==beforeWeapon,"후보 선택 전 Enter가 임의 무기를 확정하지 않음");
            for(int i=0;i<weapon.Suggestions.Items.Count;i++){await KeyPress(Key.Down);if(weapon.Suggestions.SelectedItem is Weapon w&&w.EnglishName==name)break;}
            await KeyPress(Key.Enter);Yes(Selected!.EnglishName==name&&!weapon.IsDropDownOpen,"화살표·Enter로 무기 확정");
            var handle=LayoutDiagnostics.Tree(scenarioDrawer!).OfType<Button>().First(b=>AutomationProperties.GetName(b).StartsWith("상황 설정 열기"));
            await Invoke(handle);await Task.Delay(250);await Settle();Yes(scenarioDrawer!.IsOpen,"상황 설정 손잡이 열기");
            var targetFaction=faction.Items.Cast<EnemyDamageProfile>().Select((f,i)=>(f,i)).First(x=>EnemyCatalog.Load().Enemies.Any(e=>e.Faction==x.f.Id&&e.Name.Contains("헤비 거너"))).i;
            await Select(faction,targetFaction);await Select(enemy,enemy.Items.Cast<EnemyDefinition>().Select((e,i)=>(e,i)).First(x=>x.e.Name.Contains("헤비 거너")).i);
            await Value(level,"200");await Select(difficulty,1);await Select(encounter,3);
            var close=LayoutDiagnostics.Tree(scenarioDrawer).OfType<Button>().First(b=>b.Content as string=="완료 · 닫기");await Invoke(close);await Task.Delay(200);await Settle();
            Yes(!scenarioDrawer.IsOpen&&Capture().Enemy?.Level==200&&difficulty.SelectedIndex==1,"대상·레벨·강철의 길 설정 완료");
            await Calculate();Yes(accuracyStatus.IsVisible&&accuracyStatus.Text.Contains("계산 완료"),"고정 헤더에 계산 적용 상태 표시");
            var baseDamage=lastCombat!.Result.Dps.Mean;
            await Invoke(pinComparison);Yes(comparisonBaseline==lastCombat,"계산 결과 A 지정");
            await Equip(category=="primary"?"Serration":"Hornet Strike",0);
            await Equip(category=="primary"?"Split Chamber":"Barrel Diffusion",1);
            Yes(lastCombat is null&&!pinComparison.IsEnabled&&combatResultHost.Content is null,"모딩 변경 시 이전 교전 결과·기준 지정 무효화");
            Yes(comparisonBaseline!.Build.Slots.All(s=>s is null),"A 빌드가 B의 슬롯 변경으로 오염되지 않음");
            await Calculate();Yes(lastCombat!.Result.Dps.Mean>baseDamage,"모딩 후 실제 교전 DPS 증가");
            Yes(ScenarioKey(comparisonBaseline!)==ScenarioKey(lastCombat),"동일 교전 조건으로 A/B 비교");
            var delta=lastCombat.Result.Dps.Mean-baseDamage;
            Yes(LayoutDiagnostics.Tree(comparisonHost).OfType<TextBlock>().Any(t=>t.Text.StartsWith("B − A")&&t.Text.Contains(delta.ToString("+#,##0.0;-#,##0.0;0.0"))),"DPS 증감 표시와 표본 결과 일치");
            await RenderStates("comparison");
            var graph=LayoutDiagnostics.Tree(comparisonHost).OfType<Expander>().Single(e=>e.Header as string=="A / B 시간별 DPS 그래프");
            ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(graph)!.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            render("comparison-graph",1760,920,1);Yes(LayoutDiagnostics.Tree(graph).OfType<RangedDpsPlot>().Single().IsVisible,"A/B 그래프 펼치기");
            await Value(level,"201");await Calculate();
            Yes(LayoutDiagnostics.Tree(comparisonHost).OfType<TextBlock>().Any(t=>t.Text.StartsWith("교전 조건이 다릅니다")),"대상 조건 변경 시 증감률 대신 조건 불일치 표시");
            await Invoke(restoreComparison);Yes(slots.All(s=>s is null)&&level.Text=="200"&&lastCombat is null,"기준 빌드 복원 버튼 → 모드·대상 복원, 재계산 필요");
            await Invoke(calculateButton);await Invoke(cancelButton);await Task.Delay(150);await Settle();
            Yes(lastCombat is null&&!pinComparison.IsEnabled,"취소된 결과를 비교 기준으로 지정할 수 없음");
            await Invoke(calculateButton);await Value(level,"202");await Task.Delay(100);await Settle();
            Yes(simulation is null&&lastCombat is null&&calculateButton.IsEnabled&&!progressText.Text.Contains("계산 취소"),"계산 중 조건 변경 → 취소 후 오래된 완료·취소 상태 차단");
            await Invoke(clearComparison);Yes(comparisonBaseline is null&&comparisonHost.Content is null,"비교 해제 버튼");
            Yes(!exilusRank.IsEnabled&&!arcaneRank.IsEnabled&&!stacks.IsEnabled,"미장착 장비의 랭크·중첩 비활성 표시");
            await Equip(category=="primary"?"Galvanized Aptitude":"Galvanized Diffusion",0);
            await RenderStates("long-name");
            var modName=RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slots[0]!.ModId).Name;
            var label=LayoutDiagnostics.Tree(SlotCard(0)).OfType<TextBlock>().Single(t=>t.Text==modName);
            var probe=new TextBlock{Text=label.Text,FontFamily=label.FontFamily,FontSize=label.FontSize,FontWeight=label.FontWeight,TextWrapping=TextWrapping.Wrap,LineHeight=label.LineHeight};
            probe.Measure(new Size(label.ActualWidth,double.PositiveInfinity));
            Yes(probe.DesiredSize.Height<=label.ActualHeight+1,"긴 모드 이름 말줄임 별도 검사: "+modName);
            StressLongNames(true);await Settle();render("longest-eight",1760,920,1);
            foreach(var card in slotsPanel.Children.OfType<Panel>().SelectMany(p=>p.Children.OfType<Button>()))
            {
                if(card.Tag is not ValueTuple<RangedMod,int> info)continue;
                var nameBlock=LayoutDiagnostics.Tree(card).OfType<TextBlock>().Single(t=>t.Text==info.Item1.Name);
                var measured=new TextBlock{Text=nameBlock.Text,FontFamily=nameBlock.FontFamily,FontSize=nameBlock.FontSize,FontWeight=nameBlock.FontWeight,TextWrapping=nameBlock.TextWrapping,LineHeight=nameBlock.LineHeight};
                measured.Measure(new Size(nameBlock.ActualWidth,double.PositiveInfinity));
                Yes(measured.DesiredSize.Height<=nameBlock.ActualHeight+1,"최장 모드 이름 전체 표시: "+nameBlock.Text);
            }
            StressLongNames(false);await Settle();resultScroller.ScrollToEnd();libraryScroller.ScrollToEnd();await Settle();
            render("scroll-bottom",1760,920,1);Yes(LayoutDiagnostics.Inspect(this).Length==0,"결과·보관함 맨 아래 스크롤 겹침·잘림 없음");resultScroller.ScrollToTop();libraryScroller.ScrollToTop();
            var clear=LayoutDiagnostics.Tree(this).OfType<Button>().Single(b=>b.Content as string=="8칸 비우기");await Invoke(clear);Yes(slots.All(s=>s is null),"8칸 비우기 버튼");
        }
        finally{InvalidateCombat();CombatSamples=savedSamples;comparisonBaseline=oldBaseline;Restore(saved);await Settle();}
        return checks.ToArray();
    }
}
