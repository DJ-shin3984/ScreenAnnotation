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

    private WriteableBitmap? _bitmap;

    public void Show(CapturedFrame frame)
    {
        // 모니터당 WriteableBitmap 하나를 재사용: 켤 때마다 수십 MB를 새로 할당·복사하지 않는다.
        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Bgra, frame.Stride, 0);
        _background.Source = _bitmap;
        OverlayNative.FitToMonitor(Handle, Monitor.Bounds);
        Show();
        Activate();
    }

    public new void Hide()
    {
        base.Hide();
        _background.Source = null; // 정지 화면은 더 보이지 않음. 비트맵 버퍼는 다음 켜기를 위해 유지
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
