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
    private const double FrameThicknessDip = 4;
    private const double ResizeBandThicknessDip = 2;
    private const double CornerLengthDip = 12;

    private readonly Window window;
    private HwndSource? source;
    private DispatcherOperation? pendingRegionUpdate;
    private nint hwnd;
    private bool disposed;

    public NativeOverlayWindowBehavior(Window window)
    {
        this.window = window;
        window.SourceInitialized += OnSourceInitialized;
        window.SizeChanged += OnSizeChanged;
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

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyFrameRegion();

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmNcHitTest)
        {
            handled = true;
            return HitTest(lParam);
        }

        if (message == WmDpiChanged)
        {
            // WPF applies the suggested DPI bounds after this hook returns.
            pendingRegionUpdate?.Abort();
            pendingRegionUpdate = window.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(ApplyPendingFrameRegion));
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

        int maximumInset = Math.Min(width, height) / 2;
        int frame = Math.Min(DipToPixels(FrameThicknessDip), maximumInset);
        int resizeBand = Math.Min(DipToPixels(ResizeBandThicknessDip), frame);
        int cornerLength = Math.Min(
            DipToPixels(CornerLengthDip),
            Math.Min(width / 2, height / 2));

        bool leftFrame = x < frame;
        bool rightFrame = x >= width - frame;
        bool topFrame = y < frame;
        bool bottomFrame = y >= height - frame;

        // Corners use a short length along both adjoining visible sides. This keeps
        // diagonal resize practical without extending the hit target into the center.
        if ((topFrame && x < cornerLength) || (leftFrame && y < cornerLength)) return HtTopLeft;
        if ((topFrame && x >= width - cornerLength) || (rightFrame && y < cornerLength)) return HtTopRight;
        if ((bottomFrame && x < cornerLength) || (leftFrame && y >= height - cornerLength)) return HtBottomLeft;
        if ((bottomFrame && x >= width - cornerLength) || (rightFrame && y >= height - cornerLength)) return HtBottomRight;

        // Only the outer half of the visible border resizes. The inner half remains
        // a clearly visible caption strip for moving the ruler.
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

        int frame = Math.Min(DipToPixels(FrameThicknessDip), Math.Min(width, height) / 2);
        nint outerRegion = CreateRectRgn(0, 0, width, height);
        if (outerRegion == nint.Zero)
        {
            return;
        }

        nint innerRegion = CreateRectRgn(frame, frame, width - frame, height - frame);
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

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(nint destination, nint source1, nint source2, int combineMode);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint value);
}
