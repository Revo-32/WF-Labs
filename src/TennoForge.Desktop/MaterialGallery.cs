using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal abstract record MaterialGalleryRow(MaterialGallery Owner);
internal sealed record MaterialCategoryRow(MaterialGallery Owner,string Name,int Count,int Missing,bool First):MaterialGalleryRow(Owner);
internal sealed record MaterialTilesRow(MaterialGallery Owner,MasteryMaterial[] Materials,int Columns):MaterialGalleryRow(Owner);
internal sealed record MaterialControlRow(MaterialGallery Owner,FrameworkElement Element):MaterialGalleryRow(Owner);
internal sealed record MaterialBlueprintRow(MaterialGallery Owner,MasteryMaterial[] Blueprints,int Columns):MaterialGalleryRow(Owner);

// Virtualize complete horizontal rows rather than putting hundreds of controls
// in an unbounded WrapPanel. Category headings, tiles and optional footer use
// the same pixel-scrolling viewport; no material tile owns a ScrollViewer.
internal sealed class MaterialGallery:ListBox
{
    private MasteryMaterial[] materials=[];
    private MaterialOrder order;
    private FrameworkElement? footer,tail,empty;
    private MasteryMaterial[] blueprints=[];
    private bool showBlueprints;
    private bool resetPending;
    private int generation;
    private Window? host;
    internal int Columns {get;private set;}=4;
    internal MasteryMaterial[] DisplayedMaterials=>Items.OfType<MaterialTilesRow>().SelectMany(r=>r.Materials).ToArray();
    internal MaterialGalleryRow[] Rows=>Items.Cast<MaterialGalleryRow>().ToArray();

