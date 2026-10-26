using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class MasteryProgressView : Grid
{
    internal MasteryOverview? Model {get;private set;}
    private readonly ProgressRing ring=new(){Width=120,Height=120,IsHitTestVisible=false};
    private readonly TextBlock percent=Label("—",26,"AccentBrush");
    private readonly TextBlock total=Label("—",28),completed=Label("—",28),remaining=Label("—",28),caption=Label("게임에서 동기화하면 숙련 현황이 표시됩니다.",13,"MutedBrush");
    private readonly StackPanel categories=new();
    internal Action<int>? FilterRequested;
    internal MasteryProgressView()
    {
        ColumnDefinitions.Add(new(){Width=new(144)});ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var circle=new Grid{Width=120,Height=120,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};circle.Children.Add(ring);
        var center=new StackPanel{Width=112,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false};
        percent.TextAlignment=TextAlignment.Center;percent.HorizontalAlignment=HorizontalAlignment.Stretch;percent.Margin=new(0,0,0,4);center.Children.Add(percent);
        var subtitle=Label("마스터리 완료",12,"MutedBrush");subtitle.TextAlignment=TextAlignment.Center;subtitle.Margin=new(0);center.Children.Add(subtitle);circle.Children.Add(center);Children.Add(circle);
        var stats=new Grid{Margin=new(8,0,24,0),VerticalAlignment=VerticalAlignment.Center};stats.RowDefinitions.Add(new(){Height=GridLength.Auto});stats.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var numbers=new UniformGridShim();numbers.Add(Metric("전체 장비",total,()=>FilterRequested?.Invoke(2)));numbers.Add(Metric("완료",completed,()=>FilterRequested?.Invoke(3)));numbers.Add(Metric("남은 장비",remaining,()=>FilterRequested?.Invoke(0)));stats.Children.Add(numbers);caption.Margin=new(0,12,0,0);Grid.SetRow(caption,1);stats.Children.Add(caption);Grid.SetColumn(stats,1);Children.Add(stats);
        categories.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(categories,2);Children.Add(categories);
        ToolTip="전체는 현재 카탈로그의 마스터리 대상 장비입니다. 한정 장비도 포함하며, 40랭크 장비는 40까지 완료해야 숙련 완료로 집계합니다. 판매한 장비의 과거 이력도 포함합니다.";
    }
    private static TextBlock Label(string text,double size,string brush="TextBrush")=>RivenPresentation.Text(text,size,brush);
    private static Button Metric(string name,TextBlock number,Action click)
    {
        var body=new StackPanel();body.Children.Add(Label(name,13,"MutedBrush"));body.Children.Add(number);var button=new Button{Content=body,Padding=new(0),BorderThickness=new(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new(0,0,16,0)};button.Click+=(_,_)=>click();AutomationProperties.SetName(button,name+" 장비 보기");return button;
    }
    internal void Update(MasteryOverview model)
    {
        Model=model;total.Text=$"{model.Total:N0}";completed.Text=model.Completed?.ToString("N0")??"—";remaining.Text=model.Remaining?.ToString("N0")??"—";percent.Text=model.Completion is {} fraction?fraction is 0 or 1?$"{fraction:P0}":$"{fraction:P1}":"—";
        caption.Text=model.Completed is not null?$"진행 중 {model.InProgress:N0} · 미시작 {model.Unstarted:N0}\n현재 보유 {model.Owned?.ToString("N0")??"—"}종 · 과거 숙련 이력 포함":"동기화 후 완료·미완료를 집계합니다.";
        if(model.Completed is not null)
        {
            caption.Inlines.Clear();var partial=new Run($"진행 중 {model.InProgress:N0}");partial.SetResourceReference(TextElement.ForegroundProperty,"CapacityGoodBrush");caption.Inlines.Add(partial);
            caption.Inlines.Add(new Run($" · 미시작 {model.Unstarted:N0}\n현재 보유 {model.Owned?.ToString("N0")??"—"}종 · 과거 숙련 이력 포함"));
        }
        ring.Set(model);AutomationProperties.SetName(this,$"마스터리 전체 {total.Text}, 완료 {completed.Text}, 남은 장비 {remaining.Text}");categories.Children.Clear();
        foreach(var group in model.Groups)
        {
            var row=new Grid{Margin=new(0,0,0,5)};row.ColumnDefinitions.Add(new(){Width=new(72)});row.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new(68)});
            row.Children.Add(Label(group.Name,12,"MutedBrush"));var progress=new ProgressBar{Minimum=0,Maximum=Math.Max(1,group.Total),Value=group.Completed??0,Height=4,VerticalAlignment=VerticalAlignment.Center,Margin=new(8,0,12,4)};progress.SetResourceReference(Control.BackgroundProperty,"StrokeBrush");progress.SetResourceReference(Control.ForegroundProperty,"AccentBrush");Grid.SetColumn(progress,1);row.Children.Add(progress);
            var value=Label(group.Completed is {} done?$"{done} / {group.Total}":$"— / {group.Total}",12,"MutedBrush");value.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(value,2);row.Children.Add(value);categories.Children.Add(row);
        }
    }
    private sealed class UniformGridShim : Grid
    {
        internal void Add(UIElement child){ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetColumn(child,Children.Count);Children.Add(child);}
    }
    private sealed class ProgressRing : FrameworkElement
    {
        private static readonly DependencyProperty AccentProperty=DependencyProperty.Register("Accent",typeof(Brush),typeof(ProgressRing),new FrameworkPropertyMetadata(Brushes.Transparent,FrameworkPropertyMetadataOptions.AffectsRender));
        private MasteryOverview? model;
        internal ProgressRing(){SetResourceReference(AccentProperty,"AccentBrush");}
        internal void Set(MasteryOverview value){model=value;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            double radius=Math.Max(1,Math.Min(ActualWidth,ActualHeight)/2-7);var center=new Point(ActualWidth/2,ActualHeight/2);dc.DrawEllipse(null,new Pen((Brush)FindResource("StrokeBrush"),9),center,radius,radius);
            if(model?.Completion is not {} value)return;
            Arc(0,value,(Brush)FindResource("AccentBrush"));Arc(value,(model.InProgress??0)/(double)Math.Max(1,model.Total),(Brush)FindResource("CapacityGoodBrush"));
            void Arc(double start,double fraction,Brush brush)
            {
                if(fraction<=0)return;if(fraction>=.999999){dc.DrawEllipse(null,new Pen(brush,9),center,radius,radius);return;}
                Point At(double t)=>new(center.X+radius*Math.Cos(t*2*Math.PI-Math.PI/2),center.Y+radius*Math.Sin(t*2*Math.PI-Math.PI/2));
                var geometry=new StreamGeometry();using(var path=geometry.Open()){path.BeginFigure(At(start),false,false);path.ArcTo(At(start+fraction),new(radius,radius),0,fraction>.5,SweepDirection.Clockwise,true,false);}geometry.Freeze();dc.DrawGeometry(null,new Pen(brush,9),geometry);
            }
        }
    }
}
