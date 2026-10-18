namespace Edgeless.Windowing;

/// <summary>Managed windows plus the windows the user switched off with the hotkey. UI thread only.</summary>
internal sealed class WindowRegistry
{
    private readonly Dictionary<nint, ManagedWindow> _windows = [];
    private readonly HashSet<(nint Hwnd, uint Pid)> _userDisabled = [];

    /// <summary>Raised when a window starts or stops being managed.</summary>
    public event Action? Changed;

    public IReadOnlyCollection<ManagedWindow> All => _windows.Values;

    public bool TryGet(nint hwnd, out ManagedWindow window) => _windows.TryGetValue(hwnd, out window!);

    public void Add(ManagedWindow window)
    {
        _windows[window.Hwnd] = window;
        Changed?.Invoke();
    }

    public bool Remove(nint hwnd)
    {
        var removed = _windows.Remove(hwnd);
        if (removed)
        {
            Changed?.Invoke();
        }

        return removed;
    }

    public bool HasPid(uint pid) => _windows.Values.Any(w => w.Pid == pid);

    public void MarkUserDisabled(nint hwnd, uint pid) => _userDisabled.Add((hwnd, pid));

    public bool IsUserDisabled(nint hwnd, uint pid) => _userDisabled.Contains((hwnd, pid));

    public void ClearUserDisabled(nint hwnd) => _userDisabled.RemoveWhere(e => e.Hwnd == hwnd);

    /// <summary>Drops every trace of a process that exited.</summary>
    public void ForgetPid(uint pid)
    {
        foreach (var hwnd in _windows.Values.Where(w => w.Pid == pid).Select(w => w.Hwnd).ToList())
        {
            Remove(hwnd);
        }

        _userDisabled.RemoveWhere(e => e.Pid == pid);
    }
}
