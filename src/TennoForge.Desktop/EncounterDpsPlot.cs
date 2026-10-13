using System.Windows;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class EncounterDpsPlot:Plot
{
    private EncounterSample[] points=[];
    internal void Set(EncounterSample[] value){points=value;Summary="타격 직전 상태를 고정한 전체 콤보 DPS · 컨오 포함/제외 범위";Changed();}
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(points.Length==0)return;
        var area=new Rect(56,32,Math.Max(1,ActualWidth-70),Math.Max(1,ActualHeight-74));
        var max=Math.Max(1,points.Max(p=>Math.Max(p.SnapshotDps!.High,p.StartDps)));var last=Math.Max(2,points[^1].Hit);
        Point At(double x,double y)=>new(area.Left+(x-1)/(last-1)*area.Width,area.Bottom-y/max*area.Height);
        Label(dc,"● 컨오 포함",new(area.Left,2),13,ChartPalette.Accent);Label(dc,"● 컨오 제외",new(area.Left+130,2),13,ChartPalette.Comparison);
        for(var i=0;i<=3;i++){var v=max*i/3;dc.DrawLine(new Pen(GridInk,1),At(1,v),At(last,v));Label(dc,ChartPalette.Number(v),new(area.Left-7,At(1,v).Y-9),13,right:true);}
        for(var series=0;series<2;series++)
        {
            EncounterRange R(EncounterSample p)=>series==0?p.SnapshotDps!:p.WithoutCoDps!;
            var color=series==0?ChartPalette.Accent:ChartPalette.Comparison;
            var shape=new StreamGeometry();using(var g=shape.Open()){g.BeginFigure(At(points[0].Hit,R(points[0]).Low),true,true);foreach(var p in points)g.LineTo(At(p.Hit,R(p).High),true,false);foreach(var p in points.Reverse())g.LineTo(At(p.Hit,R(p).Low),true,false);}shape.Freeze();
            var fill=color.Clone();fill.Opacity=.16;dc.DrawGeometry(fill,null,shape);
            for(var i=1;i<points.Length;i++){dc.DrawLine(new Pen(color,1.5),At(points[i-1].Hit,R(points[i-1]).High),At(points[i].Hit,R(points[i]).High));dc.DrawLine(new Pen(color,1),At(points[i-1].Hit,R(points[i-1]).Low),At(points[i].Hit,R(points[i]).Low));}
        }
        dc.DrawLine(new Pen(Muted,1){DashStyle=DashStyles.Dash},At(1,points[0].StartDps),At(last,points[0].StartDps));
        string Range(EncounterRange r)=>Math.Abs(r.High-r.Low)<.05?$"{r.Low:N1}":$"{r.Low:N1}–{r.High:N1}";
        foreach(var p in points)
        {
            var growth=p.StartDps>0?$"{p.SnapshotDps!.Low/p.StartDps-1:+0.0%;-0.0%;0%}–{p.SnapshotDps.High/p.StartDps-1:+0.0%;-0.0%;0%}":"—";
            var hint=$"{p.Hit}번째 타격 직전 · {p.Earliest:0.##}–{p.Latest:0.##}초 구간\n상태 고정 직접 DPS {Range(p.SnapshotDps!)}\n동일 상태 컨오 제외 {Range(p.WithoutCoDps!)}\n컨오 기여 +{Range(p.CoGain!)}\n시작 대비 {growth}\n상태 {Range(p.StatusTypes)}종 · 방어력 {Range(p.Armor)}\n콤보 {p.Combo:0.0}×\n음영: 타격 시각·상태 만료의 불확실성 범위\n두 선은 같은 적 상태·콤보에서 컨오만 비교합니다.";
            Targets.Add((new Rect(At(p.Hit,0).X-6,area.Top,12,area.Height),hint));
        }
        Label(dc,"1",new(area.Left,area.Bottom+10));Label(dc,$"{last}",new(area.Right,area.Bottom+10),right:true);Label(dc,"타격 순서 · 실제 타격 시각 미기재",new(area.Left+area.Width/2,area.Bottom+10),centered:true);
    }
}
