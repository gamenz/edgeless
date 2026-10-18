using Edgeless.Platform;

namespace Edgeless.IntegrationTests.Platform;

public sealed class SingleInstanceTests
{
    private static SingleInstance? AcquireOnOtherThread(string name, TimeSpan wait)
    {
        SingleInstance? result = null;
        var thread = new Thread(() => result = SingleInstance.TryAcquire(name, wait));
        thread.Start();
        thread.Join();
        return result;
    }

    [Fact]
    public void SingleInstance_SecondAcquireFails_AfterDisposeSucceeds()
    {
        var name = @"Local\Edgeless.Tests." + Guid.NewGuid().ToString("N");
        var first = SingleInstance.TryAcquire(name, TimeSpan.Zero);
        Assert.NotNull(first);

        Assert.Null(AcquireOnOtherThread(name, TimeSpan.Zero));

        first.Dispose();
        var again = AcquireOnOtherThread(name, TimeSpan.Zero);
        Assert.NotNull(again);
    }

    [Fact]
    public void SingleInstance_WaitsForRelease()
    {
        var name = @"Local\Edgeless.Tests." + Guid.NewGuid().ToString("N");
        var first = SingleInstance.TryAcquire(name, TimeSpan.Zero)!;
        SingleInstance? waited = null;
        var waiter = new Thread(() => waited = SingleInstance.TryAcquire(name, TimeSpan.FromSeconds(5)));
        waiter.Start();

        // A mutex must be released by its owning thread, so this thread disposes it after a delay.
        Thread.Sleep(300);
        first.Dispose();
        waiter.Join();

        Assert.NotNull(waited);
    }
}
