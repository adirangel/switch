using System.Runtime.InteropServices;
using System.Text;
using ScreenSwitch.Core;
using ScreenSwitch.Monitors;

namespace ScreenSwitch;

/// <summary>
/// Headless entry points: <c>--switch</c>, <c>--to &lt;input&gt;</c> and <c>--list</c>.
/// Useful for binding the switch to an external launcher, and for diagnosing a monitor that will
/// not cooperate without having to interpret balloon tips.
/// </summary>
internal static class CommandLineRunner
{
    private const int AttachParentProcess = -1;

    public static int Run(string[] args)
    {
        var output = new StringBuilder();

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "--list" or "-l" => List(output),
                "--switch" or "-s" => Switch(output, null),
                "--to" or "-t" => SwitchTo(output, args.Length > 1 ? args[1] : null),
                "--autostart" => Autostart(output, args.Length > 1 ? args[1] : null),
                _ => Help(output),
            };
        }
        catch (Exception ex)
        {
            output.AppendLine(Strings.Cli_Error(ex.Message));
            return 1;
        }
        finally
        {
            Write(output.ToString());
        }
    }

    /// <summary>One aligned usage line: the command padded to a fixed column, then its description.</summary>
    private static void Usage(StringBuilder output, string command, string description)
        => output.AppendLine($"  {command,-36}{description}");

    private static int Help(StringBuilder output)
    {
        output.AppendLine(Strings.Cli_HelpIntro);
        output.AppendLine();

        // The command column is padded in code rather than baked into each translated line, so the
        // descriptions stay aligned whatever length they turn out to be in a given language.
        Usage(output, "ScreenSwitch.exe", Strings.Cli_HelpTrayDesc);
        Usage(output, "ScreenSwitch.exe --switch", Strings.Cli_HelpSwitchDesc);
        Usage(output, "ScreenSwitch.exe --to HDMI1", Strings.Cli_HelpToDesc);
        Usage(output, "ScreenSwitch.exe --list", Strings.Cli_HelpListDesc);
        Usage(output, "ScreenSwitch.exe --autostart on|off", Strings.Cli_HelpAutostartDesc);

        output.AppendLine();
        output.AppendLine(Strings.Cli_ConfigFile(AppConfig.DefaultPath));
        return 0;
    }

    /// <summary>
    /// Turns "start with Windows" on or off without opening the tray menu, so it can be set up
    /// from a script. With no argument it just reports the current state.
    /// </summary>
    private static int Autostart(StringBuilder output, string? mode)
    {
        if (mode is null)
        {
            output.AppendLine(StartupManager.IsEnabled()
                ? Strings.Cli_AutostartOn
                : Strings.Cli_AutostartOff);
            return 0;
        }

        bool enable;
        switch (mode.Trim().ToLowerInvariant())
        {
            case "on" or "enable" or "true" or "1":
                enable = true;
                break;
            case "off" or "disable" or "false" or "0":
                enable = false;
                break;
            default:
                output.AppendLine(Strings.Cli_AutostartUnknownValue(mode));
                return 1;
        }

        if (!StartupManager.TrySet(enable, out var error))
        {
            output.AppendLine(Strings.Cli_AutostartFailed(error));
            return 1;
        }

        output.AppendLine(enable
            ? Strings.Cli_AutostartEnabled
            : Strings.Cli_AutostartDisabled);
        return 0;
    }

    private static int List(StringBuilder output)
    {
        var snapshots = MonitorService.Describe();

        if (snapshots.Count == 0)
        {
            output.AppendLine(Strings.Cli_NoMonitors);
            return 1;
        }

        foreach (var snapshot in snapshots)
        {
            output.AppendLine(snapshot.Model is null ? snapshot.Description : $"{snapshot.Description} ({snapshot.Model})");
            output.AppendLine($"  device : {snapshot.DeviceName}");
            output.AppendLine($"  id     : {snapshot.Key}");
            output.AppendLine($"  current: {(snapshot.CurrentInput is null ? "?" : InputSources.CanonicalName(snapshot.CurrentInput.Value))}");
            output.AppendLine($"  inputs : {string.Join(", ", snapshot.SupportedInputs.Select(InputSources.CanonicalName))}");

            if (snapshot.Error is not null)
            {
                output.AppendLine($"  error  : {snapshot.Error}");
            }

            output.AppendLine();
        }

        return 0;
    }

    private static int SwitchTo(StringBuilder output, string? value)
    {
        if (!InputSources.TryParse(value, out var code))
        {
            output.AppendLine(Strings.Cli_UnknownInput(value ?? Strings.Cli_MissingValue));
            output.AppendLine(Strings.Cli_PossibleValues(string.Join(", ", InputSources.All.Select(i => i.Name))));
            return 2;
        }

        return Switch(output, code);
    }

    private static int Switch(StringBuilder output, byte? target)
    {
        var config = AppConfig.Load(AppConfig.DefaultPath, out _);

        if (target is null && config.ResolveGlobalTarget() is null && config.MonitorTargets.Count == 0)
        {
            output.AppendLine(Strings.Cli_NoTargetConfigured);
            return 2;
        }

        var report = MonitorService.SwitchAll(config, target);

        if (report.NoMonitors)
        {
            output.AppendLine(Strings.Cli_NoMonitors);
            return 1;
        }

        foreach (var outcome in report.Outcomes)
        {
            output.AppendLine(outcome.Success
                ? $"OK   {outcome.MonitorName} → {InputSources.CanonicalName(outcome.Target)}"
                : $"FAIL {outcome.MonitorName}: {outcome.Error}");
        }

        return report.AllSucceeded ? 0 : 1;
    }

    /// <summary>
    /// Writes to the console that launched us. This is a WinExe so it has no console of its own;
    /// when there is no parent console either (double-clicked from Explorer) fall back to a dialog
    /// rather than dropping the output on the floor.
    /// </summary>
    private static void Write(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (AttachConsole(AttachParentProcess))
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine();
            Console.Write(text);
            FreeConsole();
            return;
        }

        MessageBox.Show(
            text,
            "ScreenSwitch",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();
}
