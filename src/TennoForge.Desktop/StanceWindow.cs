using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed class StanceWindow : Window
{
    public StanceWindow(Stance stance,StanceCatalog catalog)
    {
        Title=$"WF Labs · {stance.Name}";Width=840;Height=760;MinWidth=620;MinHeight=480;
        FontFamily=(FontFamily)Application.Current.Resources["Wanted"];WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SourceInitialized+=(_,_)=>Backdrop.Apply(this,false,Theme.IsDark);
        var p=new StackPanel{Margin=new(26,8,26,26)};
        p.Children.Add(Label(stance.Name,26));p.Children.Add(Label(stance.Id+" · WF Wiki 기준",14,"AccentBrush"));
        p.Children.Add(Label($"위키 개정 {catalog.RevisionTime[..10]} · revision {catalog.Revision}",12,"MutedBrush"));
        p.Children.Add(Label("타격 피해 → 해당 타격의 강제·확률 프록 → 다음 타격의 CO 순서로 계산합니다. 지속 피해 틱은 교전 직접 DPS에 포함하지 않습니다.",14,"MutedBrush"));
        foreach(var c in stance.Combos)
        {
            var box=new StackPanel();box.Children.Add(Label($"{c.InputLabel}  ·  {c.Name}",18));
            box.Children.Add(Label($"{c.HitCount}회 타격 · 기본 동작 시간 {(c.Duration.HasValue?$"{c.Duration:0.###}초":"미기재")} · 모션 배율 합계 {c.MotionSum:0.##}×",13,"AccentBrush"));
            var firstHit=1;var co=StanceHitCoCatalog.Load().Find(stance.Id,c.Key);var directIndex=0;
            foreach(var h in c.Hits)
            {
                if(h.Multiplier==0){box.Children.Add(Label($"동작 {h.Group}   회복 동작 · 피해 없음",12,"MutedBrush"));continue;}
                var procs=string.Join(", ",h.ForcedProcs.Select(Proc));
                var modifiers=new List<string>();if(h.Slash!=1)modifiers.Add($"베기 ×{h.Slash}");if(h.Puncture!=1)modifiers.Add($"관통 ×{h.Puncture}");if(h.Impact!=1)modifiers.Add($"충격 ×{h.Impact}");if(h.StatusMultiplier!=1)modifiers.Add($"상태확률 ×{h.StatusMultiplier}");
                var radial=h.AttackType=="Slam"&&c.Key!="Slam";
                var coText=!radial&&co is not null?$" · CO 배율 {co.Factors[directIndex]:0.###}×":"";
                box.Children.Add(Label($"{firstHit}"+(h.Count>1?$"–{firstHit+h.Count-1}":"")+$"타 · 입력 {h.Group} · 피해 {h.Multiplier:0.##}×"+(procs.Length>0?$"   |   {procs}":"")+(modifiers.Count>0?"   |   "+string.Join(", ",modifiers):"")+coText+(radial?"   |   방사 슬램 · 직접 대상 제외":""),14,"MutedBrush"));
                if(!radial){firstHit+=h.Count;directIndex++;}
            }
            var card=new Border{Child=box,Padding=new(0,16,0,8),Margin=new(0,8,0,0),BorderThickness=new(0,1,0,0)};card.SetResourceReference(Border.BorderBrushProperty,"StrokeBrush");p.Children.Add(card);
        }
        var source=new Button{Content="WF Wiki 원문 열기",Margin=new(0,16,0,12)};source.Click+=(_,_)=>Process.Start(new ProcessStartInfo(stance.Source){UseShellExecute=true});p.Children.Add(source);
        p.Children.Add(Label("출처: WARFRAME Wiki contributors · CC BY-NC-SA 3.0\n전체 동작 시간과 개별 타격 발생 시각은 서로 다른 데이터입니다.",12,"MutedBrush"));
        var scroller=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var body=new Grid();body.SetResourceReference(Panel.BackgroundProperty,"SolidSurfaceBrush");
        body.RowDefinitions.Add(new(){Height=new(WindowFrame.HeaderHeight-1)});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var controls=WindowFrame.Controls(this);controls.HorizontalAlignment=HorizontalAlignment.Right;controls.Margin=new(0,0,18,0);body.Children.Add(controls);
        Grid.SetRow(scroller,1);body.Children.Add(scroller);WindowFrame.Attach(this,body);
    }
    private static TextBlock Label(string text,double size,string key="TextBrush")
    {var t=new TextBlock{Text=text,FontSize=size,Margin=new(0,0,0,8),TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.ForegroundProperty,key);return t;}
    private static string Proc(string s)=>s.Replace("unresolved:","") switch{"Bleed"=>"강제 베기","Knockback"=>"강제 충격","Weakened"=>"강제 관통","Knockdown"=>"넉다운","Ragdoll"=>"래그돌","Lifted"=>"공중 띄우기","Stagger"=>"비틀거림",var other=>other};
}
