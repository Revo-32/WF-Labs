using System.Windows;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private InventoryPage? inventoryPage;
    internal void ShowInventory()
    {
        if(inventoryPage is null)
        {
            inventoryPage=new InventoryPage(UiFonts.PersistChanges);
            inventoryPage.CanReadGame=()=>rivenCapturePending is null&&!capturingRelic&&relicPage?.Busy!=true;
            inventoryPage.ModRequested=id=>
            {
                var melee=catalog.Weapons.FirstOrDefault(w=>w.Id==id);
                if(melee is not null){ShowModding();weapon.SelectedItem=melee;return;}
                var gun=ReferenceCatalog.Load().Weapons.FirstOrDefault(w=>w.InternalId==id);
                if(gun is null){inventoryPage.Status.Text="이 장비는 현재 모딩 화면에 연결되지 않았습니다.";return;}
                ShowRanged(gun.Category);
                rangedPages[gun.Category].SelectInventoryWeapon(gun.Id);
            };
            Closed+=(_,_)=>inventoryPage.Dispose();
        }
        page.Content=inventoryPage;SelectPage(5);
    }
}
