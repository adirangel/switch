using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class InputSourcesTests
{
    [Theory]
    [InlineData("HDMI1", 0x11)]
    [InlineData("hdmi1", 0x11)]
    [InlineData("HDMI 1", 0x11)]
    [InlineData("hdmi-1", 0x11)]
    [InlineData("HDMI", 0x11)]
    [InlineData("HDMI2", 0x12)]
    [InlineData("DisplayPort1", 0x0F)]
    [InlineData("DisplayPort 1", 0x0F)]
    [InlineData("DP", 0x0F)]
    [InlineData("dp2", 0x10)]
    [InlineData("0x11", 0x11)]
    [InlineData("0X0F", 0x0F)]
    [InlineData("17", 17)]
    [InlineData("  HDMI1  ", 0x11)]
    public void ParsesEverySpellingAUserMightWrite(string text, byte expected)
    {
        Assert.True(InputSources.TryParse(text, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Thunderbolt")]
    [InlineData("0xZZ")]
    [InlineData("999")]
    public void RejectsWhatItCannotResolve(string? text)
    {
        Assert.False(InputSources.TryParse(text, out _));
    }

    [Fact]
    public void CanonicalNameRoundTripsThroughTryParse()
    {
        foreach (var source in InputSources.All)
        {
            Assert.True(InputSources.TryParse(InputSources.CanonicalName(source.Code), out var code));
            Assert.Equal(source.Code, code);
        }
    }

    [Fact]
    public void DisplayNameRoundTripsThroughTryParse()
    {
        foreach (var source in InputSources.All)
        {
            Assert.True(InputSources.TryParse(InputSources.DisplayName(source.Code), out var code));
            Assert.Equal(source.Code, code);
        }
    }

    [Fact]
    public void FallsBackToHexForVendorSpecificCodes()
    {
        Assert.Equal("0x1F", InputSources.CanonicalName(0x1F));
        Assert.Equal("Input 0x1F", InputSources.DisplayName(0x1F));
    }
}
