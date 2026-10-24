using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal async Task<object> CheckModCardLayouts(Action<FrameworkElement,string,int,int,double> render)
    {
        var checks=new List<string>();var originalFont=UiFonts.Current.Id;
        try
        {
            // Reproduce the CI runner's native window constraint on any desktop.
            // The exported viewport must really be 1760x920, not a small window
            // surrounded by empty pixels in a larger PNG.
            var maximumWidth=MaxWidth;var maximumHeight=MaxHeight;
            try
            {
                MinWidth=0;MinHeight=0;MaxWidth=1044;MaxHeight=788;Width=1044;Height=788;
                UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                render((FrameworkElement)Content,"native-viewport-small-desktop",1760,920,1);
                if(Math.Abs(((FrameworkElement)Content).ActualWidth-1760)>2||Math.Abs(((FrameworkElement)Content).ActualHeight-920)>2)
                    throw new Exception("작은 데스크톱 제약이 검수용 뷰포트를 축소함");
                CheckModdingLayout("작은 데스크톱 제약 · 실제 1760×920");
                checks.Add("작은 데스크톱의 native 창 제약에서도 요청한 검수 뷰포트와 보관함 두 줄 유지");
            }
            finally{MaxWidth=maximumWidth;MaxHeight=maximumHeight;}
            // Exercise the real card factory, with every finish and the reported name,
            // against the independently measured safe area of the 160 x 104 frame.
            foreach(var font in UiFonts.Choices)
            foreach(var dark in new[]{true,false})
            foreach(var scale in new[]{1d,1.25,1.5})
            {
                UiFonts.Apply(font.Id);SetDarkTheme(dark);
                var (gallery,cards)=RangedPage.CardGalleryForCheck();
                var height=(int)gallery.Height;
                render(gallery,$"cards-{font.Id}-{dark}-{scale}",1356,height,scale);
                foreach(var card in cards)CheckFrame(card,$"{font.Id}/{dark}/{scale}",checks);
                var (meleeGallery,meleeCards)=board.CardGalleryForCheck();
                render(meleeGallery,$"melee-cards-{font.Id}-{dark}-{scale}",1356,(int)meleeGallery.Height,scale);
                foreach(var card in meleeCards)CheckFrame(card,$"melee/{font.Id}/{dark}/{scale}",checks);
                var icons=PolarityGallery();render(icons,$"polarity-{font.Id}-{dark}-{scale}",720,100,scale);
                foreach(var icon in LayoutDiagnostics.Tree(icons).OfType<System.Windows.Shapes.Path>())
                {
                    var viewport=(FrameworkElement)VisualTreeHelper.GetParent(icon);
                    var ink=icon.Data.GetRenderBounds(icon.Stroke is null?null:new Pen(icon.Stroke,icon.StrokeThickness));
                    if(!new Rect(0,0,viewport.ActualWidth,viewport.ActualHeight).Contains(ink))throw new Exception($"극성 원본 문양이 뷰포트 밖으로 잘림: {ink} / {viewport.ActualWidth}x{viewport.ActualHeight} / {icon.Data}");
                }
                checks.Add($"극성 7종·빈 슬롯·옴니 / {font.Id}/{dark}/{scale}: 문양·코스트 정렬");
            }
            UiFonts.Apply(originalFont);SetDarkTheme(true);
            var reported=board.ReportedCardForCheck();render(reported,"reported-fixed-inset",220,92,1);var fixedPadding=reported.Padding;
            reported.Padding=new(fixedPadding.Left,6,fixedPadding.Right,6);
            render(reported,"reported-old-inset",220,92,1);
            bool caught=false;
            try{CheckFrame(reported,"reported-old-inset",new());}
            catch(Exception ex)when(ex.Message.StartsWith("장식선")){caught=true;}
            if(!caught)throw new Exception("기존 위쪽 6px 여백의 극성/프레임 겹침을 검사에서 놓침");
            reported.Padding=fixedPadding;render(reported,"reported-fixed-inset",220,92,1);CheckFrame(reported,"reported-fixed-inset",checks);
            checks.Add("사용자가 보고한 갈바나이즈드 리플렉스: 기존 여백 실패·수정 여백 통과");
            ShowModding();
            foreach(var editing in new[]{false,true})
            {
                FormaForCheck(editing);
                foreach(var scale in new[]{1d,1.25,1.5})
                {
                    render((FrameworkElement)Content,$"melee-forma-{editing}-{scale}",1760,920,scale);
                    CheckModdingLayout($"melee/{editing}/{scale}");
                    foreach(var card in LayoutDiagnostics.Tree(moddingEditor!).OfType<Border>().Where(c=>c.Child is ModCardFace))
                        CheckFrame(card,$"melee/{editing}/{scale}",checks);
                }
            }
            FormaForCheck(false);
            foreach(var kind in new[]{"primary","secondary"})
            {
                ShowRanged(kind);var page=rangedPages[kind];UpdateLayout();
                await page.CheckFilteredCardAnchor();checks.Add(kind+": 검색 결과 한 장도 보관함 맨 위에 고정");
                page.StressLongNames(true);
                try
                {
                    foreach(var (width,height) in new[]{(1760,920),(2560,1080)})
                    foreach(var scale in new[]{1d,1.25,1.5})
                    {
                        Width=width;Height=height;UpdateLayout();
                        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                        render((FrameworkElement)Content,$"{kind}-{width}-{scale}",width,height,scale);
                        page.CheckVisibleCardRows();
                        foreach(var card in LayoutDiagnostics.Tree(page).OfType<Button>().Where(b=>b.Content is ModCardFace))
                            CheckFrame(card,$"{kind}/{width}/{scale}",checks);
                        checks.Add($"{kind}/{width}/{scale}: 보관함 두 줄 전체 표시·가로 스크롤 없음");
                        if(workspaceViewport.ScrollableWidth>1)throw new Exception("카드 검사: 최소 창 가로 스크롤");
                    }
                }
                finally{page.StressLongNames(false);}
            }
        }
        finally{UiFonts.Apply(originalFont);SetDarkTheme(true);}
        return new{passed=true,method="실제 카드·프레임 경계 측정, 글꼴 3종 × 테마 2종 × 배율 3종, 주/보조 최소/넓은 창",checks};
    }
    private static Grid PolarityGallery()
    {
        var gallery=new Grid();gallery.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");
        foreach(var code in new[]{"AP_ATTACK","AP_DEFENSE","AP_TACTIC","AP_POWER","AP_WARD","AP_PRECEPT","AP_UMBRA","None","Omni"})
        {
            var column=gallery.ColumnDefinitions.Count;gallery.ColumnDefinitions.Add(new(){Width=new(80)});
            var stack=new StackPanel{Margin=new(8)};
            var title=new TextBlock{Text=ModBoard.PolarityLabel(code),FontSize=12,Margin=new(0,0,0,8),TextWrapping=TextWrapping.NoWrap};stack.Children.Add(title);
            foreach(var color in new[]{"AccentBrush","CapacityGoodBrush","ErrorBrush"})stack.Children.Add(ModCardLayout.Header("12",code,color));
            Grid.SetColumn(stack,column);gallery.Children.Add(stack);
        }
        return gallery;
    }
    private static void CheckFrame(FrameworkElement card,string context,List<string> checks)
    {
        var face=(ModCardFace)(card is Button b?b.Content:((Border)card).Child);
        var mod=card.Tag is ValueTuple<RangedMod,int> data?data.Item1:null;
        var finish=mod is null?(ModFinish)card.Tag:ModAppearance.ForId(mod.Id);
        Rect Bounds(FrameworkElement child)=>child.TransformToAncestor(card).TransformBounds(new Rect(child.RenderSize));
        var name=Bounds(face.ModName);var header=Bounds(face.Metadata);
        var inset=card.ActualWidth*(finish==ModFinish.Amalgam?13.8:7.35)/160;
        var bottom=card.ActualHeight*97/104;
        var id=$"{context}/{face.ModName.Text}/{card.ActualWidth:0}/{face.LibraryCard}";
        if(name.Left<inset+1||name.Right>card.ActualWidth-inset-1||name.Bottom>bottom||name.Top<header.Bottom-.1)
            throw new Exception($"프레임/이름/코스트 겹침 {id}: name={name}, header={header}, inset={inset}, bottom={bottom}");
        if(face.ModName.ActualHeight>face.ModName.LineHeight*2+1/VisualTreeHelper.GetDpi(card).DpiScaleY||face.ModName.ActualWidth<50)
            throw new Exception("두 줄 이름 영역 부족: "+id);
        if(mod is not null&&(card.ToolTip is not string tip||!tip.StartsWith(mod.Name,StringComparison.Ordinal)))
            throw new Exception("말줄임 이름의 전체 툴팁 누락: "+id);
        if(header.Right>card.ActualWidth-inset||header.Left<inset)
            throw new Exception("코스트·극성이 프레임 침범: "+id);
        // Inspect the painted lines, including diagonal corner ornaments, rather than
        // merely testing the horizontal extent of the background image.
        var brush=(DrawingBrush)Application.Current.FindResource(ModAppearance.BackgroundKey(finish));
        var drawing=(DrawingGroup)brush.Drawing;
        var matrix=new MatrixTransform(card.ActualWidth/160,0,0,card.ActualHeight/104,0,0);
        var content=new[]{face.ModName,face.Metadata}.Concat(face.Children.OfType<Button>());
        foreach(var element in content)
        {
            var clearance=Bounds(element);clearance.Inflate(2,2);
            foreach(var ornament in drawing.Children.OfType<GeometryDrawing>().Where(d=>d.Pen is not null))
            {
                var ink=ornament.Geometry.GetWidenedPathGeometry(ornament.Pen);ink.Transform=matrix;
                if(ink.FillContainsWithDetail(new RectangleGeometry(clearance))!=IntersectionDetail.Empty)
                    throw new Exception($"장식선과 2px 여백 부족: {id} / {element.GetType().Name} / {clearance}");
            }
        }
        var meta=(Panel)face.Metadata;var cost=Bounds((FrameworkElement)meta.Children[0]);var polarity=Bounds((FrameworkElement)meta.Children[1]);
        if(Math.Abs((cost.Top+cost.Bottom-polarity.Top-polarity.Bottom)/2)>.8||polarity.Left-cost.Right<4)
            throw new Exception("코스트·극성 중심선 또는 간격 불일치: "+id);
        if(face.Children.OfType<Button>().Any(button=>Bounds(button).IntersectsWith(name)||Bounds(button).IntersectsWith(header)))
            throw new Exception("포르마 버튼이 이름·극성에 겹침: "+id);
        checks.Add(id+": 장식선 2px 여백·이름·코스트·극성 중심선·포르마 영역 검사");
    }
}

