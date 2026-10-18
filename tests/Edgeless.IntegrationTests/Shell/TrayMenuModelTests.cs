using Edgeless.Shell;

namespace Edgeless.IntegrationTests.Shell;

public sealed class TrayMenuModelTests
{
    private static TrayMenuState State(bool hasAddable = true, bool elevated = false)
        => new(Enabled: true, Addable: hasAddable ? [(3, "new.exe — New"), (4, "tool.exe — Tool")] : [],
            Managed: [(1, "game.exe — Game"), (2, "other.exe — Other")], AutostartOn: true, IsElevated: elevated);

    [Fact]
    public void Build_FollowsSpecOrderAndCopy()
    {
        var items = TrayMenuModel.Build(State());

        Assert.Equal(
            ["Enable Edgeless", "Add to list", "Managed windows", "Settings…", "Start with Windows",
             "Restart as administrator", "Open log folder", "Exit"],
            items.Where(i => !i.IsSeparator).Select(i => i.Text));
        Assert.True(items.Single(i => i.Command == TrayCommand.ToggleEnabled).Checked);
        Assert.True(items.Single(i => i.Command == TrayCommand.ToggleAutostart).Checked);
    }

    [Fact]
    public void Build_ListsManagedWindowsInSubmenu()
    {
        var submenu = TrayMenuModel.Build(State()).Single(i => i.Text == "Managed windows");

        Assert.Equal(["game.exe — Game", "other.exe — Other"], submenu.Children.Select(c => c.Text));
        Assert.Equal([(nint)1, (nint)2], submenu.Children.Select(c => c.Hwnd));
        Assert.All(submenu.Children, c => Assert.Equal(TrayCommand.RestoreManaged, c.Command));
    }

    [Fact]
    public void Build_ListsAddableWindowsInSubmenu()
    {
        var submenu = TrayMenuModel.Build(State()).Single(i => i.Text == "Add to list");

        Assert.True(submenu.Enabled);
        Assert.Equal(["new.exe — New", "tool.exe — Tool"], submenu.Children.Select(c => c.Text));
        Assert.Equal([(nint)3, (nint)4], submenu.Children.Select(c => c.Hwnd));
        Assert.All(submenu.Children, c => Assert.Equal(TrayCommand.AddWindow, c.Command));
    }

    [Fact]
    public void Build_DisablesAddWhenNothingToAdd()
        => Assert.False(TrayMenuModel.Build(State(hasAddable: false)).Single(i => i.Text == "Add to list").Enabled);

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
