using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private SchedulePage? schedulePage;
    private void EnsureSchedulePage()
    {
        if(schedulePage is null)
        {
            schedulePage=new(UiFonts.PersistChanges);
            schedulePage.ModRequested=id=>
            {
                var melee=catalog.Weapons.FirstOrDefault(w=>w.Id==id);if(melee is not null){ShowModding();weapon.SelectedItem=melee;return;}
                var gun=ReferenceCatalog.Load().Weapons.FirstOrDefault(w=>w.InternalId==id);if(gun is null)return;ShowRanged(gun.Category);rangedPages[gun.Category].SelectInventoryWeapon(gun.Id);
            };
            Closed+=(_,_)=>schedulePage.Dispose();
        }
    }
    internal void ShowSchedules(){EnsureSchedulePage();page.Content=schedulePage;SelectPage(6);}
}
