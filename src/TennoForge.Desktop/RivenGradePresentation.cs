using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class RivenGradePresentation
{
    internal static Color ColorFor(string tier)=>(Color)ColorConverter.ConvertFromString(tier switch{"F"=>"#9AA3AF","C"=>"#CAD2DD","B"=>"#72D3AB","A"=>"#79B8FF","S"=>"#F2CB57","SS"=>"#F4AC72",_=>"#D7A5FF"});
    internal static Border Badge(double low,double? high=null)
    {
        var tier=RivenRating.Tier(low);var top=RivenRating.Tier(high??low);var label=tier==top?tier:tier+"–"+top;var color=ColorFor(tier);
        var strong=tier is "S" or "SS" or "SSS";
        var frame=new Border{CornerRadius=new(4),BorderBrush=new SolidColorBrush(color),BorderThickness=new(strong?1.5:1),Background=new SolidColorBrush(Color.FromArgb(strong?(byte)45:(byte)20,color.R,color.G,color.B))};
        // Keep text outside the effect's visual subtree so the glyphs stay crisp.
        if(strong&&UiMotion.Enabled)frame.Effect=new DropShadowEffect{Color=color,ShadowDepth=0,BlurRadius=tier=="SSS"?12:tier=="SS"?8:5,Opacity=.35};
        var layers=new Grid();layers.Children.Add(frame);layers.Children.Add(new TextBlock{Text=label,FontSize=15,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(color),Margin=new(8,3,8,3),VerticalAlignment=VerticalAlignment.Center});
        var badge=new Border{Margin=new(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,Child=layers};
        badge.ToolTip="WF Labs 수치 등급 · F <20 / C ≥20 / B ≥40 / A ≥60 / S ≥80 / SS ≥90 / SSS ≥97%\n음옵은 페널티가 작을수록 높은 등급 · DPS 점수가 아닙니다.";
        return badge;
    }
}
