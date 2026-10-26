using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckApproximationUx(Action<FrameworkElement,string,double> render)
    {
        var checks=new List<string>();
        foreach(var dark in new[]{true,false})
        foreach(var name in new[]{"Phantasma","Sporelacer (Secondary)","Tombfinger (Secondary)"})
        {
            SetDarkTheme(dark);var category=name=="Phantasma"?"primary":"secondary";ShowRanged(category);UpdateLayout();
            checks.AddRange(await rangedPages[category].CheckApproximationUx(name,(root,phase,scale)=>{render(root,$"{name}-{dark}-{phase}-{scale}",scale);if(workspaceViewport.ScrollableWidth>1)throw new Exception("지원 최소 창에서 가로 스크롤 발생: "+workspaceViewport.ScrollableWidth);}));
        }
        return new{passed=true,checks};
    }
}

internal sealed partial class RangedPage
{
    internal async Task<string[]> CheckApproximationUx(string name,Action<FrameworkElement,string,double> render)
    {
        var saved=Capture();var savedSamples=CombatSamples;CombatSamples=()=>256;var checks=new List<string>();
        void Yes(bool value,string text){if(!value)throw new Exception(name+": "+text);checks.Add(name+": "+text);}
        async Task Settle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        try
        {
            var w=RangedEngine.Weapons.Single(w=>w.EnglishName==name);
            Restore(saved with{WeaponId=w.Id,AttackIndex=name=="Phantasma"?1:0,Slots=new ModSlot?[8],Conditions=new(AllowApproximations:true),Kitgun=null,Arcane="None",Exilus=null,Riven=null,Progenitor=null,Evolutions=[],EncounterMode=1});
            scenarioDrawer?.SetOpen(true,false);await Settle();
            Yes(LayoutDiagnostics.Tree(conditionPanel).OfType<CheckBox>().Any(c=>c.Content as string=="미기재 규칙은 근사 계산"&&c.IsChecked==true),"근사 설정 표시");
            if(name=="Phantasma")
            {
                var label=LayoutDiagnostics.Tree(conditionPanel).OfType<TextBlock>().Single(t=>t.Text.StartsWith("충전할 탄약"));
                var box=((Panel)label.Parent).Children.OfType<TextBox>().Single();
                ((IValueProvider)UIElementAutomationPeer.CreatePeerForElement(box)!.GetPattern(PatternInterface.Value)).SetValue("2");await Settle();
                Yes(Capture().Conditions!.PhantasmaChargeAmmo==2,"충전 탄약 입력 이벤트 반영");
                Yes(Math.Abs(RangedEngine.Calculate(Capture()).Cadence!.Charge-2/11d)<1e-9,"입력 뒤 충전 시간 갱신");
            }
            foreach(var scale in new[]{1d,1.25,1.5})render((FrameworkElement)Window.GetWindow(this).Content,"settings",scale);
            scenarioDrawer?.SetOpen(false,false);await Settle();
            attack.SelectedIndex=name=="Tombfinger (Secondary)"?1:3;await Settle();Yes((calculateButton.Content as string)?.StartsWith("전체 공격 DPS·TTK ")==true,"파생 구성요소 선택 시 전체 공격 버튼 표시");
            var before=combatResultHost.Content;
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(calculateButton)!.GetPattern(PatternInterface.Invoke)).Invoke();
            var timeout=Stopwatch.StartNew();
            do{await Task.Delay(40);await Settle();}while((simulation is not null||combatResultHost.Content==before)&&timeout.Elapsed<TimeSpan.FromMinutes(2));
            Yes(attack.SelectedIndex==(name=="Phantasma"?1:0),"전체 공격 버튼 → 본탄 발사로 연결");
            Yes(simulation is null&&combatResultHost.Content is not null&&combatResultHost.Content!=before,"실제 계산 버튼 → 교전 결과 생성");
            Yes(accuracyStatus.Text.StartsWith("근사 포함")&&accuracyStatus.IsVisible,"고정 결과 헤더에 근사 표시");
            var notes=approximationDetails;
            ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(notes)!.GetPattern(PatternInterface.ExpandCollapse)).Expand();await Settle();
            Yes(notes.IsExpanded,"근사 상세 펼치기 이벤트");
            Yes(!LayoutDiagnostics.Tree((DependencyObject)combatResultHost.Content!).OfType<TextBlock>().Any(t=>t.Text=="> 0초"),"첫 발 미처치에 0초 TTK를 표시하지 않음");
            foreach(var scale in new[]{1d,1.25,1.5})render((FrameworkElement)Window.GetWindow(this).Content,"result",scale);
        }
        finally{CombatSamples=savedSamples;Restore(saved);await Settle();}
        return checks.ToArray();
    }
}
