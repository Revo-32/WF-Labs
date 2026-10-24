using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace TennoForge.Desktop;
internal static class LayoutDiagnostics
{
    internal record Issue(string Kind,string Text,double Actual,double Required,string Parent);
    internal static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree(VisualTreeHelper.GetChild(root,i)))yield return child;}
    internal static void SelfCheck()
    {
        var panel=new Grid{Width=160,Height=60};panel.Children.Add(new Button{Content="첫 버튼"});panel.Children.Add(new Button{Content="두 번째 버튼"});
        panel.Measure(new Size(160,60));panel.Arrange(new Rect(0,0,160,60));panel.UpdateLayout();
        if(!Inspect(panel).Any(i=>i.Kind=="sibling-overlap"))throw new Exception("검사기: 겹친 형제 요소를 놓쳤습니다.");
        var text=new TextBlock{Text="잘린 한국어 텍스트",FontSize=20,Height=10};text.Measure(new Size(160,10));text.Arrange(new Rect(0,0,160,10));
        if(!Inspect(text).Any(i=>i.Kind=="text-height"))throw new Exception($"검사기: 잘린 텍스트를 놓쳤습니다. actual={text.ActualWidth}x{text.ActualHeight}; desired={text.DesiredSize}; render={text.RenderSize}; clip={System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(text)?.Bounds}");
    }
    internal static Issue[] Inspect(FrameworkElement root)
    {
        var issues=new List<Issue>();
        bool Presented(FrameworkElement e){for(DependencyObject? p=e;p is not null;p=VisualTreeHelper.GetParent(p))if(p is UIElement ui&&ui.Visibility!=Visibility.Visible)return false;return e.ActualWidth>0&&e.ActualHeight>0;}
        foreach(var text in Tree(root).OfType<TextBlock>().Where(t=>Presented(t)&&!string.IsNullOrWhiteSpace(t.Text)&&t.TextTrimming==TextTrimming.None))
        {
            var probe=new TextBlock{Text=text.Text,FontFamily=text.FontFamily,FontSize=text.FontSize,FontWeight=text.FontWeight,FontStyle=text.FontStyle,FontStretch=text.FontStretch,TextWrapping=text.TextWrapping,LineHeight=text.LineHeight,LineStackingStrategy=text.LineStackingStrategy,Padding=text.Padding};
            TextOptions.SetTextFormattingMode(probe,TextOptions.GetTextFormattingMode(text));
            VisualTreeHelper.SetRootDpi(probe,VisualTreeHelper.GetDpi(text));
            Typography.SetNumeralAlignment(probe,Typography.GetNumeralAlignment(text));
            probe.Language=text.Language;
            // Ideal metrics are fractional; layout rounding may reduce the arranged width by
            // half a physical pixel. Re-measuring at that rounded-down width can invent
            // a second line which the original TextBlock never laid out.
            probe.UseLayoutRounding=text.UseLayoutRounding;
            var widthTolerance=text.UseLayoutRounding ? 0.5/VisualTreeHelper.GetDpi(text).DpiScaleX : 0;
            probe.Measure(new Size(text.TextWrapping==TextWrapping.NoWrap?double.PositiveInfinity:text.ActualWidth+widthTolerance+0.00001,double.PositiveInfinity));
            var clip=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(text)?.Bounds;
            if(clip is {} clipped&&clipped.Height+1<probe.DesiredSize.Height)issues.Add(new("text-height",text.Text,clipped.Height,probe.DesiredSize.Height,"layout-clip"));
            var parent=VisualTreeHelper.GetParent(text);var parentName=parent?.GetType().Name??"";
            if(probe.DesiredSize.Height>text.ActualHeight+1)issues.Add(new("text-height",text.Text,text.ActualHeight,probe.DesiredSize.Height,parentName));
            if(text.TextWrapping==TextWrapping.NoWrap&&probe.DesiredSize.Width>text.ActualWidth+1)issues.Add(new("text-width",text.Text,text.ActualWidth,probe.DesiredSize.Width,parentName));
            // A control's content may render outside its arranged box even when TextBlock itself reports enough room.
            for(var p=parent;p is FrameworkElement host;p=VisualTreeHelper.GetParent(p))
            {
                if(host is ScrollContentPresenter or ScrollViewer)break;
                if(host is Border or Grid or Control)
                {
                    var box=text.TransformToAncestor(host).TransformBounds(new Rect(text.RenderSize));
                    if(box.Top< -1||box.Bottom>host.ActualHeight+1)issues.Add(new("control-height",text.Text,host.ActualHeight,box.Height,host.GetType().Name));
                    if(box.Left< -1||box.Right>host.ActualWidth+1)issues.Add(new("control-width",text.Text,host.ActualWidth,box.Width,host.GetType().Name));
                    if(host is Control)break;
                }
            }
        }
        foreach(var grid in Tree(root).OfType<Grid>().Where(g=>Presented(g)&&g.TemplatedParent is null))
        {
            var children=grid.Children.OfType<FrameworkElement>().Where(e=>Presented(e)&&e.IsHitTestVisible&&e is not System.Windows.Shapes.Shape&&e is not Border {Child:null}).ToArray();
            for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++)
            {
                var a=children[i];var b=children[j];
                if(Panel.GetZIndex(a)!=Panel.GetZIndex(b))continue;
                var ra=a.TransformToAncestor(grid).TransformBounds(new Rect(a.RenderSize));var rb=b.TransformToAncestor(grid).TransformBounds(new Rect(b.RenderSize));
                var overlap=Rect.Intersect(ra,rb);
                if(!overlap.IsEmpty&&overlap.Width>2&&overlap.Height>2)issues.Add(new("sibling-overlap",a.GetType().Name+" / "+b.GetType().Name,overlap.Width,overlap.Height,grid.GetType().Name));
            }
        }
        foreach(var group in Tree(root).OfType<RivenOptionInput>().Where(Presented))
        {
            var valueTop=group.Value.TranslatePoint(new(),group).Y;
            var attributeBottom=group.Attribute.TranslatePoint(new(0,group.Attribute.ActualHeight),group).Y;
            if(valueTop-attributeBottom<5.5)issues.Add(new("option-input-gap",$"옵션 {group.Index+1}",valueTop-attributeBottom,6,"RivenOptionInput"));
            if(VisualTreeHelper.GetParent(group) is Panel parent)
            {
                var next=parent.Children.OfType<RivenOptionInput>().FirstOrDefault(g=>g.Index==group.Index+1);
                if(next is not null){var gap=next.TranslatePoint(new(),parent).Y-group.TranslatePoint(new(0,group.ActualHeight),parent).Y;if(gap<17.5)issues.Add(new("option-group-gap",$"옵션 {group.Index+1} → {next.Index+1}",gap,18,"RivenOptionInput"));}
            }
        }
        foreach(var browser in Tree(root).OfType<RivenReferenceBrowser>().Where(Presented))issues.AddRange(browser.ColumnIssues());
        return issues.Distinct().ToArray();
    }
}
public sealed partial class MainWindow
{
    internal IReadOnlyDictionary<string,LayoutDiagnostics.Issue[]> ProbeLayouts(Action<FrameworkElement,string,int,int> render)
    {
        var reports=new Dictionary<string,LayoutDiagnostics.Issue[]>();
        void Capture(FrameworkElement element,string name,int width=1760,int height=920)
        {
            render(element,name,width,height);reports[name]=LayoutDiagnostics.Inspect(element);
            var scrollers=LayoutDiagnostics.Tree(element).OfType<ScrollViewer>().Where(s=>s.Visibility==Visibility.Visible&&s.ViewportHeight>0&&s.ScrollableHeight>1).ToArray();
            foreach(var scroll in scrollers){scroll.ScrollToEnd();scroll.UpdateLayout();}
            if(scrollers.Length>0){render(element,name+"-bottom",width,height);reports[name+"-bottom"]=LayoutDiagnostics.Inspect(element);}
            foreach(var scroll in scrollers){scroll.ScrollToTop();scroll.ScrollToLeftEnd();}element.UpdateLayout();
        }
        LayoutDiagnostics.SelfCheck();
        void Main(string name)=>Capture((FrameworkElement)Content,name);
        void InputStates(string prefix)
        {
            var groups=Descendants((DependencyObject)page.Content).OfType<RivenOptionInput>().ToArray();
            if(groups.Length!=4)throw new Exception(prefix+": 옵션 입력 묶음 4개 누락");
            foreach(var g in groups){g.Attribute.SelectedIndex=0;g.Value.Text="";g.Sign.SelectedIndex=0;}
            Main(prefix+"-empty");groups[0].Attribute.SelectedIndex=1;Main(prefix+"-partial");
            groups[0].Attribute.SelectedIndex=0;Main(prefix+"-cleared");
        }
        foreach(var kind in new[]{"Rifle","Shotgun","Pistol","Melee"})
        {
            OpenRivenClass(kind);Main(kind+"-first-open");InputStates(kind+"-first-input");
        }
        ShowModding();Main("melee");scenarioDrawer!.SetOpen(true,false);Main("melee-scenario");
        foreach(var expander in Descendants((DependencyObject)page.Content).OfType<Expander>().ToArray())expander.IsExpanded=true;
        Main("melee-expanded");scenarioDrawer.SetOpen(false,false);
        Capture((FrameworkElement)settingsPopup.Child,"settings",320,610);
        Capture((FrameworkElement)capacityPopup!.Child,"capacity",380,440);
        Capture(OpenZawBuilder(false),"zaw",440,520);
        foreach(var kind in new[]{"exilus","melee","zaw"}){OpenEquipmentPicker(kind,false);Capture((FrameworkElement)equipmentPicker!.Child,"equipment-"+kind,420,560);CloseEquipmentPicker();}
        foreach(var category in new[]{"primary","secondary"})
        {
            ShowRanged(category);Main(category);rangedPages[category].DrawerForCheck.SetOpen(true,false);Main(category+"-scenario");rangedPages[category].DrawerForCheck.SetOpen(false,false);rangedPages[category].StressLongNames(true);Main(category+"-long-mods");rangedPages[category].StressLongNames(false);rangedPages[category].DrawerForCheck.SetOpen(true,false);
            foreach(var expander in Descendants((DependencyObject)page.Content).OfType<Expander>().ToArray())expander.IsExpanded=true;
            Main(category+"-expanded");PrepareRangedRivenAudit(category);Main(category+"-modal");CloseRangedRivenAudit(category);PrepareRangedRivenWorkbenchAudit(category);
            for(int i=0;i<4;i++){SelectRangedRivenSectionForCheck(category,i);Main(category+"-riven-"+i);}
            SelectRangedRivenSectionForCheck(category,2);InputStates(category+"-riven");
        }
        ShowModding();ShowRivens();var id=catalog.Weapons.First().Id;
        SetWorkshop(new(new(id,8,[new(TennoForge.Core.RivenStat.Damage,195.7),new(TennoForge.Core.RivenStat.StatusChance,106.9)],TennoForge.Core.RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK")));
        for(int i=0;i<4;i++){SelectRivenSectionForCheck(i);Main("melee-riven-"+i);}
        SelectRivenSectionForCheck(2);InputStates("melee-riven");
        for(int i=0;i<5;i++){CatalogPreviewForCheck(i);Main("catalog-"+i);}
        ShowRoadmap();Main("roadmap");
        ShowModding();Capture((FrameworkElement)Content,"small-workarea",1280,680);
        var client=new ClientSettingsWindow(()=>{});Capture((FrameworkElement)client.Content,"client",560,540);client.PreviewForAudit(false);Capture((FrameworkElement)client.Content,"client-error",560,540);
        return reports;
    }
}
