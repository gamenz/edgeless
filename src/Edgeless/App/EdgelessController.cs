using Edgeless.Core.Config;
using Edgeless.Core.Logging;
using Edgeless.Core.Rules;
using Edgeless.Events;
using Edgeless.Interop;
using Edgeless.Shell;
using Edgeless.Windowing;

namespace Edgeless.App;

/// <summary>
/// The app's brain (spec sections 4.4-4.6, 4.9). Runs on the UI thread only.
/// This file: lifecycle, enable/disable, hotkey, config and tray actions. Event handling is in the .Reapply part.
/// </summary>
internal sealed partial class EdgelessController(
    AppConfig config,
    WindowFinder finder,
    BorderlessApplier applier,
    WindowRegistry registry,
    WinEventWatcher watcher,
    ITimerScheduler timers,
    INotifier notifier,
    ReapplyGuard guard,
    ILog log) : IDisposable
{
    private AppConfig _config = config;

    public bool Enabled => _config.Enabled;

    /// <summary>Last foreground window that is not Edgeless or the shell (for "Add to list").</summary>
    public WindowInfo? LastForeground { get; private set; }

    public IReadOnlyCollection<ManagedWindow> Managed => registry.All;

    public AppConfig Config => _config;

    public void Start()
    {
        watcher.ForegroundChanged += OnForeground;
        watcher.WindowEvent += OnWindowEvent;
        watcher.StartForeground();
        if (Enabled)
        {
            ScanRunning();
        }
    }

    public void SetEnabled(bool enabled)
    {
        _config.Enabled = enabled;
        if (enabled)
        {
            ScanRunning();
            return;
        }

        foreach (var window in registry.All.ToList())
        {
            RestoreManaged(window.Hwnd, markUserDisabled: false);
        }

        foreach (var pid in _pending.Values.Select(p => p.Window.Pid).Distinct().ToList())
        {
            watcher.UnwatchProcess(pid);
        }

        _pending.Clear();
    }

    public void OnHotkey() => OnHotkey(ForeignWindow.GetForeground());

    /// <summary>Toggles borderless on <paramref name="hwnd"/> (the foreground window when called from the hotkey).</summary>
    public void OnHotkey(nint hwnd)
    {
        if (!Enabled || !finder.TryDescribe(hwnd, out var info) || finder.IsExcluded(info))
        {
            return;
        }

        if (registry.TryGet(hwnd, out _))
        {
            RestoreManaged(hwnd, markUserDisabled: true);
            return;
        }

        registry.ClearUserDisabled(hwnd);
        guard.Reset(hwnd);
        ApplyNow(info, FindProfile(info.ImagePath) ?? new GameProfile(), isReapply: false);
    }

    /// <summary>Adds the last foreground window's exe with a default profile and applies it. Null if none or listed.</summary>
    public GameProfile? AddLastForegroundToProfiles()
    {
        var last = LastForeground;
        if (last is null || last.ImagePath.Length == 0 || _config.Profiles.Any(p => ExePath.AreSame(p.Path, last.ImagePath)))
        {
            return null;
        }

        var profile = GameProfile.CreateDefault(last.ImagePath);
        _config.Profiles.Add(profile);
        log.Info($"Added profile: {profile.Path}");
        if (Enabled && ForeignWindow.IsWindow(last.Hwnd))
        {
            ApplyNow(MainWindowOf(last), profile, isReapply: false);
        }

        return profile;
    }

    /// <summary>Restores one managed window (tray submenu) and keeps automation off it, like the hotkey.</summary>
    public void RestoreWindow(nint hwnd) => RestoreManaged(hwnd, markUserDisabled: true);

    public void ApplyConfig(AppConfig newConfig)
    {
        var wasEnabled = Enabled;
        _config = newConfig;
        if (wasEnabled != newConfig.Enabled)
        {
            newConfig.Enabled = wasEnabled;
            SetEnabled(!wasEnabled);
            return;
        }

        foreach (var window in registry.All.ToList())
        {
            var profile = FindProfile(window.ImagePath);
            if (profile is null)
            {
                RestoreManaged(window.Hwnd, markUserDisabled: false);
                continue;
            }

            ApplyNow(new WindowInfo(window.Hwnd, window.Pid, window.ImagePath, ""), profile, isReapply: false);
        }

        if (Enabled)
        {
            ScanRunning();
        }
    }

    public void Dispose()
    {
        watcher.ForegroundChanged -= OnForeground;
        watcher.WindowEvent -= OnWindowEvent;
        applier.RestoreAll();
        watcher.Dispose();
    }

    private GameProfile? FindProfile(string imagePath)
        => _config.Profiles.FirstOrDefault(p => p.Enabled && ExePath.AreSame(p.Path, imagePath));

    private void ScanRunning()
    {
        foreach (var window in finder.FindProfileWindows(_config.Profiles.Where(p => p.Enabled)))
        {
            var profile = FindProfile(window.ImagePath);
            if (profile is not null && !registry.IsUserDisabled(window.Hwnd, window.Pid))
            {
                ApplyNow(window, profile, isReapply: false);
            }
        }
    }

    private void RestoreManaged(nint hwnd, bool markUserDisabled)
    {
        if (!registry.TryGet(hwnd, out var window))
        {
            return;
        }

        timers.Cancel(ApplyKey(hwnd));
        timers.Cancel(ReapplyKey(hwnd));
        applier.Restore(hwnd);
        guard.Reset(hwnd);
        if (markUserDisabled)
        {
            registry.MarkUserDisabled(hwnd, window.Pid);
        }

        UnwatchIfUnused(window.Pid);
    }

    private void UnwatchIfUnused(uint pid)
    {
        if (!registry.HasPid(pid) && !_pending.Values.Any(p => p.Window.Pid == pid))
        {
            watcher.UnwatchProcess(pid);
        }
    }

    private WindowInfo MainWindowOf(WindowInfo window)
    {
        var main = finder.FindMainWindow(window.Pid);
        return main != 0 && main != window.Hwnd && finder.TryDescribe(main, out var info) ? info : window;
    }

    private static string ApplyKey(nint hwnd) => $"apply:{hwnd}";

    private static string ReapplyKey(nint hwnd) => $"reapply:{hwnd}";
}
