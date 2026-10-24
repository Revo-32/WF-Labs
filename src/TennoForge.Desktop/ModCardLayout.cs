using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TennoForge.Desktop;

// One layout for melee, primary and secondary cards, including drag previews.
// The drawing brush stretches its 160-unit frame with the card; its inset must
// therefore grow with wide cards rather than staying at a button's default 8px.
internal static class ModCardLayout
{
    internal const double EquippedHeight=92,LibraryHeight=86;
    internal static int LibraryColumns(double width)=>Math.Max(1,(int)Math.Floor((width+6)/165));
    internal static void Apply(Border card,bool library,ModFinish finish)
    {
        card.Height=library?LibraryHeight:EquippedHeight;
        ApplyPadding(card,p=>card.Padding=p,library,finish);
    }
    internal static void Apply(Button card,bool library,ModFinish finish)
    {
        card.Style=(Style)Application.Current.FindResource("ModCardButton");
        card.Height=library?LibraryHeight:EquippedHeight;
        card.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        card.VerticalContentAlignment=VerticalAlignment.Stretch;
        ApplyPadding(card,p=>card.Padding=p,library,finish);
    }
    private static void ApplyPadding(FrameworkElement card,Action<Thickness> set,bool library,ModFinish finish)
    {
        void Update()
        {
            var edge=finish==ModFinish.Amalgam?.09:.055;
            var horizontal=Math.Max(library?10:14,Math.Ceiling(card.ActualWidth*edge+2));
            // Reserve a quiet band below the upper rim for cost/polarity and
            // below the two-line name for the lower corner ornaments.
            set(new(horizontal,14,horizontal,12));
        }
        Update();card.SizeChanged+=(_,e)=>{if(e.WidthChanged)Update();};
    }
    internal static StackPanel Header(string cost,string polarity,string color)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Height=18,Margin=new(0,0,0,2)};
        var value=new TextBlock{Text=cost,FontSize=13,LineHeight=18,Height=18,VerticalAlignment=VerticalAlignment.Center,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,TextWrapping=TextWrapping.NoWrap,Margin=new(0,0,5,0)};
        value.SetResourceReference(TextBlock.ForegroundProperty,color);row.Children.Add(value);
        var icon=PolarityIcon.Create(polarity);icon.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(icon);return row;
    }
}

internal sealed class ModCardFace : Grid
{
    internal bool LibraryCard {get;}
    internal TextBlock ModName {get;}
    internal FrameworkElement Metadata {get;}
    internal string CostText=>((TextBlock)((Panel)Metadata).Children[0]).Text;
    internal ModCardFace(string name,FrameworkElement metadata,bool library)
    {
        LibraryCard=library;Metadata=metadata;
        RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        Children.Add(metadata);
        ModName=new TextBlock{Text=name,FontSize=library?15:18,FontWeight=FontWeights.SemiBold,
            TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
            LineHeight=library?19:22,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,
            Height=library?38:44,VerticalAlignment=VerticalAlignment.Center,Margin=new(0)};
        ModName.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
        SetRow(ModName,1);Children.Add(ModName);
    }
}
