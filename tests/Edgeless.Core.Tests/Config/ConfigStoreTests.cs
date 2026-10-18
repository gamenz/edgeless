using Edgeless.Core.Config;
using Microsoft.Extensions.Time.Testing;

namespace Edgeless.Core.Tests.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 9, 30, 5, TimeSpan.Zero));

    public ConfigStoreTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Load_MissingFile_CreatesDefault()
    {
        var store = new ConfigStore(Path.Combine(_dir.Path, "sub"), _time);

        var result = store.Load();

        Assert.True(result.Config.Enabled);
        Assert.Null(result.InvalidBackupPath);
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void Load_Malformed_BacksUpAndUsesDefaults()
    {
        var store = new ConfigStore(_dir.Path, _time);
        File.WriteAllText(store.FilePath, "{ bad");

        var result = store.Load();

        var backup = Path.Combine(_dir.Path, "config.invalid-20261018-093005.json");
        Assert.Equal(backup, result.InvalidBackupPath);
        Assert.Equal("{ bad", File.ReadAllText(backup));
        Assert.True(result.Config.Enabled);
        Assert.Empty(result.Config.Profiles);
        Assert.DoesNotContain("bad", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ReturnsNormalizedConfig()
    {
        var store = new ConfigStore(_dir.Path, _time);
        File.WriteAllText(store.FilePath, """{"profiles":[{"path":"D:/Games/Game.exe"},{"path":"game.exe"}]}""");

        Assert.Equal(@"D:\Games\Game.exe", Assert.Single(store.Load().Config.Profiles).Path);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new ConfigStore(_dir.Path, _time);
        var config = new AppConfig { Enabled = false, Profiles = [GameProfile.CreateDefault(@"C:\Games\a.exe")] };

        store.Save(config);
        var loaded = store.Load().Config;

        Assert.False(loaded.Enabled);
        Assert.Equal(@"C:\Games\a.exe", Assert.Single(loaded.Profiles).Path);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new ConfigStore(_dir.Path, _time);

        store.Save(new AppConfig());
        store.Save(new AppConfig());

        Assert.Equal(["config.json"], Directory.GetFiles(_dir.Path).Select(Path.GetFileName));
    }
}
