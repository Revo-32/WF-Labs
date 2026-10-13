using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class ChartPalette
{
    // Critical tiers follow the game's HUD. Element hues are app choices inspired by effects,
    // not a claim that Warframe has a fixed universal RGB palette for every damage type.
    internal static Brush Color(string hex) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    internal static Brush Critical(int tier)=>Color(Theme.IsDark?tier switch{0=>"#D7D7D7",1=>"#F5DC59",2=>"#F6A04D",_=>"#F26670"}:tier switch{0=>"#535353",1=>"#987200",2=>"#B75B0B",_=>"#BE3341"});
    internal static Brush Damage(DamageType type)=>Color(!Theme.IsDark?type switch
    {
        DamageType.Impact=>"#406783",DamageType.Puncture=>"#857144",DamageType.Slash=>"#535353",
        DamageType.Heat=>"#B4491B",DamageType.Cold=>"#177397",DamageType.Electricity=>"#605CC5",DamageType.Toxin=>"#477713",
        DamageType.Blast=>"#91631A",DamageType.Radiation=>"#867119",DamageType.Gas=>"#3B7964",DamageType.Magnetic=>"#376AB4",
        DamageType.Viral=>"#874DB0",DamageType.Corrosive=>"#65790C",_=>"#202020"
    }:type switch
    {
        DamageType.Impact=>"#8FABC1",DamageType.Puncture=>"#C8B888",DamageType.Slash=>"#E0E0E0",
        DamageType.Heat=>"#F38A50",DamageType.Cold=>"#78CCEA",DamageType.Electricity=>"#989CFA",DamageType.Toxin=>"#A2CC66",
        DamageType.Blast=>"#E9BB6B",DamageType.Radiation=>"#D6C779",DamageType.Gas=>"#83B5A2",DamageType.Magnetic=>"#83AFEB",
        DamageType.Viral=>"#BF92DF",DamageType.Corrosive=>"#C3D65F",_=>"#FFFFFF"
    });
    internal static Brush Accent=>Color(Theme.IsDark?"#64D9F2":"#006B83");
    internal static Brush Comparison=>Color(Theme.IsDark?"#ADB9CC":"#576B86");
    internal static string Number(double n)=>n>=1_000_000?$"{n/1_000_000:0.#}M":n>=1000?$"{n/1000:0.#}k":$"{n:0.#}";
}

internal abstract class Plot : FrameworkElement
{
    private readonly CursorTooltip tip;
    protected readonly List<(Rect Area,string Text)> Targets=[];
    protected Plot()
    {
        Height=190;Focusable=true;UseLayoutRounding=true;SnapsToDevicePixels=true;
        tip=CursorTooltip.Attach(this,point=>(object)(Targets.FirstOrDefault(x=>x.Area.Contains(point)).Text??Summary),120);
    }
    protected string Summary { get; set; }="데이터 없음";
    protected Brush Ink=>(Brush)FindResource("ResultsTextBrush");
    protected Brush Muted=>(Brush)FindResource("ResultsMutedBrush");
    protected Brush GridInk=>(Brush)FindResource("ResultsStrokeBrush");
    protected void Label(DrawingContext dc,string text,Point at,double size=13,Brush? brush=null,bool right=false,bool bold=false,bool centered=false)
    {
        var dpi=VisualTreeHelper.GetDpi(this);
        var t=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface((FontFamily)Application.Current.Resources["Wanted"],FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush??Muted,null,TextRendering.Formatting(dpi),dpi.PixelsPerDip);
        var x=right?at.X-t.Width:centered?at.X-t.Width/2:at.X;
        dc.DrawText(t,new Point(Math.Round(x*dpi.DpiScaleX)/dpi.DpiScaleX,Math.Round(at.Y*dpi.DpiScaleY)/dpi.DpiScaleY));
    }
    protected void Background(DrawingContext dc)
    { Targets.Clear();dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize)); }
    protected void Changed()
    {System.Windows.Automation.AutomationProperties.SetHelpText(this,Summary);tip.Close();InvalidateVisual();}
}

