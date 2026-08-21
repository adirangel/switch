using System.Diagnostics;
using System.Text;
using ScreenSwitch.Core;
using ScreenSwitch.Monitors;

namespace ScreenSwitch;

/// <summary>
/// Answers "is the user in the middle of a game right now?" for <see cref="SwitchGuard"/>.
///
/// Two signals, because one is not enough. <c>SHQueryUserNotificationState</c> catches exclusive
/// full-screen Direct3D, but most games — League of Legends included — are usually played
/// borderless windowed, which that API happily reports as a normal desktop. So the foreground
/// window is also measured against its monitor.
/// </summary>
internal static class ForegroundProbe
{
    /// <summary>
    /// Shell windows that legitimately cover the whole screen. Without this the desktop itself
    /// reads as full-screen and the guard would block every press made from the desktop.
    /// </summary>
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",       // the desktop
        "WorkerW",       // the desktop, again, when a wallpaper slideshow is running
        "Shell_TrayWnd", // taskbar
        "WindowsForms10.Window.8.app.0.141b42a_r9_ad1", // classic shell dialogs
    };

    /// <summary>
    /// Captures the current foreground state. Never throws: if anything here fails, the caller
    /// gets <see cref="ForegroundState.Unknown"/> and the switch goes through, because a broken
    /// probe must not be able to lock the user out of their own hotkey.
    /// </summary>
    public static ForegroundState Capture()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return ForegroundState.Unknown;
            }

            var processName = TryGetProcessName(hwnd);
            var fullscreen = IsD3DFullscreen() || CoversItsMonitor(hwnd);
            return new ForegroundState(fullscreen, processName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or PlatformNotSupportedException)
        {
            return ForegroundState.Unknown;
        }
    }

    /// <summary>True for exclusive full-screen Direct3D, and for presentation mode.</summary>
    private static bool IsD3DFullscreen()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }

        return state is NativeMethods.UserNotificationState.RunningD3dFullScreen
            or NativeMethods.UserNotificationState.PresentationMode;
    }

    /// <summary>
    /// True when <paramref name="hwnd"/> spans the entire monitor it sits on — the borderless
    /// full-screen case. Uses >= / &lt;= rather than equality because a borderless window is
    /// sometimes a pixel larger than the monitor it covers.
    /// </summary>
    private static bool CoversItsMonitor(IntPtr hwnd)
    {
        if (IsShellWindow(hwnd) || !NativeMethods.GetWindowRect(hwnd, out var window))
        {
            return false;
        }

        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (hMonitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MonitorInfoEx { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            return false;
        }

        var screen = info.rcMonitor;
        return window.Left <= screen.Left
            && window.Top <= screen.Top
            && window.Right >= screen.Right
            && window.Bottom >= screen.Bottom;
    }

    private static bool IsShellWindow(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Capacity);
        return length > 0 && ShellWindowClasses.Contains(buffer.ToString(0, length));
    }

    private static string? TryGetProcessName(IntPtr hwnd)
    {
        if (NativeMethods.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The process ended between the two calls, or is protected. Not knowing its name only
            // costs us the blocklist check; the full-screen test still applies.
            return null;
        }
    }
}
