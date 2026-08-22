using System.Globalization;
using ScreenSwitch.Core;

namespace ScreenSwitch;

/// <summary>
/// Applies the interface language once, at startup, and answers the direction questions WinForms
/// needs afterwards.
///
/// Only <see cref="CultureInfo.CurrentUICulture"/> is set. <see cref="CultureInfo.CurrentCulture"/>
/// is deliberately left on whatever Windows is using, because it governs how numbers and dates
/// parse — choosing Japanese for the menus should not change how a hex input code is read.
/// </summary>
internal static class UiCulture
{
    /// <summary>The language actually in use, once <see cref="Apply"/> has run.</summary>
    public static Language Current { get; private set; } = Localization.Supported[0];

    /// <summary>Whether the interface should be laid out right to left.</summary>
    public static bool IsRightToLeft => Localization.IsRightToLeft(Current.Code);

    /// <summary>Right-to-left flags for <see cref="MessageBox"/>, empty for left-to-right languages.</summary>
    public static MessageBoxOptions MessageBoxOptions => IsRightToLeft
        ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
        : default;

    /// <summary>Convenience for the WinForms property of the same shape.</summary>
    public static RightToLeft RightToLeft => IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;

    /// <summary>
    /// Resolves the language from <paramref name="configuredLanguage"/> and the operating system,
    /// then applies it to this thread and every thread started afterwards. Must run before any
    /// window or string lookup.
    /// </summary>
    public static void Apply(string? configuredLanguage)
    {
        Current = Localization.Resolve(configuredLanguage);

        var culture = Localization.ToCulture(Current);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>Reads the language out of the config file on disk and applies it.</summary>
    public static void ApplyFromConfig()
    {
        var config = AppConfig.Load(AppConfig.DefaultPath, out _);
        Apply(config.Language);
    }
}
