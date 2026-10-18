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
        Profiles =
        [
            new GameProfile { Path = @"D:\Games\A\a.exe", SizeMode = SizeMode.Center, Monitor = @"\\.\DISPLAY2" },
        ],
    };

    [Fact]
    public void Draft_EditsADeepCopy()
    {
        var original = Sample();
        var draft = new SettingsDraft(original);

        draft.Profiles[0].Path = @"D:\Games\Changed\changed.exe";
        draft.Profiles[0].Custom.Width = 1;
        draft.Hotkey.Key = "Z";

        Assert.Equal(@"D:\Games\A\a.exe", original.Profiles[0].Path);
        Assert.Equal(1920, original.Profiles[0].Custom.Width);
        Assert.Equal("B", original.Hotkey.Key);
    }

    [Fact]
    public void ToConfig_NormalizesAndKeepsNonEditedFields()
    {
        var draft = new SettingsDraft(Sample());
        draft.AddProfile("d:/games/b/New Game.exe");

        var config = draft.ToConfig();

        Assert.False(config.Enabled);
        Assert.Equal(AutostartMode.Normal, config.Autostart);
        Assert.Equal([@"D:\Games\A\a.exe", @"d:\games\b\New Game.exe"], config.Profiles.Select(p => p.Path));
    }

    [Fact]
    public void AddProfile_NewPath_AppendsNormalized()
    {
        var draft = new SettingsDraft(new AppConfig());

        Assert.Equal(0, draft.AddProfile("D:/Games/A/game.exe"));
        Assert.Equal(@"D:\Games\A\game.exe", draft.Profiles[0].Path);
    }

    [Fact]
    public void AddProfile_ExistingPath_ReturnsExistingIndex()
    {
        var draft = new SettingsDraft(new AppConfig());

        Assert.Equal(0, draft.AddProfile(@"D:\Games\A\game.exe"));
        Assert.Equal(1, draft.AddProfile(@"D:\Games\B\game.exe")); // same name, other folder: a separate game
        Assert.Equal(0, draft.AddProfile("d:/games/a/GAME.exe"));
        Assert.Equal(2, draft.Profiles.Count);
    }

    [Fact]
    public void AddProfile_InvalidPath_ReturnsMinusOne()
    {
        var draft = new SettingsDraft(new AppConfig());

        Assert.Equal(-1, draft.AddProfile("game.exe"));
        Assert.Empty(draft.Profiles);
    }

    [Fact]
    public void ReplacePath_ChangesPath()
    {
        var draft = new SettingsDraft(Sample());

        Assert.True(draft.ReplacePath(0, "E:/Moved/a.exe"));
        Assert.Equal(@"E:\Moved\a.exe", draft.Profiles[0].Path);
    }

    [Fact]
    public void ReplacePath_DuplicateOfOther_IsRejected()
    {
        var draft = new SettingsDraft(Sample());
        draft.AddProfile(@"D:\Games\B\b.exe");

        Assert.False(draft.ReplacePath(1, @"d:\games\a\A.EXE"));
        Assert.False(draft.ReplacePath(1, "relative.exe"));
        Assert.Equal(@"D:\Games\B\b.exe", draft.Profiles[1].Path);
    }

    [Theory]
    [InlineData(@"D:\Games\A\Game.exe", @"game.exe — D:\Games\A")]
    [InlineData(@"D:\Game.exe", @"game.exe — D:\")]
    [InlineData(@"D:\Work\edgeless\tests\bin\Debug\Game.exe", @"game.exe — D:\…\bin\Debug")]
    [InlineData(@"\\server\share\games\x\y\Game.exe", @"game.exe — \\server\share\…\x\y")]
    public void ListLabel_ShowsNameAndShortFolder(string path, string expected)
        => Assert.Equal(expected, SettingsDraft.ListLabel(new GameProfile { Path = path }));

    [Fact]
    public void RemoveAt_ClampsSelection()
    {
        var draft = new SettingsDraft(new AppConfig());
        draft.AddProfile(@"C:\a.exe");
        draft.AddProfile(@"C:\b.exe");

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

        Assert.Equal(["Current monitor", @"\\.\DISPLAY1 — 2560×1440", @"\\.\DISPLAY2 — 1920×1080"], options.Select(o => o.Label));
        Assert.Equal(["current", @"\\.\DISPLAY1", @"\\.\DISPLAY2"], options.Select(o => o.Value));
    }

    [Fact]
    public void MonitorIndex_UnknownDeviceIsAppendedSoItIsNotLost()
    {
        var options = SettingsDraft.MonitorOptions([new(@"\\.\DISPLAY1", new RectI(0, 0, 100, 100))]);

        var index = SettingsDraft.MonitorIndex(options, @"\\.\DISPLAY7");

        Assert.Equal(2, index);
        Assert.Equal(@"\\.\DISPLAY7 (disconnected)", options[2].Label);
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
