using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class InventoryPage
{
    internal readonly Grid Dashboard=new(),MasteryArea=new(),RelicArea=new();
    internal readonly MasteryProgressView Progress=new();
    internal readonly TextBlock MasteryCount=Text("",13,"MutedBrush"),RelicGoal=Text("보유 성유물에서 추천",13,"MutedBrush"),RelicCriteria=Text("10 P 이상 보상 확률순",14,"AccentBrush"),RelicEmpty=Text("보유 성유물을 동기화하면 추천이 표시됩니다.",15,"MutedBrush");
    internal readonly ComboBox RelicOrder=new(){ItemsSource=new[]{"가치 있는 보상 확률","플래티넘 기댓값","두캇 기댓값"},SelectedIndex=0};
    internal readonly ComboBox Threshold=new(){ItemsSource=new[]{"5 P 이상","10 P 이상","20 P 이상","50 P 이상"},SelectedIndex=1};
    internal readonly CheckBox AllRefinements=new(){Content="모든 정련도 비교",Margin=new(0,8,0,8)};
    internal Button ClearGoal=null!;
    internal Expander RelicDetails=null!,RelicOptions=null!;
    internal readonly Expander SyncDetails=new(){Header="동기화 정보",Visibility=Visibility.Collapsed};
    private bool dashboardBuilt;
    internal readonly TextBlock RelicInputError=Text("",13,"ErrorBrush");
    private double ValueThreshold=>new[]{5d,10,20,50}[Math.Max(0,Threshold.SelectedIndex)];

    private void BuildDashboard()
    {
        Margin=new(8,12,8,0);RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel{Margin=new(0,0,0,8)};var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};
        var info=ActionButton("동기화 정보",()=>SyncDetails.IsExpanded=!SyncDetails.IsExpanded);
        foreach(var button in new[]{info,SyncButton,ImportButton,ExportButton}){button.Margin=new(8,0,0,0);button.Height=36;actions.Children.Add(button);}DockPanel.SetDock(actions,Dock.Right);header.Children.Add(actions);header.Children.Add(Text("장비",24));Children.Add(header);
        var metadata=new DockPanel();Automatic.Margin=new(20,0,4,0);DockPanel.SetDock(Automatic,Dock.Right);metadata.Children.Add(Automatic);var facts=new StackPanel();facts.Children.Add(summary);facts.Children.Add(priceStatus);metadata.Children.Add(facts);
        SyncDetails.Content=new ScrollViewer{Content=metadata,MaxHeight=80,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};SyncDetails.Margin=new(0,12,0,0);SyncDetails.Expanded+=(_,_)=>SyncDetails.Visibility=Visibility.Visible;SyncDetails.Collapsed+=(_,_)=>SyncDetails.Visibility=Visibility.Collapsed;Grid.SetRow(SyncDetails,3);Children.Add(SyncDetails);
        Status.FontSize=13;Status.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");Status.Margin=new(0,0,0,20);Grid.SetRow(Status,1);Children.Add(Status);
        Dashboard.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetRow(Dashboard,2);Children.Add(Dashboard);
        MasteryArea.Margin=new(0);for(int i=0;i<4;i++)MasteryArea.RowDefinitions.Add(new(){Height=i==3?new(1,GridUnitType.Star):GridLength.Auto});Dashboard.Children.Add(MasteryArea);
        var title=new DockPanel{Margin=new(0,0,0,12)};DockPanel.SetDock(MasteryCount,Dock.Right);title.Children.Add(MasteryCount);MasteryArea.Children.Add(title);
        Progress.Margin=new(0,0,0,20);Progress.FilterRequested=index=>{Ownership.SelectedIndex=index;};Grid.SetRow(Progress,1);MasteryArea.Children.Add(Progress);
        Ownership.ItemsSource=new[]{"마스터리 미완료","보유 · 랭작 필요","전체 장비","마스터리 완료"};Ownership.SelectedIndex=0;
        Search.MinWidth=140;Category.MinWidth=110;Ownership.MinWidth=140;Category.Margin=new(8,0,0,0);Ownership.Margin=new(8,0,0,0);
        var filters=new Grid{Margin=new(0,0,0,16)};filters.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});filters.ColumnDefinitions.Add(new(){Width=GridLength.Auto});filters.ColumnDefinitions.Add(new(){Width=GridLength.Auto});filters.Children.Add(SearchBox(Search,"장비 이름 검색"));Grid.SetColumn(Category,1);filters.Children.Add(Category);Grid.SetColumn(Ownership,2);filters.Children.Add(Ownership);Grid.SetRow(filters,2);MasteryArea.Children.Add(filters);
        var workspace=new Grid();workspace.ColumnDefinitions.Add(new(){Width=new(1.08,GridUnitType.Star)});workspace.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});workspace.Children.Add(MasteryList);
        var detail=new Border{Child=Scroll(MasteryDetail),Padding=new(20,0,0,0),Margin=new(16,0,0,0),BorderThickness=new(1,0,0,0)};detail.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Grid.SetColumn(detail,1);workspace.Children.Add(detail);Grid.SetRow(workspace,3);MasteryArea.Children.Add(workspace);
        MasteryList.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='100'/></Grid.ColumnDefinitions><StackPanel Margin='0,0,12,0'><TextBlock Text='{Binding Name}' FontSize='16' TextWrapping='Wrap'/><TextBlock Text='{Binding State}' FontSize='12' Foreground='{DynamicResource MutedBrush}' TextWrapping='Wrap' Margin='0,5,0,0'/></StackPanel><TextBlock Grid.Column='1' Text='{Binding Value}' FontSize='14' Foreground='{DynamicResource AccentBrush}' TextWrapping='Wrap' TextAlignment='Right' VerticalAlignment='Center'/></Grid></DataTemplate>");
        // RelicArea is hosted exclusively by the relic recommendation subtab.
        for(int i=0;i<5;i++)RelicArea.RowDefinitions.Add(new(){Height=i==3?new(1,GridUnitType.Star):GridLength.Auto});
        var railHeader=new DockPanel{Margin=new(0,0,0,8)};ClearGoal=ActionButton("전체 추천",()=>{goal=null;selectedGoalId=null;RefreshRelics();});ClearGoal.Margin=new(12,0,0,0);ClearGoal.MinHeight=30;ClearGoal.Padding=new(10,4,10,4);DockPanel.SetDock(ClearGoal,Dock.Right);railHeader.Children.Add(ClearGoal);railHeader.Children.Add(Text("성유물 추천",21));RelicArea.Children.Add(railHeader);
        var description=new StackPanel{Margin=new(0,0,0,12)};description.Children.Add(RelicCriteria);description.Children.Add(RelicGoal);Grid.SetRow(description,1);RelicArea.Children.Add(description);
        var options=new StackPanel();options.Children.Add(Field("정렬",RelicOrder));options.Children.Add(Field("가치 기준",Threshold));Squad.MinWidth=0;Squad.Margin=new(0);options.Children.Add(Field("분대",Squad));Traces.Width=double.NaN;Traces.Margin=new(0);options.Children.Add(Field("보이드 잔영",Traces));OnlyAffordable.Margin=new(0,8,0,8);options.Children.Add(OnlyAffordable);options.Children.Add(AllRefinements);
        RelicInputError.Visibility=Visibility.Collapsed;RelicInputError.Margin=new(0,4,0,8);options.Children.Insert(4,RelicInputError);
        options.Children.Add(Text("보상 하나를 선택할 때의 확률·기댓값입니다. 시세 누락 시 ‘최소’ 확률로 표시하며, 동일 성유물·정련도의 분대를 가정합니다.",12,"MutedBrush"));
        RelicOptions=new Expander{Header="추천 조건",Content=new ScrollViewer{Content=options,MaxHeight=225,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},Margin=new(0,0,0,12)};Grid.SetRow(RelicOptions,2);RelicArea.Children.Add(RelicOptions);
        RelicList.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><StackPanel><TextBlock Text='{Binding Name}' FontSize='17' TextWrapping='Wrap'/><TextBlock Text='{Binding State}' FontSize='12' Foreground='{DynamicResource MutedBrush}' TextWrapping='Wrap' Margin='0,6,0,8'/><TextBlock Text='{Binding Value}' FontSize='15' Foreground='{DynamicResource AccentBrush}' TextWrapping='Wrap'/><ProgressBar Maximum='1' Value='{Binding Meter,TargetNullValue=0}' Height='3' Margin='0,8,0,0' Background='{DynamicResource StrokeBrush}' Foreground='{DynamicResource AccentBrush}'><ProgressBar.Style><Style TargetType='ProgressBar'><Style.Triggers><DataTrigger Binding='{Binding Meter}' Value='{x:Null}'><Setter Property='Visibility' Value='Collapsed'/></DataTrigger></Style.Triggers></Style></ProgressBar.Style></ProgressBar></StackPanel></DataTemplate>");
        Grid.SetRow(RelicList,3);RelicArea.Children.Add(RelicList);RelicEmpty.Margin=new(8,8,8,0);RelicEmpty.VerticalAlignment=VerticalAlignment.Top;RelicEmpty.IsHitTestVisible=false;Grid.SetRow(RelicEmpty,3);Panel.SetZIndex(RelicEmpty,1);RelicArea.Children.Add(RelicEmpty);
        RelicDetails=new Expander{Header="선택한 성유물 · 보상·확률",Content=new ScrollViewer{Content=RelicDetail,MaxHeight=210,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,12,8,0)},Margin=new(0,12,0,0)};Grid.SetRow(RelicDetails,4);RelicArea.Children.Add(RelicDetails);
        RelicOrder.SelectionChanged+=(_,_)=>RefreshRelics();Threshold.SelectionChanged+=(_,_)=>RefreshRelics();AllRefinements.Checked+=(_,_)=>RefreshRelics();AllRefinements.Unchecked+=(_,_)=>RefreshRelics();
        BuildGoals(title);BuildAcquisitionTabs(title,filters);BuildMaterialsTab();BuildRelicPlanningView();AutomationProperties.SetName(Category,"마스터리 장비 분류");AutomationProperties.SetName(Ownership,"마스터리 목록 필터");AutomationProperties.SetName(RelicOrder,"성유물 추천 정렬");AutomationProperties.SetName(Threshold,"성유물 보상 가치 기준");dashboardBuilt=true;
    }
    private static FrameworkElement Field(string title,FrameworkElement control)
    {var p=new StackPanel{Margin=new(0,0,0,8)};p.Children.Add(Text(title,12,"MutedBrush"));p.Children.Add(control);return p;}
    private static FrameworkElement SearchBox(TextBox input,string placeholder)
    {
        var host=new Grid();host.Children.Add(input);var hint=Text(placeholder,14,"MutedBrush");hint.VerticalAlignment=VerticalAlignment.Center;hint.Margin=new(12,0,12,0);hint.IsHitTestVisible=false;host.Children.Add(hint);
        void Hint()=>hint.Visibility=string.IsNullOrEmpty(input.Text)&&!input.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Collapsed;
        input.IsKeyboardFocusWithinChanged+=(_,_)=>Hint();input.TextChanged+=(_,_)=>Hint();AutomationProperties.SetName(input,placeholder);return host;
    }
}
