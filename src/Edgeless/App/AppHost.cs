using Edgeless.Core.Config;
using Edgeless.Core.Logging;
using Edgeless.Core.Rules;
using Edgeless.Events;
using Edgeless.Platform;
using Edgeless.Shell;
using Edgeless.Windowing;

namespace Edgeless.App;

/// <summary>Composition root: builds every component, wires UI-thread events and runs the message loop.</summary>
internal sealed partial class AppHost : IDisposable
{
    private readonly FileLogger _log;
    private readonly ConfigStore _store;
    private readonly MessageWindow _window;
    private readonly TrayIcon _tray;
    private readonly TrayMenu _menu;
    private readonly Notifier _notifier;
    private readonly WindowRegistry _registry = new();
    private readonly BorderlessApplier _applier;
    private readonly HotkeyManager _hotkey;
    private readonly EdgelessController _controller;
    private readonly Autostart _autostart = new();
    private bool _disposed;

    private AppHost()
    {
        _log = new FileLogger(AppPaths.LogDirectory, TimeProvider.System);
        _log.Info($"Edgeless starting (elevated={Elevation.IsElevated}, exe={AppPaths.ExePath})");
        MessageWindow.HandlerFailed += ex => _log.Error($"UI handler failed: {ex}");
        WinEventWatcher.HandlerFailed += ex => _log.Error($"WinEvent handler failed: {ex}");

        _store = new ConfigStore(AppPaths.ConfigDirectory, TimeProvider.System);
        var load = _store.Load();

        _window = new MessageWindow();
        _tray = new TrayIcon(_window.Handle);
        _menu = new TrayMenu(_window.Handle);
        _notifier = new Notifier(_tray, new NotificationThrottle(TimeProvider.System), _log);
        _applier = new BorderlessApplier(_registry, _log);
        _hotkey = new HotkeyManager(_window.Handle);
        var finder = new WindowFinder(new ProcessPathCache(), (uint)Environment.ProcessId);
        var config = PathCanonicalizer.CanonicalizeProfiles(load.Config);
        _controller = new EdgelessController(config, finder, _applier, _registry, new WinEventWatcher(), _window,
            _notifier, new ReapplyGuard(TimeProvider.System), _log);

        _tray.Add();
        if (load.InvalidBackupPath is not null)
        {
            _notifier.Notify("config-invalid", "Edgeless",
                $"config.json bị lỗi, đã sao lưu thành {Path.GetFileName(load.InvalidBackupPath)} và dùng cấu hình mặc định.");
        }
    }

    public static int Run(CommandLineOptions options)
    {
        using var host = new AppHost();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => host.OnUnhandledException(e.ExceptionObject);
        host.Start();
        MessageWindow.RunMessageLoop();
        host._log.Info("Edgeless exiting");
        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controller.Dispose();
        _hotkey.Dispose();
        _tray.Dispose();
        _window.Dispose();
        _log.Dispose();
    }

    private void Start()
    {
        _window.HotkeyPressed += _controller.OnHotkey;
        _window.TrayCallback += OnTrayCallback;
        _window.TaskbarCreated += _tray.Readd;
        _window.ShowSettingsRequested += OpenSettings;
        _window.SessionEnding += OnSessionEnding;
        _registry.Changed += UpdateTooltip;

        _autostart.RepairPath(AppPaths.ExePath);
        _controller.Config.Autostart = _autostart.GetCurrent();
        RegisterHotkey(_controller.Config.Hotkey);
        _controller.Start();
        UpdateTooltip();
    }

    private bool RegisterHotkey(HotkeyConfig hotkey)
    {
        var ok = HotkeyBinding.TryParse(hotkey, out var modifiers, out var virtualKey)
            && _hotkey.Register(modifiers, virtualKey);
        if (!ok)
        {
            var name = string.Join("+", hotkey.Modifiers.Append(hotkey.Key));
            _log.Warn($"Hotkey {name} could not be registered");
            _notifier.Notify("hotkey", "Edgeless", $"Không đăng ký được hotkey {name} (có thể ứng dụng khác đang dùng).");
        }

        return ok;
    }

    private void UpdateTooltip() => _tray.SetTooltip(TrayMenuModel.Tooltip(_controller.Enabled, _registry.All.Count));

    private void OnSessionEnding()
    {
        _log.Info("Session ending: restoring all windows");
        _applier.RestoreAll();
    }

    private void OnUnhandledException(object exception)
    {
        _log.Error($"Unhandled exception: {exception}");
        try
        {
            _applier.RestoreAll();
        }
        catch (Exception ex)
        {
            _log.Error($"Restore after crash failed: {ex.Message}");
        }
    }
}
