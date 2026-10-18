using Edgeless.Core.Config;

namespace Edgeless.Core.Tests.Config;

public sealed class HotkeyBindingTests
{
    [Fact]
    public void HotkeyBinding_ParsesCtrlAltB()
    {
        Assert.True(HotkeyBinding.TryParse(new HotkeyConfig(), out var modifiers, out var virtualKey));
        Assert.Equal(0x4003u, modifiers);
        Assert.Equal(0x42u, virtualKey);
    }

    [Theory]
    [InlineData("win,shift", "f12", 0x400Cu, 0x7Bu)]
    [InlineData("Ctrl", "7", 0x4002u, 0x37u)]
    [InlineData("Alt", "F24", 0x4001u, 0x87u)]
    public void HotkeyBinding_ParsesOtherKeys(string modifiers, string key, uint expectedModifiers, uint expectedKey)
    {
        var config = new HotkeyConfig { Modifiers = [.. modifiers.Split(',')], Key = key };

        Assert.True(HotkeyBinding.TryParse(config, out var parsedModifiers, out var parsedKey));
        Assert.Equal(expectedModifiers, parsedModifiers);
        Assert.Equal(expectedKey, parsedKey);
    }

    [Fact]
    public void HotkeyBinding_RejectsNoModifierOrUnknownKey()
    {
        Assert.False(HotkeyBinding.TryParse(new HotkeyConfig { Modifiers = [], Key = "B" }, out _, out _));
        Assert.False(HotkeyBinding.TryParse(new HotkeyConfig { Modifiers = ["Ctrl"], Key = "Enter" }, out _, out _));
        Assert.False(HotkeyBinding.TryParse(new HotkeyConfig { Modifiers = ["Hyper"], Key = "B" }, out _, out _));
        Assert.False(HotkeyBinding.TryParse(new HotkeyConfig { Modifiers = ["Ctrl"], Key = "F25" }, out _, out _));
    }

    [Fact]
    public void HotkeyBinding_FromNative_RoundTrips()
    {
        var config = HotkeyBinding.FromNative(HotkeyBinding.MOD_CONTROL | HotkeyBinding.MOD_SHIFT, 0x4E);

        Assert.Equal(["Ctrl", "Shift"], config.Modifiers);
        Assert.Equal("N", config.Key);
        Assert.True(HotkeyBinding.TryParse(config, out var modifiers, out var virtualKey));
        Assert.Equal(HotkeyBinding.MOD_CONTROL | HotkeyBinding.MOD_SHIFT | HotkeyBinding.MOD_NOREPEAT, modifiers);
        Assert.Equal(0x4Eu, virtualKey);
    }
}
