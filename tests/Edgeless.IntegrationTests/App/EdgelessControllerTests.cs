using Edgeless.Core.Config;
using Edgeless.Core.Styles;
using Edgeless.IntegrationTests.Support;
using Edgeless.Interop;

namespace Edgeless.IntegrationTests.App;

[Trait("Category", "Integration")]
public sealed class EdgelessControllerTests
{
    internal static AppConfig ConfigFor(string path, int delayMs = 100)
    {
        var profile = GameProfile.CreateDefault(path);
        profile.ApplyDelayMs = delayMs;
        return new AppConfig { Profiles = [profile] };
    }

    internal static bool IsBorderless(nint hwnd) => StyleCalculator.IsBorderless(ForeignWindow.GetStyles(hwnd), false);

    [Fact]
    public void Foreground_ListedGame_AppliesAfterDelay()
    {
        using var window = TestWindowProcess.Start();
        // Not started: no start-up scan, so only the (simulated) foreground event can apply.
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath, delayMs: 300), start: false);

        host.Invoke(() => host.Controller.OnForeground(window.Hwnd));

        Assert.False(IsBorderless(window.Hwnd));
        Assert.True(TestUser32.WaitUntil(() => IsBorderless(window.Hwnd), TimeSpan.FromSeconds(2)));
        Assert.Equal(MonitorApi.FromWindow(window.Hwnd).Bounds, ForeignWindow.GetWindowRect(window.Hwnd));
    }

    [Fact]
    public void Start_ScansRunningGames()
    {
        using var window = TestWindowProcess.Start();
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));

        Assert.True(IsBorderless(window.Hwnd));
        Assert.Equal(1, host.Invoke(() => host.Registry.All.Count));
    }

    [Fact]
    public void Foreground_UnlistedWindow_NotTouched()
    {
        using var window = TestWindowProcess.Start();
        // Same file name, different folder: must not match.
        using var host = new MessageLoopHost(ConfigFor(@"C:\Games\Other\Edgeless.TestWindow.exe"));

        host.Invoke(() => host.Controller.OnForeground(window.Hwnd));
        Thread.Sleep(400);

        Assert.False(IsBorderless(window.Hwnd));
        Assert.Equal("edgeless.testwindow.exe", host.Invoke(() => host.Controller.LastForeground?.ExeName));
        Assert.Equal(TestWindowProcess.ExePath, host.Invoke(() => host.Controller.LastForeground?.ImagePath),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GameResetsStyle_IsReappliedWithin1Second()
    {
        using var window = TestWindowProcess.Start(mode: "reset-once", delayMs: 300);
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));
        Assert.True(IsBorderless(window.Hwnd));

        Assert.True(TestUser32.WaitUntil(() => host.Log.Count("Applied:") >= 2, TimeSpan.FromSeconds(1.5)));
        Assert.True(TestUser32.WaitUntil(() => IsBorderless(window.Hwnd), TimeSpan.FromSeconds(1)));
        Assert.Equal(MonitorApi.FromWindow(window.Hwnd).Bounds, ForeignWindow.GetWindowRect(window.Hwnd));
    }

    [Fact]
    public void GameFights_GuardTripsAndRestores()
    {
        using var window = TestWindowProcess.Start(mode: "fight", delayMs: 50);
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));

        Assert.True(TestUser32.WaitUntil(
            () => host.Notifier.Keys.Contains("guard:" + TestWindowProcess.ExePath, StringComparer.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(15)));
        Assert.Equal(0, host.Invoke(() => host.Registry.All.Count));
        var applies = host.Log.Count("Applied:");
        Thread.Sleep(2000);
        Assert.Equal(applies, host.Log.Count("Applied:"));
        Assert.False(IsBorderless(window.Hwnd));
    }

    [Fact]
    public void Minimized_AppliesOnRestore()
    {
        using var window = TestWindowProcess.Start();
        TestUser32.ShowWindow(window.Hwnd, TestUser32.SW_MINIMIZE);
        Assert.True(TestUser32.WaitUntil(() => ForeignWindow.IsMinimized(window.Hwnd), TimeSpan.FromSeconds(2)));

        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath)); // start-up scan meets a minimized game
        Thread.Sleep(400);
        Assert.False(IsBorderless(window.Hwnd));
        Assert.Equal(0, host.Invoke(() => host.Registry.All.Count));

        TestUser32.ShowWindow(window.Hwnd, TestUser32.SW_RESTORE);
        Assert.True(TestUser32.WaitUntil(() => IsBorderless(window.Hwnd), TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void SetEnabledFalse_RestoresAll()
    {
        using var window = TestWindowProcess.Start();
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));
        Assert.True(IsBorderless(window.Hwnd));

        host.Invoke(() => host.Controller.SetEnabled(false));

        Assert.False(IsBorderless(window.Hwnd));
        Assert.Equal(0, host.Invoke(() => host.Registry.All.Count));
        host.Invoke(() => host.Controller.OnHotkey(window.Hwnd));
        Assert.False(IsBorderless(window.Hwnd));
    }

    [Fact]
    public void Dispose_RestoresAll()
    {
        using var window = TestWindowProcess.Start();
        var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));
        Assert.True(IsBorderless(window.Hwnd));

        host.Dispose();

        Assert.False(IsBorderless(window.Hwnd));
    }

    [Fact]
    public void WindowDestroyed_CleansRegistry()
    {
        using var window = TestWindowProcess.Start();
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExePath));
        Assert.Equal(1, host.Invoke(() => host.Registry.All.Count));

        window.Kill();

        Assert.True(TestUser32.WaitUntil(() => host.Invoke(() => host.Registry.All.Count) == 0, TimeSpan.FromSeconds(2)));
    }
}
