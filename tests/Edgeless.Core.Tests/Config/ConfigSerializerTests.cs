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
              "exe": "eldenring.exe",
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
        Assert.Equal("eldenring.exe", profile.Exe);
        Assert.Equal("current", profile.Monitor);
        Assert.Equal(SizeMode.Fill, profile.SizeMode);
        Assert.Equal(1920, profile.Custom.Width);
        Assert.False(profile.AddPopupStyle);
        Assert.Equal(500, profile.ApplyDelayMs);

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"sizeMode\": \"fill\"", json, StringComparison.Ordinal);
        Assert.Contains("\"autostart\": \"normal\"", json, StringComparison.Ordinal);
        Assert.Equal(json, ConfigSerializer.Serialize(ConfigSerializer.Deserialize(json)));
    }

    [Fact]
    public void Deserialize_MissingFields_UsesDefaults()
    {
        var config = ConfigSerializer.Deserialize("""{"profiles":[{"exe":"a.exe"}]}""");

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
    [InlineData("""{"profiles":[{"exe":"a.exe","sizeMode":"stretch"}]}""")]
    public void Deserialize_MalformedOrUnknownEnum_Throws(string json)
        => Assert.ThrowsAny<JsonException>(() => ConfigSerializer.Deserialize(json));

    [Fact]
    public void Deserialize_AllowsCommentsAndTrailingCommas()
        => Assert.False(ConfigSerializer.Deserialize("{ // off\n \"enabled\": false, }").Enabled);

    [Fact]
    public void Normalize_DropsAndClampsJunk()
    {
        var config = ConfigSerializer.Deserialize("""
            {
              "hotkey": null,
              "profiles": [
                { "exe": "" },
                { "exe": "Game", "custom": { "width": 0, "height": -5 } },
                { "exe": "game.exe", "applyDelayMs": 42 },
                { "exe": "x", "applyDelayMs": -5 },
                { "exe": "y", "applyDelayMs": 99999 },
                { "exe": "z", "monitor": "" }
              ]
            }
            """);

        var normalized = ConfigSerializer.Normalize(config);

        Assert.Equal(["game.exe", "x.exe", "y.exe", "z.exe"], normalized.Profiles.Select(p => p.Exe));
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
    public void CreateDefault_UsesNormalizedExeAndDefaults()
    {
        var profile = GameProfile.CreateDefault(@"C:\Games\Foo.EXE");

        Assert.Equal("foo.exe", profile.Exe);
        Assert.Equal(SizeMode.Fill, profile.SizeMode);
        Assert.Equal("current", profile.Monitor);
    }
}