internal sealed class DamageDonut : Plot
{
    private (DamageType Type,double Value)[] slices=[];
    internal double Total=>slices.Sum(x=>x.Value);
    internal void SetData(Dictionary<DamageType,double>? damage)
    {
        slices=damage?.Where(x=>x.Value>0).OrderByDescending(x=>x.Value).Select(x=>(x.Key,x.Value)).ToArray()??[];
        Height=Math.Max(190,slices.Length*28+12);
        Summary=string.Join("\n",slices.Select(x=>$"{Labels.Damage(x.Type)}: {x.Value:N1} · {x.Value/Total:P1}"));Changed();
    }
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(slices.Length==0){Label(dc,"계산할 빌드를 선택하세요.",new(8,64));return;}
        var narrow=ActualWidth<290;var radius=narrow?56:64;var center=new Point(radius+4,Height/2);var inside=radius-11;
        var legendX=2*radius+22;var legendWidth=ActualWidth-legendX;double angle=-90;
        var total=Total;
        foreach(var (type,value) in slices)
        {
            var sweep=value/total*360;
            Geometry ring;
            if(sweep>359.999)ring=new CombinedGeometry(GeometryCombineMode.Exclude,new EllipseGeometry(center,radius,radius),new EllipseGeometry(center,inside,inside));
            else
            {
                Point At(double degrees,double r)=>new(center.X+Math.Cos(degrees*Math.PI/180)*r,center.Y+Math.Sin(degrees*Math.PI/180)*r);
                var g=new StreamGeometry();using(var path=g.Open())
                {path.BeginFigure(At(angle,radius),true,true);path.ArcTo(At(angle+sweep,radius),new(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);path.LineTo(At(angle+sweep,inside),true,false);path.ArcTo(At(angle,inside),new(inside,inside),0,sweep>180,SweepDirection.Counterclockwise,true,false);}
                g.Freeze();ring=g;
            }
            dc.DrawGeometry(ChartPalette.Damage(type),new Pen((Brush)FindResource("ResultsSurfaceBrush"),1),ring);
            angle+=sweep;
        }
        Label(dc,"비치명 합계",new(center.X,center.Y-23),14,centered:true);
        Label(dc,ChartPalette.Number(total),new(center.X,center.Y+1),20,Ink,bold:true,centered:true);
        var y=Math.Max(6,Height/2-slices.Length*14);
        foreach(var (type,value) in slices)
        {
            dc.DrawRoundedRectangle(ChartPalette.Damage(type),null,new(legendX,y+5,8,8),2,2);
            Label(dc,Labels.Damage(type),new(legendX+15,y),14,Ink);
            Label(dc,$"{value/total:P1}",new(ActualWidth-2,y),14,Ink,true);
            Targets.Add((new Rect(legendX,y,Math.Max(1,legendWidth),28),$"{Labels.Damage(type)}\n비치명 피해 {value:N1}\n전체의 {value/total:P2}"));y+=28;
        }
        Targets.Add((new Rect(0,0,legendX,Height),Summary));
    }
}

