using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using TennoForge.Core;
namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal async Task<object> CheckLiveInventoryWorkflow(string directory,Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();void Yes(bool b,string s){if(!b)throw new InvalidOperationException(s);checks.Add(s);}
        static void Click(Button b)=>((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
        ShowInventory();var p=inventoryPage!;var watch=System.Diagnostics.Stopwatch.StartNew();Click(p.SyncButton);await Dispatcher.Yield();
        while(!p.SyncButton.IsEnabled&&watch.Elapsed<TimeSpan.FromSeconds(20))await Task.Delay(100);
        Yes(p.Snapshot.InventoryComplete&&p.Snapshot.MasteryComplete&&p.Snapshot.Equipment.Length>0,"실제 게임 → 실제 동기화 버튼 → 보유·마스터리 이력 수집");
        var snapshot=p.Snapshot;Yes(p.MasteryList.Items.Count>0&&p.Progress.Model?.Owned==p.CatalogData.Items.Count(i=>i.Masterable&&snapshot.Count(i.Id)>0),"실제 보유 목록과 랭작 추천 UI 연결");
        render(this,"real-dashboard",1760,920,1);p.Ownership.SelectedIndex=2;
        var item=snapshot.Equipment.First(x=>p.CatalogData.Items.Any(i=>i.Id==x.Id));p.Search.Text=p.CatalogData.Name(item.Id);p.UpdateLayout();Yes(p.MasteryList.Items.Count>0,"실제 보유 장비 한국어 검색");p.Search.Text="";
        string path=Path.Combine(directory,"normalized-snapshot.json");p.ChooseExportFile=()=>path;Click(p.ExportButton);await Dispatcher.Yield();Yes(File.Exists(path),"실제 내보내기 버튼 · 허용 필드만 저장");
        string raw=File.ReadAllText(path);Yes(!raw.Contains("AccountId",StringComparison.OrdinalIgnoreCase)&&!raw.Contains("Nonce",StringComparison.OrdinalIgnoreCase),"정규화 저장에 계정 인증 필드 없음");
        using var restored=new InventoryPage(false);restored.ChooseImportFile=()=>path;Click(restored.ImportButton);await Dispatcher.Yield();Yes(restored.Snapshot.Quantities.OrderBy(x=>x.Key).SequenceEqual(snapshot.Quantities.OrderBy(x=>x.Key))&&restored.Snapshot.MasteryRanks.OrderBy(x=>x.Key).SequenceEqual(snapshot.MasteryRanks.OrderBy(x=>x.Key)),"새 페이지 인스턴스 → 실제 불러오기 버튼 → 보유·이력 원형 복원");
        return new{passed=checks.Count,failed=Array.Empty<string>(),checks,seconds=watch.Elapsed.TotalSeconds,masteryRank=snapshot.MasteryRank,equipment=snapshot.Equipment.Length,items=snapshot.Quantities.Count,mastery=snapshot.MasteryRanks.Count,snapshot.Sections,source=snapshot.Source};
    }
}
