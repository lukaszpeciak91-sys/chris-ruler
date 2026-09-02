using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChrisRuler;

/// <summary>
/// Keeps the native window's center outside its input region and supplies standard
/// non-client hit-test results for the remaining frame.
/// </summary>
internal sealed class NativeOverlayWindowBehavior : IDisposable
{
    private const int WmNcHitTest = 0x0084;
    private const int WmSetCursor = 0x0020;
    private const int WmDpiChanged = 0x02E0;

    private const int HtNowhere = 0;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const int Error = 0;
    private const int RgnDiff = 4;
    private const int SmYVirtualScreen = 77;
    private const int SmCyVirtualScreen = 79;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    // These values define both the native input region and the row mask rendered in XAML.
    private const double TopBarHeightDip = 30;
    private const double BottomBarHeightDip = 22;
    private const double SideBarWidthDip = 9;
    private const double ResizeBandThicknessDip = 3;
    private const double CornerLengthDip = 16;
    private const int IdcSizeAll = 32646;

    private readonly Window window;
    private readonly FrameworkElement[] controls;
    private HwndSource? source;
    private DispatcherOperation? pendingRegionUpdate;
    private nint hwnd;
    private bool disposed;
    private nint sizeAllCursor;

    public NativeOverlayWindowBehavior(Window window, params FrameworkElement[] controls)
    {
        this.window = window;
        this.controls = controls;
        window.SourceInitialized += OnSourceInitialized;
        window.SizeChanged += OnSizeChanged;
        window.Loaded += OnLoaded;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        window.SourceInitialized -= OnSourceInitialized;
        window.SizeChanged -= OnSizeChanged;
        window.Loaded -= OnLoaded;
        pendingRegionUpdate?.Abort();
        pendingRegionUpdate = null;

        if (source is not null)
        {
            source.RemoveHook(WindowProc);
            source = null;
        }

        hwnd = nint.Zero;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (disposed || source is not null)
        {
            return;
        }

        hwnd = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(hwnd);
        if (source is null)
        {
            hwnd = nint.Zero;
            return;
        }

        source.AddHook(WindowProc);
        ApplyFrameRegion();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ScheduleFrameRegionUpdate();

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyFrameRegion();

    public void MoveDownOneHeight()
    {
        if (disposed || hwnd == nint.Zero || !GetWindowRect(hwnd, out Rect rect))
        {
            return;
        }

        int height = rect.Bottom - rect.Top;
        int virtualTop = GetSystemMetrics(SmYVirtualScreen);
        int virtualBottom = virtualTop + GetSystemMetrics(SmCyVirtualScreen);

        // Move by one guide height unless that would cross the virtual desktop's
        // bottom edge; in that case, stop flush with the edge instead of disappearing.
        int newTop = Math.Max(virtualTop, Math.Min(rect.Top + height, virtualBottom - height));
        SetWindowPos(hwnd, nint.Zero, rect.Left, newTop, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmNcHitTest)
        {
            handled = true;
            return HitTest(lParam);
        }

        if (message == WmSetCursor && unchecked((short)((long)lParam & 0xFFFF)) == HtCaption)
        {
            sizeAllCursor = sizeAllCursor != nint.Zero ? sizeAllCursor : LoadCursor(nint.Zero, IdcSizeAll);
            if (sizeAllCursor != nint.Zero)
            {
                SetCursor(sizeAllCursor);
                handled = true;
                return 1;
            }
        }

        if (message == WmDpiChanged)
        {
            // WPF applies the suggested DPI bounds after this hook returns.
            ScheduleFrameRegionUpdate();
        }

        return nint.Zero;
    }

    private nint HitTest(nint lParam)
    {
        if (!GetWindowRect(hwnd, out Rect windowRect))
        {
            // Do not turn an unknown area into a draggable surface when native state
            // is unavailable. A later hit test can recover normally.
            return HtNowhere;
        }

        // WM_NCHITTEST packs signed virtual-screen coordinates into LPARAM.
        // Sign extension is required for monitors left of or above the primary display.
        int screenX = unchecked((short)((long)lParam & 0xFFFF));
        int screenY = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
        int x = screenX - windowRect.Left;
        int y = screenY - windowRect.Top;
        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top;
        if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= width || y >= height)
        {
            return HtNowhere;
        }

        // Explicit client areas take priority over the nearby top/right resize zones.
        // The native region contains only these compact rectangles and the frame.
        if (controls.Any(control => GetControlRect(control).Contains(x, y)))
        {
            return 1; // HTCLIENT lets WPF deliver the button input.
        }

        int maximumInset = Math.Min(width, height) / 2;
        int leftBar = Math.Min(DipToPixels(SideBarWidthDip), maximumInset);
        int rightBar = leftBar;
        int topBar = Math.Min(DipToPixels(TopBarHeightDip), maximumInset);
        int bottomBar = Math.Min(DipToPixels(BottomBarHeightDip), maximumInset);
        int resizeBand = Math.Min(DipToPixels(ResizeBandThicknessDip),
            Math.Min(Math.Min(leftBar, rightBar), Math.Min(topBar, bottomBar)));
        int cornerLength = Math.Min(
            DipToPixels(CornerLengthDip),
            Math.Min(width / 2, height / 2));

        bool leftFrame = x < leftBar;
        bool rightFrame = x >= width - rightBar;
        bool topFrame = y < topBar;
        bool bottomFrame = y >= height - bottomBar;

        // Corners use a short length along both adjoining visible sides. This keeps
        // diagonal resize practical without extending the hit target into the center.
        if ((topFrame && x < cornerLength) || (leftFrame && y < cornerLength)) return HtTopLeft;
        if ((topFrame && x >= width - cornerLength) || (rightFrame && y < cornerLength)) return HtTopRight;
        if ((bottomFrame && x < cornerLength) || (leftFrame && y >= height - cornerLength)) return HtBottomLeft;
        if ((bottomFrame && x >= width - cornerLength) || (rightFrame && y >= height - cornerLength)) return HtBottomRight;

        // Only a narrow outer band resizes. The substantial inner portion of each
        // frame bar remains an easy-to-grab caption surface.
        if (x < resizeBand) return HtLeft;
        if (x >= width - resizeBand) return HtRight;
        if (y < resizeBand) return HtTop;
        if (y >= height - resizeBand) return HtBottom;

        // The inner part of the frame acts like a title bar, without showing one.
        return HtCaption;
    }

