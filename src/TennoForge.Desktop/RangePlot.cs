using System.Windows;
using System.Windows.Media;

namespace TennoForge.Desktop;

internal record RangePoint(double X,double Low,double High,string Hint);
internal sealed class RangePlot:Plot
{
    private RangePoint[] points=[];
    private string axis="";
    internal void SetData(RangePoint[] value,string label){points=value;axis=label;Summary="타격 시각 불확실성에 따른 직접 피해·방어력 범위";Changed();}
    internal void Clear(){points=[];Changed();}
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(points.Length==0)return;
        var area=new Rect(48,14,Math.Max(1,ActualWidth-60),Math.Max(1,ActualHeight-54));
        var max=Math.Max(1,points.Max(p=>p.High));var xMax=Math.Max(2,points.Max(p=>p.X));
        Point At(double x,double y)=>new(area.Left+(x-1)/(xMax-1)*area.Width,area.Bottom-y/max*area.Height);
        for(var i=0;i<=3;i++){var y=max*i/3;var p=At(1,y);dc.DrawLine(new Pen(GridInk,1),p,new(area.Right,p.Y));Label(dc,ChartPalette.Number(y),new(area.Left-7,p.Y-9),13,right:true);}
        var shape=new StreamGeometry();using(var g=shape.Open()){g.BeginFigure(At(points[0].X,points[0].Low),true,true);foreach(var p in points)g.LineTo(At(p.X,p.High),true,false);foreach(var p in points.Reverse())g.LineTo(At(p.X,p.Low),true,false);}shape.Freeze();
        var fill=ChartPalette.Accent.Clone();fill.Opacity=.18;dc.DrawGeometry(fill,null,shape);
        for(var i=1;i<points.Length;i++){dc.DrawLine(new Pen(ChartPalette.Accent,1.5),At(points[i-1].X,points[i-1].High),At(points[i].X,points[i].High));dc.DrawLine(new Pen(ChartPalette.Comparison,1.5),At(points[i-1].X,points[i-1].Low),At(points[i].X,points[i].Low));}
        if(points.Length==1)dc.DrawEllipse(ChartPalette.Accent,null,At(points[0].X,points[0].High),3,3);
        foreach(var p in points)Targets.Add((new Rect(At(p.X,0).X-6,area.Top,12,area.Height),p.Hint));
        Label(dc,"1",new(area.Left,area.Bottom+8));Label(dc,xMax.ToString("0"),new(area.Right,area.Bottom+8),right:true);Label(dc,axis,new(area.Left+area.Width/2,area.Bottom+8),centered:true);
    }
}
