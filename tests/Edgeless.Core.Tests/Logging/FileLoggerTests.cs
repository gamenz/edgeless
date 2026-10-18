using Edgeless.Core.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Edgeless.Core.Tests.Logging;

public sealed class FileLoggerTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 9, 30, 5, 123, TimeSpan.Zero));

    public FileLoggerTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Logger_WritesFormattedLine()
    {
        using (var log = new FileLogger(_dir.Path, _time))
        {
            log.Info("hello");
            log.Warn("careful");
            log.Error("boom");
        }

        var lines = File.ReadAllLines(Path.Combine(_dir.Path, "edgeless.log"));
        Assert.Equal(
            ["2026-10-18 09:30:05.123 [INFO] hello", "2026-10-18 09:30:05.123 [WARN] careful", "2026-10-18 09:30:05.123 [ERROR] boom"],
            lines);
    }

    [Fact]
    public void Logger_FlushesEveryLine()
    {
        using var log = new FileLogger(_dir.Path, _time);

        log.Info("visible before dispose");

        using var reader = new FileStream(Path.Combine(_dir.Path, "edgeless.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.Contains("visible before dispose", new StreamReader(reader).ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_RollsAtMaxBytes_KeepsThreeFiles()
    {
        using (var log = new FileLogger(_dir.Path, _time, maxBytes: 200))
        {
            for (var i = 0; i < 50; i++)
            {
                log.Info($"line {i:D3}");
            }
        }

        var names = Directory.GetFiles(_dir.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(["edgeless.1.log", "edgeless.2.log", "edgeless.log"], names);
        foreach (var file in Directory.GetFiles(_dir.Path))
        {
            Assert.True(new FileInfo(file).Length <= 200);
        }

        Assert.EndsWith("line 049", File.ReadAllLines(Path.Combine(_dir.Path, "edgeless.log"))[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_CreatesMissingDirectory()
    {
        var nested = Path.Combine(_dir.Path, "a", "logs");

        using (var log = new FileLogger(nested, _time))
        {
            log.Info("x");
        }

        Assert.True(File.Exists(Path.Combine(nested, "edgeless.log")));
    }
}
