using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class LocalizationTests
{
    /// <summary>Placeholders like {0}, which a translation silently dropping would throw over.</summary>
    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>The translations, i.e. every shipped language except the neutral English set.</summary>
    public static TheoryData<string> TranslatedLanguages()
    {
        var data = new TheoryData<string>();
        foreach (var language in Localization.Supported)
        {
            if (language.Code != Localization.FallbackCode)
            {
                data.Add(language.Code);
            }
        }

        return data;
    }

    /// <summary>
    /// One language's own strings, with no fallback. English lives in the neutral resource set
    /// rather than a Strings.en.resx, so it is read through the invariant culture.
    /// </summary>
    private static Dictionary<string, string> Read(string code)
    {
        var culture = code == Localization.FallbackCode
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(code);

        var set = Strings.SetFor(culture);
        Assert.NotNull(set);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in set!)
        {
            result[(string)entry.Key] = (string)(entry.Value ?? string.Empty);
        }

        return result;
    }

    // ------------------------------------------------------------- the shipped set

    [Fact]
    public void ShipsExactlyTheLanguagesWeClaim()
    {
        var codes = Localization.Supported.Select(l => l.Code).ToArray();

        Assert.Equal(["en", "es", "fr", "pt", "he", "ar", "ja", "zh-Hans"], codes);
    }

    [Fact]
    public void EveryLanguageHasANativeName()
    {
        // The menu lists native names so a speaker can find their language without reading English.
        foreach (var language in Localization.Supported)
        {
            Assert.False(string.IsNullOrWhiteSpace(language.NativeName), language.Code);
            Assert.False(string.IsNullOrWhiteSpace(language.EnglishName), language.Code);
        }
    }

    [Fact]
    public void TheNeutralSetIsNotEmpty()
    {
        Assert.NotEmpty(Read(Localization.FallbackCode));
    }

    // ------------------------------------------------------------- translation completeness

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void TranslationHasEveryKeyAndNoExtras(string code)
    {
        var english = Read(Localization.FallbackCode);
        var translated = Read(code);

        var missing = english.Keys.Except(translated.Keys).OrderBy(k => k).ToArray();
        var extra = translated.Keys.Except(english.Keys).OrderBy(k => k).ToArray();

        Assert.True(missing.Length == 0, $"{code} is missing: {string.Join(", ", missing)}");
        Assert.True(extra.Length == 0, $"{code} has keys English does not: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void TranslationHasNoEmptyValues(string code)
    {
        var blank = Read(code).Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToArray();

        Assert.True(blank.Length == 0, $"{code} has empty values: {string.Join(", ", blank)}");
    }

    /// <summary>
    /// The one that matters most: a translation that loses a {0} throws at runtime, in a language
    /// the author probably cannot read, on a code path they may never hit themselves.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void TranslationKeepsEveryPlaceholder(string code)
    {
        var english = Read(Localization.FallbackCode);
        var translated = Read(code);
        var problems = new List<string>();

        foreach (var (key, value) in english)
        {
            if (!translated.TryGetValue(key, out var other))
            {
                continue; // Reported by the completeness test; not this test's business.
            }

            var expected = Placeholders(value);
            var actual = Placeholders(other);

            if (!expected.SetEquals(actual))
            {
                problems.Add($"{key} (expected {Show(expected)}, got {Show(actual)})");
            }
        }

        Assert.True(problems.Count == 0, $"{code}: {string.Join("; ", problems)}");

        static HashSet<string> Placeholders(string text)
            => Placeholder.Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

        static string Show(HashSet<string> set)
            => set.Count == 0 ? "none" : string.Join(",", set.OrderBy(s => s));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void TranslationIsNotJustTheEnglishCopiedOver(string code)
    {
        var english = Read(Localization.FallbackCode);
        var translated = Read(code);

        // Some values legitimately match (product names, "Id: {0}"), but a whole language matching
        // means the file was generated and never actually translated.
        var identical = english.Count(p => translated.TryGetValue(p.Key, out var v) && v == p.Value);

        Assert.True(identical < english.Count / 2, $"{code} looks untranslated: {identical}/{english.Count} identical to English");
    }

    // ------------------------------------------------------------- choosing a language

    [Fact]
    public void ConfiguredLanguageWins()
    {
        var resolved = Localization.Resolve("ja", CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal("ja", resolved.Code);
    }

    [Fact]
    public void FallsBackToTheOperatingSystemWhenNothingIsConfigured()
    {
        Assert.Equal("fr", Localization.Resolve(null, CultureInfo.GetCultureInfo("fr-FR")).Code);
        Assert.Equal("es", Localization.Resolve("", CultureInfo.GetCultureInfo("es-MX")).Code);
    }

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("es-419", "es")]
    [InlineData("ar-EG", "ar")]
    [InlineData("zh-Hans-CN", "zh-Hans")]
    public void RegionalVariantsLandOnTheirLanguage(string system, string expected)
    {
        Assert.Equal(expected, Localization.Resolve(null, CultureInfo.GetCultureInfo(system)).Code);
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("ko-KR")]
    [InlineData("")]
    public void AnUntranslatedSystemLanguageGetsEnglish(string system)
    {
        Assert.Equal("en", Localization.Resolve(null, CultureInfo.GetCultureInfo(system)).Code);
    }

    [Fact]
    public void AnUnknownConfiguredLanguageDefersToTheSystemRatherThanFailing()
    {
        // A hand-edited config saying "klingon" should not be fatal, and should not override a
        // perfectly good system language either.
        Assert.Equal("ja", Localization.Resolve("klingon", CultureInfo.GetCultureInfo("ja-JP")).Code);
    }

    [Fact]
    public void ConfiguredLanguageIsForgivingAboutCaseAndSpacing()
    {
        Assert.Equal("zh-Hans", Localization.Resolve("  ZH-hans ", CultureInfo.InvariantCulture).Code);
    }

    // ------------------------------------------------------------- writing direction

    [Theory]
    [InlineData("he")]
    [InlineData("ar")]
    public void RightToLeftLanguagesAreMarkedAsSuch(string code)
    {
        Assert.True(Localization.IsRightToLeft(code));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("pt")]
    [InlineData("ja")]
    [InlineData("zh-Hans")]
    public void LeftToRightLanguagesAreNot(string code)
    {
        // This is the bug that shipped: the tray menu was pinned right-to-left for everyone.
        Assert.False(Localization.IsRightToLeft(code));
    }

    [Fact]
    public void AnUnknownCodeIsNotRightToLeft()
    {
        Assert.False(Localization.IsRightToLeft("klingon"));
        Assert.False(Localization.IsRightToLeft(null));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void EveryLanguageResolvesToARealCulture(string code)
    {
        var language = Localization.Find(code);
        Assert.NotNull(language);

        var culture = Localization.ToCulture(language!.Value);
        Assert.NotNull(culture);
    }
}
