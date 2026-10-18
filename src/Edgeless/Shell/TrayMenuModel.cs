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
        => enabled ? $"Edgeless — On ({managedCount} windows)" : "Edgeless — Off";

    public static List<TrayMenuItem> Build(TrayMenuState state)
    {
        var addText = state.LastForegroundExe is null
            ? "Add focused window to list"
            : $"Add \"{state.LastForegroundExe}\" to list";
        var managed = state.Managed
            .Select(m => new TrayMenuItem(m.Label, TrayCommand.RestoreManaged, Hwnd: m.Hwnd))
            .ToList();

        var items = new List<TrayMenuItem>
        {
            new("Enable Edgeless", TrayCommand.ToggleEnabled, Checked: state.Enabled),
            new(addText, TrayCommand.AddLastForeground,
                Enabled: state.LastForegroundExe is not null && !state.LastForegroundListed),
            new("Managed windows", null, Enabled: managed.Count > 0) { Children = managed },
            TrayMenuItem.Separator,
            new("Settings…", TrayCommand.OpenSettings),
            new("Start with Windows", TrayCommand.ToggleAutostart, Checked: state.AutostartOn),
        };
        if (!state.IsElevated)
        {
            items.Add(new("Restart as administrator", TrayCommand.RestartElevated));
        }

        items.Add(new("Open log folder", TrayCommand.OpenLogFolder));
        items.Add(TrayMenuItem.Separator);
        items.Add(new("Exit", TrayCommand.Exit));
        return items;
    }
}
