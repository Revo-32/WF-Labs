using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class DpsEquationView : Border
{
    internal static readonly FontFamily MathFont=new("Cambria Math");
    private readonly WrapPanel factors=new(){Margin=new(0,2,0,0)};
    private readonly TextBlock context=new(){FontSize=13,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock hint=new(){FontSize=13,LineHeight=20,Height=22,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
    private readonly Dictionary<string,Cell> cells=[];
    private readonly Run quantity=new("DPS");
    private string defaultHint="";
    internal DpsEquation? Equation {get;private set;}
    internal bool ReduceMotion {get;set;}
    internal string[] Highlighted=>cells.Where(p=>p.Value.Highlighted).Select(p=>p.Key).ToArray();
    private sealed class Cell
    {
        internal required TextBlock Number;
        internal required TextBlock Label;
        internal required Border Accent;
        internal bool Highlighted;
    }
    internal DpsEquationView()
    {
        Padding=new(0,0,0,10);Margin=new(0,0,10,14);BorderThickness=new(0,0,0,1);
        SetResourceReference(BorderBrushProperty,"ResultsStrokeBrush");
        var heading=new DockPanel();
        var title=new TextBlock{FontFamily=MathFont,FontSize=20,Height=30,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,14,0)};
        title.Inlines.Add(quantity);title.Inlines.Add(new Run("target"){BaselineAlignment=BaselineAlignment.Subscript,FontSize=13});title.Inlines.Add(new Run(" ="));
        title.SetResourceReference(TextBlock.ForegroundProperty,"ResultsTextBrush");heading.Children.Add(title);heading.Children.Add(context);
        context.SetResourceReference(TextBlock.ForegroundProperty,"ResultsMutedBrush");hint.SetResourceReference(TextBlock.ForegroundProperty,"ResultsMutedBrush");
        Child=new StackPanel{Children={heading,factors,hint}};
        TextOptions.SetTextFormattingMode(title,TextFormattingMode.Ideal);
        SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"방어력을 반영한 DPS 계산식");
    }
    internal void SetEquation(DpsEquation? model)
    {
        Equation=model;cells.Clear();factors.Children.Clear();
        if(model is null){context.Text="계산 조건 확인 필요";hint.Text="";return;}
        quantity.Text=model.DamageOnly?"Damage":"DPS";
        context.Text=model.Context??(model.IsCombo?"시작 상태 고정 · Σ 콤보 타격":"시작 상태 고정 · 단일 공격");
        defaultHint=model.Context is not null?"각 접촉의 피해 브라켓·치명타·방어층을 적용한 뒤 합산 · 표본 수와 오차는 아래 결과에 표시":model.IsCombo?"타격별 계산을 합산한 단계별 배수 · 표시값은 반올림":"현재 발동 조건 기준 · 표시값은 반올림";hint.Text=defaultHint;
        if(model.Limitations is {Length:>0} pending)defaultHint=$"일부 효과 미반영·미검증 {pending.Length}건 · 계산식·출처에서 상세 확인";
        if(model.DamageOnly)defaultHint=model.Context is not null?(model.Limitations is {Length:>0}?"첫 접촉 피해 · 일부 성분 제외/조건부 항목은 아래 계산 범위 확인":"첫 접촉 피해 · 지연된 피해는 이후 시간에 별도 계산"):model.Limitations is {Length:>0}?"조건부 동작 피해 · 출처 상충/미확인 항목 확인 · 시간 미기재로 DPS 제외":"동작 1회 피해 · 위키에 시간이 없어 DPS로 환산하지 않습니다.";
        hint.Text=defaultHint;hint.ToolTip=model.Limitations is {Length:>0}?string.Join("\n\n",model.Limitations):defaultHint;
        hint.SetResourceReference(TextBlock.ForegroundProperty,model.Limitations is {Length:>0}?"ChartAccentBrush":"ResultsMutedBrush");
        for(var index=0;index<model.Terms.Length;index++)
        {
            var term=model.Terms[index];
            var label=new TextBlock{Text=term.Label,FontSize=13,Height=20,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
            label.SetResourceReference(TextBlock.ForegroundProperty,"ResultsMutedBrush");
            var number=new TextBlock{Text=(index==0?"":term.Operator+" ")+Format(term.Value)+(term.Id=="time"?" s":""),FontFamily=MathFont,FontSize=20,Height=30,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center};
            number.SetResourceReference(TextBlock.ForegroundProperty,"ResultsTextBrush");TextOptions.SetTextFormattingMode(number,TextFormattingMode.Ideal);
            var accent=new Border{Height=1,Opacity=0};accent.SetResourceReference(BackgroundProperty,"ChartAccentBrush");
            var body=new StackPanel{Children={label,number,accent}};
            var measure=new FormattedText(number.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(MathFont,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),24,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var box=new Border{Child=body,Width=Math.Max(116,Math.Ceiling(measure.WidthIncludingTrailingWhitespace)+14),Height=55,Padding=new(0,0,12,0),Background=Brushes.Transparent};
            var cell=new Cell{Label=label,Number=number,Accent=accent};cells[term.Id]=cell;factors.Children.Add(box);
            CursorTooltip.Attach(box,term.Label+"\n"+term.Detail+"\n정밀값: "+term.Value.ToString("G12",CultureInfo.InvariantCulture));
            box.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,term.Label+" "+term.Value.ToString("G8"));
        }
        var result=new TextBlock{Text="≈ "+model.Result.ToString("N1",CultureInfo.InvariantCulture),FontFamily=MathFont,FontSize=23,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,14,0,0)};
        result.SetResourceReference(TextBlock.ForegroundProperty,"ChartAccentBrush");factors.Children.Add(new Border{Child=result,MinWidth=112,Height=55,Padding=new(0,0,8,0)});
    }
    internal void Highlight(IEnumerable<string> terms,string? explanation=null,bool immediate=false)
    {
        var selected=terms.ToHashSet();hint.Text=explanation??defaultHint;
        hint.ToolTip=explanation??(Equation?.Limitations is {Length:>0} pending?string.Join("\n\n",pending):hint.Text);
        foreach(var (id,cell) in cells)
        {
            var active=selected.Contains(id);cell.Highlighted=active;
            var color=((SolidColorBrush)Application.Current.Resources[active?"ChartAccentBrush":"ResultsTextBrush"]).Color;
            var from=(cell.Number.Foreground as SolidColorBrush)?.Color??color;
            var brush=new SolidColorBrush(from);cell.Number.Foreground=brush;
            cell.Label.SetResourceReference(TextBlock.ForegroundProperty,active?"ChartAccentBrush":"ResultsMutedBrush");
            var animate=!immediate&&!ReduceMotion&&SystemParameters.ClientAreaAnimation;
            var size=active?24d:20d;
            if(animate)
            {
                var duration=new Duration(TimeSpan.FromMilliseconds(active?160:190));
                cell.Number.BeginAnimation(TextBlock.FontSizeProperty,new DoubleAnimation(size,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);
                brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(color,duration));
                cell.Accent.BeginAnimation(OpacityProperty,new DoubleAnimation(active?1:0,duration));
            }
            else
            {
                cell.Number.BeginAnimation(TextBlock.FontSizeProperty,null);cell.Number.FontSize=size;brush.Color=color;
                cell.Accent.BeginAnimation(OpacityProperty,null);cell.Accent.Opacity=active?1:0;
            }
        }
    }
    internal void Retheme()=>Highlight(Highlighted,null,true);
    internal double FontSizeFor(string id)=>cells[id].Number.FontSize;
    internal string FontNameFor(string id)=>cells[id].Number.FontFamily.Source;
    private static string Format(double value)=>Math.Abs(value)>=100000||value!=0&&Math.Abs(value)<.001?value.ToString("0.###E+0",CultureInfo.InvariantCulture):value.ToString(value>=1000?"#,0.##":"0.###",CultureInfo.InvariantCulture);
}
