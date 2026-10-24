using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TennoForge.Desktop;

// Animate state indicators only. Text, caret geometry and result rows stay still.
internal static class UiMotion
{
    private static bool initialized,reduceMotion;
    private static readonly ConditionalWeakTable<Popup,Disclosure> disclosures=new();
    private static readonly ConditionalWeakTable<ComboBox,object> connectedBoxes=new();
    private static readonly List<WeakReference<Disclosure>> active=[];
    private static readonly List<WeakReference<UIElement>> actions=[];
    internal static bool Enabled=>!reduceMotion&&SystemParameters.ClientAreaAnimation;
    internal static bool ReduceMotion
    {
        get=>reduceMotion;
        set{reduceMotion=value;if(!Enabled)FinishAll();}
    }
    internal static void Initialize()
    {
        if(initialized)return;initialized=true;
        SystemParameters.StaticPropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(SystemParameters.ClientAreaAnimation)&&!Enabled)FinishAll();};
    }
    internal static void ConnectComboBox(ComboBox box)
    {
        void Connect()
        {
            box.ApplyTemplate();
            if(box.Template?.FindName("PART_Popup",box) is not Popup popup)return;
            foreach(var toggle in LayoutDiagnostics.Tree(box).OfType<ToggleButton>().ToArray())
            {toggle.ApplyTemplate();if(toggle.Template?.FindName("Chevron",toggle) is FrameworkElement arrow){Attach(popup,arrow);break;}}
        }
        if(!connectedBoxes.TryGetValue(box,out _)){connectedBoxes.Add(box,new());box.DropDownOpened+=(_,_)=>Connect();}
        Connect();box.Dispatcher.BeginInvoke(new Action(Connect),System.Windows.Threading.DispatcherPriority.Loaded);
    }
    internal static void Attach(Popup popup,FrameworkElement arrow)
    {
        if(disclosures.TryGetValue(popup,out _))return;
        var state=new Disclosure(popup,arrow);disclosures.Add(popup,state);active.RemoveAll(w=>!w.TryGetTarget(out _));active.Add(new(state));
    }
    internal static double Angle(Popup popup)=>disclosures.TryGetValue(popup,out var state)?state.Rotation.Angle:double.NaN;
    internal static bool Animating(Popup popup)=>disclosures.TryGetValue(popup,out var state)&&state.Rotation.HasAnimatedProperties;
    internal static string Diagnostic(ComboBox box)=>$"loaded={box.IsLoaded}, connected={connectedBoxes.TryGetValue(box,out _)}, paths={string.Join(',',LayoutDiagnostics.Tree(box).OfType<Path>().Select(p=>p.Name))}";
    internal static void RevealAction(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
        if(!Enabled||!element.IsVisible)return;
        actions.RemoveAll(w=>!w.TryGetTarget(out var existing)||ReferenceEquals(existing,element));actions.Add(new(element));
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.3,1,TimeSpan.FromMilliseconds(100)){FillBehavior=FillBehavior.Stop});
    }
    private static void FinishAll()
    {
        foreach(var item in active)if(item.TryGetTarget(out var state))state.Sync(false);
        foreach(var item in actions)if(item.TryGetTarget(out var control)){control.BeginAnimation(UIElement.OpacityProperty,null);control.Opacity=1;}
        actions.Clear();active.RemoveAll(w=>!w.TryGetTarget(out _));
    }
    private sealed class Disclosure
    {
        private readonly Popup popup;
        internal readonly RotateTransform Rotation=new();
        internal Disclosure(Popup popup,FrameworkElement arrow)
        {
            this.popup=popup;arrow.RenderTransformOrigin=new(.5,.5);arrow.RenderTransform=Rotation;
            popup.Opened+=(_,_)=>Sync(true);popup.Closed+=(_,_)=>Sync(true);
            arrow.Unloaded+=(_,_)=>Sync(false);Sync(false);
        }
        internal void Sync(bool animate)
        {
            var start=Rotation.Angle;var target=popup.IsOpen?180d:0d;
            Rotation.BeginAnimation(RotateTransform.AngleProperty,null);Rotation.Angle=target;
            if(animate&&Enabled&&Math.Abs(start-target)>.1)
                Rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(start,target,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
        }
    }
}