internal record PlotPoint(double X,double Y,string Label,bool Selected=false);
internal sealed class SeriesPlot : Plot
{
    private PlotPoint[] points=[];
    private string xLabel="",yLabel="";
    private bool line;
    private double minX,maxX;
    internal IReadOnlyList<PlotPoint> Points=>points;
    internal void SetData(PlotPoint[] data,bool connect,string x,string y,double minimum,double maximum)
    {points=data;line=connect;xLabel=x;yLabel=y;minX=Math.Min(minimum,data.Min(p=>p.X));maxX=Math.Max(maximum,data.Max(p=>p.X));Summary=string.Join("\n",points.Where((p,i)=>p.Selected||!line||i%5==0).Select(p=>p.Label));Changed();}
    internal void Clear(){points=[];Summary="데이터 없음";Changed();}
    protected override void OnRender(DrawingContext dc)
    {
        Background(dc);if(points.Length==0){Label(dc,"계산할 빌드를 선택하세요.",new(8,64));return;}
        var plot=new Rect(54,26,Math.Max(1,ActualWidth-72),Height-76);
        var largest=points.Max(p=>p.Y);var power=Math.Pow(10,Math.Floor(Math.Log10(Math.Max(largest,1))));var ceiling=Math.Max(1,Math.Ceiling(largest/power*2)/2*power);
        Point Position(PlotPoint p)=>new(plot.Left+(p.X-minX)/Math.Max(1,maxX-minX)*plot.Width,plot.Bottom-p.Y/ceiling*plot.Height);
        Label(dc,yLabel,new(0,0),14,Ink);
        for(int i=0;i<=3;i++)
        {
            var y=plot.Bottom-i*plot.Height/3;dc.DrawLine(new Pen(GridInk,1),new(plot.Left,y),new(plot.Right,y));
            Label(dc,ChartPalette.Number(ceiling*i/3),new(plot.Left-6,y-9),13,Muted,true);
        }
        var tickStep=Math.Max(1,(int)Math.Ceiling(points.Length/Math.Max(2,plot.Width/38)));
        var ticks=line?Enumerable.Range(0,4).Select(i=>minX+(maxX-minX)*i/3):points.Where((_,i)=>i%tickStep==0).Select(p=>p.X);
        foreach(var x in ticks)
        {var px=plot.Left+(x-minX)/Math.Max(1,maxX-minX)*plot.Width;var s=line?ChartPalette.Number(x):x.ToString("0");Label(dc,s,new(px,plot.Bottom+7),13,centered:true);}
        Label(dc,xLabel,new(plot.Right,Height-20),13,Muted,true);
        var ink=ChartPalette.Accent;
        if(line)
        {
            var area=new StreamGeometry();
            using(var draw=area.Open())
            {
                draw.BeginFigure(new Point(Position(points[0]).X,plot.Bottom),true,true);
                foreach(var p in points)draw.LineTo(Position(p),true,false);
                draw.LineTo(new Point(Position(points[^1]).X,plot.Bottom),true,false);
            }
            area.Freeze();
            var color=((SolidColorBrush)ink).Color;
            var fill=new LinearGradientBrush(Color.FromArgb(Theme.IsDark?(byte)48:(byte)32,color.R,color.G,color.B),Color.FromArgb(0,color.R,color.G,color.B),90);fill.Freeze();
            dc.DrawGeometry(fill,null,area);
            var path=new StreamGeometry();using(var draw=path.Open()){draw.BeginFigure(Position(points[0]),false,false);foreach(var p in points.Skip(1))draw.LineTo(Position(p),true,false);}path.Freeze();dc.DrawGeometry(null,new Pen(ink,1.8),path);
        }
        for(int i=0;i<points.Length;i++)
        {
            var p=points[i];var point=Position(p);
            var band=plot.Width/Math.Max(1,maxX-minX);
            if(!line)
            {
                var barWidth=Math.Clamp(band*.6,1,26);
                dc.DrawRectangle(ink,null,new Rect(point.X-barWidth/2,point.Y,barWidth,Math.Max(0,plot.Bottom-point.Y)));
            }
            if(p.Selected)
            {
                dc.DrawLine(new Pen(GridInk,1){DashStyle=DashStyles.Dash},new(point.X,plot.Top),new(point.X,plot.Bottom));
                dc.DrawEllipse(ink,new Pen((Brush)FindResource("ResultsBackgroundBrush"),2),point,4,4);
            }
            var halfWidth=line?plot.Width/Math.Max(1,points.Length-1)/2:band/2;
            var bounds=new Rect(Math.Max(plot.Left,point.X-halfWidth),plot.Top,Math.Min(plot.Right,point.X+halfWidth)-Math.Max(plot.Left,point.X-halfWidth),plot.Height);
            Targets.Add((bounds,p.Label));
            if(p.Selected)Label(dc,"현재",new(Math.Clamp(point.X,plot.Left+14,plot.Right-14),Math.Max(2,point.Y-25)),13,ink,centered:true);
        }
    }
}
