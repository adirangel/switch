using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenSwitch.Core;

/// <summary>
/// User settings, stored as JSON at <see cref="DefaultPath"/> so it can be hand-edited.
/// The file is deliberately small: on each machine the only thing that really matters is
/// <see cref="TargetInput"/> — "which input should the monitors jump to when I press the hotkey
/// here", i.e. the input belonging to the *other* computer.
/// </summary>
public sealed class AppConfig
{
    /// <summary>
    /// Input the monitors are switched to. Null until the user picks one on first run.
    /// Accepts names ("HDMI1", "DisplayPort 1"), aliases ("DP") or hex ("0x11").
    /// </summary>
    public string? TargetInput { get; set; }

    /// <summary>
    /// Per-monitor overrides keyed by the monitor id shown in "Detect monitors". Only needed when
    /// the two monitors are wired to different ports (e.g. one HDMI 1, the other HDMI 2).
    /// </summary>
    public Dictionary<string, string> MonitorTargets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Global hotkey, parsed by <see cref="HotkeySpec.TryParse"/>.</summary>
    public string Hotkey { get; set; } = HotkeySpec.Default.ToString();

    /// <summary>
    /// Pause between monitors. Some monitors ignore a DDC write that lands while they are still
    /// processing the previous one, so the second monitor gets a small head start.
    /// </summary>
    public int DelayBetweenMonitorsMs { get; set; } = 150;

    /// <summary>Show a tray balloon with the result of each switch.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Retry a monitor that did not accept the input change. Costs a second or two in the failure
    /// case and nothing when the first write works.
    /// </summary>
    public int RetryCount { get; set; } = 1;

    [JsonIgnore]
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenSwitch",
        "config.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The configured global target, or null when it is unset or unparseable.</summary>
    public byte? ResolveGlobalTarget()
        => InputSources.TryParse(TargetInput, out var code) ? code : null;

    /// <summary>
    /// The input a specific monitor should switch to: its override when one is configured,
    /// otherwise the global target.
    /// </summary>
    public byte? ResolveTargetFor(string? monitorKey)
    {
        if (monitorKey is not null &&
            MonitorTargets.TryGetValue(monitorKey, out var perMonitor) &&
            InputSources.TryParse(perMonitor, out var code))
        {
            return code;
        }

        return ResolveGlobalTarget();
    }

    public HotkeySpec ResolveHotkey()
        => HotkeySpec.TryParse(Hotkey, out var spec) ? spec : HotkeySpec.Default;

    /// <summary>
    /// Loads the config, returning defaults when the file is missing. A corrupt file is reported
    /// through <paramref name="error"/> rather than thrown, so the app still starts and the user
    /// can fix the file from the tray menu.
    /// </summary>
    public static AppConfig Load(string path, out string? error)
    {
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppConfig();
            }

            return JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = ex.Message;
            return new AppConfig();
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    /// <summary>Saves without throwing; a failed write must never take the tray app down.</summary>
    public bool TrySave(string path, out string? error)
    {
        error = null;
        try
        {
            Save(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = ex.Message;
            return false;
        }
    }
}
