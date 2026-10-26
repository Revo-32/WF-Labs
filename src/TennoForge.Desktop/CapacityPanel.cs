using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private readonly CapacityCatalog capacityCatalog=CapacityCatalog.Load();
    private readonly TextBox weaponRank=new(){Text="30"},masteryRank=new(){Text="30"};
    private readonly CheckBox catalyst=new(){Content="오로킨 카탈리스트",IsChecked=true},stanceEquipped=new(){Content="스탠스 장착 · 최대 랭크",IsChecked=true};
    private record PolarityChoice(string Id,string Name);
    private readonly ComboBox capacityStancePolarity=new(){ItemsSource=CapacityEngine.OrdinaryPolarities.Append("Universal").Select(id=>new PolarityChoice(id,ModBoard.PolarityLabel(id))).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=0};
    private readonly TextBlock capacityValue=T("—",16),capacityHint=T("",12);
    private readonly Grid capacityBar=new(){Height=2,Margin=new(0,2,0,0)};
    private readonly Border capacityFill=new(){CornerRadius=new(2)};
    private Popup? capacityPopup;
    private bool restoringBuild,formaEditing;
    private CapacityResult? capacityResult;

    private FrameworkElement CreateCapacityView()
    {
        var summary=new StackPanel{Orientation=Orientation.Horizontal};var label=T("수용량",12);label.Margin=new(0,0,8,0);label.VerticalAlignment=VerticalAlignment.Center;
        capacityValue.FontSize=14;capacityValue.LineHeight=20;capacityValue.Margin=new(0,0,8,0);capacityValue.VerticalAlignment=VerticalAlignment.Center;capacityHint.Margin=new(0);capacityHint.VerticalAlignment=VerticalAlignment.Center;
        summary.Children.Add(label);summary.Children.Add(capacityValue);summary.Children.Add(capacityHint);var arrow=T("⌄",12);arrow.Margin=new(8,0,0,0);arrow.VerticalAlignment=VerticalAlignment.Center;summary.Children.Add(arrow);
        capacityBar.ColumnDefinitions.Add(new(){Width=new(0,GridUnitType.Star)});capacityBar.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var track=new Border{CornerRadius=new(2)};track.SetResourceReference(Border.BackgroundProperty,"StrokeBrush");Grid.SetColumnSpan(track,2);capacityBar.Children.Add(track);capacityBar.Children.Add(capacityFill);
        weaponRank.ToolTip="선택한 무기의 최대 랭크 이하";masteryRank.ToolTip="최대 30";
        var inputs=Columns(Field("무기 랭크",weaponRank),Field("마스터리 랭크",masteryRank));
        var reset=Button("기본 극성 불러오기",()=>ResetCapacityForWeapon());reset.FontSize=12;reset.Padding=new(10,6,10,6);
        reset.ToolTip="무기에 내장된 극성 개수를 앞 슬롯부터 배치합니다. 게임 내 슬롯 위치와 다르면 포르마 설정에서 맞추세요.";
        var settings=Stack(T("수용량 설정",16),inputs,catalyst,Spacer(8),stanceEquipped,Spacer(12),Field("스탠스 슬롯 극성",capacityStancePolarity),reset);
        var details=Button("",()=>{if(capacityPopup is not null)capacityPopup.IsOpen=!capacityPopup.IsOpen;});details.Content=summary;details.Height=24;details.Padding=new(6,0,6,0);details.HorizontalAlignment=HorizontalAlignment.Left;
        details.ToolTip="수용량 설정: 무기 랭크·카탈리스트·스탠스 극성";details.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"수용량 설정 열기");
        var popupBody=new Border{Child=new ScrollViewer{Content=settings,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=420},Width=380,Padding=new(16),CornerRadius=new(6),BorderThickness=new(1)};
        popupBody.SetResourceReference(Border.BackgroundProperty,"PopupBrush");popupBody.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");
        capacityPopup=new Popup{Child=popupBody,PlacementTarget=details,Placement=PlacementMode.Bottom,VerticalOffset=6,StaysOpen=false,AllowsTransparency=true};
        PopupLayout.Configure(capacityPopup,this);
        popupBody.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){capacityPopup.IsOpen=false;details.Focus();e.Handled=true;}};
        foreach(var input in new[]{weaponRank,masteryRank})input.TextChanged+=(_,_)=>Recalculate();
        catalyst.Checked+=(_,_)=>Recalculate();catalyst.Unchecked+=(_,_)=>Recalculate();
        stanceEquipped.Checked+=(_,_)=>Recalculate();stanceEquipped.Unchecked+=(_,_)=>{if(!initializing)attackMode.SelectedIndex=1;Recalculate();};
        capacityStancePolarity.SelectionChanged+=(_,_)=>Recalculate();
        var source=Button("위키",()=>{if(stance.SelectedItem is Stance s)new StanceWindow(s,stanceCatalog){Owner=this}.ShowDialog();});source.Height=24;source.Padding=new(8,0,8,0);source.FontSize=12;source.ToolTip="선택한 스탠스의 위키 자료";
        var row=new DockPanel();DockPanel.SetDock(source,Dock.Right);row.Children.Add(source);row.Children.Add(details);
        var body=Stack(row,capacityBar);body.Children.Add(capacityPopup);return body;
    }
    private FrameworkElement CreateModActions()
    {
        var buttons=new Grid();
        foreach(var width in new[]{GridLength.Auto,new GridLength(6),GridLength.Auto,new GridLength(6),new GridLength(1,GridUnitType.Star)})buttons.ColumnDefinitions.Add(new(){Width=width});
        var forma=Button("포르마 설정",()=>{});forma.Click+=(_,_)=>{formaEditing=!formaEditing;board.SetFormaEditing(formaEditing);forma.Content=formaEditing?"극성 설정 완료":"포르마 설정";};
        forma.ToolTip="각 슬롯 왼쪽 위에 극성 선택 버튼을 표시합니다. 슬롯의 극성은 모드를 옮겨도 그 자리에 유지됩니다.";
        var clear=Button("8칸 비우기",board.ClearMods);clear.ToolTip="일반 모드 8개를 한 번에 해제합니다. 설정한 슬롯 극성과 스탠스는 유지합니다.";
        foreach(var button in new[]{forma,clear}){button.FontSize=12;button.Padding=new(8,2,8,2);button.Height=30;buttons.Children.Add(button);}
        Grid.SetColumn(clear,2);CompactInput(stance,32);CompactInput(stanceCombo,32);Grid.SetColumn(stance,4);buttons.Children.Add(stance);
        return buttons;
    }
    private CapacitySetup ReadCapacity()=>new(Integer(weaponRank,"무기 랭크"),Integer(masteryRank,"마스터리 랭크"),catalyst.IsChecked==true,stanceEquipped.IsChecked==true,(stance.SelectedItem as Stance)?.Id,(string?)capacityStancePolarity.SelectedValue??"None",board.SlotPolarities);
    private void ApplyCapacity(CapacitySetup settings)
    {
        weaponRank.Text=settings.WeaponRank.ToString();masteryRank.Text=settings.MasteryRank.ToString();catalyst.IsChecked=settings.Catalyst;stanceEquipped.IsChecked=settings.StanceEquipped;
        capacityStancePolarity.SelectedValue=settings.StanceSlotPolarity;board.SetSlotPolarities(settings.SlotPolarities,false);
    }
    private void ResetCapacityForWeapon()
    {
        if(weapon.SelectedItem is not Weapon w)return;
        var defaults=capacityCatalog.Default(w.Id,(stance.SelectedItem as Stance)?.Id);
        board.SetSlotPolarities(defaults.SlotPolarities,false);capacityStancePolarity.SelectedValue=defaults.StanceSlotPolarity;Recalculate();
    }
    private void UpdateCapacity(CapacityResult result)
    {
        capacityResult=result;capacityValue.Text=$"{result.Used} / {result.Total}";
        planningResult.Text=result.Fits?"":"계획용 결과 · 수용량 초과로 실제 장착 불가";
        var key=result.Fits?"CapacityGoodBrush":"ErrorBrush";capacityValue.SetResourceReference(TextBlock.ForegroundProperty,key);capacityFill.SetResourceReference(Border.BackgroundProperty,key);capacityHint.SetResourceReference(TextBlock.ForegroundProperty,key);
        capacityHint.Text=result.Fits?$"{result.Remaining} 남음":$"{-result.Remaining} 초과 · 장착 불가";
        capacityHint.ToolTip=$"기본 {result.BaseCapacity} + 스탠스 {result.StanceBonus}"+(result.Fits?"":" · 계획용 피해 계산");
        var ratio=result.Total>0?Math.Min(1,result.Used/(double)result.Total):result.Used>0?1:0;
        capacityBar.ColumnDefinitions[0].Width=new(ratio,GridUnitType.Star);capacityBar.ColumnDefinitions[1].Width=new(1-ratio,GridUnitType.Star);
    }
    private void ClearCapacity(){capacityResult=null;capacityValue.Text="—";capacityHint.Text="용량 조건을 확인하세요.";capacityBar.ColumnDefinitions[0].Width=new(0,GridUnitType.Star);capacityBar.ColumnDefinitions[1].Width=new(1,GridUnitType.Star);}
}
