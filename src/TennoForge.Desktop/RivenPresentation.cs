using System.Windows.Documents;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

// Shared visual language for melee and firearm Rivens. No game arithmetic lives here.
internal static class RivenPresentation
{
    internal static TextBlock Text(string value,double size=15,string brush="TextBrush")
    {
        var t=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,LineHeight=Math.Ceiling(size*1.5),Margin=new(0,0,0,8)};
        if(size>=18)t.FontWeight=FontWeights.SemiBold;Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);
        t.SetResourceReference(TextBlock.ForegroundProperty,brush);return t;
    }
    internal static StackPanel Stack(params UIElement[] children){var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p;}
    internal static Border Rule(UIElement child)
    {
        var b=new Border{Child=child,BorderThickness=new(0,0,0,1),Padding=new(0,14,0,18),Margin=new(0,0,0,8)};
        b.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");return b;
    }
    internal static FrameworkElement Range(string name,string unit,double min,double max,double? value,bool negative)
    {
        RollGrade? grade=null;string? error=null;
        if(value is {} v)try{grade=RivenEngine.Grade(v,min,max);}catch(ArgumentException){error="범위 밖의 값 · 옵션 구성과 랭크를 확인하세요.";}
        var title=new DockPanel();var current=Text(value is {} number?$"{(negative?"−":"+")}{number:0.###}{unit}":"수치 입력 전",24,value is null?"MutedBrush":"AccentBrush");
        current.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(current,Dock.Right);title.Children.Add(current);title.Children.Add(Text(name+(negative?" · 음옵":""),17));
        var strip=new Grid{Height=12,Margin=new(1,10,1,10)};strip.SetResourceReference(Panel.BackgroundProperty,"StrokeBrush");
        if(grade is {} g)
        {
            var fill=new Border{HorizontalAlignment=HorizontalAlignment.Left};fill.SetResourceReference(Border.BackgroundProperty,"AccentBrush");strip.Children.Add(fill);
            var band=new Border{Height=22,Opacity=.35,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};band.SetResourceReference(Border.BackgroundProperty,"AccentBrush");strip.Children.Add(band);
            var marker=new Border{Width=3,Height=22,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};marker.SetResourceReference(Border.BackgroundProperty,"TextBrush");strip.Children.Add(marker);
            strip.SizeChanged+=(_,_)=>{var w=strip.ActualWidth;fill.Width=w*g.Position;band.Width=Math.Min(w,Math.Max(2,w*(g.PositionHigh-g.PositionLow)));band.Margin=new(Math.Clamp(w*g.PositionLow,0,Math.Max(0,w-band.Width)),0,0,0);marker.Margin=new(Math.Clamp(w*g.Position-1.5,0,Math.Max(0,w-3)),0,0,0);};
        }
        var ends=new DockPanel();var maximum=Text($"{(negative?"크기 최대":"최대")}  {max:0.###}{unit}",14,"MutedBrush");maximum.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(maximum,Dock.Right);ends.Children.Add(maximum);ends.Children.Add(Text($"{(negative?"크기 최소":"최소")}  {min:0.###}{unit}",14,"MutedBrush"));
        var differenceUnit=unit=="%"?"%p":unit;
        string detail=error??(grade is {} g2?$"범위 위치 {g2.Position:P1}  ·  "+(negative?(g2.MinCompatible?"최소 표시값 가능":$"최소 크기까지 {Math.Max(0,value!.Value-min):0.###}{differenceUnit}"):(g2.MaxCompatible?"최대 표시값 가능":$"최대까지 {Math.Max(0,max-value!.Value):0.###}{differenceUnit}")):"게임 표시값을 입력하면 현재 위치가 나타납니다.");
        if(negative)detail+=" · 왼쪽일수록 음옵 크기 감소";
        var panel=Stack(title,strip,ends,Text(detail,14,error is null?"MutedBrush":"ErrorBrush"));panel.ToolTip="최소·최대와의 차이에서 %p는 퍼센트 수치 사이의 차이입니다. 예: 100% → 110%는 10%p.";
        System.Windows.Automation.AutomationProperties.SetName(panel,$"{name}, 최소 {min}, 최대 {max}, {detail}");return Rule(panel);
    }
    internal static FrameworkElement Recipe(string ingredients,string result,string range,bool negative)
        =>Rule(Stack(Text(ingredients,14,"MutedBrush"),Text("↓  "+result,20,"AccentBrush"),Text(range,25),Text($"남는 옵션은 유지 · 대체 {(negative?"음옵":"양옵")} 1개 추가",14),Text("대체 옵션의 종류·수치는 무작위입니다. 위 수치 구간은 반올림 오차를 포함한 계승 범위입니다.",13,"MutedBrush")));
}

