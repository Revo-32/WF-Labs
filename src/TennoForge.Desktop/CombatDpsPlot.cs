using System.Windows;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class CombatDpsPlot:Plot
{
    private CombatPoint[] points=[];
    private CombatEvent[] events=[];
    internal void Set(CombatTimelineResult? result){points=result?.Points??[];events=result?.Events??[];Summary="시간별 현재 상태 직접 DPS · 동일 상태에서 컨오를 제외한 DPS 비교";Changed();}
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(points.Length==0)return;
        var end=Math.Max(.01,points[^1].Time);var max=Math.Max(1,points.Max(p=>Math.Max(p.StartDps,p.SnapshotDps)));
        var area=new Rect(54,30,Math.Max(1,ActualWidth-68),Math.Max(1,ActualHeight-70));
        Point At(double t,double d)=>new(area.Left+t/end*area.Width,area.Bottom-d/max*area.Height);
        Label(dc,"● 현재 상태 DPS",new(area.Left,2),13,ChartPalette.Accent);Label(dc,"● 컨오 제외",new(area.Left+145,2),13,ChartPalette.Comparison);
        for(var i=0;i<=3;i++){var v=max*i/3;dc.DrawLine(new Pen(GridInk,1),At(0,v),At(end,v));Label(dc,ChartPalette.Number(v),new(area.Left-6,At(0,v).Y-9),13,right:true);}
        var baseline=new Pen(Muted,1){DashStyle=DashStyles.Dash};dc.DrawLine(baseline,At(0,points[0].StartDps),At(end,points[0].StartDps));
        for(var i=1;i<points.Length;i++)
        {dc.DrawLine(new Pen(ChartPalette.Accent,1.8),At(points[i-1].Time,points[i-1].SnapshotDps),At(points[i].Time,points[i].SnapshotDps));dc.DrawLine(new Pen(ChartPalette.Comparison,1.5),At(points[i-1].Time,points[i-1].WithoutCoDps),At(points[i].Time,points[i].WithoutCoDps));}
        foreach(var p in points)
        {
            var co=p.SnapshotDps-p.WithoutCoDps;var growth=p.StartDps>0?p.SnapshotDps/p.StartDps-1:0;
            var hint=$"{p.Time:0.00}초 · 다음 타격의 상태 고정 직접 DPS\n전체 {p.SnapshotDps:N1}\n컨오 제외 {p.WithoutCoDps:N1}\n컨오 기여 +{co:N1}"+(p.WithoutCoDps>0?$" (+{co/p.WithoutCoDps:P1})":"")+$"\n시작 대비 {growth:+0.0%;-0.0%;0%}\n상태 {p.StatusTypes:0.0}종 · 방어력 {p.Armor:N1} · 콤보 {p.Combo:0.0}×\n점선: 타격 전 시작 DPS · 표본 평균\n컨오 제외선은 같은 전투 상태에서 컨오만 뺀 비교입니다. 별도 빌드의 전투 시뮬레이션이 아닙니다.";
            var recent=events.Where(e=>e.Time<=p.Time&&e.Time>=p.Time-.5&&e.Kind is "buff" or "armor" or "layer").TakeLast(3).Select(e=>$"{e.Time:0.00}초 {e.Description}").ToArray();
            if(recent.Length>0)hint+="\n\n최근 사건 · 대표 표본\n"+string.Join("\n",recent);
            Targets.Add((new Rect(At(p.Time,0).X-5,area.Top,10,area.Height),hint));
        }
        Label(dc,"0",new(area.Left,area.Bottom+10));Label(dc,$"{end:0.##}초",new(area.Right,area.Bottom+10),right:true);Label(dc,"경과 시간",new(area.Left+area.Width/2,area.Bottom+10),centered:true);
    }
}
