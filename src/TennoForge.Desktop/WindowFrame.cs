using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Path = System.Windows.Shapes.Path;

namespace TennoForge.Desktop;

// Keep the native HWND and resize/caption hit testing. No layered window or blur loop.
internal static class WindowFrame
{
    internal const double HeaderHeight = 64;
    internal static Border Attach(Window window, FrameworkElement content)
    {
        window.Icon = BrandIcon.WindowIcon;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.CanResize;
        window.Background = Brushes.Transparent;
        window.UseLayoutRounding = true;
        TextRendering.Apply(window,VisualTreeHelper.GetDpi(window));
        window.SourceInitialized+=(_,_)=>TextRendering.Apply(window,VisualTreeHelper.GetDpi(window));
        window.DpiChanged+=(_,e)=>TextRendering.Apply(window,e.NewDpi);
        TextOptions.SetTextHintingMode(window, TextHintingMode.Fixed);
        var systemCorners = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = HeaderHeight,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = systemCorners ? new Thickness(1) : new Thickness(0),
            CornerRadius = systemCorners ? new CornerRadius(0) : new CornerRadius(12),
            UseAeroCaptionButtons = false
        });
        var shell = new Border { Child = new System.Windows.Documents.AdornerDecorator{Child=content}, BorderThickness = new Thickness(1) };
        shell.SnapsToDevicePixels=true;
        shell.SetResourceReference(Border.BackgroundProperty,"ResultsBackgroundBrush");
        RenderOptions.SetClearTypeHint(shell,ClearTypeHint.Enabled);
        shell.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
        void Shape()
        {
            var radius = window.WindowState == WindowState.Maximized ? 0 : systemCorners ? 8 : 12;
            shell.CornerRadius = new CornerRadius(radius);
            content.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, content.ActualWidth), Math.Max(0, content.ActualHeight)), radius, radius);
        }
        content.SizeChanged += (_, _) => Shape();
        window.StateChanged += (_, _) => Shape();
        window.Content = shell;
        return shell;
    }

    internal static StackPanel Controls(Window window)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        WindowChrome.SetIsHitTestVisibleInChrome(panel, true);
        var minimize = IconButton("최소화", "M 2,8 L 12,8", () => SystemCommands.MinimizeWindow(window));
        var maximize = IconButton("최대화", "M 2,2 L 12,2 12,12 2,12 Z", () =>
        {
            if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
            else SystemCommands.MaximizeWindow(window);
        });
        var close = IconButton("닫기", "M 2,2 L 12,12 M 12,2 L 2,12", () => SystemCommands.CloseWindow(window));
        close.Style = (Style)Application.Current.Resources["CloseWindowButton"];
        window.StateChanged += (_, _) =>
        {
            var restored = window.WindowState != WindowState.Maximized;
            var name = restored ? "최대화" : "이전 크기로 복원";
            maximize.ToolTip = name;
            AutomationProperties.SetName(maximize, name);
            ((Path)maximize.Content).Data = Geometry.Parse(restored ? "M 2,2 L 12,2 12,12 2,12 Z" : "M 4,2 L 12,2 12,10 M 2,4 L 10,4 10,12 2,12 Z");
        };
        panel.Children.Add(minimize); panel.Children.Add(maximize); panel.Children.Add(close);
        return panel;
    }

    private static Button IconButton(string label, string geometry, Action action)
    {
        var icon = new Path { Data = Geometry.Parse(geometry), Width = 14, Height = 14, StrokeThickness = 1.25, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TextBrush");
        var button = new Button { Content = icon, ToolTip = label, Style = (Style)Application.Current.Resources["WindowButton"] };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }
}
