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

    private static ForegroundState Desktop => new(IsFullscreen: false, CapturesCursor: false, ProcessName: "explorer");

    private static ForegroundState Game(string name = "SomeGame", bool fullscreen = true, bool capturesCursor = false)
        => new(fullscreen, capturesCursor, name);

    // ------------------------------------------------------------- generic detection

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

    [Theory]
    [InlineData("SomeGame")]
    [InlineData("AnotherGame")]
    [InlineData("csgo")]
    [InlineData("EldenRing")]
    [InlineData("a game released after this code was written")]
    public void BlocksAnyFullScreenApplicationWhateverItIsCalled(string name)
    {
        // The whole point: no list is consulted, so a game nobody has heard of is still covered.
        var guard = new SwitchGuard();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(new AppConfig(), Game(name), T0, out var blockedBy));
        Assert.Equal(name, blockedBy);
    }

    [Theory]
    [InlineData("SomeGame")]
    [InlineData("AnotherGame")]
    public void BlocksAnyWindowedApplicationThatLocksTheCursor(string name)
    {
        // A game in a real window is caught by the cursor being confined to it — again, no name
        // matching involved.
        var guard = new SwitchGuard();

        var windowed = Game(name, fullscreen: false, capturesCursor: true);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(new AppConfig(), windowed, T0, out _));
    }

    [Fact]
    public void BlocksAFullScreenAppItCannotName()
    {
        var guard = new SwitchGuard();

        var state = new ForegroundState(IsFullscreen: true, CapturesCursor: false, ProcessName: null);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config(), state, T0, out var blockedBy));
        Assert.Null(blockedBy);
    }

    [Fact]
    public void ShippedDefaultsNameNoGames()
    {
        // The default config is deliberately empty of game names: detection is behavioural, so
        // nobody has to see someone else's library in their settings.
        Assert.Empty(new AppConfig().BlockedProcesses);
    }

    [Fact]
    public void LeavesAnOrdinaryWindowedAppAlone()
    {
        var guard = new SwitchGuard();

        // Windowed, cursor free, not listed: a text editor, not a game.
        var editor = Game("Notepad", fullscreen: false);
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(new AppConfig(), editor, T0, out _));
    }

    // ------------------------------------------------------------- the optional blocklist

    [Fact]
    public void BlocksAListedProcessEvenWithNoOtherSignal()
    {
        var guard = new SwitchGuard();

        // The escape hatch for a game that runs windowed and leaves the cursor free.
        var windowed = Game(fullscreen: false);
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(Config("SomeGame"), windowed, T0, out _));
    }

    [Theory]
    [InlineData("SomeGame")]
    [InlineData("SomeGame.exe")]
    [InlineData("  somegame.EXE  ")]
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
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config("SomeGame"), windowed, T0, out _));
    }

    [Fact]
    public void EmptyAndWhitespaceEntriesNeverMatch()
    {
        var guard = new SwitchGuard();

        var state = new ForegroundState(IsFullscreen: false, CapturesCursor: false, ProcessName: "   ");
        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(Config("", "   ", ".exe"), state, T0, out _));
    }

    // ------------------------------------------------------------- the override

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
    public void ResetForgetsAPendingOverride()
    {
        var guard = new SwitchGuard();
        var config = Config();

        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0, out _));
        guard.Reset();
        Assert.Equal(GuardVerdict.Block, guard.Evaluate(config, Game(), T0.AddMilliseconds(100), out _));
    }

    // ------------------------------------------------------------- opting out

    [Fact]
    public void DisabledGuardAlwaysAllows()
    {
        var guard = new SwitchGuard();
        var config = Config();
        config.BlockWhileGaming = false;

        Assert.Equal(GuardVerdict.Allow, guard.Evaluate(config, Game(capturesCursor: true), T0, out _));
    }
}