    internal MaterialGallery()
    {
        BorderThickness=new(0);Background=Brushes.Transparent;Padding=new(0,0,12,0);
        HorizontalContentAlignment=HorizontalAlignment.Stretch;
        SetResourceReference(ForegroundProperty,"TextBrush");Focusable=false;
        ScrollViewer.SetCanContentScroll(this,true);
        ScrollViewer.SetHorizontalScrollBarVisibility(this,ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(this,ScrollBarVisibility.Auto);
        SetValue(VirtualizingPanel.IsVirtualizingProperty,true);
        SetValue(VirtualizingPanel.VirtualizationModeProperty,VirtualizationMode.Recycling);
        SetValue(VirtualizingPanel.ScrollUnitProperty,ScrollUnit.Pixel);
        SetValue(VirtualizingPanel.CacheLengthProperty,new VirtualizationCacheLength(.5,1));
        SetValue(VirtualizingPanel.CacheLengthUnitProperty,VirtualizationCacheLengthUnit.Page);
        ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        var style=new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FocusableProperty,false));style.Setters.Add(new Setter(PaddingProperty,new Thickness(0)));
        style.Setters.Add(new Setter(MarginProperty,new Thickness(0)));
        style.Setters.Add(new Setter(TemplateProperty,XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><ContentPresenter HorizontalAlignment='Stretch'/></ControlTemplate>")));
        ItemContainerStyle=style;
        ItemTemplate=new DataTemplate{VisualTree=new FrameworkElementFactory(typeof(MaterialGalleryPresenter))};
        KeyboardNavigation.SetTabNavigation(this,KeyboardNavigationMode.Continue);
        PreviewMouseWheel+=(_,e)=>{if(e.OriginalSource is Visual source&&IsAncestorOf(source))CloseDetails();};
        AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((_,e)=>
        {if((e.VerticalChange!=0||e.HorizontalChange!=0)&&e.OriginalSource is Visual source&&IsAncestorOf(source))CloseDetails();}));
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)CloseDetails();};
        Loaded+=(_,_)=>{if(host is not null)return;host=Window.GetWindow(this);if(host is null)return;host.LocationChanged+=HostChanged;host.Deactivated+=HostChanged;};
        Unloaded+=(_,_)=>{CloseDetails();if(host is not null){host.LocationChanged-=HostChanged;host.Deactivated-=HostChanged;host=null;}};
        SizeChanged+=(_,e)=>
        {
            if(e.WidthChanged)CloseDetails();
            int columns=e.NewSize.Width-SystemParameters.VerticalScrollBarWidth>=1360?4:3;
            if(columns==Columns)return;
            var anchor=VisibleAnchor();Columns=columns;Rebuild(false,anchor);
        };
        AutomationProperties.SetName(this,"카테고리별 필요 재료 · 한 페이지 스크롤");
    }

    internal void SetMaterials(MasteryMaterial[] value,MaterialOrder sort,FrameworkElement footer,FrameworkElement tail,FrameworkElement empty,MasteryMaterial[] blueprints,bool showBlueprints)
    {
        bool changed=order!=sort||!materials.SequenceEqual(value)||!ReferenceEquals(this.footer,footer);
        if(!changed&&ReferenceEquals(this.tail,tail)&&ReferenceEquals(this.empty,empty)&&ReferenceEquals(this.blueprints,blueprints)&&this.showBlueprints==showBlueprints)return;
        materials=value;order=sort;this.footer=footer;this.tail=tail;this.empty=empty;this.blueprints=blueprints;this.showBlueprints=showBlueprints;
        Rebuild(changed,"blueprints");
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element,object item)
    {
        base.PrepareContainerForItemOverride(element,item);
        string name=item switch
        {
            MaterialCategoryRow category=>$"{category.Name} · {category.Count:N0}종",
            MaterialTilesRow tiles=>string.Join(" · ",tiles.Materials.Select(m=>m.Name)),
            MaterialBlueprintRow bp=>string.Join(" · ",bp.Blueprints.Select(m=>$"{m.Name} {m.Missing:N0}개")),
            MaterialControlRow control when ReferenceEquals(control.Element,footer)=>"추가로 필요한 설계도",
            MaterialControlRow control when ReferenceEquals(control.Element,tail)=>"계산 기준과 제작비",
            _=>"필요 재료 상태"
        };
        AutomationProperties.SetName(element,name);
    }

    private static int FamiliarOrder(string id)=>id switch
    {
        "/Lotus/Types/Items/Research/EnergyComponent"=>0,
        "/Lotus/Types/Items/Research/BioComponent"=>1,
        "/Lotus/Types/Items/Research/ChemComponent"=>2,
        "/Lotus/Types/Items/MiscItems/Forma"=>3,
        _=>4
    };

    private void Rebuild(bool reset,string? anchor=null)
    {
        resetPending|=reset;
        var rows=new List<MaterialGalleryRow>();bool first=true;
        foreach(var group in materials.GroupBy(m=>m.PreparationGroup).OrderBy(g=>g.Min(m=>m.PreparationPriority)).ThenBy(g=>g.Key,StringComparer.Ordinal))
        {
            rows.Add(new MaterialCategoryRow(this,group.Key,group.Count(),group.Count(m=>m.Missing>0),first));first=false;
            var sorted=MaterialPreparation.Sort(group,order);
            if(order==MaterialOrder.Preparation&&group.Min(m=>m.PreparationPriority)==0)
                sorted=sorted.OrderBy(m=>FamiliarOrder(m.Id)).ToArray();
            for(int offset=0;offset<sorted.Length;offset+=Columns)rows.Add(new MaterialTilesRow(this,sorted.Skip(offset).Take(Columns).ToArray(),Columns));
        }
        if(materials.Length==0&&empty is not null)rows.Add(new MaterialControlRow(this,empty));
        if(footer is not null)rows.Add(new MaterialControlRow(this,footer));
        if(showBlueprints)for(int offset=0;offset<blueprints.Length;offset+=Columns)
            rows.Add(new MaterialBlueprintRow(this,blueprints.Skip(offset).Take(Columns).ToArray(),Columns));
        if(tail is not null)rows.Add(new MaterialControlRow(this,tail));
        ItemsSource=rows.ToArray();int version=++generation;
        Dispatcher.BeginInvoke(()=>
        {
            if(version!=generation)return;
            if(resetPending){resetPending=false;ScrollToTop();return;}
            var target=anchor=="footer"?rows.OfType<MaterialControlRow>().FirstOrDefault(r=>ReferenceEquals(r.Element,tail)):
                anchor=="blueprints"?rows.OfType<MaterialControlRow>().FirstOrDefault(r=>ReferenceEquals(r.Element,footer)):
                rows.FirstOrDefault(r=>r is MaterialTilesRow tiles&&tiles.Materials.Any(m=>m.Id==anchor)||
                    r is MaterialBlueprintRow bp&&bp.Blueprints.Any(m=>"bp:"+m.Id==anchor));
            if(target is not null)ScrollIntoView(target);
        },DispatcherPriority.Loaded);
    }

    private string? VisibleAnchor()
    {
        for(int index=0;index<Items.Count;index++)
        {
            if(ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement element)continue;
            var bounds=element.TransformToAncestor(this).TransformBounds(new Rect(element.RenderSize));
            if(bounds.Bottom<=0||bounds.Top>=ActualHeight)continue;
            if(Items[index] is MaterialTilesRow tiles)return tiles.Materials.FirstOrDefault()?.Id;
            if(Items[index] is MaterialBlueprintRow bp)return "bp:"+bp.Blueprints.First().Id;
            if(Items[index] is MaterialControlRow control&&ReferenceEquals(control.Element,footer))return "blueprints";
            if(Items[index] is MaterialControlRow last&&ReferenceEquals(last.Element,tail))return "footer";
        }
        return null;
    }

    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        if(parent is ScrollViewer scroll)return scroll;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)if(FindScroll(VisualTreeHelper.GetChild(parent,i)) is {} child)return child;
        return null;
    }
    private void ScrollToTop()=>FindScroll(this)?.ScrollToTop();
    private void Navigate(Func<object?> target,bool top=false)
    {
        // A delayed reflow/accordion anchor must not override a later explicit
        // navigation. Re-issue after generation/layout has realized its rows.
        resetPending=top;int version=++generation;
        void Apply(bool settled)
        {
            if(version!=generation)return;
            if(top){ScrollToTop();if(settled)resetPending=false;}
            else if(target() is {} row)ScrollIntoView(row);
        }
        Apply(false);Dispatcher.BeginInvoke(()=>{if(version!=generation)return;UpdateLayout();Apply(true);},DispatcherPriority.Loaded);
    }
    internal void ShowStart()=>Navigate(()=>Items.Count>0?Items[0]:null,true);
    internal void ShowFooter()=>Navigate(()=>Items.OfType<MaterialControlRow>().LastOrDefault());
    internal void ShowMaterial(string id)=>Navigate(()=>Items.OfType<MaterialTilesRow>().FirstOrDefault(r=>r.Materials.Any(m=>m.Id==id)));
    private void HostChanged(object? sender,EventArgs e)=>CloseDetails();
    private static IEnumerable<MaterialTileView> Tiles(DependencyObject element)
    {
        if(element is MaterialTileView tile)yield return tile;
        for(int index=0;index<VisualTreeHelper.GetChildrenCount(element);index++)foreach(var child in Tiles(VisualTreeHelper.GetChild(element,index)))yield return child;
    }
    internal void CloseDetails(){foreach(var tile in Tiles(this))tile.DetailsPopup.IsOpen=false;}

    internal FrameworkElement Present(MaterialGalleryRow row)
    {
        if(row is MaterialControlRow control)
        {
            // Recycling and a 3↔4 column reflow can realize the new footer
            // before the old presenter is released. Retain its controls/state.
            if(LogicalTreeHelper.GetParent(control.Element) is ContentControl old)old.Content=null;
            return control.Element;
        }
        if(row is MaterialCategoryRow category)
        {
            var heading=new StackPanel{Orientation=Orientation.Horizontal};
            heading.Children.Add(Label(category.Name,19));
            var count=Label($"{category.Count:N0}종 · 부족 {category.Missing:N0}종",13,"MutedBrush");count.Margin=new(14,0,0,0);count.VerticalAlignment=VerticalAlignment.Center;heading.Children.Add(count);
            var rule=new Border{Child=heading,BorderThickness=new(0,category.First?0:1,0,0),Padding=new(0,category.First?2:22,0,0),Margin=new(0,0,0,14)};
            rule.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");return rule;
        }
        if(row is MaterialBlueprintRow blueprint)
        {
            var list=new Grid{Margin=new(20,0,0,8)};
            for(int column=0;column<blueprint.Columns;column++)list.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
            for(int column=0;column<blueprint.Blueprints.Length;column++)
            {
                var item=blueprint.Blueprints[column];var label=Label($"{item.Name} ×{item.Missing:N0}",14);label.Margin=new(0,4,20,4);
                Grid.SetColumn(label,column);list.Children.Add(label);
            }
            return list;
        }
        var tiles=(MaterialTilesRow)row;var grid=new Grid{Margin=new(0,0,0,14)};
        for(int column=0;column<tiles.Columns;column++)
        {
            if(column>0)grid.ColumnDefinitions.Add(new(){Width=new(20)});
            grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        }
        for(int column=0;column<tiles.Materials.Length;column++)
        {
            var material=tiles.Materials[column];
            var tile=new MaterialTileView(material,CloseDetails);
            Grid.SetColumn(tile,column*2);grid.Children.Add(tile);
        }
        return grid;
    }

    internal static TextBlock Label(string text,double size,string brush="TextBrush")=>RivenPresentation.Text(text,size,brush);
}