internal sealed partial class RangedPage
{
    internal static (Grid Gallery,Button[] Cards) CardGalleryForCheck()
    {
        var mods=RangedEngine.ModCatalog.Mods;
        var examples=mods.GroupBy(m=>ModAppearance.ForId(m.Id)).Select(g=>g.OrderByDescending(m=>m.Name.Length).First())
            .Concat(mods.Where(m=>m.EnglishName is "Expel Infested" or "Primed Expel Infested" or "Galvanized Diffusion"))
            .DistinctBy(m=>m.Id).ToArray();
        var gallery=new Grid{Height=examples.Length*100,UseLayoutRounding=true};
        gallery.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");
        var cards=new List<Button>();
        foreach(var width in new[]{144,200,310})for(int i=0;i<2;i++)gallery.ColumnDefinitions.Add(new(){Width=new(width+8)});
        for(int row=0;row<examples.Length;row++)
        {
            gallery.RowDefinitions.Add(new(){Height=new(100)});
            for(int col=0;col<6;col++)
            {
                var card=new Button{Margin=new(4),VerticalAlignment=VerticalAlignment.Top};
                Decorate(card,examples[row],examples[row].MaxRank,libraryCard:col%2==1);
                Grid.SetRow(card,row);Grid.SetColumn(card,col);gallery.Children.Add(card);cards.Add(card);
            }
        }
        return (gallery,cards.ToArray());
    }
    internal void CheckVisibleCardRows()
    {
        var cards=library.Children.OfType<Button>().Take(library.Columns*2).ToArray();
        if(cards.Length<library.Columns*2)throw new Exception("두 줄 검사에 필요한 보관함 카드 부족");
        foreach(var card in cards)
        {
            var box=card.TransformToAncestor(libraryScroller).TransformBounds(new Rect(card.RenderSize));
            if(box.Top<-.5||box.Bottom>libraryScroller.ViewportHeight+.5)
                throw new Exception($"{category}: 보관함 두 줄이 보이지 않음 {box} / {libraryScroller.ViewportHeight}");
        }
    }
    internal async Task CheckFilteredCardAnchor()
    {
        var before=query.Text;
        var first=(ValueTuple<RangedMod,int>)library.Children.OfType<Button>().First().Tag;
        var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(query)!;
        var value=(System.Windows.Automation.Provider.IValueProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Value);
        try
        {
            value.SetValue(first.Item1.EnglishName);
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            var result=library.Children.OfType<Button>().Single();
            var top=result.TranslatePoint(new(),libraryScroller).Y;
            if(top<-.5||top>12)throw new Exception($"{category}: 검색 결과 한 장이 보관함 위쪽에서 벗어남 ({top})");
        }
        finally{value.SetValue(before);UpdateLayout();}
    }
}

