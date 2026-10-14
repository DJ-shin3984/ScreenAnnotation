using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Windows.Overlay;

namespace ScreenAnnotation.Rendering;

/// <summary>모니터 하나를 덮는 불투명 전체 화면 창. 루트의 ScaleTransform 하나로 자식은 물리 픽셀 좌표에 그린다.</summary>
public sealed class OverlayWindow : Window
{
    private readonly ScaleTransform _dpiScale;
    private readonly Image _background = new() { Stretch = Stretch.None, SnapsToDevicePixels = true, UseLayoutRounding = true };

    public MonitorInfo Monitor { get; }
    public nint Handle { get; }

    /// <summary>자식 요소를 두는 루트. 물리 픽셀 단위.</summary>
    public Grid Root { get; }

    public OverlayWindow(MonitorInfo monitor)
    {
        Monitor = monitor;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = true;
        AllowsTransparency = false;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;

        RenderOptions.SetBitmapScalingMode(_background, BitmapScalingMode.NearestNeighbor);
        _dpiScale = new ScaleTransform(96.0 / monitor.DpiX, 96.0 / monitor.DpiY);
        Root = new Grid { LayoutTransform = _dpiScale, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        Root.Children.Add(_background);
        Content = Root;

        Handle = new WindowInteropHelper(this).EnsureHandle();
        OverlayNative.MakeToolWindow(Handle);
    }

    public void Show(CapturedFrame frame)
    {
        _background.Source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Bgra, frame.Stride);
        OverlayNative.FitToMonitor(Handle, Monitor.Bounds);
        Show();
        Activate();
    }

    public new void Hide()
    {
        base.Hide();
        _background.Source = null; // 캡처 메모리 해제
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        OverlayNative.FitToMonitor(Handle, Monitor.Bounds);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _dpiScale.ScaleX = 1 / newDpi.DpiScaleX;
        _dpiScale.ScaleY = 1 / newDpi.DpiScaleY;
        OverlayNative.FitToMonitor(Handle, Monitor.Bounds);
    }
}
