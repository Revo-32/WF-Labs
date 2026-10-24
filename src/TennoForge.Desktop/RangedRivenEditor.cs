using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RangedRivenEditor : Border
{
    private sealed record Choice(string Key,string Name);
    private readonly ReferenceWeapon weapon;
    private readonly bool bindToCurrentBuild;
    private readonly ComboBox rank=new(){ItemsSource=Enumerable.Range(0,9).ToArray(),SelectedIndex=8};
    private readonly TextBox cost=new(){Text="10"};
    private readonly ComboBox polarity=new(){ItemsSource=new[]{new Choice("Madurai","마두라이"),new Choice("Vazarin","바자린"),new Choice("Naramon","나라몬")},DisplayMemberPath="Name",SelectedIndex=0};
    private readonly (ComboBox Stat,TextBox Value,ComboBox Sign)[] rows=new (ComboBox,TextBox,ComboBox)[4];
    private readonly StackPanel charts=new(),splices=new();
    private readonly FrameworkElement[] endpoints=new FrameworkElement[4];
    private readonly TextBlock message=Text("");
    private readonly Button apply=new(){Content="리벤 장착",IsEnabled=false,Padding=new(16,8,16,8)};
    private readonly RivenSections sections;
    private readonly RivenReferenceBrowser reference;
    private RangedRivenRoll? valid;
    internal RangedRivenRoll? ValidRoll=>valid;
    internal bool IsEmpty=>rows.All(r=>r.Stat.SelectedItem is Choice {Key:""}&&string.IsNullOrEmpty(r.Value.Text));
    private static TextBlock Text(string text,double size=14)=>RivenPresentation.Text(text,size);
    private static StackPanel Stack(params UIElement[] children)=>RivenPresentation.Stack(children);
    private static ScrollViewer Scroll(UIElement child)=>new(){Content=child,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0)};
    internal RangedRivenEditor(ReferenceWeapon weapon,RangedRivenRoll? initial,Action<RangedRivenRoll> accept,Action close,bool inline=false,Func<RangedBuild>? capture=null,Func<int>? destination=null)
    {
        if(initial is not null&&initial.Version!=RivenBalance.Version)throw new ArgumentException(RivenBalance.LegacyMessage);
        this.weapon=weapon;bindToCurrentBuild=inline;captureBuild=capture;captureDestination=destination??(()=>0);Width=1180;MaxHeight=810;Padding=new(24);BorderThickness=new(1);CornerRadius=new(6);
        if(inline){apply.Content="현재 모딩에 가정 적용";Width=double.NaN;MaxHeight=double.PositiveInfinity;BorderThickness=new(0);Padding=new(0);}UiHierarchy.Primary(apply);
        SetResourceReference(BackgroundProperty,"ResultsBackgroundBrush");SetResourceReference(BorderBrushProperty,"StrokeBrush");
        var root=new Grid();root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});Child=root;
        var header=new DockPanel{Margin=new(0,0,0,12)};var dismiss=new Button{Content="×",Padding=new(10,4,10,4),ToolTip="닫기"};dismiss.Click+=(_,_)=>close();DockPanel.SetDock(dismiss,Dock.Right);if(!inline)header.Children.Add(dismiss);header.Children.Add(Text(weapon.Name+" · 리벤",22));if(inline)header.Visibility=Visibility.Collapsed;root.Children.Add(Stack(header,message));
        var body=Stack(Text("내 리벤",20),Text("게임에 표시된 옵션 순서대로 입력"),Text("리벤 랭크 · 수치 범위에 적용"),rank);
        rank.Margin=new(0,0,0,14);
        var choices=new[]{new Choice("","옵션 선택")}.Concat(RangedRivens.Table.Attributes.Where(a=>a.Bases.ContainsKey(RangedEngine.ModCategory(weapon))).Select(a=>new Choice(a.Key,a.Name+(a.Spliced?" · 접합":"")+" ("+a.Unit+")"))).ToArray();
        for(int i=0;i<rows.Length;i++)
        {
            int index=i;var attribute=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedIndex=0,Margin=new(0,0,0,6)};
            var value=new TextBox{Margin=new(0,0,8,0),ToolTip="게임 표시값"};value.ToolTip="게임에 표시된 수치의 크기 · 부호는 옆에서 선택";
            var sign=new ComboBox{ItemsSource=new[]{"양옵 (+)","음옵 (−)"},SelectedIndex=0};rows[i]=(attribute,value,sign);
            var actions=new WrapPanel{Margin=new(0,5,0,8)};var endpointDetails=new Expander{Header="최소·최대값으로 시험",Content=actions};endpoints[i]=endpointDetails;
            foreach(var maximum in new[]{false,true}){bool end=maximum;var button=new Button{Content=end?"최대값 입력":"최소값 입력",Padding=new(8,3,8,3),Margin=new(0,0,6,0),ToolTip="선택한 구성의 경계값으로 비교할 가정을 입력합니다."};button.Click+=(_,_)=>SetEndpoint(index,end);actions.Children.Add(button);}
            body.Children.Add(new RivenOptionInput(i,attribute,value,sign,endpointDetails));
        }
        body.Children.Add(new Expander{Header="장착 설정 · 코스트 / 극성",Content=Stack(Text("0랭크 코스트"),cost,Text("극성"),polarity),Margin=new(0,4,0,12)});
        reference=new RivenReferenceBrowser(ReferenceRanges,RangedRivens.Table.Recipes.Select(r=>(RangedRivens.Attribute(r.First).Name+" + "+RangedRivens.Attribute(r.Second).Name,RangedRivens.Attribute(r.Result).Name)));
        var overview=Stack(Text("옵션 수치 분석",24),charts);
        var splice=Stack(Text("접합",24),Text("계승 범위를 확인하고, 대체 옵션이 포함된 예시를 만들어 비교하세요."),splices);
        sections=new RivenSections(("수치 확인",Scroll(overview)),("접합",Scroll(splice)),("옵션 도감",reference),("비교 · 순환",Scroll(CreateWorkshop())));
        var columns=new Grid();columns.ColumnDefinitions.Add(new(){Width=new(inline?1:1.05,GridUnitType.Star)});columns.ColumnDefinitions.Add(new(){Width=new(28)});columns.ColumnDefinitions.Add(new(){Width=new(inline?2.35:1.6,GridUnitType.Star)});
        columns.Children.Add(Scroll(body));Grid.SetColumn(sections,2);columns.Children.Add(sections);Grid.SetRow(columns,1);root.Children.Add(columns);
        var footer=new DockPanel{LastChildFill=false,Margin=new(0,16,0,0)};DockPanel.SetDock(apply,Dock.Right);footer.Children.Add(apply);
        var sources=new WrapPanel();foreach(var (label,url) in new[]{("위키 수치 기준",RangedRivens.Table.Source.Url),("44.1.3 패치",RivenBalance.Source)}){var button=new Button{Content=label,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,0,8,8),Padding=new(12,8,12,8)};button.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});sources.Children.Add(button);}
        overview.Children.Add(new Expander{Header="수치 범위 · 출처",Content=Stack(Text("전 무기 공통 배율 1.2 · Hotfix 44.1.3"),Text("막대는 최소·최대 사이의 위치입니다. 획득 확률이나 빌드 점수가 아닙니다."),sources),Margin=new(0,14,0,0)});
        Grid.SetRow(footer,2);root.Children.Add(footer);apply.Click+=(_,_)=>{try{if(valid is {} roll)accept(roll);}catch(ArgumentException ex){message.Text=ex.Message;message.Visibility=Visibility.Visible;}};
        if(initial is not null)
        {
            rank.SelectedIndex=initial.Rank;cost.Text=initial.BaseDrain?.ToString()??"";polarity.SelectedItem=polarity.Items.Cast<Choice>().SingleOrDefault(p=>p.Key==initial.Polarity);
            for(int i=0;i<Math.Min(initial.Traits.Length,4);i++){var t=initial.Traits[i];rows[i].Stat.SelectedItem=choices.Single(a=>a.Key==t.Stat);rows[i].Value.Text=t.Value.ToString(CultureInfo.InvariantCulture);rows[i].Sign.SelectedIndex=t.Negative?1:0;}
        }
        foreach(var row in rows){row.Stat.SelectionChanged+=(_,_)=>Refresh();row.Sign.SelectionChanged+=(_,_)=>Refresh();row.Value.TextChanged+=(_,_)=>Refresh();}rank.SelectionChanged+=(_,_)=>Refresh();cost.TextChanged+=(_,_)=>Refresh();polarity.SelectionChanged+=(_,_)=>Refresh();Refresh();sections.Select(0);
    }
    private RangedRivenRoll Read()=>new(weapon.Id,rank.SelectedIndex,rows.Where(r=>r.Stat.SelectedItem is Choice {Key:not ""}).Select(r=>{var key=((Choice)r.Stat.SelectedItem).Key;return new RangedRivenTrait(key,double.TryParse(r.Value.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)?v:double.NaN,r.Sign.SelectedIndex==1,RangedRivens.Attribute(key).Spliced);}).ToArray(),RangedRivens.Table.Version,int.TryParse(cost.Text,out var drain)?drain:null,(polarity.SelectedItem as Choice)?.Key);
    private void Refresh()
    {
        if(applyingWorkshop)return;
        InvalidateComparison();
        valid=null;apply.IsEnabled=false;for(int i=0;i<4;i++)endpoints[i].Visibility=rows[i].Stat.SelectedItem is Choice {Key:not ""}?Visibility.Visible:Visibility.Collapsed;charts.Children.Clear();splices.Children.Clear();reference.Refresh();
        try
        {
            var roll=Read();var shape=roll with{BaseDrain=0,Polarity="Madurai",Traits=roll.Traits.Select(t=>t with{Value=0}).ToArray()};RangedRivens.Validate(shape,false);reference.FollowComposition(shape.Traits.Count(t=>!t.Negative),shape.Traits.Count(t=>t.Negative));
            foreach(var trait in roll.Traits){var range=RangedRivens.Range(roll,trait);var attribute=RangedRivens.Attribute(trait.Stat);charts.Children.Add(RivenPresentation.Range(attribute.Name,attribute.Unit,range.Minimum,range.Maximum,double.IsFinite(trait.Value)?trait.Value:null,trait.Negative));}
            RangedRivens.Validate(roll,requireMetadata:false);message.Text="";
            try{RangedRivens.Validate(roll);valid=roll;apply.IsEnabled=session?.Pending is null;}catch(ArgumentException){message.Text="수치 분석 가능 · 장착·DPS 비교에는 코스트와 극성이 필요합니다.";}
            foreach(var preview in RangedRivens.Predict(roll))
            {
                splices.Children.Add(RivenPresentation.Recipe(RangedRivens.Attribute(roll.Traits[preview.First].Stat).Name+" + "+RangedRivens.Attribute(roll.Traits[preview.Second].Stat).Name,RangedRivens.Attribute(preview.Result).Name,$"{preview.Value.Low:0.###}–{preview.Value.High:0.###}{RangedRivens.Attribute(preview.Result).Unit}",preview.ReplacementNegative));
                var button=ActionButton("이 접합으로 예시 만들기",()=>WorkshopAction(state=>RangedRivenWorkshop.SpliceExample(state,preview,new Random())));button.IsEnabled=session?.Pending is null;splices.Children.Add(button);
            }
            if(splices.Children.Count==0)splices.Children.Add(Text("현재 옵션으로 가능한 신규 접합이 없습니다. 옵션 도감의 전체 조합표를 확인하세요."));
        }
        catch(ArgumentException ex)
        {
            message.Text=IsEmpty?"":ex.Message;if(charts.Children.Count==0)charts.Children.Add(UiHierarchy.RivenStart(()=>{rows[0].Stat.BringIntoView();rows[0].Stat.Focus();rows[0].Stat.IsDropDownOpen=true;},()=>sections.Select(2)));
            splices.Children.Add(Text("옵션 2~3개와 게임 표시값을 입력하면 가능한 접합이 나타납니다.",16));
        }
        message.Visibility=string.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;
        RefreshWorkshopControls();
    }
    private void SetEndpoint(int index,bool maximum)
    {
        try{var shape=Read() with{BaseDrain=0,Polarity="Madurai"};shape=shape with{Traits=shape.Traits.Select(t=>t with{Value=0}).ToArray()};RangedRivens.Validate(shape,false);if(rows[index].Stat.SelectedItem is not Choice {Key:not ""} choice)return;var range=RangedRivens.Range(shape,shape.Traits.Single(t=>t.Stat==choice.Key));rows[index].Value.Text=Math.Round(maximum?range.Maximum:range.Minimum,1).ToString(CultureInfo.InvariantCulture);}catch(ArgumentException ex){message.Text=ex.Message;}
    }
    private IEnumerable<RivenReferenceEntry> ReferenceRanges(int positive,int negative,bool isNegative,bool spliced)
    {
        var shape=new RangedRivenRoll(weapon.Id,rank.SelectedIndex,Enumerable.Range(0,positive).Select(_=>new RangedRivenTrait("Damage",0)).Concat(Enumerable.Range(0,negative).Select(_=>new RangedRivenTrait("Zoom",0,true))).ToArray(),RivenBalance.Version);
        foreach(var attribute in RangedRivens.Table.Attributes.Where(a=>a.Spliced==spliced&&a.Bases.ContainsKey(RangedEngine.ModCategory(weapon))&&(!isNegative||!a.PositiveOnly&&!a.Spliced)))
        {var range=RangedRivens.Range(shape,new(attribute.Key,0,isNegative,spliced));yield return new(attribute.Name,attribute.Key,attribute.Unit,range.Minimum,range.Maximum);}
    }
    internal void SelectSectionForCheck(int index)=>sections.Select(index);
    internal void CheckAndApply()
    {
        if(!apply.IsEnabled||valid is null||charts.Children.Count!=valid.Traits.Length)throw new InvalidOperationException("총기 리벤 편집·범위 그래프 구성 실패");
        var original=rows[0].Value.Text;rows[0].Value.Text="잘못된 값";if(apply.IsEnabled)throw new InvalidOperationException("잘못된 리벤 입력이 장착 가능");
        rows[0].Value.Text="";if(charts.Children.Count!=Read().Traits.Length)throw new InvalidOperationException("빈 값에서 범위 표시 누락");
        rows[0].Value.Text=original;reference.CheckFilters();for(int i=0;i<3;i++)sections.Select(i);sections.Select(0);
        if(!apply.IsEnabled)throw new InvalidOperationException("리벤 입력 복원 실패");apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
