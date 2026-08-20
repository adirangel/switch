using ScreenSwitch.Core;
using Xunit;

namespace ScreenSwitch.Tests;

public class HotkeySpecTests
{
    [Fact]
    public void ParsesTheDefaultCombination()
    {
        Assert.True(HotkeySpec.TryParse("Ctrl+Alt+S", out var spec));
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, spec.Modifiers);
        Assert.Equal('S', spec.VirtualKey);
        Assert.Equal(HotkeySpec.Default, spec);
    }

    [Theory]
    [InlineData("ctrl + alt + s")]
    [InlineData("CONTROL+ALT+S")]
    [InlineData("Alt+Ctrl+S")]
    public void IsForgivingAboutCaseSpacingAndOrder(string text)
    {
        Assert.True(HotkeySpec.TryParse(text, out var spec));
        Assert.Equal(HotkeySpec.Default, spec);
    }

    [Theory]
    [InlineData("Ctrl+Shift+F12", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7Bu)]
    [InlineData("Win+Alt+Space", HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x20u)]
    [InlineData("Ctrl+Alt+PageDown", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x22u)]
    [InlineData("Ctrl+Alt+7", HotkeyModifiers.Control | HotkeyModifiers.Alt, (uint)'7')]
    [InlineData("Ctrl+F1", HotkeyModifiers.Control, 0x70u)]
    public void ParsesModifiersAndKeys(string text, HotkeyModifiers modifiers, uint virtualKey)
    {
        Assert.True(HotkeySpec.TryParse(text, out var spec));
        Assert.Equal(modifiers, spec.Modifiers);
        Assert.Equal(virtualKey, spec.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("S")]              // no modifier: Windows will not register it
    [InlineData("Ctrl+Alt")]       // no key
    [InlineData("Ctrl+Alt+S+D")]   // two keys
    [InlineData("Ctrl+Alt+F25")]   // out of the F1-F24 range
    [InlineData("Ctrl+Alt+Banana")]
    public void RejectsCombinationsItCannotRegister(string? text)
    {
        Assert.False(HotkeySpec.TryParse(text, out _));
    }

    [Theory]
    [InlineData("Ctrl+Alt+S")]
    [InlineData("Ctrl+Shift+F12")]
    [InlineData("Ctrl+Alt+Win+Delete")]
    [InlineData("Shift+Win+Home")]
    public void ToStringRoundTrips(string text)
    {
        Assert.True(HotkeySpec.TryParse(text, out var spec));
        Assert.True(HotkeySpec.TryParse(spec.ToString(), out var reparsed));
        Assert.Equal(spec, reparsed);
    }

    [Fact]
    public void ToStringUsesAStableModifierOrder()
    {
        Assert.True(HotkeySpec.TryParse("Alt+Shift+Ctrl+Win+K", out var spec));
        Assert.Equal("Ctrl+Alt+Shift+Win+K", spec.ToString());
    }
}
