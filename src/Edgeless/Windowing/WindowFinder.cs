using Edgeless.Core.Config;
using Edgeless.Core.Rules;
using Edgeless.Interop;

namespace Edgeless.Windowing;

/// <param name="ImagePath">Normalized full path of the owning process's exe ("" for Edgeless itself).</param>
internal sealed record WindowInfo(nint Hwnd, uint Pid, string ImagePath, string ClassName)
{
    public string ExeName => ExePath.FileName(ImagePath);
}

/// <summary>Identifies top-level windows and picks each game's main window (spec section 4.1).</summary>
internal sealed class WindowFinder(ProcessPathCache cache, uint ownPid)
{
    public bool TryDescribe(nint hwnd, out WindowInfo info)
    {
        info = null!;
        var pid = ForeignWindow.GetProcessId(hwnd);
        if (pid == 0)
        {
            return false;
        }

        var path = pid == ownPid ? "" : cache.GetImagePath(pid);
        if (path is null)
        {
            return false;
        }

        info = new WindowInfo(hwnd, pid, path, ForeignWindow.GetClassName(hwnd));
        return true;
    }

    public bool IsExcluded(WindowInfo info) => ExclusionRules.IsExcluded(info.ExeName, info.ClassName, info.Pid == ownPid);

    public nint FindMainWindow(uint pid)
        => WindowSelector.SelectLargest(ForeignWindow.EnumTopLevel()
            .Where(hwnd => ForeignWindow.GetProcessId(hwnd) == pid)
            .Select(ForeignWindow.Describe));

    /// <summary>The main window of every running process whose exe path matches an enabled profile (one per PID).</summary>
    public IReadOnlyList<WindowInfo> FindProfileWindows(IEnumerable<GameProfile> enabledProfiles)
    {
        var paths = enabledProfiles
            .Select(p => ExePath.Normalize(p.Path))
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byPid = new Dictionary<uint, List<nint>>();
        foreach (var hwnd in ForeignWindow.EnumTopLevel())
        {
            var pid = ForeignWindow.GetProcessId(hwnd);
            if (pid == 0 || pid == ownPid || !ForeignWindow.IsVisible(hwnd))
            {
                continue;
            }

            var path = cache.GetImagePath(pid);
            if (path is not null && paths.Contains(path))
            {
                (byPid.TryGetValue(pid, out var list) ? list : byPid[pid] = []).Add(hwnd);
            }
        }

        return SelectMainWindows(byPid.Values);
    }

    /// <summary>
    /// The main window of every running exe (one per path) with a titled, visible, non-excluded window.
    /// Feeds the tray "Add to list" submenu.
    /// </summary>
    public IReadOnlyList<WindowInfo> FindAddableWindows()
    {
        var byPath = new Dictionary<string, List<nint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var hwnd in ForeignWindow.EnumTopLevel())
        {
            if (!ForeignWindow.IsVisible(hwnd) || ForeignWindow.GetTitle(hwnd).Length == 0
                || !TryDescribe(hwnd, out var info) || info.ImagePath.Length == 0 || IsExcluded(info))
            {
                continue;
            }

            (byPath.TryGetValue(info.ImagePath, out var list) ? list : byPath[info.ImagePath] = []).Add(hwnd);
        }

        return SelectMainWindows(byPath.Values);
    }

    public void ForgetProcess(uint pid) => cache.Forget(pid);

    private List<WindowInfo> SelectMainWindows(IEnumerable<List<nint>> groups)
    {
        var result = new List<WindowInfo>();
        foreach (var windows in groups)
        {
            var main = WindowSelector.SelectLargest(windows.Select(ForeignWindow.Describe));
            if (main != 0 && TryDescribe(main, out var info))
            {
                result.Add(info);
            }
        }

        return result;
    }
}
