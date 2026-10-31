using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private Grid? relicHub;
    private ContentControl? relicHubContent;
    internal RadioButton? RelicRewardsTab,RelicPlanningTab;
    private void EnsureRelicHub()
    {
        EnsureRelicPage();if(relicHub is not null)return;
        relicHub=new Grid();relicHub.RowDefinitions.Add(new(){Height=GridLength.Auto});relicHub.RowDefinitions.Add(new(){Height=GridLength.Auto});relicHub.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(8,8,0,12)};relicHub.Children.Add(tabs);
        relicHubContent=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};Grid.SetRow(relicHubContent,2);relicHub.Children.Add(relicHubContent);
        var recognition=relicPage!.RecognitionBar;if(recognition.Parent is Panel parent)parent.Children.Remove(recognition);Grid.SetRow(recognition,1);relicHub.Children.Add(recognition);
        RelicRewardsTab=new(){Content="보상 인식·시세",GroupName="RelicHub"+GetHashCode(),Style=(Style)Application.Current.Resources["NavigationItem"],Margin=new(0,0,20,0)};
        RelicPlanningTab=new(){Content="성유물 추천",GroupName=RelicRewardsTab.GroupName,Style=RelicRewardsTab.Style};
        foreach(var tab in new[]{RelicRewardsTab,RelicPlanningTab}){AutomationProperties.SetName(tab,tab.Content.ToString());tabs.Children.Add(tab);}
        RelicRewardsTab.Checked+=(_,_)=>relicHubContent.Content=relicPage;
        RelicPlanningTab.Checked+=(_,_)=>{EnsureInventoryPage();relicHubContent.Content=inventoryPage!.RelicPlanningView;};
        RelicRewardsTab.IsChecked=true;
    }
    internal void ShowRelicPlanning(){EnsureRelicHub();RelicPlanningTab!.IsChecked=true;page.Content=relicHub;SelectPage(4);}
    internal void ShowRelicRewards(){ShowRelics();RelicRewardsTab!.IsChecked=true;}
}
