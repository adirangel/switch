namespace ScreenSwitch.Core;

/// <summary>
/// One selectable video input, as defined by MCCS VCP feature 0x60 ("Input Select").
/// </summary>
/// <param name="Code">The raw VCP value written to feature 0x60.</param>
/// <param name="Name">Canonical, config-file friendly name (e.g. <c>HDMI1</c>).</param>
/// <param name="DisplayName">Human readable name shown in the tray menu.</param>
public readonly record struct InputSource(byte Code, string Name, string DisplayName);

/// <summary>
/// Catalog of the standard MCCS input-select values plus parsing helpers, so the config file can
/// say <c>"HDMI1"</c>, <c>"hdmi 1"</c>, <c>"0x11"</c> or <c>17</c> and all mean the same thing.
/// </summary>
public static class InputSources
{
    private static readonly InputSource[] Catalog =
    [
        new(0x01, "VGA1", "VGA 1"),
        new(0x02, "VGA2", "VGA 2"),
        new(0x03, "DVI1", "DVI 1"),
        new(0x04, "DVI2", "DVI 2"),
        new(0x05, "Composite1", "Composite 1"),
        new(0x06, "Composite2", "Composite 2"),
        new(0x07, "SVideo1", "S-Video 1"),
        new(0x08, "SVideo2", "S-Video 2"),
        new(0x09, "Tuner1", "Tuner 1"),
        new(0x0A, "Tuner2", "Tuner 2"),
        new(0x0B, "Tuner3", "Tuner 3"),
        new(0x0C, "Component1", "Component 1"),
        new(0x0D, "Component2", "Component 2"),
        new(0x0E, "Component3", "Component 3"),
        new(0x0F, "DisplayPort1", "DisplayPort 1"),
        new(0x10, "DisplayPort2", "DisplayPort 2"),
        new(0x11, "HDMI1", "HDMI 1"),
        new(0x12, "HDMI2", "HDMI 2"),
    ];

    /// <summary>Extra spellings accepted in the config file, mapped to their canonical VCP code.</summary>
    private static readonly Dictionary<string, byte> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DP"] = 0x0F,
        ["DP1"] = 0x0F,
        ["DisplayPort"] = 0x0F,
        ["DP2"] = 0x10,
        ["HDMI"] = 0x11,
        ["USBC"] = 0x0F,
        ["USB-C"] = 0x0F,
        ["TypeC"] = 0x0F,
        ["DVI"] = 0x03,
        ["VGA"] = 0x01,
    };

    public static IReadOnlyList<InputSource> All => Catalog;

    /// <summary>Human readable label for a VCP value, falling back to hex for vendor specific codes.</summary>
    public static string DisplayName(byte code)
    {
        foreach (var source in Catalog)
        {
            if (source.Code == code)
            {
                return source.DisplayName;
            }
        }

        return $"Input 0x{code:X2}";
    }

    /// <summary>Canonical config-file spelling for a VCP value, e.g. <c>HDMI1</c> or <c>0x1F</c>.</summary>
    public static string CanonicalName(byte code)
    {
        foreach (var source in Catalog)
        {
            if (source.Code == code)
            {
                return source.Name;
            }
        }

        return $"0x{code:X2}";
    }

    /// <summary>
    /// Parses an input written by a human: a canonical name, an alias, <c>0x11</c>, or a plain
    /// decimal value. Returns false for anything that is not a valid VCP 0x60 value.
    /// </summary>
    public static bool TryParse(string? text, out byte code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        foreach (var source in Catalog)
        {
            if (string.Equals(source.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                code = source.Code;
                return true;
            }
        }

        // "HDMI 1" and "hdmi-1" should resolve like "HDMI1".
        var compact = trimmed.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        foreach (var source in Catalog)
        {
            if (string.Equals(source.Name, compact, StringComparison.OrdinalIgnoreCase))
            {
                code = source.Code;
                return true;
            }
        }

        if (Aliases.TryGetValue(compact, out var aliased))
        {
            code = aliased;
            return true;
        }

        if (compact.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return byte.TryParse(compact.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out code);
        }

        return byte.TryParse(compact, out code);
    }
}
