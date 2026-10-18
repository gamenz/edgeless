using System.Text.Json;
using Edgeless.Core.Config;

namespace Edgeless.Core.Tests.Config;

public sealed class ConfigSerializerTests
{
    private const string SpecExample = """
        {
          "version": 1,
          "enabled": true,
          "hotkey": { "modifiers": ["Ctrl", "Alt"], "key": "B" },
          "autostart": "normal",
          "profiles": [
            {
              "path": "D:\\Games\\Elden Ring\\Game\\eldenring.exe",
              "enabled": true,
              "monitor": "current",
              "sizeMode": "fill",
              "custom": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
              "addPopupStyle": false,
              "applyDelayMs": 500
            }
          ]
        }
        """;

    [Fact]
    public void Deserialize_SpecExample_RoundTrips()
    {
        var config = ConfigSerializer.Deserialize(SpecExample);

        Assert.Equal(1, config.Version);
        Assert.True(config.Enabled);
        Assert.Equal(["Ctrl", "Alt"], config.Hotkey.Modifiers);
        Assert.Equal("B", config.Hotkey.Key);
        Assert.Equal(AutostartMode.Normal, config.Autostart);
        var profile = Assert.Single(config.Profiles);
        Assert.Equal(@"D:\Games\Elden Ring\Game\eldenring.exe", profile.Path);
        Assert.Equal("eldenring.exe", profile.ExeName);
        Assert.Equal("current", profile.Monitor);
        Assert.Equal(SizeMode.Fill, profile.SizeMode);
        Assert.Equal(1920, profile.Custom.Width);
        Assert.False(profile.AddPopupStyle);
        Assert.Equal(500, profile.ApplyDelayMs);

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"sizeMode\": \"fill\"", json, StringComparison.Ordinal);
        Assert.Contains("\"path\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"exe", json, StringComparison.Ordinal);
        Assert.Equal(json, ConfigSerializer.Serialize(ConfigSerializer.Deserialize(json)));
    }

    [Fact]
    public void Deserialize_MissingFields_UsesDefaults()
    {
        var config = ConfigSerializer.Deserialize("""{"profiles":[{"path":"C:\\a.exe"}]}""");

        Assert.True(config.Enabled);
        Assert.Equal(["Ctrl", "Alt"], config.Hotkey.Modifiers);
        Assert.Equal("B", config.Hotkey.Key);
        Assert.Equal(AutostartMode.Off, config.Autostart);
        var profile = Assert.Single(config.Profiles);
        Assert.True(profile.Enabled);
        Assert.Equal(SizeMode.Fill, profile.SizeMode);
        Assert.Equal(500, profile.ApplyDelayMs);
        Assert.Equal("current", profile.Monitor);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"profiles":[{"path":"C:\\a.exe","sizeMode":"stretch"}]}""")]
    public void Deserialize_MalformedOrUnknownEnum_Throws(string json)
        => Assert.ThrowsAny<JsonException>(() => ConfigSerializer.Deserialize(json));

    [Fact]
    public void Deserialize_AllowsCommentsAndTrailingCommas()
        => Assert.False(ConfigSerializer.Deserialize("{ // off\n \"enabled\": false, }").Enabled);

    [Fact]
    public void Normalize_DropsBlankRelativeAndDuplicatePaths()
    {
        var config = ConfigSerializer.Deserialize("""
            {
              "hotkey": null,
              "profiles": [
                { "path": "" },
                { "path": "game.exe" },
                { "path": "D:/G/a.exe", "custom": { "width": 0, "height": -5 } },
                { "path": "d:\\g\\A.EXE", "applyDelayMs": 42 },
                { "path": "D:\\G\\b.exe", "applyDelayMs": -5 },
                { "path": "D:\\G\\c.exe", "applyDelayMs": 99999 },
                { "path": "D:\\G\\d.exe", "monitor": "" }
              ]
            }
            """);

        var normalized = ConfigSerializer.Normalize(config);

        Assert.Equal([@"D:\G\a.exe", @"D:\G\b.exe", @"D:\G\c.exe", @"D:\G\d.exe"], normalized.Profiles.Select(p => p.Path));
        Assert.Equal(500, normalized.Profiles[0].ApplyDelayMs);
        Assert.Equal(1920, normalized.Profiles[0].Custom.Width);
        Assert.Equal(1080, normalized.Profiles[0].Custom.Height);
        Assert.Equal(0, normalized.Profiles[1].ApplyDelayMs);
        Assert.Equal(10000, normalized.Profiles[2].ApplyDelayMs);
        Assert.Equal("current", normalized.Profiles[3].Monitor);
        Assert.Equal("B", normalized.Hotkey.Key);
        Assert.Equal(1, normalized.Version);
    }

    [Fact]
    public void CreateDefault_NormalizesPath()
    {
        var profile = GameProfile.CreateDefault("C:/Games/Foo.EXE");

        Assert.Equal(@"C:\Games\Foo.EXE", profile.Path);
        Assert.Equal("foo.exe", profile.ExeName);
        Assert.Equal(SizeMode.Fill, profile.SizeMode);
        Assert.Equal("current", profile.Monitor);
    }
}
