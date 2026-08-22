using System.Globalization;

namespace ScreenSwitch.Core;

/// <summary>A language the interface is translated into.</summary>
/// <param name="Code">The culture name used for the satellite assembly, e.g. <c>zh-Hans</c>.</param>
/// <param name="EnglishName">Name in English, for documentation and logs.</param>
/// <param name="NativeName">Name as its own speakers write it — what the language menu shows.</param>
public readonly record struct Language(string Code, string EnglishName, string NativeName);

/// <summary>
/// Picks the interface language and reports its writing direction.
///
/// Resolution order: the config's explicit choice, then the language Windows is running in, then
/// English. English is the neutral resource set, so it is what an unlisted language falls back to
/// rather than an error.
/// </summary>
public static class Localization
{
    /// <summary>The neutral resource set: the language every other one falls back to.</summary>
    public const string FallbackCode = "en";

    /// <summary>
    /// Languages with a translation, in the order the menu lists them. Native names are used so a
    /// speaker can find their own language without reading English first.
    /// </summary>
    public static readonly IReadOnlyList<Language> Supported =
    [
        new("en", "English", "English"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("pt", "Portuguese", "Português"),
        new("he", "Hebrew", "עברית"),
        new("ar", "Arabic", "العربية"),
        new("ja", "Japanese", "日本語"),
        new("zh-Hans", "Chinese (Simplified)", "简体中文"),
    ];

    /// <summary>Scripts that read right to left, which the tray menu has to mirror for.</summary>
    private static readonly HashSet<string> RightToLeftCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "he",
        "ar",
    };

    /// <summary>True when <paramref name="code"/> is one we ship a translation for.</summary>
    public static bool IsSupported(string? code) => Find(code) is not null;

    /// <summary>The supported language matching <paramref name="code"/>, or null.</summary>
    public static Language? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        foreach (var language in Supported)
        {
            if (string.Equals(language.Code, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        // "pt-BR" and "es-MX" should land on "pt" and "es" rather than falling through to English.
        var separator = trimmed.IndexOfAny(['-', '_']);
        if (separator > 0)
        {
            var parent = trimmed[..separator];
            foreach (var language in Supported)
            {
                if (string.Equals(language.Code, parent, StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The language to run in. <paramref name="configured"/> is the config's choice — null or
    /// unrecognised means "follow the operating system", and an operating system language we do
    /// not translate falls back to English.
    /// </summary>
    public static Language Resolve(string? configured, CultureInfo systemCulture)
    {
        if (Find(configured) is { } chosen)
        {
            return chosen;
        }

        // Walk up the culture chain: zh-Hans-CN -> zh-Hans -> zh.
        for (var culture = systemCulture; culture is not null && culture != CultureInfo.InvariantCulture; culture = culture.Parent)
        {
            if (Find(culture.Name) is { } fromSystem)
            {
                return fromSystem;
            }

            if (culture.Parent == culture)
            {
                break;
            }
        }

        return Supported[0];
    }

    /// <summary>Convenience overload resolving against the culture Windows is running in.</summary>
    public static Language Resolve(string? configured) => Resolve(configured, CultureInfo.CurrentUICulture);

    /// <summary>Whether <paramref name="code"/> is written right to left.</summary>
    public static bool IsRightToLeft(string? code)
    {
        var language = Find(code);
        return language is not null && RightToLeftCodes.Contains(language.Value.Code);
    }

    /// <summary>The <see cref="CultureInfo"/> for a language, falling back to invariant-safe English.</summary>
    public static CultureInfo ToCulture(Language language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language.Code);
        }
        catch (CultureNotFoundException)
        {
            // A trimmed or minimal ICU build might not know every name; English keeps the app usable.
            return CultureInfo.GetCultureInfo(FallbackCode);
        }
    }
}
