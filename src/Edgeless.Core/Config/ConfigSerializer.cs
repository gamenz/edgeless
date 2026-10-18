using System.Text.Json;
using Edgeless.Core.Geometry;
using Edgeless.Core.Rules;

namespace Edgeless.Core.Config;

public static class ConfigSerializer
{
    public const int MaxApplyDelayMs = 10_000;

    /// <summary>Parses config.json. Throws <see cref="JsonException"/> on malformed JSON or unknown enum values.</summary>
    public static AppConfig Deserialize(string json)
        => JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig)
            ?? throw new JsonException("config.json is null");

    public static string Serialize(AppConfig config)
        => JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig).ReplaceLineEndings("\n");

    /// <summary>
    /// Repairs hand-edited junk in place: drops blank and duplicate exes (first wins), clamps delays,
    /// restores defaults for blank monitor, invalid custom size and missing objects.
    /// </summary>
    public static AppConfig Normalize(AppConfig config)
    {
        config.Version = 1;
        config.Hotkey ??= new HotkeyConfig();
        config.Hotkey.Modifiers ??= [];
        config.Hotkey.Key ??= "";

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var profiles = new List<GameProfile>();
        foreach (var profile in config.Profiles ?? [])
        {
            if (profile is null)
            {
                continue;
            }

            profile.Exe = ExeNameMatcher.Normalize(profile.Exe ?? "");
            if (profile.Exe.Length == 0 || !seen.Add(profile.Exe))
            {
                continue;
            }

            NormalizeProfile(profile);
            profiles.Add(profile);
        }

        config.Profiles = profiles;
        return config;
    }

    private static void NormalizeProfile(GameProfile profile)
    {
        profile.ApplyDelayMs = Math.Clamp(profile.ApplyDelayMs, 0, MaxApplyDelayMs);
        if (string.IsNullOrWhiteSpace(profile.Monitor))
        {
            profile.Monitor = TargetRectCalculator.CurrentMonitor;
        }

        profile.Custom ??= new CustomRect();
        if (profile.Custom.Width < 1)
        {
            profile.Custom.Width = CustomRect.DefaultWidth;
        }

        if (profile.Custom.Height < 1)
        {
            profile.Custom.Height = CustomRect.DefaultHeight;
        }
    }
}
