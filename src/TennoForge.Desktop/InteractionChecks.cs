using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class InteractionChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    internal static object Tooltip()
    {
        var cases=new List<object>();
        foreach(var scale in new[]{1d,1.25,1.5})
        {
            var area=new Rect(-1920/scale,0,1920/scale,1080/scale);var size=new Size(320,180);
            foreach(var point in new[]{new Point(area.Left+10,10),new Point(area.Right-10,10),new Point(area.Left+10,area.Bottom-10),new Point(area.Right-10,area.Bottom-10)})
            {
                var at=CursorTooltip.Place(point,size,area);var rect=new Rect(at,size);
                if(!area.Contains(rect)||rect.Contains(point))throw new Exception("툴팁의 가장자리 배치·커서 가림 검사 실패");
                cases.Add(new{scale,x=point.X,y=point.Y,tooltipX=at.X,tooltipY=at.Y,passed=true});
            }
        }
        // Invisible test window/popup in the work area: a real popup HWND, no cursor movement.
        var owner=new Border{Width=600,Height=400,Background=Brushes.Transparent};
        var host=new Window{Content=owner,Width=620,Height=420,Left=SystemParameters.WorkArea.Left+40,Top=SystemParameters.WorkArea.Top+40,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,Opacity=0,ShowInTaskbar=false,ShowActivated=false};
        var tip=CursorTooltip.Attach(owner,p=>p.X<100?"충격\n비치명 피해 413.8\n전체의 2.05%":"바이러스\n비치명 피해 9,945.1\n전체의 49.2%",0);tip.Tip.Opacity=0;
        try
        {
            host.Show();host.UpdateLayout();
            tip.MoveForCheck(new Point(40,40));tip.Tip.IsOpen=true;Settle();tip.MoveForCheck(new Point(40,40));Settle();var first=RectForTip();
            tip.MoveForCheck(new Point(180,110));Settle();var second=RectForTip();
            var dpi=VisualTreeHelper.GetDpi(owner);
            if(Math.Abs((second.Left-first.Left)-140*dpi.DpiScaleX)>2||Math.Abs((second.Top-first.Top)-70*dpi.DpiScaleY)>2)throw new Exception($"실제 툴팁이 커서를 따라 이동하지 않음: {second.Left-first.Left}, {second.Top-first.Top}");
            var body=(StackPanel)tip.Tip.Content;
            if(((TextBlock)body.Children[0]).Text!="바이러스"||((TextBlock)body.Children[1]).LineHeight<22)throw new Exception("툴팁 내용·행간 갱신 실패");
            var fontMode=TextOptions.GetTextFormattingMode(body.Children[0]);
            if(fontMode!=TextRendering.Formatting(dpi))throw new Exception("열린 툴팁의 DPI별 글자 배치가 본문과 다름");
            var area=CursorTooltip.WorkArea(owner,new Point(180,110));var edgePoint=new Point(area.Right-10,area.Bottom-10);
            tip.MoveForCheck(edgePoint);Settle();
            var edgeRect=RectForTip();var cursor=owner.PointToScreen(edgePoint);
            if(edgeRect.Right>=cursor.X||edgeRect.Bottom>=cursor.Y)throw new Exception("실제 툴팁 창이 우하단 커서를 가리거나 반전 배치되지 않음");
            owner.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
            if(tip.Tip.IsOpen)throw new Exception("드래그 시작 시 툴팁 닫기 실패");
            tip.MoveForCheck(new Point(40,40));tip.Tip.IsOpen=true;Settle();
            owner.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});
            if(tip.Tip.IsOpen)throw new Exception("목록 스크롤 시 툴팁 닫기 실패");
            tip.Tip.IsOpen=true;Settle();
            host.Close();Settle();if(tip.Tip.IsOpen)throw new Exception("화면 해제 후 툴팁이 남아 있음");
            return new{passed=true,edgeCases=cases,nativeMove=new{x=second.Left-first.Left,y=second.Top-first.Top,scale=dpi.DpiScaleX},nativeEdgeFlip=true,dragCloses=true,scrollCloses=true,fontMode=fontMode.ToString(),lineHeight=22,unloadCloses=true};
        }
        finally{tip.Close();host.Close();}
        NativeRect RectForTip(){if(PresentationSource.FromVisual(tip.Tip) is not HwndSource source||!GetWindowRect(source.Handle,out var r))throw new Exception("툴팁 HWND를 찾지 못함");return r;}
        static void Settle(){Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
    }
}

public sealed partial class MainWindow
{
    internal Window TypographyForCheck()
    {
        var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};
        foreach(var mode in new[]{(TextFormattingMode.Ideal,TextRenderingMode.Grayscale),(TextFormattingMode.Display,TextRenderingMode.Auto),(TextFormattingMode.Display,TextRenderingMode.Grayscale)})
        {
            var stack=new StackPanel{Margin=new(16)};stack.Children.Add(T($"{mode.Item1} · {mode.Item2}",14));
            foreach(var size in new[]{12d,13,14,16})
            {
                var line=T("코스트순  프레셔 포인트\n충격 · 비치명 피해 413.8",size);TextOptions.SetTextFormattingMode(line,mode.Item1);TextOptions.SetTextRenderingMode(line,mode.Item2);stack.Children.Add(line);
            }
            grid.Children.Add(stack);
        }
        var shell=new Border{Child=grid};shell.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");RenderOptions.SetClearTypeHint(shell,ClearTypeHint.Enabled);
        return new Window{Content=shell,UseLayoutRounding=true};
    }
    internal object CheckInteractions()
    {
        var sort=board.CheckSortDirections();
        var names=new[]{"Pressure Point","Fury","Condition Overload","Primed Pressure Point","Galvanized Steel","Sacrificial Steel","Amalgam Organ Shatter","Melee Riven Mod"};
        var expected=new[]{ModFinish.Common,ModFinish.Uncommon,ModFinish.Rare,ModFinish.Legendary,ModFinish.Galvanized,ModFinish.Umbra,ModFinish.Amalgam,ModFinish.Riven};
        for(var i=0;i<names.Length;i++)if(ModAppearance.For(catalog.Mods.Single(m=>m.EnglishName==names[i]))!=expected[i])throw new Exception("위키 ID에 따른 모드 등급 마감 불일치: "+names[i]);
        if(catalog.Mods.Any(m=>ModAppearance.For(m)==ModFinish.Neutral))throw new Exception("보관함 모드 등급 메타데이터 누락");
        return new{passed=true,sort,appearanceCount=catalog.Mods.Length,appearanceExamples=names,tooltip=InteractionChecks.Tooltip()};
    }
    internal Window DetailPreviewForCheck(string kind)
    {
        FrameworkElement content=kind switch
        {
            "finishes"=>board.AppearanceGallery(),
            "ascending"=>board.SortPreviewForCheck(false),
            "descending"=>board.SortPreviewForCheck(true),
            _=>board.DetailsForCheck()
        };
        var shell=new Border{Child=content,Padding=new(16)};shell.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");
        return new Window{Content=shell,UseLayoutRounding=true};
    }
}
