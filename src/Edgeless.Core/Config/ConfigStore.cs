using System.Text;
using System.Text.Json;

namespace Edgeless.Core.Config;

/// <param name="InvalidBackupPath">Set when a malformed config.json was moved aside.</param>
public sealed record ConfigLoadResult(AppConfig Config, string? InvalidBackupPath);

/// <summary>Loads and saves config.json; malformed files are backed up and replaced by defaults.</summary>
public sealed class ConfigStore(string directory, TimeProvider time)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public string FilePath { get; } = Path.Combine(directory, "config.json");

    public ConfigLoadResult Load()
    {
        if (!File.Exists(FilePath))
        {
            var created = new AppConfig();
            Save(created);
            return new ConfigLoadResult(created, null);
        }

        try
        {
            return new ConfigLoadResult(ConfigSerializer.Normalize(ConfigSerializer.Deserialize(File.ReadAllText(FilePath))), null);
        }
        catch (JsonException)
        {
            var backup = Path.Combine(directory, $"config.invalid-{time.GetLocalNow():yyyyMMdd-HHmmss}.json");
            File.Move(FilePath, backup, overwrite: true);
            var defaults = new AppConfig();
            Save(defaults);
            return new ConfigLoadResult(defaults, backup);
        }
    }

    /// <summary>Writes a temp file then renames it over config.json, so a crash never leaves a half-written file.</summary>
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, ConfigSerializer.Serialize(config), Utf8NoBom);
        File.Move(temp, FilePath, overwrite: true);
    }
}
