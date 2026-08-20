using ScreenSwitch.Core;

namespace ScreenSwitch.Monitors;

/// <summary>Snapshot of one monitor, taken once so the tray menu does not have to talk DDC on every open.</summary>
internal sealed record MonitorSnapshot(
    string Key,
    string Description,
    string DeviceName,
    string? Model,
    byte? CurrentInput,
    IReadOnlyList<byte> SupportedInputs,
    string? Error);

/// <summary>Outcome of switching a single monitor.</summary>
internal sealed record SwitchOutcome(string MonitorName, byte Target, bool Success, string? Error);

/// <summary>All the outcomes of one switch action.</summary>
internal sealed record SwitchReport(IReadOnlyList<SwitchOutcome> Outcomes)
{
    public bool AnySucceeded => Outcomes.Any(o => o.Success);

    public bool AllSucceeded => Outcomes.Count > 0 && Outcomes.All(o => o.Success);

    public bool NoMonitors => Outcomes.Count == 0;
}

/// <summary>
/// Finds the DDC/CI-capable monitors attached to this machine and drives their input selection.
/// Everything here blocks on hardware round trips; call it from a background thread.
/// </summary>
internal static class MonitorService
{
    /// <summary>
    /// Offered when a monitor refuses to report its capabilities, so the menu still lists the
    /// inputs a modern display almost certainly has rather than nothing at all.
    /// </summary>
    private static readonly byte[] FallbackInputs = [0x0F, 0x10, 0x11, 0x12];

    /// <summary>
    /// Enumerates every physical monitor. The caller owns the returned instances and must dispose
    /// them — each holds a handle from <c>GetPhysicalMonitorsFromHMONITOR</c>.
    /// </summary>
    public static List<DdcMonitor> Enumerate()
    {
        var monitors = new List<DdcMonitor>();

        // The callback is kept in a local so the GC cannot collect it mid-enumeration.
        NativeMethods.MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            try
            {
                monitors.AddRange(FromHandle(hMonitor));
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                // No dxva2 (extremely old or stripped Windows): nothing to add, keep enumerating.
            }

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        return monitors;
    }

    private static IEnumerable<DdcMonitor> FromHandle(IntPtr hMonitor)
    {
        var info = new NativeMethods.MonitorInfoEx { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
        var deviceName = NativeMethods.GetMonitorInfo(hMonitor, ref info) ? info.szDevice : string.Empty;

        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
        {
            yield break;
        }

        var physical = new NativeMethods.PhysicalMonitor[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
        {
            yield break;
        }

        for (var i = 0; i < physical.Length; i++)
        {
            yield return new DdcMonitor(physical[i], deviceName, BuildKey(deviceName, i));
        }
    }

    /// <summary>
    /// Builds the identifier used for per-monitor overrides. The hardware device id is preferred
    /// because it survives the display indexes being reshuffled; the GDI name is the fallback.
    /// </summary>
    private static string BuildKey(string deviceName, int index)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            var device = new NativeMethods.DisplayDevice { cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayDevice>() };
            if (NativeMethods.EnumDisplayDevices(deviceName, (uint)index, ref device, NativeMethods.EddGetDeviceInterfaceName) &&
                !string.IsNullOrWhiteSpace(device.DeviceID))
            {
                return device.DeviceID;
            }
        }

        return string.IsNullOrEmpty(deviceName) ? $"MONITOR#{index}" : $"{deviceName}#{index}";
    }

    /// <summary>Reads the state of every monitor: current input, advertised inputs and model.</summary>
    public static List<MonitorSnapshot> Describe()
    {
        var snapshots = new List<MonitorSnapshot>();
        var monitors = Enumerate();

        try
        {
            foreach (var monitor in monitors)
            {
                var capabilities = monitor.TryGetCapabilities();
                var supported = CapabilitiesParser.ParseSupportedInputs(capabilities);
                var hasCurrent = monitor.TryGetCurrentInput(out var current, out var error);

                snapshots.Add(new MonitorSnapshot(
                    monitor.Key,
                    monitor.Description,
                    monitor.DeviceName,
                    CapabilitiesParser.ParseModel(capabilities),
                    hasCurrent ? current : null,
                    supported.Count > 0 ? supported : FallbackInputs,
                    hasCurrent ? null : error));
            }
        }
        finally
        {
            foreach (var monitor in monitors)
            {
                monitor.Dispose();
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Switches every monitor to its configured target. Monitors are handled independently: one
    /// that refuses the command does not stop the others, and each failure is reported back.
    /// </summary>
    /// <param name="config">Supplies the target input, retry count and inter-monitor delay.</param>
    /// <param name="targetOverride">When set, used for every monitor instead of the config.</param>
    public static SwitchReport SwitchAll(AppConfig config, byte? targetOverride = null)
    {
        var outcomes = new List<SwitchOutcome>();
        var monitors = Enumerate();

        try
        {
            for (var i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                var target = targetOverride ?? config.ResolveTargetFor(monitor.Key);

                if (target is null)
                {
                    outcomes.Add(new SwitchOutcome(monitor.Description, 0, false, "לא הוגדר קלט יעד"));
                    continue;
                }

                outcomes.Add(Switch(monitor, target.Value, Math.Max(0, config.RetryCount)));

                if (i < monitors.Count - 1 && config.DelayBetweenMonitorsMs > 0)
                {
                    Thread.Sleep(Math.Min(config.DelayBetweenMonitorsMs, 5000));
                }
            }
        }
        finally
        {
            foreach (var monitor in monitors)
            {
                monitor.Dispose();
            }
        }

        return new SwitchReport(outcomes);
    }

    private static SwitchOutcome Switch(DdcMonitor monitor, byte target, int retryCount)
    {
        string? lastError = null;

        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(250);
            }

            if (monitor.TrySetInput(target, out var error))
            {
                return new SwitchOutcome(monitor.Description, target, true, null);
            }

            lastError = error;
        }

        return new SwitchOutcome(monitor.Description, target, false, lastError);
    }
}
