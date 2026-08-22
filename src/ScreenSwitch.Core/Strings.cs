using System.Globalization;
using System.Resources;

namespace ScreenSwitch.Core;

/// <summary>
/// Typed access to the translated interface strings.
///
/// Every string the user can see lives in <c>Resources/Strings.resx</c> (English, the neutral set)
/// with one <c>Strings.&lt;code&gt;.resx</c> per translation. Lookups follow
/// <see cref="CultureInfo.CurrentUICulture"/>, which <c>Program</c> sets once at startup from the
/// config and the operating system, so nothing below has to be told which language to use.
///
/// The properties are generated from the neutral file; a key present here but missing from a
/// translation falls back to English rather than failing, and the unit tests assert that no such
/// gap exists.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("ScreenSwitch.Core.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// The raw string for <paramref name="key"/>. Returns the key itself if it is missing
    /// entirely, which is visible in the UI but keeps the app running.
    /// </summary>
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Looks up <paramref name="key"/> and fills its <c>{0}</c> placeholders.</summary>
    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    /// <summary>Only that culture's own strings, without falling back to English. For tests.</summary>
    public static ResourceSet? SetFor(CultureInfo culture)
        => Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);


    /// <summary>ScreenSwitch is already running. Look for the icon in the notification area, next to the ...</summary>
    public static string App_AlreadyRunning => Get("App_AlreadyRunning");

    /// <summary>Could not open the registry key</summary>
    public static string Startup_CannotOpenRegistryKey => Get("Startup_CannotOpenRegistryKey");

    /// <summary>The monitor did not respond to the DDC/CI command (DDC/CI may be switched off in its menu)</summary>
    public static string Monitor_NoDdcResponse => Get("Monitor_NoDdcResponse");

    /// <summary>No target input configured</summary>
    public static string Monitor_NoTargetConfigured => Get("Monitor_NoTargetConfigured");

    /// <summary>Error: {0}</summary>
    public static string Cli_Error(object? arg0) => Format("Cli_Error", arg0);

    /// <summary>ScreenSwitch — switches monitor inputs between computers over DDC/CI.</summary>
    public static string Cli_HelpIntro => Get("Cli_HelpIntro");

    /// <summary>run as a system tray icon</summary>
    public static string Cli_HelpTrayDesc => Get("Cli_HelpTrayDesc");

    /// <summary>switch to the target from the config file</summary>
    public static string Cli_HelpSwitchDesc => Get("Cli_HelpSwitchDesc");

    /// <summary>switch to a specific input (DisplayPort1 / HDMI1 / HDMI2 / 0x11)</summary>
    public static string Cli_HelpToDesc => Get("Cli_HelpToDesc");

    /// <summary>show monitors, current input and supported inputs</summary>
    public static string Cli_HelpListDesc => Get("Cli_HelpListDesc");

    /// <summary>start automatically with Windows</summary>
    public static string Cli_HelpAutostartDesc => Get("Cli_HelpAutostartDesc");

    /// <summary>Config file: {0}</summary>
    public static string Cli_ConfigFile(object? arg0) => Format("Cli_ConfigFile", arg0);

    /// <summary>Auto-start: enabled</summary>
    public static string Cli_AutostartOn => Get("Cli_AutostartOn");

    /// <summary>Auto-start: disabled</summary>
    public static string Cli_AutostartOff => Get("Cli_AutostartOff");

    /// <summary>Unrecognised value: {0}. Use on or off.</summary>
    public static string Cli_AutostartUnknownValue(object? arg0) => Format("Cli_AutostartUnknownValue", arg0);

    /// <summary>Could not change auto-start: {0}</summary>
    public static string Cli_AutostartFailed(object? arg0) => Format("Cli_AutostartFailed", arg0);

    /// <summary>Auto-start enabled. ScreenSwitch will start on its own after a reboot.</summary>
    public static string Cli_AutostartEnabled => Get("Cli_AutostartEnabled");

    /// <summary>Auto-start disabled.</summary>
    public static string Cli_AutostartDisabled => Get("Cli_AutostartDisabled");

    /// <summary>No DDC/CI capable monitors found.</summary>
    public static string Cli_NoMonitors => Get("Cli_NoMonitors");

    /// <summary>Unknown input: {0}</summary>
    public static string Cli_UnknownInput(object? arg0) => Format("Cli_UnknownInput", arg0);

    /// <summary>(missing)</summary>
    public static string Cli_MissingValue => Get("Cli_MissingValue");

    /// <summary>Possible values: {0}</summary>
    public static string Cli_PossibleValues(object? arg0) => Format("Cli_PossibleValues", arg0);

    /// <summary>No target input configured. Run --to &lt;input&gt; or set targetInput in the config file.</summary>
    public static string Cli_NoTargetConfigured => Get("Cli_NoTargetConfigured");

    /// <summary>Could not read the config file</summary>
    public static string Tray_ConfigReadErrorTitle => Get("Tray_ConfigReadErrorTitle");

    /// <summary>{0} Default settings were loaded.</summary>
    public static string Tray_ConfigReadErrorBody(object? arg0) => Format("Tray_ConfigReadErrorBody", arg0);

    /// <summary>A full-screen application</summary>
    public static string Tray_BlockedFullscreenApp => Get("Tray_BlockedFullscreenApp");

    /// <summary>Switch blocked</summary>
    public static string Tray_BlockedTitle => Get("Tray_BlockedTitle");

    /// <summary>{0} is running. Press the shortcut again to switch anyway.</summary>
    public static string Tray_BlockedBody(object? arg0) => Format("Tray_BlockedBody", arg0);

    /// <summary>A target must be set</summary>
    public static string Tray_NoTargetTitle => Get("Tray_NoTargetTitle");

    /// <summary>Open the ScreenSwitch menu and pick "Switch to" to choose which input to switch to.</summary>
    public static string Tray_NoTargetBody => Get("Tray_NoTargetBody");

    /// <summary>ScreenSwitch — switching monitors…</summary>
    public static string Tray_Switching => Get("Tray_Switching");

    /// <summary>Switch failed</summary>
    public static string Tray_SwitchFailedTitle => Get("Tray_SwitchFailedTitle");

    /// <summary>Switch partly succeeded</summary>
    public static string Tray_SwitchPartialTitle => Get("Tray_SwitchPartialTitle");

    /// <summary>No monitors found</summary>
    public static string Tray_NoMonitorsTitle => Get("Tray_NoMonitorsTitle");

    /// <summary>No DDC/CI capable monitor was detected. Check that DDC/CI is enabled in the monitor's menu.</summary>
    public static string Tray_NoMonitorsBody => Get("Tray_NoMonitorsBody");

    /// <summary>the target</summary>
    public static string Tray_TheTarget => Get("Tray_TheTarget");

    /// <summary>Monitors switched</summary>
    public static string Tray_SwitchedTitle => Get("Tray_SwitchedTitle");

    /// <summary>All monitors switched to {0}.</summary>
    public static string Tray_SwitchedBody(object? arg0) => Format("Tray_SwitchedBody", arg0);

    /// <summary>Monitor detection failed</summary>
    public static string Tray_DetectFailedTitle => Get("Tray_DetectFailedTitle");

    /// <summary>ScreenSwitch is running</summary>
    public static string Tray_FirstRunTitle => Get("Tray_FirstRunTitle");

    /// <summary>Right-click the icon and pick "Switch to" to choose which input the monitors should move ...</summary>
    public static string Tray_FirstRunBody => Get("Tray_FirstRunBody");

    /// <summary>Start ScreenSwitch automatically with Windows?  The monitors will then be ready to switch...</summary>
    public static string Tray_AutostartPromptBody => Get("Tray_AutostartPromptBody");

    /// <summary>Could not change auto-start</summary>
    public static string Tray_AutostartFailedTitle => Get("Tray_AutostartFailedTitle");

    /// <summary>Unknown error</summary>
    public static string Tray_UnknownError => Get("Tray_UnknownError");

    /// <summary>Could not save settings</summary>
    public static string Tray_SaveConfigFailedTitle => Get("Tray_SaveConfigFailedTitle");

    /// <summary>Keyboard shortcut not registered</summary>
    public static string Tray_HotkeyFailedTitle => Get("Tray_HotkeyFailedTitle");

    /// <summary>{0} is probably taken by another application. You can change it in the config file. The t...</summary>
    public static string Tray_HotkeyFailedBody(object? arg0) => Format("Tray_HotkeyFailedBody", arg0);

    /// <summary>ScreenSwitch — no target set</summary>
    public static string Tray_HeaderNoTarget => Get("Tray_HeaderNoTarget");

    /// <summary>ScreenSwitch — target: {0}</summary>
    public static string Tray_HeaderTarget(object? arg0) => Format("Tray_HeaderTarget", arg0);

    /// <summary>Switch to the other computer   ({0})</summary>
    public static string Tray_SwitchNow(object? arg0) => Format("Tray_SwitchNow", arg0);

    /// <summary>Switch to</summary>
    public static string Tray_SwitchTo => Get("Tray_SwitchTo");

    /// <summary>Set standing target</summary>
    public static string Tray_SetDefault => Get("Tray_SetDefault");

    /// <summary>Monitor details…</summary>
    public static string Tray_MonitorDetails => Get("Tray_MonitorDetails");

    /// <summary>Detect monitors again</summary>
    public static string Tray_RedetectMonitors => Get("Tray_RedetectMonitors");

    /// <summary>Open config file</summary>
    public static string Tray_OpenConfigFile => Get("Tray_OpenConfigFile");

    /// <summary>Start with Windows</summary>
    public static string Tray_StartWithWindows => Get("Tray_StartWithWindows");

    /// <summary>Language</summary>
    public static string Tray_Language => Get("Tray_Language");

    /// <summary>Automatic (follow Windows)</summary>
    public static string Tray_LanguageAutomatic => Get("Tray_LanguageAutomatic");

    /// <summary>Exit</summary>
    public static string Tray_Exit => Get("Tray_Exit");

    /// <summary>(active now)</summary>
    public static string Tray_ActiveMarker => Get("Tray_ActiveMarker");

    /// <summary>— now: {0}</summary>
    public static string Tooltip_Now(object? arg0) => Format("Tooltip_Now", arg0);

    /// <summary>Could not open the config file</summary>
    public static string Tray_CannotOpenConfigTitle => Get("Tray_CannotOpenConfigTitle");

    /// <summary>ScreenSwitch — monitor details</summary>
    public static string Details_Title => Get("Details_Title");

    /// <summary>Check the monitor's OSD: System Setup -&gt; DDC/CI -&gt; On</summary>
    public static string Details_CheckOsd => Get("Details_CheckOsd");

    /// <summary>Device: {0}</summary>
    public static string Details_Device(object? arg0) => Format("Details_Device", arg0);

    /// <summary>Id: {0}</summary>
    public static string Details_Id(object? arg0) => Format("Details_Id", arg0);

    /// <summary>Current input: {0}</summary>
    public static string Details_CurrentInput(object? arg0) => Format("Details_CurrentInput", arg0);

    /// <summary>unknown</summary>
    public static string Details_Unknown => Get("Details_Unknown");

    /// <summary>Supported inputs: {0}</summary>
    public static string Details_SupportedInputs(object? arg0) => Format("Details_SupportedInputs", arg0);

    /// <summary>Error: {0}</summary>
    public static string Details_Error(object? arg0) => Format("Details_Error", arg0);

    /// <summary>Config file: {0}</summary>
    public static string Details_ConfigFile(object? arg0) => Format("Details_ConfigFile", arg0);
}
