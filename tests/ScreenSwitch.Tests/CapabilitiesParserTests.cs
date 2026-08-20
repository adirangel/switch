using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class CapabilitiesParserTests
{
    /// <summary>A capabilities string shaped like the one an ASUS TUF VG27AQ3A reports.</summary>
    private const string AsusCapabilities =
        "(prot(monitor)type(LCD)model(VG27AQ3A)cmds(01 02 03 07 0C E3 F3)" +
        "vcp(02 04 05 08 10 12 14(01 05 06 08 0B) 16 18 1A 52 60(0F 11 12) " +
        "AC AE B2 B6 C0 C6 C8 C9 D6(01 04 05) DF E0 E1 E2 E3)" +
        "mswhql(1)asset_eep(40)mccs_ver(2.2))";

    [Fact]
    public void ParsesTheInputsListedUnderFeature0x60()
    {
        Assert.Equal(new byte[] { 0x0F, 0x11, 0x12 }, CapabilitiesParser.ParseSupportedInputs(AsusCapabilities));
    }

    [Fact]
    public void ParsesTheModelName()
    {
        Assert.Equal("VG27AQ3A", CapabilitiesParser.ParseModel(AsusCapabilities));
    }

    [Fact]
    public void IgnoresA60ThatIsAValueOfAnotherFeature()
    {
        // Feature 14's value list contains 60; only the top-level 60(...) group is the input list.
        const string capabilities = "(vcp(14(01 60 61) 60(0F 11)))";
        Assert.Equal(new byte[] { 0x0F, 0x11 }, CapabilitiesParser.ParseSupportedInputs(capabilities));
    }

    [Fact]
    public void ReturnsEmptyWhenFeature0x60HasNoValueList()
    {
        // A monitor that supports input select but does not enumerate its inputs.
        Assert.Empty(CapabilitiesParser.ParseSupportedInputs("(vcp(02 04 60 DF))"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage without any structure")]
    [InlineData("(vcp(60(0F 11")] // truncated reply: unbalanced parentheses
    public void ReturnsEmptyForUnusableInput(string? capabilities)
    {
        Assert.Empty(CapabilitiesParser.ParseSupportedInputs(capabilities));
    }

    [Fact]
    public void DropsDuplicateInputValues()
    {
        Assert.Equal(new byte[] { 0x0F, 0x11 }, CapabilitiesParser.ParseSupportedInputs("(vcp(60(0F 11 0F)))"));
    }

    [Fact]
    public void IsNotFooledByAFeatureNameEndingInVcp()
    {
        // "xvcp(...)" must not be read as the vcp section; the real one comes later.
        Assert.Equal(new byte[] { 0x12 }, CapabilitiesParser.ParseSupportedInputs("(xvcp(60(01))vcp(60(12)))"));
    }

    [Fact]
    public void HandlesWhitespaceBetweenSectionNameAndParenthesis()
    {
        Assert.Equal(new byte[] { 0x0F }, CapabilitiesParser.ParseSupportedInputs("(vcp (60(0F)))"));
    }

    [Fact]
    public void ReturnsNullModelWhenAbsent()
    {
        Assert.Null(CapabilitiesParser.ParseModel("(vcp(60(0F 11)))"));
    }
}