public sealed partial class ModBoard
{
    internal Border ReportedCardForCheck()
    {
        var mod=catalog.Mods.Single(m=>m.EnglishName=="Galvanized Reflex");var before=slotPolarities[0];
        try
        {
            slotPolarities[0]=mod.Polarity;var card=CreateCard(mod,mod.MaxRank,0);card.Width=220;
            card.SetResourceReference(Border.BorderBrushProperty,"AccentBrush");return card;
        }
        finally{slotPolarities[0]=before;}
    }
    internal (Grid Gallery,Border[] Cards) CardGalleryForCheck()
    {
        var examples=catalog.Mods.GroupBy(ModAppearance.For).Select(g=>g.OrderByDescending(m=>m.Name.Length).First())
            .Concat(catalog.Mods.Where(m=>m.EnglishName is "Galvanized Reflex" or "Sacrificial Steel" or "Sacrificial Pressure")).DistinctBy(m=>m.Id).ToArray();
        var gallery=new Grid{Height=examples.Length*100,UseLayoutRounding=true};gallery.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");
        var cards=new List<Border>();
        foreach(var width in new[]{144,200,310})for(int i=0;i<2;i++)gallery.ColumnDefinitions.Add(new(){Width=new(width+8)});
        for(int row=0;row<examples.Length;row++)
        {
            gallery.RowDefinitions.Add(new(){Height=new(100)});
            for(int col=0;col<6;col++)
            {
                var card=CreateCard(examples[row],examples[row].MaxRank,null,col%2==1);card.Margin=new(4);card.VerticalAlignment=VerticalAlignment.Top;
                Grid.SetRow(card,row);Grid.SetColumn(card,col);gallery.Children.Add(card);cards.Add(card);
            }
        }
        return (gallery,cards.ToArray());
    }
}
