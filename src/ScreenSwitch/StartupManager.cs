using Microsoft.Win32;

namespace ScreenSwitch;

/// <summary>
/// Toggles "start with Windows" through the per-user Run key. HKCU needs no elevation, which
/// matters on a locked-down work machine.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenSwitch";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Enables or disables auto-start. Returns false (with a reason) if the registry write fails.</summary>
    public static bool TrySet(bool enabled, out string? error)
    {
        error = null;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "לא ניתן לפתוח את מפתח הרג'יסטרי";
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return false;
        }
    }
}