    private void ApplyFrameRegion()
    {
        if (disposed || hwnd == nint.Zero || !GetWindowRect(hwnd, out Rect windowRect))
        {
            return;
        }

        // SetWindowRgn coordinates are window-relative. WindowStyle=None together
        // with AllowsTransparency means this WPF HWND has no separate native frame,
        // so the WPF border fills these full window dimensions.
        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top;
        if (width < 2 || height < 2)
        {
            return;
        }

        int maximumInset = Math.Min(width, height) / 2;
        int sideBar = Math.Min(DipToPixels(SideBarWidthDip), maximumInset);
        int topBar = Math.Min(DipToPixels(TopBarHeightDip), maximumInset);
        int bottomBar = Math.Min(DipToPixels(BottomBarHeightDip), maximumInset);
        nint outerRegion = CreateRectRgn(0, 0, width, height);
        if (outerRegion == nint.Zero)
        {
            return;
        }

        nint innerRegion = CreateRectRgn(sideBar, topBar, width - sideBar, height - bottomBar);
        if (innerRegion == nint.Zero)
        {
            DeleteObject(outerRegion);
            return;
        }

        int regionType = CombineRgn(outerRegion, outerRegion, innerRegion, RgnDiff);
        DeleteObject(innerRegion);
        if (regionType == Error)
        {
            DeleteObject(outerRegion);
            return;
        }

        // Unlike HTTRANSPARENT, a native window-region hole is absent from desktop
        // hit testing entirely, so clicks reach windows owned by other processes too.
        if (SetWindowRgn(hwnd, outerRegion, true) == 0)
        {
            DeleteObject(outerRegion);
        }
        // On success Windows owns outerRegion; it must not be deleted here.
    }

    private void ApplyPendingFrameRegion()
    {
        pendingRegionUpdate = null;
        ApplyFrameRegion();
    }

    private void ScheduleFrameRegionUpdate()
    {
        pendingRegionUpdate?.Abort();
        pendingRegionUpdate = window.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(ApplyPendingFrameRegion));
    }

    private int DipToPixels(double dip)
    {
        uint dpi = GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            // WPF's per-monitor value is the safest fallback if the native query
            // temporarily fails during a window/DPI transition.
            double wpfDpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX;
            dpi = double.IsFinite(wpfDpi) && wpfDpi > 0
                ? (uint)Math.Round(wpfDpi)
                : 96;
        }

        return Math.Max(1, (int)Math.Ceiling(dip * dpi / 96.0));
    }

    private PixelRect GetControlRect(FrameworkElement control)
    {
        if (!control.IsLoaded || control.ActualWidth <= 0 || control.ActualHeight <= 0)
        {
            return default;
        }

        Point topLeft = control.TransformToAncestor(window).Transform(new Point(0, 0));
        return new PixelRect(
            DipToPixels(topLeft.X),
            DipToPixels(topLeft.Y),
            DipToPixels(topLeft.X + control.ActualWidth),
            DipToPixels(topLeft.Y + control.ActualHeight));
    }

    private readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint LoadCursor(nint instance, int cursorName);

    [DllImport("user32.dll")]
    private static extern nint SetCursor(nint cursor);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(nint destination, nint source1, nint source2, int combineMode);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint value);
}
