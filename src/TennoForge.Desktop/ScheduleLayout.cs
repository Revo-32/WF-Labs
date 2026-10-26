using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    private readonly TextBlock TenetClock=Text("—",27),CodaClock=Text("—",27),CodaPhase=Text("판매 회차 교체까지",13,"MutedBrush");
    internal readonly ScrollViewer DashboardScroller=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Stretch};
    private (Expander Arbitration,Expander Weapons) BuildScheduleLayout()
    {
        Margin=new(8,12,8,0);
        for(int i=0;i<4;i++)RowDefinitions.Add(new(){Height=i==2?new(1,GridUnitType.Star):GridLength.Auto});
        var header=new DockPanel{Margin=new(0,0,0,20)};RefreshButton.Margin=new(12,0,0,0);DockPanel.SetDock(RefreshButton,Dock.Right);header.Children.Add(RefreshButton);FeaturesButton=Button("오버레이 · 알림",OpenFeatures);FeaturesButton.Margin=new(12,0,0,0);DockPanel.SetDock(FeaturesButton,Dock.Right);header.Children.Add(FeaturesButton);header.Children.Add(Text("일정",24));Children.Add(header);
        var overview=new Grid();overview.RowDefinitions.Add(new(){Height=GridLength.Auto});overview.RowDefinitions.Add(new(){Height=GridLength.Auto});overview.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});Grid.SetRow(ScheduleContent,2);Children.Add(ScheduleContent);ScheduleContent.Content=overview;
        var highlights=new Grid{Margin=new(0,0,0,24)};
        NextTitle.FontSize=14;NextCountdown.FontSize=34;NextNode.FontSize=16;Ongoing.FontSize=13;
        var nextBody=new Grid();nextBody.ColumnDefinitions.Add(new(){Width=new(230)});nextBody.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var time=new StackPanel();time.Children.Add(NextTitle);NextCountdown.Margin=new(0,8,0,0);time.Children.Add(NextCountdown);nextBody.Children.Add(time);NextTitle.Margin=new(0);
        var node=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new(24,0,0,0)};node.Children.Add(NextNode);Ongoing.Margin=new(0,8,0,0);node.Children.Add(Ongoing);Grid.SetColumn(node,1);nextBody.Children.Add(node);
        highlights.Children.Add(Surface(nextBody,new(20),new()));
        var clocks=new Grid();clocks.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});clocks.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var tenet=new StackPanel();tenet.Children.Add(Text("테넷 · 보너스 교체까지",13,"MutedBrush"));TenetClock.Margin=new(0,4,0,0);tenet.Children.Add(TenetClock);clocks.Children.Add(tenet);
        var coda=new StackPanel{Margin=new(24,0,0,0)};coda.Children.Add(CodaPhase);CodaClock.Margin=new(0,4,0,0);coda.Children.Add(CodaClock);Grid.SetColumn(coda,1);clocks.Children.Add(coda);var shopsCard=Surface(clocks,new(12),new());overview.Children.Add(highlights);
        var cycles=new Grid{Margin=new(0,0,0,24)};int column=0;
        foreach(var def in WorldScheduleEngine.Definitions)
        {
            cycles.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});var cell=new StackPanel();
            var title=new DockPanel();var source=Text("",11,"MutedBrush");DockPanel.SetDock(source,Dock.Right);title.Children.Add(source);var name=Text(def.Name,14,"MutedBrush");name.ToolTip=def.Detail;title.Children.Add(name);cell.Children.Add(title);
            var state=Text("불러오기 전",20);state.Margin=new(0,10,0,0);cell.Children.Add(state);var clock=Text("—",27);clock.Margin=new(0,4,0,8);cell.Children.Add(clock);
            var progress=new ProgressBar{Height=3,Minimum=0,Maximum=1,Margin=new(0,0,0,10)};progress.SetResourceReference(Control.BackgroundProperty,"StrokeBrush");progress.SetResourceReference(Control.ForegroundProperty,"AccentBrush");cell.Children.Add(progress);var next=Text("",12,"MutedBrush");cell.Children.Add(next);
            var box=new Border{Child=cell,Padding=new(0,0,column==4?0:24,0)};Grid.SetColumn(box,column++);cycles.Children.Add(box);cycleViews[def.Id]=(state,clock,next,progress,source);
        }
        Grid.SetRow(cycles,1);overview.Children.Add(cycles);
        var body=new Grid();
        var arby=new Grid();for(int i=0;i<5;i++)arby.RowDefinitions.Add(new(){Height=i==3?new(1,GridUnitType.Star):GridLength.Auto});arby.Children.Add(Text("중재",21));
        var filters=new WrapPanel{Margin=new(0,12,0,12)};
        foreach(var field in new[]{Field("등급",Tier),Field("기간",Horizon),Field("미션",Mission),Field("팩션",Faction)})filters.Children.Add(field);ResetFilters.Margin=new(0,0,0,0);ResetFilters.VerticalAlignment=VerticalAlignment.Bottom;filters.Children.Add(ResetFilters);Grid.SetRow(filters,1);arby.Children.Add(filters);
        currentArbitration.FontSize=13;currentArbitration.Margin=new(0,0,0,12);Grid.SetRow(currentArbitration,2);arby.Children.Add(currentArbitration);Grid.SetRow(ArbitrationList,3);arby.Children.Add(ArbitrationList);
        emptyArbitration.Margin=new(12,12,12,0);emptyArbitration.VerticalAlignment=VerticalAlignment.Top;emptyArbitration.IsHitTestVisible=false;Grid.SetRow(emptyArbitration,3);Panel.SetZIndex(emptyArbitration,1);arby.Children.Add(emptyArbitration);
        var arbyExpanded=new Expander{Header="선택한 중재 · 상세·출처",Content=new ScrollViewer{Content=ArbitrationDetail,MaxHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,12,12,0)},Margin=new(0,12,0,0)};Grid.SetRow(arbyExpanded,4);arby.Children.Add(arbyExpanded);body.Children.Add(arby);
        var weapons=new Grid();for(int i=0;i<5;i++)weapons.RowDefinitions.Add(new(){Height=i==3?new(1,GridUnitType.Star):GridLength.Auto});
        var weaponFilters=new WrapPanel{Margin=new(0,12,0,12)};Shop.MinWidth=200;weaponFilters.Children.Add(Field("상점",Shop));WishOnly.Margin=new(12,0,0,0);WishOnly.VerticalAlignment=VerticalAlignment.Bottom;weaponFilters.Children.Add(WishOnly);Grid.SetRow(weaponFilters,1);weapons.Children.Add(weaponFilters);
        reportStatus.FontSize=12;reportStatus.Margin=new(0,0,0,12);Grid.SetRow(reportStatus,2);weapons.Children.Add(reportStatus);Grid.SetRow(WeaponList,3);weapons.Children.Add(WeaponList);
        var weaponExpanded=new Expander{Header="선택한 무기 · 찜하기·모딩",Content=new ScrollViewer{Content=WeaponDetail,MaxHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,12,12,0)},Margin=new(0,12,0,0)};Grid.SetRow(weaponExpanded,4);weapons.Children.Add(weaponExpanded);
        var weaponPage=CreateWeaponRotationView(shopsCard,weapons);var tabs=CreateScheduleTabs(overview,weaponPage);Grid.SetRow(tabs,1);Children.Add(tabs);
        DashboardScroller.Content=body;Grid.SetRow(DashboardScroller,2);overview.Children.Add(DashboardScroller);
        DashboardScroller.SizeChanged+=(_,_)=>
        {
            bool compact=DashboardScroller.ActualHeight is >0 and <300;body.Height=compact?360:double.NaN;DashboardScroller.VerticalScrollBarVisibility=compact?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        };
        var footer=new Grid{Margin=new(0,16,0,0)};footer.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Status.FontSize=12;Status.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(Status);var sources=(Expander)Sources();sources.Margin=new(24,0,0,0);Grid.SetColumn(sources,1);footer.Children.Add(sources);Grid.SetRow(footer,3);Children.Add(footer);
        sources.Expanded+=(_,_)=>{arbyExpanded.IsExpanded=false;weaponExpanded.IsExpanded=false;};arbyExpanded.Expanded+=(_,_)=>sources.IsExpanded=false;weaponExpanded.Expanded+=(_,_)=>sources.IsExpanded=false;
        SizeChanged+=(_,_)=>
        {
            bool compact=ActualHeight<650;header.Margin=new(0,0,0,compact?12:20);highlights.Margin=new(0,0,0,compact?16:24);cycles.Margin=new(0,0,0,compact?16:24);footer.Margin=new(0,compact?8:16,0,0);
        };
        return(arbyExpanded,weaponExpanded);
    }
    private static FrameworkElement Field(string title,FrameworkElement input)
    {var panel=new StackPanel{Margin=new(0,0,8,0)};panel.Children.Add(Text(title,12,"MutedBrush"));input.Height=34;panel.Children.Add(input);return panel;}
    private static Border Surface(UIElement child,Thickness padding,Thickness margin)
    {var panel=new Border{Child=child,Padding=padding,Margin=margin,CornerRadius=new(2)};panel.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");return panel;}
}
