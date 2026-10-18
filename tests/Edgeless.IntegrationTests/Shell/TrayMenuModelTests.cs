using Edgeless.Shell;

namespace Edgeless.IntegrationTests.Shell;

public sealed class TrayMenuModelTests
{
    private static TrayMenuState State(string? lastExe = "game.exe", bool listed = false, bool elevated = false)
        => new(Enabled: true, LastForegroundExe: lastExe, LastForegroundListed: listed,
            Managed: [(1, "game.exe — Game"), (2, "other.exe — Other")], AutostartOn: true, IsElevated: elevated);

    [Fact]
    public void Build_FollowsSpecOrderAndCopy()
    {
        var items = TrayMenuModel.Build(State());

        Assert.Equal(
            ["Enable Edgeless", "Add \"game.exe\" to list", "Managed windows", "Settings…", "Start with Windows",
             "Restart as administrator", "Open log folder", "Exit"],
            items.Where(i => !i.IsSeparator).Select(i => i.Text));
        Assert.True(items.Single(i => i.Command == TrayCommand.ToggleEnabled).Checked);
        Assert.True(items.Single(i => i.Command == TrayCommand.ToggleAutostart).Checked);
    }

    [Fact]
    public void Build_ListsManagedWindowsInSubmenu()
    {
        var submenu = TrayMenuModel.Build(State()).Single(i => i.Children.Count > 0);

        Assert.Equal(["game.exe — Game", "other.exe — Other"], submenu.Children.Select(c => c.Text));
        Assert.Equal([(nint)1, (nint)2], submenu.Children.Select(c => c.Hwnd));
        Assert.All(submenu.Children, c => Assert.Equal(TrayCommand.RestoreManaged, c.Command));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("game.exe", true)]
    public void Build_DisablesAddWhenNoneOrAlreadyListed(string? lastExe, bool listed)
    {
        var add = TrayMenuModel.Build(State(lastExe, listed)).Single(i => i.Command == TrayCommand.AddLastForeground);

        Assert.False(add.Enabled);
    }

    [Fact]
    public void Build_HidesRestartElevatedWhenElevated()
        => Assert.DoesNotContain(TrayMenuModel.Build(State(elevated: true)), i => i.Command == TrayCommand.RestartElevated);

    [Fact]
    public void Tooltip_ShowsStateAndCount()
    {
        Assert.Equal("Edgeless — On (2 windows)", TrayMenuModel.Tooltip(true, 2));
        Assert.Equal("Edgeless — Off", TrayMenuModel.Tooltip(false, 2));
    }
}
