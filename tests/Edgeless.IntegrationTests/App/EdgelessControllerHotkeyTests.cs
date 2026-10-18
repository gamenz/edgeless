using Edgeless.Core.Config;
using Edgeless.IntegrationTests.Support;
using Edgeless.Interop;
using static Edgeless.IntegrationTests.App.EdgelessControllerTests;

namespace Edgeless.IntegrationTests.App;

[Trait("Category", "Integration")]
public sealed class EdgelessControllerHotkeyTests
{
    [Fact]
    public void Hotkey_TogglesForegroundWindow()
    {
        using var window = TestWindowProcess.Start();
        var originalStyles = ForeignWindow.GetStyles(window.Hwnd);
        var originalRect = ForeignWindow.GetWindowRect(window.Hwnd);
        using var host = new MessageLoopHost(new AppConfig());

        host.Invoke(() => host.Controller.OnHotkey(window.Hwnd));
        Assert.True(IsBorderless(window.Hwnd));

        host.Invoke(() => host.Controller.OnHotkey(window.Hwnd));
        Assert.Equal(originalStyles, ForeignWindow.GetStyles(window.Hwnd));
        Assert.Equal(originalRect, ForeignWindow.GetWindowRect(window.Hwnd));
    }

    [Fact]
    public void Hotkey_Off_BlocksAutoApplyUntilPressedAgain()
    {
        using var window = TestWindowProcess.Start();
        using var host = new MessageLoopHost(ConfigFor(TestWindowProcess.ExeName));
        Assert.True(IsBorderless(window.Hwnd));

        host.Invoke(() => host.Controller.OnHotkey(window.Hwnd));
        host.Invoke(() => host.Controller.OnForeground(window.Hwnd));
        Thread.Sleep(400);
        Assert.False(IsBorderless(window.Hwnd));

        host.Invoke(() => host.Controller.OnHotkey(window.Hwnd));
        Assert.True(IsBorderless(window.Hwnd));
    }

    [Fact]
    public void Hotkey_OnExcludedWindow_DoesNothing()
    {
        var taskbar = TestUser32.FindWindow("Shell_TrayWnd", null);
        Assert.NotEqual(0, taskbar);
        var styles = ForeignWindow.GetStyles(taskbar);
        using var host = new MessageLoopHost(new AppConfig());

        host.Invoke(() => host.Controller.OnHotkey(taskbar));

        Assert.Equal(0, host.Invoke(() => host.Registry.All.Count));
        Assert.Equal(styles, ForeignWindow.GetStyles(taskbar));
    }

    [Fact]
    public void AddLastForeground_AddsDefaultProfileAndApplies()
    {
        using var window = TestWindowProcess.Start();
        using var host = new MessageLoopHost(new AppConfig());
        host.Invoke(() => host.Controller.OnForeground(window.Hwnd));

        var added = host.Invoke(host.Controller.AddLastForegroundToProfiles);

        Assert.Equal(TestWindowProcess.ExeName, added?.Exe);
        Assert.True(IsBorderless(window.Hwnd));
        Assert.Null(host.Invoke(host.Controller.AddLastForegroundToProfiles));
    }
}
