using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ChrisRuler;

/// <summary>
/// Owns process-wide ruler state and the single native target for global row hotkeys.
/// Individual windows retain all geometry and movement behavior.
/// </summary>
internal sealed class RulerCoordinator : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkUp = 0x26;
    private const uint VkDown = 0x28;
    private const uint VkR = 0x52;
    private const int MoveUpHotkeyId = 1;
    private const int MoveDownHotkeyId = 2;
    private const int NewRulerHotkeyId = 3;

    private readonly List<MainWindow> rulers = [];
    private readonly HwndSource hotkeySource;
    private MainWindow? lastActiveRuler;
    private bool moveUpHotkeyRegistered;
    private bool moveDownHotkeyRegistered;
    private bool newRulerHotkeyRegistered;
    private bool disposed;

    public RulerCoordinator()
    {
        var parameters = new HwndSourceParameters("ChrisRuler.Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        hotkeySource = new HwndSource(parameters);
        hotkeySource.AddHook(WindowProc);

        // This hidden process-local HWND is the sole owner of all global hotkeys.
        moveUpHotkeyRegistered = RegisterHotKey(hotkeySource.Handle, MoveUpHotkeyId, ModAlt, VkUp);
        moveDownHotkeyRegistered = RegisterHotKey(hotkeySource.Handle, MoveDownHotkeyId, ModAlt, VkDown);
        // Failure is non-fatal and independent of the existing movement shortcuts.
        newRulerHotkeyRegistered = RegisterHotKey(
            hotkeySource.Handle, NewRulerHotkeyId, ModControl | ModAlt, VkR);
    }

    public event EventHandler? ActiveRulerChanged;

    public void CreateInitialRuler() => CreateRuler(ownsGeometryPersistence: true, source: null);

    public void CreateNewRuler() => CreateRuler(ownsGeometryPersistence: false, lastActiveRuler);

    public bool IsActive(MainWindow ruler) => ReferenceEquals(lastActiveRuler, ruler);

    public void Register(MainWindow ruler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!rulers.Contains(ruler))
        {
            rulers.Add(ruler);
            SetActive(ruler);
        }
    }

    public void MarkActive(MainWindow ruler)
    {
        if (!disposed && rulers.Contains(ruler))
        {
            SetActive(ruler);
        }
    }

    public void Unregister(MainWindow ruler)
    {
        if (disposed || !rulers.Remove(ruler))
        {
            return;
        }

        if (ReferenceEquals(lastActiveRuler, ruler))
        {
            SetActive(rulers.Count > 0 ? rulers[^1] : null);
        }

        if (rulers.Count == 0)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        rulers.Clear();
        lastActiveRuler = null;

        if (moveUpHotkeyRegistered)
        {
            UnregisterHotKey(hotkeySource.Handle, MoveUpHotkeyId);
            moveUpHotkeyRegistered = false;
        }

        if (moveDownHotkeyRegistered)
        {
            UnregisterHotKey(hotkeySource.Handle, MoveDownHotkeyId);
            moveDownHotkeyRegistered = false;
        }

        if (newRulerHotkeyRegistered)
        {
            UnregisterHotKey(hotkeySource.Handle, NewRulerHotkeyId);
            newRulerHotkeyRegistered = false;
        }

        hotkeySource.RemoveHook(WindowProc);
        hotkeySource.Dispose();
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmHotkey || lastActiveRuler is null)
        {
            return nint.Zero;
        }

        int hotkeyId = unchecked((int)(long)wParam);
        if (hotkeyId == MoveUpHotkeyId)
        {
            lastActiveRuler.MoveUpOneRow();
            handled = true;
        }
        else if (hotkeyId == MoveDownHotkeyId)
        {
            lastActiveRuler.MoveDownOneRow();
            handled = true;
        }
        else if (hotkeyId == NewRulerHotkeyId)
        {
            CreateNewRuler();
            handled = true;
        }

        return nint.Zero;
    }

    private void CreateRuler(bool ownsGeometryPersistence, MainWindow? source)
    {
        ColorTheme theme = source?.SelectedTheme ?? ColorTheme.Available[0];
        WindowGeometry? geometry = source?.GetOffsetCloneGeometry();
        var ruler = new MainWindow(this, ownsGeometryPersistence, theme, geometry);
        ruler.Show();
    }

    private void SetActive(MainWindow? ruler)
    {
        if (ReferenceEquals(lastActiveRuler, ruler))
        {
            return;
        }

        lastActiveRuler = ruler;
        ActiveRulerChanged?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
