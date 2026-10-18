using Edgeless.Core.Rules;
using Microsoft.Extensions.Time.Testing;

namespace Edgeless.Core.Tests.Rules;

public sealed class NotificationThrottleTests
{
    [Fact]
    public void Throttle_SameKeyWithin5Minutes_Suppressed()
    {
        var time = new FakeTimeProvider();
        var throttle = new NotificationThrottle(time);

        Assert.True(throttle.ShouldShow("uipi:game.exe"));
        time.Advance(TimeSpan.FromMinutes(4.9));
        Assert.False(throttle.ShouldShow("uipi:game.exe"));
        Assert.True(throttle.ShouldShow("guard:game.exe"));
        time.Advance(TimeSpan.FromMinutes(0.1));
        Assert.True(throttle.ShouldShow("uipi:game.exe"));
    }
}
