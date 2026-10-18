using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Edgeless.Interop;

namespace Edgeless.Events;

internal enum WinEventKind
{
    LocationChanged,
    MinimizeEnd,
    Destroyed,
}

/// <summary>
/// Out-of-context WinEvent hooks: one global foreground hook plus three narrow hooks per watched game process.
/// Callbacks arrive on the thread that installed the hooks, which must run a message loop.
/// </summary>
internal sealed unsafe class WinEventWatcher : IDisposable
{
    [ThreadStatic]
    private static WinEventWatcher? _current;

    private readonly Dictionary<uint, nint[]> _processHooks = [];
    private nint _foregroundHook;

    public WinEventWatcher() => _current = this;

    public event Action<nint>? ForegroundChanged;

    public event Action<nint, WinEventKind>? WindowEvent;

    public void StartForeground()
    {
        if (_foregroundHook == 0)
        {
            _foregroundHook = WinEventApi.Hook(
                WinEventApi.EVENT_SYSTEM_FOREGROUND, WinEventApi.EVENT_SYSTEM_FOREGROUND, 0, &OnEvent);
        }
    }

    public void WatchProcess(uint pid)
    {
        if (_processHooks.ContainsKey(pid))
        {
            return;
        }

        _processHooks[pid] =
        [
            WinEventApi.Hook(WinEventApi.EVENT_SYSTEM_MINIMIZEEND, WinEventApi.EVENT_SYSTEM_MINIMIZEEND, pid, &OnEvent),
            WinEventApi.Hook(WinEventApi.EVENT_OBJECT_DESTROY, WinEventApi.EVENT_OBJECT_DESTROY, pid, &OnEvent),
            WinEventApi.Hook(WinEventApi.EVENT_OBJECT_LOCATIONCHANGE, WinEventApi.EVENT_OBJECT_LOCATIONCHANGE, pid, &OnEvent),
        ];
    }

    public void UnwatchProcess(uint pid)
    {
        if (_processHooks.Remove(pid, out var hooks))
        {
            foreach (var hook in hooks)
            {
                WinEventApi.Unhook(hook);
            }
        }
    }

    public void Dispose()
    {
        foreach (var pid in _processHooks.Keys.ToList())
        {
            UnwatchProcess(pid);
        }

        WinEventApi.Unhook(_foregroundHook);
        _foregroundHook = 0;
        if (_current == this)
        {
            _current = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var watcher = _current;
        if (watcher is null || hwnd == 0 || idObject != WinEventApi.OBJID_WINDOW || idChild != WinEventApi.CHILDID_SELF)
        {
            return;
        }

        try
        {
            if (eventType == WinEventApi.EVENT_SYSTEM_FOREGROUND)
            {
                watcher.ForegroundChanged?.Invoke(hwnd);
                return;
            }

            WinEventKind? kind = eventType switch
            {
                WinEventApi.EVENT_OBJECT_LOCATIONCHANGE => WinEventKind.LocationChanged,
                WinEventApi.EVENT_SYSTEM_MINIMIZEEND => WinEventKind.MinimizeEnd,
                WinEventApi.EVENT_OBJECT_DESTROY => WinEventKind.Destroyed,
                _ => null,
            };
            if (kind is { } k)
            {
                watcher.WindowEvent?.Invoke(hwnd, k);
            }
        }
        catch (Exception ex)
        {
            HandlerFailed?.Invoke(ex);
        }
    }

    /// <summary>Raised for exceptions thrown by handlers (they must never escape into native code).</summary>
    public static event Action<Exception>? HandlerFailed;
}
