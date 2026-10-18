using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Shell;

/// <summary>
/// Hidden top-level window owning the UI thread's messages: hotkey, one-shot timers, tray callbacks, broadcasts.
/// Not message-only, so it receives TaskbarCreated and session-end broadcasts.
/// </summary>
internal sealed unsafe class MessageWindow : ITimerScheduler, IDisposable
{
    public const string ClassName = "Edgeless.MessageWindow";
    public const uint WM_TRAY = WM_APP + 1;
    public const string ShowSettingsMessage = "Edgeless.ShowSettings";

    private const uint WM_APP = 0x8000;
    private const uint WM_POST = WM_APP + 2;
    private const uint WM_TIMER = 0x0113;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUERYENDSESSION = 0x0011;
    private const uint WM_ENDSESSION = 0x0016;

    private static readonly ConcurrentDictionary<nint, MessageWindow> Instances = new();
    private static readonly Lock ClassLock = new();
    private static readonly HashSet<nint> Dialogs = [];
    private static bool _classRegistered;

    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly Dictionary<nuint, (string Key, Action Callback)> _timers = [];
    private readonly Dictionary<string, nuint> _timerIds = new(StringComparer.Ordinal);
    private readonly uint _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private readonly uint _showSettings = PInvoke.RegisterWindowMessage(ShowSettingsMessage);
    private nuint _nextTimerId = 1;

    public MessageWindow()
    {
        RegisterClassOnce();
        fixed (char* className = ClassName)
        fixed (char* title = "Edgeless")
        {
            Handle = PInvoke.CreateWindowEx(0, className, title, 0, 0, 0, 0, 0, HWND.Null, HMENU.Null,
                PInvoke.GetModuleHandle((PCWSTR)null), null);
        }

        if (Handle == 0)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastSystemError()}");
        }

        Instances[Handle] = this;
    }

    /// <summary>Raised for exceptions thrown by handlers (they must never escape into the window procedure).</summary>
    public static event Action<Exception>? HandlerFailed;

    public event Action? HotkeyPressed;

    public event Action? TaskbarCreated;

    public event Action? ShowSettingsRequested;

    public event Action? SessionEnding;

    /// <summary>Tray icon callback (NOTIFYICON_VERSION_4): event = LOWORD(lParam), anchor x/y from wParam.</summary>
    public event Action<uint, int, int>? TrayCallback;

    public nint Handle { get; }

    public static void RunMessageLoop()
    {
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            if (IsDialogNavigation(&msg))
            {
                continue;
            }

            PInvoke.TranslateMessage(in msg);
            PInvoke.DispatchMessage(in msg);
        }
    }

    /// <summary>Gives a window dialog-style keyboard navigation (Tab, Enter) in the shared message loop.</summary>
    public static void RegisterDialog(nint hwnd) => Dialogs.Add(hwnd);

    public static void UnregisterDialog(nint hwnd) => Dialogs.Remove(hwnd);

    /// <summary>For other window procedures: report an exception that must not escape into native code.</summary>
    public static void ReportHandlerFailure(Exception ex) => HandlerFailed?.Invoke(ex);

    private static bool IsDialogNavigation(MSG* msg)
    {
        foreach (var dialog in Dialogs)
        {
            if (PInvoke.IsDialogMessage(new HWND(dialog), msg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ends <see cref="RunMessageLoop"/>; call on the UI thread.</summary>
    public static void PostQuit() => PInvoke.PostQuitMessage(0);

    /// <summary>Runs <paramref name="action"/> on the UI thread. Safe to call from any thread.</summary>
    public void Post(Action action)
    {
        _posted.Enqueue(action);
        PInvoke.PostMessage(new HWND(Handle), WM_POST, 0, 0);
    }

    public void Schedule(string key, TimeSpan delay, Action callback)
    {
        Cancel(key);
        var id = _nextTimerId++;
        _timers[id] = (key, callback);
        _timerIds[key] = id;
        PInvoke.SetTimer(new HWND(Handle), id, (uint)Math.Max(1, delay.TotalMilliseconds), null);
    }

    public void Cancel(string key)
    {
        if (_timerIds.Remove(key, out var id))
        {
            _timers.Remove(id);
            PInvoke.KillTimer(new HWND(Handle), id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _timers.Keys)
        {
            PInvoke.KillTimer(new HWND(Handle), id);
        }

        _timers.Clear();
        _timerIds.Clear();
        Instances.TryRemove(Handle, out _);
        PInvoke.DestroyWindow(new HWND(Handle));
    }

    private static void RegisterClassOnce()
    {
        lock (ClassLock)
        {
            if (_classRegistered)
            {
                return;
            }

            fixed (char* className = ClassName)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &WindowProc,
                    hInstance = PInvoke.GetModuleHandle((PCWSTR)null),
                    lpszClassName = className,
                };
                if (PInvoke.RegisterClassEx(in wc) == 0)
                {
                    throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastSystemError()}");
                }
            }

            _classRegistered = true;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (Instances.TryGetValue(hwnd, out var window))
        {
            try
            {
                if (window.HandleMessage(msg, wParam, lParam, out var result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                HandlerFailed?.Invoke(ex);
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private bool HandleMessage(uint msg, WPARAM wParam, LPARAM lParam, out LRESULT result)
    {
        result = new LRESULT(0);
        switch (msg)
        {
            case WM_POST:
                while (_posted.TryDequeue(out var action))
                {
                    Run(action);
                }

                return true;
            case WM_TIMER:
                OnTimer(wParam.Value);
                return true;
            case WM_HOTKEY:
                HotkeyPressed?.Invoke();
                return true;
            case WM_TRAY:
                TrayCallback?.Invoke(
                    (uint)(lParam.Value & 0xFFFF), (short)(wParam.Value & 0xFFFF), (short)((wParam.Value >> 16) & 0xFFFF));
                return true;
            case WM_QUERYENDSESSION:
                SessionEnding?.Invoke();
                result = new LRESULT(1);
                return true;
            case WM_ENDSESSION when wParam.Value != 0:
                SessionEnding?.Invoke();
                return true;
        }

        if (msg == _taskbarCreated && msg != 0)
        {
            TaskbarCreated?.Invoke();
            return true;
        }

        if (msg == _showSettings && msg != 0)
        {
            ShowSettingsRequested?.Invoke();
            return true;
        }

        return false;
    }

    private void OnTimer(nuint id)
    {
        PInvoke.KillTimer(new HWND(Handle), id);
        if (_timers.Remove(id, out var timer))
        {
            _timerIds.Remove(timer.Key);
            Run(timer.Callback);
        }
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            HandlerFailed?.Invoke(ex);
        }
    }
}
