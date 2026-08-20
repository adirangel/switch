using System.Text;

namespace ScreenSwitch.Core;

/// <summary>
/// Parses the MCCS capabilities string a monitor returns over DDC/CI. A typical reply looks like:
/// <code>
/// (prot(monitor)type(LCD)model(VG27AQ3A)cmds(01 02 03 07 0C E3 F3)vcp(02 04 14(01 05 08) 60(0F 11 12) DF)mccs_ver(2.2))
/// </code>
/// We only need two things out of it: the model name, and which values feature 0x60 accepts —
/// that list is exactly the set of inputs the monitor can be switched to.
/// </summary>
public static class CapabilitiesParser
{
    private const string InputSelectFeature = "60";

    /// <summary>
    /// Returns the input values listed under VCP feature 0x60, in the order the monitor reported
    /// them. Returns an empty list when the string is missing, malformed, or does not advertise 0x60.
    /// </summary>
    public static IReadOnlyList<byte> ParseSupportedInputs(string? capabilities)
    {
        var vcp = ExtractSection(capabilities, "vcp");
        if (vcp is null)
        {
            return [];
        }

        // Walk the vcp body tracking which feature code each parenthesised group belongs to, so a
        // "60" appearing *inside* another feature's value list (e.g. "14(01 60)") is not mistaken
        // for the input-select feature itself.
        string? lastToken = null;
        var token = new StringBuilder();

        for (var i = 0; i < vcp.Length; i++)
        {
            var c = vcp[i];

            if (Uri.IsHexDigit(c))
            {
                token.Append(c);
                continue;
            }

            if (token.Length > 0)
            {
                lastToken = token.ToString();
                token.Clear();
            }

            if (c != '(')
            {
                continue;
            }

            var close = FindMatchingParen(vcp, i);
            if (close < 0)
            {
                break;
            }

            if (string.Equals(lastToken, InputSelectFeature, StringComparison.OrdinalIgnoreCase))
            {
                return ParseHexList(vcp.Substring(i + 1, close - i - 1));
            }

            i = close;
            lastToken = null;
        }

        return [];
    }

    /// <summary>Returns the <c>model(...)</c> value, or null when the monitor did not report one.</summary>
    public static string? ParseModel(string? capabilities)
    {
        var model = ExtractSection(capabilities, "model")?.Trim();
        return string.IsNullOrEmpty(model) ? null : model;
    }

    /// <summary>
    /// Returns the body of <c>name(...)</c> — the text between its parentheses, with nested
    /// parentheses preserved — or null when the section is absent or unbalanced.
    /// </summary>
    private static string? ExtractSection(string? capabilities, string name)
    {
        if (string.IsNullOrWhiteSpace(capabilities))
        {
            return null;
        }

        var searchFrom = 0;
        while (true)
        {
            var start = capabilities.IndexOf(name, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            searchFrom = start + name.Length;

            // Reject a match that is only the tail of a longer identifier (e.g. "vcp" inside "xvcp").
            if (start > 0 && (char.IsLetterOrDigit(capabilities[start - 1]) || capabilities[start - 1] == '_'))
            {
                continue;
            }

            var open = searchFrom;
            while (open < capabilities.Length && char.IsWhiteSpace(capabilities[open]))
            {
                open++;
            }

            if (open >= capabilities.Length || capabilities[open] != '(')
            {
                continue;
            }

            var close = FindMatchingParen(capabilities, open);
            if (close < 0)
            {
                return null;
            }

            return capabilities.Substring(open + 1, close - open - 1);
        }
    }

    /// <summary>Index of the ')' matching the '(' at <paramref name="openIndex"/>, or -1 if unbalanced.</summary>
    private static int FindMatchingParen(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Parses "0F 11 12" into bytes, dropping duplicates and keeping the reported order.</summary>
    private static IReadOnlyList<byte> ParseHexList(string text)
    {
        var values = new List<byte>();
        var seen = new HashSet<byte>();

        foreach (var part in text.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out var value) && seen.Add(value))
            {
                values.Add(value);
            }
        }

        return values;
    }
}
