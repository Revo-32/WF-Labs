using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed class ModBoard
{
    private const string DragFormat = "WFLabs.ModCard.v1";
    private readonly Catalog catalog;
    private readonly UniformGrid rack = new() { Columns=4,Rows=2 };
    private readonly WrapPanel library = new();
    private readonly ScrollViewer libraryScroller = new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly TextBox search = new();
    private readonly ComboBox sort = new() { ItemsSource=new[]{"이름순","코스트순","극성순"},SelectedIndex=0,Width=108 };
    private readonly DockPanel rankEditor = new();
    private bool descending;
    private readonly Button direction;
    private readonly System.Windows.Shapes.Path sortArrow=new(){Width=18,Height=18,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Stretch=Stretch.None};
    private readonly ComboBox filter = new() { ItemsSource=new[]{"전체 모드","피해","치명타","원소","공격 속도","상태이상"},SelectedIndex=0,Width=108 };
    private readonly TextBlock count=Text("",12,"MutedBrush"), message=Text("",12,"MutedBrush");
    private ModSlot?[] slots = new ModSlot?[8];
    private string[] slotPolarities=Enumerable.Repeat("None",8).ToArray();
    private bool formaEditing;
    private int selected;
    private string? weaponId;
    public void SetWeapon(string id){weaponId=id;RenderLibrary();}
    public event Action? Changed;
    public event Action? EditRiven;
    private RivenRoll? riven;
    public void SetRiven(RivenRoll? value){riven=value;RenderRack();}
    private bool IsRiven(Mod mod)=>MeleeRuleCatalog.Load().Mods.SingleOrDefault(m=>m.Id==mod.Id)?.Effects?.ContainsKey("Riven")==true;
    private string RivenDescription()=>riven is null?"리벤 탭에서 옵션 입력":string.Join("\n",riven.Traits.Select(t=>$"{RivenEngine.Label(t.Stat)} {(t.Negative?"−":"+")}{t.Value:0.###}{RivenValues.Unit(t.Stat)}"));
    public event Action<Mod?>? HoveredModChanged;
    public FrameworkElement RackView { get; }
    public FrameworkElement LibraryView { get; }
    public ModSlot?[] Slots => (ModSlot?[])slots.Clone();
    public string[] SlotPolarities=>(string[])slotPolarities.Clone();
    public void SetSlotPolarities(string[] value,bool notify=true)
    {if(value.Length!=8||value.Any(p=>!CapacityEngine.OrdinaryPolarities.Contains(p)))throw new ArgumentException("일반 슬롯 극성 설정 오류");slotPolarities=(string[])value.Clone();RenderRack();if(notify)Changed?.Invoke();}
    public void SetFormaEditing(bool enabled){formaEditing=enabled;RenderRack();}
    public void ClearMods()=>SetSlots(new ModSlot?[8]);
    public int VisibleLibraryCount { get; private set; }
    public string Message => message.Text;
    public string[] VisibleModIds { get; private set; } = [];
    internal FrameworkElement LibraryItemForCheck(string id)=>(FrameworkElement)library.Children[Array.IndexOf(VisibleModIds,id)];
    internal void HoverSlotForCheck(int index,bool enter)=>((FrameworkElement)rack.Children[index]).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=enter?Mouse.MouseEnterEvent:Mouse.MouseLeaveEvent});
    public ModBoard(Catalog catalog)
    {
        this.catalog=catalog;
        var messageStyle=new Style(typeof(TextBlock),(Style)Application.Current.Resources[typeof(TextBlock)]);messageStyle.Triggers.Add(new Trigger{Property=TextBlock.TextProperty,Value="",Setters={new Setter(UIElement.VisibilityProperty,Visibility.Collapsed)}});message.Style=messageStyle;
        var rackPanel=new StackPanel();rackPanel.Children.Add(rack);rackPanel.Children.Add(rankEditor);rackPanel.Children.Add(message);RackView=rackPanel;
        var tray=new Grid();tray.RowDefinitions.Add(new(){Height=GridLength.Auto});tray.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var titlebar=new DockPanel{Margin=new(0,0,0,4)};
        direction=SmallButton("","정렬 방향 전환",()=>{descending=!descending;RenderLibrary();});
        sortArrow.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"TextBrush");direction.Content=sortArrow;
        direction.Width=32;direction.Height=32;
        DockPanel.SetDock(direction,Dock.Right);titlebar.Children.Add(direction);
        sort.Margin=new(0,0,6,0);sort.Height=sort.MinHeight=32;sort.FontSize=14;sort.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(sort,Dock.Right);titlebar.Children.Add(sort);
        var label=Text("모드 보관함",14);label.Margin=new(0);label.FontWeight=FontWeights.SemiBold;label.VerticalAlignment=VerticalAlignment.Center;titlebar.Children.Add(label);
        var searchbar=new DockPanel{Margin=new(0,0,0,6)};
        filter.Height=filter.MinHeight=32;filter.FontSize=14;search.Height=32;search.FontSize=14;search.Padding=new(10,3,10,3);
        DockPanel.SetDock(filter,Dock.Right);searchbar.Children.Add(filter);
        search.Margin=new(0,0,8,0);search.ToolTip="한국어 또는 영문 모드 이름 검색";
        search.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"모드 검색");
        var searchGrid=new Grid();searchGrid.Children.Add(search);
        var placeholder=Text("모드 검색",14,"MutedBrush");placeholder.Margin=new(12,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;searchGrid.Children.Add(placeholder);
        search.TextChanged+=(_,_)=>placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        searchbar.Children.Add(searchGrid);
        var toolbar=new StackPanel();toolbar.Children.Add(titlebar);toolbar.Children.Add(searchbar);tray.Children.Add(toolbar);
        libraryScroller.Content=library;Grid.SetRow(libraryScroller,1);tray.Children.Add(libraryScroller);
        libraryScroller.PreviewDragOver+=(_,e)=>{var y=e.GetPosition(libraryScroller).Y;if(y<24)libraryScroller.ScrollToVerticalOffset(libraryScroller.VerticalOffset-16);else if(y>libraryScroller.ActualHeight-24)libraryScroller.ScrollToVerticalOffset(libraryScroller.VerticalOffset+16);};
        library.SizeChanged+=(_,_)=>SizeLibraryCards();
        tray.AllowDrop=true;
        tray.DragOver+=(_,e)=>{e.Effects=ReadDrag(e)is{SourceSlot:not null}?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
        tray.Drop+=(_,e)=>{if(ReadDrag(e)is{}payload)ReturnToLibrary(payload);e.Handled=true;};
        var panel=new Grid();panel.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});panel.RowDefinitions.Add(new(){Height=GridLength.Auto});panel.Children.Add(tray);Grid.SetRow(count,1);count.Margin=new(0,4,0,0);panel.Children.Add(count);LibraryView=panel;
        search.TextChanged+=(_,_)=>RenderLibrary();filter.SelectionChanged+=(_,_)=>RenderLibrary();sort.SelectionChanged+=(_,_)=>RenderLibrary();
        RenderRack();RenderLibrary();
    }
    private static TextBlock Text(string text,double size=14,string color="TextBrush")
    {size=Math.Max(13,size);var t=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,LineHeight=size<=14?20:24,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,Margin=new(0,0,0,4)};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
    private static Button SmallButton(string label,string hint,Action clicked)
    {var b=new Button{Content=label,FontSize=12,Padding=new(6,2,6,2),MinWidth=28,Height=28,ToolTip=hint};b.Click+=(_,e)=>{clicked();e.Handled=true;};return b;}
    public void SetSlots(ModSlot?[] value,bool notify=true)
    {
        if(value.Length!=8)throw new ArgumentException("슬롯은 8개여야 합니다.");
        slots=(ModSlot?[])value.Clone();
        message.Text="";
        message.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        RenderRack();RenderLibrary();if(notify)Changed?.Invoke();
    }
    public void ChangeRank(int i,int rank)
    {
        if(slots[i] is not {} slot)return;
        var mod=catalog.Mods.Single(m=>m.Id==slot.ModId);rank=Math.Clamp(rank,0,mod.MaxRank);
        if(IsRiven(mod)){EditRiven?.Invoke();return;}
        slots[i]=slot with{Rank=rank};RenderRack();Changed?.Invoke();
    }
    public bool DropOnSlot(ModDrag payload,int target)
    {
        try {slots=ModEditing.Drop(catalog,slots,payload,target);selected=target;message.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");message.Text="";RenderRack();RenderLibrary();Changed?.Invoke();return true;}
        catch(ArgumentException e){message.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");message.Text=e.Message;return false;}
    }
    public bool ReturnToLibrary(ModDrag payload)
    {
        try {slots=ModEditing.Remove(slots,payload);message.Text="모드를 해제했습니다.";message.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");RenderRack();RenderLibrary();Changed?.Invoke();return true;}
        catch(ArgumentException e){message.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");message.Text=e.Message;return false;}
    }
    private void Equip(Mod mod)
    {var empty=Array.FindIndex(slots,s=>s is null);DropOnSlot(new(mod.Id),empty>=0?empty:selected);}
    private void RenderRack()
    {
        rack.Children.Clear();
        for(var i=0;i<8;i++)
        {
            var index=i;var slot=slots[i];var mod=slot is null?null:catalog.Mods.Single(m=>m.Id==slot.ModId);
            var card=CreateCard(mod,slot?.Rank??0,index);card.Margin=new(0,0,index%4==3?0:6,6);card.AllowDrop=true;card.Focusable=true;
            card.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,$"장착 슬롯 {i+1}: {mod?.Name??"비어 있음"}");
            card.PreviewMouseDown+=(_,e)=>
            {
                selected=index;RenderRankEditor();
                foreach(Border item in rack.Children)RestoreCardBorder(item,false);
                card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");
                if(!OnButton(e.OriginalSource as DependencyObject))card.Focus();
            };
            card.KeyDown+=(_,e)=>{if(e.Key==Key.Delete && slots[index] is {} s){ReturnToLibrary(new(s.ModId,index));e.Handled=true;}};
            card.DragOver+=(_,e)=>
            {
                var payload=ReadDrag(e);e.Effects=DragDropEffects.None;
                if(payload is not null)try{ModEditing.Drop(catalog,slots,payload,index);e.Effects=DragDropEffects.Move;card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");}catch(ArgumentException ex){message.Text=ex.Message;}
                e.Handled=true;
            };
            card.DragLeave+=(_,_)=>RestoreCardBorder(card,index==selected);
            card.Drop+=(_,e)=>{if(ReadDrag(e)is{}payload)DropOnSlot(payload,index);e.Handled=true;};
            if(mod is not null)EnableDrag(card,()=>new(mod.Id,index));
            if(index==selected)card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");
            rack.Children.Add(card);
        }
        RenderRankEditor();
    }
    private void RenderRankEditor()
    {
        rankEditor.Children.Clear();rankEditor.Height=28;rankEditor.Margin=new(0,0,0,2);
        if(slots[selected] is not {} slot){rankEditor.Children.Add(Text($"슬롯 {selected+1} · 비어 있음",12,"MutedBrush"));return;}
        var mod=catalog.Mods.Single(m=>m.Id==slot.ModId);
        var remove=SmallButton("해제","선택한 슬롯 해제",()=>ReturnToLibrary(new(slot.ModId,selected)));DockPanel.SetDock(remove,Dock.Right);rankEditor.Children.Add(remove);
        var plus=SmallButton("+","선택한 모드의 랭크 올리기",()=>ChangeRank(selected,slot.Rank+1));plus.IsEnabled=slot.Rank<mod.MaxRank;DockPanel.SetDock(plus,Dock.Right);rankEditor.Children.Add(plus);
        var value=Text($"{slot.Rank}/{mod.MaxRank}",12,"AccentBrush");value.Margin=new(8,0,8,0);value.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(value,Dock.Right);rankEditor.Children.Add(value);
        var minus=SmallButton("−","선택한 모드의 랭크 내리기",()=>ChangeRank(selected,slot.Rank-1));minus.IsEnabled=slot.Rank>0;DockPanel.SetDock(minus,Dock.Right);rankEditor.Children.Add(minus);
        var label=Text($"{mod.Name} · 랭크",12,"MutedBrush");label.VerticalAlignment=VerticalAlignment.Center;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;rankEditor.Children.Add(label);
    }
    private Border CreateCard(Mod? mod,int rank,int? index,bool libraryCard=false)
    {
        var card=new Border{CornerRadius=new(5),BorderThickness=new(1),Padding=new(8,6,8,6),Height=libraryCard?80:120,Cursor=Cursors.Hand};
        var finish=mod is null?ModFinish.Neutral:ModAppearance.For(mod);card.Tag=finish;
        if(mod is not null)
        {
            card.MouseEnter+=(_,_)=>HoveredModChanged?.Invoke(mod);card.MouseLeave+=(_,_)=>HoveredModChanged?.Invoke(null);
            card.GotKeyboardFocus+=(_,_)=>HoveredModChanged?.Invoke(mod);card.LostKeyboardFocus+=(_,_)=>HoveredModChanged?.Invoke(null);
            card.Unloaded+=(_,_)=>{if(card.IsMouseOver||card.IsKeyboardFocusWithin)HoveredModChanged?.Invoke(null);};
        }
        card.SetResourceReference(Border.BackgroundProperty,mod is null?"EmptyBrush":ModAppearance.BackgroundKey(finish));RestoreCardBorder(card,false);
        if(mod is null)
        {
            var empty=Text("＋",22,"MutedBrush");empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;
            var emptyBody=new Grid();emptyBody.Children.Add(empty);if(formaEditing&&index is {} emptyIndex)emptyBody.Children.Add(PolarityButton(emptyIndex));card.Child=emptyBody;return card;
        }
        var actualRiven=IsRiven(mod)&&riven is not null&&mod.Id==RivenValues.ModId(riven.WeaponId);
        if(actualRiven&&riven!.BaseDrain is not null&&riven.Polarity is not null)mod=CapacityEngine.ResolvedMod(mod,riven);
        var p=new Grid();p.RowDefinitions.Add(new(){Height=GridLength.Auto});p.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var header=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,0,0,2)};
        var drain=index is {} equipped?CapacityEngine.SlotCost(mod,rank,slotPolarities[equipped]):mod.BaseDrain+rank;
        var unknownDrain=IsRiven(mod)&&(!actualRiven||riven!.BaseDrain is null||riven.Polarity is null);
        var cost=Text(unknownDrain?"?":drain.ToString(),12,drain<mod.BaseDrain+rank?"CapacityGoodBrush":drain>mod.BaseDrain+rank?"ErrorBrush":"AccentBrush");cost.Margin=new(0,0,4,0);header.Children.Add(cost);header.Children.Add(PolarityIcon.Create(mod.Polarity));p.Children.Add(header);
        if(formaEditing&&index is {} slotIndex)p.Children.Add(PolarityButton(slotIndex));
        var body=new StackPanel{VerticalAlignment=VerticalAlignment.Top};
        var name=Text(mod.Name,14);name.FontWeight=FontWeights.SemiBold;name.LineHeight=20;name.Height=40;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new(0,0,0,libraryCard?0:4);body.Children.Add(name);
        if(!libraryCard){var effect=Text(IsRiven(mod)?RivenDescription():DescribeCompact(mod,rank),13,"MutedBrush");effect.Margin=new(0);effect.MaxHeight=40;effect.TextTrimming=TextTrimming.CharacterEllipsis;body.Children.Add(effect);}
        Grid.SetRow(body,1);p.Children.Add(body);card.Child=p;
        if(index is {} i)
        {
            var menu=new ContextMenu();var remove=new MenuItem{Header="모드 해제 (Delete)"};remove.Click+=(_,_)=>ReturnToLibrary(new(mod.Id,i));menu.Items.Add(remove);card.ContextMenu=menu;
        }
        var details=Details(mod,rank);
        if(IsRiven(mod)){details.Children.Clear();details.Children.Add(Text("장착 리벤",16));details.Children.Add(Text(RivenDescription(),14));details.Children.Add(SmallButton("리벤 편집","입력한 실제 수치 편집",()=>EditRiven?.Invoke()));}
        if(index is {} slotForTip)details.Children.Add(Text($"슬롯 {slotForTip+1}: {PolarityLabel(slotPolarities[slotForTip])} · 실제 소모 {drain}",12,"MutedBrush"));
        CursorTooltip.Attach(card,details);
        card.SetValue(System.Windows.Automation.AutomationProperties.HelpTextProperty,Describe(mod,rank));
        card.MouseEnter+=(_,_)=>card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");
        card.MouseLeave+=(_,_)=>RestoreCardBorder(card,index==selected||card.IsKeyboardFocused);
        card.GotKeyboardFocus+=(_,_)=>card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");
        card.LostKeyboardFocus+=(_,_)=>RestoreCardBorder(card,index==selected);
        return card;
    }
    private static void RestoreCardBorder(Border card,bool selected)
    {card.SetResourceReference(Border.BorderBrushProperty,selected?"AccentBrush":ModAppearance.StrokeKey(card.Tag is ModFinish finish?finish:ModFinish.Neutral));}
    private Button PolarityButton(int index)
    {
        var line=new StackPanel{Orientation=Orientation.Horizontal};var number=Text($"{index+1}",12);number.Margin=new(0,0,4,0);line.Children.Add(number);
        var symbol=PolarityIcon.Create(slotPolarities[index]);symbol.Width=12;symbol.Height=12;line.Children.Add(symbol);
        var button=new Button{Content=line,Padding=new(4,2,4,2),Height=24,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new(-5,-4,0,0),ToolTip=$"슬롯 {index+1} 극성: {PolarityLabel(slotPolarities[index])}"};
        button.Click+=(_,e)=>
        {
            var menu=new ContextMenu{PlacementTarget=button};
            foreach(var polarity in CapacityEngine.OrdinaryPolarities)
            {var choice=polarity;var item=new MenuItem{Header=(slotPolarities[index]==choice?"✓  ":"    ")+PolarityLabel(choice)};item.Click+=(_,_)=>{slotPolarities[index]=choice;RenderRack();Changed?.Invoke();};menu.Items.Add(item);}
            button.ContextMenu=menu;menu.IsOpen=true;e.Handled=true;
        };
        return button;
    }
    public static string PolarityLabel(string value)=>value switch{"None"=>"극성 없음","Universal"=>"스탠스 포르마","Omni"=>"옴니 · 움브라는 기본 코스트",_=>ModLibrary.PolarityName(value)};
    private static StackPanel Details(Mod mod,int rank)
    {
        var details=new StackPanel{MaxWidth=300};
        var title=Text(mod.Name,16);title.FontWeight=FontWeights.SemiBold;title.Margin=new(0,0,0,8);details.Children.Add(title);
        var isRiven=MeleeRuleCatalog.Load().Mods.SingleOrDefault(m=>m.Id==mod.Id)?.Effects?.ContainsKey("Riven")==true;
        var info=Text(isRiven?"리벤 · 실제 모드의 극성·코스트·옵션 필요":$"{ModAppearance.Label(ModAppearance.For(mod))} · {ModLibrary.PolarityName(mod.Polarity)}\n코스트 {mod.BaseDrain+rank} · 랭크 {rank}/{mod.MaxRank}",13,"MutedBrush");info.LineHeight=22;details.Children.Add(info);
        var effect=Text(Describe(mod,rank),14);effect.LineHeight=23;effect.Margin=new(0,10,0,0);details.Children.Add(effect);
        var issues=MeleeRuleCatalog.Load().Mods.SingleOrDefault(m=>m.Id==mod.Id)?.Unsupported??mod.CalculationIssues;
        if(issues is {Length:>0}){var issue=Text(string.Join(" ",issues),13,"ErrorBrush");issue.LineHeight=22;issue.Margin=new(0,12,0,0);details.Children.Add(issue);}return details;
    }
    private static string DescribeCompact(Mod mod,int rank)
    {
        // The card condenses supported effects; the tooltip retains the official Korean rank text.
        double scale=(rank+1d)/(mod.MaxRank+1d);
        var lines=new List<string>();
        if((MeleeRuleCatalog.Load().Mods.SingleOrDefault(m=>m.Id==mod.Id)?.Unsupported??mod.CalculationIssues) is {Length:>0})return "효과 계산 미지원\n설명은 마우스 오버";
        if(mod.Element is {} element)lines.Add($"{Labels.Damage(element)} +{mod.ElementBonus*scale:P0}");
        foreach(var (stat,value) in mod.Effects)
        {
            var label=stat switch{Stat.Damage=>"피해",Stat.CriticalChance=>"치확",Stat.CriticalDamage=>"치피",Stat.AttackSpeed=>"공속",Stat.StatusChance=>"상확",Stat.BloodRush=>"치확",Stat.WeepingWounds=>"상확",Stat.ConditionOverload=>"피해",_=>null};
            if(label is null)return Describe(mod,rank);
            lines.Add($"{label} +{value*scale:P0}");
            if(stat==Stat.CriticalChance&&Describe(mod,rank).Contains("강 공격 시 2배",StringComparison.Ordinal))lines.Add("강공격 시 2배");
            if(stat is Stat.BloodRush or Stat.WeepingWounds)lines.Add("추가 콤보당");
            if(stat==Stat.ConditionOverload)lines.Add("상태 종류당");
        }
        return lines.Count==0?Describe(mod,rank):string.Join("\n",lines);
    }
    public static string Describe(Mod mod,int rank)
    {
        if(mod.RankText is {} ranks&&rank>=0&&rank<ranks.Length)return string.Join("\n",ranks[rank]);
        double scale=(rank+1d)/(mod.MaxRank+1d);
        string Label(Stat s)=>s switch{Stat.Damage=>"근접 피해",Stat.CriticalChance=>"치명타 확률",Stat.CriticalDamage=>"치명타 피해",Stat.AttackSpeed=>"공격 속도",Stat.StatusChance=>"상태이상 확률",Stat.BloodRush=>"콤보당 치확",Stat.WeepingWounds=>"콤보당 상태 확률",Stat.ConditionOverload=>"상태 종류당 피해",_=>s.ToString()};
        var lines=mod.Effects.Select(e=>$"+{e.Value*scale:P0} {Label(e.Key)}").ToList();
        if(mod.Element is {} element)lines.Insert(0,$"+{mod.ElementBonus*scale:P0} {Labels.Damage(element)}");return string.Join("\n",lines);
    }
    private void RenderLibrary()
    {
        UpdateSortPresentation();
        library.Children.Clear();var q=search.Text.Trim();
        var rules=MeleeRuleCatalog.Load().Mods.ToDictionary(m=>m.Id);
        var compatible=catalog.Mods.Where(m=>!m.Id.Contains("/PvPMods/")&&(weaponId is null||!rules.TryGetValue(m.Id,out var rule)||rule.CompatibleWeapons.Length==0||rule.CompatibleWeapons.Contains(weaponId)||(ExaltedRules.Find(weaponId)?.Family==ExaltedFamily.Clones&&m.Id=="/Lotus/Upgrades/Mods/Melee/WeaponMeleeStealthLethalMod")));
        bool Matches(Mod m)
        {
            var r=rules[m.Id];var keys=(r.Effects?.Keys??Enumerable.Empty<string>()).Concat(r.Conditional?.Keys??Enumerable.Empty<string>()).ToHashSet();
            return filter.SelectedIndex switch{
                1=>keys.Overlaps(["Damage","ConditionOverload","HeavyDamage","SlamDamage","FinisherDamage","ThrowWeaponDamage"])||keys.Any(k=>k.StartsWith("Vs",StringComparison.Ordinal)),
                2=>keys.Overlaps(["CriticalChance","CriticalDamage","BloodRush","SlideCriticalChance","TennokaiCriticalDamage"]),
                3=>r.Element.HasValue||keys.Any(k=>Enum.TryParse<DamageType>(k,out _)),4=>keys.Contains("AttackSpeed"),
                5=>keys.Overlaps(["StatusChance","WeepingWounds","StatusDamage","StatusDuration","TennokaiStatus"]),_=>true};
        }
        var list=compatible.Where(m=>weaponId is null||!rules.TryGetValue(m.Id,out var exaltedRule)||ExaltedRules.ModAllowed(weaponId,exaltedRule)).Where(m=>(m.Name.Contains(q,StringComparison.OrdinalIgnoreCase)||m.EnglishName.Contains(q,StringComparison.OrdinalIgnoreCase))&&Matches(m)).ToArray();
        list=ModLibrary.Sort(list,(ModSort)sort.SelectedIndex,descending).ToArray();
        VisibleModIds=list.Select(m=>m.Id).ToArray();
        foreach(var mod in list)
        {
            var card=CreateCard(mod,mod.MaxRank,null,true);card.Width=144;card.Margin=new(0,0,6,6);card.Focusable=true;
            card.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,mod.Name);
            EnableDrag(card,()=>new(mod.Id));card.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2){Equip(mod);e.Handled=true;}};
            card.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Equip(mod);e.Handled=true;}};
            library.Children.Add(card);
        }
        VisibleLibraryCount=list.Length;count.Text=$"{list.Length}개 · 최대 랭크 코스트 · 효과는 마우스 오버";SizeLibraryCards();
    }
    private void UpdateSortPresentation()
    {
        sortArrow.Data=Geometry.Parse(descending?"M 3,3 L 9,3 M 3,7 L 7,7 M 3,11 L 5,11 M 14,3 L 14,15 M 11,12 L 14,15 17,12":"M 3,3 L 5,3 M 3,7 L 7,7 M 3,11 L 9,11 M 14,15 L 14,3 M 11,6 L 14,3 17,6");
        var state=descending?"내림차순":"오름차순";var next=descending?"오름차순":"내림차순";
        direction.ToolTip=$"현재 {state} · {next}으로 변경";
        direction.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,$"{sort.SelectedItem} {state} · {next}으로 변경");
    }
    private void SizeLibraryCards()
    {
        if(library.ActualWidth<=0)return;
        var columns=Math.Max(1,(int)Math.Floor((library.ActualWidth+6)/125));
        // Round down so WPF layout rounding cannot push the last card to an extra row.
        var width=Math.Floor(library.ActualWidth/columns)-6;
        foreach(Border card in library.Children)card.Width=Math.Max(1,width);
    }
    public void SortForCheck(ModSort mode,bool reverse=false){descending=reverse;sort.SelectedIndex=(int)mode;RenderLibrary();}
    internal object CheckSortDirections()
    {
        var records=new List<object>();
        foreach(var mode in Enum.GetValues<ModSort>())
        {
            SortForCheck(mode);var original=VisibleModIds.ToArray();var up=sortArrow.Data.ToString();
            direction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var down=sortArrow.Data.ToString();
            if(!descending||up==down||!direction.ToolTip.ToString()!.Contains("현재 내림차순")||VisibleModIds.SequenceEqual(original))throw new Exception($"{mode}: 실제 클릭 후 방향 아이콘·설명·목록 불일치");
            if(mode==ModSort.Cost&&catalog.Mods.Single(m=>m.Id==VisibleModIds[0]).BaseDrain+catalog.Mods.Single(m=>m.Id==VisibleModIds[0]).MaxRank!=catalog.Mods.Max(m=>m.BaseDrain+m.MaxRank))throw new Exception("실제 클릭 후 코스트 내림차순 실패");
            direction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(descending||sortArrow.Data.ToString()!=up||!VisibleModIds.SequenceEqual(original)||!direction.ToolTip.ToString()!.Contains("현재 오름차순"))throw new Exception($"{mode}: 두 번째 클릭 후 원래 방향 복원 실패");
            records.Add(new{mode=mode.ToString(),passed=true});
        }
        SortForCheck(ModSort.Name);return records;
    }
    internal FrameworkElement AppearanceGallery()
    {
        var grid=new UniformGrid{Columns=4};
        foreach(var name in new[]{"Pressure Point","Fury","Condition Overload","Primed Pressure Point","Galvanized Steel","Sacrificial Steel","Amalgam Organ Shatter","Melee Riven Mod"})
        {
            var mod=catalog.Mods.Single(m=>m.EnglishName==name);var p=new StackPanel{Margin=new(8)};
            p.Children.Add(Text(ModAppearance.Label(ModAppearance.For(mod)),14));p.Children.Add(CreateCard(mod,mod.MaxRank,null,true));grid.Children.Add(p);
        }
        return grid;
    }
    internal FrameworkElement SortPreviewForCheck(bool reverse)
    {
        SortForCheck(ModSort.Cost,reverse);
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        var box=new ComboBox{ItemsSource=new[]{"코스트순"},SelectedIndex=0,Height=32,MinHeight=32,Width=108,FontSize=14};row.Children.Add(box);
        var icon=new System.Windows.Shapes.Path{Data=sortArrow.Data,Width=18,Height=18,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Stretch=Stretch.None};icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"TextBrush");
        row.Children.Add(new Button{Content=icon,Width=32,Height=32,Padding=new(6,2,6,2),Margin=new(6,0,0,0)});
        return row;
    }
    private static ModDrag? ReadDrag(DragEventArgs e)=>e.Data.GetDataPresent(DragFormat)?e.Data.GetData(DragFormat)as ModDrag:null;
    private static bool OnButton(DependencyObject? d)
    {while(d is not null){if(d is ButtonBase)return true;d=d is FrameworkContentElement content?content.Parent:VisualTreeHelper.GetParent(d);}return false;}
    private void EnableDrag(FrameworkElement card,Func<ModDrag> payload)
    {
        Point? start=null;
        card.PreviewMouseLeftButtonDown+=(_,e)=>{start=OnButton(e.OriginalSource as DependencyObject)?null:e.GetPosition(card);if(start is not null)card.CaptureMouse();};
        card.PreviewMouseLeftButtonUp+=(_,_)=>{start=null;if(Mouse.Captured==card)card.ReleaseMouseCapture();};
        card.LostMouseCapture+=(_,_)=>start=null;
        card.PreviewMouseMove+=(_,e)=>
        {
            if(e.LeftButton!=MouseButtonState.Pressed){start=null;if(Mouse.Captured==card)card.ReleaseMouseCapture();return;}if(start is not {} origin)return;
            var point=e.GetPosition(card);
            if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            start=null;
            BeginPointerDrag(card,payload());
            e.Handled=true;
        };
    }
    private PointerDrag? pointerDrag;
    internal bool PointerActiveForCheck=>pointerDrag is not null;
    internal bool PointerVisibleForCheck=>pointerDrag?.PreviewVisible==true;
    internal object CheckPointerDragFlow(FrameworkElement ancestor)
    {
        var saved=Slots;var checks=new List<string>();
        void Yes(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
        try
        {
            SetSlots(new ModSlot?[8]);ancestor.UpdateLayout();
            var source=(FrameworkElement)library.Children[0];var modId=VisibleModIds[0];
            BeginPointerDrag(source,new(modId));var session=pointerDrag??throw new Exception("드래그 시작 실패");
            var target=(FrameworkElement)rack.Children[0];session.Update(target.TranslatePoint(new Point(target.ActualWidth/2,target.ActualHeight/2),session.Host));
            Yes(session.PreviewVisible,"보관함 → 슬롯: 반투명 카드 표시");session.Finish();Yes(slots[0]?.ModId==modId,"보관함 → 슬롯: 장착");ancestor.UpdateLayout();
            source=(FrameworkElement)rack.Children[0];BeginPointerDrag(source,new(modId,0));session=pointerDrag!;target=(FrameworkElement)rack.Children[1];session.Update(target.TranslatePoint(new Point(target.ActualWidth/2,target.ActualHeight/2),session.Host));session.Finish();
            Yes(slots[0] is null&&slots[1]?.ModId==modId,"슬롯 → 슬롯: 이동");ancestor.UpdateLayout();
            source=(FrameworkElement)rack.Children[1];BeginPointerDrag(source,new(modId,1));session=pointerDrag!;session.Update(LibraryView.TranslatePoint(new Point(40,100),session.Host));
            Yes(session.PreviewVisible&&count.Text.Contains("모드 해제"),"슬롯 → 보관함: 카드 표시·해제 영역 강조 · "+session.Diagnostics);session.Finish();Yes(slots.All(s=>s is null),"슬롯 → 보관함: 드래그 해제");ancestor.UpdateLayout();
            source=(FrameworkElement)library.Children[0];BeginPointerDrag(source,new(VisibleModIds[0]));session=pointerDrag!;session.Update(new(-10,-10));session.Finish();Yes(slots.All(s=>s is null),"유효하지 않은 위치에 놓기: 상태 보존");
            SetSlots(saved);ancestor.UpdateLayout();source=(FrameworkElement)rack.Children[0];if(slots[0] is {} item){BeginPointerDrag(source,new(item.ModId,0));pointerDrag!.Update(LibraryView.TranslatePoint(new Point(40,100),pointerDrag.Host));pointerDrag.Dispose();Yes(slots.SequenceEqual(saved)&&source.Opacity==1,"취소: 슬롯·원본 투명도 복원");}
            return new{passed=true,checks};
        }
        finally{pointerDrag?.Dispose();SetSlots(saved);}
    }
    internal void RemovalPreviewForCheck(bool show)
    {
        pointerDrag?.Dispose();if(!show||slots[0] is not {} item)return;
        BeginPointerDrag((FrameworkElement)rack.Children[0],new(item.ModId,0));
        pointerDrag!.Update(LibraryView.TranslatePoint(new Point(LibraryView.ActualWidth*.48,120),pointerDrag.Host));
    }
    private void BeginPointerDrag(FrameworkElement source,ModDrag payload)
    {
        pointerDrag?.Dispose();
        if(Window.GetWindow(source)?.Content is not FrameworkElement shell)return;
        pointerDrag=new PointerDrag(this,source,shell,payload);
    }
    // In-app dragging stays on the normal WPF input/render path. OLE's modal
    // drag loop no longer owns the pointer or postpones the card preview frame.
    private sealed class PointerDrag : IDisposable
    {
        private readonly ModBoard board;private readonly FrameworkElement host;
        private readonly Window? owner;
        private readonly ModDrag payload;private readonly ModDragPreview? preview;
        private bool disposed;private int? target;private bool remove;
        internal FrameworkElement Host=>host;
        internal bool PreviewVisible=>preview is {Visibility:Visibility.Visible,VisibleSnapshotPixels:>1000};
        internal string Diagnostics=>$"active={!disposed}, pixels={preview?.VisibleSnapshotPixels}, visible={preview?.Visibility}, remove={remove}, slot={target}, library={board.LibraryView.RenderSize}";
        internal PointerDrag(ModBoard board,FrameworkElement source,FrameworkElement host,ModDrag payload)
        {
            this.board=board;this.host=host;this.payload=payload;
            owner=Window.GetWindow(source);if(owner is not null){owner.Deactivated+=Deactivate;owner.Closed+=Deactivate;}
            var mod=board.catalog.Mods.Single(m=>m.Id==payload.ModId);var rank=payload.SourceSlot is {} i?board.slots[i]!.Rank:mod.MaxRank;
            preview=ModDragPreview.Start(source,board.CreateCard(mod,rank,payload.SourceSlot,payload.SourceSlot is null));
            if(source.ToolTip is ToolTip tip)tip.IsOpen=false;
            host.PreviewMouseMove+=Move;host.PreviewMouseLeftButtonUp+=Up;host.PreviewKeyDown+=Key;
            host.LostMouseCapture+=Lost;Mouse.Capture(host,CaptureMode.Element);host.Focus();
            Update(Mouse.GetPosition(host));
        }
        private void Move(object sender,MouseEventArgs e){Update(e.GetPosition(host));e.Handled=true;}
        private void Up(object sender,MouseButtonEventArgs e){Update(e.GetPosition(host));Finish();e.Handled=true;}
        private void Key(object sender,KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){Dispose();e.Handled=true;}}
        private void Lost(object sender,MouseEventArgs e){if(Mouse.Captured!=host)Dispose();}
        private void Deactivate(object? sender,EventArgs e)=>Dispose();
        internal void Update(Point point)
        {
            if(disposed)return;
            preview?.MoveFrom(host,point);target=null;remove=false;
            for(var i=0;i<board.rack.Children.Count;i++)
            {
                var card=(Border)board.rack.Children[i];var inside=new Rect(card.RenderSize).Contains(host.TranslatePoint(point,card));
                RestoreCardBorder(card,i==board.selected);
                if(inside)try{ModEditing.Drop(board.catalog,board.slots,payload,i);target=i;card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");}catch(ArgumentException){}
            }
            if(target is null&&payload.SourceSlot is not null)
                remove=new Rect(board.LibraryView.RenderSize).Contains(host.TranslatePoint(point,board.LibraryView));
            if(board.LibraryView is Panel panel)panel.Background=remove?new SolidColorBrush(Color.FromArgb(36,229,195,84)):null;
            board.count.Text=remove?"여기에 놓아 모드 해제":$"{board.VisibleLibraryCount}개 · 최대 랭크 코스트 · 효과는 마우스 오버";
            host.Cursor=target is not null||remove?Cursors.Hand:Cursors.No;
        }
        internal void Finish()
        {
            if(disposed)return;var slot=target;var returnToLibrary=remove;Dispose();
            if(slot is {} i)board.DropOnSlot(payload,i);else if(returnToLibrary)board.ReturnToLibrary(payload);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            host.PreviewMouseMove-=Move;host.PreviewMouseLeftButtonUp-=Up;host.PreviewKeyDown-=Key;host.LostMouseCapture-=Lost;
            if(owner is not null){owner.Deactivated-=Deactivate;owner.Closed-=Deactivate;}
            if(Mouse.Captured==host)Mouse.Capture(null);host.ClearValue(FrameworkElement.CursorProperty);preview?.Dispose();
            if(board.LibraryView is Panel panel)panel.Background=null;
            board.count.Text=$"{board.VisibleLibraryCount}개 · 최대 랭크 코스트 · 효과는 마우스 오버";
            for(var i=0;i<board.rack.Children.Count;i++)RestoreCardBorder((Border)board.rack.Children[i],i==board.selected);
            if(board.pointerDrag==this)board.pointerDrag=null;
        }
    }
    public void SearchForCheck(string query){search.Text=query;}
    public FrameworkElement CardForCheck()=>CreateCard(catalog.Mods.Single(m=>m.EnglishName=="Pressure Point"),5,null);
    public FrameworkElement LibraryCardForCheck()=>CreateCard(catalog.Mods.Single(m=>m.EnglishName=="Condition Overload"),5,null,true);
    public FrameworkElement DetailsForCheck()=>Details(catalog.Mods.Single(m=>m.EnglishName=="Condition Overload"),5);
    internal FrameworkElement SlotForCheck(int index)=>(FrameworkElement)rack.Children[index];
    internal void ResetLibraryScroll()=>libraryScroller.ScrollToTop();
    internal void CheckIndependentLibraryScroll(FrameworkElement ancestor)
    {
        var before=RackView.TranslatePoint(new Point(),ancestor);
        libraryScroller.ScrollToBottom();libraryScroller.UpdateLayout();
        if(libraryScroller.VerticalOffset<=0)throw new Exception("보관함 내부 스크롤 이동 실패");
        var after=RackView.TranslatePoint(new Point(),ancestor);
        if((after-before).Length>.1)throw new Exception("보관함 스크롤이 장착 슬롯을 이동시킵니다.");
    }
    internal record LibraryGeometry(double ViewportTop,double ViewportBottom,double ViewportHeight,int Columns,int FullyVisibleCards,int FullyVisibleRows,double RackBottom,double ScrollThumbHeight);
    internal LibraryGeometry MeasureLibrary(FrameworkElement ancestor)
    {
        var origin=libraryScroller.TranslatePoint(new Point(),ancestor);
        var viewport=new Rect(origin,new Size(libraryScroller.ViewportWidth,libraryScroller.ViewportHeight));
        viewport.Intersect(new Rect(0,0,ancestor.ActualWidth,ancestor.ActualHeight));
        var cards=library.Children.Cast<Border>().Select(c=>new Rect(c.TranslatePoint(new Point(),ancestor),c.RenderSize)).ToArray();
        var visible=cards.Where(r=>r.Top>=viewport.Top-.1&&r.Bottom<=viewport.Bottom+.1&&r.Left>=viewport.Left-.1&&r.Right<=viewport.Right+.1).ToArray();
        var columns=cards.Length==0?0:cards.Count(r=>Math.Abs(r.Top-cards[0].Top)<.1);
        var rows=cards.GroupBy(r=>Math.Round(r.Top,1)).Count(g=>g.All(visible.Contains));
        var scrollbar=(ScrollBar)libraryScroller.Template.FindName("PART_VerticalScrollBar",libraryScroller);
        var track=(Track)scrollbar.Template.FindName("PART_Track",scrollbar);
        var thumbHeight=LayoutInformation.GetLayoutClip(track.Thumb)?.Bounds.Height??track.Thumb.ActualHeight;
        return new(viewport.Top,viewport.Bottom,viewport.Height,columns,visible.Length,rows,RackView.TranslatePoint(new Point(0,RackView.ActualHeight),ancestor).Y,thumbHeight);
    }
}
