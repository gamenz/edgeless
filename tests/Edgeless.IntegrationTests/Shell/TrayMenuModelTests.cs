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
            ["Bật Edgeless", "Thêm \"game.exe\" vào danh sách", "Đang áp dụng", "Cài đặt…", "Khởi động cùng Windows",
             "Khởi động lại với quyền admin", "Mở thư mục log", "Thoát"],
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
        Assert.Equal("Edgeless — Đang bật (2 cửa sổ)", TrayMenuModel.Tooltip(true, 2));
        Assert.Equal("Edgeless — Đang tắt", TrayMenuModel.Tooltip(false, 2));
    }
}
