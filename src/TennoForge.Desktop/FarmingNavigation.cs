using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private FarmingWorkspace? farmingWorkspace;
    internal FarmingWorkspace Farming
    {
        get
        {
            if(farmingWorkspace is null)
            {
                farmingWorkspace=new(UiFonts.PersistChanges);
                farmingWorkspace.Changed+=()=>
                {
                    relicPage?.UpdateHighlights();
                    if(farmingWorkspace.Document.Goals.Any(g=>g.Enabled&&g.Notify)){EnsureSchedulePage();schedulePage!.StartMonitoring();}
                    schedulePage?.TickFeatures();
                };
            }
            return farmingWorkspace;
        }
    }
    private void AddCurrentFarmingGoal()
    {
        string? id=lastModdingKind==0?(weapon.SelectedItem as Weapon)?.Id:
            rangedPages.TryGetValue(lastModdingKind==1?"primary":"secondary",out var ranged)?ranged.RivenContext.Weapon.InternalId:null;
        if(id is null||!Farming.Catalog.Items.Any(i=>i.Id==id)){error.Text="이 조립 장비는 구성 부품을 장비 화면에서 목표로 선택하세요.";return;}
        ShowInventory();inventoryPage!.AddFarmingGoal(id);
    }
}
