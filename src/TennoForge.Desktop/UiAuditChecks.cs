using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal object AuditUi(Action<FrameworkElement,string,int,int> render)
    {
        var checks=new List<string>();var scrolls=0;var actionGaps=0;
        IEnumerable<DependencyObject> Tree(DependencyObject node)
        {yield return node;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Tree(VisualTreeHelper.GetChild(node,i)))yield return child;}
        void Capture(FrameworkElement content,string name,int width=1760,int height=920,bool bottom=true,bool natural=false)
        {
            if(natural){content.Measure(new Size(width,height));height=Math.Min(height,(int)Math.Ceiling(content.DesiredSize.Height));}
            render(content,name,width,height);
            var viewers=Tree(content).OfType<ScrollViewer>().Where(s=>s.Visibility==Visibility.Visible&&s.VerticalScrollBarVisibility is ScrollBarVisibility.Auto or ScrollBarVisibility.Visible&&s.ViewportHeight>0&&s.ScrollableHeight>1).ToArray();
            foreach(var s in viewers)
            {
                s.ScrollToEnd();s.UpdateLayout();
                if(Math.Abs(s.VerticalOffset-s.ScrollableHeight)>.1)throw new Exception(name+": 스크롤 끝에 도달하지 못했습니다.");
                var bar=Tree(s).OfType<ScrollBar>().FirstOrDefault(b=>b.Orientation==Orientation.Vertical&&b.Visibility==Visibility.Visible);
                if(bar is null||bar.ActualWidth<6||bar.ActualHeight<20)throw new Exception(name+": 세로 스크롤바 누락/잘림");
                scrolls++;
            }
            if(bottom&&viewers.Length>0)render(content,name+"-bottom",width,height);
            foreach(var s in viewers)s.ScrollToTop();content.UpdateLayout();
            foreach(var panel in Tree(content).OfType<StackPanel>().Where(p=>p.Orientation==Orientation.Vertical))
            {
                var children=panel.Children.OfType<FrameworkElement>().Where(c=>c.Visibility==Visibility.Visible&&c.ActualHeight>0).ToArray();
                for(var i=1;i<children.Length;i++)if(children[i-1] is Button a&&children[i] is Button b)
                {
                    var gap=b.TranslatePoint(new(),panel).Y-a.TranslatePoint(new(0,a.ActualHeight),panel).Y;
                    if(gap<4)throw new Exception(name+": 버튼 간격 부족: "+a.Content+" / "+b.Content+" ("+gap+")");actionGaps++;
                }
            }
            checks.Add(name);
        }
        foreach(var dark in new[]{true,false})
        {
            SetDarkTheme(dark);var theme=dark?"dark":"light";
            void Main(string name)=>Capture((FrameworkElement)Content,theme+"-"+name);
            ShowModding();Main("modding");
            foreach(var section in Tree((FrameworkElement)page.Content).OfType<Expander>().ToArray())section.IsExpanded=true;
            Main("modding-expanded");
            if(dark)
            {
                var scroller=scenarioScroller!;scroller.UpdateLayout();
                var pages=(int)Math.Ceiling(scroller.ScrollableHeight/Math.Max(1,scroller.ViewportHeight-80));
                for(var i=1;i<=pages;i++){scroller.ScrollToVerticalOffset(i*(scroller.ViewportHeight-80));scroller.UpdateLayout();render((FrameworkElement)Content,theme+"-scenario-page-"+i,1760,920);}
                scroller.ScrollToTop();
            }
            foreach(var section in Tree((FrameworkElement)page.Content).OfType<Expander>().ToArray())section.IsExpanded=false;
            FormaForCheck(true);Main("forma");FormaForCheck(false);
            Capture((FrameworkElement)settingsPopup.Child,theme+"-settings",320,610,natural:true);
            Capture((FrameworkElement)capacityPopup!.Child,theme+"-capacity",380,440,natural:true);
            Capture(OpenZawBuilder(false),theme+"-zaw",440,500,natural:true);
            foreach(var kind in new[]{"exilus","melee","zaw"})
            {OpenEquipmentPicker(kind,false);Capture((FrameworkElement)equipmentPicker!.Child,theme+"-equipment-"+kind,420,540,natural:true);CloseEquipmentPicker();}
            if(board.SlotForCheck(0).ContextMenu is {} menu)Capture(menu,theme+"-mod-menu",220,50,false);
            var stanceWindow=StanceForCheck();Capture((FrameworkElement)stanceWindow.Content,theme+"-stance",620,480);
            ShowRivens();
            var id=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
            SetWorkshop(new(new(id,8,[new(RivenStat.Damage,97.8),new(RivenStat.StatusChance,53.5)],RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK")));
            Main("riven");PendingRivenForCheck();Main("riven-pending");SetWorkshop(rivenSession! with{Pending=null});
            var editable=(TextBox?)rivenWeapon.Template.FindName("Query",rivenWeapon);
            if(editable is null||editable.Visibility!=Visibility.Visible)throw new Exception("검색 가능한 무기 입력 템플릿 누락");
            var dropdown=(Popup)rivenLock.Template.FindName("PART_Popup",rivenLock);
            Capture((FrameworkElement)dropdown.Child,theme+"-dropdown",520,320,natural:true);
            var weapons=(Popup)rivenWeapon.Template.FindName("PART_Popup",rivenWeapon);
            Capture((FrameworkElement)weapons.Child,theme+"-weapon-dropdown",560,320,natural:true);
            rivenWeapon.Filter("니카나");rivenWeapon.Suggestions.SelectedIndex=1;
            Capture((FrameworkElement)weapons.Child,theme+"-weapon-search",560,320,natural:true);
            rivenWeapon.Filter("존재하지않는무기zz");
            Capture((FrameworkElement)weapons.Child,theme+"-weapon-search-empty",560,160,natural:true);
            for(var i=0;i<5;i++){CatalogPreviewForCheck(i);Main("catalog-"+i);}
            catalogSearch.Text="존재하지 않는 검색어";Main("catalog-empty");catalogSearch.Text="";
            ShowRoadmap();Main("roadmap");
            var client=new ClientSettingsWindow(()=>{});Capture((FrameworkElement)client.Content,theme+"-client",560,540);
            client.PreviewForAudit(true);Capture((FrameworkElement)client.Content,theme+"-client-download",560,540);
            client.PreviewForAudit(false);Capture((FrameworkElement)client.Content,theme+"-client-error",560,540);
            var confirm=new ConfirmationWindow("업데이트 적용","현재 빌드를 보관하고 업데이트를 설치한 뒤 다시 시작할까요?","설치 후 다시 시작");
            Capture((FrameworkElement)confirm.Content,theme+"-confirmation",520,320);
            // Exercise the shared templates with overflow in both axes, including track clicks.
            var overflow=new ScrollViewer{Width=360,Height=180,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=string.Join("\n",Enumerable.Repeat(new string('W',160),40)),TextWrapping=TextWrapping.NoWrap}};
            Capture(overflow,theme+"-scrollbars",360,180);
            foreach(var bar in Tree(overflow).OfType<ScrollBar>())
            {
                var track=(Track)bar.Template.FindName("PART_Track",bar);
                var command=track.IncreaseRepeatButton.Command;
                if(command!=(bar.Orientation==Orientation.Horizontal?ScrollBar.PageRightCommand:ScrollBar.PageDownCommand))throw new Exception("스크롤바 방향 명령 불일치");
                ((System.Windows.Input.RoutedCommand)command).Execute(null,track.IncreaseRepeatButton);overflow.UpdateLayout();
                if((bar.Orientation==Orientation.Horizontal?overflow.HorizontalOffset:overflow.VerticalOffset)<=0)throw new Exception("스크롤바 페이지 클릭 이동 실패");
            }
        }
        SetDarkTheme(true);ShowModding();return new{passed=true,surfaces=checks,scrollChecks=scrolls,adjacentButtonGapChecks=actionGaps};
    }
}
