using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private readonly StackPanel topTabs = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border dragSpace = new() { Background = Brushes.Transparent, MinWidth = 40 };
    private FrameworkElement headerBrand = null!, headerControls = null!;
    private Button settingsButton = null!;
    private Popup settingsPopup = null!;
    private ComboBox equipmentKind=null!;
    private bool switchingEquipment;

    private FrameworkElement CreateHeader()
    {
        var header = new Grid { Margin = new Thickness(20, 0, 16, 0) };
        for (var i = 0; i < 7; i++) header.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var mark = BrandIcon.CreateSymbol();
        var name = T("WF Labs", 19); name.FontFamily=(FontFamily)Application.Current.Resources["Wanted"]; name.Margin = new Thickness(0); name.VerticalAlignment = VerticalAlignment.Center;
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 28, 0) };
        brand.Children.Add(mark); brand.Children.Add(name); header.Children.Add(brand); headerBrand = brand;
        foreach (var (label, action) in new (string, Action)[] { ("모드", () => {ReturnToModding();}), ("리벤", ShowRivens), ("자료", ShowCatalog), ("개발", ShowRoadmap), ("성유물",ShowRelics), ("내 장비",ShowInventory), ("일정",ShowSchedules) })
        {
            var tab = new RadioButton { Content = label, GroupName = "Labs", Width = 64, Height = 40, Style = (Style)Application.Current.Resources["NavigationItem"] };
            AutomationProperties.SetName(tab, label + " 탭");
            tab.Checked += (_, _) => action(); navigation.Add(tab); topTabs.Children.Add(tab);
        }
        topTabs.Children.Remove(navigation[4]);topTabs.Children.Insert(2,navigation[4]);
        topTabs.Children.Remove(navigation[5]);topTabs.Children.Insert(3,navigation[5]);
        topTabs.Children.Remove(navigation[6]);topTabs.Children.Insert(0,navigation[6]);
        Grid.SetColumn(topTabs, 1); header.Children.Add(topTabs); WindowChrome.SetIsHitTestVisibleInChrome(topTabs, true);
        equipmentKind=new ComboBox{ItemsSource=new[]{"근접","주무기","보조무기"},SelectedIndex=0,Width=110,Height=32,Margin=new(12,0,0,0)};
        equipmentKind.SelectionChanged+=(_,_)=>{if(switchingEquipment)return;if(navigation[1].IsChecked==true){ShowRivens();return;}if(equipmentKind.SelectedIndex==0)ShowModding();else ShowRanged(equipmentKind.SelectedIndex==1?"primary":"secondary");};
        // The legacy selector is retained as navigation state, not as a hidden user decision.
        Grid.SetColumn(dragSpace, 2); header.Children.Add(dragSpace);
        foreach (var button in new[] { Button("저장", SaveBuild), Button("불러오기", LoadBuild), Button("비교 기준 지정", () => { if (current is not null) { comparisonBuild = ReadBuild(); UpdateComparison(); } }) })
        {
            button.Height = 32; button.Padding = new Thickness(12, 4, 12, 4); button.FontSize = 13; button.Margin = new Thickness(6, 0, 0, 0); buildActions.Children.Add(button);
        }
        Grid.SetColumn(buildActions, 3); header.Children.Add(buildActions); WindowChrome.SetIsHitTestVisibleInChrome(buildActions, true);
        updateButton=Button("업데이트",OpenClientSettings);updateButton.Height=32;updateButton.FontSize=13;updateButton.Padding=new(12,4,12,4);updateButton.Margin=new(6,0,0,0);updateButton.VerticalAlignment=VerticalAlignment.Center;
        Grid.SetColumn(updateButton,4);header.Children.Add(updateButton);WindowChrome.SetIsHitTestVisibleInChrome(updateButton,true);
        settingsButton = Button("설정", () => settingsPopup.IsOpen = !settingsPopup.IsOpen);
        settingsButton.Height = 32; settingsButton.FontSize = 13; settingsButton.Padding = new Thickness(12, 4, 12, 4); settingsButton.VerticalAlignment = VerticalAlignment.Center; settingsButton.Margin = new Thickness(12, 0, 0, 0);
        settingsButton.ToolTip = "계산 표본 · 글꼴 · 테마 · 경량 모드";
        Grid.SetColumn(settingsButton, 5); header.Children.Add(settingsButton); WindowChrome.SetIsHitTestVisibleInChrome(settingsButton, true);
        var settings = Stack(CreateCalculationSettings(),Spacer(14),T("화면 설정", 14), performanceSwitch, themeSwitch, Spacer(14), UiFonts.CreateSelector(), Spacer(14), Button("앱 실행 · 업데이트",OpenClientSettings),T($"v{VelopackUpdateFeed.AppVersion} · PC · 한국어\nWF Wiki 기준", 12, "#596B85"));
        foreach(var action in settings.Children.OfType<Button>())action.Margin=new(0,8,0,12);
        themeSwitch.Margin=new(0,8,0,0);
        var settingsPanel = new Border { Child = new ScrollViewer{Content=settings,MaxHeight=560,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,8,0)}, Width = 320, Padding = new Thickness(16), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        settingsPanel.SetResourceReference(Border.BackgroundProperty, "PopupBrush"); settingsPanel.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
        settingsPopup = new Popup { Child = settingsPanel, PlacementTarget = settingsButton, Placement = PlacementMode.Bottom, VerticalOffset = 8, AllowsTransparency = true, StaysOpen = false };
        PopupLayout.Configure(settingsPopup,this);
        KeyboardNavigation.SetTabNavigation(settingsPanel, KeyboardNavigationMode.Cycle);
        settingsPopup.Opened += (_, _) => performanceSwitch.Focus();
        settingsPopup.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { settingsPopup.IsOpen = false; settingsButton.Focus(); e.Handled = true; } };
        // Keep the popup in the logical tree for dynamic theme resources and lifecycle ownership.
        Grid.SetColumn(settingsPopup, 5); header.Children.Add(settingsPopup);
        headerControls = WindowFrame.Controls(this); headerControls.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(headerControls, 6); header.Children.Add(headerControls);
        var shell = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) };
        shell.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
        return shell;
    }

    internal object CheckHeaderLayout()
    {
        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
        var visible = new[] { headerBrand, topTabs, dragSpace, buildActions, updateButton, settingsButton, headerControls }.Where(x => x.Visibility == Visibility.Visible).Select(Bounds).ToArray();
        if (visible.Any(x => x.Left < 0 || x.Right > root.ActualWidth + .5 || x.Top < 0 || x.Bottom > WindowFrame.HeaderHeight + .5)) throw new Exception("상단 탐색/창 버튼이 잘립니다.");
        for (var i = 1; i < visible.Length; i++) if (visible[i - 1].Right > visible[i].Left + .5) throw new Exception("상단 메뉴와 작업 버튼이 겹칩니다.");
        if (Bounds(dragSpace).Width < 40 || Bounds(page).Left > 24) throw new Exception("창 이동 공간 또는 전체 너비 작업 영역이 부족합니다.");
        if (!topTabs.Children.OfType<System.Windows.Controls.RadioButton>().Select(x => x.Content as string).SequenceEqual(new[] { "일정", "모드", "리벤", "성유물", "내 장비", "자료", "개발" }) || navigation.Count(x => x.IsChecked == true) != 1) throw new Exception("한국어 탭/선택 표시 실패");
        return new { passed = true, tabs = navigation.Select(x => x.Content), dragWidth = dragSpace.ActualWidth, pageLeft = Bounds(page).Left };
    }

    internal (Point Caption, Point Tab, Point Settings, Point Close) HeaderHitPointsForCheck()
    {
        Point Center(FrameworkElement element) => element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), this);
        return (Center(dragSpace), Center(navigation[1]), Center(settingsButton), Center((FrameworkElement)((Panel)headerControls).Children[^1]));
    }
}

