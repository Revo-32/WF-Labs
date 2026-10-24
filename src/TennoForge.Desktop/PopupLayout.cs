using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace TennoForge.Desktop;
internal static class PopupLayout
{
    private sealed class State {internal Popup? Current;}
    private static readonly ConditionalWeakTable<Window,State> owners=new();
    internal static void Close(Window owner){if(owners.TryGetValue(owner,out var state)&&state.Current is {} p)p.IsOpen=false;}
    internal static void Configure(Popup popup,Window owner)
    {
        var state=owners.GetOrCreateValue(owner);
        var anchor=popup.PlacementTarget as FrameworkElement;
        RoutedEventHandler anchorUnloaded=(_,_)=>popup.IsOpen=false;
        popup.StaysOpen=false;popup.Placement=PlacementMode.Custom;
        popup.CustomPopupPlacementCallback=(size,targetSize,offset)=>
        {
            if(popup.PlacementTarget is not FrameworkElement target)return [new(new(0,targetSize.Height+6),PopupPrimaryAxis.Vertical)];
            var origin=target.TranslatePoint(new(),owner);double width=Math.Max(size.Width,owner.ActualWidth),height=Math.Max(size.Height,owner.ActualHeight);
            double x=Math.Clamp(origin.X,8,Math.Max(8,width-size.Width-8));
            double below=origin.Y+targetSize.Height+6;double above=origin.Y-size.Height-6;
            double y=below+size.Height<=height-8?below:above>=8?above:Math.Clamp(below,8,Math.Max(8,height-size.Height-8));
            return [new(new(x-origin.X,y-origin.Y),PopupPrimaryAxis.None)];
        };
        popup.Opened+=(_,_)=>
        {
            if(state.Current is {} previous&&previous!=popup)previous.IsOpen=false;
            state.Current=popup;
            if(anchor is not null){anchor.Unloaded-=anchorUnloaded;anchor.Unloaded+=anchorUnloaded;}
            if(popup.Child is FrameworkElement child){child.MaxWidth=Math.Max(240,Math.Min(owner.ActualWidth-16,SystemParameters.WorkArea.Width-16));child.MaxHeight=Math.Max(240,Math.Min(owner.ActualHeight-16,SystemParameters.WorkArea.Height-16));}
        };
        popup.Closed+=(_,_)=>{if(state.Current==popup)state.Current=null;if(anchor is not null)anchor.Unloaded-=anchorUnloaded;};
        if(popup.Child is UIElement body)body.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!SearchField.HandlesEscape(Keyboard.FocusedElement)){popup.IsOpen=false;popup.PlacementTarget?.Focus();e.Handled=true;}};
    }
}