internal sealed class RivenSections : Grid
{
    private readonly List<RadioButton> buttons=[];
    private readonly List<UIElement> pages=[];
    internal int SelectedIndex {get;private set;}
    internal RivenSections(params (string Title,UIElement Page)[] sections)
    {
        RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var nav=new WrapPanel{Margin=new(0,0,0,18)};Children.Add(nav);var group="riven-"+Guid.NewGuid();
        for(int i=0;i<sections.Length;i++)
        {
            int index=i;var button=new RadioButton{Content=sections[i].Title,GroupName=group,MinWidth=86,Height=40,Style=(Style)Application.Current.FindResource("NavigationItem"),Margin=new(0,0,16,0),Padding=new(6,10,6,10)};
            button.Checked+=(_,_)=>Select(index);buttons.Add(button);nav.Children.Add(button);
            var page=sections[i].Page;pages.Add(page);Grid.SetRow(page,1);Children.Add(page);
        }
        Select(0);
    }
    internal void Select(int index)
    {
        SelectedIndex=index;for(int i=0;i<pages.Count;i++){pages[i].Visibility=i==index?Visibility.Visible:Visibility.Collapsed;buttons[i].IsChecked=i==index;}
    }
}

internal record RivenReferenceEntry(string Name,string Search,string Unit,double Minimum,double Maximum);
internal sealed class RivenReferenceBrowser : Grid
{
    private readonly ComboBox shape=new(){ItemsSource=new[]{"양옵 2개","양옵 2개 + 음옵 1개","양옵 3개","양옵 3개 + 음옵 1개"},SelectedIndex=0};
    private readonly ComboBox kind=new(){ItemsSource=new[]{"일반 양옵","일반 음옵","접합 옵션"},SelectedIndex=0};
    private readonly TextBox search=new(){ToolTip="한국어·영문 옵션 이름 검색"};
    private readonly StackPanel listing=new();
    private Grid? rangeTable;
    private readonly Func<int,int,bool,bool,IEnumerable<RivenReferenceEntry>> provider;
    internal int VisibleEntries {get;private set;}
    private bool customShape,followingShape;
    internal RivenReferenceBrowser(Func<int,int,bool,bool,IEnumerable<RivenReferenceEntry>> provider,IEnumerable<(string Ingredients,string Result)> recipes)
    {
        this.provider=provider;RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var filters=new Grid();for(int i=0;i<3;i++)filters.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var controls=new UIElement[]{RivenPresentation.Stack(RivenPresentation.Text("옵션 구성"),shape),RivenPresentation.Stack(RivenPresentation.Text("확인할 옵션"),kind),RivenPresentation.Stack(RivenPresentation.Text("옵션 검색"),new SearchField(search,"옵션 이름 검색"))};
        for(int i=0;i<3;i++){((FrameworkElement)controls[i]).Margin=new(0,0,12,0);Grid.SetColumn(controls[i],i);filters.Children.Add(controls[i]);}
        Children.Add(RivenPresentation.Stack(RivenPresentation.Text("옵션 도감",22),RivenPresentation.Text("왼쪽의 현재 랭크 적용 · 구성 선택은 내 리벤을 변경하지 않습니다.",14,"MutedBrush"),filters));
        var body=new StackPanel();
        var guide=new StackPanel();foreach(var recipe in recipes)guide.Children.Add(RivenPresentation.Rule(RivenPresentation.Stack(RivenPresentation.Text(recipe.Result,17,"AccentBrush"),RivenPresentation.Text(recipe.Ingredients,14,"MutedBrush"))));
        body.Children.Add(new Expander{Header="전체 접합 조합표",Content=guide,Margin=new(0,0,0,16)});body.Children.Add(listing);
        var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0),Margin=new(0,18,0,0)};Grid.SetRow(scroll,1);Children.Add(scroll);
        shape.SelectionChanged+=(_,_)=>{if(!followingShape)customShape=true;Refresh();};kind.SelectionChanged+=(_,_)=>{if(kind.SelectedIndex==1&&shape.SelectedIndex%2==0)shape.SelectedIndex++;Refresh();};search.TextChanged+=(_,_)=>Refresh();Refresh();
    }
    internal void Refresh()
    {
        listing.Children.Clear();rangeTable=null;VisibleEntries=0;var positives=shape.SelectedIndex<2?2:3;var negatives=shape.SelectedIndex%2;
        if(kind.SelectedIndex==1&&negatives==0){listing.Children.Add(RivenPresentation.Text("음옵 범위를 보려면 ‘음옵 1개’ 구성을 선택하세요.",16));return;}
        try
        {
            if(kind.SelectedIndex==1)listing.Children.Add(RivenPresentation.Text("음옵은 부호를 뺀 크기입니다. 실제 유불리는 옵션과 빌드에 따라 다릅니다.",14,"MutedBrush"));
            var entries=provider(positives,negatives,kind.SelectedIndex==1,kind.SelectedIndex==2).Where(e=>(e.Name+" "+e.Search).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase));
            // One table owns the column widths for every row. Independent row
            // grids can resolve star widths differently while measuring text.
            rangeTable=new Grid();
            foreach(var width in new[]{1.4,1,1})rangeTable.ColumnDefinitions.Add(new(){Width=new(width,GridUnitType.Star)});
            void Row(string name,string minimum,string maximum,bool header=false)
            {
                var row=rangeTable.RowDefinitions.Count;rangeTable.RowDefinitions.Add(new(){Height=GridLength.Auto,MinHeight=44});
                var labels=new[]{name,minimum,maximum};
                for(int i=0;i<3;i++)
                {
                    var cell=RivenPresentation.Text(labels[i],header?14:16,header?"MutedBrush":i==2?"AccentBrush":"TextBrush");
                    cell.Margin=new(i==0?0:12,10,12,10);cell.VerticalAlignment=VerticalAlignment.Center;cell.HorizontalAlignment=i==0?HorizontalAlignment.Stretch:HorizontalAlignment.Right;
                    Grid.SetRow(cell,row);Grid.SetColumn(cell,i);rangeTable.Children.Add(cell);
                }
                var rule=new Border{Height=1,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};rule.SetResourceReference(Border.BackgroundProperty,"StrokeBrush");
                Grid.SetRow(rule,row);Grid.SetColumnSpan(rule,3);rangeTable.Children.Add(rule);
            }
            Row("옵션","최소","최대",true);
            foreach(var e in entries){Row(e.Name,$"{e.Minimum:0.###}{e.Unit}",$"{e.Maximum:0.###}{e.Unit}");VisibleEntries++;}
            if(VisibleEntries>0)listing.Children.Add(rangeTable);
            if(VisibleEntries==0)listing.Children.Add(RivenPresentation.Text("일치하는 옵션이 없습니다. 검색어를 줄여 보세요.",16));
        }catch(ArgumentException ex){listing.Children.Add(RivenPresentation.Text(ex.Message,14,"ErrorBrush"));}
    }
    internal IEnumerable<LayoutDiagnostics.Issue> ColumnIssues()
    {
        if(rangeTable is not {ActualWidth:>0} table||VisibleEntries==0)yield break;
        foreach(var column in new[]{1,2})
        {
            var cells=table.Children.OfType<TextBlock>().Where(t=>Grid.GetColumn(t)==column).ToArray();
            var right=cells.Select(t=>t.TranslatePoint(new(t.ActualWidth,0),table).X).ToArray();
            if(right.Max()-right.Min()>1)yield return new("riven-column-alignment",column==1?"최소":"최대",right.Max()-right.Min(),1,"RivenReferenceBrowser");
        }
    }
    internal void FollowComposition(int positive,int negative)
    {
        if(customShape||positive is <2 or >3||negative is <0 or >1)return;
        followingShape=true;try{shape.SelectedIndex=(positive==3?2:0)+negative;}finally{followingShape=false;}
    }
    internal void CheckFilters()
    {
        var previousShape=shape.SelectedIndex;var previousKind=kind.SelectedIndex;var wasCustom=customShape;
        customShape=false;FollowComposition(3,1);if(shape.SelectedIndex!=3)throw new InvalidOperationException("현재 리벤 구성의 도감 초기값 오류");
        shape.SelectedIndex=0;FollowComposition(2,1);if(shape.SelectedIndex!=0)throw new InvalidOperationException("사용자가 선택한 도감 구성이 덮어써짐");
        search.Text="존재하지않는옵션";if(VisibleEntries!=0)throw new InvalidOperationException("도감 검색 필터 실패");search.Text="";
        kind.SelectedIndex=1;shape.SelectedIndex=0;if(VisibleEntries!=0)throw new InvalidOperationException("음옵 없는 구성의 범위 표시 오류");shape.SelectedIndex=1;if(VisibleEntries==0)throw new InvalidOperationException("음옵 범위 누락");kind.SelectedIndex=2;if(VisibleEntries==0)throw new InvalidOperationException("접합 범위 누락");kind.SelectedIndex=previousKind;shape.SelectedIndex=previousShape;customShape=wasCustom;
    }
}
