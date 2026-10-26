using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class ScheduleOverlayView : Grid
{
    private static readonly Brush White=new SolidColorBrush(Color.FromRgb(244,244,242)),Muted=new SolidColorBrush(Color.FromRgb(193,199,204)),Accent=new SolidColorBrush(Color.FromRgb(242,203,87));
    private readonly TextBlock clock=Label("",18),source=Label("",11);
    private readonly ContentBody body=new(),duplicate=new();
    private double textScale=double.NaN,shadowStrength=-1;private bool shadowEnabled;
    internal VerticalMarquee Marquee {get;}
    internal FrameworkElement PrimaryContent=>body;
    internal TextBlock ClockForCheck=>clock;
    internal ScheduleOverlayData? Model {get;private set;}
    internal ScheduleOverlayView()
    {
        Margin=new(16,12,16,12);RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var header=new Grid{Margin=new(0,0,0,12)};source.Foreground=Muted;source.HorizontalAlignment=HorizontalAlignment.Right;source.VerticalAlignment=VerticalAlignment.Center;clock.FontWeight=FontWeights.SemiBold;header.Children.Add(clock);header.Children.Add(source);Children.Add(header);
        Marquee=new(body,duplicate);Grid.SetRow(Marquee,1);Children.Add(Marquee);
    }
    private static TextBlock Label(string text,double size)
    {
        var t=new TextBlock{Text=text,FontSize=size,Foreground=White,TextWrapping=TextWrapping.Wrap,LineHeight=size*1.4,LineStackingStrategy=LineStackingStrategy.BlockLineHeight};
        TextOptions.SetTextFormattingMode(t,TextFormattingMode.Ideal);TextOptions.SetTextRenderingMode(t,TextRenderingMode.Grayscale);Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);return t;
    }
    internal void SuspendMotion(bool suspended)=>Marquee.Suspend(suspended);
    internal void Update(string time,ScheduleOverlayData data,SchedulePreferences preferences)
    {
        Model=data;clock.Text=time;source.Text=data.Source;
        if(textScale!=preferences.TextScale||shadowEnabled!=preferences.TextShadow||shadowStrength!=preferences.ShadowStrength)
        {
            textScale=preferences.TextScale;shadowEnabled=preferences.TextShadow;shadowStrength=preferences.ShadowStrength;clock.FontSize=18*textScale;clock.LineHeight=26*textScale;source.FontSize=11*textScale;source.LineHeight=source.FontSize*1.4;
            Shadow(clock,shadowEnabled,shadowStrength);Shadow(source,shadowEnabled,shadowStrength);
        }
        body.Update(data,preferences);duplicate.Update(data,preferences);Marquee.SetScale(preferences.TextScale);
    }
    internal static void Shadow(UIElement text,bool enabled,double strength)=>text.Effect=enabled&&strength>0?new DropShadowEffect{Color=Colors.Black,BlurRadius=4,ShadowDepth=1,Opacity=strength,RenderingBias=RenderingBias.Performance}:null;
    private sealed class ContentBody : Grid
    {
        private readonly TextBlock notice=Label("",12),timerHeading=Label("일정",12),fissureHeading=Label("성유물 균열",12);
        private readonly Grid sections=new();
        private readonly StackPanel timers=new(),fissures=new();
        private readonly Border timerSection,fissureSection;
        private readonly Dictionary<string,EntryView> timerRows=new(),fissureRows=new();
        private double scale=double.NaN,shadowStrength=-1;private bool shadowEnabled;private bool? layoutWide,layoutBoth;
        internal ContentBody()
        {
            RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=GridLength.Auto});
            timerSection=Section(timerHeading,timers);fissureSection=Section(fissureHeading,fissures);sections.Children.Add(timerSection);sections.Children.Add(fissureSection);Children.Add(sections);
            notice.Foreground=Muted;notice.Margin=new(0,8,0,0);Grid.SetRow(notice,1);Children.Add(notice);SizeChanged+=(_,_)=>ArrangeSections();
        }
        private static Border Section(TextBlock title,UIElement content)
        {
            var panel=new StackPanel();title.Foreground=Muted;title.Margin=new(0,0,0,5);panel.Children.Add(title);panel.Children.Add(content);return new Border{Child=panel,BorderBrush=new SolidColorBrush(Color.FromArgb(90,120,126,133))};
        }
        internal void Update(ScheduleOverlayData data,SchedulePreferences preferences)
        {
            bool appearance=scale!=preferences.TextScale||shadowEnabled!=preferences.TextShadow||shadowStrength!=preferences.ShadowStrength;
            scale=preferences.TextScale;shadowEnabled=preferences.TextShadow;shadowStrength=preferences.ShadowStrength;
            notice.Text=data.Notice;notice.Visibility=data.Notice.Length==0?Visibility.Collapsed:Visibility.Visible;
            SetRows(timers,timerRows,data.Timers,false,false);SetRows(fissures,fissureRows,data.Fissures,true,data.FissureFilter!="전체");
            fissureHeading.Text=$"성유물 균열 · {data.FissureFilter} · {data.Fissures.Length}";timerHeading.Text=data.Timers.Any(t=>t.Kind=="alert")?"일정 · 얼럿":"일정";
            timerSection.Visibility=data.Timers.Length==0?Visibility.Collapsed:Visibility.Visible;fissureSection.Visibility=data.ShowFissures?Visibility.Visible:Visibility.Collapsed;
            if(appearance)
            {
                foreach(var text in new[]{timerHeading,fissureHeading,notice}){text.FontSize=12*scale;text.LineHeight=text.FontSize*1.4;Shadow(text,shadowEnabled,shadowStrength);}
                foreach(var entry in timerRows.Values.Concat(fissureRows.Values))entry.Appearance(scale,shadowEnabled,shadowStrength);
            }
            ArrangeSections();
        }
        private void SetRows(StackPanel host,Dictionary<string,EntryView> views,ScheduleOverlayEntry[] data,bool fissure,bool hideMode)
        {
            bool rebuild=!host.Children.OfType<EntryView>().Select(v=>v.Id).SequenceEqual(data.Select(d=>d.Id));
            if(rebuild){host.Children.Clear();var keep=data.Select(r=>r.Id).ToHashSet();foreach(var key in views.Keys.Where(k=>!keep.Contains(k)).ToArray())views.Remove(key);}
            foreach(var row in data){if(!views.TryGetValue(row.Id,out var view)){view=new(row.Id,fissure);views[row.Id]=view;view.Appearance(scale,shadowEnabled,shadowStrength);}view.Update(row,hideMode);if(rebuild)host.Children.Add(view);}
        }
        private void ArrangeSections()
        {
            bool both=timerSection.Visibility==Visibility.Visible&&fissureSection.Visibility==Visibility.Visible,wide=ActualWidth>=660*scale&&both;
            if(layoutWide==wide&&layoutBoth==both)return;layoutWide=wide;layoutBoth=both;
            sections.RowDefinitions.Clear();sections.ColumnDefinitions.Clear();timerSection.Margin=new(0);fissureSection.Margin=new(0);fissureSection.BorderThickness=new(0);fissureSection.Padding=new(0);
            Grid.SetRow(timerSection,0);Grid.SetColumn(timerSection,0);Grid.SetRow(fissureSection,0);Grid.SetColumn(fissureSection,0);
            if(wide){sections.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});sections.ColumnDefinitions.Add(new(){Width=new(1.2,GridUnitType.Star)});Grid.SetColumn(fissureSection,1);timerSection.Margin=new(0,0,16,0);fissureSection.BorderThickness=new(1,0,0,0);fissureSection.Padding=new(16,0,0,0);}
            else if(both){sections.RowDefinitions.Add(new(){Height=GridLength.Auto});sections.RowDefinitions.Add(new(){Height=GridLength.Auto});Grid.SetRow(fissureSection,1);timerSection.Margin=new(0,0,0,12);fissureSection.BorderThickness=new(0,1,0,0);fissureSection.Padding=new(0,12,0,0);}
        }
    }
    private sealed class EntryView : Border
    {
        internal string Id {get;}
        private readonly TextBlock title=Label("",13),detail=Label("",12),value=Label("",18),badge=Label("",11);
        private readonly bool fissure;
        internal EntryView(string id,bool isFissure)
        {
            Id=id;fissure=isFissure;Margin=new(0);Padding=new(0,5,0,5);BorderThickness=new(0,0,0,1);BorderBrush=new SolidColorBrush(Color.FromArgb(35,170,178,187));
            var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            title.FontWeight=FontWeights.Medium;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;title.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(title);
            badge.Foreground=Muted;badge.TextWrapping=TextWrapping.NoWrap;badge.Margin=new(6,0,8,0);badge.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(badge,1);grid.Children.Add(badge);
            value.FontWeight=FontWeights.Medium;value.Margin=new(8,0,0,0);value.HorizontalAlignment=HorizontalAlignment.Right;value.VerticalAlignment=VerticalAlignment.Center;value.TextWrapping=TextWrapping.NoWrap;Grid.SetColumn(value,2);grid.Children.Add(value);
            detail.Foreground=Muted;detail.TextWrapping=TextWrapping.NoWrap;detail.TextTrimming=TextTrimming.CharacterEllipsis;detail.Margin=new(0,2,0,0);Grid.SetRow(detail,1);Grid.SetColumnSpan(detail,3);grid.Children.Add(detail);Child=grid;
        }
        internal void Update(ScheduleOverlayEntry row,bool hideMode)
        {
            string note=fissure?row.Detail:row.Detail+(row.Badge.Length>0?" · "+row.Badge:"");string tag=fissure&&!hideMode?row.Badge.Split(" · ")[0]:"";
            if(title.Text!=row.Title)title.Text=row.Title;if(detail.Text!=note)detail.Text=note;if(value.Text!=row.Value)value.Text=row.Value;if(badge.Text!=tag)badge.Text=tag;badge.Visibility=tag.Length==0?Visibility.Collapsed:Visibility.Visible;value.Foreground=row.Highlight?Accent:White;badge.Foreground=Muted;
            ToolTip=$"{row.Title}\n{row.Detail}\n{row.Badge} · {row.Value}";
            Tag=fissure?"fissure":row.Kind;System.Windows.Automation.AutomationProperties.SetName(this,(string)ToolTip);
        }
        internal void Appearance(double scale,bool shadow,double strength)
        {title.FontSize=13*scale;detail.FontSize=12*scale;badge.FontSize=10*scale;value.FontSize=(fissure?14:16)*scale;foreach(var t in new[]{title,detail,value,badge}){t.LineHeight=t.FontSize*1.4;Shadow(t,shadow,strength);}}
    }
}
