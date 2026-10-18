using Edgeless.Windowing;
using Microsoft.Extensions.Time.Testing;

namespace Edgeless.IntegrationTests.Windowing;

public sealed class ProcessPathCacheTests
{
    [Fact]
    public void Cache_ExpiresSoReusedPidIsRequeried()
    {
        var time = new FakeTimeProvider();
        var image = @"C:\Program Files\Browser\chrome.exe";
        var queries = 0;
        var cache = new ProcessPathCache(time, _ => { queries++; return image; });

        Assert.Equal(@"C:\Program Files\Browser\chrome.exe", cache.GetImagePath(1234));
        Assert.Equal(@"C:\Program Files\Browser\chrome.exe", cache.GetImagePath(1234)); // same scan: cached
        Assert.Equal(1, queries);

        // chrome exits, a game starts and Windows hands it the same PID.
        image = @"D:\Games\EldenRing\eldenring.exe";
        time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(@"D:\Games\EldenRing\eldenring.exe", cache.GetImagePath(1234));
        Assert.Equal(2, queries);
    }

    [Fact]
    public void Failures_AreNotCached()
    {
        var queries = 0;
        var cache = new ProcessPathCache(new FakeTimeProvider(), _ => { queries++; return null; });

        Assert.Null(cache.GetImagePath(1));
        Assert.Null(cache.GetImagePath(1));
        Assert.Equal(2, queries);
    }

    [Fact]
    public void Forget_DropsEntry()
    {
        var queries = 0;
        var cache = new ProcessPathCache(new FakeTimeProvider(), _ => { queries++; return @"C:\a.exe"; });
        cache.GetImagePath(1);

        cache.Forget(1);
        cache.GetImagePath(1);

        Assert.Equal(2, queries);
    }
}
