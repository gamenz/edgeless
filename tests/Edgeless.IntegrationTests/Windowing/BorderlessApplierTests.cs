using Edgeless.Core.Config;
using Edgeless.Core.Logging;
using Edgeless.Core.Styles;
using Edgeless.IntegrationTests.Support;
using Edgeless.Interop;
using Edgeless.Windowing;

namespace Edgeless.IntegrationTests.Windowing;

[Trait("Category", "Integration")]
public sealed class BorderlessApplierTests
{
    private readonly WindowRegistry _registry = new();
    private readonly WindowFinder _finder = new(new ProcessNameCache(), (uint)Environment.ProcessId);
    private readonly BorderlessApplier _applier;

    public BorderlessApplierTests() => _applier = new BorderlessApplier(_registry, NullLog.Instance);

    private WindowInfo Describe(TestWindowProcess window)
    {
        Assert.True(_finder.TryDescribe(window.Hwnd, out var info));
        return info;
    }

    [Fact]
    public void Finder_DescribesAndFindsMainWindow()
    {
        using var window = TestWindowProcess.Start();

        var info = Describe(window);

        Assert.Equal(TestWindowProcess.ExeName, info.ExeName);
        Assert.Equal(window.Pid, info.Pid);
        Assert.False(_finder.IsExcluded(info));
        Assert.Equal(window.Hwnd, _finder.FindMainWindow(window.Pid));
        Assert.Contains(_finder.FindProfileWindows([GameProfile.CreateDefault("Edgeless.TestWindow")]),
            w => w.Hwnd == window.Hwnd);
    }

    [Fact]
    public void Apply_RemovesFrame_FillsMonitor()
    {
        using var window = TestWindowProcess.Start();

        var result = _applier.Apply(Describe(window), new GameProfile());

        Assert.Equal(ApplyResult.Applied, result);
        Assert.True(StyleCalculator.IsBorderless(ForeignWindow.GetStyles(window.Hwnd), addPopupStyle: false));
        Assert.Equal(MonitorApi.FromWindow(window.Hwnd).Bounds, ForeignWindow.GetWindowRect(window.Hwnd));
        Assert.True(_registry.TryGet(window.Hwnd, out _));
    }

    [Fact]
    public void Apply_Twice_SecondIsAlreadyBorderless()
    {
        using var window = TestWindowProcess.Start();
        var info = Describe(window);

        _applier.Apply(info, new GameProfile());

        Assert.Equal(ApplyResult.AlreadyBorderless, _applier.Apply(info, new GameProfile()));
    }

    [Fact]
    public void Apply_AddPopupStyle_SetsPopup()
    {
        using var window = TestWindowProcess.Start();

        _applier.Apply(Describe(window), new GameProfile { AddPopupStyle = true });

        Assert.NotEqual(0u, ForeignWindow.GetStyles(window.Hwnd).Style & StyleFlags.WS_POPUP);
    }

    [Fact]
    public void Restore_ReturnsOriginalStylesAndRect()
    {
        using var window = TestWindowProcess.Start();
        var originalStyles = ForeignWindow.GetStyles(window.Hwnd);
        var originalRect = ForeignWindow.GetWindowRect(window.Hwnd);
        _applier.Apply(Describe(window), new GameProfile());

        Assert.True(_applier.Restore(window.Hwnd));

        Assert.Equal(originalStyles, ForeignWindow.GetStyles(window.Hwnd));
        Assert.Equal(originalRect, ForeignWindow.GetWindowRect(window.Hwnd));
        Assert.Empty(_registry.All);
    }

    [Fact]
    public void Restore_OriginallyMaximized_IsMaximizedAgain()
    {
        using var window = TestWindowProcess.Start(maximized: true);
        _applier.Apply(Describe(window), new GameProfile());
        Assert.False(ForeignWindow.IsMaximized(window.Hwnd));

        _applier.Restore(window.Hwnd);

        Assert.True(ForeignWindow.IsMaximized(window.Hwnd));
    }

    [Fact]
    public void Apply_Minimized_IsSkipped()
    {
        using var window = TestWindowProcess.Start();
        TestUser32.ShowWindow(window.Hwnd, TestUser32.SW_MINIMIZE);
        Assert.True(TestUser32.WaitUntil(() => ForeignWindow.IsMinimized(window.Hwnd), TimeSpan.FromSeconds(2)));
        var styles = ForeignWindow.GetStyles(window.Hwnd); // includes WS_MINIMIZE now

        var result = _applier.Apply(Describe(window), new GameProfile());

        Assert.Equal(ApplyResult.SkippedMinimized, result);
        Assert.Equal(styles, ForeignWindow.GetStyles(window.Hwnd));
        Assert.Empty(_registry.All);
    }

    [Fact]
    public void Restore_AfterProcessExit_DoesNothing()
    {
        using var window = TestWindowProcess.Start();
        _applier.Apply(Describe(window), new GameProfile());
        window.Kill();

        Assert.False(_applier.Restore(window.Hwnd));
        Assert.Empty(_registry.All);
    }

    [Fact]
    public void Center_KeepsClientSize()
    {
        using var window = TestWindowProcess.Start();
        var client = ForeignWindow.GetClientSize(window.Hwnd);
        var monitor = MonitorApi.FromWindow(window.Hwnd).Bounds;

        _applier.Apply(Describe(window), new GameProfile { SizeMode = SizeMode.Center });

        var rect = ForeignWindow.GetWindowRect(window.Hwnd);
        Assert.Equal(client, (rect.Width, rect.Height));
        Assert.Equal(monitor.Left + (monitor.Width - client.Item1) / 2, rect.Left);
        Assert.Equal(monitor.Top + (monitor.Height - client.Item2) / 2, rect.Top);
    }

    [Fact]
    public void RestoreAll_RestoresEveryManagedWindow()
    {
        using var first = TestWindowProcess.Start();
        using var second = TestWindowProcess.Start();
        _applier.Apply(Describe(first), new GameProfile());
        _applier.Apply(Describe(second), new GameProfile());

        _applier.RestoreAll();

        Assert.Empty(_registry.All);
        Assert.False(StyleCalculator.IsBorderless(ForeignWindow.GetStyles(first.Hwnd), false));
        Assert.False(StyleCalculator.IsBorderless(ForeignWindow.GetStyles(second.Hwnd), false));
    }
}
