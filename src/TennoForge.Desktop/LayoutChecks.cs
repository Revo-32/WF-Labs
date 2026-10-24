using System.Windows;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    // Verify usable, visible work area rather than the height of an offscreen child.
    public object CheckModdingLayout(string name)
    {
        Rect Bounds(FrameworkElement element)=>element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
        var area=Bounds(page);var equipment=Bounds(equipmentCard!);var rack=Bounds(board.RackView);var tray=Bounds(board.LibraryView);
        var category=Bounds(weaponClass);var weaponName=Bounds(weapon);var stanceBounds=Bounds(stance);var actions=Bounds(equipmentActions!);
        var editor=Bounds(moddingEditor!);var results=Bounds(resultsDashboard!);
        if(!InsideColumn(editor)||!InsideColumn(results))throw new Exception(name+": 모딩·결과 열이 창 밖으로 밀림");
        if(Math.Abs(results.Width/editor.Width-1)>.01||editor.Right>=results.Left)throw new Exception(name+": 5:5 열 비율 또는 분리 실패");
        bool InsideColumn(Rect r)=>r.Left>=area.Left-.5&&r.Right<=area.Right+.5&&r.Top>=area.Top-.5&&r.Bottom<=area.Bottom+.5;
        var geometry=board.MeasureLibrary(root);
        bool Inside(Rect r)=>r.Left>=area.Left-.5&&r.Right<=area.Right+.5&&r.Top>=area.Top-.5&&r.Bottom<=area.Bottom+.5;
        if(!Inside(equipment)||!Inside(rack)||!Inside(tray))throw new Exception($"{name}: 설정·장착 슬롯·보관함이 화면 밖으로 밀렸습니다. 영역 {area}, 설정 {equipment}, 슬롯 {rack}, 보관함 {tray}");
        var upgrades=Bounds(equipmentSlots!);
        if(!Inside(upgrades)||upgrades.Top<rack.Bottom||upgrades.Bottom>tray.Top)throw new Exception(name+": 추가 장비 슬롯이 모드 아래에 배치되지 않음");
        if(Math.Abs(category.Top-weaponName.Top)>.5||Math.Abs(category.Bottom-weaponName.Bottom)>.5)throw new Exception(name+": 무기군과 무기 선택이 같은 줄에 있지 않습니다.");
        if(stanceBounds.Top<actions.Top-.5||stanceBounds.Bottom>actions.Bottom+.5||stanceBounds.Width<120)throw new Exception(name+": 스탠스가 편집 버튼 옆에서 사용할 수 있는 너비로 배치되지 않았습니다.");
        if(equipment.Height>124||attackMode.ActualHeight>34)throw new Exception($"{name}: 상단 여백 과다 (설정 {equipment.Height}, 계산 방식 {attackMode.ActualHeight})");
        if(geometry.Columns<3||geometry.FullyVisibleRows<2)throw new Exception($"{name}: 보관함에서 온전한 모드 카드 두 줄이 보이지 않습니다. {geometry}");
        if(geometry.ScrollThumbHeight<24)throw new Exception(name+": 보관함 스크롤 손잡이가 너무 작게 잘렸습니다.");
        if(geometry.ViewportBottom>area.Bottom+.5||geometry.RackBottom>geometry.ViewportTop)throw new Exception(name+": 보관함과 슬롯이 겹치거나 잘립니다.");
        var strip=Bounds(weaponStatStrip!);
        if(!Inside(strip)||strip.Height>150)throw new Exception(name+": 핵심 능력치 고정 영역이 너무 크거나 창 밖에 있음");
        foreach(var value in new[]{chanceValue,criticalValue,statusValue,speedValue})
            if(!IsPresented(value)||!strip.Contains(Bounds(value))||value.FontSize<24)throw new Exception(name+": 치확·상확 등 핵심 능력치가 바로 보이지 않음");
        var header=CheckHeaderLayout();
        var typography=new{scale=System.Windows.Media.VisualTreeHelper.GetDpi(root).PixelsPerDip,format=System.Windows.Media.TextOptions.GetTextFormattingMode(root).ToString(),smoothing=System.Windows.Media.TextOptions.GetTextRenderingMode(root).ToString()};
        return new{name,width=root.ActualWidth,height=root.ActualHeight,columns=new{editor=editor.Width,results=results.Width,ratio="5:5 · 하단 상황 설정"},equipmentHeight=equipment.Height,attackModeHeight=attackMode.ActualHeight,library=geometry,header,typography,passed=true};
    }
    public void SettleSimulationForPreview()
    {
        debounce.Stop();
        ShowEncounter(TennoForge.Core.EncounterEngine.Calculate(catalog,ReadBuild(),stanceCatalog,64));
        if(currentStance is {Duration:null}){ttkMetric.Text="시간 미기재";spread.Text="동작별 타격 피해 제공 · 위키에 전체 시간 없음";}
        else if(currentStance is {} sc)ShowStanceTtk(TennoForge.Core.StanceEngine.Simulate(sc,ReadBuild().Scenario.Enemy!.Health));
        else if(current is {} c)ShowTtk(TennoForge.Core.DamageEngine.Simulate(c,ReadBuild().Scenario.Enemy!.Health));
    }
}
