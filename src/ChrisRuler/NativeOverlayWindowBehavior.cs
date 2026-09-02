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
    private const int WmHotkey = 0x0312;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;

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
    private const uint MonitorDefaultToNull = 0;
    private const double ResizeBandThicknessDip = 3;
    private const double CornerLengthDip = 16;
    private const int IdcSizeAll = 32646;
    private const uint ModAlt = 0x0001;
    private const uint VkUp = 0x26;
    private const uint VkDown = 0x28;
    private const int MoveUpHotkeyId = 1;
    private const int MoveDownHotkeyId = 2;

    private readonly Window window;
    private readonly Action<bool> setResizeEmphasis;
    private readonly FrameworkElement[] controls;
    private readonly WindowGeometryStore geometryStore = new();
    private HwndSource? source;
    private DispatcherOperation? pendingRegionUpdate;
    private nint hwnd;
    private bool disposed;
    private bool moveUpHotkeyRegistered;
    private bool moveDownHotkeyRegistered;
    private nint sizeAllCursor;
    private bool resizeEmphasisActive;

    public bool IsLocked { get; set; }

    public NativeOverlayWindowBehavior(
        Window window,
        Action<bool> setResizeEmphasis,
        params FrameworkElement[] controls)
    {
        this.window = window;
        this.setResizeEmphasis = setResizeEmphasis;
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
        SetResizeEmphasis(false);

        UnregisterHotkeys();

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
        RestoreWindowGeometry();
        // RegisterHotKey provides the two deliberate application shortcuts without
        // installing a global keyboard hook or observing any unrelated input.
        moveUpHotkeyRegistered = RegisterHotKey(hwnd, MoveUpHotkeyId, ModAlt, VkUp);
        moveDownHotkeyRegistered = RegisterHotKey(hwnd, MoveDownHotkeyId, ModAlt, VkDown);
        ApplyFrameRegion();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ScheduleFrameRegionUpdate();

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyFrameRegion();

    public void MoveUpOneRow() => MoveOneRow(-1);

    public void MoveDownOneRow() => MoveOneRow(1);

    public void SaveWindowGeometry()
    {
        if (!disposed && hwnd != nint.Zero && GetWindowRect(hwnd, out Rect rect))
        {
            geometryStore.Save(new WindowGeometry(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top));
        }
    }

    private void RestoreWindowGeometry()
    {
        WindowGeometry? saved = geometryStore.Load();
        if (saved is null || saved.Width <= 0 || saved.Height <= 0)
        {
            return;
        }

        var bounds = new Rect
        {
            Left = saved.Left,
            Top = saved.Top,
            Right = SaturatingAdd(saved.Left, saved.Width),
            Bottom = SaturatingAdd(saved.Top, saved.Height)
        };

        int minimumWidth = DipToPixels(window.MinWidth);
        int minimumHeight = DipToPixels(window.MinHeight);
        if (saved.Width < minimumWidth || saved.Height < minimumHeight ||
            bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top ||
            MonitorFromRect(ref bounds, MonitorDefaultToNull) == nint.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, nint.Zero, saved.Left, saved.Top, saved.Width, saved.Height,
            SwpNoZOrder | SwpNoActivate);
    }

    private static int SaturatingAdd(int value, int increment)
    {
        long result = (long)value + increment;
        return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
    }

    private void MoveOneRow(int direction)
    {
        if (disposed || hwnd == nint.Zero || !GetWindowRect(hwnd, out Rect rect))
        {
            return;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        FrameGeometry geometry = GetFrameGeometry(width, height);
        int rowStep = Math.Max(0, height - geometry.TopBar - geometry.BottomBar);
        int virtualTop = GetSystemMetrics(SmYVirtualScreen);
        int virtualBottom = virtualTop + GetSystemMetrics(SmCyVirtualScreen);
        int maximumTop = Math.Max(virtualTop, virtualBottom - height);

        // Native pixel geometry is the source of truth for both the region hole and
        // navigation. Integer-pixel steps cannot accumulate fractional DIP rounding
        // error over repeated movements at non-100% display scaling.
        long requestedTop = (long)rect.Top + (long)direction * rowStep;
        int newTop = (int)Math.Clamp(requestedTop, virtualTop, maximumTop);
        SetWindowPos(hwnd, nint.Zero, rect.Left, newTop, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmSizing)
        {
            SetResizeEmphasis(true);
        }
        else if (message == WmExitSizeMove)
        {
            SetResizeEmphasis(false);
        }

        if (message == WmNcHitTest)
        {
            handled = true;
            return HitTest(lParam);
        }

        if (message == WmHotkey)
        {
            int hotkeyId = unchecked((int)(long)wParam);
            if (hotkeyId == MoveUpHotkeyId)
            {
                MoveUpOneRow();
                handled = true;
            }
            else if (hotkeyId == MoveDownHotkeyId)
            {
                MoveDownOneRow();
                handled = true;
            }

            return nint.Zero;
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

        // The center is absent from the native window region. While locked, the
        // remaining frame stays a plain client surface: no caption drag or resize
        // hit results are exposed, but the controls above remain usable.
        if (IsLocked)
        {
            return 1;
        }

        FrameGeometry geometry = GetFrameGeometry(width, height);
        int leftBar = geometry.SideBar;
        int rightBar = leftBar;
        int topBar = geometry.TopBar;
        int bottomBar = geometry.BottomBar;
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

        FrameGeometry geometry = GetFrameGeometry(width, height);
        int sideBar = geometry.SideBar;
        int topBar = geometry.TopBar;
        int bottomBar = geometry.BottomBar;
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

    private void SetResizeEmphasis(bool isActive)
    {
        if (resizeEmphasisActive == isActive)
        {
            return;
        }

        resizeEmphasisActive = isActive;
        setResizeEmphasis(isActive);
    }

    private void UnregisterHotkeys()
    {
        if (hwnd == nint.Zero)
        {
            return;
        }

        if (moveUpHotkeyRegistered)
        {
            UnregisterHotKey(hwnd, MoveUpHotkeyId);
            moveUpHotkeyRegistered = false;
        }

        if (moveDownHotkeyRegistered)
        {
            UnregisterHotKey(hwnd, MoveDownHotkeyId);
            moveDownHotkeyRegistered = false;
        }
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

    private FrameGeometry GetFrameGeometry(int width, int height)
    {
        int maximumInset = Math.Min(width, height) / 2;
        return new FrameGeometry(
            Math.Min(DipToPixels(OverlayGeometry.SideBarWidthDip), maximumInset),
            Math.Min(DipToPixels(OverlayGeometry.TopBarHeightDip), maximumInset),
            Math.Min(DipToPixels(OverlayGeometry.BottomBarHeightDip), maximumInset));
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

    private readonly record struct FrameGeometry(int SideBar, int TopBar, int BottomBar);

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref Rect rect, uint flags);

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
