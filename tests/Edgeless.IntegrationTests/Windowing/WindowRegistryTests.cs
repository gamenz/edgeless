using Edgeless.Core.Config;
using Edgeless.Windowing;

namespace Edgeless.IntegrationTests.Windowing;

public sealed class WindowRegistryTests
{
    private static ManagedWindow Window(nint hwnd, uint pid) => new()
    {
        Hwnd = hwnd, Pid = pid, ExeName = "game.exe", OriginalStyles = default, OriginalPlacement = default,
        Profile = new GameProfile(),
    };

    [Fact]
    public void Changed_RaisedOnAddRemoveAndForgetPid()
    {
        var registry = new WindowRegistry();
        var changes = 0;
        registry.Changed += () => changes++;

        registry.Add(Window(1, 10));
        registry.Add(Window(2, 10));
        registry.Remove(1);
        registry.Remove(1); // not managed any more: no event
        registry.ForgetPid(10);

        Assert.Equal(4, changes);
        Assert.Empty(registry.All);
    }

    [Fact]
    public void UserDisabled_IsKeyedByHwndAndPid()
    {
        var registry = new WindowRegistry();

        registry.MarkUserDisabled(1, 10);

        Assert.True(registry.IsUserDisabled(1, 10));
        Assert.False(registry.IsUserDisabled(1, 11)); // reused hwnd in another process
        registry.ForgetPid(10);
        Assert.False(registry.IsUserDisabled(1, 10));
    }
}
