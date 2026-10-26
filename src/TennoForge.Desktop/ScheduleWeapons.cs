using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    internal readonly ContentControl ScheduleContent=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
    internal readonly Dictionary<string,RadioButton> PageTabs=new();
    internal readonly TextBlock IncarnonClock=Text("—",27,"AccentBrush"),IncarnonWeek=Text("이번 주",21),IncarnonSource=Text("",13,"MutedBrush");
    internal readonly StackPanel IncarnonCurrent=new(),IncarnonNext=new(),IncarnonCalendar=new();
    private string incarnonKey="";
    private FrameworkElement CreateScheduleTabs(FrameworkElement overview,FrameworkElement weapons)
    {
        var tabs=new WrapPanel{Margin=new(0,0,0,16)};foreach(var (label,content) in new[]{("종합 일정",overview),("무기 순환",weapons)})
        {
            var tab=new RadioButton{Content=label,GroupName="SchedulePages",MinWidth=110,Height=36,Margin=new(0,0,12,0),Style=(Style)Application.Current.FindResource("NavigationItem")};
            AutomationProperties.SetName(tab,"일정 "+label+" 탭");tab.Checked+=(_,_)=>ScheduleContent.Content=content;PageTabs[label]=tab;tabs.Children.Add(tab);
        }
        PageTabs["종합 일정"].IsChecked=true;return tabs;
    }
    private FrameworkElement CreateWeaponRotationView(FrameworkElement shopClocks,FrameworkElement shops)
    {
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});root.ColumnDefinitions.Add(new(){Width=new(32)});root.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var lich=new Grid();lich.RowDefinitions.Add(new(){Height=GridLength.Auto});lich.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var heading=RivenPresentation.Stack(Text("리치 무기 · 테넷·코다 상점",22),shopClocks);heading.Margin=new(0,0,0,16);lich.Children.Add(heading);Grid.SetRow(shops,1);lich.Children.Add(shops);
        var left=new ScrollViewer{Content=lich,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,8,0)};
        var details=((Panel)shops).Children.OfType<Expander>().Single();
        void FitShop()=>lich.Height=Math.Max(details.IsExpanded?640:420,left.ActualHeight);
        left.SizeChanged+=(_,_)=>FitShop();details.Expanded+=(_,_)=>{FitShop();details.Dispatcher.BeginInvoke(()=>details.BringIntoView(),DispatcherPriority.Loaded);};details.Collapsed+=(_,_)=>FitShop();root.Children.Add(left);
        var incarnon=new StackPanel();incarnon.Children.Add(Text("인카논 · 강철의 길 순환로",22));incarnon.Children.Add(Text("매주 5개 중 2개 선택 · 월요일 오전 9시(KST) 교체",14,"MutedBrush"));
        var countdown=new DockPanel{Margin=new(0,8,0,10)};IncarnonClock.Margin=new(0);DockPanel.SetDock(IncarnonClock,Dock.Right);countdown.Children.Add(IncarnonClock);countdown.Children.Add(Text("주간 교체까지",14,"MutedBrush"));incarnon.Children.Add(countdown);incarnon.Children.Add(IncarnonSource);
        IncarnonWeek.Margin=new(0,18,0,8);incarnon.Children.Add(IncarnonWeek);incarnon.Children.Add(IncarnonCurrent);incarnon.Children.Add(Text("다음 주 · 예상",18));incarnon.Children.Add(IncarnonNext);
        incarnon.Children.Add(new Expander{Header="전체 9주 순환표",Content=IncarnonCalendar,Margin=new(0,16,0,0)});incarnon.Children.Add(Button("순환표 출처 ↗",()=>Open(IncarnonRotations.Table.WikiUrl)));
        var right=new Border{Child=Scroll(incarnon),BorderThickness=new(1,0,0,0),Padding=new(24,0,0,0)};right.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Grid.SetColumn(right,2);root.Children.Add(right);
        return root;
    }
    private void RefreshIncarnon(DateTimeOffset now)
    {
        var current=IncarnonRotations.Current(now,Data.Snapshot?.Incarnon);var next=IncarnonRotations.Upcoming(now,8,Data.Snapshot?.Incarnon);
        IncarnonClock.Text=WorldScheduleEngine.Countdown(current.EndUtc,now);IncarnonWeek.Text=current.Week>0?$"이번 주 · {current.Week}주차":"이번 주 · 새 구성";
        IncarnonSource.Text=(current.Confirmed&&!Data.Cached?current.Source+" 확인":"순환표·저장 자료 기준 예상")+" · "+WorldScheduleEngine.KoreaTime(current.StartUtc)+" ~ "+WorldScheduleEngine.KoreaTime(current.EndUtc);
        var key=current.StartUtc.ToString("o")+":"+current.Week+":"+string.Join(',',current.Weapons.Select(w=>w.Id+":"+w.Name));if(key==incarnonKey)return;incarnonKey=key;
        IncarnonCurrent.Children.Clear();IncarnonNext.Children.Clear();IncarnonCalendar.Children.Clear();IncarnonCurrent.Children.Add(WeaponChoices(current));
        if(next.Length>0){IncarnonNext.Children.Add(Text(WorldScheduleEngine.KoreaTime(next[0].StartUtc)+$" · {next[0].Week}주차",13,"MutedBrush"));IncarnonNext.Children.Add(WeaponChoices(next[0]));}
        else IncarnonNext.Children.Add(Text("새로운 구성입니다. 다음 순환표 확인이 필요합니다.",14,"MutedBrush"));
        foreach(var window in new[]{current}.Concat(next))IncarnonCalendar.Children.Add(RivenPresentation.Stack(Text($"{window.Week}주차 · {TimeZoneInfo.ConvertTime(window.StartUtc,WorldScheduleEngine.Korea):MM-dd} ~ {TimeZoneInfo.ConvertTime(window.EndUtc,WorldScheduleEngine.Korea):MM-dd}",14,"AccentBrush"),Text(string.Join(" · ",window.Weapons.Select(w=>w.Name)),14),new Border{Height=12}));
    }
    private FrameworkElement WeaponChoices(IncarnonRotationWindow window)
    {
        var row=new UniformGrid{Columns=5,Margin=new(0,0,0,16)};foreach(var weapon in window.Weapons)
        {
            var name=Text(weapon.Name,15);name.Height=44;name.LineHeight=22;name.Margin=new(0);name.TextTrimming=TextTrimming.CharacterEllipsis;name.VerticalAlignment=VerticalAlignment.Center;
            var category=Text(weapon.Category switch{"Primary"=>"주무기","Secondary"=>"보조무기","Melee"=>"근접무기",_=>"어댑터"},12,"MutedBrush");category.Margin=new(0);
            var button=Button("",()=>ModRequested?.Invoke(weapon.Id));button.Content=RivenPresentation.Stack(name,category);button.Padding=new(8,8,8,8);button.Margin=new(0,0,6,0);button.MinHeight=86;button.IsEnabled=weapon.Id.Length>0;button.ToolTip=weapon.Name+" · 모딩 열기";AutomationProperties.SetName(button,weapon.Name+" 인카논 모딩 열기");row.Children.Add(button);
        }
        return row;
    }
}
