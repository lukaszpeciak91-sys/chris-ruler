using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const int RgnDiff = 4;
    private const double InteractiveFrameThicknessDip = 8;
    private const double ResizeBandThicknessDip = 5;

    private readonly Window window;
    private HwndSource? source;
    private nint hwnd;

    public NativeOverlayWindowBehavior(Window window)
    {
        this.window = window;
        window.SourceInitialized += OnSourceInitialized;
        window.SizeChanged += OnSizeChanged;
    }

    public void Dispose()
    {
        window.SourceInitialized -= OnSourceInitialized;
        window.SizeChanged -= OnSizeChanged;

        if (source is not null)
        {
            source.RemoveHook(WindowProc);
            source = null;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        hwnd = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(hwnd);
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
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyFrameRegion));
        }

        return nint.Zero;
    }

    private nint HitTest(nint lParam)
    {
        if (!GetWindowRect(hwnd, out Rect windowRect))
        {
            return HtCaption;
        }

        // WM_NCHITTEST packs signed virtual-screen coordinates into LPARAM.
        // Sign extension is required for monitors left of or above the primary display.
        int screenX = unchecked((short)((long)lParam & 0xFFFF));
        int screenY = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
        int x = screenX - windowRect.Left;
        int y = screenY - windowRect.Top;
        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top;
        int resizeBand = DipToPixels(ResizeBandThicknessDip);

        bool left = x < resizeBand;
        bool right = x >= width - resizeBand;
        bool top = y < resizeBand;
        bool bottom = y >= height - resizeBand;

        // Corners take precedence so diagonal resizing remains available.
        if (top && left) return HtTopLeft;
        if (top && right) return HtTopRight;
        if (bottom && left) return HtBottomLeft;
        if (bottom && right) return HtBottomRight;
        if (left) return HtLeft;
        if (right) return HtRight;
        if (top) return HtTop;
        if (bottom) return HtBottom;

        // The inner part of the frame acts like a title bar, without showing one.
        return HtCaption;
    }

    private void ApplyFrameRegion()
    {
        if (hwnd == nint.Zero || !GetClientRect(hwnd, out Rect clientRect))
        {
            return;
        }

        int width = clientRect.Right - clientRect.Left;
        int height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        int frame = Math.Min(DipToPixels(InteractiveFrameThicknessDip), Math.Min(width, height) / 2);
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

        CombineRgn(outerRegion, outerRegion, innerRegion, RgnDiff);
        DeleteObject(innerRegion);

        // Unlike HTTRANSPARENT, a native window-region hole is absent from desktop
        // hit testing entirely, so clicks reach windows owned by other processes too.
        if (SetWindowRgn(hwnd, outerRegion, true) == 0)
        {
            DeleteObject(outerRegion);
        }
        // On success Windows owns outerRegion; it must not be deleted here.
    }

    private int DipToPixels(double dip)
    {
        uint dpi = GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            dpi = 96;
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
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

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
