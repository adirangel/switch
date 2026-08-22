using System.Reflection;
using System.Text;
using ScreenSwitch.Core;
using ScreenSwitch.Monitors;

namespace ScreenSwitch;

/// <summary>
/// The whole application: a tray icon, a context menu and a global hotkey, all wired to
/// <see cref="MonitorService.SwitchAll"/>. There is no main window — the app is only ever seen as
/// the icon in the notification area.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly string _configPath;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly HotkeyWindow _hotkeyWindow;

    private readonly AppConfig _config;
    private readonly Font _boldFont;

    /// <summary>Keeps a stray hotkey press mid-game from yanking the monitors away.</summary>
    private readonly SwitchGuard _guard = new();

    /// <summary>
    /// Balloons raised during construction, before the message loop exists to show them.
    /// Flushed once <see cref="OnStarted"/> runs.
    /// </summary>
    private readonly List<(string Title, string Message, ToolTipIcon Icon)> _pendingNotifications = [];

    private IReadOnlyList<MonitorSnapshot> _snapshots = [];
    private bool _switching;
    private bool _started;

    /// <summary>
    /// No target configured at launch, i.e. this is the first run on this machine. Used to offer
    /// auto-start once the user has finished setting up, rather than on a bare first launch.
    /// </summary>
    private bool _firstRun;
    private bool _startupOffered;

    public TrayAppContext(string? configPath = null)
    {
        _configPath = configPath ?? AppConfig.DefaultPath;
        _config = AppConfig.Load(_configPath, out var configError);

        _menu = new ContextMenuStrip { RightToLeft = UiCulture.RightToLeft, ShowImageMargin = false };
        _menu.Opening += (_, _) => RebuildMenu();
        _boldFont = new Font(_menu.Font, FontStyle.Bold);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = _menu,
            Text = "ScreenSwitch",
        };
        _notifyIcon.MouseClick += OnIconClicked;

        _firstRun = !HasAnyTarget;

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.Pressed += (_, _) => OnHotkeyPressed();
        RegisterHotkey();

        UpdateTooltip();

        if (configError is not null)
        {
            Notify(Strings.Tray_ConfigReadErrorTitle, Strings.Tray_ConfigReadErrorBody(configError), ToolTipIcon.Warning);
        }

        // Everything above ran before Application.Run, so there is no message loop yet and no
        // WinForms synchronization context for `await` to come back to. Defer the first monitor
        // scan to a one-shot timer, which fires once the loop is running and continuations are
        // guaranteed to resume on the UI thread.
        var startupTimer = new System.Windows.Forms.Timer { Interval = 1 };
        startupTimer.Tick += (sender, _) =>
        {
            ((System.Windows.Forms.Timer)sender!).Stop();
            ((System.Windows.Forms.Timer)sender).Dispose();
            OnStarted();
        };
        startupTimer.Start();
    }

    private void OnStarted()
    {
        _started = true;

        foreach (var (title, message, icon) in _pendingNotifications)
        {
            ShowBalloon(title, message, icon);
        }

        _pendingNotifications.Clear();

        _ = RefreshMonitorsAsync(announceFirstRun: true);
    }

    /// <summary>
    /// Whether a switch can be performed at all. Usually that means a global target, but a config
    /// that only sets per-monitor targets is valid too.
    /// </summary>
    private bool HasAnyTarget => _config.ResolveGlobalTarget() is not null || _config.MonitorTargets.Count > 0;

    // ---------------------------------------------------------------- actions

    /// <summary>Double-click on the tray icon is the fastest possible path to a switch.</summary>
    private void OnIconClicked(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Clicks == 2)
        {
            SwitchAsync(null);
        }
    }

    /// <summary>
    /// The hotkey path, which — unlike the menu and the tray icon — can fire while the user is in
    /// the middle of a game. <see cref="SwitchGuard"/> decides whether the press was meant.
    /// </summary>
    private void OnHotkeyPressed()
    {
        var verdict = _guard.Evaluate(_config, ForegroundProbe.Capture(), DateTimeOffset.UtcNow, out var blockedBy);

        if (verdict == GuardVerdict.Block)
        {
            var who = string.IsNullOrWhiteSpace(blockedBy) ? Strings.Tray_BlockedFullscreenApp : blockedBy;
            Notify(
                Strings.Tray_BlockedTitle,
                Strings.Tray_BlockedBody(who),
                ToolTipIcon.Info);
            return;
        }

        SwitchAsync(null);
    }

    /// <summary>
    /// Sends every monitor to the target input. Runs off the UI thread because each monitor costs
    /// a DDC round trip, and guards against re-entry so a double press cannot interleave writes.
    /// </summary>
    private async void SwitchAsync(byte? targetOverride)
    {
        if (_switching)
        {
            return;
        }

        // Nothing configured yet and no explicit choice: point the user at the menu instead of
        // failing silently.
        if (targetOverride is null && !HasAnyTarget)
        {
            Notify(Strings.Tray_NoTargetTitle, Strings.Tray_NoTargetBody, ToolTipIcon.Info);
            return;
        }

        _switching = true;
        _notifyIcon.Text = Truncate(Strings.Tray_Switching);

        try
        {
            var config = _config;
            var report = await Task.Run(() => MonitorService.SwitchAll(config, targetOverride));
            ReportSwitch(report, targetOverride ?? config.ResolveGlobalTarget());
        }
        catch (Exception ex)
        {
            Notify(Strings.Tray_SwitchFailedTitle, ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _switching = false;
            UpdateTooltip();
        }
    }

    private void ReportSwitch(SwitchReport report, byte? target)
    {
        if (report.NoMonitors)
        {
            Notify(Strings.Tray_NoMonitorsTitle, Strings.Tray_NoMonitorsBody, ToolTipIcon.Warning);
            return;
        }

        if (report.AllSucceeded)
        {
            var name = target is null ? Strings.Tray_TheTarget : InputSources.DisplayName(target.Value);
            Notify(Strings.Tray_SwitchedTitle, Strings.Tray_SwitchedBody(name), ToolTipIcon.Info);
            return;
        }

        var details = new StringBuilder();
        foreach (var outcome in report.Outcomes)
        {
            details.AppendLine(outcome.Success
                ? $"✔ {outcome.MonitorName}"
                : $"✘ {outcome.MonitorName}: {outcome.Error}");
        }

        Notify(
            report.AnySucceeded ? Strings.Tray_SwitchPartialTitle : Strings.Tray_SwitchFailedTitle,
            details.ToString().TrimEnd(),
            report.AnySucceeded ? ToolTipIcon.Warning : ToolTipIcon.Error);
    }

    /// <summary>
    /// Re-reads what each monitor reports, so the menu can offer only inputs that actually exist.
    /// Slow (the capabilities string alone can take a second per monitor), hence off-thread.
    /// </summary>
    private async Task RefreshMonitorsAsync(bool announceFirstRun = false)
    {
        try
        {
            _snapshots = await Task.Run(MonitorService.Describe);
        }
        catch (Exception ex)
        {
            _snapshots = [];
            Notify(Strings.Tray_DetectFailedTitle, ex.Message, ToolTipIcon.Error);
            return;
        }

        UpdateTooltip();

        if (announceFirstRun && !HasAnyTarget)
        {
            Notify(
                Strings.Tray_FirstRunTitle,
                Strings.Tray_FirstRunBody,
                ToolTipIcon.Info);
        }
    }

    private void SetDefaultTarget(byte target)
    {
        _config.TargetInput = InputSources.CanonicalName(target);
        SaveConfig();
        UpdateTooltip();
        OfferStartupOnce();
    }

    /// <summary>
    /// Asks once, at the end of first-run setup, whether to start with Windows. Without this the
    /// option sits unnoticed in the context menu and every reboot costs a trip to the .exe.
    /// </summary>
    private void OfferStartupOnce()
    {
        if (!_firstRun || _startupOffered || StartupManager.IsEnabled())
        {
            return;
        }

        _startupOffered = true;

        var answer = MessageBox.Show(
            Strings.Tray_AutostartPromptBody,
            "ScreenSwitch",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1,
            UiCulture.MessageBoxOptions);

        if (answer == DialogResult.Yes && !StartupManager.TrySet(true, out var error))
        {
            Notify(Strings.Tray_AutostartFailedTitle, error ?? Strings.Tray_UnknownError, ToolTipIcon.Error);
        }
    }

    private void SaveConfig()
    {
        if (!_config.TrySave(_configPath, out var error))
        {
            Notify(Strings.Tray_SaveConfigFailedTitle, error ?? Strings.Tray_UnknownError, ToolTipIcon.Error);
        }
    }

    private void RegisterHotkey()
    {
        var spec = _config.ResolveHotkey();
        if (!_hotkeyWindow.TryRegister(spec))
        {
            Notify(
                Strings.Tray_HotkeyFailedTitle,
                Strings.Tray_HotkeyFailedBody(spec),
                ToolTipIcon.Warning);
        }
    }

    // ---------------------------------------------------------------- menu

    private void RebuildMenu()
    {
        var previous = _menu.Items.Cast<ToolStripItem>().ToArray();
        _menu.Items.Clear();
        foreach (var item in previous)
        {
            item.Dispose();
        }

        var target = _config.ResolveGlobalTarget();
        var hotkey = _config.ResolveHotkey();

        var header = new ToolStripMenuItem(target is null
            ? Strings.Tray_HeaderNoTarget
            : Strings.Tray_HeaderTarget(InputSources.DisplayName(target.Value))) { Enabled = false };
        _menu.Items.Add(header);

        var switchNow = new ToolStripMenuItem(Strings.Tray_SwitchNow(hotkey), null, (_, _) => SwitchAsync(null))
        {
            Font = _boldFont,
            Enabled = HasAnyTarget && !_switching,
        };
        _menu.Items.Add(switchNow);

        _menu.Items.Add(new ToolStripSeparator());

        var switchTo = new ToolStripMenuItem(Strings.Tray_SwitchTo);
        var setDefault = new ToolStripMenuItem(Strings.Tray_SetDefault);
        foreach (var code in AvailableInputs())
        {
            var value = code;
            var label = InputSources.DisplayName(value) + CurrentInputMarker(value);

            switchTo.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) =>
            {
                // First run: the first input the user picks also becomes the default, so the
                // hotkey works from then on without a second trip through the menu.
                if (_config.ResolveGlobalTarget() is null)
                {
                    SetDefaultTarget(value);
                }

                SwitchAsync(value);
            }));

            setDefault.DropDownItems.Add(new ToolStripMenuItem(InputSources.DisplayName(value), null, (_, _) => SetDefaultTarget(value))
            {
                Checked = target == value,
                CheckOnClick = false,
            });
        }

        _menu.Items.Add(switchTo);
        _menu.Items.Add(setDefault);

        _menu.Items.Add(new ToolStripSeparator());

        _menu.Items.Add(new ToolStripMenuItem(Strings.Tray_MonitorDetails, null, (_, _) => ShowMonitorDetails()));
        _menu.Items.Add(new ToolStripMenuItem(Strings.Tray_RedetectMonitors, null, async (_, _) => await RefreshMonitorsAsync()));
        _menu.Items.Add(new ToolStripMenuItem(Strings.Tray_OpenConfigFile, null, (_, _) => OpenConfigFile()));

        _menu.Items.Add(BuildLanguageMenu());

        _menu.Items.Add(new ToolStripMenuItem(Strings.Tray_StartWithWindows, null, (_, _) => ToggleStartup())
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = false,
        });

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem(Strings.Tray_Exit, null, (_, _) => ExitApp()));
    }

    /// <summary>
    /// Language picker, listing each language under its own name. "Automatic" clears the setting
    /// and goes back to following Windows, which is the default and what most people want.
    /// </summary>
    private ToolStripMenuItem BuildLanguageMenu()
    {
        var menu = new ToolStripMenuItem(Strings.Tray_Language);

        menu.DropDownItems.Add(new ToolStripMenuItem(Strings.Tray_LanguageAutomatic, null, (_, _) => SetLanguage(null))
        {
            Checked = _config.Language is null,
            CheckOnClick = false,
        });

        menu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var language in Localization.Supported)
        {
            var code = language.Code;
            menu.DropDownItems.Add(new ToolStripMenuItem(language.NativeName, null, (_, _) => SetLanguage(code))
            {
                Checked = string.Equals(_config.Language, code, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false,
            });
        }

        return menu;
    }

    /// <summary>
    /// Switches language in place. The menu is rebuilt on every open and the tooltip is refreshed
    /// here, so the change is visible immediately; balloons already on screen keep the old text,
    /// which is not worth chasing.
    /// </summary>
    private void SetLanguage(string? code)
    {
        _config.Language = code;
        SaveConfig();

        UiCulture.Apply(code);
        _menu.RightToLeft = UiCulture.RightToLeft;
        UpdateTooltip();
    }

    /// <summary>
    /// The union of inputs the attached monitors advertise, so the menu shows HDMI 2 only when a
    /// monitor actually has it. Falls back to the usual suspects before the first scan finishes.
    /// </summary>
    private IReadOnlyList<byte> AvailableInputs()
    {
        var codes = new List<byte>();
        var seen = new HashSet<byte>();

        foreach (var snapshot in _snapshots)
        {
            foreach (var code in snapshot.SupportedInputs)
            {
                if (seen.Add(code))
                {
                    codes.Add(code);
                }
            }
        }

        if (codes.Count == 0)
        {
            return [0x0F, 0x10, 0x11, 0x12];
        }

        codes.Sort();
        return codes;
    }

    /// <summary>Marks the input a monitor is currently showing, which is always the one you are looking at.</summary>
    private string CurrentInputMarker(byte code)
        => _snapshots.Any(s => s.CurrentInput == code) ? Strings.Tray_ActiveMarker : string.Empty;

    private void ShowMonitorDetails()
    {
        var text = new StringBuilder();

        if (_snapshots.Count == 0)
        {
            text.AppendLine(Strings.Cli_NoMonitors);
            text.AppendLine();
            text.AppendLine(Strings.Details_CheckOsd);
        }

        foreach (var snapshot in _snapshots)
        {
            text.AppendLine(snapshot.Model is null ? snapshot.Description : $"{snapshot.Description} ({snapshot.Model})");
            text.AppendLine(Strings.Details_Device(snapshot.DeviceName));
            text.AppendLine(Strings.Details_Id(snapshot.Key));
            text.AppendLine(Strings.Details_CurrentInput(snapshot.CurrentInput is null ? Strings.Details_Unknown : InputSources.DisplayName(snapshot.CurrentInput.Value)));
            text.AppendLine(Strings.Details_SupportedInputs(string.Join(", ", snapshot.SupportedInputs.Select(InputSources.DisplayName))));

            if (snapshot.Error is not null)
            {
                text.AppendLine(Strings.Details_Error(snapshot.Error));
            }

            text.AppendLine();
        }

        text.AppendLine(Strings.Details_ConfigFile(_configPath));

        MessageBox.Show(
            text.ToString(),
            Strings.Details_Title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            UiCulture.MessageBoxOptions);
    }

    private void OpenConfigFile()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                SaveConfig();
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Notify(Strings.Tray_CannotOpenConfigTitle, ex.Message, ToolTipIcon.Error);
        }
    }

    private void ToggleStartup()
    {
        var enable = !StartupManager.IsEnabled();
        if (!StartupManager.TrySet(enable, out var error))
        {
            Notify(Strings.Tray_AutostartFailedTitle, error ?? Strings.Tray_UnknownError, ToolTipIcon.Error);
        }
    }

    // ---------------------------------------------------------------- plumbing

    private void UpdateTooltip()
    {
        var target = _config.ResolveGlobalTarget();
        var current = _snapshots.FirstOrDefault()?.CurrentInput;

        var text = new StringBuilder("ScreenSwitch");
        if (current is not null)
        {
            text.Append(Strings.Tooltip_Now(InputSources.DisplayName(current.Value)));
        }

        if (target is not null)
        {
            text.Append($" ← {InputSources.DisplayName(target.Value)}");
        }

        _notifyIcon.Text = Truncate(text.ToString());
    }

    /// <summary>NotifyIcon.Text is capped at 63 characters; anything longer throws.</summary>
    private static string Truncate(string text)
        => text.Length <= 63 ? text : text[..60] + "…";

    private void Notify(string title, string message, ToolTipIcon icon)
    {
        if (!_config.ShowNotifications)
        {
            return;
        }

        if (!_started)
        {
            _pendingNotifications.Add((title, message, icon));
            return;
        }

        ShowBalloon(title, message, icon);
    }

    private void ShowBalloon(string title, string message, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private static Icon LoadTrayIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ScreenSwitch.app.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyWindow.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
