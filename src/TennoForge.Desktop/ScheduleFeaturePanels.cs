using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class SchedulePage
{
    private Action? refreshFeatureContents,refreshFeatureFilters;
    private static Button FeatureAction(string title,Action action)
    {var b=new Button{Content=title,MinHeight=34,Padding=new(12,6,12,6),Margin=new(0),VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(b,title);b.Click+=(_,_)=>action();return b;}
    private static StackPanel FeatureGroup(string title)
    {var p=new StackPanel();var t=Text(title,18);t.Margin=new(0,0,0,12);p.Children.Add(t);return p;}
    private static Grid FeatureColumns(UIElement left,UIElement right)
    {
        var g=new Grid();g.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});g.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var a=new Border{Child=Scroll(left),Margin=new(0,0,24,0)};g.Children.Add(a);var b=new Border{Child=Scroll(right),BorderThickness=new(1,0,0,0),Padding=new(24,0,0,0)};b.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");Grid.SetColumn(b,1);g.Children.Add(b);return g;
    }
    private Grid BuildFeatureRoot()
    {
        var root=new Grid{Margin=new(24)};root.Resources[typeof(Button)]=Application.Current.FindResource("ScheduleAction");
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel{Margin=new(0,0,0,16)};var close=FeatureAction("닫기",()=>featureWindow?.Close());close.Margin=new(12,0,0,0);DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        var edit=FeatureAction("위치·크기 조정",BeginOverlayEdit);edit.Margin=new(16,0,0,0);DockPanel.SetDock(edit,Dock.Right);header.Children.Add(edit);
        var enabled=new CheckBox{Content="게임 오버레이",IsChecked=Preferences.OverlayEnabled,VerticalAlignment=VerticalAlignment.Center,FontSize=13};dialogOverlaySwitch=enabled;AutomationProperties.SetName(enabled,"일정 오버레이 켜기");DockPanel.SetDock(enabled,Dock.Right);header.Children.Add(enabled);
        void Enable(){if(syncingOverlay)return;Preferences=Preferences with{OverlayEnabled=enabled.IsChecked==true};SavePreferences();TickFeatures();}enabled.Checked+=(_,_)=>Enable();enabled.Unchecked+=(_,_)=>Enable();
        var title=Text("오버레이 · 알림",22);title.Margin=new(0);title.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(title);header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource==title&&e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)featureWindow?.DragMove();};root.Children.Add(header);
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,0,20)};Grid.SetRow(tabs,1);root.Children.Add(tabs);var content=new Grid();Grid.SetRow(content,2);root.Children.Add(content);
        var display=FeatureGroup("성유물 균열");var show=new CheckBox{Content="현재 열린 성유물 표시",IsChecked=Preferences.ShowCurrentFissures,Margin=new(0,0,0,12)};display.Children.Add(show);
        bool syncingPath=false;int lastPath=Preferences.FissureView;var pathChoices=new Dictionary<int,RadioButton>();
        var pathLabel=Text("표시할 경로",12,"MutedBrush");pathLabel.Margin=new(0,0,0,4);display.Children.Add(pathLabel);var paths=new WrapPanel{Margin=new(0,0,0,20)};
        foreach(var (label,index) in new[]{("전체",0),("강철의 길만",1),("일반",2),("보이드 폭풍",3),("알림 대상",4)})
        {
            var r=new RadioButton{Content=label,GroupName="OverlayFissurePath",IsChecked=Preferences.FissureView==index,MinWidth=label.Length*11+18,Height=34,Margin=new(0,0,8,0),FontSize=13,Style=(Style)Application.Current.FindResource("NavigationItem")};AutomationProperties.SetName(r,"균열 경로 "+label);pathChoices[index]=r;
            r.Checked+=(_,_)=>{if(syncingPath)return;Preferences=Preferences with{FissureView=index};SavePreferences();refreshFeatureContents?.Invoke();TickFeatures();};paths.Children.Add(r);
        }
        display.Children.Add(paths);var picks=new StackPanel();
        foreach(var (id,label) in new[]{("nextS","다음 S티어 중재"),("arbitration","현재 중재")}.Concat(WorldScheduleEngine.Definitions.Select(d=>(d.Id,d.Name))).Concat(new[]{("tenet","테넷 상점"),("coda","코다 상점"),("fissures","알림 조건에 맞는 성유물 균열")}))
        {
            var c=new CheckBox{Content=label,IsChecked=Preferences.OverlayItems.Contains(id),Margin=new(0,5,0,5)};c.SetResourceReference(StyleProperty,"ChecklistItem");AutomationProperties.SetName(c,label+" 화면에 표시");
            void Change(){var set=Preferences.OverlayItems.ToHashSet();if(c.IsChecked==true)set.Add(id);else set.Remove(id);Preferences=Preferences with{OverlayItems=set.ToArray()};SavePreferences();TickFeatures();}c.Checked+=(_,_)=>Change();c.Unchecked+=(_,_)=>Change();picks.Children.Add(c);
        }
        display.Children.Add(new Expander{Header="표시할 일정 선택",FontSize=17,Content=picks,IsExpanded=true,Margin=new(0,0,0,16)});
        var showAlerts=new CheckBox{Content="현재 게임 얼럿 표시",IsChecked=Preferences.ShowGameAlerts,Margin=new(0,0,0,16)};display.Children.Add(showAlerts);
        void Show(){if(syncingPath)return;Preferences=Preferences with{ShowCurrentFissures=show.IsChecked==true,ShowGameAlerts=showAlerts.IsChecked==true};SavePreferences();TickFeatures();}show.Checked+=(_,_)=>Show();show.Unchecked+=(_,_)=>Show();showAlerts.Checked+=(_,_)=>Show();showAlerts.Unchecked+=(_,_)=>Show();
        var liveGroup=FeatureGroup("개별 균열 고정");liveGroup.Children.Add(Text("선택한 균열을 게임 오버레이에 표시합니다.",14,"MutedBrush"));var refresh=FeatureAction("새로고침",async()=>{await RefreshAsync(true);refreshFeatureContents?.Invoke();});refresh.HorizontalAlignment=HorizontalAlignment.Left;refresh.Margin=new(0,0,0,12);liveGroup.Children.Add(refresh);var live=new StackPanel();liveGroup.Children.Add(live);
        var notifications=FeatureGroup("성유물 알림");var form=new Grid{Margin=new(0,0,0,12)};form.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});form.ColumnDefinitions.Add(new(){Width=new(132)});form.ColumnDefinitions.Add(new(){Width=new(90)});
        var types=new[]{new KeyValuePair<string,string>("","모든 미션")}.Concat(FissureParser.MissionTypes.Where(p=>new[]{"MT_EXTERMINATION","MT_CAPTURE","MT_RESCUE","MT_SABOTAGE","MT_MOBILE_DEFENSE","MT_DEFENSE","MT_SURVIVAL","MT_TERRITORY","MT_EXCAVATE","MT_INTEL","MT_ARTIFACT","MT_VOID_CASCADE","MT_CORRUPTION","MT_ALCHEMY","MT_MIRROR_DEFENSE"}.Contains(p.Key)).OrderBy(p=>p.Value)).ToArray();
        var mission=new ComboBox{ItemsSource=types,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue="MT_VOID_CASCADE",MinWidth=130};var mode=new ComboBox{ItemsSource=new[]{"모든 경로","강철의 길","일반","보이드 폭풍"},SelectedIndex=1};var era=new ComboBox{ItemsSource=new[]{"전체","리스","메소","네오","액시","레퀴엠","옴니아"},SelectedIndex=0};
        foreach(var (input,label,index) in new[]{(mission,"미션 유형",0),(mode,"경로",1),(era,"성유물",2)}){AutomationProperties.SetName(input,"성유물 알림 "+label);var field=Field(label,input);Grid.SetColumn(field,index);form.Children.Add(field);}notifications.Children.Add(form);
        var rules=new StackPanel();var ruleStatus=Text("",12,"MutedBrush");
        void Rules()
        {
            rules.Children.Clear();foreach(var r in Preferences.FissureRules)
            {
                string label=(r.Mode switch{1=>"강철의 길",2=>"일반",3=>"보이드 폭풍",_=>"모든 경로"})+" · "+(r.Missions.Length==0?"모든 미션":string.Join(" · ",r.Missions.Select(FissureParser.MissionLabel)))+(r.Tier==0?"":$" · {era.Items[Math.Clamp(r.Tier,0,6)]}");
                var row=new DockPanel{Margin=new(0,6,0,6)};var remove=FeatureAction("삭제",()=>{Preferences=Preferences with{FissureRules=Preferences.FissureRules.Where(x=>x.Id!=r.Id).ToArray()};SavePreferences();Rules();refreshFeatureContents?.Invoke();});remove.Margin=new(8,0,0,0);DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
                var check=new CheckBox{Content=new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap},ToolTip=label,IsChecked=r.Enabled,VerticalAlignment=VerticalAlignment.Center};check.SetResourceReference(StyleProperty,"ChecklistItem");AutomationProperties.SetName(check,label+" 알림");void Change(){Preferences=Preferences with{FissureRules=Preferences.FissureRules.Select(x=>x.Id==r.Id?x with{Enabled=check.IsChecked==true}:x).ToArray()};SavePreferences();refreshFeatureContents?.Invoke();TickFeatures();}check.Checked+=(_,_)=>Change();check.Unchecked+=(_,_)=>Change();row.Children.Add(check);rules.Children.Add(row);
            }
            if(rules.Children.Count==0)rules.Children.Add(Text("등록한 성유물 알림 없음",13,"MutedBrush"));
        }
        notifications.Children.Add(FeatureAction("이 조건으로 알림 추가",()=>
        {
            var code=mission.SelectedValue as string??"";string[] codes=code.Length==0?[]:[code];var existing=Preferences.FissureRules.FirstOrDefault(r=>r.Mode==mode.SelectedIndex&&r.Tier==era.SelectedIndex&&r.Missions.SequenceEqual(codes));
            if(existing is not null)Preferences=Preferences with{FissureRules=Preferences.FissureRules.Select(r=>r.Id==existing.Id?r with{Enabled=true}:r).ToArray()};
            else if(Preferences.FissureRules.Length<20)Preferences=Preferences with{FissureRules=Preferences.FissureRules.Append(new(Guid.NewGuid().ToString("N"),codes,mode.SelectedIndex,era.SelectedIndex)).ToArray()};else{ruleStatus.Text="알림 조건은 최대 20개입니다.";return;}
            ruleStatus.Text=existing is null?"알림을 추가했습니다.":"이미 등록된 조건을 켰습니다.";SavePreferences();Rules();refreshFeatureContents?.Invoke();TickFeatures();
        }));ruleStatus.Margin=new(0,6,0,8);notifications.Children.Add(ruleStatus);notifications.Children.Add(rules);Rules();
        var alertsTitle=Text("게임 얼럿",18);alertsTitle.Margin=new(0,24,0,12);notifications.Children.Add(alertsTitle);
        var alertNotify=new CheckBox{Content="새 게임 얼럿 알림",IsChecked=Preferences.NotifyGameAlerts,Margin=new(0,0,0,12)};AutomationProperties.SetName(alertNotify,"새 게임 얼럿 알림");notifications.Children.Add(alertNotify);
        void AlertNotify(){Preferences=Preferences with{NotifyGameAlerts=alertNotify.IsChecked==true};SavePreferences();TickFeatures();}alertNotify.Checked+=(_,_)=>AlertNotify();alertNotify.Unchecked+=(_,_)=>AlertNotify();notifications.Children.Add(Text("보상 이름 포함 · 비우면 모든 보상",12,"MutedBrush"));
        var keyword=new TextBox{Text=Preferences.AlertRewardFilter,MaxLength=120,MinHeight=34};AutomationProperties.SetName(keyword,"얼럿 보상 필터");
        void CommitReward(){if(Preferences.AlertRewardFilter==keyword.Text)return;Preferences=Preferences with{AlertRewardFilter=keyword.Text};SavePreferences();TickFeatures();}
        keyword.LostFocus+=(_,_)=>CommitReward();keyword.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){CommitReward();e.Handled=true;}};
        var rewardRow=new DockPanel{Margin=new(0,0,0,8)};var applyReward=FeatureAction("적용",CommitReward);applyReward.Margin=new(8,0,0,0);DockPanel.SetDock(applyReward,Dock.Right);rewardRow.Children.Add(applyReward);rewardRow.Children.Add(keyword);notifications.Children.Add(rewardRow);
        var output=FeatureGroup("알림 방식");var outputs=new WrapPanel{Margin=new(0,0,0,16)};foreach(var (label,isSound) in new[]{("팝업 알림",false),("소리 알림",true)})
        {var c=new CheckBox{Content=label,IsChecked=isSound?Preferences.Sound:Preferences.Popup,Margin=new(0,0,24,0)};void Change(){Preferences=isSound?Preferences with{Sound=c.IsChecked==true}:Preferences with{Popup=c.IsChecked==true};SavePreferences();}c.Checked+=(_,_)=>Change();c.Unchecked+=(_,_)=>Change();outputs.Children.Add(c);}output.Children.Add(outputs);output.Children.Add(OverlaySlider("알림 음량",0,100,Preferences.SoundVolume*100,v=>Preferences=Preferences with{SoundVolume=v/100},v=>$"{v:0}%"));
        var test=FeatureAction("알림 테스트",()=>{if(Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")!="1")Notify([new("test","알림 테스트","실제 미션 발생 알림이 아닙니다.",Clock().AddMinutes(1))]);});test.Margin=new(0,0,0,24);output.Children.Add(test);output.Children.Add(Text("현재 게임 얼럿",18));var alertList=new StackPanel();output.Children.Add(alertList);
        var style=FeatureGroup("배경과 글자");style.Children.Add(OverlaySlider("배경 투명도",0,100,(1-Preferences.BackgroundOpacity)*100,v=>Preferences=Preferences with{BackgroundOpacity=1-v/100},v=>$"{v:0}%"));var shadow=new CheckBox{Content="글자 그림자",IsChecked=Preferences.TextShadow,Margin=new(0,8,0,12)};void Shadow(){Preferences=Preferences with{TextShadow=shadow.IsChecked==true};SavePreferences();TickFeatures();}shadow.Checked+=(_,_)=>Shadow();shadow.Unchecked+=(_,_)=>Shadow();style.Children.Add(shadow);
        style.Children.Add(OverlaySlider("그림자 진하기",0,100,Preferences.ShadowStrength*100,v=>Preferences=Preferences with{ShadowStrength=v/100},v=>$"{v:0}%"));style.Children.Add(OverlaySlider("글자 크기",85,130,Preferences.TextScale*100,v=>Preferences=Preferences with{TextScale=v/100},v=>$"{v:0}%"));style.Children.Add(Text("투명도는 배경에만 적용됩니다.",12,"MutedBrush"));
        var reset=FeatureAction("기본 위치로 복원",()=>{overlay?.FinishEdit();Preferences=Preferences with{X=.02,Y=.12,Width=480,Height=620};SavePreferences();TickFeatures();});reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new(0,12,0,0);display.Children.Add(reset);
        var previewGroup=FeatureGroup("미리보기");var preview=new ScheduleOverlayView{Height=420};var previewFrame=new Border{Child=preview,BorderThickness=new(1)};previewFrame.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");previewGroup.Children.Add(previewFrame);
        refreshFeaturePreview=()=>{var now=Clock();var data=OverlayData(now);previewFrame.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(Preferences.BackgroundOpacity*255),15,17,19));preview.Update(TimeZoneInfo.ConvertTime(now,WorldScheduleEngine.Korea).ToString("HH:mm:ss"),data with{Timers=data.Timers.Take(2).ToArray(),Fissures=data.Fissures.Take(3).ToArray()},Preferences);};
        refreshFeatureContents=()=>
        {
            live.Children.Clear();if(!FissureAlerts.Fresh(Data.Snapshot,Clock())||Data.Cached)live.Children.Add(Text("저장 자료 · 온라인 갱신 대기",12,"MutedBrush"));
            foreach(var f in (Data.Snapshot?.Fissures??[]).Where(f=>f.Active(Clock())&&FissureAllowed(f)).OrderBy(f=>f.EndUtc))
            {
                var text=new TextBlock{Text=$"{f.TierLabel} · {f.Mission} · {f.Mode}\n{f.Node} · 종료 {TimeZoneInfo.ConvertTime(f.EndUtc,WorldScheduleEngine.Korea):HH:mm}",FontSize=13,LineHeight=19,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
                var c=new CheckBox{Content=text,ToolTip=text.Text+"\n체크하면 오버레이에 고정 표시",IsChecked=Preferences.OverlayItems.Contains("fissure:"+f.Id),Margin=new(0,5,0,5)};c.SetResourceReference(StyleProperty,"ChecklistItem");void Change(){var ids=Preferences.OverlayItems.ToHashSet();if(c.IsChecked==true)ids.Add("fissure:"+f.Id);else ids.Remove("fissure:"+f.Id);Preferences=Preferences with{OverlayItems=ids.ToArray(),OverlayEnabled=c.IsChecked==true||Preferences.OverlayEnabled};SavePreferences();TickFeatures();}c.Checked+=(_,_)=>Change();c.Unchecked+=(_,_)=>Change();live.Children.Add(c);
            }
            if(live.Children.Count==0)live.Children.Add(Text("현재 조건에 맞는 균열 없음",13,"MutedBrush"));alertList.Children.Clear();
            if(!GameAlerts.Fresh(Data.Snapshot,Clock())||Data.Cached)alertList.Children.Add(Text("저장 자료 · 온라인 갱신 대기",12,"MutedBrush"));
            foreach(var a in (Data.Snapshot?.Alerts??[]).Where(a=>a.Active(Clock())).OrderBy(a=>a.EndUtc)){var t=Text($"{a.Mission} · {a.Node} · {TimeZoneInfo.ConvertTime(a.EndUtc,WorldScheduleEngine.Korea):HH:mm} 종료\n{a.Reward}",13);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;t.ToolTip=$"{a.Mission} · {a.Node}\n{a.Reward}\n종료 {WorldScheduleEngine.KoreaTime(a.EndUtc)}";t.Margin=new(0,8,0,12);alertList.Children.Add(t);}
            if(alertList.Children.Count==0)alertList.Children.Add(Text("현재 열린 얼럿 없음",13,"MutedBrush"));
        };
        refreshFeatureFilters=()=>{syncingPath=true;foreach(var (index,r) in pathChoices)r.IsChecked=index==Preferences.FissureView;show.IsChecked=Preferences.ShowCurrentFissures;showAlerts.IsChecked=Preferences.ShowGameAlerts;syncingPath=false;if(lastPath!=Preferences.FissureView){lastPath=Preferences.FissureView;refreshFeatureContents?.Invoke();}};
        var panels=new[]{FeatureColumns(display,liveGroup),FeatureColumns(notifications,output),FeatureColumns(style,previewGroup)};foreach(var panel in panels){panel.Visibility=Visibility.Collapsed;content.Children.Add(panel);}
        foreach(var (name,index) in new[]{("표시",0),("알림",1),("스타일",2)})
        {var tab=new RadioButton{Content=name,GroupName="ScheduleFeatureTabs",Width=80,Height=36,Style=(Style)Application.Current.FindResource("NavigationItem")};AutomationProperties.SetName(tab,"오버레이 "+name+" 탭");tab.Checked+=(_,_)=>{for(int i=0;i<panels.Length;i++)panels[i].Visibility=i==index?Visibility.Visible:Visibility.Collapsed;refreshFeatureContents?.Invoke();refreshFeaturePreview?.Invoke();};FeatureTabs[name]=tab;tabs.Children.Add(tab);}
        var footer=Text("변경 사항 자동 저장 · 새 미션은 1분마다 확인 · 같은 미션은 한 번만 알림",12,"MutedBrush");footer.Margin=new(0,16,0,0);Grid.SetRow(footer,3);root.Children.Add(footer);FeatureTabs["표시"].IsChecked=true;return root;
    }
}
