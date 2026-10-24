using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private readonly StackPanel rivenRangeCharts=new(){Margin=new(0,8,0,20)};
    private readonly StackPanel rivenDpsCharts=new(){Margin=new(0,4,0,16)};
    private static Grid ProportionalTrack(double position,string brush,double height=8)
    {
        var grid=new Grid{Height=height,Margin=new(0,8,0,8)};
        grid.SetResourceReference(Panel.BackgroundProperty,"StrokeBrush");
        grid.ColumnDefinitions.Add(new(){Width=new(Math.Clamp(position,0,1),GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new(){Width=new(1-Math.Clamp(position,0,1),GridUnitType.Star)});
        var fill=new Border();fill.SetResourceReference(Border.BackgroundProperty,brush);grid.Children.Add(fill);
        return grid;
    }
    private void AddRivenRangeChart(RivenTrait trait,double value,double min,double max,RollGrade grade)
    {
        rivenRangeCharts.Children.Add(RivenPresentation.Range(RivenEngine.Label(trait.Stat),RivenValues.Unit(trait.Stat),min,max,value,trait.Negative));
    }
    private void UpdateRivenComparisonChart(EncounterResult baseline,EncounterResult candidate)
    {
        rivenDpsCharts.Children.Clear();
        var a=baseline.Timeline;var b=candidate.Timeline;
        if(baseline.Unsupported.Length>0||candidate.Unsupported.Length>0||a is not {Unsupported.Length:0}||b is not {Unsupported.Length:0})
        {rivenDpsCharts.Children.Add(T("이 조건의 확정 비교 그래프는 제공할 수 없습니다. 아래 계산 범위를 확인하세요.",14,"#596B85"));return;}
        var dps=a.Seconds>0&&b.Seconds>0;
        var values=new[]{(a.DirectDamage+a.StatusDamage)/(dps?a.Seconds:1),(b.DirectDamage+b.StatusDamage)/(dps?b.Seconds:1)};
        var max=values.Max();var timelines=new[]{a,b};
        rivenDpsCharts.Children.Add(T(dps?"직접 + 상태 평균 DPS":"첫 접촉 피해",18));
        rivenDpsCharts.Children.Add(T("같은 모딩 · 적 · 교전 조건 | 직접 피해 ■ 노랑 · 상태 피해 ■ 보라",14,"#596B85"));
        for(var i=0;i<2;i++)
        {
            var t=timelines[i];var total=t.DirectDamage+t.StatusDamage;
            var heading=T($"{(i==0?"기준":"후보")}   {values[i]:N1}",18);heading.Margin=new(0,6,0,2);rivenDpsCharts.Children.Add(heading);
            var track=ProportionalTrack(max>0?values[i]/max:0,"AccentBrush",16);
            if(total>0)
            {
                var stack=new Grid();stack.ColumnDefinitions.Add(new(){Width=new(t.DirectDamage,GridUnitType.Star)});stack.ColumnDefinitions.Add(new(){Width=new(t.StatusDamage,GridUnitType.Star)});
                var direct=new Border();direct.SetResourceReference(Border.BackgroundProperty,"AccentBrush");stack.Children.Add(direct);
                var statusBar=new Border{Background=new SolidColorBrush(Color.FromRgb(179,141,219))};Grid.SetColumn(statusBar,1);stack.Children.Add(statusBar);track.Children.Clear();track.Children.Add(stack);
            }
            rivenDpsCharts.Children.Add(track);
            var error=t.DamageEstimate?.Approximate95HalfWidth;
            rivenDpsCharts.Children.Add(T($"직접 {t.DirectDamage/(dps?t.Seconds:1):N1} · 상태 {t.StatusDamage/(dps?t.Seconds:1):N1}"+(error is {} e?$" · 95% 오차폭 ±{e/(dps?t.Seconds:1):N1}":"")+ $" · {t.Trials:N0}회",14,"#596B85"));
        }
        rivenDpsCharts.Children.Add(T(values[0]>0?$"기준 대비 {(values[1]/values[0]-1)*100:+0.0;-0.0;0.0}%": "기준값 0 · 증감률 계산 불가",20));
        rivenDpsCharts.Children.Add(T("막대는 0부터 같은 축척 · 표본 평균 비교이며 획득 확률이 아닙니다.",14,"#596B85"));
    }
}
