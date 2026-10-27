using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RivenOverlay : Window
{
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetStyle(IntPtr h,int index,IntPtr style);
    private readonly OverlayPositioner positioner=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
    private readonly IntPtr game;private readonly long opened=Environment.TickCount64;
    private readonly bool right,automatic;
    private readonly ContentControl body=new();
    private readonly TextBlock state=new(){FontSize=12,Margin=new(12,3,12,3),Foreground=Brushes.LightGray};
    private RivenOcrResult displayed;
    internal int ContentUpdates{get;private set;}=1;
    internal bool Pending{get;private set;}
    internal bool Dismissed{get;private set;}
    internal RivenOverlay(IntPtr game,RivenOcrResult result,bool right=false,bool automatic=false)
    {
        this.game=game;this.right=right;this.automatic=automatic;displayed=result;Title="WF Labs · 리벤 옵션 분석";Width=360;Height=510;Left=-30000;Top=-30000;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        ShowActivated=false;ShowInTaskbar=false;Topmost=true;Focusable=false;IsHitTestVisible=false;UseLayoutRounding=true;
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
        var root=new Grid{Background=new SolidColorBrush(Color.FromRgb(15,17,19))};root.RowDefinitions.Add(new(){Height=new(24)});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        body.Content=BuildSide(result,right,automatic);Grid.SetRow(body,1);root.Children.Add(body);root.Children.Add(state);Content=root;SetPending(false);
        SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(this).Handle;SetStyle(hwnd,-20,new(RelicOverlay.GetStyle(hwnd,-20).ToInt64()|RelicOverlay.PassThroughStyle));};
        timer.Tick+=(_,_)=>{if(RelicCapture.GetForegroundWindow()!=game||RelicCapture.IsMinimized(game)||!automatic&&Environment.TickCount64-opened>30000){Close();return;}try{Position();}catch(InvalidOperationException){Close();}};
        Closed+=(_,_)=>{Dismissed=true;timer.Stop();};
    }
    internal void SetPending(bool pending)
    {
        if(Pending==pending&&state.Text.Length>0)return;Pending=pending;
        state.Text=pending?"변경 확인 중 · 이전 결과":"현재 인식 결과";
    }
    internal void UpdateResult(RivenOcrResult result)
    {
        bool layoutChanged=displayed.Cards.Length!=result.Cards.Length;
        bool same=displayed.Cards.Length==result.Cards.Length&&displayed.Cards.Zip(result.Cards).All(pair=>pair.First.Corroborated==pair.Second.Corroborated&&pair.First.Report.Kind==pair.Second.Report.Kind&&pair.First.Report.Rank==pair.Second.Report.Rank&&pair.First.Report.Options.SequenceEqual(pair.Second.Report.Options)&&pair.First.Report.Issues.SequenceEqual(pair.Second.Report.Issues));
        if(!same){body.Content=BuildSide(result,right,automatic);ContentUpdates++;}
        displayed=result;SetPending(false);
        if(layoutChanged&&IsVisible&&!Dismissed){Position();UiMotion.RevealAction(body);}
    }
    internal void Present(){if(RelicCapture.GetForegroundWindow()!=game)return;Show();Position();timer.Start();}
    private void Position()
    {
        var bounds=RelicCapture.ClientBounds(game);var dpi=VisualTreeHelper.GetDpi(this);
        var rect=Placement(bounds,dpi,right,displayed.Cards.Length>1);
        if(!right&&displayed.Cards.Length>1)
        {
            var crop=displayed.SourceCrop??new Rect(0,0,1,1);
            int gutter=(int)(displayed.Cards.Min(c=>crop.X+c.Bounds.X*crop.Width)*bounds.Width)-32;
            if(gutter>=rect.Width*.8&&gutter<rect.Width)rect.Width=gutter;
        }
        var content=(FrameworkElement)Content;content.Measure(new Size(rect.Width/dpi.DpiScaleX,double.PositiveInfinity));
        rect.Height=Math.Min(rect.Height,Math.Max(100,(int)Math.Ceiling(content.DesiredSize.Height*dpi.DpiScaleY)));
        positioner.Apply(new WindowInteropHelper(this).Handle,rect);
    }
    internal static Int32Rect Placement(Int32Rect bounds,DpiScale dpi,bool right,bool comparison=false)
    {
        bool wide=comparison&&!right;
        int w=Math.Min((int)(bounds.Width*(wide?.42:.23)),(int)((wide?720:360)*dpi.DpiScaleX)),h=Math.Min((int)(bounds.Height*.68),(int)(740*dpi.DpiScaleY));
        return new(bounds.X+(right?bounds.Width-w-16:16),bounds.Y+(bounds.Height-h)/2,w,h);
    }
    internal static FrameworkElement BuildSide(RivenOcrResult result,bool right,bool automatic=true)
    {
        if(result.Cards.Length>1)return BuildComparison(result,right,automatic);
        var accent=new SolidColorBrush(Color.FromRgb(242,203,87));var panel=new StackPanel{Margin=new(14)};
        TextBlock Text(string text,double size=14,Brush? brush=null)=>new(){Text=text,FontSize=size,LineHeight=size*1.45,Foreground=brush??Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,6)};
        void Rated(string label,double low,double? high=null,double size=16)
        {
            var row=new DockPanel{Margin=new(0,0,0,6)};var badge=RivenGradePresentation.Badge(low,high);DockPanel.SetDock(badge,Dock.Right);row.Children.Add(badge);
            var text=Text(label,size);text.Margin=new(0);row.Children.Add(text);panel.Children.Add(row);
        }
        panel.Children.Add(Text("WF Labs · 리벤 "+(automatic?"자동 인식":"분석"),15,accent));
        if(result.Cards.Length==0)panel.Children.Add(Text(result.Notice,14,Brushes.Salmon));
        else
        {
            var card=result.Cards[right&&result.Cards.Length>1?1:0];var report=card.Report;bool reliable=card.Corroborated&&report.Valid;
            bool spliceOnly=right&&result.Cards.Length==1;
            panel.Children.Add(Text(spliceOnly?"접합 분석":result.Cards.Length==1?"현재 옵션 수율":right?"오른쪽 카드":"왼쪽 카드",20));
            string classification=report.DetectedKind&&report.KindCandidates.Length!=1?"종류 확인 필요":RivenScan.KindName(report.Kind)+(report.DetectedKind?" · 자동 판별":"");
            panel.Children.Add(Text($"{classification} · {report.Rank}랭크 기준",12,Brushes.LightGray));
            if(!reliable)panel.Children.Add(Text("인식 확인 필요 · 앱에서 수정",14,Brushes.Salmon));
            if(!spliceOnly&&report.Values.Length==0&&report.Options.Length>0)
            {
                panel.Children.Add(Text($"읽힌 옵션 {report.Options.Length}개 · 수율 계산 대기",12,Brushes.LightGray));
                foreach(var option in report.Options)
                {
                    panel.Children.Add(Text(option.Name+(option.Negative?" · 음옵":""),14));
                    bool displayedNegative=option.Key=="Recoil"?!option.Negative:option.Negative;
                    panel.Children.Add(Text($"{(displayedNegative?"−":"+")}{option.Value:0.###}{option.Unit}",18,accent));
                }
            }
            if(!spliceOnly)foreach(var value in report.Values)
            {
                var o=value.Option;bool compact=result.Cards.Length>1;
                panel.Children.Add(Text(o.Name+(o.Negative?" · 음옵":"")+(compact&&reliable?$"  ({value.Range.Minimum:0.##}~{value.Range.Maximum:0.##}{o.Unit})":""),compact?13:14));
                var shown=$"{(o.Negative?"−":"+")}{o.Value:0.###}{o.Unit}";
                if(reliable&&value.Grade is {} grade)Rated(shown+$"    품질 {RivenRating.Quality(grade.Position,o.Negative):P1}",RivenRating.Quality(grade.Position,o.Negative),size:compact?16:18);else panel.Children.Add(Text(shown,18,accent));
                if(reliable&&value.Grade is {} g)panel.Children.Add(new ProgressBar{Value=RivenRating.Quality(g.Position,o.Negative),Minimum=0,Maximum=1,Height=4,Foreground=new SolidColorBrush(RivenGradePresentation.ColorFor(RivenRating.Tier(RivenRating.Quality(g.Position,o.Negative)))),Background=new SolidColorBrush(Color.FromRgb(55,57,60)),Margin=new(0,0,0,5)});
                if(reliable&&!compact)panel.Children.Add(Text($"{value.Range.Minimum:0.##} ~ {value.Range.Maximum:0.##}{o.Unit}",12,Brushes.LightGray));
            }
            if(reliable&&(right||result.Cards.Length>1))
            {
                panel.Children.Add(Text($"지금 접합 가능 · {report.Recipes.Length}가지",16,accent));
                bool spliced=report.Options.Any(o=>report.Kind=="Melee"?RivenValues.IsSpliced(Enum.Parse<RivenStat>(o.Key)):RangedRivens.Attribute(o.Key).Spliced);
                if(spliceOnly&&report.Recipes.Length==0)panel.Children.Add(Text(spliced?"이미 접합된 옵션은 재접합을 예측하지 않습니다.":"현재 카드에는 재료 두 옵션이 함께 없습니다.",13,Brushes.LightGray));
                foreach(var recipe in report.Recipes)
                {
                    panel.Children.Add(Text(recipe.Ingredients,13,Brushes.LightGray));
                    Rated("→ "+recipe.Result,recipe.MinimumQuality,recipe.MaximumQuality);
                    if(spliceOnly)panel.Children.Add(Text($"계승 범위 {recipe.Minimum:0.##} ~ {recipe.Maximum:0.##}",13));
                    panel.Children.Add(Text($"무작위 대체 {(recipe.Negative?"음옵":"양옵")} {recipe.ReplacementOptions}종",13,Brushes.LightGray));
                }
                var opportunities=RivenScan.Opportunities(report);
                if(opportunities.Length>0)
                {
                    panel.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromRgb(55,57,60)),Margin=new(0,6,0,10)});
                    panel.Children.Add(Text($"옵션 변경 후 가능 · {opportunities.Length}가지",16,accent));
                    foreach(var opportunity in opportunities)
                    {
                        var ingredients=opportunity.HeldName+(opportunity.HeldNegative?" (음옵)":"")+" + "+opportunity.MissingName+" 필요";
                        if(result.Cards.Length>1)Rated(ingredients+" → "+opportunity.ResultName,opportunity.MinimumQuality,opportunity.MaximumQuality,13);
                        else{panel.Children.Add(Text(ingredients,13,Brushes.LightGray));Rated("→ "+opportunity.ResultName,opportunity.MinimumQuality,opportunity.MaximumQuality);}
                    }
                    panel.Children.Add(Text("예상 등급: 보유 옵션의 수율 유지 가정\n부족한 옵션의 수치 미정 · 획득 확률 아님",12,Brushes.LightGray));
                }
            }
        }
        panel.Children.Add(Text("등급 = 수치 품질 · DPS 점수 아님\n음옵은 페널티가 작을수록 높은 등급\n클릭은 게임으로 통과",12,Brushes.LightGray));
        // A click-through panel cannot scroll. Downscale only when a high-DPI game
        // leaves less vertical space than the complete card needs.
        var content=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Top,Child=new Border{Width=340,Child=panel}};
        return new Border{Background=new SolidColorBrush(Color.FromArgb(242,15,17,19)),BorderBrush=new SolidColorBrush(Color.FromRgb(96,87,54)),BorderThickness=new(1),CornerRadius=new(5),Child=content};
    }
}
