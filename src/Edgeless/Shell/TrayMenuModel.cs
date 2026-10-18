namespace Edgeless.Shell;

internal enum TrayCommand
{
    ToggleEnabled,
    AddLastForeground,
    RestoreManaged,
    OpenSettings,
    ToggleAutostart,
    RestartElevated,
    OpenLogFolder,
    Exit,
}

internal sealed record TrayMenuState(
    bool Enabled,
    string? LastForegroundExe,
    bool LastForegroundListed,
    IReadOnlyList<(nint Hwnd, string Label)> Managed,
    bool AutostartOn,
    bool IsElevated);

internal sealed record TrayMenuItem(
    string Text, TrayCommand? Command, bool Enabled = true, bool Checked = false, nint Hwnd = 0)
{
    public static readonly TrayMenuItem Separator = new("", null);

    public IReadOnlyList<TrayMenuItem> Children { get; init; } = [];

    public bool IsSeparator => Command is null && Children.Count == 0 && Text.Length == 0;
}

/// <summary>What the tray menu shows (spec section 4.7). Pure, so the copy and rules are testable.</summary>
internal static class TrayMenuModel
{
    public static string Tooltip(bool enabled, int managedCount)
        => enabled ? $"Edgeless — Đang bật ({managedCount} cửa sổ)" : "Edgeless — Đang tắt";

    public static List<TrayMenuItem> Build(TrayMenuState state)
    {
        var addText = state.LastForegroundExe is null
            ? "Thêm cửa sổ đang focus vào danh sách"
            : $"Thêm \"{state.LastForegroundExe}\" vào danh sách";
        var managed = state.Managed
            .Select(m => new TrayMenuItem(m.Label, TrayCommand.RestoreManaged, Hwnd: m.Hwnd))
            .ToList();

        var items = new List<TrayMenuItem>
        {
            new("Bật Edgeless", TrayCommand.ToggleEnabled, Checked: state.Enabled),
            new(addText, TrayCommand.AddLastForeground,
                Enabled: state.LastForegroundExe is not null && !state.LastForegroundListed),
            new("Đang áp dụng", null, Enabled: managed.Count > 0) { Children = managed },
            TrayMenuItem.Separator,
            new("Cài đặt…", TrayCommand.OpenSettings),
            new("Khởi động cùng Windows", TrayCommand.ToggleAutostart, Checked: state.AutostartOn),
        };
        if (!state.IsElevated)
        {
            items.Add(new("Khởi động lại với quyền admin", TrayCommand.RestartElevated));
        }

        items.Add(new("Mở thư mục log", TrayCommand.OpenLogFolder));
        items.Add(TrayMenuItem.Separator);
        items.Add(new("Thoát", TrayCommand.Exit));
        return items;
    }
}
