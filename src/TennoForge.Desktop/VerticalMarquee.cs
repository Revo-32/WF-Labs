using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TennoForge.Desktop;

// Only overflowing, visible overlays own an animation clock. Movement changes
// a render transform; it never rebuilds layout or scrolls the HWND per frame.
internal sealed class VerticalMarquee : Grid
{
    internal const double ReadingPause=2.5,BaseSpeed=12,LoopGap=32;
    private readonly ScrollViewer viewport=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Focusable=false,IsHitTestVisible=false};
    private readonly StackPanel track=new(){VerticalAlignment=VerticalAlignment.Top};
    private readonly Border gap=new(){Height=LoopGap,Visibility=Visibility.Collapsed};
    private readonly FrameworkElement primary,repeat;
    private readonly TranslateTransform translation=new();
    private AnimationClock? animation;
    private bool queued,suspended;
    private double configuredHeight=-1,configuredViewport=-1,configuredWidth=-1,scale=1,configuredScale=-1;
    internal double ContentHeight=>primary.ActualHeight;
    internal double ViewportHeight=>viewport.ViewportHeight;
    internal double Offset=>-translation.Y;
    internal bool Running=>animation is not null;
    internal bool HasAnimation=>translation.HasAnimatedProperties;
    internal double BottomTime {get;private set;}
    internal double LoopTime {get;private set;}
    internal int Revision {get;private set;}
    internal FrameworkElement DuplicateForCheck=>repeat;
    internal VerticalMarquee(FrameworkElement content,FrameworkElement duplicate)
    {
        primary=content;repeat=duplicate;repeat.Visibility=Visibility.Collapsed;track.RenderTransform=translation;
        track.Children.Add(primary);track.Children.Add(gap);track.Children.Add(repeat);viewport.Content=track;Children.Add(viewport);
        Loaded+=(_,_)=>RequestLayout();Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{if(IsVisible)RequestLayout();else Stop();};
        viewport.SizeChanged+=(_,_)=>RequestLayout();primary.SizeChanged+=(_,_)=>RequestLayout();
    }
    internal void SetScale(double value){if(Math.Abs(value-scale)<.001)return;scale=value;RequestLayout();}
    internal void Suspend(bool value){if(suspended==value)return;suspended=value;if(value)Stop();else RequestLayout();}
    private void RequestLayout()
    {
        if(queued)return;queued=true;Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{queued=false;Refresh();}));
    }
    private void Refresh()
    {
        double height=primary.ActualHeight,visible=viewport.ViewportHeight,width=viewport.ViewportWidth;
        if(!IsVisible||suspended||visible<=1||height<=visible+.5)
        {Stop();repeat.Visibility=gap.Visibility=Visibility.Collapsed;return;}
        if(animation is not null&&Math.Abs(height-configuredHeight)<.5&&Math.Abs(visible-configuredViewport)<.5&&Math.Abs(width-configuredWidth)<.5&&Math.Abs(scale-configuredScale)<.001)return;
        Stop();repeat.Visibility=gap.Visibility=Visibility.Visible;gap.Height=LoopGap*scale;
        configuredHeight=height;configuredViewport=visible;configuredWidth=width;configuredScale=scale;
        double speed=BaseSpeed*scale,bottom=height-visible,cycle=height+gap.Height;
        BottomTime=ReadingPause+bottom/speed;LoopTime=ReadingPause+cycle/speed+ReadingPause;
        var movement=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromSeconds(LoopTime),RepeatBehavior=RepeatBehavior.Forever};
        movement.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ReadingPause))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-bottom,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(BottomTime))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-bottom,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(BottomTime+ReadingPause))));
        movement.KeyFrames.Add(new LinearDoubleKeyFrame(-cycle,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(LoopTime))));
        Timeline.SetDesiredFrameRate(movement,30);animation=(AnimationClock)movement.CreateClock(true);translation.ApplyAnimationClock(TranslateTransform.YProperty,animation);Revision++;
    }
    private void Stop()
    {
        if(animation is not null){animation.Controller?.Remove();translation.ApplyAnimationClock(TranslateTransform.YProperty,null);animation=null;}
        translation.Y=0;
    }
    internal void SeekForCheck(double seconds)=>animation?.Controller?.SeekAlignedToLastTick(TimeSpan.FromSeconds(seconds),TimeSeekOrigin.BeginTime);
    internal void PauseForCheck()=>animation?.Controller?.Pause();
    internal void ResumeForCheck()=>animation?.Controller?.Resume();
}
