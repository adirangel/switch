using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class SwitchGuardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 21, 0, 0, TimeSpan.Zero);

    /// <summary>A config with the guard on and nothing but the given processes blocked.</summary>
    private static AppConfig Config(params string[] blocked) => new()
    {
        BlockWhileGaming = true,
        BlockedProcesses = [.. blocked],
        OverrideWindowMs = 1500,
    };

    private static ForegroundState Desktop => new(IsFullscreen: false, ProcessName: "explorer");

    private static ForegroundState Game(string name = "League of Legends", bool fullscreen = true)
        => new(fullscreen, name);

    [Fact]
    public void AllowsWhenNothingIsInTheWay()
    {
        var guard = new SwitchGuard();

        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config(), Desktop, T0, out var blockedBy));
        Assert.Null(blockedBy);
    }

    [Fact]
    public void AllowsWhenTheProbeLearnedNothing()
    {
        var guard = new SwitchGuard();

        // A probe that fails must never be able to lock the user out of the hotkey.
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config(), ForegroundState.Unknown, T0, out _));
    }

    [Fact]
    public void BlocksAFullScreenApp()
    {
        var guard = new SwitchGuard();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config(), Game(), T0, out var blockedBy));
        Assert.Equal("League of Legends", blockedBy);
    }

    [Fact]
    public void BlocksAFullScreenAppItCannotName()
    {
        var guard = new SwitchGuard();

        var state = new ForegroundState(IsFullscreen: true, ProcessName: null);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config(), state, T0, out var blockedBy));
        Assert.Null(blockedBy);
    }

    [Fact]
    public void BlocksAListedProcessEvenInAWindow()
    {
        var guard = new SwitchGuard();

        // The point of the blocklist: a game played windowed is still a game.
        var windowed = Game(fullscreen: false);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config("League of Legends"), windowed, T0, out _));
    }

    [Theory]
    [InlineData("League of Legends")]
    [InlineData("League of Legends.exe")]
    [InlineData("  league of legends.EXE  ")]
    public void MatchesBlockedNamesLoosely(string configured)
    {
        var guard = new SwitchGuard();

        var windowed = Game(fullscreen: false);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config(configured), windowed, T0, out _));
    }

    [Fact]
    public void DoesNotBlockAWindowedProcessThatIsNotListed()
    {
        var guard = new SwitchGuard();

        var windowed = Game("Notepad", fullscreen: false);
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config("League of Legends"), windowed, T0, out _));
    }

    [Fact]
    public void SecondPressInsideTheWindowGoesThrough()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Override, guard.Evaluate(config, Game(), T0.AddMilliseconds(400), out var blockedBy));
        Assert.Null(blockedBy);
    }

    [Fact]
    public void SecondPressAfterTheWindowIsBlockedAgain()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(2500), out _));
    }

    [Fact]
    public void OverrideIsSpentAfterUse()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Override, guard.Evaluate(config, Game(), T0.AddMilliseconds(100), out _));

        // A third press starts over rather than riding the previous override.
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(200), out _));
    }

    [Fact]
    public void LeavingTheGameClearsAPendingOverride()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(config, Desktop, T0.AddMilliseconds(200), out _));

        // Back in the game, the next press is a fresh first press — not an override.
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(300), out _));
    }

    [Fact]
    public void ZeroWindowDisablesTheOverride()
    {
        var guard = new SwitchGuard();
        var config = Config();
        config.OverrideWindowMs = 0;

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(10), out _));
    }

    [Fact]
    public void AbsurdWindowIsClampedRatherThanTrusted()
    {
        var guard = new SwitchGuard();
        var config = Config();
        config.OverrideWindowMs = int.MaxValue;

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));

        // A hand-edited config must not turn "press twice" into "any time this week".
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMinutes(5), out _));
    }

    [Fact]
    public void ClockGoingBackwardsDoesNotGrantAnOverride()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMinutes(-1), out _));
    }

    [Fact]
    public void DisabledGuardAlwaysAllows()
    {
        var guard = new SwitchGuard();
        var config = Config();
        config.BlockWhileGaming = false;

        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(config, Game(), T0, out _));
    }

    [Fact]
    public void ResetForgetsAPendingOverride()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        guard.Reset();
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(100), out _));
    }

    [Fact]
    public void BlockedProcessesDefaultsToTheGameTheUserPlays()
    {
        // Documents the shipped default: LoL is blocked out of the box, windowed or not.
        var guard = new SwitchGuard();

        var windowed = Game(fullscreen: false);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(new AppConfig(), windowed, T0, out _));
    }

    [Fact]
    public void EmptyAndWhitespaceEntriesNeverMatch()
    {
        var guard = new SwitchGuard();

        var state = new ForegroundState(IsFullscreen: false, ProcessName: "   ");
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config("", "   ", ".exe"), state, T0, out _));
    }
}
