using ScreenSwitch.Core;
using ScreenSwitch.Monitors;

namespace ScreenSwitch;

/// <summary>
/// A message-only window that owns the global hotkey registration. Windows delivers
/// <c>WM_HOTKEY</c> to a window, so the tray app needs one even though it shows no UI.
/// </summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xA51C;
    private const int HwndMessage = -3;

    private bool _registered;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "ScreenSwitchHotkey",
            Parent = HwndMessage,
        });
    }

    /// <summary>Raised on the UI thread when the user presses the hotkey.</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Registers <paramref name="spec"/>, replacing any previous registration. Returns false when
    /// another application already owns the combination — the caller should tell the user and
    /// carry on, since the tray icon still works.
    /// </summary>
    public bool TryRegister(HotkeySpec spec)
    {
        Unregister();

        _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, (uint)spec.Modifiers, spec.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}
