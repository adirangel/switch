using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class AppConfigTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScreenSwitchTests", Guid.NewGuid().ToString("N"));

    private string ConfigPath => Path.Combine(_directory, "config.json");

    [Fact]
    public void SurvivesCollectionsExplicitlySetToNull()
    {
        // The config file is documented as hand-editable, so "monitorTargets": null is a thing a
        // user can plausibly write. It must not take the app down on startup.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, """
            {
              "targetInput": "HDMI1",
              "monitorTargets": null,
              "blockedProcesses": null
            }
            """);

        var config = AppConfig.Load(ConfigPath, out var error);

        Assert.Null(error);
        Assert.NotNull(config.MonitorTargets);
        Assert.NotNull(config.BlockedProcesses);
        Assert.Empty(config.MonitorTargets);
        Assert.Empty(config.BlockedProcesses);
    }

    [Fact]
    public void MonitorTargetsStayCaseInsensitiveAfterLoading()
    {
        // The in-memory default uses OrdinalIgnoreCase; System.Text.Json builds its own dictionary
        // when it sets the property, so the comparer has to be re-applied or the intent is
        // silently lost for every config that came off disk.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, """
            {
              "targetInput": "HDMI1",
              "monitorTargets": { "\\\\?\\DISPLAY#ACI27E7": "HDMI2" }
            }
            """);

        var config = AppConfig.Load(ConfigPath, out _);

        Assert.Equal((byte?)0x12, config.ResolveTargetFor(@"\\?\display#aci27e7"));
    }

    [Fact]
    public void DefaultsAreUsableExceptForTheTargetTheUserMustPick()
    {
        var config = new AppConfig();

        Assert.Null(config.ResolveGlobalTarget());
        Assert.Equal(HotkeySpec.Default, config.ResolveHotkey());
        Assert.True(config.ShowNotifications);
    }

    [Fact]
    public void RoundTripsThroughTheFile()
    {
        var original = new AppConfig
        {
            TargetInput = "HDMI1",
            Hotkey = "Ctrl+Shift+F12",
            DelayBetweenMonitorsMs = 300,
            ShowNotifications = false,
            RetryCount = 2,
            MonitorTargets = { ["MONITOR#1"] = "HDMI2" },
        };

        original.Save(ConfigPath);
        var loaded = AppConfig.Load(ConfigPath, out var error);

        Assert.Null(error);
        Assert.Equal((byte)0x11, loaded.ResolveGlobalTarget());
        Assert.Equal((byte)0x12, loaded.ResolveTargetFor("MONITOR#1"));
        Assert.Equal("Ctrl+Shift+F12", loaded.ResolveHotkey().ToString());
        Assert.Equal(300, loaded.DelayBetweenMonitorsMs);
        Assert.False(loaded.ShowNotifications);
        Assert.Equal(2, loaded.RetryCount);
    }

    [Fact]
    public void MissingFileYieldsDefaultsWithoutAnError()
    {
        var config = AppConfig.Load(ConfigPath, out var error);

        Assert.Null(error);
        Assert.Null(config.ResolveGlobalTarget());
    }

    [Fact]
    public void CorruptFileIsReportedRatherThanThrown()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, "{ this is not json");

        var config = AppConfig.Load(ConfigPath, out var error);

        Assert.NotNull(error);
        Assert.Equal(HotkeySpec.Default, config.ResolveHotkey());
    }

    [Fact]
    public void HandEditedValuesAreAccepted()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, """{ "targetInput": "dp", "hotkey": "ctrl+alt+p" }""");

        var config = AppConfig.Load(ConfigPath, out var error);

        Assert.Null(error);
        Assert.Equal((byte)0x0F, config.ResolveGlobalTarget());
        Assert.Equal("Ctrl+Alt+P", config.ResolveHotkey().ToString());
    }

    [Fact]
    public void AnUnparseableTargetReadsAsUnconfiguredRatherThanSwitchingSomewhereRandom()
    {
        var config = new AppConfig { TargetInput = "Thunderbolt" };

        Assert.Null(config.ResolveGlobalTarget());
        Assert.Null(config.ResolveTargetFor("MONITOR#1"));
    }

    [Fact]
    public void AnUnparseableHotkeyFallsBackToTheDefault()
    {
        Assert.Equal(HotkeySpec.Default, new AppConfig { Hotkey = "nonsense" }.ResolveHotkey());
    }

    [Fact]
    public void MonitorsWithoutAnOverrideUseTheGlobalTarget()
    {
        var config = new AppConfig
        {
            TargetInput = "DisplayPort1",
            MonitorTargets = { ["MONITOR#1"] = "HDMI2" },
        };

        Assert.Equal((byte)0x12, config.ResolveTargetFor("MONITOR#1"));
        Assert.Equal((byte)0x0F, config.ResolveTargetFor("MONITOR#2"));
        Assert.Equal((byte)0x0F, config.ResolveTargetFor(null));
    }

    [Fact]
    public void SaveCreatesTheDirectoryItNeeds()
    {
        Assert.False(Directory.Exists(_directory));

        new AppConfig { TargetInput = "HDMI1" }.Save(ConfigPath);

        Assert.True(File.Exists(ConfigPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
