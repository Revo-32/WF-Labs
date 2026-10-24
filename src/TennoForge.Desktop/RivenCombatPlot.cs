using System.Windows;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

/// <summary>Independent encounter runs share the same axes; neither curve implies an instantaneous derivative.</summary>
internal sealed class RivenCombatPlot : Plot
{
    private RangedCombatPoint[] current=[],without=[];
    internal void Set(RangedCombatPoint[] excluded,RangedCombatPoint[] included)
    {
        current=included.Where(p=>p.Time>0).ToArray();without=excluded.Where(p=>p.Time>0).ToArray();
        Summary="기준과 후보의 누적 피해 · 같은 전투 시간과 조건 · 처치 뒤 피해 누적 종료";Changed();
    }
    private static double Dps(RangedCombatPoint p)=>(p.DirectDamage+p.StatusDamage);
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(current.Length==0)return;
        var area=new Rect(58,32,Math.Max(1,ActualWidth-74),Math.Max(1,ActualHeight-76));
        var maximum=Math.Max(1,current.Concat(without).Max(Dps));var end=current[^1].Time;
        Point At(RangedCombatPoint p)=>new(area.Left+p.Time/end*area.Width,area.Bottom-Dps(p)/maximum*area.Height);
        Label(dc,"● 후보",new(area.Left,1),13,ChartPalette.Accent);
        Label(dc,"┄ 기준",new(area.Left+122,1),13,ChartPalette.Comparison);
        for(int i=0;i<=3;i++)
        {
            var y=area.Bottom-area.Height*i/3;
            dc.DrawLine(new Pen(GridInk,1),new(area.Left,y),new(area.Right,y));
            Label(dc,ChartPalette.Number(maximum*i/3),new(area.Left-7,y-9),13,right:true);
            Label(dc,(end*i/3).ToString("0.##"),new(area.Left+area.Width*i/3,area.Bottom+7),13,centered:true);
        }
        foreach(var (data,brush,dashed) in new[]{(without,ChartPalette.Comparison,true),(current,ChartPalette.Accent,false)})
        {
            var pen=new Pen(brush,1.8);if(dashed)pen.DashStyle=DashStyles.Dash;
            for(int i=1;i<data.Length;i++)dc.DrawLine(pen,At(data[i-1]),At(data[i]));
        }
        for(int i=0;i<current.Length;i++)
        {
            var p=current[i];var other=without.FirstOrDefault(x=>Math.Abs(x.Time-p.Time)<1e-8);
            var label=$"{p.Time:0.##}초 · 누적 피해\n후보 {Dps(p):N1}";
            if(other is not null)label+=$"\n기준 {Dps(other):N1}\n차이 {Dps(p)-Dps(other):+0.0;-0.0;0}";
            label+=$"\n직접 피해 {p.DirectDamage:N1}\n상태 피해 {p.StatusDamage:N1}\n각 조건에서 방어층·상태·처치 시점을 독립 재계산";
            var width=area.Width/Math.Max(1,current.Length-1);
            Targets.Add((new Rect(Math.Max(area.Left,At(p).X-width/2),area.Top,width,area.Height),label));
        }
        Label(dc,"교전 시간 (초)",new(area.Right,ActualHeight-19),13,right:true);
    }
}
