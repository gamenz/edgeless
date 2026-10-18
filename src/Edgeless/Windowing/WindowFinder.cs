using Edgeless.Core.Config;
using Edgeless.Core.Rules;
using Edgeless.Interop;

namespace Edgeless.Windowing;

internal sealed record WindowInfo(nint Hwnd, uint Pid, string ExeName, string ClassName);

/// <summary>Identifies top-level windows and picks each game's main window (spec section 4.1).</summary>
internal sealed class WindowFinder(ProcessNameCache cache, uint ownPid)
{
    public bool TryDescribe(nint hwnd, out WindowInfo info)
    {
        info = null!;
        var pid = ForeignWindow.GetProcessId(hwnd);
        if (pid == 0)
        {
            return false;
        }

        var exe = pid == ownPid ? "" : cache.GetExeName(pid);
        if (exe is null)
        {
            return false;
        }

        info = new WindowInfo(hwnd, pid, exe, ForeignWindow.GetClassName(hwnd));
        return true;
    }

    public bool IsExcluded(WindowInfo info) => ExclusionRules.IsExcluded(info.ExeName, info.ClassName, info.Pid == ownPid);

    public nint FindMainWindow(uint pid)
        => WindowSelector.SelectLargest(ForeignWindow.EnumTopLevel()
            .Where(hwnd => ForeignWindow.GetProcessId(hwnd) == pid)
            .Select(ForeignWindow.Describe));

    /// <summary>The main window of every running process that matches an enabled profile (one per PID).</summary>
    public IReadOnlyList<WindowInfo> FindProfileWindows(IEnumerable<GameProfile> enabledProfiles)
    {
        var exes = enabledProfiles.Select(p => ExeNameMatcher.Normalize(p.Exe)).ToHashSet(StringComparer.Ordinal);
        var byPid = new Dictionary<uint, List<nint>>();
        foreach (var hwnd in ForeignWindow.EnumTopLevel())
        {
            var pid = ForeignWindow.GetProcessId(hwnd);
            if (pid == 0 || pid == ownPid || !ForeignWindow.IsVisible(hwnd))
            {
                continue;
            }

            var exe = cache.GetExeName(pid);
            if (exe is not null && exes.Contains(exe))
            {
                (byPid.TryGetValue(pid, out var list) ? list : byPid[pid] = []).Add(hwnd);
            }
        }

        var result = new List<WindowInfo>();
        foreach (var (pid, windows) in byPid)
        {
            var main = WindowSelector.SelectLargest(windows.Select(ForeignWindow.Describe));
            if (main != 0 && TryDescribe(main, out var info))
            {
                result.Add(info);
            }
        }

        return result;
    }

    public void ForgetProcess(uint pid) => cache.Forget(pid);
}
