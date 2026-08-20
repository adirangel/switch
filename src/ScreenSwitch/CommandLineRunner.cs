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
                _ => Help(output),
            };
        }
        catch (Exception ex)
        {
            output.AppendLine($"שגיאה: {ex.Message}");
            return 1;
        }
        finally
        {
            Write(output.ToString());
        }
    }

    private static int Help(StringBuilder output)
    {
        output.AppendLine("ScreenSwitch — מעביר את קלט המסכים בין מחשבים דרך DDC/CI.");
        output.AppendLine();
        output.AppendLine("  ScreenSwitch.exe               הפעלה כאייקון במגש המערכת");
        output.AppendLine("  ScreenSwitch.exe --switch      מעבר ליעד שמוגדר בקובץ ההגדרות");
        output.AppendLine("  ScreenSwitch.exe --to HDMI1    מעבר לקלט מסוים (DisplayPort1 / HDMI1 / HDMI2 / 0x11)");
        output.AppendLine("  ScreenSwitch.exe --list        הצגת המסכים, הקלט הנוכחי והקלטים הנתמכים");
        output.AppendLine();
        output.AppendLine($"קובץ הגדרות: {AppConfig.DefaultPath}");
        return 0;
    }

    private static int List(StringBuilder output)
    {
        var snapshots = MonitorService.Describe();

        if (snapshots.Count == 0)
        {
            output.AppendLine("לא נמצאו מסכים תומכי DDC/CI.");
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
            output.AppendLine($"קלט לא מוכר: {value ?? "(חסר)"}");
            output.AppendLine($"ערכים אפשריים: {string.Join(", ", InputSources.All.Select(i => i.Name))}");
            return 2;
        }

        return Switch(output, code);
    }

    private static int Switch(StringBuilder output, byte? target)
    {
        var config = AppConfig.Load(AppConfig.DefaultPath, out _);

        if (target is null && config.ResolveGlobalTarget() is null && config.MonitorTargets.Count == 0)
        {
            output.AppendLine("לא הוגדר קלט יעד. הרץ --to <input> או הגדר targetInput בקובץ ההגדרות.");
            return 2;
        }

        var report = MonitorService.SwitchAll(config, target);

        if (report.NoMonitors)
        {
            output.AppendLine("לא נמצאו מסכים תומכי DDC/CI.");
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
