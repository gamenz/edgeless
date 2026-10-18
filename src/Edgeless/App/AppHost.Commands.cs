using Edgeless.Interop;
using Edgeless.Platform;
using Edgeless.Shell;

namespace Edgeless.App;

/// <summary>Tray icon interaction and menu commands.</summary>
internal sealed partial class AppHost
{
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint WM_LBUTTONDBLCLK = 0x0203;

    private void OnTrayCallback(uint notification, int x, int y)
    {
        switch (notification)
        {
            case WM_CONTEXTMENU:
                _menu.Show(BuildMenuState(), x, y, OnCommand);
                break;
            case WM_LBUTTONDBLCLK:
                OpenSettings();
                break;
        }
    }

    private TrayMenuState BuildMenuState()
    {
        var last = _controller.LastForeground;
        var managed = _registry.All
            .Select(w => (w.Hwnd, Label: $"{w.ExeName} — {ForeignWindow.GetTitle(w.Hwnd)}"))
            .ToList();
        return new TrayMenuState(
            _controller.Enabled,
            last?.ExeName,
            last is not null && _controller.Config.Profiles.Any(p => p.Exe == last.ExeName),
            managed,
            AutostartOn: IsAutostartOn(),
            Elevation.IsElevated);
    }

    private void OnCommand(TrayCommand command, nint hwnd)
    {
        _log.Info($"Tray command: {command}");
        switch (command)
        {
            case TrayCommand.ToggleEnabled:
                _controller.SetEnabled(!_controller.Enabled);
                SaveConfig();
                break;
            case TrayCommand.AddLastForeground:
                if (_controller.AddLastForegroundToProfiles() is not null)
                {
                    SaveConfig();
                }

                break;
            case TrayCommand.RestoreManaged:
                _controller.RestoreWindow(hwnd);
                break;
            case TrayCommand.OpenSettings:
                OpenSettings();
                break;
            case TrayCommand.ToggleAutostart:
                ToggleAutostart();
                break;
            case TrayCommand.RestartElevated:
                RestartElevated();
                break;
            case TrayCommand.OpenLogFolder:
                Elevation.OpenWithShell(AppPaths.LogDirectory);
                break;
            case TrayCommand.Exit:
                MessageWindow.PostQuit();
                break;
        }

        UpdateTooltip();
    }

    private void SaveConfig()
    {
        try
        {
            _store.Save(_controller.Config);
        }
        catch (IOException ex)
        {
            _log.Error($"Saving config failed: {ex.Message}");
        }
    }

    private void RestartElevated()
    {
        if (Elevation.TryRestartElevated("--elevated-restart"))
        {
            // Dispose (on loop exit) restores every window; the new instance waits for our mutex.
            MessageWindow.PostQuit();
        }
    }

    private void OpenSettings()
    {
        // Replaced by the settings window in a later task; until then open the JSON file.
        Elevation.OpenWithShell(_store.FilePath);
    }

    private bool IsAutostartOn() => false;

    private void ToggleAutostart() => _log.Info("Autostart toggle not available yet");
}
