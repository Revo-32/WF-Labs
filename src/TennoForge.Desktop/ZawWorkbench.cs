using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private ArcaneSlot? selectedExodia;
    private readonly ComboBox exodiaRank=new();
    private readonly TextBlock zawSlotName=T("조우 전용",14),zawSlotRank=T("",13);
    private FrameworkElement WeaponWithZawBuilder()
    {
        var row=new DockPanel();var open=Button("조우",()=>OpenZawBuilder());open.ToolTip="타격부 · 손잡이 · 연결부로 조우 조립";open.Margin=new(4,0,0,0);CompactInput(open,32);DockPanel.SetDock(open,Dock.Right);row.Children.Add(open);row.Children.Add(weapon);return row;
    }
    private FrameworkElement OpenZawBuilder(bool show=true)
    {
        var parts=ZawCatalog.Parts;var strike=new ComboBox{ItemsSource=parts.Strikes,DisplayMemberPath="Id",SelectedIndex=8};var grip=new ComboBox{ItemsSource=parts.Grips,DisplayMemberPath="Id",SelectedIndex=5};var link=new ComboBox{ItemsSource=parts.Links,DisplayMemberPath="Id",SelectedIndex=8};
        if(weapon.SelectedItem is Weapon selected&&ZawCatalog.IsZaw(selected.Id)){var ids=selected.Id[4..].Split('|');strike.SelectedItem=parts.Strikes.Single(p=>p.Id==ids[0]);grip.SelectedItem=parts.Grips.Single(p=>p.Id==ids[1]);link.SelectedItem=parts.Links.Single(p=>p.Id==ids[2]);}
        var label=T("",16);label.TextWrapping=TextWrapping.Wrap;
        string Id()=>ZawCatalog.Id(((ZawStrike)strike.SelectedItem).Id,((ZawGrip)grip.SelectedItem).Id,((ZawLink)link.SelectedItem).Id);
        void Refresh(){var w=ZawCatalog.Weapon(Id());label.Text=$"{w.WeaponType} · 기본 피해 {w.Damage.Values.Sum():0.##}\n치명타 {w.CriticalChance:P0} / {w.CriticalMultiplier:0.0}× · 상태 {w.StatusChance:P0}\n공격 속도 {w.AttackSpeed:0.###} · 리벤 공통 배율 1.2";}
        foreach(var c in new[]{strike,grip,link})c.SelectionChanged+=(_,_)=>Refresh();Refresh();
        var popup=new Popup{PlacementTarget=weapon,Placement=PlacementMode.Bottom,StaysOpen=false};
        var select=Button("이 조우 사용",()=>{weaponClass.SelectedIndex=0;weapon.SelectedItem=catalog.Weapons.Single(w=>w.Id==Id());popup.IsOpen=false;});
        var note=T("랭크 30 완성 수치 · 무기군에 맞는 스탠스 연결\n강공격·슬램의 별도 무기 수치는 원문 확인 범위만 제공",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var panel=new Border{Width=440,Padding=new(16),BorderThickness=new(1),Child=Stack(T("조우 조립",20),Field("타격부",strike),Field("손잡이",grip),Field("연결부",link),label,note,select)};
        var body=panel.Child;panel.Child=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        panel.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");panel.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");popup.Child=panel;PopupLayout.Configure(popup,this);popup.IsOpen=show;return panel;
    }
}
