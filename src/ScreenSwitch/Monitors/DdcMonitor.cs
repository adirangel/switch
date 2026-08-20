using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenSwitch.Monitors;

/// <summary>
/// One physical monitor reachable over DDC/CI. Wraps the handle returned by
/// <c>GetPhysicalMonitorsFromHMONITOR</c> and must be disposed to release it.
/// </summary>
/// <remarks>
/// Every call here is a round trip over the video cable and can take tens to hundreds of
/// milliseconds — reading the capabilities string is the slow one, often over a second. Never call
/// these on the UI thread.
/// </remarks>
internal sealed class DdcMonitor : IDisposable
{
    private readonly NativeMethods.PhysicalMonitor[] _handleArray;
    private bool _disposed;

    internal DdcMonitor(NativeMethods.PhysicalMonitor physicalMonitor, string deviceName, string key)
    {
        _handleArray = [physicalMonitor];
        Description = string.IsNullOrWhiteSpace(physicalMonitor.szPhysicalMonitorDescription)
            ? "Monitor"
            : physicalMonitor.szPhysicalMonitorDescription.Trim();
        DeviceName = deviceName;
        Key = key;
    }

    private IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handleArray[0].hPhysicalMonitor;
        }
    }

    /// <summary>Model description reported by the driver, e.g. "Generic PnP Monitor".</summary>
    public string Description { get; }

    /// <summary>GDI device name, e.g. <c>\\.\DISPLAY1</c>.</summary>
    public string DeviceName { get; }

    /// <summary>Stable-ish identifier used as the key for per-monitor overrides in the config.</summary>
    public string Key { get; }

    /// <summary>Reads the currently active input (VCP 0x60).</summary>
    public bool TryGetCurrentInput(out byte input, out string? error)
    {
        input = 0;
        error = null;

        if (!NativeMethods.GetVCPFeatureAndVCPFeatureReply(Handle, NativeMethods.VcpInputSelect, out _, out var current, out _))
        {
            error = DescribeLastError();
            return false;
        }

        // Some monitors answer with the input in the low byte and status bits above it.
        input = (byte)(current & 0xFF);
        return true;
    }

    /// <summary>Writes VCP 0x60, telling the monitor to display a different input.</summary>
    public bool TrySetInput(byte input, out string? error)
    {
        error = null;

        if (!NativeMethods.SetVCPFeature(Handle, NativeMethods.VcpInputSelect, input))
        {
            error = DescribeLastError();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the monitor's MCCS capabilities string. Returns null when the monitor does not
    /// support the request — several do not, even though input switching itself works.
    /// </summary>
    public string? TryGetCapabilities()
    {
        if (!NativeMethods.GetCapabilitiesStringLength(Handle, out var length) || length == 0 || length > 64 * 1024)
        {
            return null;
        }

        var buffer = new StringBuilder((int)length);
        if (!NativeMethods.CapabilitiesRequestAndCapabilitiesReply(Handle, buffer, length))
        {
            return null;
        }

        return buffer.ToString();
    }

    private static string DescribeLastError()
    {
        var code = Marshal.GetLastWin32Error();
        if (code == 0)
        {
            // dxva2 reports "no DDC/CI" by failing without setting an error code.
            return "המסך לא הגיב לפקודת DDC/CI (ייתכן ש-DDC/CI כבוי בתפריט המסך)";
        }

        return new Win32Exception(code).Message;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handleArray[0].hPhysicalMonitor != IntPtr.Zero)
        {
            NativeMethods.DestroyPhysicalMonitors(1, _handleArray);
            _handleArray[0].hPhysicalMonitor = IntPtr.Zero;
        }
    }
}
