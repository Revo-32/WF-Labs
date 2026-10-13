using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private static bool IsPresented(FrameworkElement element)
    {
        for(DependencyObject? current=element;current is not null&&current is not Window;current=System.Windows.Media.VisualTreeHelper.GetParent(current))
            if(current is UIElement view&&view.Visibility!=Visibility.Visible)return false;
        return element.ActualWidth>0&&element.ActualHeight>0;
    }
    internal object CheckUsabilityFlow()
    {
        var checks=new List<string>();var saved=ReadBuild();
        void Yes(bool x,string label){if(!x)throw new Exception(label);checks.Add(label);}
        try
        {
            ShowModding();UpdateLayout();
            Yes(!equationDetails.IsExpanded,"첫 화면에서 능력치가 수식보다 우선");
            Yes(SelectedEncounter==EncounterMode.FiveSeconds,"첫 실행은 DPS를 보여 주는 5초 교전");
            foreach(var (mode,button) in encounterButtons)
            {
                button.IsChecked=true;
                Yes(SelectedEncounter==mode&&encounterButtons.Values.Count(b=>b.IsChecked==true)==1,"한 번 선택으로 교전 변경 · "+mode);
            }
            var before=chanceValue.Text;
            var steel=catalog.Mods.Single(m=>m.EnglishName=="True Steel");
            var slots=new ModSlot?[8];slots[0]=new(steel.Id,steel.MaxRank);
            ApplyBuild(saved with{Slots=slots});
            Yes(current is {} c&&chanceValue.Text==c.CriticalChance.ToString("P1")&&statusValue.Text==c.StatusChance.ToString("P1"),"모드 변경 후 치확·상확이 계산값과 일치");
            Yes(chanceValue.Text!=before,"치확 모드 변경이 상단 숫자에 반영");
            UpdateLayout();var pos=weaponStatStrip!.TranslatePoint(new(),root);
            resultsScroller!.ScrollToEnd();UpdateLayout();
            Yes(pos==weaponStatStrip.TranslatePoint(new(),root)&&IsPresented(chanceValue)&&IsPresented(statusValue),"상세 결과를 스크롤해도 능력치는 고정");
            foreach(var section in Descendants((DependencyObject)resultsScroller.Content).OfType<Expander>())section.IsExpanded=true;
            equationDetails.IsExpanded=true;UpdateLayout();
            Yes(IsPresented(equationView)&&IsPresented(rawMetric)&&IsPresented(targetMetric)&&IsPresented(hitMetric)&&IsPresented(rateValue)&&IsPresented(traceText)&&IsPresented(notice),"계산식·기존 DPS·피해·출처 정보 유지");
            foreach(var section in Descendants((DependencyObject)resultsScroller.Content).OfType<Expander>())section.IsExpanded=false;
            equationDetails.IsExpanded=false;
            return new{passed=true,checks};
        }
        finally{ApplyBuild(saved);resultsScroller!.ScrollToTop();}
    }
    internal void UsabilityAnalysisPreview()
    {
        foreach(var section in Descendants((DependencyObject)resultsScroller!.Content).OfType<Expander>().Where(e=>Equals(e.Header,"타격별 변화 · 컨오 기여 · 방어력")))section.IsExpanded=true;
    }
    internal void UsabilityPreview(bool timed)
    {
        encounterButtons[timed?EncounterMode.FiveSeconds:EncounterMode.FirstHit].IsChecked=true;
        ApplySampleSetting(128);Recalculate();SettleSimulationForPreview();resultsScroller!.ScrollToTop();
    }
}
