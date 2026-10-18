using Edgeless.Core.Config;
using Edgeless.Core.Geometry;
using Edgeless.Shell.Settings;

namespace Edgeless.IntegrationTests.Shell;

public sealed class SettingsDraftTests
{
    private static AppConfig Sample() => new()
    {
        Enabled = false,
        Autostart = AutostartMode.Normal,
        Profiles = [new GameProfile { Exe = "a.exe", SizeMode = SizeMode.Center, Monitor = @"\\.\DISPLAY2" }],
    };

    [Fact]
    public void Draft_EditsADeepCopy()
    {
        var original = Sample();
        var draft = new SettingsDraft(original);

        draft.Profiles[0].Exe = "changed.exe";
        draft.Profiles[0].Custom.Width = 1;
        draft.Hotkey.Key = "Z";

        Assert.Equal("a.exe", original.Profiles[0].Exe);
        Assert.Equal(1920, original.Profiles[0].Custom.Width);
        Assert.Equal("B", original.Hotkey.Key);
    }

    [Fact]
    public void ToConfig_NormalizesAndKeepsNonEditedFields()
    {
        var draft = new SettingsDraft(Sample());
        draft.AddProfile();
        draft.Profiles[^1].Exe = "New Game";

        var config = draft.ToConfig();

        Assert.False(config.Enabled);
        Assert.Equal(AutostartMode.Normal, config.Autostart);
        Assert.Equal(["a.exe", "new game.exe"], config.Profiles.Select(p => p.Exe));
    }

    [Fact]
    public void AddProfile_SelectsUniquePlaceholder_RemoveAtClampsSelection()
    {
        var draft = new SettingsDraft(new AppConfig());

        Assert.Equal(0, draft.AddProfile());
        Assert.Equal(1, draft.AddProfile());
        Assert.Equal(["game.exe", "game2.exe"], draft.Profiles.Select(p => p.Exe));

        Assert.Equal(0, draft.RemoveAt(1));
        Assert.Equal(-1, draft.RemoveAt(0));
        Assert.Empty(draft.Profiles);
    }

    [Fact]
    public void MonitorOptions_StartWithCurrentThenDevices()
    {
        MonitorDescriptor[] monitors =
        [
            new(@"\\.\DISPLAY1", new RectI(0, 0, 2560, 1440)),
            new(@"\\.\DISPLAY2", new RectI(-1920, 0, 0, 1080)),
        ];

        var options = SettingsDraft.MonitorOptions(monitors);

        Assert.Equal(["Màn hình hiện tại", @"\\.\DISPLAY1 — 2560×1440", @"\\.\DISPLAY2 — 1920×1080"], options.Select(o => o.Label));
        Assert.Equal(["current", @"\\.\DISPLAY1", @"\\.\DISPLAY2"], options.Select(o => o.Value));
    }

    [Fact]
    public void MonitorIndex_UnknownDeviceIsAppendedSoItIsNotLost()
    {
        var options = SettingsDraft.MonitorOptions([new(@"\\.\DISPLAY1", new RectI(0, 0, 100, 100))]);

        var index = SettingsDraft.MonitorIndex(options, @"\\.\DISPLAY7");

        Assert.Equal(2, index);
        Assert.Equal(@"\\.\DISPLAY7 (không kết nối)", options[2].Label);
    }

    [Theory]
    [InlineData((ushort)0x0642, new[] { "Ctrl", "Alt" }, "B")] // HOTKEYF_CONTROL|HOTKEYF_ALT, 'B'
    [InlineData((ushort)0x017B, new[] { "Shift" }, "F12")]
    public void Hotkey_FromControl(ushort value, string[] modifiers, string key)
    {
        var hotkey = SettingsDraft.HotkeyFromControl(value);

        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(key, hotkey.Key);
    }

    [Fact]
    public void Hotkey_ToControl_RoundTrips()
        => Assert.Equal((ushort)0x0642, SettingsDraft.HotkeyToControl(new HotkeyConfig()));

    [Fact]
    public void ParseInt_FallsBackOnJunk()
    {
        Assert.Equal(250, SettingsDraft.ParseInt(" 250 ", 7));
        Assert.Equal(-40, SettingsDraft.ParseInt("-40", 7));
        Assert.Equal(7, SettingsDraft.ParseInt("abc", 7));
    }
}
