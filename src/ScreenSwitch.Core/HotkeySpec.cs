namespace ScreenSwitch.Core;

/// <summary>Modifier flags, matching the values <c>RegisterHotKey</c> expects.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// A global hotkey as written in the config file (e.g. <c>"Ctrl+Alt+S"</c>), resolved to the
/// modifier flags and virtual-key code <c>RegisterHotKey</c> needs.
/// </summary>
public readonly record struct HotkeySpec(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static readonly HotkeySpec Default = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'S');

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Backspace"] = 0x08,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Pause"] = 0x13,
        ["Esc"] = 0x1B,
        ["Escape"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["PrintScreen"] = 0x2C,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["NumLock"] = 0x90,
        ["ScrollLock"] = 0x91,
    };

    /// <summary>
    /// Parses a "Ctrl+Alt+S" style string. Accepts <c>Ctrl</c>/<c>Control</c>, <c>Alt</c>,
    /// <c>Shift</c>, <c>Win</c>/<c>Windows</c> as modifiers, and a letter, digit, F-key or one of
    /// the named keys above. Returns false when there is no modifier or no usable key.
    /// </summary>
    public static bool TryParse(string? text, out HotkeySpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        uint? key = null;

        foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    continue;
                case "win":
                case "windows":
                case "super":
                case "meta":
                    modifiers |= HotkeyModifiers.Win;
                    continue;
            }

            if (key is not null)
            {
                return false; // Two non-modifier keys: not a hotkey we can register.
            }

            if (!TryParseKey(part, out var parsedKey))
            {
                return false;
            }

            key = parsedKey;
        }

        // Windows refuses to register an unmodified key, and stealing a bare key from every other
        // app would be hostile anyway.
        if (key is null || modifiers == HotkeyModifiers.None)
        {
            return false;
        }

        spec = new HotkeySpec(modifiers, key.Value);
        return true;
    }

    private static bool TryParseKey(string part, out uint virtualKey)
    {
        virtualKey = 0;

        if (part.Length == 1)
        {
            var c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }

            return false;
        }

        if ((part[0] is 'F' or 'f') && int.TryParse(part.AsSpan(1), out var fKey) && fKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + fKey - 1);
            return true;
        }

        return NamedKeys.TryGetValue(part, out virtualKey);
    }

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static string KeyName(uint virtualKey)
    {
        if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x70 + 1}";
        }

        foreach (var pair in NamedKeys)
        {
            if (pair.Value == virtualKey)
            {
                return pair.Key;
            }
        }

        return $"0x{virtualKey:X2}";
    }
}
