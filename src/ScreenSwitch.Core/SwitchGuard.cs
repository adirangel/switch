namespace ScreenSwitch.Core;

/// <summary>
/// What is in front of the user at the moment the hotkey arrives. Captured by the Windows layer
/// and handed to <see cref="SwitchGuard"/>, which is what keeps the decision testable off Windows.
/// </summary>
/// <param name="IsFullscreen">
/// The foreground window covers a whole monitor, or Windows reports an exclusive full-screen
/// Direct3D app. Both shapes of "a game is running" land here.
/// </param>
/// <param name="CapturesCursor">
/// Something is confining the mouse to a region smaller than the desktop. Games that run in a
/// window generally lock the cursor to keep mouse-look working, so this catches them without
/// anyone having to name them.
/// </param>
/// <param name="ProcessName">Foreground process name, without the <c>.exe</c>; null when unknown.</param>
public readonly record struct ForegroundState(bool IsFullscreen, bool CapturesCursor, string? ProcessName)
{
    /// <summary>
    /// Nothing could be determined. Deliberately treated as "not a game": a probe that fails must
    /// never leave the user unable to switch.
    /// </summary>
    public static readonly ForegroundState Unknown = new(false, false, null);
}

/// <summary>The outcome of <see cref="SwitchGuard.Evaluate"/>.</summary>
public enum GuardVerdict
{
    /// <summary>Nothing in the way — switch.</summary>
    Allow,

    /// <summary>A game is in front. Swallow this press and tell the user how to insist.</summary>
    Block,

    /// <summary>A game is in front, but this is the second press in a row — the user means it.</summary>
    Override,
}

/// <summary>
/// Decides whether a hotkey press should really switch the monitors.
///
/// The problem it solves: the switch hotkey is a system-wide combination, and mid-game a stray
/// press would yank both monitors to the other computer at the worst possible moment. So while a
/// game is in the foreground the first press is swallowed, and only a second press inside
/// <see cref="AppConfig.OverrideWindowMs"/> goes through. Alt-tab out, minimise, or use the tray
/// icon and the guard stops applying — which is the "unless I actually meant it" part.
///
/// Only the hotkey is guarded. The tray menu, a double-click on the icon and the command line all
/// switch unconditionally: reaching any of them already means the user left the game.
/// </summary>
public sealed class SwitchGuard
{
    /// <summary>Longest override window we will honour, however the config is edited.</summary>
    private const int MaxOverrideWindowMs = 10_000;

    /// <summary>When the last press was blocked; null once a press is allowed or overridden.</summary>
    private DateTimeOffset? _blockedAt;

    /// <summary>
    /// Verdict for a hotkey press happening at <paramref name="now"/>.
    /// </summary>
    /// <param name="blockedBy">
    /// On <see cref="GuardVerdict.Block"/>, what triggered it — a process name, or null when the
    /// application could not be named. Null for every other verdict.
    /// </param>
    public GuardVerdict Evaluate(AppConfig config, ForegroundState state, DateTimeOffset now, out string? blockedBy)
    {
        blockedBy = null;

        if (!config.BlockWhileGaming)
        {
            _blockedAt = null;
            return GuardVerdict.Allow;
        }

        if (!ShouldBlock(config, state, out var reason))
        {
            _blockedAt = null;
            return GuardVerdict.Allow;
        }

        // A second press while the first is still "warm" is the user insisting.
        var window = TimeSpan.FromMilliseconds(Math.Clamp(config.OverrideWindowMs, 0, MaxOverrideWindowMs));
        if (window > TimeSpan.Zero && _blockedAt is { } previous && now - previous <= window && now >= previous)
        {
            _blockedAt = null;
            return GuardVerdict.Override;
        }

        _blockedAt = now;
        blockedBy = reason;
        return GuardVerdict.Block;
    }

    /// <summary>Forgets any pending override, so the next press starts from a clean slate.</summary>
    public void Reset() => _blockedAt = null;

    private static bool ShouldBlock(AppConfig config, ForegroundState state, out string? reason)
    {
        reason = null;

        // An explicitly listed process blocks whatever size its window is: a game played in a
        // window is still a game.
        var foreground = Normalise(state.ProcessName);
        if (foreground is not null)
        {
            foreach (var entry in config.BlockedProcesses)
            {
                if (string.Equals(Normalise(entry), foreground, StringComparison.OrdinalIgnoreCase))
                {
                    reason = state.ProcessName;
                    return true;
                }
            }
        }

        // Neither of these looks at *which* application it is: any full-screen app, and anything
        // holding the cursor captive, counts. That is what makes the guard work for games nobody
        // has listed anywhere.
        if (state.IsFullscreen || state.CapturesCursor)
        {
            reason = state.ProcessName;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Trims a process name to its comparable form. The config may reasonably say either
    /// "SomeGame" or "SomeGame.exe", so both are accepted.
    /// </summary>
    private static string? Normalise(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
