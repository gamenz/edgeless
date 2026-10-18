using Edgeless.Core.Config;
using Edgeless.Interop;
using Edgeless.Platform;
using Edgeless.Shell;
using Edgeless.Shell.Settings;

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
        => SettingsWindow.ShowOrActivate(_controller.Config, SaveFromSettings, () => Elevation.OpenWithShell(_store.FilePath));

    /// <summary>Applies a config saved in the settings window. Keeps the old hotkey if the new one is taken.</summary>
    private SettingsSaveResult SaveFromSettings(AppConfig newConfig)
    {
        if (!RegisterHotkey(newConfig.Hotkey))
        {
            RegisterHotkey(_controller.Config.Hotkey);
            return new SettingsSaveResult(HotkeyRegistered: false);
        }

        var autostartChanged = newConfig.Autostart != _controller.Config.Autostart;
        newConfig.Enabled = _controller.Enabled;
        _controller.ApplyConfig(newConfig);
        if (autostartChanged)
        {
            SetAutostart(newConfig.Autostart); // also saves
        }
        else
        {
            SaveConfig();
        }

        _log.Info("Settings saved");
        UpdateTooltip();
        return new SettingsSaveResult(HotkeyRegistered: true);
    }

    private bool IsAutostartOn() => _controller.Config.Autostart != AutostartMode.Off;

    /// <summary>Tray toggle: Off ↔ Normal. The elevated mode is chosen in settings.</summary>
    private void ToggleAutostart() => SetAutostart(IsAutostartOn() ? AutostartMode.Off : AutostartMode.Normal);

    private void SetAutostart(AutostartMode mode)
    {
        if (!_autostart.Apply(mode, AppPaths.ExePath))
        {
            _log.Warn($"Autostart change to {mode} failed or was cancelled");
        }

        _controller.Config.Autostart = _autostart.GetCurrent();
        SaveConfig();
    }
}
