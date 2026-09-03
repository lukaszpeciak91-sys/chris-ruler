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
    private const uint VkUp = 0x26;
    private const uint VkDown = 0x28;
    private const int MoveUpHotkeyId = 1;
    private const int MoveDownHotkeyId = 2;

    private readonly List<MainWindow> rulers = [];
    private readonly HwndSource hotkeySource;
    private MainWindow? lastActiveRuler;
    private bool moveUpHotkeyRegistered;
    private bool moveDownHotkeyRegistered;
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

        // This hidden process-local HWND is the sole owner of both global hotkeys.
        moveUpHotkeyRegistered = RegisterHotKey(hotkeySource.Handle, MoveUpHotkeyId, ModAlt, VkUp);
        moveDownHotkeyRegistered = RegisterHotKey(hotkeySource.Handle, MoveDownHotkeyId, ModAlt, VkDown);
    }

    public void Register(MainWindow ruler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!rulers.Contains(ruler))
        {
            rulers.Add(ruler);
            lastActiveRuler = ruler;
        }
    }

    public void MarkActive(MainWindow ruler)
    {
        if (!disposed && rulers.Contains(ruler))
        {
            lastActiveRuler = ruler;
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
            lastActiveRuler = rulers.Count > 0 ? rulers[^1] : null;
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

        return nint.Zero;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
