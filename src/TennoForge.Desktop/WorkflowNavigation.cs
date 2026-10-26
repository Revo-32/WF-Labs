using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private readonly WrapPanel workflowBar=new(){Orientation=Orientation.Horizontal,Margin=new(20,4,20,0)};
    private int lastModdingKind;
    private void ReturnToModding(){if(lastModdingKind==0)ShowModding();else ShowRanged(lastModdingKind==1?"primary":"secondary");}
    private void RefreshWorkflowBar()
    {
        workflowBar.Children.Clear();
        bool mod=navigation.Count>0&&navigation[0].IsChecked==true;
        bool riven=navigation.Count>1&&navigation[1].IsChecked==true;
        workflowBar.Visibility=mod||riven?Visibility.Visible:Visibility.Collapsed;
        if(!mod&&!riven)return;
        var title=T(mod?"모딩":"리벤 종류",14);title.Margin=new(0,0,16,0);title.VerticalAlignment=VerticalAlignment.Center;workflowBar.Children.Add(title);
        void Tab(string label,bool selected,Action action)
        {
            var button=new RadioButton{Content=label,GroupName="Workflow",IsChecked=selected,Height=36,MinWidth=96,Margin=new(0,0,8,0),Style=(Style)Application.Current.Resources["NavigationItem"]};
            System.Windows.Automation.AutomationProperties.SetName(button,label+(mod?" 모딩":" 리벤 분석"));
            button.Checked+=(_,_)=>action();workflowBar.Children.Add(button);
        }
        if(mod)
        {
            Tab("주무기",lastModdingKind==1,()=>ShowRanged("primary"));Tab("보조무기",lastModdingKind==2,()=>ShowRanged("secondary"));Tab("근접",lastModdingKind==0,ShowModding);
            var analyze=Button("리벤 분석 →",OpenCurrentRiven);analyze.Margin=new(24,0,0,0);analyze.Height=32;analyze.Padding=new(12,4,12,4);workflowBar.Children.Add(analyze);
        }
        else
        {
            string current=page.Content is RangedRivenWorkbench w?w.RivenClass:"Melee";
            foreach(var (label,kind) in new[]{("소총","Rifle"),("산탄총","Shotgun"),("권총","Pistol"),("근접","Melee")})Tab(label,current==kind,()=>OpenRivenClass(kind));
            var back=Button("← 모딩으로",ReturnToModding);back.Margin=new(24,0,0,0);back.Height=32;back.Padding=new(12,4,12,4);workflowBar.Children.Add(back);
            var scan=Button("화면 인식 · 오버레이",()=>ShowRivenCapture(current));scan.Margin=new(8,0,0,0);scan.Height=32;scan.Padding=new(12,4,12,4);workflowBar.Children.Add(scan);
            var note=T("공통 배율 1.2 · 종류별 옵션 범위",13);note.Margin=new(20,0,0,0);note.VerticalAlignment=VerticalAlignment.Center;workflowBar.Children.Add(note);
            if(current=="Melee"&&weapon.SelectedItem is Weapon melee){var target=T("DPS 비교 · "+melee.Name,13);target.Margin=new(20,0,0,0);target.VerticalAlignment=VerticalAlignment.Center;workflowBar.Children.Add(target);}
        }
    }
    private void OpenRivenClass(string kind)
    {
        switchingEquipment=true;equipmentKind.SelectedIndex=kind=="Melee"?0:kind=="Pistol"?2:1;switchingEquipment=false;
        ShowRivens();if(page.Content is RangedRivenWorkbench w)w.SelectClass(kind);RefreshWorkflowBar();
    }
    private void OpenCurrentRiven()
    {
        if(lastModdingKind==0){if(equippedRiven is {} roll&&traits.All(t=>(t.SelectedItem as RivenOption)?.Stat is null))ApplyRivenWorkbench(roll);OpenRivenClass("Melee");return;}
        var category=lastModdingKind==1?"primary":"secondary";var context=rangedPages[category].RivenContext;
        OpenRivenClass(RangedEngine.ModCategory(context.Weapon));
        if(context.Riven is {} ranged)rangedRivenPages[category].UseEquippedIfEmpty(ranged);
    }
}
