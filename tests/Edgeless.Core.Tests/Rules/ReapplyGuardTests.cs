using Edgeless.Core.Rules;
using Microsoft.Extensions.Time.Testing;

namespace Edgeless.Core.Tests.Rules;

public sealed class ReapplyGuardTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void FiveReapplies_Allowed_SixthWithinWindow_Trips()
    {
        var guard = new ReapplyGuard(_time);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(guard.TryRecord(1));
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.False(guard.TryRecord(1));
    }

    [Fact]
    public void OldRecords_SlideOut()
    {
        var guard = new ReapplyGuard(_time);
        for (var i = 0; i < 5; i++)
        {
            guard.TryRecord(1);
        }

        _time.Advance(TimeSpan.FromSeconds(10.001));

        Assert.True(guard.TryRecord(1));
    }

    [Fact]
    public void Windows_AreIndependent()
    {
        var guard = new ReapplyGuard(_time);
        for (var i = 0; i < 5; i++)
        {
            guard.TryRecord(1);
        }

        Assert.True(guard.TryRecord(2));
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var guard = new ReapplyGuard(_time);
        for (var i = 0; i < 5; i++)
        {
            guard.TryRecord(1);
        }

        guard.Reset(1);

        Assert.True(guard.TryRecord(1));
    }
}
