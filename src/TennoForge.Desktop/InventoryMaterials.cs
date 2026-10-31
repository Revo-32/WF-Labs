using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class InventoryPage
{
    internal readonly Grid MaterialsPane=new();
    internal RadioButton MaterialsTab=null!;
    internal readonly MaterialGallery MaterialsGallery=new();
    internal readonly CheckBox ConsumeMaterialEquipment=new(){Content="보유 장비를 제작 재료로 사용",Margin=new(0,0,0,8)};
    internal readonly CheckBox MissingMaterialsOnly=new(){Content="부족한 재료만",Margin=new(16,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    internal readonly TextBox MaterialSearch=new(){Height=36,MinWidth=220};
    internal readonly ComboBox MaterialCategory=new(){MinWidth=172,Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    internal readonly ComboBox MaterialSort=new(){MinWidth=172,Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    internal MasteryMaterial[] MaterialBlueprintRows {get;private set;}=[];
    internal readonly Expander MaterialBlueprints=new(){Margin=new(0,0,0,12)};
    internal readonly Expander MaterialCalculation=new(){Margin=new(0,16,0,16)};
    private readonly TextBlock materialCounts=Text("",13,"MutedBrush");
    private readonly TextBlock materialsSummary=Text("",14,"MutedBrush"),materialsNotes=Text("",13,"MutedBrush");
    private readonly TextBlock materialValidity=Text("보유 목록이 일부만 확인되어 부족분은 잠정 수량입니다.",13,"AccentBrush");
    private readonly StackPanel materialEmpty=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(16,32,16,48)};
    private readonly TextBlock materialEmptyText=Text("",15,"MutedBrush");
    private readonly Button materialEmptyAction=new(){Margin=new(0,12,0,0),HorizontalAlignment=HorizontalAlignment.Center};
    private readonly Border materialFooter=new(){BorderThickness=new(0,1,0,0),Padding=new(0,16,0,0),Margin=new(0,20,0,16)};
    private MasteryMaterialPlan? materialPlan;
    private InventorySnapshot? materialSnapshot;
    private bool materialConsumeSetting;

    private void BuildMaterialsTab()
    {
        MaterialsTab=new(){Content="필요 재료",GroupName=MasteryTab.GroupName,Style=MasteryTab.Style,Margin=new(16,0,0,0)};
        AutomationProperties.SetName(MaterialsTab,"마스터리 필요 재료");((Panel)MasteryTab.Parent).Children.Add(MaterialsTab);
        MaterialsPane.Visibility=Visibility.Collapsed;
        MaterialsPane.RowDefinitions.Add(new(){Height=GridLength.Auto});
        MaterialsPane.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        Grid.SetRow(MaterialsPane,1);Grid.SetRowSpan(MaterialsPane,3);MasteryArea.Children.Add(MaterialsPane);
        var header=new StackPanel{Margin=new(0,0,0,12)};
        materialsSummary.Margin=new(0,0,0,12);header.Children.Add(materialsSummary);
        materialValidity.Margin=new(0,0,0,8);header.Children.Add(materialValidity);
        MaterialCategory.ItemsSource=new[]{"전체 재료","가공 재료","오픈월드 가공 재료","희귀 재화","일반 재화","부품·선행 장비","기타 재료"};MaterialCategory.SelectedIndex=0;
        MaterialSort.ItemsSource=new[]{"준비 난도순","분류 내 부족량순","분류 내 이름순"};MaterialSort.SelectedIndex=0;
        AutomationProperties.SetName(MaterialCategory,"재료 분류");AutomationProperties.SetName(MaterialSort,"재료 정렬");
        MaterialSort.ToolTip="카테고리는 유지하고 각 카테고리 안에서 정렬합니다. 준비 난도순은 WF Labs의 준비 분류 기준입니다.";
        var filters=new DockPanel();DockPanel.SetDock(MaterialSort,Dock.Right);filters.Children.Add(MaterialSort);
        DockPanel.SetDock(MissingMaterialsOnly,Dock.Right);filters.Children.Add(MissingMaterialsOnly);
        DockPanel.SetDock(MaterialCategory,Dock.Right);filters.Children.Add(MaterialCategory);
        filters.Children.Add(new SearchField(MaterialSearch,"재료 이름 검색"));header.Children.Add(filters);
        materialCounts.Margin=new(0,8,0,0);header.Children.Add(materialCounts);MaterialsPane.Children.Add(header);
        Grid.SetRow(MaterialsGallery,1);MaterialsPane.Children.Add(MaterialsGallery);
        materialEmpty.Children.Add(materialEmptyText);materialEmpty.Children.Add(materialEmptyAction);
        materialEmptyAction.Click+=(_,_)=>
        {
            if(materialPlan?.MasteryKnown!=true)SyncButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else if(MaterialSearch.Text.Length>0||MissingMaterialsOnly.IsChecked==true||MaterialCategory.SelectedIndex>0)
            {MaterialSearch.Clear();MissingMaterialsOnly.IsChecked=false;MaterialCategory.SelectedIndex=0;MaterialSearch.Focus();}
            else MasteryTab.IsChecked=true;
        };
        var calculationContent=new StackPanel{Margin=new(0,10,0,0)};calculationContent.Children.Add(ConsumeMaterialEquipment);
        materialsNotes.Margin=new(0,8,0,0);calculationContent.Children.Add(materialsNotes);
        MaterialCalculation.Header="계산 기준·제작비·장비 재료 사용 설정";MaterialCalculation.Content=calculationContent;
        materialFooter.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");materialFooter.Child=MaterialBlueprints;
        MaterialBlueprints.Expanded+=(_,_)=>RefreshMaterials();MaterialBlueprints.Collapsed+=(_,_)=>RefreshMaterials();
        MaterialsTab.Checked+=(_,_)=>
        {
            foreach(var child in MasteryArea.Children.OfType<FrameworkElement>().Where(x=>Grid.GetRow(x)>0))
                child.Visibility=ReferenceEquals(child,MaterialsPane)?Visibility.Visible:Visibility.Collapsed;
            MasteryCount.Visibility=Visibility.Collapsed;RefreshMaterials();
        };
        ConsumeMaterialEquipment.Checked+=(_,_)=>RefreshMaterials();ConsumeMaterialEquipment.Unchecked+=(_,_)=>RefreshMaterials();
        MissingMaterialsOnly.Checked+=(_,_)=>RefreshMaterials();MissingMaterialsOnly.Unchecked+=(_,_)=>RefreshMaterials();
        MaterialSearch.TextChanged+=(_,_)=>RefreshMaterials();MaterialCategory.SelectionChanged+=(_,_)=>RefreshMaterials();MaterialSort.SelectionChanged+=(_,_)=>RefreshMaterials();
    }

    private void RefreshMaterials()
    {
        if(MaterialsTab?.IsChecked!=true)return;
        bool consume=ConsumeMaterialEquipment.IsChecked==true;
        bool changed=materialPlan is null||!ReferenceEquals(materialSnapshot,Snapshot)||materialConsumeSetting!=consume;
        if(changed){materialPlan=MasteryMaterials.Calculate(CatalogData,Snapshot,consume);materialSnapshot=Snapshot;materialConsumeSetting=consume;}
        var plan=materialPlan!;
        if(changed)
        {
            materialValidity.Visibility=plan.MasteryKnown&&!plan.InventoryKnown?Visibility.Visible:Visibility.Collapsed;
            materialsSummary.Text=plan.MasteryKnown?$"미숙련 제작 장비 {plan.RemainingCraftable:N0}개 · 보유·제작 중 {plan.AlreadyAvailable:N0}개 · 추가 제작 {plan.CraftJobs:N0}회 · "+(consume?"보유 장비 재료 사용 허용":"보유 장비 보존"):"마스터리 이력을 동기화하면 필요한 재료를 확인할 수 있습니다.";
            var blueprints=plan.Blueprints.Where(r=>r.Missing>0).ToArray();
            MaterialBlueprints.Header=$"추가로 필요한 설계도 · {blueprints.Length:N0}종";
            MaterialBlueprintRows=blueprints;
            materialFooter.Visibility=plan.MasteryKnown?Visibility.Visible:Visibility.Collapsed;
            MaterialCalculation.Visibility=materialFooter.Visibility;
            materialsNotes.Text=string.Join("\n",new[]{$"제작비 {plan.Credits:N0} Cr · 크레딧 설계도 구매 {plan.BlueprintCredits:N0} Cr (확인된 구매가)","기본 제작식 기준 · 완성 부품의 보유량과 제작 중 수량을 먼저 반영합니다. 선행 장비는 랭작 후 다음 제작에 사용하며, 재료 재고는 전체에서 한 번만 차감합니다."}.Concat(plan.Warnings));
        }
        var search=MaterialSearch.Text.Trim();var order=(MaterialOrder)Math.Max(0,MaterialSort.SelectedIndex);
        var matches=MaterialPreparation.Sort(plan.Materials.Where(r=>(MissingMaterialsOnly.IsChecked!=true||r.Missing>0)&&
            (MaterialCategory.SelectedIndex<=0||r.PreparationGroup==MaterialCategory.SelectedItem as string)&&
            (search.Length==0||r.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||r.EnglishName.Contains(search,StringComparison.OrdinalIgnoreCase))),order);
        materialCounts.Text=$"전체 {plan.Materials.Length:N0}종 · 부족 {plan.Materials.Count(r=>r.Missing>0):N0}종 · 현재 표시 {matches.Length:N0}종";
        bool filtered=search.Length>0||MissingMaterialsOnly.IsChecked==true||MaterialCategory.SelectedIndex>0;
        materialEmptyText.Text=!plan.MasteryKnown?"마스터리 이력을 동기화하면 필요한 재료가 표시됩니다.":filtered?"조건에 맞는 재료가 없습니다.":"추가로 준비할 제작 재료가 없습니다.";
        materialEmptyAction.Content=!plan.MasteryKnown?"게임에서 동기화":filtered?"필터 초기화":"전체 요약 보기";
        MaterialsGallery.SetMaterials(matches,order,materialFooter,MaterialCalculation,materialEmpty,MaterialBlueprintRows,MaterialBlueprints.IsExpanded);
    }
}
