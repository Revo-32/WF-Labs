using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private const double EquipmentSlotHeight=84;
    private readonly TextBlock exilusSlotName=T("＋",14),meleeSlotName=T("＋",14),exilusSlotRank=T("",13),meleeSlotRank=T("",13);
    private Button? exilusSlotButton,meleeSlotButton,zawSlotButton;
    private FrameworkElement? equipmentSlots;
    private Popup? equipmentPicker;
    private Panel? pickerRankHost;
    private FrameworkElement CreateEquipmentSlots()
    {
        var slots=new UniformGrid{Columns=3,Margin=new(0,8,0,0)};
        Button Slot(string title,TextBlock name,TextBlock detail,Action open)
        {
            var label=T(title,13,"#596B85");label.Margin=new(0);
            name.TextWrapping=TextWrapping.Wrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Height=40;name.Margin=new(0);
            detail.Margin=new(0);detail.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
            var band=new DockPanel{LastChildFill=true};DockPanel.SetDock(detail,Dock.Right);band.Children.Add(detail);band.Children.Add(label);
            var body=new Grid();body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});body.Children.Add(band);Grid.SetRow(name,1);body.Children.Add(name);
            var button=Button("",open);button.Content=body;button.Padding=new(9,7,9,7);button.Height=EquipmentSlotHeight;button.Margin=new(0,0,6,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            AutomationProperties.SetName(button,title+" 슬롯 · 변경");slots.Children.Add(button);return button;
        }
        exilusSlotButton=Slot("엑실러스",exilusSlotName,exilusSlotRank,()=>OpenEquipmentPicker("exilus"));
        meleeSlotButton=Slot("근접 아케인",meleeSlotName,meleeSlotRank,()=>OpenEquipmentPicker("melee"));
        zawSlotButton=Slot("조우 아케인",zawSlotName,zawSlotRank,()=>OpenEquipmentPicker("zaw"));zawSlotButton.Margin=new(0);
        WatchEquationSource(exilusSlotButton,"mod",()=>(exilusChoice.SelectedItem as ExilusChoice)?.Mod is {} m?(m.Id,m.Name):null);
        WatchEquationSource(meleeSlotButton,"arcane",()=>(arcaneEditors[0].Choice.SelectedItem as ArcaneChoice)?.Definition is {} a?(a.Id,a.Name):null);
        exodiaRank.SelectionChanged+=(_,_)=>{if(selectedExodia is {} e&&exodiaRank.SelectedItem is int rank){selectedExodia=e with {Rank=rank};RefreshEquipmentSlots();Recalculate();}};
        equipmentSlots=slots;RefreshEquipmentSlots();return slots;
    }
    private void RefreshEquipmentSlots()
    {
        var exodia=ReferenceCatalog.Load().Arcanes.FirstOrDefault(a=>a.Id==selectedExodia?.Id);
        zawSlotName.Text=exodia?.Name??"＋ 장착";zawSlotRank.Text=weapon.SelectedItem is Weapon w&&ZawCatalog.IsZaw(w.Id)?(exodia is null?"빈 슬롯":$"랭크 {selectedExodia!.Rank}/{exodia.MaxRank}"):"조우 전용";
        var exalted=weapon.SelectedItem is Weapon selectedWeapon?ExaltedRules.Find(selectedWeapon.Id):null;
        if(exilusSlotButton is not null)exilusSlotButton.IsEnabled=exalted is not {Exilus:false};
        var mod=(exilusChoice.SelectedItem as ExilusChoice)?.Mod;
        var rank=exilusRank.SelectedItem is int r?r:0;
        exilusSlotName.Text=exalted is {Exilus:false}?"슬롯 없음":mod?.Name??"＋ 장착";
        exilusSlotRank.Text=exalted is {Exilus:false}?"어빌리티 전용":mod is null?"빈 슬롯":$"랭크 {rank}/{mod.MaxRank} · {CapacityEngine.SlotCost(mod,rank,exilusPolarity.SelectedValue as string??"None")}";
        var editor=arcaneEditors[0];var arcane=(editor.Choice.SelectedItem as ArcaneChoice)?.Definition;
        meleeSlotName.Text=arcane?.Name??"＋ 장착";
        meleeSlotRank.Text=arcane is null?"빈 슬롯":$"랭크 {editor.Rank.SelectedItem}/{arcane.MaxRank}";
        if(exilusSlotButton is not null)exilusSlotButton.ToolTip=mod is null?"엑실러스 모드 장착":ModBoard.Describe(mod,rank);
        if(meleeSlotButton is not null)meleeSlotButton.ToolTip=arcane is null?"근접 아케인 장착":arcane.Describe(editor.Rank.SelectedItem is int ar?ar:0);
    }
    private void CloseEquipmentPicker()
    {
        if(equipmentPicker is {} popup)popup.IsOpen=false;
        DetachPickerInputs();
        pickerRankHost?.Children.Clear();pickerRankHost=null;equipmentPicker=null;
    }
    private void DetachPickerInputs()
    {foreach(var input in new[]{exilusRank,exilusPolarity,arcaneEditors[0].Rank,exodiaRank})if(input.Parent is Panel parent)parent.Children.Remove(input);}
    private void OpenEquipmentPicker(string kind,bool show=true)
    {
        CloseEquipmentPicker();
        var isExilus=kind=="exilus";var isZaw=kind=="zaw";
        var anchor=isExilus?exilusSlotButton:isZaw?zawSlotButton:meleeSlotButton;
        var panel=new StackPanel();var heading=new DockPanel();
        var close=Button("✕",CloseEquipmentPicker);close.Width=30;close.Height=30;close.Padding=new(0);DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
        heading.Children.Add(T(isExilus?"엑실러스 모드":isZaw?"조우 아케인":"근접 아케인",18));panel.Children.Add(heading);
        var choices=new StackPanel();var search=new TextBox{Height=34,Margin=new(0,4,0,8),ToolTip="이름 검색"};AutomationProperties.SetName(search,"장비 이름 검색");
        panel.Children.Add(new SearchField(search,isExilus?"엑실러스 모드 검색":"아케인 검색"));
        var scroll=new ScrollViewer{Content=choices,Height=264,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(scroll);
        var rankHost=new StackPanel{Margin=new(0,10,0,0)};pickerRankHost=rankHost;
        if(!isZaw)
        {
            var rank=isExilus?exilusRank:arcaneEditors[0].Rank;CompactInput(rank,32);rank.Width=80;
            var row=new DockPanel();var remove=Button("해제",()=>{if(isExilus)exilusChoice.SelectedIndex=0;else arcaneEditors[0].Choice.SelectedIndex=0;CloseEquipmentPicker();});DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var label=T("장착 랭크",14);label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new(0,0,10,0);row.Children.Add(label);row.Children.Add(rank);rankHost.Children.Add(row);
            row.Visibility=(isExilus?(exilusChoice.SelectedItem as ExilusChoice)?.Mod is not null:(arcaneEditors[0].Choice.SelectedItem as ArcaneChoice)?.Definition is not null)?Visibility.Visible:Visibility.Collapsed;
            if(isExilus){CompactInput(exilusPolarity,32);rankHost.Children.Add(Field("엑실러스 슬롯 극성",exilusPolarity));}
        }
        else
        {
            var note=T("조우 전용 슬롯입니다. 일부 투사체·범위 효과는 개별 지원 범위를 확인하세요.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;rankHost.Children.Add(note);
            if(selectedExodia is {} selected){exodiaRank.ItemsSource=Enumerable.Range(0,ReferenceCatalog.Load().Arcanes.Single(a=>a.Id==selected.Id).MaxRank+1);exodiaRank.SelectedItem=selected.Rank;rankHost.Children.Add(Field("장착 랭크",exodiaRank));rankHost.Children.Add(Button("해제",()=>{selectedExodia=null;RefreshEquipmentSlots();CloseEquipmentPicker();Recalculate();}));}
        }
        panel.Children.Add(rankHost);
        void Fill()
        {
            choices.Children.Clear();var query=search.Text.Trim();
            void Add(string name,string detail,bool selected,Action equip,bool enabled=true)
            {
                var title=T((selected?"✓  ":"")+name,14);title.Margin=new(0,0,0,4);
                var description=T(detail,13,"#596B85");description.TextWrapping=TextWrapping.Wrap;description.Margin=new(0);description.MaxHeight=60;description.TextTrimming=TextTrimming.CharacterEllipsis;
                var button=Button("",equip);button.Content=Stack(title,description);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new(10,8,10,8);button.Margin=new(0,0,0,5);button.IsEnabled=enabled;CursorTooltip.Attach(button,name+"\n"+detail);choices.Children.Add(button);
            }
            if(isExilus)
            {
                foreach(var item in exilusChoice.Items.Cast<ExilusChoice>().Where(c=>c.Mod is {} m&&(!(weapon.SelectedItem is Weapon w)||ExaltedRules.ModAllowed(w.Id,MeleeRuleCatalog.Load().Mods.Single(r=>r.Id==m.Id)))&&(m.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||m.EnglishName.Contains(query,StringComparison.OrdinalIgnoreCase))))
                {var m=item.Mod!;Add(m.Name,ModBoard.Describe(m,m.MaxRank),item==exilusChoice.SelectedItem as ExilusChoice,()=>{exilusChoice.SelectedItem=item;CloseEquipmentPicker();});}
            }
            else foreach(var a in ReferenceCatalog.Load().Arcanes.Where(a=>a.Category==(isZaw?"Zaw":"Melee")&&(a.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||a.EnglishName.Contains(query,StringComparison.OrdinalIgnoreCase))).OrderBy(a=>a.Name))
                Add(a.Name,a.Describe(a.MaxRank),(isZaw?selectedExodia?.Id:(arcaneEditors[0].Choice.SelectedItem as ArcaneChoice)?.Definition?.Id)==a.Id,()=>{if(isZaw){selectedExodia=new(a.Id,a.MaxRank);RefreshEquipmentSlots();Recalculate();}else{arcaneEditors[0].Choice.SelectedItem=arcaneEditors[0].Choice.Items.Cast<ArcaneChoice>().Single(c=>c.Definition?.Id==a.Id);if(arcaneExpander is {} conditions)conditions.IsExpanded=true;}CloseEquipmentPicker();},!isZaw||weapon.SelectedItem is Weapon chosen&&ZawCatalog.IsZaw(chosen.Id));
            if(choices.Children.Count==0)choices.Children.Add(T("검색 결과가 없습니다.",14,"#596B85"));
        }
        search.TextChanged+=(_,_)=>Fill();Fill();
        var border=new Border{Child=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},Padding=new(14),Width=420,BorderThickness=new(1)};border.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");border.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");
        var popup=new Popup{Child=border,PlacementTarget=anchor,Placement=PlacementMode.Top,StaysOpen=false,AllowsTransparency=false};equipmentPicker=popup;PopupLayout.Configure(popup,this);
        popup.Closed+=(_,_)=>{DetachPickerInputs();rankHost.Children.Clear();if(equipmentPicker==popup){equipmentPicker=null;pickerRankHost=null;}};
        if(show){popup.IsOpen=true;search.Focus();}
    }
}