internal sealed class MaterialGalleryPresenter:ContentControl
{
    public MaterialGalleryPresenter()
    {
        HorizontalContentAlignment=HorizontalAlignment.Stretch;Focusable=false;
        DataContextChanged+=(_,_)=>{Content=null;if(DataContext is MaterialGalleryRow row)Content=row.Owner.Present(row);};
    }
}

internal sealed class MaterialTileView:Border
{
    internal MasteryMaterial Material {get;}
    internal Button Details {get;}
    internal Popup DetailsPopup {get;}
    internal MaterialTileView(MasteryMaterial material,Action closeOthers)
    {
        Material=material;Padding=new(12,10,12,8);CornerRadius=new(4);Background=Brushes.Transparent;
        AutomationProperties.SetName(this,material.Name);
        var content=new StackPanel();Child=content;
        var title=new Grid{MinHeight=50};title.ColumnDefinitions.Add(new(){Width=new(62)});title.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        title.Children.Add(new Image{Source=MaterialIcons.Get(material.Id),Width=48,Height=48,Margin=new(0,0,14,0),Stretch=Stretch.Uniform});
        var name=MaterialGallery.Label(material.Name,16);name.FontWeight=FontWeights.SemiBold;name.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(name,1);title.Children.Add(name);content.Children.Add(title);
        var shortage=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,8,0,8)};
        var caption=MaterialGallery.Label("부족",13,"MutedBrush");caption.VerticalAlignment=VerticalAlignment.Center;caption.Margin=new(0,0,10,0);shortage.Children.Add(caption);
        shortage.Children.Add(MaterialGallery.Label($"{material.Missing:N0}",23,material.Missing>0?"AccentBrush":"MutedBrush"));content.Children.Add(shortage);
        var quantities=new Grid();for(int column=0;column<3;column++)quantities.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        int index=0;foreach(var (label,value) in new[]{("총 필요",material.Required),("보유",material.Owned),("제작 중",material.InFoundry)})
        {
            var stack=new StackPanel{Margin=new(0,0,8,0)};stack.Children.Add(MaterialGallery.Label(label,12,"MutedBrush"));
            var number=MaterialGallery.Label($"{value:N0}",15);number.Margin=new(0,3,0,0);stack.Children.Add(number);
            Grid.SetColumn(stack,index++);quantities.Children.Add(stack);
        }
        content.Children.Add(quantities);
        if(material.ProtectedStock>0){var protectedText=MaterialGallery.Label($"장비 보존 {material.ProtectedStock:N0}",12,"MutedBrush");protectedText.Margin=new(0,5,0,0);content.Children.Add(protectedText);}
        Details=new(){Content=$"사용 장비 {material.UsedBy.Length:N0}개 ›",Margin=new(0,8,0,0),FontSize=13,Padding=new(0,4,0,4),MinHeight=30,HorizontalAlignment=HorizontalAlignment.Left,Background=Brushes.Transparent,BorderThickness=new(0)};
        AutomationProperties.SetName(Details,material.Name+" 사용 장비");
        content.Children.Add(Details);
        var detailContent=new StackPanel();var detailHeader=new DockPanel{Margin=new(0,0,0,12)};
        var close=new Button{Content="×",Width=28,Height=28,MinHeight=28,Padding=new(0),Margin=new(12,0,0,0)};
        AutomationProperties.SetName(close,"재료 상세 닫기");
        DockPanel.SetDock(close,Dock.Right);detailHeader.Children.Add(close);detailHeader.Children.Add(MaterialGallery.Label(material.Name,17));detailContent.Children.Add(detailHeader);
        detailContent.Children.Add(MaterialGallery.Label(material.PreparationReason,13,"MutedBrush"));
        var names=MaterialGallery.Label(string.Join(" · ",material.UsedBy),14);names.Margin=new(0,12,0,0);
        detailContent.Children.Add(new ScrollViewer{Content=names,MaxHeight=230,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var surface=new Border{Child=detailContent,Width=440,MaxWidth=480,Padding=new(16),BorderThickness=new(1),CornerRadius=new(6),UseLayoutRounding=true};
        surface.SetResourceReference(Border.BackgroundProperty,"PopupBrush");surface.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");surface.SetResourceReference(System.Windows.Documents.TextElement.FontFamilyProperty,"AppFont");
        DetailsPopup=new(){Child=surface,PlacementTarget=Details,Placement=PlacementMode.Bottom,VerticalOffset=4,StaysOpen=false,AllowsTransparency=true};
        Details.Click+=(_,_)=>{bool open=!DetailsPopup.IsOpen;closeOthers();DetailsPopup.IsOpen=open;};
        close.Click+=(_,_)=>DetailsPopup.IsOpen=false;
        DetailsPopup.Opened+=(_,_)=>TextRendering.Apply(surface,VisualTreeHelper.GetDpi(Details));
        surface.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){DetailsPopup.IsOpen=false;e.Handled=true;if(Window.GetWindow(Details)?.IsActive==true)Details.Focus();}};
        Unloaded+=(_,_)=>DetailsPopup.IsOpen=false;
    }
}
