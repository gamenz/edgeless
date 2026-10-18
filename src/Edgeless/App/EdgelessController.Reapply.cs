using Edgeless.Core.Config;
using Edgeless.Events;
using Edgeless.Windowing;

namespace Edgeless.App;

/// <summary>Foreground and window events: delayed apply, debounced re-apply, guard, minimized windows.</summary>
internal sealed partial class EdgelessController
{
    private const string Title = "Edgeless";
    private static readonly TimeSpan ReapplyDebounce = TimeSpan.FromMilliseconds(250);

    /// <summary>Windows that were minimized when an apply was due; applied on MINIMIZEEND.</summary>
    private readonly Dictionary<nint, (WindowInfo Window, GameProfile Profile)> _pending = [];

    /// <summary>Handles EVENT_SYSTEM_FOREGROUND (also called directly by tests).</summary>
    public void OnForeground(nint hwnd)
    {
        if (!finder.TryDescribe(hwnd, out var info) || finder.IsExcluded(info))
        {
            return;
        }

        LastForeground = info;
        var profile = Enabled ? FindProfile(info.ImagePath) : null;
        if (profile is null || registry.IsUserDisabled(hwnd, info.Pid))
        {
            return;
        }

        timers.Schedule(ApplyKey(hwnd), TimeSpan.FromMilliseconds(profile.ApplyDelayMs), () =>
        {
            if (Enabled && !registry.IsUserDisabled(hwnd, info.Pid))
            {
                ApplyNow(MainWindowOf(info), profile, isReapply: false);
            }
        });
    }

    private void OnWindowEvent(nint hwnd, WinEventKind kind)
    {
        if (kind == WinEventKind.Destroyed)
        {
            OnDestroyed(hwnd);
            return;
        }

        if (_pending.TryGetValue(hwnd, out var pending) && kind == WinEventKind.MinimizeEnd)
        {
            timers.Schedule(ApplyKey(hwnd), ReapplyDebounce, () =>
            {
                _pending.Remove(hwnd);
                ApplyNow(pending.Window, pending.Profile, isReapply: false);
            });
            return;
        }

        if (registry.TryGet(hwnd, out _))
        {
            timers.Schedule(ReapplyKey(hwnd), ReapplyDebounce, () => Reapply(hwnd));
        }
    }

    private void Reapply(nint hwnd)
    {
        if (Enabled && registry.TryGet(hwnd, out var window))
        {
            ApplyNow(new WindowInfo(hwnd, window.Pid, window.ImagePath, ""), window.Profile, isReapply: true);
        }
    }

    private void OnDestroyed(nint hwnd)
    {
        uint pid;
        if (registry.TryGet(hwnd, out var window))
        {
            pid = window.Pid;
            registry.Remove(hwnd);
            log.Info($"Window destroyed: {window.ExeName} pid={pid} hwnd=0x{hwnd:X}");
        }
        else if (_pending.Remove(hwnd, out var pending))
        {
            pid = pending.Window.Pid;
        }
        else
        {
            return;
        }

        timers.Cancel(ApplyKey(hwnd));
        timers.Cancel(ReapplyKey(hwnd));
        guard.Reset(hwnd);
        registry.ClearUserDisabled(hwnd);
        if (!registry.HasPid(pid) && !_pending.Values.Any(p => p.Window.Pid == pid))
        {
            watcher.UnwatchProcess(pid);
            finder.ForgetProcess(pid);
        }
    }

    private void ApplyNow(WindowInfo window, GameProfile profile, bool isReapply)
    {
        switch (applier.Apply(window, profile))
        {
            case ApplyResult.Applied:
                watcher.WatchProcess(window.Pid);
                if (isReapply && !guard.TryRecord(window.Hwnd))
                {
                    TripGuard(window);
                }

                break;
            case ApplyResult.SkippedMinimized:
                _pending[window.Hwnd] = (window, profile);
                watcher.WatchProcess(window.Pid);
                break;
            case ApplyResult.AccessDenied:
                notifier.Notify("uipi:" + window.ImagePath, Title,
                    $"{window.ExeName} runs as administrator. Run Edgeless as administrator "
                    + "(tray → Restart as administrator).");
                break;
        }
    }

    private void TripGuard(WindowInfo window)
    {
        log.Warn($"Guard tripped: {window.ExeName} pid={window.Pid} hwnd=0x{window.Hwnd:X} keeps resetting its window");
        RestoreManaged(window.Hwnd, markUserDisabled: true);
        notifier.Notify("guard:" + window.ImagePath, Title,
            $"{window.ExeName} keeps resetting its window; Edgeless stopped managing it.");
    }
}
